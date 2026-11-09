# 0030 Adapter data format and loader

Status: accepted (task 3.1). Requirements: R8.1, R8.2.

## Decision

- An Adapter is one JSON document, described by `policies/adapter.schema.json` (JSON Schema 2020-12, closed: `additionalProperties: false`).
- `MustBeCleaner.Adapters` owns its own small types (`Adapter`, `AppFingerprint`, `ProcessMapping`, enums). It does not reference `MustBeCleaner.Domain`, so it builds while Domain is in flux. Mapping to Domain types happens at the Policy Core boundary later.
- The only entry points are `AdapterLoader.Parse(json)` and `AdapterLoader.LoadFile(path)`. They return a result with violations and never throw on bad content.
- Rejection is allowlist-first: any field outside the schema is `UnknownField`. On top of that, field names that imply execution (script, command, powershell, javascript, exec, hook, ...) are reported as `ExecutableField` at any depth, and string values that reference scripts, executables or interpreters are `ExecutableReference`.
- Process names (`processes.owners/helpers`) may end in `.exe`: they are matched against running processes, never launched. They must be bare file names.
- `allowedRelativePaths` must be relative with no `..`, drive, UNC or `:`. A whole segment may be `*`; partial wildcards are rejected.
- Duplicate JSON keys are rejected (malformed).
- Enum values are kebab-case strings (`build-cache`, `recycle-bin`).

## Consequences

- The detection is intentionally over-eager (for example any value containing `.js` as an extension is rejected). A false rejection costs a data edit; a false accept would run code in a deletion tool.
- JSON Schema cannot express the executable-value check, so the loader is the authority and the schema is documentation plus editor validation. A test keeps their top-level fields in sync.
- `schemaVersion` is 1. A new version needs a new ADR and a loader branch.
