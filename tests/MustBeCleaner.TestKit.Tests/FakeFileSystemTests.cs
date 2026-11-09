using System.Text;
using MustBeCleaner.TestKit.Fakes;
using Xunit;

namespace MustBeCleaner.TestKit.Tests;

public class FakeFileSystemTests
{
    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    private static FakeFileSystem WithSource()
    {
        var fs = new FakeFileSystem();
        fs.CreateDirectory(@"C:\src");
        fs.CreateDirectory(@"D:\quarantine");
        fs.WriteFile(@"C:\src\a.txt", Bytes("hello"));
        return fs;
    }

    [Fact]
    public void Written_file_can_be_read_back_with_case_insensitive_paths()
    {
        var fs = WithSource();
        Assert.Equal(Bytes("hello"), fs.ReadFile(@"c:\SRC\A.TXT"));
        Assert.True(fs.FileExists(@"C:/src/a.txt"));
        Assert.True(fs.DirectoryExists(@"C:\src"));
    }

    [Fact]
    public void Writing_into_a_missing_folder_fails_with_DirectoryNotFound()
    {
        var fs = new FakeFileSystem();
        Assert.Throws<DirectoryNotFoundException>(() => fs.WriteFile(@"C:\nope\a.txt", Bytes("x")));
    }

    [Fact]
    public void Copy_leaves_source_and_creates_identical_destination()
    {
        var fs = WithSource();
        fs.Copy(@"C:\src\a.txt", @"D:\quarantine\a.txt");
        Assert.Equal(Bytes("hello"), fs.ReadFile(@"D:\quarantine\a.txt"));
        Assert.True(fs.FileExists(@"C:\src\a.txt"));
    }

    [Fact]
    public void Move_and_Delete_remove_the_source()
    {
        var fs = WithSource();
        fs.Move(@"C:\src\a.txt", @"D:\quarantine\a.txt");
        Assert.False(fs.FileExists(@"C:\src\a.txt"));
        fs.Delete(@"D:\quarantine\a.txt");
        Assert.False(fs.FileExists(@"D:\quarantine\a.txt"));
    }

    [Fact]
    public void Copy_never_overwrites_an_existing_destination()
    {
        var fs = WithSource();
        fs.WriteFile(@"D:\quarantine\a.txt", Bytes("other"));
        Assert.Throws<IOException>(() => fs.Copy(@"C:\src\a.txt", @"D:\quarantine\a.txt"));
        Assert.Equal(Bytes("other"), fs.ReadFile(@"D:\quarantine\a.txt"));
    }

    [Fact]
    public void CrashAt_throws_SimulatedCrash_only_when_the_named_checkpoint_is_reached()
    {
        var fs = WithSource();
        fs.Inject(Fault.CrashAt("Copied"));
        fs.Checkpoint("Prepared");
        var crash = Assert.Throws<SimulatedCrashException>(() => fs.Checkpoint("Copied"));
        Assert.Equal("Copied", crash.Step);
    }

    [Fact]
    public void Crash_keeps_whatever_was_already_written()
    {
        var fs = WithSource();
        fs.Copy(@"C:\src\a.txt", @"D:\quarantine\a.txt");
        fs.Inject(Fault.CrashAt("Verified"));
        Assert.Throws<SimulatedCrashException>(() => fs.Checkpoint("Verified"));
        Assert.True(fs.FileExists(@"C:\src\a.txt"));
        Assert.True(fs.FileExists(@"D:\quarantine\a.txt"));
    }

    [Fact]
    public void DiskFull_rejects_writes_that_exceed_the_volume_capacity_and_leaves_no_partial_file()
    {
        var fs = WithSource();
        fs.Inject(Fault.DiskFull("D:", capacityBytes: 3));
        var ex = Assert.Throws<IOException>(() => fs.Copy(@"C:\src\a.txt", @"D:\quarantine\a.txt"));
        Assert.Contains("not enough space", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(fs.FileExists(@"D:\quarantine\a.txt"));
        Assert.True(fs.FileExists(@"C:\src\a.txt"));
    }

    [Fact]
    public void Locked_file_rejects_read_copy_move_and_delete_but_still_exists()
    {
        var fs = WithSource();
        fs.Inject(Fault.Locked(@"C:\src\a.txt"));
        Assert.Throws<IOException>(() => fs.ReadFile(@"C:\src\a.txt"));
        Assert.Throws<IOException>(() => fs.Copy(@"C:\src\a.txt", @"D:\quarantine\a.txt"));
        Assert.Throws<IOException>(() => fs.Move(@"C:\src\a.txt", @"D:\quarantine\a.txt"));
        Assert.Throws<IOException>(() => fs.Delete(@"C:\src\a.txt"));
        Assert.True(fs.FileExists(@"C:\src\a.txt"));
    }

    [Fact]
    public void PermissionDenied_applies_to_the_path_and_everything_beneath_it()
    {
        var fs = WithSource();
        fs.Inject(Fault.PermissionDenied(@"C:\src"));
        Assert.Throws<UnauthorizedAccessException>(() => fs.ReadFile(@"C:\src\a.txt"));
        Assert.Throws<UnauthorizedAccessException>(() => fs.Delete(@"C:\src\a.txt"));
        Assert.True(fs.FileExists(@"C:\src\a.txt"));
    }

    [Fact]
    public void SourceModifiedDuringCopy_copies_the_old_bytes_then_changes_the_source()
    {
        var fs = WithSource();
        fs.Inject(Fault.SourceModifiedDuringCopy(@"C:\src\a.txt", Bytes("changed!")));
        fs.Copy(@"C:\src\a.txt", @"D:\quarantine\a.txt");

        Assert.Equal(Bytes("hello"), fs.ReadFile(@"D:\quarantine\a.txt"));
        Assert.Equal(Bytes("changed!"), fs.ReadFile(@"C:\src\a.txt"));
    }

    [Fact]
    public void DestinationMissing_makes_the_destination_volume_vanish()
    {
        var fs = WithSource();
        fs.Inject(Fault.DestinationMissing("D:"));
        Assert.Throws<DirectoryNotFoundException>(() => fs.Copy(@"C:\src\a.txt", @"D:\quarantine\a.txt"));
        Assert.False(fs.DirectoryExists(@"D:\quarantine"));
        Assert.True(fs.FileExists(@"C:\src\a.txt"));
    }

    [Fact]
    public void Snapshot_reports_all_files_so_tests_can_assert_that_one_intact_copy_survives()
    {
        var fs = WithSource();
        fs.Copy(@"C:\src\a.txt", @"D:\quarantine\a.txt");
        var files = fs.Snapshot();
        Assert.Equal(2, files.Count);
        Assert.Equal(Bytes("hello"), files[@"D:\quarantine\a.txt"]);
    }
}
