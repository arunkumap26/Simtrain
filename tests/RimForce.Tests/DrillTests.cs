using RimForce.Core.Devices;
using RimForce.Core.Drills;
using RimForce.Core.Drills.Logics;
using RimForce.Core.Engine;
using RimForce.Core.Metrics;
using RimForce.Core.Safety;
using RimForce.Core.Sessions;

namespace RimForce.Tests;

public class DrillTests
{
    private static (SessionRunner runner, TrainingEngine e, SimulatedForceDevice sim, ManualClock c) RunDrill(
        DrillConfig cfg, double driverStrength = 9, double capPct = 60, ReferenceStrength? refs = null, double maxSeconds = 120)
    {
        var (e, sim, c) = Rig.SimEngine(new SafetySettings { GlobalCapPct = capPct });
        sim.Driver.Mode = DriverMode.FollowGuide;
        sim.Driver.StrengthNm = driverStrength;
        var runner = new SessionRunner(SessionPlan.Single(cfg), e.Profile, refs ?? new ReferenceStrength(), seed: 42);
        e.SetActivity(runner);
        Rig.Step(e, c, 3); // driver moves to the start position and settles
        Assert.True(e.Arm(out var why), why);
        runner.Start();
        double t0 = c.Now;
        while (!runner.IsFinished && runner.Phase != SessionPhase.Paused && c.Now - t0 < maxSeconds)
            Rig.Step(e, c, 0.05);
        return (runner, e, sim, c);
    }

    private static DrillConfig Short(string id, double work = 12, int sets = 1, double load = 40, Dictionary<string, double>? p = null)
    {
        var cfg = DrillCatalog.DefaultConfig(id) with { Sets = sets, WorkS = work, RestS = 2, LoadPct = load };
        if (p != null) foreach (var kv in p) cfg.Params[kv.Key] = kv.Value;
        return cfg;
    }

    [Theory]
    [InlineData("corner-hold", 60)]
    [InlineData("one-hand-hold", 50)]
    [InlineData("grip-variants", 50)]
    [InlineData("kerb-hold", 50)]
    [InlineData("wave-hold", 50)]
    [InlineData("slow-return", 30)]
    [InlineData("iso-speed", 30)]
    [InlineData("snap-catch", 30)]
    public void VirtualDriverCompletesDrillWithoutTrips(string id, double minScore)
    {
        var (runner, e, _, _) = RunDrill(Short(id));
        Assert.True(runner.IsFinished, $"phase {runner.Phase}, trip {e.Governor.LastTripMessage}");
        Assert.NotEqual(SafetyState.Tripped, e.Governor.State);
        var r = Assert.Single(runner.Results);
        Assert.True(r.Completed);
        Assert.True(r.Score >= minScore, $"{id} score {r.Score}");
        Assert.True(r.Metrics["impulseNms"] > 0);
        Assert.NotEmpty(r.Trace);
    }

    [Fact]
    public void LockToLockCountsReps()
    {
        var (runner, e, _, _) = RunDrill(Short("lock-to-lock", work: 15, load: 30));
        Assert.True(runner.IsFinished, e.Governor.LastTripMessage);
        var r = Assert.Single(runner.Results);
        Assert.True(r.Score >= 3, $"reps {r.Score}");
        Assert.Equal("reps", r.ScoreLabel);
        Assert.True(r.Metrics["peakSpeedDps"] > 50);
    }

    [Fact]
    public void ApexHunterHitsTargets()
    {
        var (runner, e, _, _) = RunDrill(Short("apex-hunter", work: 20, load: 30));
        Assert.True(runner.IsFinished, e.Governor.LastTripMessage);
        var r = Assert.Single(runner.Results);
        Assert.True(r.Metrics["hits"] >= 3, $"hits {r.Metrics["hits"]}");
    }

    [Fact]
    public void StintRunsLaps()
    {
        var cfg = Short("stint", p: new() { ["minutes"] = 1.5, ["lap"] = 1 });
        var (runner, e, _, _) = RunDrill(cfg, maxSeconds: 200);
        Assert.True(runner.IsFinished, e.Governor.LastTripMessage);
        var r = Assert.Single(runner.Results);
        Assert.True(r.Metrics["laps"] >= 1);
        Assert.True(r.Score > 30, $"stint score {r.Score}");
    }

    [Fact]
    public void FreeRideAllProfilesAreStable()
    {
        for (int prof = 0; prof < FreeRideLogic.ProfileNames.Length; prof++)
        {
            var cfg = Short("free-ride", p: new() { ["profile"] = prof, ["minutes"] = 0.2 });
            var (runner, e, _, _) = RunDrill(cfg);
            Assert.True(runner.IsFinished, $"profile {prof}: {e.Governor.LastTripMessage}");
        }
    }

    [Fact]
    public void BreakpointFindsDriverStrength()
    {
        // Weak driver (3.5 Nm) under a 60% cap (7.2 Nm): the ramp must find roughly 3.5 Nm.
        var cfg = Short("breakpoint", work: 120, load: 100, p: new() { ["rampPctPerS"] = 3, ["startPct"] = 10 });
        var refs = new ReferenceStrength();
        var (runner, e, sim, _) = RunDrill(cfg, driverStrength: 3.5, refs: refs);
        Assert.True(runner.IsFinished, e.Governor.LastTripMessage);
        var r = Assert.Single(runner.Results);
        Assert.Equal(0, r.Metrics["estimated"]);
        Assert.InRange(r.Score, 2.6, 4.2);
        Assert.NotNull(refs.Get(Hand.Both, 1));
    }

    [Fact]
    public void BreakpointAtTheCapEstimatesFromEndurance()
    {
        // Strong driver, low cap: the ramp hits the cap, then hold-to-failure estimates the max.
        var cfg = Short("breakpoint", work: 60, load: 100, p: new() { ["rampPctPerS"] = 3, ["startPct"] = 20, ["maxEnduranceS"] = 20 });
        var (runner, e, _, _) = RunDrill(cfg, driverStrength: 12, capPct: 30);
        Assert.True(runner.IsFinished, e.Governor.LastTripMessage);
        var r = Assert.Single(runner.Results);
        Assert.Equal(1, r.Metrics["estimated"]);
        Assert.True(r.Score > 3.6, $"estimated {r.Score}"); // > cap (3.6 Nm)
    }

    [Fact]
    public void MultiSetSessionRestsBetweenSetsAndAlternatesDirection()
    {
        var cfg = Short("corner-hold", work: 6, sets: 2) with { Direction = PushDir.Alternate };
        var (runner, _, _, _) = RunDrill(cfg);
        Assert.True(runner.IsFinished);
        Assert.Equal(2, runner.Results.Count);
        Assert.Equal(PushDir.Right, runner.Results[0].Direction);
        Assert.Equal(PushDir.Left, runner.Results[1].Direction);
    }

    [Fact]
    public void HandsOffDuringSetPausesSessionAndRecordsIncompleteSet()
    {
        var (e, sim, c) = Rig.SimEngine(new SafetySettings { GlobalCapPct = 50 });
        var runner = new SessionRunner(SessionPlan.Single(Short("corner-hold", work: 20, load: 60)), e.Profile, new ReferenceStrength(), 1);
        e.SetActivity(runner);
        Rig.Step(e, c, 3);
        Assert.True(e.Arm(out _));
        runner.Start();
        Rig.Step(e, c, 8); // countdown + into the set
        Assert.Equal(SessionPhase.Work, runner.Phase);
        sim.Driver.ReleaseAtTime = c.Now;
        Rig.Step(e, c, 0.5);
        Assert.Equal(SafetyState.Tripped, e.Governor.State);
        Assert.Equal(SessionPhase.Paused, runner.Phase);
        var r = Assert.Single(runner.Results);
        Assert.False(r.Completed);
    }

    [Fact]
    public void LoadsScaleWithBreakpointAndIntensity()
    {
        var refs = new ReferenceStrength();
        refs.Record(Hand.Both, 1, 8);
        var (e, _, c) = Rig.SimEngine(new SafetySettings { GlobalCapPct = 100 });
        var runner = new SessionRunner(SessionPlan.Single(Short("corner-hold", load: 50) with { Direction = PushDir.Right }), e.Profile, refs, 1);
        e.SetActivity(runner);
        e.SetIntensity(1.2);
        Rig.Step(e, c, 0.1);
        dynamic snap = runner.Snapshot();
        Assert.Equal(4.8, (double)snap.loadNm, 2); // 50% of 8 Nm x 1.2
    }

    [Fact]
    public void GripVariantsReduceLoad()
    {
        Assert.True(DrillCatalog.GripLoadFactor(GripStyle.Pinch) < DrillCatalog.GripLoadFactor(GripStyle.Fingertips));
        Assert.Equal(1.0, DrillCatalog.GripLoadFactor(GripStyle.Full));
    }

    [Fact]
    public void EveryDrillHasTargetsAndLogic()
    {
        foreach (var d in DrillCatalog.All)
        {
            Assert.NotEmpty(d.HowTo);
            Assert.True(d.Targets.Values.Any(v => v > 0));
            Assert.NotNull(DrillCatalog.CreateLogic(d.Id));
        }
        Assert.True(DrillCatalog.All.Count >= 9);
    }

    [Fact]
    public void ScoreTrackerAccuracy()
    {
        var s = new ScoreTracker();
        for (int i = 0; i < 1000; i++) s.Add(i * 0.01, 0.01, i < 800 ? 1 : 6, 0, 3, 4, 0);
        Assert.InRange(s.InZoneFraction, 0.79, 0.81);
        Assert.InRange(s.MeanAbsErr, 1.9, 2.1);
        Assert.InRange(s.Impulse, 39.9, 40.1);
        Assert.InRange(s.Tut, 9.9, 10.1);
    }

    [Fact]
    public void RohmertCurveAndInverse()
    {
        Assert.InRange(StrengthMath.EnduranceSeconds(0.5), 60, 70);
        Assert.InRange(StrengthMath.EnduranceSeconds(1.0), 5, 7);
        Assert.InRange(StrengthMath.EnduranceSeconds(0.33), 110, 140);
        Assert.InRange(StrengthMath.FractionForEndurance(66), 0.48, 0.52);
    }

    [Fact]
    public void BodyLoadAt12NmMatchesResearchTable()
    {
        var p = Rig.Profile;
        var s = StrengthMath.Compute(p, 12, 0, 2, null, 0, StrengthNorms.Men, 80);
        Assert.InRange(s.RimForceN, 79, 81);
        Assert.InRange(s.ShoulderPct, 30, 36);   // ~33%
        Assert.InRange(s.WristPct, 27, 31);      // ~29%
        Assert.InRange(s.ForearmTwistPct, 72, 78); // ~75%
        var w = StrengthMath.Compute(p, 12, 0, 2, null, 0, StrengthNorms.Women, 65, wristTechnique: true);
        Assert.True(w.ForearmTwistPct > 100);
        Assert.Equal("forearm twist", w.LimitingLink);
    }
}
