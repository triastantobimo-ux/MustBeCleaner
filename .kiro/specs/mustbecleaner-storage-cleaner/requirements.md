# Requirements Document

## Introduction

MustBeCleaner adalah aplikasi desktop Windows yang berjalan lokal dan offline sebagai storage inspector sekaligus app-aware cleaner berbasis kebijakan (policy-driven). Produk ini sejenis CCleaner tetapi tanpa iklan dan tanpa paywall pada fitur cleanup. Pemicu awal: drive C: hampir penuh (free sekitar 2,25 GiB dari 259,38 GiB) meskipun aplikasi dipasang di D:, dengan footprint besar dari AI coding agents di profil pengguna.

Dokumen ini diturunkan dari `PRODUCT.md`, `docs/PRODUCT_BLUEPRINT.md`, `docs/LOCAL_STORAGE_REVIEW.md`, `docs/research/competitor-research.md`, dan `tools/inventory-readonly.cjs`. Keputusan blueprint dipertahankan: deny mengalahkan allow, nilai tri-state (true/false/unknown) dengan unknown memblokir tindakan, deteksi terpisah dari eligibility, seluruh checkbox awal kosong, registry off dan review-only.

Persyaratan diurutkan menurut fase prioritas:

- Fase 1 Inspector (read-only): Requirement 1 sampai 9.
- Fase 2 Controlled cleanup dan recovery: Requirement 10 sampai 17.
- Fase 3 Exact duplicates: Requirement 18 sampai 19.
- Fase 4 Similar items: Requirement 20.
- Fase 5 Hardening: Requirement 21 sampai 23.
- Lintas fase (UI, aksesibilitas, privasi): Requirement 24 sampai 26.

Di luar scope: registry writer, driver updater, antivirus, startup optimizer, software updater, disk wipe, performance booster, cloud drive cleaner, dan penggunaan LLM untuk memberi izin penghapusan.

Keputusan terbuka (tidak ditetapkan oleh dokumen ini; persyaratan bersifat technology-agnostic):

1. Stack final (rekomendasi: C# + WPF + SQLite; alternatif: WinUI 3, Tauri).
2. Distribusi: portable atau installer, signing, lokasi data dan quarantine.
3. Versi Windows minimum dan filesystem yang didukung (rekomendasi: NTFS lokal; jaringan dan cloud dilewati).
4. Adapter pertama dan rule yang terbukti disposable berdasarkan versi aplikasi terpasang.
5. Kontrak exact folder duplicate untuk metadata yang didukung, serta tipe parser fuzzy awal.
6. Dukungan registry writer (saat ini tidak disetujui).
7. Nama produk final dan identitas visual final.
8. Lisensi dan model distribusi produk.

## Glossary

- **MustBeCleaner**: Aplikasi desktop Windows yang menjadi subjek dokumen ini.
- **Scanner**: Komponen read-only yang mengenumerasi metadata filesystem tanpa kemampuan menghapus atau memindahkan.
- **Policy_Engine**: Komponen deterministik, offline, dan berversi yang mengevaluasi aturan proteksi dan eligibility.
- **Adapter**: Definisi data-only per aplikasi dan versi yang memetakan lokasi, kepemilikan, kategori, blocker, dan bukti.
- **Rule**: Entri kebijakan berversi dengan ID, sumber referensi, dan hash.
- **Executor**: Komponen yang menjalankan Manifest setelah revalidasi; satu-satunya komponen yang memutasi filesystem.
- **Manifest**: Daftar item tindakan tertentu yang dipilih pengguna, dengan ID dan hash, serta snapshot evidence tidak berubah.
- **Plan**: Rencana tindakan yang dibentuk dari pilihan pengguna sebelum disetujui menjadi Manifest.
- **Item**: Satu file atau folder hasil temuan dengan identitas (volume ID, file ID, path, ukuran, waktu modifikasi, atribut).
- **Tri_State**: Nilai kondisi true, false, atau unknown; unknown diperlakukan sebagai penolakan tindakan.
- **Eligible**: Status Item yang memenuhi seluruh kondisi eligibility pada Requirement 11.
- **Protected**: Status Item yang cocok dengan sedikitnya satu protection rule dan tidak dapat dipilih untuk tindakan.
- **Review**: Status Item yang memerlukan keputusan pengguna dan belum Eligible.
- **Quarantine**: Lokasi penyimpanan sementara yang dikelola aplikasi untuk Item yang dipindahkan dan dapat dipulihkan.
- **Journal**: Catatan durable per Item dengan tahap Prepared, Copied, Verified, dan SourceReleased.
- **Logical_Bytes**: Jumlah ukuran file berdasarkan panjang data, tanpa overlap.
- **Allocated_Bytes**: Perkiraan ruang yang dialokasikan volume untuk Item unik.
- **Reclaimed_Bytes**: Selisih ruang bebas volume yang teramati sebelum dan sesudah tindakan.
- **Keeper**: Salinan yang dipertahankan dalam satu grup duplicate.
- **Exact_Duplicate**: File dengan primary data stream identik byte per byte, atau folder dengan Folder_Manifest identik.
- **Folder_Manifest**: Daftar terurut berisi relative path, tipe entry, ukuran, dan hash konten penuh setiap entry.
- **Fuzzy_Candidate**: Pasangan atau grup Item mirip yang hanya untuk review.
- **Reparse_Point**: Objek Windows seperti junction, symlink, mount point, atau cloud placeholder.
- **Transaction_Set**: Database beserta file WAL, SHM, dan journal terkait yang diperlakukan sebagai satu unit.
- **Scope**: Himpunan lokasi yang dipilih pengguna untuk dipindai.
- **Coverage**: Catatan root yang selesai, tidak lengkap, dilewati, atau gagal dibaca dalam satu scan.
- **Exception_Log**: Catatan item yang dilewati atau ditolak beserta kode alasan.
- **Registry_Review**: Tampilan temuan registry read-only tanpa kemampuan modifikasi.
- **Data_Directory**: Lokasi penyimpanan database, Journal, laporan, dan Quarantine milik MustBeCleaner.

## Requirements

### Requirement 1: Pemilihan Scope dan Mode Read-Only

**User Story:** As a pengguna Windows dengan C: hampir penuh, I want memilih lokasi yang dipindai tanpa risiko perubahan, so that saya dapat melihat footprint storage dengan aman.

#### Acceptance Criteria

1. THE Scanner SHALL beroperasi sebagai standard user tanpa elevasi administrator.
2. WHEN pengguna memilih Scope, THE MustBeCleaner SHALL menampilkan daftar root yang akan dipindai sebelum scan dimulai.
3. THE Scanner SHALL mengakses filesystem hanya dengan operasi enumerasi metadata dan tanpa API penghapusan, pemindahan, penulisan, atau perubahan ACL.
4. IF pengguna memilih root berupa Windows critical root, THEN THE MustBeCleaner SHALL menandai root tersebut Protected dan menampilkan alasannya.
5. WHEN first run dimulai, THE MustBeCleaner SHALL menjelaskan lokasi data aplikasi dan menyatakan bahwa tidak ada pembersihan otomatis.
6. THE MustBeCleaner SHALL menyediakan daftar pengecualian (exclusion) yang berlaku pada scan, preview, dan eksekusi.
7. IF sebuah path masuk daftar exclusion dan juga include rule, THEN THE Policy_Engine SHALL menerapkan exclusion.

### Requirement 2: Scan Metadata, Progress, dan Pembatalan

**User Story:** As a pengguna, I want scan yang dapat dipantau dan dibatalkan, so that saya tetap memegang kendali pada kondisi disk hampir penuh.

#### Acceptance Criteria

1. WHEN scan berjalan, THE Scanner SHALL menampilkan progress berdasarkan item dan bytes yang sudah diketahui.
2. WHILE total pekerjaan belum diketahui, THE MustBeCleaner SHALL menampilkan indikator progress indeterminate.
3. WHEN pengguna menekan pause atau cancel, THE Scanner SHALL berhenti membaca entry berikutnya dan mempertahankan hasil parsial berlabel tidak lengkap.
4. WHERE mode low-I/O diaktifkan, THE Scanner SHALL membatasi laju enumerasi sehingga aplikasi lain tetap responsif.
5. THE Scanner SHALL membaca metadata tanpa membaca isi file pada Fase 1.
6. IF scan berjalan pada drive dengan ruang bebas kurang dari 100 MiB, THEN THE MustBeCleaner SHALL menulis hasil scan hanya ke Data_Directory yang dapat ditulis dan menolak scan jika Data_Directory tidak dapat ditulis.
7. WHEN scan mengambil item yang berubah selama enumerasi, THE Scanner SHALL mencatat item tersebut sebagai berubah-saat-scan dalam Coverage.

### Requirement 3: Pelaporan Coverage dan Kejujuran Hasil

**User Story:** As a pengguna, I want mengetahui apa yang tidak terbaca, so that angka yang ditampilkan tidak disalahartikan sebagai total.

#### Acceptance Criteria

1. THE Scanner SHALL mencatat per root: status (selesai, tidak lengkap, dibatalkan, gagal), jumlah entry terhitung, jumlah Reparse_Point dilewati, jumlah access-denied, dan lama scan.
2. IF scan berakhir karena timeout, pembatalan, atau error enumerasi, THEN THE MustBeCleaner SHALL melabel hasilnya tidak lengkap dan menampilkan ukuran sebagai batas bawah.
3. WHEN root berstatus selesai, THE MustBeCleaner SHALL menyatakan bahwa selesai hanya berarti traversal Scope non-link yang didukung selesai.
4. THE MustBeCleaner SHALL membedakan tampilan nol item ditemukan dari tampilan tidak dapat dibaca.
5. IF ukuran suatu Item tidak dapat diukur, THEN THE MustBeCleaner SHALL menampilkan unavailable dan tidak menampilkan nol.
6. THE MustBeCleaner SHALL menyimpan caveat scan (logical bytes, hard link, live non-atomic scan, reparse tag tidak terklasifikasi) bersama setiap hasil scan.
7. THE MustBeCleaner SHALL mencegah penjumlahan hasil scan spesifik dengan parent scan sebagai ukuran unik.

### Requirement 4: Storage Overview dan Perubahan Antar-Scan

**User Story:** As a pengguna, I want ringkasan drive, folder, aplikasi, dan kategori, so that saya memahami asal tekanan storage di C:.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL menampilkan total dan ruang bebas setiap drive lokal pada awal scan.
2. THE MustBeCleaner SHALL menampilkan ranking folder dan aplikasi berdasarkan Logical_Bytes dengan label bahwa ukuran besar tidak berarti sampah.
3. WHEN dua scan tersedia untuk Scope yang sama, THE MustBeCleaner SHALL menampilkan delta ukuran per folder dan aplikasi.
4. THE MustBeCleaner SHALL membedakan lokasi instalasi aplikasi dari lokasi data aplikasi (profil, cache, userData).
5. THE MustBeCleaner SHALL menampilkan large files dan pertumbuhan sebagai informasi ranking tanpa status Eligible otomatis.
6. THE MustBeCleaner SHALL menghitung Item hard link dengan identitas volume ID dan file ID yang sama satu kali dalam total unik.

### Requirement 5: Klasifikasi Berbasis Policy dengan Tri-State

**User Story:** As a pengguna, I want setiap temuan memiliki status yang dapat ditelusuri, so that saya memahami mengapa item diizinkan atau diblokir.

#### Acceptance Criteria

1. THE Policy_Engine SHALL memberi setiap Item tepat satu status: Protected, Review, atau Eligible.
2. THE Policy_Engine SHALL mengevaluasi setiap kondisi eligibility sebagai Tri_State.
3. IF sebuah kondisi eligibility bernilai unknown, THEN THE Policy_Engine SHALL menolak tindakan atas Item tersebut.
4. IF sebuah protection rule cocok dengan Item, THEN THE Policy_Engine SHALL menetapkan Protected terlepas dari adapter, skor, custom include, atau mode administrator.
5. THE Policy_Engine SHALL bersifat deterministik, offline, berversi, dan tidak menggunakan LLM untuk memutuskan izin penghapusan.
6. WHEN Item diklasifikasikan, THE MustBeCleaner SHALL mencatat rule ID, versi rule, dan alasan klasifikasi.
7. THE Policy_Engine SHALL memisahkan keputusan deteksi, penentuan tidak-terpakai, dan izin penghapusan sebagai tiga keputusan berbeda.
8. WHEN folder tidak dikenali oleh Adapter mana pun, THE Policy_Engine SHALL menetapkan status inventory-only dan mengecualikan folder tersebut dari pemilihan massal.
9. THE Policy_Engine SHALL tidak memakai nama folder `cache` atau `temp`, ekstensi file, tanggal modifikasi, atau ketiadaan proses terdeteksi sebagai satu-satunya dasar status Eligible.

### Requirement 6: Daftar Proteksi Wajib

**User Story:** As a pengguna, I want data kritikal selalu terlindungi, so that cleanup tidak merusak sistem, aplikasi, atau data saya.

#### Acceptance Criteria

1. THE Policy_Engine SHALL menetapkan Protected untuk Windows, WinSxS, System32, SysWOW64, boot/EFI, Recovery, System Volume Information, driver store, Windows Installer cache, pagefile.sys, hiberfil.sys, dan swapfile.sys.
2. THE Policy_Engine SHALL menetapkan Protected untuk Program Files, WindowsApps, root instalasi aplikasi, serta file EXE, DLL, SYS, OCX, dan binary dependency, termasuk file kritikal tanpa ekstensi yang ditentukan oleh root dan kepemilikan.
3. THE Policy_Engine SHALL menetapkan Protected untuk credentials, token, key, settings, sessions, history, memory, chat, worktrees, repository, unsaved buffer, backup, dan model lokal.
4. THE Policy_Engine SHALL memperlakukan database beserta WAL, SHM, dan journal sebagai satu Transaction_Set Protected.
5. THE Policy_Engine SHALL menetapkan Protected untuk root Local, Roaming, dan `.codex` serta tidak mengizinkan recursive delete pada root tersebut.
6. THE Policy_Engine SHALL tidak mengizinkan custom include atau mode administrator menonaktifkan proteksi kritikal pada kriteria 1 sampai 5.
7. THE MustBeCleaner SHALL tidak menjalankan pengambilalihan kepemilikan, perubahan ACL, atau penghentian service secara otomatis.
8. IF akses ke Item ditolak karena permission, THEN THE MustBeCleaner SHALL mencatat Item tersebut di Exception_Log dan tidak mencoba eskalasi.

### Requirement 7: Penanganan Link, Path Escape, dan Identitas

**User Story:** As a pengguna, I want aplikasi menolak path yang keluar dari Scope, so that scan dan tindakan tidak menyentuh data di luar jangkauan.

#### Acceptance Criteria

1. THE Scanner SHALL tidak menelusuri junction, symlink, mount point, dan cloud Reparse_Point.
2. IF suatu entry adalah Reparse_Point dengan tag yang tidak terklasifikasi, THEN THE Scanner SHALL memperlakukan entry tersebut sebagai Reparse_Point dan tidak menelusurinya.
3. THE Policy_Engine SHALL memeriksa seluruh ancestor, final path, dan volume identity sebelum meloloskan Item.
4. IF path berupa UNC, device path, atau alternate data stream, THEN THE Policy_Engine SHALL menolak Item tersebut.
5. THE Policy_Engine SHALL menormalkan variasi case, nama 8.3, dan path panjang sebelum mencocokkan rule.
6. THE MustBeCleaner SHALL menyimpan identitas Item berupa volume ID, file ID, canonical path, ukuran, waktu modifikasi, dan atribut.
7. IF volume bukan NTFS lokal, THEN THE MustBeCleaner SHALL menandai Item pada volume tersebut inventory-only.

### Requirement 8: Adapter Aplikasi dan Konservatisme Awal

**User Story:** As a pengguna dengan banyak AI coding agents, I want footprint setiap aplikasi dipetakan secara hati-hati, so that cache dibedakan dari data kerja saya.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL mendefinisikan Adapter sebagai data deklaratif berisi rule ID, versi, sumber referensi, fingerprint aplikasi dan versi, relative path yang diizinkan, kategori, blocker, mapping proses dan helper, bukti yang diperlukan, biaya regenerasi, dan kebijakan recovery.
2. THE MustBeCleaner SHALL tidak mengeksekusi PowerShell, JavaScript, atau executable arbitrer dari definisi Adapter.
3. THE MustBeCleaner SHALL menyediakan Adapter awal untuk lokasi `.codex`, Local\Codex, Local\OpenAI, Packages\OpenAI.Codex, Claude, claude-mem, AutoClaw 2, Code, Qoder, OpenCode, Local\Temp, dan go-build, dengan status awal inventory-only sampai rule tervalidasi.
4. WHEN Adapter diterapkan pada `.codex`, THE Policy_Engine SHALL menetapkan sessions, archived_sessions, memories, auth, config, database SQLite beserta WAL, `.chatgpt-projects`, worktrees, skills, plugins aktif, dan `.sandbox-bin` sebagai Protected.
5. WHEN Adapter diterapkan pada `.codex\.tmp`, `.codex\cache`, atau `.codex\plugins`, THE Policy_Engine SHALL menetapkan Review sampai lifecycle, usia, dan kepemilikan aktif terverifikasi.
6. WHEN Adapter diterapkan pada claude-mem, THE Policy_Engine SHALL memperlakukan `bin` dan seluruh binary sebagai Protected karena binary aplikasi, bukan cache.
7. WHEN Adapter diterapkan pada Code, Qoder, atau OpenCode, THE Policy_Engine SHALL memisahkan HTTP/GPU/code cache per profil dari User, Backups, History, globalStorage, workspaceStorage, extensions, database, dan draft yang belum disimpan.
8. WHEN Adapter diterapkan pada profil berbasis Electron, THE Policy_Engine SHALL memisahkan HTTP cache dari cookies, localStorage, dan IndexedDB.
9. WHEN Adapter diterapkan pada Local\Temp, THE Policy_Engine SHALL mensyaratkan owner, grace period, status aktif, dan path allow rule dan tidak mengizinkan penghapusan root Temp.
10. WHEN Adapter diterapkan pada go-build, THE Policy_Engine SHALL mensyaratkan hasil resolusi GOCACHE aktual dan status build idle sebelum Eligible.
11. WHEN Adapter diterapkan pada AutoClaw 2, THE Policy_Engine SHALL menetapkan seluruh isi inventory-only karena layout dan versi belum diverifikasi.
12. WHEN Adapter diterapkan pada worktrees terkelola, THE MustBeCleaner SHALL menautkan pengguna ke lifecycle aplikasi atau Git dan tidak menampilkan checkbox cleanup.
13. IF fingerprint aplikasi atau versi tidak cocok dengan Adapter, THEN THE Policy_Engine SHALL menetapkan Review untuk Item milik Adapter tersebut.
14. WHEN pengguna mengimpor pembaruan Adapter, THE MustBeCleaner SHALL memverifikasi provenance, menampilkan diff perubahan scope, dan tidak memperluas scope tanpa persetujuan pengguna.

### Requirement 9: Preview dan Detail Item

**User Story:** As a pengguna, I want preview yang dapat ditelusuri, so that saya dapat memutuskan dengan bukti.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL menampilkan tabel kandidat dengan kolom pilih, item, aplikasi, lokasi, Logical_Bytes, waktu modifikasi terakhir, status, dan alasan.
2. THE MustBeCleaner SHALL menyediakan filter tab Aplikasi, Lokasi, Jenis data, dan Status.
3. WHEN pengguna membuka detail Item, THE MustBeCleaner SHALL menampilkan full path, bukti kepemilikan, rule ID dan versi, alasan ditemukan, alasan diizinkan atau diblokir, dampak yang mungkin terjadi, cara memulihkan, dan bukti.
4. THE MustBeCleaner SHALL tidak menampilkan isi file bertipe sensitif (database, credentials, sessions, chat) pada preview.
5. WHEN pengguna menekan Pilih kandidat yang memenuhi policy, THE MustBeCleaner SHALL memilih hanya Item Eligible dan tidak memilih Protected, Review, atau unknown.
6. WHEN pengguna memilih aplikasi pada filter, THE MustBeCleaner SHALL hanya memfilter tampilan dan tidak memilih seluruh data aplikasi tersebut.
7. THE MustBeCleaner SHALL menyatakan batas pada setiap label status dan tidak memakai kata aman tanpa batas.
8. WHEN pengguna meminta export, THE MustBeCleaner SHALL menulis CSV, JSON, atau HTML lokal dengan opsi redaksi path.

### Requirement 10: Seleksi Awal dan Pembentukan Plan

**User Story:** As a pengguna, I want kendali penuh atas apa yang dipilih, so that tidak ada item terhapus tanpa persetujuan eksplisit.

#### Acceptance Criteria

1. WHEN hasil scan ditampilkan, THE MustBeCleaner SHALL menampilkan seluruh checkbox dalam keadaan tidak tercentang.
2. WHEN pengguna memilih Item, THE MustBeCleaner SHALL membentuk Plan yang memuat total Logical_Bytes tanpa overlap parent dan child.
3. THE MustBeCleaner SHALL menampilkan ringkasan Plan berisi total terpilih, perkiraan Allocated_Bytes yang dapat dilepas, metode recovery, tujuan Quarantine, dampak ke aplikasi, item dilewati, dan ID serta hash Manifest.
4. WHEN pengguna menyetujui Plan, THE MustBeCleaner SHALL membuat Manifest berisi Item terpilih dan snapshot evidence yang tidak dapat diubah.
5. THE Executor SHALL menerima hanya Manifest ID dan item ID yang tervalidasi dan tidak menerima path arbitrer dari UI.
6. THE MustBeCleaner SHALL menerapkan persetujuan hanya pada Item dalam Manifest dan tidak pada wildcard folder.
7. IF Plan memuat Item yang berstatus bukan Eligible, THEN THE MustBeCleaner SHALL menolak pembuatan Manifest.

### Requirement 11: Eligibility dan Preflight

**User Story:** As a pengguna, I want setiap Item direvalidasi sebelum dieksekusi, so that perubahan sejak preview tidak menyebabkan kesalahan tindakan.

#### Acceptance Criteria

1. THE Policy_Engine SHALL menetapkan Item Eligible hanya WHEN seluruh kondisi berikut bernilai true: scope lokal didukung, Adapter tepercaya cocok dengan aplikasi dan versi, kategori disposable terkonfirmasi, tidak ada protection rule cocok, pemilik quiescent dan tanpa aktivitas unknown, identitas dan metadata tidak berubah sejak preview, metode recovery siap, dan Item dipilih pengguna pada Manifest.
2. WHEN eksekusi dimulai, THE Executor SHALL menjalankan preflight yang memeriksa aplikasi pemilik idle, identitas Item tidak berubah, kapasitas tujuan cukup, Journal dapat ditulis, dan versi policy sama.
3. IF proses pemilik atau helper aktif, lock atau lease ada, atau pemeriksaan proses ditolak, THEN THE Executor SHALL melewati Item tersebut dengan alasan skipped-active.
4. IF identitas, metadata, atau ancestor Item berubah sejak preview, THEN THE Executor SHALL melewati Item tersebut dengan alasan skipped-changed dan meminta scan ulang.
5. IF versi policy berubah sejak Manifest dibuat, THEN THE Executor SHALL menolak eksekusi dan meminta Plan baru.
6. THE Executor SHALL memeriksa ulang proteksi untuk setiap Item secara independen dari UI.

### Requirement 12: Metode Tindakan dan Quarantine

**User Story:** As a pengguna dengan D: lega, I want item dipindahkan ke Quarantine di D:, so that C: lega dan tindakan dapat dipulihkan.

#### Acceptance Criteria

1. WHERE D: berupa NTFS lokal yang tervalidasi, THE MustBeCleaner SHALL menawarkan Quarantine C: ke D: sebagai metode tindakan reversible.
2. THE Executor SHALL menjalankan pemindahan lintas volume melalui tahap Journal Prepared, Copied, Verified, dan SourceReleased per Item.
3. WHEN tahap Copied selesai, THE Executor SHALL memverifikasi checksum, ukuran, serta ACL, atribut, dan timestamp yang didukung sebelum melepas sumber.
4. IF sumber berubah selama penyalinan, THEN THE Executor SHALL membatalkan pelepasan sumber dan melaporkan Item tersebut sebagai gagal.
5. IF Item memiliki metadata yang tidak didukung, THEN THE Executor SHALL menolak tindakan atas Item tersebut.
6. IF aplikasi mengalami crash saat salinan di kedua lokasi ada, THEN THE MustBeCleaner SHALL melaporkan status duplicate-pending pada startup berikutnya dan tidak menebak hasil.
7. WHERE Recycle Bin dipilih, THE Executor SHALL membaca outcome per Item dan menolak fallback ke penghapusan permanen.
8. THE MustBeCleaner SHALL menyediakan permanent purge sebagai aksi eksplisit terpisah dan tidak menjalankan purge otomatis.
9. IF Data_Directory atau tujuan Quarantine hilang atau penuh, THEN THE MustBeCleaner SHALL berhenti tanpa fallback diam-diam ke C:.
10. THE MustBeCleaner SHALL mengecualikan Data_Directory dan Quarantine dari scan.
11. WHERE command bawaan aplikasi (misalnya `go clean -cache`) tidak dapat memberi preview dan recovery setara, THE MustBeCleaner SHALL menampilkannya sebagai maintenance terpisah dengan batas yang dinyatakan.

### Requirement 13: Eksekusi, Progress, dan Pembatalan

**User Story:** As a pengguna, I want eksekusi yang terlihat dan dapat dihentikan, so that saya dapat mengamati proses per item.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL menyediakan state machine Draft, Scanning, Review, PlanReady, Preflight, Running, Completed, Partial, Failed, dan Cancelled.
2. WHEN state berubah, THE MustBeCleaner SHALL mencatat timestamp, scan ID, plan ID, dan kode alasan.
3. WHILE Executor berjalan, THE MustBeCleaner SHALL menampilkan progress dan outcome per Item.
4. WHEN pengguna menekan cancel selama eksekusi, THE Executor SHALL menghentikan Item berikutnya dan menyelesaikan atau membatalkan Item berjalan secara konsisten dengan Journal.
5. THE MustBeCleaner SHALL tidak menjanjikan rollback global atas eksekusi parsial.
6. IF sebagian Item gagal, THEN THE MustBeCleaner SHALL menetapkan status Partial dan menampilkan jumlah per outcome.

### Requirement 14: Laporan Hasil dan Akuntansi Bytes

**User Story:** As a pengguna, I want laporan yang jujur, so that saya tahu berapa ruang yang benar-benar kembali.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL melaporkan Logical_Bytes terpilih, perkiraan Allocated_Bytes yang dapat dilepas, Logical_Bytes berhasil diproses, selisih ruang bebas teramati, dan byte tujuan Quarantine sebagai angka terpisah.
2. THE MustBeCleaner SHALL melabel Reclaimed_Bytes sebagai selisih ruang bebas teramati yang dapat dipengaruhi proses lain.
3. THE MustBeCleaner SHALL tidak menghitung Item di Recycle Bin pada volume yang sama sebagai Reclaimed_Bytes.
4. THE MustBeCleaner SHALL tidak menjumlahkan parent dengan child, hard link, file yang sama dari dua Scope, atau Item yang dibatalkan.
5. THE MustBeCleaner SHALL mengelompokkan outcome sebagai quarantined, recycled, skipped-active, skipped-changed, denied-policy, failed, canceled, dan recovery-required.
6. IF penulisan laporan gagal setelah operasi berhasil, THEN THE MustBeCleaner SHALL menampilkan peringatan audit evidence tidak lengkap.
7. THE MustBeCleaner SHALL tidak menampilkan skor kesehatan, klaim peningkatan kinerja, atau penghematan yang tidak diukur.
8. THE MustBeCleaner SHALL menampilkan ruang bebas sebelum dan sesudah serta okupansi Quarantine.

### Requirement 15: Recovery dan Restore

**User Story:** As a pengguna, I want memulihkan item dari Quarantine, so that kesalahan dapat dibalik.

#### Acceptance Criteria

1. WHEN pengguna meminta restore, THE MustBeCleaner SHALL memverifikasi integritas dan metadata Item di Quarantine sebelum mengembalikan.
2. IF aplikasi pemilik aktif, THEN THE MustBeCleaner SHALL menolak restore dan menjelaskan alasannya.
3. IF target restore sudah berisi data, THEN THE MustBeCleaner SHALL menghentikan restore tanpa menimpa data tersebut.
4. IF restore gagal, THEN THE MustBeCleaner SHALL mempertahankan Item di Quarantine dan melaporkan penyebab.
5. THE MustBeCleaner SHALL menjalankan restore sebagai job terpisah dengan Journal tersendiri.
6. THE MustBeCleaner SHALL mempertahankan Journal recovery sampai pekerjaan terkait selesai.

### Requirement 16: Audit Journal dan Exception Log

**User Story:** As a pengguna, I want jejak audit lokal, so that setiap keputusan dapat diperiksa.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL menulis Journal durable sebelum setiap mutasi filesystem.
2. THE MustBeCleaner SHALL mencatat setiap Item yang dilewati atau ditolak di Exception_Log beserta kode alasan.
3. THE MustBeCleaner SHALL tidak mencatat isi file, token, atau secret pada log.
4. THE MustBeCleaner SHALL menyimpan entitas ScanRun, ScanCoverage, DiscoveredItem, AppOwnershipEvidence, RuleVersion, Classification, DuplicateGroup, KeeperDecision, CleanupPlan, PlanItem, ActionAttempt, QuarantineEntry, RestoreAttempt, dan ExceptionEntry.
5. THE MustBeCleaner SHALL menyimpan path hanya pada Data_Directory lokal dengan ACL pengguna.
6. WHERE retention eksplisit dikonfigurasi, THE MustBeCleaner SHALL menerapkannya pada laporan dan riwayat scan terbatas.

### Requirement 17: Registry Review-Only

**User Story:** As a pengguna, I want temuan registry yang ditinjau terpisah, so that saya tidak mengubah registry secara tidak sengaja.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL menempatkan Registry_Review pada area Advanced, nonaktif secara default.
2. WHERE Registry_Review diaktifkan, THE MustBeCleaner SHALL menampilkan temuan tanpa checkbox tercentang dan tanpa kemampuan modifikasi.
3. THE MustBeCleaner SHALL tidak menyertakan registry writer pada MVP.
4. THE MustBeCleaner SHALL tidak menawarkan penghapusan atau perubahan kunci kritikal, machine-wide, boot, service, driver, COM registration, security policy, atau metadata produk terpasang.
5. THE MustBeCleaner SHALL tidak mempromosikan pembersihan registry sebagai sumber penghematan storage atau peningkatan kinerja.

### Requirement 18: Exact Duplicate File

**User Story:** As a pengguna, I want menemukan file identik, so that saya dapat memangkas salinan yang tidak dibutuhkan.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL memindai duplicate hanya pada folder data yang dipilih pengguna dan tidak pada executable tree, repository, package store, runtime, AppData, atau folder sistem secara default.
2. WHEN pemindaian duplicate berjalan, THE Scanner SHALL mengelompokkan berdasarkan ukuran, menghitung SHA-256 penuh, lalu memverifikasi byte per byte sebelum menetapkan Exact_Duplicate.
3. THE MustBeCleaner SHALL tidak memakai nama, ukuran, waktu, atau hash sampel saja sebagai bukti final.
4. WHEN pengguna menyiapkan tindakan, THE MustBeCleaner SHALL mensyaratkan pemilihan Keeper dan mempertahankan sedikitnya satu salinan terverifikasi per grup.
5. WHEN eksekusi dimulai, THE Executor SHALL memverifikasi ulang identitas dan isi Item sebelum bertindak.
6. IF Keeper dihapus atau berubah secara eksternal, THEN THE Executor SHALL membatalkan tindakan grup tersebut.
7. THE MustBeCleaner SHALL memperlakukan path berhard link sebagai alias dalam satu grup dan tidak mengklaimnya sebagai alokasi bebas.
8. THE MustBeCleaner SHALL menampilkan seluruh grup duplicate dengan kotak tidak tercentang.
9. THE MustBeCleaner SHALL tidak mengganti duplicate dengan hard link secara otomatis.
10. WHERE cache hash digunakan, THE MustBeCleaner SHALL menginvalidasi entri cache saat identitas atau metadata berubah dan tidak memakai mtime dengan ukuran saja untuk keputusan destruktif.
11. IF file tidak terbaca, THEN THE MustBeCleaner SHALL mengeluarkannya dari grup dan mencatat di Exception_Log.

### Requirement 19: Exact Duplicate Folder

**User Story:** As a pengguna, I want mengenali folder identik, so that saya dapat mengelola salinan folder dengan ketat.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL menetapkan dua folder sebagai Exact_Duplicate hanya WHEN enumerasi keduanya lengkap tanpa error atau skip dan Folder_Manifest keduanya identik.
2. THE MustBeCleaner SHALL mengizinkan nama root folder berbeda pada pasangan Exact_Duplicate.
3. THE MustBeCleaner SHALL membandingkan struktur relatif, folder kosong, tipe entry, dan hash konten penuh seluruh file.
4. IF folder memuat symlink, alternate data stream, EFS, sparse atau cloud file, topologi hard link, ambiguitas case-sensitive, entry tidak terbaca, atau metadata khusus yang tidak didukung, THEN THE MustBeCleaner SHALL menandai pasangan tersebut unsupported-for-cleanup.
5. IF dua folder saling overlap sebagai parent dan child, THEN THE MustBeCleaner SHALL menolak pasangan tersebut.
6. THE MustBeCleaner SHALL menyatakan bahwa identitas konten tidak sama dengan kesamaan ACL, ownership, atribut, atau fungsi aplikasi.
7. WHEN pengguna menyiapkan tindakan, THE MustBeCleaner SHALL mensyaratkan Keeper folder dan mempertahankan sedikitnya satu salinan terverifikasi.
8. FOR ALL folder yang sama dibangun ulang dari urutan enumerasi berbeda, THE MustBeCleaner SHALL menghasilkan Folder_Manifest identik (properti invarian terhadap urutan).

### Requirement 20: Similar Items (Fuzzy, Review-Only)

**User Story:** As a pengguna, I want kandidat mirip untuk ditinjau, so that saya menemukan salinan hampir sama tanpa risiko penghapusan massal.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL menyediakan fuzzy review terpisah per tipe: nama atau path, teks, gambar, dan folder.
2. THE MustBeCleaner SHALL menandai seluruh Fuzzy_Candidate sebagai review-only dan tidak menyediakan aksi hapus massal untuk Fuzzy_Candidate.
3. WHERE kecocokan nama atau path dianalisis, THE Scanner SHALL memakai normalisasi token dan edit distance tanpa membaca konten.
4. WHERE kecocokan teks dianalisis, THE Scanner SHALL menampilkan bagian yang berbeda antara dua teks pada tipe yang didukung.
5. WHERE kecocokan gambar dianalisis, THE MustBeCleaner SHALL menampilkan perbandingan berdampingan beserta resolusi.
6. WHERE kecocokan folder dianalisis, THE MustBeCleaner SHALL menampilkan overlap relative path dan konten terbobot serta menyatakan entry kosong atau tidak terbaca.
7. THE MustBeCleaner SHALL tidak menampilkan skor kesamaan sebagai probabilitas duplicate dan tidak memakai satu skor universal lintas tipe.
8. THE MustBeCleaner SHALL tidak mempromosikan Fuzzy_Candidate menjadi Exact_Duplicate berdasarkan threshold.
9. THE MustBeCleaner SHALL memproses fuzzy secara lokal tanpa cloud model atau embeddings API.
10. WHERE tipe dokumen DOCX, XLSX, atau PDF tidak memiliki parser, THE MustBeCleaner SHALL melaporkan tipe tersebut tidak didukung dan tidak mengklaim kesetaraan semantik.

### Requirement 21: Keandalan, Recovery Crash, dan Pengujian Adversarial

**User Story:** As a pemilik produk, I want bukti keselamatan sebelum rilis, so that fitur destruktif tidak dirilis tanpa pengujian.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL melewati suite pengujian yang mencakup protected path dan alias, file sistem tanpa ekstensi, path traversal, case, 8.3, UNC, device path, junction swap setelah preview, perubahan identitas file, database aktif dengan WAL, deteksi proses yang tidak dapat diakses, long path, file terkunci, sumber berubah saat copy, D: penuh atau hilang, crash pada setiap tahap Journal, kegagalan penulisan laporan, konflik restore, compressed/sparse/ADS tidak didukung, Keeper dihapus eksternal, folder bersarang overlap, dan permission denied.
2. THE MustBeCleaner SHALL menjalankan pengujian keselamatan pada fixture, sandbox, atau VM dan tidak menghapus data riil pengguna untuk membuktikan rule.
3. WHEN crash terjadi pada setiap tahap Journal, THE MustBeCleaner SHALL pulih pada startup berikutnya ke status yang terlaporkan tanpa kehilangan data.
4. IF mekanisme belum membuktikan bahwa objek yang direview identik dengan objek yang dipindah, THEN THE MustBeCleaner SHALL menahan fitur destruktif dari rilis.
5. THE MustBeCleaner SHALL mencapai nol false positive kritikal pada korpus pengujian sebelum kelulusan Fase 2.

### Requirement 22: Performa dan Batas Sumber Daya

**User Story:** As a pengguna, I want aplikasi yang ringan, so that aplikasi cleaner tidak menambah masalah storage atau responsivitas.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL mengukur baseline di hardware target untuk time to first result, total scan time, peak RSS, I/O, responsivitas UI, dan cancellation latency sebelum gate performa ditetapkan.
2. THE MustBeCleaner SHALL membatasi pertumbuhan Data_Directory melalui retention riwayat scan, cache hash, dan log.
3. THE MustBeCleaner SHALL tidak menyimpan salinan isi penuh file untuk cache fuzzy teks.
4. THE MustBeCleaner SHALL tidak mengklaim ukuran binary, RAM, atau kecepatan scan sebelum pengukuran.

### Requirement 23: Distribusi, Privasi, dan Hardening

**User Story:** As a pengguna yang peduli privasi, I want aplikasi offline tanpa iklan atau paywall, so that data lokal tidak keluar dan cleanup tidak dibatasi.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL beroperasi tanpa akun, analytics, telemetry, upload cloud, atau iklan.
2. THE MustBeCleaner SHALL menyediakan seluruh fitur cleanup tanpa pembatasan paywall.
3. THE MustBeCleaner SHALL tidak membuat koneksi jaringan keluar; koneksi tambahan hanya dengan persetujuan eksplisit pengguna.
4. WHEN pengujian offline dijalankan, THE MustBeCleaner SHALL menunjukkan nol lalu lintas keluar selama scan, preview, eksekusi, dan laporan.
5. WHEN pembaruan Rule atau Adapter diimpor, THE MustBeCleaner SHALL menerimanya melalui impor offline dengan verifikasi provenance.
6. THE MustBeCleaner SHALL menjalankan tinjauan keamanan ACL dan privasi sebelum rilis Fase 5.
7. THE MustBeCleaner SHALL mencatat keputusan distribusi (portable atau installer, signing, lokasi data) sebagai keputusan terbuka sampai disetujui pengguna.

### Requirement 24: Inspection Workspace dan Bahasa

**User Story:** As a pengguna, I want workspace inspeksi yang tenang dan jelas, so that keputusan terlihat dan bukan sekadar tombol hapus satu klik.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL menyajikan seluruh antarmuka dan pesan dalam Bahasa Indonesia yang teknis dan jelas.
2. THE MustBeCleaner SHALL menyediakan navigasi Ringkasan, Cache aplikasi, AI & developer, File sementara, Duplikat, Similar items, Recovery, Riwayat & laporan, serta Proteksi & pengaturan.
3. THE MustBeCleaner SHALL menampilkan tabel kandidat sebagai focal point dengan inspector kanan yang dapat dibuka dan ringkasan pilihan beserta metode tindakan di bagian bawah.
4. THE MustBeCleaner SHALL menampilkan status dengan teks dan ikon dan tidak hanya dengan warna.
5. IF Item bernilai unknown, THEN THE MustBeCleaner SHALL menampilkan pesan Belum dapat dinilai dengan alasannya.
6. WHEN eksekusi selesai sebagian, THE MustBeCleaner SHALL menampilkan Proses selesai sebagian dengan jumlah per outcome.
7. WHEN Item dipindahkan ke Recycle Bin, THE MustBeCleaner SHALL menampilkan bahwa ruang volume belum tentu bertambah.
8. THE MustBeCleaner SHALL menangani state first-run, empty scope, scanning, canceled partial scan, access denied, no eligible items, no duplicates, app active, disk full, source changed, destination missing, partial success, restore conflict, dan report failure dengan pesan khusus.
9. WHERE lebar jendela sempit, THE MustBeCleaner SHALL melipat inspector menjadi panel tanpa memotong tombol aksi.
10. THE MustBeCleaner SHALL mendukung tata letak 1280×800 dan 1920×1080 pada DPI 100%, 125%, 150%, dan 200%.

### Requirement 25: Tema Pastel Light, Dark, dan System

**User Story:** As a pengguna, I want tema pastel yang nyaman, so that aplikasi profesional dan tidak ramai di mode terang maupun gelap.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL menyediakan tema light, dark, dan system dengan palet pastel sage.
2. WHERE tema system dipilih, THE MustBeCleaner SHALL mengikuti pengaturan tema Windows.
3. THE MustBeCleaner SHALL menggunakan font sistem Segoe UI atau Segoe UI Variable dan font monospace hanya untuk path dan hash.
4. THE MustBeCleaner SHALL menampilkan angka ukuran dengan tabular numerals.
5. WHEN transisi state terjadi, THE MustBeCleaner SHALL menganimasikannya selama 150 sampai 200 ms.
6. WHERE pengaturan reduced motion Windows aktif, THE MustBeCleaner SHALL menonaktifkan animasi transisi.
7. THE MustBeCleaner SHALL menggunakan warna amber hanya untuk peringatan dan coral hanya untuk error.
8. THE MustBeCleaner SHALL tidak menampilkan gradient dekoratif, glow, gamifikasi storage, atau dashboard berupa kartu KPI sebagai fokus utama.

### Requirement 26: Aksesibilitas

**User Story:** As a pengguna dengan kebutuhan aksesibilitas, I want seluruh alur dapat dipakai tanpa mouse dan terbaca oleh screen reader, so that aplikasi inklusif.

#### Acceptance Criteria

1. THE MustBeCleaner SHALL mendukung seluruh alur kerja dengan keyboard saja.
2. THE MustBeCleaner SHALL menampilkan indikator fokus yang terlihat pada setiap kontrol interaktif.
3. THE MustBeCleaner SHALL memberi label eksplisit pada setiap checkbox dengan nama Item.
4. THE MustBeCleaner SHALL mengumumkan seleksi, progress, dan error kepada screen reader.
5. THE MustBeCleaner SHALL memenuhi kontras teks minimal 4,5:1 dan kontras non-teks minimal 3:1 sebagai target WCAG AA.
6. THE MustBeCleaner SHALL memberi target klik baris tabel yang memadai untuk checkbox tanpa dekorasi tambahan.
7. IF validasi aksesibilitas penuh belum dilakukan dengan teknologi bantu dan tinjauan pakar, THEN THE MustBeCleaner SHALL tidak mengklaim kepatuhan WCAG penuh.
