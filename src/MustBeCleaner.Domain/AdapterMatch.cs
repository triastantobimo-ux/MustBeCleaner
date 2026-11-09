namespace MustBeCleaner.Domain;

/// <summary>
/// What a trusted-or-not Adapter says about one Item, supplied as plain input so the Policy Core
/// never references the Adapters project (R5.5, R8.1, R11.1). Immutable.
/// Both conditions are <see cref="TriState"/>: a version mismatch is <see cref="TriState.False"/>,
/// an unverified fingerprint is <see cref="TriState.Unknown"/> (R5.2, R5.3).
/// </summary>
public sealed record AdapterMatch
{
    /// <param name="ruleId">Adapter rule ID that owns the Item. Not blank.</param>
    /// <param name="version">Adapter rule version. Not blank.</param>
    /// <param name="appAndVersionMatch">Trusted Adapter matches the app and its version (R11.1).</param>
    /// <param name="disposableCategoryConfirmed">The disposable category is confirmed (R11.1).</param>
    public AdapterMatch(string ruleId, string version, TriState appAndVersionMatch, TriState disposableCategoryConfirmed)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        RuleId = ruleId;
        Version = version;
        AppAndVersionMatch = appAndVersionMatch;
        DisposableCategoryConfirmed = disposableCategoryConfirmed;
    }

    public string RuleId { get; }
    public string Version { get; }
    public TriState AppAndVersionMatch { get; }
    public TriState DisposableCategoryConfirmed { get; }
}
