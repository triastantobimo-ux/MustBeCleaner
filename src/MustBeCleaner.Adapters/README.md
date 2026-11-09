# MustBeCleaner.Adapters

Adapter Catalog, data-only (R8.1, R8.2). Maps an app and version to locations, category, blockers, owner processes, evidence, regeneration cost and recovery policy.

Self-contained: no reference to `MustBeCleaner.Domain` (ADR 0030).

## Public interface

```csharp
AdapterLoadResult r = AdapterLoader.Parse(json);      // or AdapterLoader.LoadFile(path)
if (r.IsValid) use(r.Adapter);                         // Adapter record, all R8.1 fields
else foreach (var v in r.Violations) log(v);           // Code, JSON path, message
```

The loader never throws on bad content and never runs anything. It rejects:

- fields outside the schema (`UnknownField`)
- execution-implying field names at any depth, e.g. `script`, `command`, `powershell`, `javascript` (`ExecutableField`)
- values that reference scripts, executables or interpreters, e.g. `x.ps1`, `tool.exe`, `cmd /c`, `javascript:` (`ExecutableReference`)
- non-relative or escaping paths (`UnsafePath`), bad enums, wrong types, missing fields, unsupported `schemaVersion`, malformed JSON and duplicate keys

Process names are the one place `.exe` is allowed: they are matched, never launched.

Format and example: `policies/README.md`. Decision: `docs/decisions/0030-adapter-data-format.md`.

## Matching (R8.13)

```csharp
var m = AdapterMatcher.Match(catalog, @".codex\.tmp\x", new DetectedApp("Codex", "1.2.3"));
// AdapterMatch(adapter) | NoMatch | VersionMismatch(adapter, reason)
```

Path is relative to the app data root, compared case-insensitively by whole segments; rooted or `..` paths match nothing. The most specific owning Adapter whose app id and version fit wins. `VersionMismatch` means the Item must be Review. `NoMatch` means InventoryOnly.

## Offline update import (R8.14, R23.5)

```csharp
var pkg = UpdatePackage.LoadDirectory(folder);                 // manifest.json, manifest.sig, *.json
var r = AdapterUpdates.Import(pkg, catalog, verifier);         // verifies, diffs, changes nothing
if (r.IsAccepted) show(r.Diff);                                // added / removed / widened paths, expansions
var applied = AdapterUpdates.Apply(catalog, r.Diff!, userApproved);   // refused if diff.RequiresApproval and not approved
```

Provenance: ECDSA P-256 signature over the manifest, SHA-256 per file, one locally configured trusted key (`EcdsaProvenanceVerifier`) or any `IProvenanceVerifier`. No network code. Signing and key distribution are open (ADR 0032 proposal). Not covered: rollback protection, key rotation.
