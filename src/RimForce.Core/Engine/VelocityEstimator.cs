namespace RimForce.Core.Engine;

/// <summary>
/// Velocity and acceleration from a quantised position signal: finite difference followed by
/// first-order low-pass filters. Time constants are short enough for safety detection (~6 ms).
/// </summary>
public sealed class VelocityEstimator
{
    private bool _init;
    private double _lastT, _lastAngle;

    public double VelocityTauS { get; set; } = 0.006;
    public double AccelTauS { get; set; } = 0.02;
    public double Velocity { get; private set; }
    public double Acceleration { get; private set; }

    public void Reset() { _init = false; Velocity = 0; Acceleration = 0; }

    public void Update(double t, double angleDeg)
    {
        if (!_init) { _init = true; _lastT = t; _lastAngle = angleDeg; return; }
        double dt = t - _lastT;
        if (dt <= 1e-6) return;
        if (dt > 0.2) { _lastT = t; _lastAngle = angleDeg; Velocity = 0; Acceleration = 0; return; }
        double raw = (angleDeg - _lastAngle) / dt;
        double a = dt / (VelocityTauS + dt);
        double newV = Velocity + a * (raw - Velocity);
        double rawAcc = (newV - Velocity) / dt;
        double b = dt / (AccelTauS + dt);
        Acceleration += b * (rawAcc - Acceleration);
        Velocity = newV;
        _lastT = t;
        _lastAngle = angleDeg;
    }
}
