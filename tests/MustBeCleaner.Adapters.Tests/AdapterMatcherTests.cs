using MustBeCleaner.Adapters;
using Xunit;

namespace MustBeCleaner.Adapters.Tests;

/// <summary>R8.13: fingerprint or version mismatch means Review; unknown paths match nothing.</summary>
public class AdapterMatcherTests
{
    private static Adapter Codex(string path = @".codex\.tmp", string app = "Codex", string version = "1.2.3")
    {
        var json = AdapterJson.Valid(j => j
            .Replace(@".codex\\.tmp", path.Replace(@"\", @"\\"))
            .Replace("\"appId\": \"Codex\"", $"\"appId\": \"{app}\"")
            .Replace("\"1.2.3\"", $"\"{version}\""));
        var r = AdapterLoader.Parse(json);
        Assert.True(r.IsValid, string.Join("; ", r.Violations));
        return r.Adapter!;
    }

    private static readonly DetectedApp Installed = new("Codex", "1.2.3");

    [Fact]
    public void Matching_path_app_and_version_gives_AdapterMatch()
    {
        var a = Codex();
        var r = AdapterMatcher.Match([a], @".codex\.tmp\x\y.bin", Installed);
        Assert.Equal(new AdapterMatch(a), r);
    }

    [Fact]
    public void Path_matching_ignores_case_and_slash_style_and_includes_the_folder_itself()
    {
        var a = Codex();
        Assert.IsType<AdapterMatch>(AdapterMatcher.Match([a], ".CODEX/.tmp", Installed));
    }

    [Fact]
    public void Sibling_with_same_prefix_is_not_owned()
    {
        var r = AdapterMatcher.Match([Codex()], @".codex\.tmpfoo\x", Installed);
        Assert.IsType<NoMatch>(r);
    }

    [Fact]
    public void Unknown_path_gives_NoMatch()
    {
        Assert.IsType<NoMatch>(AdapterMatcher.Match([Codex()], @"Other\x", Installed));
    }

    [Fact]
    public void Different_version_gives_VersionMismatch()
    {
        var a = Codex();
        var r = Assert.IsType<VersionMismatch>(
            AdapterMatcher.Match([a], @".codex\.tmp\x", new DetectedApp("Codex", "9.9.9")));
        Assert.Equal(a, r.Adapter);
        Assert.False(string.IsNullOrWhiteSpace(r.Reason));
    }

    [Fact]
    public void Different_app_id_gives_VersionMismatch()
    {
        var r = AdapterMatcher.Match([Codex()], @".codex\.tmp\x", new DetectedApp("Impostor", "1.2.3"));
        Assert.IsType<VersionMismatch>(r);
    }

    [Fact]
    public void Unverified_fingerprint_never_matches_a_real_version()
    {
        var r = AdapterMatcher.Match([Codex(version: "unverified")], @".codex\.tmp\x", Installed);
        Assert.IsType<VersionMismatch>(r);
    }

    [Theory]
    [InlineData(@"..\.codex\.tmp\x")]
    [InlineData(@".codex\..\.codex\.tmp\x")]
    [InlineData(@"C:\.codex\.tmp\x")]
    [InlineData(@"\.codex\.tmp\x")]
    [InlineData("")]
    public void Escaping_or_rooted_paths_give_NoMatch(string path)
    {
        Assert.IsType<NoMatch>(AdapterMatcher.Match([Codex()], path, Installed));
    }

    [Fact]
    public void Most_specific_owning_adapter_wins()
    {
        var broad = Codex(path: ".codex");
        var narrow = Codex(path: @".codex\.tmp");
        var r = AdapterMatcher.Match([broad, narrow], @".codex\.tmp\x", Installed);
        Assert.Equal(narrow, ((AdapterMatch)r).Adapter);
    }

    [Fact]
    public void A_matching_adapter_wins_over_a_mismatching_one_for_the_same_path()
    {
        var wrongApp = Codex(app: "Other");
        var right = Codex();
        var r = AdapterMatcher.Match([wrongApp, right], @".codex\.tmp\x", Installed);
        Assert.Equal(right, ((AdapterMatch)r).Adapter);
    }
}
