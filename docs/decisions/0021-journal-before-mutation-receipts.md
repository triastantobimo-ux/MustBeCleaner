# 0021. Journal-before-mutation enforced by receipts

Status: accepted (task 4.1)

## Context
R16.1 requires a durable Journal entry before every filesystem mutation, and task 4.1 requires the Store to refuse mutation records without one.

## Decision
- `JournalStore.AppendJournal` commits with `synchronous=FULL` and returns a `JournalReceipt` only after the commit.
- `RecordActionAttempt`, `RecordQuarantineEntry` and `RecordRestoreAttempt` require a receipt. The Store checks that the receipt's entry exists in this database and covers the same Item (and the same stage for action attempts). Otherwise it throws `JournalRequiredException` and writes nothing.
- The mutation tables carry `JournalEntryId NOT NULL REFERENCES Journal`, with foreign keys on, so a second writer that bypasses the Store is still rejected by the database.
- The receipt constructor is internal, so callers cannot fabricate one.

## Scope limit
This guarantees that the Store holds a Journal entry before the mutation record. It cannot make the Executor call the Store before touching the filesystem. That ordering is the Executor's duty and is tested in task 9.7 (Property 13).

## Alternatives rejected
A boolean "journal written" flag, or checking only the database constraint: neither ties the record to the specific Item and stage.
