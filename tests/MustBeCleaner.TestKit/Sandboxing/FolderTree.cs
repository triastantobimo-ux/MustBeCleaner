using FsCheck;
using FsCheck.Fluent;

namespace MustBeCleaner.TestKit.Sandboxing;

/// <summary>One folder or file in a <see cref="FolderTree"/>. Paths are relative, '\'-separated.</summary>
public sealed record TreeEntry(string Path, bool IsDirectory, byte[] Content);

/// <summary>A valid folder tree: unique case-insensitively, parents exist, no file has children.</summary>
public sealed record FolderTree(IReadOnlyList<TreeEntry> Entries)
{
    /// <summary>Normalizes raw (possibly conflicting) entries into a tree that can exist on a case-insensitive disk.</summary>
    public static FolderTree From(IEnumerable<TreeEntry> raw)
    {
        var kinds = new Dictionary<string, TreeEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in raw)
        {
            var parts = entry.Path.Split('\\');
            var parents = Enumerable.Range(1, parts.Length - 1).Select(n => string.Join('\\', parts.Take(n))).ToList();
            var blocked = parents.Any(p => kinds.TryGetValue(p, out var k) && !k.IsDirectory)
                          || kinds.ContainsKey(entry.Path);
            if (blocked) continue;
            foreach (var p in parents.Where(p => !kinds.ContainsKey(p))) kinds[p] = new TreeEntry(p, true, []);
            kinds[entry.Path] = entry;
        }
        return new FolderTree(kinds.Values.OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase).ToList());
    }
}

public static class SandboxTreeExtensions
{
    public static void Materialize(this Sandbox sandbox, FolderTree tree)
    {
        foreach (var e in tree.Entries)
        {
            if (e.IsDirectory) sandbox.CreateDirectory(e.Path);
            else sandbox.WriteFile(e.Path, e.Content);
        }
    }
}

/// <summary>FsCheck generator for folder trees with case variants, 8.3-like names, Unicode, and empty folders. Register via [MbcProperty(..., Arbitrary = [typeof(FolderTreeArbitrary)])].</summary>
public static class FolderTreeArbitrary
{
    private static readonly string[] Names =
    [
        "docs", "Docs", "DOCS", "Readme.TXT", "readme.txt", "PROGRA~1", "SETUP~1.EXE", "NEWFOL~1",
        "r\u00e9sum\u00e9", "\u65e5\u672c\u8a9e", "caf\u00e9.txt", "\u0414\u0430\u043d\u043d\u044b\u0435", "a b", "data.bin", "x",
    ];

    public static Gen<FolderTree> Generator()
    {
        var segment = Gen.Elements(Names);
        var path = Gen.Choose(1, 3).SelectMany(depth => Gen.ArrayOf(segment, depth)).Select(s => string.Join('\\', s));
        var entry = path.SelectMany(p => Gen.Elements(true, false).SelectMany(isDir =>
            Gen.ArrayOf(Gen.Choose(0, 255).Select(i => (byte)i), isDir ? 0 : 8)
               .Select(content => new TreeEntry(p, isDir, content))));
        return Gen.Choose(0, 8).SelectMany(n => Gen.ArrayOf(entry, n)).Select(FolderTree.From);
    }

    public static Arbitrary<FolderTree> Trees() => Generator().ToArbitrary();
}
