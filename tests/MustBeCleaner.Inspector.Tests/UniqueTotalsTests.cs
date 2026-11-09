using MustBeCleaner.Domain;
using MustBeCleaner.Inspector;
using MustBeCleaner.TestKit.Sandboxing;

namespace MustBeCleaner.Inspector.Tests;

/// <summary>Identity capture and unique totals (R3.5, R3.7, R4.6, R7.6). Sandbox fixtures only (R21.2).</summary>
public class UniqueTotalsTests
{
    private static ScopeEntries Scope(string name, ScanResult r) => new(name, r.Entries);

    private static InspectedEntry NoIdentityFile(string path)
        => new(path, EntryKind.File, ReparseKind.None, null, null, false, null);

    [Fact]
    public void Captured_identity_has_volume_128bit_file_id_canonical_path_size_mtime_attributes()
    {
        using var sb = new Sandbox();
        var file = sb.WriteFile(@"a\one.txt", "1234567");

        var e = new StorageInspector().Scan([sb.Root]).Entries.Single(x => x.Kind == EntryKind.File);

        var id = e.Identity!;
        Assert.Equal(16, id.VolumeId.Length);
        Assert.Equal(32, id.FileId.Length);
        Assert.Equal(PathPolicy.Normalize(file).Path!.Value.Value, id.CanonicalPath);
        Assert.Equal(7, id.Size);
        Assert.True(id.ModifiedUtc > DateTime.UtcNow.AddHours(-1));
        Assert.Equal(DateTimeKind.Utc, id.ModifiedUtc.Kind);
        Assert.False(id.Attributes.HasFlag(FileAttributes.Directory));
    }

    [Fact]
    public void Hard_linked_file_is_counted_once()
    {
        using var sb = new Sandbox();
        sb.WriteFile(@"l\orig.bin", "payload"); // 7 bytes
        Assert.True(sb.TryCreateHardLink(@"l\hard.bin", @"l\orig.bin"));

        var report = UniqueTotals.Compute([Scope("s", new StorageInspector().Scan([sb.Root]))]);

        Assert.Equal(1, report.Total.UniqueFiles);
        Assert.Equal(7, report.Total.KnownBytes);
        Assert.Equal(1, report.Total.HardLinkAliasesCollapsed);
    }

    [Fact]
    public void Parent_and_child_scopes_do_not_double_count()
    {
        using var sb = new Sandbox();
        sb.WriteFile(@"p\c\x.bin", "12345");
        sb.WriteFile(@"p\y.bin", "123");
        var inspector = new StorageInspector();
        var parent = inspector.Scan([sb.Resolve("p")]);
        var child = inspector.Scan([sb.Resolve(@"p\c")]);

        var report = UniqueTotals.Compute([Scope("parent", parent), Scope("child", child)]);

        Assert.Equal(2, report.Total.UniqueFiles);
        Assert.Equal(8, report.Total.KnownBytes);
        Assert.Equal(2, report.PerScope["parent"].UniqueFiles);
        Assert.Equal(1, report.PerScope["child"].UniqueFiles);
    }

    [Fact]
    public void Same_file_in_two_scopes_is_counted_once_in_total()
    {
        using var sb = new Sandbox();
        sb.WriteFile(@"a\same.bin", "1234");
        var inspector = new StorageInspector();
        var s1 = inspector.Scan([sb.Resolve("a")]);
        var s2 = inspector.Scan([sb.Resolve("a")]);

        var report = UniqueTotals.Compute([Scope("one", s1), Scope("two", s2)]);

        Assert.Equal(1, report.Total.UniqueFiles);
        Assert.Equal(4, report.Total.KnownBytes);
    }

    [Fact]
    public void Hard_links_in_different_scopes_count_once_in_total()
    {
        using var sb = new Sandbox();
        sb.WriteFile(@"a\orig.bin", "123456");
        Assert.True(sb.TryCreateHardLink(@"b\link.bin", @"a\orig.bin"));
        var inspector = new StorageInspector();

        var report = UniqueTotals.Compute([
            Scope("a", inspector.Scan([sb.Resolve("a")])),
            Scope("b", inspector.Scan([sb.Resolve("b")]))]);

        Assert.Equal(1, report.Total.UniqueFiles);
        Assert.Equal(6, report.Total.KnownBytes);
        Assert.Equal(1, report.PerScope["a"].UniqueFiles);
        Assert.Equal(1, report.PerScope["b"].UniqueFiles);
    }

    [Fact]
    public void Unavailable_size_is_null_not_zero_and_marks_a_lower_bound()
    {
        var report = UniqueTotals.Compute([new ScopeEntries("s", [NoIdentityFile(@"C:\SANDBOX\LOCKED.BIN")])]);

        Assert.Equal(1, report.Total.UniqueFiles);
        Assert.Equal(1, report.Total.UnavailableSize);
        Assert.Null(report.Total.KnownBytes);
        Assert.True(report.Total.IsLowerBound);
    }

    [Fact]
    public void Unavailable_size_does_not_hide_known_sizes()
    {
        using var sb = new Sandbox();
        sb.WriteFile(@"a\k.bin", "123");
        var scanned = new StorageInspector().Scan([sb.Root]);
        var entries = scanned.Entries.Concat([NoIdentityFile(@"C:\SANDBOX\LOCKED.BIN")]).ToList();

        var report = UniqueTotals.Compute([new ScopeEntries("s", entries)]);

        Assert.Equal(2, report.Total.UniqueFiles);
        Assert.Equal(3, report.Total.KnownBytes);
        Assert.Equal(1, report.Total.UnavailableSize);
        Assert.True(report.Total.IsLowerBound);
    }

    [Fact]
    public void Identity_less_entry_seen_in_two_scopes_is_counted_once_by_path()
    {
        var e = NoIdentityFile(@"C:\SANDBOX\LOCKED.BIN");
        var report = UniqueTotals.Compute([new ScopeEntries("a", [e]), new ScopeEntries("b", [e])]);
        Assert.Equal(1, report.Total.UniqueFiles);
    }

    [Fact]
    public void Empty_input_is_a_true_zero_not_unavailable()
    {
        var report = UniqueTotals.Compute([]);
        Assert.Equal(0, report.Total.UniqueFiles);
        Assert.Equal(0, report.Total.KnownBytes);
        Assert.False(report.Total.IsLowerBound);
    }

    [Fact]
    public void Directories_and_reparse_points_add_no_bytes()
    {
        using var sb = new Sandbox();
        sb.WriteFile(@"t\inner.txt", "xx");
        Assert.True(sb.TryCreateJunction(@"links\j", "t"));

        var report = UniqueTotals.Compute([Scope("s", new StorageInspector().Scan([sb.Root]))]);

        Assert.Equal(1, report.Total.UniqueFiles);
        Assert.Equal(2, report.Total.KnownBytes);
    }
}
