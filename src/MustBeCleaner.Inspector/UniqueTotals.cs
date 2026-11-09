namespace MustBeCleaner.Inspector;

/// <summary>Entries observed under one named Scope (R3.7).</summary>
/// <param name="Scope">Scope name.</param>
/// <param name="Entries">Entries from one or more scans of that Scope.</param>
public sealed record ScopeEntries(string Scope, IReadOnlyList<InspectedEntry> Entries);

/// <summary>Totals over unique filesystem objects.</summary>
/// <param name="UniqueFiles">Distinct file objects (R3.5, R4.6).</param>
/// <param name="KnownBytes">Sum of known sizes; null when files exist but none has a known size. Never a stand-in zero (R3.5).</param>
/// <param name="UnavailableSize">Unique files whose size could not be read (R7.6).</param>
/// <param name="HardLinkAliasesCollapsed">Extra names of already-counted objects that were skipped (R4.6).</param>
public sealed record Totals(int UniqueFiles, long? KnownBytes, int UnavailableSize, int HardLinkAliasesCollapsed)
{
    /// <summary>True when some size is unavailable, so <see cref="KnownBytes"/> is a lower bound.</summary>
    public bool IsLowerBound => UnavailableSize > 0;
}

/// <summary>Totals per Scope plus a grand total in which every object counts once.</summary>
public sealed record TotalsReport(Totals Total, IReadOnlyDictionary<string, Totals> PerScope);

/// <summary>
/// Unique-object totals. An object is identified by (volume ID, file ID), never by path, so hard links
/// count once (R4.6). Only files carry bytes: directories and reparse points add none, so a parent never
/// re-adds its children (R3.7). The grand total shares one seen-set across Scopes, so a file reached from
/// two Scopes counts once (R3.7). Unavailable size stays null and is never summed as zero (R3.5, R7.6).
/// Pure: no I/O.
/// </summary>
public static class UniqueTotals
{
    /// <summary>Computes per-Scope and grand totals.</summary>
    public static TotalsReport Compute(IReadOnlyList<ScopeEntries> scopes)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        var grand = new Accumulator();
        var per = new Dictionary<string, Totals>(StringComparer.Ordinal);
        foreach (var scope in scopes)
        {
            var own = new Accumulator();
            foreach (var e in scope.Entries.Where(e => e.Kind == EntryKind.File))
            {
                own.Add(e);
                grand.Add(e);
            }
            if (!per.TryAdd(scope.Scope, own.ToTotals()))
                throw new ArgumentException($"Scope name '{scope.Scope}' appears twice.", nameof(scopes));
        }
        return new TotalsReport(grand.ToTotals(), per);
    }

    private sealed class Accumulator
    {
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        private int _files, _unavailable, _aliases;
        private long _bytes;
        private bool _anyKnown;

        public void Add(InspectedEntry e)
        {
            var key = e.Identity is { } id ? $"{id.VolumeId}:{id.FileId}" : "P:" + e.Path.ToUpperInvariant();
            if (!_seen.Add(key)) { _aliases++; return; }
            _files++;
            if (e.Identity?.Size is { } size) { _bytes += size; _anyKnown = true; }
            else _unavailable++;
        }

        public Totals ToTotals()
            => new(_files, _anyKnown || _unavailable == 0 ? _bytes : null, _unavailable, _aliases);
    }
}
