# MustBeCleaner.Inspector

Status: task 5.1 done (enumeration), the protection hook is wired to the Policy Core, and Property 5 (unique totals) is covered. Coverage persistence, overview and delta come later.

Read-only metadata enumeration as a standard user. TIDAK boleh mereferensikan Executor. Referensi: Domain, Adapters, Infrastructure.

## Interface

```csharp
var result = new StorageInspector(protectionHook?).Scan([@"D:\data"], cancellationToken);
// result.Entries   : InspectedEntry (path, kind, reparse kind, ItemIdentity, link count, protection mark)
// result.Coverage  : RootCoverage per root (Complete / Partial / Cancelled / Rejected / NotFound)
// result.AccessDenied : paths that could not be read; no escalation
```

## Identity and unique totals (task 5.2)

`ItemIdentity` per file: volume ID (16 hex), 128-bit file ID (32 hex), canonical path (`PathPolicy.Normalize`, upper-case fallback for 8.3 names), size, mtime UTC, attributes (R7.6).

```csharp
var report = UniqueTotals.Compute([new ScopeEntries("cache", scan.Entries)]);
// report.Total    : UniqueFiles, KnownBytes (long?), UnavailableSize, HardLinkAliasesCollapsed, IsLowerBound
// report.PerScope : same shape per Scope name
```

- Objects are keyed by (volume ID, file ID): hard links count once (R4.6).
- Only files carry bytes; parents never re-add children (R3.7). One seen-set across Scopes in the grand total (R3.7).
- Unavailable size is `null`, never zero; `IsLowerBound` flags it (R3.5, R7.6). Identity-less files dedupe by upper-cased path.
- Duplicate Scope names throw `ArgumentException`.
- Property 5 (`UniqueTotalsProperties`) checks unique totals across generated objects, hard links, directories and Scopes.

## Rules enforced

- No traversal of junction, symlink, mount point, cloud or unclassified reparse points; they are recorded (R7.1, R7.2).
- Access denied is recorded and the scan continues (R6.8).
- Roots must be absolute drive paths; relative, UNC, device and ADS roots are rejected (R7.5).
- Protection comes from `IProtectionHook`. The default is `PolicyProtectionHook`, backed by `PolicyEngine.Classify`: the fixed protection list, user exclusions and forbidden paths all apply (R6.7). `WindowsCriticalRoots` is an explicit minimal hook for root-only checks.
- No mutation API and no Executor reference (R1.3), checked by `ReadOnlyBoundaryTests`.

Win32 calls live in `Infrastructure/Interop/Win32ReadOnly.cs`. See `docs/decisions/0050-read-only-win32-interop-port.md`.
