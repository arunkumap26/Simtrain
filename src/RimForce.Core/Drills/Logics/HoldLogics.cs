using RimForce.Core.Engine;
using RimForce.Core.Metrics;

namespace RimForce.Core.Drills.Logics;

internal static class Cues
{
    public static string Correction(double err, double zone, string holdWord = "HOLD")
    {
        if (err < -zone) return "TURN RIGHT";
        if (err > zone) return "TURN LEFT";
        return holdWord;
    }

    /// <summary>At 3 o'clock clockwise = pressing down; at 9 o'clock clockwise = lifting.</summary>
    public static string? OneHandAction(Hand hand, int dir) => hand switch
    {
        Hand.Right => dir > 0 ? "press" : "lift",
        Hand.Left => dir > 0 ? "lift" : "press",
        _ => null,
    };

    public static string GripLabel(GripStyle g) => g switch
    {
        GripStyle.OpenHand => "OPEN HAND",
        GripStyle.Fingertips => "FINGERTIPS",
        GripStyle.Pinch => "PINCH",
        _ => "FULL GRIP",
    };
}

/// <summary>Corner hold / one-hand hold / grip variants: constant torque, hold a target angle.</summary>
public class HoldLogic : SetLogic
{
    protected double AngleAbs;
    protected double Zone;
    protected double Target => C.Dir * AngleAbs;

    public override void Begin(SetContext ctx)
    {
        base.Begin(ctx);
        AngleAbs = ctx.P("angle", 0);
        Zone = ctx.P("zone", 3);
    }

    protected virtual double LoadFraction(double elapsed) => 1.0;

    public override SetFrame Step(in ActivityTick t, double elapsed, bool scoring)
    {
        double load = C.LoadNm(t.Intensity) * LoadFraction(elapsed);
        if (scoring) Score.Add(t.Time, t.Dt, t.AngleDeg, Target, Zone, -t.MotorTorqueNm, t.VelocityDps);
        return new SetFrame(-C.Dir * load, 0, Target, Vibration(t.Time, elapsed, t.Intensity));
    }

    protected virtual double Vibration(double time, double elapsed, double intensity) => 0;

    public override GuideFrame Guide(double elapsed, in ActivityTick t)
    {
        double err = t.AngleDeg - Target;
        return new GuideFrame
        {
            TargetDeg = Target,
            ZoneDeg = Zone,
            Path = Flat(Target),
            Cue = elapsed < 1.5 ? "LOAD COMING IN" : Cues.Correction(err, Zone),
            SubCue = SubCue(elapsed),
            PullDir = -C.Dir,
            RepProgress = Math.Clamp(elapsed / Math.Max(1, C.WorkS), 0, 1),
            Hand = C.Hand == Hand.Both ? null : (C.Hand == Hand.Left ? "LEFT · 9 o'clock" : "RIGHT · 3 o'clock"),
            Grip = C.Grip == GripStyle.Full ? null : Cues.GripLabel(C.Grip),
            Action = Cues.OneHandAction(C.Hand, C.Dir),
        };
    }

    protected virtual string? SubCue(double elapsed) =>
        $"Wheel pulls {(C.Dir > 0 ? "left" : "right")} · push {DirWord(C.Dir).ToLowerInvariant()}";

    public override (double score, Dictionary<string, double> metrics) Finish()
    {
        var m = Score.FinalMetrics();
        return (Score.AccuracyScore(), m);
    }
}

/// <summary>Hold + kerb strikes: short bursts of vibration on top of the hold.</summary>
public sealed class KerbHoldLogic : HoldLogic
{
    protected override double Vibration(double time, double elapsed, double intensity)
    {
        double freq = C.P("vibHz", 24);
        double amp = C.P("vibNm", 0.8) * intensity;
        double cycle = C.P("kerbEveryS", 1.2);
        double on = 0.35;
        double phase = elapsed % cycle;
        if (elapsed < 2 || phase > on) return 0;
        double env = Math.Sin(Math.PI * phase / on); // smooth burst
        return amp * env * Math.Sin(2 * Math.PI * freq * time);
    }

    protected override string? SubCue(double elapsed) => "Kerb strikes — keep the wheel still";
}

/// <summary>Hold 0° (or target) while the load moves: wave, pyramid or random.</summary>
public sealed class WaveHoldLogic : HoldLogic
{
    private double[] _randomKnots = Array.Empty<double>();

    public override void Begin(SetContext ctx)
    {
        base.Begin(ctx);
        _randomKnots = Enumerable.Range(0, 64).Select(_ => 0.45 + 0.55 * ctx.Rng.NextDouble()).ToArray();
    }

    protected override double LoadFraction(double e)
    {
        int profile = (int)C.P("profile", 0);
        double period = C.P("periodS", 8);
        switch (profile)
        {
            case 1: // pyramid 50-65-80-95-80-65-50
                double[] steps = { 0.5, 0.65, 0.8, 0.95, 0.8, 0.65, 0.5 };
                int i = (int)Math.Clamp(e / Math.Max(1, C.WorkS) * steps.Length, 0, steps.Length - 1);
                return steps[i];
            case 2: // smooth random every 2 s
                double k = e / 2.0;
                int a = (int)k % _randomKnots.Length, b = (a + 1) % _randomKnots.Length;
                double f = k - Math.Floor(k);
                f = f * f * (3 - 2 * f);
                return _randomKnots[a] + (_randomKnots[b] - _randomKnots[a]) * f;
            default:
                return 0.6 + 0.4 * Math.Sin(2 * Math.PI * e / period - Math.PI / 2);
        }
    }

    protected override string? SubCue(double elapsed) => $"Load at {LoadFraction(elapsed) * 100:0}% · stay steady";
}

/// <summary>
/// Breakpoint test: hold 0° while torque rises slowly. The breakpoint is the torque at which the wheel
/// leaves ±slipDeg for longer than slipTimeS. This is the benchmark every other load scales from.
/// If the ramp reaches the torque cap while you are still holding (common for two-handed tests on a
/// 12 Nm base), the test switches to hold-to-failure at the cap and estimates your max from how long
/// you last, using Rohmert's endurance curve: max ≈ cap / f(endurance time).
/// </summary>
public sealed class BreakpointLogic : SetLogic
{
    private double _slipSince = double.NaN;
    private double _torqueAtSlip;
    private double? _breakpoint;
    private bool _estimated;
    private bool _lowerBound;
    private bool _enduranceMode;
    private double _enduranceStart;
    private double _enduranceNm;
    private bool _finished;
    private double _cappedSince = double.NaN;
    private double _lastWanted;
    private double _bestHeld;
    private double _elapsed;

    private double StartNm => C.Profile.MaxTorqueNm * C.P("startPct", 15) / 100.0;
    private double RateNmPerS => C.Profile.MaxTorqueNm * C.P("rampPctPerS", 1.0) / 100.0;
    private double SlipDeg => C.P("slipDeg", 5);
    private double SlipTime => C.P("slipTimeS", 0.5);
    private double MaxEnduranceS => C.P("maxEnduranceS", 240);

    public double? BreakpointNm => _breakpoint;
    public bool Estimated => _estimated;
    public bool EnduranceMode => _enduranceMode;

    private double Wanted(double elapsed) => _enduranceMode
        ? _enduranceNm
        : Math.Min(C.DrillCapNm, StartNm + RateNmPerS * Math.Max(0, elapsed - 1.5));

    public override SetFrame Step(in ActivityTick t, double elapsed, bool scoring)
    {
        _elapsed = elapsed;
        double wanted = Wanted(elapsed);
        _lastWanted = wanted;
        double actual = Math.Abs(t.MotorTorqueNm);

        if (!_finished && scoring)
        {
            Score.Add(t.Time, t.Dt, t.AngleDeg, 0, SlipDeg, -t.MotorTorqueNm, t.VelocityDps);
            if (Math.Abs(t.AngleDeg) > SlipDeg)
            {
                if (double.IsNaN(_slipSince)) { _slipSince = t.Time; _torqueAtSlip = actual; }
                else if (t.Time - _slipSince >= SlipTime) Conclude(elapsed);
            }
            else
            {
                _slipSince = double.NaN;
                _bestHeld = Math.Max(_bestHeld, actual);
            }

            // The governor (safety cap) or the drill cap is limiting us and the user is still holding.
            if (!_enduranceMode && Math.Abs(t.AngleDeg) <= SlipDeg && (wanted - actual > 0.1 || wanted >= C.DrillCapNm - 1e-6))
            {
                if (double.IsNaN(_cappedSince)) _cappedSince = t.Time;
                else if (t.Time - _cappedSince > 3)
                {
                    _enduranceMode = true;
                    _enduranceStart = elapsed;
                    _enduranceNm = actual;
                    Emit("breakpointCapped", new { nm = Math.Round(actual, 2) });
                }
            }
            else if (!_enduranceMode) _cappedSince = double.NaN;

            if (_enduranceMode && elapsed - _enduranceStart >= MaxEnduranceS) { _lowerBound = true; Conclude(elapsed); }
        }

        return new SetFrame(_finished ? 0 : -C.Dir * wanted, 0, 0, EndNow: _finished);
    }

    private void Conclude(double elapsed)
    {
        _finished = true;
        if (_enduranceMode)
        {
            double held = elapsed - _enduranceStart - (double.IsNaN(_slipSince) ? 0 : SlipTime);
            double f = StrengthMath.FractionForEndurance(Math.Max(1, held));
            _breakpoint = Math.Round(_enduranceNm / f, 2);
            _estimated = true;
        }
        else
        {
            _breakpoint = Math.Round(Math.Max(_torqueAtSlip, 0.1), 2);
        }
        Emit("breakpoint", new { nm = _breakpoint, estimated = _estimated, dir = C.Dir, hand = C.Hand.ToString() });
    }

    public override bool IsDone(double elapsed) => _finished || elapsed >= Math.Max(30, C.WorkS) + (_enduranceMode ? MaxEnduranceS + 5 : 0);

    public override GuideFrame Guide(double elapsed, in ActivityTick t) => new()
    {
        TargetDeg = 0,
        ZoneDeg = SlipDeg,
        Path = Flat(0),
        Cue = elapsed < 1.5 ? "GET READY" : (Math.Abs(t.AngleDeg) > SlipDeg ? "FIGHT IT!" : _enduranceMode ? "AT THE CAP · HOLD ON" : "HOLD CENTRE"),
        SubCue = _enduranceMode
            ? $"Holding {_enduranceNm:0.0} Nm for {elapsed - _enduranceStart:0} s · last as long as you can"
            : $"Rising to {_lastWanted:0.0} Nm · push {DirWord(C.Dir).ToLowerInvariant()}",
        PullDir = -C.Dir,
        Hand = C.Hand == Hand.Both ? null : (C.Hand == Hand.Left ? "LEFT · 9 o'clock" : "RIGHT · 3 o'clock"),
        Action = Cues.OneHandAction(C.Hand, C.Dir),
        RepProgress = _enduranceMode
            ? Math.Clamp((elapsed - _enduranceStart) / MaxEnduranceS, 0, 1)
            : Math.Clamp(_lastWanted / Math.Max(0.1, C.DrillCapNm), 0, 1),
    };

    public override Dictionary<string, double> Live()
    {
        var m = Score.LiveMetrics();
        m["rampNm"] = Math.Round(_lastWanted, 2);
        m["bestHeldNm"] = Math.Round(_bestHeld, 2);
        m["enduranceS"] = _enduranceMode ? Math.Round(_elapsed - _enduranceStart, 1) : 0;
        return m;
    }

    public override (double score, Dictionary<string, double> metrics) Finish()
    {
        var m = Score.FinalMetrics();
        double bp = _breakpoint ?? Math.Round(_bestHeld, 2);
        m["breakpointNm"] = bp;
        m["estimated"] = _estimated ? 1 : 0;
        // Still holding when the time limit ended the test: the estimate is a minimum ("≥").
        m["lowerBound"] = _lowerBound ? 1 : 0;
        m["capNm"] = Math.Round(_enduranceNm, 2);
        m["valid"] = _breakpoint != null ? 1 : 0;
        return (bp, m);
    }

    public override bool QualifiesForProgression(Dictionary<string, double> metrics) => true;
}
