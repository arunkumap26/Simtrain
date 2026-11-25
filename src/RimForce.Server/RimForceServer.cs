using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RimForce.Core.App;
using RimForce.Core.Safety;

namespace RimForce.Server;

/// <summary>
/// Serves the UI and a WebSocket bridge to the <see cref="AppController"/> on loopback only.
/// Every connection needs the random per-launch token, and browser Origins other than our own are refused,
/// so a random web page cannot connect to the wheel.
/// </summary>
public sealed class RimForceServer : IAsyncDisposable
{
    private readonly AppController _app;
    private readonly string? _uiDir;
    private readonly ConcurrentDictionary<Guid, Channel<string>> _clients = new();
    private WebApplication? _web;
    private readonly int _requestedPort;

    public string Token { get; }
    public int Port { get; private set; }
    public string BaseUrl => $"http://127.0.0.1:{Port}";
    public string AppUrl => $"{BaseUrl}/?token={Token}";
    public int ClientCount => _clients.Count;

    public RimForceServer(AppController app, int port = 0, string? token = null, string? uiDir = null)
    {
        _app = app;
        _requestedPort = port;
        _uiDir = uiDir;
        Token = token ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, _requestedPort));
        var web = builder.Build();
        web.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(10) });

        web.Map("/ws", async ctx =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest) { ctx.Response.StatusCode = 400; return; }
            if (ctx.Request.Query["token"] != Token) { ctx.Response.StatusCode = 403; return; }
            if (!OriginAllowed(ctx.Request.Headers.Origin.ToString())) { ctx.Response.StatusCode = 403; return; }
            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            await RunClient(ws, ctx.RequestAborted);
        });

        web.MapGet("/healthz", () => Results.Text("ok"));
        web.MapFallback(ServeUi);

        _app.Outgoing += Broadcast;
        await web.StartAsync();
        _web = web;
        var addr = web.Urls.FirstOrDefault() ?? $"http://127.0.0.1:{_requestedPort}";
        Port = new Uri(addr.Replace("[::]", "127.0.0.1")).Port;
    }

    private bool OriginAllowed(string origin)
    {
        if (string.IsNullOrEmpty(origin)) return true; // non-browser client (tests)
        return origin == $"http://127.0.0.1:{Port}" || origin == $"http://localhost:{Port}";
    }

    private void Broadcast(string json)
    {
        bool isTick = json.StartsWith("{\"type\":\"tick\"", StringComparison.Ordinal);
        foreach (var ch in _clients.Values)
        {
            // Ticks may be dropped for slow clients; events and state never are (unbounded writer for those).
            if (isTick && ch.Reader.Count > 8) continue;
            ch.Writer.TryWrite(json);
        }
    }

    private async Task RunClient(WebSocket ws, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var outbox = Channel.CreateUnbounded<string>(new UnboundedChannelOptions { SingleReader = true });
        _clients[id] = outbox;
        _app.Engine.Heartbeat();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sender = Task.Run(async () =>
        {
            try
            {
                await foreach (var msg in outbox.Reader.ReadAllAsync(cts.Token))
                {
                    var bytes = Encoding.UTF8.GetBytes(msg);
                    await ws.SendAsync(bytes, WebSocketMessageType.Text, true, cts.Token);
                }
            }
            catch { }
        });

        try
        {
            outbox.Writer.TryWrite(System.Text.Json.JsonSerializer.Serialize(new { type = "state", data = _app.State() }, AppController.Json));
            var buffer = new byte[64 * 1024];
            var sb = new StringBuilder();
            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var res = await ws.ReceiveAsync(buffer, ct);
                if (res.MessageType == WebSocketMessageType.Close) break;
                sb.Append(Encoding.UTF8.GetString(buffer, 0, res.Count));
                if (!res.EndOfMessage) continue;
                string text = sb.ToString();
                sb.Clear();
                string reply = await Task.Run(() => _app.HandleMessage(text));
                outbox.Writer.TryWrite(reply);
            }
        }
        catch { /* client went away */ }
        finally
        {
            _clients.TryRemove(id, out _);
            outbox.Writer.TryComplete();
            cts.Cancel();
            try { await sender; } catch { }
            if (_clients.IsEmpty && _app.Engine.Governor.State == SafetyState.Armed)
                _app.Engine.Trip(TripCode.UiHeartbeat, "App window disconnected");
            try { if (ws.State == WebSocketState.Open) await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); } catch { }
        }
    }

    // ---------------- static UI ----------------

    private static readonly Dictionary<string, string> Mime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8", [".js"] = "text/javascript; charset=utf-8", [".css"] = "text/css; charset=utf-8",
        [".svg"] = "image/svg+xml", [".png"] = "image/png", [".jpg"] = "image/jpeg", [".json"] = "application/json",
        [".woff2"] = "font/woff2", [".woff"] = "font/woff", [".ico"] = "image/x-icon", [".wav"] = "audio/wav", [".mp3"] = "audio/mpeg",
        [".webmanifest"] = "application/manifest+json", [".txt"] = "text/plain",
    };

    private static readonly Lazy<Dictionary<string, string>> Embedded = new(() =>
    {
        var asm = typeof(RimForceServer).Assembly;
        return asm.GetManifestResourceNames()
            .Where(n => n.StartsWith("ui/", StringComparison.Ordinal) || n.StartsWith("ui\\", StringComparison.Ordinal))
            .ToDictionary(n => n.Replace('\\', '/').Substring(3), n => n, StringComparer.OrdinalIgnoreCase);
    });

    public static bool HasEmbeddedUi => Embedded.Value.ContainsKey("index.html");

    private async Task ServeUi(HttpContext ctx)
    {
        string path = ctx.Request.Path.Value?.TrimStart('/') ?? "";
        if (path.Contains("..")) { ctx.Response.StatusCode = 400; return; }
        if (path.Length == 0) path = "index.html";
        string ext = Path.GetExtension(path);
        ctx.Response.Headers.CacheControl = "no-cache";
        ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";

        Stream? stream = null;
        if (_uiDir != null)
        {
            var file = Path.Combine(_uiDir, path.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(file)) { file = Path.Combine(_uiDir, "index.html"); ext = ".html"; }
            if (File.Exists(file)) stream = File.OpenRead(file);
        }
        else
        {
            var map = Embedded.Value;
            if (!map.TryGetValue(path, out var res)) { res = map.GetValueOrDefault("index.html"); ext = ".html"; }
            if (res != null) stream = typeof(RimForceServer).Assembly.GetManifestResourceStream(res);
        }

        if (stream == null)
        {
            ctx.Response.ContentType = "text/html; charset=utf-8";
            await ctx.Response.WriteAsync("<h1>RimForce</h1><p>The UI has not been built. Run <code>npm ci && npm run build</code> in <code>ui/</code> and rebuild.</p>");
            return;
        }
        await using (stream)
        {
            ctx.Response.ContentType = Mime.GetValueOrDefault(ext, "application/octet-stream");
            await stream.CopyToAsync(ctx.Response.Body);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _app.Outgoing -= Broadcast;
        foreach (var c in _clients.Values) c.Writer.TryComplete();
        if (_web != null)
        {
            try { await _web.StopAsync(TimeSpan.FromSeconds(1)); } catch { }
            await _web.DisposeAsync();
        }
    }
}
