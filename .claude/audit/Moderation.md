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
design-reviewed). L5=2 (WASM client service + API endpoint deferred — no `ClientModerationWriteService` yet).
L6=5 (composite index `ix_reports_reported_entity_type_reported_entity_id` added in migration
`20260625140459_WU34_Moderation`).

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
  `SuspendedUntilUtc` set on `User`. Status + notification + `Report` record set together. Login-blocking
  enforcement is a deferred follow-up WU (see `workplan.md` note after WU39).
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

## Feature 53 — Story Import & Verification (→ WU39, deps WU34)

**Note:** Story import and import verification are **split into WU39** (after WU34). `/mod/submissions`'s
import-verification tab shell lands in WU34; the full import workflow and verification logic land in WU39.
`StoryImport` stays in `Core/Models/` until WU39, which will relocate it to `Core/Moderation/` or
`Core/Stories/`.

**WU39 scope (not settled until WU39 opusplan):**
- Author-facing import submission: `SourcePlatform`/`SourceUrl` + original dates, create `StoryImport` row,
  route story into `PendingApproval`.
- Mod verification tab: confirm account holder is original author (MVP = manual review of two-way link;
  `StoryImport.VerificationStatus` records outcome).
- Open: whether `OriginalPublishedDate`/`OriginalLastUpdatedDate` need a migration (not in current
  `StoryImport` model); two-way link mechanism (site publishes a verifiable token the author puts on the
  source page, vs. purely manual review).

**Stages:** L1 — Stage 5. L2 — Stage 2. L3/L3.5 — Stage 2. L4 — Stage 1. L5 — N/A.

## Feature 62 — SiteDailyStat Worker (below the line)
- **L1 — N/A** (Phase A removed the EF model; `site_daily_stats` is a raw-SQL mart — divergence resolved,
  matching `AlsoFavoritedScore`/`AlsoRecommendedScore`/`UserStoryTreeSearchEntry`; schema preserved in
  `Discovery.md`'s Layer-8 implementation notes). `DailyStoryStat` was dropped entirely, not modeled at all.
- **L8 — Stage 2** (daily aggregation; no user-facing UI in MVP; naturally sorts late — nothing to
  aggregate yet). All other layers **N/A**.
