using Microsoft.Data.Sqlite;
using MustBeCleaner.Infrastructure.Journal;

namespace MustBeCleaner.Infrastructure.Tests;

/// <summary>Test-only side door to inspect what is really on disk, independent of the Store API.</summary>
internal static class RawSql
{
    private static SqliteConnection Connect(string dataDirectory)
    {
        var path = Path.Combine(dataDirectory, JournalStore.DatabaseFileName);
        var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        connection.Open();
        return connection;
    }

    public static void Execute(string dataDirectory, string sql)
    {
        using var c = Connect(dataDirectory);
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public static long Scalar(string dataDirectory, string sql)
    {
        using var c = Connect(dataDirectory);
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        return Convert.ToInt64(cmd.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    public static List<string> Strings(string dataDirectory, string sql)
    {
        using var c = Connect(dataDirectory);
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        using var r = cmd.ExecuteReader();
        var list = new List<string>();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }
}
