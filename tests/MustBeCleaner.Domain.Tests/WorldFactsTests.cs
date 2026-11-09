using MustBeCleaner.Domain;

namespace MustBeCleaner.Domain.Tests;

public class WorldFactsTests
{
    // R5.2, R5.3: a condition nobody observed is Unknown, never True.
    [Fact]
    public void Unobserved_condition_is_unknown()
    {
        var facts = WorldFacts.None;

        Assert.Equal(TriState.Unknown, facts[Condition.OwnerQuiescent]);
    }

    [Fact]
    public void Observed_condition_returns_its_value_and_others_stay_unknown()
    {
        var facts = WorldFacts.None.With(Condition.OwnerQuiescent, TriState.True);

        Assert.Equal(TriState.True, facts[Condition.OwnerQuiescent]);
        Assert.Equal(TriState.Unknown, facts[Condition.RecoveryReady]);
    }

    [Fact]
    public void With_does_not_mutate_the_original()
    {
        var original = WorldFacts.None;
        _ = original.With(Condition.OwnerQuiescent, TriState.True);

        Assert.Equal(TriState.Unknown, original[Condition.OwnerQuiescent]);
    }
}
