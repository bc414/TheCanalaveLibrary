# Audit — Moderation/

**Features:** 46 (reporting), 47 (queue & actions), 48 (approval workflow), 53 (import & verification),
62 (SiteDailyStat worker). Routes `/mod/reports`, `/mod/submissions`, `/mod/users`. Desktop-only, **no
dispatcher pattern** (§3.10).

## Shared Context
**Entities (Core/Moderation/ — relocated in WU34):** `Report` (`ReportReasonId`/`ReportStatusId` Restrict,
polymorphic `ReportedEntityType`→short + `ReportedEntityId` widened to **long** in WU34 migration,
`DateReported` default, `(ReportStatusId)` and `(ReportedEntityType, ReportedEntityId)` indexes added),
`ReportReason`/`ReportStatus` (seeded, Restrict delete). **`StoryImport` stays in `Core/Models/` until WU39.**
`Story.ActiveReportCount` for mod-triage ordering (not auto-hiding — see WU34 settled decisions).
**No services or components built prior to WU34.**

**Schema additions in WU34:** `Report.ReportedEntityId int→long`; `ReportedEntityType` +`Message = 5`;
soft-delete columns on Story/BaseComment/BaseBlogPost/Recommendation (renamed in pre-integration cleanup
2026-06-26: `IsTakenDown bool`, `TakedownDate DateTime?`, `TakedownReason string?`; formerly
`IsHidden`/`DateModeratedRemoved`/`ModerationRemovalReason`); `User.AccountStatus` + `SuspendedUntilUtc` +
`ActiveReportCount`; `NotificationType` seed for `StoryApproved = 75` (category `YourStories=2`, deep-link
`KindFor → Story`).

**Not EF entities:** `SiteDailyStat` (PK `StatDate`) is a raw-SQL data mart (no `DbSet`, no migration —
see Feature 62 below); `DailyStoryStat` was dropped entirely, never modeled.

## Feature 46 — Content Reporting

**WU34 settled constraints:**
- Report targets: Story, User, Comment, BlogPost, Recommendation, PrivateMessage (`ReportedEntityId` is long).
- `SubmitReportAsync` validates the allow-set, stamps `ReporterUserId`/`Open`, increments target's
  `ActiveReportCount` (uniform `AdjustActiveReportCount` switch; skips PrivateMessage), best-effort
  `NotifyReportReceivedAsync`. No auto-hide or auto-flag logic.
- `ReportDialog.razor` is a reusable modal (reuses `ConfirmDialog`/modal-shell pattern, WU9). One host per
  consuming page; wired via `HasDelegate`-gated `OnReport` callbacks.
- **Open for opusplan:** specific `ReportDialog` state shape (selected reason + notes field); whether the
  reasons dropdown is a radio group or select; StoryCard/UserCard caret integration specifics.

**Stage note (WU34 — 2026-06-25):** L1=5, L2=5, L3=5, L3.5=5 (all verified: `dotnet test` green,
298 integration tests + 417 unit tests). `IModerationReadService`/`IModerationWriteService` implemented in
`Core/Moderation/` + `Server/Moderation/`. `ServerModerationWriteService.SubmitReportAsync` validated by
`ModerationServiceTests.SubmitReportAsync_CreatesReportRow_IncrementsStoryActiveReportCount` (Integration).
`ReportDialog.razor` + `OnReport` wired in all StoryDeck composites (BookshelvesDesktop/Mobile,
GroupDesktop/Mobile, ProfileDesktop/Mobile) and `CommentSection`. L4=3 (functional Tailwind applied; not
design-reviewed). L5=2 (WASM client service + API endpoint deferred — no `ClientModerationWriteService` yet;
superseded, see the L5 note below).
L6=5 (composite index `ix_reports_reported_entity_type_reported_entity_id` added in migration
`20260625140459_WU34_Moderation`).

**L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
site now runs global InteractiveAuto; report-queue reads verified as AdminUser in a real WASM
runtime during the flip's browser wave (`/mod/reports`, `/mod/submissions`, `/mod/users`; report
submission not driven). Full wave narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.

## Feature 47 — Moderation Queue & Actions

**WU34 settled constraints:**
- `/mod/reports` and `/mod/users` — server-rendered, mod-gated (`RequireModerator` policy), no dispatcher.
- Report queue ordered by `ActiveReportCount` desc (most-reported first) — triage sort only, never an
  automation trigger. Report counts are mod-only (no public-facing badge).
- Content removal: soft-takedown default (`IsTakenDown = true`, reversible, author notified with `TakedownReason`);
  separate explicit hard-delete for illegal content (CSAM/piracy). `LoadModeratableAsync` single loader switch
  + interface mutation via `IModeratableContent` in `ServerModerationWriteService` (pre-integration cleanup
  2026-06-26 collapsed the prior triple switch).
- Account actions: `AccountStatus` enum (Active/Warned/Suspended/Banned — **no Shadowbanned**) +
  `SuspendedUntilUtc` set on `User`. Status + notification + `Report` record set together.
  **Login-blocking enforcement landed in WU38a** (`CanalaveSignInManager.CanSignInAsync` +
  security-stamp bump on Suspend/Ban in `ApplyAccountActionAsync` — see
  `canalave-conventions/security.md` "Account-Status Enforcement" and this file's WU38a Stage note
  below).
- Polymorphic target label + deep-link resolved via two-pass `BatchLoadEntitiesAsync` pattern (one query
  per present target type — same pattern as `GetNotificationsAsync`).
- `User.ActiveReportCount` added (symmetric with other targets); `AdjustActiveReportCount` switch skips
  `PrivateMessage` (no display surface for DM report count).

**Stage note (WU34 — 2026-06-25):** L1=5, L2=5, L3=5, L3.5=5. `ModReportsPage.razor` + `ModUsersPage.razor`
built at `/mod/reports` + `/mod/users` — server-rendered, mod-gated. Claim/resolve/soft-remove/warn-user
flows implemented and covered by `ModerationServiceTests.ResolveNoActionAsync_*` and
`ResolveWithRemovalAsync_SoftHides_*` (Integration). `AdjustActiveReportCount` switch verified — Message
type is no-op (unit test). L4=3 (functional styling, not design-reviewed). L5=N/A. L6=5 (same migration
as Feature 46).

**Stage note (WU38a — 2026-07-11):** L2 stays Stage 5, re-verified (additive). `ApplyAccountActionAsync`
now calls `UserManager.UpdateSecurityStampAsync(targetUser)` after setting `Suspended`/`Banned` (not
`Warned`) so an already-open session dies via the existing 30-min
`IdentityRevalidatingAuthenticationStateProvider` stamp revalidation, closing the "login-blocking
enforcement deferred" gap this section used to point at — see `audit/Identity.md` WU38a Stage note
and `canalave-conventions/security.md` "Account-Status Enforcement" for the full mechanism (the
sign-in-side half, `CanalaveSignInManager`, lives in Identity, not here). **Verified:** Integration
(`AccountStatusEnforcementTests.ApplyAccountActionAsync_SuspendUser_BumpsSecurityStamp`/
`_BanUser_BumpsSecurityStamp`/`_WarnUser_DoesNotBumpSecurityStamp`) — stamp changes on Suspend/Ban,
unchanged on Warn. `dotnet test` 1483/1483 green.

## Feature 48 — Story Approval Workflow

**WU34 settled constraints:**
- `StoryDetail.PostApprovalStatus` (live field, enforced by `StoryValidations.CanSubmitForApproval`) is the
  submission mechanism — **not** `RequestedStatusId` (that was a deliberations-doc artifact, never built).
- Approve: `StoryStatusId = PostApprovalStatus` + `NotifyStoryApprovedAsync`.
- Reject: `StoryStatusId = Rejected` + `ActionTaken` reason + `NotifyStoryRejectedAsync`.
- `/mod/submissions` is a tabbed shell in WU34; import-verification tab drops in with WU39. Rec-approval
  wiring deferred (recs write as `Approved` directly; tab added later without restructuring).

**Stage note (WU34 — 2026-06-25):** L1=5, L2=5, L3=5, L3.5=5. `ModSubmissionsPage.razor` built at
`/mod/submissions` with tabbed shell (Stories tab active, Imports tab placeholder for WU39). Approve/reject
flows implemented and covered by `ModerationServiceTests.ApproveStoryAsync_*` /
`RejectStoryAsync_*` (Integration). `StoryApproved` notification wired end-to-end
(`NotifyStoryApprovedAsync` → `CreateCoreAsync` → notification row). L4=3 (functional styling). L5=N/A.

**Stage note (pre-integration cleanup — 2026-06-26):** Features 46/47/48, all L1-L3 cells updated.
Soft-delete columns renamed from `IsHidden`/`DateModeratedRemoved`/`ModerationRemovalReason` → `IsTakenDown`/
`TakedownDate`/`TakedownReason` across `Story`, `BaseComment`, `BaseBlogPost`, `Recommendation`; EF named
filter key `"ModeratedVisibility"` → `"IsTakenDown"`; `IModeratableContent` interface added in
`Core/Moderation/` with `AuthorUserId` projection; `ServerModerationWriteService` triple switch collapsed to
`LoadModeratableAsync` + interface mutation (one per-type loader switch remains; `AdjustActiveReportCountAsync`
stays as set-based `ExecuteUpdateAsync`); all moderation service filter bypasses changed from parameterless
`IgnoreQueryFilters()` to `IgnoreQueryFilters(["IsTakenDown"])` so `ContentRating`/`GroupAudience` stay
live — a moderator's rating reach equals their `ShowMatureContent` setting; report rows for entities filtered
by `ContentRating` are dropped (not placeholder-labelled); no-op `IgnoreQueryFilters()` on `ReadDb.Reports`
removed. Verified: `dotnet test` green (see verification section below). Note: integration tests for the new
per-mod rating-scoping behavior still to be added (see plan).

**Stage note (filter revamp — 2026-06-27):** `ServerModerationWriteService` — all 11 `IgnoreQueryFilters(
["IsTakenDown"])` on `writeDb` removed. Write context sees ground truth by architectural rule (no filters);
no bypass is needed when mods load entities to act on them. Moderation *read* bypasses in
`ServerModerationReadService` (`~6 calls`) kept — these are legitimate elevated reads (mod queue must see
taken-down content); each annotated `// elevated read:`. `ModerationServiceTests.ResolveWithRemovalAsync_
SoftHides_DropsFromPublicQuery_VisibleWithIgnoreFilter` corrected to use `ReadOnlyApplicationDbContext` for
the public-visibility assertion (was using write context, which is now unfiltered). Tests: Integration tier,
all 1232 pass. See `audit/Stories.md` §"Filter revamp Stage note" for the full cross-cutting narrative.

**Stage note (L4.5-Browser verification — 2026-07-02, Features 46/47/48 → L4.5=5):** full
report→claim→resolve and approve/reject cycles driven in a real browser against the seeded dev DB.
- **F46:** report filed on a chapter comment via `ReportDialog` (reason select + notes + submit);
  `reports` row verified in psql (reporter/status/notes correct).
- **F47:** `/mod/reports` as ModUser listed all three open reports; Claim → `UnderReview` +
  `ModeratorUserId` stamped; Act panel enforced the removal-reason validation ("A removal reason is
  required"), then Hide content → report `Resolved` (`ActionTaken` + `DateResolved` stamped), target
  comment `IsTakenDown=t` with `TakedownReason`, `ActiveReportCount` decremented to 0, and the
  comment no longer renders in the chapter thread. Takedown also fired the account-action
  notification to the comment author (type 70).
- **F48:** per-mod ContentRating scoping verified in both directions — ModUser
  (`ShowMatureContent=f`) saw only the E-rated pending story; AdminUser saw both. Approve →
  `StoryStatusId = PostApprovalStatus` (InProgress) + `StoryApproved` notification (type 75) to the
  author; Reject → validation requires a reason, then `Rejected` + `TakedownReason`/`TakedownDate`
  stamped + `StoryRejected` notification (type 71).
- **Seeder bug found & fixed same-session:** `DataSeeder` stamped `PostApprovalStatus = status` for
  every story, making the two PendingApproval seeds self-referential (approval would have been a
  silent no-op). Seeder now maps PendingApproval → InProgress; live rows patched via psql. The
  production submit path was already sound (`CanSubmitForApproval` requires a resolved status —
  Unit-covered; approve semantics Integration-covered by `ApproveStoryAsync_SetsPostApprovalStatus_
  NotifiesAuthor`).
- L4 stays 3 (functional styling, not design-reviewed) — nothing unusable found.

## Feature 53 — External Story Links & Verification (reframed 2026-07-11)

**Reframe (settled 2026-07-11, WU38d plan — supersedes the "Story Import & Verification" scope
below):** the feature is **"Also posted on" external story links**, plain language, display-first.
A story lists the other sites it's also live on (X, Y, Z — *multiple* links), shown low-key on the
story page (after the chapter list, before recommendations — awareness, not an invitation to click
away). Each link has a `VerificationStatus`; `Verified` links render an author-verified checkmark.
**Purpose (anti-theft):** anyone can pull a story off AO3/FFN via FicHub and re-upload it under
their own account — community members who recognize a story and see unverified links report it
(Feature 46, existing flow) for takedown. The site is anti-predatory even toward non-users: it
protects the wider Pokémon author population, including inactive authors. File-format *content*
ingestion (the other thing "import" used to mean here) is now **Feature 63** (`audit/Import.md`).

**Settled (WU38d — author-facing half, do not revisit):**
- **Data model:** `StoryImport` (one row, single source) remodeled to `StoryExternalLink` (many per
  story: `StoryExternalLinkId`, `StoryId`, `ExternalPlatformId` FK, `Url`, `VerificationStatus`
  enum-mapped `short` (`Unverified`/`Verified`/`Rejected`), `DateAdded`) + seeded
  `ExternalPlatform` lookup (`Name`, nullable `DomainPattern` for paste-a-URL auto-detect;
  "Other" row displays the URL's host). **Deliberately a lookup table, NOT a hybrid C# enum** —
  no compile-time branching on platform; the fanfic world's long tail of small archives should be
  seed rows, not code changes; matches the `ReportReason`/`ReportStatus` pattern. WU39 hangs
  per-platform verification properties off this table, not code branches. Entities live in
  `Core/Stories/` (story-page display is the primary use).
- **WU38d ships:** the remodel migration, story-page "Also posted on" row (checkmark only when
  `Verified`), `StoryPropertiesForm` repeatable link rows + original-dates edit surface,
  write-service sync (new links start `Unverified`; editing a verified link's URL resets it to
  `Unverified`). `Story.OriginalPublishedDate`/`OriginalLastUpdatedDate` already exist — no
  migration for those.
- **Dropped by the reframe:** "route the story into `PendingApproval`" — links don't gate story
  approval (Feature 48 untouched); verification is per-link, display-only.

**WU39 (re-minted as "External Link Verification (mod workflow)", deps WU34):** the
`/mod/submissions` link-verification tab (shell stubbed in WU34), moderator review of `Unverified`
links, the two-way-link authorship mechanism (site publishes a verifiable token the author puts on
the source page, vs. purely manual review — still open, WU39's question), flipping
`VerificationStatus` (checkmarks appear on the story page automatically).

**Stages:** L1 — Stage 5 (WU38d remodel migrated). L2 — Stage 2. L3/L3.5 — Stage 2.
L4 — Stage 1. L5 — N/A. (L2/L3/L3.5 stay 2 because the feature's mod-verification half is WU39;
the author-facing half below is done.)

**WU38d Stage note (2026-07-11) — author-facing half shipped:**
- **L1:** migration `WU38d_StoryExternalLinks` (drop `story_imports`, create
  `story_external_links` + seeded `external_platforms` ×7, unique `(story_id, url)`,
  Restrict FK to the lookup). Applied cleanly to the 3012-story dev workbench and to
  Testcontainers in every integration run. Global URL uniqueness (old rule) deliberately
  dropped — whether two stories claiming one URL is theft is a WU39/Feature-46 judgment, not a
  schema constraint a thief could use to squat a URL.
- **Built:** `Core/Stories/{StoryExternalLink, ExternalPlatform}` + `VerificationStatusEnum`;
  `StoryExternalLinkDto`/`EditDto`/`ExternalPlatformDto`; `GetExternalPlatformsAsync` on
  `IStoryReadService`; projection into `StoryDetailsDTO.ExternalLinks` and
  `GetStoryForEditAsync`; write-service sync (match on (platform, URL): unchanged rows keep
  status, missing rows delete, new rows start Unverified — so editing a verified link's URL
  resets it); URL validation (absolute http/https); `StoryPropertiesForm` "Also posted on"
  section (repeatable rows, paste-a-URL platform auto-detect via `DomainPattern`, original-dates
  inputs); `StoryExternalLinksRow` on the story page (after chapters, before recommendations —
  settled placement; checkmark + "Author verified" tooltip only when `Verified`; `rel=nofollow`).
- **Verified:** Integration (`StoryExternalLinkTests`, 7 tests — seeded lookup, Unverified start,
  dedupe/blank-drop, invalid-URL validation, **verified-kept vs URL-change-reset sync semantics**,
  row deletion, original-dates round-trip); RazorComponents (`StoryExternalLinksRowTests`, 4 —
  checkmark gating, absent-when-empty, and the settled after-chapters/before-recommendations
  placement asserted on `StoryDesktop`); browser (2026-07-11) — add-link with live AO3
  auto-detect, save, psql-confirmed Unverified row + original date, story-page row rendered in
  place, psql flip to Verified → checkmark + tooltip appeared.

## Feature 62 — SiteDailyStat Worker

**Requirements settled 2026-07-10 (WU-SiteDailyStat plan)** — reconciling the Gemini design source
(`GeminiDiscussions/MyActivity September to November 2025_filtered.md:38146`, 2025-10-29) against
the live schema. Full counter-by-counter source audit, the `new_`/`total_` rule, and the privacy
reasoning for `active_users` live at `.claude/skills/canalave-conventions/layer8-data-marts.md`
§`site_daily_stats` — this note carries the settled-vs-open constraints for the build session, per
Doc-Touch Timing.

**Settled constraints (do not revisit without a Stage-4 diagnosis):**
- `site_daily_stats` is an **append-only time-series of ground truth**, upserted
  (`INSERT … ON CONFLICT (stat_date) DO UPDATE`) — **not** a swap-table rebuild like the three
  discovery marts. L1 was previously N/A ("Phase A removed the EF model, raw-SQL mart") — that is
  now reversed: `SiteDailyStat` gets a normal EF entity + `DbSet` + migration (the one documented
  L8 exception — low-volume ground truth with rich time-series reads, unlike the rebuildable marts
  or the SUM-only `daily_story_stats`). The worker still writes only via raw SQL, never through the
  EF change tracker. `DailyStoryStat` (a different, never-built table from the same Gemini
  discussion) stays dropped/never-modeled — do not confuse the two.
- Full column set, `new_`/`total_` split, exclusions (series/vouches/badges/messages/likes), and
  the `stories_approved`/`favorites_added` build-time source verifications: see the skill doc table.
  `total_stories` counts published/visible stories only.
- `active_users`/"last seen" requires new `User.CreatedUtc` + `User.LastActiveUtc` columns and a
  third Signal-Buffering signal (`LastActive*`, `Server/Identity/`) — **authenticated requests
  only**, no tracking cookie, gated for public display by the existing
  `PrivacySettings.ShowActivityStatus`. This is a build prerequisite, not part of the worker itself.
- A **user-facing dashboard is in scope** (`/mod/stats`, mod/admin-gated, per the user — beyond
  MVP "flourishes"), activating L2/L3-Logic/L3.5/L4/L4.5 for this row (previously N/A). L5 stays
  server-rendered (no WASM flip as part of this work).

**Resolved during build:** `favorites_added` is sourced from `UserStoryInteractionDate.FavoriteDate`/
`HiddenFavoriteDate`. `stories_approved` is **dropped** — confirmed no dated column exists anywhere
on the approval path (`ApproveStoryAsync` flips `StoryStatusId` with no timestamp write); adding one
is out of this build's scope. The moderation-health panel's approval signal is `reports_resolved`
only.

**Resolved during build (chart set):** headline totals (users/stories/words); 3 small-multiple
growth line charts (one axis each — users/stories/words differ in scale, per the dataviz skill's
"never dual-axis" rule); a DAU line chart; a 2-series Reports-Filed-vs-Resolved line chart (shared
axis, both are "report counts"); a plain data table for the 12 flow counters — a table was chosen
over an 11-color bar chart per the dataviz skill's "sometimes the answer is not a chart" guidance
(too many disparate categorical counts for a legible fixed-hue-order palette).

**Stage note (WU-SiteDailyStat, 2026-07-11):** L1=5, L2=5, L3-Logic=5, L3.5-Structure=5, L4-Style=3
(functional Tailwind, passes `check-design-tokens.ps1`; not design-reviewed — same convention as
sibling mod pages), L4.5-Browser=5, L5=N/A (server-rendered only, no WASM flip), L6=N/A (the
`stat_date` PK is already the covering index for time-series reads — no additional index needed),
L8=5.

- **Built:** `SiteDailyStat` EF entity/migration + `User.CreatedUtc`/`LastActiveUtc` columns
  (`AddSiteDailyStatAndUserActivityColumns` migration); `UserActivityBuffer`/`Flusher`/
  `FlushWorker` + `ServerUserActivityWriteService` (`Server/Identity/`) — a third Signal-Buffering
  instance; `UserActivityTracker` (non-visual, mounted once in `Routes.razor`, stamps activity on
  circuit start + every navigation for authenticated users only); `SiteDailyStatAggregator` +
  `SiteDailyStatWorker` (`Server/Moderation/`) — one raw `INSERT … ON CONFLICT` per completed UTC
  day, day-boundary comparisons via explicit UTC range parameters (never a `::date` cast, which
  would be session-timezone-dependent); `ISiteDailyStatReadService`/`ServerSiteDailyStatReadService`
  (plain LINQ, since this is the one L8 table with an EF model); `/mod/stats` dashboard
  (`ModStatsPage.razor` + `DailyStatLineChart`/`StatTile`/`ActivityRow`); `ProfileHeaderDto`/
  `ServerUserProfileReadService`/`ProfileBanner` extended with `LastSeenUtc` (gated by
  `PrivacySettings.ShowActivityStatus`, same shape as the existing `Stats` gate); a `/dev/marts/
  site-daily-stat` diagnostic probe.
- **Test tiers:** Unit — `UserActivityBufferTests` (latest-timestamp coalescing/restore, mirrors
  `ViewCountBufferTests`), `SiteDailyStatWorkerTests` (`PreviousCompletedUtcDay`/`MissingDays` pure
  boundary logic, made `public` test seams per the repo's no-`InternalsVisibleTo` convention).
  Integration — `SiteDailyStatAggregatorTests` (one dated event of every counted kind seeded on a
  target day plus one deliberately outside the day's range to prove boundary filtering; every
  column asserted; a second pass proves the upsert **recomputes** rather than accumulates, unlike
  the view-count flusher's `+=`), `UserActivityFlushTests` (buffer→flush→`GREATEST` no-regression,
  mirrors `ViewCountFlushTests`). RazorComponents — **no dedicated test for `ModStatsPage`**: its
  `@code` is a thin init-load (service calls assigned to fields) with no `EventCallback`
  invocations or non-trivial computed state, which the repo's own testing convention says to skip
  ("cover what cannot be verified by reading the file"); sibling mod pages (`ModReportsPage`,
  `ModUsersPage`) — whose `@code` is more complex — carry no RazorComponents test either. The real
  logic (aggregation math) is covered by the Integration tier above.
- **Live browser verification (2026-07-11, server-only path, standing dev DB — not wiped):** the
  migration applied cleanly to 3012 existing stories / 2007 real users (all backfilled to the
  migration's deploy instant, confirming the documented one-time `new_users` deploy-day-spike
  limitation); the worker's bounded startup gap-fill backfilled 30 days unprompted, with real
  varying `new_comments` per day (121–283) proving day-boundary aggregation across genuine history;
  `/mod/stats` loaded and rendered live as AdminUser (mod-gate passed); the
  activity-buffer→flush→`active_users`/"Last seen Jul 11, 2026" loop was confirmed end-to-end on a
  real profile page, for both the owner (AdminUser) and a non-owner viewer (TestUser).
- `dotnet test`: 1421/1421 (524 Unit + 479 RazorComponents + 418 Integration).
