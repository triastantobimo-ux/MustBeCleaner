using IdentityBinding;
namespace IdentityBinding.Tests;

public class IdentityCaptureTests : IDisposable
{
    private readonly Sandbox _sb = new();
    public void Dispose() => _sb.Dispose();

    [Fact]
    public void Same_file_captured_twice_is_the_same_object()
    {
        var f = _sb.WriteFile("a.txt", "hello");
        Assert.True(Identity.Capture(f).SameObjectAs(Identity.Capture(f)));
    }

    [Fact]
    public void Different_files_are_different_objects()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        var b = _sb.WriteFile("b.txt", "hello");
        Assert.False(Identity.Capture(a).SameObjectAs(Identity.Capture(b)));
    }

    [Fact]
    public void File_id_is_128_bit_capable_and_nonzero()
    {
        var id = Identity.Capture(_sb.WriteFile("a.txt", "x"));
        Assert.NotEqual(0UL, id.VolumeSerial);
        Assert.True(id.FileIdLow != 0 || id.FileIdHigh != 0);
    }

    [Fact]
    public void Hard_link_alias_resolves_to_the_same_object_and_reports_link_count_two()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        var alias = _sb.Path_("alias.txt");
        Assert.True(Native2.CreateHardLink(alias, a, IntPtr.Zero));
        var ia = Identity.Capture(a);
        Assert.True(ia.SameObjectAs(Identity.Capture(alias)));
        Assert.Equal(2u, ia.LinkCount);
    }

    [Fact]
    public void Hard_link_names_enumerate_every_alias()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        Assert.True(Native2.CreateHardLink(_sb.Path_("alias.txt"), a, IntPtr.Zero));
        var names = Identity.HardLinkNames(a);
        Assert.Equal(2, names.Count);
        Assert.Contains(names, n => n.EndsWith(@"\a.txt", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(names, n => n.EndsWith(@"\alias.txt", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rename_after_preview_keeps_identity_but_path_lookup_misses()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        var previewed = Identity.Capture(a);
        File.Move(a, _sb.Path_("renamed.txt"));
        Assert.False(File.Exists(a));
        Assert.True(previewed.SameObjectAs(Identity.Capture(_sb.Path_("renamed.txt"))));
    }

    [Fact]
    public void Held_handle_final_path_follows_a_rename()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        using var h = Identity.Open(a);
        File.Move(a, _sb.Path_("renamed.txt"));
        Assert.EndsWith("renamed.txt", Identity.FinalPath(h), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Open_by_id_reaches_the_same_object_after_rename()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        var previewed = Identity.Capture(a);
        File.Move(a, _sb.Path_("renamed.txt"));
        using var hint = Identity.Open(_sb.Root);
        using var byId = Identity.OpenById(hint, previewed);
        Assert.True(previewed.SameObjectAs(Identity.Capture(byId)));
        Assert.EndsWith("renamed.txt", Identity.FinalPath(byId), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Delete_and_recreate_at_same_path_is_detected_as_a_different_object()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        var previewed = Identity.Capture(a);
        File.Delete(a);
        File.WriteAllText(a, "hello");
        Assert.False(previewed.SameObjectAs(Identity.Capture(a)));
    }

    [Fact]
    public void Atomic_replace_with_another_file_is_detected_as_a_different_object()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        var tmp = _sb.WriteFile("tmp.txt", "hello");
        var previewed = Identity.Capture(a);
        File.Replace(tmp, a, null);
        Assert.False(previewed.SameObjectAs(Identity.Capture(a)));
    }

    [Fact]
    public void In_place_modification_keeps_object_identity_but_metadata_check_catches_it()
    {
        var a = _sb.WriteFile("a.txt", "hello");
        var previewed = Identity.Capture(a);
        File.WriteAllText(a, "hello world, changed");
        var now = Identity.Capture(a);
        Assert.True(previewed.SameObjectAs(now));
        Assert.False(previewed.SameObjectAndMetadata(now));
    }

    [Fact]
    public void Same_size_same_mtime_overwrite_is_not_detected_by_metadata()
    {
        // Documents a limit: identity + size + mtime cannot see a content change that restores mtime.
        var a = _sb.WriteFile("a.txt", "hello");
        var mtime = File.GetLastWriteTimeUtc(a);
        var previewed = Identity.Capture(a);
        File.WriteAllText(a, "HELLO");
        File.SetLastWriteTimeUtc(a, mtime);
        Assert.True(previewed.SameObjectAndMetadata(Identity.Capture(a)));
    }
}

internal static class Native2
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    public static extern bool CreateHardLink(string newName, string existing, IntPtr sec);
}
