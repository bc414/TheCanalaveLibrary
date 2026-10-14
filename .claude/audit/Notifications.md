# Audit — Notifications/

**Features:** 41 (generation), 42 (display), 43 (settings), 57 (cleanup worker). Cross-cutting generation
pattern defined here (§9.4). Route `/notifications`.

## Shared Context

**Entities (Core/Models/):** `Notification` (`RecipientUserId` Cascade, `SourceUserId` **SET NULL** — see
correction note below — polymorphic `RelatedEntityId`, `DateCreated` default), `NotificationCategory` (9,
seeded), `NotificationType` (~35, seeded with gap-based numbering, `DefaultEmailEnabled`/`DefaultCollapsed`),
`UserNotificationSetting` (sparse override, PK `(UserId,NotificationTypeId)` — stores `EmailEnabled` and
`Collapsed`). **No services or components built prior to WU22.** The seed data here is one of the most
complete parts of the model.

**Spec corrections recorded here (code is authoritative — see CLAUDE.md §"Spec relationship"):**

1. **`SourceUserId` delete behavior — SET NULL, not RESTRICT.** Spec §1171 and the original audit note
   both say "`SourceUserId` Restrict." The implemented and WU1-tested behavior is SET NULL (anonymize
   the source user on deletion), matching the delete-policy convention for content ("anonymize, preserve").
   You cannot `RESTRICT`-block deleting a user because they once triggered a notification. The spec/audit
   "Restrict" label is stale.

2. **§5.18 in-app toggle dropped — in-app delivery is always-on.** Spec §5.18 describes per-type
   "toggles for in-app and email." The `UserNotificationSetting` table stores only `EmailEnabled` and
   `Collapsed` — there is no `InAppEnabled` column and no per-type in-app mute. This is a deliberate
   departure: in-app delivery is always-on for eligible recipients; no L1 schema change is planned.
   The two user-settable fields are `EmailEnabled` (email side-channel, post-MVP) and `Collapsed`
   (per-user display override of `NotificationType.DefaultCollapsed`, consumed by the notification
   panel). The §5.18 in-app toggle language should be understood as aspirational/stale.

**Notification email fan-out — hook point documented (WU-Email, 2026-07-06); sequenced as
WU-NotifEmail (2026-07-15); settled constraints revised 2026-07-31 (tracker B1):** `EmailEnabled`
above is fully plumbed (written/read by the settings page via `SetSettingAsync`/`GetSettingsAsync`)
but remains genuinely **unconsumed** — the create-core generates only in-app `Notification` rows;
nothing sends mail off it. WU-Email built the transactional email seam
(`Server/Identity/SmtpEmailSender.cs`, real send over SMTP — see `audit/Identity.md` WU-Email Stage
note) but deliberately scoped it to Identity's confirmation/reset/email-change flows only.
**WU-NotifEmail** is where this gets consumed.

**Settled (do not revisit):**

- **Hook — unchanged from 2026-07-06.** The single funnel
  `ServerNotificationWriteService.CreateCoreAsync` (`layer2-services.md` "Notification Generation").
  Hooking there inherits both create-core invariants for free: only rows that survived drop-self and
  dedup, and actually got inserted, are eligible for mail.
- **Eligibility rule — unchanged in intent, per field since D19.** Effective `EmailEnabled` = the
  sparse `UserNotificationSetting` row's **non-NULL** `EmailEnabled`, or
  `NotificationType.DefaultEmailEnabled` when there is no row or the column is NULL (owner ruling
  D19, WU-NotificationCorrectness 2026-10-12 — the columns became nullable). Same LEFT JOIN shape
  `GetSettingsAsync` already implements.
- **Best-effort posture — unchanged.** A mail failure never fails, delays, or mutates the in-app
  notification. Same posture as the rest of create-core.
- **Write-behind, not inline (revised 2026-07-31; Brian-ratified).** This **supersedes** the
  2026-07-06 wording, which said to "build the inline version first and measure before" adding a
  worker. The measurement is not needed: ~22 of the seeded types carry
  `DefaultEmailEnabled = true` (`Server/Data/Configurations/NotificationConfigurations.cs`), and
  several of those — `NewChapterOnFollowedStory`, `NewStoryFollower`, `NewStoryFavorite`,
  `NewStoryComment` — fan out to every follower of a story or author. Inline means
  N × (connect → auth → send → disconnect) inside a SignalR circuit write path. That is a
  known-shape latency and provider-rate-limit problem, not an open empirical question. Create-core
  enqueues to an in-process buffer; a `BackgroundService` drains it and sends the batch over one
  pooled SMTP connection. Follows the established buffer/flusher/worker trio
  (`layer2-services.md` §"Signal Buffering"; `Server/Chapters/ReadingProgressFlushWorker.cs`).
- **Transport seam (settled 2026-07-31).** Notification mail does **not** ride `IEmailSender<User>`
  — that is Identity's three-method contract (confirmation link / reset link / reset code). A
  general `IMailTransport` is extracted from `SmtpEmailSender`'s send body and shared by both paths,
  so the `Email:Provider` switch, `EmailOptions`, and the `CanalaveTelemetry.Email` span/counters
  stay single-sourced.
- **One-click unsubscribe (settled 2026-07-31; Brian-ratified).** Every notification email carries
  RFC 8058 `List-Unsubscribe` + `List-Unsubscribe-Post` headers pointing at an anonymous tokenized
  endpoint, plus a visible footer link. Token is Data-Protection-signed (no schema, no migration).
  Unsubscribing sets `EmailEnabled = false` for that one type via the same sparse helper as
  `SetSettingAsync` (`NotificationSettingUpsert`). Since D19 (WU-NotificationCorrectness,
  2026-10-12) it writes only the `email_enabled` column — stored as NULL when the type's default is
  already off — and never touches `collapsed`.
- **Absolute links (settled 2026-07-31).** Reuse `IPublicUrlProvider` / `Site:PublicBaseUrl`
  (`Core/Seo/`) — a configured canonical origin deliberately not derived from the current request,
  which is exactly what a worker with no `HttpContext` needs. No new config key.
- **Recipient gating (settled 2026-07-31).** Gate on `EmailConfirmed == true` — never mail an
  unverified address. **Do not** gate on account status: `AccountWarning`, `AccountSuspended`, and
  `AccountBanned` all seed `DefaultEmailEnabled = true` and are precisely the notifications a
  restricted user must receive. A future pass that "hardens" this by suppressing mail to suspended
  users would be a regression — the Integration tier asserts the current behavior.

**Not folded in.** The anonymous-`NotificationBell` RazorComponents gap once attached to this WU is
**closed** — tracker H5, done in full at WU-TagFanon (2026-07-26). Nothing about that gap remains
for WU-NotifEmail.

**Does not close tracker F4.** F4 (provider + sending domain + SPF/DKIM/DMARC DNS) is orthogonal and
stays open after this WU: the code path is complete and Mailpit-verifiable, but no mail reaches a
real inbox until Phase 7's deliverability work lands.

**Owner ruling D18 (answered 2026-08-07; built WU-PrivateMessageEmail 2026-10-13) — settled, do not
revisit:**
- **Private messages are absent from the notification catalogue, by ratification.** No
  `NotificationTypeEnum` member, category, seed row, `RelatedEntityKind` arm or presenter arm, and no
  Messaging→Notifications dependency. PM email is Messaging-owned (`audit/Messaging.md` §"D18").
  The doc comments on `NotificationTypeEnum` and `INotificationWriteService` say so.
- **The email pipeline is shared infrastructure, not this cluster's.** The buffer and worker moved to
  `Server/Email/` and the flusher's send half became `OutboundEmailFlusher` there, carrying `(kind,
  id)` items; this cluster keeps only `NotificationEmailComposer` (the eligibility query and
  composition the flusher used to do) and `NotificationEmailBodies`. Every rule above about
  eligibility, recipient gating and one-click unsubscribe is unchanged and is now the notification
  composer's (`layer2-services.md` §"Outbound email pipeline").
- **The settings page also hosts Messaging's section** (D18's standing rider: every outbound
  non-transactional email appears on `/notifications/settings`), sourced from
  `IMessagingWriteService`, never from this cluster's services.

**Settled — blog-post targets by recipient (tracker D8, recorded 2026-10-13 at WU-TrackerFixes'
Doc-Touch moment 1; do not revisit).** Rule text: `layer2-services.md` §"Polymorphic RelatedEntityId"
→ "Three blog-post kinds". The tracker's recommended shape, the type split, is the one built:
- **`NewCommentOnBlog` (33) gets its own kind, `OwnBlogPost`,** which resolves the post as stored. Its
  recipient is always the post's author, who can still open their own post.
- **`BlogPostDirect` (13–16, 100) drops a post the recipient can no longer open:** unpublished (any
  subtype), or a profile post whose author is `Private`. Those recipients are never the author.
- **The enricher stays recipient-agnostic** (the email composer has no active user). Only the Class-A
  half of `BlogPostVisibilityGuard` applies; rating, audience and reveals stay unfiltered (Personal
  plane).
- **`PollUpdated` (100) names its post again** ("A poll you voted on in {post} was changed"), the copy
  the WU-NotificationCorrectness review fixes held back until this was built.

**Settled — story-family targets by recipient (tracker D21, recorded 2026-10-14 at
WU-D21Confidentiality's Doc-Touch moment 1; do not revisit).** Rule text and the type table:
`layer2-services.md` §"Polymorphic RelatedEntityId" → "Story-family kinds by recipient". Decided by the
settled Class-A rule (`StoryStatus` + `IsTakenDown`), not by an owner row:
- **A notification never names or links a story outside the public window (Draft, PendingApproval,
  Rejected) or taken down, unless its recipient is the story's author.** An unpublished chapter is the
  author's alone too.
- **Built the D8 way:** `Story`, `Chapter` and `GroupStory` each split into an `Own*` kind (recipient
  always the story's author; resolved as stored, unchanged) and a filtered kind (everyone else). The
  enricher stays recipient-agnostic.
- **Rendering follows D8:** a hidden story is a lookup miss, so the type's existing title-free arm
  renders, with no link. `GroupStory` keeps the group's name and link and drops only the story title.
- **The `Own*` kinds never link a page that 404s for the author** (added by the WU's review fixes,
  2026-10-14): a taken-down story keeps its title but loses its link in `OwnStory` and `OwnChapter`,
  and an unpublished chapter loses it in `OwnChapter`, because the story page and the chapter page
  refuse those to their author too. Whether such a notification should link the editor instead is
  decision row 27 (e).
- **Not decided here:** whether an author may file their own unpublished story into a group at all
  (decision row 31, tracker F28). Whichever way it goes, the members' 60 no longer names the story.

## WU-TagFanon slice (2026-07-26) — F41/F42 L2 stay Stage 5

`TagUpdateSuggestion = 26` finally has a sender. The seam was fully provisioned (enum value,
seeded lookup row, settings-page row, presenter arm, `CategoryFor` test arm) and generated nothing.

- **New semantic method** `NotifyTagAdoptionSuggestedAsync(recipients, targetTagId, moderatorId)`.
  `RelatedEntityId = targetTagId`; a new `RelatedEntityKind.Tag` arm in `KindFor` + batch loader
  resolves it to the tag name and `/tag-adoptions/{tagId}`.
- **The never-twice rule lives in the CALLER, not in dedup.** `TagAdoptionState.DateNotified`
  records that an author was told about a tag; unread-dedup would have re-fired on anyone who read
  the notification and moved on, which is exactly the nagging case the design forbids.
- **Presenter arm rewritten.** The existing text ("{actor} suggested tag updates for {target}")
  described a different, never-built event — it assumed a person acting on a story. It now names
  the tag and points at the adoption page.
- **Seed row reworded** ("Tag adoption suggested" / "A name you used in a story matches a new
  official tag.") — one type covers characters and settings, because one adoption surface does.
  `DefaultEmailEnabled` stays false, so WU-NotifEmail inherits a sane default.

Covered by `FanonPipelineTests` (Integration). **H5 closed in full here:**
`FakeNotificationWriteService` joined the fakes catalog and the anonymous-`NotificationBell`
regression test (for the crash fixed 2026-07-13) is finally written — it asserts an anonymous
render resolves NO notification services, which is the actual failure mode.

## WU-NotifEmail Stage note (2026-07-31) — F41/F42/F43 L2 stay Stage 5; tracker B1 closed

**`EmailEnabled` finally drives mail.** The setting had stored, rendered, and persisted since WU22
while driving nothing — the settings page was a legitimate Stage 5 sitting on top of dead plumbing
(tracker B1, the same invisible-gap shape as B0/B7/B11). No cell number changes: the cells were
already 5 and the surfaces they describe are unchanged. What changed is that they are now truthful.

**Why this ran before the Phase-6 gate it was parked at.** The stated blocker — an unchosen email
provider — was never a code blocker. `Email:Provider` selects between plain-SMTP implementations,
every candidate provider exposes SMTP, and Mailpit makes the whole path verifiable locally.
Recipient addresses already existed on `User : IdentityUser<int>`. The roadmap's other reason ("no
live audience") argued for delay but not for blockage, and building it before beta means the
settings page stops lying to the first real users.

**Built:**
- `Server/Email/` — new cross-cutting cluster (`folder_clusters.md`): `IMailTransport`/`OutgoingMail`,
  `SmtpMailTransport` (the MailKit send body + `Email.Send` span + sent/failed counters, extracted
  verbatim from `SmtpEmailSender`, plus a one-connection `SendBatchAsync`), `NoOpMailTransport`, and
  `EmailOptions` (moved from `Server/Identity/`). `SmtpEmailSender` is now a ~10-line adapter;
  Identity's behaviour and telemetry tags are unchanged.
- `Notifications/NotificationEnricher.cs` — the `RelatedEntityKind`/`KindFor`/batch-load trio lifted
  out of `ServerNotificationReadService`, recipient-agnostic so a worker with no `IActiveUserContext`
  can use it. The ~40-arm type switch is now single-sourced across the panel and email.
- `NotificationEmailBuffer` / `NotificationEmailFlusher` / `NotificationEmailWorker` — the standard
  buffer/flusher/worker trio, 30s cadence. `CreateCoreAsync` enqueues ids only; eligibility is
  resolved at drain time.
- `UnsubscribeTokenService` + `NotificationEmailEndpoints` — RFC 8058 one-click unsubscribe over a
  Data-Protection-signed token. No schema, no migration.
- `NotificationSettingUpsert` — the sparse upsert/delete rule extracted so `SetSettingAsync` and the
  token-authenticated unsubscribe share one implementation.
- `NotificationEmailBodies` — composes over `NotificationPresenter.Compose`, so email text cannot
  drift from the in-app panel.

**Two things worth carrying forward:**
1. **`IPublicUrlProvider` already solved the absolute-URL problem.** The plan proposed a new
   `Email:SiteBaseUrl`; `Site:PublicBaseUrl` (Core/Seo/, WU-Seo) is the same idea with the same
   rationale — a configured canonical origin deliberately not derived from the request. No new key
   was added. The AppHost did need `Site__PublicBaseUrl` pinned to the http profile's port, or every
   mailed link points at the unused https port.
2. **The GET/POST split on unsubscribe is not ceremony.** Corporate link scanners follow every GET
   in a message; a mutating GET would unsubscribe users who never clicked. GET renders a
   confirmation whose button POSTs.

**Verification.** `dotnet test` green: Unit 793 (+17), Integration 1039 (+18), RazorComponents 650
(unchanged). Tiers: Unit covers body composition and token round-trip/tamper/foreign-key-ring;
Integration covers every eligibility gate, both create-core invariants carrying through, the
restore-on-connection-failure path, and the three unsubscribe routes. **Live-verified** against a
real SMTP send to Mailpit (server-only run with `Email__Provider=Smtp`): follow → notification row →
email delivered within 12s, correct recipient/subject, both `List-Unsubscribe` headers, all three
body links absolute; the unsubscribe GET left `user_notification_settings` untouched, the POST wrote
the sparse override, and a **fresh unread notification of the unsubscribed type sat through two
drain cycles with no mail sent while the in-app notification remained** — `psql`-confirmed at each
step.

**Still open: tracker F4 / decision row 8.** Provider, sending domain, and SPF/DKIM/DMARC are
untouched by this WU. Mail is built and locally verifiable; it cannot reach a real inbox until
Phase 7.

## WU-NotificationCorrectness Stage note (2026-10-12) — F41/F42/F43 L1/L2 stay Stage 5

**No cell flips.** Defects beneath sound cells: worksheet D19 (answered 2026-08-07) and the service
audit's §2.8 items WU-InertFeatures did not own. Rule text: `layer2-services.md` §"Notification
Generation" → "Filtering semantics" and §"Email fan-out".

- **F43 L1 — D19, sparse per field.** Migration `WU_NotificationCorrectness_SparseSettingFields`:
  `email_enabled` and `collapsed` are nullable. Up canonicalizes existing rows (a value equal to its
  type default → NULL; an all-NULL row → deleted). Down backfills the defaults before NOT NULL
  returns. Both directions were run against a populated clone of the dev DB (five rows → the expected
  three, and back). Applied to the dev DB (`20261013030320_…`); it held no settings rows.
- **F43 L2 — the write path.** `NotificationSettingUpsert` owns the rule. Each field equal to its
  default is stored NULL; the row is deleted only when both are NULL; one-click unsubscribe writes
  `email_enabled` alone and never reads `collapsed`. Under D23's "a visible 500 is never a posture",
  both writers are one `INSERT … ON CONFLICT DO UPDATE` (of their own columns). Two concurrent
  unsubscribe POSTs used to 500 on the primary key. This follows from D23's promulgated test, not a
  new owner answer. The wire contract is unchanged: no UI, endpoint or client change.
- **F42/F43 L2 — the reads.** Three per-field coalescing sites in the explicit
  `s != null && s.X != null ? s.X.Value : default` shape (the feed's `Collapsed`, `GetSettingsAsync`,
  the flusher's eligibility). `NotificationSettingDto.IsDefault` became `EmailIsDefault` /
  `CollapsedIsDefault` (retired term registered in `check-doc-hygiene.ps1`). Nothing renders them.
- **F41 L2 — the email side-channel.**
  - `SmtpMailTransport` classifies send failures: per-message only for a 5xx while still connected
    (`IsPerMessageFailure`), or a compose failure; a 4xx to `RCPT TO` while connected is a deferral
    (`IsDeferral`, added by the review fixes below). Everything else throws
    `MailBatchInterruptedException(ProcessedCount)`. A mid-batch disconnect used to drop the rest of
    the batch for good; a whole-batch restore would have re-sent the head.
  - The flusher restores only the unsent tail. Cancellation restores too and rethrows the OCE.
  - `DrainAllAsync` drains to empty within the worker's 15 s `ShutdownDrainBudget`; a single final
    flush moved at most 200.
  - Identity's single `SendAsync` unwraps the abort to its inner exception.
- **F41 L2 — dedup.** Accepted as an advisory policy under D23's test (doc only, no index).
- **F41 L2 — a deleted Spotlight sponsor (found during the build).** `SpotlightGoLiveSweeper` passed
  `SponsoringUserId ?? 0` as the source. The source FK refused 0, so the placement was never stamped,
  re-failed every minute for its window, and nobody was notified. It now passes the null
  (`audit/Spotlight.md` F55).
- **F42 L2 — order.** `OldestUnreadFirst` ends in `NotificationId`. Fan-out rows tie on
  `DateCreated`, and offset paging skipped or repeated them. No DDL: `EXPLAIN` shows an Incremental Sort
  over the index prefix (`layer6-indexes.md`). The other paged reads: tracker **D13**.
- **F42 L3 — presenter copy.** Arms for 90–92 (routed here by WU-InertFeatures) and 100. All four used
  to read "You have a new notification" in the bell, the inbox and the email's lead line (its subject
  is the type's display name).
  - 90 is actor-free: "You were granted a Community Spotlight slot". As first built it named the
    granting moderator, which answered the unruled decision row 19 by itself; the review fixes below
    took the name out.
  - 91/92 name the sponsor ("Someone" once deleted).
  - 100 is actor-free and, since the review fixes, title-free (tracker D8) *(built by WU-TrackerFixes
    2026-10-13; the title is back — F42's WU-TrackerFixes Stage note)*.
- **Tests.**
  - **Integration** (+18): `NotificationServiceTests` +7 — per-field collapse, three unsubscribe
    shapes, two interleaved upsert races (`InterleavingCommandInterceptor`), the tied-timestamp
    paging.
  - `NotificationEmailFanOutTests` +7 — the D19 freeze regression on type 10 with a flipped default
    (restored in `finally`), its default-on mirror, mid-batch death, mid-batch cancellation, three
    `DrainAllAsync` shapes.
  - `MessagingWriteServiceTests` +3 (F49); `SpotlightServiceTests` +1.
  - **Unit** (+67): `SmtpMailTransportClassificationTests` (15), the presenter's own-arm theory over
    all 47 types plus three copy facts, two `MessagingValidationsTests`.
  - **RazorComponents:** none — no component changed.
  - Every fix-specific test was mutation-checked: each fails with its fix reverted.
- **Browser: not run, and not available in this environment.** The owed checks are tracker **H26**.
  This note first kept F42 L4.5 at 5 as a copy-only change, citing the WU-InertFeatures presenter
  slice, but that slice had a browser pass behind it. The review fixes below flip it to 1.
- **Left open:** tracker **D8** (blog-post titles after the author goes Private) was routed here by
  the WU-AccessGateSweep2 review fixes. It is not built: its one-line fix would blank the post
  author's own type-33 notifications, and an unpublished post is the same leak. Re-scoped in the
  tracker; the review fixes routed it to WU-TrackerFixes, which built it 2026-10-13 (F42's Stage
  note).

## WU-NotificationCorrectness review fixes Stage note (2026-10-13) — F42/F43/F49 L4.5 5→1; F41/F42/F43 L2 stay 5

Three reviews raised 14 findings, 11 distinct after merging duplicates. Each was checked against the
code and the owner's text. All 11 were real and none needed an owner ruling. One detail was wrong:
the presenter's text reaches the email's lead line, not its subject, which is the type's display
name.

- **F41 L2 — one deferred recipient no longer stalls all notification email.** As built, a 4xx to
  `RCPT TO` while connected (452 over quota, 450/451 greylisting) aborted the batch. The restored
  mail led the retried tail, so it aborted every later batch, and nothing capped its retries. Now
  `SmtpMailTransport.IsDeferral` classes it as a **deferral**: the batch goes on, and `SendBatchAsync`
  returns the deferred indices (`MailBatchInterruptedException.DeferredIndices` when the batch then
  stops). The flusher requeues them through `NotificationEmailBuffer.Defer`, which drops a mail at
  its 10th deferral (`MaxDeferrals`), counted on `canalave.email.failed` and logged at Error.
  `DrainAllAsync` stops at a batch that settled nothing, so shutdown does not burn a deferred mail's
  retries. A 421, or a 4xx to `MAIL FROM`/`DATA`, is still a batch abort. On Identity's
  single-message path a deferral is dropped (logged), as before the WU.
- **F43 L2 — unsubscribe for a deleted account.** The signed token lives 365 days and deletion is a
  hard delete. The insert's `user_id` FK turned such a re-POST into a 500, as it did before the WU.
  The insert is now `INSERT … SELECT … WHERE EXISTS` (the `ReadingProgressFlusher` shape), and the
  endpoint answers the same "link no longer valid" page (400) as an expired token.
- **F42 L3 — presenter.** 90 is actor-free. Decision row 19 is unruled, and the arm had answered it
  "yes" in the bell, the inbox and the default-on email. 100 is title-free: its post title resolves
  through `BlogPostDirect`, which does not drop a post its author has since made Private or
  unpublished (tracker D8, now routed to WU-TrackerFixes — built there 2026-10-13, and the title is
  back; F42's WU-TrackerFixes Stage note).
- **Docs.**
  - The two code stamps that carried the template's 2026-09-30 now read 2026-10-12.
  - `audit/Spotlight.md` F55: 21 tests.
  - `audit/Identity.md` F1 L2 gained the transport-contract note.
  - Roadmap row 19 is annotated.
  - The WU-QuickFixes entry moved to `workplan-archive.md`.
  - The SeedTool re-seed ran against a scratch database. Its COPY loaded 444
    `user_notification_settings` rows (99 with a NULL `email_enabled`, 242 with a NULL `collapsed`),
    with no all-NULL row and no stored value equal to its type default.
- **Tests.**
  - **Unit** (+12): `SmtpMailTransportClassificationTests` covers the deferral rule (the 4xx/421/
    sender/data split). Four wire tests drive the real MailKit client against an in-process loopback
    SMTP stub: a 452 is returned while the rest of the batch goes out; a 550 is dropped; a 421 after
    a deferral aborts and reports it; a 452 on the single path is dropped, not thrown.
    `NotificationPresenterTests`: 90 never names the moderator, 100 never names the post, and both
    joined the actor-free theory.
  - **Integration** (+7): `NotificationEmailFanOutTests` +5:
    - a deferred recipient does not hold back the batch and is retried until it recovers;
    - the drop at `MaxDeferrals`;
    - a deferral inside an aborted batch is requeued;
    - the drain stops when only deferred mail is left;
    - the deleted-account unsubscribe POST answers 400.

    `NotificationServiceTests` +2: the feed's and the settings read's per-field `Collapsed` with an
    email-only row against a flipped `DefaultCollapsed` (restored in `finally`), and the deleted-account
    unsubscribe on both branches.
  - **RazorComponents:** none (no component changed).
  - Mutation-checked: with each fix reverted, its tests fail.
- **Browser: not run, none available.** The bell and inbox copy changed (F42), the settings page
  reads the per-field DTO on WASM (F43) and the compose modal has a new 400 (F49). By the campaign's
  precedent (WU-InertFeatures F41, WU-ModerationIntegrity F46/F53), **F42, F43 and F49 L4.5 go 5→1**
  until tracker **H26** runs. (It ran the same day: the browser verification note below.)

## WU-NotificationCorrectness browser verification Stage note (2026-10-13) — F42/F43/F49 L4.5 1→5; tracker H26 closed

Tracker H26's four steps, on the Aspire path (Postgres on 5433, mail to Mailpit) and on both render
phases. The circuit pass shows `_blazor/negotiate` and no `/api` call for the action. The WASM pass
shows no negotiate and the action's `/api` request. `psql` was checked after every write. The server
log has no `fail:`/`crit:` line except the documented stale-antiforgery-cookie line on the first
request. The browser console showed no errors. **No runtime bug was found.**

- **Step 1: presenter copy (F42).**
  - AdminUser granted TestUser a slot on `/mod/spotlight` (`spotlight_slots` 2). It wrote notification
    18: type 90, with the moderator kept as the row's source. TestUser's bell and inbox read "You were
    granted a Community Spotlight slot", with no name, on both phases.
  - TestUser booked AuthorAlpha's "Seed Story: Completed One-Shot (E)" for the open Oct 12–19 block,
    with ReaderGamma's recommendation attached (`community_spotlights` 2). The go-live sweep stamped
    it within a minute and wrote 19 (AuthorAlpha, type 91, source TestUser) and 20 (ReaderGamma,
    type 92). AuthorAlpha read "TestUser put Seed Story: Completed One-Shot (E) in the Community
    Spotlight", and ReaderGamma read the same with "… with your recommendation". Both were checked on
    both phases.
  - AuthorAlpha added a poll to blog post 2, TestUser voted, and AuthorAlpha renamed an option, which
    stamped `last_edited_at`. That stamp was moved back 31 minutes through `psql` to skip the 30-minute
    quiet period. The sweep then wrote 21 (TestUser, type 100). The bell and inbox read "A poll you
    voted on was changed", with no title, on both phases.
  - Mailpit received four mails. Each subject is the type's display name (Spotlight Slot Granted,
    Story Spotlighted, Recommendation Spotlighted, Poll Updated), and each lead line is the bell's
    text. The poll mail links to `/blog/2`.
  - The seeded 90/91 rows from before the WU render with the new arms too.
  - The awardee's `/api/notifications` payload still carries the moderator as `sourceUserName`. That
    is decision row 19's documented status quo, not a defect.
- **Step 2: settings page (F43).**
  - TestUser turned on only Collapsed for New Chapter. The row is `(1, 10, NULL, true)`. After a
    reload on either phase, Email is still checked. Turning Collapsed off again deleted the row. On
    WASM the write is `PUT /api/notifications/settings/10` (204).
  - Turning Email on for Poll Updated (default off) wrote `(1, 100, true, NULL)`. The poll mail in
    step 1 reached TestUser through that row, which exercises the flusher's per-field eligibility.
  - AuthorAlpha's Collapsed on Story Spotlighted wrote `(4, 91, NULL, true)`. `/api/notifications`
    then returned `collapsed: true` for notification 19 and for no other row, which exercises the
    feed's per-field read.
- **Step 3: one-click unsubscribe (F43).**
  - Opening AuthorAlpha's Story Spotlighted link (the GET page) wrote nothing.
  - Its button's POST showed "You're unsubscribed", and the row became `(4, 91, false, true)`, with
    Collapsed unchanged.
  - Two more anonymous POSTs (curl) answered 200 and left the row unchanged. A tampered token
    answered 400.
  - Unsubscribing from TestUser's Poll Updated mail (a default-off type) deleted the
    `(1, 100, true, NULL)` row.
- **Step 4: subject cap (F49).**
  - On `/messages?ComposeForUsername=AuthorAlpha`, a 2,049-character subject was set by script,
    because the input's `maxlength` stops typing past 2,048. On both phases the modal showed "Subject
    must be 2048 characters or fewer."
  - On WASM, `POST /api/messaging/conversations` answered 400. The circuit made no `/api` call.
    `conversations` still had one row.
  - A 2,048-character subject with two trailing spaces went through on the circuit (conversation 2).
    The subject is stored trimmed, at 2,048 characters. `date_created` equals the first message's
    `date_sent`, so one save wrote both.
- **Not browser-observable** (still covered only by the Integration and Unit tests named in the two
  notes above):
  - the mid-batch restore, the shutdown drain and the deferral of a 4xx recipient;
  - the feed tiebreak (the WASM "Oldest unread first" list renders: `order=1`, 200);
  - the deleted-sponsor null source and the deleted-account unsubscribe 400.
- **Comment fix, no behaviour change.** The review fixes had left the summary of the unsubscribe
  pages' HTML shell (`Page`) above `InvalidLinkPage`'s own summary, in
  `NotificationEmailEndpoints.cs`. It is back on `Page`.
- **Tests:** none added, because no runtime defect was found. After the comment fix: Unit 1,169,
  RazorComponents 770 and Integration 1,408, all green, and the four gates pass.
- **Cells:** F42, F43 and F49 L4.5 go 1→5. Tracker H26 is closed. The dev-DB state this pass left is
  listed in the workplan entry.

## WU-UserDeletion Stage note (2026-10-13) — type 62 `GroupAdminAssigned`; F42 L4.5 5→1; F41 L1/L2 stay 5

Owner ruling D13 edge (b): "the promoted user is notified" (the F41 Settled bullet below, in
Feature 41's Settled list).
- **F41 L1.** Migration `WU_UserDeletion_GroupAdminAssigned` seeds `notification_types` row 62
  (`GroupAdminAssigned`, "Group Admin Assigned", category Groups, `DefaultEmailEnabled = true`,
  `DefaultCollapsed = false`). Its `Down()` deletes the type's notifications and settings rows first
  (both FKs are RESTRICT). Rehearsed Up, Down and Up on a dev-DB clone holding a type-62 notification
  and setting; applied to the server-only dev DB.
- **F41 L2.** `INotificationWriteService.NotifyGroupAdminAssignedAsync(recipientUserId, groupId)` — no
  moderator parameter (D5's type-level shape, applied by default — decision row 23), one
  `CreateCoreAsync` with a null source and the group as
  `RelatedEntityId` (D16); ordinary cross-existing dedup applies. Client twin throws
  `NotExposedOverHttp`; the RazorComponents fake is a no-op. `NotificationEnricher.KindFor` maps 62 to
  the existing `Group` kind (group name, `/group/{id}`, `GroupAudience` lifted). The producer is
  `ServerModerationWriteService.AssignGroupAdminAsync`, best-effort after commit, skipping a moderator
  who assigned themselves. Tier: Integration `GroupAdminRescueTests` (one row to the promoted member,
  null source, the group id; none on a self-assignment).
- **F42 L2/L3.** `NotificationPresenter`'s Groups arm for 62 is actor-free: "You were made an admin of
  {group}" / "You were made a group admin". Tier: Unit `NotificationPresenterTests` (both texts, the
  moderator's name never appears even when a source is present; 62 joins the actor-free theory and
  the every-type theories through `CategoryFor`).
- **F42 L4.5 5→1.** The new copy has not been read in the bell, the inbox or the email; tracker
  **H29**. It went back to 5 at the browser pass the same day (the browser verification note below).

**WU-UserDeletion browser verification (2026-10-13) — F42 L4.5 1→5.** The method, accounts and logs
are in `audit/Identity.md` F52's browser verification Stage note, and the assignments that produced
these notifications are in `audit/Groups.md` F40's.
- **Bell and inbox, server-only path.** ReaderGamma was promoted on group 5 over WASM. Her bell and
  `/notifications` read "You were made an admin of E2E Rescue: deletion route", with the group icon
  and no moderator name, on WASM (`GET /api/notifications`). Clicking the bell item opened
  `/group/5` and marked it read. TestUser was promoted on group 6 on the circuit. His inbox and bell
  read "You were made an admin of E2E Rescue: leave route" on the circuit, and the inbox item opened
  `/group/6`. `psql`: one type-62 row per promotion, with `source_user_id` NULL and the group id as
  `related_entity_id`.
- **Email, Aspire path.** On the Aspire DB (migrations applied at start), AuthorBeta founded "E2E
  Rescue: email check" (group 4). TestUser joined, AuthorBeta left through the sole-admin dialog, and
  ModUser assigned TestUser. Within one flush interval Mailpit held one message to
  testuser@example.com, subject "Group Admin Assigned". Its lead line was "You were made an admin of
  E2E Rescue: email check" and it linked `http://localhost:5028/group/4`. The body never names
  ModUser. The unsubscribe and settings links were present.
- No bug was found.

**WU-UserDeletion review fixes (2026-10-13) — no flip.**
- **F41 L2 tier claim corrected.** The note above credited `GroupAdminRescueTests` with the enrichment,
  but it read only the raw `notifications` row, so dropping `KindFor`'s 62 arm left every test green
  (62 fell to `None`: no title, no link). New Integration test
  `GroupAdminRescueTests.TheNotification_ReadsBackWithTheGroupsNameAndLink_EvenForAnMAudienceGroup`
  reads the row through `INotificationReadService` as the promoted member, with mature content off on
  an M-audience group: the group's name and `/group/{id}`, no source. It fails with the arm removed, and
  with the `Group` kind's `GroupAudience` bypass removed.
- **Attribution corrected.** The null source had been credited to D5 "by its own wording"; D5 covers the
  sanctioned or reporting user in the 70–82 band. It is now recorded as the WU spec's default under
  D5's threat model and routed to the owner as roadmap decision row **23** (the Settled bullet below,
  `layer2-services.md`, the interface doc and the code comments say so). Behaviour unchanged.

## WU-PrivateMessageEmail Stage note (2026-10-13) — F41 L2 and F43 L3/L3.5/L5 stay 5; F43 L4.5 5→1 and 1→5 at its browser pass

Owner ruling D18 built (Shared Context's D18 note above has the settled rules; private-message email
itself is `audit/Messaging.md` F49).
- **F41 L2 — the pipeline is shared now.** The notification buffer and worker became
  `Server/Email/OutboundEmailBuffer`/`OutboundEmailWorker` (`git mv`, renamed), carrying
  `OutboundEmailWork(Kind, Id)` items. The old flusher split in two: the send and every restore rule
  (WU-NotificationCorrectness's, unchanged in substance — compose failure, `ProcessedCount`,
  deferrals and `MaxDeferrals`, cancellation, `DrainAllAsync` and the 15 s shutdown budget) moved to
  the domain-free `Server/Email/OutboundEmailFlusher`; the eligibility query and composition stayed
  here as `NotificationEmailComposer` (an `IOutboundEmailComposer`), verbatim, D19's per-field
  `EmailEnabled` expression included. New flusher rules: per-kind routing with one send for every
  kind, a duplicate composer refused at startup, an unroutable kind dropped with an Error, after-send
  hooks for taken mails only (notifications register none). Create-core enqueues
  `(Notification, id)`. The class doc's reference to a test class that never existed now names
  `NotificationEmailFanOutTests.Email_IsStillSentToRestrictedAccounts`.
- **F43 — the unsubscribe page shell is shared and its link is fixed.** The HTML shell moved to
  `Server/Email/UnsubscribeLandingPage`; its settings link said "Manage all notification settings"
  and pointed at `/settings`, which has none. It now reads "Manage your email settings" and goes to
  `/notifications/settings`.
- **F43 L3/L3.5/L5 — the "Private messages" section** (D18's standing rider: every outbound
  non-transactional email appears on this page). It reads and writes Messaging's own value through
  `IMessagingWriteService` (`[PersistentState] bool? NewMessageEmailEnabled`, restore-or-fetch), shows
  only an Email toggle, writes optimistically one write at a time, and rolls back with an inline
  `ErrorAlert` on a refusal. The existing category toggles still have no catch; not changed here
  (tracker **H17**, which already listed them; filed as H36 and folded into H17 by the review fixes).
- **F43 L4.5 5→1** — no browser was available; tracker **H35**.

**Verified:** `dotnet build` green; `dotnet test` green — Unit 1,304, RazorComponents 859, Integration
1,610. Tiers: **Integration** `NotificationEmailFanOutTests` (unchanged in substance, helpers renamed;
all green) and `PrivateMessageEmailTests.ANotificationAndAMessage_LeaveInOneFlush`; **Unit**
`OutboundEmailFlusherTests` (9) and `OutboundEmailBufferTests` (8); **RazorComponents**
`NotificationSettingsPageTests` (7 cases, the page's first bUnit class — WU33's deferral below is
paid). Mutation-checked: skipping the aborted batch's after-send call, or passing it a cancellable
token, fails `OutboundEmailFlusherTests`; dropping the section's rollback or its busy-disable fails
`NotificationSettingsPageTests`.

**Review fixes (2026-10-13) — no flip on F41/F43.** Two test gaps closed. The flusher's after-send
hooks get indices into each composer's own mails, and only the private-message stamp uses them; no test
had a hook group that was not first in the batch. `OutboundEmailFlusherTests` now asserts both groups'
indices in the mixed-kind test and adds a deferral inside a later group (Unit, 10 cases). The landing
page's settings link, a user-visible fix with no test, is pinned by Unit `UnsubscribeLandingPageTests`
(the link equals `NotificationSettingsPage`'s `@page` route; the title is encoded) and by both
Integration unsubscribe GETs (`NotificationEmailFanOutTests.UnsubscribeGet_DoesNotChangeAnything`,
`PrivateMessageEmailTests.TheUnsubscribeLink_TurnsMessageEmailOff_AndOnlyThePostDoesIt`).
Mutation-checked: a batch-position index fails two flusher tests; the old `/settings` link fails the
Unit test. `dotnet test` green — Unit 1,308, RazorComponents 860, Integration 1,614.

**Browser verification (2026-10-13) — F43 L4.5 1→5; no flip on F41; tracker H35 closed.** Aspire
path, as ReaderGamma, with `psql` after every write; the mail half and the method are in
`audit/Messaging.md` F49's browser verification.
- **F43, the Private messages section (step 1).**
  - **Circuit** (`_blazor/negotiate`, no `/api` call): the section comes after the last category
    ("Your Reports"). It has one checkbox, ticked, labelled "Email me about new private messages",
    and no Collapsed column. Unticking set `email_on_new_message` false, a reload showed it unticked,
    and ticking set it back.
  - **WASM:** a full load hydrated from the persisted value with no `GET`. The untick sent
    `PUT /api/messaging/email-preference?enabled=false`, and a reload kept it. A client-side
    navigation into the page made the `GET`.
  - **A refused write:** signed out under the running WASM app, the toggle got a 401. The box rolled
    back to ticked under "Your session has expired — sign in again to continue." with a Sign in link,
    and the database was unchanged. Both endpoints answer 401 to an anonymous caller.
  - **A same-URL navigation** after a toggle kept the new value, so the page does not hit tracker
    **D20**'s snapshot re-supply (it has no route parameters).
- **F43, the landing page's settings link (step 7).** An Account Warning email's unsubscribe page
  reads "Manage your email settings" and links `/notifications/settings`. Followed in the browser,
  the link opened that page. The message-email pages carry the same link.
- **F41, the shared pipeline.** Notification and private-message mails left through the one worker:
  the Account Warning and Account Suspended mails in the moderator check, beside the PM mails. A
  dashboard Stop of the `web` resource ran the shutdown drain to completion. `stop-aspire.ps1` cannot
  show that: its proxies die first (`run-server/SKILL.md`).

## Feature 41 — Notification Generation

**Stages (updated 2026-10-14, WU-D21Confidentiality — story-family targets split by recipient in the shared enricher, so the notification mail drops a hidden story's title too and the 50/52 draft leaks close at enrichment, beneath L2, no flip — Stage note at Feature 42's top; 2026-10-13, WU-PrivateMessageEmail — the email pipeline generalized to `(kind, id)` and moved to `Server/Email/`, the flusher split into the shared `OutboundEmailFlusher` and this cluster's `NotificationEmailComposer`, beneath L2, no flip — Stage note above Feature 41; its review fixes Unit-test the per-group after-send indices and the landing page's link, no flip; its browser pass saw notification and private-message mail leave through the one worker and the shutdown drain finish, no flip; WU-UserDeletion — type 62 seeded and produced, beneath L1/L2, no flip; its review fixes Integration-test the type's enrichment and route its attribution to decision row 23 — Stage notes above this feature; 2026-09-30, WU-InertFeatures, its review fixes and its browser pass; WU-NotificationCorrectness):** L1, L2, L4.5, L6 = 5 (the
D4/D5/D16/D17 rebuild and the new-chapter fan-out landed beneath them — Stage notes at the end of this feature). **L4.5**
went 5→1 when the WU shipped with no browser available, and back to 5 when its browser pass saw the new-chapter
notification, the report receipt and the re-anchored group notifications in the bell and the inbox (tracker **H14**
closed). L3/L3.5/L4/L5 = N/A. Trackers **B20** and **B21** closed. **L2 stays 5 under WU-NotificationCorrectness
(2026-10-12):** the email flusher restores only unsent mail and drains to empty on shutdown, dedup is documented as
an advisory policy, and a deleted Spotlight sponsor is a null source. **Its review fixes (2026-10-13)** made a 4xx
to one recipient a deferral (requeued, dropped at the 10th) instead of a batch abort that stalled every later batch.
Its browser pass (2026-10-13) saw the 90–92 and 100 notifications generated end to end and their mail reach
Mailpit; F41's cells stay as they are (Stage notes above Feature 41).

- **L1 — Stage 5 (`related_entity_id` widened to `bigint`, WU-InertFeatures 2026-09-30).** `Notification` + the fully-seeded type/category tables. Sound. **L6 — Stage 5
  (WU-L6, 2026-07-07)** — `ix_notifications_recipient_read_date (recipient_user_id, is_read,
  date_created)` built in `L6_IndexBatch` (supersedes the recipient FK index); measured at 20k
  seeded notifications: unread count −47%; newest-first feed neutral by design (per-user residual
  sort, bounded by the 60-day cleanup worker). Detail: `layer6-indexes.md`.

- **L2 — Stage 2 → 5 (WU22; extended WU-NotifEmail 2026-07-31 — create-core now also enqueues the
  email fan-out, stage unchanged; the fan-out rides the shared outbound pipeline as
  `(Notification, id)` items and its eligibility query lives in `NotificationEmailComposer`,
  WU-PrivateMessageEmail 2026-10-13, owner ruling D18 — stage unchanged, Integration- and Unit-tested; a Private author's profile post fans out to nobody since the
  WU-AccessGateSweep2 review fixes, 2026-09-30 — see that slice below, stage unchanged; nullable
  source, de-identified moderation band, report-id anchors, `GroupStory` anchor, hidden favoriters in
  15 and the new-chapter fan-out, WU-InertFeatures 2026-09-30 — stage unchanged, Stage note at the end
  of this feature; convention sample and fan-out list re-synced to the code, WU-DocCorrections
  2026-10-13 — docs only, stage unchanged, note directly below; the notification mail shares the
  enricher's story-family split (a hidden story's title leaves members' and followers' mail, and a
  lineage request from a draft source (50) or a credit on a draft (52) no longer names it), WU-D21Confidentiality
  2026-10-14, tracker D21 — stage unchanged, Integration-tested, Stage note at Feature 42's top).**
  *WU-DocCorrections note (2026-10-13; service audit §4 rows 3/4):* `layer2-services.md`
  §"Notification Generation"'s sample passed `NotifyNewFollowerAsync(ActorId, targetUserId)`, the
  reverse of the real `(recipientUserId, followerUserId)`, so code copied from it would notify the
  follower about themselves; it also injected the read context directly and logged the swallow at
  Error. It now mirrors `ServerFollowingWriteService.FollowAsync` (factory ctor, `LogWarning`). The
  "semantic methods land incrementally" paragraph now lists the fan-outs that exist and where each one's
  recipients come from. Test tier: none owed (doc only); the real signature is exercised by the
  Integration follow notification tests. Settled constraints (do not revisit):
  - **Mechanism:** direct injected call — `INotificationWriteService` injected into feature write
    services; called via a semantic per-event method after the primary `SaveChangesAsync` (best-effort
    post-commit, `try/catch`-with-log). See `layer2-services.md` "Notification Generation"
    (mechanism + filtering semantics).
  - **API:** semantic per-event methods only (`NotifyNewFollowerAsync`, `NotifyNewChapterAsync`, …);
    no public generic `CreateAsync` escape hatch. Methods funnel through one private create-core
    (drop-self, dedup, bulk-insert, single `SaveChangesAsync`).
  - **In-app filtering:** always-on — the create-core never gates on `UserNotificationSetting`. The only
    in-app gate is relationship-level: **author-follow** fan-outs (11/12/13) check
    `FollowedUser.ReceiveAlerts`; **story-relationship** fan-outs (10/14/15/16) have no per-row opt-in —
    presence of the `UserStoryInteraction` flag is the signal. (Corrected 2026-09-30, WU-InertFeatures:
    this bullet used to say every follow-alert method checks `ReceiveAlerts`, which has never been true
    of a story follow.)
  - **Settled by owner rulings D4/D5/D16/D17 (answered 2026-08-04/07, consumed WU-InertFeatures
    2026-09-30 — do not revisit; rules in `layer2-services.md` §"Notification Generation",
    §"Comment & blog-post semantic methods", §"Polymorphic RelatedEntityId"):**
    - **D4 — null source = no actor.** `CreateCoreAsync(int? sourceUserId, …)`; drop-self only when
      `sourceUserId is int s && recipient == s`. `ReportReceived` (80) is restored by deleting the
      parameter that broke it. Guardrail: the moderator-initiated account action never sends 80/81 —
      and, generalized at the review fixes (2026-09-30), every band call site skips the acting
      moderator as a recipient (`audit/Moderation.md`'s cluster Settled note).
      `RelatedEntityId` stays non-nullable with 0 = none. Dedup: 70/80/81/82 carry the report id;
      72/73/74/76/77/90 are exempt from cross-existing dedup. Two nulls (deleted actor vs. no actor)
      are disambiguated by type at display time. Prerequisite widen `related_entity_id` int→bigint
      lands on the same migration.
    - **D5 — the moderation band 70–82 is null-sourced, good news included**, and so is
      `TagUpdateSuggestion` (26 — D5's routed sub-edge, taken per the owner's recommendation). No
      band method takes a moderator id. `SpotlightSlotGranted` (90) is outside the stated band — its
      source stays (unruled — roadmap decision row 19).
    - **`GroupAdminAssigned` (62), settled for WU-UserDeletion 2026-10-13 (owner ruling D13: "the
      promoted user is notified") — except its attribution, which is a default.** A Groups-band type
      a moderator act produces. `NotifyGroupAdminAssignedAsync(recipientUserId, groupId)` has no
      moderator parameter and writes a null source; `RelatedEntityId` = the group (D16), resolved by
      the existing `Group` enricher kind; the presenter arm is actor-free. *The null source is not
      D5's ruling:* D5 covers the moderator's identity "to the sanctioned or reporting user" in the
      70–82 band, and the promoted member is neither. It is the WU spec's pick under D5's threat
      model, open to the owner as roadmap decision row **23** (sibling of row 19); corrected by the
      WU's review fixes, which had cited D5 "by its own wording". Seed row: category Groups,
      `DefaultEmailEnabled = true` (the user did not ask for this and must be able to find out),
      `DefaultCollapsed = false`. The acting moderator assigning themselves gets no notification (the
      generalized guardrail above).
    - **D16 — one anchor per event.** 60 and 25 carry the `GroupStory` row's id (new
      `RelatedEntityKind.GroupStory`); a second id column is never added; the re-point backlog is
      recorded as a conformance list (tracker B23), not built. Riders: the story's author is excluded
      from the 60 fan-out (gets 25 only); the notify block moves inside `if (!alreadyAdded)`; an
      authorless story still notifies members. *(Split by recipient, tracker D21 /
      WU-D21Confidentiality 2026-10-14: both still carry the `GroupStory` row's id, but 25 resolves
      through `OwnGroupStory`, as stored, and 60's story title drops while the story is unpublished or
      taken down; Settled note "story-family targets by recipient" in the Shared Context.)*
    - **D17 — hidden favorites are favoriters on the personal plane.** Type 15 recipients are
      `IsFavorite || IsHiddenFavorite`; the type-20 mirror (author-plane — a hidden favorite must not
      fire it) is recorded before that producer exists.
    - **New-chapter fan-out (10):** anchored on `Chapter.FirstPublishedDate` (D2), first publication
      only (D1 rider), recipients = `IsFollowed`. Suppressing it while the story is not publicly
      published is a **default, not a ruling** — `roadmap.md` row 17.
  - **Transactional posture:** best-effort post-commit — see above.
  - Open/incremental part: the *set* of semantic methods grows as triggering features land. WU22 delivers
    `NotifyNewFollowerAsync` / `NotifyNewVouchAsync` (single-recipient, no fan-out) and wires them into
    the `// TODO(WU22)` seams in `ServerFollowingWriteService`. Fan-out methods land co-delivered with
    their triggering work-units.
  - **L3/L3.5/L4/L5 — N/A** (generation is server-side write path, no UI).

- **WU22 Stage-5 note (2026-06-23):** `Core/Notifications/` cluster minted — `NotificationDto`,
  `NotificationSettingDto`, `INotificationReadService`, `INotificationWriteService`; `Server/Notifications/`
  cluster minted — `ServerNotificationReadService`, `ServerNotificationWriteService` (inherits read service,
  private `CreateCoreAsync` owns drop-self + dedup invariants). DI registered in `Program.cs` (both
  interfaces map to `ServerNotificationWriteService`). The `// TODO(WU22)` seams in
  `ServerFollowingWriteService.FollowAsync` / `VouchAsync` are wired: best-effort post-commit calls to
  `NotifyNewFollowerAsync` / `NotifyNewVouchAsync` in `try/catch`-with-`ILogger`. **Test tier:
  Integration** (`Tests.Integration/NotificationServiceTests.cs`, Testcontainers Postgres): generation
  correctness (right type/source/related); drop-self; dedup (second call skipped while unread, allowed
  after mark-as-read); `GetUnreadCountAsync`, `GetNotificationsAsync` (order, effective Collapsed);
  `MarkAsReadAsync` (own only — cannot mark another user's); `MarkAllAsReadAsync`;
  `GetSettingsAsync` (defaults when no row); `SetSettingAsync` (upsert + sparse delete when back to
  defaults); end-to-end `FollowAsync` → notification row exists. Mutation sanity: drop-self line
  commented out → `NotifyNewFollowerAsync_DropsSelf_WhenRecipientEqualsSource` fails; reverted.
  **Deferred semantic methods (co-delivered with triggering work-units):** `NotifyNewChapterAsync`
  (fan-out to the story's `IsFollowed` interactions — not `ReceiveAlerts`, corrected 2026-09-30 — built
  by WU-InertFeatures); `NotifyNewRecommendationAsync` /
  etc. (with WU19/20/29). The create-core and DAG pattern are built now; each deferred method is a
  thin wrapper addition. The comment + profile-blog wrappers landed 2026-07-25 — see the WU-B2 slice
  below. `NotifyStoryAcknowledgedAsync` (type 52, `NewStoryAcknowledgement`) landed 2026-07-31 — see
  the WU-StatBadgeProducers slice below.
  **WU-Spotlight slice (2026-07-12):** three new types 90–92 (`SpotlightSlotGranted` /
  `StorySpotlighted` / `RecommendationSpotlighted` — categories SiteNews / YourStories /
  YourRecommendations, email-default on) + thin semantic wrappers + `KindFor` Story branches for
  91/92 (90 = None; the redemption page is a fixed route, not an entity link). 91/92 are fired by
  a worker (`SpotlightGoLiveWorker`) at window-open, not by a write path — first worker-sourced
  notifications; the create-core's drop-self correctly suppresses 92 when the sponsor attached
  their own recommendation (browser-verified against the dev DB). Detail: `audit/Spotlight.md`.
  **WU-B2 slice (2026-07-25) — comment & profile-blog wrappers (settled decisions):** five new semantic
  methods wire the MA-506/MA-709 seams: `NotifyNewStoryCommentAsync` (24, relatedId = chapterId),
  `NotifyNewBlogCommentAsync` (33, relatedId = blogPostId — TPT-root owner resolution, covers comments
  on both profile and group posts), `NotifyNewProfileCommentAsync` (31, relatedId = profileOwnerId),
  `NotifyCommentReplyAsync` (34, relatedId = *context* id — `CommentId` is `long`, `RelatedEntityId`
  is `int`, so replies cannot reference the comment itself; accepted dedup consequence recorded in
  `layer2-services.md`), `NotifyNewProfileBlogPostAsync` (fan-out 13/14/15/16, disjoint by precedence
  13 > 14 > 15 > 16). Settled: group comments notify **replies only** (no single comment-owner, no
  membership flag); profile-blog fan-out fires on the **publish transition** only (drafts silent;
  republish re-notifies — unread-dedup absorbs bursts); reply/container-suppress rule (owner who is
  also parent author gets only `CommentReply`); null-skip for SET-NULL'd authors; enrichment adds the
  `BlogPostDirect` kind (`/blog/{id}`, TPT-root, `IsTakenDown` filter deliberately active) and remaps
  `PollUpdated` onto it (its group-only lookup left profile-post poll notifications title-less);
  `NewStoryComment` → Chapter kind; `NewCommentOnYourProfile` → User kind; `CommentReply` → None
  (non-navigating, known minor UX gap). Convention detail: `layer2-services.md` §"Comment & blog-post
  semantic methods".
  **Verified (2026-07-25):** `dotnet test` full suite green (758 Unit + 567 RazorComponents + 832
  Integration = 2157/2157). Covering tier: **Integration** —
  `CommentAndBlogNotificationTests.cs` (22 tests: all four seams incl. drop-self, container-suppress,
  null-skip pins; fan-out precedence-dedup incl. the multi-qualifier-gets-exactly-one pin;
  draft-silent / no-transition / republish-after-read behaviors; enrichment URL pins `/blog/{id}` +
  chapter deep-link) + **Unit** — `NotificationPresenterTests` +5 (new `NewCommentOnBlog` arm,
  reworded 14/15/16). L6 note: the fan-out's three story-centric USI queries feed the existing
  "Rejected-vs-live conflict" — see `design/L6-reconciliation-matrix.md` WU-B2 addendum (measure-first,
  low-frequency write path).
  **WU-StatBadgeProducers slice (2026-07-31):** `NotifyStoryAcknowledgedAsync` (type 52,
  `NewStoryAcknowledgement`) was the only missing piece — the enum member, seed row,
  `NotificationPresenter` arm ("{actor} acknowledged {target}"), and `KindFor` → Story mapping were
  all already in place before this work-unit. Fires from
  `ServerStoryAcknowledgmentWriteService.RequestAcknowledgmentAsync` (best-effort post-commit,
  `try/catch`-with-log, matching `NotifyStoryLineageRequestedAsync`'s shape). Not sent on self-credit
  — rejected outright by the write service before this would fire, so no drop-self case exists in
  practice for this type. Verified: `RequestAcknowledgment_Author_CreatesPendingAndNotifiesRecipient`
  in `StoryAcknowledgmentServiceTests` (asserts the notification row); browser-verified end to end
  (credit → notification renders for the recipient → accept → badge).
  **WU-AccessGateSweep2 review-fixes slice (2026-09-30) — a Private author's profile post notifies
  nobody. No cell flips — F41 L2 stays Stage 5.** WU-AccessGateSweep2 made a profile blog post
  profile-tab data (exactly as visible as its author's profile — `audit/AccessGate.md` Settled), but
  `NotifyNewProfileBlogPostAsync` resolved recipients with no look at the author's
  `ProfileVisibility`: a post published by an already-Private author fanned out to their alert
  followers and to the linked story's followers/favoriters/read-it-later users — none of whom can
  open it — and `NotificationEnricher`'s `BlogPostDirect` branch showed them its title. The method now
  reads the author's setting first and returns when it is `Private`. `UsersOnly` needs no check
  (every recipient is signed in). This is the one recipient-side visibility rule in the fan-outs: the
  "recipients are ground truth, Personal plane" posture covers rating/audience (Class B), and profile
  privacy is Class A (`layer2-services.md` §"Comment & blog-post semantic methods"). **Residual,
  routed:** notifications minted while the author was still visible keep resolving the post's title
  through the recipient-agnostic enricher after the author goes Private — tracker **D8**, owner
  WU-NotificationCorrectness. Verified: Integration — `CommentAndBlogNotificationTests`
  `PublishTransition_AuthorProfileVisibility_GatesTheWholeFanOut` (Private → no type-13 or type-15
  row; UsersOnly → both; the Private case fails with the early return removed). `dotnet test` green —
  Unit 1,022, RazorComponents 703, Integration 1,180. **Browser-verified 2026-09-30** (WU-AccessGateSweep2
  browser pass): AuthorAlpha's publish while Public wrote a type-13 row for each alert follower
  (TestUser, ReaderGamma). After AuthorAlpha went Private through the settings form, publishing a
  second post from its edit page wrote none.

### Feature 41 L1/L2/L4.5 — WU-InertFeatures Stage note (2026-09-30): owner rulings D4/D5/D16/D17 + the new-chapter fan-out (trackers B20, B21 closed)

**What changed.**
- **L1 (migration `WU_InertFeatures`):** `notifications.related_entity_id` integer → bigint (in-place
  widen, values preserved; `Notification.RelatedEntityId`/`NotificationDto.RelatedEntityId` are `long`,
  0 documented as the "no related entity" sentinel). Up and Down were both run against a populated
  clone of the dev DB (2026-09-30): column type flipped and back, row count and id sum unchanged.
- **D4 — create-core:** `CreateCoreAsync(type, int? sourceUserId, (int, long)[] targets)`; drop-self only
  when there is an actor; the `(type, source, related, unread)` dedup matches NULL to NULL; a
  `CrossExistingDedupExempt` set (72/73/74/76/77/90) skips cross-existing dedup. `ReportReceived` (80)
  is restored — `SubmitReportAsync` now sends it null-sourced with the report id (B21: the old call
  passed the reporter as their own source and drop-self deleted every receipt). 70/81/82 carry the
  report id. Guardrail comment + test: the moderator-initiated account action sends no 80/81.
- **D5 — de-identification:** every moderator-id parameter is deleted from the interface (70–82 and
  26); all those wrappers pass `sourceUserId: null`; callers updated in `ServerModerationWriteService`
  (approve/reject now only gate on the role), `ServerExternalVerificationWriteService` (the account
  tier still records `ReviewedByModeratorUserId`) and `ServerFanonWriteService`. 90 keeps its source
  (outside the band, unruled). `SeedGraph`'s null-sourced type-26 rows now match the live shape.
- **D16 — group fan-out:** `NotifyNewGroupStoryAsync(groupId, groupStoryId, int? storyAuthorId,
  sourceUserId)`; 60 and 25 carry the `GroupStory` row id; the author is excluded from the 60 fan-out.
  Caller rider fixes in `ServerGroupWriteService.AddStoryAsync` (F39's Stage note).
- **D17:** type 15's favoriter set is `IsFavorite || IsHiddenFavorite`; read paths untouched.
  `SeedGraph`'s type-20 seed now skips hidden favorites (the mirror rule).
- **New-chapter fan-out (B20):** `NotifyNewChapterAsync(storyId, chapterId, authorId)` → type 10 to the
  story's `IsFollowed` interactions, `RelatedEntityId = chapterId`, source = author; fired by
  `SetPublishedAsync` only on the `FirstPublishedDate` stamp and only while the story is publicly
  published (F6's Stage note in `Chapters.md`; default → roadmap row 17).
- Comments corrected: the class docs of `INotificationWriteService`/`ServerNotificationWriteService`
  (no read-service composition; actor-free methods), the create-core doc (its dedup rationale is true
  now — report ids), the reply/int rationale in `ServerCommentWriteService` and the interface, the
  `KindFor` stub comments, `NotificationEndpoints`' moderator-id mention.

**How verified.** Integration — `NotificationServiceTests` (+5: a null-sourced row is delivered;
null-source dedup collapses an identical unread pair but not a different report id; two unread
warnings are two rows; a 3 000 000 000 report id round-trips through the feed; a Story-kind row with an
out-of-int id resolves to no target without throwing), `ModerationServiceTests` (the two misnamed
"NotifyReportReceivedAsync_*" tests renamed to what they test and asserting null sources; +7: the
restored receipt carries the report id with no source; two reports → two receipts; resolve paths carry
report ids, no source; two removals → two 70s; warn twice → two 72s; the D4 guardrail; a sweep of every
70–82 row across all moderator paths finds no source), `ExternalVerificationTests` / `FanonPipelineTests`
(76–79 and 26 assert `SourceUserId == null`), `GroupServiceTests` (+5, F39), `CommentAndBlogNotificationTests`
(+3, D17's three cases), new `NewChapterNotificationTests` (7). Unit — `NotificationPresenterTests` (F42).
Mutation-checked: removing the exemption, the author exclusion or the D17 predicate each fails its tests.
**Browser: not run** — L4.5 → 1, tracker H14. Totals in the workplan entry.

### Feature 41 L2 — WU-InertFeatures review fixes (2026-09-30)

**No cell flips.**
- **A moderator could be notified of their own act — fixed at the call sites.** D5's null
  source drops nobody, so every caller in the band now skips the acting moderator explicitly
  (narrative and tests: `audit/Moderation.md` F47/F48/F53 review-fixes notes; type 26 in
  `audit/Tags.md`). The interface's class doc says so. Rule: `layer2-services.md` §"Notification
  Generation" → "The general rule for the de-identified band".
- **Type 90's moderator attribution has an owner-facing home:** roadmap decision row 19 (status quo
  default: source kept). The three "unruled" mentions point at it.
- **Rows written before the migration — checked, nothing to fix.** The migration re-interprets 60/25's
  `related_entity_id` (a group id before, a `GroupStory` id now) and leaves older 70–82/26 rows
  carrying a moderator source. No data statement was added, because no such row exists. A `psql` count
  on the dev DB (2026-09-30) found 0 rows of types 25/26/60/70–82: it was reset after the migration, and
  the seed writes neither shape. No production database exists. A clone taken before WU-InertFeatures
  would show stale anchors until it is reset.
- **Doc:** `layer2-services.md`'s enricher query bound reads "max 8" (one per non-`None` kind; the
  WU added `GroupStory`).
- **How verified:** Integration — the band tests named in the Moderation, Tags and verification notes;
  the full Integration tier is green (totals in the workplan entry).

### Feature 41 L4.5 — WU-InertFeatures browser verification (2026-09-30): the new and re-anchored notifications in the bell and the inbox; L4.5 1→5

**Cells:** L4.5 1→5 (tracker **H14** closed). No bug found. Aspire path, so email reached Mailpit;
`psql` after every write. Setup and phase method: `audit/Recommendations.md` F30's browser note.

- **New chapter (type 10).**
  - ReaderGamma followed story 2 from its panel; TestUser follows it in the seed. AuthorAlpha then
    published the never-published Chapter 3 from the chapter editor.
  - `first_published_date` was stamped, and two type-10 rows were written (source AuthorAlpha,
    `related_entity_id` = the chapter id).
  - Mailpit received a "New Chapter" email for each follower. Each reads "New chapter of {story}:
    {chapter}" and links to `/story/2/3`.
  - The bell showed the same text on the circuit and on WASM. A click opened `/story/2/3` and marked
    the row read.
  - Unpublish, then Publish: the anchor did not move and no row was written.
- **Reports (70–82).**
  - TestUser reported a comment → type 80 with `source_user_id` NULL and the report id. The bell
    reads "Thanks — we received your report"; a click marks it read and goes nowhere.
  - ModUser hid the comment from `/mod/reports`. TestUser got 81 ("A report you filed has been
    resolved"). The comment's author, ReaderGamma, got 70 in the bell and by email. Neither names
    the moderator.
  - Resolving AuthorBeta's seed report with No action → 82, carrying that report's id.
  - A `psql` count found 0 of the 5 rows in 70–82 with a source.
  - ModUser filed a report with the ordinary button and resolved it: the receipt only, no 82.
- **Group (60/25).**
  - TestUser added AuthorAlpha's story 7 to the standard group, making `GroupStory` 3.
  - ReaderGamma got 60: "Seed Story: Character Pairing (T) was added to Seed Group: Standard".
  - AuthorAlpha got 25: "Your story … was added to …". A click opens `/group/1`.
  - Both rows are anchored on `GroupStory` 3. Nothing went to TestUser, the adder. AuthorAlpha is a
    member and was not sent a 60.
  - A re-add wrote nothing.
- **Phases.** Bell read on the circuit (TestUser type 10, AuthorBeta 82, ModUser 80) and on WASM
  (TestUser 10/80/81, ReaderGamma 60/70/10, AuthorAlpha 25).
- **Not driven:** 72–79 and 26. Integration covers their NULL source.
- **Seen, already routed:** 90/91 read "You have a new notification" (presenter arms for 90–92 belong to
  WU-NotificationCorrectness).

## Feature 42 — Notification Display

**WU-D21Confidentiality Stage note (2026-10-14) — tracker D21 closed; F42 L2 stays 5, L4.5 5→1.**
`NotificationEnricher` split its three story-family kinds by recipient, the D8 way (Settled note
"story-family targets by recipient" in the Shared Context above; rule and type table:
`layer2-services.md` §"Polymorphic RelatedEntityId" → "Story-family kinds by recipient"):
- **Enumerated first.** Every `KindFor` arm that reads a story title was traced to its producer's
  recipient. Three kinds, 23 types: `Story` (19 types, 6 of them B24 stubs), `Chapter` (10, 24),
  `GroupStory` (60, 25). Nine `Story` types, 24 and 25 tell the story's author; the rest tell a
  follower, a recommender (self-recommendation is refused), a group member (60 excludes the author), a
  lineage counterpart or a credited user.
- **`OwnStory`, `OwnChapter`, `OwnGroupStory`** serve the author-recipient types and resolve the story
  as stored, as every story kind did before (the unfiltered `StoryListings` read), taken down included
  (the link to a taken-down story or an unpublished chapter dropped by the review fixes below).
- **`Story`, `Chapter`, `GroupStory`** now read `Stories` through the public window (not Draft,
  PendingApproval or Rejected) with `IsTakenDown` active and `ContentRating` lifted (Personal plane).
  The window is restated without the `StoryStatus` filter's viewer clause, so the answer never depends
  on who is asking and the email composer (no active user) shares it. `Chapter` also requires the
  chapter to be published (`StoryVisibilityGuard.IsChapterVisibleAsync`'s rule).
- **Rendering follows D8:** a hidden story is a lookup miss. `Story`/`Chapter` targets drop whole, so
  the type's title-free arm renders with no link. A `GroupStory` target keeps the group's name and link
  and loses only `TargetContextTitle` ("A new story was added to {group}").
- **Mint-time leaks of the same root, closed by the same lookup:** an author's own draft filed into a
  group (the tracker's probe; whether that add is allowed is decision row 31 / F28, untouched), a
  lineage request from a draft source (50, whose source is only ownership-gated), and a credit on a
  draft (52).
- **Copy:** type 52's title-free arm said "{actor} acknowledged your story" to the credited user, about
  the actor's story; the fix makes it reachable for every credit on a draft, so it now reads "{actor}
  acknowledged your help on a story".
Tests: **Integration** `StoryNotificationConfidentialityTests` (17): the probe (an author's own draft
filed into their group; a second member's 60 keeps the group, loses the story title); a story
unpublished or taken down after the fan-out (the member's 60 loses it, the author's 25 keeps it);
every `Story`-kind type over each hidden state, author types titled and the rest not, with a mature
rating that never hides anything; a classification check that every type linking `/story/{id}` is in
one of the two lists; the chapter kind under an unpublished story, a takedown and an unpublished
chapter (10 drops, 24 keeps); the real lineage and credit producers on a draft (52 is named once the
story is published); the 60's email for a draft carries the group and no story title. With the fix
removed 13 fail (the three visible-story controls and the classification check pass, as they should).
Mutation-checked separately: 25 or 24 moved to its filtered kind, 75 moved to `Story`, the chapter
clause removed, `IsTakenDown` lifted, and an unlisted type mapped to `Story` each fail their test.
**Unit** `NotificationPresenterTests` +1 (the 52 arm with and without a title). **L4.5 5→1:** the bell
and the inbox render the title-free copy for hidden stories, and 52's copy changed, undriven (no browser
in this pass); tracker **H43**. Found while scoping, not a notification: tracker **D22** (the credit
and lineage manage lists show a counterpart's unpublished story title).
**Review fixes (2026-10-14; three reviewers, twelve findings: eleven real, eight of them distinct).** One behavior change: the
`Own*` kinds linked a page their author cannot open. The story page and the chapter page keep the
`IsTakenDown` filter for the author too, and the chapter page serves only a published chapter, so
`OwnStory` and `OwnChapter` now keep the title and drop the link for a taken-down story, and
`OwnChapter` also for an unpublished chapter (the pre-split kinds linked these too). Whether the link
should go to the editor instead is decision row 27 (e). Covered by the same Integration class: the
Story-kind theory's taken-down case and the chapter theory's takedown and unpublished-chapter cases now
expect no link for the author, and fail with the link restored. The rest is docs and comments: the
enumeration count above (23 types, 6 stubs; it said 31 and 4), the F41 headline pointers, the D16
settled notes here and in `audit/Groups.md` annotated with the split, the producer comments in
`ServerNotificationWriteService`/`INotificationWriteService`, `layer2-services.md`'s Group Rating
Waterfall clause re-tensed, and D22's product question split out as **F29** (decision row 32), with
the tests that lean on rows 31 and 32's status quo named in F28 and F29. Rejected: type 52's
title-free copy was said to be false for the "Inspiration" role, but that role was deleted by the
`WU_StatBadgeProducers` migration; the four live roles are all help. **L4.5** stays 1: H43 step 4
gains the author's takedown case.

**WU-TrackerFixes Stage note (2026-10-13) — tracker D8 closed; F42 L2 stays 5, L4.5 5→1.**
`NotificationEnricher` split its post-linked blog kind by recipient (the tracker's recommended type
split; Settled note "blog-post targets by recipient" in the Shared Context above):
- `NewCommentOnBlog` (33) resolves through a new `OwnBlogPost` kind, as stored: its recipient is the
  post's author.
- `BlogPostDirect` (13–16, 100) drops an unpublished post of any subtype, and a profile post whose
  author's `ProfileVisibility` is `Private`, in the same single query (a TPT-root `is`/cast predicate).
  A dropped post is a miss: no title, no link.
- `PollUpdated`'s presenter arm names the post again ("A poll you voted on in {post} was changed").
Tests: **Integration** `CommentAndBlogNotificationTests` +5 (under both triggers a follower's 13 loses
title and link while the author's 33 keeps them; a UsersOnly control keeps the title; a group post's 100
by publish state). Mutation-checked: 33 back on `BlogPostDirect`, and the profile filter removed, each
fail. **Unit** `NotificationPresenterTests` (the 100 arm with and without a title). **L4.5 5→1:** the
100 copy changed undriven; tracker **H38** step 5.
*Browser pass (2026-10-13), L4.5 1→5.* Bell and inbox on both render phases, `psql` for each row:
AuthorAlpha published post 4 ("E2E D8: followers see this title"), and her followers got type 13
(notifications 40052–40054). Then she edited poll 2 on post 2 (its `last_edited_at` was aged past the
30-minute quiet period through `psql`, and the sweep wrote type 100 for TestUser, notification 40055).
TestUser read "AuthorAlpha posted a blog entry: E2E D8: followers see this title" (click: `/blog/4`) and
"A poll you voted on in Seed Blog Post: Published, Linked Story was changed". AuthorAlpha then set her
profile Private through the privacy form: TestUser's two became "AuthorAlpha posted a new blog entry" and
"A poll you voted on was changed", with no link (a click stays on `/notifications`), while AuthorAlpha's
own 33 ("TestUser commented on Seed Blog Post: Published, Linked Story", 40047) kept its title and opened
`/blog/2`. Her visibility was restored to Public through `psql` afterwards. Tracker **H38** closed.

**WU-ModerationIntegrity Stage note (2026-09-30) — no flip:** the shared `ActiveUser.RequireUserId()`
guard (`Core/Identity/ActiveUserContextExtensions.cs`) replaces this service's private
`RequireAuthenticatedUser` copy — owner ruling D9's "one shared guard", WU-ModerationIntegrity
2026-09-30. Same `InvalidOperationException` → 401, so no behavior change; the existing Integration
tests for the anonymous refusal stay green. `ServerNotificationReadService` exposes `ActiveUser` to the
write class in place of `CurrentUserId` (CS9107 shape).

- **L1 — Stage 5.** **L2 — Stage 2 → 5 (WU22; shared auth guard WU-ModerationIntegrity 2026-09-30; the two-pass enrichment moved out to
  `NotificationEnricher` at WU-NotifEmail 2026-07-31 so email shares it — stage unchanged; `long` ids,
  the `GroupStory` kind, `TargetContextTitle` and the two-nulls presenter rule, WU-InertFeatures
  2026-09-30 — stage unchanged, see the slice below; per-field `Collapsed`, the `OldestUnreadFirst`
  id tiebreak and presenter arms for 90–92/100, WU-NotificationCorrectness 2026-10-12 — stage
  unchanged; the type-62 presenter arm, WU-UserDeletion 2026-10-13 — stage unchanged; Stage notes above Feature 41; blog-post targets split by recipient, WU-TrackerFixes 2026-10-13, tracker D8 — stage unchanged, Integration-tested, Stage note at this feature's top; story-family targets split by recipient, WU-D21Confidentiality 2026-10-14, tracker D21 — stage unchanged, Integration- and Unit-tested, Stage note at this feature's top; its review fixes the same day keep the author's title but drop the link to a taken-down story or an unpublished chapter — stage unchanged, Integration-tested, same note).** **L4.5 — Stage 1 (5→1 at WU-D21Confidentiality, 2026-10-14: hidden stories' notifications render title-free and type 52's copy changed, and (its review fixes) an author's notifications for their taken-down story stop navigating, undriven — tracker H43; Stage note at this feature's top. Before that Stage 5: 1→5 at the WU-TrackerFixes browser pass, 2026-10-13: `PollUpdated` names its post again, driven on both render phases — tracker H38 closed; 5→1 at WU-TrackerFixes earlier the same day. Before that Stage 5: 5→1 at WU-UserDeletion, 1→5 at its browser pass, both 2026-10-13:** the type-62 copy was read in the bell and the inbox on both render phases and in its email on the Aspire path — tracker **H29** closed, browser verification note above Feature 41). Before that it went 5→1 at the
  WU-NotificationCorrectness review fixes (2026-10-13) and 1→5 at its browser pass the same day. That pass read the 90–92 and 100 copy in the bell and the inbox on both render phases (tracker
  **H26** closed; browser verification note above Feature 41).
  **WU-InertFeatures slice (2026-09-30):** `NotificationEnricher.ResolveTargetsAsync` takes
  `(type, long)` pairs and narrows each kind's id set to `int` before querying (out-of-range ids miss);
  results are a `NotificationTarget(Title, Url, ContextTitle)` record consumed by the read service and
  the email flusher; new `GroupStory` kind (explicit joins, `GroupAudience` bypass, title = group, link =
  `/group/{id}`, context = story); the `Chapter` kind also returns the story title as context.
  `NotificationDto` gains an optional trailing `TargetContextTitle`. `NotificationPresenter`: the
  two-nulls rule (actor-free types never say "Someone"); `ReportReceived` reworded as a receipt to the
  reporter; explicit arms for 75–79 (previously the catch-all); 60 → "{story} was added to {group}",
  25 → "Your story {story} was added to {group}", 10 → "New chapter of {story}: {chapter}", each with
  one- and zero-name fallbacks. Covered by Unit `NotificationPresenterTests` (+8 tests, 25 cases, incl. a 14-type
  actor-free theory) and the Integration enrichment pins in `GroupServiceTests` /
  `NewChapterNotificationTests` / `NotificationServiceTests`. L4.5 stays 5 (copy-only change; the
  bell's new types were driven by the WU-InertFeatures browser pass, 2026-09-30, on both render
  phases — F41's browser-verification note).
  Settled constraints:
  - `INotificationReadService`: `GetUnreadCountAsync()`, `GetNotificationsAsync(page, pageSize)`. All
    self-scoped via `IActiveUserContext` (the whole surface is "my notifications").
  - `GetNotificationsAsync` returns `NotificationDto` with effective `Collapsed` (type default
    overridden by the user's sparse setting when the row's `Collapsed` is non-NULL — per field since
    D19). Both orders end in the unique `NotificationId` (WU-NotificationCorrectness).
  - The bell in the layout injects `INotificationReadService` directly — legitimate cross-cutting
    injection (see `render-and-layout.md` "Notification bell").
  - Mark-as-read mutations (`MarkAsReadAsync`, `MarkAllAsReadAsync`) live on `INotificationWriteService`
    (it inherits from `INotificationReadService`).
  - **L3-Logic — Stage 5, L3.5-Structure — Stage 5** (built WU33, 2026-06-24 — the bell in the
    layout, the panel grouped by `NotificationCategory` with `DefaultCollapsed`/user override per type,
    the flyout preview; browser-verified 2026-07-01/02, the section at the end of this file). **L4 —
    Stage 1.** (Corrected WU-NotificationCorrectness, 2026-10-12: this line still read "Stage 2 …
    deferred to WU33" against the grid.)
  - **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
    site now runs global InteractiveAuto; notifications page + bell badge verified in a real WASM
    runtime during the flip's browser wave (persisted unread count). Full wave narrative + the 7
    bugs found/fixed: `workplan.md` WU-GlobalFlip.
  - **L6 — Stage 5 (WU-L6, 2026-07-07** — `ix_notifications_recipient_read_date` built + measured;
    see the Feature 41 L6 note).

- **WU22 Stage-5 note (L2 only, 2026-06-23):** `INotificationReadService.GetUnreadCountAsync()`,
  `GetNotificationsAsync(page, pageSize)` (LEFT JOIN UserNotificationSettings → effective Collapsed),
  and `GetSettingsAsync()` (LEFT JOIN UserNotificationSettings → effective EmailEnabled/Collapsed,
  IsDefault flag) are all in `ServerNotificationReadService`. `MarkAsReadAsync` /
  `MarkAllAsReadAsync` are in `ServerNotificationWriteService`. Covered by Integration tier (see
  Feature 41 Stage-5 note). L3/L3.5/L4/L5 remain Stage 2 — deferred to WU33.

- **WU33 additive L2 changes (2026-06-24) — L2 re-verified Stage 5 after enrichment:**
  `NotificationDto` extended with three additive nullable fields: `string? SourceUserName` (actor display
  name; null when source deleted via SET NULL or type has no actor), `string? TargetTitle` (resolved entity
  title), `string? TargetUrl` (resolved deep link). Populated by two-pass batch enrichment in
  `GetNotificationsAsync`: (1) LEFT JOIN to `Users` on `SourceUserId` for `SourceUserName`; (2) materialize
  the page, classify each row by a private `RelatedEntityKind` switch, batch-load each kind present on the
  page into a `Dictionary<int,(Title,Url)>`, stitch. See `layer2-services.md` "Polymorphic RelatedEntityId —
  Two-Pass Batch Enrichment." `INotificationReadService.GetNotificationsAsync` gains additive optional param
  `NotificationFeedOrder order = NotificationFeedOrder.NewestFirst` — existing callers unaffected.
  `NewestFirst` → `DateCreated desc`; `OldestUnreadFirst` → `OrderBy(IsRead).ThenBy(DateCreated)`.
  Confirmed contract-additive (new DTO fields + new optional param); no existing test or caller breaks.
  L3/L3.5/L4 built in WU33 (see Stage-5 note below after WU33 completes).

- **WU33 Stage-5 note (L3/L3.5, 2026-06-24):** F42 L3-Logic and L3.5-Structure → Stage 5.
  Components built: `NotificationCategoryVisuals.cs` (static enum→display-data map, mirrors
  `BookshelfTabVisuals`; reuses `UserStoryInteractionVisuals.For(Follow)` teal, `Ignore` red,
  `RecommendationIcons.RecommendationIconPath` green; new SVG paths for SiteNews/YourProfile/
  Collaborations/Groups/YourReports); `NotificationPresenter.cs` (static per-type message composer,
  with per-type icon overrides: HiddenGem → gem icon/Torterra Emerald); `NotificationItem.razor`
  (pure leaf — icon, composed text, relative timestamp, unread dot, `OnActivate` callback);
  `NotificationsPage.razor` (`@page "/notifications"`, `[Authorize]`, injects `INotificationWriteService`,
  by-date / by-category view toggle, sort toggle for date feed, `<details>` category groups seeded
  from effective `Collapsed`, mark-all-read, `PaginationControls` backed by `GetTotalCountAsync`);
  `NotificationBell.razor` (cross-cutting layout element, `<AuthorizeView>` wrapper, UserCard caret
  pattern, badge count, flyout preview 8 items, mark-all, "See all" link; injects
  `INotificationWriteService` to cover mark-as-read from the flyout; inserted before `<LoginDisplay />`
  in `DesktopLayout` and `MobileLayout`); `GetTotalCountAsync()` added additively to
  `INotificationReadService` and `ServerNotificationReadService`. L4 stays Stage 1 — Tailwind classes
  written but not locked into Pattern Accumulation pending visual sign-off (WU8/WU13/WU23 precedent).
  **Test tiers:** Integration (22 tests, 6 new WU33: SourceUserName resolved, TargetUrl for User-kind,
  null target for SiteAnnouncement, OldestUnreadFirst ordering, GetTotalCountAsync, anonymous 0);
  Unit: `NotificationCategoryVisualsTests` (13 tests — all 9 categories non-empty, reuse color matches,
  AllCategories count/order) + `NotificationPresenterTests` (22 tests — all types non-null fields, actor
  fallback "Someone", target embedded, HiddenGem icon override, no null-literal in text). RazorComponents
  tier for notification UI deferred — `FakeNotificationWriteService` not yet in the fakes catalog (no
  other consumer existed to prompt it); add in the next WU that writes a bUnit notification test.

- **WU35 correction (2026-06-24) — enrichment not in committed server impl:**
  A full `dotnet build --no-incremental` during WU35 revealed that `ServerNotificationReadService`
  had never been updated after the WU33 interface/DTO changes: it still had the 2-param
  `GetNotificationsAsync(int page, int pageSize)` signature and the old 8-arg `NotificationDto`
  constructor call. The WU33 audit note above overstated what was committed.
  **Fix applied in WU35:** param added, ordering switch added, `null` stubs passed for
  `SourceUserName`/`TargetTitle`/`TargetUrl` — the actual two-pass enrichment logic is still
  pending. **Practical consequence:** the three enrichment fields are always `null` in production
  until the enrichment batch lands. Since F42 L3/L3.5 are Stage 2 (notification UI not built),
  nothing currently displays these fields. Stage-5 note for F42 L2 stands for the
  plumbing/contract (compile-clean, ordering correct); the enrichment is an additive
  implementation detail to complete before the notification UI work-unit (WU33).

- **Anonymous-viewer crash fix (2026-07-13) — L3/L5 remain Stage 5; found via browser debugging:**
  `NotificationBell` threw an unhandled 401 (`CanalaveErrorBoundary` "chrome" island wiped —
  `CreateMenu`/`MessagesNavLink`/`NotificationBell`/`UserMenu` all disappeared together) for any
  anonymous viewer under the WASM runtime, not only right after logout — reproduced cold in a
  browser tab that had never authenticated at all. Root cause: `NotificationBell`'s own
  `<AuthorizeView>` gated its *markup* but not its `OnInitializedAsync`, which called
  `INotificationWriteService.GetNotificationsAsync`/`GetUnreadCountAsync` unconditionally —
  `@inject` (even written physically inside `<Authorized>` markup) and lifecycle methods resolve/run
  at component construction regardless of conditional markup. The server impl silently tolerated this
  (anonymous-safe zero/empty return); the WU-L5Sweep/WU-GlobalFlip (2026-07-12/13) WASM client impl
  hits the real `RequireAuthorization()` endpoint and throws instead. This exact latent defect was
  already named in `layer3-logic.md` "Deferring DI Behind AuthorizeView (WU43)" as a known gap in
  `NotificationBell` specifically, predating a test that would have caught it. **Fix:** split into
  `NotificationBell.razor` (thin `<AuthorizeView>` wrapper, no `@inject`) +
  `NotificationBellInner.razor` (all markup/services/`[PersistentState] UnreadCount`, instantiated
  only when authorized) — the standard wrapper/inner pattern, not a defensive auth re-check. **Verified:**
  browser — cold anonymous tab loads clean (no crash, "Log in" shown); login → logout cycle
  (dev-bar TestUser) shows the chrome island correctly flip to "Log in" with zero console errors;
  flyout preview/mark-all-read still work for an authenticated viewer post-split. RazorComponents
  tier: full 639-test suite green before and after (no test exercised this path — the fakes-catalog
  gap `layer3-logic.md` flagged still stands; adding `FakeNotificationWriteService` + an anonymous-
  viewer NotificationBell test is follow-up work, not done here).

- **Circuit-concurrency fix (2026-07-01) — L2 remains Stage 5; found via browser debugging:**
  First real browser login (dev-bar TestUser) crashed with `InvalidOperationException: A second
  operation was started on this context instance` — `NotificationBell` + `MessagesNavLink` both
  render in the layout, both backed by the same circuit-scoped `ReadOnlyApplicationDbContext`, and
  Blazor Server interleaves their async init. Sequentializing the bell's two awaits only *moved*
  the stack trace (partial fix — see `debugging.md`). Root fix is cross-cutting, not F42-local:
  all read services now create a per-method context from a scoped
  `IDbContextFactory<ReadOnlyApplicationDbContext>`; `ServerNotificationReadService`'s protected
  `ReadDb` became `ReadDbFactory`, `BatchLoadEntitiesAsync` takes the context as a parameter, and
  the bell's parallel `RefreshAsync` loads were restored (sanctioned under the factory rule).
  Convention: `layer2-services.md` §"Read-Context Concurrency: Factory Per Method" (supersedes
  spec §6.6). **Verified:** browser — dev-bar login renders authenticated home with bell +
  messages, no 500; Integration — `ConcurrentReadAccessTests` (two-services-one-scope,
  one-service-parallel-calls, chrome-plus-page shapes); full suite green post-refactor.

- **Mid-session refresh bug found and fixed (WU-AccountEnforcement, 2026-07-30) — L3-Logic stays
  Stage 5, no Stage change.** `NotificationBellInner`'s own header comment claimed "Count and
  preview refresh on mount / navigation," but nothing ever subscribed to
  `NavigationManager.LocationChanged` — only the mount half was true. A notification landing
  mid-session (this WU's own motivating case: the account-status moderator notifications
  `AccountStatusBanner` surfaces alongside) stayed invisible in the bell until the next full page
  load, even though its sibling `MessagesNavLink` had the correct pattern all along. Found while
  building the account-status live-read fix (`audit/Identity.md`'s WU-AccountEnforcement Stage
  note), not independently reported — same bug class, fixed the same way: `LocationChanged`
  subscription re-queries `GetUnreadCountAsync`/`GetNotificationsAsync`, `IDisposable`
  unsubscribe, `try`/`catch` → `LogWarning` degrade-to-last-known on failure (parity with
  `MessagesNavLink.RefreshCountAsync`). **Verified:** RazorComponents —
  `NotificationBellTests.AuthorizedRender_Navigates_RefreshesUnreadCount` (fake unread count
  changes between mount and a simulated `NavigateTo`, badge reflects the new count without a
  reload). `dotnet test` full suite green (2344 total: 763 Unit + 620 RazorComponents + 961
  Integration). Live-verified via real Chrome + `psql` in the same WU (`audit/Identity.md`
  WU-AccountEnforcement Stage note): a moderator's Warn action incremented this badge in the
  target's tab on the same in-app navigation that surfaced `AccountStatusBanner`.

## Feature 43 — Notification Settings

- **L1 — Stage 5** (`UserNotificationSetting` sparse-override; `EmailEnabled`/`Collapsed` — see Shared
  Context correction; `NULL` for either = use default, §5.18 as corrected; both columns nullable and
  sparse per field since D19, WU-NotificationCorrectness 2026-10-12 — stage unchanged, migration
  applied). **L2 — Stage 2 → 5 (WU22;
  `EmailEnabled` stopped being inert at WU-NotifEmail 2026-07-31 — it now gates real mail and is
  writable from the one-click unsubscribe endpoint as well as this page; stage unchanged; per-field
  write/read rule, race-free upsert and per-field DTO flags, WU-NotificationCorrectness 2026-10-12 —
  stage unchanged; an unsubscribe link for a since-deleted account answers 400 instead of a 500, its
  review fixes 2026-10-13; Stage notes above Feature 41).**
  Settled constraints (the sparse rule is per field since owner ruling D19 — answered 2026-08-07,
  built WU-NotificationCorrectness 2026-10-12; rule text `layer2-services.md` §"Notification
  Generation" → "Filtering semantics"):
  - `GetSettingsAsync()` returns `NotificationSettingDto[]` grouped by category — LEFT JOIN
    `UserNotificationSetting` onto `NotificationType`; a missing row **or** a NULL column ⇒ that
    field's type default (`DefaultEmailEnabled`, `DefaultCollapsed`), resolved per field. Carries
    `EmailIsDefault` / `CollapsedIsDefault` (per field; the row-level `IsDefault` is retired) so a UI
    could tell "following the site default" from an override — nothing renders them yet.
  - `SetSettingAsync(NotificationTypeEnum type, bool emailEnabled, bool collapsed)` — the wire
    contract keeps two effective `bool`s. Server-side, each field equal to its type default is stored
    as NULL; the row is deleted only when both are NULL; otherwise one race-free
    `INSERT … ON CONFLICT DO UPDATE`. Self-scoped via `IActiveUserContext`.
  - Not built, by ruling: a three-state default/on/off control (D19's accepted cost).
  - **L3/L3.5 — Stage 5** (built WU33, 2026-06-24 — see its Stage-5 note below; a "Private messages"
    section read and written through `IMessagingWriteService` added by WU-PrivateMessageEmail,
    2026-10-13, owner ruling D18's standing rider — RazorComponents-tested, stage unchanged, Stage note
    above Feature 41). Settings page driven by DB data. **L4 — Stage 1.** (Corrected WU-NotificationCorrectness, 2026-10-12: this line still
    read "Stage 2 … deferred to WU33" against the grid's 5/5.)
  - **L4.5 — Stage 5 (5→1 at WU-PrivateMessageEmail, 1→5 at its browser pass, both 2026-10-13: the
    "Private messages" section read, toggled and reloaded on both render phases with `psql` as ground
    truth, a refused write rolled back with a readable line on WASM; tracker **H35** closed; the
    WU-PrivateMessageEmail Stage note above Feature 41).** Before that it was 5: it went 5→1 at the WU-NotificationCorrectness review fixes (2026-10-13),
    because the page now reads the per-field `NotificationSettingDto` on WASM and the one-click
    unsubscribe writes one column. It went 1→5 at the browser pass the same day. That pass drove
    the per-field toggles on both render phases and the Mailpit unsubscribe, with `psql` as ground
    truth (tracker **H26** closed; browser verification note above Feature 41).
  - **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13; the Private messages section's WASM path is Messaging's
    new `email-preference` endpoints and client calls, WU-PrivateMessageEmail 2026-10-13 — Unit- and
    Integration-tested, stage unchanged).** Endpoints + client impl live (WU-L5Sweep) and the
    site now runs global InteractiveAuto; the notification settings page loads in a real WASM
    runtime during the flip's browser wave. Full wave narrative + the 7 bugs found/fixed:
    `workplan.md` WU-GlobalFlip.

- **WU22 Stage-5 note (L2 only, 2026-06-23):** `SetSettingAsync` sparse-model — upserts the override
  row when values differ from type defaults; deletes it when both match defaults (returning to NULL =
  "use default"). `GetSettingsAsync` LEFT-JOINs onto types; NULL → IsDefault = true. Covered by
  Integration tier (see Feature 41 Stage-5 note). L3/L3.5/L4 remain Stage 2 — deferred to WU33.

- **WU33 Stage-5 note (L3/L3.5, 2026-06-24):** F43 L3-Logic and L3.5-Structure → Stage 5.
  `NotificationSettingsPage.razor` built: `@page "/notifications/settings"`, `[Authorize]`, injects
  `INotificationWriteService`. Groups all notification types by `NotificationCategoryVisuals.AllCategories`
  ordering; renders each category with its icon/label header and a `grid-cols-[1fr_auto_auto]` per-type
  row (type name + description + Email checkbox + Collapsed checkbox). Per-row immediate save: each
  `@onchange` handler calls `SetSettingAsync(dto.TypeId, emailEnabled, collapsed)` inline — no `EditForm`,
  no Save button. Optimistic local update via `_settings = [.. _settings.Select(s => s.TypeId == dto.TypeId
  ? s with { ... } : s)]` before await. Lambda capture bug avoided with `var d = dto` inside `@foreach`.
  L4 stays Stage 1 — Tailwind classes written but not locked into Pattern Accumulation pending visual
  sign-off (WU8/WU13/WU23 precedent). **Test tiers:** Integration (covered by WU22's `GetSettingsAsync`/
  `SetSettingAsync` tests; settings page is a pass-through to those service methods — no new integration
  test needed). Unit (NotificationCategoryVisuals tests cover the category-grouping logic). RazorComponents
  test for the settings page deferred with F42's bell/page tests (same blocker: `FakeNotificationWriteService`
  not yet in the fakes catalog; add when the first bUnit notification test is needed).

## Feature 57 — Notification Cleanup Worker

- **L2 — Stage 5 (WU-NotificationCleanup, 2026-07-15).** Built as the standard worker/body split
  (the `SpotlightGoLiveWorker`/`SpotlightGoLiveSweeper` pattern): `NotificationCleanupWorker`
  (`BackgroundService`, 24 h `PeriodicTimer`, first sweep ~5 s after startup, survives a failed
  cycle) delegates to `NotificationCleanupSweeper.SweepAsync` — one set-based `ExecuteDeleteAsync`
  on `IsRead && DateCreated < now − RetentionPeriod` (60 days, `public static readonly` so tests
  reference the real constant). Unread notifications are kept indefinitely regardless of age — the
  user hasn't seen them, and the unread count must stay truthful. `TestAppFactory` removes the
  worker; tests drive the sweeper directly. **No new index** for the `is_read + date_created`
  predicate: `ix_notifications_recipient_read_date` leads with recipient so the sweep scans, but a
  once-daily sweep over a table pruned to ≤60 days of read rows is negligible, and a dedicated
  partial index would tax every notification insert to save a background job milliseconds.
  **Test tiers:** Integration (`NotificationCleanupTests` ×2 — four-quadrant read×age matrix
  deletes exactly the read+aged row; nothing-eligible sweep deletes zero). The timer loop itself is
  the shared, already-proven `BackgroundService` scaffold (no automated tier exercises cadence).
  Also verified end-to-end 2026-07-15 against the dev DB: boot sweep deleted 13,520 aged read rows
  (bulk-seed data + a psql marker), survivors confirmed via psql and the rendered `/notifications`
  page. All other layers **N/A** (pure background computation — Layer 2 *is* the worker).

## L4.5-Browser verification (2026-07-01/02) — F41 + F42 + F43 → Stage 5

- **F41 generation via real seams (psql-verified after UI actions):** Follow → NewFollowerOnYou
  (type 30) to the followee; Vouch → NewVouchOnYou (type 32); recommender's Hidden Gem designation
  → HiddenGem (type 23) to the story author. Drop-self/dedup paths remain Integration-covered.
- **F42 display:** bell badge counts unread; flyout preview with unread dots; "Mark all read"
  clears badge + dots (verified in the first browser wave); page composes presenter text with
  live enrichment (`SourceUserName` resolved: "AuthorAlpha is now following you"); By date ↔
  By category toggle groups under category headers with counts; Newest/Oldest-unread orderings
  render.
- **F43 settings:** grouped per-type grid renders; toggling Site Announcement's Email checkbox
  wrote the sparse override row (`user_notification_settings (1, 0, email_enabled=t)`) — the
  sparse upsert model live in-browser.
