using RimForce.Core.App;
using RimForce.Server;

// Dev / demo host: runs the real app logic against the simulated wheel and serves the UI in a browser.
//   dotnet run --project src/RimForce.DevHost -- --port 5180 [--token dev] [--db path] [--ui-dir ui/dist]
int port = 5180;
string? token = null;
string? db = null;
string? uiDir = null;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--port": port = int.Parse(args[++i]); break;
        case "--token": token = args[++i]; break;
        case "--db": db = args[++i]; break;
        case "--ui-dir": uiDir = args[++i]; break;
    }
}
db ??= Path.Combine(Path.GetTempPath(), "rimforce-dev", "rimforce.db");

using var app = new AppController(new AppOptions { DbPath = db, Demo = true, Host = "devhost", ExportRoot = Path.Combine(Path.GetDirectoryName(db)!, "exports") });
app.Start();
await using var server = new RimForceServer(app, port, token, uiDir);
await server.StartAsync();
Console.WriteLine($"RimForce dev host (simulated wheel) — open {server.AppUrl}");
Console.WriteLine($"DB: {db}");
var done = new TaskCompletionSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; done.TrySetResult(); };
AppDomain.CurrentDomain.ProcessExit += (_, _) => done.TrySetResult();
await done.Task;
