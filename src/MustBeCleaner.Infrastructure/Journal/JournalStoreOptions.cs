namespace MustBeCleaner.Infrastructure.Journal;

/// <summary>Settings for <see cref="JournalStore.Open"/>.</summary>
/// <param name="DataDirectory">Absolute path of an existing Data_Directory (R12.9, R16.5). Never created.</param>
public sealed record JournalStoreOptions(string DataDirectory)
{
    /// <summary>Default minimum free space on the Data_Directory volume: 100 MiB (R2.6).</summary>
    public const long DefaultMinimumFreeBytes = 100L * 1024 * 1024;

    /// <summary>Quarantine destination, when one is configured. Must already exist (R12.9).</summary>
    public string? QuarantineDirectory { get; init; }

    /// <summary>Free space required on the Data_Directory volume before the Store opens.</summary>
    public long MinimumFreeBytes { get; init; } = DefaultMinimumFreeBytes;
}
