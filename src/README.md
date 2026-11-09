# src

Proyek produk, satu proyek per komponen Split-Privilege. Domain, Adapters, Infrastructure, dan Inspector sudah berisi implementasi; Duplicates dan Executor masih shell kosong.

- `MustBeCleaner.Domain`: Policy Core murni (tanpa I/O). Tidak mereferensikan proyek lain.
- `MustBeCleaner.Adapters`: Adapter Catalog data-only. Tidak mereferensikan proyek lain (ADR 0030).
- `MustBeCleaner.Infrastructure`: Store / Journal SQLite, logging, retention, dan interop. Tidak mereferensikan proyek lain.
- `MustBeCleaner.Inspector`: enumerasi read-only. Mereferensikan Domain, Adapters, dan Infrastructure; tidak boleh mereferensikan Executor.
- `MustBeCleaner.Duplicates`: analisis duplikat read-only. Mereferensikan Domain dan Inspector; tidak boleh mereferensikan Executor.
- `MustBeCleaner.Executor`: satu-satunya proyek yang boleh memutasi filesystem. Mereferensikan Domain, Adapters, dan Infrastructure.

Setiap folder proyek punya `README.md` sendiri.
