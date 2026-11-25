using RimForce.Core.Engine;

namespace RimForce.Core.Drills.Logics;

/// <summary>One piece of a lap: a straight (no load) or a corner (ramp in, hold at apex, ramp out).</summary>
public sealed record LapSegment(string Name, double ApexDeg, double EntryS, double HoldS, double ExitS, double LoadFrac)
{
    /// <summary>Peak ghost speed of a smoothstep is 1.5 x amplitude / duration; keep corner exits under ~165 deg/s.</summary>
    public double ExitS { get; init; } = Math.Max(ExitS, Math.Abs(ApexDeg) / 110.0);
    public double EntryS { get; init; } = Math.Max(EntryS, Math.Abs(ApexDeg) / 110.0);

    public double Duration => EntryS + HoldS + ExitS;
    public bool IsStraight => ApexDeg == 0 || LoadFrac == 0;
    public static LapSegment Straight(double s) => new("Straight", 0, 0, s, 0, 0);
}

public sealed record LapProfile(string Id, string Name, string Description, IReadOnlyList<LapSegment> Segments)
{
    public double LapS => Segments.Sum(s => s.Duration);

    /// <summary>(angle, load fraction, segment index, progress in segment) at time t into the lap.</summary>
    public (double angle, double load, int seg, double progress) At(double t)
    {
        double lap = LapS;
        t %= lap;
        if (t < 0) t += lap;
        for (int i = 0; i < Segments.Count; i++)
        {
            var s = Segments[i];
            if (t < s.Duration)
            {
                double f;
                if (t < s.EntryS) { f = Smooth(t / s.EntryS); }
                else if (t < s.EntryS + s.HoldS) f = 1;
                else f = 1 - Smooth((t - s.EntryS - s.HoldS) / Math.Max(0.01, s.ExitS));
                return (s.ApexDeg * f, s.LoadFrac * f, i, t / s.Duration);
            }
            t -= s.Duration;
        }
        return (0, 0, 0, 0);
    }

    private static double Smooth(double x) { x = Math.Clamp(x, 0, 1); return x * x * (3 - 2 * x); }
}

public static class LapProfiles
{
    /// <summary>Fast circuit: few corners, long sustained loads at small steering angles.</summary>
    public static readonly LapProfile HighSpeed = new("high-speed", "High-speed lap",
        "Long, heavy, fast corners at small angles — sustained forearm and shoulder load.",
        new[]
        {
            LapSegment.Straight(5),
            new LapSegment("T1 · fast right", 35, 1.0, 3.5, 1.0, 0.85),
            LapSegment.Straight(3),
            new LapSegment("T2 · long left sweeper", -45, 1.2, 5.0, 1.2, 0.95),
            LapSegment.Straight(2.5),
            new LapSegment("T3 · esses R", 30, 0.6, 1.0, 0.5, 0.7),
            new LapSegment("T4 · esses L", -32, 0.5, 1.0, 0.5, 0.75),
            new LapSegment("T5 · esses R", 36, 0.5, 1.2, 0.6, 0.8),
            LapSegment.Straight(5),
            new LapSegment("T6 · double-apex right", 50, 1.2, 6.0, 1.2, 1.0),
            LapSegment.Straight(4),
            new LapSegment("T7 · flat-out kink left", -18, 0.6, 1.5, 0.6, 0.6),
            LapSegment.Straight(6),
            new LapSegment("T8 · last right", 40, 1.0, 3.0, 1.0, 0.9),
            LapSegment.Straight(3),
        });

    /// <summary>Street circuit: lots of short, frequent loads at big steering angles.</summary>
    public static readonly LapProfile Street = new("street", "Street lap",
        "Tight 90° corners, a hairpin and a chicane — short, frequent loads and big angles.",
        new[]
        {
            LapSegment.Straight(3),
            new LapSegment("T1 · 90° right", 95, 0.7, 1.4, 0.6, 0.7),
            LapSegment.Straight(2),
            new LapSegment("T2 · 90° left", -90, 0.7, 1.2, 0.6, 0.7),
            LapSegment.Straight(1.5),
            new LapSegment("T3 · chicane R", 60, 0.4, 0.5, 0.4, 0.6),
            new LapSegment("T4 · chicane L", -65, 0.4, 0.5, 0.4, 0.6),
            LapSegment.Straight(2.5),
            new LapSegment("T5 · hairpin right", 160, 1.1, 2.2, 1.0, 0.6),
            LapSegment.Straight(3),
            new LapSegment("T6 · 90° left", -100, 0.7, 1.5, 0.6, 0.75),
            LapSegment.Straight(1.5),
            new LapSegment("T7 · 90° right", 90, 0.6, 1.2, 0.6, 0.75),
            LapSegment.Straight(2),
            new LapSegment("T8 · tunnel left", -55, 0.8, 2.5, 0.8, 0.8),
            LapSegment.Straight(2),
            new LapSegment("T9 · 90° right", 95, 0.6, 1.3, 0.6, 0.7),
            LapSegment.Straight(1.5),
            new LapSegment("T10 · final left", -85, 0.7, 1.6, 0.7, 0.8),
            LapSegment.Straight(3),
        });

    public static readonly IReadOnlyList<LapProfile> All = new[] { HighSpeed, Street };
    public static LapProfile ByIndex(int i) => All[Math.Clamp(i, 0, All.Count - 1)];
}

/// <summary>
/// Stint: a looping lap of corner-shaped loads for 10-40 minutes. The ghost is the racing line: the
/// runway shows the corners coming. Corner torque pulls you back towards centre like self-aligning torque.
/// </summary>
public sealed class StintLogic : SetLogic
{
    private LapProfile _lap = LapProfiles.HighSpeed;
    private int _laps;
    private int _lastSeg = -1;
    private readonly List<double> _lapAccuracy = new();
    private double _lapIn, _lapT;
    private double _zone;

    public override void Begin(SetContext ctx)
    {
        base.Begin(ctx);
        _lap = LapProfiles.ByIndex((int)ctx.P("lap", 0));
        _zone = ctx.P("zone", 6);
    }

    public override bool IsDone(double elapsed) => elapsed >= C.P("minutes", 10) * 60 + 1.5;

    public override SetFrame Step(in ActivityTick t, double elapsed, bool scoring)
    {
        double load = C.LoadNm(t.Intensity);
        double lt = Math.Max(0, elapsed - 1.5);
        var (angle, frac, seg, _) = _lap.At(lt);
        int lapNo = (int)(lt / _lap.LapS);
        if (lapNo != _laps && scoring)
        {
            double acc = _lapT > 0 ? _lapIn / _lapT * 100 : 0;
            _lapAccuracy.Add(acc);
            Emit("lap", new { lap = lapNo, accuracy = Math.Round(acc, 1) });
            _lapIn = _lapT = 0;
            _laps = lapNo;
        }
        if (seg != _lastSeg) { _lastSeg = seg; if (!_lap.Segments[seg].IsStraight) Emit("corner", new { name = _lap.Segments[seg].Name }); }
        if (scoring)
        {
            Score.Add(t.Time, t.Dt, t.AngleDeg, angle, _zone, -t.MotorTorqueNm, t.VelocityDps);
            _lapT += t.Dt;
            if (Math.Abs(t.AngleDeg - angle) <= _zone) _lapIn += t.Dt;
        }
        double open = -Math.Sign(angle) * load * frac;
        // Corner exits let self-aligning torque unwind the wheel through your hands; allow brisker motion.
        return new SetFrame(open, 0, angle, RunawayOverrideDps: 280);
    }

    public override GuideFrame Guide(double elapsed, in ActivityTick t)
    {
        double lt = Math.Max(0, elapsed - 1.5);
        var (angle, frac, seg, prog) = _lap.At(lt);
        var s = _lap.Segments[seg];
        // Look further ahead on a lap: 6 s of track in 40 samples.
        var path = new double[40];
        for (int i = 0; i < path.Length; i++) path[i] = _lap.At(lt + i * 0.15).angle;
        string cue = s.IsStraight ? "STRAIGHT · RELAX GRIP" : Cues.Correction(t.AngleDeg - angle, _zone, "ON THE LINE");
        int next = (seg + 1) % _lap.Segments.Count;
        while (_lap.Segments[next].IsStraight && next != seg) next = (next + 1) % _lap.Segments.Count;
        return new GuideFrame
        {
            TargetDeg = angle,
            ZoneDeg = _zone,
            Path = path,
            PathStepS = 0.15,
            Cue = elapsed < 1.5 ? "LIGHTS OUT…" : cue,
            SubCue = s.IsStraight ? $"Next: {_lap.Segments[next].Name}" : $"{s.Name} · {frac * 100:0}% load",
            PullDir = angle == 0 ? 0 : -Math.Sign(angle),
            RepProgress = (lt % _lap.LapS) / _lap.LapS,
        };
    }

    public override Dictionary<string, double> Live()
    {
        var m = Score.LiveMetrics();
        m["lap"] = _laps + 1;
        m["lapS"] = Math.Round(_lap.LapS, 1);
        m["lastLapAccPct"] = _lapAccuracy.Count > 0 ? _lapAccuracy[^1] : 0;
        return m;
    }

    public override (double score, Dictionary<string, double> metrics) Finish()
    {
        var m = Score.FinalMetrics();
        m["laps"] = _laps;
        m["bestLapAccPct"] = _lapAccuracy.Count > 0 ? Math.Round(_lapAccuracy.Max(), 1) : 0;
        m["lastLapAccPct"] = _lapAccuracy.Count > 0 ? Math.Round(_lapAccuracy[^1], 1) : 0;
        if (_lapAccuracy.Count >= 2)
            m["fadePct"] = Math.Round(_lapAccuracy[0] - _lapAccuracy[^1], 1);
        return (Score.AccuracyScore(10), m);
    }

    public override bool QualifiesForProgression(Dictionary<string, double> metrics) =>
        metrics.TryGetValue("meanErrDeg", out var e) && e < 5;
}

/// <summary>
/// Free ride: pick a resistance profile and just turn. No targets, live metrics only.
/// 0 constant, 1 spring ("bungee"), 2 damper ("water"), 3 friction ("sand"), 4 wave, 5 kerb, 6 everything.
/// </summary>
public sealed class FreeRideLogic : SetLogic
{
    public static readonly string[] ProfileNames = { "Constant", "Bungee (spring)", "Water (damper)", "Sand (friction)", "Wave", "Kerbs", "Everything" };

    public override bool IsDone(double elapsed) => elapsed >= C.P("minutes", 5) * 60 + 1.5;

    public override SetFrame Step(in ActivityTick t, double elapsed, bool scoring)
    {
        double load = C.LoadNm(t.Intensity);
        int prof = (int)C.P("profile", 2);
        double open = 0, reactive = 0, vib = 0;
        double wave = 0.6 + 0.4 * Math.Sin(2 * Math.PI * elapsed / 6 - Math.PI / 2);
        switch (prof)
        {
            case 0: open = -C.Dir * load; break;
            case 1: reactive = Spring(t.AngleDeg, 0, load / 90.0, load); break;
            case 2: reactive = Damper(t.VelocityDps, load / 300.0, load); break;
            case 3: reactive = Friction(t.VelocityDps, load); break;
            case 4: open = -C.Dir * load * wave; break;
            case 5:
                open = -C.Dir * load * 0.7;
                vib = ((elapsed % 1.1) < 0.35 ? 1 : 0) * 0.8 * Math.Sin(2 * Math.PI * 24 * t.Time) * t.Intensity;
                break;
            default:
                reactive = Spring(t.AngleDeg, 0, load / 180.0, load * 0.5) + Damper(t.VelocityDps, load / 600.0, load * 0.5) + Friction(t.VelocityDps, load * 0.15);
                break;
        }
        if (scoring) Score.Add(t.Time, t.Dt, t.AngleDeg, t.AngleDeg, 1, -t.MotorTorqueNm, t.VelocityDps, trackAccuracy: false);
        // No target in free ride; the (simulated) driver plays with a slow sweep.
        double play = 60 * Math.Sin(2 * Math.PI * elapsed / 8.0);
        return new SetFrame(open, reactive, play, vib, RunawayOverrideDps: prof is 1 or 6 ? 300 : null);
    }

    public override GuideFrame Guide(double elapsed, in ActivityTick t)
    {
        int prof = (int)C.P("profile", 2);
        return new GuideFrame
        {
            TargetDeg = t.AngleDeg,
            ZoneDeg = 0,
            Path = Flat(t.AngleDeg),
            ShowGhost = false,
            Cue = "FREE RIDE",
            SubCue = ProfileNames[Math.Clamp(prof, 0, ProfileNames.Length - 1)],
            PullDir = 0,
        };
    }

    public override (double score, Dictionary<string, double> metrics) Finish()
    {
        var m = Score.FinalMetrics();
        return (Math.Round(Score.Impulse, 1), m);
    }

    public override bool QualifiesForProgression(Dictionary<string, double> metrics) => true;
}
