namespace RimForce.Core.Devices;

[Flags]
public enum EffectSupport
{
    None = 0,
    Constant = 1,
    Spring = 2,
    Damper = 4,
    Friction = 8,
    Inertia = 16,
    Periodic = 32,
}

public sealed record DeviceInfo(string Id, string Name, bool IsSimulated, EffectSupport Effects);

/// <summary>A raw reading from the wheel. Angle is degrees from centre, positive = clockwise (right).</summary>
public readonly record struct DeviceReading(
    double AngleDeg,
    bool Connected,
    uint Buttons,
    string? Error = null);

/// <summary>
/// What the device should output right now, after the safety governor. Everything is percent of
/// DirectInput full scale (the SimPro overall-strength cap applies on top, inside the base).
/// Positive constant force = clockwise torque.
/// Native condition effects are optional; by default RimForce renders springs/dampers in software
/// as part of ConstantPct so the torque is exactly known (see DECISIONS.md).
/// </summary>
public readonly record struct ForceOutput(
    double ConstantPct,
    double NativeSpringPct = 0,
    double NativeSpringCenterDeg = 0,
    double NativeDamperPct = 0,
    double NativeFrictionPct = 0)
{
    public static readonly ForceOutput Zero = new(0);
    public bool IsZero => ConstantPct == 0 && NativeSpringPct == 0 && NativeDamperPct == 0 && NativeFrictionPct == 0;
}

/// <summary>
/// Abstraction over a force-feedback wheel. Implementations: SimulatedForceDevice (Core) and
/// DirectInputForceDevice (Windows). All calls come from the force-loop thread except StopAll,
/// which must be safe to call from any thread (watchdog, UI, focus loss).
/// </summary>
public interface IForceDevice : IDisposable
{
    DeviceInfo? Info { get; }
    bool IsConnected { get; }

    IReadOnlyList<DeviceInfo> Enumerate();
    bool Connect(string? deviceId = null);

    /// <summary>Acquire the device and create effects. No force is output until Apply is called with a non-zero value.</summary>
    bool Arm();

    /// <summary>Release force output gracefully (effects stopped, device stays connected for reading).</summary>
    void Disarm();

    /// <summary>Immediately stop every effect. Thread-safe. Must never throw.</summary>
    void StopAll();

    DeviceReading Read();
    void Apply(in ForceOutput output);

    /// <summary>Optional device gain (0-100) applied by the device on every effect (DIPROP_FFGAIN).</summary>
    void SetDeviceGain(double pct);
}
