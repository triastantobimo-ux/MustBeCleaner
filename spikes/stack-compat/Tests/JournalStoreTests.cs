using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Spike.Core;
using Xunit;

namespace Spike.Tests;

public class JournalStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("mbc-spike-").FullName;
    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, true);
    }

    [Fact]
    public void Opens_in_wal_mode_and_reads_back_a_written_stage()
    {
        using var store = JournalStore.Open(Path.Combine(_dir, "j.db"));
        Assert.Equal("wal", store.JournalMode);
        store.Record("item-1", "Prepared");
        Assert.Equal("Prepared", store.StageOf("item-1"));
    }

    [Fact]
    public void Wal_sidecar_exists_while_open()
    {
        var path = Path.Combine(_dir, "j.db");
        using var store = JournalStore.Open(path);
        store.Record("i", "Copied");
        Assert.True(File.Exists(path + "-wal"));
    }

    [FsCheck.Xunit.Property(MaxTest = 100)]
    public bool Smoke_reverse_twice_is_identity(int[] xs) =>
        xs.Reverse().Reverse().SequenceEqual(xs);

    [FsCheck.Xunit.Property(MaxTest = 100)]
    public bool Stage_roundtrips_for_any_item_id(NonEmptyString id)
    {
        using var store = JournalStore.Open(Path.Combine(_dir, Guid.NewGuid() + ".db"));
        store.Record(id.Get, "Verified");
        return store.StageOf(id.Get) == "Verified";
    }
}
