# 0041. Retention rules for reports, scan history and logs

Status: accepted (task 4.2)

## Context
R16.6 and R22.2 require bounded growth with configurable retention. R15.6 forbids deleting an unfinished recovery Journal. R16.5 keeps all data in the Data_Directory.

## Decision
- `RetentionPolicy`: `MaxReports` (default 50), `MaxReportAge`, `MaxScanRuns` (default 20, minimum 1), `MaxScanAge`. `null` turns a limit off. Items beyond the count or older than the age are removed.
- Reports: only plain files directly inside `<Data_Directory>/reports`. Subfolders, reparse points and a reparse-point `reports` folder are left alone, so nothing outside the Data_Directory can be reached.
- Scan history: `JournalStore.DeleteScanRuns` removes the run, coverage, discovered Items, classification and ownership evidence in one transaction.
- The Journal table is append-only and retention never touches it.
- A scan run is held, never deleted, when one of its Items has Journal entries but none at `SourceReleased`, or an action ended `RecoveryRequired`. The store enforces this itself, not only the caller.
- Logs: `FileLogSink` rolls at a size limit and keeps a fixed file count inside `<Data_Directory>/logs`.
- Quarantine and `journal.db` are never deleted by retention.

## Not covered
Exception_Log rows and hash cache are not pruned yet (the hash cache does not exist). Add them when a limit is agreed.
