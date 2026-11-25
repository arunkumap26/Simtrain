using RimForce.Core.Engine;
using RimForce.Core.Hardware;

namespace RimForce.Core.Drills;

/// <summary>Everything a set needs to know about itself.</summary>
public sealed class SetContext
{
    public required DrillDefinition Definition { get; init; }
    public required DrillConfig Config { get; init; }
    public required WheelProfile Profile { get; init; }
    public required int ItemIndex { get; init; }
    public required int SetIndex { get; init; }
    /// <summary>Resolved push direction for this set (+1 = user pushes clockwise, -1 = anticlockwise).</summary>
    public required int Dir { get; init; }
    public required Hand Hand { get; init; }
    public GripStyle Grip { get; init; } = GripStyle.Full;
    /// <summary>Your max torque for this hand/direction (latest breakpoint) or the device max if untested.</summary>
    public required double ReferenceMaxNm { get; init; }
    public required bool ReferenceIsBreakpoint { get; init; }
    public required Random Rng { get; init; }
    /// <summary>Load multiplier for weaker grip styles etc.</summary>
    public double LoadFactor { get; init; } = 1.0;

    public double WorkS => Config.WorkS;
    public double DrillCapNm => Profile.MaxTorqueNm * Definition.DrillCapPct / 100.0;

    /// <summary>The prescribed load in Nm at a given master intensity.</summary>
    public double LoadNm(double intensity) =>
        Math.Min(DrillCapNm, Config.LoadPct / 100.0 * ReferenceMaxNm * LoadFactor * intensity);

    public double P(string key, double fallback) => Config.P(key, fallback);
    public string HandLabel => Hand switch { Hand.Left => "LEFT HAND", Hand.Right => "RIGHT HAND", _ => "BOTH HANDS" };
}

/// <summary>Forces wanted for one tick of a set.</summary>
public readonly record struct SetFrame(
    double OpenLoopNm,
    double ReactiveNm,
    double TargetDeg,
    double VibrationNm = 0,
    double? RunawayOverrideDps = null,
    bool EndNow = false,
    double? HandsOffSpeedWhenBrakingDps = null);

/// <summary>
/// The per-drill logic for one working set. The session runner handles position/countdown/rest; the set
/// logic only decides forces, the ghost target and scoring while force is on.
/// </summary>
public abstract class SetLogic
{
    protected SetContext C { get; private set; } = null!;
    protected ScoreTracker Score { get; } = new();
    private readonly List<ActivityEvent> _events = new();

    public virtual void Begin(SetContext ctx) => C = ctx;

    /// <param name="elapsed">Seconds since the work phase began (force ramping in during the first 1.5 s).</param>
    /// <param name="scoring">True once the force is fully ramped in.</param>
    public abstract SetFrame Step(in ActivityTick tick, double elapsed, bool scoring);

    /// <summary>Ghost / cue information for the UI. Called at UI rate.</summary>
    public abstract GuideFrame Guide(double elapsed, in ActivityTick tick);

    public virtual bool IsDone(double elapsed) => elapsed >= C.WorkS;

    /// <summary>Live numbers for the HUD while the set runs.</summary>
    public virtual Dictionary<string, double> Live() => Score.LiveMetrics();

    /// <summary>Final score 0-100 (or a raw value such as reps / Nm for tests) and metrics.</summary>
    public abstract (double score, Dictionary<string, double> metrics) Finish();

    /// <summary>Set completed well enough to progress (used by the program).</summary>
    public virtual bool QualifiesForProgression(Dictionary<string, double> metrics) =>
        !metrics.TryGetValue("meanErrDeg", out var e) || e < 3.0;

    public virtual int Hands => C.Hand == Hand.Both ? 2 : 1;
    public ScoreTracker Tracker => Score;

    protected void Emit(string kind, object? data = null) => _events.Add(new ActivityEvent(kind, data));

    public IReadOnlyList<ActivityEvent> DrainEvents()
    {
        if (_events.Count == 0) return Array.Empty<ActivityEvent>();
        var copy = _events.ToArray();
        _events.Clear();
        return copy;
    }

    // ---- shared helpers ----

    /// <summary>Constant path for the runway (n samples).</summary>
    protected static double[] Flat(double v, int n = 30)
    {
        var a = new double[n];
        Array.Fill(a, v);
        return a;
    }

    protected double[] SamplePath(Func<double, double> targetAt, double fromElapsed, int n = 30, double step = 0.1)
    {
        var a = new double[n];
        for (int i = 0; i < n; i++) a[i] = targetAt(fromElapsed + i * step);
        return a;
    }

    /// <summary>Software spring: -k (angle - centre), clamped.</summary>
    protected static double Spring(double angle, double centre, double kNmPerDeg, double maxNm) =>
        Math.Clamp(-kNmPerDeg * (angle - centre), -maxNm, maxNm);

    /// <summary>Software damper: -c * velocity, clamped.</summary>
    protected static double Damper(double vel, double cNmPerDps, double maxNm) =>
        Math.Clamp(-cNmPerDps * vel, -maxNm, maxNm);

    /// <summary>Software friction: smooth sign function so it does not chatter at rest.</summary>
    protected static double Friction(double vel, double nm) => -nm * Math.Tanh(vel / 8.0);

    protected static string DirWord(int dir) => dir > 0 ? "RIGHT" : "LEFT";
}

/// <summary>Accumulates accuracy and work metrics while force is on.</summary>
public sealed class ScoreTracker
{
    private double _time, _inZone, _absErr, _sqErr, _errMean, _errM2;
    private long _n;
    private double _impulse, _workConc, _workEcc, _tut, _peak, _sumTorque, _maxDev, _peakPower;
    private readonly List<float> _trace = new();
    private double _lastTraceT = double.NegativeInfinity;

    public double Time => _time;
    public double InZoneFraction => _time > 0 ? _inZone / _time : 0;
    public double MeanAbsErr => _time > 0 ? _absErr / _time : 0;
    public double RmsErr => _time > 0 ? Math.Sqrt(_sqErr / _time) : 0;
    public double ErrSd => _n > 1 ? Math.Sqrt(_errM2 / (_n - 1)) : 0;
    public double MaxDev => _maxDev;
    public double Impulse => _impulse;
    public double PeakTorque => _peak;
    public double AvgTorque => _time > 0 ? _sumTorque / _time : 0;
    public double WorkConcJ => _workConc;
    public double WorkEccJ => _workEcc;
    public double Tut => _tut;
    public double PeakPowerW => _peakPower;

    /// <param name="resistedNm">Torque the user resists (positive = user pushes clockwise).</param>
    public void Add(double t, double dt, double angle, double target, double zone, double resistedNm, double velDps, bool trackAccuracy = true)
    {
        if (dt <= 0) return;
        _time += dt;
        double err = angle - target;
        if (trackAccuracy)
        {
            double ae = Math.Abs(err);
            if (ae <= zone) _inZone += dt;
            _absErr += ae * dt;
            _sqErr += err * err * dt;
            _maxDev = Math.Max(_maxDev, ae);
            _n++;
            double d = err - _errMean;
            _errMean += d / _n;
            _errM2 += d * (err - _errMean);
        }
        double tq = Math.Abs(resistedNm);
        _impulse += tq * dt;
        _sumTorque += tq * dt;
        if (tq > 0.5) _tut += dt;
        _peak = Math.Max(_peak, tq);
        double p = resistedNm * velDps * Units.DegToRad;
        if (p >= 0) _workConc += p * dt; else _workEcc += -p * dt;
        _peakPower = Math.Max(_peakPower, Math.Abs(p));

        if (t - _lastTraceT >= 0.05)
        {
            _lastTraceT = t;
            _trace.Add((float)t); _trace.Add((float)angle); _trace.Add((float)target); _trace.Add((float)resistedNm);
        }
    }

    public float[] Trace(double t0)
    {
        var a = _trace.ToArray();
        for (int i = 0; i < a.Length; i += 4) a[i] = (float)(a[i] - t0);
        return a;
    }

    public Dictionary<string, double> LiveMetrics() => new()
    {
        ["timeInZonePct"] = Math.Round(InZoneFraction * 100, 1),
        ["meanErrDeg"] = Math.Round(MeanAbsErr, 2),
        ["impulseNms"] = Math.Round(_impulse, 1),
        ["workJ"] = Math.Round(_workConc + _workEcc, 1),
        ["tutS"] = Math.Round(_tut, 1),
        ["peakNm"] = Math.Round(_peak, 2),
    };

    public Dictionary<string, double> FinalMetrics() => new()
    {
        ["timeInZonePct"] = Math.Round(InZoneFraction * 100, 1),
        ["meanErrDeg"] = Math.Round(MeanAbsErr, 2),
        ["rmsErrDeg"] = Math.Round(RmsErr, 2),
        ["sdDeg"] = Math.Round(ErrSd, 2),
        ["maxDevDeg"] = Math.Round(_maxDev, 1),
        ["impulseNms"] = Math.Round(_impulse, 1),
        ["workConcJ"] = Math.Round(_workConc, 1),
        ["workEccJ"] = Math.Round(_workEcc, 1),
        ["tutS"] = Math.Round(_tut, 1),
        ["peakNm"] = Math.Round(_peak, 2),
        ["avgNm"] = Math.Round(AvgTorque, 2),
        ["peakPowerW"] = Math.Round(_peakPower, 1),
    };

    /// <summary>Standard accuracy score: 70% time in zone, 30% closeness.</summary>
    public double AccuracyScore(double errScaleDeg = 6) =>
        Math.Round(Math.Clamp(100 * (0.7 * InZoneFraction + 0.3 * Math.Max(0, 1 - MeanAbsErr / errScaleDeg)), 0, 100), 1);
}
