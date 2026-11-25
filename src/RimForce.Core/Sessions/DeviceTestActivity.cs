using RimForce.Core.Engine;
using RimForce.Core.Safety;

namespace RimForce.Core.Sessions;

/// <summary>
/// Device Test page: plays one effect alone (the governor caps everything at 10% in test mode) with live readouts.
/// Effects are rendered the same way drills render them (software), so what you feel here is what drills do.
/// </summary>
public sealed class DeviceTestActivity : IActivity
{
    public static readonly string[] Effects = { "constant-left", "constant-right", "spring", "damper", "friction", "vibration", "none" };

    private readonly List<ActivityEvent> _events = new();
    private double _start = double.NaN;
    private ActivityTick _last;

    public DeviceTestActivity(string effect, double pctOfMax, double maxTorqueNm)
    {
        Effect = Effects.Contains(effect) ? effect : "none";
        Pct = Math.Clamp(pctOfMax, 0, SafetyGovernor.TestModeCapPct);
        MaxTorqueNm = maxTorqueNm;
    }

    public string Effect { get; }
    public double Pct { get; }
    public double MaxTorqueNm { get; }
    public string Kind => "deviceTest";
    public bool IsFinished => false;
    private double Nm => Pct / 100.0 * MaxTorqueNm;

    public ActivityStep Step(in ActivityTick t)
    {
        _last = t;
        if (double.IsNaN(_start)) _start = t.Time;
        double e = t.Time - _start;
        double nm = Nm;
        double cap = MaxTorqueNm * SafetyGovernor.TestModeCapPct / 100.0;
        TorqueRequest req = Effect switch
        {
            "constant-left" => new(-nm, 0, cap, true),
            "constant-right" => new(nm, 0, cap, true),
            "spring" => new(0, Math.Clamp(-nm / 90.0 * t.AngleDeg, -nm, nm), cap, true),
            "damper" => new(0, Math.Clamp(-nm / 200.0 * t.VelocityDps, -nm, nm), cap, true),
            "friction" => new(0, -nm * Math.Tanh(t.VelocityDps / 8.0), cap, true),
            "vibration" => new(0, 0, cap, true, VibrationNm: Math.Min(nm, 1.0) * Math.Sin(2 * Math.PI * 20 * e)),
            _ => TorqueRequest.None,
        };
        return new ActivityStep(req, 0);
    }

    public void OnSafetyStop(string reason) => _events.Add(new ActivityEvent("deviceTestStopped", reason));

    public object Snapshot() => new
    {
        kind = Kind,
        effect = Effect,
        pct = Pct,
        nm = Math.Round(Nm, 2),
        angle = Math.Round(_last.AngleDeg, 2),
        velocity = Math.Round(_last.VelocityDps, 1),
        motorNm = Math.Round(_last.MotorTorqueNm, 3),
    };

    public IReadOnlyList<ActivityEvent> DrainEvents()
    {
        if (_events.Count == 0) return Array.Empty<ActivityEvent>();
        var c = _events.ToArray();
        _events.Clear();
        return c;
    }
}

/// <summary>
/// Force-direction check (hands OFF the wheel): a small clockwise nudge (4% for 200 ms) then nothing.
/// If the wheel moves clockwise the mapping is right; anticlockwise means force must be inverted.
/// Also estimates rotor+rim inertia from the initial acceleration (J = torque / alpha).
/// </summary>
public sealed class DirectionCheckActivity : IActivity
{
    public const double NudgePct = 4;
    public const double NudgeS = 0.2;
    public const double ObserveS = 0.6;

    private readonly List<ActivityEvent> _events = new();
    private readonly double _nudgeNm;
    private double _start = double.NaN;
    private double _startAngle;
    private double _peakAccel;
    private bool _done;
    private ActivityTick _last;

    public DirectionCheckActivity(double maxTorqueNm) => _nudgeNm = maxTorqueNm * NudgePct / 100.0;

    public string Kind => "directionCheck";
    public bool IsFinished => _done;
    public string Result { get; private set; } = "running";
    public double DisplacementDeg { get; private set; }
    public double? InertiaKgM2 { get; private set; }

    public ActivityStep Step(in ActivityTick t)
    {
        _last = t;
        if (_done) return ActivityStep.Idle;
        if (!t.Armed) return ActivityStep.Idle;
        if (double.IsNaN(_start)) { _start = t.Time; _startAngle = t.AngleDeg; }
        double e = t.Time - _start;
        // The governor's soft start would slow the nudge to nothing, so this check skips the envelope by
        // sending the nudge as vibration-free open loop with ForceWanted; the envelope still applies, so we wait
        // for it: total window = ramp (1.5 s) + nudge + observe.
        double rampS = 1.6;
        bool nudging = e >= rampS && e < rampS + NudgeS;
        if (e >= rampS && e < rampS + 0.1) _peakAccel = Math.Max(_peakAccel, t.AccelDps2);
        if (e < rampS) _startAngle = t.AngleDeg;
        if (e >= rampS + NudgeS + ObserveS)
        {
            DisplacementDeg = t.AngleDeg - _startAngle;
            Result = DisplacementDeg > 1.5 ? "ok" : DisplacementDeg < -1.5 ? "inverted" : "inconclusive";
            if (_peakAccel > 50) InertiaKgM2 = Math.Round(_nudgeNm / (_peakAccel * Math.PI / 180.0), 4);
            _done = true;
            _events.Add(new ActivityEvent("directionCheck", new { result = Result, displacementDeg = Math.Round(DisplacementDeg, 1), inertia = InertiaKgM2 }));
            return new ActivityStep(new TorqueRequest(0, 0, _nudgeNm * 1.5, false), 0);
        }
        // Before the nudge we want the envelope at 1 with zero torque, so the nudge itself is a clean step
        // (limited only by the 80 ms slew limit).
        return new ActivityStep(new TorqueRequest(nudging ? _nudgeNm : 0, 0, _nudgeNm * 1.5, true), 0);
    }

    public void OnSafetyStop(string reason)
    {
        if (_done) return;
        _done = true;
        Result = "aborted";
        _events.Add(new ActivityEvent("directionCheck", new { result = Result, reason }));
    }

    public object Snapshot() => new { kind = Kind, result = Result, angle = Math.Round(_last.AngleDeg, 1), displacementDeg = Math.Round(DisplacementDeg, 1) };

    public IReadOnlyList<ActivityEvent> DrainEvents()
    {
        if (_events.Count == 0) return Array.Empty<ActivityEvent>();
        var c = _events.ToArray();
        _events.Clear();
        return c;
    }
}
