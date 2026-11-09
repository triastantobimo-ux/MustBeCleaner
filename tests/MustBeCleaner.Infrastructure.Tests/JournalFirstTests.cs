using MustBeCleaner.Infrastructure.Journal;
using MustBeCleaner.TestKit.Sandboxing;

namespace MustBeCleaner.Infrastructure.Tests;

/// <summary>Journal-before-mutation (R16.1) and persistence of the R16.4 entities. Temp sandboxes only.</summary>
public sealed class JournalFirstTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2025, 1, 2, 3, 4, 5, TimeSpan.Zero);
    private readonly Sandbox _sb = new();
    private readonly string _dir;
    private readonly JournalStore _store;

    public JournalFirstTests()
    {
        _dir = _sb.CreateDirectory("data");
        _store = JournalStore.Open(new JournalStoreOptions(_dir));
    }

    public void Dispose()
    {
        _store.Dispose();
        _sb.Dispose();
    }

    private JournalReceipt Journal(string item, JournalStage stage) =>
        _store.AppendJournal(new JournalEntry(item, "m1", stage, T0));

    [Fact]
    public void Journal_entry_is_durable_and_visible_to_another_connection_before_append_returns()
    {
        var receipt = Journal("i1", JournalStage.Prepared);
        Assert.Equal(1, RawSql.Scalar(_dir, $"SELECT COUNT(*) FROM Journal WHERE EntryId={receipt.EntryId}"));
        Assert.Equal("wal", RawSql.Strings(_dir, "PRAGMA journal_mode")[0]);
    }

    [Fact]
    public void Action_attempt_with_matching_receipt_is_stored()
    {
        var receipt = Journal("i1", JournalStage.Copied);
        var id = _store.RecordActionAttempt(
            new ActionAttemptRecord("i1", "m1", JournalStage.Copied, ActionOutcome.Failed, "locked"), receipt);
        var stored = Assert.Single(_store.ListActionAttempts());
        Assert.Equal(id, stored.AttemptId);
        Assert.Equal(receipt.EntryId, stored.JournalEntryId);
        Assert.Equal(ActionOutcome.Failed, stored.Attempt.Outcome);
    }

    [Fact]
    public void Action_attempt_without_receipt_is_refused_and_nothing_is_written()
    {
        Assert.Throws<JournalRequiredException>(() => _store.RecordActionAttempt(
            new ActionAttemptRecord("i1", "m1", JournalStage.Prepared, ActionOutcome.Quarantined, null), null!));
        Assert.Empty(_store.ListActionAttempts());
    }

    [Fact]
    public void Action_attempt_with_receipt_for_other_item_or_stage_is_refused()
    {
        var receipt = Journal("i1", JournalStage.Prepared);
        Assert.Throws<JournalRequiredException>(() => _store.RecordActionAttempt(
            new ActionAttemptRecord("other", "m1", JournalStage.Prepared, ActionOutcome.Failed, null), receipt));
        Assert.Throws<JournalRequiredException>(() => _store.RecordActionAttempt(
            new ActionAttemptRecord("i1", "m1", JournalStage.SourceReleased, ActionOutcome.Quarantined, null), receipt));
        Assert.Empty(_store.ListActionAttempts());
    }

    [Fact]
    public void Receipt_from_another_store_is_refused()
    {
        using var otherSandbox = new Sandbox();
        using var other = JournalStore.Open(new JournalStoreOptions(otherSandbox.CreateDirectory("data")));
        var foreign = other.AppendJournal(new JournalEntry("i1", "m1", JournalStage.Prepared, T0));
        // Mine is empty, so entry id 1 does not exist here.
        Assert.Throws<JournalRequiredException>(() => _store.RecordActionAttempt(
            new ActionAttemptRecord("i1", "m1", JournalStage.Prepared, ActionOutcome.Failed, null), foreign));
    }

    [Fact]
    public void Database_itself_rejects_mutation_row_without_journal_entry()
    {
        Assert.ThrowsAny<Exception>(() => RawSqlForeignKeys.InsertOrphanAttempt(_dir));
    }

    [Fact]
    public void Quarantine_entry_and_restore_attempt_require_receipts()
    {
        var quarantine = new QuarantineEntryRecord("q1", "i1", "D:\\q\\a", "abc", "{}");
        Assert.Throws<JournalRequiredException>(() => _store.RecordQuarantineEntry(quarantine, null!));
        Assert.Null(_store.GetQuarantineEntry("q1"));

        var r1 = Journal("i1", JournalStage.Verified);
        _store.RecordQuarantineEntry(quarantine, r1);
        Assert.Equal(quarantine, _store.GetQuarantineEntry("q1"));

        var restore = new RestoreAttemptRecord("q1", "conflict", "target not empty");
        Assert.Throws<JournalRequiredException>(() => _store.RecordRestoreAttempt(restore, null!));
        Assert.Throws<JournalRequiredException>(() => _store.RecordRestoreAttempt(restore, Journal("wrong-item", JournalStage.Prepared)));
        Assert.Throws<JournalRequiredException>(() => _store.RecordRestoreAttempt(
            new RestoreAttemptRecord("missing", "x", null), Journal("i1", JournalStage.Prepared)));
        _store.RecordRestoreAttempt(restore, Journal("i1", JournalStage.Prepared));
        Assert.Equal(restore, Assert.Single(_store.ListRestoreAttempts("q1")));
    }

    [Fact]
    public void Journal_is_append_only()
    {
        var receipt = Journal("i1", JournalStage.Prepared);
        Assert.ThrowsAny<Exception>(() => RawSql.Execute(_dir, $"UPDATE Journal SET Stage='Copied' WHERE EntryId={receipt.EntryId}"));
        Assert.ThrowsAny<Exception>(() => RawSql.Execute(_dir, "DELETE FROM Journal"));
        var entry = Assert.Single(_store.ReadJournal());
        Assert.Equal(JournalStage.Prepared, entry.Entry.Stage);
        Assert.Equal(T0, entry.Entry.At);
    }

    [Fact]
    public void Schema_contains_every_required_entity()
    {
        var tables = RawSql.Strings(_dir, "SELECT name FROM sqlite_master WHERE type='table'");
        foreach (var name in new[]
        {
            "ScanRun", "ScanCoverage", "DiscoveredItem", "AppOwnershipEvidence", "RuleVersion", "Classification",
            "DuplicateGroup", "KeeperDecision", "CleanupPlan", "PlanItem", "ActionAttempt", "QuarantineEntry",
            "RestoreAttempt", "ExceptionEntry", "Journal",
        })
            Assert.Contains(name, tables);
    }

    [Fact]
    public void Non_mutation_entities_round_trip()
    {
        _store.Add(new ScanRunRecord("s1", ["C:\\Users"], T0, "Completed", "p1", ["partial"]));
        _store.Add(new ScanCoverageRecord("s1", "C:\\Users", "Complete", 10, 1, 2, 300));
        _store.Add(new DiscoveredItemRecord("i1", "s1", "vol", "file", "c:\\users\\a", null, T0, 32));
        _store.Add(new DiscoveredItemRecord("i2", "s1", "vol", "file2", "c:\\users\\b", 0, null, 0));
        _store.Add(new AppOwnershipEvidenceRecord("i1", "rule.a", "1", ["path"]));
        _store.Add(new RuleVersionRecord("rule.a", "1", "src", "hash"));
        _store.Add(new ClassificationRecord("i1", ClassificationStatus.Review, "rule.a", "1", ["unknown"], "{}"));
        _store.Add(new DuplicateGroupRecord("g1", DuplicateKind.File, ["i1", "i2"], null, false));
        _store.Add(new KeeperDecisionRecord("g1", "i1"));
        _store.Add(new CleanupPlanRecord("p1", "{}"));
        _store.Add(new PlanItemRecord("p1", "i1", "{}"));
        _store.Add(new ExceptionEntryRecord("i1", "ACCESS_DENIED", T0));

        var run = _store.GetScanRun("s1")!;
        Assert.Equal(["C:\\Users"], run.Scope);
        Assert.Equal(["partial"], run.Caveats);
        Assert.Equal(T0, run.StartedAt);
        Assert.Null(_store.GetDiscoveredItem("i1")!.SizeBytes);   // unavailable stays unavailable (R3.5)
        Assert.Equal(0L, _store.GetDiscoveredItem("i2")!.SizeBytes);
        Assert.Equal("ACCESS_DENIED", Assert.Single(_store.ListExceptions()).ReasonCode);
        Assert.Equal(1, RawSql.Scalar(_dir, "SELECT COUNT(*) FROM DuplicateGroup WHERE KeeperId IS NULL AND Verified=0"));
    }

    [Fact]
    public void Reopening_keeps_data()
    {
        Journal("i1", JournalStage.Prepared);
        _store.Dispose();
        using var again = JournalStore.Open(new JournalStoreOptions(_dir));
        Assert.Single(again.ReadJournal());
    }
}

internal static class RawSqlForeignKeys
{
    public static void InsertOrphanAttempt(string dir)
    {
        var path = Path.Combine(dir, JournalStore.DatabaseFileName);
        using var c = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False;Foreign Keys=True");
        c.Open();
        using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO ActionAttempt (JournalEntryId, ItemId, ManifestId, JournalStage, Outcome) VALUES (999,'i','m','Prepared','Failed')";
        cmd.ExecuteNonQuery();
    }
}
