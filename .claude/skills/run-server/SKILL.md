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
- No database setup needed: on every Development startup, `DataSeeder.SeedDevelopmentDataAsync`
  runs `Database.MigrateAsync()` — which **creates the database when it doesn't exist** and applies
  pending migrations — then seeds representative data into an empty DB (see "Dev DB lifecycle").
- `psql` is on PATH (`C:\Program Files\PostgreSQL\18\bin`, added to the User PATH) — use it for
  direct fixture setup/teardown and result verification when a check can't be driven through the
  app's own UI (see "Dev diagnostics endpoints" below). New shells pick this up automatically; an
  already-open shell needs `$env:Path += ";C:\Program Files\PostgreSQL\18\bin"` first.
  Credentials match the connection string above.

**Do not use the Aspire AppHost for MVP dev.** `AppHost.cs` still defines Postgres/Redis
resources for when Aspire orchestration returns post-MVP, but running it is not the current
workflow — run `TheCanalaveLibrary.Server` directly.

## Start

Preferred — the repo scripts (agents via the PowerShell tool; the user manually):

```powershell
.\scripts\start-dev-server.ps1               # foreground, Ctrl+C to stop (manual use)
.\scripts\start-dev-server.ps1 -Background   # detached; logs to %TEMP%\canalave-dev-server.log,
                                             # waits for "Now listening on", fails loudly if not
```

The script refuses to double-start (checks the port first) and, in `-Background` mode, tails the
log on startup failure. What it does under the hood (also usable raw from bash):

```
cd TheCanalaveLibrary.Server
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-launch-profile --urls http://localhost:5028 > /tmp/server_run.log 2>&1 &
```

Confirm it's actually up — don't just trust that the command returned (the script's `-Background`
mode does this for you):

```
tail -f /tmp/server_run.log   # wait for "Now listening on" + "Application started"
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5028/
```

`ASPNETCORE_ENVIRONMENT` must be `Development` or the app won't read
`appsettings.Development.json` and will fail to resolve the `canalavedb` connection string.

## Stop — cleanly

Preferred:

```powershell
.\scripts\stop-dev-server.ps1
```

**Gotcha the script exists to encode:** if you started `dotnet run` backgrounded from a shell
(`... &`), the PID the shell hands back is the `dotnet` launcher, not the actual
`TheCanalaveLibrary.Server.exe` worker process. Killing that PID leaves the real process (and the
port) alive. The script finds and kills whatever is bound to the port, then verifies the port is
actually free (raw equivalent below; don't name the variable `$pid` — it's a PowerShell read-only
automatic variable):

```powershell
$conn = Get-NetTCPConnection -LocalPort 5028 -State Listen -ErrorAction SilentlyContinue
if ($conn) { Stop-Process -Id $conn.OwningProcess -Force }
```

## Dev DB lifecycle — keep or wipe (agent's choice)

The dev database is a **persistent workbench**, not a per-run scratch space. Before starting
work against it, decide deliberately:

- **Keep (the default).** Existing state may be a fixture someone built by hand — a bug repro
  mid-investigation, a manually-arranged scenario — and it is not recoverable after a wipe.
  When the current state doesn't interfere with what you're doing, leave it alone. If you can't
  tell whether the state is deliberate, **ask the user before wiping**.
- **Wipe** when: prior state could confound what you're about to observe; the schema or seed
  data changed (new migration, edited `DataSeeder`); junk rows are causing FK/uniqueness
  errors; or the user asks for a clean slate.

Wipe = one script (never hand-delete rows — FK-ordered surgical deletes are a losing game):

```powershell
.\scripts\reset-dev-db.ps1            # stop server + DROP DATABASE; next start rebuilds
.\scripts\reset-dev-db.ps1 -Restart   # same, then starts the server (migrate + seed happen now)
```

There is no CREATE step: the next Development startup's `MigrateAsync` recreates the database
and `DataSeeder` populates it (`DevSeed=Full` per `appsettings.Development.json`).

**What a fresh DB contains** (full inventory + rationale: `DataSeeder.cs` header, the single
source of truth): 7 deterministic users (`TestUser`=1 and `AdminUser`=2 for the dev bar,
`ModUser`, `AuthorAlpha`, `AuthorBeta`, `ReaderGamma` mature-off, `LurkerDelta` private;
all `Password123!`), a real tag taxonomy, 12 stories across ratings/statuses (multi-chapter,
alternate chapter version, drafts, two pending-approval for the mod queue), TestUser bookshelf
rows on every tab, comments/recommendations/groups/blog posts/messages/notifications/reports.
Names are deliberately artificial ("Seed Story: …") — seed data must never read as real
community content.

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

## Browser-based debugging & verification (Chrome)

For checks that curl can't answer — does the page actually render, does a click flow work, does a
bug reproduce only under a real circuit — drive the running server with the
`mcp__claude-in-chrome__*` tools instead of (or in addition to) curl. This is a debugging
*technique*, not a workflow phase or a fourth automated test tier, and it isn't CI'd: `dotnet test`
(Unit / Integration / RazorComponents, per `canalave-conventions/testing.md`) remains the source of
truth for CI-verified correctness. See `canalave-conventions/debugging.md` for *when* to reach for
it as part of full-breadth debugging (in short: whenever a hypothesis depends on real rendering,
real navigation, real auth-cookie behavior, or real DI-scoped services shared across
concurrently-initializing components — realism the automated tiers deliberately trade away for
speed and determinism). What it's good for: first discovery of a runtime-only bug, and confirming a
fix actually resolves it. What it doesn't do: replace or generate automated regression coverage —
a bug found this way still needs a same-session code fix (`debugging.md` "Fix same-session"), and,
where the bug class is testable within one of the three tiers, a test added there.

1. Start the server headless per "Start" above and confirm it's listening.
2. If the browser tools are deferred, load them in one `ToolSearch` call:
   `select:mcp__claude-in-chrome__tabs_context_mcp,mcp__claude-in-chrome__navigate,mcp__claude-in-chrome__computer,mcp__claude-in-chrome__read_page,mcp__claude-in-chrome__tabs_create_mcp`
3. Call `tabs_context_mcp` (with `createIfEmpty: true` if no tab group exists yet) before anything
   else, then `navigate` a tab to `http://localhost:5028/`.
4. Use `computer` (`screenshot`, `left_click`, `type`, etc.) or `read_page`/`get_page_text` to
   drive and inspect the page.

**Skipping login:** `TheCanalaveLibrary.SharedUI/Layout/DevLoginBar.razor` renders a
`Dev | TestUser | AdminUser` bar, but only when `HostEnv.IsDevelopment()` — it's compiled into
every build, not stripped, so don't assume its absence means something broke; it means the app
isn't in Development. The links are plain anchors to GET `/dev/wu12/login-as/{username}`
(Development-only endpoint; signs in via `SignInManager` and redirects to `/`; users seeded by
`DataSeeder.cs`) — you can also just navigate to that URL directly, including to *switch* users
mid-session (AdminUser has the Admin role for `/mod/*` pages). No password, email confirmation,
or antiforgery form to drive. Prefer this over scripting the real login form when the thing under
test is what happens *after* auth, not the login flow itself. (It was previously a JS fetch-POST
from the bar, which silently dropped the request on an established circuit — don't reintroduce that.)

Stop the server the same way as "Stop — cleanly" above when done; browser tabs don't need explicit
cleanup.

## Known tooling false-alarm

`dotnet ef migrations list` (and similar EF CLI commands run outside the app's own startup)
can report `password authentication failed for user "postgres"` against a phantom `canalavedb`
database name. This is an EF design-time-host quirk — it does not read
`appsettings.Development.json` the way the real app does — and is not evidence the app or DB
connection is broken. Trust an actual `dotnet run` + HTTP check over `dotnet ef` CLI output for
connectivity questions.
