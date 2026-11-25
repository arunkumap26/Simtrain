namespace RimForce.Core.Drills;

public enum Hand { Both, Left, Right }

/// <summary>Which way the USER pushes. "Right" = you turn/hold clockwise, so the wheel pulls you back anticlockwise.</summary>
public enum PushDir { Right, Left, Alternate }

public enum GripStyle { Full, OpenHand, Fingertips, Pinch }

public static class Targets
{
    public const string Forearm = "forearm";
    public const string Grip = "grip";
    public const string Shoulder = "shoulder";
    public const string Biceps = "biceps";
    public const string Reaction = "reaction";
    public const string Endurance = "endurance";
}

public sealed record ParamOption(string Value, string Label);

/// <summary>A tunable drill parameter, rendered by the UI as a slider or segmented control.</summary>
public sealed record ParamSpec(
    string Key,
    string Label,
    double Default,
    double Min,
    double Max,
    double Step,
    string? Unit = null,
    IReadOnlyList<ParamOption>? Options = null,
    string? Help = null);

public sealed record DrillDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Isometric, Dynamic, Eccentric, Reactive, Endurance, Test, Sandbox.</summary>
    public required string Category { get; init; }
    public required string Tagline { get; init; }
    public required string Description { get; init; }
    public required string[] HowTo { get; init; }
    /// <summary>Training targets with weights 0..1 (forearm, grip, shoulder, biceps, reaction, endurance).</summary>
    public required IReadOnlyDictionary<string, double> Targets { get; init; }
    public required string ResistanceProfile { get; init; }
    public IReadOnlyList<ParamSpec> Params { get; init; } = Array.Empty<ParamSpec>();
    public int DefaultSets { get; init; } = 3;
    public double DefaultWorkS { get; init; } = 30;
    public double DefaultRestS { get; init; } = 60;
    /// <summary>Default load as % of your reference max (breakpoint, or device max if untested).</summary>
    public double DefaultLoadPct { get; init; } = 50;
    /// <summary>Hard cap for this drill as % of the device maximum.</summary>
    public double DrillCapPct { get; init; } = 100;
    public bool UsesHandChoice { get; init; }
    public bool UsesDirection { get; init; } = true;
    public PushDir Direction { get; init; } = PushDir.Alternate;
    public bool IsTest { get; init; }
    public bool Endless { get; init; }
    public string ScoreLabel { get; init; } = "score";
    public string Accent { get; init; } = "#38bdf8";
    public string Icon { get; init; } = "hold";
    /// <summary>Science / F1 reference shown in the drill details.</summary>
    public string? Why { get; init; }
}

/// <summary>How the user wants to run a drill (from the Drills page or the program).</summary>
public sealed record DrillConfig
{
    public required string DrillId { get; init; }
    public double LoadPct { get; init; } = 50;
    public int Sets { get; init; } = 3;
    public double WorkS { get; init; } = 30;
    public double RestS { get; init; } = 60;
    public Hand Hand { get; init; } = Hand.Both;
    public PushDir Direction { get; init; } = PushDir.Alternate;
    public Dictionary<string, double> Params { get; init; } = new();
    /// <summary>Label in a session ("Warm-up", "Working", "Cool-down", "Test").</summary>
    public string Role { get; init; } = "Working";

    public double P(string key, double fallback) => Params.TryGetValue(key, out var v) ? v : fallback;
}

/// <summary>What the UI draws as the ghost: where the wheel should be now and soon.</summary>
public sealed record GuideFrame
{
    public double TargetDeg { get; init; }
    public double ZoneDeg { get; init; } = 3;
    /// <summary>Future target angles sampled every PathStepS seconds (for the runway / "track ahead" view).</summary>
    public double[] Path { get; init; } = Array.Empty<double>();
    public double PathStepS { get; init; } = 0.1;
    public string Cue { get; init; } = "";
    public string? SubCue { get; init; }
    /// <summary>Sign of the torque the wheel applies (-1 pulls left, +1 pulls right, 0 none).</summary>
    public int PullDir { get; init; }
    public double? TargetSpeedDps { get; init; }
    /// <summary>Progress through the current rep / segment 0..1 (for ring animations).</summary>
    public double RepProgress { get; init; }
    public bool ShowGhost { get; init; } = true;
    public string? Hand { get; init; }
    public string? Grip { get; init; }
    /// <summary>"press" or "lift" when one hand is used at 3/9 o'clock.</summary>
    public string? Action { get; init; }
    public double? MarkerLeft { get; init; }
    public double? MarkerRight { get; init; }
}

public sealed record SetResult
{
    public required string DrillId { get; init; }
    public required string DrillName { get; init; }
    public int ItemIndex { get; init; }
    public int SetIndex { get; init; }
    public string Role { get; init; } = "Working";
    public Hand Hand { get; init; }
    public PushDir Direction { get; init; }
    public string? Grip { get; init; }
    public double LoadNm { get; init; }
    public double LoadPct { get; init; }
    public double Intensity { get; init; } = 1;
    public double WorkS { get; init; }
    public bool Completed { get; init; }
    public double Score { get; init; }
    public string ScoreLabel { get; init; } = "score";
    public Dictionary<string, double> Metrics { get; init; } = new();
    /// <summary>Downsampled trace: [t, angle, target, torque] repeated.</summary>
    public float[] Trace { get; init; } = Array.Empty<float>();
    public DateTime StartedUtc { get; init; }
    public DateTime EndedUtc { get; init; }
    public string? Note { get; init; }
}
