namespace MustBeCleaner.Domain;

/// <summary>
/// A versioned set of policy rules (R5.5). Every <see cref="Classification"/> records the
/// version it was made with (R5.6). The protection list is fixed in code (ADR 0012); the set
/// carries the version and the user exclusions. Immutable.
/// </summary>
public sealed record RuleSet
{
    /// <param name="version">Non-blank version label, for example "2025.1".</param>
    /// <param name="exclusions">User exclusions; they beat every other rule (R1.6, R1.7).</param>
    public RuleSet(string version, IEnumerable<CanonicalPath>? exclusions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        Version = version;
        Exclusions = (exclusions ?? []).ToArray();
    }

    public string Version { get; }

    public IReadOnlyList<CanonicalPath> Exclusions { get; }
}
