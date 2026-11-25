using System.Text.Json;
using System.Text.Json.Nodes;
using RimForce.Core.Devices;
using RimForce.Core.Drills;
using RimForce.Core.Drills.Logics;
using RimForce.Core.Engine;
using RimForce.Core.Metrics;
using RimForce.Core.Programs;
using RimForce.Core.Safety;
using RimForce.Core.Sessions;
using RimForce.Core.Settings;
using RimForce.Core.Storage;

namespace RimForce.Core.App;

/// <summary>Source of wheels: the simulator in Core, DirectInput on Windows.</summary>
public interface IDeviceProvider
{
    IReadOnlyList<DeviceInfo> List();
    IForceDevice? Create(string id);
}

public sealed class SimulatedDeviceProvider : IDeviceProvider
{
    private readonly IClock _clock;
    public SimulatedDeviceProvider(IClock clock) => _clock = clock;
    public IReadOnlyList<DeviceInfo> List() => new SimulatedForceDevice(_clock).Enumerate();
    public IForceDevice? Create(string id) => id == "sim" ? new SimulatedForceDevice(_clock) : null;
}

public sealed class AppOptions
{
    public required string DbPath { get; init; }
    public bool Demo { get; init; }
    public IClock Clock { get; init; } = new SystemClock();
    public List<IDeviceProvider> Providers { get; init; } = new();
    public string Version { get; init; } = "0.1.0";
    public string Host { get; init; } = "unknown";
    /// <summary>Folder for CSV exports.</summary>
    public string? ExportRoot { get; init; }
}

/// <summary>
/// The application brain shared by the Windows app and the dev host: owns the engine, storage, program,
/// sessions and devices, and speaks a small JSON protocol with the UI (commands in, ticks/events/state out).
/// </summary>
public sealed class AppController : IDisposable
{
    public static JsonSerializerOptions Json => Repository.Json;

    private readonly AppOptions _opt;
    private readonly Repository _repo;
    private readonly IClock _clock;
    private readonly object _lock = new();
    private AppSettings _settings;
    private ReferenceStrength _refs;
    private TrainingProgram? _program;
    private SessionRunner? _runner;
    private long? _sessionId;
    private EngineHost? _host;
    private Thread? _pump;
    private volatile bool _running;
    private ButtonAction? _bindPending;
    private string? _deviceId;

    public TrainingEngine Engine { get; }
    public AppSettings Settings => _settings;
    public Repository Repository => _repo;
    public SessionRunner? Runner => _runner;
    public bool Demo => _opt.Demo;

    /// <summary>Outgoing JSON messages (ticks, events, state) for every connected UI.</summary>
    public event Action<string>? Outgoing;
    /// <summary>The host should reveal this path in the file explorer (CSV export).</summary>
    public event Action<string>? OpenPathRequested;

    public AppController(AppOptions options)
    {
        _opt = options;
        _clock = options.Clock;
        _repo = new Repository(options.DbPath);
        _settings = _repo.LoadSettings();
        _refs = _repo.LoadReferenceStrength();
        _program = _repo.LoadProgram();

        IForceDevice device = PickInitialDevice(out _deviceId);
        Engine = new TrainingEngine(device, _clock, _settings.Wheel, _settings.Safety);
        ApplySettingsToEngine();
        Engine.SetIntensity(_settings.Intensity);
    }

    private IEnumerable<IDeviceProvider> Providers =>
        _opt.Demo ? new IDeviceProvider[] { new SimulatedDeviceProvider(_clock) } : _opt.Providers.Append(new SimulatedDeviceProvider(_clock));

    public IReadOnlyList<DeviceInfo> ListDevices()
    {
        var all = new List<DeviceInfo>();
        foreach (var p in Providers)
        {
            try { all.AddRange(p.List()); } catch { }
        }
        return all;
    }

    private IForceDevice PickInitialDevice(out string id)
    {
        if (!_opt.Demo)
        {
            var devices = ListDevices().Where(d => !d.IsSimulated).ToList();
            var choice = devices.FirstOrDefault(d => d.Id == _settings.DeviceId)
                ?? devices.FirstOrDefault(d => d.Name.Contains("Simagic", StringComparison.OrdinalIgnoreCase))
                ?? devices.FirstOrDefault();
            if (choice != null && CreateDevice(choice.Id) is { } real && real.Connect(choice.Id))
            {
                id = choice.Id;
                return real;
            }
        }
        var sim = new SimulatedForceDevice(_clock, _settings.Wheel);
        sim.Connect();
        id = "sim";
        return sim;
    }

    private IForceDevice? CreateDevice(string id)
    {
        foreach (var p in Providers)
        {
            try { if (p.Create(id) is { } d) return d; } catch { }
        }
        return null;
    }

    private bool IsSim => Engine.Device is SimulatedForceDevice;

    private void ApplySettingsToEngine()
    {
        var s = _settings;
        Engine.Profile = s.Wheel;
        Engine.Governor.Settings = s.Safety;
        // The simulator cannot hurt anyone: no checklist / direction check needed to play the demo.
        Engine.Governor.ChecklistAccepted = s.ChecklistAccepted || IsSim;
        Engine.Governor.RequireDirectionCheck = !IsSim;
        Engine.Governor.DirectionVerified = s.DirectionVerified;
        Engine.Norms = StrengthNorms.For(s.Norms);
        Engine.BodyMassKg = s.BodyMassKg;
        Engine.WristTechnique = s.Technique == "wrists";
        Engine.ButtonMap.Clear();
        foreach (var kv in s.Buttons) Engine.ButtonMap[kv.Key] = kv.Value;
        Engine.ReferenceMaxNm = _refs.Resolve(Hand.Both, 1);
        if (_host != null) _host.TargetHz = s.LoopHz;
    }

    private void SaveSettings(AppSettings s)
    {
        _settings = s.Sanitised();
        _repo.SaveSettings(_settings);
        ApplySettingsToEngine();
    }

    // ---------------- lifecycle ----------------

    public void Start()
    {
        if (_running) return;
        _running = true;
        _host = new EngineHost(Engine, _settings.LoopHz);
        _host.Start();
        _pump = new Thread(PumpMain) { IsBackground = true, Name = "RimForce UI pump" };
        _pump.Start();
    }

    private void PumpMain()
    {
        while (_running)
        {
            try { Pump(); } catch { }
            Thread.Sleep(16);
        }
    }

    /// <summary>Drain engine events (persisting results) and push the latest tick. Called ~60 Hz.</summary>
    public void Pump()
    {
        while (Engine.TryDequeueEvent(out var e) && e != null) HandleEngineEvent(e);
        if (Engine.Latest is { } snap) Send(new { type = "tick", data = snap });
    }

    private void Send(object msg)
    {
        var h = Outgoing;
        if (h == null) return;
        string json;
        try { json = JsonSerializer.Serialize(msg, Json); }
        catch (Exception ex) { json = JsonSerializer.Serialize(new { type = "error", message = ex.Message }, Json); }
        h(json);
    }

    private void SendEvent(string kind, object? data = null) => Send(new { type = "event", kind, data });

    public void BroadcastState() => Send(new { type = "state", data = State() });

    /// <summary>Run on the force-loop thread (thread-safe engine access) and wait for the result.</summary>
    private T OnLoop<T>(Func<T> f)
    {
        if (_host == null) return f();
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Engine.Post(() => { try { tcs.SetResult(f()); } catch (Exception ex) { tcs.SetException(ex); } });
        if (!tcs.Task.Wait(1000)) throw new TimeoutException("Force loop did not respond");
        return tcs.Task.Result;
    }

    private void OnLoop(Action a) => OnLoop(() => { a(); return true; });

    // ---------------- engine events → storage / program ----------------

    private void HandleEngineEvent(EngineEvent e)
    {
        switch (e.Kind)
        {
            case "setEnd" when e.Data is SetResult r:
                lock (_lock)
                {
                    _sessionId ??= _repo.StartSession(_runner?.Plan.Title ?? r.DrillName, _runner?.Plan.ProgramRef);
                    long setId = _repo.SaveSet(_sessionId.Value, r);
                    if (r.DrillId == "breakpoint" && r.Completed && r.Metrics.GetValueOrDefault("valid") > 0)
                    {
                        _repo.AddBreakpoint(r.Hand, r.Direction == PushDir.Right ? 1 : -1, r.Score, r.Metrics.GetValueOrDefault("estimated") > 0);
                        _refs.Record(r.Hand, r.Direction == PushDir.Right ? 1 : -1, r.Score);
                        Engine.ReferenceMaxNm = _refs.Resolve(Hand.Both, 1);
                    }
                    SendEvent("setEnd", new { setId, r.DrillId, r.DrillName, r.SetIndex, r.Role, r.Hand, r.Direction, r.Grip, r.LoadNm, r.Score, r.ScoreLabel, r.Completed, r.Metrics, r.WorkS, r.Note });
                }
                break;

            case "sessionComplete":
                lock (_lock)
                {
                    if (_sessionId is { } sid) _repo.EndSession(sid, e.Data);
                    if (_runner?.Plan.ProgramRef is { } key && _program != null)
                    {
                        double capNm = Engine.Governor.GlobalCapNm;
                        _program = ProgramGenerator.ApplySessionResults(_program, key, _runner.Results, capNm,
                            (id, pct) => pct / 100.0 * (_refs.Resolve(Hand.Both, 1) ?? Engine.Profile.MaxTorqueNm));
                        _repo.SaveProgram(_program);
                    }
                    _sessionId = null;
                }
                Engine.Post(() => Engine.Disarm());
                SendEvent("sessionComplete", e.Data);
                BroadcastState();
                break;

            case "directionCheck":
                HandleDirectionCheck(e.Data);
                break;

            case "button":
                if (_bindPending is { } action && e.Data is int idx)
                {
                    var map = new Dictionary<int, ButtonAction>(_settings.Buttons.Where(kv => kv.Value != action).ToDictionary(kv => kv.Key, kv => kv.Value)) { [idx] = action };
                    _bindPending = null;
                    SaveSettings(_settings with { Buttons = map });
                    SendEvent("buttonBound", new { button = idx, action = action.ToString() });
                    BroadcastState();
                }
                else SendEvent("button", e.Data);
                break;

            case "intensity":
                _settings = _settings with { Intensity = Engine.Intensity };
                try { _repo.SaveSettings(_settings); } catch { }
                SendEvent("intensity", e.Data);
                break;

            default:
                SendEvent(e.Kind, e.Data);
                break;
        }
    }

    private void HandleDirectionCheck(object? data)
    {
        var node = JsonSerializer.SerializeToNode(data, Json) as JsonObject;
        string result = node?["result"]?.GetValue<string>() ?? "aborted";
        if (result is "ok" or "inverted")
        {
            var wheel = _settings.Wheel;
            if (result == "inverted") wheel = wheel with { InvertForce = !wheel.InvertForce };
            double? j = node?["inertia"]?.GetValue<double?>();
            if (j is > 0.005 and < 0.3) wheel = wheel with { InertiaKgM2 = j.Value };
            SaveSettings(_settings with { Wheel = wheel, DirectionVerified = true });
        }
        Engine.Post(() => { Engine.Disarm(); Engine.SetActivity(null); });
        SendEvent("directionCheck", data);
        BroadcastState();
    }

    // ---------------- commands ----------------

    /// <summary>Handle one command from the UI: {"id": n, "cmd": "...", "args": {...}}. Returns the reply JSON.</summary>
    public string HandleMessage(string json)
    {
        JsonNode? msg;
        try { msg = JsonNode.Parse(json); }
        catch { return Reply(null, false, error: "Bad JSON"); }
        var id = msg?["id"]?.GetValue<int?>();
        string cmd = msg?["cmd"]?.GetValue<string>() ?? "";
        var args = msg?["args"] as JsonObject ?? new JsonObject();
        try
        {
            object? data = Execute(cmd, args);
            return Reply(id, true, data);
        }
        catch (CommandException ce) { return Reply(id, false, error: ce.Message); }
        catch (Exception ex) { return Reply(id, false, error: ex.Message); }
    }

    private static string Reply(int? id, bool ok, object? data = null, string? error = null) =>
        JsonSerializer.Serialize(new { type = "reply", id, ok, data, error }, Json);

    private sealed class CommandException : Exception { public CommandException(string m) : base(m) { } }

    private static T Arg<T>(JsonObject a, string key, T fallback)
    {
        try { return a[key] is { } n ? n.Deserialize<T>(Json) ?? fallback : fallback; }
        catch { return fallback; }
    }

    public object? Execute(string cmd, JsonObject args)
    {
        switch (cmd)
        {
            case "hello":
                return State();

            case "heartbeat":
                Engine.Heartbeat();
                return null;

            case "focus":
                Engine.SetFocus(Arg(args, "focused", true));
                return null;

            case "stop":
                Engine.Stop(Arg(args, "reason", "Stopped"));
                return null;

            case "ackTrip":
                OnLoop(() => Engine.Governor.AcknowledgeTrip(_clock.Now));
                return null;

            case "acceptChecklist":
                SaveSettings(_settings with { ChecklistAccepted = true });
                BroadcastState();
                return null;

            case "arm":
            {
                string? why = null;
                bool ok = OnLoop(() => Engine.Arm(out why));
                if (!ok) throw new CommandException(why ?? "Could not arm");
                return null;
            }

            case "disarm":
                OnLoop(() => Engine.Disarm());
                return null;

            case "startDrill":
            {
                var cfg = args["config"].Deserialize<DrillConfig>(Json) ?? throw new CommandException("Missing config");
                if (!DrillCatalog.Exists(cfg.DrillId)) throw new CommandException("Unknown drill");
                var def = DrillCatalog.Get(cfg.DrillId);
                cfg = cfg with
                {
                    Sets = Math.Clamp(cfg.Sets, 1, 20),
                    WorkS = Math.Clamp(cfg.WorkS, 5, 3600),
                    RestS = Math.Clamp(cfg.RestS, 0, 600),
                    LoadPct = Math.Clamp(cfg.LoadPct, 5, def.IsTest ? 100 : 120),
                };
                LoadPlan(SessionPlan.Single(cfg));
                return null;
            }

            case "startProgramSession":
            {
                if (_program == null) throw new CommandException("No program yet. Generate one first.");
                string? key = Arg<string?>(args, "key", null);
                var s = key == null ? _program.Next : _program.Sessions.FirstOrDefault(x => x.Key == key);
                if (s == null) throw new CommandException("Program complete. Generate a new one!");
                LoadPlan(ProgramGenerator.ToPlan(s));
                return null;
            }

            case "go":
            {
                // Arm if needed and start/resume the session (3-2-1 countdown).
                var runner = _runner ?? throw new CommandException("Pick a drill or session first.");
                string? why = null;
                bool ok = OnLoop(() =>
                {
                    if (Engine.Governor.State == SafetyState.Tripped) { why = "Acknowledge the safety stop first."; return false; }
                    if (Engine.Governor.State != SafetyState.Armed && !Engine.Arm(out why)) return false;
                    if (Engine.Governor.TestMode) { why = "Finish the device test first."; return false; }
                    runner.Start();
                    return true;
                });
                if (!ok) throw new CommandException(why ?? "Could not start");
                return null;
            }

            case "next":
                _runner?.Next();
                return null;

            case "endSession":
                EndSession();
                return null;

            case "setIntensity":
            {
                double v = Arg(args, "value", Engine.Intensity);
                OnLoop(() => Engine.SetIntensity(v));
                return new { intensity = Engine.Intensity };
            }

            case "nudgeIntensity":
            {
                double d = Arg(args, "delta", 0.05);
                OnLoop(() => Engine.SetIntensity(Engine.Intensity + d));
                return new { intensity = Engine.Intensity };
            }

            case "saveSettings":
            {
                var s = args["settings"].Deserialize<AppSettings>(Json) ?? throw new CommandException("Missing settings");
                // Never let the UI silently clear safety acknowledgements.
                s = s with { ChecklistAccepted = s.ChecklistAccepted || _settings.ChecklistAccepted };
                bool directionAffected = s.Wheel.InvertAngle != _settings.Wheel.InvertAngle;
                if (directionAffected) s = s with { DirectionVerified = false };
                OnLoop(() => { if (Engine.Governor.State == SafetyState.Armed && directionAffected) Engine.Stop("Settings changed"); });
                SaveSettings(s);
                BroadcastState();
                return null;
            }

            case "generateProgram":
            {
                int per = Arg(args, "sessionsPerWeek", _settings.SessionsPerWeek);
                _program = ProgramGenerator.Generate(per, needsBaseline: !_refs.Any);
                _repo.SaveProgram(_program);
                SaveSettings(_settings with { SessionsPerWeek = per });
                BroadcastState();
                return null;
            }

            case "history":
                return History();

            case "trace":
                return new { trace = _repo.GetTrace(Arg(args, "setId", 0L)) };

            case "exportCsv":
            {
                string root = _opt.ExportRoot ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "RimForce");
                string folder = _repo.ExportCsv(Path.Combine(root, "export-" + DateTime.Now.ToString("yyyyMMdd-HHmmss")));
                OpenPathRequested?.Invoke(folder);
                return new { folder };
            }

            case "selectDevice":
            {
                string devId = Arg(args, "id", "sim");
                SelectDevice(devId);
                BroadcastState();
                return null;
            }

            case "deviceTest":
            {
                string effect = Arg(args, "effect", "none");
                double pct = Arg(args, "pct", 10.0);
                if (_runner != null && _runner.WantsArmed) throw new CommandException("End the current session first.");
                string? why = null;
                bool ok = OnLoop(() =>
                {
                    if (effect == "none") { Engine.Disarm(); Engine.SetActivity(null); return true; }
                    if (!(Engine.Governor.State == SafetyState.Armed && Engine.Governor.TestMode) && !Engine.ArmTestMode(out why)) return false;
                    Engine.SetActivity(new DeviceTestActivity(effect, pct, Engine.Profile.MaxTorqueNm));
                    return true;
                });
                if (!ok) throw new CommandException(why ?? "Could not start the test");
                _runner = null;
                return null;
            }

            case "directionCheck":
            {
                if (_runner != null && _runner.WantsArmed) throw new CommandException("End the current session first.");
                string? why = null;
                bool ok = OnLoop(() =>
                {
                    if (!Engine.ArmTestMode(out why)) return false;
                    Engine.SetActivity(new DirectionCheckActivity(Engine.Profile.MaxTorqueNm));
                    return true;
                });
                if (!ok) throw new CommandException(why ?? "Could not start the check");
                _runner = null;
                return null;
            }

            case "setCentre":
            {
                double raw = Engine.RawAngleDeg * (_settings.Wheel.InvertAngle ? -1 : 1);
                SaveSettings(_settings with { Wheel = _settings.Wheel with { CenterOffsetDeg = Math.Round(raw, 2) } });
                BroadcastState();
                return new { offset = raw };
            }

            case "calibrateTorque":
            {
                // Luggage scale hooked on the rim, pulled tangentially, wheel held still at the test torque.
                double kg = Arg(args, "measuredKg", 0.0);
                double pct = Arg(args, "appliedPct", 10.0);
                if (kg <= 0 || pct <= 0) throw new CommandException("Enter the scale reading.");
                double measuredNm = kg * Hardware.Units.G * _settings.Wheel.RimRadiusM;
                double nominal = _settings.Wheel.BaseMaxTorqueNm * _settings.Wheel.SimProStrengthPct / 100.0 * pct / 100.0;
                double factor = Math.Clamp(measuredNm / nominal, 0.5, 1.5);
                SaveSettings(_settings with { Wheel = _settings.Wheel with { TorqueCalibration = Math.Round(factor, 3) } });
                BroadcastState();
                return new { factor };
            }

            case "bindButton":
                _bindPending = Enum.TryParse<ButtonAction>(Arg(args, "action", "None"), true, out var a) && a != ButtonAction.None ? a : null;
                return null;

            case "clearButtons":
                SaveSettings(_settings with { Buttons = new() });
                BroadcastState();
                return null;

            case "demoDriver":
            {
                if (Engine.Device is not SimulatedForceDevice sim) throw new CommandException("Only in demo mode");
                string mode = Arg(args, "mode", "follow");
                sim.Driver.Mode = mode switch { "manual" => DriverMode.Manual, "off" => DriverMode.HandsOff, _ => DriverMode.FollowGuide };
                if (args.ContainsKey("target")) sim.Driver.ManualTargetDeg = Arg(args, "target", 0.0);
                if (args.ContainsKey("strength")) sim.Driver.StrengthNm = Math.Clamp(Arg(args, "strength", 9.0), 1, 40);
                sim.Driver.ReleaseAtTime = null;
                return null;
            }

            default:
                throw new CommandException($"Unknown command '{cmd}'");
        }
    }

    private void LoadPlan(SessionPlan plan)
    {
        OnLoop(() =>
        {
            if (Engine.Governor.State == SafetyState.Armed && Engine.Governor.TestMode) Engine.Stop("Device test ended");
            if (_runner != null && _runner.Phase == SessionPhase.Work) throw new CommandException("Finish or end the current set first.");
            var runner = new SessionRunner(plan, Engine.Profile, _refs);
            lock (_lock) { _runner = runner; _sessionId = null; }
            Engine.SetActivity(runner);
        });
        BroadcastState();
    }

    private void EndSession()
    {
        OnLoop(() =>
        {
            Engine.Stop("Session ended");
            Engine.SetActivity(null);
        });
        lock (_lock)
        {
            if (_sessionId is { } sid) _repo.EndSession(sid, new { ended = "user" });
            _sessionId = null;
            _runner = null;
        }
        BroadcastState();
    }

    private void SelectDevice(string id)
    {
        var dev = CreateDevice(id) ?? throw new CommandException("Device not found");
        if (!dev.Connect(id)) throw new CommandException("Could not open the device");
        OnLoop(() =>
        {
            Engine.ReplaceDevice(dev);
            Engine.SetActivity(_runner);
        });
        _deviceId = id;
        bool changed = _settings.DeviceId != id && !(dev is SimulatedForceDevice);
        var s = _settings with { DeviceId = dev is SimulatedForceDevice ? _settings.DeviceId : id };
        if (changed) s = s with { DirectionVerified = false };
        SaveSettings(s);
    }

    // ---------------- state & history ----------------

    public object State()
    {
        var s = _settings;
        var program = _program;
        return new
        {
            version = _opt.Version,
            host = _opt.Host,
            demo = _opt.Demo || IsSim,
            settings = s,
            devices = ListDevices(),
            device = Engine.Device.Info,
            deviceId = _deviceId,
            requireDirectionCheck = Engine.Governor.RequireDirectionCheck,
            maxTorqueNm = Engine.Profile.MaxTorqueNm,
            capNm = Engine.Governor.GlobalCapNm,
            drills = DrillCatalog.All,
            laps = LapProfiles.All.Select(l => new { l.Id, l.Name, l.Description, lapS = l.LapS, segments = l.Segments.Select(x => new { x.Name, x.ApexDeg, x.Duration, x.LoadFrac }) }),
            freeRideProfiles = FreeRideLogic.ProfileNames,
            breakpoints = _refs.ToDictionary(),
            referenceMaxNm = _refs.Resolve(Hand.Both, 1),
            program = program == null ? null : new
            {
                program.CreatedUtc,
                program.Weeks,
                program.SessionsPerWeek,
                progress = program.Progress,
                completed = program.CompletedKeys,
                next = program.Next?.Key,
                workingLoadPct = program.WorkingLoadPct,
                workMultiplier = program.WorkMultiplier,
                log = program.ProgressionLog.TakeLast(20),
                sessions = program.Sessions.Select(x => new
                {
                    x.Key, x.Week, x.Number, x.Template, x.Focus, x.Deload, x.Retest, x.Baseline,
                    done = program.CompletedKeys.Contains(x.Key),
                    items = x.Items.Select(i => new { i.DrillId, name = DrillCatalog.Get(i.DrillId).Name, i.Role, i.Sets, i.WorkS, i.RestS, i.LoadPct, i.Hand, i.Direction }),
                }),
            },
            session = _runner == null ? null : new { _runner.Plan.Title, _runner.Plan.ProgramRef, items = _runner.Plan.Items.Count },
            bindPending = _bindPending?.ToString(),
        };
    }

    public object History()
    {
        var sessions = _repo.RecentSessions(50);
        var bps = _repo.Breakpoints();
        var weekly = _repo.WeeklyVolume(12);
        var sets = _repo.AllSets().TakeLast(300).Select(x => new { x.Id, x.SessionId, x.DrillId, x.DrillName, x.Role, x.Hand, x.Direction, x.LoadNm, x.Score, x.ScoreLabel, x.Completed, x.StartedUtc, x.Metrics });
        return new { sessions, breakpoints = bps, weekly, sets };
    }

    public void Dispose()
    {
        _running = false;
        try { Engine.Stop("App closing"); } catch { }
        _host?.Dispose();
        _pump?.Join(300);
        try { Engine.Device.Dispose(); } catch { }
        _repo.Dispose();
    }
}
