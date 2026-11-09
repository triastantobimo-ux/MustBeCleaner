using System.Security.AccessControl;
using System.Security.Principal;
using MustBeCleaner.Inspector;
using MustBeCleaner.TestKit.Sandboxing;

namespace MustBeCleaner.Inspector.Tests;

/// <summary>Public-interface tests. Every scan runs on a Sandbox, never on real user data (R21.2).</summary>
public class StorageInspectorTests
{
    private sealed class FixedHook(string root) : IProtectionHook
    {
        public ProtectionVerdict Evaluate(string fullPath) =>
            fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? new(true, "test.rule") : new(false, null);
    }

    private static InspectedEntry Find(ScanResult r, string path) =>
        r.Entries.Single(e => string.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));

    [Fact]
    public void Enumerates_files_and_directories_with_identity_and_size()
    {
        using var sb = new Sandbox();
        var file = sb.WriteFile(@"a\one.txt", "1234567");
        var dir = sb.CreateDirectory(@"a\empty");

        var r = new StorageInspector().Scan([sb.Root]);

        var f = Find(r, file);
        Assert.Equal(EntryKind.File, f.Kind);
        Assert.Equal(7, f.Identity!.Size);
        Assert.False(string.IsNullOrEmpty(f.Identity.FileId));
        Assert.Equal(EntryKind.Directory, Find(r, dir).Kind);
        Assert.Equal(CoverageStatus.Complete, r.Coverage.Single().Status);
        Assert.True(r.IsComplete);
    }

    [Fact]
    public void Hard_links_share_object_identity_and_report_link_count()
    {
        using var sb = new Sandbox();
        var orig = sb.WriteFile(@"l\orig.bin", "payload");
        Assert.True(sb.TryCreateHardLink(@"l\hard.bin", @"l\orig.bin"));

        var r = new StorageInspector().Scan([sb.Root]);

        var a = Find(r, orig);
        var b = Find(r, sb.Resolve(@"l\hard.bin"));
        Assert.True(a.Identity!.IsSameObjectAs(b.Identity!));
        Assert.Equal(2u, a.LinkCount);
    }

    [Fact]
    public void Junction_is_recorded_but_never_traversed()
    {
        using var sb = new Sandbox();
        var inner = sb.WriteFile(@"target\inner.txt", "x");
        Assert.True(sb.TryCreateJunction(@"links\junction", "target"));
        var junction = sb.Resolve(@"links\junction");

        var r = new StorageInspector().Scan([sb.Root]);

        var j = Find(r, junction);
        Assert.Equal(EntryKind.ReparsePoint, j.Kind);
        Assert.Equal(ReparseKind.JunctionOrMountPoint, j.Reparse);
        Assert.DoesNotContain(r.Entries, e => e.Path.StartsWith(junction + "\\", StringComparison.OrdinalIgnoreCase));
        Assert.Single(r.Entries, e => e.Path.EndsWith("inner.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(1, r.Coverage.Single().ReparseSkipped);
        Assert.NotNull(Find(r, inner));
    }

    [Fact]
    public void Symlink_directory_is_not_traversed_when_it_can_be_created()
    {
        using var sb = new Sandbox();
        sb.WriteFile(@"target\inner.txt", "x");
        var link = sb.Resolve("symlink");
        try { Directory.CreateSymbolicLink(link, sb.Resolve("target")); }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException) { return; } // no privilege: UNVERIFIED path

        var r = new StorageInspector().Scan([sb.Root]);

        Assert.Equal(ReparseKind.SymbolicLink, Find(r, link).Reparse);
        Assert.DoesNotContain(r.Entries, e => e.Path.StartsWith(link + "\\", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(0xA0000003u, ReparseKind.JunctionOrMountPoint)]
    [InlineData(0xA000000Cu, ReparseKind.SymbolicLink)]
    [InlineData(0x9000001Au, ReparseKind.Cloud)]
    [InlineData(0x9000701Au, ReparseKind.Cloud)]
    [InlineData(0x8000001Bu, ReparseKind.Unclassified)]
    [InlineData(0x12345678u, ReparseKind.Unclassified)]
    [InlineData(0u, ReparseKind.Unclassified)]
    public void Reparse_tags_are_classified_and_unknown_tags_stay_unclassified(uint tag, ReparseKind expected)
        => Assert.Equal(expected, ReparseClassifier.Classify(tag));

    [Fact]
    public void Protection_hook_marks_matching_entries_only()
    {
        using var sb = new Sandbox();
        var prot = sb.WriteFile(@"prot\x.dll", "x");
        var free = sb.WriteFile(@"free\y.txt", "y");

        var r = new StorageInspector(new FixedHook(sb.Resolve("prot"))).Scan([sb.Root]);

        Assert.True(Find(r, prot).IsProtected);
        Assert.Equal("test.rule", Find(r, prot).ProtectionRuleId);
        Assert.False(Find(r, free).IsProtected);
    }

    [Fact]
    public void Default_hook_protects_windows_roots_on_segment_boundaries()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var hook = WindowsCriticalRoots.Default();

        Assert.True(hook.Evaluate(windows).IsProtected);
        Assert.True(hook.Evaluate(Path.Combine(windows, "System32", "kernel32.dll").ToUpperInvariant()).IsProtected);
        Assert.False(hook.Evaluate(windows + "Old\\x.txt").IsProtected);
        Assert.False(hook.Evaluate(Path.Combine(Path.GetTempPath(), "x.txt")).IsProtected);
    }

    [Fact]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void Access_denied_is_recorded_and_the_scan_continues()
    {
        using var sb = new Sandbox();
        var locked = sb.CreateDirectory("locked");
        sb.WriteFile(@"locked\hidden.txt", "h");
        var sibling = sb.WriteFile(@"open\ok.txt", "ok");
        var me = WindowsIdentity.GetCurrent().User!;
        var deny = new FileSystemAccessRule(me, FileSystemRights.ListDirectory, AccessControlType.Deny);
        var info = new DirectoryInfo(locked);
        var acl = info.GetAccessControl();
        acl.AddAccessRule(deny);
        info.SetAccessControl(acl);
        try
        {
            var r = new StorageInspector().Scan([sb.Root]);

            Assert.Contains(r.AccessDenied, a => string.Equals(a.Path, locked, StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(Find(r, sibling));
            Assert.DoesNotContain(r.Entries, e => e.Path.EndsWith("hidden.txt", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(CoverageStatus.Partial, r.Coverage.Single().Status);
            Assert.Equal(1, r.Coverage.Single().AccessDenied);
            Assert.False(r.IsComplete);
        }
        finally
        {
            var restore = info.GetAccessControl();
            restore.RemoveAccessRule(deny);
            info.SetAccessControl(restore);
        }
    }

    [Theory]
    [InlineData(@"relative\path")]
    [InlineData(@"\\server\share\x")]
    [InlineData(@"\\?\C:\x")]
    [InlineData(@"C:\x:stream")]
    [InlineData("")]
    public void Unsafe_roots_are_rejected_without_enumeration(string root)
    {
        var r = new StorageInspector().Scan([root]);

        Assert.Empty(r.Entries);
        Assert.Equal(CoverageStatus.Rejected, r.Coverage.Single().Status);
    }

    [Fact]
    public void Missing_root_is_reported_not_found()
    {
        using var sb = new Sandbox();
        var r = new StorageInspector().Scan([Path.Combine(sb.Root, "nope")]);
        Assert.Equal(CoverageStatus.NotFound, r.Coverage.Single().Status);
    }

    [Fact]
    public void Cancelled_scan_is_labelled_incomplete()
    {
        using var sb = new Sandbox();
        sb.WriteFile(@"a\b.txt", "b");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var r = new StorageInspector().Scan([sb.Root], cts.Token);

        Assert.Equal(CoverageStatus.Cancelled, r.Coverage.Single().Status);
        Assert.False(r.IsComplete);
    }
}
