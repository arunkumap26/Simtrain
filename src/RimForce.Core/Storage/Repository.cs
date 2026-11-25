using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using RimForce.Core.Drills;
using RimForce.Core.Programs;
using RimForce.Core.Sessions;
using RimForce.Core.Settings;

namespace RimForce.Core.Storage;

public sealed record BreakpointRecord(long Id, DateTime MeasuredUtc, Hand Hand, int Dir, double TorqueNm, bool Estimated);

public sealed record SessionRecord(long Id, DateTime StartedUtc, DateTime? EndedUtc, string Title, string? ProgramRef, int Sets, int Completed, double ImpulseNms, double WorkJ, double TutS);

public sealed record SetRecord(long Id, long SessionId, string DrillId, string DrillName, int ItemIndex, int SetIndex, string Role, Hand Hand, PushDir Direction,
    string? Grip, double LoadNm, double LoadPct, double WorkS, bool Completed, double Score, string ScoreLabel, Dictionary<string, double> Metrics, DateTime StartedUtc);

public sealed record WeekVolume(string Week, int Sessions, int Sets, double ImpulseNms, double WorkJ, double TutS);

/// <summary>SQLite persistence: settings, sessions, sets (with downsampled traces), breakpoints, program.</summary>
public sealed class Repository : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private readonly SqliteConnection _db;
    private readonly object _lock = new();

    public string Path { get; }

    public Repository(string path)
    {
        Path = path;
        var dir = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        _db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate }.ToString());
        _db.Open();
        Exec("PRAGMA journal_mode=WAL;");
        Migrate();
    }

    public static string DefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(root)) root = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        return System.IO.Path.Combine(root, "RimForce", "rimforce.db");
    }

    private void Migrate()
    {
        Exec("""
            CREATE TABLE IF NOT EXISTS settings (key TEXT PRIMARY KEY, json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS sessions (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                started_utc TEXT NOT NULL, ended_utc TEXT, title TEXT NOT NULL, program_ref TEXT, summary_json TEXT);
            CREATE TABLE IF NOT EXISTS sets (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                session_id INTEGER NOT NULL REFERENCES sessions(id),
                drill_id TEXT NOT NULL, drill_name TEXT NOT NULL, item_index INTEGER, set_index INTEGER, role TEXT,
                hand TEXT, direction TEXT, grip TEXT, load_nm REAL, load_pct REAL, intensity REAL, work_s REAL,
                completed INTEGER, score REAL, score_label TEXT, metrics_json TEXT, trace BLOB,
                started_utc TEXT, ended_utc TEXT, note TEXT);
            CREATE INDEX IF NOT EXISTS ix_sets_session ON sets(session_id);
            CREATE TABLE IF NOT EXISTS breakpoints (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                measured_utc TEXT NOT NULL, hand TEXT NOT NULL, dir INTEGER NOT NULL, torque_nm REAL NOT NULL, estimated INTEGER NOT NULL DEFAULT 0);
            CREATE TABLE IF NOT EXISTS program (id INTEGER PRIMARY KEY CHECK (id = 1), json TEXT NOT NULL);
            """);
    }

    private void Exec(string sql)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
    }

    private static string Iso(DateTime d) => d.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
    private static DateTime ParseIso(string s) => DateTime.Parse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    // ---------------- settings ----------------

    public AppSettings LoadSettings()
    {
        var json = GetKey("app");
        if (json == null) return new AppSettings();
        try { return (JsonSerializer.Deserialize<AppSettings>(json, Json) ?? new AppSettings()).Sanitised(); }
        catch { return new AppSettings(); }
    }

    public void SaveSettings(AppSettings s) => SetKey("app", JsonSerializer.Serialize(s.Sanitised(), Json));

    public string? GetKey(string key)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT json FROM settings WHERE key = $k";
            cmd.Parameters.AddWithValue("$k", key);
            return cmd.ExecuteScalar() as string;
        }
    }

    public void SetKey(string key, string json)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "INSERT INTO settings(key, json) VALUES($k, $j) ON CONFLICT(key) DO UPDATE SET json = excluded.json";
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$j", json);
            cmd.ExecuteNonQuery();
        }
    }

    // ---------------- sessions & sets ----------------

    public long StartSession(string title, string? programRef, DateTime? startedUtc = null)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "INSERT INTO sessions(started_utc, title, program_ref) VALUES($s, $t, $p); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("$s", Iso(startedUtc ?? DateTime.UtcNow));
            cmd.Parameters.AddWithValue("$t", title);
            cmd.Parameters.AddWithValue("$p", (object?)programRef ?? DBNull.Value);
            return (long)cmd.ExecuteScalar()!;
        }
    }

    public void EndSession(long id, object? summary = null, DateTime? endedUtc = null)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "UPDATE sessions SET ended_utc = $e, summary_json = $j WHERE id = $id";
            cmd.Parameters.AddWithValue("$e", Iso(endedUtc ?? DateTime.UtcNow));
            cmd.Parameters.AddWithValue("$j", summary == null ? DBNull.Value : JsonSerializer.Serialize(summary, Json));
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
    }

    public long SaveSet(long sessionId, SetResult r)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO sets(session_id, drill_id, drill_name, item_index, set_index, role, hand, direction, grip, load_nm, load_pct, intensity,
                                 work_s, completed, score, score_label, metrics_json, trace, started_utc, ended_utc, note)
                VALUES($sid, $d, $dn, $ii, $si, $role, $h, $dir, $g, $lnm, $lpct, $int, $w, $c, $sc, $sl, $m, $tr, $st, $en, $note);
                SELECT last_insert_rowid();
                """;
            cmd.Parameters.AddWithValue("$sid", sessionId);
            cmd.Parameters.AddWithValue("$d", r.DrillId);
            cmd.Parameters.AddWithValue("$dn", r.DrillName);
            cmd.Parameters.AddWithValue("$ii", r.ItemIndex);
            cmd.Parameters.AddWithValue("$si", r.SetIndex);
            cmd.Parameters.AddWithValue("$role", r.Role);
            cmd.Parameters.AddWithValue("$h", r.Hand.ToString());
            cmd.Parameters.AddWithValue("$dir", r.Direction.ToString());
            cmd.Parameters.AddWithValue("$g", (object?)r.Grip ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$lnm", r.LoadNm);
            cmd.Parameters.AddWithValue("$lpct", r.LoadPct);
            cmd.Parameters.AddWithValue("$int", r.Intensity);
            cmd.Parameters.AddWithValue("$w", r.WorkS);
            cmd.Parameters.AddWithValue("$c", r.Completed ? 1 : 0);
            cmd.Parameters.AddWithValue("$sc", r.Score);
            cmd.Parameters.AddWithValue("$sl", r.ScoreLabel);
            cmd.Parameters.AddWithValue("$m", JsonSerializer.Serialize(r.Metrics, Json));
            cmd.Parameters.AddWithValue("$tr", FloatsToBytes(r.Trace));
            cmd.Parameters.AddWithValue("$st", Iso(r.StartedUtc == default ? DateTime.UtcNow : r.StartedUtc));
            cmd.Parameters.AddWithValue("$en", Iso(r.EndedUtc == default ? DateTime.UtcNow : r.EndedUtc));
            cmd.Parameters.AddWithValue("$note", (object?)r.Note ?? DBNull.Value);
            return (long)cmd.ExecuteScalar()!;
        }
    }

    private static byte[] FloatsToBytes(float[] f)
    {
        var b = new byte[f.Length * 4];
        Buffer.BlockCopy(f, 0, b, 0, b.Length);
        return b;
    }

    private static float[] BytesToFloats(byte[] b)
    {
        var f = new float[b.Length / 4];
        Buffer.BlockCopy(b, 0, f, 0, f.Length * 4);
        return f;
    }

    public float[] GetTrace(long setId)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT trace FROM sets WHERE id = $id";
            cmd.Parameters.AddWithValue("$id", setId);
            return cmd.ExecuteScalar() is byte[] b ? BytesToFloats(b) : Array.Empty<float>();
        }
    }

    public IReadOnlyList<SessionRecord> RecentSessions(int limit = 30)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = """
                SELECT s.id, s.started_utc, s.ended_utc, s.title, s.program_ref, COUNT(t.id), COALESCE(SUM(t.completed),0), t.session_id
                FROM sessions s LEFT JOIN sets t ON t.session_id = s.id
                GROUP BY s.id ORDER BY s.started_utc DESC LIMIT $n
                """;
            cmd.Parameters.AddWithValue("$n", limit);
            var list = new List<(long id, DateTime st, DateTime? en, string title, string? pr, int sets, int done)>();
            using (var r = cmd.ExecuteReader())
                while (r.Read())
                    list.Add((r.GetInt64(0), ParseIso(r.GetString(1)), r.IsDBNull(2) ? null : ParseIso(r.GetString(2)), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4), r.GetInt32(5), r.GetInt32(6)));
            return list.Select(x =>
            {
                var sets = SetsForSession(x.id);
                return new SessionRecord(x.id, x.st, x.en, x.title, x.pr, x.sets, x.done,
                    Math.Round(sets.Sum(s => s.Metrics.GetValueOrDefault("impulseNms")), 1),
                    Math.Round(sets.Sum(s => s.Metrics.GetValueOrDefault("workConcJ") + s.Metrics.GetValueOrDefault("workEccJ")), 1),
                    Math.Round(sets.Sum(s => s.Metrics.GetValueOrDefault("tutS")), 1));
            }).ToList();
        }
    }

    public IReadOnlyList<SetRecord> SetsForSession(long sessionId) => QuerySets("WHERE session_id = $p ORDER BY id", sessionId);

    public IReadOnlyList<SetRecord> AllSets() => QuerySets("ORDER BY id", null);

    private IReadOnlyList<SetRecord> QuerySets(string where, object? p)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = $"""
                SELECT id, session_id, drill_id, drill_name, item_index, set_index, role, hand, direction, grip, load_nm, load_pct, work_s,
                       completed, score, score_label, metrics_json, started_utc FROM sets {where}
                """;
            if (p != null) cmd.Parameters.AddWithValue("$p", p);
            var list = new List<SetRecord>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                var metrics = JsonSerializer.Deserialize<Dictionary<string, double>>(r.IsDBNull(16) ? "{}" : r.GetString(16), Json) ?? new();
                list.Add(new SetRecord(r.GetInt64(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetInt32(4), r.GetInt32(5),
                    r.IsDBNull(6) ? "Working" : r.GetString(6),
                    Enum.TryParse<Hand>(r.GetString(7), out var h) ? h : Hand.Both,
                    Enum.TryParse<PushDir>(r.GetString(8), out var d) ? d : PushDir.Right,
                    r.IsDBNull(9) ? null : r.GetString(9), r.GetDouble(10), r.GetDouble(11), r.GetDouble(12),
                    r.GetInt32(13) == 1, r.GetDouble(14), r.GetString(15), metrics, ParseIso(r.GetString(17))));
            }
            return list;
        }
    }

    /// <summary>Training volume per ISO week (yyyy-Www).</summary>
    public IReadOnlyList<WeekVolume> WeeklyVolume(int weeks = 12)
    {
        var sets = AllSets();
        return sets.GroupBy(s => IsoWeek(s.StartedUtc))
            .OrderBy(g => g.Key)
            .TakeLast(weeks)
            .Select(g => new WeekVolume(g.Key, g.Select(s => s.SessionId).Distinct().Count(), g.Count(),
                Math.Round(g.Sum(s => s.Metrics.GetValueOrDefault("impulseNms")), 1),
                Math.Round(g.Sum(s => s.Metrics.GetValueOrDefault("workConcJ") + s.Metrics.GetValueOrDefault("workEccJ")), 1),
                Math.Round(g.Sum(s => s.Metrics.GetValueOrDefault("tutS")), 1)))
            .ToList();
    }

    public static string IsoWeek(DateTime d) => $"{ISOWeek.GetYear(d)}-W{ISOWeek.GetWeekOfYear(d):00}";

    // ---------------- breakpoints ----------------

    public void AddBreakpoint(Hand hand, int dir, double nm, bool estimated, DateTime? when = null)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "INSERT INTO breakpoints(measured_utc, hand, dir, torque_nm, estimated) VALUES($t, $h, $d, $n, $e)";
            cmd.Parameters.AddWithValue("$t", Iso(when ?? DateTime.UtcNow));
            cmd.Parameters.AddWithValue("$h", hand.ToString());
            cmd.Parameters.AddWithValue("$d", dir >= 0 ? 1 : -1);
            cmd.Parameters.AddWithValue("$n", nm);
            cmd.Parameters.AddWithValue("$e", estimated ? 1 : 0);
            cmd.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<BreakpointRecord> Breakpoints()
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT id, measured_utc, hand, dir, torque_nm, estimated FROM breakpoints ORDER BY measured_utc";
            var list = new List<BreakpointRecord>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new BreakpointRecord(r.GetInt64(0), ParseIso(r.GetString(1)), Enum.TryParse<Hand>(r.GetString(2), out var h) ? h : Hand.Both,
                    r.GetInt32(3), r.GetDouble(4), r.GetInt32(5) == 1));
            return list;
        }
    }

    /// <summary>Latest breakpoint per hand/direction loaded into a ReferenceStrength.</summary>
    public ReferenceStrength LoadReferenceStrength()
    {
        var refs = new ReferenceStrength();
        foreach (var b in Breakpoints()) refs.Record(b.Hand, b.Dir, b.TorqueNm); // ordered by time, latest wins
        return refs;
    }

    // ---------------- program ----------------

    public TrainingProgram? LoadProgram()
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT json FROM program WHERE id = 1";
            var json = cmd.ExecuteScalar() as string;
            if (json == null) return null;
            try { return JsonSerializer.Deserialize<TrainingProgram>(json, Json); } catch { return null; }
        }
    }

    public void SaveProgram(TrainingProgram p)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "INSERT INTO program(id, json) VALUES(1, $j) ON CONFLICT(id) DO UPDATE SET json = excluded.json";
            cmd.Parameters.AddWithValue("$j", JsonSerializer.Serialize(p, Json));
            cmd.ExecuteNonQuery();
        }
    }

    // ---------------- CSV export ----------------

    public static string CsvEscape(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    public string ExportSetsCsv()
    {
        var sets = AllSets();
        var keys = sets.SelectMany(s => s.Metrics.Keys).Distinct().OrderBy(k => k).ToList();
        var sb = new StringBuilder();
        sb.Append("set_id,session_id,started_utc,drill_id,drill,role,set,hand,direction,grip,load_nm,load_pct,work_s,completed,score,score_label");
        foreach (var k in keys) sb.Append(',').Append(CsvEscape(k));
        sb.AppendLine();
        foreach (var s in sets)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{s.Id},{s.SessionId},{Iso(s.StartedUtc)},{CsvEscape(s.DrillId)},{CsvEscape(s.DrillName)},{CsvEscape(s.Role)},{s.SetIndex + 1},{s.Hand},{s.Direction},{CsvEscape(s.Grip)},");
            sb.Append(CultureInfo.InvariantCulture, $"{s.LoadNm},{s.LoadPct},{s.WorkS},{(s.Completed ? 1 : 0)},{s.Score},{CsvEscape(s.ScoreLabel)}");
            foreach (var k in keys)
                sb.Append(',').Append(s.Metrics.TryGetValue(k, out var v) ? v.ToString(CultureInfo.InvariantCulture) : "");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public string ExportBreakpointsCsv()
    {
        var sb = new StringBuilder("measured_utc,hand,direction,torque_nm,estimated\n");
        foreach (var b in Breakpoints())
            sb.Append(CultureInfo.InvariantCulture, $"{Iso(b.MeasuredUtc)},{b.Hand},{(b.Dir > 0 ? "right" : "left")},{b.TorqueNm},{(b.Estimated ? 1 : 0)}\n");
        return sb.ToString();
    }

    /// <summary>Writes sets.csv and breakpoints.csv into a folder; returns the folder.</summary>
    public string ExportCsv(string folder)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllText(System.IO.Path.Combine(folder, "sets.csv"), ExportSetsCsv());
        File.WriteAllText(System.IO.Path.Combine(folder, "breakpoints.csv"), ExportBreakpointsCsv());
        return folder;
    }

    public void Dispose()
    {
        lock (_lock) _db.Dispose();
    }
}
