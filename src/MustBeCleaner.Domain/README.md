# MustBeCleaner.Domain
Policy Core murni: tipe inti klasifikasi Item. Tanpa I/O, tanpa referensi ke proyek lain (R5-R7, design "Policy Core").

Status: task 2.1, 2.2 dan 2.3 (`PolicyEngine.Classify`, daftar proteksi) selesai.

## Public surface (namespace `MustBeCleaner.Domain`)
| Tipe | Peran | Requirement |
|------|-------|-------------|
| `TriState` | True / False / Unknown; Unknown adalah default dan memblokir tindakan | R5.2, R5.3 |
| `Status` | Protected, Review, Eligible, InventoryOnly | R5.1, R5.8 |
| `Condition` | 7 kondisi eligibility yang diamati | R11.1 |
| `ItemIdentity` | volume ID, file ID, canonical path, ukuran (null = tidak tersedia), mtime, atribut; `IsSameObjectAs` | R7.6, R3.5 |
| `ItemFacts` | identitas + rantai ancestor + pemilik Adapter | R7.3, R5.8 |
| `WorldFacts` | observasi dunia nyata; tidak diamati = Unknown | R5.2 |
| `RuleSet` | berversi | R5.5, R5.6 |
| `Classification` | status, rule ID, versi, alasan, kondisi; Eligible hanya jika semua kondisi True | R5.1-R5.6, R11.1 |

| `PathPolicy.Normalize` | raw string menjadi `CanonicalPath` atau `PathRejection`; case, prefix `\\?\`, `.`/`..`; tolak UNC, device path, ADS, nama 8.3 | R7.4, R7.5 |
| `CanonicalPath`, `PathNormalization`, `PathRejection` | hasil normalisasi; nama 8.3 (`~N`) menjadi `UnresolvedShortName`, tidak ditebak (ADR 0011) | R7.5 |
| `PathScope` | include + exclusion; `Decide` mengembalikan `ScopeDecision`; exclusion mengalahkan include | R1.6, R1.7 |

| `PolicyEngine.Classify` | fungsi murni; urutan: exclusion, path terlarang, proteksi, volume non-NTFS-lokal, Adapter, kondisi R11.1 | R5, R6, R7.7, R11.1 |
| `AdapterMatch` | input Adapter di level Domain (tanpa referensi ke proyek Adapters) | R5.5, R11.1 |
| `TransactionSet.KeyOf` | kunci sama untuk db + wal/shm/journal | R6.4 |

Semua tipe immutable; koleksi disalin saat konstruksi. Keputusan desain: `docs/decisions/0010-eligible-unrepresentable-without-all-true.md`, `docs/decisions/0012-protection-list-design.md`.

`Classification` punya kesetaraan nilai atas `Reasons` dan `Conditions` (untuk Property 3, task 2.4).

## Menjalankan test
```powershell
$env:PATH="$env:USERPROFILE\.dotnet10;$env:PATH"; $env:DOTNET_ROOT="$env:USERPROFILE\.dotnet10"
dotnet test tests/MustBeCleaner.Domain.Tests -m:1
```
