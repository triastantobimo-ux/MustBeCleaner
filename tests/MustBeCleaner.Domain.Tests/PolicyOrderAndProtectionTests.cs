namespace MustBeCleaner.Domain.Tests;

public class PolicyOrderAndProtectionTests
{
    private static readonly AdapterMatch TrustedCache =
        new("adapter.test.cache", "3", TriState.True, TriState.True);

    private static WorldFacts AllTrue() =>
        Enum.GetValues<Condition>().Aggregate(WorldFacts.None, (w, c) => w.With(c, TriState.True));

    private static Classification Run(
        string path,
        bool directory = false,
        AdapterMatch? adapter = null,
        WorldFacts? world = null,
        RuleSet? rules = null)
    {
        var identity = new ItemIdentity("vol", "file", path, 1, DateTime.UnixEpoch,
            directory ? FileAttributes.Directory : FileAttributes.Normal);
        return PolicyEngine.Classify(new ItemFacts(identity, [], adapter?.RuleId, adapter), world ?? AllTrue(), rules ?? new RuleSet("2025.1"));
    }

    // R1.7, R5.7: a user exclusion is decided first and beats an Adapter match.
    [Fact]
    public void User_exclusion_beats_trusted_adapter()
    {
        var path = @"C:\Users\u\AppData\Local\Acme\Cache";
        var rules = new RuleSet("2025.1", [PathPolicy.Normalize(path).Path!.Value]);
        var c = Run(path + @"\blob_1", adapter: TrustedCache, rules: rules);
        Assert.Equal(Status.Protected, c.Status);
        Assert.Equal("X-USER-EXCLUSION", c.RuleId);
        Assert.Equal("2025.1", c.RuleVersion);
    }

    // R7.4, R5.7: a forbidden path (UNC, device, ADS, short name) is refused before any Adapter rule.
    [Theory]
    [InlineData(@"\\server\share\cache\blob")]
    [InlineData(@"\\.\PhysicalDrive0")]
    [InlineData(@"C:\Users\u\file.txt:stream")]
    [InlineData(@"C:\PROGRA~1\Acme\x")]
    [InlineData(@"relative\path")]
    public void Forbidden_path_is_protected_even_with_adapter(string path)
    {
        var c = Run(path, adapter: TrustedCache);
        Assert.Equal(Status.Protected, c.Status);
        Assert.Equal("F-FORBIDDEN-PATH", c.RuleId);
        Assert.NotEmpty(c.Reasons);
    }

    // R6.1: Program Files and WindowsApps.
    [Theory]
    [InlineData(@"C:\Program Files\Acme\data.txt")]
    [InlineData(@"C:\Program Files (x86)\Acme\data.txt")]
    [InlineData(@"C:\Program Files\WindowsApps\Pkg\data.txt")]
    [InlineData(@"D:\WindowsApps\Pkg\data.txt")]
    public void Program_files_and_windowsapps_are_protected(string path)
    {
        Assert.Equal(Status.Protected, Run(path, adapter: TrustedCache).Status);
    }

    // R6.2: executable and driver binaries are protected wherever they sit.
    [Theory]
    [InlineData(@"C:\Users\u\AppData\Local\Acme\Cache\tool.exe")]
    [InlineData(@"C:\Users\u\AppData\Local\Acme\Cache\lib.DLL")]
    [InlineData(@"C:\Users\u\AppData\Local\Acme\Cache\drv.sys")]
    [InlineData(@"C:\Users\u\AppData\Local\Acme\Cache\ctl.ocx")]
    public void Binary_extensions_are_protected_even_with_adapter(string path)
    {
        var c = Run(path, adapter: TrustedCache);
        Assert.Equal(Status.Protected, c.Status);
        Assert.Equal("P-BINARY", c.RuleId);
    }

    // R6.2: an extensionless file nobody owns is treated as a possible binary; an owned one is not.
    [Fact]
    public void Extensionless_file_without_owner_is_protected_but_owned_is_not()
    {
        var path = @"C:\Users\u\AppData\Local\Acme\Cache\blob_1";
        var unowned = Run(path);
        Assert.Equal(Status.Protected, unowned.Status);
        Assert.Equal("P-EXTENSIONLESS-UNOWNED", unowned.RuleId);
        Assert.Equal(Status.Eligible, Run(path, adapter: TrustedCache).Status);
    }

    // R6.3: credentials, sessions, history, memory, worktrees, repos, backups, models.
    [Theory]
    [InlineData(@"C:\Users\u\AppData\Local\Acme\Cache\Credentials\x.dat")]
    [InlineData(@"C:\Users\u\AppData\Roaming\Acme\Sessions\x.dat")]
    [InlineData(@"C:\Users\u\AppData\Roaming\Acme\History\x.dat")]
    [InlineData(@"C:\Users\u\.codex\memories\x.md")]
    [InlineData(@"C:\Users\u\.codex\worktrees\abc\x.dat")]
    [InlineData(@"C:\Users\u\src\app\.git\config.dat")]
    [InlineData(@"C:\Users\u\repos\app\x.dat")]
    [InlineData(@"C:\Users\u\Backups\x.dat")]
    [InlineData(@"C:\Users\u\AppData\Local\Acme\Models\x.dat")]
    public void Valuable_user_data_is_protected_even_with_adapter(string path)
    {
        var c = Run(path, adapter: TrustedCache);
        Assert.Equal(Status.Protected, c.Status);
        Assert.StartsWith("P-", c.RuleId);
    }

    // R6.4: database, WAL, SHM and journal are one Transaction_Set.
    [Theory]
    [InlineData(@"C:\Users\u\AppData\Local\Acme\Cache\store.db")]
    [InlineData(@"C:\Users\u\AppData\Local\Acme\Cache\store.db-wal")]
    [InlineData(@"C:\Users\u\AppData\Local\Acme\Cache\store.db-shm")]
    [InlineData(@"C:\Users\u\AppData\Local\Acme\Cache\store.db-journal")]
    [InlineData(@"C:\Users\u\AppData\Local\Acme\Cache\store.sqlite")]
    public void Database_files_are_protected_as_transaction_set(string path)
    {
        var c = Run(path, adapter: TrustedCache);
        Assert.Equal(Status.Protected, c.Status);
        Assert.Equal("P-TRANSACTION-SET", c.RuleId);
    }

    [Fact]
    public void Transaction_set_members_share_one_key()
    {
        var dir = @"C:\Users\u\AppData\Local\Acme\Cache\";
        var keys = new[] { "store.db", "store.db-wal", "STORE.DB-SHM", "store.db-journal" }
            .Select(n => TransactionSet.KeyOf(dir + n)).ToArray();
        Assert.All(keys, k => Assert.NotNull(k));
        Assert.Single(keys.Distinct());
        Assert.Null(TransactionSet.KeyOf(dir + "blob_1"));
    }

    // R6.5: Local, Roaming and .codex roots are never recursive-deletable.
    [Theory]
    [InlineData(@"C:\Users\u\AppData\Local")]
    [InlineData(@"C:\Users\u\AppData\Roaming")]
    [InlineData(@"C:\Users\u\.codex")]
    public void Never_recursive_roots_are_protected(string path)
    {
        var c = Run(path, directory: true, adapter: TrustedCache);
        Assert.Equal(Status.Protected, c.Status);
        Assert.Equal("P-NEVER-RECURSIVE-ROOT", c.RuleId);
    }

    // R6.6: no input can lift protection: perfect world, trusted adapter, still Protected.
    [Fact]
    public void Protection_wins_over_perfect_world_and_adapter()
    {
        var c = Run(@"C:\Windows\Temp\x", adapter: TrustedCache, world: AllTrue());
        Assert.Equal(Status.Protected, c.Status);
    }

    // R7.7: protection is checked before the volume check.
    [Fact]
    public void Protected_item_on_non_ntfs_volume_stays_protected()
    {
        var world = AllTrue().With(Condition.LocalScopeSupported, TriState.False);
        Assert.Equal(Status.Protected, Run(@"C:\Windows\x", world: world).Status);
    }

    // R5.6: two classifications with equal contents are equal.
    [Fact]
    public void Classification_has_value_equality_over_collections()
    {
        var a = Run(@"C:\Users\u\AppData\Local\Acme\Cache\blob_1", adapter: TrustedCache);
        var b = Run(@"C:\Users\u\AppData\Local\Acme\Cache\blob_1", adapter: TrustedCache);
        Assert.NotSame(a, b);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, Run(@"C:\Windows\x"));
    }

    // R5.9: folder name, extension and age alone never make Eligible; an unrelated owner id does not either.
    [Fact]
    public void Unknown_adapter_state_is_never_eligible()
    {
        var identity = new ItemIdentity("vol", "file", @"C:\Users\u\AppData\Local\Acme\Cache\blob_1", 1, DateTime.UnixEpoch, FileAttributes.Normal);
        var item = new ItemFacts(identity, [], "adapter.test.cache");
        var c = PolicyEngine.Classify(item, AllTrue(), new RuleSet("2025.1"));
        Assert.NotEqual(Status.Eligible, c.Status);
    }
}
