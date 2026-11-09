using MustBeCleaner.Infrastructure.Journal;
using MustBeCleaner.Infrastructure.Retention;
using MustBeCleaner.TestKit.Sandboxing;

namespace MustBeCleaner.Infrastructure.Tests;

/// <summary>R16.6, R22.2, R15.6: bounded growth, never an unfinished recovery journal, never outside the Data_Directory.</summary>
public sealed class RetentionTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private readonly Sandbox _sb = new();
    private readonly string _data;
    private readonly JournalStore _store;

    public RetentionTests()
    {
        _data = _sb.CreateDirectory("data");
        _store = JournalStore.Open(new JournalStoreOptions(_data));
    }

    public void Dispose()
    {
        _store.Dispose();
        _sb.Dispose();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private RetentionResult Apply(RetentionPolicy p) => RetentionService.Apply(_store, p, new FixedClock(Now));

    private void AddScan(string id, int daysAgo, string itemId)
    {
        _store.Add(new ScanRunRecord(id, ["C:\\x"], Now.AddDays(-daysAgo), "Completed", "p1", []));
        _store.Add(new DiscoveredItemRecord(itemId, id, "v", "f-" + itemId, "C:\\x\\" + itemId, 10, null, 0));
        _store.Add(new ClassificationRecord(itemId, ClassificationStatus.Review, "r", "1", [], "{}"));
    }

    private string Report(string name, int daysAgo)
    {
        var path = _sb.WriteFile($"data/{RetentionService.ReportsDirectoryName}/{name}", "r");
        File.SetLastWriteTimeUtc(path, Now.UtcDateTime.AddDays(-daysAgo));
        return path;
    }

    [Fact]
    public void Defaults_are_bounded()
    {
        var d = new RetentionPolicy();
        Assert.NotNull(d.MaxReports);
        Assert.NotNull(d.MaxScanRuns);
    }

    [Fact]
    public void Keeps_only_the_newest_scan_runs_and_removes_their_dependent_rows()
    {
        AddScan("s1", 30, "i1");
        AddScan("s2", 20, "i2");
        AddScan("s3", 10, "i3");
        var r = Apply(new RetentionPolicy { MaxScanRuns = 2 });
        Assert.Equal(1, r.ScanRunsDeleted);
        Assert.Null(_store.GetScanRun("s1"));
        Assert.NotNull(_store.GetScanRun("s2"));
        Assert.NotNull(_store.GetScanRun("s3"));
        Assert.Null(_store.GetDiscoveredItem("i1"));
        Assert.NotNull(_store.GetDiscoveredItem("i3"));
        Assert.Equal(0, RawSql.Scalar(_data, "SELECT COUNT(*) FROM Classification WHERE ItemId='i1'"));
    }

    [Fact]
    public void Age_limit_removes_old_scan_runs()
    {
        AddScan("old", 100, "i1");
        AddScan("new", 1, "i2");
        var r = Apply(new RetentionPolicy { MaxScanRuns = 10, MaxScanAge = TimeSpan.FromDays(30) });
        Assert.Equal(1, r.ScanRunsDeleted);
        Assert.Null(_store.GetScanRun("old"));
        Assert.NotNull(_store.GetScanRun("new"));
    }

    [Fact]
    public void Scan_run_holding_an_unfinished_recovery_journal_is_never_deleted()
    {
        AddScan("s1", 90, "stuck");
        AddScan("s2", 80, "done");
        AddScan("s3", 1, "fresh");
        _store.AppendJournal(new JournalEntry("stuck", "m", JournalStage.Prepared, Now.AddDays(-90)));
        _store.AppendJournal(new JournalEntry("stuck", "m", JournalStage.Copied, Now.AddDays(-90)));
        _store.AppendJournal(new JournalEntry("done", "m", JournalStage.Prepared, Now.AddDays(-80)));
        _store.AppendJournal(new JournalEntry("done", "m", JournalStage.SourceReleased, Now.AddDays(-80)));

        var r = Apply(new RetentionPolicy { MaxScanRuns = 1, MaxScanAge = TimeSpan.FromDays(7) });

        Assert.NotNull(_store.GetScanRun("s1"));
        Assert.NotNull(_store.GetDiscoveredItem("stuck"));
        Assert.Null(_store.GetScanRun("s2"));
        Assert.NotNull(_store.GetScanRun("s3"));
        Assert.Equal(1, r.ScanRunsDeleted);
        Assert.Equal(1, r.ScanRunsHeldForRecovery);
        Assert.Equal(4, _store.ReadJournal().Count);
    }

    [Fact]
    public void Scan_run_with_a_recovery_required_outcome_is_held()
    {
        AddScan("s1", 90, "lost");
        AddScan("s2", 1, "fresh");
        var receipt = _store.AppendJournal(new JournalEntry("lost", "m", JournalStage.SourceReleased, Now));
        _store.RecordActionAttempt(new ActionAttemptRecord("lost", "m", JournalStage.SourceReleased, ActionOutcome.RecoveryRequired, "crash"), receipt);
        Apply(new RetentionPolicy { MaxScanRuns = 1 });
        Assert.NotNull(_store.GetScanRun("s1"));
    }

    [Fact]
    public void The_store_refuses_to_delete_a_held_scan_run_even_when_asked_directly()
    {
        AddScan("s1", 90, "stuck");
        _store.AppendJournal(new JournalEntry("stuck", "m", JournalStage.Prepared, Now));
        Assert.Equal(0, _store.DeleteScanRuns(["s1"]));
        Assert.NotNull(_store.GetScanRun("s1"));
    }

    [Fact]
    public void Reports_are_pruned_by_count_and_age_oldest_first()
    {
        Report("a.json", 50);
        Report("b.json", 40);
        Report("c.json", 2);
        Report("d.json", 1);
        var r = Apply(new RetentionPolicy { MaxReports = 3, MaxReportAge = TimeSpan.FromDays(35) });
        Assert.Equal(2, r.ReportsDeleted);
        var left = Directory.GetFiles(Path.Combine(_data, RetentionService.ReportsDirectoryName)).Select(Path.GetFileName).Order().ToArray();
        Assert.Equal(new[] { "c.json", "d.json" }, left);
    }

    [Fact]
    public void Null_limits_keep_everything()
    {
        Report("a.json", 500);
        AddScan("s1", 500, "i1");
        var r = Apply(new RetentionPolicy { MaxReports = null, MaxReportAge = null, MaxScanRuns = null, MaxScanAge = null });
        Assert.Equal(0, r.ReportsDeleted + r.ScanRunsDeleted);
    }

    [Fact]
    public void Nothing_outside_the_data_directory_is_deleted_even_through_a_junction()
    {
        var outside = _sb.WriteFile("outside/precious.json", "keep");
        File.SetLastWriteTimeUtc(outside, Now.UtcDateTime.AddDays(-999));
        var reports = Path.Combine(_data, RetentionService.ReportsDirectoryName);
        if (!_sb.TryCreateJunction($"data/{RetentionService.ReportsDirectoryName}", "outside"))
            return; // junctions not permitted here: the guard below is then UNVERIFIED for this run.

        var r = Apply(new RetentionPolicy { MaxReports = 0, MaxReportAge = TimeSpan.FromDays(1) });

        Assert.Equal(0, r.ReportsDeleted);
        Assert.True(File.Exists(outside));
        Assert.True(Directory.Exists(reports));
    }

    [Fact]
    public void Linked_files_and_subfolders_inside_reports_are_left_alone()
    {
        var keep = _sb.WriteFile("outside/keep.txt", "k");
        Report("old.json", 400);
        _sb.CreateDirectory($"data/{RetentionService.ReportsDirectoryName}/sub");
        _sb.WriteFile($"data/{RetentionService.ReportsDirectoryName}/sub/old.json", "x");
        var r = Apply(new RetentionPolicy { MaxReports = 0 });
        Assert.Equal(1, r.ReportsDeleted);
        Assert.True(File.Exists(Path.Combine(_data, RetentionService.ReportsDirectoryName, "sub", "old.json")));
        Assert.True(File.Exists(keep));
    }

    [Fact]
    public void Quarantine_and_the_database_are_never_touched()
    {
        var q = _sb.CreateDirectory("quarantine");
        using var store2 = JournalStore.Open(new JournalStoreOptions(_sb.CreateDirectory("data2")) { QuarantineDirectory = q });
        var held = _sb.WriteFile("quarantine/item.bin", "payload");
        File.SetLastWriteTimeUtc(held, Now.UtcDateTime.AddDays(-999));
        RetentionService.Apply(store2, new RetentionPolicy { MaxReports = 0, MaxScanRuns = 1 }, new FixedClock(Now));
        Assert.True(File.Exists(held));
        Assert.True(File.Exists(Path.Combine(store2.DataDirectory, JournalStore.DatabaseFileName)));
    }

    [Fact]
    public void Negative_limits_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Apply(new RetentionPolicy { MaxReports = -1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Apply(new RetentionPolicy { MaxScanRuns = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => Apply(new RetentionPolicy { MaxScanAge = TimeSpan.FromDays(-1) }));
    }
}
