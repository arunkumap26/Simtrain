using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using RimForce.Core.App;
using RimForce.Core.Engine;
using RimForce.Core.Safety;
using RimForce.Core.Sessions;
using RimForce.Server;

namespace RimForce.Tests;

public class AppControllerTests : IDisposable
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), "rimforce-app-" + Guid.NewGuid().ToString("N") + ".db");
    private readonly ManualClock _clock = new();
    private readonly AppController _app;
    private readonly List<string> _out = new();

    public AppControllerTests()
    {
        _app = new AppController(new AppOptions { DbPath = _db, Demo = true, Clock = _clock });
        _app.Outgoing += s => { lock (_out) _out.Add(s); };
    }

    public void Dispose()
    {
        _app.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_db); } catch { }
    }

    private JsonNode Cmd(string cmd, object? args = null)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new { id = 1, cmd, args = args ?? new { } }, AppController.Json);
        return JsonNode.Parse(_app.HandleMessage(json))!;
    }

    private void Run(double s)
    {
        int n = (int)(s * 360);
        for (int i = 0; i < n; i++) { _clock.Advance(1.0 / 360); _app.Engine.Heartbeat(); _app.Engine.Tick(); }
        _app.Pump();
    }

    [Fact]
    public void HelloReturnsState()
    {
        var r = Cmd("hello");
        Assert.True(r["ok"]!.GetValue<bool>());
        Assert.True(r["data"]!["demo"]!.GetValue<bool>());
        Assert.True(r["data"]!["drills"]!.AsArray().Count >= 13);
    }

    [Fact]
    public void UnknownCommandFails()
    {
        var r = Cmd("selfDestruct");
        Assert.False(r["ok"]!.GetValue<bool>());
    }

    [Fact]
    public void DrillRunsEndToEndAndIsPersisted()
    {
        var cfg = new { drillId = "corner-hold", loadPct = 40, sets = 1, workS = 6, restS = 0, hand = "Both", direction = "Right", @params = new { angle = 0, zone = 3 } };
        Assert.True(Cmd("startDrill", new { config = cfg })["ok"]!.GetValue<bool>());
        Run(1);
        var go = Cmd("go");
        Assert.True(go["ok"]!.GetValue<bool>(), go["error"]?.ToString());
        Assert.Equal(SafetyState.Armed, _app.Engine.Governor.State);
        Run(14);
        Assert.Equal(SessionPhase.Complete, _app.Runner!.Phase);
        Run(0.2);
        var hist = Cmd("history");
        Assert.Single(hist["data"]!["sets"]!.AsArray());
        lock (_out) Assert.Contains(_out, s => s.Contains("\"kind\":\"setEnd\""));
        lock (_out) Assert.Contains(_out, s => s.StartsWith("{\"type\":\"tick\""));
    }

    [Fact]
    public void StopIsImmediateAndTripNeedsAck()
    {
        var cfg = new { drillId = "corner-hold", loadPct = 40, sets = 1, workS = 30, restS = 0, hand = "Both", direction = "Right", @params = new { angle = 0 } };
        Cmd("startDrill", new { config = cfg });
        Run(1);
        Cmd("go");
        Run(6);
        Cmd("stop");
        Assert.Equal(SafetyState.Disarmed, _app.Engine.Governor.State);
        _app.Engine.Trip(TripCode.HandsOff, "test");
        var go = Cmd("go");
        Assert.False(go["ok"]!.GetValue<bool>());
        Cmd("ackTrip");
        Run(0.1);
        Assert.Equal(SafetyState.Disarmed, _app.Engine.Governor.State);
    }

    [Fact]
    public void IntensityCommandsClampAndPersist()
    {
        Cmd("setIntensity", new { value = 3.0 });
        Assert.Equal(1.5, _app.Engine.Intensity);
        Cmd("nudgeIntensity", new { delta = -0.1 });
        Assert.Equal(1.4, _app.Engine.Intensity, 3);
        Run(0.05);
        Assert.Equal(1.4, _app.Repository.LoadSettings().Intensity, 3);
    }

    [Fact]
    public void SettingsCannotClearChecklistAcceptance()
    {
        Cmd("acceptChecklist");
        var s = _app.Settings with { ChecklistAccepted = false, ForceUnit = "N" };
        Cmd("saveSettings", new { settings = s });
        Assert.True(_app.Settings.ChecklistAccepted);
        Assert.Equal("N", _app.Settings.ForceUnit);
    }

    [Fact]
    public void ProgramGenerationAndStart()
    {
        Assert.True(Cmd("generateProgram", new { sessionsPerWeek = 2 })["ok"]!.GetValue<bool>());
        Assert.True(Cmd("startProgramSession")["ok"]!.GetValue<bool>());
        Assert.Equal("w1s1", _app.Runner!.Plan.ProgramRef);
    }

    [Fact]
    public void DeviceTestIsCappedAt10Percent()
    {
        Cmd("acceptChecklist");
        Run(0.5);
        var r = Cmd("deviceTest", new { effect = "constant-right", pct = 50 });
        Assert.True(r["ok"]!.GetValue<bool>(), r["error"]?.ToString());
        Assert.True(_app.Engine.Governor.TestMode);
        Run(3);
        Assert.True(Math.Abs(_app.Engine.Latest!.MotorTorqueNm) <= _app.Engine.Profile.MaxTorqueNm * 0.10 + 1e-6);
        Cmd("deviceTest", new { effect = "none" });
    }
}

public class ServerSecurityTests : IAsyncLifetime
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), "rimforce-srv-" + Guid.NewGuid().ToString("N") + ".db");
    private AppController _app = null!;
    private RimForceServer _server = null!;

    public async Task InitializeAsync()
    {
        _app = new AppController(new AppOptions { DbPath = _db, Demo = true });
        _app.Start();
        _server = new RimForceServer(_app, 0, "secret-token");
        await _server.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _server.DisposeAsync();
        _app.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_db); } catch { }
    }

    private async Task<WebSocketState> TryConnect(string token, string? origin)
    {
        using var ws = new ClientWebSocket();
        if (origin != null) ws.Options.SetRequestHeader("Origin", origin);
        try
        {
            await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{_server.Port}/ws?token={token}"), CancellationToken.None);
            return ws.State;
        }
        catch (WebSocketException) { return WebSocketState.Closed; }
    }

    [Fact]
    public async Task WrongTokenIsRejected() => Assert.NotEqual(WebSocketState.Open, await TryConnect("nope", null));

    [Fact]
    public async Task ForeignOriginIsRejected() => Assert.NotEqual(WebSocketState.Open, await TryConnect("secret-token", "https://evil.example"));

    [Fact]
    public async Task OwnOriginAndTokenConnectAndReceiveTicks()
    {
        using var ws = new ClientWebSocket();
        ws.Options.SetRequestHeader("Origin", $"http://127.0.0.1:{_server.Port}");
        await ws.ConnectAsync(new Uri($"ws://127.0.0.1:{_server.Port}/ws?token=secret-token"), CancellationToken.None);
        var buf = new byte[1 << 20];
        int ticks = 0;
        var until = DateTime.UtcNow.AddSeconds(3);
        while (ticks < 5 && DateTime.UtcNow < until)
        {
            using var cts = new CancellationTokenSource(1000);
            var r = await ws.ReceiveAsync(buf, cts.Token);
            if (Encoding.UTF8.GetString(buf, 0, r.Count).StartsWith("{\"type\":\"tick\"")) ticks++;
        }
        Assert.True(ticks >= 5);
    }

    [Fact]
    public async Task ServerListensOnLoopbackOnly()
    {
        using var http = new HttpClient();
        var ok = await http.GetStringAsync($"http://127.0.0.1:{_server.Port}/healthz");
        Assert.Equal("ok", ok);
        // The listener is bound to IPAddress.Loopback; a non-loopback address of this machine must not answer.
        var other = (await Dns.GetHostAddressesAsync(Dns.GetHostName())).FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
        if (other == null) return;
        await Assert.ThrowsAnyAsync<Exception>(() => http.GetStringAsync($"http://{other}:{_server.Port}/healthz"));
    }
}
