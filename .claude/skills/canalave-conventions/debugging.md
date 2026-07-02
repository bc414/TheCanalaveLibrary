# Debugging Methodology — All Layers

Applies whenever a bug surfaces that a plan, a code review, or `dotnet test` didn't predict — most
often during browser verification (see `run-server/SKILL.md` "Browser-based debugging &
verification"). This file is the *method*; it stays abstract and code-agnostic on purpose so it
doesn't go stale as features change. Concrete lessons from a specific bug belong in that feature's
`audit/<FolderName>.md` Stage note or, if the bug turned out to be cross-cutting, in
`cross-cutting.md` / `layer2-services.md` — never copied into this file.

## Reproduce before fixing

Get a reliable repro and know the exact trigger before touching code. A fix aimed at a guess is a
coin flip; a fix aimed at a reproduced failure is a test.

## Read every diagnostic surface, and correlate them

**The browser shows the symptom; the server log shows the cause and its preceding context.** A
crash rendered in the browser is the *last* thing that happened, not the *first* thing that went
wrong — read the server log *around* the exception, not just the exception line it prints.

This stack exposes several distinct diagnostic surfaces. Know all of them; reaching for only one
(usually the browser page) is how root causes get missed:

- **Server console log** (`dotnet run` stdout, e.g. `/tmp/server_run.log` per `run-server/SKILL.md`)
  — the highest-fidelity source. `ILogger` output, unhandled exceptions with full frames, and EF
  Core's `Executed DbCommand` query logging (every SQL statement, parameters, and timing in
  Development). Ordering here is what reveals concurrency and lifetime bugs — two overlapping
  `DbCommand`s are visible in this log long before they're visible anywhere else.
- **The ASP.NET/Blazor developer exception page** — the in-browser error page (Stack / Query /
  Cookies / Headers / Routing tabs). A rendering of the server-side exception; useful for the
  immediate frame and request context, but shows only the failure, not what led up to it.
- **Browser DevTools console** (`read_console_messages`) — JS errors, SignalR/circuit errors, the
  "An unhandled error has occurred" circuit-teardown bar.
- **Browser network tab** (`read_network_requests`) — HTTP status codes, the `_blazor` SignalR
  socket, failed `fetch`es (e.g. a dev-login POST), redirects.
- **The rendered page itself** (screenshot / `get_page_text`) — the error-boundary bar or raw
  exception HTML; often the only signal available without opening DevTools.
- **PostgreSQL log + direct `psql`** — constraint, lock, and FK violations, and the actual DB state
  before/after a call (see `run-server/SKILL.md` "Prerequisites" for `psql` fixture usage).
- **Dev-diagnostics endpoints** (`DevDiagnosticsEndpoints.cs`) — JSON probes for service-layer state
  that's awkward to reach through the real UI/auth flow (see `testing.md` "Dev-diagnostics endpoints
  are probes, not the regression net").

## Hypothesize the mechanism, not the symptom

State *why* the failure happens, not just what was observed. "It throws a 500" is a symptom;
"two components share one scoped instance and both issue an operation before either completes" is
a mechanism. Only a mechanism-level hypothesis can be falsified by a targeted fix.

## Re-run the identical repro after every candidate fix

A stack trace that *moves* — same exception type, different frame, after a fix — means a
contributor was removed, not the cause. Never declare victory on "a different error now, so
progress." Re-run the exact repro that first produced the failure; only a clean pass counts.

## Classify scope before recording

Before writing anything down, decide: is this bug confined to one feature, or does it live in
infrastructure shared across features (DI lifetime, a DbContext, layout-level chrome, a cross-cutting
service)? The answer determines where the finding belongs:

- **Feature-local** → that feature's `audit/<FolderName>.md` Stage note.
- **Cross-cutting** → the relevant convention file (`cross-cutting.md`, `layer2-services.md`, etc.)
  with pointers from each affected feature's audit file — never buried in just one of them. A bug
  that reproduces via two unrelated features sharing one piece of infrastructure is cross-cutting by
  definition, even if it was first noticed through a single feature's UI.

## Some bugs are only visible at runtime on a real circuit

The automated tiers (`testing.md`: Unit, Integration, RazorComponents/bUnit) each trade away some
slice of realism on purpose — that's what makes them fast and deterministic. That trade-off means
certain bug classes (real DI-scope sharing across concurrently-initializing components, real
circuit/SignalR timing, real auth-cookie behavior) are structurally invisible to all three tiers at
once. When a hypothesis depends on one of these, don't keep guessing against the automated
tiers — drive the actual running app in a browser instead. Mechanics are in `run-server/SKILL.md`
("Browser-based debugging & verification"); this file only says *when* to reach for it.

## Fix same-session

Once a bug is confirmed and understood, fix it in the same session it was found — don't leave a
known-unsound cell at its current Stage number while deferring the fix. This keeps `status.md`'s
Stage numbers meaningful: a Stage 5 cell should mean the code is actually sound, not "sound as far
as anyone got around to checking."
