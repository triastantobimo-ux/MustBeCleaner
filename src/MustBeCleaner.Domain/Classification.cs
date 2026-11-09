using System.Collections.Immutable;

namespace MustBeCleaner.Domain;

/// <summary>
/// Result of classifying one Item: status, the rule that decided it, the rule version, the
/// reasons, and the value of every eligibility condition (R5.1, R5.6). Immutable.
/// An <see cref="Status.Eligible"/> classification cannot be built unless every
/// <see cref="Condition"/> is <see cref="TriState.True"/> (R5.3, R11.1).
/// </summary>
public sealed record Classification
{
    /// <param name="status">Decided status.</param>
    /// <param name="ruleId">ID of the deciding rule. Not blank.</param>
    /// <param name="ruleVersion">Version of the rule set used. Not blank.</param>
    /// <param name="reasons">At least one human-readable reason.</param>
    /// <param name="conditions">Observed conditions; any missing condition becomes Unknown.</param>
    /// <exception cref="ArgumentException">Blank rule ID/version, no reasons, or Eligible with a non-True condition.</exception>
    public Classification(
        Status status,
        string ruleId,
        string ruleVersion,
        IEnumerable<string> reasons,
        IEnumerable<KeyValuePair<Condition, TriState>> conditions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleVersion);
        ArgumentNullException.ThrowIfNull(reasons);
        ArgumentNullException.ThrowIfNull(conditions);

        var reasonList = reasons.ToImmutableArray();
        if (reasonList.IsEmpty)
            throw new ArgumentException("At least one reason is required.", nameof(reasons));

        var map = Enum.GetValues<Condition>().ToImmutableDictionary(c => c, _ => TriState.Unknown);
        foreach (var (condition, value) in conditions) map = map.SetItem(condition, value);

        if (status == Status.Eligible && map.Values.Any(v => v != TriState.True))
            throw new ArgumentException("Eligible requires every condition to be True.", nameof(conditions));

        Status = status;
        RuleId = ruleId;
        RuleVersion = ruleVersion;
        Reasons = reasonList;
        Conditions = map;
    }

    public Status Status { get; }
    public string RuleId { get; }
    public string RuleVersion { get; }
    public IReadOnlyList<string> Reasons { get; }
    public IReadOnlyDictionary<Condition, TriState> Conditions { get; }

    /// <summary>Value equality over every field, including the reason list and the condition map (R5.6).</summary>
    public bool Equals(Classification? other) =>
        other is not null
        && Status == other.Status
        && RuleId == other.RuleId
        && RuleVersion == other.RuleVersion
        && Reasons.SequenceEqual(other.Reasons)
        && Conditions.Count == other.Conditions.Count
        && Conditions.All(kv => other.Conditions.TryGetValue(kv.Key, out var v) && v == kv.Value);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Status);
        hash.Add(RuleId);
        hash.Add(RuleVersion);
        foreach (var r in Reasons) hash.Add(r);
        foreach (var kv in Conditions.OrderBy(kv => kv.Key)) { hash.Add(kv.Key); hash.Add(kv.Value); }
        return hash.ToHashCode();
    }
}
