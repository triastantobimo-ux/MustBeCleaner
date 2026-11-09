namespace MustBeCleaner.Infrastructure.Journal;

/// <summary>Fail-closed checks for the Data_Directory and Quarantine (R12.9). Never creates directories.</summary>
internal static class StorageGuard
{
    /// <summary>Returns the normalized absolute path or throws <see cref="StorageUnavailableException"/>.</summary>
    public static string Validate(StorageLocation location, string? path, long minimumFreeBytes)
    {
        var configured = path ?? string.Empty;
        if (string.IsNullOrWhiteSpace(configured) || !Path.IsPathFullyQualified(configured))
            throw new StorageUnavailableException(location, StorageFailure.NotAbsolute, configured);

        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(configured));
        if (!Directory.Exists(full))
            throw new StorageUnavailableException(location, StorageFailure.Missing, configured);

        var probe = Path.Combine(full, ".mbc-probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new StorageUnavailableException(location, StorageFailure.Unwritable, configured, e);
        }

        long free;
        try { free = new DriveInfo(Path.GetPathRoot(full)!).AvailableFreeSpace; }
        catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException)
        {
            throw new StorageUnavailableException(location, StorageFailure.SpaceUnknown, configured, e);
        }
        if (free < minimumFreeBytes)
            throw new StorageUnavailableException(location, StorageFailure.InsufficientSpace, configured);

        return full;
    }
}
