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
            var env = await CoreWebView2Environment.CreateAsync(null, Path.Combine(Program.DataDir, "WebView2"));
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
            web.Source = new Uri(_server!.AppUrl);
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

    protected override void OnDeactivate(EventArgs e)
    {
        base.OnDeactivate(e);
        // In browser mode this window is not the UI; the page reports its own focus instead.
        if (_web != null) _app?.Engine.SetFocus(false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Escape or Keys.Space) _app?.Engine.Stop("Key stop");
    }

    protected override async void OnFormClosing(FormClosingEventArgs e)
    {
        try { _app?.Engine.Stop("Closing"); } catch { }
        base.OnFormClosing(e);
        try { if (_server != null) await _server.DisposeAsync(); } catch { }
        try { _app?.Dispose(); } catch { }
        Log.Write("Closed");
    }
}
