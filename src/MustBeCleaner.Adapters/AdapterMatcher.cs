namespace MustBeCleaner.Adapters;

/// <summary>The app as detected on this machine: identity plus the installed version (R8.13).</summary>
public sealed record DetectedApp(string AppId, string Version);

/// <summary>Outcome of <see cref="AdapterMatcher.Match"/>.</summary>
public abstract record AdapterMatchResult
{
    private protected AdapterMatchResult() { }
}

/// <summary>An Adapter owns the path and its fingerprint and version match the detected app.</summary>
public sealed record AdapterMatch(Adapter Adapter) : AdapterMatchResult;

/// <summary>No Adapter owns the path. The location stays InventoryOnly (Property 18).</summary>
public sealed record NoMatch : AdapterMatchResult;

/// <summary>
/// An Adapter owns the path but the detected app or version differs from its fingerprint.
/// The Item MUST be treated as Review, never Eligible (R8.13).
/// </summary>
public sealed record VersionMismatch(Adapter Adapter, string Reason) : AdapterMatchResult;

/// <summary>Maps an Item path to the Adapter that owns it (R8.13). Pure and offline.</summary>
public static class AdapterMatcher
{
    /// <summary>
    /// Finds the Adapter owning <paramref name="itemRelativePath"/> (relative to the app data root, as in
    /// <see cref="Adapter.AllowedRelativePaths"/>). Fingerprint or version mismatch yields
    /// <see cref="VersionMismatch"/>, which callers must classify as Review (R8.13).
    /// </summary>
    public static AdapterMatchResult Match(
        IReadOnlyList<Adapter> catalog, string itemRelativePath, DetectedApp app)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(app);
        var item = Segments(itemRelativePath);
        if (item is null) return new NoMatch();

        var owners = new List<(Adapter Adapter, int Depth)>();
        foreach (var adapter in catalog)
        {
            var depth = -1;
            foreach (var allowed in adapter.AllowedRelativePaths)
            {
                var root = Segments(allowed);
                if (root is not null && root.Length <= item.Length && StartsWith(item, root))
                    depth = Math.Max(depth, root.Length);
            }
            if (depth >= 0) owners.Add((adapter, depth));
        }
        if (owners.Count == 0) return new NoMatch();

        var ordered = owners.OrderByDescending(o => o.Depth).ThenBy(o => o.Adapter.RuleId, StringComparer.Ordinal).ToList();
        var match = ordered.FirstOrDefault(o => Fits(o.Adapter, app));
        if (match.Adapter is not null) return new AdapterMatch(match.Adapter);

        var first = ordered[0].Adapter;
        var reason = !string.Equals(first.Fingerprint.AppId, app.AppId, StringComparison.OrdinalIgnoreCase)
            ? $"Detected app '{app.AppId}' differs from Adapter fingerprint '{first.Fingerprint.AppId}'."
            : $"Detected version '{app.Version}' is not among the Adapter versions ({string.Join(", ", first.Fingerprint.Versions)}).";
        return new VersionMismatch(first, reason);
    }

    private static bool Fits(Adapter a, DetectedApp app) =>
        string.Equals(a.Fingerprint.AppId, app.AppId, StringComparison.OrdinalIgnoreCase)
        && a.Fingerprint.Versions.Contains(app.Version, StringComparer.Ordinal);

    internal static bool StartsWith(string[] item, string[] root)
    {
        for (var i = 0; i < root.Length; i++)
            if (!string.Equals(item[i], root[i], StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    /// <summary>Splits a relative path; null when empty, rooted, drive-qualified or escaping.</summary>
    internal static string[]? Segments(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var p = path.Replace('/', '\\');
        if (p.StartsWith('\\') || p.Contains(':')) return null;
        var parts = p.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Any(s => s is "." or "..")) return null;
        return parts;
    }
}
