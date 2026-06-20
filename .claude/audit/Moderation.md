# Audit — Moderation/

**Features:** 46 (reporting), 47 (queue & actions), 48 (approval workflow), 53 (import & verification),
62 (SiteDailyStat worker). Routes `/mod/reports`, `/mod/submissions`, `/mod/users`. Desktop-only, **no
dispatcher pattern** (§3.10).

## Shared Context
**Entities (Core/Models/):** `Report` (`ReportReasonId`/`ReportStatusId` Restrict, polymorphic
`ReportedEntityType`→short + `ReportedEntityId`, `DateReported` default), `ReportReason`/`ReportStatus`
(seeded), `StoryImport` (unique `StoryId`, unique `SourceUrl`). `Story.ActiveReportCount` for
auto-flagging. **No services or components built.**

**Not EF entities:** `SiteDailyStat` (PK `StatDate`) is a raw-SQL data mart (no `DbSet`, no migration —
see Feature 62 below); `DailyStoryStat` was dropped entirely, never modeled.

## Feature 46 — Content Reporting
- **L1 — Stage 5** (`Report` + seeded reason/status lookups). **L2 — Stage 2.** **L3/L3.5 — Stage 2.
  L4 — Stage 1. L5 — Stage 2** (user-facing report submission can be WASM). **L6 — Stage 2.**

## Feature 47 — Moderation Queue & Actions
- **L1 — Stage 5** (`ActiveReportCount` auto-flagging). **L2 — Stage 2.** **L3-Logic — Stage 2** (review,
  content removal, rejection, account actions). **L3.5-Structure — Stage 2** (desktop-only mod queue, no
  dispatcher). **L4 — Stage 1. L5 — N/A** (desktop-only moderator surface, server-rendered). **L6 —
  Stage 2.**

## Feature 48 — Story Approval Workflow
- **L1 — Stage 5** (`StoryStatus` lifecycle Draft→PendingApproval→PostApprovalStatus;
  `StoryDetail.PostApprovalStatus`). **L2 — Stage 2** (approve/reject, import verification). **L3/L3.5 —
  Stage 2. L4 — Stage 1. L5 — N/A.**

## Feature 53 — Story Import & Verification
- **L1 — Stage 5** (`StoryImport`, 1-to-1 with Story, unique source URL). **L2 — Stage 2** (two-way link
  verification; MVP manual mod verification). **L3/L3.5 — Stage 2. L4 — Stage 1. L5 — N/A.**

## Feature 62 — SiteDailyStat Worker (below the line)
- **L1 — N/A** (Phase A removed the EF model; `site_daily_stats` is a raw-SQL mart — divergence resolved,
  matching `AlsoFavoritedScore`/`AlsoRecommendedScore`/`UserStoryTreeSearchEntry`; schema preserved in
  `Discovery.md`'s Layer-8 implementation notes). `DailyStoryStat` was dropped entirely, not modeled at all.
- **L8 — Stage 2** (daily aggregation; no user-facing UI in MVP; naturally sorts late — nothing to
  aggregate yet). All other layers **N/A**.
