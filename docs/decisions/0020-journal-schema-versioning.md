# 0020. Journal database schema versioning

Status: accepted (task 4.1)

## Context
The Journal Store is a long-lived SQLite file in the Data_Directory (R16.4). Later tasks add columns and tables. An older build must never write to a database it does not understand, because the Journal drives crash recovery (R12.6).

## Decision
- The schema version is `PRAGMA user_version`. Version 1 is created in one transaction on first open of an empty database.
- A database with a version higher than the build supports is refused with `StorageFailure.SchemaNewer`. No downgrade, no best-effort read.
- Upgrades are forward-only, one numbered step per version, each in a transaction. None exist yet.
- The Journal table is append-only, enforced by `BEFORE UPDATE` and `BEFORE DELETE` triggers. Retention (task 4.2) applies to reports and scan history, not the Journal, which R15.6 keeps until related work finishes.
- WAL is required. If SQLite does not report `wal`, the open is refused.

## Consequences
Roll-back of the app after a schema bump means restoring the old database, not opening the new one. Changing the Journal retention rule needs a new ADR and a migration that drops the triggers deliberately.
