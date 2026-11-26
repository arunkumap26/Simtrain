using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using RimForce.Core.App;
using RimForce.DirectInput;
using RimForce.Server;

namespace RimForce.App;

/// <summary>
/// The Windows shell: a dark window hosting the UI in WebView2, the app controller (force loop, storage) and the
/// loopback server the UI talks to. Window focus changes go straight to the safety governor.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly bool _demo;
    private readonly bool _forceBrowser;
    private AppController? _app;
    private RimForceServer? _server;
    private WebView2? _web;
    private nint _hwnd;
    private readonly Label _status;

    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, System.Text.StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(nint hwnd, int id, uint mods, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hwnd, int id);
    private const int WM_HOTKEY = 0x0312;
    private const int StopHotkeyId = 0x5246;
    private const uint VK_PAUSE = 0x13, MOD_NOREPEAT = 0x4000;
    private const int WM_ACTIVATE = 0x0006;
    private const int WA_INACTIVE = 0;

    public MainForm(bool demo, bool forceBrowser)
    {
        _demo = demo;
        _forceBrowser = forceBrowser;
        Text = demo ? "RimForce — demo (simulated wheel)" : "RimForce";
        BackColor = Color.FromArgb(7, 10, 15);
        ForeColor = Color.FromArgb(220, 230, 240);
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1600, 980);
        MinimumSize = new Size(1100, 700);
        KeyPreview = true;
        try
        {
            using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("rimforce.ico");
            if (s != null) Icon = new Icon(s);
        }
        catch { }
        _status = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 14f),
            ForeColor = Color.FromArgb(150, 170, 190),
            Text = "Starting RimForce…",
        };
        Controls.Add(_status);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _hwnd = Handle;
        try { int on = 1; DwmSetWindowAttribute(Handle, 20, ref on, sizeof(int)); } catch { } // dark title bar
        // Global STOP: Pause/Break stops all force even when another window has focus.
        try { if (!RegisterHotKey(Handle, StopHotkeyId, MOD_NOREPEAT, VK_PAUSE)) Log.Write("Could not register the Pause/Break STOP hotkey (another app owns it)"); } catch { }
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1.0";
            _app = new AppController(new AppOptions
            {
                DbPath = Path.Combine(Program.DataDir, "rimforce.db"),
                Demo = _demo,
                Host = "windows",
                Version = version,
                Providers = { new DirectInputDeviceProvider(DiOptions) },
            });
            _app.OpenPathRequested += path =>
            {
                try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true }); } catch { }
            };
            _app.Start();
            _server = new RimForceServer(_app);
            await _server.StartAsync();
            Log.Write($"Started v{version} demo={_demo} url={_server.BaseUrl} device={_app.Engine.Device.Info?.Name}");

            if (_forceBrowser || !await TryStartWebView())
                OpenInBrowser();
        }
        catch (Exception ex)
        {
            Log.Write("Startup failed: " + ex);
            _status.Text = "RimForce could not start:\n" + ex.Message + "\n\nSee " + Log.PathName;
        }
    }

    private DirectInputOptions DiOptions()
    {
        var s = _app?.Settings;
        bool background = s?.CooperativeLevel == "background" || _forceBrowser || _web == null;
        return new DirectInputOptions(() => _hwnd, background, s?.Safety.FiniteEffectDurations ?? false,
            (int)Math.Round((s?.Safety.EffectDurationS ?? 0.25) * 1000), s?.Wheel.RotationRangeDeg ?? 900);
    }

    private async Task<bool> TryStartWebView()
    {
        try
        {
            var web = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(7, 10, 15) };
            var options = new CoreWebView2EnvironmentOptions(
                "--disable-background-timer-throttling --disable-renderer-backgrounding --disable-backgrounding-occluded-windows " +
                "--disable-features=IntensiveWakeUpThrottling,CalculateNativeWinOcclusion");
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Program.DataDir, "WebView2"), options);
            Controls.Add(web);
            await web.EnsureCoreWebView2Async(env);
            web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            web.CoreWebView2.Settings.IsZoomControlEnabled = false;
            web.CoreWebView2.Settings.IsStatusBarEnabled = false;
            web.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;
            web.CoreWebView2.ProcessFailed += (_, a) =>
            {
                Log.Write("WebView2 process failed: " + a.ProcessFailedKind);
                _app?.Engine.Trip(RimForce.Core.Safety.TripCode.UiHeartbeat, "UI process crashed");
            };
            web.Source = new Uri(_server!.AppUrl + "&shell=webview2");
            Controls.Remove(_status);
            web.BringToFront();
            _web = web;
            return true;
        }
        catch (Exception ex)
        {
            Log.Write("WebView2 unavailable: " + ex.Message);
            return false;
        }
    }

    private void OpenInBrowser()
    {
        _status.Text = "RimForce is running in your browser.\n\nKeep this window open — closing it stops RimForce and the wheel.\n\n" + _server!.AppUrl;
        try { Process.Start(new ProcessStartInfo(_server.AppUrl) { UseShellExecute = true }); } catch { }
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        _app?.Engine.SetFocus(true);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt64() == StopHotkeyId)
        {
            _app?.Engine.Stop("Pause/Break STOP");
            Log.Write("Global STOP hotkey");
        }
        // WM_ACTIVATE with WA_INACTIVE: lParam is the window being activated (may be 0).
        if (m.Msg == WM_ACTIVATE && (m.WParam.ToInt64() & 0xFFFF) == WA_INACTIVE)
        {
            try { OnLostActivation(m.LParam); } catch (Exception ex) { Log.Write("Focus check failed: " + ex.Message); _app?.Engine.SetFocus(false); }
        }
        base.WndProc(ref m);
    }

    /// <summary>
    /// The window stopped being the active window. If the window taking over belongs to RimForce itself (its own
    /// process or the WebView2 browser processes that render the UI — e.g. a dropdown or tooltip popup), it is not
    /// a real focus loss. Anything else stops the wheel and is named in the safety message and the log.
    /// </summary>
    private void OnLostActivation(nint activated)
    {
        // In browser mode this window is not the UI; the page reports its own focus instead.
        if (_web == null) return;
        nint target = activated != 0 ? activated : GetForegroundWindow();
        uint pid = 0;
        if (target != 0) GetWindowThreadProcessId(target, out pid);
        if (pid != 0 && IsOwnProcess(pid))
        {
            Log.Write($"Focus moved to RimForce's own UI process (pid {pid}) — not treated as focus loss");
            return;
        }
        string who = DescribeWindow(target, pid);
        Log.Write("Focus lost to " + who);
        _app?.Engine.SetFocus(false, who);
    }

    private bool IsOwnProcess(uint pid)
    {
        if (pid == (uint)Environment.ProcessId) return true;
        var core = _web?.CoreWebView2;
        if (core == null) return false;
        if (pid == core.BrowserProcessId) return true;
        try
        {
            foreach (var p in core.Environment.GetProcessInfos())
                if ((uint)p.ProcessId == pid) return true;
        }
        catch { }
        return false;
    }

    private static string DescribeWindow(nint hwnd, uint pid)
    {
        if (hwnd == 0 || pid == 0) return "another window";
        string name = "unknown";
        try { using var p = Process.GetProcessById((int)pid); name = p.ProcessName + ".exe"; } catch { }
        var root = GetAncestor(hwnd, 2 /* GA_ROOT */);
        var sb = new System.Text.StringBuilder(256);
        GetWindowText(root != 0 ? root : hwnd, sb, sb.Capacity);
        string title = sb.ToString().Trim();
        return string.IsNullOrEmpty(title) ? name : $"{name} — \"{(title.Length > 60 ? title[..60] + "…" : title)}\"";
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Escape or Keys.Space) _app?.Engine.Stop("Key stop");
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        try { _app?.Engine.Stop("Closing"); } catch { }
        try { UnregisterHotKey(Handle, StopHotkeyId); } catch { }
        base.OnFormClosing(e);
        try { if (_server != null) await _server.DisposeAsync(); } catch { }
        try { _app?.Dispose(); } catch { }
        Log.Write("Closed");
    }
}
