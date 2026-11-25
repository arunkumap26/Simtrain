using RimForce.Core.Drills;
using RimForce.Core.Programs;
using RimForce.Core.Settings;
using RimForce.Core.Storage;

namespace RimForce.Tests;

public class ProgramTests
{
    private static List<SetResult> Results(ProgramSession s, bool complete = true, double err = 1.5) =>
        s.Items.SelectMany(cfg => Enumerable.Range(0, cfg.Sets).Select(i => new SetResult
        {
            DrillId = cfg.DrillId,
            DrillName = cfg.DrillId,
            SetIndex = i,
            Role = cfg.Role,
            Completed = complete,
            Metrics = new() { ["meanErrDeg"] = err, ["qualifies"] = complete && err < 3 ? 1 : 0 },
        })).ToList();

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void EightWeeksWithDeloadsAndRetests(int perWeek)
    {
        var p = ProgramGenerator.Generate(perWeek);
        Assert.Equal(8 * perWeek, p.Sessions.Count);
        Assert.All(p.Sessions.Where(s => s.Week is 4 or 8), s => Assert.True(s.Deload));
        Assert.All(p.Sessions.Where(s => s.Week is not (4 or 8)), s => Assert.False(s.Deload));
        var retests = p.Sessions.Where(s => s.Retest).ToList();
        Assert.Equal(new[] { 4, 8 }, retests.Select(s => s.Week));
        Assert.All(retests, s => Assert.Contains(s.Items, i => i.DrillId == "breakpoint"));
        Assert.True(p.Sessions[0].Baseline);
    }

    [Fact]
    public void EverySessionHasWarmupDrillsAndCooldown()
    {
        var p = ProgramGenerator.Generate(3, needsBaseline: false);
        foreach (var s in p.Sessions)
        {
            Assert.Equal("Warm-up", s.Items[0].Role);
            Assert.Equal(25, s.Items[0].LoadPct);
            Assert.Equal("Cool-down", s.Items[^1].Role);
            int working = s.Items.Count(i => i.Role == "Working");
            Assert.InRange(working, 3, 5);
        }
    }

    [Fact]
    public void WorkingSetsStartAt50Percent()
    {
        var p = ProgramGenerator.Generate(3, needsBaseline: false);
        Assert.All(p.Sessions[0].Items.Where(i => i.Role == "Working"), i => Assert.Equal(50, i.LoadPct));
    }

    [Fact]
    public void QualifyingSessionAdds5Percent()
    {
        var p = ProgramGenerator.Generate(3, needsBaseline: false);
        var s = p.Next!;
        var p2 = ProgramGenerator.ApplySessionResults(p, s.Key, Results(s));
        var drill = s.Items.First(i => i.Role == "Working").DrillId;
        Assert.Equal(55, p2.WorkingLoadPct[drill]);
        Assert.Contains(s.Key, p2.CompletedKeys);
        Assert.NotEqual(s.Key, p2.Next!.Key);
    }

    [Fact]
    public void PoorAccuracyKeepsTheLoad()
    {
        var p = ProgramGenerator.Generate(3, needsBaseline: false);
        var s = p.Next!;
        var p2 = ProgramGenerator.ApplySessionResults(p, s.Key, Results(s, err: 4));
        var drill = s.Items.First(i => i.Role == "Working").DrillId;
        Assert.Equal(50, p2.WorkingLoadPct[drill]);
    }

    [Fact]
    public void IncompleteSetsKeepTheLoad()
    {
        var p = ProgramGenerator.Generate(3, needsBaseline: false);
        var s = p.Next!;
        var p2 = ProgramGenerator.ApplySessionResults(p, s.Key, Results(s, complete: false));
        Assert.All(s.Items.Where(i => i.Role == "Working"), i => Assert.Equal(50, p2.WorkingLoadPct[i.DrillId]));
    }

    [Fact]
    public void DeloadWeekIs60PercentOfNormalAndDoesNotProgress()
    {
        var p = ProgramGenerator.Generate(3, needsBaseline: false);
        var deload = p.Sessions.First(s => s.Deload);
        var item = deload.Items.First(i => i.Role == "Working");
        Assert.Equal(30, item.LoadPct); // 60% of 50
        var p2 = ProgramGenerator.ApplySessionResults(p, deload.Key, Results(deload));
        Assert.Equal(50, p2.WorkingLoadPct[item.DrillId]);
    }

    [Fact]
    public void AtTheTorqueCapProgressionSwitchesToTime()
    {
        var p = ProgramGenerator.Generate(3, needsBaseline: false);
        var s = p.Next!;
        var p2 = ProgramGenerator.ApplySessionResults(p, s.Key, Results(s), capNm: 4.0, resolveLoadNm: (_, pct) => pct / 100 * 12);
        var drill = s.Items.First(i => i.Role == "Working").DrillId;
        Assert.Equal(50, p2.WorkingLoadPct[drill]);
        Assert.True(p2.WorkMultiplier[drill] > 1.0);
        // future sessions of that drill get longer sets
        var future = p2.Sessions.First(x => !p2.CompletedKeys.Contains(x.Key) && x.Items.Any(i => i.DrillId == drill && i.Role == "Working") && !x.Deload);
        var orig = p.Sessions.First(x => x.Key == future.Key).Items.First(i => i.DrillId == drill && i.Role == "Working");
        Assert.True(future.Items.First(i => i.DrillId == drill && i.Role == "Working").WorkS > orig.WorkS);
    }

    [Fact]
    public void PlanConversion()
    {
        var p = ProgramGenerator.Generate(3);
        var plan = ProgramGenerator.ToPlan(p.Next!);
        Assert.Equal("w1s1", plan.ProgramRef);
        Assert.NotEmpty(plan.Items);
    }
}

public class StorageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "rimforce-tests-" + Guid.NewGuid().ToString("N"));
    private string Db => Path.Combine(_dir, "t.db");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Fact]
    public void SettingsRoundTrip()
    {
        using var repo = new Repository(Db);
        Assert.False(repo.LoadSettings().ChecklistAccepted);
        var s = new AppSettings { ChecklistAccepted = true, ForceUnit = "N", Wheel = new() { RimDiameterMm = 280, SimProStrengthPct = 80 } };
        repo.SaveSettings(s);
        var back = repo.LoadSettings();
        Assert.True(back.ChecklistAccepted);
        Assert.Equal("N", back.ForceUnit);
        Assert.Equal(280, back.Wheel.RimDiameterMm);
        Assert.Equal(80, back.Wheel.SimProStrengthPct);
    }

    [Fact]
    public void SessionsSetsAndTraces()
    {
        using var repo = new Repository(Db);
        long sid = repo.StartSession("Test", "w1s1");
        var trace = new float[] { 0, 1, 2, 3, 0.05f, 1.1f, 2, 3.1f };
        long setId = repo.SaveSet(sid, new SetResult
        {
            DrillId = "corner-hold", DrillName = "Corner Hold", SetIndex = 0, Hand = Hand.Left, Direction = PushDir.Right,
            LoadNm = 4.2, LoadPct = 50, WorkS = 30, Completed = true, Score = 87.5,
            Metrics = new() { ["impulseNms"] = 120, ["workConcJ"] = 3, ["workEccJ"] = 1, ["tutS"] = 28 },
            Trace = trace, StartedUtc = DateTime.UtcNow,
        });
        repo.EndSession(sid, new { ok = true });
        var sets = repo.SetsForSession(sid);
        var set = Assert.Single(sets);
        Assert.Equal(Hand.Left, set.Hand);
        Assert.Equal(87.5, set.Score);
        Assert.Equal(trace, repo.GetTrace(setId));
        var sess = Assert.Single(repo.RecentSessions());
        Assert.Equal(1, sess.Completed);
        Assert.Equal(120, sess.ImpulseNms);
        var vol = Assert.Single(repo.WeeklyVolume());
        Assert.Equal(120, vol.ImpulseNms);
    }

    [Fact]
    public void BreakpointsLatestWins()
    {
        using var repo = new Repository(Db);
        repo.AddBreakpoint(Hand.Both, 1, 6.0, false, DateTime.UtcNow.AddDays(-7));
        repo.AddBreakpoint(Hand.Both, 1, 7.5, false, DateTime.UtcNow);
        repo.AddBreakpoint(Hand.Right, -1, 3.1, true);
        var refs = repo.LoadReferenceStrength();
        Assert.Equal(7.5, refs.Get(Hand.Both, 1));
        Assert.Equal(3.1, refs.Get(Hand.Right, -1));
        Assert.Equal(3, repo.Breakpoints().Count);
    }

    [Fact]
    public void ProgramRoundTrip()
    {
        using var repo = new Repository(Db);
        Assert.Null(repo.LoadProgram());
        var p = ProgramGenerator.Generate(2);
        repo.SaveProgram(p);
        var back = repo.LoadProgram()!;
        Assert.Equal(p.Sessions.Count, back.Sessions.Count);
        Assert.Equal(p.Sessions[3].Items.Count, back.Sessions[3].Items.Count);
        Assert.Equal(p.WorkingLoadPct["corner-hold"], back.WorkingLoadPct["corner-hold"]);
    }

    [Fact]
    public void CsvExportHasHeaderAndRows()
    {
        using var repo = new Repository(Db);
        long sid = repo.StartSession("Test", null);
        repo.SaveSet(sid, new SetResult { DrillId = "stint", DrillName = "Race Stint, long", Completed = true, Metrics = new() { ["laps"] = 3 } });
        repo.AddBreakpoint(Hand.Both, -1, 5, false);
        var folder = repo.ExportCsv(Path.Combine(_dir, "csv"));
        var sets = File.ReadAllLines(Path.Combine(folder, "sets.csv"));
        Assert.Equal(2, sets.Length);
        Assert.Contains("laps", sets[0]);
        Assert.Contains("\"Race Stint, long\"", sets[1]);
        var bps = File.ReadAllLines(Path.Combine(folder, "breakpoints.csv"));
        Assert.Contains("left", bps[1]);
    }
}
