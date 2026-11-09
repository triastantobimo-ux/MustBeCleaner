# 0031 Adapter protection conventions and schema gaps (PROPOSAL)

Status: **proposed**, needs user decision. Task 3.2. Requirements: R8.3-R8.12. Builds on ADR 0030.

## Context

ADR 0030 keeps the Adapter schema closed. R8.4-R8.12 need things the schema has no field for. The initial catalog expresses them with `category`, `blockers` and `requiredEvidence`, and splits mixed locations into several files. The schema was not changed.

## Gaps

1. **No protected flag.** "Protected" is the convention `blockers` contains `protected-path` plus category `user-data` or `binary`. Nothing enforces it. The loader accepts any blocker string.
2. **No UI affordance.** R8.12 (worktrees without a cleanup checkbox, link to lifecycle) is the blocker `no-cleanup-checkbox` and evidence `app-or-git-lifecycle`. The UI/Policy Core must choose to honour it.
3. **Path root is undefined.** The schema says "relative to the app root". The catalog uses the user profile directory. Local/Roaming/Packages/`.codex` have no single app root.
4. **Overlap precedence is undefined.** Protected and Review files for one app are split so their paths do not overlap, checked by a test. If two Adapters did overlap, no rule says which wins. Policy Core must apply "Protected wins".
5. **No values for grace period, GOCACHE, idle build.** Only evidence IDs (`grace-period-elapsed`, `gocache-resolved`, `build-idle`).
6. **Free-text vocabulary.** Blocker and evidence IDs are not an enumerated set, so a typo silently weakens a rule.
7. **No "unknown version" value.** `versions` needs one item, so the catalog uses the literal `unverified`.
8. **`recoveryPolicy` has no "none".** Protected files use `not-recoverable`, which reads as "cannot be recovered" rather than "no action offered".

## Proposed options (not applied)

- Add a controlled vocabulary file (`policies/vocabulary.json`) for blocker and evidence IDs, validated by the loader.
- Add an explicit `protection` field (`protected` / `review` / `inventory-only`) and an `actions` allow list.
- Define the path root as the user profile directory in ADR 0030's schema text.

Any of these widens the schema and needs a new ADR plus a loader branch. Until the user decides, the conventions above stay in force and the tests in `InitialAdapterCatalogTests` pin them.
