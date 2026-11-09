# Architecture Selection: mustbecleaner-storage-cleaner

Catatan metode: metrik di bawah adalah estimasi kualitatif-kuantitatif dari alokasi Requirement 1-26 ke komponen, bukan hasil pengukuran kode. Stack tetap open decision; arsitektur ini technology-agnostic.

## Recommended Architecture: Split-Privilege Pipeline (Inspector read-only | Executor mutasi) dengan Policy Core bersama dan Store sebagai batas persistence

### Rationale
Kebutuhan terkuat adalah invarian keselamatan: Scanner tidak boleh punya API mutasi (R1.3), UI hanya mengirim Manifest ID (R10.5), dan Executor memeriksa ulang proteksi sendiri (R11.6). Memisahkan sisi baca dan sisi mutasi pada batas komponen membuat invarian itu struktural, bukan konvensi. Policy Core murni (tanpa I/O) dipakai kedua sisi sehingga evaluasi ulang deterministik dan mudah diuji adversarial (R21). Trade-off: lebih banyak komponen dan satu batas persistence yang harus dijaga konsisten (Plan/Manifest/Journal) dibanding layered biasa.
Jika kecepatan implementasi MVP lebih penting daripada jaminan struktural, Candidate A (Layered) lebih sederhana, tetapi proteksi bergantung disiplin kode.

### Components
| Component | Owned State | Responsibility |
|-----------|-------------|----------------|
| Policy Core | Rule set berversi, daftar proteksi, definisi Tri_State | Klasifikasi Protected/Review/Eligible, eligibility, normalisasi path, rule ID dan alasan. Murni, tanpa I/O |
| Adapter Catalog | Adapter data-only, provenance, diff impor | Pemetaan aplikasi/versi ke lokasi, kategori, blocker, proses pemilik |
| Inspector | ScanRun, ScanCoverage, DiscoveredItem | Enumerasi metadata read-only, coverage, overview, delta antar-scan, identitas Item |
| Duplicate Analyzer | DuplicateGroup, hash cache | Exact file/folder (size, SHA-256, byte verify, Folder_Manifest) dan fuzzy review-only. Read-only |
| Plan Service | CleanupPlan, PlanItem, KeeperDecision, Manifest | Seleksi awal kosong, total non-overlap, pembuatan Manifest immutable |
| Executor | ActionAttempt, QuarantineEntry, RestoreAttempt | Preflight, revalidasi, quarantine C: ke D:, Recycle, restore, state machine eksekusi. Satu-satunya pemutasi filesystem |
| Journal Store | Journal, ExceptionEntry, RuleVersion, semua entitas persisten | Batas persistence durable (SQLite lokal di Data_Directory), retention, recovery crash startup |
| Reporting | Laporan, export CSV/JSON/HTML | Akuntansi bytes terpisah, outcome per Item, redaksi path |
| UI Shell | View state, tema, preferensi | Workspace inspeksi, filter tab, inspector, tema, aksesibilitas. Tanpa akses path arbitrer ke Executor |

### Information Flow
| From \ To | Policy | Adapters | Inspector | Dup | Plan | Executor | Store | Report | UI |
|-----------|--------|----------|-----------|-----|------|----------|-------|--------|----|
| Policy | - | | | | | | | | |
| Adapters | | - | | | | | | | |
| Inspector | → | → | - | | | | → | | |
| Dup | → | | → | - | | | → | | |
| Plan | → | | | | - | | → | | |
| Executor | → | → | | | | - | → | | |
| Store | | | | | | | - | | |
| Report | | | | | | | → | - | |
| UI | | | → | → | → | → (hanya Manifest ID) | | → | - |

Tidak ada siklus sinkron: UI memanggil ke bawah; komponen lain hanya memanggil Policy, Adapters, dan Store. Notifikasi progress ke UI melalui event/observasi (←), bukan panggilan balik sinkron.

### Requirement Allocation
| Requirement | Component(s) |
|-------------|--------------|
| R1 Scope, read-only | Inspector, Policy Core |
| R2-R3 Scan, coverage | Inspector, Store |
| R4 Overview, delta | Inspector |
| R5-R7 Klasifikasi, proteksi, link/identitas | Policy Core |
| R8 Adapter | Adapter Catalog |
| R9 Preview | UI Shell |
| R10 Seleksi, Plan | Plan Service |
| R11 Eligibility, preflight | Executor |
| R12 Quarantine | Executor |
| R13 Eksekusi, state machine | Executor |
| R14 Laporan | Reporting |
| R15 Restore | Executor |
| R16 Audit, entitas | Journal Store |
| R17 Registry review | Inspector (read-only) |
| R18-R19 Exact duplicates | Duplicate Analyzer |
| R20 Fuzzy | Duplicate Analyzer |
| R21 Pengujian | Lintas (test harness atas Policy Core dan Executor) |
| R22 Performa | Inspector, Journal Store |
| R23 Privasi, offline | Lintas (tanpa komponen jaringan) |
| R24-R26 UI, tema, aksesibilitas | UI Shell |

### Key Design-Induced Invariants
1. Hanya Executor yang memegang kemampuan mutasi filesystem; komponen lain tidak memiliki dependensi ke API tersebut.
2. Executor menghitung ulang klasifikasi lewat Policy Core, tidak mempercayai status dari Inspector atau UI.
3. Manifest immutable: setelah dibuat, Plan Service tidak dapat mengubahnya; Executor hanya membaca dari Store.
4. Journal ditulis durable sebelum setiap mutasi; Store menolak mutasi tanpa entri Journal.
5. Policy Core tidak melakukan I/O; fakta dunia nyata (proses aktif, identitas) disuplai sebagai input sehingga unknown dapat dinyatakan eksplisit.
6. Data_Directory dan Quarantine dikecualikan oleh Inspector; Store gagal tertutup jika lokasi hilang, tanpa fallback ke C:.

### Alternatives Considered
| Candidate | Strength | Weakness | Why Not Selected |
|-----------|----------|----------|-----------------|
| A. Layered (UI / Application / Domain / Infrastructure) | Familiar, cepat dibangun, sedikit komponen | Proteksi dan mutasi hidup berdampingan di Infrastructure; batas read vs write tidak dipaksa struktur; Application menjadi god-object | Invarian R1.3 dan R10.5 hanya ditegakkan konvensi |
| B. Split-Privilege Pipeline (dipilih) | Batas keselamatan struktural, Policy murni mudah diuji, tanpa siklus | Lebih banyak komponen, kontrak Store harus ketat | - |
| C. Event-sourced job pipeline (Journal sebagai log pusat) | Audit dan crash recovery sangat kuat, replay mudah | Kompleksitas tinggi untuk scan besar, volume event besar di disk terbatas, bertentangan R22.2 | Terlalu berat untuk MVP, risiko pertumbuhan Data_Directory |

### Metrics Summary (estimasi)
| Metric | Selected (B) | Alt A (Layered) | Alt C (Event-sourced) |
|--------|-------------|-----------------|-----------------------|
| Cross-cutting reqs % | ~15% (R1, R21-R23) | ~35% | ~30% |
| Cross-cutting invariants % | ~20% | ~45% | ~25% |
| Flow density (edges / N(N-1)) | ~0,15 | ~0,25 | ~0,30 |
| God object score (maks % state satu komponen) | ~25% (Store sebagai penyimpan, bukan logika) | ~45% (Application) | ~50% (Event log) |
| Sync cycles | 0 | 1 (Application <-> Infrastructure callback) | 1-2 |
| Max fan-in | 3 (Policy, Store) | 4 | 5 |
| Max fan-out | 3 (Executor) | 4 | 4 |
| Evolvability cost (komponen berubah per REQ baru) | ~1,5 | ~2,5 | ~2,0 |
