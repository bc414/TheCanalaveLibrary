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
- **Eligibility rule — unchanged.** Effective `EmailEnabled` = the sparse `UserNotificationSetting`
  row's value, or `NotificationType.DefaultEmailEnabled` when no row exists. Same LEFT JOIN shape
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
  Unsubscribing sets `EmailEnabled = false` for that one type via the same sparse upsert/delete
  semantics as `SetSettingAsync`.
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

## Feature 41 — Notification Generation

**Stages (updated 2026-09-30, WU-InertFeatures and its review fixes):** L1, L2, L6 = 5 (the D4/D5/D16/D17 rebuild and the
new-chapter fan-out landed beneath them — Stage notes at the end of this feature); **L4.5 = 1**
(flipped 5→1: the new-chapter notification, the restored report receipt and the re-anchored group
notifications were never seen in a browser bell — returns to 5 with tracker **H14**'s pass);
L3/L3.5/L4/L5 = N/A. Trackers **B20** and **B21** closed.

- **L1 — Stage 5 (`related_entity_id` widened to `bigint`, WU-InertFeatures 2026-09-30).** `Notification` + the fully-seeded type/category tables. Sound. **L6 — Stage 5
  (WU-L6, 2026-07-07)** — `ix_notifications_recipient_read_date (recipient_user_id, is_read,
  date_created)` built in `L6_IndexBatch` (supersedes the recipient FK index); measured at 20k
  seeded notifications: unread count −47%; newest-first feed neutral by design (per-user residual
  sort, bounded by the 60-day cleanup worker). Detail: `layer6-indexes.md`.

- **L2 — Stage 2 → 5 (WU22; extended WU-NotifEmail 2026-07-31 — create-core now also enqueues the
  email fan-out, stage unchanged; a Private author's profile post fans out to nobody since the
  WU-AccessGateSweep2 review fixes, 2026-09-30 — see that slice below, stage unchanged; nullable
  source, de-identified moderation band, report-id anchors, `GroupStory` anchor, hidden favoriters in
  15 and the new-chapter fan-out, WU-InertFeatures 2026-09-30 — stage unchanged, Stage note at the end
  of this feature).** Settled constraints (do not revisit):
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
    - **D16 — one anchor per event.** 60 and 25 carry the `GroupStory` row's id (new
      `RelatedEntityKind.GroupStory`); a second id column is never added; the re-point backlog is
      recorded as a conformance list (tracker B23), not built. Riders: the story's author is excluded
      from the 60 fan-out (gets 25 only); the notify block moves inside `if (!alreadyAdded)`; an
      authorless story still notifies members.
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

## Feature 42 — Notification Display

- **L1 — Stage 5.** **L2 — Stage 2 → 5 (WU22; the two-pass enrichment moved out to
  `NotificationEnricher` at WU-NotifEmail 2026-07-31 so email shares it — stage unchanged; `long` ids,
  the `GroupStory` kind, `TargetContextTitle` and the two-nulls presenter rule, WU-InertFeatures
  2026-09-30 — stage unchanged, see the slice below).**
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
  bell's new types are listed in tracker H14's pass).
  Settled constraints:
  - `INotificationReadService`: `GetUnreadCountAsync()`, `GetNotificationsAsync(page, pageSize)`. All
    self-scoped via `IActiveUserContext` (the whole surface is "my notifications").
  - `GetNotificationsAsync` returns `NotificationDto` with effective `Collapsed` (type default
    overridden by the user's sparse setting when a row exists).
  - The bell in the layout injects `INotificationReadService` directly — legitimate cross-cutting
    injection (see `render-and-layout.md` "Notification bell").
  - Mark-as-read mutations (`MarkAsReadAsync`, `MarkAllAsReadAsync`) live on `INotificationWriteService`
    (it inherits from `INotificationReadService`).
  - **L3-Logic — Stage 2** (the notification bell in the layout; panel grouped by `NotificationCategory`,
    `DefaultCollapsed`/user-override per type). **L3.5-Structure — Stage 2** (panel + flyout preview).
    **L4 — Stage 1.** All deferred to WU33.
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
  Context correction; `NULL` for either = use default, §5.18 as corrected). **L2 — Stage 2 → 5 (WU22;
  `EmailEnabled` stopped being inert at WU-NotifEmail 2026-07-31 — it now gates real mail and is
  writable from the one-click unsubscribe endpoint as well as this page; stage unchanged).**
  Settled constraints:
  - `GetSettingsAsync()` returns `NotificationSettingDto[]` grouped by category — LEFT JOIN
    `UserNotificationSetting` onto `NotificationType`; NULL ⇒ type defaults (`DefaultEmailEnabled`,
    `DefaultCollapsed`). Includes `IsDefault` flag so the UI knows which rows are overridden.
  - `SetSettingAsync(NotificationTypeEnum type, bool emailEnabled, bool collapsed)` — sparse: upsert
    the override row when either field differs from the type defaults; delete the row when both match
    the defaults (returning to default requires no stored row). Self-scoped via `IActiveUserContext`.
  - **L3/L3.5 — Stage 2.** Settings page driven by DB data. **L4 — Stage 1.** All
    deferred to WU33.
  - **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
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
