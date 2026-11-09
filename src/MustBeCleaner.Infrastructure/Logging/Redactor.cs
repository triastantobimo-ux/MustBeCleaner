using System.Text.RegularExpressions;

namespace MustBeCleaner.Infrastructure.Logging;

/// <summary>
/// Removes token, secret and credential shaped substrings from text before it can reach a log (R16.3).
/// Pattern based, so it is a second line of defence: <see cref="SafeLog"/> already refuses free text
/// for identifiers and reason codes. See docs/decisions/0040-log-redaction-approach.md.
/// </summary>
public static partial class Redactor
{
    /// <summary>Replacement text for anything removed.</summary>
    public const string Mask = "[REDACTED]";

    /// <summary>Returns <paramref name="text"/> with secret-shaped parts replaced by <see cref="Mask"/>.</summary>
    public static string Scrub(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var s = PrivateKey().Replace(text, Mask);
        s = UrlUserInfo().Replace(s, "://" + Mask + "@");
        s = AuthScheme().Replace(s, "$1 " + Mask);
        s = KeyValueSecret().Replace(s, m => m.Groups[1].Value + "=" + Mask);
        s = Jwt().Replace(s, Mask);
        s = KnownPrefix().Replace(s, Mask);
        s = Opaque().Replace(s, m => LooksOpaque(m.Value) ? Mask : m.Value);
        return s;
    }

    // Long tokens made of letters and digits are treated as secrets. Pure hex (checksums) is an identifier and stays.
    private static bool LooksOpaque(string v) =>
        v.Any(char.IsLetter) && v.Any(char.IsDigit) && !HexOnly().IsMatch(v);

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?(-----END [A-Z ]*PRIVATE KEY-----|$)")]
    private static partial Regex PrivateKey();

    [GeneratedRegex(@"://[^/\s:@]+:[^@\s/]+@")]
    private static partial Regex UrlUserInfo();

    [GeneratedRegex(@"\b(Bearer|Basic)\s+[A-Za-z0-9._~+/=\-]{8,}", RegexOptions.IgnoreCase)]
    private static partial Regex AuthScheme();

    [GeneratedRegex(@"\b(password|passwd|pwd|secret|client[_-]?secret|token|access[_-]?token|api[_-]?key|access[_-]?key|authorization|credentials?|sig|sas)\b\s*[=:]\s*(""[^""]*""|'[^']*'|[^\s&;,""']+)", RegexOptions.IgnoreCase)]
    private static partial Regex KeyValueSecret();

    [GeneratedRegex(@"\beyJ[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]*")]
    private static partial Regex Jwt();

    [GeneratedRegex(@"\b(?:gh[pousr]_[A-Za-z0-9]{20,}|AKIA[0-9A-Z]{16}|xox[baprs]-[A-Za-z0-9\-]{10,}|sk-[A-Za-z0-9]{20,})")]
    private static partial Regex KnownPrefix();

    [GeneratedRegex(@"[A-Za-z0-9+/=_\-]{40,}")]
    private static partial Regex Opaque();

    [GeneratedRegex(@"^[0-9a-fA-F]+$")]
    private static partial Regex HexOnly();
}
