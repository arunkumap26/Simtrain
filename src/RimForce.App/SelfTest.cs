using System.Net.Http;
using RimForce.Core.App;
using RimForce.DirectInput;
using RimForce.Server;

namespace RimForce.App;

/// <summary>
/// `RimForce.exe --selftest`: headless start-up check without touching force feedback. Writes
/// %LOCALAPPDATA%\RimForce\selftest.txt and exits 0 when everything that can be checked without a human passes.
/// Useful to send back when something does not start.
/// </summary>
internal static class SelfTest
{
    public static int Run()
    {
        var lines = new List<string> { $"RimForce self-test {DateTime.Now:O}", $"OS: {Environment.OSVersion}", $"64-bit: {Environment.Is64BitProcess}" };
        int failures = 0;
        void Check(string name, Func<string> f)
        {
            try { lines.Add($"PASS {name}: {f()}"); }
            catch (Exception ex) { failures++; lines.Add($"FAIL {name}: {ex.GetType().Name}: {ex.Message}"); }
        }

        Check("embedded UI", () => RimForceServer.HasEmbeddedUi ? "present" : throw new Exception("ui/index.html not embedded"));
        Check("DirectInput enumerate", () =>
        {
            var devs = DirectInputForceDevice.EnumerateDevices();
            return devs.Count == 0 ? "no force-feedback devices (fine if the wheel is unplugged)" : string.Join("; ", devs.Select(d => $"{d.Name} [{d.Id}]"));
        });
        string db = Path.Combine(Path.GetTempPath(), "rimforce-selftest.db");
        AppController? app = null;
        RimForceServer? server = null;
        Check("SQLite + app controller", () =>
        {
            app = new AppController(new AppOptions { DbPath = db, Demo = true, Host = "selftest" });
            app.Start();
            Thread.Sleep(1200);
            return $"force loop {app.Engine.Latest?.LoopHz:0} Hz";
        });
        Check("loopback server", () =>
        {
            server = new RimForceServer(app!);
            server.StartAsync().GetAwaiter().GetResult();
            using var http = new HttpClient();
            var health = http.GetStringAsync(server.BaseUrl + "/healthz").GetAwaiter().GetResult();
            var html = http.GetStringAsync(server.BaseUrl + "/").GetAwaiter().GetResult();
            if (!html.Contains("<div id=\"root\">")) throw new Exception("index.html not served");
            return $"{health}, {server.BaseUrl}";
        });
        try { server?.DisposeAsync().AsTask().GetAwaiter().GetResult(); } catch { }
        try { app?.Dispose(); } catch { }
        try { File.Delete(db); } catch { }

        lines.Add(failures == 0 ? "RESULT: OK" : $"RESULT: {failures} failure(s)");
        var text = string.Join(Environment.NewLine, lines);
        try { File.WriteAllText(Path.Combine(Program.DataDir, "selftest.txt"), text); } catch { }
        try { Console.WriteLine(text); } catch { }
        return failures == 0 ? 0 : 1;
    }
}
