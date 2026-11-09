# MustBeCleaner.Infrastructure
Journal Store (SQLite, WAL) and, later, interop Windows. Task 4.1 implements the Journal Store. Domain is not referenced yet; persistence records live here and are mapped to Domain types later.

## Public interface
`MustBeCleaner.Infrastructure.Journal.JournalStore`
- `Open(JournalStoreOptions)`: fail-closed open (see below).
- `AppendJournal(JournalEntry)` returns a `JournalReceipt` after a durable commit.
- `RecordActionAttempt` / `RecordQuarantineEntry` / `RecordRestoreAttempt`: need a receipt.
- `Add(...)` per entity, `GetScanRun`, `GetDiscoveredItem`, `ListExceptions`, `ReadJournal`, and the other reads.
- `ExclusionRoots`, `IsExcluded(path)`: Data_Directory and Quarantine, for the scanner (R12.10).

## Fail-closed behavior (R12.9, R12.10)
`Open` throws `StorageUnavailableException` (`Location`, `Failure`) when:
- the path is empty or relative (`NotAbsolute`),
- the directory does not exist or is a file (`Missing`). It is never created,
- a probe file or the database cannot be written (`Unwritable`),
- free space is below `MinimumFreeBytes`, default 100 MiB (`InsufficientSpace`), or cannot be read (`SpaceUnknown`),
- the database is not valid (`Corrupt`) or newer than this build (`SchemaNewer`),
- WAL is not available.

There is no fallback to C:, `%LOCALAPPDATA%` or any other location. The Quarantine directory, when configured, must exist and be writable.

## Journal before mutation (R16.1)
Mutation tables have `JournalEntryId NOT NULL REFERENCES Journal`. The Store also checks the receipt covers the same Item and stage. The Journal table is append-only (triggers). See ADR 0020 and 0021.

## Schema (version 1, `PRAGMA user_version`)
| Table | Key | Notes |
|---|---|---|
| ScanRun | ScanId | scope and caveats as JSON arrays |
| ScanCoverage | ScanId, Root | counts and duration |
| DiscoveredItem | ItemId | volume ID, file ID, path; `SizeBytes` NULL means unavailable, never 0 |
| AppOwnershipEvidence | ItemId, rule, version | evidence JSON |
| RuleVersion | RuleId, Version | source ref, hash |
| Classification | ItemId | status, rule, reasons JSON, conditions JSON |
| DuplicateGroup | GroupId | kind, members JSON, keeper, verified |
| KeeperDecision | GroupId | |
| CleanupPlan, PlanItem | PlanId, (PlanId, ItemId) | totals and evidence snapshot |
| Journal | EntryId | stage, item, manifest, time; append-only |
| ActionAttempt | AttemptId | FK Journal, stage, outcome |
| QuarantineEntry | EntryId | FK Journal, location, checksum, metadata |
| RestoreAttempt | AttemptId | FK Journal and QuarantineEntry |
| ExceptionEntry | Id | reason code and time only, no file content |

Manifest (immutable) is task 8.2 and is not in this schema yet.

## Logging, Exception_Log and retention (task 4.2)
- `Logging.SafeLog.Write(event, LogField...)`: one JSON line per event to an `ILogSink`. Fields are typed (`Id`, `Reason`, `Path`, `Count`); no free text, no content (R16.3). Paths are masked unless `LogPolicy.AllowPaths`. `Redactor.Scrub` removes token/secret shapes. ADR 0040.
- `Logging.FileLogSink(dataDirectory)`: `logs/mustbecleaner.log`, rolled by size, fixed file count (R22.2). Data_Directory must exist.
- `Logging.ExceptionLog.Record(itemId, ExceptionReason)`: stores a reason code (R16.2, R6.8); no escalation.
- `Retention.RetentionService.Apply(store, RetentionPolicy)`: prunes `reports/` and scan history (R16.6, R22.2). Never deletes Journal rows, never a scan run with unfinished recovery (R15.6), never anything outside the Data_Directory. ADR 0041.
- Store additions: `ListScanHistory`, `DeleteScanRuns` (refuses held runs).

## Not done
ACL review of the Data_Directory (task 19.2). The Store does not set or verify ACLs. Exception_Log rows and the hash cache are not pruned.

## Run tests
```powershell
$env:PATH="$env:USERPROFILE\.dotnet10;$env:PATH"; $env:DOTNET_ROOT="$env:USERPROFILE\.dotnet10"
dotnet test tests/MustBeCleaner.Infrastructure.Tests -m:1
```
Tests use temp sandboxes only.
