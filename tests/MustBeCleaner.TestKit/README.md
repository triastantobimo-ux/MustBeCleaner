# MustBeCleaner.TestKit

Library tes bersama. Antarmuka publik kecil:

- `Fakes.FakeFileSystem` + `Fakes.Fault`: filesystem in-memory dengan injeksi kegagalan (`CrashAt`, `DiskFull`, `Locked`, `PermissionDenied`, `SourceModifiedDuringCopy`, `DestinationMissing`). Kode yang diuji memanggil `Checkpoint("NamaTahap")` agar crash dapat disuntikkan.
- `Sandboxing.Sandbox`: folder temp terisolasi. Semua path melalui `Resolve`, yang melempar `SandboxEscapeException` di luar root. Mendukung file, folder kosong, hard link, dan junction (jika diizinkan), plus `AddStandardTree()`.
- `Sandboxing.FolderTree` + `FolderTreeArbitrary`: generator FsCheck pohon folder (varian case, nama mirip 8.3, Unicode, folder kosong) dan `Sandbox.Materialize`.
- `Pbt.PbtConfig`, `Pbt.PropertyTag`, `Pbt.MbcPropertyAttribute`: minimal 100 iterasi dan tag `Feature: mustbecleaner-storage-cleaner, Property {n}: {title}`.

Contoh:

```csharp
[MbcProperty(5, "Total byte tanpa overlap")]
public bool Total_is_non_overlapping(/* generated input */) => /* ... */;
```
