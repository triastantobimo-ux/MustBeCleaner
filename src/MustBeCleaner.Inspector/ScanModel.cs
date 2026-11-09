using MustBeCleaner.Domain;

namespace MustBeCleaner.Inspector;

/// <summary>Basic shape of an inspected entry.</summary>
public enum EntryKind
{
    /// <summary>Regular file.</summary>
    File,
    /// <summary>Directory that was (or would be) traversed.</summary>
    Directory,
    /// <summary>Any reparse point; recorded, never traversed (R7.1, R7.2).</summary>
    ReparsePoint,
}

/// <summary>One entry observed during a scan. Metadata only (R1.1).</summary>
/// <param name="Path">Absolute path as enumerated.</param>
/// <param name="Kind">File, directory or reparse point.</param>
/// <param name="Reparse">Reparse classification; <see cref="ReparseKind.None"/> for ordinary entries.</param>
/// <param name="Identity">Object identity, or null when the handle could not be opened (R7.6).</param>
/// <param name="LinkCount">Hard link count, or null when identity is unavailable (R4.6).</param>
/// <param name="IsProtected">True when a protection hook matched (R6.7).</param>
/// <param name="ProtectionRuleId">Deciding rule ID when protected.</param>
public sealed record InspectedEntry(
    string Path,
    EntryKind Kind,
    ReparseKind Reparse,
    ItemIdentity? Identity,
    uint? LinkCount,
    bool IsProtected,
    string? ProtectionRuleId);

/// <summary>Outcome of scanning one root.</summary>
public enum CoverageStatus
{
    /// <summary>Everything under the root was enumerated.</summary>
    Complete,
    /// <summary>Enumeration finished but some directories were unreadable.</summary>
    Partial,
    /// <summary>Scan was cancelled; results are a lower bound (R2.3).</summary>
    Cancelled,
    /// <summary>Root refused: relative, UNC, device path or alternate data stream (R7.5).</summary>
    Rejected,
    /// <summary>Root does not exist.</summary>
    NotFound,
}

/// <summary>Coverage for one root (R2.5).</summary>
public sealed record RootCoverage(string Root, CoverageStatus Status, int EntryCount, int ReparseSkipped, int AccessDenied);

/// <summary>A path the scan could not read because access was denied (R6.8). No escalation was attempted.</summary>
public sealed record AccessDeniedRecord(string Path);

/// <summary>Everything a scan observed.</summary>
public sealed record ScanResult(
    IReadOnlyList<InspectedEntry> Entries,
    IReadOnlyList<RootCoverage> Coverage,
    IReadOnlyList<AccessDeniedRecord> AccessDenied)
{
    /// <summary>True only when every root finished <see cref="CoverageStatus.Complete"/>.</summary>
    public bool IsComplete => Coverage.All(c => c.Status == CoverageStatus.Complete);
}
