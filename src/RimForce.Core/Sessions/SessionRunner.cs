using RimForce.Core.Drills;
using RimForce.Core.Engine;
using RimForce.Core.Hardware;
using RimForce.Core.Safety;

namespace RimForce.Core.Sessions;

public sealed record SessionItem(DrillConfig Config);

public sealed record SessionPlan(string Title, IReadOnlyList<SessionItem> Items)
{
    /// <summary>Program reference ("week:session") when the plan came from the program.</summary>
    public string? ProgramRef { get; init; }
    public static SessionPlan Single(DrillConfig c) => new(DrillCatalog.Get(c.DrillId).Name, new[] { new SessionItem(c) });
}

public enum SessionPhase { Ready, Countdown, Work, SetDone, Rest, Paused, Complete }

/// <summary>
/// Runs a session (one or more drills, each with sets) on the force loop:
/// Ready → Countdown(3-2-1) → Work → SetDone → Rest → Countdown → … → Complete.
/// A safety stop pauses the session; Start retries the interrupted set.
/// </summary>
public sealed class SessionRunner : IActivity, IHandsAware, INextable
{
    public const double CountdownS = 3.0;
    public const double SetDoneS = 1.6;

    private readonly SessionPlan _plan;
    private readonly WheelProfile _profile;
    private readonly ReferenceStrength _refs;
    private readonly Random _rng;
    private readonly List<ActivityEvent> _events = new();
    private readonly List<SetResult> _results = new();
    private readonly object _lock = new();

    private int _item, _set;
    private SessionPhase _phase = SessionPhase.Ready;
    private double _phaseStart = double.NaN;
    private double _phaseLen;
    private double _now;
    private SetLogic? _logic;
    private SetContext? _ctx;
    private double _workStart;
    private DateTime _setStartedUtc;
    private int _lastCountdown = -1;
    private string? _pausedReason;
    private bool _startRequested;
    private bool _skipRequested;
    private ActivityTick _lastTick;
    private double _intensityAtStart = 1;
    private double _lastGhost;

    public SessionRunner(SessionPlan plan, WheelProfile profile, ReferenceStrength refs, int seed = 0)
    {
        if (plan.Items.Count == 0) throw new ArgumentException("Empty session");
        _plan = plan;
        _profile = profile;
        _refs = refs;
        _rng = seed == 0 ? new Random() : new Random(seed);
        PrepareSet();
    }

    public string Kind => "session";
    public SessionPlan Plan => _plan;
    public SessionPhase Phase => _phase;
    public int ItemIndex => _item;
    public int SetIndex => _set;
    public IReadOnlyList<SetResult> Results => _results;
    public bool IsFinished => _phase == SessionPhase.Complete;
    public int HandsOnWheel => _logic?.Hands ?? 2;
    public double? ReferenceMaxNm => _ctx is { ReferenceIsBreakpoint: true } c ? c.ReferenceMaxNm : null;
    public bool InWork => _phase == SessionPhase.Work;
    public int SetSerial { get; private set; }

    /// <summary>Is force about to be (or being) applied? The engine must be armed for these.</summary>
    public bool WantsArmed => _phase is SessionPhase.Countdown or SessionPhase.Work or SessionPhase.SetDone or SessionPhase.Rest;

    private DrillConfig Cfg => _plan.Items[_item].Config;
    private DrillDefinition Def => DrillCatalog.Get(Cfg.DrillId);

    // ---------- commands (thread-safe; applied on the next tick) ----------

    /// <summary>Start / resume: from Ready, Paused or Rest go to the 3-2-1 countdown. Caller must arm the engine first.</summary>
    public void Start() { lock (_lock) _startRequested = true; }

    /// <summary>Skip the rest / current waiting phase.</summary>
    public void Next() { lock (_lock) _skipRequested = true; }

    // ---------- IActivity ----------

    public ActivityStep Step(in ActivityTick t)
    {
        _now = t.Time;
        _lastTick = t;
        if (double.IsNaN(_phaseStart)) _phaseStart = t.Time;
        bool start, skip;
        lock (_lock) { start = _startRequested; skip = _skipRequested; _startRequested = _skipRequested = false; }

        double inPhase = t.Time - _phaseStart;
        switch (_phase)
        {
            case SessionPhase.Ready:
            case SessionPhase.Paused:
                if (start && t.Armed) EnterCountdown(t.Time);
                break;

            case SessionPhase.Countdown:
                if (!t.Armed) { Pause("Disarmed"); break; }
                int n = (int)Math.Ceiling(CountdownS - inPhase);
                if (n != _lastCountdown && n > 0) { _lastCountdown = n; Emit("countdown", new { n }); }
                if (inPhase >= CountdownS || skip) EnterWork(t.Time);
                break;

            case SessionPhase.Work:
            {
                double elapsed = t.Time - _workStart;
                bool scoring = elapsed >= 1.5 && t.Envelope >= 0.98;
                var frame = _logic!.Step(t, elapsed, scoring);
                foreach (var e in _logic.DrainEvents()) _events.Add(e);
                if (frame.EndNow || _logic.IsDone(elapsed) || skip)
                {
                    FinishSet(t.Time, completed: !skip || frame.EndNow);
                    EnterSetDone(t.Time);
                    return new ActivityStep(new TorqueRequest(0, 0, _ctx!.DrillCapNm, false), frame.TargetDeg);
                }
                _lastGhost = frame.TargetDeg;
                var req = new TorqueRequest(frame.OpenLoopNm, frame.ReactiveNm, _ctx!.DrillCapNm, true, frame.RunawayOverrideDps, frame.VibrationNm, frame.HandsOffSpeedWhenBrakingDps);
                return new ActivityStep(req, frame.TargetDeg);
            }

            case SessionPhase.SetDone:
                if (inPhase >= SetDoneS) Advance(t.Time);
                break;

            case SessionPhase.Rest:
                if (inPhase >= _phaseLen || skip)
                {
                    if (t.Armed) EnterCountdown(t.Time);
                    else EnterPhase(SessionPhase.Ready, t.Time, 0);
                }
                break;

            case SessionPhase.Complete:
                break;
        }

        if (_logic != null) _lastGhost = _logic.Guide(0, t).TargetDeg;
        return new ActivityStep(TorqueRequest.None, _lastGhost);
    }

    public void OnSafetyStop(string reason)
    {
        if (_phase == SessionPhase.Work)
        {
            FinishSet(_now, completed: false, note: reason);
            PrepareSet(); // fresh logic for the retry
        }
        if (_phase is SessionPhase.Countdown or SessionPhase.Work or SessionPhase.SetDone or SessionPhase.Rest)
            Pause(reason);
    }

    public IReadOnlyList<ActivityEvent> DrainEvents()
    {
        if (_events.Count == 0) return Array.Empty<ActivityEvent>();
        var copy = _events.ToArray();
        _events.Clear();
        return copy;
    }

    // ---------- phases ----------

    private void EnterPhase(SessionPhase p, double now, double len)
    {
        _phase = p;
        _phaseStart = now;
        _phaseLen = len;
        Emit("phase", new { phase = p.ToString().ToLowerInvariant() });
    }

    private void Pause(string reason)
    {
        _pausedReason = reason;
        EnterPhase(SessionPhase.Paused, _now, 0);
    }

    private void EnterCountdown(double now)
    {
        _pausedReason = null;
        _lastCountdown = -1;
        if (_logic == null) PrepareSet();
        EnterPhase(SessionPhase.Countdown, now, CountdownS);
    }

    private void EnterWork(double now)
    {
        _workStart = now;
        SetSerial++;
        _setStartedUtc = DateTime.UtcNow;
        _intensityAtStart = _lastTick.Intensity;
        EnterPhase(SessionPhase.Work, now, Def.Endless ? Cfg.P("minutes", 10) * 60 : Cfg.WorkS);
        Emit("setStart", new { item = _item, set = _set, drill = Def.Id });
    }

    private void EnterSetDone(double now) => EnterPhase(SessionPhase.SetDone, now, SetDoneS);

    private void Advance(double now)
    {
        bool moreSets = _set + 1 < Cfg.Sets;
        bool moreItems = _item + 1 < _plan.Items.Count;
        if (!moreSets && !moreItems)
        {
            _logic = null;
            EnterPhase(SessionPhase.Complete, now, 0);
            Emit("sessionComplete", Summary());
            return;
        }
        double rest = Cfg.RestS;
        if (moreSets) _set++;
        else
        {
            _item++;
            _set = 0;
            rest = Math.Max(rest, 30);
            Emit("itemStart", new { item = _item, drill = Cfg.DrillId });
        }
        PrepareSet();
        if (rest <= 0.5) EnterCountdown(now);
        else EnterPhase(SessionPhase.Rest, now, rest);
    }

    private void PrepareSet()
    {
        var def = Def;
        var cfg = Cfg;
        int dir = !def.UsesDirection ? 1 : cfg.Direction switch
        {
            PushDir.Right => 1,
            PushDir.Left => -1,
            _ => _set % 2 == 0 ? 1 : -1,
        };
        Hand hand = cfg.Hand;
        if (def.Id == "one-hand-hold" && hand == Hand.Both)
            hand = (_set / (cfg.Direction == PushDir.Alternate ? 2 : 1)) % 2 == 0 ? Hand.Right : Hand.Left;
        var grip = def.Id == "grip-variants" ? (GripStyle)(1 + _set % 3) : GripStyle.Full;
        if (def.Id == "grip-variants" && cfg.Params.TryGetValue("grip", out var g)) grip = (GripStyle)(int)g;
        double? refNm = _refs.Resolve(hand, dir);
        _ctx = new SetContext
        {
            Definition = def,
            Config = cfg,
            Profile = _profile,
            ItemIndex = _item,
            SetIndex = _set,
            Dir = dir,
            Hand = hand,
            Grip = grip,
            ReferenceMaxNm = refNm ?? _profile.MaxTorqueNm,
            ReferenceIsBreakpoint = refNm != null,
            Rng = new Random(_rng.Next()),
            LoadFactor = DrillCatalog.GripLoadFactor(grip),
        };
        _logic = DrillCatalog.CreateLogic(def.Id);
        _logic.Begin(_ctx);
    }

    private void FinishSet(double now, bool completed, string? note = null)
    {
        if (_logic == null || _ctx == null) return;
        var (score, metrics) = _logic.Finish();
        bool qualifies = completed && _logic.QualifiesForProgression(metrics);
        var result = new SetResult
        {
            DrillId = Def.Id,
            DrillName = Def.Name,
            ItemIndex = _item,
            SetIndex = _set,
            Role = Cfg.Role,
            Hand = _ctx.Hand,
            Direction = _ctx.Dir > 0 ? PushDir.Right : PushDir.Left,
            Grip = _ctx.Grip == GripStyle.Full ? null : _ctx.Grip.ToString(),
            LoadNm = Math.Round(_ctx.LoadNm(_intensityAtStart), 2),
            LoadPct = Cfg.LoadPct,
            Intensity = _intensityAtStart,
            WorkS = Math.Round(now - _workStart, 1),
            Completed = completed,
            Score = score,
            ScoreLabel = Def.ScoreLabel,
            Metrics = metrics,
            Trace = _logic.Tracker.Trace(_workStart),
            StartedUtc = _setStartedUtc,
            EndedUtc = DateTime.UtcNow,
            Note = note,
        };
        result.Metrics["qualifies"] = qualifies ? 1 : 0;
        _results.Add(result);

        if (Def.Id == "breakpoint" && completed && metrics.TryGetValue("valid", out var v) && v > 0)
            _refs.Record(_ctx.Hand, _ctx.Dir, score);

        Emit("setEnd", result);
        _logic = null;
    }

    private object Summary() => new
    {
        title = _plan.Title,
        programRef = _plan.ProgramRef,
        sets = _results.Count,
        completed = _results.Count(r => r.Completed),
        impulseNms = Math.Round(_results.Sum(r => r.Metrics.GetValueOrDefault("impulseNms")), 1),
        workJ = Math.Round(_results.Sum(r => r.Metrics.GetValueOrDefault("workConcJ") + r.Metrics.GetValueOrDefault("workEccJ")), 1),
        tutS = Math.Round(_results.Sum(r => r.Metrics.GetValueOrDefault("tutS")), 1),
        avgScore = _results.Where(r => r.ScoreLabel == "score").Select(r => r.Score).DefaultIfEmpty(0).Average(),
    };

    private void Emit(string kind, object? data = null) => _events.Add(new ActivityEvent(kind, data));

    // ---------- snapshot for the UI ----------

    public object Snapshot()
    {
        var def = Def;
        var cfg = Cfg;
        var t = _lastTick;
        double inPhase = double.IsNaN(_phaseStart) ? 0 : _now - _phaseStart;
        double elapsed = _phase == SessionPhase.Work ? _now - _workStart : 0;
        GuideFrame? guide = null;
        if (_logic != null)
        {
            guide = _logic.Guide(elapsed, t);
            if (_phase is SessionPhase.Ready or SessionPhase.Paused or SessionPhase.Rest)
                guide = guide with { Cue = _phase == SessionPhase.Rest ? "REST" : "GET IN POSITION", SubCue = HowToFirst(def) };
            else if (_phase == SessionPhase.Countdown)
                guide = guide with { Cue = Math.Ceiling(CountdownS - inPhase).ToString("0") };
        }
        var last = _results.Count > 0 ? _results[^1] : null;
        SessionItem? nextItem = _set + 1 < cfg.Sets ? _plan.Items[_item] : (_item + 1 < _plan.Items.Count ? _plan.Items[_item + 1] : null);
        return new
        {
            kind = "session",
            title = _plan.Title,
            programRef = _plan.ProgramRef,
            phase = _phase.ToString().ToLowerInvariant(),
            phaseElapsedS = Math.Round(inPhase, 2),
            phaseLenS = _phaseLen,
            itemIndex = _item,
            itemCount = _plan.Items.Count,
            setIndex = _set,
            setCount = cfg.Sets,
            role = cfg.Role,
            drill = new { def.Id, def.Name, def.Category, def.Accent, def.Icon, def.ResistanceProfile, def.ScoreLabel, def.Tagline, def.HowTo },
            hand = _ctx?.Hand.ToString().ToLowerInvariant(),
            dir = _ctx == null ? null : (_ctx.Dir > 0 ? "right" : "left"),
            grip = _ctx?.Grip.ToString(),
            loadNm = _ctx == null ? 0 : Math.Round(_ctx.LoadNm(t.Intensity), 2),
            loadPct = cfg.LoadPct,
            refMaxNm = _ctx?.ReferenceMaxNm,
            refIsBreakpoint = _ctx?.ReferenceIsBreakpoint ?? false,
            workS = def.Endless ? cfg.P("minutes", 10) * 60 : cfg.WorkS,
            restS = cfg.RestS,
            elapsedS = Math.Round(elapsed, 2),
            guide,
            live = _logic?.Live(),
            pausedReason = _pausedReason,
            last = last == null ? null : new { last.DrillName, last.SetIndex, last.Score, last.ScoreLabel, last.Completed, last.Metrics, last.LoadNm, last.Note },
            next = nextItem == null ? null : new { DrillCatalog.Get(nextItem.Config.DrillId).Name, set = _set + 1 < cfg.Sets ? _set + 2 : 1 },
            results = _results.Select(r => new { r.DrillName, r.SetIndex, r.Score, r.ScoreLabel, r.Completed, r.Role }).ToArray(),
        };
    }

    private static string HowToFirst(DrillDefinition d) => d.HowTo.Length > 0 ? d.HowTo[0] : d.Tagline;
}
