namespace MustBeCleaner.Domain.Tests;

public class PolicyEngineTests
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
        var item = new ItemFacts(identity, [], adapter?.RuleId, adapter);
        return PolicyEngine.Classify(item, world ?? AllTrue(), rules ?? new RuleSet("2025.1"));
    }

    // R6.1: Windows critical roots are Protected.
    [Theory]
    [InlineData(@"C:\Windows", true)]
    [InlineData(@"C:\Windows\WinSxS\x86_foo", true)]
    [InlineData(@"C:\Windows\System32\config\SAM", false)]
    [InlineData(@"C:\Windows\SysWOW64\kernel.txt", false)]
    [InlineData(@"C:\Windows\System32\DriverStore\FileRepository", true)]
    [InlineData(@"C:\Windows\Installer\abc.msi", false)]
    [InlineData(@"C:\Boot\BCD", false)]
    [InlineData(@"C:\EFI\Microsoft", true)]
    [InlineData(@"C:\Recovery", true)]
    [InlineData(@"C:\System Volume Information", true)]
    [InlineData(@"C:\pagefile.sys", false)]
    [InlineData(@"C:\hiberfil.sys", false)]
    [InlineData(@"C:\swapfile.sys", false)]
    public void Windows_critical_roots_are_protected(string path, bool directory)
    {
        var c = Run(path, directory);
        Assert.Equal(Status.Protected, c.Status);
        Assert.Equal("2025.1", c.RuleVersion);
        Assert.NotEmpty(c.Reasons);
    }
}

public class PolicyEngineEligibilityTests
{
    private static readonly AdapterMatch TrustedCache =
        new("adapter.test.cache", "3", TriState.True, TriState.True);

    private const string CachePath = @"C:\Users\u\AppData\Local\Acme\Cache\blob_1";

    private static WorldFacts AllTrue() =>
        Enum.GetValues<Condition>().Aggregate(WorldFacts.None, (w, c) => w.With(c, TriState.True));

    private static Classification Run(
        string path,
        bool directory = false,
        AdapterMatch? adapter = null,
        WorldFacts? world = null,
        RuleSet? rules = null,
        DateTime? modifiedUtc = null)
    {
        var identity = new ItemIdentity("vol", "file", path, 1, modifiedUtc ?? DateTime.UnixEpoch,
            directory ? FileAttributes.Directory : FileAttributes.Normal);
        return PolicyEngine.Classify(new ItemFacts(identity, [], adapter?.RuleId, adapter), world ?? AllTrue(), rules ?? new RuleSet("2025.1"));
    }

    // R11.1, R5.6: Eligible only with a trusted Adapter and every condition True; records rule and version.
    [Fact]
    public void Trusted_adapter_with_every_condition_true_is_eligible()
    {
        var c = Run(CachePath, adapter: TrustedCache);

        Assert.Equal(Status.Eligible, c.Status);
        Assert.Equal("adapter.test.cache", c.RuleId);
        Assert.Equal("2025.1", c.RuleVersion);
        Assert.All(c.Conditions.Values, v => Assert.Equal(TriState.True, v));
    }

    // R5.2, R5.3, R11.1: any one condition that is not True blocks Eligible, and the reason names it.
    [Theory]
    [MemberData(nameof(EachConditionNotTrue))]
    public void Any_condition_not_true_gives_review_naming_the_condition(Condition blocked, TriState value)
    {
        var adapter = new AdapterMatch(
            TrustedCache.RuleId,
            TrustedCache.Version,
            blocked == Condition.TrustedAdapterMatches ? value : TriState.True,
            blocked == Condition.DisposableCategoryConfirmed ? value : TriState.True);
        var world = blocked is Condition.TrustedAdapterMatches or Condition.DisposableCategoryConfirmed
            ? AllTrue()
            : AllTrue().With(blocked, value);

        var c = Run(CachePath, adapter: adapter, world: world);

        Assert.Equal(Status.Review, c.Status);
        Assert.Equal(value, c.Conditions[blocked]);
        Assert.Contains(c.Reasons, r => r.Contains(blocked.ToString()));
    }

    public static TheoryData<Condition, TriState> EachConditionNotTrue()
    {
        var data = new TheoryData<Condition, TriState>();
        foreach (var c in Enum.GetValues<Condition>())
            foreach (var v in new[] { TriState.Unknown, TriState.False })
            {
                // R7.7: a False volume check is InventoryOnly, covered by its own test.
                if (c == Condition.LocalScopeSupported && v == TriState.False) continue;
                data.Add(c, v);
            }
        return data;
    }

    // R7.7: an Item on a volume that is not local NTFS is inventory-only, Adapter match or not.
    [Fact]
    public void Item_on_non_ntfs_local_volume_is_inventory_only()
    {
        var world = AllTrue().With(Condition.LocalScopeSupported, TriState.False);

        var c = Run(CachePath, adapter: TrustedCache, world: world);

        Assert.Equal(Status.InventoryOnly, c.Status);
        Assert.Equal(TriState.False, c.Conditions[Condition.LocalScopeSupported]);
    }

    // R5.3, R5.9: a file with no Adapter owner is never Eligible, whatever its name, extension or mtime says.
    [Theory]
    [InlineData(@"C:\Users\u\AppData\Local\Temp\scratch.tmp")]
    [InlineData(@"C:\Users\u\Documents\cache\old.log")]
    public void File_without_adapter_is_review_not_eligible(string path)
    {
        var c = Run(path, modifiedUtc: new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(Status.Review, c.Status);
        Assert.Equal(TriState.False, c.Conditions[Condition.TrustedAdapterMatches]);
    }

    // R5.8: a folder no Adapter recognizes is inventory-only, even when the world looks perfect.
    [Fact]
    public void Folder_no_adapter_recognizes_is_inventory_only()
    {
        var c = Run(@"C:\Users\u\AppData\Local\Mystery\Cache", directory: true);

        Assert.Equal(Status.InventoryOnly, c.Status);
        Assert.NotEmpty(c.RuleId);
        Assert.NotEmpty(c.Reasons);
    }
}
