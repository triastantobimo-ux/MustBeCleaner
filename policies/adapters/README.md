# policies/adapters

Initial Adapters (task 3.2, R8.3-R8.12). Every file is `trust: inventory-only` with `fingerprint.versions: ["unverified"]`, so any real app version mismatches and falls to Review (R8.13). **No file says a path is safe to delete.** Listing a path means "this Adapter covers it", never "it may be removed".

Loaded and checked by `tests/MustBeCleaner.Adapters.Tests/InitialAdapterCatalogTests.cs` through `AdapterLoader`.

## Conventions (see ADR 0031 proposal)

- Paths are relative to the **user profile directory** (for example `.codex\sessions`, `AppData\Local\go-build`). The schema only says "relative to the app root"; this catalog fixes that root.
- A location with mixed tiers is split into several files named `<location>.<tier>.json`.
- **Protected**: `blockers` contains `protected-path`, category is `user-data` or `binary`, `recoveryPolicy` is `not-recoverable` (nothing to recover because no action is ever offered). `requiredEvidence` is only `protection-rule-removed-by-user-decision`.
- **Review**: category `cache`, `temp`, `build-cache` or `other`, with `requiredEvidence` listing what must be proven first.
- **Inventory-only**: root folders with `root-not-deletable` plus unknowns as blockers.
- These identifiers are conventions, not schema-enforced vocabulary.

## Files

| File | Location | Tier | Requirements | Evidence |
|---|---|---|---|---|
| `codex-home.protected.json` | `.codex`: sessions, archived_sessions, memories, auth, config, SQLite plus WAL/SHM, .chatgpt-projects, skills, .sandbox-bin | Protected | R8.4 | LOCAL_STORAGE_REVIEW "Komponen terbesar `.codex`"; Blueprint 6 |
| `codex-home.review.json` | `.codex\.tmp`, `cache`, `plugins` | Review | R8.5 | same; plugins also carry `active-plugins-protected` |
| `codex-worktrees.managed.json` | `.codex\worktrees` | Protected, `no-cleanup-checkbox`, evidence `app-or-git-lifecycle` | R8.4, R8.12 | LOCAL_STORAGE_REVIEW; Blueprint 6 (worktree lifecycle) |
| `codex-desktop-local.json` | `AppData\Local\Codex` | Inventory-only | R8.3 | LOCAL_STORAGE_REVIEW "Lokasi yang diminta pengguna" |
| `openai-local.json` | `AppData\Local\OpenAI` | Inventory-only | R8.3 | same |
| `openai-codex-package.json` | `AppData\Local\Packages\OpenAI.Codex_2p2nqsd0c76g0` | Inventory-only | R8.3 | LOCAL_STORAGE_REVIEW (package footprint) |
| `claude-mem.bin.protected.json` | `AppData\Local\claude-mem\bin` | Protected binary | R8.6 | LOCAL_STORAGE_REVIEW (`claude-mem.exe`, "binary aplikasi, bukan cache") |
| `claude-mem.root.json` | `AppData\Local\claude-mem` | Inventory-only | R8.3, R8.6 | Blueprint 6 ("nama `mem` tidak berarti temporary") |
| `claude.cache.json` / `claude.protected.json` | `AppData\Roaming\Claude` | Review cache / Protected profile state | R8.8 | LOCAL_STORAGE_REVIEW; Blueprint 6 |
| `code.cache.json` / `code.protected.json` | `AppData\Roaming\Code`, `.vscode\extensions` | Review cache / Protected user data and profile state | R8.7, R8.8 | LOCAL_STORAGE_REVIEW (Roaming Code groups); Blueprint 6 |
| `qoder.cache.json` / `qoder.protected.json` | `AppData\Roaming\com.qoder.app.stable` | Review cache / Protected | R8.7, R8.8 | LOCAL_STORAGE_REVIEW; layout **assumed** |
| `opencode.json` | none (paths empty) | Inventory-only | R8.3, R8.7 | Blueprint 6 only; no OpenCode path in the evidence |
| `windows-temp.json` | `AppData\Local\Temp\*` (children only, never the root) | Review | R8.9 | LOCAL_STORAGE_REVIEW; Blueprint 6 |
| `go-build.json` | `AppData\Local\go-build` | Review | R8.10 | LOCAL_STORAGE_REVIEW (not matched to `go env GOCACHE`); Blueprint 6 |
| `autoclaw2.json` | `AppData\Local\AutoClaw 2` | Inventory-only, `layout-unverified` | R8.11 | LOCAL_STORAGE_REVIEW; Blueprint 6 |

## Not verified

- Exact file names `auth.json`, `config.toml`, `*.sqlite-wal`, `*.sqlite-shm`, and the Chromium names (`Cache`, `Code Cache`, `GPUCache`, `Local Storage`, `IndexedDB`, `Session Storage`, `Network\Cookies`) are conventional, not read from the user's machine. Protecting a name that does not exist is harmless; a missing name is a gap.
- Process image names (`codex.exe`, `Code.exe`, ...) are unverified guesses. Only "a process named `codex` was running" is in the evidence.
- Qoder layout is assumed to mirror Electron/VS Code. Claude local agent sessions are not named in the evidence and are not listed.
- Grace period length, "idle build" detection and GOCACHE resolution are evidence identifiers only; the schema holds no values for them.
