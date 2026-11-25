using RimForce.Core.Engine;
using RimForce.Core.Safety;

namespace RimForce.Tests;

public class SafetyGovernorTests
{
    private static TorqueRequest Want(double nm, double cap = 100) => new(nm, 0, cap, true);

    // ---- Rule 1: nothing applies force until armed ----

    [Fact]
    public void NoForceBeforeArm()
    {
        var clock = new ManualClock();
        var g = new SafetyGovernor(new SafetySettings(), Rig.Profile) { ChecklistAccepted = true, DirectionVerified = true };
        var o = Rig.Run(g, clock, 3, Want(5));
        Assert.Equal(0, o.TorqueNm);
        Assert.Equal(SafetyState.Disarmed, g.State);
    }

    [Fact]
    public void ArmRefusedWithoutChecklist()
    {
        var g = new SafetyGovernor(new SafetySettings(), Rig.Profile) { DirectionVerified = true };
        Assert.False(g.TryArm(0, 0, 0, true, out var why));
        Assert.Contains("checklist", why, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArmRefusedWithoutDirectionCheck()
    {
        var g = new SafetyGovernor(new SafetySettings(), Rig.Profile) { ChecklistAccepted = true };
        Assert.False(g.TryArm(0, 0, 0, true, out var why));
        Assert.Contains("direction", why, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ArmRefusedWhileWheelMovingOrNearLock()
    {
        var g = new SafetyGovernor(new SafetySettings(), Rig.Profile) { ChecklistAccepted = true, DirectionVerified = true };
        Assert.False(g.TryArm(0, 0, 200, true, out _));
        Assert.False(g.TryArm(0, 440, 0, true, out _));
        Assert.False(g.TryArm(0, 0, 0, false, out _));
        Assert.True(g.TryArm(0, 10, 0, true, out _));
    }

    [Fact]
    public void UserStopIsImmediate()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        var o = Rig.Run(g, clock, 2, Want(3));
        Assert.True(o.TorqueNm > 2.9);
        g.UserStop(clock.Now);
        clock.Advance(0.002);
        o = g.Process(clock.Now, Rig.Still(), Want(3));
        Assert.Equal(0, o.TorqueNm);
        Assert.Equal(SafetyState.Disarmed, g.State);
    }

    [Fact]
    public void EngineStopCallsDeviceStopAllImmediately()
    {
        var (e, sim, c) = Rig.SimEngine();
        var act = new FixedActivity { Request = Want(3) };
        e.SetActivity(act);
        Rig.Step(e, c, 0.1);
        Assert.True(e.Arm(out var why), why);
        Rig.Step(e, c, 2);
        int before = sim.StopAllCount;
        e.Stop();
        Assert.True(sim.StopAllCount > before);
        Assert.Equal(0, sim.LastCommand.ConstantPct);
    }

    // ---- Rule 2: caps ----

    [Fact]
    public void GlobalCapDefaultsTo40Percent()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        var o = Rig.Run(g, clock, 3, Want(12));
        Assert.Equal(12 * 0.4, o.TorqueNm, 3);
    }

    [Fact]
    public void DrillCapWinsWhenLower()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock, new SafetySettings { GlobalCapPct = 100 });
        var o = Rig.Run(g, clock, 3, new TorqueRequest(-10, 0, 2.5, true));
        Assert.Equal(-2.5, o.TorqueNm, 3);
    }

    [Fact]
    public void ReactiveTorqueIsCappedToo()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        var o = Rig.Run(g, clock, 3, new TorqueRequest(3, 30, 100, true));
        Assert.True(o.TorqueNm <= 4.8 + 1e-9);
    }

    [Fact]
    public void CapCannotExceed100Percent()
    {
        var s = new SafetySettings { GlobalCapPct = 250 }.Sanitised();
        Assert.Equal(100, s.GlobalCapPct);
    }

    // ---- Rule 3: soft start / soft end / pulse ramps ----

    [Fact]
    public void SoftStartTakesAtLeast1_5Seconds()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock, new SafetySettings { GlobalCapPct = 100 });
        var half = Rig.Run(g, clock, 0.75, Want(4));
        Assert.InRange(half.TorqueNm, 1.8, 2.2);
        var almost = Rig.Run(g, clock, 0.6, Want(4));
        Assert.True(almost.TorqueNm < 4.0);
        var full = Rig.Run(g, clock, 0.3, Want(4));
        Assert.Equal(4, full.TorqueNm, 3);
    }

    [Fact]
    public void RampUpCannotBeConfiguredBelow1_5Seconds()
    {
        Assert.Equal(1.5, new SafetySettings { RampUpS = 0.2 }.Sanitised().RampUpS);
        Assert.Equal(0.08, new SafetySettings { MinPulseRampS = 0.01 }.Sanitised().MinPulseRampS);
    }

    [Fact]
    public void SoftEndRampsDownOver0_3SecondsThenDisarms()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock, new SafetySettings { GlobalCapPct = 100 });
        Rig.Run(g, clock, 2, Want(4));
        g.RequestDisarm(clock.Now);
        var mid = Rig.Run(g, clock, 0.15, Want(4));
        Assert.InRange(mid.TorqueNm, 1.6, 2.4);
        Assert.Equal(SafetyState.Armed, g.State);
        var end = Rig.Run(g, clock, 0.2, Want(4));
        Assert.Equal(0, end.TorqueNm);
        Assert.Equal(SafetyState.Disarmed, g.State);
    }

    [Fact]
    public void SetEndRampsDownWhenForceNoLongerWanted()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock, new SafetySettings { GlobalCapPct = 100 });
        Rig.Run(g, clock, 2, Want(4));
        var o = Rig.Run(g, clock, 0.32, new TorqueRequest(4, 0, 100, false));
        Assert.Equal(0, o.TorqueNm, 6);
        Assert.Equal(SafetyState.Armed, g.State); // still armed, just resting
    }

    [Fact]
    public void PulseStepRampsOverAtLeast80ms()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock, new SafetySettings { GlobalCapPct = 50 }); // cap 6 Nm
        Rig.Run(g, clock, 2, Want(0));
        // Step to the full cap.
        var at40 = Rig.Run(g, clock, 0.04, Want(6));
        Assert.InRange(at40.TorqueNm, 2.7, 3.3);
        var at78 = Rig.Run(g, clock, 0.038, Want(6));
        Assert.True(at78.TorqueNm < 6);
        var at90 = Rig.Run(g, clock, 0.012, Want(6));
        Assert.Equal(6, at90.TorqueNm, 3);
    }

    [Fact]
    public void PulsesNeverExceedCap()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        double max = 0;
        var rnd = new Random(1);
        for (int i = 0; i < 3000; i++)
        {
            clock.Advance(0.002);
            var o = g.Process(clock.Now, Rig.Still(), Want(rnd.NextDouble() * 40 - 20));
            max = Math.Max(max, Math.Abs(o.TorqueNm));
        }
        Assert.True(max <= 4.8 + 1e-9);
    }

    // ---- Rule 4: hands-off ----

    [Fact]
    public void HandsOffTripsAfter50msAbove400dps()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock, new SafetySettings { RunawayDetection = false, OverspeedFadeStartDps = 400 });
        Rig.Run(g, clock, 2, Want(3));
        // Wheel spinning the way the motor pushes, above the threshold, with the fade disabled.
        var o = Rig.Run(g, clock, 0.04, Want(3), _ => Rig.Still(0, 410));
        Assert.Equal(SafetyState.Armed, g.State);
        o = Rig.Run(g, clock, 0.016, Want(3), _ => Rig.Still(0, 410));
        Assert.Equal(SafetyState.Tripped, g.State);
        Assert.Equal(TripCode.HandsOff, g.LastTrip);
        Assert.Equal(0, o.TorqueNm);
    }

    [Fact]
    public void ShortSpeedSpikeDoesNotTrip()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock, new SafetySettings { RunawayDetection = false, OverspeedFadeStartDps = 400 });
        Rig.Run(g, clock, 2, Want(3));
        Rig.Run(g, clock, 0.03, Want(3), _ => Rig.Still(0, 410));
        Rig.Run(g, clock, 0.1, Want(3));
        Rig.Run(g, clock, 0.03, Want(3), _ => Rig.Still(0, 410));
        Assert.Equal(SafetyState.Armed, g.State);
    }

    [Fact]
    public void SprintDrillMayRaiseHandsOffSpeedOnlyWhileBraking()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        var sprint = new TorqueRequest(3, 0, 100, true, HandsOffSpeedWhenBrakingDps: 720);
        Rig.Run(g, clock, 2, sprint);
        Rig.Run(g, clock, 0.3, sprint, _ => Rig.Still(0, -600)); // motor +3 Nm, wheel -600 deg/s: braking
        Assert.Equal(SafetyState.Armed, g.State);
        Rig.Run(g, clock, 0.06, sprint, _ => Rig.Still(0, -750)); // above the 720 ceiling
        Assert.Equal(TripCode.HandsOff, g.LastTrip);
    }

    [Fact]
    public void WithoutSprintOverrideBrakingOverspeedStillTrips()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        Rig.Run(g, clock, 2, Want(3));
        Rig.Run(g, clock, 0.06, Want(3), _ => Rig.Still(0, -450));
        Assert.Equal(TripCode.HandsOff, g.LastTrip);
    }

    [Fact]
    public void RunawayTripsWhenMotorDrivesTheWheel()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        Rig.Run(g, clock, 2, Want(3));
        Rig.Run(g, clock, 0.03, Want(3), _ => Rig.Still(0, 200)); // same direction as torque
        Assert.Equal(SafetyState.Tripped, g.State);
        Assert.Equal(TripCode.Runaway, g.LastTrip);
    }

    [Fact]
    public void FastMotionAgainstTheMotorIsAllowed()
    {
        // Lock-to-lock sprints: user moves fast against a damper. Torque opposes motion -> not a runaway.
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        Rig.Run(g, clock, 2, new TorqueRequest(0, -3, 100, true));
        Rig.Run(g, clock, 0.3, new TorqueRequest(0, -3, 100, true), _ => Rig.Still(0, 350));
        Assert.Equal(SafetyState.Armed, g.State);
    }

    [Fact]
    public void OverspeedFadeReducesMotorDrivenTorque()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock, new SafetySettings { RunawayDetection = false });
        Rig.Run(g, clock, 2, Want(4));
        clock.Advance(0.002);
        var o = g.Process(clock.Now, Rig.Still(0, 325), Want(4)); // halfway between 250 and 400
        Assert.InRange(o.TorqueNm, 1.8, 2.2);
    }

    // ---- Rule 5: angle limit ----

    [Fact]
    public void AngleLimitTripsWithin20DegOfLock()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        Rig.Run(g, clock, 2, Want(3));
        Rig.Run(g, clock, 0.01, Want(3), _ => Rig.Still(425));
        Assert.Equal(SafetyState.Armed, g.State);
        Rig.Run(g, clock, 0.004, Want(3), _ => Rig.Still(-431));
        Assert.Equal(TripCode.AngleLimit, g.LastTrip);
    }

    // ---- Rule 6: watchdog ----

    [Fact]
    public void MissedTickOver100msTripsOnNextTick()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        Rig.Run(g, clock, 2, Want(3));
        clock.Advance(0.12);
        var o = g.Process(clock.Now, Rig.Still(), Want(3));
        Assert.Equal(0, o.TorqueNm);
        Assert.Equal(TripCode.Watchdog, g.LastTrip);
    }

    [Fact]
    public void WatchdogThreadCheckTripsWhenLoopStalls()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        Rig.Run(g, clock, 1, Want(3));
        clock.Advance(0.05);
        Assert.False(g.CheckWatchdog(clock.Now));
        clock.Advance(0.06);
        Assert.True(g.CheckWatchdog(clock.Now));
        Assert.Equal(SafetyState.Tripped, g.State);
    }

    [Fact]
    public void EngineHostWatchdogStopsDeviceWhenTickHangs()
    {
        var clock = new SystemClock();
        var sim = new RimForce.Core.Devices.SimulatedForceDevice(clock, Rig.Profile);
        sim.Connect();
        var e = new TrainingEngine(sim, clock, Rig.Profile, new SafetySettings { RequireUiHeartbeat = false });
        e.Governor.ChecklistAccepted = true;
        e.Governor.RequireDirectionCheck = false;
        var hang = new ManualResetEventSlim(false);
        var act = new HangingActivity(hang);
        e.SetActivity(act);
        using var host = new EngineHost(e, 500);
        host.Start();
        Thread.Sleep(50);
        e.Post(() => e.Arm(out _));
        Thread.Sleep(300);
        Assert.Equal(SafetyState.Armed, e.Governor.State);
        act.HangNow = true;
        Thread.Sleep(400);
        Assert.Equal(SafetyState.Tripped, e.Governor.State);
        Assert.Equal(TripCode.Watchdog, e.Governor.LastTrip);
        Assert.True(sim.StopAllCount > 0);
        hang.Set();
    }

    private sealed class HangingActivity : IActivity
    {
        private readonly ManualResetEventSlim _gate;
        public volatile bool HangNow;
        public HangingActivity(ManualResetEventSlim gate) => _gate = gate;
        public string Kind => "hang";
        public bool IsFinished => false;
        public ActivityStep Step(in ActivityTick tick)
        {
            if (HangNow) _gate.Wait(2000);
            return new ActivityStep(new TorqueRequest(1, 0, 100, true), 0);
        }
        public void OnSafetyStop(string reason) { }
        public object Snapshot() => new { };
        public IReadOnlyList<ActivityEvent> DrainEvents() => Array.Empty<ActivityEvent>();
    }

    // ---- errors, disconnect, focus, heartbeat ----

    [Fact]
    public void DisconnectTrips()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        Rig.Run(g, clock, 1, Want(3));
        clock.Advance(0.002);
        var o = g.Process(clock.Now, new SafetyInput(0, 0, false, null, 0), Want(3));
        Assert.True(o.StopAllRequested);
        Assert.Equal(TripCode.Disconnected, g.LastTrip);
    }

    [Fact]
    public void DeviceErrorTrips()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        clock.Advance(0.002);
        g.Process(clock.Now, new SafetyInput(0, 0, true, "DIERR_INPUTLOST", 0), Want(3));
        Assert.Equal(TripCode.DeviceError, g.LastTrip);
    }

    [Fact]
    public void FocusLossTripsEngine()
    {
        var (e, sim, c) = Rig.SimEngine();
        e.SetActivity(new FixedActivity { Request = Want(2) });
        Rig.Step(e, c, 0.05);
        Assert.True(e.Arm(out _));
        Rig.Step(e, c, 1);
        e.SetFocus(false);
        Assert.Equal(TripCode.FocusLost, e.Governor.LastTrip);
        Assert.Equal(0, sim.LastCommand.ConstantPct);
    }

    [Fact]
    public void UiHeartbeatTimeoutTrips()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        Rig.Run(g, clock, 0.5, Want(2), _ => new SafetyInput(0, 0, true, null, 1.6));
        Assert.Equal(TripCode.UiHeartbeat, g.LastTrip);
    }

    [Fact]
    public void TripMustBeAcknowledgedBeforeRearm()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        g.Trip(clock.Now, TripCode.HandsOff, "Hands off detected");
        Assert.False(g.TryArm(clock.Now, 0, 0, true, out var why));
        Assert.Contains("Acknowledge", why);
        g.AcknowledgeTrip(clock.Now);
        Assert.True(g.TryArm(clock.Now, 0, 0, true, out _));
    }

    [Fact]
    public void EventsAreReported()
    {
        var clock = new ManualClock();
        var g = Rig.ArmedGovernor(clock);
        g.Trip(clock.Now, TripCode.AngleLimit, "x");
        var ev = g.DrainEvents();
        Assert.Contains(ev, e => e.Kind == "armed");
        Assert.Contains(ev, e => e.Kind == "tripped" && e.Code == TripCode.AngleLimit);
        Assert.Empty(g.DrainEvents());
    }
}
