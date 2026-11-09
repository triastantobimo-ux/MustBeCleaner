using IdentityBinding;
namespace IdentityBinding.Tests;

public class AncestorChainTests : IDisposable
{
    private readonly Sandbox _sb = new();
    public void Dispose() => _sb.Dispose();

    [Fact]
    public void Plain_directory_chain_has_no_reparse_points()
    {
        var f = _sb.WriteFile(@"d1\d2\f.txt", "x");
        var chain = Ancestors.CaptureChain(f);
        Assert.All(chain.Skip(1), e => Assert.False(e.Identity.IsReparsePoint));
        Assert.EndsWith("f.txt", chain[^1].Path);
    }

    [Fact]
    public void Junction_as_ancestor_is_rejected_at_capture()
    {
        _sb.WriteFile(@"real\f.txt", "x");
        Junction.Create(_sb.Path_("link"), _sb.Path_("real"));
        var ex = Assert.Throws<ReparsePointInChainException>(() => Ancestors.CaptureChain(_sb.Path_(@"link\f.txt")));
        Assert.EndsWith("link", ex.At);
    }

    [Fact]
    public void Junction_swap_after_preview_is_detected_by_chain_recheck()
    {
        var f = _sb.WriteFile(@"d\sub\f.txt", "x");
        _sb.WriteFile(@"other\f.txt", "x");
        var chain = Ancestors.CaptureChain(f);
        Assert.True(Ancestors.StillMatches(chain));

        Directory.Move(_sb.Path_(@"d\sub"), _sb.Path_(@"d\sub_old"));
        Junction.Create(_sb.Path_(@"d\sub"), _sb.Path_("other"));

        Assert.False(Ancestors.StillMatches(chain));
    }

    [Fact]
    public void Directory_replaced_by_a_real_directory_after_preview_is_detected_by_chain_recheck()
    {
        var f = _sb.WriteFile(@"d\sub\f.txt", "x");
        var chain = Ancestors.CaptureChain(f);
        Directory.Move(_sb.Path_(@"d\sub"), _sb.Path_(@"d\sub_old"));
        _sb.WriteFile(@"d\sub\f.txt", "x");
        Assert.False(Ancestors.StillMatches(chain));
    }

    [Fact]
    public void Open_through_a_swapped_junction_is_caught_by_final_path_comparison()
    {
        // Post-open check: the handle actually opened must resolve to the path we expected.
        var f = _sb.WriteFile(@"d\sub\f.txt", "x");
        _sb.WriteFile(@"other\f.txt", "x");
        Directory.Move(_sb.Path_(@"d\sub"), _sb.Path_(@"d\sub_old"));
        Junction.Create(_sb.Path_(@"d\sub"), _sb.Path_("other"));

        using var h = Identity.Open(f);
        var final = Identity.FinalPath(h);
        Assert.False(string.Equals(final, f, StringComparison.OrdinalIgnoreCase));
        Assert.EndsWith(@"other\f.txt", final, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Junction_swap_is_visible_in_file_identity_when_target_differs()
    {
        var f = _sb.WriteFile(@"d\sub\f.txt", "x");
        _sb.WriteFile(@"other\f.txt", "x");
        var previewed = Identity.Capture(f);
        Directory.Move(_sb.Path_(@"d\sub"), _sb.Path_(@"d\sub_old"));
        Junction.Create(_sb.Path_(@"d\sub"), _sb.Path_("other"));
        Assert.False(previewed.SameObjectAs(Identity.Capture(f)));
    }

    [Fact]
    public void Directory_handle_without_share_delete_blocks_renaming_that_directory()
    {
        var f = _sb.WriteFile(@"d\sub\f.txt", "x");
        using var pin = Identity.Open(_sb.Path_(@"d\sub"), 1u | 2u, Native.GENERIC_READ);
        Assert.ThrowsAny<IOException>(() => Directory.Move(_sb.Path_(@"d\sub"), _sb.Path_(@"d\sub_old")));
        Assert.True(File.Exists(f));
    }

    [Fact]
    public void Pinning_a_directory_does_not_stop_its_children_from_being_swapped()
    {
        // Limit: the pin protects the pinned directory's own name, not its descendants.
        var f = _sb.WriteFile(@"d\sub\f.txt", "x");
        using var pin = Identity.Open(_sb.Path_("d"), 1u | 2u, Native.GENERIC_READ);
        Directory.Move(_sb.Path_(@"d\sub"), _sb.Path_(@"d\sub_old")); // must not throw
        Assert.False(File.Exists(f));
    }
}



