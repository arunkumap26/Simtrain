using RimForce.Core.Engine;

namespace RimForce.Core.Drills.Logics;

/// <summary>
/// Push-through max (concentric "1-rep max" ladder). Each rung: the wheel loads up to a constant torque pulling you
/// back towards centre, then you must turn TO the target (default 35°) within the time limit. Success → let it come
/// back slowly, shake out, next rung is heavier. The first rung you can't complete ends the test; your max is the
/// heaviest rung you completed. Loads are % of the DEVICE maximum (this is a test, not a % of you).
/// </summary>
public sealed class MaxPushLogic : SetLogic
{
    private enum Stage { Load, Push, Return, Shake, Done }

    private const double ReturnS = 2.0, ShakeS = 3.0;
    /// <summary>Loads ramp in at this rate so you can brace (a 10 Nm jump takes ~2.5 s).</summary>
    private const double LoadRateNmPerS = 4.0;
    private double LoadS => Math.Max(1.2, Math.Abs(_level - _fromLevel) / LoadRateNmPerS);
    private Stage _stage = Stage.Load;
    private double _stageStart;
    private int _rung;
    private double _level;              // Nm of the current rung
    private double _bestNm;             // heaviest completed rung
    private bool _capped;
    private double _pushTime;
    private double _elapsed;
    private double _peakTorque;
    private double _fromLevel;          // torque at the start of a ramp
    private double _currentTorque;
    private readonly List<(double nm, bool ok, double seconds)> _attempts = new();

    private double Target => C.P("targetDeg", 35);
    private double PushLimitS => C.P("pushTimeS", 4);
    private double StartNm => C.Profile.MaxTorqueNm * C.P("startPct", 30) / 100.0;
    private double StepNm => C.Profile.MaxTorqueNm * C.P("stepPct", 10) / 100.0;
    private double CeilingNm => C.DrillCapNm;

    public double BestNm => _bestNm;

    public override void Begin(SetContext ctx)
    {
        base.Begin(ctx);
        _level = Math.Min(StartNm, CeilingNm);
        _stageStart = 1.5; // after the governor's soft start
        _fromLevel = 0;
    }

    public override bool IsDone(double elapsed) => _stage == Stage.Done || elapsed > 600;

    private void Enter(Stage s, double elapsed) { _stage = s; _stageStart = elapsed; _fromLevel = _currentTorque; }

    public override SetFrame Step(in ActivityTick t, double elapsed, bool scoring)
    {
        _elapsed = elapsed;
        double inStage = Math.Max(0, elapsed - _stageStart);
        double target = 0;
        double torque;
        int dir = C.Dir;
        _peakTorque = Math.Max(_peakTorque, Math.Abs(t.MotorTorqueNm));

        // Overpowered: the wheel is being pulled back past centre (the wrong way) — that rung is a fail.
        // Detect it early so the load drops before the wheel gets fast (otherwise runaway detection would stop it).
        double slipLimit = _stage == Stage.Load ? -10 : -6;
        if (_stage is Stage.Load or Stage.Push && elapsed >= 1.5 &&
            (dir * t.AngleDeg < slipLimit || (dir * t.AngleDeg < -3 && dir * t.VelocityDps < -60)))
        {
            _attempts.Add((_level, false, inStage));
            Emit("miss", null);
            Enter(Stage.Done, elapsed);
        }

        switch (_stage)
        {
            case Stage.Load:
            {
                double f = Math.Clamp(inStage / LoadS, 0, 1);
                torque = _fromLevel + (_level - _fromLevel) * f * f * (3 - 2 * f);
                if (elapsed >= 1.5 && inStage >= LoadS) { Enter(Stage.Push, elapsed); Emit("rung", new { rung = _rung + 1, nm = Math.Round(_level, 2) }); }
                break;
            }
            case Stage.Push:
            {
                torque = _level;
                target = dir * Target;
                if (dir * t.AngleDeg >= Target - 3)
                {
                    _pushTime = inStage;
                    _attempts.Add((_level, true, inStage));
                    _bestNm = Math.Max(_bestNm, _level);
                    Emit("apex", new { hits = _rung + 1, ms = Math.Round(inStage * 1000) });
                    if (_level >= CeilingNm - 1e-6) { _capped = true; Enter(Stage.Return, elapsed); break; }
                    Enter(Stage.Return, elapsed);
                }
                else if (inStage >= PushLimitS)
                {
                    _attempts.Add((_level, false, inStage));
                    Emit("miss", null);
                    Enter(Stage.Done, elapsed);
                }
                break;
            }
            case Stage.Return:
            {
                // Let it come back slowly: the pull fades to 30% while the ghost walks back to centre.
                double f = Math.Clamp(inStage / ReturnS, 0, 1);
                torque = _level * (1 - 0.7 * f);
                target = dir * Target * (1 - f);
                if (inStage >= ReturnS)
                {
                    if (_capped) { Enter(Stage.Done, elapsed); break; }
                    Enter(Stage.Shake, elapsed);
                }
                break;
            }
            case Stage.Shake:
            {
                double f = Math.Clamp(inStage / 0.6, 0, 1);
                torque = _fromLevel + (0.15 * _level - _fromLevel) * f;
                if (inStage >= ShakeS)
                {
                    _rung++;
                    _level = Math.Min(CeilingNm, _level + StepNm);
                    Enter(Stage.Load, elapsed);
                }
                break;
            }
            default:
                torque = 0;
                break;
        }

        _currentTorque = torque;
        if (scoring && _stage != Stage.Done)
            Score.Add(t.Time, t.Dt, t.AngleDeg, target, 4, -t.MotorTorqueNm, t.VelocityDps, trackAccuracy: false);
        // The wheel always pulls back towards centre (opposite to your push); you move against it.
        return new SetFrame(-dir * torque, 0, target, EndNow: _stage == Stage.Done);
    }

    public override GuideFrame Guide(double elapsed, in ActivityTick t)
    {
        double inStage = Math.Max(0, elapsed - _stageStart);
        string cue = _stage switch
        {
            Stage.Load => elapsed < 1.5 ? "GET READY" : "BRACE — LOADING",
            Stage.Push => DirWord(C.Dir) == "RIGHT" ? "PUSH! ▶" : "◀ PUSH!",
            Stage.Return => "EASE BACK",
            Stage.Shake => "SHAKE OUT",
            _ => "DONE",
        };
        double target = _stage switch
        {
            Stage.Push => C.Dir * Target,
            Stage.Return => C.Dir * Target * (1 - Math.Clamp(inStage / ReturnS, 0, 1)),
            _ => 0,
        };
        return new GuideFrame
        {
            TargetDeg = target,
            ZoneDeg = 3,
            Path = Flat(target),
            Cue = cue,
            SubCue = _stage == Stage.Push
                ? $"Rung {_rung + 1}: {_level:0.0} Nm · {Math.Max(0, PushLimitS - inStage):0.0} s"
                : $"Next: {_level:0.0} Nm · best {_bestNm:0.0} Nm",
            PullDir = -C.Dir,
            RepProgress = _stage == Stage.Push ? Math.Clamp(inStage / PushLimitS, 0, 1) : Math.Clamp(_level / Math.Max(0.1, CeilingNm), 0, 1),
            MarkerRight = C.Dir > 0 ? Target : null,
            MarkerLeft = C.Dir < 0 ? -Target : null,
            Hand = C.Hand == Hand.Both ? null : (C.Hand == Hand.Left ? "LEFT · 9 o'clock" : "RIGHT · 3 o'clock"),
            Action = Cues.OneHandAction(C.Hand, C.Dir),
        };
    }

    public override Dictionary<string, double> Live()
    {
        var m = Score.LiveMetrics();
        m["rung"] = _rung + 1;
        m["rungNm"] = Math.Round(_level, 2);
        m["bestPushNm"] = Math.Round(_bestNm, 2);
        return m;
    }

    public override (double score, Dictionary<string, double> metrics) Finish()
    {
        var m = Score.FinalMetrics();
        m["maxPushNm"] = Math.Round(_bestNm, 2);
        m["rungs"] = _attempts.Count(a => a.ok);
        m["capped"] = _capped ? 1 : 0;
        m["lastPushS"] = Math.Round(_pushTime, 2);
        m["valid"] = _bestNm > 0 ? 1 : 0;
        return (Math.Round(_bestNm, 2), m);
    }

    public override bool QualifiesForProgression(Dictionary<string, double> metrics) => true;
}
