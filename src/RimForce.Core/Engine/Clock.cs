using System.Diagnostics;

namespace RimForce.Core.Engine;

/// <summary>Monotonic time source in seconds. Injected everywhere so tests are deterministic.</summary>
public interface IClock
{
    double Now { get; }
}

public sealed class SystemClock : IClock
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    public double Now => _sw.Elapsed.TotalSeconds;
}

public sealed class ManualClock : IClock
{
    public double Now { get; private set; }
    public ManualClock(double start = 0) => Now = start;
    public void Advance(double seconds) => Now += seconds;
    public void Set(double t) => Now = t;
}
