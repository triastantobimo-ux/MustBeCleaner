# Status proyek MustBeCleaner

Diperbarui: 9 November 2026. Sumber kebenaran per tugas: `.kiro/specs/mustbecleaner-storage-cleaner/tasks.md`. Baseline seluruh solution sudah di-commit; pekerjaan lanjutan dicatat di bagian bawah.

## Ringkasan

- Spec lengkap: requirements (26 requirement), design (arsitektur Split-Privilege Pipeline, 21 correctness property), tasks (98 item menurut hitungan tool; 13 tugas daun selesai, 17 checkbox termasuk tugas induk).
- Stack diputuskan: C# di .NET 10 LTS, WPF dengan WPF UI 4.3.0, CommunityToolkit.Mvvm, SQLite, xUnit, FsCheck. Lihat `docs/decisions/0001-stack.md`.
- Belum ada UI produk, belum ada fitur yang mengubah file pengguna. I/O yang ada hanya enumerasi read-only dan Data_Directory (journal SQLite, log, retention).
- Build dan test seluruh solution sudah dijalankan sebagai satu kesatuan: 12 proyek, 0 warning, 0 error; 392 test lulus, 0 gagal, 0 dilewati (TestKit 48, Adapters 111, Infrastructure 66, Domain 133, Inspector 34).

## Tugas selesai

| Tugas | Hasil | Bukti uji terakhir yang dilaporkan |
|---|---|---|
| 1.1 Spike stack | .NET 10 LTS, versi paket dipin; `docs/research/stack-spike.md` | 7 test lulus, smoke WPF lulus |
| 1.2 Spike identitas objek | Verdict PARTIAL; `docs/research/identity-binding-spike.md` | 29 test lulus (net9.0) |
| 1.3 Kerangka uji | Solution, TestKit (fake filesystem, sandbox, konvensi PBT) | 48 test lulus |
| 2.1 Tipe inti Domain | TriState, Status, ItemIdentity, Classification, dll | 24 test lulus |
| 2.2 Normalisasi path dan exclusion | `PathPolicy`, `PathScope` | 65 test lulus (Domain) |
| 2.3 classify dan proteksi | `PolicyEngine`, `ProtectionRules`, `TransactionSet` | 133 test lulus (Domain) |
| 3.1 Skema Adapter | Skema JSON tertutup dan loader yang menolak konten eksekusi | 41 test baru lulus |
| 3.2 Adapter awal | 18 file di `policies/adapters/`, semua inventory-only | 67 test lulus (Adapters) |
| 3.3 matchAdapter dan importUpdate | Pencocokan versi, impor offline dengan provenance dan diff | 111 test lulus (Adapters) |
| 4.1 Journal Store | SQLite WAL, fail-closed, journal-before-mutation | 22 test lulus |
| 4.2 Logging, Exception_Log, retention | Logging tanpa secret, retention terbatas | 66 test lulus (Infrastructure) |
| 5.1 Enumerasi read-only | `StorageInspector`, interop Win32 read-only | 24 test lulus |
| 5.2 Identitas Item dan total unik | `UniqueTotals` (hard link, tanpa double count) | 34 test lulus (Inspector) |

Sub-tugas opsional bertanda `*` (property test) belum dikerjakan. Tugas induk 1 sampai 4 ditandai selesai otomatis oleh tool; tugas induk 5 belum selesai.

## Verifikasi terakhir

Build dan test dijalankan pada 9 November 2026 dengan SDK 10.0.403 dari `C:\Users\mika\.dotnet10` (bukan `dotnet` 9.0.320 bawaan PATH), Release dan Debug, `-m:1`. Rincian: 12 proyek, 0 warning, 0 error; 392 test lulus, 0 gagal, 0 dilewati.

## Tugas berikutnya (tidak dimulai)

Tugas 5.3 (coverage, progress, pause/cancel, low-I/O) siap dikerjakan. Tugas lain dalam antrean masih berstatus queued. Untuk melanjutkan, jalankan tugas dari tasks.md.

## Keputusan yang menunggu pengguna

1. ADR 0031 (usulan): konvensi proteksi Adapter. "Protected" saat ini hanya konvensi nama blocker, tidak dipaksa loader. Pilihan: kosakata terkontrol, field `protection`, definisi root path.
2. ADR 0032 (usulan): siapa memegang kunci penandatangan paket Adapter dan dari mana kunci tepercaya berasal. Sampai diputuskan, tidak ada paket nyata yang bisa diverifikasi.
3. Ambang ruang bebas 100 MiB di R2.6 (dipilih oleh saya, belum disetujui).
4. Distribusi (portable vs installer, signing), versi Windows minimum, dan nama produk: masih terbuka.

## Temuan penting

- Gerbang rilis fitur destruktif (R21.4) masih PARTIAL. Terbukti: identitas file 128-bit, deteksi rename/replace, deteksi junction swap, penguncian handle saat copy-verify. Belum terbukti: symlink dan mount point, subtree direktori, alur lintas volume C: ke D:, penggunaan ulang file ID, serangan balapan waktu.
- .NET 9 dan 8 berakhir dukungan 10 November 2026. Proyek memakai .NET 10 yang terpasang di `C:\Users\mika\.dotnet10` (tidak mengubah PATH). Gunakan:
  `$env:PATH="$env:USERPROFILE\.dotnet10;$env:PATH"; $env:DOTNET_ROOT="$env:USERPROFILE\.dotnet10"`
- Jangan memberi nama root namespace `Wpf` (bentrok dengan `Wpf.Ui`). Bangun dengan `-m:1` untuk menghindari kunci file antarproyek.

## Batas dan hal yang belum diverifikasi

- Nama file dan folder dalam Adapter (mis. `auth.json`, nama folder Chromium) dan nama proses bersifat konvensional atau dugaan, bukan dibaca dari mesin pengguna.
- Pola proteksi belum diuji terhadap `evidence/*.json`; pola seperti `.ldb` dan `.wal` tanpa nama DB belum tercakup.
- Pengujian symlink dilewati karena tidak bisa dibuat di mesin ini; hanya klasifikasi tag yang diuji.
- Beberapa tugas tidak mengikuti TDD satu-test-satu-merah secara ketat (2.2, 2.3, 3.2, 4.2); tercatat jujur di laporan masing-masing.
- Tampilan WPF (Mica, palet pastel) belum pernah dilihat; hanya logika dan smoke test.
- Tidak ada data nyata pengguna yang dibaca, diubah, atau dihapus oleh tugas mana pun.

## Peta dokumentasi

| Lokasi | Isi |
|---|---|
| `PRODUCT.md`, `docs/PRODUCT_BLUEPRINT.md`, `docs/LOCAL_STORAGE_REVIEW.md`, `docs/research/competitor-research.md` | Blueprint dan riset awal |
| `.kiro/specs/mustbecleaner-storage-cleaner/` | requirements, design, architecture_selection, tasks |
| `docs/decisions/` | ADR 0001 sampai 0050 |
| `docs/research/stack-spike.md`, `identity-binding-spike.md` | Hasil spike |
| `GLOSSARY.md` | Istilah domain (hanya ditambah, tidak ditimpa) |
| `README.md` dan README per proyek di `src/`, `tests/`, `policies/`, `spikes/` | Cara menjalankan dan batas tiap modul |
| `spikes/` | Kode sekali pakai; jangan direferensikan oleh `src/` |
