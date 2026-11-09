using MustBeCleaner.Adapters;
using Xunit;

namespace MustBeCleaner.Adapters.Tests;

/// <summary>
/// Task 3.2 (R8.3-R8.12): the shipped initial Adapters under policies/adapters.
/// Only the public loader is used. Paths are relative to the user profile directory (see policies/adapters/README.md).
/// </summary>
public class InitialAdapterCatalogTests
{
    private const string Protected = "protected-path";

    private static readonly Lazy<IReadOnlyList<Adapter>> Catalog = new(LoadAll);

    private static string AdaptersDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "MustBeCleaner.sln"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("MustBeCleaner.sln not found."), "policies", "adapters");
    }

    private static IReadOnlyList<Adapter> LoadAll()
    {
        var files = Directory.Exists(AdaptersDir()) ? Directory.GetFiles(AdaptersDir(), "*.json") : [];
        var list = new List<Adapter>();
        foreach (var f in files)
        {
            var r = AdapterLoader.LoadFile(f);
            Assert.True(r.IsValid, $"{Path.GetFileName(f)}: {string.Join("; ", r.Violations)}");
            list.Add(r.Adapter!);
        }
        return list;
    }

    private static IEnumerable<Adapter> App(string appId) => Catalog.Value.Where(a => a.Fingerprint.AppId == appId);

    private static string N(string p) => p.Replace('/', '\\').TrimEnd('\\').ToLowerInvariant();

    private static bool Overlaps(string a, string b)
    {
        var x = N(a); var y = N(b);
        return x == y || x.StartsWith(y + "\\") || y.StartsWith(x + "\\");
    }

    [Fact]
    public void Whole_directory_loads_with_unique_rule_ids()
    {
        Assert.NotEmpty(Catalog.Value);
        Assert.Equal(Catalog.Value.Count, Catalog.Value.Select(a => a.RuleId).Distinct().Count());
    }

    [Fact]
    public void Every_adapter_is_inventory_only_unverified_and_claims_nothing_safe()
    {
        foreach (var a in Catalog.Value)
        {
            Assert.Equal(AdapterTrust.InventoryOnly, a.Trust);
            Assert.Equal(["unverified"], a.Fingerprint.Versions);
            Assert.NotEmpty(a.Blockers);
            Assert.NotEmpty(a.RequiredEvidence);
            Assert.Equal("0.1.0", a.Version);
        }
    }

    [Theory]
    [InlineData("Codex")]
    [InlineData("CodexDesktop")]
    [InlineData("OpenAI")]
    [InlineData("OpenAICodexPackage")]
    [InlineData("Claude")]
    [InlineData("ClaudeMem")]
    [InlineData("AutoClaw2")]
    [InlineData("Code")]
    [InlineData("Qoder")]
    [InlineData("OpenCode")]
    [InlineData("WindowsTemp")]
    [InlineData("GoBuild")]
    public void R8_3_every_required_location_has_an_adapter(string appId) => Assert.NotEmpty(App(appId));

    [Fact]
    public void R8_4_codex_home_protects_user_work_and_state()
    {
        var paths = App("Codex").Where(a => a.Blockers.Contains(Protected)).SelectMany(a => a.AllowedRelativePaths).Select(N).ToHashSet();
        foreach (var p in new[]
        {
            @".codex\sessions", @".codex\archived_sessions", @".codex\memories", @".codex\auth.json", @".codex\config.toml",
            @".codex\thread_history_1.sqlite", @".codex\thread_history_1.sqlite-wal", @".codex\thread_history_1.sqlite-shm",
            @".codex\logs_2.sqlite", @".codex\logs_2.sqlite-wal", @".codex\logs_2.sqlite-shm",
            @".codex\.chatgpt-projects", @".codex\worktrees", @".codex\skills", @".codex\.sandbox-bin",
        })
            Assert.Contains(p, paths);
    }

    [Fact]
    public void R8_5_codex_tmp_cache_plugins_are_review_until_lifecycle_age_and_ownership_are_verified()
    {
        foreach (var p in new[] { @".codex\.tmp", @".codex\cache", @".codex\plugins" })
        {
            var a = Assert.Single(App("Codex"), x => x.AllowedRelativePaths.Select(N).Contains(p) && !x.Blockers.Contains(Protected));
            Assert.Contains("lifecycle-verified", a.RequiredEvidence);
            Assert.Contains("age-verified", a.RequiredEvidence);
            Assert.Contains("active-ownership-verified", a.RequiredEvidence);
        }
        var plugins = App("Codex").Single(x => x.AllowedRelativePaths.Select(N).Contains(@".codex\plugins"));
        Assert.Contains("active-plugins-protected", plugins.Blockers);
    }

    [Fact]
    public void R8_12_managed_worktrees_defer_to_lifecycle_and_offer_no_cleanup_checkbox()
    {
        var wt = App("Codex").Where(a => a.AllowedRelativePaths.Select(N).Contains(@".codex\worktrees")).ToList();
        Assert.NotEmpty(wt);
        foreach (var a in wt)
        {
            Assert.Contains(Protected, a.Blockers);
            Assert.Contains("no-cleanup-checkbox", a.Blockers);
            Assert.Contains("app-or-git-lifecycle", a.RequiredEvidence);
        }
    }

    [Fact]
    public void R8_6_claude_mem_bin_is_protected_binary_not_cache()
    {
        var a = Assert.Single(App("ClaudeMem"), x => x.AllowedRelativePaths.Select(N).Contains(@"appdata\local\claude-mem\bin"));
        Assert.Equal(AdapterCategory.Binary, a.Category);
        Assert.Contains(Protected, a.Blockers);
        Assert.DoesNotContain(App("ClaudeMem"), x => x.Category == AdapterCategory.Cache);
    }

    [Theory]
    [InlineData("Code")]
    [InlineData("Qoder")]
    [InlineData("Claude")]
    public void R8_7_R8_8_electron_http_cache_is_separate_from_profile_state_and_user_data(string appId)
    {
        var cache = App(appId).Where(a => a.Category == AdapterCategory.Cache).ToList();
        var prot = App(appId).Where(a => a.Blockers.Contains(Protected)).ToList();
        Assert.NotEmpty(cache);
        Assert.NotEmpty(prot);
        foreach (var c in cache)
        {
            Assert.DoesNotContain(Protected, c.Blockers);
            foreach (var cp in c.AllowedRelativePaths)
                foreach (var pp in prot.SelectMany(p => p.AllowedRelativePaths))
                    Assert.False(Overlaps(cp, pp), $"{cp} overlaps protected {pp}");
        }
        var protectedPaths = string.Join("|", prot.SelectMany(p => p.AllowedRelativePaths)).ToLowerInvariant();
        Assert.Contains(@"\local storage", protectedPaths);
        Assert.Contains(@"\indexeddb", protectedPaths);
        Assert.Contains(@"\network\cookies", protectedPaths);
    }

    [Fact]
    public void R8_7_code_protects_user_backups_history_storage_extensions()
    {
        var protectedPaths = App("Code").Where(a => a.Blockers.Contains(Protected)).SelectMany(a => a.AllowedRelativePaths).Select(N).ToHashSet();
        foreach (var p in new[]
        {
            @"appdata\roaming\code\user", @"appdata\roaming\code\backups", @"appdata\roaming\code\user\history",
            @"appdata\roaming\code\user\globalstorage", @"appdata\roaming\code\user\workspacestorage", @".vscode\extensions",
        })
            Assert.Contains(p, protectedPaths);
    }

    [Fact]
    public void R8_9_temp_needs_owner_grace_active_and_path_rule_and_never_the_root()
    {
        var a = Assert.Single(App("WindowsTemp"));
        foreach (var e in new[] { "owner-verified", "grace-period-elapsed", "not-active", "path-allow-rule" })
            Assert.Contains(e, a.RequiredEvidence);
        Assert.Contains("temp-root-never-deletable", a.Blockers);
        Assert.DoesNotContain(@"appdata\local\temp", a.AllowedRelativePaths.Select(N));
    }

    [Fact]
    public void R8_10_go_build_needs_actual_gocache_and_idle_build()
    {
        var a = Assert.Single(App("GoBuild"));
        Assert.Equal(AdapterCategory.BuildCache, a.Category);
        Assert.Contains("gocache-resolved", a.RequiredEvidence);
        Assert.Contains("build-idle", a.RequiredEvidence);
        Assert.Contains("gocache-unresolved", a.Blockers);
    }

    [Fact]
    public void R8_11_autoclaw_everything_is_inventory_only_with_unverified_layout()
    {
        foreach (var a in App("AutoClaw2"))
        {
            Assert.Equal(AdapterTrust.InventoryOnly, a.Trust);
            Assert.Contains("layout-unverified", a.Blockers);
        }
    }

    [Fact]
    public void Protected_paths_never_overlap_a_non_protected_adapter_in_the_same_app()
    {
        foreach (var g in Catalog.Value.GroupBy(a => a.Fingerprint.AppId))
        {
            var prot = g.Where(a => a.Blockers.Contains(Protected)).ToList();
            foreach (var a in prot) Assert.True(a.Category is AdapterCategory.UserData or AdapterCategory.Binary, a.RuleId);
            foreach (var other in g.Where(a => !a.Blockers.Contains(Protected)))
                foreach (var op in other.AllowedRelativePaths)
                    foreach (var pp in prot.SelectMany(p => p.AllowedRelativePaths))
                        Assert.False(N(op) == N(pp) || N(op).StartsWith(N(pp) + "\\"), $"{other.RuleId}: {op} is inside protected {pp}");
        }
    }
}
