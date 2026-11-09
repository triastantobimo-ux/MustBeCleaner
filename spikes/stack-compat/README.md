# spikes/stack-compat

Throwaway prototype verifying the stack in `docs/decisions/0001-stack.md` works together on .NET 10. Do not copy code into `src/`. Results: `docs/research/stack-spike.md`.

| Folder | Purpose |
|---|---|
| `Core/` | `FileIdentity` (P/Invoke volume serial + file index), `JournalStore` (SQLite WAL) |
| `Wpf/` | WPF UI window: NavigationView, Mica with solid fallback, light/dark toggle, `--smoke` mode |
| `Tests/` | xUnit + FsCheck tests for Core |

## Run

Needs the .NET 10 SDK (pinned in `global.json`). If it is installed outside PATH:

```powershell
$env:PATH="$env:USERPROFILE\.dotnet10;$env:PATH"; $env:DOTNET_ROOT="$env:USERPROFILE\.dotnet10"
dotnet test Tests/Tests.csproj -m:1
dotnet build Wpf/Wpf.csproj -m:1
Wpf\bin\Debug\net10.0-windows\Wpf.exe            # interactive window
Wpf\bin\Debug\net10.0-windows\Wpf.exe --smoke    # writes %TEMP%\mbc-spike-smoke.txt, then exits
```

Only temp files are touched. `-m:1` avoids a parallel-build file lock seen between projects.
