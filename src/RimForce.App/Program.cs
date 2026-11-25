using System.Diagnostics;
using System.Runtime.InteropServices;

namespace RimForce.App;

internal static class Program
{
    [DllImport("winmm.dll")] private static extern uint timeBeginPeriod(uint ms);
    [DllImport("winmm.dll")] private static extern uint timeEndPeriod(uint ms);

    public static string DataDir { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RimForce");

    [STAThread]
    private static void Main(string[] args)
    {
        Directory.CreateDirectory(DataDir);
        bool demo = args.Any(a => a.Equals("--demo", StringComparison.OrdinalIgnoreCase));
        bool browser = args.Any(a => a.Equals("--browser", StringComparison.OrdinalIgnoreCase));

        using var mutex = new Mutex(true, "Global\\RimForce.SingleInstance", out bool first);
        if (!first)
        {
            MessageBox.Show("RimForce is already running. Only one copy may control the wheel.", "RimForce", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Write("Unhandled: " + e.ExceptionObject);
        Application.ThreadException += (_, e) => Log.Write("UI thread: " + e.Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { Log.Write("Task: " + e.Exception); e.SetObserved(); };

        // 1 ms timer resolution so the force loop can hold 360-500 Hz.
        timeBeginPeriod(1);
        try
        {
            using (var p = Process.GetCurrentProcess()) p.PriorityClass = ProcessPriorityClass.AboveNormal;
        }
        catch { }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        try
        {
            Application.Run(new MainForm(demo, browser));
        }
        finally
        {
            timeEndPeriod(1);
        }
    }
}

internal static class Log
{
    private static readonly object Gate = new();
    public static string PathName => System.IO.Path.Combine(Program.DataDir, "rimforce.log");

    public static void Write(string msg)
    {
        try
        {
            lock (Gate) File.AppendAllText(PathName, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {msg}{Environment.NewLine}");
        }
        catch { }
    }
}
