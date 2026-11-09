# tests

- `MustBeCleaner.TestKit`: library bersama (fake filesystem, sandbox, generator pohon folder, konvensi PBT).
- `MustBeCleaner.TestKit.Tests`: tes untuk TestKit dan guard struktur solution.
- `MustBeCleaner.Domain.Tests`: tes Policy Core dan property test Domain.
- `MustBeCleaner.Adapters.Tests`: tes skema, katalog, dan update Adapter.
- `MustBeCleaner.Infrastructure.Tests`: tes Journal, logging, dan retention.
- `MustBeCleaner.Inspector.Tests`: tes enumerasi read-only dan total unik.

Jalankan dari root repo dengan SDK 10 aktif (lihat `global.json`):

```powershell
dotnet test MustBeCleaner.sln -m:1 --no-build
```

Build dulu dengan `dotnet build MustBeCleaner.sln -m:1` bila belum ada output build. `-m:1` mencegah kunci file antarproyek.
