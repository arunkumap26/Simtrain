using RimForce.Core.Engine;
using RimForce.Core.Hardware;
using RimForce.Core.Safety;

namespace RimForce.Core.Settings;

/// <summary>Everything the user configures. Persisted as JSON in the SQLite settings table.</summary>
public sealed record AppSettings
{
    public WheelProfile Wheel { get; init; } = new();
    public SafetySettings Safety { get; init; } = new();
    public bool ChecklistAccepted { get; init; }
    public bool DirectionVerified { get; init; }
    public string? DeviceId { get; init; }
    /// <summary>"kgf", "N" or "lbf" for force readouts.</summary>
    public string ForceUnit { get; init; } = "kgf";
    public bool AudioEnabled { get; init; } = true;
    public double AudioVolume { get; init; } = 0.7;
    public bool VoiceCues { get; init; } = true;
    public int SessionsPerWeek { get; init; } = 3;
    /// <summary>Force loop rate (Hz). 360 Hz like irFFB/MAIRA; ~500 Hz is at the practical limit of many bases.</summary>
    public double LoopHz { get; init; } = 360;
    /// <summary>DirectInput cooperative level: "foreground" (safer, the default) or "background".</summary>
    public string CooperativeLevel { get; init; } = "foreground";
    /// <summary>Use native DirectInput spring/damper/friction instead of software rendering (Device Test only by default).</summary>
    public bool NativeConditionEffects { get; init; }
    /// <summary>Wheel button index → action.</summary>
    public Dictionary<int, ButtonAction> Buttons { get; init; } = new();
    /// <summary>Master intensity remembered between sessions.</summary>
    public double Intensity { get; init; } = 1.0;
    /// <summary>"arms" (push-pull with shoulders) or "wrists" (elbows tucked, forearm rotation only). Changes strength comparisons.</summary>
    public string Technique { get; init; } = "arms";
    public double? BodyMassKg { get; init; }
    /// <summary>Which reference strength norms the "% of typical" readouts compare against: "men" or "women".</summary>
    public string Norms { get; init; } = "men";
    /// <summary>First-run wizard finished.</summary>
    public bool OnboardingDone { get; init; }

    public AppSettings Sanitised() => this with
    {
        Safety = Safety.Sanitised(),
        Wheel = Wheel with
        {
            BaseMaxTorqueNm = Math.Clamp(Wheel.BaseMaxTorqueNm, 1, 40),
            SimProStrengthPct = Math.Clamp(Wheel.SimProStrengthPct, 0, 100),
            RimDiameterMm = Math.Clamp(Wheel.RimDiameterMm, 200, 400),
            RotationRangeDeg = Math.Clamp(Wheel.RotationRangeDeg, 180, 2520),
            InertiaKgM2 = Math.Clamp(Wheel.InertiaKgM2, 0.005, 0.5),
            HandRimFriction = Math.Clamp(Wheel.HandRimFriction, 0.2, 1.5),
        },
        ForceUnit = ForceUnit is "kgf" or "N" or "lbf" ? ForceUnit : "kgf",
        AudioVolume = Math.Clamp(AudioVolume, 0, 1),
        SessionsPerWeek = Math.Clamp(SessionsPerWeek, 2, 3),
        LoopHz = Math.Clamp(LoopHz, 250, 1000),
        CooperativeLevel = CooperativeLevel == "background" ? "background" : "foreground",
        Intensity = Math.Clamp(Intensity, 0.1, 1.5),
        Technique = Technique == "wrists" ? "wrists" : "arms",
        Norms = Norms == "women" ? "women" : "men",
    };
}
