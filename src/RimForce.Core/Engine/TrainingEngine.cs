using System.Collections.Concurrent;
using RimForce.Core.Devices;
using RimForce.Core.Hardware;
using RimForce.Core.Metrics;
using RimForce.Core.Safety;

namespace RimForce.Core.Engine;

public sealed record EngineEvent(string Kind, object? Data = null);

public sealed record EngineSnapshot(
    double T,
    double AngleDeg,
    double VelocityDps,
    double MotorTorqueNm,
    double UserTorqueNm,
    double CommandPct,
    double Envelope,
    string Safety,
    string? TripCode,
    string? TripMessage,
    double LoopHz,
    double MaxLoopGapMs,
    double Intensity,
    double GlobalCapNm,
    double MaxTorqueNm,
    bool Connected,
    string? DeviceName,
    bool Simulated,
    LiveStrength Strength,
    object? Activity);

/// <summary>Wheel button actions that can be bound in settings.</summary>
public enum ButtonAction { None, IntensityUp, IntensityDown, Next, Stop }

/// <summary>
/// The force loop. One Tick() = read wheel, run activity, pass through the safety governor, write wheel.
/// Deterministic and clock-injected so it can be stepped from tests; <see cref="EngineHost"/> runs it on a thread.
/// Commands from other threads are queued and executed at the start of the next tick, except Stop which is immediate.
/// </summary>
public sealed class TrainingEngine
{
    private readonly IClock _clock;
    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly ConcurrentQueue<EngineEvent> _events = new();
    private readonly VelocityEstimator _vel = new();
    private WheelProfile _profile;
    private double _lastTick = double.NaN;
    private double _motorTorque;
    private double _commandPct;
    private double _userTorque;
    private double _userTorqueDisplay;
    private double _budget;
    private int _lastSetSerial = -1;
    private LiveStrength _strength = LiveStrength.Zero;
    private double _angle;
    private double _uiHeartbeat;
    private uint _lastButtons;
    private double _loopHz;
    private int _ticksThisWindow;
    private double _windowStart = double.NaN;
    private double _maxGapMs, _maxGapWindow;
    private double _lastPublish = double.NaN;
    private volatile EngineSnapshot? _latest;

    public TrainingEngine(IForceDevice device, IClock clock, WheelProfile profile, SafetySettings safety)
    {
        Device = device;
        _clock = clock;
        _profile = profile;
        Governor = new SafetyGovernor(safety, profile);
        _uiHeartbeat = clock.Now;
    }

    public IForceDevice Device { get; private set; }
    public SafetyGovernor Governor { get; }
    public IClock Clock => _clock;
    public IActivity? Activity { get; private set; }
    public double Intensity { get; private set; } = 1.0;
    public double PublishIntervalS { get; set; } = 1.0 / 60.0;
    public Dictionary<int, ButtonAction> ButtonMap { get; } = new();
    /// <summary>Maximum torque (Nm) used for "% of max" when the activity does not provide one (latest breakpoint).</summary>
    public double? ReferenceMaxNm { get; set; }
    public StrengthNorms Norms { get; set; } = StrengthNorms.Men;
    public double? BodyMassKg { get; set; }
    public bool WristTechnique { get; set; }

    public WheelProfile Profile
    {
        get => _profile;
        set { _profile = value; Governor.Profile = value; (Device as SimulatedForceDevice)?.SetProfile(value); }
    }

    public EngineSnapshot? Latest => _latest;
    public double AngleDeg => _angle;
    public double VelocityDps => _vel.Velocity;

    // ---------------- thread-safe API ----------------

    public void Post(Action command) => _commands.Enqueue(command);

    public bool TryDequeueEvent(out EngineEvent? e) => _events.TryDequeue(out e);

    public void Emit(string kind, object? data = null) => _events.Enqueue(new EngineEvent(kind, data));

    /// <summary>Immediate stop from any thread (STOP button, Esc, Space). Never ramps.</summary>
    public void Stop(string reason = "Stopped")
    {
        Governor.UserStop(_clock.Now);
        try { Device.StopAll(); } catch { /* StopAll must never throw */ }
        Post(() => Activity?.OnSafetyStop(reason));
    }

    /// <summary>Immediate safety trip from any thread (focus loss, device error...).</summary>
    public void Trip(TripCode code, string message)
    {
        Governor.Trip(_clock.Now, code, message);
        try { Device.StopAll(); } catch { }
        Post(() => Activity?.OnSafetyStop(message));
    }

    public void Heartbeat() => Volatile.Write(ref _uiHeartbeat, _clock.Now);

    public void SetFocus(bool focused)
    {
        if (!focused && Governor.Settings.StopOnFocusLoss && Governor.State == SafetyState.Armed)
            Trip(TripCode.FocusLost, "App lost focus");
    }

    // ---------------- loop-thread API (call via Post when on another thread) ----------------

    public bool Arm(out string? reason)
    {
        double now = _clock.Now;
        if (!Device.IsConnected) { reason = "No wheel connected."; return false; }
        if (!Governor.TryArm(now, _angle, _vel.Velocity, Device.IsConnected, out reason)) return false;
        if (!Device.Arm())
        {
            Governor.Trip(now, TripCode.DeviceError, "Could not acquire the wheel for force feedback");
            reason = "Could not acquire the wheel (is iRacing or another FFB app open?)";
            return false;
        }
        Device.SetDeviceGain(100);
        Heartbeat();
        return true;
    }

    public void Disarm() => Governor.RequestDisarm(_clock.Now);

    public void SetActivity(IActivity? activity)
    {
        Activity = activity;
        Emit("activity", activity?.Kind);
    }

    public void SetIntensity(double value)
    {
        Intensity = Math.Round(Math.Clamp(value, 0.1, 1.5), 3);
        Emit("intensity", Intensity);
    }

    public void ReplaceDevice(IForceDevice device)
    {
        try { Device.StopAll(); Device.Disarm(); } catch { }
        if (Governor.State == SafetyState.Armed) Governor.UserStop(_clock.Now);
        Device = device;
        _vel.Reset();
    }

    /// <summary>One iteration of the force loop.</summary>
    public void Tick()
    {
        while (_commands.TryDequeue(out var cmd))
        {
            try { cmd(); }
            catch (Exception ex) { Emit("error", ex.Message); }
        }

        double now = _clock.Now;
        double dt = double.IsNaN(_lastTick) ? 0 : now - _lastTick;
        _lastTick = now;
        TrackLoopRate(now, dt);

        DeviceReading reading;
        try { reading = Device.Read(); }
        catch (Exception ex) { reading = new DeviceReading(_angle, Device.IsConnected, 0, ex.Message); }

        double angle = _profile.InvertAngle ? -reading.AngleDeg : reading.AngleDeg;
        _angle = angle;
        _vel.Update(now, angle);
        double vel = _vel.Velocity;
        double acc = _vel.Acceleration;

        // User torque estimate: J*alpha = motor + user  =>  user = J*alpha - motor.
        double jAlpha = _profile.InertiaKgM2 * acc * Units.DegToRad;
        _userTorque = jAlpha - _motorTorque;
        double a = dt <= 0 ? 1 : dt / (0.04 + dt);
        _userTorqueDisplay += a * (_userTorque - _userTorqueDisplay);

        bool armed = Governor.State == SafetyState.Armed;
        var tick = new ActivityTick(now, dt, angle, vel, acc, _motorTorque, _userTorque, Governor.Envelope, armed, Intensity);

        ActivityStep step = ActivityStep.Idle;
        if (Activity is { } act)
        {
            try { step = act.Step(tick); }
            catch (Exception ex)
            {
                Trip(TripCode.InternalError, "Drill error: " + ex.Message);
                step = ActivityStep.Idle;
            }
            foreach (var e in act.DrainEvents()) _events.Enqueue(new EngineEvent(e.Kind, e.Data));
        }

        if (Device is SimulatedForceDevice sim) sim.Driver.GuideTargetDeg = step.GuideTargetDeg;

        double hbAge = now - Volatile.Read(ref _uiHeartbeat);
        var gin = new SafetyInput(angle, vel, reading.Connected, reading.Error, hbAge);
        var gout = Governor.Process(now, gin, step.Request);

        if (gout.StopAllRequested)
        {
            try { Device.StopAll(); } catch { }
            Activity?.OnSafetyStop(Governor.LastTripMessage ?? "Safety stop");
        }

        _motorTorque = gout.TorqueNm;
        double pct = _profile.NmToPercent(gout.TorqueNm);
        if (_profile.InvertForce) pct = -pct;
        _commandPct = Math.Clamp(pct, -100, 100);

        if (Governor.State == SafetyState.Armed)
        {
            try { Device.Apply(new ForceOutput(_commandPct)); }
            catch (Exception ex) { Trip(TripCode.DeviceError, ex.Message); }
        }
        else if (Governor.State == SafetyState.Disarmed && armed)
        {
            // just finished a graceful disarm
            try { Device.Apply(ForceOutput.Zero); Device.Disarm(); } catch { }
        }

        foreach (var se in Governor.DrainEvents())
            _events.Enqueue(new EngineEvent("safety", new { se.Kind, Code = se.Code.ToString(), se.Message }));

        HandleButtons(reading.Buttons);
        UpdateStrength(dt);

        if (double.IsNaN(_lastPublish) || now - _lastPublish >= PublishIntervalS)
        {
            _lastPublish = now;
            Publish(now);
        }
    }

    private void UpdateStrength(double dt)
    {
        var info = Activity as IHandsAware;
        int hands = info?.HandsOnWheel ?? 2;
        double? max = info?.ReferenceMaxNm ?? ReferenceMaxNm;
        if (info != null && info.SetSerial != _lastSetSerial) { _lastSetSerial = info.SetSerial; _budget = 0; }
        var st = StrengthMath.Compute(_profile, _userTorqueDisplay, _vel.Velocity, hands, max, _angle, Norms, BodyMassKg, WristTechnique, _budget);
        if (info?.InWork == true && dt > 0 && st.LimitingPct > 15)
            _budget = Math.Min(999, _budget + dt / st.EnduranceS * 100);
        _strength = st;
    }

    private void Publish(double now)
    {
        var strength = _strength;
        object? act = null;
        try { act = Activity?.Snapshot(); } catch { }
        _latest = new EngineSnapshot(
            now, _angle, _vel.Velocity, _motorTorque, _userTorqueDisplay, _commandPct, Governor.Envelope,
            Governor.State.ToString(), Governor.State == SafetyState.Tripped ? Governor.LastTrip.ToString() : null,
            Governor.State == SafetyState.Tripped ? Governor.LastTripMessage : null,
            _loopHz, _maxGapMs, Intensity, Governor.GlobalCapNm, _profile.MaxTorqueNm,
            Device.IsConnected, Device.Info?.Name, Device.Info?.IsSimulated ?? false, strength, act);
    }

    private void TrackLoopRate(double now, double dt)
    {
        if (double.IsNaN(_windowStart)) _windowStart = now;
        _ticksThisWindow++;
        _maxGapWindow = Math.Max(_maxGapWindow, dt * 1000);
        if (now - _windowStart >= 1.0)
        {
            _loopHz = _ticksThisWindow / (now - _windowStart);
            _maxGapMs = _maxGapWindow;
            _ticksThisWindow = 0;
            _maxGapWindow = 0;
            _windowStart = now;
        }
    }

    private void HandleButtons(uint buttons)
    {
        uint pressed = buttons & ~_lastButtons;
        _lastButtons = buttons;
        if (pressed == 0) return;
        for (int i = 0; i < 32; i++)
        {
            if ((pressed & (1u << i)) == 0) continue;
            Emit("button", i);
            if (!ButtonMap.TryGetValue(i, out var action)) continue;
            switch (action)
            {
                case ButtonAction.IntensityUp: SetIntensity(Intensity + 0.05); break;
                case ButtonAction.IntensityDown: SetIntensity(Intensity - 0.05); break;
                case ButtonAction.Stop: Stop("Wheel button stop"); break;
                case ButtonAction.Next: Emit("next"); (Activity as INextable)?.Next(); break;
            }
        }
    }
}

/// <summary>Activities that know how many hands are on the wheel and the user's reference max torque.</summary>
public interface IHandsAware
{
    int HandsOnWheel { get; }
    double? ReferenceMaxNm { get; }
    /// <summary>True while a working set is running (force on).</summary>
    bool InWork { get; }
    /// <summary>Increments every time a new set starts (resets per-set accumulators).</summary>
    int SetSerial { get; }
}

/// <summary>Activities that react to a "next / skip" input (wheel button, Enter).</summary>
public interface INextable
{
    void Next();
}
