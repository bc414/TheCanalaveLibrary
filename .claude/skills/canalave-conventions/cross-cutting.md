# Cross-Cutting Concerns

**Narrowed scope (2026-07-07):** this file used to hold ~26 unrelated sections and had grown too
large to be a single coherent unit. It was split into four themed files —
[render-and-layout.md](render-and-layout.md), [identity-and-authorization.md](identity-and-authorization.md),
[content-safety.md](content-safety.md), [error-handling.md](error-handling.md) — and several
feature/layer-specific sections were relocated into the existing layer file that already owns their
mechanism half (`layer2-services.md`, `layer3.5-structure.md`, `layer3-logic.md`). See each target
file's intro line for what moved where. What remains here is genuinely cross-cutting infra/misc that
didn't fit any single theme: messaging's architecture posture, rich-text sanitization, Aspire dev
orchestration, read-replica readiness, delete policy, and dev-only diagnostics.

## Private Messaging Architecture

### Stateless, permanently — SignalR is ruled out, not deferred

The spec described "real-time via SignalR" for messaging. That framing is **permanently reversed**
(hardened 2026-07-07 from an earlier "deferred post-MVP" framing — see Resolved,
`middle_plan_v2.md`): messaging is request/response, identical in shape to every other feature. The
recipient sees new messages on navigate/refresh; the global unread badge refreshes on layout render
(navigation). The rationale: the use-case is substantive and infrequent, and real-time delivery
already has an owner — Discord handles ambient chat, this site's messaging is deliberately for
substantive, async, long-form conversation, the same decision that constrains group conversations
away from live chat. SignalR realtime buys nothing here and would cost a lot (first and only
app-level Hub, new test harness, reconnection handling — with no existing template) for a use case
that doesn't want it.

**This is not "someday, additive."** `MessagesHub` was the only SignalR Hub ever proposed anywhere
in this project; with it gone, the app has zero app-defined Hubs, permanently — see
`horizontal-scaling.md` §2 for why that also means no SignalR backplane is ever needed at N≥2.

### Two unread systems by design — do not unify

The app has two entirely separate unread-state systems, each the right shape for its domain:

| System | Model | Read-state unit | Cleared by |
|---|---|---|---|
| **Notifications** | Event rows in `Notification` table | Per-event boolean | Individual dismiss / "mark all read" |
| **Messaging** | `ConversationParticipant.LastReadTimestamp` | Per-conversation high-water mark | Timestamp write on thread open |

**Do not unify them.** Notification event-rows and conversation watermarks answer structurally
different questions:
- A notification is a discrete point-in-time fact; once read, it stays done.
- A conversation is a durable object you reopen; "unread" means "messages after where I left off,"
  and marking-read is one timestamp write that clears an unbounded set in O(1).

Generating a `Notification` row on every incoming PM would create **two unread truths** (the
watermark AND the notification's read flag) that must be kept in sync, cleared in two inboxes, with
the attendant sync bugs. A notification would also be a pointer with no content of its own, layered
over a message that already has a first-class home and its own unread boundary.

**Rule:** private messages **never** create `Notification` rows. `INotificationWriteService` is not
injected by `IMessagingWriteService`. The global unread-messages badge (a `MessagesNavLink` component
in the Desktop/Mobile layout chrome, `render-and-layout.md`) calls
`IMessagingReadService.GetUnreadConversationCountAsync()` directly — that badge is the cross-cutting
signal; the notification bell is for social/content events.

### 1-on-1 only

Group conversations are out of scope. The N-participant data model (`ConversationParticipant`) is
kept, but the compose flow always targets a single recipient and every conversation exactly two
participants. Group discussions happen in public on-site (`/group/…`) or off-site.

## Rich Text & Sanitization

All user-submitted HTML is sanitized **server-side** with `HtmlSanitizer` (allow-list) before saving.
Never trust client sanitization, never persist raw user HTML. Full allow-list + write-path contract:
`layer2-services.md` §"User HTML Is Sanitized Once, On Save — Never On Display".

**EditorView** (universal across all text surfaces): chapters, comments, author notes, descriptions,
recommendations, profile bios, blog posts, AND private messages. Desktop shows full toolbar; mobile
shows compact toolbar with overflow for less-used formatting **(deferred — WU6 shipped desktop only;
not MVP-blocking, see `layer3.5-structure.md` "Third-Party Wrapper Composite")**.

## Aspire 13 Configuration

Live since 2026-07-05 (WU-Aspire). AppHost defines dev containers; it **never deploys**. The
authoritative resource graph is `AppHost/AppHost.cs` itself (Postgres 18 on host port 5433 →
`canalavedb`; Redis as `cache` on 6379 (unconsumed at N=1 — see `horizontal-scaling.md`); Garage
(S3-compatible, `dxflrs/garage`) on port 3900, superseding the spec's MinIO; web = Server's `http`
launch profile → 5028). Run/stop/wipe workflow: `run-server/SKILL.md` "Aspire path".

Standing rules, all applied in `AppHost.cs`:

- **Version alignment is a correctness constraint:** the `Aspire.AppHost.Sdk` (the csproj's
  top-level SDK since 13.x — it encapsulates `Aspire.Hosting.AppHost`, no explicit package),
  every `Aspire.Hosting.*` package, and any app-side `Aspire.*` client package ride the same
  version (13.4.6). DCP + dashboard are delivered through the SDK's NuGet pin — a mismatched SDK
  runs mismatched orchestration binaries. `aspire update` bumps the whole set.
- **Persistent-lifetime containers + named volumes + stable secrets.** All three backing services
  use `WithLifetime(ContainerLifetime.Persistent)`, `WithContainerName("canalave-…")`, and named
  `-data` volumes. Passwords are `AddParameter(..., secret: true)` from AppHost **user secrets**
  (`Parameters:postgres-password`, `Parameters:minio-password`) — a stable password is mandatory
  once a data volume exists (a regenerated one no longer matches the initialized cluster).
- **The Server consumes plain connection strings.** `WithReference(canalaveDb)` injects
  `ConnectionStrings__canalavedb` as an env var, which overrides `appsettings.Development.json`;
  plain `GetConnectionString("canalavedb")` + `AddDbContext` just works. **No Aspire Npgsql EF
  client package** — `AddNpgsqlDbContext` stays banned per `layer2-services.md` "DbContext
  Registration" (pooling vs. Scoped `IActiveUserContext`). The `cache` container is provisioned
  but consumed by nothing at N=1 — it exists for the deferred N≥2 body-swap of the in-process
  signal buffers (Layer 7 dissolved 2026-07-06). Full N≥2 scale-out story: `horizontal-scaling.md`.
- **The dev S3 endpoint is Garage (`dxflrs/garage`), a plain `AddContainer`** — settled 2026-07-05,
  superseding the spec's MinIO (OSS archived 2026-02; its Aspire toolkit package deprecated).
  `--single-node --default-bucket` bootstraps layout/key/bucket from `GARAGE_DEFAULT_*` env vars
  (restart-idempotent); config bind-mounted from `AppHost/garage.toml`, whose `s3_region` must
  match the injected `ImageStorage:S3:Region` ("garage"). The client side is **the same AWS S3 SDK
  code** as Cloudflare R2 in prod — interchangeable only under the three wire-format constraints
  centralized in `S3ImageStorageService.CreateClient` (unchunked uploads, WHEN_REQUIRED checksums,
  path-style); rationale + R2 specifics: `audit/ImageStorage.md` "R2 interchangeability".
- **ServiceDefaults** holds shared cross-cutting config (telemetry, health checks, resilience);
  under the AppHost the OTLP env vars light up the dashboard's logs/traces/metrics automatically.

**Dual-Configuration Strategy:** `dotnet ef` CLI cannot see AppHost's configuration. Design-time EF
tooling and the server-only run path both read the Server project's own configuration
(`appsettings.Development.json` → local Postgres 5432); the AppHost env-var injection only exists
at Aspire runtime. The two run paths therefore point at **different databases** by design.

## Read Replica Awareness

No physical read replica exists today — there is one Postgres instance under both run paths
(`run-server/SKILL.md`). The `ReadOnlyApplicationDbContext`/`ApplicationDbContext` split is
architectural **readiness** for a future replica (`SKILL.md`: "read replica when scale demands"),
not an active replication topology. When a replica does land, the same reasoning applies as today:
UI shows optimistic local state after a write rather than immediately re-reading through the read
context, since even a genuine replica would trail the primary slightly.

## Delete Policy Summary

- **Content** (stories, comments, blog posts, recs): SET NULL on author → anonymize, preserve.
- **Interaction data** (follows, interactions, badges, settings): CASCADE on user.
- **Lookup tables** (tags, themes, statuses): RESTRICT → cannot delete if in use.
- **Self-references** (parent comments, parent tags, parent folders): SET NULL → children become top-level.

## Dev-Only Diagnostic Endpoints

When a code path is hard to exercise through the real UI/auth flow during local verification
(e.g. an operation scoped to "the currently authenticated user," which would otherwise require
logging in as a throwaway fixture user), add a Development-only minimal-API endpoint that calls
the service method directly instead of reaching for a one-off temporary endpoint inline in
`Program.cs`.

**Home:** `TheCanalaveLibrary.Server/Diagnostics/DevDiagnosticsEndpoints.cs`
(`MapDevDiagnosticsEndpoints`), same `{Feature}Endpoints.Map{Feature}Endpoints` shape as
`StoryEndpoints`. Mapped exactly once, inside the existing `if (app.Environment.IsDevelopment())`
block in `Program.cs` — never reachable outside local dev. Add new diagnostic routes to this one
file rather than creating new ad-hoc endpoint files or inlining lambdas in `Program.cs`; it's the
single auditable place reviewers (and future agents) check for "what dev-only backdoors exist."

See `.claude/skills/run-server/SKILL.md` "Dev diagnostics endpoints" for the verification workflow
(pairs with direct `psql` fixture setup/assertions).
