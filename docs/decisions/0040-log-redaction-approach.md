# 0040. Log redaction: typed fields first, pattern scrub second

Status: accepted (task 4.2)

## Context
R16.3 forbids file content, tokens and secrets in logs. R16.2 and R6.8 require an Exception_Log entry with a reason code for every skipped or denied Item.

## Decision
- `SafeLog` is the only logging entry point. It takes `LogField` values built by `Id`, `Reason`, `Path` and `Count`. There is no free-text or byte/stream overload, so content has no way in.
- `Id` values must be plain identifiers (`[A-Za-z0-9_.:-]{1,128}`), else `[REDACTED]`. `Reason` values must look like a code, else `INVALID_REASON`.
- `Path` is masked (`[PATH]`) unless `LogPolicy.AllowPaths` is true. When allowed: query string removed, secret patterns scrubbed, length capped.
- Field keys that look like content or secrets (content, payload, body, data, text, token, secret, password, credential, key, auth, sig, cookie) always log `[REDACTED]`, whatever the value.
- `Redactor.Scrub` is the second line: bearer/basic credentials, `key=value` secrets, JWTs, known token prefixes, URL user info, private key blocks, long mixed letter+digit tokens. Pure hex (checksums) is kept.
- Exception_Log reasons are an enum (`ExceptionReason`) mapped to fixed codes, so only known codes are stored.
- A failing sink never throws into the caller.

## Limits
Pattern scrubbing is heuristic; an unrecognised secret in a path could still pass when `AllowPaths` is true. That is why paths are off by default and why identifiers and reasons are not scrubbed but restricted.

## Alternatives rejected
A generic `Log(string message)` with regex scrubbing only: one missed pattern leaks content.
