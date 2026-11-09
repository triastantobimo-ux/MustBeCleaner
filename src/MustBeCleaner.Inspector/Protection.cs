namespace MustBeCleaner.Inspector;

/// <summary>Verdict from a protection hook for one path (R6.7).</summary>
/// <param name="IsProtected">True when the path must be marked Protected.</param>
/// <param name="RuleId">Deciding rule ID when protected.</param>
public readonly record struct ProtectionVerdict(bool IsProtected, string? RuleId);

/// <summary>
/// Seam through which the Policy Core (task 2.3) supplies the full protection list. The Inspector only
/// asks; it never decides policy beyond the minimal default (R6.7).
/// </summary>
public interface IProtectionHook
{
    /// <summary>Evaluates an absolute path.</summary>
    ProtectionVerdict Evaluate(string fullPath);
}

/// <summary>
/// Minimal built-in list of Windows critical roots used until the Policy Core list replaces it (R6.7).
/// A root protects itself and everything below it; matching is case-insensitive and segment-aware.
/// </summary>
public sealed class WindowsCriticalRoots : IProtectionHook
{
    /// <summary>Rule ID reported for matches.</summary>
    public const string RuleId = "inspector.minimal.windows-critical-root";

    private readonly string[] _roots;

    /// <summary>Creates the hook for explicit absolute roots.</summary>
    public WindowsCriticalRoots(IEnumerable<string> roots)
        => _roots = roots.Where(r => !string.IsNullOrWhiteSpace(r)).Select(Trim).ToArray();

    /// <summary>Roots derived from the running Windows installation.</summary>
    public static WindowsCriticalRoots Default()
    {
        var systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "";
        return new WindowsCriticalRoots(
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            systemDrive.Length > 0 ? Path.Combine(systemDrive, "System Volume Information") : "",
            systemDrive.Length > 0 ? Path.Combine(systemDrive, "Recovery") : "",
            systemDrive.Length > 0 ? Path.Combine(systemDrive, "Boot") : "",
        ]);
    }

    /// <inheritdoc />
    public ProtectionVerdict Evaluate(string fullPath)
    {
        var path = Trim(fullPath);
        foreach (var root in _roots)
        {
            if (path.Equals(root, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase))
                return new ProtectionVerdict(true, RuleId);
        }
        return new ProtectionVerdict(false, null);
    }

    private static string Trim(string p) => p.TrimEnd('\\', '/');
}
