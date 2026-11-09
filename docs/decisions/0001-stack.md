# ADR 0001: Stack aplikasi

Status: diputuskan, 9 November 2026. Dasar: permintaan pengguna "lakukan pemilihan stack".

## Keputusan

- Bahasa dan runtime: C# di .NET LTS terbaru (target .NET 10; versi dipin saat scaffold setelah cek compatibility).
- UI: WPF (Windows-only, tanpa WebView).
- Persistence: SQLite lokal di Data_Directory yang dapat dipilih (D:).
- Windows interop: P/Invoke untuk volume ID, file ID, reparse point, dan IFileOperation.
- Property-based test: FsCheck (dengan xUnit).
- Distribusi (portable vs installer, signing): tetap terbuka.

## Alasan

1. Executor perlu API Win32 tingkat rendah (identitas file, reparse tag, ancestor check). .NET memberi akses langsung tanpa lapisan jembatan.
2. Satu bahasa untuk engine, UI, dan test menjaga komponen Split-Privilege tetap sederhana: pemisahan dilakukan lewat proyek terpisah, dan proyek Inspector tidak mereferensikan proyek Executor.
3. WPF punya virtualisasi tabel matang untuk daftar kandidat besar (ratusan ribu entry).
4. Tidak ada WebView: tidak menambah profil cache Chromium/WebView2 pada mesin yang sedang kekurangan C:.
5. Lingkungan lokal sudah memiliki .NET SDK dan Windows Desktop runtime; Rust dan cargo belum terpasang.

## Alternatif yang ditolak

- WinUI 3: packaging dan runtime Windows App SDK menambah risiko tanpa keuntungan untuk tabel padat.
- Tauri (Rust + TypeScript): dua toolchain, cargo belum terpasang, dan WebView2 membawa profil cache sendiri.
- Electron: membundel Chromium dan Node, bertentangan dengan tujuan footprint minimal.

## Batas pengetahuan

- Belum ada benchmark. Pilihan ini adalah penilaian engineering, bukan hasil pengukuran.
- SDK terpasang hanya 9.0.320; .NET 10 SDK perlu dipasang. Status dukungan versi harus dicek ulang saat scaffold.
- Pengikatan identitas objek (spike 1.2) belum dibuktikan di stack ini; hasil negatif tetap menahan fitur destruktif.

## Toolchain UI dan pengembangan (ditambahkan)

Tujuan: WPF dengan tampilan modern Fluent (Windows 11) tanpa melanggar R25 (pastel sage, tanpa gradient dekoratif, glow, atau KPI card).

| Kebutuhan | Pilihan | Catatan |
|---|---|---|
| Kontrol dan tema Fluent | WPF UI (lepoco/wpfui, namespace Wpf.Ui) | Mica/Acrylic, title bar gelap, NavigationView, Snackbar, Dialog, light/dark. Akses via NuGet |
| MVVM | CommunityToolkit.Mvvm | Source generator untuk ObservableProperty dan RelayCommand |
| DI dan lifetime | Microsoft.Extensions.Hosting | Inspector dan Executor didaftarkan terpisah |
| SQLite | Microsoft.Data.Sqlite | Mode WAL untuk Journal Store |
| Tabel besar | DataGrid WPF dengan virtualisasi, atau ListView WPF UI | Dievaluasi di spike; tabel kandidat harus tetap responsif untuk ratusan ribu baris |
| Storage map (view sekunder) | Treemap gambar sendiri (DrawingVisual) | Tanpa pustaka chart berat; ukuran besar tidak ditampilkan sebagai "sampah" |
| Ikon | Fluent System Icons (via WPF UI) | Status selalu teks + ikon, bukan warna saja |
| Tes | xUnit, FsCheck.Xunit | PBT minimal 100 iterasi |
| Audit aksesibilitas | Accessibility Insights for Windows | Untuk R26; bukan bukti WCAG penuh |
| Debug visual tree | Snoop | Alat dev saja, tidak dibundel |
| SDK | .NET 10 SDK | Perlu dipasang; SDK terpasang saat ini 9.0.320 |

## Versi terpin (spike 1.1, 9 November 2026)

Diverifikasi di `spikes/stack-compat/`; detail dan bukti di `docs/research/stack-spike.md`.

- .NET 10 LTS (aktif, dukungan sampai 14 Nov 2028): SDK 10.0.403, runtime 10.0.14. .NET 9 dan 8 berakhir 10 Nov 2026.
- WPF-UI 4.3.0, CommunityToolkit.Mvvm 8.4.2, Microsoft.Extensions.Hosting 10.0.14, Microsoft.Data.Sqlite 10.0.14.
- xunit 2.9.3, xunit.runner.visualstudio 4.0.1, Microsoft.NET.Test.Sdk 18.11.0, FsCheck.Xunit 3.6.0.
- Tidak diverifikasi: tampilan Mica, fallback solid, tabel besar. Jangan memakai root namespace `Wpf` (bentrok dengan `Wpf.Ui`).

(Catatan lama di bawah, digantikan oleh versi terpin di atas.) Versi paket dipin saat scaffold setelah cek NuGet. Dari pencarian web, WPF UI terakhir terlihat bernomor 4.x (UNVERIFIED nomor terbaru) dan CommunityToolkit.Mvvm 8.4.x (UNVERIFIED, dari sumber sekunder). Kompatibilitas WPF UI dengan .NET 10 dicek di spike 1.1.

Batas: tampilan "keren" diukur lewat konsistensi, kepadatan informasi, dan gerak halus 150 sampai 200 ms, bukan efek dekoratif. Mica bergantung versi Windows; fallback ke warna solid pastel pada sistem tanpa dukungan.
