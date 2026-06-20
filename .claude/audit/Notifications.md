# Audit — Notifications/

**Features:** 41 (generation), 42 (display), 43 (settings), 57 (cleanup worker). Cross-cutting generation
pattern defined here (§9.4). Route `/notifications`.

## Shared Context
**Entities (Core/Models/):** `Notification` (`RecipientUserId` Cascade, `SourceUserId` Restrict —
polymorphic `RelatedEntityId`, `DateCreated` default), `NotificationCategory` (9, seeded),
`NotificationType` (~35, seeded with gap-based numbering, `DefaultEmailEnabled`/`DefaultCollapsed`),
`UserNotificationSetting` (sparse override, PK `(UserId,NotificationTypeId)`). **No services or components
built.** The seed data here is one of the most complete parts of the model.

## Feature 41 — Notification Generation
- **L1 — Stage 5.** `Notification` + the fully-seeded type/category tables. Sound. **L2 — Stage 2** — the
  cross-cutting generation pattern (§9.4) on high-effort events (NOT likes) is unbuilt. **L3/L3.5/L4/L5 —
  N/A** (generation is server-side, no UI). **L6 — Stage 2** (`(recipient_user_id, is_read, date_created)`
  indexes pending).

## Feature 42 — Notification Display
- **L1 — Stage 5.** **L2 — Stage 2** (`INotificationReadService`). **L3-Logic — Stage 2** — the
  notification bell in the layout injects `INotificationReadService` directly (legitimate cross-cutting
  injection, §3.19); panel grouped by `NotificationCategory`, `DefaultCollapsed` per type, mark-as-read.
  **L3.5-Structure — Stage 2** (panel + flyout preview). **L4 — Stage 1. L5 — Stage 2. L6 — Stage 2.**

## Feature 43 — Notification Settings
- **L1 — Stage 5** (`UserNotificationSetting` sparse-override; NULL = use default, §5.18). **L2 — Stage 2.**
  **L3/L3.5 — Stage 2** (settings page driven by DB data). **L4 — Stage 1. L5 — Stage 2.**

## Feature 57 — Notification Cleanup Worker
- **L2 — Stage 2.** `IHostedService` deleting read notifications older than 60 days. Pure background
  computation (Layer 2 *is* the worker). Nothing to clean until data ages — naturally sorts late. All
  other layers **N/A**.
