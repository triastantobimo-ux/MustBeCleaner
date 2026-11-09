using System.Collections.Immutable;

namespace MustBeCleaner.Domain;

/// <summary>
/// Facts about one Item that the Policy Core classifies: its identity, the ancestor chain
/// (R7.3), and the Adapter rule that claims it, if any (R5.8). Immutable, no I/O.
/// </summary>
public sealed record ItemFacts
{
    /// <param name="identity">Item identity.</param>
    /// <param name="ancestors">Canonical paths from the volume root down to the Item's parent.</param>
    /// <param name="adapterRuleId">ID of the Adapter rule that owns the Item, or null if no Adapter recognizes it.</param>
    /// <param name="adapterMatch">
    /// Detail of the Adapter match. When null but <paramref name="adapterRuleId"/> is set, the owner is
    /// known and every Adapter condition is Unknown (R5.3).
    /// </param>
    public ItemFacts(ItemIdentity identity, IEnumerable<string> ancestors, string? adapterRuleId, AdapterMatch? adapterMatch = null)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(ancestors);
        Identity = identity;
        Ancestors = ancestors.ToImmutableArray();
        Adapter = adapterMatch ?? (adapterRuleId is null ? null : new AdapterMatch(adapterRuleId, "unknown", TriState.Unknown, TriState.Unknown));
        AdapterRuleId = adapterMatch?.RuleId ?? adapterRuleId;
    }

    public ItemIdentity Identity { get; }

    public IReadOnlyList<string> Ancestors { get; }

    public string? AdapterRuleId { get; }

    /// <summary>The Adapter's statement about this Item, or null when no Adapter recognizes it (R5.8).</summary>
    public AdapterMatch? Adapter { get; }
}
