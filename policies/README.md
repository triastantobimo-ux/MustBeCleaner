# policies

Declarative, data-only policy files.

- `adapter.schema.json` - JSON Schema (2020-12) for one Adapter (R8.1). Closed: unknown fields are invalid.
- `adapter.example.json` - a valid example, loaded by a test so it cannot rot.

## Adapter fields

| Field | Type | Meaning |
|---|---|---|
| `schemaVersion` | `1` | Format version. |
| `ruleId`, `version` | string | Rule identity, recorded with each classification. |
| `sourceReference` | string | Where the rule comes from (doc or evidence path). |
| `fingerprint` | `{ appId, versions[] }` | App identity and exact supported versions. Mismatch means Review. |
| `allowedRelativePaths` | string[] | Relative to the app root. No `..`, drive, UNC or `:`. A whole segment may be `*`. |
| `category` | enum | `cache`, `temp`, `build-cache`, `log`, `user-data`, `binary`, `other` |
| `trust` | enum | `inventory-only` (initial) or `validated` |
| `blockers` | string[] | Condition IDs that block action, e.g. `owner-process-active`. |
| `processes` | `{ owners[], helpers[] }` | Process image names to check for activity. Never launched. |
| `requiredEvidence` | string[] | Evidence needed before eligibility. |
| `regenerationCost` | enum | `low`, `medium`, `high`, `unknown` |
| `recoveryPolicy` | enum | `quarantine`, `recycle-bin`, `not-recoverable` |

## No executable content (R8.2)

Rejected by `AdapterLoader`: fields such as `script`, `command`, `powershell`, `javascript`; values that name scripts, executables or interpreters; unknown fields. The only `.exe` allowed is a process name under `processes`.

## Example

```json
{
  "schemaVersion": 1,
  "ruleId": "example.cache",
  "version": "0.1.0",
  "sourceReference": "docs/LOCAL_STORAGE_REVIEW.md",
  "fingerprint": { "appId": "ExampleApp", "versions": ["1.0.0"] },
  "allowedRelativePaths": ["ExampleApp\\Cache", "ExampleApp\\Profiles\\*\\Code Cache"],
  "category": "cache",
  "trust": "inventory-only",
  "blockers": ["owner-process-active", "lock-present"],
  "processes": { "owners": ["ExampleApp.exe"], "helpers": ["ExampleApp Helper.exe"] },
  "requiredEvidence": ["owner-idle", "age-over-grace-period"],
  "regenerationCost": "low",
  "recoveryPolicy": "quarantine"
}
```
