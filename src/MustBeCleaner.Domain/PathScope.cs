namespace MustBeCleaner.Domain;

/// <summary>Outcome of testing one path against a <see cref="PathScope"/>.</summary>
public enum ScopeDecision
{
    /// <summary>The path was refused by normalization (UNC, device, ADS, ...).</summary>
    Rejected,
    /// <summary>An exclusion covers the path. Wins over any include (R1.7).</summary>
    Excluded,
    /// <summary>An include covers the path and no exclusion does.</summary>
    Included,
    /// <summary>No include covers the path.</summary>
    NotIncluded,
}

/// <summary>
/// Include and exclusion roots. Exclusion beats include (R1.6, R1.7). Pure: no I/O.
/// </summary>
public sealed class PathScope
{
    private readonly CanonicalPath[] _includes;
    private readonly CanonicalPath[] _excludes;

    public PathScope(IEnumerable<CanonicalPath> includes, IEnumerable<CanonicalPath> excludes)
    {
        ArgumentNullException.ThrowIfNull(includes);
        ArgumentNullException.ThrowIfNull(excludes);
        _includes = includes.ToArray();
        _excludes = excludes.ToArray();
    }

    /// <summary>Decides <paramref name="raw"/>: normalizes first, then excludes before includes.</summary>
    public ScopeDecision Decide(string? raw)
    {
        var n = PathPolicy.Normalize(raw);
        if (!n.IsCanonical) return ScopeDecision.Rejected;
        var path = n.Path!.Value;
        if (_excludes.Any(e => PathPolicy.IsSameOrUnder(path, e))) return ScopeDecision.Excluded;
        return _includes.Any(i => PathPolicy.IsSameOrUnder(path, i)) ? ScopeDecision.Included : ScopeDecision.NotIncluded;
    }

    /// <summary>The rejection reason for <paramref name="raw"/>, or null when it normalizes.</summary>
    public PathRejection? Reject(string? raw) => PathPolicy.Normalize(raw).Reason;
}
