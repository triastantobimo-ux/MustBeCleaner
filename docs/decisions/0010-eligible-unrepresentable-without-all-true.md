# ADR 0010: Eligible tidak dapat dibangun tanpa semua kondisi True
Status: diputuskan oleh implementasi task 2.1 (belum ada keputusan pengguna terpisah; dapat dibalik sebelum task 2.3).

## Keputusan
- `TriState.Unknown` adalah nilai default (0). Kondisi yang tidak diamati (`WorldFacts`, `Classification.Conditions`) dibaca Unknown.
- Konstruktor `Classification` menolak `Status.Eligible` bila ada `Condition` yang bukan `True`. Aturan R11.1 dan Property 2 ditegakkan oleh tipe, bukan hanya oleh evaluator.
- `Condition` hanya memuat 7 kondisi yang diamati. "Tidak ada protection rule cocok" bukan `Condition`: ditentukan oleh `RuleSet`.

## Alternatif yang ditolak
- Validasi hanya di evaluator `classify` (task 2.3): Executor atau test dapat membuat `Classification` Eligible yang tidak sah tanpa terdeteksi.

## Konsekuensi
- Pembuat `Classification` Protected/Review/InventoryOnly tetap bebas mengisi `Conditions` (sebagian atau kosong).
- Menambah `Condition` baru langsung memperketat Eligible; perubahan itu harus lewat ADR baru karena mengubah R11.1.
