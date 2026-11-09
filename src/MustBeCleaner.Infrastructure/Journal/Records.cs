namespace MustBeCleaner.Infrastructure.Journal;

// Persistence record types (R16.4). They mirror design.md "Data Models" and are deliberately
// independent of the Domain project; a later task maps them to Domain types.

/// <summary>Executor stage written to the Journal per Item (R12.2).</summary>
public enum JournalStage
{
    /// <summary>Intent recorded, nothing copied yet.</summary>
    Prepared,
    /// <summary>Copy to the destination finished.</summary>
    Copied,
    /// <summary>Checksum, size and metadata verified (R12.3).</summary>
    Verified,
    /// <summary>Source released; the move is complete.</summary>
    SourceReleased,
}

/// <summary>Outcome of one action on one Item (R14.5).</summary>
public enum ActionOutcome
{
    /// <summary>Moved to Quarantine.</summary>
    Quarantined,
    /// <summary>Sent to the Recycle Bin.</summary>
    Recycled,
    /// <summary>Skipped because the owning application is active (R11.4).</summary>
    SkippedActive,
    /// <summary>Skipped because the Item changed since preview (R11.3).</summary>
    SkippedChanged,
    /// <summary>Denied by policy (R11.6).</summary>
    DeniedPolicy,
    /// <summary>The action failed.</summary>
    Failed,
    /// <summary>The user canceled before this Item.</summary>
    Canceled,
    /// <summary>State is unknown after a crash; the user must resolve it (R12.6).</summary>
    RecoveryRequired,
}

/// <summary>Classification status (R5.1).</summary>
public enum ClassificationStatus
{
    /// <summary>Never touched.</summary>
    Protected,
    /// <summary>Needs human review.</summary>
    Review,
    /// <summary>All eligibility conditions are true.</summary>
    Eligible,
    /// <summary>Counted and shown, never acted on.</summary>
    InventoryOnly,
}

/// <summary>Kind of a duplicate group (R18, R19).</summary>
public enum DuplicateKind
{
    /// <summary>Exact duplicate files.</summary>
    File,
    /// <summary>Exact duplicate folders.</summary>
    Folder,
}

/// <summary>One scan run (R2). <paramref name="Scope"/> and <paramref name="Caveats"/> are stored as JSON arrays.</summary>
public sealed record ScanRunRecord(
    string ScanId,
    IReadOnlyList<string> Scope,
    DateTimeOffset StartedAt,
    string Status,
    string PolicyVersion,
    IReadOnlyList<string> Caveats);

/// <summary>Per-root scan coverage (R3.1).</summary>
public sealed record ScanCoverageRecord(
    string ScanId,
    string Root,
    string Status,
    long EntryCount,
    long ReparseSkipped,
    long AccessDenied,
    long DurationMs);

/// <summary>
/// A discovered Item with its identity (R7.6). <paramref name="SizeBytes"/> is <c>null</c> for
/// "unavailable"; it is never stored as zero (R3.5).
/// </summary>
public sealed record DiscoveredItemRecord(
    string ItemId,
    string ScanId,
    string VolumeId,
    string FileId,
    string CanonicalPath,
    long? SizeBytes,
    DateTimeOffset? ModifiedAt,
    int Attributes);

/// <summary>Evidence tying an Item to an Adapter rule (R8).</summary>
public sealed record AppOwnershipEvidenceRecord(
    string ItemId,
    string AdapterRuleId,
    string AdapterVersion,
    IReadOnlyList<string> Evidence);

/// <summary>Versioned rule reference (R5.6).</summary>
public sealed record RuleVersionRecord(string RuleId, string Version, string SourceRef, string Hash);

/// <summary>Tri-state classification result with rule and version (R5.6). <paramref name="Conditions"/> is opaque JSON.</summary>
public sealed record ClassificationRecord(
    string ItemId,
    ClassificationStatus Status,
    string RuleId,
    string RuleVersion,
    IReadOnlyList<string> Reasons,
    string Conditions);

/// <summary>Exact duplicate group (R18, R19).</summary>
public sealed record DuplicateGroupRecord(
    string GroupId,
    DuplicateKind Kind,
    IReadOnlyList<string> Members,
    string? KeeperId,
    bool Verified);

/// <summary>The Keeper the user chose for a group (R18.4).</summary>
public sealed record KeeperDecisionRecord(string GroupId, string KeeperItemId);

/// <summary>A cleanup Plan; <paramref name="Totals"/> is opaque JSON (R10).</summary>
public sealed record CleanupPlanRecord(string PlanId, string Totals);

/// <summary>An Item in a Plan with its evidence snapshot (R10.6).</summary>
public sealed record PlanItemRecord(string PlanId, string ItemId, string EvidenceSnapshot);

/// <summary>Audit exception: an Item skipped or denied, with a reason code and never file content (R16.2, R16.3).</summary>
public sealed record ExceptionEntryRecord(string ItemId, string ReasonCode, DateTimeOffset At);

/// <summary>
/// A Journal entry written before a filesystem mutation (R16.1). <paramref name="Detail"/> must not
/// contain file content, tokens or secrets (R16.3).
/// </summary>
public sealed record JournalEntry(
    string ItemId,
    string ManifestId,
    JournalStage Stage,
    DateTimeOffset At,
    string? Detail = null);

/// <summary>A Journal entry as stored, with its store-assigned id.</summary>
public sealed record StoredJournalEntry(long EntryId, JournalEntry Entry);

/// <summary>
/// Proof that a durable Journal entry exists. Only <see cref="JournalStore.AppendJournal"/> can
/// create one; mutation records require it (R16.1).
/// </summary>
public sealed class JournalReceipt
{
    internal JournalReceipt(long entryId, string itemId, JournalStage stage)
    {
        EntryId = entryId;
        ItemId = itemId;
        Stage = stage;
    }

    /// <summary>Store-assigned Journal entry id.</summary>
    public long EntryId { get; }

    /// <summary>Item the entry covers.</summary>
    public string ItemId { get; }

    /// <summary>Stage the entry recorded.</summary>
    public JournalStage Stage { get; }
}

/// <summary>An action attempt on one Item (R13). Needs a receipt for the same Item and stage.</summary>
public sealed record ActionAttemptRecord(
    string ItemId,
    string ManifestId,
    JournalStage Stage,
    ActionOutcome Outcome,
    string? Reason);

/// <summary>An Item held in Quarantine (R12.1). Needs a receipt for the same Item.</summary>
public sealed record QuarantineEntryRecord(
    string EntryId,
    string ItemId,
    string Location,
    string Checksum,
    string Metadata);

/// <summary>A restore attempt for a Quarantine entry (R15). Needs a receipt for the entry's Item.</summary>
public sealed record RestoreAttemptRecord(string EntryId, string Status, string? Reason);

/// <summary>One scan run in the history. <paramref name="HoldsUnfinishedRecovery"/> blocks deletion (R15.6).</summary>
public sealed record ScanHistoryEntry(string ScanId, DateTimeOffset StartedAt, bool HoldsUnfinishedRecovery);

/// <summary>An action attempt as stored.</summary>
public sealed record StoredActionAttempt(long AttemptId, long JournalEntryId, ActionAttemptRecord Attempt);
