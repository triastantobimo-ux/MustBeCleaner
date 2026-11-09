using MustBeCleaner.Infrastructure.Journal;
using MustBeCleaner.TestKit.Sandboxing;

namespace MustBeCleaner.Infrastructure.Tests;

/// <summary>Data_Directory fail-closed behavior (R12.9, R12.10, R16.5). Temp sandboxes only.</summary>
public sealed class DataDirectoryTests : IDisposable
{
    private readonly Sandbox _sb = new();
    public void Dispose() => _sb.Dispose();

    [Fact]
    public void Open_creates_database_in_existing_directory_with_wal()
    {
        var dir = _sb.CreateDirectory("data");
        using var store = JournalStore.Open(new JournalStoreOptions(dir));
        Assert.True(File.Exists(Path.Combine(dir, JournalStore.DatabaseFileName)));
        Assert.Equal(JournalStore.CurrentSchemaVersion, store.SchemaVersion);
    }

    [Fact]
    public void Open_refuses_missing_directory_and_does_not_create_it()
    {
        var dir = _sb.Resolve("missing");
        var ex = Assert.Throws<StorageUnavailableException>(() => JournalStore.Open(new JournalStoreOptions(dir)));
        Assert.Equal(StorageFailure.Missing, ex.Failure);
        Assert.Equal(StorageLocation.DataDirectory, ex.Location);
        Assert.False(Directory.Exists(dir));
    }

    [Fact]
    public void Open_refuses_relative_path()
    {
        var ex = Assert.Throws<StorageUnavailableException>(() => JournalStore.Open(new JournalStoreOptions("data")));
        Assert.Equal(StorageFailure.NotAbsolute, ex.Failure);
    }

    [Fact]
    public void Open_refuses_when_path_is_a_file()
    {
        var file = _sb.WriteFile("data", "x");
        var ex = Assert.Throws<StorageUnavailableException>(() => JournalStore.Open(new JournalStoreOptions(file)));
        Assert.Equal(StorageFailure.Missing, ex.Failure);
    }

    [Fact]
    public void Open_refuses_when_free_space_below_minimum()
    {
        var dir = _sb.CreateDirectory("data");
        var options = new JournalStoreOptions(dir) { MinimumFreeBytes = long.MaxValue };
        var ex = Assert.Throws<StorageUnavailableException>(() => JournalStore.Open(options));
        Assert.Equal(StorageFailure.InsufficientSpace, ex.Failure);
        Assert.False(File.Exists(Path.Combine(dir, JournalStore.DatabaseFileName)));
    }

    [Fact]
    public void Open_refuses_unwritable_database()
    {
        var dir = _sb.CreateDirectory("data");
        var db = Path.Combine(dir, JournalStore.DatabaseFileName);
        File.WriteAllBytes(db, []);
        File.SetAttributes(db, FileAttributes.ReadOnly);
        var ex = Assert.Throws<StorageUnavailableException>(() => JournalStore.Open(new JournalStoreOptions(dir)));
        Assert.Equal(StorageFailure.Unwritable, ex.Failure);
    }

    [Fact]
    public void Open_refuses_missing_quarantine_directory()
    {
        var dir = _sb.CreateDirectory("data");
        var options = new JournalStoreOptions(dir) { QuarantineDirectory = _sb.Resolve("quarantine") };
        var ex = Assert.Throws<StorageUnavailableException>(() => JournalStore.Open(options));
        Assert.Equal(StorageLocation.Quarantine, ex.Location);
        Assert.Equal(StorageFailure.Missing, ex.Failure);
        Assert.False(Directory.Exists(_sb.Resolve("quarantine")));
    }

    [Fact]
    public void Open_refuses_database_from_newer_schema()
    {
        var dir = _sb.CreateDirectory("data");
        using (var store = JournalStore.Open(new JournalStoreOptions(dir))) { }
        RawSql.Execute(dir, $"PRAGMA user_version = {JournalStore.CurrentSchemaVersion + 1}");
        var ex = Assert.Throws<StorageUnavailableException>(() => JournalStore.Open(new JournalStoreOptions(dir)));
        Assert.Equal(StorageFailure.SchemaNewer, ex.Failure);
    }

    [Fact]
    public void Open_refuses_corrupt_database()
    {
        var dir = _sb.CreateDirectory("data");
        File.WriteAllText(Path.Combine(dir, JournalStore.DatabaseFileName), new string('x', 4096));
        var ex = Assert.Throws<StorageUnavailableException>(() => JournalStore.Open(new JournalStoreOptions(dir)));
        Assert.Equal(StorageFailure.Corrupt, ex.Failure);
    }

    [Fact]
    public void Data_directory_and_quarantine_are_exclusion_roots()
    {
        var dir = _sb.CreateDirectory("data");
        var quarantine = _sb.CreateDirectory("quarantine");
        using var store = JournalStore.Open(new JournalStoreOptions(dir) { QuarantineDirectory = quarantine });
        Assert.Equal([dir, quarantine], store.ExclusionRoots);
        Assert.True(store.IsExcluded(dir));
        Assert.True(store.IsExcluded(Path.Combine(quarantine, "a", "b.txt").ToUpperInvariant()));
        Assert.False(store.IsExcluded(dir + "-sibling" + Path.DirectorySeparatorChar + "x"));
        Assert.False(store.IsExcluded(_sb.Resolve("other\\file.txt")));
    }

    [Fact]
    public void Exclusion_roots_without_quarantine_contain_only_data_directory()
    {
        var dir = _sb.CreateDirectory("data");
        using var store = JournalStore.Open(new JournalStoreOptions(dir));
        Assert.Equal([dir], store.ExclusionRoots);
    }
}
