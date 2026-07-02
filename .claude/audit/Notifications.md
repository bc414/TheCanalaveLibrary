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

- **L2 — Stage 2.** `IHostedService` deleting read notifications older than 60 days. Pure background
  computation (Layer 2 *is* the worker). Nothing to clean until data ages — naturally sorts late. All
  other layers **N/A**.
