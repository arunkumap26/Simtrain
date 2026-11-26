using System.Text.Json.Nodes;
using RimForce.Core.App;
using RimForce.Core.Devices;
using RimForce.Core.Drills;
using RimForce.Core.Engine;
using RimForce.Core.Safety;
using RimForce.Core.Sessions;

namespace RimForce.Tests;

public class MaxTestTests
{
    [Fact]
    public void CapOverrideReplacesGlobalCapButNotTestMode()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        Assert.Equal(4.8, g.GlobalCapNm, 3);
        g.CapOverridePct = 100;
        Assert.Equal(12, g.GlobalCapNm, 3);
        var o = Rig.Run(g, clock, 3, new TorqueRequest(11, 0, 100, true));
        Assert.Equal(11, o.TorqueNm, 3);
        g.UserStop(clock.Now);
        Assert.True(g.TryArmTestMode(clock.Now, 0, 0, true, out _));
        Assert.Equal(1.2, g.GlobalCapNm, 3); // device tests stay at 10%
        g.CapOverridePct = null;
    }

    [Fact]
    public void PushThroughLadderFindsDriverStrength()
    {
        var (e, sim, c) = Rig.SimEngine(new SafetySettings { GlobalCapPct = 100 });
        sim.Driver.StrengthNm = 6;
        sim.Driver.Mode = DriverMode.FollowGuide;
        var cfg = DrillCatalog.DefaultConfig("max-push") with { Direction = PushDir.Right, Sets = 1, RestS = 2 };
        var runner = new SessionRunner(SessionPlan.Single(cfg), e.Profile, new ReferenceStrength(), 3);
        e.SetActivity(runner);
        Rig.Step(e, c, 1);
        Assert.True(e.Arm(out var why), why);
        runner.Start();
        double t0 = c.Now;
        while (!runner.IsFinished && runner.Phase != SessionPhase.Paused && c.Now - t0 < 300) Rig.Step(e, c, 0.05);
        Assert.True(runner.IsFinished, $"{runner.Phase} {e.Governor.LastTripMessage}");
        var r = Assert.Single(runner.Results);
        Assert.Equal("max-push", r.DrillId);
        Assert.InRange(r.Score, 3.5, 6.1);
        Assert.True(r.Metrics["rungs"] >= 1);
    }

    [Fact]
    public void PushThroughAtTheCeilingIsMarkedCapped()
    {
        var (e, sim, c) = Rig.SimEngine(new SafetySettings { GlobalCapPct = 100 });
        sim.Driver.StrengthNm = 30; // stronger than the 12 Nm motor
        var cfg = DrillCatalog.DefaultConfig("max-push") with { Direction = PushDir.Left, Sets = 1, RestS = 2 };
        cfg.Params["startPct"] = 60;
        cfg.Params["stepPct"] = 20;
        var runner = new SessionRunner(SessionPlan.Single(cfg), e.Profile, new ReferenceStrength(), 3);
        e.SetActivity(runner);
        Rig.Step(e, c, 1);
        Assert.True(e.Arm(out _));
        runner.Start();
        double t0 = c.Now;
        while (!runner.IsFinished && runner.Phase != SessionPhase.Paused && c.Now - t0 < 300) Rig.Step(e, c, 0.05);
        Assert.True(runner.IsFinished, $"{runner.Phase} {e.Governor.LastTripMessage}");
        var r = Assert.Single(runner.Results);
        Assert.Equal(12, r.Score, 1);
        Assert.Equal(1, r.Metrics["capped"]);
    }
}

public class SpeedStrengthTests
{
    private static SetResult Run(double trueMaxNm, Hand hand = Hand.Both, PushDir dir = PushDir.Right)
    {
        var (e, sim, c) = Rig.SimEngine(new SafetySettings { GlobalCapPct = 100 });
        sim.Driver.StrengthNm = trueMaxNm;
        sim.Driver.MaxRefSpeedDps = 4000;
        sim.Driver.FatigueRate = 0;
        var cfg = DrillCatalog.DefaultConfig("speed-profile") with { Direction = dir, Hand = hand };
        var runner = new SessionRunner(SessionPlan.Single(cfg), e.Profile, new ReferenceStrength(), 5);
        e.SetActivity(runner);
        Rig.Step(e, c, 1);
        Assert.True(e.Arm(out var why), why);
        runner.Start();
        double t0 = c.Now;
        while (!runner.IsFinished && runner.Phase != SessionPhase.Paused && c.Now - t0 < 400) Rig.Step(e, c, 0.05);
        Assert.True(runner.IsFinished, $"{runner.Phase} {e.Governor.LastTripMessage}");
        return Assert.Single(runner.Results);
    }

    [Theory]
    [InlineData(8.0)]
    [InlineData(14.0)]
    public void EstimatesTrueMaxWithin12Percent(double trueMax)
    {
        var r = Run(trueMax);
        Assert.Equal(1, r.Metrics["valid"]);
        Assert.InRange(r.Score, trueMax * 0.88, trueMax * 1.12);
        Assert.Equal(3, r.Metrics["confidence"]);
    }

    [Fact]
    public void EstimatesBeyondTheMotorLimitWithMediumConfidence()
    {
        var r = Run(20, Hand.Left, PushDir.Right);
        Assert.True(r.Score > 12, $"estimate {r.Score}"); // more than the 12 Nm motor could ever measure directly
        Assert.InRange(r.Score, 16, 28);
        Assert.True(r.Metrics["confidence"] >= 2);
    }

    [Fact]
    public void FlagsUnreliableExtrapolation()
    {
        var r = Run(30);
        Assert.Equal(1, r.Metrics["confidence"]);
    }
}

public class MaxTestCommandTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), "rimforce-max-" + Guid.NewGuid().ToString("N") + ".db");
    private readonly ManualClock _clock = new();
    private readonly AppController _app;

    public MaxTestCommandTests() => _app = new AppController(new AppOptions { DbPath = _db, Demo = true, Clock = _clock });

    public void Dispose()
    {
        _app.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_db); } catch { }
    }

    private JsonNode Cmd(string cmd, object? args = null) =>
        JsonNode.Parse(_app.HandleMessage(System.Text.Json.JsonSerializer.Serialize(new { id = 1, cmd, args = args ?? new { } }, AppController.Json)))!;

    [Fact]
    public void FullPowerNeedsConfirmationAndOnlyLastsForTheTest()
    {
        Assert.False(Cmd("startMaxTest", new { fullPower = true })["ok"]!.GetValue<bool>());
        Assert.Equal(4.8, _app.Engine.Governor.GlobalCapNm, 3);

        var r = Cmd("startMaxTest", new { fullPower = true, confirmed = true, battery = new[] { "profile-right-lift", "push-both-left", "bogus" } });
        Assert.True(r["ok"]!.GetValue<bool>(), r["error"]?.ToString());
        Assert.Equal(12, _app.Engine.Governor.GlobalCapNm, 3);
        Assert.Equal(3, _app.Runner!.Plan.Items.Count); // warm-up + 2 valid tests
        Assert.Equal("maxtest", _app.Runner.Plan.ProgramRef);

        Cmd("endSession");
        Assert.Equal(4.8, _app.Engine.Governor.GlobalCapNm, 3);
    }

    [Fact]
    public void LoadingAnotherDrillRestoresTheCap()
    {
        Cmd("startMaxTest", new { fullPower = true, confirmed = true });
        Assert.Equal(12, _app.Engine.Governor.GlobalCapNm, 3);
        var cfg = new { drillId = "corner-hold", loadPct = 40, sets = 1, workS = 10, restS = 0, hand = "Both", direction = "Right", @params = new { angle = 0 } };
        Cmd("startDrill", new { config = cfg });
        Assert.Equal(4.8, _app.Engine.Governor.GlobalCapNm, 3);
    }
}
