# Produk: Windows Storage & Cache Cleaner

<!-- impeccable:product-schema 1 -->

## Platform

Windows desktop lokal/offline. Schema platform Impeccable belum memuat Windows native; platform ini tidak ditafsirkan sebagai web/mobile. Framework belum dipilih.

## Stack

Open decision. Pengguna meminta perbandingan stack dan rekomendasi; belum menyetujui stack implementasi.

## Users

Bimo, pengguna Windows dengan banyak aplikasi dan AI coding agents, yang perlu mengendalikan pertumbuhan storage di C: meskipun aplikasi dipasang di D:.

## Product Purpose

Menemukan penggunaan storage, memilah artefak yang memiliki bukti kelayakan cleanup, menampilkan preview yang dapat ditelusuri, menjalankan tindakan pilihan pengguna, dan menyediakan laporan serta pemulihan sesuai metode tindakan.

## Operating Context

Lokasi awal: `.codex`, AppData Local, Temp, Roaming, claude-mem, AutoClaw 2, Claude, Code, Qoder, dan go-build. Lokasi adalah scope observasi, bukan izin menghapus seluruh folder.

## Capabilities and Constraints

- Inventory storage; cache cleaner berdasarkan aplikasi/lokasi/jenis data; exact duplicate file/folder; fuzzy duplicate review.
- Guardrails wajib melindungi Windows/system files, registry crucial, DLL, aplikasi aktif, dan data yang tidak boleh hilang.
- Pengguna memilih item; preview, running status, dan result report wajib tersedia.
- Registry harus unticked secara default dan membutuhkan review terpisah. Tingkat dukungan modifikasi registry belum disepakati.
- Tidak ada iklan; fitur cleanup tidak dibatasi paywall. Distribusi/lisensi produk belum diputuskan.
- Light/dark dengan pastel design.
- Tahap yang dikonfirmasi: blueprint Windows dahulu, tanpa prototype atau implementasi cleanup.

## Brand Commitments

Bahasa Indonesia, teknis jelas, intuitif, profesional, tidak ramai. Nama final dan identitas visual belum dipilih. Antislop during dipilih untuk sesi 9 November 2026; bukan global preference.

## Evidence on Hand

- Screenshot pengguna: C: hampir penuh, `.codex` sekitar 10,2 GB menurut tampilan Explorer.
- `evidence/local-inventory.json`: pemeriksaan metadata lokal, dengan coverage/error/caveat tersimpan.
- `docs/research/competitor-research.md`: riset primary source; bukan pengujian langsung aplikasi pembanding.

## Product Principles

1. Ukuran besar tidak berarti junk; ketidaktahuan menghasilkan status dilindungi/review.
2. Keamanan keputusan dapat diperiksa dari rule, evidence, dan exception log.
3. Persetujuan berlaku untuk manifest tindakan tertentu, bukan wildcard folder.
4. Keberhasilan dihitung dari outcome terverifikasi, termasuk partial failure dan keterbatasan pemulihan.
5. Data dan operasi cleanup tetap lokal; koneksi tambahan harus eksplisit.
