namespace MustBeCleaner.Domain;

/// <summary>
/// Recognizes the files of one database Transaction_Set: the database file plus its WAL, SHM and
/// journal sidecars. They are protected together and never separately (R6.4; ADR 0012). Pure.
/// </summary>
public static class TransactionSet
{
    private static readonly string[] DatabaseExtensions = [".DB", ".SQLITE", ".SQLITE3"];
    private static readonly string[] SidecarSuffixes = ["-WAL", "-SHM", "-JOURNAL"];

    /// <summary>
    /// The upper-case path of the database file that owns <paramref name="path"/>, or null when the
    /// file is not part of a Transaction_Set. A database and its sidecars return the same key (R6.4).
    /// </summary>
    public static string? KeyOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var key = path.Replace('/', '\\').ToUpperInvariant();
        foreach (var suffix in SidecarSuffixes)
        {
            if (key.EndsWith(suffix, StringComparison.Ordinal))
            {
                key = key[..^suffix.Length];
                break;
            }
        }
        return Array.Exists(DatabaseExtensions, e => key.EndsWith(e, StringComparison.Ordinal)) ? key : null;
    }
}
