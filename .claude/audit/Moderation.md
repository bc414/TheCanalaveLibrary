# Audit — Moderation/

**Features:** 46 (reporting), 47 (queue & actions), 48 (approval workflow), 53 (import & verification),
62 (SiteDailyStat worker). Routes `/mod/reports`, `/mod/submissions`, `/mod/users`. Desktop-only, **no
dispatcher pattern** (§3.10).

## Shared Context
**Entities (Core/Models/):** `Report` (`ReportReasonId`/`ReportStatusId` Restrict, polymorphic
`ReportedEntityType`→short + `ReportedEntityId`, `DateReported` default), `ReportReason`/`ReportStatus`
(seeded), `StoryImport` (unique `StoryId`, unique `SourceUrl`), `SiteDailyStat` (PK `StatDate`),
`DailyStoryStat` (PK `(StoryId,StatDate)`). `Story.ActiveReportCount` for auto-flagging. **No services or
components built.**

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
- **L1 — Stage 4.** `SiteDailyStat` is EF-modeled (`DbSet`, `HasKey(StatDate)`) — but `layer8-data-marts.md`
  lists it among data-mart tables that should have **no EF model class**. Same divergence as features
  59–61. (Note: `layer1-data-model.md`'s migration-exclusion list names only the three Discovery marts,
  not `SiteDailyStat`, so the skill is mildly self-inconsistent here — worth a user decision on whether
  `SiteDailyStat`/`DailyStoryStat` are EF-modeled aggregates or raw-SQL marts.)
- **L8 — Stage 2** (daily aggregation; no user-facing UI in MVP; naturally sorts late — nothing to
  aggregate yet). All other layers **N/A**.
