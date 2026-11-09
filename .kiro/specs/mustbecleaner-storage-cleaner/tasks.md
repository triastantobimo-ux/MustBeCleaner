# Implementation Plan: MustBeCleaner Storage Cleaner

## Overview

Rencana ini menurunkan `requirements.md` (R1 sampai R26) dan `design.md` (arsitektur Split-Privilege Pipeline, 21 correctness properties) menjadi tugas koding bertahap. Urutan mengikuti fase prioritas: Fase 1 Inspector (read-only), Fase 2 Controlled cleanup dan recovery, Fase 3 Exact duplicates, Fase 4 Similar items, Fase 5 Hardening.

Catatan penting:

- Stack final masih keputusan terbuka. Tugas 1.1 adalah spike evaluasi tanpa komitmen. Tugas lain ditulis netral terhadap bahasa dan baru dikerjakan setelah pengguna menyetujui stack secara eksplisit. Nama stack di spike hanyalah kandidat.
- Tidak ada tugas yang menghapus, memindahkan, atau mengubah data riil pengguna. Seluruh pengujian keselamatan memakai fake filesystem, fixture, atau sandbox/VM (R21.2).
- Tidak ada registry writer. Registry hanya review-only dan nonaktif secara default (R17).
- Hanya Executor yang memegang kemampuan mutasi filesystem. Komponen lain tidak boleh bergantung pada API mutasi.
- Pustaka property-based test dipilih sesuai stack final, bukan diimplementasikan dari nol. Setiap properti memakai minimal 100 iterasi dan tag `Feature: mustbecleaner-storage-cleaner, Property {n}: {judul}`.

## Tasks

### Fase 0: Keputusan Stack dan Fondasi Uji

- [x] 1. Spike keputusan stack dan fondasi pengujian aman
  - [x] 1.1 Spike evaluasi stack/framework (tanpa komitmen)
    - Buat prototipe kecil sekali pakai untuk kandidat stack (rekomendasi blueprint: C# + WPF + SQLite; alternatif WinUI 3 dan Tauri) yang mengukur: enumerasi metadata Windows, akses volume ID dan file ID, SQLite WAL, virtualisasi tabel, dan ketersediaan pustaka PBT
    - Tulis hasil dan trade-off ke `docs/research/stack-spike.md` sebagai masukan keputusan; jangan memilih stack final dan jangan memindahkan kode spike ke produk
    - Tandai keputusan stack, distribusi, dan versi Windows minimum sebagai menunggu persetujuan pengguna
    - _Requirements: 23.7, 22.1, 7.6_

  - [x] 1.2 Spike pengikatan identitas objek (gerbang rilis destruktif)
    - Buktikan pada fixture di folder sandbox bahwa objek yang direview dapat diikat ke objek yang dipindah (volume ID, file ID, pemeriksaan ancestor, deteksi junction swap setelah preview)
    - Dokumentasikan hasil ke `docs/research/identity-binding-spike.md`; hasil negatif berarti fitur destruktif ditahan dari rilis
    - _Requirements: 21.4, 7.3, 7.6_

  - [x] 1.3 Siapkan kerangka uji: fake filesystem dan fixture sandbox
    - Buat fake filesystem dengan injeksi kegagalan (crash per tahap, disk penuh, file terkunci, sumber berubah, permission denied)
    - Buat generator fixture pohon folder (case, 8.3, Unicode, folder kosong, hard link, reparse) di direktori sementara terisolasi, tanpa menyentuh data riil
    - Siapkan runner PBT dan konvensi tag properti
    - _Requirements: 21.1, 21.2_

### Fase 1: Inspector (Read-Only)

- [x] 2. Policy Core (murni, tanpa I/O)
  - [x] 2.1 Definisikan tipe dan kontrak inti
    - Implementasikan `ItemIdentity`, `TriState`, `Status` (Protected, Review, Eligible, InventoryOnly), `Classification`, `ItemFacts`, `WorldFacts`, `RuleSet` berversi
    - _Requirements: 5.1, 5.2, 5.6, 7.6_

  - [x] 2.2 Implementasikan `normalizePath`, penolakan path terlarang, dan exclusion
    - Normalisasi case, nama 8.3, prefix path panjang; tolak UNC, device path, alternate data stream
    - Terapkan exclusion yang mengalahkan include
    - _Requirements: 1.6, 1.7, 7.4, 7.5_

  - [x] 2.3 Implementasikan `classify` dengan daftar proteksi dan eligibility Tri_State
    - Urutan evaluasi: exclusion, penolakan path, protection rule, kecocokan Adapter, kondisi eligibility
    - Protection mengalahkan Adapter, skor, custom include, dan mode administrator; unknown menolak; folder tidak dikenal menjadi InventoryOnly; volume non-NTFS lokal menjadi InventoryOnly
    - Catat rule ID, versi rule, dan alasan; jangan jadikan nama `cache`/`temp`, ekstensi, tanggal, atau ketiadaan proses sebagai dasar tunggal Eligible
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 5.9, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 7.7, 11.1_

  - [ ]* 2.4 Tulis property test untuk proteksi mengalahkan allow
    - **Property 1: Proteksi mengalahkan allow**
    - **Validates: Requirements 5.4, 6.6**

  - [ ]* 2.5 Tulis property test untuk unknown memblokir dan Eligible hanya jika semua true
    - **Property 2: Unknown memblokir, Eligible hanya jika semua true**
    - **Validates: Requirements 5.1, 5.2, 5.3, 11.1**

  - [ ]* 2.6 Tulis property test untuk klasifikasi deterministik
    - **Property 3: Klasifikasi deterministik**
    - **Validates: Requirements 5.5, 5.6**

  - [ ]* 2.7 Tulis property test untuk exclusion, path terlarang, dan normalisasi idempoten
    - **Property 4: Exclusion mengalahkan include; path terlarang ditolak; normalisasi idempoten**
    - **Validates: Requirements 1.7, 7.4, 7.5**

- [x] 3. Adapter Catalog (data-only)
  - [x] 3.1 Definisikan skema Adapter dan loader deklaratif
    - Skema: rule ID, versi, sumber referensi, fingerprint aplikasi dan versi, relative path, kategori, blocker, mapping proses, bukti, biaya regenerasi, kebijakan recovery
    - Loader menolak konten yang dapat dieksekusi (PowerShell, JavaScript, executable)
    - _Requirements: 8.1, 8.2_

  - [x] 3.2 Buat data Adapter awal berstatus inventory-only
    - Cakup `.codex`, Local\Codex, Local\OpenAI, Packages\OpenAI.Codex, Claude, claude-mem, AutoClaw 2, Code, Qoder, OpenCode, Local\Temp, go-build
    - Terapkan aturan Protected/Review/inventory-only per R8.4 sampai R8.12 (termasuk worktrees tanpa checkbox cleanup)
    - _Requirements: 8.3, 8.4, 8.5, 8.6, 8.7, 8.8, 8.9, 8.10, 8.11, 8.12_

  - [x] 3.3 Implementasikan `matchAdapter` dan `importUpdate`
    - Fingerprint atau versi tidak cocok menghasilkan Review
    - Impor offline memverifikasi provenance, menampilkan diff scope, dan tidak memperluas scope tanpa persetujuan
    - _Requirements: 8.13, 8.14, 23.5_

  - [ ]* 3.4 Tulis property test untuk Adapter tidak cocok menjadi Review
    - **Property 18: Adapter tidak cocok menjadi Review**
    - **Validates: Requirements 5.8, 8.13**

  - [ ]* 3.5 Tulis unit test Adapter awal
    - Verifikasi klasifikasi `.codex`, claude-mem, Electron cache vs cookies/IndexedDB, Local\Temp, go-build, AutoClaw 2 pada fixture
    - _Requirements: 8.4, 8.5, 8.6, 8.7, 8.8, 8.9, 8.10, 8.11_

- [x] 4. Journal Store (batas persistence)
  - [x] 4.1 Implementasikan skema persistence dan Data_Directory fail-closed
    - Entitas: ScanRun, ScanCoverage, DiscoveredItem, AppOwnershipEvidence, RuleVersion, Classification, DuplicateGroup, KeeperDecision, CleanupPlan, PlanItem, ActionAttempt, QuarantineEntry, RestoreAttempt, ExceptionEntry
    - Simpan di Data_Directory lokal dengan ACL pengguna; berhenti tanpa fallback ke C: bila lokasi hilang atau penuh; kecualikan Data_Directory dan Quarantine dari scan
    - Store menolak mutasi tanpa entri Journal durable
    - _Requirements: 12.9, 12.10, 16.1, 16.4, 16.5_

  - [x] 4.2 Implementasikan logging tanpa secret, Exception_Log, dan retention
    - Catat item dilewati atau ditolak dengan kode alasan; jangan tulis isi file, token, atau secret
    - Retention terkonfigurasi untuk laporan dan riwayat scan
    - _Requirements: 6.8, 16.2, 16.3, 16.6, 22.2_

  - [ ]* 4.3 Tulis property test untuk log tidak memuat secret
    - **Property 20: Log tidak memuat secret**
    - **Validates: Requirements 16.3**

- [ ] 5. Inspector (enumerasi read-only)
  - [x] 5.1 Implementasikan enumerasi metadata read-only dan penanganan reparse point
    - Berjalan sebagai standard user; hanya API enumerasi metadata, tanpa API hapus, pindah, tulis, atau ACL
    - Jangan telusuri junction, symlink, mount point, cloud placeholder, atau reparse tag tidak terklasifikasi
    - Tandai Windows critical root sebagai Protected; tolak access denied tanpa eskalasi
    - _Requirements: 1.1, 1.3, 1.4, 2.5, 6.7, 6.8, 7.1, 7.2_

  - [x] 5.2 Implementasikan identitas Item, hard link, dan perhitungan total unik
    - Simpan volume ID, file ID, canonical path, ukuran, mtime, atribut; hitung hard link sekali; hindari penjumlahan parent-child dan lintas Scope
    - Ukuran tak terukur dilaporkan `unavailable`, bukan nol
    - _Requirements: 3.5, 3.7, 4.6, 7.6_

  - [ ] 5.3 Implementasikan coverage, progress, pause/cancel, dan mode low-I/O
    - Catat status per root, jumlah entry, reparse dilewati, access-denied, durasi; label hasil parsial sebagai tidak lengkap dengan ukuran sebagai batas bawah; catat item berubah-saat-scan
    - Progress determinate/indeterminate; tolak scan bila Data_Directory tidak dapat ditulis pada ruang bebas < 100 MiB
    - Simpan caveat scan bersama hasil
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.6, 2.7, 3.1, 3.2, 3.3, 3.4, 3.6_

  - [ ] 5.4 Implementasikan storage overview dan delta antar-scan
    - Total dan ruang bebas drive, ranking folder dan aplikasi berdasarkan Logical_Bytes, pembedaan lokasi instalasi vs data aplikasi, large files tanpa status Eligible otomatis
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [ ] 5.5 Implementasikan Registry_Review read-only
    - Nonaktif default di area Advanced, tanpa checkbox tercentang, tanpa kemampuan modifikasi, tanpa registry writer
    - _Requirements: 17.1, 17.2, 17.3, 17.4, 17.5_

  - [ ]* 5.6 Tulis property test untuk total byte tanpa overlap
    - **Property 5: Total byte tanpa overlap**
    - **Validates: Requirements 3.7, 4.6, 10.2, 14.4, 18.7**

  - [ ]* 5.7 Tulis unit test coverage dan scan parsial
    - Cakup pembatalan, timeout, nol item vs tidak terbaca, item berubah saat scan, reparse tak terklasifikasi pada fixture
    - _Requirements: 2.3, 2.7, 3.2, 3.4, 7.2_

- [ ] 6. UI Shell Fase 1 (workspace inspeksi)
  - [ ] 6.1 Bangun shell, navigasi, tema, dan bahasa
    - Antarmuka Bahasa Indonesia; navigasi Ringkasan, Cache aplikasi, AI & developer, File sementara, Duplikat, Similar items, Recovery, Riwayat & laporan, Proteksi & pengaturan
    - Tema light/dark/system pastel sage, Segoe UI, monospace hanya untuk path dan hash, tabular numerals, transisi 150 sampai 200 ms, reduced motion, amber hanya peringatan, coral hanya error, tanpa gradient dekoratif atau KPI card
    - _Requirements: 24.1, 24.2, 25.1, 25.2, 25.3, 25.4, 25.5, 25.6, 25.7, 25.8_

  - [ ] 6.2 Bangun tabel kandidat, inspector kanan, dan filter
    - Kolom pilih, item, aplikasi, lokasi, Logical_Bytes, mtime, status, alasan; filter Aplikasi, Lokasi, Jenis data, Status; detail Item lengkap tanpa pratinjau isi file sensitif
    - Status dengan teks dan ikon; unknown tampil "Belum dapat dinilai" beserta alasan; label status bernuansa batas
    - Semua checkbox awal tidak tercentang; inspector melipat menjadi panel pada jendela sempit; layout 1280×800 dan 1920×1080 pada DPI 100% sampai 200%
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.7, 10.1, 24.3, 24.4, 24.5, 24.9, 24.10_

  - [ ] 6.3 Bangun first run, pemilihan Scope, dan daftar exclusion
    - Jelaskan lokasi data aplikasi dan tidak ada pembersihan otomatis; tampilkan daftar root sebelum scan; kelola exclusion
    - _Requirements: 1.2, 1.5, 1.6_

  - [ ] 6.4 Implementasikan export CSV/JSON/HTML lokal dengan opsi redaksi path
    - _Requirements: 9.8_

  - [ ]* 6.5 Tulis tes UI keyboard, fokus, label, dan kontras
    - Alur keyboard-only, indikator fokus, label checkbox per Item, pengumuman screen reader, kontras 4,5:1 dan 3:1
    - _Requirements: 26.1, 26.2, 26.3, 26.4, 26.5, 26.6_

- [ ] 7. Checkpoint Fase 1 - Pastikan semua tes lulus
  - Pastikan semua tes lulus, tanyakan kepada pengguna bila ada pertanyaan.

### Fase 2: Controlled Cleanup dan Recovery

- [ ] 8. Plan Service
  - [ ] 8.1 Implementasikan seleksi dan `selectEligible`
    - Seleksi awal kosong; "Pilih kandidat yang memenuhi policy" hanya memilih Eligible; filter aplikasi tidak mengubah seleksi; total tanpa overlap
    - _Requirements: 9.5, 9.6, 10.1, 10.2_

  - [ ] 8.2 Implementasikan `approve` dan Manifest immutable
    - Tolak Plan yang memuat Item non-Eligible atau Fuzzy_Candidate; tolak grup tanpa Keeper; hash dari serialisasi kanonik; persetujuan hanya pada Item di Manifest, bukan wildcard folder
    - Ringkasan Plan: total, estimasi Allocated_Bytes, metode recovery, tujuan Quarantine, dampak aplikasi, item dilewati, ID dan hash Manifest
    - _Requirements: 10.3, 10.4, 10.5, 10.6, 10.7, 18.4, 20.2_

  - [ ]* 8.3 Tulis property test untuk seleksi hanya Eligible dan dimulai kosong
    - **Property 6: Seleksi hanya mengambil Eligible dan dimulai kosong**
    - **Validates: Requirements 9.5, 9.6, 10.1**

  - [ ]* 8.4 Tulis property test untuk Plan dengan Item non-Eligible atau fuzzy ditolak
    - **Property 7: Plan dengan Item non-Eligible atau fuzzy ditolak**
    - **Validates: Requirements 10.7, 20.2, 20.8**

- [ ] 9. Executor (satu-satunya pemutasi filesystem)
  - [ ] 9.1 Implementasikan preflight dan revalidasi independen
    - Terima hanya Manifest ID; periksa aplikasi idle, identitas dan ancestor tidak berubah, kapasitas tujuan, Journal dapat ditulis, versi policy sama; evaluasi ulang proteksi via Policy Core
    - Outcome skipped-active, skipped-changed, atau penolakan eksekusi
    - _Requirements: 10.5, 11.2, 11.3, 11.4, 11.5, 11.6_

  - [ ] 9.2 Implementasikan Quarantine C: ke D: dengan tahap Journal
    - Tahap Prepared, Copied, Verified, SourceReleased per Item; verifikasi checksum, ukuran, dan metadata yang didukung sebelum melepas sumber; batalkan pelepasan bila sumber berubah; tolak metadata tidak didukung
    - Hanya menawarkan metode bila D: NTFS lokal tervalidasi; tanpa fallback diam-diam ke C:
    - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5, 12.9, 16.1_

  - [ ] 9.3 Implementasikan Recycle Bin, permanent purge terpisah, dan maintenance command
    - Baca outcome per Item, tanpa fallback ke penghapusan permanen; purge hanya aksi eksplisit terpisah; command bawaan aplikasi sebagai maintenance dengan batas dinyatakan
    - _Requirements: 12.7, 12.8, 12.11_

  - [ ] 9.4 Implementasikan state machine eksekusi dan pembatalan
    - Draft, Scanning, Review, PlanReady, Preflight, Running, Completed, Partial, Failed, Cancelled; catat timestamp, scan ID, plan ID, kode alasan; cancel menghentikan Item berikutnya konsisten dengan Journal; tanpa klaim rollback global
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5, 13.6_

  - [ ] 9.5 Implementasikan `recoverOnStartup`
    - Lapor duplicate-pending dan recovery-required pada startup tanpa menebak hasil
    - _Requirements: 12.6, 21.3_

  - [ ] 9.6 Implementasikan restore sebagai job terpisah
    - Verifikasi integritas dan metadata, tolak bila aplikasi aktif, hentikan bila target berisi data, pertahankan Item di Quarantine saat gagal, Journal restore sendiri
    - _Requirements: 15.1, 15.2, 15.3, 15.4, 15.5, 15.6_

  - [ ]* 9.7 Tulis property test untuk urutan Journal dan kehilangan data nol
    - **Property 13: Urutan Journal dan kehilangan data nol**
    - **Validates: Requirements 12.2, 12.3, 12.6, 21.3**

  - [ ]* 9.8 Tulis property test restore tidak menimpa dan mempertahankan Quarantine
    - **Property 14: Restore tidak menimpa dan mempertahankan Quarantine saat gagal**
    - **Validates: Requirements 15.3, 15.4**

  - [ ]* 9.9 Tulis property test preflight melewati Item berubah atau aktif
    - **Property 15: Preflight melewati Item yang berubah atau aktif**
    - **Validates: Requirements 11.3, 11.4, 11.5, 11.6**

  - [ ]* 9.10 Tulis property test state machine hanya menerima transisi sah
    - **Property 17: State machine hanya menerima transisi sah**
    - **Validates: Requirements 13.1, 13.2**

- [ ] 10. Reporting
  - [ ] 10.1 Implementasikan laporan dan akuntansi byte terpisah
    - Logical_Bytes terpilih, estimasi Allocated_Bytes, Logical_Bytes diproses, selisih ruang bebas teramati, byte Quarantine; Recycle volume sama bukan Reclaimed_Bytes; kelompok outcome; ruang bebas sebelum dan sesudah; okupansi Quarantine; peringatan bila penulisan laporan gagal; tanpa skor kesehatan atau klaim kinerja
    - _Requirements: 14.1, 14.2, 14.3, 14.4, 14.5, 14.6, 14.7, 14.8_

  - [ ]* 10.2 Tulis property test akuntansi outcome dan byte konsisten
    - **Property 16: Akuntansi outcome dan byte konsisten**
    - **Validates: Requirements 3.5, 13.6, 14.1, 14.3, 14.5**

  - [ ]* 10.3 Tulis property test round trip serialisasi Manifest dan laporan
    - **Property 19: Round trip serialisasi Manifest dan laporan**
    - **Validates: Requirements 9.8, 10.4**

- [ ] 11. UI Fase 2
  - [ ] 11.1 Bangun ringkasan pilihan, Plan, dan persetujuan Manifest
    - Ringkasan pilihan dan metode tindakan di bagian bawah; tombol pilih kandidat sesuai policy; UI hanya mengirim Manifest ID ke Executor
    - _Requirements: 9.5, 10.3, 10.4, 10.5, 24.3_

  - [ ] 11.2 Bangun progress eksekusi, hasil, dan pesan state khusus
    - Progress dan outcome per Item; "Proses selesai sebagian"; catatan Recycle Bin belum tentu menambah ruang; pesan khusus first-run, empty scope, scanning, canceled partial, access denied, no eligible, app active, disk full, source changed, destination missing, restore conflict, report failure
    - _Requirements: 13.3, 24.6, 24.7, 24.8_

  - [ ] 11.3 Bangun tampilan Recovery dan Riwayat & laporan
    - _Requirements: 14.8, 15.1, 24.2_

- [ ] 12. Pengujian adversarial dan gerbang rilis Fase 2 (fixture/sandbox saja)
  - [ ] 12.1 Implementasikan suite adversarial pada fixture
    - Cakup protected path dan alias, file sistem tanpa ekstensi, path traversal, case, 8.3, UNC, device path, junction swap setelah preview, perubahan identitas, database dengan WAL, proses tak dapat diakses, long path, file terkunci, sumber berubah saat copy, D: penuh atau hilang, kegagalan penulisan laporan, konflik restore, compressed/sparse/ADS, folder bersarang overlap, permission denied
    - Jalankan hanya di fake filesystem, direktori sementara terisolasi, atau VM; jangan menyentuh data riil pengguna
    - _Requirements: 21.1, 21.2_

  - [ ] 12.2 Implementasikan injeksi crash pada setiap tahap Journal
    - Verifikasi pemulihan startup ke status terlaporkan tanpa kehilangan data
    - _Requirements: 21.3_

  - [ ] 12.3 Tetapkan gerbang rilis Fase 2
    - Korpus false positive kritikal harus nol; fitur destruktif tetap dinonaktifkan bila hasil spike 1.2 belum membuktikan pengikatan identitas
    - _Requirements: 21.4, 21.5_

- [ ] 13. Checkpoint Fase 2 - Pastikan semua tes lulus
  - Pastikan semua tes lulus, tanyakan kepada pengguna bila ada pertanyaan.

### Fase 3: Exact Duplicates

- [ ] 14. Duplicate Analyzer (read-only)
  - [ ] 14.1 Implementasikan exact duplicate file
    - Hanya pada folder data pilihan pengguna (bukan executable tree, repository, package store, runtime, AppData, folder sistem secara default); ukuran, SHA-256 penuh, verifikasi byte; hard link sebagai alias; file tak terbaca dikeluarkan dan dicatat
    - _Requirements: 18.1, 18.2, 18.3, 18.7, 18.11_

  - [ ] 14.2 Implementasikan hash cache dengan invalidasi identitas
    - Invalidasi saat ukuran, mtime, file ID, atau atribut berubah; tidak memakai mtime dan ukuran saja untuk keputusan destruktif
    - _Requirements: 18.10_

  - [ ] 14.3 Implementasikan `buildFolderManifest` dan `findExactFolders`
    - Entri terurut kanonik (relative path, tipe, ukuran, hash isi); nama root boleh berbeda; folder kosong dan tipe entry dihitung; nyatakan identitas konten bukan kesamaan ACL, owner, atribut
    - _Requirements: 19.1, 19.2, 19.3, 19.6, 19.8_

  - [ ] 14.4 Implementasikan penandaan unsupported-for-cleanup dan penolakan overlap
    - Symlink, ADS, EFS, sparse/cloud, topologi hard link, ambiguitas case-sensitive, entry tidak terbaca; tolak parent-child
    - _Requirements: 19.4, 19.5_

  - [ ] 14.5 Integrasikan Keeper dengan Plan Service dan Executor
    - Wajib pilih Keeper, pertahankan sedikitnya satu salinan terverifikasi, verifikasi ulang identitas dan isi saat eksekusi, batalkan grup bila Keeper hilang atau berubah; tanpa penggantian hard link otomatis
    - _Requirements: 18.4, 18.5, 18.6, 18.9, 19.7_

  - [ ]* 14.6 Tulis property test exact duplicate ditentukan oleh isi
    - **Property 10: Exact duplicate ditentukan oleh isi**
    - **Validates: Requirements 18.2, 18.3, 19.1, 19.3**

  - [ ]* 14.7 Tulis property test Folder_Manifest tak bergantung urutan enumerasi
    - **Property 9: Folder_Manifest tak bergantung urutan enumerasi**
    - **Validates: Requirements 19.2, 19.8**

  - [ ]* 14.8 Tulis property test folder fitur tidak didukung atau overlap ditolak
    - **Property 11: Folder dengan fitur tidak didukung atau overlap ditolak**
    - **Validates: Requirements 19.4, 19.5**

  - [ ]* 14.9 Tulis property test cache hash diinvalidasi saat identitas berubah
    - **Property 12: Cache hash diinvalidasi saat identitas berubah**
    - **Validates: Requirements 18.10**

  - [ ]* 14.10 Tulis property test Keeper dipertahankan per grup
    - **Property 8: Keeper dipertahankan per grup**
    - **Validates: Requirements 18.4, 18.6, 19.7**

  - [ ] 14.11 Bangun UI Duplikat
    - Semua grup tidak tercentang; pilih Keeper; pesan no duplicates
    - _Requirements: 18.8, 24.8_

- [ ] 15. Checkpoint Fase 3 - Pastikan semua tes lulus
  - Pastikan semua tes lulus, tanyakan kepada pengguna bila ada pertanyaan.

### Fase 4: Similar Items (Review-Only)

- [ ] 16. Fuzzy review lokal
  - [ ] 16.1 Implementasikan kecocokan nama/path
    - Normalisasi token dan edit distance tanpa membaca isi; tidak pernah menjadi Exact_Duplicate
    - _Requirements: 20.1, 20.3, 20.8_

  - [ ] 16.2 Implementasikan kecocokan teks dengan tampilan bagian berbeda
    - Tanpa menyimpan salinan isi penuh di cache
    - _Requirements: 20.4, 22.3_

  - [ ] 16.3 Implementasikan kecocokan gambar berdampingan dengan resolusi
    - _Requirements: 20.5_

  - [ ] 16.4 Implementasikan kecocokan folder (overlap path dan konten terbobot)
    - Nyatakan entry kosong atau tidak terbaca
    - _Requirements: 20.6_

  - [ ] 16.5 Laporkan tipe dokumen tanpa parser sebagai tidak didukung
    - DOCX, XLSX, PDF tanpa klaim kesetaraan semantik
    - _Requirements: 20.10_

  - [ ] 16.6 Bangun UI Similar items
    - Per tipe, review-only, tanpa aksi hapus massal, tanpa skor sebagai probabilitas atau skor universal, proses lokal tanpa cloud model atau embeddings API
    - _Requirements: 20.1, 20.2, 20.7, 20.9_

  - [ ]* 16.7 Tulis property test metrik nama simetris
    - **Property 21: Metrik nama simetris**
    - **Validates: Requirements 20.3**

- [ ] 17. Checkpoint Fase 4 - Pastikan semua tes lulus
  - Pastikan semua tes lulus, tanyakan kepada pengguna bila ada pertanyaan.

### Fase 5: Hardening

- [ ] 18. Performa dan batas sumber daya
  - [ ] 18.1 Bangun harness baseline performa di hardware target
    - Ukur time to first result, total scan time, peak RSS, I/O, responsivitas UI, cancellation latency; tetapkan gerbang performa setelah pengukuran dan jangan mengklaim angka sebelumnya
    - _Requirements: 22.1, 22.4_

  - [ ] 18.2 Uji pertumbuhan Data_Directory
    - Verifikasi retention riwayat scan, hash cache, dan log membatasi pertumbuhan
    - _Requirements: 22.2_

- [ ] 19. Privasi, offline, dan keamanan
  - [ ] 19.1 Implementasikan uji offline tanpa lalu lintas keluar
    - Scan, preview, eksekusi (pada fixture), dan laporan; tanpa akun, telemetry, upload, iklan, atau paywall
    - _Requirements: 23.1, 23.2, 23.3, 23.4_

  - [ ] 19.2 Implementasikan tinjauan otomatis ACL dan privasi Data_Directory
    - Verifikasi ACL pengguna dan tidak ada path atau secret bocor di log
    - _Requirements: 16.5, 23.6_

  - [ ] 19.3 Tambahkan smoke test invarian arsitektur
    - Scanner dan komponen non-Executor tanpa dependensi API mutasi; tidak ada registry writer; Executor tidak menerima path arbitrer
    - _Requirements: 1.3, 10.5, 17.3_

- [ ] 20. Audit aksesibilitas dan DPI
  - [ ] 20.1 Jalankan audit kontras, keyboard, dan layout lintas DPI
    - 1280×800 dan 1920×1080 pada DPI 100%, 125%, 150%, 200%; jangan klaim kepatuhan WCAG penuh tanpa validasi teknologi bantu dan pakar
    - _Requirements: 24.10, 26.1, 26.2, 26.5, 26.7_

- [ ] 21. Checkpoint akhir - Pastikan semua tes lulus
  - Pastikan semua tes lulus, tanyakan kepada pengguna bila ada pertanyaan.

## Notes

- Tugas bertanda `*` bersifat opsional dan dapat dilewati untuk MVP lebih cepat; seluruh 21 properti design.md tercakup (P1-P4 di tugas 2, P18 di 3, P20 di 4, P5 di 5, P6-P7 di 8, P13-P15 dan P17 di 9, P16 dan P19 di 10, P8-P12 di 14, P21 di 16).
- Stack, distribusi, versi Windows minimum, adapter pertama yang terbukti disposable, kontrak folder exact, dan parser fuzzy awal tetap keputusan terbuka milik pengguna.
- Semua pengujian keselamatan memakai fake filesystem, fixture, atau sandbox; tidak ada tugas yang mengubah data riil.
- Setiap properti memakai minimal 100 iterasi dan tag `Feature: mustbecleaner-storage-cleaner, Property {n}: {judul}`.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "1.2", "1.3"] },
    { "id": 1, "tasks": ["2.1", "3.1", "4.1"] },
    { "id": 2, "tasks": ["2.2", "3.2", "4.2", "5.1"] },
    { "id": 3, "tasks": ["2.3", "3.3", "4.3", "5.2", "5.3"] },
    { "id": 4, "tasks": ["2.4", "2.5", "2.6", "2.7", "3.4", "3.5", "5.4", "5.5", "5.6", "5.7"] },
    { "id": 5, "tasks": ["6.1"] },
    { "id": 6, "tasks": ["6.2", "6.3", "6.4"] },
    { "id": 7, "tasks": ["6.5", "8.1"] },
    { "id": 8, "tasks": ["8.2", "9.1", "9.4"] },
    { "id": 9, "tasks": ["8.3", "8.4", "9.2", "9.3", "9.10"] },
    { "id": 10, "tasks": ["9.5", "9.6", "10.1"] },
    { "id": 11, "tasks": ["9.7", "9.8", "9.9", "10.2", "10.3"] },
    { "id": 12, "tasks": ["11.1", "11.2", "11.3"] },
    { "id": 13, "tasks": ["12.1", "12.2"] },
    { "id": 14, "tasks": ["12.3"] },
    { "id": 15, "tasks": ["14.1", "14.3"] },
    { "id": 16, "tasks": ["14.2", "14.4"] },
    { "id": 17, "tasks": ["14.5", "14.6", "14.7", "14.8", "14.9"] },
    { "id": 18, "tasks": ["14.10", "14.11"] },
    { "id": 19, "tasks": ["16.1", "16.2", "16.3", "16.4", "16.5"] },
    { "id": 20, "tasks": ["16.6", "16.7"] },
    { "id": 21, "tasks": ["18.1", "18.2", "19.1", "19.2", "19.3"] },
    { "id": 22, "tasks": ["20.1"] }
  ]
}
```
