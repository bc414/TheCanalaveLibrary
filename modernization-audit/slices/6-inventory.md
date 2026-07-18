# Slice 6 — Patterns Inventory (Identity, Profiles, Badges, Notifications)

1. **Pagination**
mechanism: offset via `PaginationControls` + a `GetTotalCountAsync` companion; page state in `_page`, server-side `Skip/Take`.
exemplar: `NotificationsPage.razor:125-133` (`TotalCount > PageSize` → `PaginationControls`); `ServerNotificationReadService.cs:143,188` (`skip = (page-1)*pageSize`).
deviations: Profile story/blog tabs delegate paging to `GetListingsAsync`/`GetByAuthorAsync` (S2/S7 read services); profile Series/TagSelections/Lists tabs are full-list, no pagination (deliberate — small sets).

2. **DTO mapping**
mechanism: single-`.Select` projection straight to record DTOs; two-step (materialize page → batch-enrich) only for notifications' polymorphic target.
exemplar: `ServerUserProfileReadService.cs:29-46` anonymous-then-DTO; `ServerNotificationReadService.cs:135-221` two-pass.
deviations: `GetProfileHeaderAsync` projects `PrivacySettings` (a JSON complex type) into the anonymous row then reads its fields C#-side — fine (owned JSON). No tuple returns except `(Items,Total)` from composed read services.

3. **Error surfacing**
mechanism: services throw `InvalidOperationException` for auth/not-found; endpoints wrap in `EndpointHelpers.ExecuteWriteAsync` (→ 401); UI reference = `ExceptionPresenter` + Toast/InlineAlert.
exemplar: `ProfilePage.razor:394-397` (`ExceptionPresenter.GetUserMessage` + `Toasts.Show(...Danger)`).
deviations: **MA-603** `SettingsPage` hand-rolls alert `<div>`s + raw `ex.Message`; **MA-611** business-rule `InvalidOperationException` mis-maps to 401 (Badge/UserSettings); `NotificationSettingsPage` has no error channel on per-row save.

4. **Form patterns**
mechanism: parameter-driven leaf sub-forms (no `@inject`), `_seeded`-guarded `OnParametersSet` copies `Initial*`→private edit fields, `HandleSave` builds a DTO and raises `OnSave` EventCallback; page owns all service calls + `RunWithFeedbackAsync`. Enum/bool `<select>` uses the cast-value + `@onchange` TryParse idiom.
exemplar: `ReaderSettingsForm.razor:137-179`; `SettingsPage.razor:238-257` (`RunWithFeedbackAsync`).
deviations: **MA-609** `AppearanceSettingsForm` uses a `_themeId==0` sentinel guard + 4 scalar `Initial*` params instead of `_seeded`+`Initial` DTO. `NotificationSettingsPage` breaks the pattern deliberately (per-row immediate `@onchange` save, optimistic, no EditForm/Save button — WU33).

5. **Flyout/overlay mechanics**
mechanism: `NotificationBellInner` uses the UserCard-caret pattern — relative container + toggle button → `absolute top-full` panel, dismissed by a `fixed inset-0 z-(--z-dropdown)` catcher div with `@onclick="Close"`. Notification category view uses `<details>` disclosures.
exemplar: `NotificationBellInner.razor:38-75`.
deviations: none — z-tokens (`z-(--z-dropdown)`) used correctly; no fixed-modal misuse.

6. **Optimistic updates & debounce**
mechanism: mark-as-read flips the local DTO (`x with { IsRead = true }`) + decrements the badge before awaiting the service; no timers/debounce anywhere in slice.
exemplar: `NotificationBellInner.razor:105-117`; `NotificationsPage.razor:210-222`.
deviations: `NotificationSettingsPage` optimistic per-row update has no rollback on save failure (Tier 3). No H-10 debounce-loss surface exists here.

7. **Disposal & lifecycle**
mechanism: no `IDisposable`/CTS/JS-interop subscriptions in slice UI (bell has no timer/live push — "count refreshes on mount/navigation"). `[PersistentState]` on public props for the prerender→interactive handoff, with `??=` restore-or-fetch.
exemplar: `NotificationBellInner.razor:89-99`; `SettingsPage.razor:99-103,122-156`.
deviations: none. Signal-buffer workers (`UserActivityFlushWorker`, `NotificationCleanupWorker`, `UserStatRecalculationWorker`) follow the standard `BackgroundService`+drain-after-cancel shape; `TestAppFactory` removes them.

8. **Query shape**
mechanism: factory-per-method `await using` context; projection (never entity materialize-then-map) except badge `SetDisplayOrder` which tracks ≤10 rows deliberately; notification enrichment is the two-pass batch (one query per `RelatedEntityKind`, never N+1 — **verified**).
exemplar: `ServerNotificationReadService.cs:277-351` (`BatchLoadEntitiesAsync`, group-by-kind).
deviations: `GetProfileHeaderAsync` splits vouches into a second query to avoid badge fan-out duplication (`:63-64`) — a documented, correct choice.

9. **Write-method skeleton**
mechanism: auth guard (`RequireCurrentUserId`/`RequireAuthenticatedUser` → resolve from `IActiveUserContext`, throw when anonymous) → load/`ExecuteUpdate` → sanitize (rich text) → `SaveChangesAsync`. Self-scoped services take **no** userId param (§3.5).
exemplar: `ServerUserSettingsService.cs:28-33,100-129` (guard + `sanitizer.Sanitize` bio before persist).
deviations: **MA-601** `ServerBadgeWriteService` takes a trusted `userId` param with no guard (the anti-pattern); `ServerNotificationWriteService` create-core is correctly self-source-scoped.

10. **Endpoint & client shape**
mechanism: `MapGroup("/api/{feature}")` + `.RequireAuthorization()`; writes via `ExecuteWriteAsync`; client impls inherit read→write, per-class `ThrowIfWriteFailedAsync`, nullable reads via `GetNullableFromJsonAsync`.
exemplar: `UserSettingsEndpoints.cs:40-108` (RequireAuthorization on every route, no userId over HTTP); `NotificationEndpoints.cs:11-19` (deliberately does NOT map generation methods).
deviations: **MA-601** `BadgeEndpoints` + **MA-602** `UserProfileEndpoints` trust client-supplied `userId`/`includePrivate` — broken access control; `UserProfileEndpoints` also omits `RequireAuthorization`.

11. **Sanitization & derived fields**
mechanism: profile bio (`EditorView` rich text) is `sanitizer.Sanitize`d in the write service before persist; `RichTextView` renders the trusted stored HTML via `MarkupString` with no re-sanitize.
exemplar: `ServerUserSettingsService.cs:115,126`; `ProfileDesktop.razor:54`/`ProfileMobile.razor:70` `<RichTextView HtmlContent="@BioHtml"/>`.
deviations: none — **MA-201 class is clean in this slice** (write sanitizes, display trusts). Notifications render plain composed text (`NotificationPresenter`), no user HTML.

12. **Notification triggering**
mechanism: semantic `NotifyNew*Async` wrappers over one private `CreateCoreAsync` owning drop-self + within-batch + cross-existing dedup; best-effort post-commit try/catch in the calling feature service.
exemplar: `ServerNotificationWriteService.cs:282-334` (create-core invariants **verified present**).
deviations: none. Generation methods are server-internal only (not HTTP-mapped) — the privilege-escalation guard Badges lacks.

13. **Counter updates**
mechanism: real-time `UserStat` increments are same-transaction `ExecuteUpdateAsync` in other slices' write services; F58 `UserStatRecalculator` reconciles via set-based `UPDATE…FROM` with `IS DISTINCT FROM` (drift-corrected count) + a zero-unmatched pass; insert-missing-row first (the real user-stat populator).
exemplar: `UserStatRecalculator.cs:14-49` (doc) + `ServerBadgeWriteService.cs:36-39` (`MaxAsync` DisplayOrder slot).
deviations: none in slice (the in-slice worker touches no auth — pure `ApplicationDbContext`, confirmed).

14. **Test idioms**
mechanism: Integration (Testcontainers Postgres, Respawn) for services/workers, seeding via `IntegrationTestBase`/`UserManager`; Unit for pure composers (`NotificationPresenter`, `EmailBodies`, `UserActivityBuffer`); RazorComponents (bUnit) for parameter-driven forms/dispatcher.
exemplar: `UserStatRecalculatorTests` (11 tests, mutation-sanity per counter family); `ProfilePageTests.TabSwitch_OnSameInstance_ReloadsTabPayload`.
deviations: `FakeNotificationWriteService` is absent from the fakes catalog, so `NotificationBell`/pages/settings-page have **no RazorComponents coverage** (audit/Notifications.md flags this as WU-NotifEmail follow-up) — the anonymous-bell crash (2026-07-13) had no test that would have caught it. Auth-cookie/claims + SecurityStampValidator timing stay in the manual band (testing.md) — not a gap.

15. **Code economy** (dim 15 — fixed feature set)

**(a) Per-cluster product LOC + pattern-tax**
| Cluster | Product LOC (svc+UI+endpoints) | Notes |
|---|---|---|
| Identity (svc+plumbing, excl. Pages) | ~950 | claims factory / sign-in mgr / active-user context are small + load-bearing |
| Identity **Pages (scaffold)** | **~3052** | biggest single block; ~1325 is unbuilt-feature scaffold (MA-610) |
| Profiles (svc+endpoints) | ~695 | UserStatRecalculator 289 is the mass |
| Profiles UI (12 razor) | ~2130 | settings-form family + ProfileDesktop/Mobile pair |
| Badges | ~200 svc + 156 UI | thin |
| Notifications (svc+endpoints) | ~866 | two-pass enrichment carries the read service |
| Notifications UI (7) | ~856 | |

**(b) Compression candidates** (LOC saved · sites collapsed · new machinery)
| Candidate | Saved | Sites | Inherits | Class |
|---|---|---|---|---|
| Extract `SettingsCard` container + `SettingsSaveButton` + labeled-field atoms from the 6 settings forms | ~150-200 | 6 forms | one atom layer over utility-first markup | **trade** |
| Single `ProfileBodyModel` record for the ~20-param block passed identically to `ProfileDesktop` + `ProfileMobile` | ~40 (halves a 20-line param block passed twice) | 2 call sites (`ProfilePage:57-104`) | one record DTO | **trade** |
| Prune unbuilt-feature Identity scaffold pages (2FA/passkey/external-login) + Manage nav entries | ~1325 | 11 files | loses the framework flow for a future 2FA feature | **trade / product decision (MA-610)** |

**(c) Near-identical pairs**
- `ProfileDesktop.razor` (303) / `ProfileMobile.razor` (345): genuinely structurally different (horizontal tab bar + right filter sidebar vs. `<details>` tab dropdown + filter overlay — the Bookshelves idiom) → **false economy to merge** (honors "separate only when structurally different"). Their shared cost is the parameter list, not the body — see (b).
- The 6 settings forms (`Reader/Privacy/Author/Appearance/Profile/Badge`): each edits a distinct field set → correctly separate. The repeated container/Save-button/field Tailwind recipe is the tax, not the forms.
- `ServerActiveUserContext` / `WasmActiveUserContext`: deliberate twins (identical claim reads + anonymous defaults); the drift risk is unenforced (calibration B-flag candidate — analyzer/test gap), not a merge candidate.

**(d) Mechanical repetition w/ fixable root cause**
- Every settings `<select>`/`<input>` repeats the full `rounded-md border border-(--color-border) bg-(--color-surface) px-3 py-2 … focus:ring-2 focus:ring-(--color-action-ink)` string (~15 occurrences across the family) — root cause is the utility-first-no-component-library axiom; a shared field atom is the only DRY lever (see (b), trade).

**(e) False economies considered & rejected**
- Merging Desktop/Mobile profile components (structurally different — (c)).
- Merging Load vs. Save tag-selection surfaces (out of slice, but the same "opposite operations" logic applies to why the 6 settings forms stay separate).
- A generic notification `CreateAsync` (would bypass drop-self/dedup — layer2 rejects it; the semantic-wrapper repetition is disciplined, not tax).
- Mapping the ~20 `Notify*Async` generation methods as endpoints to "complete" the API surface — deliberately NOT done (privilege-escalation surface); Badges' failure to follow this is MA-601.
