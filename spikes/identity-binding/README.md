# Spike 1.2: identity binding (throwaway)

Proves, on fixtures in a fresh temp sandbox only, that the object reviewed at preview time can be bound to the object acted on later. Findings and verdict: [docs/research/identity-binding-spike.md](../../docs/research/identity-binding-spike.md).

## Layout

- `IdentityBinding/` Win32 P/Invoke helpers: `Identity` (capture, open, open by ID, final path, hard link names), `Ancestors` (reparse-aware chain capture and recheck), `Native`.
- `IdentityBinding.Tests/` xUnit tests, one behavior per test. `Sandbox` creates `%TEMP%\mbc-idspike-<guid>` and deletes only that directory on dispose (guarded by prefix check; junctions are removed before the recursive delete).

## Run

```
dotnet test
```

Requires Windows, NTFS temp volume, .NET SDK 9 (net9.0 used because only SDK 9.0.320 is installed; ADR 0001 targets .NET 10). Tests create junctions with `cmd /c mklink /J` inside the sandbox. Nothing outside the sandbox is read or written.

## Status

Throwaway code. Do not reference from product projects; port the verified approach into the Executor/Inspector with its own tests.
