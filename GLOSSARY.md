
## Identity binding terms (spike 1.2)

- **Object identity**: volume serial plus 128-bit file ID read from an open handle (`GetFileInformationByHandleEx`, `FileIdInfo`). Not a path string. Two paths with the same object identity are aliases of one object.
- **Preview identity**: the object identity (plus size, mtime, attributes) captured when the user reviewed an Item.
- **Identity binding**: the guarantee that the object acted on is the object previewed. Required by R21.4 before any destructive feature ships.
- **Hold**: an open handle kept across copy, verify and release whose share mask omits write and delete, so no other process can modify, rename or delete the object meanwhile.
- **Ancestor chain**: every path component from the drive root to the Item, each captured without following reparse points.
- **Junction swap**: replacing an ancestor directory with a junction after preview so the same path reaches a different object.
- **Post-open check**: comparing the final path of the handle actually opened against the expected path.
- **Hard link alias**: another name for the same object (link count above 1). Quarantining one alias does not remove the data.

## Test foundation terms (task 1.3)
- **Sandbox**: an isolated temp folder (`MustBeCleaner-sandbox-<guid>` under the system temp) for building real on-disk fixtures. Every path goes through `Resolve`, which throws `SandboxEscapeException` outside the root. Never touches real user data (R21.2).
- **Sandbox root**: the sandbox's own temp folder. The only place the sandbox may read, write or delete.
- **Fake filesystem** (`FakeFileSystem`): in-memory filesystem for property and crash tests. Case-insensitive paths, copy never overwrites.
- **Fault**: a failure injected into the fake filesystem: crash at a named step, disk full, locked file, source modified during copy, permission denied, destination missing.
- **Checkpoint**: a named step (for example `Copied`) that code under test announces; an injected crash fault throws `SimulatedCrashException` there while keeping state written so far.
- **Folder tree** (`FolderTree`): generated set of folders and files (case variants, 8.3-like names, Unicode, empty folders) that can be materialized into a Sandbox.
- **Property tag**: the display name every property test carries: `Feature: mustbecleaner-storage-cleaner, Property {n}: {title}`. Produced by `[MbcProperty(n, title)]`, which also enforces at least 100 iterations.
- **Split-Privilege layout**: Inspector-side projects (Inspector, Duplicates, Adapters, Domain, Infrastructure) must not reference Executor, the only mutating project. Guarded by `SolutionStructureTests`.

## Stack terms (task 1.1)

Product terms (Item, Journal, Transaction_Set, Executor, Manifest, Quarantine, Data_Directory, and the rest) are defined in the Glossary of `.kiro/specs/mustbecleaner-storage-cleaner/requirements.md`. This file adds only terms the repo introduces.

- **Spike**: throwaway prototype under `spikes/` that answers one question. Never copied into `src/`.
- **File identity (spike form)**: volume serial (32-bit) plus 64-bit file index from `GetFileInformationByHandle`. Weaker than the 128-bit Object identity above; used only to prove P/Invoke works on this stack.
- **Mica fallback**: when Windows build is below 22000, the window uses a solid pastel background instead of the Mica backdrop.
- **Pinned version**: exact NuGet or SDK version recorded in `docs/decisions/0001-stack.md` after a verified spike run.

## Adapter terms (task 3.1)

`Adapter`, `Rule` and `Policy_Engine` are defined in the requirements Glossary. This file adds:

- **Adapter document**: one JSON file describing an Adapter, validated by `policies/adapter.schema.json`.
- **Adapter loader** (`AdapterLoader`): the only way to get an `Adapter`; returns violations instead of throwing and rejects anything executable (R8.2).
- **Violation**: one reason a document was rejected, with a code (`UnknownField`, `ExecutableField`, `ExecutableReference`, `UnsafePath`, ...) and a JSON path.
- **Trust** (`inventory-only` / `validated`): inventory-only is the initial state of every Adapter (R8.3).
- **Process mapping**: owner and helper process image names that are matched to detect activity. Never launched.
## Journal Store terms (task 4.1)
Product terms (Journal, Data_Directory, Quarantine, Exception_Log) are in the requirements Glossary. This adds repo terms only.
- **Journal Store**: the SQLite (WAL) database `journal.db` in the Data_Directory. The only persistence boundary (`JournalStore`).
- **Journal receipt**: proof returned by `AppendJournal` after the entry is committed. Required to write a mutation record.
- **Mutation record**: an `ActionAttempt`, `QuarantineEntry` or `RestoreAttempt`. Refused without a matching receipt.
- **Fail-closed open**: `JournalStore.Open` throws `StorageUnavailableException` when the Data_Directory or Quarantine is relative, missing, unwritable or below the free-space minimum. It never creates a directory and never picks another location.
- **Exclusion roots**: the Data_Directory and Quarantine paths the scanner must skip (`JournalStore.ExclusionRoots`, R12.10).
- **Persistence record**: Infrastructure-owned record type for one stored entity (for example `ScanRunRecord`). Mapped to Domain types later.
- **Schema version**: `PRAGMA user_version` of `journal.db`. A newer version than the build knows is refused.
## Policy Core terms (task 2.1)
Product terms (Tri_State, Eligible, Protected, Review, Policy_Engine, Item) are defined in the requirements Glossary. This section adds terms the Policy Core introduces.
- **Policy Core**: the pure, I/O-free part of the Policy_Engine. Takes facts as input and returns a Classification.
- **Inventory-only**: Item status for something no Adapter recognizes or that sits on a non-NTFS volume. Listed, never offered for action.
- **Eligibility condition**: one of the seven observed true/false/unknown checks that must all be true before an Item is Eligible. "No protection rule matches" is decided by the rule set and is not one of them.
- **Item facts**: what is known about one Item: identity, ancestor chain, and the Adapter rule that owns it, if any.
- **World facts**: observations about the outside world (active processes, locks, app fingerprint) handed to the Policy Core. Whatever was not observed counts as unknown.
- **Rule set**: the versioned collection of policy rules. Every Classification records the version it was made with.
- **Classification**: the outcome for one Item: status, deciding rule ID, rule version, reasons, and the value of every eligibility condition.

## Path policy terms (task 2.2)
Product terms (Scope, exclusion, include rule, Policy_Engine) are in the requirements Glossary. This adds repo terms only.
- **Canonical path** (`CanonicalPath`): drive-absolute, upper-case, backslash-separated, no `\\?\` prefix, no `.`/`..`, no trailing separator except at the root. Only `PathPolicy.Normalize` creates one.
- **Path rejection** (`PathRejection`): the reason a raw path was refused: Empty, Relative, Unc, DevicePath, AlternateDataStream, InvalidCharacter, EscapesRoot, UnresolvedShortName.
- **Unresolved short name**: a segment shaped like an 8.3 name (`PROGRA~1`). Expanding it needs I/O, so the Policy Core rejects it instead of guessing (ADR 0011).
- **Path scope** (`PathScope`): include and exclusion roots. `Decide` returns Rejected, Excluded, Included or NotIncluded; exclusion beats include.

## Initial Adapter catalog terms (task 3.2)
- **Adapter catalog**: the set of Adapter JSON files in `policies/adapters`, loaded as a whole by tests.
- **Tier**: protected, review or inventory-only treatment of a location. Encoded by convention in category, blockers and evidence, not by a schema field (ADR 0031 proposal).
- **Protected-path blocker**: the blocker ID `protected-path`; marks an Adapter whose paths are never offered for action.
- **Profile root**: the user profile directory; the base of every `allowedRelativePaths` entry in the catalog.
- **Unverified fingerprint**: version `unverified`, which matches no real app version, so Items fall to Review (R8.13).

## Logging and Retention terms (task 4.2)
Product terms (Exception_Log, Data_Directory, Journal) are in the requirements Glossary. This adds repo terms only.
- **Safe log**: `SafeLog`, the only logging entry point. Accepts typed fields (id, reason, path, count), never free text or content.
- **Redaction**: replacing secret-shaped or content-like values with `[REDACTED]` (`Redactor`, ADR 0040).
- **Reason code**: fixed upper-case code for a skipped or denied Item, for example `ACCESS_DENIED` (`ExceptionReason`).
- **Retention policy**: limits on count and age of reports and scan runs. `null` switches a limit off.
- **Held scan run**: a scan run due for deletion that is kept because one of its Items has an unfinished recovery (R15.6).
- **Unfinished recovery**: an Item with Journal entries but none at `SourceReleased`, or an action that ended `RecoveryRequired`.

## Inspector terms (task 5.1)
Product terms (Scanner, Item, Scan_Coverage, Exception_Log, Protected) are in the requirements Glossary. This adds repo terms only.
- **Inspector** (`StorageInspector`): read-only metadata enumeration. No mutation API, no Executor reference.
- **Inspected entry**: one file, directory or reparse point observed by a scan: identity, link count, protection mark.
- **Reparse kind**: classification of a reparse tag (`SymbolicLink`, `JunctionOrMountPoint`, `Cloud`, `Unclassified`). All kinds are recorded and none are traversed.
- **Unclassified reparse tag**: any tag the Inspector does not name. Treated like a junction: never traversed.
- **Root coverage**: per scan root result: `Complete`, `Partial`, `Cancelled`, `Rejected` or `NotFound`, with entry, reparse-skipped and access-denied counts.
- **Protection hook** (`IProtectionHook`): seam where the Policy Core supplies the full protected-path list. Default is `WindowsCriticalRoots`, a minimal list.
- **Read-only interop** (`Win32ReadOnly`): the only Win32 surface the Inspector uses; attribute-only opens and directory listing. See ADR 0050.

## Unique totals terms (task 5.2)
- **Unique object**: one filesystem object keyed by (volume ID, file ID). All its hard link names are one unique object.
- **Alias collapsed**: an extra name of an already-counted object, skipped in totals (`HardLinkAliasesCollapsed`).
- **Scope entries** (`ScopeEntries`): the entries seen under one named Scope, input to `UniqueTotals`.
- **Grand total**: totals where one seen-set spans all Scopes, so an object reached from two Scopes counts once.
- **Lower bound**: totals where some size is unavailable; `KnownBytes` covers only the known sizes (`IsLowerBound`).
- **Unavailable size**: `null` size. Never summed as zero. A file without readable identity is unavailable and is deduplicated by upper-cased path.

## Adapter matching and update terms (task 3.3)

- **Detected app** (`DetectedApp`): app id plus the one installed version found on this machine. Compared with an Adapter's fingerprint.
- **Version mismatch** (`VersionMismatch`): an Adapter owns the path but app id or version is not in its fingerprint. The Item is Review, never Eligible (R8.13).
- **Update package** (`UpdatePackage`): offline folder with `manifest.json`, detached `manifest.sig` and Adapter JSON files (ADR 0032 proposal).
- **Provenance verifier** (`IProvenanceVerifier`): checks the manifest signature against a locally configured trusted key. No network.
- **Import diff** (`ImportDiff`): per-Adapter added, removed and widened paths plus scope expansions. Computing it changes nothing.
- **Scope expansion** (`ScopeExpansion`): any change that enlarges what may be touched or loosens protection. Needs explicit user approval to apply.
- **Widened path**: an added path that covers an existing path (for example `.codex` replacing `.codex\.tmp`).

## Policy Core terms (task 2.3)

- **Protection rule** (`ProtectionRules`): fixed in-code rule that makes an Item Protected. No Adapter, score, custom include or administrator mode can lift it (ADR 0012).
- **Transaction_Set** (`TransactionSet`): a database file plus its WAL, SHM and journal sidecars; protected together under one key.
- **Forbidden path**: a raw path that fails normalization; classified Protected as `F-FORBIDDEN-PATH`.
- **User exclusion**: path root from the RuleSet; classified Protected as `X-USER-EXCLUSION`, decided before every other rule.
- **Unowned extensionless file**: a file with no extension and no Adapter owner; Protected as it may be a binary.
