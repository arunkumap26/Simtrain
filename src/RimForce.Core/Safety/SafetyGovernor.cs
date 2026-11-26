using RimForce.Core.Hardware;

namespace RimForce.Core.Safety;

/// <summary>Torque wanted by the active drill for this tick, split by how it may be limited.</summary>
public readonly record struct TorqueRequest(
    /// <summary>Open-loop torque (constant loads, pulses, vibration). Slew-rate limited. Nm, + = clockwise.</summary>
    double OpenLoopNm,
    /// <summary>State-dependent torque (software spring/damper/friction). Clamped but not slew limited.</summary>
    double ReactiveNm,
    /// <summary>Per-drill cap (Nm). The lower of this and the global cap wins.</summary>
    double DrillCapNm,
    /// <summary>True while a set is running and force should be present (drives the soft-start envelope).</summary>
    bool ForceWanted,
    /// <summary>Optional per-drill runaway threshold (deg/s) for drills that move the wheel briskly (e.g. snap catch).</summary>
    double? RunawaySpeedOverrideDps = null,
    /// <summary>Small zero-mean vibration (kerbs). Not slew limited, but clamped to min(MaxVibrationNm, 25% of cap).</summary>
    double VibrationNm = 0,
    /// <summary>
    /// Sprint drills only: raises the hands-off speed threshold (max 720 deg/s) but ONLY while the motor is braking
    /// the motion. Whenever the motor pushes the way the wheel moves, the normal threshold applies.
    /// </summary>
    double? HandsOffSpeedWhenBrakingDps = null)
{
    public static readonly TorqueRequest None = new(0, 0, 0, false);
}

public readonly record struct SafetyInput(
    double AngleDeg,
    double VelocityDps,
    bool DeviceConnected,
    string? DeviceError,
    double UiHeartbeatAgeS);

public readonly record struct GovernorOutput(double TorqueNm, double Envelope, bool StopAllRequested);

/// <summary>
/// Every force command passes through here. Owns the arm/disarm/trip state machine and enforces:
///  1 arm required, immediate stop on user stop / focus loss / error / disconnect
///  2 global + per-drill caps
///  3 soft start (>=1.5 s), soft end (0.3 s), open-loop slew limit (full cap in >=80 ms)
///  4 hands-off detection (speed) + runaway detection (motor winning) + overspeed fade
///  5 angle limit near the lock
///  6 force-loop watchdog
///  9 UI heartbeat
/// Thread-safe: the force loop calls Process; the watchdog thread and UI thread call Trip/Stop.
/// </summary>
public sealed class SafetyGovernor
{
    private readonly object _lock = new();
    private SafetySettings _s;
    private WheelProfile _profile;

    private double _envelope;
    private double _lastTorque;
    /// <summary>Below this motor torque the wheel cannot be "running away" (Nm).</summary>
    public const double HandsOffMinNm = 0.3;
    /// <summary>Absolute ceiling for the sprint-drill hands-off threshold.</summary>
    public const double MaxSprintSpeedDps = 720;
    private double _openLoopOut;
    private double _lastTick = double.NaN;
    private double _overSpeedSince = double.NaN;
    private double _runawaySince = double.NaN;
    private bool _rampingDown;
    private readonly List<SafetyEvent> _events = new();

    public SafetyGovernor(SafetySettings settings, WheelProfile profile)
    {
        _s = settings.Sanitised();
        _profile = profile;
    }

    public SafetyState State { get; private set; } = SafetyState.Disarmed;
    public TripCode LastTrip { get; private set; }
    public string? LastTripMessage { get; private set; }
    public double Envelope { get { lock (_lock) return _envelope; } }
    public double LastTickTime { get { lock (_lock) return _lastTick; } }

    /// <summary>Set by the app when the one-time safety checklist has been accepted.</summary>
    public bool ChecklistAccepted { get; set; }
    /// <summary>Set once the force-direction check passed (not needed for the simulator).</summary>
    public bool DirectionVerified { get; set; }
    public bool RequireDirectionCheck { get; set; } = true;

    public SafetySettings Settings
    {
        get { lock (_lock) return _s; }
        set { lock (_lock) _s = value.Sanitised(); }
    }

    public WheelProfile Profile
    {
        get { lock (_lock) return _profile; }
        set { lock (_lock) _profile = value; }
    }

    public double GlobalCapNm { get { lock (_lock) return _profile.MaxTorqueNm * EffectiveCapPct / 100.0; } }

    /// <summary>True while armed in low-force test mode (device test / direction check).</summary>
    public bool TestMode { get; private set; }
    public const double TestModeCapPct = 10;
    public const double MaxVibrationNm = 2.0;

    private double? _capOverridePct;

    /// <summary>
    /// Temporary cap (% of device max) that replaces the global cap, e.g. 100 during a confirmed Max Strength Test.
    /// Null = use the global cap. Test mode (device tests) still wins and stays at 10%.
    /// </summary>
    public double? CapOverridePct
    {
        get { lock (_lock) return _capOverridePct; }
        set { lock (_lock) _capOverridePct = value is { } v ? Math.Clamp(v, 0, 100) : null; }
    }

    private double EffectiveCapPct => TestMode ? Math.Min(_s.GlobalCapPct, TestModeCapPct) : (_capOverridePct ?? _s.GlobalCapPct);

    /// <summary>
    /// Arm for the Device Test page: does not need the direction check, but the cap is forced down to 10%
    /// until the next disarm/stop.
    /// </summary>
    public bool TryArmTestMode(double now, double angleDeg, double velocityDps, bool deviceConnected, out string? reason)
    {
        bool savedReq = RequireDirectionCheck;
        RequireDirectionCheck = false;
        try
        {
            if (!TryArm(now, angleDeg, velocityDps, deviceConnected, out reason)) return false;
            lock (_lock) TestMode = true;
            return true;
        }
        finally { RequireDirectionCheck = savedReq; }
    }

    public bool TryArm(double now, double angleDeg, double velocityDps, bool deviceConnected, out string? reason)
    {
        lock (_lock)
        {
            reason = null;
            if (State == SafetyState.Tripped) reason = $"Safety stop active ({LastTripMessage}). Acknowledge it first.";
            else if (!ChecklistAccepted) reason = "Complete the safety checklist first.";
            else if (RequireDirectionCheck && !DirectionVerified) reason = "Run the force direction check on the Device page first.";
            else if (!deviceConnected) reason = "No wheel connected.";
            else if (Math.Abs(velocityDps) > _s.ArmMaxSpeedDps) reason = "Hold the wheel still to arm.";
            else if (Math.Abs(angleDeg) > _profile.LockDeg - _s.LockMarginDeg) reason = "Bring the wheel back towards centre to arm.";
            if (reason != null) return false;

            State = SafetyState.Armed;
            TestMode = false;
            _lastTorque = 0;
            _envelope = 0;
            _openLoopOut = 0;
            _rampingDown = false;
            _lastTick = now;
            _overSpeedSince = _runawaySince = double.NaN;
            _events.Add(new SafetyEvent(now, "armed", TripCode.None, "Armed"));
            return true;
        }
    }

    /// <summary>Graceful disarm: force ramps out over RampDownS, then the state becomes Disarmed.</summary>
    public void RequestDisarm(double now)
    {
        lock (_lock)
        {
            if (State != SafetyState.Armed) return;
            _rampingDown = true;
            if (_envelope <= 0) Finish(now);
        }
    }

    /// <summary>Immediate stop (Esc / Space / STOP button). No ramp.</summary>
    public void UserStop(double now) => Trip(now, TripCode.UserStop, "Stopped");

    /// <summary>Immediate stop with a reason. Output goes to zero this instant and the device is told to StopAll.</summary>
    public void Trip(double now, TripCode code, string message)
    {
        lock (_lock)
        {
            bool wasActive = State == SafetyState.Armed;
            _envelope = 0;
            _openLoopOut = 0;
            _rampingDown = false;
            if (code == TripCode.UserStop)
            {
                // A user stop is not a fault: no acknowledgement needed.
                State = SafetyState.Disarmed;
                _events.Add(new SafetyEvent(now, "stopped", code, message));
                return;
            }
            if (!wasActive && State == SafetyState.Tripped) return;
            if (!wasActive && State == SafetyState.Disarmed && code is TripCode.FocusLost or TripCode.UiHeartbeat) return;
            State = SafetyState.Tripped;
            LastTrip = code;
            LastTripMessage = message;
            _events.Add(new SafetyEvent(now, "tripped", code, message));
        }
    }

    public void AcknowledgeTrip(double now)
    {
        lock (_lock)
        {
            if (State != SafetyState.Tripped) return;
            State = SafetyState.Disarmed;
            _events.Add(new SafetyEvent(now, "acknowledged", LastTrip, "Safety stop acknowledged"));
        }
    }

    /// <summary>Called by the independent watchdog thread. Returns true if it tripped.</summary>
    public bool CheckWatchdog(double now)
    {
        lock (_lock)
        {
            if (State != SafetyState.Armed || double.IsNaN(_lastTick)) return false;
            if (now - _lastTick <= _s.WatchdogS) return false;
        }
        Trip(now, TripCode.Watchdog, "Force loop stalled");
        return true;
    }

    public IReadOnlyList<SafetyEvent> DrainEvents()
    {
        lock (_lock)
        {
            if (_events.Count == 0) return Array.Empty<SafetyEvent>();
            var copy = _events.ToArray();
            _events.Clear();
            return copy;
        }
    }

    /// <summary>One force-loop tick. Returns the torque the device may output right now.</summary>
    public GovernorOutput Process(double now, in SafetyInput input, in TorqueRequest req)
    {
        SafetySettings s;
        WheelProfile p;
        double envelope, openOut, dt;
        bool rampingDown;
        lock (_lock)
        {
            dt = double.IsNaN(_lastTick) ? 0 : now - _lastTick;
            _lastTick = now;
            if (State != SafetyState.Armed)
                return new GovernorOutput(0, 0, false);
            s = _s; p = _profile; envelope = _envelope; openOut = _openLoopOut; rampingDown = _rampingDown;
        }

        // ---- rule checks that trip immediately ----
        if (dt > s.WatchdogS) { Trip(now, TripCode.Watchdog, $"Force loop stalled ({dt * 1000:0} ms)"); return Stopped(); }
        if (!input.DeviceConnected) { Trip(now, TripCode.Disconnected, "Wheel disconnected"); return Stopped(); }
        if (input.DeviceError != null) { Trip(now, TripCode.DeviceError, "Device error: " + input.DeviceError); return Stopped(); }
        if (s.RequireUiHeartbeat && input.UiHeartbeatAgeS > s.UiHeartbeatTimeoutS)
        { Trip(now, TripCode.UiHeartbeat, "App window stopped responding"); return Stopped(); }

        bool forceActive = envelope > 0.001 || Math.Abs(openOut) > 0.05;
        double absV = Math.Abs(input.VelocityDps);

        // Rule 5: angle limit.
        if (forceActive && Math.Abs(input.AngleDeg) > p.LockDeg - s.LockMarginDeg)
        { Trip(now, TripCode.AngleLimit, $"Wheel too close to the lock ({input.AngleDeg:0}°)"); return Stopped(); }

        // Rule 4: hands off = sustained overspeed while force is active (|torque| >= 0.3 Nm). Sprint drills may
        // raise the threshold, but only while the motor brakes the motion (see DECISIONS.md).
        double lastTq;
        lock (_lock) lastTq = _lastTorque;
        bool torqueActive = Math.Abs(lastTq) >= HandsOffMinNm;
        bool braking = lastTq * input.VelocityDps < 0;
        double handsOffSpeed = braking && req.HandsOffSpeedWhenBrakingDps is { } sprint
            ? Math.Clamp(sprint, s.HandsOffSpeedDps, MaxSprintSpeedDps)
            : s.HandsOffSpeedDps;
        if (forceActive && torqueActive && absV > handsOffSpeed)
        {
            if (double.IsNaN(_overSpeedSince)) _overSpeedSince = now;
            if (now - _overSpeedSince >= s.HandsOffTimeS)
            { Trip(now, TripCode.HandsOff, "Hands off detected"); return Stopped(); }
        }
        else _overSpeedSince = double.NaN;

        double tickDt = Math.Clamp(dt, 0, 0.05);

        // Rule 3: soft start / soft end envelope.
        double target = (req.ForceWanted && !rampingDown) ? 1.0 : 0.0;
        if (target > envelope) envelope = Math.Min(target, envelope + tickDt / s.RampUpS);
        else if (target < envelope) envelope = Math.Max(target, envelope - tickDt / s.RampDownS);

        // Rule 2: caps.
        double capPct;
        lock (_lock) capPct = EffectiveCapPct;
        double cap = Math.Max(0, Math.Min(p.MaxTorqueNm * capPct / 100.0, req.DrillCapNm));

        // Rule 3b: open-loop slew limit, the full cap can be crossed no faster than MinPulseRampS.
        double maxStep = cap / s.MinPulseRampS * tickDt;
        double wantOpen = Math.Clamp(req.OpenLoopNm, -cap, cap);
        openOut += Math.Clamp(wantOpen - openOut, -maxStep, maxStep);
        openOut = Math.Clamp(openOut, -cap, cap);

        double vibLimit = Math.Min(MaxVibrationNm, 0.25 * cap);
        double torque = (openOut + Math.Clamp(req.ReactiveNm, -cap, cap) + Math.Clamp(req.VibrationNm, -vibLimit, vibLimit)) * envelope;
        torque = Math.Clamp(torque, -cap, cap);

        double rawTorque = torque; // before the fade: what the drill is trying to do
        // Rule 7 support: overspeed fade when the motor is the one moving the wheel.
        bool motorWinning = torque * input.VelocityDps > 0;
        if (motorWinning && absV > s.OverspeedFadeStartDps)
        {
            double k = 1.0 - (absV - s.OverspeedFadeStartDps) / Math.Max(1, s.HandsOffSpeedDps - s.OverspeedFadeStartDps);
            torque *= Math.Clamp(k, 0, 1);
        }

        // Rule 4b: runaway (motor winning, fast, with meaningful torque).
        double runawaySpeed = Math.Min(s.HandsOffSpeedDps, req.RunawaySpeedOverrideDps ?? s.RunawaySpeedDps);
        if (s.RunawayDetection && motorWinning && absV > runawaySpeed && Math.Abs(torque) > s.RunawayMinNm)
        {
            if (double.IsNaN(_runawaySince)) _runawaySince = now;
            if (now - _runawaySince >= s.RunawayTimeS)
            { Trip(now, TripCode.Runaway, "Hands off detected (wheel running away)"); return Stopped(); }
        }
        else _runawaySince = double.NaN;

        lock (_lock)
        {
            if (State != SafetyState.Armed) return Stopped(); // tripped from another thread meanwhile
            _envelope = envelope;
            _openLoopOut = openOut;
            _lastTorque = rawTorque;
            if (_rampingDown && envelope <= 0) { Finish(now); _lastTorque = 0; return new GovernorOutput(0, 0, false); }
        }
        return new GovernorOutput(torque, envelope, false);
    }

    private static GovernorOutput Stopped() => new(0, 0, true);

    private void Finish(double now)
    {
        State = SafetyState.Disarmed;
        _rampingDown = false;
        _envelope = 0;
        _openLoopOut = 0;
        _events.Add(new SafetyEvent(now, "disarmed", TripCode.None, "Disarmed"));
    }
}
