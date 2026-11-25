using RimForce.Core.Devices;
using RimForce.Core.Engine;
using RimForce.Core.Hardware;
using RimForce.Core.Safety;

namespace RimForce.Tests;

/// <summary>An activity that always wants the same torque.</summary>
internal sealed class FixedActivity : IActivity
{
    public TorqueRequest Request { get; set; }
    public double Guide { get; set; }
    public int SafetyStops { get; private set; }
    public string Kind => "fixed";
    public bool IsFinished => false;
    public ActivityStep Step(in ActivityTick tick) => new(Request, Guide);
    public void OnSafetyStop(string reason) => SafetyStops++;
    public object Snapshot() => new { };
    public IReadOnlyList<ActivityEvent> DrainEvents() => Array.Empty<ActivityEvent>();
}

internal static class Rig
{
    public static WheelProfile Profile => new() { BaseMaxTorqueNm = 12, SimProStrengthPct = 100, RimDiameterMm = 300, RotationRangeDeg = 900 };

    public static SafetyGovernor ArmedGovernor(ManualClock clock, SafetySettings? s = null, WheelProfile? p = null)
    {
        var g = new SafetyGovernor(s ?? new SafetySettings(), p ?? Profile)
        {
            ChecklistAccepted = true,
            DirectionVerified = true,
        };
        Assert.True(g.TryArm(clock.Now, 0, 0, true, out var reason), reason);
        return g;
    }

    public static SafetyInput Still(double angle = 0, double vel = 0) => new(angle, vel, true, null, 0);

    /// <summary>Run the governor at 500 Hz for a duration, returning the last output.</summary>
    public static GovernorOutput Run(SafetyGovernor g, ManualClock clock, double seconds, TorqueRequest req, Func<double, SafetyInput>? input = null)
    {
        GovernorOutput last = default;
        int n = (int)Math.Round(seconds / 0.002);
        for (int i = 0; i < n; i++)
        {
            clock.Advance(0.002);
            last = g.Process(clock.Now, input?.Invoke(clock.Now) ?? Still(), req);
        }
        return last;
    }

    public static (TrainingEngine engine, SimulatedForceDevice sim, ManualClock clock) SimEngine(SafetySettings? s = null, WheelProfile? p = null)
    {
        var clock = new ManualClock();
        var profile = p ?? Profile;
        var sim = new SimulatedForceDevice(clock, profile);
        sim.Connect();
        var engine = new TrainingEngine(sim, clock, profile, s ?? new SafetySettings());
        engine.Governor.ChecklistAccepted = true;
        engine.Governor.RequireDirectionCheck = false;
        return (engine, sim, clock);
    }

    public static void Step(TrainingEngine e, ManualClock c, double seconds, double hz = 500)
    {
        int n = (int)Math.Round(seconds * hz);
        for (int i = 0; i < n; i++)
        {
            c.Advance(1.0 / hz);
            e.Heartbeat();
            e.Tick();
        }
    }
}
