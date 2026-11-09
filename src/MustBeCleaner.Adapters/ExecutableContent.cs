using System.Text.RegularExpressions;

namespace MustBeCleaner.Adapters;

/// <summary>
/// Detects content that could cause something to run (R8.2). Deliberately over-eager:
/// a false rejection costs a data edit, a false accept costs a deletion tool running arbitrary code.
/// </summary>
internal static partial class ExecutableContent
{
    /// <summary>Property names that imply execution, matched case-insensitively anywhere in the document.</summary>
    private static readonly HashSet<string> ForbiddenFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "scripts", "command", "commands", "cmd", "exec", "execute", "run", "shell", "powershell",
        "pwsh", "javascript", "js", "eval", "hook", "hooks", "preinstall", "postinstall", "args", "arguments",
        "executable", "entrypoint", "invoke", "bat", "batch", "program", "binary", "bash",
    };

    public static bool IsForbiddenFieldName(string name) => ForbiddenFieldNames.Contains(name);

    /// <summary>True when <paramref name="value"/> names a script, executable, or interpreter invocation.</summary>
    public static bool LooksExecutable(string value) => Pattern().IsMatch(value);

    [GeneratedRegex(
        @"\.(exe|bat|cmd|ps1|psm1|psd1|vbs|vbe|js|jse|wsf|msi|scr|lnk|sh|jar|reg|hta)\b"
        + @"|\b(powershell|pwsh|cscript|wscript|mshta|rundll32|regsvr32|invoke-expression|invoke-command|iex)\b"
        + @"|\bcmd(\.exe)?\s*/[ck]\b|\b(javascript|vbscript|data):|\$\(|`|&&|\|\|",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
