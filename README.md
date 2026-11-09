# MustBeCleaner
Must Be Cleaner. Will ensure that all of your junk is clean.

## Struktur dan cara menjalankan tes

Solution `MustBeCleaner.sln`, target `net10.0` (diatur di `Directory.Build.props`; SDK dipin di `global.json`). Layout Split-Privilege dari `architecture_selection.md`:

| Proyek | Peran | Referensi |
|--------|-------|-----------|
| `src/MustBeCleaner.Domain` | Domain dan Policy Core (murni, tanpa I/O) | tidak ada |
| `src/MustBeCleaner.Adapters` | Adapter Catalog (data-only) | tidak ada (ADR 0030) |
| `src/MustBeCleaner.Infrastructure` | Store / Journal, interop | tidak ada |
| `src/MustBeCleaner.Inspector` | Inspector read-only | Domain, Adapters, Infrastructure |
| `src/MustBeCleaner.Duplicates` | Duplicate Analyzer read-only | Domain, Inspector |
| `src/MustBeCleaner.Executor` | Satu-satunya pemutasi filesystem | Domain, Adapters, Infrastructure |
| `tests/MustBeCleaner.TestKit` | Library test bersama: fake filesystem, sandbox, konvensi PBT | FsCheck.Xunit |
| `tests/MustBeCleaner.TestKit.Tests` | Tes untuk TestKit dan guard struktur solution | TestKit |
| `tests/MustBeCleaner.Domain.Tests` | Tes Policy Core | Domain, TestKit |
| `tests/MustBeCleaner.Adapters.Tests` | Tes skema, katalog, dan update Adapter | Adapters, TestKit |
| `tests/MustBeCleaner.Infrastructure.Tests` | Tes Journal, logging, retention | Infrastructure, TestKit |
| `tests/MustBeCleaner.Inspector.Tests` | Tes enumerasi read-only dan total unik | Inspector, TestKit |

Inspector, Duplicates, Adapters, Domain, dan Infrastructure tidak boleh mereferensikan Executor (langsung maupun transitif); `SolutionStructureTests` menjaganya.

Perintah (dari root repo):

```powershell
# global.json meminta SDK 10.0.100 (rollForward latestFeature).
dotnet --version   # harus 10.0.x
dotnet build MustBeCleaner.sln -m:1
dotnet test MustBeCleaner.sln -m:1 --no-build
```

Jika `dotnet --version` masih 9.x, SDK 10 belum ada di PATH. Tambahkan foldernya ke `PATH` dan set `DOTNET_ROOT` ke folder yang sama sebelum menjalankan perintah di atas. `-m:1` mencegah kunci file antarproyek saat build paralel.

Konvensi tes:
- Tes keselamatan hanya memakai `FakeFileSystem` atau `Sandbox` (folder temp sendiri). Data riil pengguna tidak pernah disentuh (R21.2).
- Property test memakai `[MbcProperty(nomor, "judul")]`: minimal 100 iterasi dan nama tes `Feature: mustbecleaner-storage-cleaner, Property {n}: {title}`.
- Istilah ada di `GLOSSARY.md`.
