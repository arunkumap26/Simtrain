using RimForce.Core.Devices;
using RimForce.Core.Engine;
using RimForce.Core.Safety;

namespace RimForce.Tests;

public class SimulatorIntegrationTests
{
    [Fact]
    public void DriverHoldsAgainstConstantForce()
    {
        var (e, sim, c) = Rig.SimEngine(new SafetySettings { GlobalCapPct = 50 });
        sim.Driver.Mode = DriverMode.FollowGuide;
        e.SetActivity(new FixedActivity { Request = new TorqueRequest(-4, 0, 100, true), Guide = 0 });
        Rig.Step(e, c, 0.1);
        Assert.True(e.Arm(out var why), why);
        Rig.Step(e, c, 4);
        Assert.Equal(SafetyState.Armed, e.Governor.State);
        Assert.InRange(sim.TrueAngleDeg, -3, 3);
        Assert.InRange(e.Latest!.MotorTorqueNm, -4.01, -3.9);
        // user torque estimate should roughly mirror the motor when the wheel is still
        Assert.InRange(e.Latest.UserTorqueNm, 3.0, 5.0);
    }

    [Fact]
    public void VirtualDriverLetsGo_HandsOffTripsWithin100ms()
    {
        var (e, sim, c) = Rig.SimEngine(new SafetySettings { GlobalCapPct = 40 });
        sim.Driver.Mode = DriverMode.FollowGuide;
        e.SetActivity(new FixedActivity { Request = new TorqueRequest(4.8, 0, 100, true), Guide = 0 });
        Rig.Step(e, c, 0.1);
        Assert.True(e.Arm(out var why), why);
        Rig.Step(e, c, 3);
        Assert.Equal(SafetyState.Armed, e.Governor.State);

        double releaseAt = c.Now;
        sim.Driver.ReleaseAtTime = releaseAt;
        double? trippedAt = null;
        for (int i = 0; i < 500 && trippedAt == null; i++)
        {
            Rig.Step(e, c, 0.002);
            if (e.Governor.State == SafetyState.Tripped) trippedAt = c.Now;
        }
        Assert.NotNull(trippedAt);
        Assert.True(trippedAt - releaseAt <= 0.100, $"tripped after {(trippedAt - releaseAt) * 1000:0} ms");
        Assert.Contains("Hands off", e.Governor.LastTripMessage);
        Assert.Equal(0, sim.LastCommand.ConstantPct);
        // and the wheel did not get anywhere near the lock
        Assert.InRange(Math.Abs(sim.TrueAngleDeg), 0, 60);
    }

    [Fact]
    public void LetGoAtFullCap_HandsOffTripsWithin100ms()
    {
        var (e, sim, c) = Rig.SimEngine(new SafetySettings { GlobalCapPct = 100 });
        e.SetActivity(new FixedActivity { Request = new TorqueRequest(-12, 0, 100, true), Guide = 0 });
        sim.Driver.StrengthNm = 20;
        Rig.Step(e, c, 0.1);
        Assert.True(e.Arm(out _));
        Rig.Step(e, c, 3);
        double releaseAt = c.Now;
        sim.Driver.ReleaseAtTime = releaseAt;
        while (e.Governor.State == SafetyState.Armed && c.Now - releaseAt < 1) Rig.Step(e, c, 0.002);
        Assert.True(c.Now - releaseAt <= 0.1, $"{(c.Now - releaseAt) * 1000:0} ms");
    }

    [Fact]
    public void ReversedForceDirectionWithSoftwareSpringIsCaught()
    {
        // If the base pushes the opposite way to what we command, a software spring turns into an
        // anti-spring and would run the wheel to the lock. The runaway/angle rules must stop it.
        var (e, sim, c) = Rig.SimEngine(new SafetySettings { GlobalCapPct = 40 });
        sim.HardwareInvertsForce = true;
        sim.Driver.Mode = DriverMode.HandsOff;
        var spring = new SpringActivity(0.1);
        e.SetActivity(spring);
        sim.SetState(5);
        Rig.Step(e, c, 0.05);
        Assert.True(e.Arm(out var why), why);
        Rig.Step(e, c, 5);
        Assert.Equal(SafetyState.Tripped, e.Governor.State);
        Assert.True(Math.Abs(sim.TrueAngleDeg) < 450 - 20, $"angle {sim.TrueAngleDeg}");
    }

    [Fact]
    public void NoActivityMeansNoForce()
    {
        var (e, sim, c) = Rig.SimEngine();
        Rig.Step(e, c, 0.05);
        Assert.True(e.Arm(out _));
        Rig.Step(e, c, 2);
        Assert.Equal(0, sim.LastCommand.ConstantPct);
    }

    [Fact]
    public void DisconnectDuringForceStopsEverything()
    {
        var (e, sim, c) = Rig.SimEngine();
        e.SetActivity(new FixedActivity { Request = new TorqueRequest(3, 0, 100, true) });
        Rig.Step(e, c, 0.05);
        Assert.True(e.Arm(out _));
        Rig.Step(e, c, 2);
        sim.SimulateDisconnect = true;
        Rig.Step(e, c, 0.01);
        Assert.Equal(TripCode.Disconnected, e.Governor.LastTrip);
        Assert.Equal(0, sim.LastCommand.ConstantPct);
    }

    [Fact]
    public void LoopRateIsMeasured()
    {
        var (e, _, c) = Rig.SimEngine();
        Rig.Step(e, c, 2.2, 500);
        Assert.InRange(e.Latest!.LoopHz, 490, 510);
    }

    [Fact]
    public void IntensityIsClampedAndReported()
    {
        var (e, _, c) = Rig.SimEngine();
        e.SetIntensity(5);
        Assert.Equal(1.5, e.Intensity);
        e.SetIntensity(0);
        Assert.Equal(0.1, e.Intensity);
    }

    private sealed class SpringActivity : IActivity
    {
        private readonly double _k;
        public SpringActivity(double kNmPerDeg) => _k = kNmPerDeg;
        public string Kind => "spring";
        public bool IsFinished => false;
        public ActivityStep Step(in ActivityTick t) => new(new TorqueRequest(0, -_k * t.AngleDeg, 100, true), 0);
        public void OnSafetyStop(string reason) { }
        public object Snapshot() => new { };
        public IReadOnlyList<ActivityEvent> DrainEvents() => Array.Empty<ActivityEvent>();
    }
}
