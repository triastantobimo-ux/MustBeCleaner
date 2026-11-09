using System.Security.Cryptography;
using IdentityBinding;
namespace IdentityBinding.Tests;

public class HandleHoldTests : IDisposable
{
    private readonly Sandbox _sb = new();
    public void Dispose() => _sb.Dispose();

    private static FileStream HoldRead(string p) => new(p, FileMode.Open, FileAccess.Read, FileShare.Read);

    [Fact]
    public void Held_handle_without_share_write_or_delete_blocks_writers()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        using var hold = HoldRead(a);
        Assert.Throws<IOException>(() => File.WriteAllText(a, "other"));
    }

    [Fact]
    public void Held_handle_blocks_delete()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        using var hold = HoldRead(a);
        Assert.Throws<IOException>(() => File.Delete(a));
    }

    [Fact]
    public void Held_handle_blocks_rename()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        using var hold = HoldRead(a);
        Assert.Throws<IOException>(() => File.Move(a, _sb.Path_("b.txt")));
    }

    [Fact]
    public void Held_handle_blocks_atomic_replace_over_it()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        var tmp = _sb.WriteFile("tmp.txt", "hello");
        using var hold = HoldRead(a);
        Assert.ThrowsAny<IOException>(() => File.Replace(tmp, a, null));
    }

    [Fact]
    public void Held_handle_still_allows_other_readers()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        using var hold = HoldRead(a);
        using var other = new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.Equal(5, other.Length);
    }

    [Fact]
    public void Hold_cannot_be_taken_when_a_writer_is_already_active()
    {
        // The hold doubles as an activity probe: it fails if a writer is open.
        var a = _sb.WriteFile("a.txt", "hello");
        using var writer = new FileStream(a, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        Assert.Throws<IOException>(() => HoldRead(a));
    }

    [Fact]
    public void Copy_verify_release_with_hold_yields_identical_copy_and_unchanged_source()
    {
        var a = _sb.WriteFile("a.txt", new string('z', 100_000));
        var dst = _sb.Path_(@"q\a.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(dst)!);

        var before = Identity.Capture(a);
        using (var hold = HoldRead(a))
        {
            using (var d = new FileStream(dst, FileMode.CreateNew, FileAccess.Write)) hold.CopyTo(d);
            hold.Position = 0;
            var srcHash = SHA256.HashData(hold);
            var dstHash = SHA256.HashData(File.ReadAllBytes(dst));
            Assert.Equal(srcHash, dstHash);
            Assert.Throws<IOException>(() => File.WriteAllText(a, "tamper"));
            Assert.True(before.SameObjectAndMetadata(Identity.Capture(hold.SafeFileHandle)));
        }
        File.Delete(a); // after release, source release is possible
        Assert.False(File.Exists(a));
    }

    [Fact]
    public void Hold_is_released_on_dispose_and_writers_then_succeed()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        var hold = HoldRead(a);
        hold.Dispose();
        File.WriteAllText(a, "after");
        Assert.Equal("after", File.ReadAllText(a));
    }

    [Fact]
    public void Writer_that_opened_before_the_hold_with_share_read_write_is_not_blocked_if_hold_allows_it()
    {
        // Limit: a hold that grants FILE_SHARE_WRITE does NOT stop writers. Only the share mask matters.
        var a = _sb.WriteFile("a.txt", "hello");
        using var weak = new FileStream(a, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        File.WriteAllText(a, "changed"); // succeeds: weak hold gives no protection
        Assert.Equal("changed", File.ReadAllText(a));
    }
}
