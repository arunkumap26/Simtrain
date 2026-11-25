using RimForce.Core.Engine;
using RimForce.Core.Hardware;

namespace RimForce.Core.Devices;

/// <summary>
/// Physics model of a direct-drive wheel plus a <see cref="VirtualDriver"/> holding it.
/// Integrates on every Read() up to the clock's current time using the last applied output.
/// The reported angle is quantised like a 16-bit DirectInput axis so the engine's velocity
/// estimator sees realistic data.
/// </summary>
public sealed class SimulatedForceDevice : IForceDevice
{
    private readonly IClock _clock;
    private readonly object _lock = new();
    private WheelProfile _profile;
    private double _t;
    private double _angle;      // deg
    private double _vel;        // deg/s
    private double _motorNm;    // actual motor torque (first-order lag behind command)
    private ForceOutput _cmd = ForceOutput.Zero;
    private bool _armed;
    private bool _connected;
    private double _gain = 1.0;

    public VirtualDriver Driver { get; } = new();

    /// <summary>Mechanical damping of the base (SimPro "mechanical damper"), Nm per deg/s.</summary>
    public double MechDampingNmPerDps { get; set; } = 0.0012;
    public double MechFrictionNm { get; set; } = 0.04;
    /// <summary>Motor torque bandwidth time constant (s).</summary>
    public double MotorLagS { get; set; } = 0.0015;
    /// <summary>Simulate a base whose force direction is reversed (to test safety).</summary>
    public bool HardwareInvertsForce { get; set; }
    /// <summary>Simulate unplugging the USB cable.</summary>
    public bool SimulateDisconnect { get; set; }
    /// <summary>Velocity (deg/s) at which a native damper reaches its full coefficient (DirectInput normalises velocity).</summary>
    public double NativeDamperFullScaleDps { get; set; } = 1000;

    public double TrueAngleDeg { get { lock (_lock) return _angle; } }
    public double TrueVelocityDps { get { lock (_lock) return _vel; } }
    public double MotorTorqueNm { get { lock (_lock) return _motorNm; } }
    public double DriverTorqueNm => Driver.LastTorqueNm;
    public bool IsArmed { get { lock (_lock) return _armed; } }
    public ForceOutput LastCommand { get { lock (_lock) return _cmd; } }
    public int StopAllCount { get; private set; }

    public SimulatedForceDevice(IClock clock, WheelProfile? profile = null)
    {
        _clock = clock;
        _profile = profile ?? new WheelProfile();
        _t = clock.Now;
    }

    public void SetProfile(WheelProfile p) { lock (_lock) _profile = p; }

    public DeviceInfo? Info { get; private set; }
    public bool IsConnected => _connected && !SimulateDisconnect;

    public IReadOnlyList<DeviceInfo> Enumerate() =>
        new[] { new DeviceInfo("sim", "Simulated Alpha EVO (demo)", true,
            EffectSupport.Constant | EffectSupport.Spring | EffectSupport.Damper | EffectSupport.Friction | EffectSupport.Periodic) };

    public bool Connect(string? deviceId = null)
    {
        Info = Enumerate()[0];
        _connected = true;
        _t = _clock.Now;
        return true;
    }

    public bool Arm()
    {
        lock (_lock) { if (!IsConnected) return false; _armed = true; return true; }
    }

    public void Disarm()
    {
        lock (_lock) { _armed = false; _cmd = ForceOutput.Zero; }
    }

    public void StopAll()
    {
        lock (_lock) { _cmd = ForceOutput.Zero; StopAllCount++; }
    }

    public void SetDeviceGain(double pct) { lock (_lock) _gain = Math.Clamp(pct, 0, 100) / 100.0; }

    public void Apply(in ForceOutput output)
    {
        lock (_lock)
        {
            if (!_armed || !IsConnected) { _cmd = ForceOutput.Zero; return; }
            _cmd = output;
        }
    }

    public DeviceReading Read()
    {
        lock (_lock)
        {
            Integrate(_clock.Now);
            if (!IsConnected) return new DeviceReading(0, false, 0, "Device disconnected");
            double range = _profile.RotationRangeDeg;
            // 16-bit quantisation over the full rotation range.
            double counts = Math.Round((_angle / range + 0.5) * 65535.0);
            double q = (counts / 65535.0 - 0.5) * range;
            return new DeviceReading(q, true, 0);
        }
    }

    /// <summary>Directly place the wheel (tests).</summary>
    public void SetState(double angleDeg, double velDps = 0)
    {
        lock (_lock) { _angle = angleDeg; _vel = velDps; _t = _clock.Now; }
    }

    private double CommandedMotorNm(double angle, double vel)
    {
        double max = _profile.MaxTorqueNm * _gain;
        double nm = _cmd.ConstantPct / 100.0 * max;
        double lock_ = _profile.LockDeg;
        if (_cmd.NativeSpringPct != 0)
            nm += -_cmd.NativeSpringPct / 100.0 * max * Math.Clamp((angle - _cmd.NativeSpringCenterDeg) / lock_, -1, 1);
        if (_cmd.NativeDamperPct != 0)
            nm += -_cmd.NativeDamperPct / 100.0 * max * Math.Clamp(vel / NativeDamperFullScaleDps, -1, 1);
        if (_cmd.NativeFrictionPct != 0)
            nm += -_cmd.NativeFrictionPct / 100.0 * max * Math.Tanh(vel / 5.0);
        nm = Math.Clamp(nm, -max, max);
        return HardwareInvertsForce ? -nm : nm;
    }

    private void Integrate(double now)
    {
        const double h = 0.0005;
        double J = Math.Max(0.005, _profile.InertiaKgM2);
        double lock_ = _profile.LockDeg;
        int guard = 0;
        while (_t < now - 1e-9 && guard++ < 200_000)
        {
            double dt = Math.Min(h, now - _t);
            double target = (_armed && IsConnected) ? CommandedMotorNm(_angle, _vel) : 0;
            _motorNm += (target - _motorNm) * Math.Min(1.0, dt / MotorLagS);

            double driver = Driver.ComputeTorque(_t, dt, _angle, _vel);
            double mech = -MechDampingNmPerDps * _vel - MechFrictionNm * Math.Tanh(_vel / 3.0);
            double total = _motorNm + driver + mech;

            double accDeg = total / J * Units.RadToDeg;
            _vel += accDeg * dt;
            _angle += _vel * dt;
            if (_angle > lock_) { _angle = lock_; if (_vel > 0) _vel = 0; }
            if (_angle < -lock_) { _angle = -lock_; if (_vel < 0) _vel = 0; }
            _t += dt;
        }
        if (guard >= 200_000) _t = now; // large jump (debugger pause): skip ahead
    }

    public void Dispose() { StopAll(); _connected = false; }
}
