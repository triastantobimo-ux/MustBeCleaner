namespace MustBeCleaner.Domain;

/// <summary>
/// Pure classification entry point of the Policy Core (R5, R6, R7.3, R7.7, R11.1). Evaluation order,
/// first match wins: path normalization (a path that fails it is forbidden), user exclusion, protection
/// rules, volume support, Adapter match, eligibility conditions (R5.7; ADR 0012). Nothing before the last
/// step can be undone by a later one, and no input lifts a Protected result (R5.4, R6.6).
/// </summary>
public static class PolicyEngine
{
    private static readonly IReadOnlyDictionary<Condition, TriState> NoConditions = new Dictionary<Condition, TriState>();

    /// <summary>Classifies one Item. Deterministic: equal inputs give equal results (R5.6).</summary>
    public static Classification Classify(ItemFacts item, WorldFacts world, RuleSet ruleSet)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(ruleSet);
        var version = ruleSet.Version;

        // 1. Normalize the path (R7.4, R7.5). Exclusion matching needs the canonical form, so a path
        // that cannot be normalized is forbidden before any other rule can see it.
        var normalized = PathPolicy.Normalize(item.Identity.CanonicalPath);
        if (!normalized.IsCanonical)
        {
            return new(Status.Protected, "F-FORBIDDEN-PATH", version,
                [$"Path refused: {normalized.Reason} (R7.4, R7.5)."], NoConditions);
        }
        var path = normalized.Path!.Value;

        // 2. User exclusion (R1.6, R1.7).
        if (ruleSet.Exclusions.Any(e => PathPolicy.IsSameOrUnder(path, e)))
            return new(Status.Protected, "X-USER-EXCLUSION", version,
                ["Covered by a user exclusion (R1.6, R1.7)."], NoConditions);

        // 3. Protection rules (R6).
        var isDirectory = item.Identity.Attributes.HasFlag(FileAttributes.Directory);
        if (ProtectionRules.Match(path, isDirectory, item.Adapter is not null) is { } p)
            return new(Status.Protected, p.RuleId, version, [p.Reason], NoConditions);

        // 4. Volume support (R7.7).
        var scope = world[Condition.LocalScopeSupported];
        if (scope == TriState.False)
            return new(Status.InventoryOnly, "I-UNSUPPORTED-VOLUME", version,
                ["Item is not on a local NTFS volume; listed for inventory only (R7.7)."],
                new Dictionary<Condition, TriState> { [Condition.LocalScopeSupported] = TriState.False });

        // 5. Adapter match (R5.8).
        var adapter = item.Adapter;
        if (adapter is null && isDirectory)
            return new(Status.InventoryOnly, "I-UNKNOWN-FOLDER", version,
                ["No Adapter recognizes this folder; listed for inventory only (R5.8)."], NoConditions);

        // 6. Eligibility conditions (R5.2, R5.3, R11.1).
        var conditions = new Dictionary<Condition, TriState>
        {
            [Condition.LocalScopeSupported] = scope,
            [Condition.TrustedAdapterMatches] = adapter?.AppAndVersionMatch ?? TriState.False,
            [Condition.DisposableCategoryConfirmed] = adapter?.DisposableCategoryConfirmed ?? TriState.Unknown,
            [Condition.OwnerQuiescent] = world[Condition.OwnerQuiescent],
            [Condition.IdentityUnchangedSincePreview] = world[Condition.IdentityUnchangedSincePreview],
            [Condition.RecoveryReady] = world[Condition.RecoveryReady],
            [Condition.SelectedOnManifest] = world[Condition.SelectedOnManifest],
        };
        var ruleId = adapter?.RuleId ?? "R-UNRECOGNIZED-FILE";
        return conditions.Values.All(v => v == TriState.True)
            ? new(Status.Eligible, ruleId, version,
                [$"Every R11.1 condition is True under Adapter '{ruleId}' v{adapter!.Version}."], conditions)
            : new(Status.Review, ruleId, version,
                conditions.Where(kv => kv.Value != TriState.True).Select(kv => $"Condition {kv.Key} is {kv.Value}; action refused (R5.3)."),
                conditions);
    }
}
