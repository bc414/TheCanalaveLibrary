# Slice 4 — Discovery & Interaction (Features 11-17, 31-34, 51, 59-61)

Audited 2026-07-17 by the S4 slice agent. Read-only pass; no builds/tests run — all `verify: [pending]`.
Scope: Discovery (Search/FTS/Tree Search/Tag Directory/Auto Tree Search/Mart Worker/Also-Favorited),
Tags (F11-15), UserStoryInteractions (F16-17 + Bookshelves), CustomLists (F51). Atoms
`StoryDeck`/`StoryCard`/`UserCard`/`RecommendationCard`/`TagChip`/`CanalaveTypeahead`/`ConfirmDialog`/
`InlineAlert`/`PaginationControls`/`ReportDialog` are S0/S2/S5's (cited where consumed, not re-audited).
`StoryPage`/`StoryEditorPage` are S2's. EF configs are S1's. Migrations excluded.

**Overall read: this is the cleanest slice audited so far.** Every write path is correctly authorized
(MA-301 class clean — a positive contrast with S2/S3), no unsanitized user-HTML path exists (MA-201
class clean — all user text is bounded plain text by settled design), all read services are
factory-per-method, the raw-SQL mart/rCTE builders are fully parameterized with compile-time table
constants (no injection surface), and the manual-tree JS interop uses WeakMap/WeakSet + element-scoped
listeners so it needs no `IDisposable`. Findings are the exceptions, headed by the deferred H-10
resolution (USI-panel dispose **drops** the pending write) and one WU44-pre-flagged latent panel bug.

## File inventory (path + LOC)

### Product — Core (measured)
| LOC | File | LOC | File |
|---|---|---|---|
| Discovery/ | | Tags/ | |
| 15 | StoryFilterDto.cs | 24 | Tag.cs |
| 8 | TagIncludeMode.cs | 30 | TagChipDto.cs |
| 20 | SiteSearchModes.cs / SearchMode.cs | 22 | ITagReadService.cs / 18 ITagWriteService.cs |
| 30 | DefaultUserStoryInteractionFilterSetting.cs / UserStoryInteractionFilterSetting.cs / UserStoryInteractionFilterType.cs | 20 | CreateTagDto.cs / UpdateTagDto.cs / TagValidations.cs / TagValidationException.cs |
| 14 | IDiscoveryDefaultsReadService.cs | 26 | TagDirectoryGroupDto.cs / TagDirectoryNodeDto.cs / TagTypeLayout.cs / TagTypeEnum.cs / TagPriority.cs |
| 22 | TreeSearchEdgeType.cs / TreeSearchRequest.cs / ITreeSearchReadService.cs / TreeSearchControlsSelection.cs / TreeSearchTab.cs | 48 | SavedTagSelection.cs / SavedTagSelectionEntry.cs / *Dto.cs / *Input.cs / *Validations.cs / I*Read+WriteService.cs / SortEnum / ValidationException |
| 55 | ManualTreeEdge.cs / ManualTreeNeighbors.cs / IManualTreeSearchReadService.cs / TreeSearchPathParser.cs / TreeSearchListingResultDto.cs | | |
| 20 | ICoOccurrenceReadService.cs / CoOccurrenceRequest.cs | UserStoryInteractions/ | |
| | CustomLists/ | 30 | UserStoryInteraction.cs / UserStoryInteractionDate.cs |
| 60 | CustomList.cs / CustomListEntry.cs / CustomListDtos.cs / CustomListSortEnum.cs / CustomListValidations.cs / *Exception.cs / I*Read+WriteService.cs | 60 | UserStoryInteractionStateDto.cs / …StateUpdate.cs / …TypeEnum.cs / …Constants.cs / …DisplayContext.cs / I…Read+WriteService.cs |
| | Bookshelves/ | 20 | BookshelfTab.cs |

### Product — Server/Discovery, Tags, UserStoryInteractions, CustomLists
| LOC | File | LOC | File |
|---|---|---|---|
| 222 | Discovery/DiscoveryMartSchema.cs | 149 | Tags/ServerTagReadService.cs |
| 103 | Discovery/DiscoveryMartRebuilder.cs | 130 | Tags/ServerTagWriteService.cs |
| ~40 | Discovery/DiscoveryMartWorker.cs | 93 | Tags/TagEndpoints.cs |
| 361 | Discovery/ServerTreeSearchReadService.cs | 114 | Tags/ServerSavedTagSelectionReadService.cs |
| 313 | Discovery/ServerManualTreeSearchReadService.cs | 170 | Tags/ServerSavedTagSelectionWriteService.cs |
| 134 | Discovery/ServerCoOccurrenceReadService.cs | 72 | Tags/SavedTagSelectionEndpoints.cs |
| 68 | Discovery/ServerDiscoveryDefaultsReadService.cs | 93 | UserStoryInteractions/ServerUserStoryInteractionReadService.cs |
| ~35 each | Discovery/{ManualTreeSearch,TreeSearch,CoOccurrence,DiscoveryDefaults}Endpoints.cs | 163 | UserStoryInteractions/ServerUserStoryInteractionWriteService.cs |
| 205 | CustomLists/ServerCustomListWriteService.cs | 84 | UserStoryInteractions/UserStoryInteractionEndpoints.cs |
| 139 | CustomLists/ServerCustomListReadService.cs | 107 | CustomLists/CustomListEndpoints.cs |

### Product — SharedUI
| LOC | File | LOC | File |
|---|---|---|---|
| 200 | Discovery/SearchPage.razor | 235 | Tags/TagFilter.razor |
| 85 | Discovery/SearchDesktop.razor | 100 | Tags/TagSelector.razor |
| 124 | Discovery/SearchMobile.razor | 210 | Tags/TagEditorForm.razor |
| 268 | Discovery/TreeSearchPage.razor | 90 | Tags/TagDirectoryPage.razor |
| 99 | Discovery/TreeSearchDesktop.razor | 200 | Tags/TagDirectoryDesktop.razor |
| 132 | Discovery/TreeSearchMobile.razor | ~120 | Tags/TagDirectoryMobile.razor / TagDirectorySection.razor |
| 248 | Discovery/ResultsFilterPanel.razor | 321 | Tags/SavedTagSelectionLoadFlyoutInner.razor |
| 156 | Discovery/TreeSearchControls.razor | ~30 | Tags/SavedTagSelectionLoadFlyout.razor (wrapper) |
| 501 | Discovery/ExploreTab.razor | ~180 | Tags/SavedTagSelectionSaveDialog(Inner).razor |
| 334 | Discovery/DeepDiveTab.razor | 46 | Tags/TagChip.razor |
| 148 | Discovery/ManualTreeCanvas.razor | 146 | UserStoryInteractions/UserStoryInteractionPanel.razor |
| ~90 | Discovery/{ManualTreeEdgeToggles,TreeSearchTabStrip,TreeSearchResultBadge}.razor | ~40 | UserStoryInteractions/UserStoryInteractionButton.razor |
| 139 | Discovery/RelatedStoriesSection.razor | 83 | UserStoryInteractions/UserStoryInteractionFilter.razor |
| 40 | Discovery/ManualTreeStore.cs | 172 | Bookshelves/BookshelvesPage.razor |
| 106 | wwwroot/js/manual-tree-search.js | 94/146 | Bookshelves/BookshelvesDesktop.razor / Mobile.razor |
| 139 | CustomLists/MyListsPage.razor | 362 | CustomLists/CustomListPage.razor / 128 AddToCustomListMenu.razor |

### Product — Client (all HTTP impls, mechanically uniform)
Tags/{ClientTagRead,ClientTagWrite,ClientSavedTagSelectionRead,ClientSavedTagSelectionWrite}Service.cs;
UserStoryInteractions/{ClientUserStoryInteractionRead,Write}Service.cs;
Discovery/{ClientTreeSearchRead,ClientManualTreeSearchRead,ClientDiscoveryDefaultsRead,ClientCoOccurrenceRead}Service.cs;
CustomLists/{ClientCustomListRead,ClientCustomListWrite}Service.cs (~30-70 LOC each).

### Tests owned by this slice (read/sampled)
Integration: DiscoveryMartTests, DiscoveryDefaultsReadServiceTests, TreeSearchComposeTests,
ManualTreeSearchTests, TagReadServiceTests, TagWriteServiceTests, TagEndpointsTests,
StoryTaggingTests, UserStoryInteractionServiceTests, BookshelfStoryIdsTests,
SavedTagSelectionServiceTests, CustomListServiceTests. Unit: TagValidationsTests, ClientTagServiceTests,
SavedTagSelectionValidationsTests, TreeSearchPathParserTests, TreeSearchRequestValidationTests,
ManualTreeLayoutTests, UserStoryInteractionVisualsTests, BookshelfTabVisualsTests,
CustomListValidationsTests, ClientCustomListServiceTests. RazorComponents:
{TagEditorForm,TagChip,TagSelector,TagFilter,TagDirectory,UserStoryInteractionFilter,
UserStoryInteractionPanel,ResultsFilterPanel,TreeSearchControls,TreeSearchTabStrip,TreeSearchDesktop,
TreeSearchMobile,TreeSearchResultBadge,ManualTreeCanvas,ManualTreeEdgeToggles,ExploreTab,DeepDiveTab,
SavedTagSelectionLoadFlyout,SavedTagSelectionSaveDialog,AddToCustomListMenu}Tests + Fakes.

---

### MA-401 | Tier 2 | Bucket A | Slice 4
claim: `UserStoryInteractionPanel.Dispose()` **drops** a pending debounced interaction write. Disposing during the 2 s debounce window cancels the CTS, which throws `OperationCanceledException` inside `HandleToggleAsync`; the catch treats cancellation as "a subsequent toggle will fire the flush" — but on dispose there is no subsequent toggle, so `FlushAsync()` never runs. Toggle-a-story-then-navigate-away within 2 s silently loses the interaction. This is durable user intent (axiom 7 — "durable-direct for intent"), and the loss is **not** documented as accepted anywhere. Resolves the deferred H-10 question: it is a drop, not a flush.
evidence: `TheCanalaveLibrary.SharedUI/UserStoryInteractions/UserStoryInteractionPanel.razor:100-114` — `HandleToggleAsync`: `_debounce = new CancellationTokenSource(); ... await Task.Delay(UserStoryInteractionConstants.UserStoryInteractionDebounceMs, token); await FlushAsync();` wrapped in `catch (OperationCanceledException) { /* Debounce reset by a subsequent toggle; next HandleToggleAsync fires the flush. */ }`; `:141-145` `public void Dispose() { _debounce?.Cancel(); _debounce?.Dispose(); }` — no flush, and the component is `@implements IDisposable` not `IAsyncDisposable`. The catch comment's premise ("next HandleToggleAsync fires the flush") is false in the dispose path. Convention: `layer2-services.md` axiom 7 + §"Command Path" — "Durable user intent — including Favorite/Follow/Ignore toggles — always takes this direct path"; `layer3-logic.md` §"Optimistic Updates & Debounce" documents the debounce but not a lossy dispose. `audit/UserStoryInteractions.md` F16 L3 describes the debounce with no accepted-loss note.
cells: F16 L3-Logic (Stage 5) — **proposes reopen**
effort: S | route: Stage-4 reconcile (flush-on-dispose: fire the pending `FlushAsync` from `Dispose`/`DisposeAsync`, or convert to `IAsyncDisposable` and await it; add a RazorComponents test that disposes mid-debounce and asserts the write fired)
verify: [pending]

### MA-402 | Tier 2 | Bucket A | Slice 4
claim: `ResultsFilterPanel` snapshots `InitialFilter` **once** in `OnInitialized` and ignores every later change to the parameter, but both `SearchPage` and `TreeSearchPage` resolve the §8.7 default-exclusion set inside an **async** `OnInitializedAsync` and only then set `_filter with { ExcludedInteractions = ... }`. Blazor's synchronous first render therefore hands the panel the pre-seed (empty) filter, and the interaction-exclusion checkboxes never reflect the defaults that are actually applied to the query (e.g. "Hide stories I've ignored" renders unchecked while Ignored is in fact excluded). This is the exact one-time-`OnInitialized`-snapshot-vs-async-race shape that `TreeSearchControls` was fixed for in WU44 — and `audit/Discovery.md` explicitly flagged this pairing as unresolved, "flagging for whoever next works that cell."
evidence: `TheCanalaveLibrary.SharedUI/Discovery/ResultsFilterPanel.razor:170-190` — `protected override void OnInitialized()` reads `InitialFilter.ExcludedInteractions`/`Sort`/tags once; param doc `:132-134` "Applied during OnInitialized(); later changes to this param are ignored." Async seed happens after first render: `SearchPage.razor:99-102` `DefaultExcludedInteractions ??= [.. await DiscoveryDefaults.GetDefaultExcludedInteractionsAsync(...)]; _filter = _filter with { ExcludedInteractions = ... };` and `TreeSearchPage.razor:164-166` (same). Contrast the fixed sibling `TreeSearchControls.razor:101-107` — `OnParametersSet` re-syncs from `Initial*` until `_userHasInteracted` flips. Convention/flag: `audit/Discovery.md` WU44 note — "the same one-time-`OnInitialized()`-snapshot shape exists in the already-Stage-5 `ResultsFilterPanel`/`SearchPage` pairing (Feature 31) — not touched here."
cells: F31 L3-Logic (SearchPage/ResultsFilterPanel, Stage 5), F59 L3-Logic (TreeSearchPage reuse) — **proposes reopen**
effort: M | route: Stage-4 reconcile (apply the `TreeSearchControls` resync-until-interaction pattern to `ResultsFilterPanel`, or have the dispatcher seed the filter before the panel's first render)
verify: [pending]

### MA-403 | Tier 2 | Bucket A | Slice 4
claim: `AddToCustomListMenu` is an `<AuthorizeView>`-gated, DI-consuming leaf that declares `@inject ICustomListWriteService` at **file scope** with only inline `<AuthorizeView><Authorized>` around its markup — the exact pre-WU43 pattern that its own sibling `SavedTagSelectionLoadFlyout` was split into wrapper/inner to fix. `layer3-logic.md` states the rule as a MUST: "Any new AuthorizeView-gated, DI-consuming leaf must use the wrapper/inner split from the start." No runtime 401 results here (unlike NotificationBell, the component runs no service call on init — only on user gesture), but it imposes the documented test-registration tax: `AddToCustomListMenu` is composed inside `StoryCard`'s caret, so every bUnit test that renders a `StoryCard`/`StoryDeck` must register `ICustomListWriteService` even for anonymous contexts.
evidence: `TheCanalaveLibrary.SharedUI/CustomLists/AddToCustomListMenu.razor:3` `@inject ICustomListWriteService CustomListService` (file scope), `:13-14` `<AuthorizeView><Authorized>` wraps only the markup. Contrast the WU43-correct sibling: `SavedTagSelectionLoadFlyout.razor` (thin wrapper, no `@inject`) + `SavedTagSelectionLoadFlyoutInner.razor:2-4` (holds the `@inject`s, mounted inside `<Authorized>`). Convention: `layer3-logic.md` §"Deferring DI Behind AuthorizeView (WU43)"; `audit/CustomLists.md` L3 (AddToCustomListMenu is the "StoryCard caret composite").
cells: F51 L3-Logic (Stage 5) — **proposes reopen**
effort: S | route: Stage-4 reconcile (split into `AddToCustomListMenu` wrapper + `AddToCustomListMenuInner`, mirroring the SavedTagSelection pair)
verify: [pending]

### MA-404 | Tier 2 | Bucket A | Slice 4
claim: `TreeSearchPage`'s missing-root branch renders an inline `<div>` (HTTP 200 + client message) for a genuinely-absent story/user root instead of the sanctioned `NavigationManager.NotFound()` — the same MA-202 (S2) / MA-304 (S3) class, extending it into Discovery. A `/discover/story/{deletedId}` or `/discover/user/{deletedId}` URL returns 200, the SEO/crawler concern (F64) `render-and-layout.md` calls out. Notably, the sibling `BookshelvesPage` in this same slice **does** use `Nav.NotFound()` correctly — the first clean use of it in the audit.
evidence: `TheCanalaveLibrary.SharedUI/Discovery/TreeSearchPage.razor:33-38` — `@if (_rootNotFound) { <div ...><p>We couldn't find that @(StoryId.HasValue ? "story" : "user").</p></div> }`; `_rootNotFound` set at `:158`/`:192` when the root story/user doesn't resolve. Zero `Nav.NotFound()` in the file. Contrast `BookshelvesPage.razor:106` `if (parsed is null) { Nav.NotFound(); return; }`. Convention: `render-and-layout.md` §"NavigationManager.NotFound()"; `layer3-logic.md` §"Page Dispatcher: Entity Not Found." (`CustomListPage.razor:29-35` renders inline for a null detail too, but that is a *deliberate* missing-vs-private ambiguity — a private list must not 404-reveal its existence — so it is not filed.)
cells: F33 L3-Logic / F59 L3-Logic (TreeSearchPage, Stage 5) — **proposes reopen** (bundle with the MA-202/304 sweep)
effort: S | route: mechanical sweep (swap the `_rootNotFound` inline branch to `Nav.NotFound()`; browser-verify a deleted-root URL returns 404)
verify: [pending]

### MA-405 | Tier 3 | Bucket A | Slice 4
claim: `TagEditorForm` hand-rolls a `<p class="text-...-danger" role="alert">` for its server-validation error instead of the `InlineAlert` atom that `error-handling.md` names the ONLY channel for validation feedback — the same MA-205 (S2) class. Every newer form in the slice (`MyListsPage`, `CustomListPage`, `AddToCustomListMenu`, `SavedTagSelectionLoadFlyoutInner`) uses `InlineAlert`; `TagEditorForm` (WU27.5, pre-InlineAlert) drifted.
evidence: `TheCanalaveLibrary.SharedUI/Tags/TagEditorForm.razor:105-108` — `@if (!string.IsNullOrEmpty(_serverError)) { <p class="text-sm text-(--color-danger)" role="alert">@_serverError</p> }`. Contrast `MyListsPage.razor:39` `<InlineAlert Messages="@_createErrors" />`, `AddToCustomListMenu.razor:65` `<InlineAlert Messages="@_errors" />`. Convention: `error-handling.md` / calibration seam record — "`InlineAlert` — the ONLY channel for validation feedback."
cells: F11 L3.5-Structure (Stage 5)
effort: S | route: mechanical sweep (replace with `<InlineAlert Message="@_serverError" />`; bundle with MA-205)
verify: [pending]

### MA-406 | Tier 3 | Bucket C | Slice 4
claim: The mobile filter-drawer overlay shell is **verbatim-duplicated across three files** — `SearchMobile`, `BookshelvesMobile`, `TreeSearchMobile` — each carrying an identical backdrop + right slide-in panel + header-with-close-X + the same two inline SVG paths (hamburger `M4 6h16v2H4zm3 5h10v2H7zm3 5h4v2h-4z` and close-X). The codebase's own "extract a shared primitive at the third consumer" threshold (`layer3.5-structure.md` ConfirmDialog note; `audit/UserStoryInteractions.md` "do NOT extract" was decided when there was one) is now met. A shared `FilterDrawer`/`SlideOver` composite would collapse ~25-30 LOC × 3 sites.
evidence: `TheCanalaveLibrary.SharedUI/Discovery/SearchMobile.razor:64-93` vs `Bookshelves/BookshelvesMobile.razor:81-109` vs `Discovery/TreeSearchMobile.razor:61-89` — the `<div class="fixed inset-0 z-(--z-drawer) bg-(--color-backdrop)" @onclick="Close...">` backdrop + `<div class="fixed inset-y-0 right-0 z-(--z-drawer) ... bg-(--color-surface) ... shadow-prominent" @onclick:stopPropagation="true">` panel + close-button block are byte-identical modulo the id and the inner content. The hamburger toggle button + SVG repeat identically at `SearchMobile:15-24`, `BookshelvesMobile:54-63`, `TreeSearchMobile:31-38`.
cells: F31/F17/F33 L3.5 (no cell change)
effort: M | route: seam — direction undetermined (extract a `FilterDrawer` composite with a `ChildContent` body + `Title`; Brian decides whether the three-site tax is worth the machinery)
verify: [pending]

### MA-407 | Tier 3 | Bucket C | Slice 4
claim: `TagEndpoints` carries its **own private `ExecuteWriteAsync`** copy of the exception→status translation, while every other endpoint file in the slice (`CustomListEndpoints`, `SavedTagSelectionEndpoints`, `UserStoryInteractionEndpoints`) uses the shared `EndpointHelpers.ExecuteWriteAsync`. Intra-slice divergence + near-dup (`TagEndpoints` predates the shared helper as the WU-L5Pilot first surface).
evidence: `TheCanalaveLibrary.Server/Tags/TagEndpoints.cs:68-91` — `private static async Task<IResult> ExecuteWriteAsync(Func<Task<IResult>> action)` catching `TagValidationException`→400 / `UnauthorizedAccessException`→403 / `KeyNotFoundException`→404. Siblings call `EndpointHelpers.ExecuteWriteAsync` (`CustomListEndpoints.cs:54`, `SavedTagSelectionEndpoints.cs:45`, `UserStoryInteractionEndpoints.cs:55`).
cells: F11 L5 (no cell change)
effort: S | route: seam — direction undetermined (fold `TagValidationException` handling into `EndpointHelpers.ExecuteWriteAsync` if it doesn't already cover `ArgumentException`-derived; then delete the local copy)
verify: [pending]

### MA-408 | Tier 3 | Bucket C | Slice 4
claim: `ServerSavedTagSelectionReadService.GetPublicSelectionsByUserAsync` loops `HydrateDetailAsync` once per selection id — two queries (header + entry-join) **per selection** — an N+1 the read-layer convention warns against. Volume is low (a user's public saved selections), but the profile TagSelections tab surfaces this, and a single batched join over all ids would remove the loop.
evidence: `TheCanalaveLibrary.Server/Tags/ServerSavedTagSelectionReadService.cs:54-72` — `foreach (int id in ids) { ... await HydrateDetailAsync(readDb, id); }`, and `HydrateDetailAsync:82-103` runs one header query + one `SavedTagSelectionEntries⨝Tags` query each. Convention: `layer2-services.md` §"Query Path" — "Avoid materializing entities then mapping"; the DAG/N+1 guardrail in `layer3-logic.md` §"Service Injection Principle."
cells: F15 L2 (Stage 5)
effort: S | route: seam — direction undetermined (batch the entry-join across all ids; Brian decides whether the low volume justifies the change)
verify: [pending]

### MA-409 | Tier 3 | Bucket A | Slice 4
claim: `ServerUserStoryInteractionWriteService` reads the story's author id with the `(int?)`-projection pattern `layer2-services.md` explicitly says to avoid (project to an anonymous reference type instead, so "row missing" is distinguishable from "column null"). Harmless here — both the authorless-story and (impossible, FK-guaranteed) missing-story cases collapse to "no favorites-counter update," which is the desired behavior — but it is a stated convention deviation that a future refactor could make bite.
evidence: `TheCanalaveLibrary.Server/UserStoryInteractions/ServerUserStoryInteractionWriteService.cs:87-91` — `int? storyAuthorId = await writeDb.Stories.Where(s => s.StoryId == storyId).Select(s => (int?)s.AuthorId).FirstOrDefaultAsync(); if (storyAuthorId.HasValue) { ... }`. Convention: `layer2-services.md` §"Scalar projections on nullable FK columns — use anonymous-type, not `(int?)`".
cells: F16 L2 (Stage 5)
effort: S | route: mechanical sweep (project to `new { s.AuthorId }`)
verify: [pending]

---

## Hypothesis results (slice 4)

- **H-01** (`@key` on stateful list children): **clean** — `ExploreTab.razor:470` keys `@key="story.StoryId"` on the stateful `StoryCard` rows in `StorySection`; `ManualTreeCanvas.razor:33` keys node buttons `@key="node.NodeId"`; `TagFilter.razor:58` keys the init-only-seeded `TagSelector` loop `@key="{type}-{_selectionGeneration}"` (the WU43 re-seed idiom, correct); `UserStoryInteractionPanel` is the canonical keyed child (keyed by the deck/card parents, S2-verified). No unkeyed `@foreach` over a param-caching child found.
- **H-02** (route-param reload discipline): **clean** — `BookshelvesPage` (`:68-94`, sentinel `_loadedTab=""` + `_initialized`, plain-assign `Items=null` on reload, `Nav.NotFound()` on bad slug), `TreeSearchPage` (`:102-136`, `_loadedRoute` tuple sentinel, plain reload nulling persisted state), `CustomListPage` (`:168-197`, `_loadedListId=int.MinValue`, plain reload) all conform. `SearchPage`/`MyListsPage` have no route param (restore-or-fetch `OnInitializedAsync` only).
- **H-03** (unnamed `HasIndex` overwrite): **n/a** — EF configs are S1's; the raw-SQL mart indexes in `DiscoveryMartSchema` are each explicitly + uniquely named (`ix_tree_search_user_edge`, etc.).
- **H-04** (read-context factory-per-method): **clean** — every read service opens `await using ... readDbFactory.CreateDbContextAsync()` per method (`ServerTagReadService`, `ServerUserStoryInteractionReadService`, `ServerCustomListReadService`, `ServerSavedTagSelectionReadService`, `ServerDiscoveryDefaultsReadService`); the raw-ADO services (`ServerTreeSearchReadService`, `ServerCoOccurrenceReadService`, `ServerManualTreeSearchReadService`) each open their own `readDb` + connection per method; write services hold only `writeDb`; base services expose `protected ActiveUser`/`ReadDbFactory` (CS9107 idiom). `DiscoveryMartRebuilder` holds the write `ApplicationDbContext` but is a scoped worker service (not circuit-reachable) — correct.
- **H-05** (dead Tailwind classes): **clean** — paren-form tokens throughout; bare-name semantic tokens (`bg-surface`, `text-text`) are the sanctioned dual style; `text-white` appears only on colored `mission`/`action` Control grounds (matching `PaginationControls`, CI-green per `audit/CustomLists.md` L4). No v3 bracket-form, no raw palette scale (`bg-yellow-50`), no raw hex.
- **H-06** (unregistered silent catches): **clean** — no bare `catch {}` in slice. `ServerCoOccurrenceReadService.cs:105-113` catches a *typed* `PostgresException` (UndefinedTable) + `LogWarning` + graceful empty (documented degraded read); `DiscoveryMartRebuilder.cs:94-101` records telemetry then rethrows.
- **H-07** (stale/untracked TODO comments): **clean** — no `TODO`/`HACK`/`FIXME` in slice product code.
- **H-08** (`Nav.NotFound()` vs manual): **MA-404** — `TreeSearchPage` renders inline for a missing root (filed, extends MA-202/304). `BookshelvesPage` uses `Nav.NotFound()` correctly (first clean use in the audit). `CustomListPage`'s inline null-detail branch is a deliberate missing-vs-private ambiguity (not filed).
- **H-09** (dispatcher load parallelism): **clean** — the dispatcher load bodies are genuine dependency chains (`candidateIds → listings → states`, `root → defaults → search → states`, `detail → ids → page → states`), not independent-await blocks; no `StoryPage`-style parallelizable set. `Task.WhenAll` would not apply.
- **H-10** (debounced writes lost on dispose): **MA-401 — DROP.** `UserStoryInteractionPanel.Dispose()` cancels the debounce CTS and never flushes; toggle-then-navigate-away within the 2 s window silently loses the write. Not documented as accepted. Resolved: it is a drop.
- **H-11** (doc-vs-code staleness): **mostly clean** — the WU44 `ResultsFilterPanel` flag is filed as a code bug (MA-402), not a fresh doc contradiction. One minor stale comment: `TagFilter.razor:116-127` `DefaultFilterTypes` XML says "All five tag types" while the class-level param doc `:76-78` says "all six" — five is correct (Relationship removed WU37); the "six" comment is stale. Noted, not separately filed.
- **H-12** (fire-and-forget without observation): **clean** — no `_ = SomeAsync(...)` launches in slice; `RelatedStoriesSection`, `ExploreTab`, `DeepDiveTab`, and every dispatcher `await` their service calls.
- **H-13** (denormalized counter discipline): **clean** — `ServerUserStoryInteractionWriteService.cs:84-122` uses the transition-delta rule (capture-before, flip-check `willBe != was`) + atomic `ExecuteUpdateAsync(SetProperty(us => us.X + delta))` for `FavoritesOnStories`/`StoriesRead`/`StoriesInProgress`/`StoriesIgnored` — no tracked `++`.
- **H-14** (elevated reads annotated): **n/a** — no `IgnoreQueryFilters` in slice product code. `ServerCustomListWriteService.CloneListAsync` deliberately clones through the *filtered* read context (the opposite of a bypass — the settled visibility-on-clone privacy rule, verified applied); `ServerManualTreeSearchReadService` constrains story-valued rows via the filtered `Stories` DbSet.
- **H-15** (write-path by-id lookups bypass ContentRating): **clean by construction** — write-service existence checks read the unfiltered `writeDb` (`ServerCustomListWriteService.cs:85` AddStory, `:119` clone-source; `ServerSavedTagSelectionWriteService` update/copy; USI writes load by composite PK on `writeDb`) — no `readDb` PK fetch in a write path, so the phantom-`KeyNotFound` class can't occur.
- **H-16** (`[FromQuery]` on non-GET arrays): **clean** — `UserStoryInteractionEndpoints`/`TagEndpoints` bind `int[]` via GET repeated-key (the documented GET-bindable exception); complex reads with array/DTO params correctly POST (`TreeSearchEndpoints`, `CoOccurrenceEndpoints` — carrying `CoOccurrenceRequest.ExcludedInteractions`, `ManualTreeSearchEndpoints/neighbors`), each with a doc comment; `node-displays` GET `int[]` documented.
- **H-17** (nullable client reads use tolerant helpers): **clean** — `ClientCustomListReadService.GetListDetailAsync` / `ClientSavedTagSelectionReadService.GetSelectionDetailAsync` return `Task<T?>` via `GetNullableFromJsonAsync` (audit-confirmed + `Client*ServiceTests` unit-covered; server returns `Results.Json(nullable)`). Non-nullable result DTOs (`ClientTreeSearchReadService`) use `ReadFromJsonAsync` + `EnsureSuccessStatusCode` — correct for a guaranteed-non-null shape.
- **H-18** (aria-labels): **clean** — no slice component wraps `EditorView` (tags/lists/selections use plain `<input>`/`<textarea>`, not rich text), so the EditorView-collision rule is n/a. Icon-only controls are labeled: drawer close (`aria-label="Close filters"`), zoom in/out (`ManualTreeCanvas.razor:64,67`), Deep Dive panel close (`:52`), Bookshelves tab dropdown. The `TagEditorForm` hand-rolled alert is MA-405 (a channel issue, not aria).
- **H-19** (AuthorizeView-gated DI wrapper/inner split): **MA-403** — `AddToCustomListMenu` injects at file scope under only an inline `<AuthorizeView>`. `SavedTagSelectionLoadFlyout`/`SaveDialog` correctly use the wrapper/inner split (WU43). `TagDirectoryPage` injects the write service but is a page dispatcher (router constructs it; the write path is user-gesture-driven, and `<AuthorizeView Roles>` gates the affordances) — not the leaf class the rule targets.
- **H-20** (feedback-channel discipline): **MA-405** — `TagEditorForm` hand-rolls a `<p role="alert">`. Everything else uses `InlineAlert` (`MyListsPage`, `CustomListPage`, `AddToCustomListMenu`, `SavedTagSelectionLoadFlyoutInner`) with catches routed through `ExceptionPresenter`; no toast carries validation; no raw `ex.Message` in UI.

**MA-201 class (stored-XSS):** clean — no `EditorView`/rich-HTML write path exists in this slice. All persisted user text (CustomList `ListName`, SavedTagSelection `Nickname`/`Description`, Tag `Description`) is bounded **plain text** by settled design (`audit/Tags.md` F15: Description is "bounded plain text, not rich HTML — no EditorView/RichTextView/sanitize pipeline"), trimmed on save and rendered with Razor auto-encoding, never `MarkupString`. Sanitization is correctly absent because there is nothing to sanitize.

**MA-301 class (broken access control):** clean — every write is authorized at the service layer. Tag writes: `RequireMod()` on Create/Update/Delete (`ServerTagWriteService.cs:15,47,79`). USI writes: self-scoped to `userId` from `CurrentUserId` on a composite-PK row — no cross-user write is expressible. CustomList: `RequireOwnedListAsync` on rename/visibility/delete/add/remove, `RequireAuthenticatedUser` on create, public-or-owner gate on clone. SavedTagSelection: `RequireOwner` on update/delete, public-or-owner on copy. Clone/copy-on-share correctly copy only cloner-visible entries via the *filtered* read context (`ServerCustomListWriteService.cs:136-144`), so hidden content never leaks into the cloner's account.
