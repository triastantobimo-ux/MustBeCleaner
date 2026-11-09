namespace MustBeCleaner.Domain.Tests;

public class PathPolicyTests
{
    private static string Canon(string raw)
    {
        var result = PathPolicy.Normalize(raw);
        Assert.True(result.IsCanonical, $"expected canonical, got {result.Reason}");
        return result.Path!.Value.Value;
    }

    private static PathRejection Rejected(string raw)
    {
        var result = PathPolicy.Normalize(raw);
        Assert.False(result.IsCanonical);
        return result.Reason!.Value;
    }

    // R7.5: case variants and separator variants collapse to one canonical form.
    [Theory]
    [InlineData(@"C:\Users\Mika\AppData", @"C:\USERS\MIKA\APPDATA")]
    [InlineData(@"c:\users\mika\appdata", @"C:\USERS\MIKA\APPDATA")]
    [InlineData("c:/Users//Mika/AppData/", @"C:\USERS\MIKA\APPDATA")]
    [InlineData(@"C:\", @"C:\")]
    public void Case_and_separator_variants_share_one_canonical_form(string raw, string expected)
    {
        Assert.Equal(expected, Canon(raw));
    }

    // R7.4: UNC, device paths and alternate data streams are refused.
    [Theory]
    [InlineData(@"\\server\share\dir", PathRejection.Unc)]
    [InlineData(@"\\?\UNC\server\share\dir", PathRejection.Unc)]
    [InlineData(@"\\.\PhysicalDrive0", PathRejection.DevicePath)]
    [InlineData(@"\\?\Volume{01234567-89ab-cdef-0123-456789abcdef}\x", PathRejection.DevicePath)]
    [InlineData(@"\\?\GLOBALROOT\Device\HarddiskVolume1\x", PathRejection.DevicePath)]
    [InlineData(@"\??\C:\x", PathRejection.DevicePath)]
    [InlineData(@"C:\dir\file.txt:secret", PathRejection.AlternateDataStream)]
    [InlineData(@"C:\dir\file.txt:secret:$DATA", PathRejection.AlternateDataStream)]
    [InlineData(@"C:\dir:stream\file.txt", PathRejection.AlternateDataStream)]
    public void Forbidden_path_kinds_are_rejected(string raw, PathRejection expected)
    {
        Assert.Equal(expected, Rejected(raw));
    }

    // R7.5: the \\?\ long-path prefix is the same path as the plain form.
    [Fact]
    public void Long_path_prefix_is_stripped()
    {
        Assert.Equal(Canon(@"C:\Users\Mika"), Canon(@"\\?\C:\Users\Mika"));
        Assert.Equal(Canon(@"C:\Users\Mika"), Canon(@"\\?\c:\users\mika\"));
    }

    // R7.5 + design note: 8.3 names cannot be expanded without I/O, so never guess.
    [Theory]
    [InlineData(@"C:\PROGRA~1\App")]
    [InlineData(@"c:\users\MIKA~1\appdata")]
    [InlineData(@"C:\Users\x\DOCUME~2.TXT")]
    [InlineData(@"\\?\C:\PROGRA~1")]
    public void Short_names_are_flagged_unresolved(string raw)
    {
        Assert.Equal(PathRejection.UnresolvedShortName, Rejected(raw));
    }

    [Theory]
    [InlineData(@"C:\Users\a~b")]          // tilde but not 8.3 shape
    [InlineData(@"C:\Users\verylongname~1")] // base longer than 6 chars is not a short name
    public void Tilde_names_that_are_not_8dot3_pass(string raw)
    {
        Assert.True(PathPolicy.Normalize(raw).IsCanonical);
    }

    [Theory]
    [InlineData("", PathRejection.Empty)]
    [InlineData("   ", PathRejection.Empty)]
    [InlineData(@"Users\Mika", PathRejection.Relative)]
    [InlineData(@"\Users\Mika", PathRejection.Relative)]
    [InlineData(@"C:Users", PathRejection.Relative)]
    [InlineData(@"C:", PathRejection.Relative)]
    [InlineData(@"C:\a\*.txt", PathRejection.InvalidCharacter)]
    [InlineData("C:\\a\\b\u0001", PathRejection.InvalidCharacter)]
    [InlineData(@"C:\a\..\..", PathRejection.EscapesRoot)]
    public void Malformed_paths_are_rejected(string raw, PathRejection expected)
    {
        Assert.Equal(expected, Rejected(raw));
    }

    [Theory]
    [InlineData(@"C:\a\.\b", @"C:\A\B")]
    [InlineData(@"C:\a\x\..\b", @"C:\A\B")]
    [InlineData(@"C:\a\b. ", @"C:\A\B")]
    public void Dot_segments_and_trailing_dots_are_resolved(string raw, string expected)
    {
        Assert.Equal(expected, Canon(raw));
    }

    [Fact]
    public void Null_is_rejected_as_empty()
    {
        Assert.Equal(PathRejection.Empty, Rejected(null!));
    }

    // Property 4 (example form; the generative property is task 2.7): idempotence.
    [Theory]
    [InlineData(@"c:/Users//Mika/./AppData/")]
    [InlineData(@"\\?\c:\x\y\..\z.")]
    [InlineData(@"C:\")]
    public void Normalization_is_idempotent(string raw)
    {
        var once = Canon(raw);
        Assert.Equal(once, Canon(once));
    }

    // R1.6, R1.7: exclusion beats include.
    private static CanonicalPath C(string raw) => PathPolicy.Normalize(raw).Path!.Value;

    [Fact]
    public void Excluded_path_is_excluded_even_when_included()
    {
        var scope = new PathScope(includes: [C(@"C:\Users\Mika")], excludes: [C(@"C:\Users\Mika\Private")]);

        Assert.Equal(ScopeDecision.Excluded, scope.Decide(@"c:\users\mika\private"));
        Assert.Equal(ScopeDecision.Excluded, scope.Decide(@"C:\Users\Mika\Private\deep\file.txt"));
        Assert.Equal(ScopeDecision.Included, scope.Decide(@"C:\Users\Mika\Other\file.txt"));
    }

    [Fact]
    public void Exclusion_matches_on_segment_boundary_only()
    {
        var scope = new PathScope([C(@"C:\Users")], [C(@"C:\Users\Mika")]);

        Assert.Equal(ScopeDecision.Included, scope.Decide(@"C:\Users\MikaTwo\x"));
    }

    [Fact]
    public void Path_outside_every_include_is_not_included()
    {
        var scope = new PathScope([C(@"C:\Users\Mika")], []);

        Assert.Equal(ScopeDecision.NotIncluded, scope.Decide(@"D:\Data"));
    }

    [Fact]
    public void Forbidden_path_is_rejected_regardless_of_scope()
    {
        var scope = new PathScope([C(@"C:\Users")], []);

        Assert.Equal(ScopeDecision.Rejected, scope.Decide(@"\\server\share"));
        Assert.Equal(ScopeDecision.Rejected, scope.Decide(@"C:\Users\PROGRA~1"));
        Assert.Equal(PathRejection.Unc, scope.Reject(@"\\server\share"));
    }

    [Fact]
    public void Excluding_the_root_excludes_everything_below()
    {
        var scope = new PathScope([C(@"C:\")], [C(@"C:\")]);

        Assert.Equal(ScopeDecision.Excluded, scope.Decide(@"C:\any\thing"));
    }
}
