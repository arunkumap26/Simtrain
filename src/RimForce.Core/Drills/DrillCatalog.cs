using RimForce.Core.Drills.Logics;

namespace RimForce.Core.Drills;

public static class DrillCatalog
{
    private static Dictionary<string, double> T(double forearm, double grip, double shoulder, double biceps, double reaction = 0, double endurance = 0) => new()
    {
        [Targets.Forearm] = forearm,
        [Targets.Grip] = grip,
        [Targets.Shoulder] = shoulder,
        [Targets.Biceps] = biceps,
        [Targets.Reaction] = reaction,
        [Targets.Endurance] = endurance,
    };

    private static readonly ParamOption[] Angles = { new("0", "0°"), new("45", "45°"), new("90", "90°") };

    public static readonly IReadOnlyList<DrillDefinition> All = new List<DrillDefinition>
    {
        new()
        {
            Id = "corner-hold", Name = "Corner Hold", Category = "Isometric", Icon = "hold", Accent = "#22d3ee",
            Tagline = "Hold the line through a long, loaded corner.",
            Description = "Constant torque pulls the wheel back towards centre, exactly like self-aligning torque mid-corner. Turn to the target and keep the wheel inside the zone.",
            HowTo = new[] { "Hands at 9 and 3, thumbs OUTSIDE the spokes.", "Turn to the ghost wheel and match it.", "Keep the needle inside the zone until the timer ends.", "Breathe — don't brace your neck." },
            Targets = T(1.0, 0.6, 0.8, 0.2), ResistanceProfile = "constant",
            Params = new[]
            {
                new ParamSpec("angle", "Target angle", 45, 0, 90, 45, "°", Angles),
                new ParamSpec("zone", "Zone", 3, 2, 8, 1, "±°"),
            },
            DefaultSets = 4, DefaultWorkS = 30, DefaultRestS = 60, DefaultLoadPct = 50,
            Why = "Isometric holds build strength at the trained joint angle (±~15°), so we train at the angles you actually corner at.",
        },
        new()
        {
            Id = "one-hand-hold", Name = "One-Hand Hold", Category = "Grip", Icon = "hand", Accent = "#a78bfa",
            Tagline = "All the torque through one hand. The main grip builder.",
            Description = "Same as a corner hold but one hand does all the work, doubling the force per hand. At 3 o'clock a clockwise push is a press-down; at 9 o'clock it is a lift.",
            HowTo = new[] { "Use only the hand shown, at 3 or 9 o'clock.", "Other hand resting on your thigh, not the wheel.", "Match the ghost and hold.", "Switch hands next set." },
            Targets = T(1.0, 1.0, 0.7, 0.3), ResistanceProfile = "constant", UsesHandChoice = true,
            Params = new[]
            {
                new ParamSpec("angle", "Target angle", 0, 0, 90, 45, "°", Angles),
                new ParamSpec("zone", "Zone", 4, 2, 8, 1, "±°"),
            },
            DefaultSets = 4, DefaultWorkS = 25, DefaultRestS = 60, DefaultLoadPct = 40,
            Why = "Wrist extensors (ECR/ECU) fatigue fastest in kart drivers; one-handed holds load them hardest with a capped motor.",
        },
        new()
        {
            Id = "grip-variants", Name = "Grip Variants", Category = "Grip", Icon = "grip", Accent = "#a78bfa",
            Tagline = "Open hand, fingertips, pinch — make the rim feel twice as heavy.",
            Description = "Corner holds with a different grip every set. Weaker grips need less torque to be hard, so loads scale down automatically (open 85%, fingertips 70%, pinch 60%).",
            HowTo = new[] { "Use the grip shown on screen.", "Fingertips: no palm contact.", "Pinch: thumb against fingers on the rim.", "Stop if the rim slips out of your hand." },
            Targets = T(0.8, 1.0, 0.4, 0.1), ResistanceProfile = "constant",
            Params = new[]
            {
                new ParamSpec("angle", "Target angle", 0, 0, 90, 45, "°", Angles),
                new ParamSpec("zone", "Zone", 4, 2, 8, 1, "±°"),
            },
            DefaultSets = 4, DefaultWorkS = 20, DefaultRestS = 45, DefaultLoadPct = 45,
        },
        new()
        {
            Id = "lock-to-lock", Name = "Lock-to-Lock Sprints", Category = "Power", Icon = "sprint", Accent = "#fb923c",
            Tagline = "The Red Bull weighted-wheel test: end to end, as fast as you can.",
            Description = "A heavy damper: the faster you turn, the harder it pushes back. Sprint between the two markers and count reps.",
            HowTo = new[] { "Both hands, firm grip.", "Turn to the lit marker, then the other one.", "Full range every rep — touch both markers.", "Go all-out for the whole set." },
            Targets = T(0.8, 0.5, 1.0, 0.3), ResistanceProfile = "damper", UsesDirection = false,
            Params = new[]
            {
                new ParamSpec("amplitude", "Markers at", 90, 45, 180, 15, "±°"),
                new ParamSpec("refSpeed", "Load reached at", 360, 120, 720, 30, "°/s", Help: "Lower = heavier at slow speeds."),
            },
            DefaultSets = 3, DefaultWorkS = 20, DefaultRestS = 90, DefaultLoadPct = 60, ScoreLabel = "reps",
            Why = "Red Bull's Performance Centre tests drivers by moving a weighted wheel left to right as fast as possible.",
        },
        new()
        {
            Id = "slow-return", Name = "Slow Return", Category = "Eccentric", Icon = "return", Accent = "#34d399",
            Tagline = "Push out against a spring, then let it win — slowly.",
            Description = "A spring pulls to centre. Turn out to the ghost, hold, then follow the ghost back over 3-4 s. The controlled return is eccentric work for shoulders and forearms.",
            HowTo = new[] { "Follow the ghost out.", "Hold one second.", "Let the wheel come back exactly as slowly as the ghost.", "Never let go during the return." },
            Targets = T(0.8, 0.5, 0.9, 0.5), ResistanceProfile = "spring",
            Params = new[]
            {
                new ParamSpec("amplitude", "Turn out to", 90, 45, 135, 15, "°"),
                new ParamSpec("returnS", "Return time", 3.5, 2, 6, 0.5, "s"),
            },
            DefaultSets = 3, DefaultWorkS = 45, DefaultRestS = 75, DefaultLoadPct = 55,
            Why = "Eccentric (lowering) work tolerates higher loads than concentric work and builds tendon resilience.",
        },
        new()
        {
            Id = "snap-catch", Name = "Snap Catch", Category = "Reactive", Icon = "snap", Accent = "#f472b6",
            Tagline = "Random kicks — like a snap of oversteer. Catch it inside ±5°.",
            Description = "Random left/right torque pulses at random times on top of a light base load. Keep the wheel centred. Trains wrist stability and reaction.",
            HowTo = new[] { "Hold centre with a relaxed but ready grip.", "When it kicks, catch it fast.", "Return to centre and settle.", "Score = how little it moved." },
            Targets = T(0.8, 0.8, 0.5, 0.1, reaction: 1.0), ResistanceProfile = "pulses", DrillCapPct = 70,
            Params = new[]
            {
                new ParamSpec("zone", "Catch zone", 5, 3, 10, 1, "±°"),
                new ParamSpec("baseFrac", "Base load", 0.25, 0, 0.5, 0.05, "×", Help: "Constant pre-load between kicks."),
                new ParamSpec("maxGapS", "Max gap", 2.6, 1.2, 5, 0.2, "s"),
            },
            DefaultSets = 3, DefaultWorkS = 30, DefaultRestS = 60, DefaultLoadPct = 30, ScoreLabel = "score",
        },
        new()
        {
            Id = "kerb-hold", Name = "Kerb Hold", Category = "Isometric", Icon = "kerb", Accent = "#22d3ee",
            Tagline = "Hold the corner while you ride the kerbs.",
            Description = "A corner hold with bursts of vibration like hitting a sausage kerb. Rendered in software, so it works even if the base has no periodic effects.",
            HowTo = new[] { "Hold the ghost angle.", "Don't let the buzz move the wheel.", "Relax your shoulders, grip with your hands." },
            Targets = T(0.9, 1.0, 0.6, 0.1, endurance: 0.4), ResistanceProfile = "constant+vibration",
            Params = new[]
            {
                new ParamSpec("angle", "Target angle", 45, 0, 90, 45, "°", Angles),
                new ParamSpec("zone", "Zone", 4, 2, 8, 1, "±°"),
                new ParamSpec("vibHz", "Kerb frequency", 24, 10, 40, 2, "Hz"),
                new ParamSpec("vibNm", "Kerb strength", 0.8, 0.2, 2, 0.1, "Nm"),
            },
            DefaultSets = 3, DefaultWorkS = 30, DefaultRestS = 60, DefaultLoadPct = 45,
        },
        new()
        {
            Id = "stint", Name = "Race Stint", Category = "Endurance", Icon = "stint", Accent = "#fbbf24",
            Tagline = "Lap after lap of corner-shaped loads. Follow the racing line.",
            Description = "A looping lap: corners ramp in, hold and ramp out while straights let you breathe. The runway shows the corners coming. 10-40 minutes.",
            HowTo = new[] { "Follow the ghost — it is the racing line.", "Watch the track ahead for the next corner.", "Relax your grip on the straights.", "Consistency beats heroics." },
            Targets = T(1.0, 0.8, 0.7, 0.2, endurance: 1.0), ResistanceProfile = "lap",
            Params = new[]
            {
                new ParamSpec("lap", "Lap", 0, 0, 1, 1, null, new[] { new ParamOption("0", "High-speed"), new ParamOption("1", "Street") }),
                new ParamSpec("minutes", "Stint length", 10, 2, 40, 1, "min"),
                new ParamSpec("zone", "Line width", 6, 3, 12, 1, "±°"),
            },
            DefaultSets = 1, DefaultWorkS = 600, DefaultRestS = 0, DefaultLoadPct = 45, UsesDirection = false, Endless = true,
            Why = "Drivers' forearm muscles show measurable fatigue after short sessions; endurance under repeated loads is the limiter.",
        },
        new()
        {
            Id = "breakpoint", Name = "Breakpoint Test", Category = "Test", Icon = "test", Accent = "#f43f5e", IsTest = true,
            Tagline = "How much can you hold? The benchmark everything scales from.",
            Description = "Hold 0° while torque rises 1% per second. The breakpoint is the torque at which the wheel leaves ±5° for more than half a second. Test each direction (and each hand).",
            HowTo = new[] { "Warm up first.", "Hold dead centre.", "Fight it as long as you can.", "Force fades out automatically when you lose it." },
            Targets = T(1.0, 0.8, 0.8, 0.3), ResistanceProfile = "ramp", UsesHandChoice = true,
            Params = new[]
            {
                new ParamSpec("rampPctPerS", "Ramp rate", 1.0, 0.5, 3, 0.25, "%/s"),
                new ParamSpec("startPct", "Start at", 15, 5, 40, 5, "%"),
            },
            DefaultSets = 2, DefaultWorkS = 120, DefaultRestS = 120, DefaultLoadPct = 100, ScoreLabel = "Nm",
            Direction = PushDir.Alternate,
        },
        new()
        {
            Id = "iso-speed", Name = "Iso-Speed Sweeps", Category = "Power", Icon = "iso", Accent = "#fb923c",
            Tagline = "Isokinetic-style: same speed, maximum pressure, whole range.",
            Description = "The ghost sweeps at a fixed speed. A damper is tuned so that keeping up with it means pushing the prescribed load through the whole range — like an isokinetic dynamometer.",
            HowTo = new[] { "Stay on the ghost.", "Push steadily — not in bursts.", "Even pressure both directions." },
            Targets = T(0.9, 0.5, 0.9, 0.4), ResistanceProfile = "damper",
            Params = new[]
            {
                new ParamSpec("speedDps", "Sweep speed", 90, 30, 240, 15, "°/s"),
                new ParamSpec("amplitude", "Range", 75, 30, 135, 15, "±°"),
            },
            DefaultSets = 3, DefaultWorkS = 30, DefaultRestS = 60, DefaultLoadPct = 55,
            Why = "F1 drivers reportedly train on isokinetic steering dynamometers (IsoMed-type) that hold a fixed speed.",
        },
        new()
        {
            Id = "apex-hunter", Name = "Apex Hunter", Category = "Reactive", Icon = "apex", Accent = "#f472b6",
            Tagline = "Targets pop up. Get there fast, hold it, next.",
            Description = "A spring pulls to centre while apex targets appear at random angles. Reach each one and hold it for a second. Scores reaction time and capture speed.",
            HowTo = new[] { "Watch for the next apex.", "Move decisively, stop precisely.", "Hold inside the ring until it fills." },
            Targets = T(0.7, 0.5, 0.8, 0.3, reaction: 1.0), ResistanceProfile = "spring",
            Params = new[]
            {
                new ParamSpec("amplitude", "Max angle", 100, 45, 180, 15, "±°"),
                new ParamSpec("zone", "Apex size", 4, 2, 8, 1, "±°"),
                new ParamSpec("holdS", "Hold", 1.0, 0.5, 3, 0.25, "s"),
            },
            DefaultSets = 3, DefaultWorkS = 40, DefaultRestS = 60, DefaultLoadPct = 45, UsesDirection = false,
        },
        new()
        {
            Id = "wave-hold", Name = "Wave Hold", Category = "Isometric", Icon = "wave", Accent = "#22d3ee",
            Tagline = "The load breathes in and out. You don't move.",
            Description = "Hold the target while the torque rises and falls — a smooth wave, a pyramid or random. Trains force control and steadiness under changing load.",
            HowTo = new[] { "Hold the ghost.", "Feel the load change and match it.", "Stay smooth — no overshoot." },
            Targets = T(1.0, 0.7, 0.7, 0.2), ResistanceProfile = "wave",
            Params = new[]
            {
                new ParamSpec("profile", "Profile", 0, 0, 2, 1, null, new[] { new ParamOption("0", "Wave"), new ParamOption("1", "Pyramid"), new ParamOption("2", "Random") }),
                new ParamSpec("angle", "Target angle", 0, 0, 90, 45, "°", Angles),
                new ParamSpec("periodS", "Wave period", 8, 3, 20, 1, "s"),
                new ParamSpec("zone", "Zone", 3, 2, 8, 1, "±°"),
            },
            DefaultSets = 3, DefaultWorkS = 40, DefaultRestS = 60, DefaultLoadPct = 60,
        },
        new()
        {
            Id = "speed-profile", Name = "Speed–Strength Profile", Category = "Test", Icon = "sprint", Accent = "#f43f5e", IsTest = true,
            Tagline = "Estimates your true max — even beyond what the wheel can make.",
            Description = "Turn as fast as you can against several loads. Heavier loads make you slower in a near-straight line; extending that line to zero speed estimates your maximum steering torque, power and top speed. Works past the motor's 12 Nm limit.",
            HowTo = new[] { "Hands at 9 and 3, thumbs outside the spokes.", "BRACE while it loads, then on GO turn to the marker as fast as you can.", "Ease it back — don't let go.", "Every rep is all-out. Rest when it says shake out." },
            Targets = T(1.0, 0.7, 1.0, 0.4), ResistanceProfile = "load–velocity",
            Params = new[]
            {
                new ParamSpec("loads", "Loads", 5, 3, 8, 1),
                new ParamSpec("minPct", "Lightest load", 40, 10, 80, 5, "%"),
                new ParamSpec("maxPct", "Heaviest load", 100, 50, 100, 5, "%"),
                new ParamSpec("reps", "Reps per load", 2, 1, 3, 1),
                new ParamSpec("rangeDeg", "Push to", 70, 30, 120, 5, "°"),
            },
            DefaultSets = 1, DefaultWorkS = 600, DefaultRestS = 90, DefaultLoadPct = 100, ScoreLabel = "Nm", Direction = PushDir.Right,
            Why = "Load–velocity profiling is how coaches estimate a 1-rep max without maximal lifts; here it estimates the torque your shoulders and arms can produce at zero speed.",
        },
        new()
        {
            Id = "max-push", Name = "Push-Through Max", Category = "Test", Icon = "test", Accent = "#f43f5e", IsTest = true,
            Tagline = "How much can you move? A 1-rep-max ladder for steering.",
            Description = "The wheel loads up and pulls you back to centre. Turn to the target within the time limit. Every rung you make, the load goes up. The first rung you can't complete ends the test — your max is the heaviest one you moved.",
            HowTo = new[] { "Hands at 9 and 3, thumbs outside the spokes.", "Brace while it loads, then PUSH to the marker.", "Let it come back slowly — don't let go.", "Shake out, next rung is heavier." },
            Targets = T(1.0, 0.8, 1.0, 0.4), ResistanceProfile = "ladder",
            Params = new[]
            {
                new ParamSpec("targetDeg", "Push to", 35, 20, 60, 5, "°"),
                new ParamSpec("pushTimeS", "Time limit", 4, 2, 8, 0.5, "s"),
                new ParamSpec("startPct", "First rung", 30, 10, 60, 5, "%"),
                new ParamSpec("stepPct", "Step", 10, 5, 20, 2.5, "%"),
            },
            DefaultSets = 1, DefaultWorkS = 300, DefaultRestS = 90, DefaultLoadPct = 100, ScoreLabel = "Nm", Direction = PushDir.Right,
        },
        new()
        {
            Id = "free-ride", Name = "Free Ride", Category = "Sandbox", Icon = "free", Accent = "#94a3b8",
            Tagline = "Pick a resistance and just turn. Live numbers, no targets.",
            Description = "Choose a resistance profile — constant, bungee (spring), water (damper), sand (friction), wave, kerbs, or everything — set the intensity and play.",
            HowTo = new[] { "Pick a profile.", "Use + / − (or your wheel buttons) to change resistance live.", "Watch the live strength readout." },
            Targets = T(0.6, 0.5, 0.6, 0.3), ResistanceProfile = "selectable", Endless = true,
            Params = new[]
            {
                new ParamSpec("profile", "Resistance", 2, 0, 6, 1, null, FreeRideLogic.ProfileNames.Select((n, i) => new ParamOption(i.ToString(), n)).ToArray()),
                new ParamSpec("minutes", "Length", 5, 1, 30, 1, "min"),
            },
            DefaultSets = 1, DefaultWorkS = 300, DefaultRestS = 0, DefaultLoadPct = 35, ScoreLabel = "N·m·s",
        },
    };

    public static DrillDefinition Get(string id) =>
        All.FirstOrDefault(d => d.Id == id) ?? throw new ArgumentException($"Unknown drill '{id}'");

    public static bool Exists(string id) => All.Any(d => d.Id == id);

    public static SetLogic CreateLogic(string id) => id switch
    {
        "corner-hold" or "one-hand-hold" or "grip-variants" => new HoldLogic(),
        "kerb-hold" => new KerbHoldLogic(),
        "wave-hold" => new WaveHoldLogic(),
        "breakpoint" => new BreakpointLogic(),
        "max-push" => new MaxPushLogic(),
        "speed-profile" => new SpeedStrengthLogic(),
        "lock-to-lock" => new LockToLockLogic(),
        "slow-return" => new SlowReturnLogic(),
        "iso-speed" => new IsoSpeedLogic(),
        "snap-catch" => new SnapCatchLogic(),
        "apex-hunter" => new ApexHunterLogic(),
        "stint" => new StintLogic(),
        "free-ride" => new FreeRideLogic(),
        _ => throw new ArgumentException($"Unknown drill '{id}'"),
    };

    /// <summary>Default config for a drill (what the Drills page pre-fills).</summary>
    public static DrillConfig DefaultConfig(string id)
    {
        var d = Get(id);
        return new DrillConfig
        {
            DrillId = id,
            LoadPct = d.DefaultLoadPct,
            Sets = d.DefaultSets,
            WorkS = d.DefaultWorkS,
            RestS = d.DefaultRestS,
            Hand = d.UsesHandChoice && id == "one-hand-hold" ? Hand.Right : Hand.Both,
            Direction = d.Direction,
            Params = d.Params.ToDictionary(p => p.Key, p => p.Default),
        };
    }

    public static double GripLoadFactor(GripStyle g) => g switch
    {
        GripStyle.OpenHand => 0.85,
        GripStyle.Fingertips => 0.7,
        GripStyle.Pinch => 0.6,
        _ => 1.0,
    };
}
