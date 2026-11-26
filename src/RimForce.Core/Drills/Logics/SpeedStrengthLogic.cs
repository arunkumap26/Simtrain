using RimForce.Core.Engine;
using RimForce.Core.Hardware;

namespace RimForce.Core.Drills.Logics;

/// <summary>
/// Speed–strength (load–velocity) profile: estimates your maximum steering torque BEYOND what the motor can make.
///
/// Each rep: the wheel loads up to a constant torque L pulling towards centre, you brace, then on "GO" turn as fast
/// as you can to the marker (default 70°). We record the mean velocity of the push and the mean torque you produced
/// (L plus the J·α needed to accelerate the rim). Repeating this at several loads gives the classic, nearly linear
/// torque–velocity relationship T = T0 − k·v. Extending that line to zero speed gives T0, your maximum (isometric)
/// torque, even when T0 is far above the motor's 12 Nm. It also yields v0 (unloaded max speed) and Pmax = T0·v0/4.
/// This is the same idea as load–velocity 1RM prediction used in strength training. It is an ESTIMATE: the further T0
/// is above the heaviest load tested, the longer the extrapolation.
/// </summary>
public sealed class SpeedStrengthLogic : SetLogic
{
    private enum Stage { Load, Ready, Push, Return, Rest, Done }

    private const double LoadRateNmPerS = 4.0, ReadyS = 1.1, StartTimeoutS = 2.5, PushTimeoutS = 3.0, ReturnS = 2.0, RestS = 6.0;
    /// <summary>Above this push speed a progressive brake is added so the wheel never gets dangerously fast.</summary>
    public const double BrakeFromDps = 550, BrakeNmPerDps = 0.04, BrakeMaxNm = 8;

    private double[] _loadsPct = Array.Empty<double>();
    private int _repsPerLoad;
    private double _range;
    private int _li, _rep;
    private Stage _stage = Stage.Load;
    private double _stageStart;
    private double _level, _from, _current;
    private double _pushStartT = double.NaN, _startAngle, _torqueInt, _peakV;
    private double? _failedAtNm;
    private double? _heldFailNm;
    private double _startVel, _motorInt;
    private readonly List<Rep> _reps = new();

    private readonly record struct Rep(double LoadNm, double MeanTorqueNm, double MeanDps, double PeakDps);

    public IReadOnlyList<(double loadNm, double torqueNm, double dps)> Points =>
        _reps.Select(r => (r.LoadNm, r.MeanTorqueNm, r.MeanDps)).ToList();

    public override void Begin(SetContext ctx)
    {
        base.Begin(ctx);
        int n = (int)Math.Clamp(ctx.P("loads", 5), 3, 8);
        double lo = Math.Clamp(ctx.P("minPct", 40), 10, 90), hi = Math.Clamp(ctx.P("maxPct", 100), lo + 5, 100);
        _loadsPct = Enumerable.Range(0, n).Select(i => lo + (hi - lo) * i / (n - 1)).ToArray();
        _repsPerLoad = (int)Math.Clamp(ctx.P("reps", 2), 1, 3);
        _range = Math.Clamp(ctx.P("rangeDeg", 70), 30, 120);
        _level = LevelFor(0);
        _stageStart = 1.5;
    }

    private double LevelFor(int i) => Math.Min(C.DrillCapNm, C.Profile.MaxTorqueNm * _loadsPct[Math.Min(i, _loadsPct.Length - 1)] / 100.0);
    private double LoadS => Math.Max(1.0, Math.Abs(_level - _from) / LoadRateNmPerS);
    private void Enter(Stage s, double elapsed) { _stage = s; _stageStart = elapsed; _from = _current; }
    public override bool IsDone(double elapsed) => _stage == Stage.Done || elapsed > 900;

    private void NextRep(double elapsed)
    {
        _rep++;
        if (_rep >= _repsPerLoad) { _rep = 0; _li++; }
        if (_li >= _loadsPct.Length) { Enter(Stage.Done, elapsed); return; }
        _level = LevelFor(_li);
        Enter(Stage.Load, elapsed);
    }

    public override SetFrame Step(in ActivityTick t, double elapsed, bool scoring)
    {
        int dir = C.Dir;
        double inStage = Math.Max(0, elapsed - _stageStart);
        double torque = 0, brake = 0, target = 0;

        // Overpowered while bracing: the wheel is being pulled back past centre. End cleanly before it gets fast.
        if (_stage is Stage.Load or Stage.Ready && elapsed >= 1.5 &&
            (dir * t.AngleDeg < -8 || (dir * t.AngleDeg < -3 && dir * t.VelocityDps < -60)))
        {
            _heldFailNm = _current;
            Fail(elapsed);
        }

        switch (_stage)
        {
            case Stage.Load:
            {
                double f = Math.Clamp(inStage / LoadS, 0, 1);
                torque = _from + (_level - _from) * f * f * (3 - 2 * f);
                if (elapsed >= 1.5 && inStage >= LoadS && Math.Abs(t.AngleDeg) < 8) Enter(Stage.Ready, elapsed);
                break;
            }
            case Stage.Ready:
                torque = _level;
                if (inStage >= ReadyS)
                {
                    Enter(Stage.Push, elapsed);
                    _pushStartT = double.NaN;
                    _torqueInt = 0;
                    _peakV = 0;
                    Emit("go", new { loadNm = Math.Round(_level, 2) });
                }
                break;
            case Stage.Push:
            {
                torque = _level;
                target = dir * _range;
                double pos = dir * t.AngleDeg, vel = dir * t.VelocityDps;
                brake = Math.Clamp((vel - BrakeFromDps) * BrakeNmPerDps, 0, BrakeMaxNm);
                if (double.IsNaN(_pushStartT))
                {
                    if (pos > 3) { _pushStartT = t.Time; _startAngle = pos; _startVel = vel; _motorInt = 0; }
                    else if (inStage > StartTimeoutS || pos < -8) { Fail(elapsed); break; }
                }
                else
                {
                    _torqueInt += dir * t.UserTorqueNm * t.Dt;
                    _motorInt += -dir * t.MotorTorqueNm * t.Dt;
                    _peakV = Math.Max(_peakV, vel);
                    double dt = t.Time - _pushStartT;
                    if (pos >= _range - 2 && dt > 0.02)
                    {
                        double meanDps = (pos - _startAngle) / dt;
                        // You = motor resistance + the torque needed to speed up the rotor and rim (J·Δω/Δt).
                        double inertial = C.Profile.InertiaKgM2 * (vel - _startVel) * Units.DegToRad / dt;
                        double meanNm = _motorInt / dt + inertial;
                        _reps.Add(new Rep(_level, meanNm, meanDps, _peakV));
                        Emit("rep", new { reps = _reps.Count, dps = Math.Round(meanDps), nm = Math.Round(meanNm, 1) });
                        Enter(Stage.Return, elapsed);
                    }
                    else if (dt > PushTimeoutS || pos < -8) Fail(elapsed);
                }
                break;
            }
            case Stage.Return:
            {
                double f = Math.Clamp(inStage / ReturnS, 0, 1);
                torque = _level * (1 - 0.75 * f);
                target = dir * _range * (1 - f);
                if (inStage >= ReturnS) Enter(Stage.Rest, elapsed);
                break;
            }
            case Stage.Rest:
            {
                double f = Math.Clamp(inStage / 0.5, 0, 1);
                torque = _from + (0.1 * _level - _from) * f;
                if (inStage >= RestS) NextRep(elapsed);
                break;
            }
            default:
                torque = 0;
                break;
        }

        _current = torque;
        if (scoring && _stage != Stage.Done)
            Score.Add(t.Time, t.Dt, t.AngleDeg, target, 4, -t.MotorTorqueNm, t.VelocityDps, trackAccuracy: false);
        // The wheel pulls back towards centre; the brake (only above BrakeFromDps) also opposes the push.
        return new SetFrame(-dir * torque, -dir * brake, target, EndNow: _stage == Stage.Done,
            HandsOffSpeedWhenBrakingDps: Safety.SafetyGovernor.MaxSprintSpeedDps);
    }

    private void Fail(double elapsed)
    {
        // Couldn't move this load: your concentric max at this speed is below it — stop adding weight.
        _failedAtNm = _level;
        Emit("miss", null);
        Enter(Stage.Done, elapsed);
    }

    public override GuideFrame Guide(double elapsed, in ActivityTick t)
    {
        double inStage = Math.Max(0, elapsed - _stageStart);
        int dir = C.Dir;
        string cue = _stage switch
        {
            Stage.Load => elapsed < 1.5 ? "GET READY" : "BRACE",
            Stage.Ready => "READY…",
            Stage.Push => dir > 0 ? "GO! FAST ▶" : "◀ GO! FAST",
            Stage.Return => "EASE BACK",
            Stage.Rest => "SHAKE OUT",
            _ => "DONE",
        };
        double target = _stage switch
        {
            Stage.Push => dir * _range,
            Stage.Return => dir * _range * (1 - Math.Clamp(inStage / ReturnS, 0, 1)),
            _ => 0,
        };
        var last = _reps.Count > 0 ? _reps[^1] : (Rep?)null;
        return new GuideFrame
        {
            TargetDeg = target,
            ZoneDeg = 4,
            Path = Flat(target),
            Cue = cue,
            SubCue = $"Load {_li + 1}/{_loadsPct.Length}: {_level:0.0} Nm · rep {_rep + 1}/{_repsPerLoad}" +
                     (last is { } r ? $" · last {r.MeanDps:0}°/s" : ""),
            PullDir = -dir,
            RepProgress = Math.Clamp((_li * _repsPerLoad + _rep) / (double)Math.Max(1, _loadsPct.Length * _repsPerLoad), 0, 1),
            MarkerRight = dir > 0 ? _range : null,
            MarkerLeft = dir < 0 ? -_range : null,
        };
    }

    public override Dictionary<string, double> Live()
    {
        var m = Score.LiveMetrics();
        m["fvLoadNm"] = Math.Round(_level, 2);
        m["fvReps"] = _reps.Count;
        if (_reps.Count > 0) m["fvLastDps"] = Math.Round(_reps[^1].MeanDps);
        var fit = Fit();
        if (fit.valid) m["t0Nm"] = Math.Round(fit.t0, 1);
        return m;
    }

    /// <summary>Best (fastest) rep per load, then least squares T = a + b·v.</summary>
    private (bool valid, double t0, double v0Dps, double pmaxW, double r2, List<Rep> pts) Fit()
    {
        var pts = _reps.GroupBy(r => Math.Round(r.LoadNm, 3)).Select(g => g.MaxBy(r => r.MeanDps)).OrderBy(r => r.LoadNm).ToList();
        if (pts.Count < 2) return (false, 0, 0, 0, 0, pts);
        var xs = pts.Select(p => p.MeanDps * Units.DegToRad).ToArray();
        var ys = pts.Select(p => p.MeanTorqueNm).ToArray();
        double mx = xs.Average(), my = ys.Average();
        double sxx = xs.Sum(x => (x - mx) * (x - mx)), sxy = xs.Zip(ys).Sum(z => (z.First - mx) * (z.Second - my));
        if (sxx < 1e-9) return (false, 0, 0, 0, 0, pts);
        double b = sxy / sxx, a = my - b * mx;
        if (b >= -1e-3 || a <= 0) return (false, 0, 0, 0, 0, pts);
        double ssTot = ys.Sum(y => (y - my) * (y - my));
        double ssRes = xs.Zip(ys).Sum(z => Math.Pow(z.Second - (a + b * z.First), 2));
        double r2 = ssTot > 1e-9 ? 1 - ssRes / ssTot : 1;
        double v0 = -a / b; // rad/s
        return (true, a, v0 * Units.RadToDeg, a * v0 / 4, r2, pts);
    }

    public override (double score, Dictionary<string, double> metrics) Finish()
    {
        var m = Score.FinalMetrics();
        var fit = Fit();
        m["points"] = fit.pts.Count;
        for (int i = 0; i < fit.pts.Count; i++)
        {
            m[$"p{i + 1}LoadNm"] = Math.Round(fit.pts[i].LoadNm, 2);
            m[$"p{i + 1}Nm"] = Math.Round(fit.pts[i].MeanTorqueNm, 2);
            m[$"p{i + 1}Dps"] = Math.Round(fit.pts[i].MeanDps, 1);
        }
        if (_failedAtNm is { } f) m["failedAtNm"] = Math.Round(f, 2);
        if (_heldFailNm is { } hf) m["couldNotHoldNm"] = Math.Round(hf, 2);
        m["heaviestNm"] = fit.pts.Count > 0 ? Math.Round(fit.pts.Max(p => p.LoadNm), 2) : 0;
        if (fit.valid)
        {
            m["t0Nm"] = Math.Round(fit.t0, 2);
            m["v0Dps"] = Math.Round(fit.v0Dps);
            m["pmaxW"] = Math.Round(fit.pmaxW, 1);
            m["r2"] = Math.Round(fit.r2, 3);
            double heaviest = fit.pts.Max(p => p.MeanTorqueNm);
            double reach = fit.t0 / Math.Max(0.1, heaviest);   // how far we extrapolate beyond the hardest rep
            double vSpread = (fit.pts.Max(p => p.MeanDps) - fit.pts.Min(p => p.MeanDps)) / Math.Max(1, fit.pts.Max(p => p.MeanDps));
            // 3 = high, 2 = medium, 1 = low
            m["confidence"] = fit.r2 >= 0.95 && reach <= 1.6 && vSpread >= 0.25 ? 3 : fit.r2 >= 0.85 && reach <= 2.2 && vSpread >= 0.12 ? 2 : 1;
            m["extrapolation"] = Math.Round(reach, 2);
            m["valid"] = 1;
            m["estimated"] = 1;
        }
        else m["valid"] = 0;
        return (fit.valid ? Math.Round(fit.t0, 2) : 0, m);
    }

    public override bool QualifiesForProgression(Dictionary<string, double> metrics) => true;
}
