---
name: run-server
description: >
  How to start and cleanly stop The Canalave Library's web app for local dev/verification.
  Use whenever asked to run, start, boot, launch, stop, or kill the server, or to verify a
  change works end-to-end against the real database. Two run paths exist: server-only
  (TheCanalaveLibrary.Server directly against local Postgres — the lightweight default) and
  the Aspire path (AppHost orchestrates containerized Postgres + Redis + Garage S3 + Mailpit +
  dashboard, with S3-backed image storage and a real transactional-email send path).
---

# Running the server

## Two run paths — pick one, they're mutually exclusive

Both bind the web app to `http://localhost:5028`, so only one can run at a time (every start
script refuses to start if 5028 is taken). All verification techniques below — curl, browser
tools, DevLoginBar, dev diagnostics endpoints — work identically under either path.

| | **Server-only** (default) | **Aspire** |
|---|---|---|
| Start/stop | `start-dev-server.ps1` / `stop-dev-server.ps1` | `start-aspire.ps1` / `stop-aspire.ps1` |
| Database | Local PostgreSQL 18, port **5432**, `TheCanalaveLibraryDB` | Containerized Postgres 18, port **5433**, `canalavedb` |
| Redis / S3 (Garage) | none | containers (6379 / 3900) |
| Image storage | `Local` provider (wwwroot/uploads) | `S3` provider (Garage bucket `canalave-images`) |
| Email | `NoOp` sender — `RegisterConfirmation.razor` shows the confirmation link on-page | `Smtp` sender (MailKit) → Mailpit inbox, `http://localhost:8025` |
| Needs Docker | no | yes (Docker Desktop running) |
| DB wipe | `reset-dev-db.ps1` | `reset-aspire-db.ps1` |

**When to pick which:** server-only for ordinary L1–L4 feature work (faster inner loop, no
Docker dependency — and the standing workbench DB with hand-built fixtures lives there). Aspire
when the work touches orchestrated infrastructure (Garage/S3 image storage, service
discovery, OpenTelemetry traces in the dashboard, or the deferred N≥2 Valkey/RESP swap of the
signal buffers — the `cache` container is consumed by nothing at N=1) or when verifying the app
boots correctly under the orchestration it will resemble in production. **The two paths have separate
databases** — state built in one does not exist in the other; the "Dev DB lifecycle" keep-or-wipe
rules below apply to each independently.

## Server-only path

### Prerequisites

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

### Start

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

### Stop — cleanly

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

## Aspire path

`AppHost/` (Aspire 13, SDK + hosting packages version-aligned — keep them that way, `aspire update`
does it) orchestrates five resources: containerized **Postgres 18** (host port **5433** — local
PG 18 owns 5432; database `canalavedb`), **Redis** (resource name `cache`, port 6379), **Garage**
(S3-compatible blob store, S3 API port **3900**, bucket `canalave-images` — supersedes the spec's
MinIO, see `audit/ImageStorage.md`), **Mailpit** (dev email inbox, SMTP port **1025**, web UI port
**8025** — WU-Email), and the web project (same `http://localhost:5028`, with the `S3`
image-storage provider and `Smtp` email provider both active via injected env vars — uploads go
to the Garage bucket and stored `/uploads/…` URLs are served from it by `ImageEndpoints`;
confirmation/reset/email-change mail is sent for real over SMTP to Mailpit instead of surfacing
as an on-page link). Postgres/Redis/Garage are **persistent-lifetime with named volumes**
(`canalave-postgres`/`canalave-redis`/`canalave-garage`): they keep running and keep data after
the AppHost stops, which makes restarts fast and the Aspire DB a persistent workbench. Mailpit is
persistent-lifetime too (stays up across AppHost restarts) but carries **no data volume** — mail
is ephemeral and safe to lose, so wiping it is just restarting the container. On first web-app
start against an empty volume, migrate + seed runs exactly like the server-only path. Garage
self-bootstraps its layout, access key, and bucket on start (`--single-node --default-bucket`,
restart-idempotent); its config is bind-mounted from `AppHost/garage.toml`.

Credentials are **AppHost user secrets** (`Parameters:postgres-password` = `butterfree`,
`Parameters:garage-s3-secret` and `Parameters:garage-rpc-secret` = random hex, all set via
`dotnet user-secrets set --project AppHost`). They are machine-local: a fresh clone must set all
three before the AppHost will start (the Garage ones can be any fresh random hex — but changing
them after the Garage volume exists orphans the bootstrapped key; wipe `canalave-garage-meta`/
`-data` volumes if you rotate them).

### Start / stop / wipe

```powershell
.\scripts\start-aspire.ps1               # foreground, Ctrl+C to stop (manual use)
.\scripts\start-aspire.ps1 -Background   # detached; logs to %TEMP%\canalave-aspire.log,
                                         # waits for the WEB APP (not just the dashboard) to answer
.\scripts\stop-aspire.ps1                # kills AppHost; DCP tears down the web server;
                                         # containers KEEP RUNNING (add -StopContainers to stop them)
.\scripts\reset-aspire-db.ps1            # stop AppHost + remove postgres container AND volume;
                                         # next start rebuilds (add -Restart to do that immediately)
```

Docker Desktop must be running first (the script checks). First-ever start pulls the three
images — allow minutes; `-TimeoutSeconds 600` if needed. The background script prints the
**tokenized dashboard login URL** (`http://localhost:15031/login?t=…`) extracted from the log;
dashboard auth stays on (don't disable it — it's one grep to get the token).

### Verifying against the Aspire DB

Same techniques as everywhere else, different port: `psql -h localhost -p 5433 -U postgres -d
canalavedb` (password `butterfree`). `docker ps --filter name=canalave-` shows the containers;
container logs via the dashboard (per-resource Console/Structured logs + OTel traces) or
`docker logs canalave-postgres`. Blob ground truth (Garage has no web console):
`docker exec canalave-garage /garage bucket info canalave-images` (object count + size), and a
direct `curl` of the stored `/uploads/…` URL (S3 mode serves it from the bucket — 200 with
`Cache-Control: immutable` proves the full write→store→serve loop). **Email ground truth:**
open `http://localhost:8025` (Mailpit's web UI, no auth) — register/reset/email-change flows land
there as real messages; click the confirmation/reset link directly from Mailpit's rendered HTML
view to drive the rest of the flow. Mailpit also exposes a JSON API
(`GET http://localhost:8025/api/v1/messages`) if a script needs to assert on the latest message
without a browser.

### Gotchas encoded here

- **http transport:** the AppHost runs on plain http locally, which Aspire only allows with
  `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` — set in both the `http` launch profile and
  `start-aspire.ps1`. Removing it makes startup fail with an `applicationUrl … must be an https
  address` OptionsValidationException.
- **Postgres major pinned:** `WithImageTag("18")` — PG minors are volume-compatible, majors are
  not. A major bump = `reset-aspire-db.ps1` (wipe + reseed), never an in-place tag change.
- **Image storage differs per path by design:** Aspire = `S3` provider (Garage bucket), server-only
  = `Local` provider (wwwroot/uploads). An image uploaded under one path does not exist under the
  other — same separate-workbench rule as the databases. `reset-aspire-db.ps1` wipes only
  Postgres; Garage blobs whose DB rows were wiped become harmless dev orphans (wipe them too by
  removing the `canalave-garage` container + `canalave-garage-meta`/`-data` volumes).
- **The S3 client wire format is deliberate** — unchunked uploads, `WHEN_REQUIRED` checksums,
  path-style, centralized in `S3ImageStorageService.CreateClient`. Don't "simplify" them away;
  they are what keeps Garage (dev) and Cloudflare R2 (prod) interchangeable. Detail:
  `audit/ImageStorage.md` "R2 interchangeability".
- **Email differs per path by design, same as image storage:** Aspire = `Smtp` provider →
  Mailpit (real send, no external account); server-only = `NoOp` provider → the on-page
  confirmation link in `RegisterConfirmation.razor` (gated on `EmailSender is
  IdentityNoOpEmailSender`, so it auto-hides the moment a real sender is configured — no code
  change needed to move between the two). Don't expect a server-only registration to produce a
  Mailpit message, or vice versa.
- **`stop-dev-server.ps1` is the wrong stop for this path** — it frees 5028 but leaves the
  AppHost + containers up (and DCP may show the web resource as failed). Use `stop-aspire.ps1`.

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
community content. Seed rows that participate in workflow state machines must carry valid
**target** states, not just valid FKs — a row can satisfy every constraint and still make the
workflow a silent no-op (see the `PostApprovalStatus` comment in `DataSeeder.cs` for the worked
example).

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

### Driving the UI reliably

Tool-behavior notes below were verified against the claude-in-chrome extension on 2026-07-02; the
extension updates frequently and has no unified known-limitations page upstream, so if a note here
contradicts observed behavior, re-verify empirically before trusting either.

**Tab state is everything (setup, do this first).** Chrome's Memory Saver / Energy Saver freezes
background tabs, and a frozen renderer hangs the CDP layer every browser tool sits on. Nearly every
"tool failure" in the first L4.5 pass — screenshot timeouts ("renderer may be frozen"), JS-eval
timeouts, clicks that change nothing, `loading="lazy"` images stuck at `complete:false` — was this
one cause, not the tools and not the app. Work in a freshly created tab (it becomes the window's
active tab) and prefer re-creating a tab over fighting a stale one; ask the user to keep the
window visible / exempt `localhost` from Memory Saver (`chrome://settings/performance`) if
throttling persists. **Recognize the symptom cluster and foreground/recreate the tab before
suspecting the app.**

**Coordinates are a documented contract, not a stable one.** `computer` coordinates live in
screenshot-pixel space = CSS px × `devicePixelRatio` (Windows commonly 1.25), and the mapping
moves whenever the window/viewport changes. Pin the viewport with `resize_window` (≈1280×720) at
session start if coordinate clicks are unavoidable — but prefer refs and `form_input`, which don't
depend on the mapping at all.

**Use each tool for what it's for.** `form_input` is the intended tool for form controls (text
inputs, selects, checkboxes, textareas) and — verified 2026-07-02 — its values serialize correctly
into both static-SSR form POSTs (Identity pages) and interactive-circuit `@bind` fields. `computer`
clicks (ref-based is fine) are for buttons and links. Clicks can transiently no-op even in a
healthy tab: **always verify the effect** (page text, network log, DB) **and retry once** before
escalating.

**Fallbacks when a healthy tab still won't cooperate.** On an interactive circuit, JS dispatch via
`javascript_tool` reaches Blazor reliably: `el.click()` bubbles into Blazor's delegated event
handler; for `@bind` fields set `.value` then
`el.dispatchEvent(new Event('change', {bubbles: true}))` (`'input'` when the field binds with
`@bind:event="oninput"`); Quill editors: set `.ql-editor.innerHTML` and dispatch `input` — the
pull-on-submit `GetHtmlAsync` reads the editor DOM. When a tab can't be foregrounded, verify image
assets by direct `fetch` + decode and `read_network_requests` instead of screenshots (a
never-rendered tab never loads lazy images — that's browser behavior, not a bug).

**Ground truth is the database.** After every UI mutation, confirm the actual row via `psql`
(credentials in Prerequisites) before declaring the behavior verified — page text can lag, lie, or
describe optimistic local state.

Stop the server the same way as "Stop — cleanly" above when done; browser tabs don't need explicit
cleanup.

## Known tooling false-alarm

`dotnet ef migrations list` (and similar EF CLI commands run outside the app's own startup)
can report `password authentication failed for user "postgres"` against a phantom `canalavedb`
database name. This is an EF design-time-host quirk — it does not read
`appsettings.Development.json` the way the real app does — and is not evidence the app or DB
connection is broken. Trust an actual `dotnet run` + HTTP check over `dotnet ef` CLI output for
connectivity questions.
