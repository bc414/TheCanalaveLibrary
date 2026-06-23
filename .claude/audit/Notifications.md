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

## Feature 41 — Notification Generation

- **L1 — Stage 5.** `Notification` + the fully-seeded type/category tables. Sound. **L6 — Stage 2**
  (`(recipient_user_id, is_read, date_created)` indexes pending, deferred to Post-MVP L6 batch).

- **L2 — Stage 2 → 5 (WU22).** Settled constraints (do not revisit):
  - **Mechanism:** direct injected call — `INotificationWriteService` injected into feature write
    services; called via a semantic per-event method after the primary `SaveChangesAsync` (best-effort
    post-commit, `try/catch`-with-log). See `cross-cutting.md` "Notification Creation" and
    `layer2-services.md` "Notification Generation."
  - **API:** semantic per-event methods only (`NotifyNewFollowerAsync`, `NotifyNewChapterAsync`, …);
    no public generic `CreateAsync` escape hatch. Methods funnel through one private create-core
    (drop-self, dedup, bulk-insert, single `SaveChangesAsync`).
  - **In-app filtering:** always-on — the create-core never gates on `UserNotificationSetting`. The only
    in-app gate is relationship-level: fan-out follow-alert methods check `FollowedUser.ReceiveAlerts`.
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
  (fan-out to `ReceiveAlerts` followers, with WU17/chapter-publish flow); `NotifyNewCommentAsync` /
  `NotifyNewRecommendationAsync` / etc. (with WU19/20/29). The create-core and DAG pattern are built
  now; each deferred method is a thin wrapper addition.

## Feature 42 — Notification Display

- **L1 — Stage 5.** **L2 — Stage 2 → 5 (WU22).** Settled constraints:
  - `INotificationReadService`: `GetUnreadCountAsync()`, `GetNotificationsAsync(page, pageSize)`. All
    self-scoped via `IActiveUserContext` (the whole surface is "my notifications").
  - `GetNotificationsAsync` returns `NotificationDto` with effective `Collapsed` (type default
    overridden by the user's sparse setting when a row exists).
  - The bell in the layout injects `INotificationReadService` directly — legitimate cross-cutting
    injection (§cross-cutting.md).
  - Mark-as-read mutations (`MarkAsReadAsync`, `MarkAllAsReadAsync`) live on `INotificationWriteService`
    (it inherits from `INotificationReadService`).
  - **L3-Logic — Stage 2** (the notification bell in the layout; panel grouped by `NotificationCategory`,
    `DefaultCollapsed`/user-override per type). **L3.5-Structure — Stage 2** (panel + flyout preview).
    **L4 — Stage 1. L5 — Stage 2.** All deferred to WU33.
  - **L6 — Stage 2** (index `(recipient_user_id, is_read, date_created DESC)` pending L6 batch).

- **WU22 Stage-5 note (L2 only, 2026-06-23):** `INotificationReadService.GetUnreadCountAsync()`,
  `GetNotificationsAsync(page, pageSize)` (LEFT JOIN UserNotificationSettings → effective Collapsed),
  and `GetSettingsAsync()` (LEFT JOIN UserNotificationSettings → effective EmailEnabled/Collapsed,
  IsDefault flag) are all in `ServerNotificationReadService`. `MarkAsReadAsync` /
  `MarkAllAsReadAsync` are in `ServerNotificationWriteService`. Covered by Integration tier (see
  Feature 41 Stage-5 note). L3/L3.5/L4/L5 remain Stage 2 — deferred to WU33.

## Feature 43 — Notification Settings

- **L1 — Stage 5** (`UserNotificationSetting` sparse-override; `EmailEnabled`/`Collapsed` — see Shared
  Context correction; `NULL` for either = use default, §5.18 as corrected). **L2 — Stage 2 → 5 (WU22).**
  Settled constraints:
  - `GetSettingsAsync()` returns `NotificationSettingDto[]` grouped by category — LEFT JOIN
    `UserNotificationSetting` onto `NotificationType`; NULL ⇒ type defaults (`DefaultEmailEnabled`,
    `DefaultCollapsed`). Includes `IsDefault` flag so the UI knows which rows are overridden.
  - `SetSettingAsync(NotificationTypeEnum type, bool emailEnabled, bool collapsed)` — sparse: upsert
    the override row when either field differs from the type defaults; delete the row when both match
    the defaults (returning to default requires no stored row). Self-scoped via `IActiveUserContext`.
  - **L3/L3.5 — Stage 2.** Settings page driven by DB data. **L4 — Stage 1. L5 — Stage 2.** All
    deferred to WU33.

- **WU22 Stage-5 note (L2 only, 2026-06-23):** `SetSettingAsync` sparse-model — upserts the override
  row when values differ from type defaults; deletes it when both match defaults (returning to NULL =
  "use default"). `GetSettingsAsync` LEFT-JOINs onto types; NULL → IsDefault = true. Covered by
  Integration tier (see Feature 41 Stage-5 note). L3/L3.5/L4 remain Stage 2 — deferred to WU33.

## Feature 57 — Notification Cleanup Worker

- **L2 — Stage 2.** `IHostedService` deleting read notifications older than 60 days. Pure background
  computation (Layer 2 *is* the worker). Nothing to clean until data ages — naturally sorts late. All
  other layers **N/A**.
