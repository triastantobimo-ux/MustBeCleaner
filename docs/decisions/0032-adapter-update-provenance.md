# 0032 Adapter update provenance scheme (PROPOSAL)
Status: **proposed**, needs user decision. Task 3.3. Requirements: R8.13, R8.14, R23.5, R23.7 (signing is an open distribution decision).

## Context
R8.14 and R23.5 require offline Adapter updates with provenance verification, a scope diff, and no silent scope widening. Who signs, how keys are issued and rotated, and whether the app ships a built-in key are distribution decisions (R23.7) the user has not made.

## Implemented (behind an interface, so the decision stays open)
- Package = `manifest.json` + `manifest.sig` + Adapter JSON files, read from a local folder. No network code; a test asserts the assembly references no `System.Net*`.
- `manifest.json`: `{"formatVersion":1,"files":[{"name":"x.json","sha256":"<hex>"}]}`.
- `manifest.sig`: detached ECDSA P-256 / SHA-256 signature (IEEE P1363, 64 bytes) over the exact manifest bytes.
- `IProvenanceVerifier` is the seam. `EcdsaProvenanceVerifier` takes one locally configured trusted public key (SPKI DER). No certificate chain, no revocation, no online lookup.
- Checks in order: signature, manifest shape, plain file names, every file present and hash-equal, no unlisted files, each file passes `AdapterLoader` (no executable content, R8.2), no duplicate rule IDs.
- `Import` returns an `ImportDiff` only. `Apply` is a separate call, recomputes the diff against the catalog it is given, and refuses when scope expands without `userApproved`.
- A package is a delta: Adapters replace or add by rule ID. Nothing is removed by a package.
- Scope expansion that needs approval: new Adapter, added paths (widened ones listed separately), added versions, trust inventory-only to validated, removed blockers, removed required evidence.

## Open decisions for the user
1. Who holds the signing key, and where does the trusted public key come from (shipped in the build, or placed in the Data_Directory by the user)?
2. Key rotation and revocation (not implemented; offline revocation would need a signed list).
3. Rollback protection: a validly signed older package can currently be re-imported. A monotonic version check is not implemented.
4. Whether removing an Adapter by package should exist.
5. Whether `Validated` trust may ever arrive through an update, or only through a build.

## Consequences
- Until the key decision is made, production wiring has no trusted key, so no real package can verify. Tests use throwaway keys and a fake verifier.
- Approval is a boolean the caller passes. The UI must obtain it from an explicit user action showing the diff.
