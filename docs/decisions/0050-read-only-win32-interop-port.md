# 0050 Read-only Win32 interop ported from the identity-binding spike

Status: accepted (task 5.1). Requirements: R1.1, R1.3, R1.4, R6.7, R6.8, R7.1, R7.2.

## Decision

- The verified approach from `docs/research/identity-binding-spike.md` is ported, not referenced: `src/MustBeCleaner.Infrastructure/Interop/Win32ReadOnly.cs`. The spike project stays throwaway.
- Only the read half is ported: `CreateFileW` (attribute-only access, share all, `FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT`), `GetFileInformationByHandle`, `GetFileInformationByHandleEx(FileIdInfo)`, `FindFirstFileExW`, `FindNextFileW`, `FindClose`. Hold, pin, `OpenFileById` and `GetFinalPathNameByHandle` are Executor concerns (R21.4) and are not ported here.
- `BY_HANDLE_FILE_INFORMATION` and `WIN32_FIND_DATAW` use `Pack = 4`, the spike's finding (FILETIME is two DWORDs, so 64-bit fields are 4-aligned).
- Directory listing uses `FindFirstFileExW` instead of `System.IO` enumeration because the reparse tag (`dwReserved0`) is only available there. It lets the Inspector classify reparse points and refuse unclassified tags.
- Every reparse point is recorded and never traversed. Tags: mount point and symlink are named, cloud is matched by tag base with the state nibble masked, everything else is `Unclassified`. Junction and volume mount point share a tag, so they are one kind until `FSCTL_GET_REPARSE_POINT` is added.
- Access denied is recorded (path and count) and the scan continues. There is no retry, no privilege adjustment and no elevation (R6.8).
- Protection is a hook (`IProtectionHook`). The Inspector ships a minimal Windows-critical-root list as a default only; the Policy Core list (task 2.3) replaces it by passing its own hook.
- R1.3 is enforced by tests that read the compiled metadata: the Inspector assembly has no Executor reference and no member reference to a mutating `System.IO`/process/registry API, and the `Win32ReadOnly` imports must be on a read-only allowlist.

## Consequences

- Identity is captured per entry with one handle open each. This costs time on large trees; the baseline arrives with the performance task (R22.1).
- Symlink traversal refusal is covered by a test that returns early when symlinks cannot be created; on this machine it could not be created, so that path is UNVERIFIED. The classifier itself is covered by tag tests.
- Cloud, mount point and unclassified reparse points are tested at tag-classification level only; no fixture creates them.
- Elevated behavior, non-NTFS volumes and long paths beyond the `\\?\` prefix are untested.
