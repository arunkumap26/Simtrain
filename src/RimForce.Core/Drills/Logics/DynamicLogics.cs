using RimForce.Core.Engine;

namespace RimForce.Core.Drills.Logics;

/// <summary>
/// Lock-to-lock sprints (the Red Bull weighted-wheel test): a heavy damper, turn end to end as fast as
/// you can. Resistance grows with speed (torque = load at the reference speed).
/// </summary>
public sealed class LockToLockLogic : SetLogic
{
    private double _amp;
    private int _side;          // which end we are heading to (+1 right, -1 left)
    private int _halfReps;
    private double _peakSpeed, _sumPeakSpeed;
    private double _repPeak;

    public int Reps => _halfReps / 2;

    public override void Begin(SetContext ctx)
    {
        base.Begin(ctx);
        _amp = ctx.P("amplitude", 90);
        _side = ctx.Dir >= 0 ? 1 : -1;
    }

    private double C_NmPerDps(double load) => load / Math.Max(60, C.P("refSpeed", 360));

    public override SetFrame Step(in ActivityTick t, double elapsed, bool scoring)
    {
        double load = C.LoadNm(t.Intensity);
        double reactive = Damper(t.VelocityDps, C_NmPerDps(load), load * 1.25);
        if (scoring)
        {
            Score.Add(t.Time, t.Dt, t.AngleDeg, _side * _amp, 10, -t.MotorTorqueNm, t.VelocityDps, trackAccuracy: false);
            _repPeak = Math.Max(_repPeak, Math.Abs(t.VelocityDps));
            if (_side * t.AngleDeg >= _amp - 8)
            {
                _halfReps++;
                _sumPeakSpeed += _repPeak;
                _peakSpeed = Math.Max(_peakSpeed, _repPeak);
                _repPeak = 0;
                _side = -_side;
                if (_halfReps % 2 == 0) Emit("rep", new { reps = Reps });
            }
        }
        // The damper always brakes the motion, so a quick driver may exceed the normal hands-off speed.
        return new SetFrame(0, reactive, _side * _amp, HandsOffSpeedWhenBrakingDps: 720);
    }

    public override GuideFrame Guide(double elapsed, in ActivityTick t) => new()
    {
        TargetDeg = _side * _amp,
        ZoneDeg = 8,
        Path = Flat(_side * _amp),
        Cue = elapsed < 1.5 ? "READY…" : (_side > 0 ? "RIGHT! ▶" : "◀ LEFT!"),
        SubCue = $"{Reps} reps · faster = heavier",
        PullDir = 0,
        MarkerLeft = -_amp,
        MarkerRight = _amp,
        RepProgress = (_halfReps % 2) / 2.0,
    };

    public override Dictionary<string, double> Live()
    {
        var m = Score.LiveMetrics();
        m["reps"] = Reps;
        m["peakSpeedDps"] = Math.Round(_peakSpeed);
        return m;
    }

    public override (double score, Dictionary<string, double> metrics) Finish()
    {
        var m = Score.FinalMetrics();
        m["reps"] = Reps;
        m["peakSpeedDps"] = Math.Round(_peakSpeed);
        m["avgRepSpeedDps"] = _halfReps > 0 ? Math.Round(_sumPeakSpeed / _halfReps) : 0;
        m["repsPerMin"] = C.WorkS > 0 ? Math.Round(Reps / C.WorkS * 60, 1) : 0;
        return (Reps, m);
    }

    public override bool QualifiesForProgression(Dictionary<string, double> metrics) => true;
}

/// <summary>
/// Slow return (eccentric): push out against a spring, hold, then let the spring bring you back over
/// 3-4 s following the ghost. The lowering phase is where the eccentric work happens.
/// </summary>
public sealed class SlowReturnLogic : SetLogic
{
    private const double OutS = 1.6, HoldS = 1.0, PauseS = 1.0;
    private double _amp, _returnS;
    private int _rep;
    private int _repDir;
    private double _repStart;
    private double _retErrSq, _retTime;
    private int _returns;
    private readonly ScoreTracker _returnTracker = new();

    private double Cycle => OutS + HoldS + _returnS + PauseS;

    public override void Begin(SetContext ctx)
    {
        base.Begin(ctx);
        _amp = ctx.P("amplitude", 90);
        _returnS = Math.Clamp(ctx.P("returnS", 3.5), 2, 8);
        _repDir = ctx.Dir;
    }

    /// <summary>Ghost angle at time since rep start, for the given direction.</summary>
    private double GhostAt(double r, int dir)
    {
        if (r < 0) return 0;
        double a;
        if (r < OutS) { double f = r / OutS; a = _amp * (f * f * (3 - 2 * f)); }
        else if (r < OutS + HoldS) a = _amp;
        else if (r < OutS + HoldS + _returnS) a = _amp * (1 - (r - OutS - HoldS) / _returnS);
        else a = 0;
        return dir * a;
    }

    private string PhaseName(double r) =>
        r < OutS ? "out" : r < OutS + HoldS ? "hold" : r < OutS + HoldS + _returnS ? "return" : "pause";

    private int DirForRep(int rep) => C.Config.Direction == PushDir.Alternate ? (rep % 2 == 0 ? C.Dir : -C.Dir) : C.Dir;

    public override SetFrame Step(in ActivityTick t, double elapsed, bool scoring)
    {
        double load = C.LoadNm(t.Intensity);
        double k = load / Math.Max(10, _amp);
        double work = Math.Max(0, elapsed - 1.5);
        int rep = (int)(work / Cycle);
        if (rep != _rep) { _rep = rep; Emit("rep", new { reps = rep }); }
        _repDir = DirForRep(rep);
        double r = work - rep * Cycle;
        double ghost = elapsed < 1.5 ? 0 : GhostAt(r, _repDir);

        if (scoring)
        {
            Score.Add(t.Time, t.Dt, t.AngleDeg, ghost, 5, -t.MotorTorqueNm, t.VelocityDps);
            if (PhaseName(r) == "return")
            {
                double e = t.AngleDeg - ghost;
                _retErrSq += e * e * t.Dt;
                _retTime += t.Dt;
                _returnTracker.Add(t.Time, t.Dt, t.AngleDeg, ghost, 5, -t.MotorTorqueNm, t.VelocityDps);
                if (r + t.Dt >= OutS + HoldS + _returnS) _returns++;
            }
        }
        // Spring centred at 0; small constant pre-load keeps tension at the centre.
        double reactive = Spring(t.AngleDeg, 0, k, load * 1.2);
        return new SetFrame(0, reactive, ghost);
    }

    public override GuideFrame Guide(double elapsed, in ActivityTick t)
    {
        double work = Math.Max(0, elapsed - 1.5);
        int rep = (int)(work / Cycle);
        double r = work - rep * Cycle;
        int dir = DirForRep(rep);
        string phase = elapsed < 1.5 ? "ready" : PhaseName(r);
        string cue = phase switch
        {
            "ready" => "GET READY",
            "out" => dir > 0 ? "TURN OUT ▶" : "◀ TURN OUT",
            "hold" => "HOLD",
            "return" => "EASE BACK… SLOWLY",
            _ => "RESET",
        };
        double rp = phase switch
        {
            "out" => r / OutS,
            "hold" => (r - OutS) / HoldS,
            "return" => (r - OutS - HoldS) / _returnS,
            _ => 0,
        };
        // Path: sample the ghost into the future (it may cross into the next rep).
        var path = new double[30];
        for (int i = 0; i < path.Length; i++)
        {
            double w = work + i * 0.1;
            int rr = (int)(w / Cycle);
            path[i] = elapsed < 1.5 ? 0 : GhostAt(w - rr * Cycle, DirForRep(rr));
        }
        return new GuideFrame
        {
            TargetDeg = path[0],
            ZoneDeg = 5,
            Path = path,
            Cue = cue,
            SubCue = phase == "return" ? $"Match the ghost · {_returnS:0.0} s down" : $"Rep {rep + 1}",
            PullDir = path[0] == 0 ? 0 : -Math.Sign(path[0]),
            RepProgress = Math.Clamp(rp, 0, 1),
        };
    }

    public override Dictionary<string, double> Live()
    {
        var m = Score.LiveMetrics();
        m["reps"] = _returns;
        m["returnRmsDeg"] = _retTime > 0 ? Math.Round(Math.Sqrt(_retErrSq / _retTime), 1) : 0;
        return m;
    }

    public override (double score, Dictionary<string, double> metrics) Finish()
    {
        var m = Score.FinalMetrics();
        double rms = _retTime > 0 ? Math.Sqrt(_retErrSq / _retTime) : 99;
        m["reps"] = _returns;
        m["returnRmsDeg"] = Math.Round(rms, 2);
        m["eccentricWorkJ"] = Math.Round(_returnTracker.WorkEccJ, 1);
        m["meanErrDeg"] = Math.Round(_returnTracker.MeanAbsErr, 2);
        double score = Math.Round(Math.Clamp(100 * (1 - rms / 12), 0, 100), 1);
        return (score, m);
    }
}

/// <summary>
/// Iso-speed sweeps (isokinetic style): the ghost sweeps at a fixed speed and the resistance is a damper
/// tuned so that moving at that speed means pushing exactly the prescribed load.
/// </summary>
public sealed class IsoSpeedLogic : SetLogic
{
    private const double Dwell = 0.4;
    private double _amp, _speed;
    private double _speedErrSum, _speedTime;

    public override void Begin(SetContext ctx)
    {
        base.Begin(ctx);
        _amp = ctx.P("amplitude", 75);
        _speed = Math.Clamp(ctx.P("speedDps", 90), 20, 300);
    }

    private double Sweep => 2 * _amp / _speed;
    private double Cycle => 2 * (Sweep + Dwell);

    /// <summary>Ghost: start at 0, go to +A, dwell, sweep to -A, dwell, ...</summary>
    private (double angle, double vel) GhostAt(double w)
    {
        if (w < 0) return (0, 0);
        double first = _amp / _speed; // 0 -> +A (or -A)
        int s = C.Dir >= 0 ? 1 : -1;
        if (w < first) return (s * _speed * w, s * _speed);
        if (w < first + Dwell) return (s * _amp, 0);
        double r = (w - first - Dwell) % Cycle;
        if (r < Sweep) return (s * (_amp - _speed * r), -s * _speed);
        if (r < Sweep + Dwell) return (-s * _amp, 0);
        r -= Sweep + Dwell;
        if (r < Sweep) return (s * (-_amp + _speed * r), s * _speed);
        return (s * _amp, 0);
    }

    public override SetFrame Step(in ActivityTick t, double elapsed, bool scoring)
    {
        double load = C.LoadNm(t.Intensity);
        double c = load / _speed;
        var (ghost, gv) = GhostAt(elapsed - 1.5);
        if (scoring)
        {
            Score.Add(t.Time, t.Dt, t.AngleDeg, ghost, 6, -t.MotorTorqueNm, t.VelocityDps);
            if (gv != 0)
            {
                _speedErrSum += Math.Abs(t.VelocityDps - gv) / _speed * t.Dt;
                _speedTime += t.Dt;
            }
        }
        return new SetFrame(0, Damper(t.VelocityDps, c, load * 1.3), ghost);
    }

    public override GuideFrame Guide(double elapsed, in ActivityTick t)
    {
        var (g, gv) = GhostAt(elapsed - 1.5);
        return new GuideFrame
        {
            TargetDeg = g,
            ZoneDeg = 6,
            Path = SamplePath(w => GhostAt(w - 1.5).angle, elapsed),
            Cue = elapsed < 1.5 ? "READY…" : gv == 0 ? "TURN" : "STAY WITH THE GHOST",
            SubCue = $"{_speed:0}°/s sweep · steady pressure",
            TargetSpeedDps = gv,
            PullDir = 0,
            MarkerLeft = -_amp,
            MarkerRight = _amp,
        };
    }

    public override (double score, Dictionary<string, double> metrics) Finish()
    {
        var m = Score.FinalMetrics();
        double speedErr = _speedTime > 0 ? _speedErrSum / _speedTime : 1;
        m["speedErrPct"] = Math.Round(speedErr * 100, 1);
        double score = Math.Round(Math.Clamp(0.6 * Score.AccuracyScore(8) + 40 * (1 - Math.Min(1, speedErr)), 0, 100), 1);
        return (score, m);
    }

    public override bool QualifiesForProgression(Dictionary<string, double> metrics) =>
        metrics.TryGetValue("meanErrDeg", out var e) && e < 6;
}
