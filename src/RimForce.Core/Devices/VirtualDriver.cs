namespace RimForce.Core.Devices;

public enum DriverMode
{
    /// <summary>Hands off the wheel: zero torque.</summary>
    HandsOff,
    /// <summary>Track a target angle supplied by the engine's guide (the ghost), like a real user following the UI.</summary>
    FollowGuide,
    /// <summary>Track a target set manually (mouse / keyboard in demo mode).</summary>
    Manual,
}

/// <summary>
/// A scripted human for demo mode and integration tests. Model:
///   torque = stiffness*(ref - angle) - damping*velocity + voluntary
/// where "voluntary" is a delayed, slow integral correction (humans react in ~120-180 ms),
/// all limited by a fatigue-reduced strength capacity.
/// </summary>
public sealed class VirtualDriver
{
    public DriverMode Mode { get; set; } = DriverMode.FollowGuide;

    /// <summary>Maximum torque this driver can produce (Nm), fresh.</summary>
    public double StrengthNm { get; set; } = 9.0;

    /// <summary>Reaction delay for voluntary corrections (s).</summary>
    public double ReactionDelayS { get; set; } = 0.14;

    /// <summary>How fast the driver fatigues (0 = never).</summary>
    public double FatigueRate { get; set; } = 0.012;

    public double StiffnessNmPerDeg { get; set; } = 0.5;
    /// <summary>How fast the driver's intended position can move (deg/s): a human does not teleport.</summary>
    public double MaxRefSpeedDps { get; set; } = 300;
    public double DampingNmPerDps { get; set; } = 0.012;
    public double VoluntaryGain { get; set; } = 2.2;   // Nm per deg per s
    public double TremorNm { get; set; } = 0.06;

    /// <summary>Target supplied by the engine (guide/ghost angle) in FollowGuide mode.</summary>
    public double GuideTargetDeg { get; set; }
    /// <summary>Target set by the UI in Manual mode.</summary>
    public double ManualTargetDeg { get; set; }

    /// <summary>Scripted release: at this time the driver lets go (for tests / demo of safety).</summary>
    public double? ReleaseAtTime { get; set; }

    public double Fatigue { get; private set; }
    public double LastTorqueNm { get; private set; }
    public double CapacityNm => StrengthNm * (1.0 - Fatigue);

    private double _voluntary;
    private double _delayedErr;
    private double _ref = double.NaN;
    private readonly Queue<(double t, double err)> _delayLine = new();
    private readonly Random _rng;
    private double _wander;

    public VirtualDriver(int seed = 7) => _rng = new Random(seed);

    public void Reset()
    {
        _voluntary = 0;
        _delayedErr = 0;
        _ref = double.NaN;
        _delayLine.Clear();
        Fatigue = 0;
        LastTorqueNm = 0;
    }

    public double ComputeTorque(double t, double dt, double angleDeg, double velocityDps)
    {
        if (ReleaseAtTime is { } releaseAt && t >= releaseAt)
            Mode = DriverMode.HandsOff;

        if (Mode == DriverMode.HandsOff)
        {
            _voluntary = 0;
            _delayedErr = 0;
            _delayLine.Clear();
            LastTorqueNm = 0;
            _ref = angleDeg;
            Recover(dt);
            return 0;
        }

        double target = Mode == DriverMode.Manual ? ManualTargetDeg : GuideTargetDeg;
        if (double.IsNaN(_ref)) _ref = angleDeg;
        double maxStep = MaxRefSpeedDps * dt;
        _ref += Math.Clamp(target - _ref, -maxStep, maxStep);
        double err = _ref - angleDeg;

        // Delayed perception for voluntary control.
        _delayLine.Enqueue((t, err));
        while (_delayLine.Count > 1 && _delayLine.Peek().t <= t - ReactionDelayS)
            _delayedErr = _delayLine.Dequeue().err;
        double delayedErr = _delayedErr;

        double cap = CapacityNm;
        _voluntary += VoluntaryGain * delayedErr * dt;
        _voluntary = Math.Clamp(_voluntary, -cap, cap);

        _wander += (_rng.NextDouble() - 0.5) * 0.4 * dt;
        _wander *= 0.995;
        double tremor = TremorNm * Math.Sin(2 * Math.PI * 9.0 * t) + _wander;

        double torque = StiffnessNmPerDeg * err - DampingNmPerDps * velocityDps + _voluntary + tremor;
        torque = Math.Clamp(torque, -cap, cap);

        // Fatigue grows with relative effort squared; recovers when lightly loaded.
        double rel = Math.Abs(torque) / Math.Max(0.1, StrengthNm);
        Fatigue = Math.Clamp(Fatigue + FatigueRate * rel * rel * dt, 0, 0.6);
        if (rel < 0.2) Recover(dt);

        LastTorqueNm = torque;
        return torque;
    }

    private void Recover(double dt) => Fatigue = Math.Max(0, Fatigue - 0.02 * Fatigue * dt);
}
