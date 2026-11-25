using RimForce.Core.Drills;
using RimForce.Core.Sessions;

namespace RimForce.Core.Programs;

public sealed record ProgramSession
{
    public int Week { get; init; }
    public int Number { get; init; }           // 1..sessionsPerWeek
    public string Template { get; init; } = "A";
    public string Focus { get; init; } = "";
    public bool Deload { get; init; }
    public bool Retest { get; init; }
    public bool Baseline { get; init; }
    public List<DrillConfig> Items { get; init; } = new();
    public string Key => $"w{Week}s{Number}";
}

public sealed record TrainingProgram
{
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public int Weeks { get; init; } = 8;
    public int SessionsPerWeek { get; init; } = 3;
    public List<ProgramSession> Sessions { get; init; } = new();
    /// <summary>Current working load (% of breakpoint) per drill id. Progresses +5% per qualifying session.</summary>
    public Dictionary<string, double> WorkingLoadPct { get; init; } = new();
    /// <summary>Extra work-time multiplier per drill once the load hits the torque cap (the "overload lever").</summary>
    public Dictionary<string, double> WorkMultiplier { get; init; } = new();
    public List<string> CompletedKeys { get; init; } = new();
    public List<string> ProgressionLog { get; init; } = new();

    public ProgramSession? Next => Sessions.FirstOrDefault(s => !CompletedKeys.Contains(s.Key));
    public int CompletedCount => CompletedKeys.Count;
    public double Progress => Sessions.Count == 0 ? 0 : (double)CompletedKeys.Count / Sessions.Count;
}

/// <summary>
/// 8-week program: 2-3 sessions a week, each = warm-up at 25% of breakpoint, 3-5 drills, cool-down.
/// Working sets start at 50% of the latest breakpoint, +5% after a qualifying session (every set
/// completed with mean error under 3°), otherwise hold. Weeks 4 and 8 are deloads at 60% with a
/// breakpoint retest at the end. Templates rotate A (hold strength) → B (power & reaction) → C (grip & endurance).
/// </summary>
public static class ProgramGenerator
{
    public const double StartLoadPct = 50;
    public const double StepPct = 5;
    public const double DeloadFactor = 0.6;
    public const double WarmupPct = 25;
    public const double MaxLoadPct = 95;

    private static readonly string[] Rotation = { "A", "B", "C" };

    public static TrainingProgram Generate(int sessionsPerWeek = 3, bool needsBaseline = true, int weeks = 8)
    {
        sessionsPerWeek = Math.Clamp(sessionsPerWeek, 2, 3);
        var sessions = new List<ProgramSession>();
        int k = 0;
        for (int w = 1; w <= weeks; w++)
        {
            bool deload = w % 4 == 0;
            for (int s = 1; s <= sessionsPerWeek; s++, k++)
            {
                string tpl = Rotation[k % Rotation.Length];
                bool last = s == sessionsPerWeek;
                sessions.Add(new ProgramSession
                {
                    Week = w,
                    Number = s,
                    Template = tpl,
                    Focus = Focus(tpl) + (deload ? " · deload" : ""),
                    Deload = deload,
                    Retest = deload && last,
                    Baseline = needsBaseline && w == 1 && s == 1,
                });
            }
        }
        var p = new TrainingProgram
        {
            SessionsPerWeek = sessionsPerWeek,
            Weeks = weeks,
            Sessions = sessions,
            WorkingLoadPct = DrillCatalog.All.Where(d => !d.IsTest).ToDictionary(d => d.Id, _ => StartLoadPct),
        };
        return Materialise(p);
    }

    public static string Focus(string tpl) => tpl switch
    {
        "A" => "Hold strength",
        "B" => "Power & reaction",
        _ => "Grip & endurance",
    };

    /// <summary>(Re)builds every not-yet-completed session's drill list from the current progression state.</summary>
    public static TrainingProgram Materialise(TrainingProgram p)
    {
        var rebuilt = p.Sessions.Select(s => p.CompletedKeys.Contains(s.Key) ? s : s with { Items = BuildItems(p, s) }).ToList();
        return p with { Sessions = rebuilt };
    }

    private static DrillConfig Cfg(TrainingProgram p, ProgramSession s, string id, int sets, double work, double rest,
        Hand hand = Hand.Both, PushDir dir = PushDir.Alternate, Dictionary<string, double>? prm = null, string role = "Working", double? loadOverride = null)
    {
        var baseCfg = DrillCatalog.DefaultConfig(id);
        double load = loadOverride ?? p.WorkingLoadPct.GetValueOrDefault(id, StartLoadPct);
        if (s.Deload && role == "Working") { load *= DeloadFactor; sets = Math.Max(1, sets - 1); }
        double mult = role == "Working" ? p.WorkMultiplier.GetValueOrDefault(id, 1.0) : 1.0;
        var parms = new Dictionary<string, double>(baseCfg.Params);
        if (prm != null) foreach (var kv in prm) parms[kv.Key] = kv.Value;
        return baseCfg with
        {
            LoadPct = Math.Round(load, 1),
            Sets = sets,
            WorkS = Math.Round(work * mult),
            RestS = rest,
            Hand = hand,
            Direction = dir,
            Params = parms,
            Role = role,
        };
    }

    private static List<DrillConfig> BuildItems(TrainingProgram p, ProgramSession s)
    {
        var items = new List<DrillConfig>();
        if (s.Baseline)
        {
            items.Add(Cfg(p, s, "corner-hold", 2, 20, 30, prm: new() { ["angle"] = 0 }, role: "Warm-up", loadOverride: 20));
            items.Add(Cfg(p, s, "breakpoint", 2, 120, 120, role: "Test", loadOverride: 100));
            items.Add(Cfg(p, s, "free-ride", 1, 120, 0, prm: new() { ["profile"] = 2, ["minutes"] = 2 }, role: "Cool-down", loadOverride: 20));
            return items;
        }

        int week = s.Week;
        items.Add(Cfg(p, s, "corner-hold", 2, 20, 30, prm: new() { ["angle"] = 0 }, role: "Warm-up", loadOverride: WarmupPct));

        switch (s.Template)
        {
            case "A":
                items.Add(Cfg(p, s, "corner-hold", 4, 30, 60, prm: new() { ["angle"] = week % 2 == 0 ? 90 : 45 }));
                items.Add(Cfg(p, s, "one-hand-hold", 4, 25, 60));
                items.Add(Cfg(p, s, "wave-hold", 2, 40, 60, prm: new() { ["profile"] = week % 3 }));
                if (week >= 3) items.Add(Cfg(p, s, "slow-return", 2, 45, 75));
                break;
            case "B":
                items.Add(Cfg(p, s, "lock-to-lock", 3, 20, 90));
                items.Add(Cfg(p, s, "snap-catch", 3, 30, 60));
                items.Add(Cfg(p, s, "apex-hunter", 2, 40, 60));
                if (week >= 2) items.Add(Cfg(p, s, "iso-speed", 2, 30, 60));
                break;
            default:
                items.Add(Cfg(p, s, "grip-variants", 3, 20, 45));
                items.Add(Cfg(p, s, "kerb-hold", 3, 30, 60));
                double minutes = Math.Min(40, 8 + 2 * (week - 1));
                items.Add(Cfg(p, s, "stint", 1, minutes * 60, 0, prm: new() { ["minutes"] = minutes, ["lap"] = week % 2 == 0 ? 1 : 0 }));
                break;
        }

        if (s.Retest)
            items.Add(Cfg(p, s, "breakpoint", 2, 120, 120, role: "Test", loadOverride: 100));

        items.Add(Cfg(p, s, "free-ride", 1, 120, 0, prm: new() { ["profile"] = 2, ["minutes"] = 2 }, role: "Cool-down", loadOverride: 20));
        return items;
    }

    public static SessionPlan ToPlan(ProgramSession s) =>
        new($"Week {s.Week} · Session {s.Number} — {s.Focus}", s.Items.Select(c => new SessionItem(c)).ToList()) { ProgramRef = s.Key };

    /// <summary>
    /// Apply the progression rules after a program session.
    /// <paramref name="capNm"/> is the effective torque cap (global safety cap); <paramref name="resolveLoadNm"/>
    /// converts a drill's next working load % to Nm so we can tell when the motor cap is reached.
    /// </summary>
    public static TrainingProgram ApplySessionResults(TrainingProgram p, string sessionKey, IReadOnlyList<SetResult> results,
        double capNm = double.MaxValue, Func<string, double, double>? resolveLoadNm = null)
    {
        var session = p.Sessions.FirstOrDefault(s => s.Key == sessionKey);
        if (session == null || p.CompletedKeys.Contains(sessionKey)) return p;

        var loads = new Dictionary<string, double>(p.WorkingLoadPct);
        var mults = new Dictionary<string, double>(p.WorkMultiplier);
        var log = new List<string>(p.ProgressionLog);

        if (!session.Deload)
        {
            foreach (var cfg in session.Items.Where(i => i.Role == "Working"))
            {
                var sets = results.Where(r => r.DrillId == cfg.DrillId && r.Role == "Working").ToList();
                bool allDone = sets.Count >= cfg.Sets && sets.All(r => r.Completed);
                bool qualify = allDone && sets.All(r => r.Metrics.GetValueOrDefault("qualifies") > 0);
                double cur = loads.GetValueOrDefault(cfg.DrillId, StartLoadPct);
                if (!qualify)
                {
                    log.Add($"{session.Key} {cfg.DrillId}: hold at {cur:0}% ({(allDone ? "accuracy" : "sets incomplete")})");
                    continue;
                }
                double next = Math.Min(MaxLoadPct, cur + StepPct);
                double nextNm = resolveLoadNm?.Invoke(cfg.DrillId, next) ?? 0;
                if (next <= cur || nextNm > capNm)
                {
                    // Torque cap reached: progress time under tension instead.
                    double m = Math.Min(2.0, mults.GetValueOrDefault(cfg.DrillId, 1.0) * 1.1);
                    mults[cfg.DrillId] = Math.Round(m, 3);
                    log.Add($"{session.Key} {cfg.DrillId}: at the torque cap → work time ×{m:0.00}");
                }
                else
                {
                    loads[cfg.DrillId] = next;
                    log.Add($"{session.Key} {cfg.DrillId}: {cur:0}% → {next:0}%");
                }
            }
        }
        else log.Add($"{session.Key}: deload week, loads unchanged");

        var updated = p with
        {
            WorkingLoadPct = loads,
            WorkMultiplier = mults,
            CompletedKeys = p.CompletedKeys.Append(sessionKey).ToList(),
            ProgressionLog = log,
        };
        return Materialise(updated);
    }
}
