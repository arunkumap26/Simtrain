using RimForce.Core.Engine;

namespace RimForce.Core.Drills.Logics;

/// <summary>
/// Snap catch (perturbation training): random left/right torque pulses at random times. Keep the wheel
/// inside ±5°. Pulses ramp over >= 90 ms (the governor guarantees >= 80 ms) and never exceed the cap.
/// </summary>
public sealed class SnapCatchLogic : SetLogic
{
    private const double RampS = 0.09;
    private double _nextPulse;
    private double _pulseStart = double.NaN;
    private double _pulseHold;
    private int _pulseSign;
    private double _pulseMag;
    private double _pulseMaxDev;
    private double _recoverSince = double.NaN;
    private readonly List<(double dev, double recoveryMs)> _pulses = new();
    private double _zone;
    private bool _recovered = true;

    public override void Begin(SetContext ctx)
    {
        base.Begin(ctx);
        _zone = ctx.P("zone", 5);
        _nextPulse = 2.5 + ctx.Rng.NextDouble();
    }

    private double PulseValue(double e)
    {
        if (double.IsNaN(_pulseStart)) return 0;
        double r = e - _pulseStart;
        if (r < 0) return 0;
        if (r < RampS) return r / RampS;
        if (r < RampS + _pulseHold) return 1;
        if (r < 2 * RampS + _pulseHold) return 1 - (r - RampS - _pulseHold) / RampS;
        return 0;
    }

    public override SetFrame Step(in ActivityTick t, double elapsed, bool scoring)
    {
        double load = C.LoadNm(t.Intensity);
        double baseLoad = load * Math.Clamp(C.P("baseFrac", 0.25), 0, 0.6);

        if (scoring && elapsed >= _nextPulse && double.IsNaN(_pulseStart))
        {
            _pulseStart = elapsed;
            _pulseHold = 0.15 + C.Rng.NextDouble() * 0.12;
            _pulseSign = C.Rng.NextDouble() < 0.5 ? -1 : 1;
            _pulseMag = load * (0.6 + 0.4 * C.Rng.NextDouble());
            _pulseMaxDev = 0;
            _recovered = false;
            _recoverSince = double.NaN;
            Emit("pulse", new { dir = _pulseSign });
        }

        double pv = PulseValue(elapsed);
        if (!double.IsNaN(_pulseStart))
        {
            double r = elapsed - _pulseStart;
            _pulseMaxDev = Math.Max(_pulseMaxDev, Math.Abs(t.AngleDeg));
            if (!_recovered && r > RampS)
            {
                if (Math.Abs(t.AngleDeg) <= 2)
                {
                    if (double.IsNaN(_recoverSince)) _recoverSince = elapsed;
                    else if (elapsed - _recoverSince >= 0.2) _recovered = true;
                }
                else _recoverSince = double.NaN;
            }
            if (r > 2 * RampS + _pulseHold + 1.0 || r > 4)
            {
                double rec = _recovered ? (_recoverSince - _pulseStart) * 1000 : 4000;
                _pulses.Add((_pulseMaxDev, rec));
                Emit("catch", new { ok = _pulseMaxDev <= _zone, dev = Math.Round(_pulseMaxDev, 1), ms = Math.Round(rec) });
                _pulseStart = double.NaN;
                _nextPulse = elapsed + 0.6 + C.Rng.NextDouble() * (C.P("maxGapS", 2.6) - 0.6);
            }
        }

        if (scoring) Score.Add(t.Time, t.Dt, t.AngleDeg, 0, _zone, -t.MotorTorqueNm, t.VelocityDps);
        double open = -C.Dir * baseLoad + _pulseSign * _pulseMag * pv;
        return new SetFrame(open, 0, 0, RunawayOverrideDps: 320);
    }

    public override GuideFrame Guide(double elapsed, in ActivityTick t) => new()
    {
        TargetDeg = 0,
        ZoneDeg = _zone,
        Path = Flat(0),
        Cue = elapsed < 1.5 ? "BRACE" : (!double.IsNaN(_pulseStart) && elapsed - _pulseStart < 0.6 ? "CATCH!" : "STAY CENTRED"),
        SubCue = $"{_pulses.Count(p => p.dev <= _zone)}/{_pulses.Count} caught",
        PullDir = -C.Dir,
    };

    public override Dictionary<string, double> Live()
    {
        var m = Score.LiveMetrics();
        m["pulses"] = _pulses.Count;
        m["caught"] = _pulses.Count(p => p.dev <= _zone);
        m["lastDevDeg"] = _pulses.Count > 0 ? Math.Round(_pulses[^1].dev, 1) : 0;
        return m;
    }

    public override (double score, Dictionary<string, double> metrics) Finish()
    {
        var m = Score.FinalMetrics();
        m["pulses"] = _pulses.Count;
        m["caught"] = _pulses.Count(p => p.dev <= _zone);
        m["avgDevDeg"] = _pulses.Count > 0 ? Math.Round(_pulses.Average(p => p.dev), 2) : 0;
        m["avgRecoveryMs"] = _pulses.Count > 0 ? Math.Round(_pulses.Average(p => p.recoveryMs)) : 0;
        double score = _pulses.Count == 0 ? 0 : Math.Round(_pulses.Average(p => Math.Max(0, 1 - p.dev / 20)) * 100, 1);
        return (score, m);
    }

    public override bool QualifiesForProgression(Dictionary<string, double> metrics) =>
        metrics.TryGetValue("avgDevDeg", out var d) && d <= 5;
}

/// <summary>
/// Apex hunter (reaction under load): a spring pulls to centre; targets pop up at random angles. Get there
/// fast and hold for 1 s to "hit the apex". Trains reaction, wrist stability and shoulder control.
/// </summary>
public sealed class ApexHunterLogic : SetLogic
{
    private double _target;
    private double _targetSince;
    private double _inSince = double.NaN;
    private double _moveAt = double.NaN;
    private double _prevTarget;
    private int _hits, _misses;
    private readonly List<double> _reaction = new(), _capture = new();
    private double _amp, _zone, _holdS;

    public override void Begin(SetContext ctx)
    {
        base.Begin(ctx);
        _amp = ctx.P("amplitude", 100);
        _zone = ctx.P("zone", 4);
        _holdS = ctx.P("holdS", 1.0);
        _target = 0;
        _targetSince = 0;
    }

    private void NewTarget(double elapsed)
    {
        _prevTarget = _target;
        double t;
        int guard = 0;
        do
        {
            t = (C.Rng.NextDouble() * 2 - 1) * _amp;
        } while ((Math.Abs(t - _target) < 30 || Math.Abs(t) < 12) && guard++ < 20);
        _target = Math.Round(t);
        _targetSince = elapsed;
        _inSince = double.NaN;
        _moveAt = double.NaN;
        Emit("target", new { deg = _target });
    }

    public override SetFrame Step(in ActivityTick t, double elapsed, bool scoring)
    {
        double load = C.LoadNm(t.Intensity);
        double k = load / Math.Max(20, _amp * 0.6);
        if (scoring)
        {
            if (_target == 0 && _targetSince == 0) NewTarget(elapsed);
            double age = elapsed - _targetSince;
            if (double.IsNaN(_moveAt) && Math.Abs(t.AngleDeg - _prevTarget) > 3 && Math.Sign(t.AngleDeg - _prevTarget) == Math.Sign(_target - _prevTarget))
                _moveAt = age;
            if (Math.Abs(t.AngleDeg - _target) <= _zone)
            {
                if (double.IsNaN(_inSince)) _inSince = elapsed;
                else if (elapsed - _inSince >= _holdS)
                {
                    _hits++;
                    if (!double.IsNaN(_moveAt)) _reaction.Add(_moveAt * 1000);
                    _capture.Add((_inSince - _targetSince) * 1000);
                    Emit("apex", new { hits = _hits, ms = Math.Round((_inSince - _targetSince) * 1000) });
                    NewTarget(elapsed);
                }
            }
            else _inSince = double.NaN;
            if (age > 6) { _misses++; Emit("miss", null); NewTarget(elapsed); }
            Score.Add(t.Time, t.Dt, t.AngleDeg, _target, _zone, -t.MotorTorqueNm, t.VelocityDps, trackAccuracy: false);
        }
        // The spring helps you back towards centre between targets: allow brisk spring-assisted moves.
        return new SetFrame(0, Spring(t.AngleDeg, 0, k, load), _target, RunawayOverrideDps: 380);
    }

    public override GuideFrame Guide(double elapsed, in ActivityTick t)
    {
        bool holding = !double.IsNaN(_inSince);
        return new GuideFrame
        {
            TargetDeg = _target,
            ZoneDeg = _zone,
            Path = Flat(_target),
            Cue = elapsed < 1.5 ? "READY…" : holding ? "HOLD IT" : "HIT THE APEX",
            SubCue = $"{_hits} apexes",
            PullDir = t.AngleDeg == 0 ? 0 : -Math.Sign(t.AngleDeg),
            RepProgress = holding ? Math.Clamp((elapsed - _inSince) / _holdS, 0, 1) : 0,
        };
    }

    public override Dictionary<string, double> Live()
    {
        var m = Score.LiveMetrics();
        m["hits"] = _hits;
        m["misses"] = _misses;
        m["avgReactionMs"] = _reaction.Count > 0 ? Math.Round(_reaction.Average()) : 0;
        return m;
    }

    public override (double score, Dictionary<string, double> metrics) Finish()
    {
        var m = Score.FinalMetrics();
        m["hits"] = _hits;
        m["misses"] = _misses;
        m["avgReactionMs"] = _reaction.Count > 0 ? Math.Round(_reaction.Average()) : 0;
        m["avgCaptureMs"] = _capture.Count > 0 ? Math.Round(_capture.Average()) : 0;
        double rate = _hits + _misses > 0 ? (double)_hits / (_hits + _misses) : 0;
        double speed = _capture.Count > 0 ? Math.Clamp(1 - _capture.Average() / 4000, 0.2, 1) : 0;
        return (Math.Round(100 * rate * speed, 1), m);
    }

    public override bool QualifiesForProgression(Dictionary<string, double> metrics) =>
        metrics.TryGetValue("misses", out var mi) && mi <= 1;
}
