namespace MustBeCleaner.Domain.Tests;

public class ItemFactsAndRuleSetTests
{
    private static ItemIdentity Identity() =>
        new("v", "f", @"C:\a\b", 1, DateTime.UnixEpoch, FileAttributes.Normal);

    // R7.3: the whole ancestor chain is part of the facts.
    [Fact]
    public void ItemFacts_carries_identity_ancestors_and_ownership()
    {
        var facts = new ItemFacts(Identity(), [@"C:\", @"C:\a"], "adapter.foo");

        Assert.Equal(@"C:\a\b", facts.Identity.CanonicalPath);
        Assert.Equal([@"C:\", @"C:\a"], facts.Ancestors);
        Assert.Equal("adapter.foo", facts.AdapterRuleId);
    }

    [Fact]
    public void ItemFacts_without_adapter_has_no_owner()
    {
        var facts = new ItemFacts(Identity(), [], null);

        Assert.Null(facts.AdapterRuleId);
    }

    [Fact]
    public void ItemFacts_copies_ancestors()
    {
        var ancestors = new List<string> { @"C:\" };
        var facts = new ItemFacts(Identity(), ancestors, null);

        ancestors.Add(@"C:\x");

        Assert.Single(facts.Ancestors);
    }

    // R5.5, R5.6: rule sets are versioned.
    [Fact]
    public void RuleSet_exposes_its_version()
    {
        Assert.Equal("2025.1", new RuleSet("2025.1").Version);
    }

    [Fact]
    public void RuleSet_with_same_version_is_equal()
    {
        Assert.Equal(new RuleSet("1"), new RuleSet("1"));
        Assert.NotEqual(new RuleSet("1"), new RuleSet("2"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void RuleSet_requires_a_version(string version)
    {
        Assert.Throws<ArgumentException>(() => new RuleSet(version));
    }
}
