using System.Collections.Concurrent;
using RimForce.Core.Drills;

namespace RimForce.Core.Sessions;

/// <summary>Latest breakpoint torque per hand and push direction. Thread-safe.</summary>
public sealed class ReferenceStrength
{
    private readonly ConcurrentDictionary<(Hand hand, int dir), double> _bp = new();

    public static int DirKey(int dir) => dir >= 0 ? 1 : -1;

    public void Record(Hand hand, int dir, double nm)
    {
        if (nm > 0.05) _bp[(hand, DirKey(dir))] = nm;
    }

    public double? Get(Hand hand, int dir) => _bp.TryGetValue((hand, DirKey(dir)), out var v) ? v : null;

    /// <summary>
    /// Best available reference for a set: exact match, else derived from what we know
    /// (one hand ~55% of two hands; the other direction as a stand-in), else null.
    /// </summary>
    public double? Resolve(Hand hand, int dir)
    {
        if (Get(hand, dir) is { } exact) return exact;
        if (Get(hand, -dir) is { } other) return other;
        if (hand != Hand.Both)
        {
            var both = Get(Hand.Both, dir) ?? Get(Hand.Both, -dir);
            if (both is { } b) return b * 0.55;
        }
        else
        {
            var l = Get(Hand.Left, dir) ?? Get(Hand.Left, -dir);
            var r = Get(Hand.Right, dir) ?? Get(Hand.Right, -dir);
            if (l is { } lv && r is { } rv) return (lv + rv) * 0.9;
            if ((l ?? r) is { } one) return one / 0.55;
        }
        return null;
    }

    public IReadOnlyDictionary<string, double> ToDictionary() =>
        _bp.ToDictionary(kv => $"{kv.Key.hand}:{(kv.Key.dir > 0 ? "right" : "left")}", kv => kv.Value);

    public bool Any => !_bp.IsEmpty;
}
