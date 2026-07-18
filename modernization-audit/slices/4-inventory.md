# Slice 4 — Patterns Inventory (Discovery & Interaction)

15 dimensions per `dimensions.md`. `mechanism` / `exemplar` / `deviations`.

## 1. Pagination
mechanism: offset (`Skip/Take` on scalar id sets) everywhere; `StoryDeck` + embedded `PaginationControls` for sorted decks; random `/discover` suppresses pagination by passing `TotalCount = PageSize = Items.Count` → `TotalPages=1`; manual-tree sections page per-section via a "Show more" button + server-side `TotalCount`; co-occurrence/related uses a fixed `take=6` grid (no pager).
exemplar: `CustomListPage.razor:217-222` (`_storyIds.Skip((_page-1)*PageSize).Take(PageSize)` → order-preserving hydrate).
deviations: three distinct "no real pager" idioms (random Give-me-more, Show-more per section, fixed take-6) — each intentional per its surface, not drift.

## 2. DTO mapping
mechanism: two-step (filtered `IQueryable` → scalar id page → `GetListingsByIdsAsync` hydrate) for narrowed story surfaces; direct `.Select(new …Dto)` projection for tag/list/selection reads; `record`/positional DTOs; reorder-to-input-order convention on batch id reads.
exemplar: `ServerCustomListReadService.cs:77-97` (filtered `Stories` join → ordered id array).
deviations: `ServerManualTreeSearchReadService` inlines the `RecommendationDto`/`UserCardDto` projection at each `Select` (EF can't translate a shared projection method) and documents it must stay shape-identical to `ServerRecommendationReadService` (a deliberate, doc'd near-dup — dim 15).

## 3. Error surfacing
mechanism: typed domain exceptions (`TagValidationException`, `CustomListValidationException`, `SavedTagSelectionValidationException`) thrown in the service, translated to 400 `Results.Problem(detail)` at the endpoint, re-thrown client-side; UI catches via `ExceptionPresenter.IsUserFacing` → `InlineAlert`.
exemplar: `CustomListPage.razor:114-117` (`catch when IsUserFacing → GetUserMessages → InlineAlert`).
deviations: **MA-405** — `TagEditorForm.razor:105-108` hand-rolls `<p role="alert">` instead of `InlineAlert`.

## 4. Form patterns
mechanism: `@code`-state (no ViewModel) for the filter/toggle coordination composites (`ResultsFilterPanel`, `TagFilter`, `UserStoryInteractionFilter`, `TreeSearchControls`) per `layer3-logic.md` "selection state is `@code`"; `EditForm`+model only where DataAnnotations apply (`TagEditorForm._model`); enum `<select>` uses the numeric-value + `short.TryParse` block idiom (`ResultsFilterPanel:62-67`) OR the `@bind`+name-value idiom (`TagEditorForm:24-34`) — never mixed.
exemplar: `TreeSearchControls.razor:101-143` (buffered `@code` axis state, one emit on Apply).
deviations: none — both sanctioned enum-select idioms present, each coherent.

## 5. Flyout/overlay mechanics
mechanism: three shells — (a) centered modal `fixed inset-0 z-(--z-modal) bg-(--color-backdrop)` + `@onclick:stopPropagation` panel (`SavedTagSelectionLoadFlyoutInner`, `TagDirectoryDesktop` editor); (b) right slide-in drawer `fixed inset-y-0 right-0 z-(--z-drawer)` (the three mobile filter panels); (c) inline `absolute top-full z-(--z-dropdown)` caret/⋯ menus (`AddToCustomListMenu`, the ⋯ row menu). All z-tokens, all `renders-nothing-when-closed`.
exemplar: `BookshelvesMobile.razor:81-109`.
deviations: **MA-406** — the drawer shell (b) is verbatim-triplicated across SearchMobile/BookshelvesMobile/TreeSearchMobile.

## 6. Optimistic updates & debounce
mechanism: `UserStoryInteractionPanel` owns the sole 2 s debounce (`CancellationTokenSource` + `Task.Delay` in the coordination composite, optimistic local `_localState` flip before flush) per `layer3-logic.md`; `AddToCustomListMenu` flips membership optimistically with no refetch (idempotent write). Typeahead debounce (`CanalaveTypeahead` 300 ms) is a separate concern.
exemplar: `UserStoryInteractionPanel.razor:94-126`.
deviations: **MA-401** — dispose during the debounce window DROPS the pending flush.

## 7. Disposal & lifecycle
mechanism: manual-tree JS interop (pan/zoom/panel-drag) attaches listeners **to the DOM elements themselves** and stores state in a `WeakMap`/`WeakSet` (`manual-tree-search.js:10-11`), so no `IDisposable`/`dispose`-interop is needed — a deliberate, sound alternative to the `StoryPage`/`ChapterReadingPage` `JS…dispose()` pattern; localStorage via a typed `ManualTreeStore` wrapper (`DraftStore` precedent). Interop attach in `OnAfterRenderAsync(firstRender)`.
exemplar: `ManualTreeCanvas.razor:103-109` (attach in first-render; no teardown, correct).
deviations: `UserStoryInteractionPanel` is the one `IDisposable` and its `Dispose` mishandles the pending write (MA-401).

## 8. Query shape
mechanism: `ApplyFilters`-style shared composition (S2's `ServerStoryReadService`); raw-ADO recursive CTE over the narrow edge-list mart with `edge_type = ANY(@edges)` + LATERAL fan-out `LIMIT` + PG14 `CYCLE` path; presentation-join filtering (rating + §8.7 exclusions applied AFTER traversal, never inside the recursive term); co-occurrence self-join marts read directly (score DESC, covering index).
exemplar: `ServerTreeSearchReadService.cs:65-115` (rCTE) + `DiscoveryMartSchema.cs:62-98` (edge build).
deviations: `TraverseAsync` and `GetRawReachedAsync` share ~90% identical recursive-term SQL, **deliberately duplicated** ("kept in sync manually") to leave the frozen/tested query untouched — a doc'd false-economy-rejected (dim 15).

## 9. Write-method skeleton
mechanism: auth guard (`RequireAuthenticatedUser`/`RequireMod`/self-scope) → validate (`*Validations` + `*ValidationException`) → existence/owner check on unfiltered `writeDb` → construct with `DateTime.UtcNow` → `SaveChangesAsync` → (USI only) transition-delta counter `ExecuteUpdateAsync`. No sanitize step (no rich text). No rate-limit calls (interactions/tag-writes/list-writes are deliberately unthrottled per `security.md`).
exemplar: `ServerCustomListWriteService.cs:22-43` (create).
deviations: **MA-409** — USI counter uses `(int?)` FK projection vs the anonymous-type convention (benign).

## 10. Endpoint & client shape
mechanism: `Map{Feature}Endpoints` route groups; writes wrapped in `EndpointHelpers.ExecuteWriteAsync` (exception→status); bodied `Results.Problem`/`Results.Json(nullable)` (never body-less, per the re-execute trap); client impls mechanically uniform (`protected Http`, per-class `ThrowIfWriteFailedAsync` switch, `GetNullableFromJsonAsync` for `T?`); complex reads POST, scalar/`int[]` reads GET.
exemplar: `CustomListEndpoints.cs` + `ClientCustomListWriteService.cs:70-91`.
deviations: **MA-407** — `TagEndpoints` uses a private `ExecuteWriteAsync` copy instead of the shared helper.

## 11. Sanitization & derived fields
mechanism: **none needed** — no `EditorView` output persists in this slice; all user text is bounded plain text (trim-on-save). Derived fields: USI date-partition stamps (`FavoriteDate ?? now`); counter deltas.
exemplar: `ServerSavedTagSelectionWriteService.cs:148-149` (`NormalizeDescription` = trim, no sanitize).
deviations: none — MA-201 class clean by design.

## 12. Notification triggering
mechanism: **deliberately none** — CustomList adds are silent (settled 2026-07-13), USI/tag/list writes emit no notifications.
exemplar: `ServerCustomListWriteService.cs` (no `INotificationWriteService` dep).
deviations: none observed.

## 13. Counter updates
mechanism: transition-delta (`ExecuteUpdateAsync(SetProperty(x => x.C + delta))`, delta = `willBe != was ? ±1 : skip`) for the four USI-derived UserStats counters; no tracked `++`.
exemplar: `ServerUserStoryInteractionWriteService.cs:105-122`.
deviations: none — textbook.

## 14. Test idioms
mechanism: `IntegrationTestBase` + `SeedUserAsync`/`SeedStoryAsync` (GUID-suffixed), FK-parents documented in the class XML-doc, reject-at-limit called the natural N times (Respawn clean count), absolute assertions, `SetActiveUser`/`FakeActiveUserContext.Anonymous()` for auth branches; rCTE/mart = Integration, path-parser/layout/validations = Unit, component `@code` = RazorComponents with `aria-label`/markup-string selectors + shared `Fake*` service files.
exemplar: `CustomListServiceTests.cs:82-91` (natural-count cap) + `:29-34` (shared seeding in `InitializeAsync`).
deviations: none observed — high quality throughout.

## 15. Code economy (FIXED feature set)
**Per-cluster (product LOC, approx):** Discovery ~4.0k (heaviest — 3 raw-SQL services + 2 mart files + 6 tree/manual UI composites + ExploreTab 501 / DeepDiveTab 334), Tags ~2.0k, UserStoryInteractions ~0.9k, CustomLists ~0.9k. Pattern-tax share is low: endpoints are thin pass-throughs, client impls mechanically uniform, DTOs partition-anchored.

**Compression candidates:**
| Candidate | LOC saved / sites | Machinery cost | Class |
|---|---|---|---|
| Mobile `FilterDrawer` shell (MA-406) | ~25-30 × 3 sites | one composite w/ ChildContent+Title | **trade** (leaning win — 3rd-consumer threshold met) |
| `TagEndpoints` local `ExecuteWriteAsync` → shared (MA-407) | ~24 × 1 | none (helper exists) | **pure win** if helper covers `TagValidationException` |
| Batch `GetPublicSelectionsByUserAsync` (MA-408) | N+1 → 1 query | a join | **trade** (low volume) |

**Desktop/Mobile pair verdicts (the headline):**
- **Search D/M (85 vs 124):** shells genuinely differ (sidebar aside vs slide-in drawer) — separation justified. BUT the `StoryDeck` random/sorted results block (~30 lines) and the full `@code` param list (~18 lines) are **verbatim-duplicated**. → **trade** (split is right; the results region + `@code` are device-agnostic tax).
- **Bookshelves D/M (94 vs 146):** shells differ (horizontal tab bar vs `<details>` dropdown) — justified. `@code` (params + `_availableSorts` + `EmptyMessage` + My-Lists cross-link) duplicated; mobile filter overlay duplicates SearchMobile's. → **trade**.
- **TreeSearch D/M (99 vs 132):** shells differ (aside vs drawer) — justified. `StoryDeck`+`CardOverlay`+badge block AND all six `@code` helpers (`StoryItems`/`ItemCount`/`DegreeFor`/`PathFor`/`HopsFor`/`IsOwnRootStory`) **verbatim-duplicated** (~25 lines). → **trade**.
- Common thread: all three pairs split at the *filter-surface* granularity (correctly device-specific) but drag along a device-**agnostic** results-region + `@code` block as copy. A shared results-region + the MA-406 `FilterDrawer` would collapse the tax while keeping the small genuine device differences. Brian decides.

**Near-duplicate DTOs:** the many filter/tree/co-occurrence DTOs (`StoryFilterDto`, `TreeSearchRequest`, `TreeSearchControlsSelection`, `TagFilterSelection`, `CoOccurrenceRequest`, `ManualTree*Request`) are each partition/axis-anchored with distinct fields — NOT near-dups; the split is the Source×Filter×Sort model, disciplined. The `RecommendationDto`/`UserCardDto` projections transcribed in `ServerManualTreeSearchReadService` ARE a near-dup of `ServerRecommendationReadService`, forced by EF's no-shared-projection limit (doc'd).

**False economies considered & rejected:**
- The dual rCTE SQL (`TraverseAsync` / `GetRawReachedAsync`) — deliberately NOT refactored to one, to keep the frozen/tested traversal untouched (`ServerTreeSearchReadService.cs:270-280`). Correct.
- Two request types (`StoryNeighborsRequest`/`UserNeighborsRequest`) instead of one flag-carrying DTO — the type system enforces traversal direction at the boundary (settled WU40). Correct.
- The 11 boolean `UserStoryInteraction` columns are NOT collapsible to an enum/junction (settled axiom 3 — the wide table IS the bookshelf-tab index shape).
