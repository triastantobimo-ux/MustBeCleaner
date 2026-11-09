namespace MustBeCleaner.Infrastructure.Journal;

/// <summary>Which required location failed.</summary>
public enum StorageLocation
{
    /// <summary>The Data_Directory.</summary>
    DataDirectory,
    /// <summary>The Quarantine destination.</summary>
    Quarantine,
}

/// <summary>Why a location was refused (R12.9).</summary>
public enum StorageFailure
{
    /// <summary>The path is empty or not absolute; relative paths could land on C:.</summary>
    NotAbsolute,
    /// <summary>The directory does not exist (or is not a directory). It is never created.</summary>
    Missing,
    /// <summary>A write probe or the database could not be written.</summary>
    Unwritable,
    /// <summary>Free space is below the configured minimum.</summary>
    InsufficientSpace,
    /// <summary>Free space could not be determined, so it is treated as unsafe.</summary>
    SpaceUnknown,
    /// <summary>The database was written by a newer schema version.</summary>
    SchemaNewer,
    /// <summary>The database file is not a valid Journal database.</summary>
    Corrupt,
}

/// <summary>
/// Thrown when the Data_Directory or Quarantine is missing, unwritable or full. The Store never
/// falls back to another location (R12.9).
/// </summary>
public sealed class StorageUnavailableException(StorageLocation location, StorageFailure failure, string path, Exception? inner = null)
    : IOException($"{location} unavailable ({failure}): '{path}'. No fallback location is used.", inner)
{
    /// <summary>The location that failed.</summary>
    public StorageLocation Location { get; } = location;

    /// <summary>The reason it failed.</summary>
    public StorageFailure Failure { get; } = failure;

    /// <summary>The path as configured.</summary>
    public string Path { get; } = path;
}

/// <summary>Thrown when a mutation record is written without a matching durable Journal entry (R16.1).</summary>
public sealed class JournalRequiredException(string message) : InvalidOperationException(message);
