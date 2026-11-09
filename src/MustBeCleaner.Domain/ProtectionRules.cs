namespace MustBeCleaner.Domain;

/// <summary>A protection rule that matched: its ID and the reason shown to the user (R5.6).</summary>
internal readonly record struct ProtectionMatch(string RuleId, string Reason);

/// <summary>
/// The fixed protection list (R6.1 to R6.5; ADR 0012). Matches on the canonical, upper-case path
/// only. No override input exists, so Adapter, score, custom include and administrator mode cannot
/// disable it (R5.4, R6.6).
/// </summary>
internal static class ProtectionRules
{
    private static readonly string[] SystemRoots = ["WINDOWS", "BOOT", "EFI", "RECOVERY", "SYSTEM VOLUME INFORMATION"];
    private static readonly string[] SystemFiles = ["PAGEFILE.SYS", "HIBERFIL.SYS", "SWAPFILE.SYS"];
    private static readonly string[] ProgramRoots = ["PROGRAM FILES", "PROGRAM FILES (X86)"];
    private static readonly string[] BinaryExtensions = [".EXE", ".DLL", ".SYS", ".OCX"];
    private static readonly string[] UserDataSegments =
    [
        "CREDENTIALS", "SESSIONS", "HISTORY", "MEMORY", "MEMORIES", "WORKTREES",
        ".GIT", "REPOS", "REPOSITORIES", "BACKUPS", "BACKUP", "MODELS",
    ];

    /// <param name="path">Canonical path.</param>
    /// <param name="isDirectory">The Item is a directory.</param>
    /// <param name="hasOwner">An Adapter rule claims the Item (ownership for extensionless files, R6.2).</param>
    public static ProtectionMatch? Match(CanonicalPath path, bool isDirectory, bool hasOwner)
    {
        var segments = Segments(path);

        if (segments.Length > 0 && Array.IndexOf(SystemRoots, segments[0]) >= 0)
            return new("P-WINDOWS-CRITICAL", $"Under Windows critical root '{segments[0]}' (R6.1).");
        if (segments.Length == 1 && Array.IndexOf(SystemFiles, segments[0]) >= 0)
            return new("P-WINDOWS-CRITICAL", $"System file '{segments[0]}' (R6.1).");

        if (segments.Length > 0 && Array.IndexOf(ProgramRoots, segments[0]) >= 0)
            return new("P-PROGRAM-FILES", $"Under '{segments[0]}' (R6.1).");
        if (Array.IndexOf(segments, "WINDOWSAPPS") >= 0)
            return new("P-PROGRAM-FILES", "Under 'WindowsApps' (R6.1).");

        if (IsNeverRecursiveRoot(segments))
            return new("P-NEVER-RECURSIVE-ROOT", "Local, Roaming and .codex roots are never deleted recursively (R6.5).");

        var name = segments.Length == 0 ? "" : segments[^1];
        var extension = isDirectory ? "" : Path.GetExtension(name);
        if (Array.IndexOf(BinaryExtensions, extension) >= 0)
            return new("P-BINARY", $"Executable or driver file '{extension}' (R6.2).");

        if (!isDirectory && TransactionSet.KeyOf(path.Value) is { } key)
            return new("P-TRANSACTION-SET", $"Part of database Transaction_Set '{key}'; its files are handled together or not at all (R6.4).");

        foreach (var segment in segments)
            if (Array.IndexOf(UserDataSegments, segment) >= 0)
                return new("P-USER-DATA", $"Path contains protected data area '{segment}' (credentials, sessions, history, memory, worktrees, repos, backups or models; R6.3).");

        if (!isDirectory && extension.Length == 0 && !hasOwner)
            return new("P-EXTENSIONLESS-UNOWNED", "Extensionless file with no Adapter owner may be a binary (R6.2).");

        return null;
    }

    private static bool IsNeverRecursiveRoot(string[] s) =>
        (s.Length == 4 && s[0] == "USERS" && s[2] == "APPDATA" && (s[3] == "LOCAL" || s[3] == "ROAMING"))
        || (s.Length == 3 && s[0] == "USERS" && s[2] == ".CODEX");

    private static string[] Segments(CanonicalPath path) =>
        path.Value[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries);
}
