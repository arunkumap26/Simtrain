using System.Diagnostics;
using RimForce.Core.Safety;

namespace RimForce.Core.Engine;

/// <summary>
/// Runs <see cref="TrainingEngine.Tick"/> at a fixed rate on a dedicated high-priority thread, plus an
/// independent watchdog thread that calls StopAll if the force loop stalls for longer than WatchdogS.
/// </summary>
public sealed class EngineHost : IDisposable
{
    private readonly TrainingEngine _engine;
    private Thread? _loop;
    private Thread? _watchdog;
    private volatile bool _running;

    public EngineHost(TrainingEngine engine, double targetHz = 500)
    {
        _engine = engine;
        TargetHz = Math.Clamp(targetHz, 100, 1000);
    }

    public double TargetHz { get; set; }
    public Exception? LastLoopError { get; private set; }
    public int WatchdogTrips { get; private set; }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _loop = new Thread(LoopMain) { IsBackground = true, Name = "RimForce force loop", Priority = ThreadPriority.Highest };
        _watchdog = new Thread(WatchdogMain) { IsBackground = true, Name = "RimForce watchdog", Priority = ThreadPriority.AboveNormal };
        _loop.Start();
        _watchdog.Start();
    }

    private void LoopMain()
    {
        var sw = Stopwatch.StartNew();
        double period = 1.0 / TargetHz;
        double next = sw.Elapsed.TotalSeconds;
        while (_running)
        {
            try { _engine.Tick(); }
            catch (Exception ex)
            {
                LastLoopError = ex;
                _engine.Trip(TripCode.InternalError, "Force loop error: " + ex.Message);
            }

            period = 1.0 / TargetHz;
            next += period;
            double now = sw.Elapsed.TotalSeconds;
            if (next < now - 0.05) next = now; // fell far behind; resync instead of bursting
            // Hybrid wait: sleep while far away, spin for the last ~1.2 ms for accuracy.
            while (_running)
            {
                double remaining = next - sw.Elapsed.TotalSeconds;
                if (remaining <= 0) break;
                if (remaining > 0.0015) Thread.Sleep(1);
                else if (remaining > 0.0002) Thread.Yield();
                else Thread.SpinWait(20);
            }
        }
    }

    private void WatchdogMain()
    {
        while (_running)
        {
            Thread.Sleep(10);
            try
            {
                if (_engine.Governor.CheckWatchdog(_engine.Clock.Now))
                {
                    WatchdogTrips++;
                    _engine.Device.StopAll();
                }
            }
            catch { /* the watchdog must never die */ }
        }
    }

    public void Dispose()
    {
        _running = false;
        try { _engine.Device.StopAll(); } catch { }
        _loop?.Join(500);
        _watchdog?.Join(500);
    }
}
