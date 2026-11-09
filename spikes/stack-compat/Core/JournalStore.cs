using Microsoft.Data.Sqlite;

namespace Spike.Core;

/// <summary>Minimal Journal store in WAL mode (Glossary: Journal, Transaction_Set).</summary>
public sealed class JournalStore : IDisposable
{
    private readonly SqliteConnection _c;
    private JournalStore(SqliteConnection c) => _c = c;

    public string JournalMode { get; private set; } = "";

    public static JournalStore Open(string path)
    {
        var c = new SqliteConnection($"Data Source={path};Pooling=False");
        c.Open();
        var s = new JournalStore(c);
        s.JournalMode = (string)s.Scalar("PRAGMA journal_mode=WAL;")!;
        s.Scalar("CREATE TABLE IF NOT EXISTS journal(item TEXT PRIMARY KEY, stage TEXT NOT NULL);");
        return s;
    }

    public void Record(string item, string stage)
    {
        using var cmd = _c.CreateCommand();
        cmd.CommandText = "INSERT INTO journal(item,stage) VALUES($i,$s) ON CONFLICT(item) DO UPDATE SET stage=$s";
        cmd.Parameters.AddWithValue("$i", item);
        cmd.Parameters.AddWithValue("$s", stage);
        cmd.ExecuteNonQuery();
    }

    public string? StageOf(string item)
    {
        using var cmd = _c.CreateCommand();
        cmd.CommandText = "SELECT stage FROM journal WHERE item=$i";
        cmd.Parameters.AddWithValue("$i", item);
        return cmd.ExecuteScalar() as string;
    }

    private object? Scalar(string sql)
    {
        using var cmd = _c.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar();
    }

    public void Dispose() => _c.Dispose();
}
