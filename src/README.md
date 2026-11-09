# src

Proyek produk, satu proyek per komponen Split-Privilege. Saat ini semuanya shell kosong.

- `MustBeCleaner.Domain`: Policy Core murni (tanpa I/O). Tidak mereferensikan proyek lain.
- `MustBeCleaner.Adapters`: Adapter Catalog.
- `MustBeCleaner.Infrastructure`: Store / Journal dan interop.
- `MustBeCleaner.Inspector`: enumerasi read-only. Tidak boleh mereferensikan Executor.
- `MustBeCleaner.Duplicates`: analisis duplikat read-only. Tidak boleh mereferensikan Executor.
- `MustBeCleaner.Executor`: satu-satunya proyek yang boleh memutasi filesystem.

Setiap folder proyek punya `README.md` sendiri.
