using System.Diagnostics;
using RimForce.Core.Devices;
using SharpGen.Runtime;
using Vortice.DirectInput;

namespace RimForce.DirectInput;

/// <summary>Options read every time the device (re)initialises.</summary>
public sealed record DirectInputOptions(
    Func<nint> WindowHandle,
    bool Background = false,
    bool FiniteDurations = false,
    int FiniteDurationMs = 250,
    double RotationRangeDeg = 900);

/// <summary>Correct dinput.h HRESULTs (Vortice 3.8.3's ResultCode.InputLost/NotAcquired/... are wrong). See docs/research/02.</summary>
internal static class Di
{
    public const int InputLost = unchecked((int)0x8007001E);
    public const int NotAcquired = unchecked((int)0x8007000C);
    public const int Acquired = unchecked((int)0x800700AA);
    public const int OtherAppHasPrio = unchecked((int)0x80070005);
    public const int NotExclusiveAcquired = unchecked((int)0x80040205);
    public const int NotDownloaded = unchecked((int)0x80040203);
    public const int EffectPlaying = unchecked((int)0x80040208);
    public const int Unplugged = unchecked((int)0x80040209);
    public const int Infinite = -1, NoTrigger = -1, NominalMax = 10000;

    public static bool IsDeviceLoss(int hr) =>
        hr is InputLost or NotAcquired or OtherAppHasPrio or NotExclusiveAcquired or NotDownloaded or Unplugged;
}

/// <summary>
/// DirectInput 8 force feedback for a Simagic (or any DirectInput) wheelbase, via Vortice.DirectInput.
/// Design (docs/research/02-directinput-ffb.md):
///  - one constant-force effect, created once, INFINITE duration, updated with TypeSpecific|NoRestart;
///    every drill force (springs, dampers, pulses, vibration) is computed in software into it;
///  - native Spring/Damper/Friction are created only if requested (Device Test / setting) — on Simagic the
///    native spring ignores its centre offset and native conditions are scaled by SimPro's game sliders;
///  - unchanged magnitudes are skipped but re-sent at least every 250 ms;
///  - all DirectInput calls are serialised by a lock; StopAll takes it with a timeout and stops anyway.
/// Hardware behaviour is NOT verified — see the on-device checklist in README.md.
/// </summary>
public sealed class DirectInputForceDevice : IForceDevice
{
    private readonly object _di = new();
    private readonly Func<DirectInputOptions> _options;
    private IDirectInput8? _input;
    private IDirectInputDevice8? _dev;
    private IDirectInputEffect? _constant, _spring, _damper, _friction;
    private EffectParameters? _cfParams, _springParams, _damperParams, _frictionParams;
    private ConstantForce? _cf;
    private ConditionSet? _springCond, _damperCond, _frictionCond;
    private EffectParameterFlags _updateFlags = EffectParameterFlags.TypeSpecificParameters | EffectParameterFlags.NoRestart;
    private Guid _guid;
    private bool _open, _armed;
    private int _lastSent = int.MinValue;
    private long _lastSendTicks, _lastRestartTicks, _lastReopenTicks;
    private int _reopenDelayMs = 1000;
    private string? _error;
    private double _rangeHalfDeg = 450;
    private JoystickState _js = new();
    private double _lastAngle;

    public DirectInputForceDevice(Func<DirectInputOptions> options) => _options = options;

    public DeviceInfo? Info { get; private set; }
    public bool IsConnected => _open || (Info != null && _error == null);
    public string? LastError => _error;
    public double MeasuredUpdateHz { get; private set; }

    // ---------------- enumeration ----------------

    public static IReadOnlyList<DeviceInfo> EnumerateDevices()
    {
        using var di = DInput.DirectInput8Create();
        var list = new List<DeviceInfo>();
        foreach (var d in di.GetDevices(DeviceClass.GameControl, DeviceEnumerationFlags.AttachedOnly | DeviceEnumerationFlags.ForceFeedback))
            list.Add(new DeviceInfo(d.InstanceGuid.ToString(), d.ProductName ?? d.InstanceName ?? "Wheel", false, EffectSupport.Constant));
        return list;
    }

    public IReadOnlyList<DeviceInfo> Enumerate()
    {
        try { return EnumerateDevices(); }
        catch { return Array.Empty<DeviceInfo>(); }
    }

    public bool Connect(string? deviceId = null)
    {
        var all = Enumerate();
        var pick = all.FirstOrDefault(d => d.Id == deviceId)
                   ?? all.FirstOrDefault(d => d.Name.Contains("Simagic", StringComparison.OrdinalIgnoreCase))
                   ?? all.FirstOrDefault();
        if (pick == null) { _error = "No force-feedback wheel found"; return false; }
        _guid = Guid.Parse(pick.Id);
        Info = pick;
        _error = null;
        // The actual DirectInput device is opened lazily on the force-loop thread (first Read).
        return true;
    }

    // ---------------- open / close ----------------

    private void EnsureOpen()
    {
        if (_open || _guid == Guid.Empty) return;
        long now = Stopwatch.GetTimestamp();
        if (_lastReopenTicks != 0 && Elapsed(_lastReopenTicks, now) < _reopenDelayMs) return;
        _lastReopenTicks = now;
        var opt = _options();
        try
        {
            _input ??= DInput.DirectInput8Create();
            if (!_input.IsDeviceAttached(_guid)) throw new InvalidOperationException("Wheel not attached");
            _dev = _input.CreateDevice(_guid);
            _dev.SetDataFormat<RawJoystickState>().CheckError();                // before Acquire
            var coop = CooperativeLevel.Exclusive | (opt.Background ? CooperativeLevel.Background : CooperativeLevel.Foreground);
            _dev.SetCooperativeLevel(opt.WindowHandle(), coop).CheckError();
            try { _dev.Properties.AutoCenter = false; } catch (SharpGenException) { }
            try { _dev.GetObjectPropertiesByName("X").Range = new InputRange(-32768, 32767); } catch (SharpGenException) { }
            try { _dev.Properties.ForceFeedbackGain = Di.NominalMax; } catch (SharpGenException) { }
            _rangeHalfDeg = Math.Max(45, opt.RotationRangeDeg / 2);
            var support = EffectSupport.None;
            try
            {
                foreach (var e in _dev.GetEffects())
                {
                    if (e.Guid == EffectGuid.ConstantForce) support |= EffectSupport.Constant;
                    else if (e.Guid == EffectGuid.Spring) support |= EffectSupport.Spring;
                    else if (e.Guid == EffectGuid.Damper) support |= EffectSupport.Damper;
                    else if (e.Guid == EffectGuid.Friction) support |= EffectSupport.Friction;
                    else if (e.Guid == EffectGuid.Inertia) support |= EffectSupport.Inertia;
                    else if (e.Guid == EffectGuid.Sine) support |= EffectSupport.Periodic;
                }
            }
            catch (SharpGenException) { support = EffectSupport.Constant; }
            Info = Info! with { Effects = support };
            TryAcquire();
            _open = true;
            _error = null;
            _reopenDelayMs = 1000;
        }
        catch (Exception ex)
        {
            _error = Describe(ex);
            CloseDevice();
            _reopenDelayMs = Math.Min(15000, _reopenDelayMs * 2); // backoff 1 -> 15 s
        }
    }

    private bool TryAcquire()
    {
        if (_dev == null) return false;
        var r = _dev.Acquire();
        return r.Success;
    }

    private void CreateEffects()
    {
        if (_dev == null || _constant != null) return;
        var opt = _options();
        _cf = new ConstantForce { Magnitude = 0 };
        _cfParams = NewParams(_cf, opt);
        _constant = _dev.CreateEffect(EffectGuid.ConstantForce, _cfParams);
    }

    private static EffectParameters NewParams(TypeSpecificParameters p, DirectInputOptions opt) => new()
    {
        Flags = EffectFlags.Cartesian | EffectFlags.ObjectOffsets,
        Duration = opt.FiniteDurations ? Math.Clamp(opt.FiniteDurationMs, 50, 2000) * 1000 : Di.Infinite, // microseconds
        SamplePeriod = 0,
        Gain = Di.NominalMax,
        TriggerButton = Di.NoTrigger,               // default 0 => DIERR_INVALIDPARAM
        TriggerRepeatInterval = 0,
        StartDelay = 0,
        Axes = new[] { (int)JoystickOffset.X },
        Directions = new[] { 1 },
        Parameters = p,
    };

    private IDirectInputEffect? CreateCondition(Guid guid, out ConditionSet cond, out EffectParameters prm)
    {
        cond = new ConditionSet { Conditions = new[] { new Condition { Offset = 0, PositiveCoefficient = 0, NegativeCoefficient = 0, PositiveSaturation = Di.NominalMax, NegativeSaturation = Di.NominalMax, DeadBand = 0 } } };
        prm = NewParams(cond, _options() with { FiniteDurations = false });
        try { return _dev!.CreateEffect(guid, prm); }
        catch (SharpGenException) { return null; }
    }

    private void CloseDevice()
    {
        try { _dev?.SendForceFeedbackCommand(ForceFeedbackCommand.StopAll); } catch { }
        foreach (var e in new[] { _constant, _spring, _damper, _friction })
        {
            try { e?.Stop(); } catch { }
            try { e?.Dispose(); } catch { }
        }
        _constant = _spring = _damper = _friction = null;
        try { _dev?.Unacquire(); } catch { }
        try { _dev?.Dispose(); } catch { }
        _dev = null;
        _open = false;
        _lastSent = int.MinValue;
    }

    // ---------------- IForceDevice ----------------

    public bool Arm()
    {
        lock (_di)
        {
            EnsureOpen();
            if (_dev == null) return false;
            try
            {
                if (!TryAcquire()) { _error = "Could not acquire the wheel (close iRacing / other FFB apps, keep RimForce focused)"; return false; }
                if (_constant == null)
                {
                    // First arm on this device object: reset to a clean state and create the effect ONCE.
                    // Re-creating effects on the same device hits a known Windows driver bug (docs/research/02),
                    // so later arms only restart the existing effect.
                    _dev.SendForceFeedbackCommand(ForceFeedbackCommand.Reset);
                    _dev.SendForceFeedbackCommand(ForceFeedbackCommand.SetActuatorsOn);
                    CreateEffects();
                }
                else
                {
                    _dev.SendForceFeedbackCommand(ForceFeedbackCommand.SetActuatorsOn);
                }
                _cf!.Magnitude = 0;
                _constant!.SetParameters(_cfParams!, EffectParameterFlags.TypeSpecificParameters); // (re)downloads if needed
                _constant.Start(1).CheckError();
                _armed = true;
                _lastSent = 0;
                _lastSendTicks = _lastRestartTicks = Stopwatch.GetTimestamp();
                _error = null;
                return true;
            }
            catch (Exception ex)
            {
                _error = Describe(ex);
                return false;
            }
        }
    }

    public void Disarm()
    {
        lock (_di)
        {
            _armed = false;
            ZeroAndStop();
        }
    }

    public void StopAll()
    {
        bool locked = Monitor.TryEnter(_di, 50);
        try
        {
            _armed = false;
            ZeroAndStop();
        }
        catch { /* must never throw */ }
        finally { if (locked) Monitor.Exit(_di); }
    }

    private void ZeroAndStop()
    {
        try
        {
            if (_cf != null && _constant != null)
            {
                _cf.Magnitude = 0;
                _constant.SetParameters(_cfParams!, EffectParameterFlags.TypeSpecificParameters);
            }
        }
        catch { }
        try { _constant?.Stop(); } catch { }
        try { _spring?.Stop(); _damper?.Stop(); _friction?.Stop(); } catch { }
        try { _dev?.SendForceFeedbackCommand(ForceFeedbackCommand.StopAll); } catch { }
        _lastSent = int.MinValue;
    }

    public void SetDeviceGain(double pct)
    {
        lock (_di)
        {
            try { if (_dev != null) _dev.Properties.ForceFeedbackGain = (int)Math.Clamp(pct * 100, 0, Di.NominalMax); } catch { }
        }
    }

    public DeviceReading Read()
    {
        lock (_di)
        {
            EnsureOpen();
            if (_dev == null) return new DeviceReading(0, Info != null && _error == null, 0, _armed ? _error ?? "Device closed" : null);
            try
            {
                _dev.Poll();
                _dev.GetCurrentJoystickState(ref _js);
                double angle = _js.X / 32768.0 * _rangeHalfDeg;
                _lastAngle = angle;
                uint buttons = 0;
                var b = _js.Buttons;
                for (int i = 0; i < 32 && i < b.Length; i++) if (b[i]) buttons |= 1u << i;
                return new DeviceReading(angle, true, buttons);
            }
            catch (SharpGenException ex) when (Di.IsDeviceLoss(ex.ResultCode.Code))
            {
                // Lost focus (Foreground mode) or unplugged. While armed this is a safety stop;
                // while idle, quietly try to re-acquire.
                if (ex.ResultCode.Code == Di.Unplugged) { CloseDevice(); _error = "Wheel unplugged"; }
                else TryAcquire();
                return new DeviceReading(_lastAngle, ex.ResultCode.Code != Di.Unplugged, 0, _armed ? Describe(ex) : null);
            }
            catch (Exception ex)
            {
                _error = Describe(ex);
                CloseDevice();
                return new DeviceReading(0, false, 0, _error);
            }
        }
    }

    public void Apply(in ForceOutput output)
    {
        lock (_di)
        {
            if (!_armed || _constant == null || _cf == null) return;
            var opt = _options();
            long now = Stopwatch.GetTimestamp();
            int m = (int)Math.Round(Math.Clamp(output.ConstantPct, -100, 100) * 100);
            bool restart = opt.FiniteDurations && Elapsed(_lastRestartTicks, now) >= 100;
            if (m == _lastSent && !restart && Elapsed(_lastSendTicks, now) < 250) { ApplyNative(output); return; }
            _cf.Magnitude = m;
            try
            {
                var flags = restart ? EffectParameterFlags.TypeSpecificParameters | EffectParameterFlags.Duration | EffectParameterFlags.Start : _updateFlags;
                if (restart) _cfParams!.Duration = Math.Clamp(opt.FiniteDurationMs, 50, 2000) * 1000;
                _constant.SetParameters(_cfParams!, flags);
                if (restart) _lastRestartTicks = now;
            }
            catch (SharpGenException ex) when (ex.ResultCode.Code == Di.EffectPlaying)
            {
                _updateFlags = EffectParameterFlags.TypeSpecificParameters; // device can't update while playing without restart
                _constant.SetParameters(_cfParams!, _updateFlags);
            }
            catch (SharpGenException ex) when (Di.IsDeviceLoss(ex.ResultCode.Code))
            {
                _error = Describe(ex);
                throw new InvalidOperationException(_error);
            }
            _lastSent = m;
            double dtMs = Elapsed(_lastSendTicks, now);
            if (dtMs > 0) MeasuredUpdateHz = MeasuredUpdateHz * 0.98 + 0.02 * (1000.0 / dtMs);
            _lastSendTicks = now;
            ApplyNative(output);
        }
    }

    /// <summary>Optional native condition effects (only created when first used).</summary>
    private void ApplyNative(in ForceOutput o)
    {
        if (_dev == null) return;
        SetCondition(ref _spring, ref _springCond, ref _springParams, EffectGuid.Spring, o.NativeSpringPct);
        SetCondition(ref _damper, ref _damperCond, ref _damperParams, EffectGuid.Damper, o.NativeDamperPct);
        SetCondition(ref _friction, ref _frictionCond, ref _frictionParams, EffectGuid.Friction, o.NativeFrictionPct);
    }

    private void SetCondition(ref IDirectInputEffect? fx, ref ConditionSet? cond, ref EffectParameters? prm, Guid guid, double pct)
    {
        if (fx == null && pct == 0) return;
        if (fx == null)
        {
            fx = CreateCondition(guid, out var c, out var p);
            cond = c; prm = p;
            if (fx == null) return;
            try { fx.Start(1).CheckError(); } catch { }
        }
        int coef = (int)Math.Round(Math.Clamp(pct, 0, 100) * 100);
        if (cond!.Conditions[0].PositiveCoefficient == coef) return;
        cond.Conditions[0].PositiveCoefficient = coef;
        cond.Conditions[0].NegativeCoefficient = coef;
        try { fx.SetParameters(prm!, EffectParameterFlags.TypeSpecificParameters | EffectParameterFlags.NoRestart); } catch { }
    }

    private static double Elapsed(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;

    private static string Describe(Exception ex) => ex switch
    {
        SharpGenException s when s.ResultCode.Code == Di.OtherAppHasPrio => "Another app (or a non-focused window) holds the wheel",
        SharpGenException s when s.ResultCode.Code == Di.InputLost => "Lost the wheel (window lost focus?)",
        SharpGenException s when s.ResultCode.Code == Di.NotAcquired => "Wheel not acquired",
        SharpGenException s when s.ResultCode.Code == Di.Unplugged => "Wheel unplugged",
        SharpGenException s => $"DirectInput error 0x{s.ResultCode.Code:X8}",
        _ => ex.Message,
    };

    public void Dispose()
    {
        lock (_di)
        {
            _armed = false;
            CloseDevice();
            try { _input?.Dispose(); } catch { }
            _input = null;
        }
    }
}

/// <summary>Lists and opens DirectInput force-feedback wheels for the AppController.</summary>
public sealed class DirectInputDeviceProvider : RimForce.Core.App.IDeviceProvider
{
    private readonly Func<DirectInputOptions> _options;
    public DirectInputDeviceProvider(Func<DirectInputOptions> options) => _options = options;

    public IReadOnlyList<DeviceInfo> List()
    {
        try { return DirectInputForceDevice.EnumerateDevices(); }
        catch { return Array.Empty<DeviceInfo>(); }
    }

    public IForceDevice? Create(string id) => Guid.TryParse(id, out _) ? new DirectInputForceDevice(_options) : null;
}
