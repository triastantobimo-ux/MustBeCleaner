using MustBeCleaner.Domain;
using MustBeCleaner.TestKit.Pbt;

namespace MustBeCleaner.Inspector.Tests;

/// <summary>Property 5: unique byte totals never overlap across objects, hard links, parents or Scopes (R3.7, R4.6, R10.2, R14.4, R18.7).</summary>
public class UniqueTotalsProperties
{
    private static int Pick(int seed, int length) => (int)((uint)seed % (uint)length);

    private static InspectedEntry Entry(int seed, int index)
    {
        var path = $@"C:\data\f{index}.bin";
        var kind = seed % 7 == 0 ? EntryKind.Directory
            : seed % 11 == 0 ? EntryKind.ReparsePoint
            : EntryKind.File;
        ItemIdentity? identity = seed % 5 == 0
            ? null
            : new ItemIdentity(
                $"V{Pick(seed, 3)}",
                $"F{Pick(seed >> 2, 4)}",
                path,
                seed % 13 == 0 ? null : Pick(seed, 1000),
                DateTime.UnixEpoch,
                kind == EntryKind.Directory ? FileAttributes.Directory : FileAttributes.Normal);
        return new InspectedEntry(path, kind, ReparseKind.None, identity, null, false, null);
    }

    private static string Key(InspectedEntry e) =>
        e.Identity is { } id ? $"{id.VolumeId}:{id.FileId}" : "P:" + e.Path.ToUpperInvariant();

    [MbcProperty(5, "Total byte tanpa overlap")]
    public bool Unique_totals_count_each_object_once(int[] seeds)
    {
        var entries = seeds.Select(Entry).ToList();
        var scopes = new List<ScopeEntries>
        {
            new("a", entries.Where((_, i) => i % 2 == 0).ToList()),
            new("b", entries.Where((_, i) => i % 2 == 1).ToList()),
        };
        var report = UniqueTotals.Compute(scopes);

        var files = entries.Where(e => e.Kind == EntryKind.File).ToList();
        var unique = new Dictionary<string, long?>(StringComparer.Ordinal);
        foreach (var e in scopes.SelectMany(s => s.Entries).Where(e => e.Kind == EntryKind.File))
            unique.TryAdd(Key(e), e.Identity?.Size);

        var knownSizes = unique.Values.Where(s => s is not null).Select(s => s!.Value).ToList();
        var expectedKnown = knownSizes.Count == 0 && unique.Count > 0 ? (long?)null : knownSizes.Sum();
        var expectedUnavailable = unique.Values.Count(s => s is null);
        var expectedAliases = files.Count - unique.Count;

        var perScopeBytes = report.PerScope.Values.Sum(t => t.KnownBytes ?? 0);

        var ok = report.Total.UniqueFiles == unique.Count
            && report.Total.KnownBytes == expectedKnown
            && report.Total.UnavailableSize == expectedUnavailable
            && report.Total.HardLinkAliasesCollapsed == expectedAliases
            && report.Total.UniqueFiles <= report.PerScope.Values.Sum(t => t.UniqueFiles)
            && (report.Total.KnownBytes is not { } known || known <= perScopeBytes);
        Assert.True(ok,
            $"seeds=[{string.Join(",", seeds)}] files={files.Count} unique={unique.Count} " +
            $"total={report.Total} expectedKnown={expectedKnown} expectedUnavailable={expectedUnavailable} " +
            $"expectedAliases={expectedAliases} perScopeBytes={perScopeBytes}");
        return true;
    }
}
