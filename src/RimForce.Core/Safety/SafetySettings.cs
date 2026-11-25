namespace RimForce.Core.Safety;

/// <summary>Every number that keeps the wheel from hurting someone. Defaults are deliberately conservative.</summary>
public sealed record SafetySettings
{
    /// <summary>Global cap as % of the device maximum (DI 100% x SimPro strength). Default 40% on first run.</summary>
    public double GlobalCapPct { get; init; } = 40;

    /// <summary>Force ramp-in when a set starts (s). Never below 1.5 s.</summary>
    public double RampUpS { get; init; } = 1.5;
    /// <summary>Force ramp-out when a set ends normally (s).</summary>
    public double RampDownS { get; init; } = 0.3;
    /// <summary>Open-loop force (constant + pulses) can change at most cap / this many seconds. Never below 0.08 s.</summary>
    public double MinPulseRampS { get; init; } = 0.08;

    /// <summary>Rule 4: wheel faster than this for longer than HandsOffTimeS while force is active = hands off.</summary>
    public double HandsOffSpeedDps { get; init; } = 400;
    public double HandsOffTimeS { get; init; } = 0.05;

    /// <summary>Extra rule: the motor is winning (torque and motion same direction) faster than this = runaway.</summary>
    public bool RunawayDetection { get; init; } = true;
    public double RunawaySpeedDps { get; init; } = 180;
    public double RunawayTimeS { get; init; } = 0.025;
    public double RunawayMinNm { get; init; } = 0.8;

    /// <summary>Extra rule: when the motor is winning above this speed its torque fades linearly to zero at HandsOffSpeedDps.</summary>
    public double OverspeedFadeStartDps { get; init; } = 250;

    /// <summary>Rule 5: trip when the wheel gets within this many degrees of the lock.</summary>
    public double LockMarginDeg { get; init; } = 20;

    /// <summary>Rule 6: trip if the force loop misses this long.</summary>
    public double WatchdogS { get; init; } = 0.1;
    /// <summary>Rule 6b: give DirectInput effects a finite duration refreshed every loop so a frozen app lets force lapse.</summary>
    public bool FiniteEffectDurations { get; init; } = true;
    public double EffectDurationS { get; init; } = 0.25;

    /// <summary>Trip if the UI stops sending heartbeats (crashed / frozen window).</summary>
    public bool RequireUiHeartbeat { get; init; } = true;
    public double UiHeartbeatTimeoutS { get; init; } = 1.5;

    /// <summary>Trip when the app window loses focus.</summary>
    public bool StopOnFocusLoss { get; init; } = true;

    /// <summary>Arming is refused while the wheel is moving faster than this.</summary>
    public double ArmMaxSpeedDps { get; init; } = 30;

    public SafetySettings Sanitised() => this with
    {
        GlobalCapPct = Math.Clamp(GlobalCapPct, 0, 100),
        RampUpS = Math.Max(1.5, RampUpS),
        RampDownS = Math.Clamp(RampDownS, 0.05, 2),
        MinPulseRampS = Math.Max(0.08, MinPulseRampS),
        HandsOffSpeedDps = Math.Clamp(HandsOffSpeedDps, 100, 1000),
        HandsOffTimeS = Math.Clamp(HandsOffTimeS, 0.01, 0.2),
        RunawaySpeedDps = Math.Clamp(RunawaySpeedDps, 60, HandsOffSpeedDps),
        RunawayTimeS = Math.Clamp(RunawayTimeS, 0.005, 0.2),
        OverspeedFadeStartDps = Math.Clamp(OverspeedFadeStartDps, 50, HandsOffSpeedDps),
        LockMarginDeg = Math.Clamp(LockMarginDeg, 5, 180),
        WatchdogS = Math.Clamp(WatchdogS, 0.02, 0.5),
        EffectDurationS = Math.Clamp(EffectDurationS, 0.05, 2),
        UiHeartbeatTimeoutS = Math.Clamp(UiHeartbeatTimeoutS, 0.5, 10),
        ArmMaxSpeedDps = Math.Clamp(ArmMaxSpeedDps, 5, 200),
    };
}

public enum SafetyState { Disarmed, Armed, Tripped }

public enum TripCode
{
    None,
    UserStop,
    HandsOff,
    Runaway,
    AngleLimit,
    Watchdog,
    DeviceError,
    Disconnected,
    FocusLost,
    UiHeartbeat,
    InternalError,
}

public sealed record SafetyEvent(double Time, string Kind, TripCode Code, string Message);
