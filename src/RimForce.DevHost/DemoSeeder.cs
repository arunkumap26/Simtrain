using RimForce.Core.Devices;
using RimForce.Core.Drills;
using RimForce.Core.Engine;
using RimForce.Core.Hardware;
using RimForce.Core.Programs;
using RimForce.Core.Safety;
using RimForce.Core.Sessions;
using RimForce.Core.Settings;
using RimForce.Core.Storage;

namespace RimForce.DevHost;

/// <summary>
/// Dev/demo only: runs the first weeks of a real program through the real engine against the simulated driver
/// (who gets a little stronger every session) and stores the results with back-dated timestamps, so the
/// Progress and Program pages have something to show. Work/rest times are shortened to keep it fast.
/// </summary>
public static class DemoSeeder
{
    public static void Seed(string dbPath, int sessions = 12)
    {
        SeedProgram(dbPath, sessions);
        SeedMaxTest(dbPath);
    }

    /// <summary>A Max Strength Test run through the real test logic, one simulated arm strength per test.</summary>
    public static void SeedMaxTest(string dbPath)
    {
        using var repo = new Repository(dbPath);
        if (repo.RecentSessions(100).Any(x => x.ProgramRef == "maxtest")) return;
        var profile = new WheelProfile();
        var arms = new (string id, double strength)[]
        {
            ("profile-right-lift", 10.5), ("profile-left-lift", 9.6), ("profile-right-press", 13.2), ("profile-left-press", 12.1),
        };
        var when = DateTime.UtcNow.AddHours(-2);
        long sid = repo.StartSession("Max Strength Test", "maxtest", when);
        double offset = 0;
        foreach (var (id, strength) in arms)
        {
            var (drill, hand, dir, _) = RimForce.Core.App.AppController.MaxTestItems[id];
            var clock = new ManualClock();
            var sim = new SimulatedForceDevice(clock, profile);
            sim.Connect();
            sim.Driver.StrengthNm = strength;
            sim.Driver.MaxRefSpeedDps = 4000;
            sim.Driver.FatigueRate = 0;
            var engine = new TrainingEngine(sim, clock, profile, new SafetySettings { GlobalCapPct = 100 });
            engine.Governor.ChecklistAccepted = true;
            engine.Governor.RequireDirectionCheck = false;
            var cfg = DrillCatalog.DefaultConfig(drill) with { Hand = hand, Direction = dir, Role = "Test" };
            var runner = new SessionRunner(SessionPlan.Single(cfg), profile, new ReferenceStrength(), 11);
            engine.SetActivity(runner);
            void Step(double sec) { int n = (int)(sec * 360); for (int i = 0; i < n; i++) { clock.Advance(1.0 / 360); engine.Heartbeat(); engine.Tick(); } }
            Step(1);
            engine.Arm(out _);
            runner.Start();
            double g = 0;
            while (!runner.IsFinished && runner.Phase != SessionPhase.Paused && g < 400) { Step(0.25); g += 0.25; }
            foreach (var r in runner.Results)
            {
                repo.SaveSet(sid, r with { StartedUtc = when.AddSeconds(offset), EndedUtc = when.AddSeconds(offset + r.WorkS) });
                offset += r.WorkS + 90;
                Console.WriteLine($"Seed max: {id} true {strength} Nm -> est {r.Score} Nm (confidence {r.Metrics.GetValueOrDefault("confidence")})");
            }
        }
        repo.EndSession(sid, new { seeded = true }, when.AddSeconds(offset));
    }

    private static void SeedProgram(string dbPath, int sessions)
    {
        using var repo = new Repository(dbPath);
        if (repo.RecentSessions(1).Count > 0) { Console.WriteLine("Seed: database already has sessions, skipping program."); return; }

        var profile = new WheelProfile();
        var program = ProgramGenerator.Generate(3, needsBaseline: true);
        var refs = new ReferenceStrength();
        var start = DateTime.UtcNow.Date.AddDays(-sessions * 7.0 / 3 - 1).AddHours(18);
        double strength = 4.0;

        for (int k = 0; k < sessions && program.Next != null; k++)
        {
            var ps = program.Next;
            var when = start.AddDays(k * 7.0 / 3);
            strength += 0.11;
            var plan = ProgramGenerator.ToPlan(ps);
            plan = plan with
            {
                Items = plan.Items.Select(i => new SessionItem(i.Config with
                {
                    WorkS = i.Config.DrillId == "breakpoint" ? 120 : Math.Min(i.Config.WorkS, 25),
                    RestS = 2,
                    Params = i.Config.DrillId is "stint" or "free-ride"
                        ? new Dictionary<string, double>(i.Config.Params) { ["minutes"] = 1.0 }
                        : i.Config.Params,
                })).ToList(),
            };

            var clock = new ManualClock();
            var sim = new SimulatedForceDevice(clock, profile);
            sim.Connect();
            sim.Driver.StrengthNm = strength;
            sim.Driver.FatigueRate = 0.02;
            var engine = new TrainingEngine(sim, clock, profile, new SafetySettings { GlobalCapPct = 60 });
            engine.Governor.ChecklistAccepted = true;
            engine.Governor.RequireDirectionCheck = false;
            var runner = new SessionRunner(plan, profile, refs, seed: 100 + k);
            engine.SetActivity(runner);

            void Step(double s) { int n = (int)(s * 360); for (int i = 0; i < n; i++) { clock.Advance(1.0 / 360); engine.Heartbeat(); engine.Tick(); } }
            Step(3);
            if (!engine.Arm(out var why)) { Console.WriteLine("Seed: arm failed " + why); return; }
            runner.Start();
            double guard = 0;
            while (!runner.IsFinished && guard < 3600)
            {
                Step(0.25); guard += 0.25;
                if (runner.Phase == SessionPhase.Paused)
                {
                    engine.Governor.AcknowledgeTrip(clock.Now);
                    Step(2);
                    engine.Arm(out _);
                    runner.Start();
                }
                if (engine.Governor.State != SafetyState.Armed && runner.WantsArmed) engine.Arm(out _);
            }

            long sid = repo.StartSession(plan.Title, ps.Key, when);
            double offset = 0;
            foreach (var r in runner.Results)
            {
                var stamped = r with { StartedUtc = when.AddSeconds(offset), EndedUtc = when.AddSeconds(offset + r.WorkS) };
                offset += r.WorkS + 40;
                repo.SaveSet(sid, stamped);
                if (r.DrillId == "breakpoint" && r.Completed && r.Metrics.GetValueOrDefault("valid") > 0)
                    repo.AddBreakpoint(r.Hand, r.Direction == PushDir.Right ? 1 : -1, r.Score, r.Metrics.GetValueOrDefault("estimated") > 0, when.AddSeconds(offset));
            }
            repo.EndSession(sid, new { seeded = true }, when.AddSeconds(offset));
            program = ProgramGenerator.ApplySessionResults(program, ps.Key, runner.Results, engine.Governor.GlobalCapNm,
                (_, pct) => pct / 100.0 * (refs.Resolve(Hand.Both, 1) ?? profile.MaxTorqueNm));
            Console.WriteLine($"Seed: {ps.Key} {plan.Title} — {runner.Results.Count} sets, driver {strength:0.00} Nm");
        }

        // A couple of one-hand breakpoint tests so the strength chart has per-hand lines.
        var oneHand = new (Hand hand, int dir, double nm, int day)[]
        {
            (Hand.Right, 1, 2.6, 2), (Hand.Left, 1, 2.3, 2), (Hand.Right, 1, 2.9, sessions * 7 / 3 - 1), (Hand.Left, 1, 2.65, sessions * 7 / 3 - 1),
        };
        foreach (var o in oneHand) repo.AddBreakpoint(o.hand, o.dir, o.nm, false, start.AddDays(o.day).AddHours(1));

        repo.SaveProgram(program);
        var s = repo.LoadSettings();
        repo.SaveSettings(s with { ChecklistAccepted = true, OnboardingDone = true });
        Console.WriteLine("Seed: done.");
    }
}
