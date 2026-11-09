using MustBeCleaner.Domain;

namespace MustBeCleaner.Inspector;

/// <summary>
/// Protection hook backed by the Policy Core (R6.7). The Inspector only asks; the fixed
/// protection list, user exclusions and the forbidden-path check decide.
/// </summary>
public sealed class PolicyProtectionHook : IProtectionHook
{
    private readonly RuleSet _rules;

    /// <summary>Creates the hook. <paramref name="rules"/> defaults to a versioned set with no exclusions.</summary>
    public PolicyProtectionHook(RuleSet? rules = null) => _rules = rules ?? new RuleSet("policy-core");

    /// <inheritdoc />
    public ProtectionVerdict Evaluate(string fullPath) => Evaluate(fullPath, isDirectory: false);

    /// <inheritdoc />
    public ProtectionVerdict Evaluate(string fullPath, bool isDirectory)
    {
        var normalized = PathPolicy.Normalize(fullPath);
        if (!normalized.IsCanonical)
            return new ProtectionVerdict(true, "F-FORBIDDEN-PATH");

        var identity = new ItemIdentity(
            "inspector", "inspector", normalized.Path!.Value.Value, size: null, DateTime.UnixEpoch,
            isDirectory ? FileAttributes.Directory : FileAttributes.Normal);
        var classification = PolicyEngine.Classify(new ItemFacts(identity, [], adapterRuleId: null), WorldFacts.None, _rules);
        return classification.Status == Status.Protected
            ? new ProtectionVerdict(true, classification.RuleId)
            : new ProtectionVerdict(false, null);
    }
}
