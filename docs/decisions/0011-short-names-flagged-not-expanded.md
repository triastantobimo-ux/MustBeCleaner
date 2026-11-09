# ADR 0011: Nama 8.3 ditandai UnresolvedShortName, tidak ditebak
Status: diputuskan oleh implementasi task 2.2 (belum ada keputusan pengguna terpisah; dapat dibalik sebelum task 2.3).

## Konteks
R7.5 meminta variasi case, nama 8.3, dan path panjang dinormalkan sebelum rule dicocokkan. Mengubah `PROGRA~1` menjadi `PROGRAM FILES` membutuhkan tabel nama pendek volume, yaitu I/O. Policy Core murni (design, invarian 5), jadi tidak bisa mengembangkannya. Pemetaan `~1` ke nama panjang juga tidak stabil: bergantung urutan pembuatan dan dapat dimatikan per volume.

## Keputusan
- `PathPolicy.Normalize` menolak segmen berbentuk 8.3 (`^[^.~]{1,6}~[0-9]+(\.[^.]{1,3})?$`) dengan `PathRejection.UnresolvedShortName`. Tidak ada tebakan.
- Pemanggil yang memegang I/O (Inspector) bertugas memperluas nama pendek lewat API sistem (mis. `GetLongPathName`) sebelum memanggil `Normalize`. Hasil yang tetap berbentuk 8.3 ditolak, jadi Item itu tidak pernah Eligible (R5.3: tak diketahui menolak tindakan).
- Case: bentuk kanonik huruf besar (`ToUpperInvariant`), mengikuti pembandingan NTFS. Prefix `\\?\` dibuang. `\\?\UNC\` ditolak sebagai UNC; `\\?\Volume{}`, `\\?\GLOBALROOT`, `\\.\`, `\??\` sebagai DevicePath.
- Segmen `.` dan `..` diselesaikan secara leksikal; `..` di atas root ditolak (`EscapesRoot`). Titik dan spasi di akhir segmen dibuang seperti Windows.

## Alternatif yang ditolak
- Mengembangkan nama 8.3 di Policy Core: butuh I/O, melanggar kemurnian.
- Membiarkan `PROGRA~1` lolos apa adanya: rule proteksi untuk `Program Files` tidak akan cocok, sehingga jalur terlarang bisa terlihat aman.

## Konsekuensi
- Nama file asli yang kebetulan berbentuk 8.3 (mis. `backup~1.txt`) ditolak. Ini false positive yang disengaja: gagal tertutup.
- Idempoten: path kanonik yang dinormalkan ulang tidak berubah.
