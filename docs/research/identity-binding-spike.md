# Spike 1.2: object identity binding

Verdict for the R21.4 release gate: **PARTIAL**.

The core mechanism is proven on NTFS for single files on one volume. Several cases the product needs are not proven, so destructive features stay held back (R21.4) until they are.

## Question

Can the object reviewed at preview time be bound to the object later moved (R7.3, R7.6, R11.4, R12.2 to R12.4, R21.4)?

## Environment

- Windows NT 10.0.26300.0, temp volume NTFS, local.
- .NET SDK 9.0.320 only, `net9.0`. ADR 0001 targets .NET 10, not installed. The Win32 calls used are the same on either runtime, so the SDK difference is not expected to matter. UNVERIFIED on .NET 10.
- xUnit 2.9.2. Code in `spikes/identity-binding/`, 29 tests, all pass in the final run. Sandbox `%TEMP%\mbc-idspike-<guid>`; 0 sandbox directories left after the run.
- Run once as a normal (non-elevated) user. Symlink creation was not attempted.

## Method

1. Open with `CreateFileW` using `FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT` and attribute-only access, so a final reparse point is not followed and no data is read.
2. Read identity from the handle: `GetFileInformationByHandleEx(FileIdInfo)` gives volume serial and 128-bit file ID; `GetFileInformationByHandle` gives attributes, size, mtime, link count.
3. Ancestor chain: open each component from the root separately and fail on any reparse attribute. Recheck by recapturing and comparing identity.
4. Post-open check: `GetFinalPathNameByHandleW` on the opened handle compared with the expected path.
5. Reopen by identity with `OpenFileById` using a volume-hint handle.
6. Aliases: `FindFirstFileNameW`/`FindNextFileNameW`.
7. Hold: `FileStream` with `FileShare.Read` across copy, SHA-256 verify, then dispose.

## Results

| Behavior | Result | Evidence |
|---|---|---|
| Same file captured twice has equal identity; different files differ | PROVEN | `IdentityCaptureTests` |
| File ID is non-zero and 128-bit capable | PROVEN | `File_id_is_128_bit_capable_and_nonzero` |
| Hard link alias has the same identity, link count 2, all names enumerable | PROVEN | `Hard_link_*` |
| Rename after preview: path lookup misses, identity follows the object | PROVEN | `Rename_after_preview_*` |
| Held handle's final path follows a rename | PROVEN | `Held_handle_final_path_follows_a_rename` |
| `OpenFileById` reaches the previewed object after rename | PROVEN | `Open_by_id_reaches_*` |
| Delete and recreate at same path gives a different file ID | PROVEN in one trial. ID reuse not ruled out | see Limits |
| `File.Replace` over the path gives a different file ID | PROVEN | `Atomic_replace_*` |
| In-place edit keeps ID, size/mtime/attributes check detects it | PROVEN | `In_place_modification_*` |
| Edit that restores size and mtime is not detected by metadata | NEGATIVE, as expected | `Same_size_same_mtime_overwrite_*` |
| Junction as an ancestor is rejected at capture | PROVEN | `Junction_as_ancestor_*` |
| Junction swap after preview detected by chain recheck | PROVEN | `Junction_swap_after_preview_*` |
| Real directory swapped in after preview detected by chain recheck | PROVEN | `Directory_replaced_*` |
| Open through swapped junction caught by final-path comparison | PROVEN | `Open_through_a_swapped_junction_*` |
| Hold (share read only) blocks write, delete, rename, atomic replace | PROVEN | `Held_handle_blocks_*` |
| Hold fails if a writer already has the file open | PROVEN, usable as activity probe | `Hold_cannot_be_taken_*` |
| Copy, SHA-256 verify, release with the hold: copy identical, tamper blocked, source deletable only after release | PROVEN (same volume) | `Copy_verify_release_*` |
| Directory handle with real access and no share-delete blocks renaming that directory | PROVEN | `Directory_handle_without_share_delete_*` |
| Pinning a parent does not protect its children from being swapped | NEGATIVE, as expected | `Pinning_a_directory_*` |
| A hold that grants share-write does not stop writers | NEGATIVE, as expected | `Writer_that_opened_before_*` |

## Findings that changed the code

- A pin opened with attribute-only access (`FILE_READ_ATTRIBUTES`) did not block a directory rename, because attribute-only opens do not take part in sharing checks. A pin needs real access such as `GENERIC_READ`. The test failed first, then passed with that change.
- `BY_HANDLE_FILE_INFORMATION` needs `Pack = 4`. Without it the link count read as 262144. This also would have corrupted size and mtime, which earlier tests did not catch. The hard-link test caught it.

## Design implied for the Executor

1. At preview, store volume serial, file ID, size, mtime, attributes and the ancestor chain identities.
2. At execution: capture the chain, open the Item with a hold (share read only, real access), capture identity from that handle, then compare identity and metadata with the preview and compare the handle's final path with the expected path.
3. Pin every ancestor directory with a handle without share-delete for the duration of the Item. Pinning one level is not enough (see the negative result). This combined scheme is UNVERIFIED as a whole.
4. Copy from the held handle, verify, then release. If the hold cannot be taken, skip with skipped-active; if identity differs, skip with skipped-changed.
5. Item with link count above 1: treat as alias, do not claim the space is freed.

## Limits and not proven

- Reparse types: only junctions tested. Symlinks, mount points and cloud placeholders are UNVERIFIED (symlink creation needs privilege or developer mode; not attempted).
- File systems: only NTFS local. ReFS, exFAT, FAT, network and subst/virtual drives are UNVERIFIED. R7.7 treats non-NTFS as inventory-only.
- Directory Items: a recursive hold over a whole subtree was not built or tested. Only single files were held.
- Cross-volume move (C: to D:) was not run; only same-volume copy. Identity logic is volume-independent, but the full Prepared/Copied/Verified/SourceReleased flow with a crash is UNVERIFIED (task covered elsewhere).
- File ID reuse: NTFS may reuse a file ID after deletion. One delete-and-recreate trial gave a different ID. The reuse rate is UNVERIFIED. Mitigation: always compare size, mtime and attributes with the ID, and keep the hold.
- Metadata cannot detect an edit that restores size and mtime. The hold closes this window only while it is held; between preview and execution only a content hash (not built here) would detect it.
- ACLs, alternate data streams, 8.3 names, long path beyond `\\?\` prefix use, and sparse/compressed files were not tested.
- Concurrency: swaps were applied sequentially in the test thread, not raced against the Executor. Timing-dependent behavior is UNVERIFIED.
- Elevated or Split-Privilege execution was not tested; handle semantics may differ.

## TOCTOU residual risk

- Between preview and execution (minutes to days), nothing holds the object. Detection relies on ID plus metadata plus ancestor recheck. A change that restores metadata is invisible without a content hash.
- Between the ancestor chain check and the file open there is a window. The post-open final-path comparison and the ancestor pins narrow it. Closing it fully requires the pins to be taken before the chain check and the chain to be rechecked after, which was not tested as a sequence.
- After a hold is taken, a process running with backup privileges or a kernel component can still bypass sharing. Not tested.
- A held handle only protects the object; it does not stop another process from creating a new object at the same path. That is harmless for identity binding, and matters for restore conflicts.

## Verdict: PARTIAL

Proven: ID-based identity, alias detection, rename and replace detection, junction swap detection, post-open check, and holding a handle across copy-verify-release for a single file on local NTFS.

Not proven: other reparse types, directory subtree holds, cross-volume flow, ID reuse, raced adversary, content change that restores metadata, and the combined pin-all-ancestors scheme. Under R21.4, destructive features remain held back until those are covered by tests.

## Sources

- Win32 `GetFileInformationByHandleEx`, `FILE_ID_INFO`, `OpenFileById`, `GetFinalPathNameByHandle`, `FindFirstFileNameW`, `CreateFile` sharing rules: Microsoft Learn (not re-fetched during this spike; behavior above was confirmed by the tests, not by the docs).
