using MustBeCleaner.Domain;
using MustBeCleaner.TestKit.Pbt;

namespace MustBeCleaner.Domain.Tests;

/// <summary>
/// Generative safety properties 1-4 from design.md. Every property runs at least 100 iterations
/// through <see cref="MbcPropertyAttribute"/> and uses only pure Policy Core input (R21.2).
/// </summary>
public class PolicyProperties
{
    private static readonly (string Path, bool IsDirectory)[] ProtectedCases =
    [
        (@"C:\Windows\Temp\x", false),
        (@"C:\Program Files\Acme\cache.tmp", false),
        (@"D:\data\cache\tool.exe", false),
        (@"C:\Users\u\AppData\Local", true),
        (@"D:\data\repos\app\x.tmp", false),
        (@"D:\data\cache\store.db", false),
    ];

    private static readonly string[] ClassifiablePaths =
    [
        @"C:\Windows\Temp\x",
        @"C:\Program Files\Acme\cache.tmp",
        @"D:\data\cache\tool.exe",
        @"C:\Users\u\AppData\Local",
        @"D:\data\repos\app\x.tmp",
        @"D:\data\cache\store.db",
        @"D:\data\cache\blob.bin",
        @"C:\Users\u\AppData\Local\Acme\Cache\blob_1",
        @"\\server\share\x",
        @"\\.\PhysicalDrive0",
        @"C:\Users\u\file.txt:stream",
        @"C:\PROGRA~1\Acme\x",
        @"relative\path",
    ];

    private static readonly string[] ValidRawPaths =
    [
        @"C:\",
        @"c:\Users\Mika\AppData\",
        @"C:/a//b/./c/../d",
        @"\\?\c:\x\y\..\z.",
        @"D:\data\cache\file.txt",
        @"C:\Users\u\AppData\Local\Acme\Cache\blob.bin",
        @"C:\data\keep\cache\blob.bin",
    ];

    private static readonly string[] ForbiddenRawPaths =
    [
        @"\\server\share\x",
        @"\\.\PhysicalDrive0",
        @"\??\C:\x",
        @"C:\x:stream",
        @"C:\PROGRA~1\x",
        @"relative\path",
    ];

    private static TriState State(int bits) => (bits & 3) switch
    {
        1 => TriState.True,
        2 => TriState.False,
        _ => TriState.Unknown,
    };

    private static WorldFacts World(int mask) =>
        Enum.GetValues<Condition>().Aggregate(WorldFacts.None, (w, c) => w.With(c, State(mask >> (int)c)));

    private static AdapterMatch? Adapter(bool present, int appBits, int disposableBits) =>
        present ? new AdapterMatch("adapter.test", "1", State(appBits), State(disposableBits)) : null;

    private static Classification Run(string path, bool directory, AdapterMatch? adapter, WorldFacts world, RuleSet? rules = null)
    {
        var identity = new ItemIdentity("vol", "file", path, 1, DateTime.UnixEpoch,
            directory ? FileAttributes.Directory : FileAttributes.Normal);
        return PolicyEngine.Classify(new ItemFacts(identity, [], adapter?.RuleId, adapter), world, rules ?? new RuleSet("property"));
    }

    private static int Pick(int seed, int length) => (int)((uint)seed % (uint)length);

    // R5.4, R6.6: no Adapter or world observation can lift a protection rule.
    [MbcProperty(1, "Proteksi mengalahkan allow")]
    public bool Protection_beats_every_allow(int caseIndex, bool hasAdapter, int appBits, int disposableBits, int worldMask)
    {
        var (path, isDirectory) = ProtectedCases[Pick(caseIndex, ProtectedCases.Length)];
        var classification = Run(path, isDirectory, Adapter(hasAdapter, appBits, disposableBits), World(worldMask));

        return classification.Status == Status.Protected
            && classification.RuleId.StartsWith("P-", StringComparison.Ordinal);
    }

    // R5.1, R5.2, R5.3, R11.1: Eligible exactly when every condition is True; Unknown never becomes Eligible.
    [MbcProperty(2, "Unknown memblokir, Eligible hanya jika semua true")]
    public bool Unknown_blocks_and_eligible_requires_every_true(int hasAdapter, int appBits, int disposableBits, int worldMask)
    {
        var classification = Run(
            @"D:\data\cache\blob.bin", false,
            Adapter(hasAdapter % 2 == 0, appBits, disposableBits), World(worldMask));

        var everyConditionTrue = classification.Conditions.Values.All(v => v == TriState.True);
        var anyUnknown = classification.Conditions.Values.Any(v => v == TriState.Unknown);

        return (classification.Status == Status.Eligible) == everyConditionTrue
            && (!anyUnknown || classification.Status != Status.Eligible);
    }

    // R5.5, R5.6: equal inputs produce equal classifications and equal hashes.
    [MbcProperty(3, "Klasifikasi deterministik")]
    public bool Classification_is_deterministic(int pathIndex, bool directory, bool hasAdapter, int appBits, int disposableBits, int worldMask)
    {
        var path = ClassifiablePaths[Pick(pathIndex, ClassifiablePaths.Length)];
        var adapter = Adapter(hasAdapter, appBits, disposableBits);
        var world = World(worldMask);

        var first = Run(path, directory, adapter, world);
        var second = Run(path, directory, adapter, world);

        return first.Equals(second) && first.GetHashCode() == second.GetHashCode();
    }

    // R1.7, R7.4, R7.5: exclusion beats include, forbidden paths are refused, normalization is idempotent.
    [MbcProperty(4, "Exclusion mengalahkan include; path terlarang ditolak; normalisasi idempoten")]
    public bool Exclusion_forbidden_path_and_idempotent_normalization(int pathIndex, int forbiddenIndex, int keepIndex)
    {
        var include = PathPolicy.Normalize(@"C:\data").Path!.Value;
        var exclude = PathPolicy.Normalize(@"C:\data\keep").Path!.Value;
        var scope = new PathScope([include], [exclude]);
        var underExclusion = $@"C:\data\keep\cache\file{Pick(keepIndex, 97)}.bin";
        if (scope.Decide(underExclusion) != ScopeDecision.Excluded) return false;

        var forbidden = ForbiddenRawPaths[Pick(forbiddenIndex, ForbiddenRawPaths.Length)];
        if (PathPolicy.Normalize(forbidden).IsCanonical) return false;
        var refused = Run(forbidden, false, null, WorldFacts.None);
        if (refused.Status != Status.Protected || refused.RuleId != "F-FORBIDDEN-PATH") return false;

        var raw = ValidRawPaths[Pick(pathIndex, ValidRawPaths.Length)];
        var once = PathPolicy.Normalize(raw);
        if (!once.IsCanonical) return false;
        var twice = PathPolicy.Normalize(once.Path!.Value.Value);
        return twice.IsCanonical && twice.Path!.Value.Value == once.Path!.Value.Value;
    }
}
