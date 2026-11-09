# ADR 0012: Daftar proteksi tetap di kode, urutan evaluasi tetap

Status: diputuskan oleh implementasi task 2.3 (belum ada keputusan pengguna terpisah; dapat dibalik sebelum task 2.4).

## Keputusan
- `PolicyEngine.Classify` mengevaluasi berurutan, yang pertama cocok menang: normalisasi path (gagal normalisasi berarti path terlarang, `F-FORBIDDEN-PATH`), exclusion pengguna (`X-USER-EXCLUSION`), protection rule (`P-*`), volume non-NTFS-lokal (`I-UNSUPPORTED-VOLUME`, InventoryOnly), Adapter match, kondisi eligibility R11.1. Normalisasi mendahului exclusion karena pencocokan exclusion membutuhkan bentuk kanonik.
- Daftar proteksi (`ProtectionRules`) tetap di kode dan tidak punya input override. Adapter, skor, custom include, dan administrator mode tidak punya jalur ke sana (R5.4, R6.6).
- Rule: `P-WINDOWS-CRITICAL`, `P-PROGRAM-FILES` (Program Files, WindowsApps), `P-NEVER-RECURSIVE-ROOT` (`Users\*\AppData\Local`, `...\Roaming`, `Users\*\.codex`), `P-BINARY` (EXE/DLL/SYS/OCX), `P-TRANSACTION-SET` (db/sqlite/sqlite3 + `-wal`/`-shm`/`-journal`, satu kunci lewat `TransactionSet.KeyOf`), `P-USER-DATA` (segmen credentials, sessions, history, memory/memories, worktrees, .git, repos/repositories, backups/backup, models), `P-EXTENSIONLESS-UNOWNED`.
- File tanpa ekstensi dilindungi bila tidak punya pemilik Adapter. Di bawah root kritis ia sudah dilindungi oleh rule root.
- Exclusion pengguna dan path terlarang diberi status `Protected` (tidak bisa dipilih), bukan InventoryOnly.
- `Classification` punya kesetaraan nilai atas `Reasons` dan `Conditions`.

## Alternatif yang ditolak
- Daftar proteksi dari Adapter/JSON: Adapter yang salah atau diperbarui bisa melonggarkan proteksi. ADR 0031 menandai proteksi sebagai konvensi Adapter; Policy Core tetap menjadi pemutus akhir.
- Mencocokkan nama folder untuk Eligible: tidak pernah. Nama hanya dipakai untuk menolak (Protected), tidak untuk mengizinkan (R5.9).

## Konsekuensi
- Segmen `models`, `history`, dan seterusnya melindungi lebih luas dari yang perlu (cache Adapter di bawahnya jadi Protected). Itu sengaja: salah ke arah aman.
- Menambah atau melonggarkan rule butuh ADR baru.
- Ekstensi/pola yang belum tercakup (mis. `.ldb`, `.wal` tanpa nama DB) belum dilindungi sebagai Transaction_Set: UNVERIFIED terhadap data nyata.
