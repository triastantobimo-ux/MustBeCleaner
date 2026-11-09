namespace MustBeCleaner.Infrastructure.Journal;

/// <summary>SQL schema, version 1 (R16.4). See docs/decisions/0002-journal-schema-versioning.md.</summary>
internal static class Schema
{
    public const int CurrentVersion = 1;

    public static readonly string[] Tables =
    [
        "ScanRun", "ScanCoverage", "DiscoveredItem", "AppOwnershipEvidence", "RuleVersion",
        "Classification", "DuplicateGroup", "KeeperDecision", "CleanupPlan", "PlanItem",
        "Journal", "ActionAttempt", "QuarantineEntry", "RestoreAttempt", "ExceptionEntry",
    ];

    // Mutation tables carry JournalEntryId NOT NULL REFERENCES Journal: the database itself
    // refuses a mutation record that has no Journal entry (R16.1), on top of the Store check.
    public const string CreateV1 = """
        CREATE TABLE ScanRun (
            ScanId TEXT PRIMARY KEY, ScopeJson TEXT NOT NULL, StartedAt TEXT NOT NULL,
            Status TEXT NOT NULL, PolicyVersion TEXT NOT NULL, CaveatsJson TEXT NOT NULL);
        CREATE TABLE ScanCoverage (
            ScanId TEXT NOT NULL REFERENCES ScanRun(ScanId), Root TEXT NOT NULL, Status TEXT NOT NULL,
            EntryCount INTEGER NOT NULL, ReparseSkipped INTEGER NOT NULL, AccessDenied INTEGER NOT NULL,
            DurationMs INTEGER NOT NULL, PRIMARY KEY (ScanId, Root));
        CREATE TABLE DiscoveredItem (
            ItemId TEXT PRIMARY KEY, ScanId TEXT NOT NULL REFERENCES ScanRun(ScanId),
            VolumeId TEXT NOT NULL, FileId TEXT NOT NULL, CanonicalPath TEXT NOT NULL,
            SizeBytes INTEGER NULL, ModifiedAt TEXT NULL, Attributes INTEGER NOT NULL);
        CREATE TABLE AppOwnershipEvidence (
            ItemId TEXT NOT NULL REFERENCES DiscoveredItem(ItemId), AdapterRuleId TEXT NOT NULL,
            AdapterVersion TEXT NOT NULL, EvidenceJson TEXT NOT NULL,
            PRIMARY KEY (ItemId, AdapterRuleId, AdapterVersion));
        CREATE TABLE RuleVersion (
            RuleId TEXT NOT NULL, Version TEXT NOT NULL, SourceRef TEXT NOT NULL, Hash TEXT NOT NULL,
            PRIMARY KEY (RuleId, Version));
        CREATE TABLE Classification (
            ItemId TEXT PRIMARY KEY REFERENCES DiscoveredItem(ItemId), Status TEXT NOT NULL,
            RuleId TEXT NOT NULL, RuleVersion TEXT NOT NULL, ReasonsJson TEXT NOT NULL, ConditionsJson TEXT NOT NULL);
        CREATE TABLE DuplicateGroup (
            GroupId TEXT PRIMARY KEY, Kind TEXT NOT NULL, MembersJson TEXT NOT NULL,
            KeeperId TEXT NULL, Verified INTEGER NOT NULL);
        CREATE TABLE KeeperDecision (
            GroupId TEXT PRIMARY KEY REFERENCES DuplicateGroup(GroupId), KeeperItemId TEXT NOT NULL);
        CREATE TABLE CleanupPlan (PlanId TEXT PRIMARY KEY, TotalsJson TEXT NOT NULL);
        CREATE TABLE PlanItem (
            PlanId TEXT NOT NULL REFERENCES CleanupPlan(PlanId), ItemId TEXT NOT NULL,
            EvidenceSnapshot TEXT NOT NULL, PRIMARY KEY (PlanId, ItemId));
        CREATE TABLE Journal (
            EntryId INTEGER PRIMARY KEY AUTOINCREMENT, ItemId TEXT NOT NULL, ManifestId TEXT NOT NULL,
            Stage TEXT NOT NULL, At TEXT NOT NULL, Detail TEXT NULL);
        CREATE TRIGGER Journal_no_update BEFORE UPDATE ON Journal
            BEGIN SELECT RAISE(ABORT, 'Journal is append-only'); END;
        CREATE TRIGGER Journal_no_delete BEFORE DELETE ON Journal
            BEGIN SELECT RAISE(ABORT, 'Journal is append-only'); END;
        CREATE TABLE ActionAttempt (
            AttemptId INTEGER PRIMARY KEY AUTOINCREMENT,
            JournalEntryId INTEGER NOT NULL REFERENCES Journal(EntryId),
            ItemId TEXT NOT NULL, ManifestId TEXT NOT NULL, JournalStage TEXT NOT NULL,
            Outcome TEXT NOT NULL, Reason TEXT NULL);
        CREATE TABLE QuarantineEntry (
            EntryId TEXT PRIMARY KEY, JournalEntryId INTEGER NOT NULL REFERENCES Journal(EntryId),
            ItemId TEXT NOT NULL, Location TEXT NOT NULL, Checksum TEXT NOT NULL, Metadata TEXT NOT NULL);
        CREATE TABLE RestoreAttempt (
            AttemptId INTEGER PRIMARY KEY AUTOINCREMENT,
            JournalEntryId INTEGER NOT NULL REFERENCES Journal(EntryId),
            EntryId TEXT NOT NULL REFERENCES QuarantineEntry(EntryId), Status TEXT NOT NULL, Reason TEXT NULL);
        CREATE TABLE ExceptionEntry (
            Id INTEGER PRIMARY KEY AUTOINCREMENT, ItemId TEXT NOT NULL, ReasonCode TEXT NOT NULL, At TEXT NOT NULL);
        """;
}
