---
name: run-server
description: >
  How to start and cleanly stop The Canalave Library's web app for local dev/verification.
  Use whenever asked to run, start, boot, launch, stop, or kill the server, or to verify a
  change works end-to-end against the real database. MVP runs TheCanalaveLibrary.Server
  directly — no Aspire AppHost, no Redis, no WASM.
---

# Running the server

## Prerequisites

- Local PostgreSQL listening on `localhost:5432`, matching
  `TheCanalaveLibrary.Server/appsettings.Development.json`'s `ConnectionStrings:canalavedb`
  (`Database=TheCanalaveLibraryDB`, `User Id=postgres`, `Password=butterfree`).
- Migration (`InitialSchema`) already applied — the app applies/checks it automatically via
  `dotnet ef database update` semantics on EF Core startup; no separate step needed.
- `psql` is on PATH (`C:\Program Files\PostgreSQL\18\bin`, added to the User PATH) — use it for
  direct fixture setup/teardown and result verification when a check can't be driven through the
  app's own UI (see "Dev diagnostics endpoints" below). New shells pick this up automatically; an
  already-open shell needs `$env:Path += ";C:\Program Files\PostgreSQL\18\bin"` first.
  Credentials match the connection string above.

**Do not use the Aspire AppHost for MVP dev.** `AppHost.cs` still defines Postgres/Redis
resources for when Aspire orchestration returns post-MVP, but running it is not the current
workflow — run `TheCanalaveLibrary.Server` directly.

## Start

Interactive (opens a browser, matches `Properties/launchSettings.json`'s `http` profile, which
sets `ASPNETCORE_ENVIRONMENT=Development`):

```
cd TheCanalaveLibrary.Server
dotnet run
```

Headless / for agent verification (no browser launch, explicit env + port):

```
cd TheCanalaveLibrary.Server
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls http://localhost:5028 > /tmp/server_run.log 2>&1 &
```

Confirm it's actually up — don't just trust that the command returned:

```
tail -f /tmp/server_run.log   # wait for "Now listening on" + "Application started"
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5028/
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5028/Account/Login
```

`ASPNETCORE_ENVIRONMENT` must be `Development` or the app won't read
`appsettings.Development.json` and will fail to resolve the `canalavedb` connection string.

## Stop — cleanly

**Gotcha:** if you started `dotnet run` backgrounded from a shell (`... &`), the PID the shell
hands back is the `dotnet` launcher, not the actual `TheCanalaveLibrary.Server.exe` worker
process. Killing that PID leaves the real process (and the port) alive. Find and kill the
process that's actually bound to the port instead:

```powershell
$conn = Get-NetTCPConnection -LocalPort 5028 -State Listen -ErrorAction SilentlyContinue
if ($conn) { Stop-Process -Id $conn.OwningProcess -Force }
```

(Don't name the variable `$pid` — it's a PowerShell read-only automatic variable.)

Verify the port is actually free afterward:

```
curl -s -o /dev/null -w "%{http_code}\n" --max-time 3 http://localhost:5028/
```

(should fail to connect / return no code — if it still returns `200`, the kill didn't hit the
right process).

## Dev diagnostics endpoints

Some service-layer logic is hard to verify through the real UI flow — e.g. account deletion
operates on "the currently authenticated user," so testing it against a throwaway fixture user
would otherwise require logging in as that user (password, email confirmation, antiforgery, the
works). For cases like this, `TheCanalaveLibrary.Server/Endpoints/DevDiagnosticsEndpoints.cs`
(`MapDevDiagnosticsEndpoints`) is the standing home for Development-only diagnostic endpoints that
call a service method directly, bypassing the UI/auth flow. It's mapped only inside the
`app.Environment.IsDevelopment()` block in `Program.cs` — never reachable outside local dev.

**Add new ad-hoc verification endpoints there**, not inline in `Program.cs` and not scattered
across feature endpoint files — keeps every diagnostic shortcut in one auditable, never-shipped
place. Use `psql` (see Prerequisites) to set up fixture rows and assert on the resulting DB state
before/after calling one of these endpoints.

## Known tooling false-alarm

`dotnet ef migrations list` (and similar EF CLI commands run outside the app's own startup)
can report `password authentication failed for user "postgres"` against a phantom `canalavedb`
database name. This is an EF design-time-host quirk — it does not read
`appsettings.Development.json` the way the real app does — and is not evidence the app or DB
connection is broken. Trust an actual `dotnet run` + HTTP check over `dotnet ef` CLI output for
connectivity questions.
