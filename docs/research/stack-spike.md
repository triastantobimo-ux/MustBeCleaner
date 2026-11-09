# Stack spike (task 1.1)

Date of runs: 9 November 2026 (machine clock). Code: `spikes/stack-compat/`. Machine: Windows 10.0.26300.

## .NET LTS status (official)

Source: [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core) and [releases-index.json](https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json).

- .NET 10: LTS, active, released 11 Nov 2025, end of support 14 Nov 2028. Latest patch 10.0.14, SDK 10.0.403.
- .NET 9 (STS) and .NET 8 (LTS) both end support 10 Nov 2026, one day from the run date. The pre-installed SDK 9.0.320 is therefore not suitable for the product.
- .NET 11 is RC3 (go-live), not LTS.

Decision input: target **.NET 10**.

## SDK install

- Before: only 9.0.320 in `C:\Users\mika\.dotnet`.
- First attempt into `~\.dotnet` failed: `dotnet.exe` was locked by a running process (no files changed; `dotnet --list-sdks` still showed 9.0.320 only).
- Done: official `dotnet-install.ps1 -Version 10.0.403 -InstallDir ~\.dotnet10 -NoPath`. Result: SDK 10.0.403 plus runtimes NETCore/AspNetCore/WindowsDesktop 10.0.14 in `C:\Users\mika\.dotnet10`. PATH and machine settings unchanged; sessions set `PATH`/`DOTNET_ROOT` themselves.
- Download hash/signature check: UNVERIFIED (script warned its size check could not be measured; install completed and `dotnet --list-sdks` reports 10.0.403).

## Verified versions (NuGet latest stable on run date)

| Package | Version | Used by |
|---|---|---|
| WPF-UI | 4.3.0 | Wpf |
| CommunityToolkit.Mvvm | 8.4.2 | Wpf |
| Microsoft.Extensions.Hosting | 10.0.14 | Wpf |
| Microsoft.Data.Sqlite | 10.0.14 | Core |
| xunit | 2.9.3 | Tests |
| xunit.runner.visualstudio | 4.0.1 | Tests |
| Microsoft.NET.Test.Sdk | 18.11.0 | Tests |
| FsCheck.Xunit | 3.6.0 (brings FsCheck 3.x) | Tests |

Confirmed with `dotnet list package` (Requested = Resolved for all).

## Results

| Check | Result | Evidence |
|---|---|---|
| Build Core + Wpf + Tests on net10.0-windows | PASS | `Build succeeded. 0 Warning(s) 0 Error(s)` |
| xUnit + FsCheck tests (7: identity x3, WAL x2, 2 properties at 100 runs) | PASS | `Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7` |
| Volume ID + file ID via P/Invoke on temp file: nonzero, stable, distinct per file, survives rename | PASS | tests in `FileIdentityTests.cs` |
| SQLite WAL open/write/read; `-wal` sidecar present | PASS | `JournalStoreTests.cs`; smoke `wal=wal stage=Prepared` |
| WPF UI window starts, NavigationView with 2 items, page navigation | PASS (code path) | smoke `nav_items=True`, window title observed |
| Light/dark switch at runtime | PASS (API) | smoke `theme_switch=Dark->Light` (two toggles) |
| Mica / solid fallback selection | PASS (logic) | smoke `mica_active=True os_build=26300`; fallback branch (build < 22000) not executed: UNVERIFIED |
| Smoke inside WPF process (identity + WAL) | PASS | `RESULT=PASS`, exit code 0 |

Smoke output:

```
theme_switch=Dark->Light
mica_active=True os_build=26300
nav_items=True
file_identity=FileIdentity { VolumeSerial = 1885612393, FileIndex = 13792273860736695 }
wal=wal stage=Prepared
RESULT=PASS
```

## Not verified

- Visual appearance (Mica rendering, contrast, pastel sage palette R25): no screenshot taken. UNVERIFIED.
- Solid fallback on Windows 10 or older Windows 11: not run.
- Large-table virtualization (hundreds of thousands of rows): not tested here.
- 128-bit `FileIdInfo` identity: spike 1.2, not this spike. This spike used the 64-bit file index only.
- Package licences and vulnerability audit: not checked.

## Issues seen

- Namespace `Spike.Wpf` shadows `Wpf.Ui`; XAML build failed with `CS0234 ... 'Ui' does not exist in the namespace 'Spike.Wpf'`. Rule for scaffold: do not name a root namespace `Wpf`.
- WPF UI 4.x `NavigationView` has no content slot (`MC3028`); content comes from pages via `TargetPageType`.
- `dotnet test` without `-m:1` hit a file lock between projects once; rerun passed. A stale `testhost` from a piped run also locked a DLL (warning, build continued).
- Compatibility verdict: WPF UI 4.3.0 works with .NET 10 for the features above.
