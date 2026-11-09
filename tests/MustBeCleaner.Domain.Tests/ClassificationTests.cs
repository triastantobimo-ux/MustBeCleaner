namespace MustBeCleaner.Domain.Tests;

public class ClassificationTests
{
    private static IReadOnlyDictionary<Condition, TriState> All(TriState value) =>
        Enum.GetValues<Condition>().ToDictionary(c => c, _ => value);

    // R5.6: rule ID, rule version, and reasons are recorded.
    [Fact]
    public void Records_rule_id_version_and_reasons()
    {
        var c = new Classification(Status.Protected, "P-WINDOWS", "2025.1", ["Under Windows root"], new Dictionary<Condition, TriState>());

        Assert.Equal(Status.Protected, c.Status);
        Assert.Equal("P-WINDOWS", c.RuleId);
        Assert.Equal("2025.1", c.RuleVersion);
        Assert.Equal(["Under Windows root"], c.Reasons);
    }

    // R5.2: conditions never observed read as Unknown.
    [Fact]
    public void Condition_not_supplied_reads_as_unknown()
    {
        var c = new Classification(Status.Review, "R-1", "1", ["needs user"], new Dictionary<Condition, TriState>());

        Assert.Equal(TriState.Unknown, c.Conditions[Condition.OwnerQuiescent]);
        Assert.Equal(Enum.GetValues<Condition>().Length, c.Conditions.Count);
    }

    // R5.1, R5.3, R11.1: Eligible only when every condition is True.
    [Fact]
    public void Eligible_with_all_conditions_true_is_allowed()
    {
        var c = new Classification(Status.Eligible, "A-1", "1", ["all conditions hold"], All(TriState.True));

        Assert.Equal(Status.Eligible, c.Status);
    }

    [Theory]
    [InlineData(TriState.Unknown)]
    [InlineData(TriState.False)]
    public void Eligible_with_any_condition_not_true_is_rejected(TriState bad)
    {
        var conditions = All(TriState.True).ToDictionary(kv => kv.Key, kv => kv.Value);
        conditions[Condition.RecoveryReady] = bad;

        Assert.Throws<ArgumentException>(() =>
            new Classification(Status.Eligible, "A-1", "1", ["claimed eligible"], conditions));
    }

    [Fact]
    public void Rejects_blank_rule_id_or_version_or_no_reasons()
    {
        var none = new Dictionary<Condition, TriState>();

        Assert.Throws<ArgumentException>(() => new Classification(Status.Review, " ", "1", ["r"], none));
        Assert.Throws<ArgumentException>(() => new Classification(Status.Review, "R", "", ["r"], none));
        Assert.Throws<ArgumentException>(() => new Classification(Status.Review, "R", "1", [], none));
    }

    [Fact]
    public void Later_changes_to_the_input_do_not_alter_the_classification()
    {
        var conditions = new Dictionary<Condition, TriState> { [Condition.OwnerQuiescent] = TriState.False };
        var reasons = new List<string> { "why" };
        var c = new Classification(Status.Review, "R", "1", reasons, conditions);

        conditions[Condition.OwnerQuiescent] = TriState.True;
        reasons.Add("later");

        Assert.Equal(TriState.False, c.Conditions[Condition.OwnerQuiescent]);
        Assert.Single(c.Reasons);
    }
}
