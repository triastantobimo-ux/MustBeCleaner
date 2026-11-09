namespace IdentityBinding;

/// <summary>One link of the path from the root down to the target, as seen at capture time.</summary>
public sealed record AncestorEntry(string Path, ObjectIdentity Identity);

public static class Ancestors
{
    /// <summary>
    /// Identity of every component from the drive root to the path itself (inclusive).
    /// Each component is opened individually without following reparse points.
    /// Throws if any component is a reparse point (junction, symlink, mount point, cloud, unknown tag).
    /// </summary>
    public static IReadOnlyList<AncestorEntry> CaptureChain(string fullPath)
    {
        var chain = new List<AncestorEntry>();
        var root = System.IO.Path.GetPathRoot(fullPath)!;
        var rest = fullPath[root.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
        var cur = root;
        chain.Add(new(cur, Identity.Capture(cur)));
        foreach (var part in rest)
        {
            cur = System.IO.Path.Combine(cur, part);
            var id = Identity.Capture(cur);
            if (id.IsReparsePoint) throw new ReparsePointInChainException(cur);
            chain.Add(new(cur, id));
        }
        return chain;
    }

    /// <summary>True when every component still resolves to the same object as at capture time.</summary>
    public static bool StillMatches(IReadOnlyList<AncestorEntry> captured)
    {
        try
        {
            foreach (var e in captured)
            {
                var now = Identity.Capture(e.Path);
                if (now.IsReparsePoint || !now.SameObjectAs(e.Identity)) return false;
            }
            return true;
        }
        catch (IOException) { return false; }
    }
}

public sealed class ReparsePointInChainException(string at) : Exception("reparse point in chain at " + at)
{
    public string At { get; } = at;
}
