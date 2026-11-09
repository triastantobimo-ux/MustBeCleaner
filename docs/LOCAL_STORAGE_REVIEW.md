# Sebagian besar footprint Codex yang terukur bukan folder cache

Status: **Confirmed untuk metadata yang berhasil dibaca**, bukan klasifikasi safe-to-delete. Pemeriksaan 9 November 2026, Asia/Jakarta. Raw evidence: [inventory utama](../evidence/local-inventory.json) dan [inventory spesifik](../evidence/targeted-inventory.json). Metode dapat direproduksi dengan [script inventory](../tools/inventory-readonly.cjs).

## Kapasitas dan cakupan

Pada snapshot awal inventory, C: free **2.414.616.576 bytes = 2,25 GiB**, total **259,38 GiB**. D: free **398,24 GiB**. Angka drive dapat berubah selama sesi; screenshot pengguna sebelumnya menampilkan 2,41 GB dengan konvensi tampilan Windows. Semua ukuran tabel berikut memakai **GiB = bytes / 1.073.741.824**, bukan decimal GB.

| Root | Logical GiB teramati | File terhitung | Coverage |
|---|---:|---:|---|
| `C:\Users\mika\.codex` | 10,29 | 60.785 | Traversal selesai; 205 link dilewati; tanpa enumeration error yang tercatat |
| `C:\Users\mika\AppData\Local` | setidaknya 38,73 | 404.094 | **Tidak lengkap**, batas 150 detik; 111 link dilewati |
| `C:\Users\mika\AppData\Roaming` | 9,98 | 73.363 | Traversal selesai; 2 link dilewati |

`Selesai` hanya berarti traversal scope non-link yang didukung selesai, bukan scan semua data drive C:. Ukuran AppData Local tidak boleh diklaim total. Tidak dilakukan pemindaian seluruh profil `mika`, Windows, registry, atau drive lain. Errors kosong bukan jaminan tidak ada data lain yang tidak terjangkau akibat timeout/link exclusion.

## Komponen terbesar `.codex`

| Komponen | Logical GiB | Interpretasi dan disposition awal |
|---|---:|---|
| `worktrees` | 5,05 | Work product/dependencies/build artifacts campuran; **protected**, bukan generic cache |
| `sessions` | 1,69 | Session history; **protected** |
| `.chatgpt-projects` | 1,52 | Project data; **protected** |
| `thread_history_1.sqlite` | 0,71 | Database; **protected** bersama transaction files |
| `plugins` | 0,65 | Campuran cache/runtime/plugin; **review ownership**, jangan hapus root |
| `.sandbox-bin` | 0,30 | Binary runtime; **protected** |
| `.tmp` | 0,15 | Nama temporary tidak cukup; **review** lifecycle, age dan active ownership |
| `logs_2.sqlite` | 0,07 | Database log; **protected**, retention melalui lifecycle aplikasi |
| `cache` | 0,07 | Kandidat investigasi cache, **belum terbukti disposable** |

Kesimpulan **Supported**: menyebut seluruh 10,29 GiB sebagai “cache Codex” berisiko menyesatkan. Folder bernama `cache` sekitar 0,7% dari total teramati; tidak menutup kemungkinan ada cache di lokasi lain, worktree atau AppData. Tidak ada analisis isi DB, percakapan, credentials, atau source file yang dilakukan.

## Lokasi yang diminta pengguna

| Lokasi | Logical GiB | Batas interpretasi |
|---|---:|---|
| `Local\Temp` | 0,5751 | Traversal spesifik selesai; belum dibedakan expired/active/unknown |
| `Local\go-build` | 0,3646 | Traversal spesifik selesai; lokasi belum dicocokkan dengan actual `go env GOCACHE` |
| `Local\claude-mem` | 0,1074 | Traversal spesifik selesai; hampir seluruh ukuran berasal dari `bin\claude-mem.exe` (115.282.944 bytes), **binary aplikasi, bukan cache** |
| `Local\AutoClaw 2` | <0,0001 | Lokasi ini kecil; tidak membuktikan seluruh footprint AutoClaw kecil |
| `Local\Codex` | 0,1815 | Traversal spesifik selesai; hanya lokasi tersebut |
| `Local\OpenAI` | 0,6835 | Traversal spesifik selesai; app/profile data perlu dipisahkan |
| `Roaming\Code` | 3,03 | Total subtree dari scan Roaming; campuran cache, agent/runtime dan user data |
| `Roaming\com.qoder.app.stable` | 0,29 | Termasuk cache dan database; root tidak boleh dihapus |
| `Roaming\Claude` | 0,05 | Termasuk cache dan local agent sessions; root tidak boleh dihapus |

Inventory Local juga menemukan footprint `Packages\OpenAI.Codex_2p2nqsd0c76g0` sekitar **1,67 GiB** dalam scan parsial. Ini menunjukkan footprint Codex tersebar di beberapa lokasi; completeness subtree ini belum dicatat terpisah sehingga angka dianggap teramati, bukan total terjamin. Jangan menjumlahkan scan spesifik dengan parent Local sebagai ukuran unik.

Di Roaming Code, beberapa kelompok teramati: CachedExtensionVSIXs sekitar **1,55 GiB**, agent-host **0,62 GiB**, CachedData **0,42 GiB**, Cache **0,15 GiB**, User **0,15 GiB**. Nama kategori membantu menentukan prioritas review; tidak memberikan authorization cleanup.

Di luar AI agents, scan Local parsial menemukan Microsoft\Office sekitar **17,30 GiB**, Microsoft\OneDrive **2,72 GiB**, dan npm-cache **3,67 GiB**. Ini menjadi bukti bahwa fokus `.codex` saja tidak mencakup seluruh pressure C:. Office/OneDrive tidak diinspeksi kontennya dan tidak disarankan untuk dihapus manual.

## Apa yang sudah dan belum diverifikasi

- **Confirmed:** script hanya metadata enumeration, output lokal di workspace D:, tidak membaca konten, tidak melakukan hashing, tidak mengubah registry, ACL atau menghapus/memindahkan file. Proses bernama `codex` terdeteksi saat pemeriksaan; ini mendukung active-owner block.
- **Supported:** storage C: sangat rendah; footprint aplikasi tersebar di profil pengguna. Install location dan app-data location merupakan konsep berbeda.
- **Preliminary:** klasifikasi folder berdasarkan nama/jenis konvensional dan referensi produk; adapter versi terpasang belum dibuktikan.
- **Not Verifiable:** total safe-to-delete bytes, runtime orphan, last-used time, seluruh penyebab slowdown, exact/fuzzy duplicates, dan total AppData Local.

Logical sizes dapat double count hard links; allocation, compression, sparse data dan ADS tidak diukur. File/folder dapat berubah selama live scan. Symbolic links/junctions dilewati; metadata API Node tidak memeriksa seluruh reparse tag khusus Windows. Angka ini tidak menjadi manifest cleanup.

## Urutan penanganan yang disarankan

1. **Saat ini:** pertahankan cache/runtime/data yang belum terklasifikasi. Inventory ini tidak mengesahkan penghapusan apa pun.
2. **Tahap inspector:** prioritaskan footprint terbesar dan ownership mapping: `.codex/worktrees`, Code CachedExtensionVSIXs/agent-host, npm-cache, dan Codex app profiles. Worktrees tetap protected sampai lifecycle/Git/app review.
3. **Tahap controlled cleanup:** mulai dari adapter dengan disposable semantics yang dapat diuji, bukan folder terbesar. Untuk Go, verifikasi actual GOCACHE dan build activity; untuk Electron-family caches, pisahkan cookies/localStorage/IndexedDB dari HTTP cache.
4. **Pencegahan:** investigasi official configuration untuk lokasi cache/worktree ke D:, dengan test backup/restore dan version compatibility. Jangan memindahkan seluruh AppData atau `.codex` melalui junction secara otomatis.

Rujukan: [Electron storage paths](https://www.electronjs.org/docs/latest/api/app), [Go build cache](https://go.dev/doc/manage-install), [Codex worktree lifecycle](https://learn.chatgpt.com/docs/environments/git-worktrees). Storage Sense adalah referensi resmi untuk Windows-owned cleanup; integrasi produk dapat membuka jalur Windows tersebut tanpa menyalin penghapusan system internals. [Microsoft Storage Sense](https://support.microsoft.com/en-us/windows/experience/storage-filemanagement/manage-drive-space-with-storage-sense).
