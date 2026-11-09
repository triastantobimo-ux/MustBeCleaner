using MustBeCleaner.Infrastructure.Journal;

namespace MustBeCleaner.Infrastructure.Retention;

/// <summary>
/// Retention limits for reports and scan history (R16.6, R22.2). A <c>null</c> limit switches that
/// limit off. Defaults are bounded. See docs/decisions/0041-retention-rules.md.
/// </summary>
public sealed record RetentionPolicy
{
    /// <summary>Newest reports to keep.</summary>
    public int? MaxReports { get; init; } = 50;

    /// <summary>Reports older than this are removed.</summary>
    public TimeSpan? MaxReportAge { get; init; }

    /// <summary>Newest scan runs to keep (at least 1).</summary>
    public int? MaxScanRuns { get; init; } = 20;

    /// <summary>Scan runs older than this are removed.</summary>
    public TimeSpan? MaxScanAge { get; init; }
}

/// <summary>What <see cref="RetentionService.Apply"/> did.</summary>
/// <param name="ReportsDeleted">Report files removed.</param>
/// <param name="ScanRunsDeleted">Scan runs removed with their dependent rows.</param>
/// <param name="ScanRunsHeldForRecovery">Scan runs that were due but kept because an Item recovery is unfinished (R15.6).</param>
public readonly record struct RetentionResult(int ReportsDeleted, int ScanRunsDeleted, int ScanRunsHeldForRecovery);

/// <summary>
/// Applies <see cref="RetentionPolicy"/> to reports and scan history. It never deletes Journal rows
/// (the Journal is append-only), never deletes a scan run that holds an unfinished recovery (R15.6),
/// and only deletes plain files directly inside <c>reports/</c> of the Data_Directory (R16.5).
/// </summary>
public static class RetentionService
{
    /// <summary>Folder inside the Data_Directory that holds reports.</summary>
    public const string ReportsDirectoryName = "reports";

    /// <summary>Applies the policy once.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A limit is negative, or <c>MaxScanRuns</c> is below 1.</exception>
    public static RetentionResult Apply(JournalStore store, RetentionPolicy policy, TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(policy);
        Validate(policy);
        var now = (clock ?? TimeProvider.System).GetUtcNow();

        var reports = PruneReports(store.DataDirectory, policy, now);
        var history = store.ListScanHistory().OrderByDescending(h => h.StartedAt).ToList();
        var due = new List<ScanHistoryEntry>();
        for (var i = 0; i < history.Count; i++)
        {
            var tooMany = policy.MaxScanRuns is { } max && i >= max;
            var tooOld = policy.MaxScanAge is { } age && now - history[i].StartedAt > age;
            if (tooMany || tooOld) due.Add(history[i]);
        }
        var held = due.Count(h => h.HoldsUnfinishedRecovery);
        var deleted = store.DeleteScanRuns(due.Where(h => !h.HoldsUnfinishedRecovery).Select(h => h.ScanId).ToList());
        return new RetentionResult(reports, deleted, held);
    }

    private static int PruneReports(string dataDirectory, RetentionPolicy policy, DateTimeOffset now)
    {
        var dir = Path.Combine(dataDirectory, ReportsDirectoryName);
        var info = new DirectoryInfo(dir);
        if (!info.Exists || info.Attributes.HasFlag(FileAttributes.ReparsePoint)) return 0;

        var prefix = Path.GetFullPath(dir) + Path.DirectorySeparatorChar;
        var files = info.EnumerateFiles()
            .Where(f => !f.Attributes.HasFlag(FileAttributes.ReparsePoint))
            .Where(f => Path.GetFullPath(f.FullName).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .ToList();

        var deleted = 0;
        for (var i = 0; i < files.Count; i++)
        {
            var tooMany = policy.MaxReports is { } max && i >= max;
            var tooOld = policy.MaxReportAge is { } age && now - new DateTimeOffset(files[i].LastWriteTimeUtc, TimeSpan.Zero) > age;
            if (!tooMany && !tooOld) continue;
            try
            {
                files[i].Delete();
                deleted++;
            }
            catch (IOException) { /* in use: retry on the next run */ }
            catch (UnauthorizedAccessException) { /* no escalation (R6.8) */ }
        }
        return deleted;
    }

    private static void Validate(RetentionPolicy p)
    {
        if (p.MaxReports is < 0) throw new ArgumentOutOfRangeException(nameof(p), "MaxReports must not be negative.");
        if (p.MaxScanRuns is < 1) throw new ArgumentOutOfRangeException(nameof(p), "MaxScanRuns must be at least 1.");
        if (p.MaxReportAge is { } a && a < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(p), "MaxReportAge must not be negative.");
        if (p.MaxScanAge is { } b && b < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(p), "MaxScanAge must not be negative.");
    }
}
