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
(hardened 2026-07-07 from an earlier "deferred post-MVP" framing — resolved, see
`middle_plan_v2.md` §Resolved): messaging is request/response, identical in shape to every other feature. The
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
in `MainLayout`'s chrome, `render-and-layout.md`) calls
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
recommendations, profile bios, blog posts, AND private messages. WU6 shipped the full toolbar; a
compact narrow-viewport toolbar with overflow, **if ever built**, rides the adaptivity ladder's
rung-3 trigger — not a device fork, which no longer exists as a pattern (see
`layer3.5-structure.md` "Third-Party Wrapper Composite" and `layer4-style.md`'s adaptivity ladder).

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

- **Content** (stories, comments, blog posts, recs, polls): SET NULL on author/owner → anonymize,
  preserve. Polls joined 2026-09-30 (owner ruling D11, WU-TptHardDelete): an embedded poll is part of
  its post, and dying with its owner destroyed other users' votes. A NULL owner owns nothing
  (`layer1-data-model.md` §"Relationships & Queries").
- **Content parent → TPT child** (chapter → chapter comments, blog post → its comments and polls,
  group → group comments and group posts): RESTRICT. The service deletes the base rows first through
  `TptDelete` (owner ruling D10; `layer1-data-model.md` §"Hard-deleting a content parent").
- **Interaction data** (follows, interactions, badges, settings): CASCADE on user. **Poll votes are
  the one exception** (owner ruling D13): see "Account deletion" below.
- **Lookup tables** (tags, themes, statuses): RESTRICT → cannot delete if in use.
- **Blobs** (story covers, avatars) are not rows, so no FK reaches them. Every terminal deletion path
  captures the paths its rows name and deletes the blobs after the commit; a daily sweep collects the
  residue (owner ruling D14; `layer2-services.md` §"Image Storage — blob ownership and cleanup").
- **Comment deletion** (owner ruling D12, 2026-08-06): a hard delete. Replies are reparented to top
  level by `base_comments.parent_comment_id` `ON DELETE SET NULL`. There is no tombstone row, no
  placeholder node and no "removed" affordance anywhere in the thread; spec §5.9's placeholder is
  superseded, and deletion is invisible by design (`content-safety.md` §"Author-Controlled Content
  Actions"). That SET NULL **is** the reparent mechanism, so it is load-bearing and is **excluded from
  D10's content-parent RESTRICT set**. The spec's mechanism never worked: SET NULL erases which parent
  an orphan had, so any future revival of a placeholder needs a retained row, not this cascade.
- **Self-references** (parent tags, parent folders): SET NULL → children become top-level. The
  `group_folders.parent_folder_id` FK exists since WU-SchemaHardening (2026-10-13); before it the
  column was a bare integer and a deleted folder's sub-folders dangled out of sight. Top-level folder
  names are unique too (NULLS NOT DISTINCT), so a delete whose promoted sub-folder would duplicate a
  top-level name is rejected by the service before anything is deleted (`layer2-services.md` §"Group
  Folder Integrity"; a default, roadmap decision row 22).

### Account deletion — the ratified survival set (owner ruling D13, 2026-08-06; built WU-UserDeletion 2026-10-13)

Deleting an account is a **hard delete of the `User` row** (`UserDeletionService.DeleteUserAsync`,
`Server/Identity/`). Schema audit §3.8's three lists are the site's erasure policy, ratified with one
correction: poll votes moved from "destroyed" to "survives". Each list below was checked against the
live FK graph (`pg_constraint`, 2026-10-13).

- **Survives, anonymized (FK `SET NULL`):** stories, chapter contents, comments, blog posts,
  recommendations, series, the polls the user created (D11), **the user's poll votes** (D13), the
  private messages they sent (the other participant keeps the conversation), groups they founded,
  stories they added to groups, Community Spotlight placements they sponsored and slots granted to or by
  them, fanon links they made, external-identity reviews they performed as a moderator, and every
  report they filed, resolved or were answerable for (the moderation ledger).
- **Destroyed by cascade:** the login itself (the row carries email and password hash; Identity
  claims, external sign-in logins, roles and tokens cascade), profile, stats, notification and
  discovery-filter settings, story and chapter interactions (favorites, follows, read-later, reading
  progress), likes, recommendation-success credits, custom lists, saved tag selections, badges, group
  memberships, content reveals, the notifications they received, their conversation-participant rows,
  the follows they made, the vouches they gave, co-author / beta-reader / acknowledgment credits,
  tag-adoption states and verified external identities.
- **Destroyed or nulled by the service** (the RESTRICT edges, resolved before the delete): comments
  other people left on their profile (through `TptDelete`), follows and vouches they received, and the
  source of the notifications they caused (nulled; the recipient keeps the notification).
- **Closed by the service** (`ReportLedger`, D7's sub-edge): open reports on the account itself, on the
  destroyed profile comments, and on the messages of a conversation the deletion leaves empty.
- **Blobs** (owner ruling D14, built WU-BlobCleanup 2026-10-13): the profile picture's blob is deleted
  after the commit, best-effort, with the orphan sweep as the backstop. The covers of the user's
  stories stay with the surviving stories; whether to strip them is owner-open (`roadmap.md` decision
  row 25, tracker **F22**).

**Why poll votes survive when favorites cascade.** Interaction data whose only expression is a live
aggregate cascades: a favorite count measures a story's standing and *should* track the live user
population. A poll vote is a point-in-time record ("in June 2026, 37 people said X"), and nothing
denormalizes the tally, so cascading it rewrites a closed poll's published result. It survives
anonymized: `poll_votes.user_id` is nullable `ON DELETE SET NULL` behind a surrogate key
(`layer1-data-model.md` §"Column Conventions"), and the public voter list renders the row as
"[deleted user]" so the list keeps the tally's length. D12 (comments deleted silently) reached the
opposite answer **on cost, not on principle**: its price was a permanent third comment state on the
hottest read path; here the price is a nullable column on a small table and a render branch that
already exists.

**Conversations (D13 edge (a)).** With one participant left, the conversation survives intact: the
remaining participant keeps her row, the conversation and every message, and the deleted sender's
messages read "[deleted user]". With **no** participant left, `DeleteUserAsync` deletes the
conversation in the same transaction, after the save that cascades the participant rows (one
`ExecuteDeleteAsync`, scoped to the conversations the user was in; messages cascade). It first closes
open reports on those messages. No sweeper and no worker: account deletion is the only *designed*
producer of an empty conversation, so cleaning up at the source is exhaustive. A crash between two
saves was a second, undesigned producer; `StartConversationAsync` is one save since
WU-NotificationCorrectness, which closed it.
- **Two deletions of one conversation's participants are serialized on the conversation row.** The
  emptiness check is a check-then-act under READ COMMITTED: if both participants delete at overlapping
  times, each transaction's `NOT EXISTS` still sees the other's uncommitted participant row, neither
  deletes the conversation, and both commit an empty one that no later step reaches. So
  `DeleteUserAsync` locks the user's conversations (`SELECT … ORDER BY conversation_id FOR NO KEY
  UPDATE OF c`) before it deletes the user. The second deletion waits, and its check then sees the
  first one's committed delete. `NO KEY UPDATE` is the weakest lock two deleters conflict on, so a
  surviving participant's message insert (the FK's `KEY SHARE`) never waits on it; the id order keeps
  two deleters who share several conversations from deadlocking on them (WU-UserDeletion review
  fixes, 2026-10-13).

**Groups (D13 edge (b)).** A group whose last admin deletes their account (or leaves) survives with no
admin, `creator_id` NULL. That is a waiting state; a moderator assigns a new admin on request
(`layer2-services.md` §"Group Membership and Role Model").

**GDPR posture.** Erasure (Art. 17) attaches to personal data, not to content: a comment's text is
personal data because it is linked to its author, and once the link is severed the record is anonymous
data (Recital 26). Anonymizing in place is therefore not retention under an exception. Three things
bind every future change:
1. **The anonymization must be genuine.** The user must be able to delete their own content before
   deleting the account, and the deletion page must say so. Severing the FK does nothing for content
   whose text identifies its author. **Not yet met for two content types** (the page says so):
   stories have no self-delete (WU-AuthorStoryDelete, D15, adds it and the page's link), and blog posts
   of every subtype have a service delete but no button (tracker **B25**, which no WU owns yet). A
   recommendation the story's author removed (`Rejected`, the sticky block record) refuses its
   writer's delete; whether this rule reaches it is owner-open (tracker **F20**).
2. **No retained mapping, ever.** No `deleted_user → old_user_id` table, for support or any other
   purpose, and no softening of the hard delete into an anonymize-in-place. Either converts
   anonymization into pseudonymization and collapses the analysis.
3. **Account-level fields go unconditionally:** email, password hash, external sign-in links,
   settings, **and any auth/IP logs** (the owner's list). The database half is met by the hard delete:
   no table stores an IP address or an auth log (the per-IP rate-limit partitions live in memory and are
   never written). The log half is **a ruled obligation, not an open question**: the application log
   stream carries user ids on auth events (the deletion itself logs one) and on ordinary service lines,
   some of which pair a user id with a content id. Until a production log sink exists there is nothing
   to bound, so the obligation sits on Phase 7: the sink gets a retention bound (or a purge) that makes a
   deleted account's auth/IP log lines go — a bound on the one stream reaches the id-pairing lines
   too — sized with decision row 10's privacy policy (tracker **E11**;
   `security.md` §"Phase-7 Deferred Register"). Until then the deletion page says plainly that server
   logs are not erased with the account.

*Boundary, not ruled:* two polymorphic id columns keep the bare integer of a deleted account
(`reports.reported_entity_id` on a `User` report; `notifications.related_entity_id` on the
user-anchored types). Neither maps the id to the anonymized content, so rule 2 holds; whether they
should be zeroed at deletion is owner-open (tracker **F19**).

**The deletion page's copy is the compliance surface** (Art. 13/14 transparency).
`DeletePersonalData.razor` enumerates this policy in the user's terms: what survives anonymized
(stories with their cover images), what is destroyed (the profile with its picture), how to delete
content first (a cover through "Remove cover", since the story it sits on survives; and where that
path does not exist or is closed: stories,
blog posts, votes on a closed poll, a recommendation the author removed), that sent messages cannot be
deleted and stay in the recipient's inbox, that server logs are not erased with the account, and that
anonymization removes the name but not identifying details written into the content. **Change the
policy, change the copy, in the same WU.**

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
