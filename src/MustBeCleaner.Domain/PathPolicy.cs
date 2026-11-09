using System.Text.RegularExpressions;

namespace MustBeCleaner.Domain;

/// <summary>Why a raw path was refused by <see cref="PathPolicy.Normalize"/> (R7.4, R7.5).</summary>
public enum PathRejection
{
    /// <summary>Null, empty or whitespace.</summary>
    Empty,
    /// <summary>Not drive-absolute (relative, rooted without drive, or drive-relative like <c>C:x</c>).</summary>
    Relative,
    /// <summary>UNC path, including <c>\\?\UNC\</c> (R7.4).</summary>
    Unc,
    /// <summary>Device or NT-namespace path such as <c>\\.\X</c>, <c>\\?\Volume{..}</c>, <c>\??\</c> (R7.4).</summary>
    DevicePath,
    /// <summary>A colon after the drive letter: alternate data stream (R7.4).</summary>
    AlternateDataStream,
    /// <summary>Wildcard, reserved or control character.</summary>
    InvalidCharacter,
    /// <summary><c>..</c> climbs above the drive root.</summary>
    EscapesRoot,
    /// <summary>
    /// A segment has 8.3 short-name shape (<c>NAME~N.EXT</c>). It cannot be expanded without I/O,
    /// so the Policy Core refuses to guess (R7.5; ADR 0011).
    /// </summary>
    UnresolvedShortName,
}

/// <summary>A path in canonical form. Only <see cref="PathPolicy"/> creates one.</summary>
public readonly record struct CanonicalPath
{
    internal CanonicalPath(string value) => Value = value;

    /// <summary>Upper-case, backslash-separated, drive-absolute, no trailing separator except at the root.</summary>
    public string Value { get; }

    public override string ToString() => Value;
}

/// <summary>Result of normalization: a <see cref="CanonicalPath"/> or a <see cref="PathRejection"/>, never both.</summary>
public sealed record PathNormalization
{
    private PathNormalization(CanonicalPath? path, PathRejection? reason)
    {
        Path = path;
        Reason = reason;
    }

    public bool IsCanonical => Path is not null;
    public CanonicalPath? Path { get; }
    public PathRejection? Reason { get; }

    internal static PathNormalization Ok(string value) => new(new CanonicalPath(value), null);
    internal static PathNormalization Reject(PathRejection reason) => new(null, reason);
}

/// <summary>
/// Pure path normalization (R7.4, R7.5). No I/O: nothing here touches the filesystem, so 8.3 names
/// are flagged, not expanded (ADR 0011).
/// </summary>
public static partial class PathPolicy
{
    // Base up to 6 chars + ~N, optional extension up to 3 chars: the shape Windows generates.
    [GeneratedRegex(@"^[^.~]{1,6}~[0-9]+(\.[^.]{1,3})?$", RegexOptions.CultureInvariant)]
    private static partial Regex ShortName();

    /// <summary>
    /// Normalizes <paramref name="raw"/> to a canonical path or rejects it (R7.4, R7.5).
    /// Result is idempotent: normalizing a canonical path returns it unchanged.
    /// </summary>
    public static PathNormalization Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return PathNormalization.Reject(PathRejection.Empty);

        var p = raw.Replace('/', '\\');

        if (p.StartsWith(@"\??\", StringComparison.Ordinal)) return PathNormalization.Reject(PathRejection.DevicePath);

        if (p.StartsWith(@"\\", StringComparison.Ordinal))
        {
            if (p.StartsWith(@"\\?\", StringComparison.Ordinal))
            {
                var rest = p[4..];
                if (rest.StartsWith(@"UNC\", StringComparison.OrdinalIgnoreCase)) return PathNormalization.Reject(PathRejection.Unc);
                if (!(rest.Length >= 2 && char.IsAsciiLetter(rest[0]) && rest[1] == ':'))
                    return PathNormalization.Reject(PathRejection.DevicePath);
                p = rest;
            }
            else if (p.StartsWith(@"\\.\", StringComparison.Ordinal))
            {
                return PathNormalization.Reject(PathRejection.DevicePath);
            }
            else
            {
                return PathNormalization.Reject(PathRejection.Unc);
            }
        }

        if (p.Length < 3 || !char.IsAsciiLetter(p[0]) || p[1] != ':' || p[2] != '\\')
            return PathNormalization.Reject(PathRejection.Relative);

        var segments = new List<string>();
        foreach (var raw0 in p[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw0 == ".") continue;
            if (raw0 == "..")
            {
                if (segments.Count == 0) return PathNormalization.Reject(PathRejection.EscapesRoot);
                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            if (raw0.Contains(':')) return PathNormalization.Reject(PathRejection.AlternateDataStream);
            if (raw0.IndexOfAny(['<', '>', '"', '|', '?', '*']) >= 0 || raw0.Any(char.IsControl))
                return PathNormalization.Reject(PathRejection.InvalidCharacter);

            // Windows ignores trailing dots and spaces in a name.
            var seg = raw0.TrimEnd('.', ' ');
            if (seg.Length == 0) continue;
            if (ShortName().IsMatch(seg)) return PathNormalization.Reject(PathRejection.UnresolvedShortName);
            segments.Add(seg.ToUpperInvariant());
        }

        var drive = char.ToUpperInvariant(p[0]) + @":\";
        return PathNormalization.Ok(drive + string.Join('\\', segments));
    }

    /// <summary>True when <paramref name="path"/> equals <paramref name="root"/> or lies below it, on a segment boundary.</summary>
    internal static bool IsSameOrUnder(CanonicalPath path, CanonicalPath root)
    {
        var a = path.Value;
        var r = root.Value;
        if (!a.StartsWith(r, StringComparison.Ordinal)) return false;
        return a.Length == r.Length || r.EndsWith('\\') || a[r.Length] == '\\';
    }
}
