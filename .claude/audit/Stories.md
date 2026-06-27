# Audit — Stories/

**Features:** 4, 5, 8, 9, 10, 45 (Story creation/editing, browsing/display, arcs, series, relationships,
view-count). Largest cluster.

## Shared Context

**Entities (Core/Stories/):** `Story` (hot partition — status, counts, dates, FKs), `StoryListing`
(warm — title, short desc, cover art URL; carries the FTS `SearchVector` generated column), `StoryDetail`
(cold — long desc, slug, `PostApprovalStatus`). Vertical-partition trio with 1-to-1 cascade. `Story`
implements `IEditableStoryProperties` via explicit interface implementation (`[NotMapped]` projections
across the three partitions) — a deliberate, somewhat advanced pattern that lets one object satisfy the
edit contract. Also here: `StoryArc`, `Series`/`SeriesEntry`, `StoryRelationship` (+type lookup),
`StoryTag`/`StoryCharacter` live in the model set but belong to Tags.

**DTOs/contracts (Core/Stories/):** `CreateStoryDTO`, `StoryUpdateDTO`, `StoryDetailsDTO`, `StoryTagDTO`,
`IEditableStoryProperties`, `IStoryTag`, `StoryMappers`, `StoryValidations`, `StoryValidationException`.
**As of 2026-06-27 (sprite redesign):** `TagChipDto.SpriteIdentifier` replaces `TagChipDto.SpriteUrl`
in the character-chip projections in `ServerStoryReadService`. The read service drops its
`ISpriteReadService` dependency; sprite resolution is now in the `CharacterEntry.razor` component.

**Services:** `IStoryReadService` / `IStoryWriteService` (Core) + `ServerStoryReadService` /
`ServerStoryWriteService` (Server, direct-injection pattern per spec §6.6 — see RESOLVED note below) +
`HttpStoryReadService` / `HttpStoryWriteService` / `HttpStoryOverviewService` (Client). `StoryEndpoints`
(Server) maps the API.

**Components (SharedUI):** `StoryPage` (dispatcher, `/story/{StoryId:int}/{Slug?}`),
`StoryDesktop`/`StoryMobile` (stubs), `StoryPropertiesForm` + `StoryPropertiesViewModel` (now
`SharedUI/Stories/` — moved out of the legacy `Components/StoryProperties/` folder ahead of the
cluster's actual build; see `canalave-conventions/SKILL.md` "Code Organization"). **The relocation is
folder-only — content is unchanged and remains the Stage-4 build-to-spec scaffolding described in
Feature 4 below; do not treat the new location as endorsement of the existing content.**

**Fluent config:** inline in `ApplicationDbContext.OnModelCreating` — Story FK fan-out (Cascade to owned
collections, Restrict to `StoryStatus`, `SetNull` author anonymization), partition 1-to-1s, FTS computed
column + GIN index `ix_story_listing_search_vector`, slug unique-filtered index.

---

## Feature 4 — Story Creation & Editing

- **L1 — Stage 5.** Partition trio + `IEditableStoryProperties` plumbing is sound and matches spec §4/§7.
  Awaiting migration + build verification (no migrations exist). *Settled:* three-table vertical split;
  slug server-generated; explicit-interface edit contract.
- **L2 — Stage 5.** **RESOLVED (2026-06-20):** this cell's prior "Stage 5, nothing to reconcile" call was
  wrong — `DbStoryWriteService`/`DbStoryReadService` injected `IDbContextFactory<T>`, which was never
  registered in `Program.cs` (only plain `AddDbContext<T>` existed), so DI container validation failed at
  app startup. Surfaced by actually running the Aspire AppHost end-to-end. Per spec §6.6 ("Why Direct
  DbContext Injection over IDbContextFactory" — superseded for thread-safety reasons that don't apply
  under scoped DI), rewrote as `ServerStoryReadService(ReadOnlyApplicationDbContext readDb)` and
  `ServerStoryWriteService(ReadOnlyApplicationDbContext readDb, ApplicationDbContext writeDb) :
  ServerStoryReadService(readDb), IStoryWriteService` — primary-constructor injection, `readDb` private or
  base. Registered via `AddScoped<>` in `Program.cs` (already scoped; only the implementation type names
  changed). *Open:* cover-art upload to R2/MinIO not implemented; slug generation not visible in the write
  path.
  **WU12 (2026-06-22) closed the open item, partially:** slug generation built (server-only,
  `ServerStoryWriteService.GenerateUniqueSlugAsync` — slugify `Title`, Tier-3 uniqueness check against
  `StoryDetails.Slug`, suffix-disambiguate on collision; never on any DTO, never client-editable). A
  real NRE was found and fixed in `StoryMappers.ToStory()` — a fresh `new Story()` had null
  `StoryListing`/`StoryDetail` navs, dereferenced one line later by
  `UpdateStoryEditableProperties()`; now both partitions are initialized before mapping. The original
  `CreateStoryAsync`'s `writeDb.Attach(...)` calls on those navs were also a second, compounding bug
  (`Attach` marks Unchanged — would have skipped inserting the listing/detail rows entirely) — removed;
  `Stories.Add(newStoryDB)` alone correctly cascades the connected graph as Added. Cover-art upload is
  **not** stubbed away — `IImageStorageService`/`LocalImageStorageService` now exist (see
  `audit/ImageStorage.md`); the write path still takes `CoverArtRelativeUrl` as a pass-through string
  (no upload UI wired here — that's WU24, which now has a ready service to call).
  **How verified:** `dotnet build` green (0 warnings/errors, all 4 projects); live server boot clean;
  via `/dev/wu12/*` diagnostics (kept as standing dev tools, not removed) — `CreateStoryAsync` called
  twice with the same title produced `wu12-mature-story` then `wu12-mature-story-2` (confirmed via
  `psql`), no NRE on either call. **WU12.5 (2026-06-22)** migrated this verification into asserted,
  CI-runnable tests — `StoryWriteServiceTests` in `TheCanalaveLibrary.Tests.Integration` covers the
  same NRE/`Attach`-vs-`Add`/slug-disambiguation regressions against a real Postgres; the dev-
  diagnostics endpoints are no longer the source of truth for this behavior (see
  `canalave-conventions/testing.md`).
- **L3-Logic — Stage 5.** **WU24 (2026-06-23):** `ServerStoryWriteService` now enforces author ownership
  on both create and update paths: `CreateStoryAsync` stamps `AuthorId` from `IActiveUserContext.UserId`
  (the client-settable `AuthorId` property is removed from `CreateStoryDTO`) and throws
  `InvalidOperationException` for unauthenticated callers; `UpdateStoryAsync` loads the story, checks
  `story.AuthorId != activeUser.UserId`, and throws `UnauthorizedAccessException` for non-owners. No
  mod/admin OR in this gate — moderation is a separate WU34 path (see `cross-cutting.md` "Security vs
  affordance"). `ITagReadService` gains `GetTagChipsByIdsAsync(IReadOnlyList<int>)` — bulk chip lookup
  by exact ID for edit prefill; `ServerTagReadService` implements it following the same
  sprite-resolve-at-projection pattern as `SearchTagChipsAsync`. `StoryPropertiesViewModel` no longer
  implements `IEditableStoryProperties` (decoupled from wire DTOs; mapping is at page layer).
  `Routes.razor` switched from `RouteView` to `AuthorizeRouteView` so `[Authorize]` attributes are
  enforced (was silently ignored with `RouteView`).
- **L3.5-Structure — Stage 5.** **WU24 (2026-06-23):** `StoryPropertiesForm` fully rebuilt (Bootstrap →
  Tailwind, all 6 `TagSelector` instances wired with `OnSelectionChanged` + `InitialTagsByType` prefill,
  `EditorView @ref` pull-on-submit via public `GetLongDescriptionAsync()`, `InputFile` for cover art,
  `Rating`+`Status` selects, server validation error display, `IsLoading` disabled-submit guard). No
  `@inject` — presentational, passes `StoryPropertiesViewModel` and `OnValidSubmit`; page owns all I/O.
  `StoryEditorPage.razor` created: both `@page "/story/new"` and `@page "/story/{StoryId:int}/edit"` +
  `[Authorize]`; thin dispatcher (loads story + tag chips for edit prefill, maps ViewModel ↔ DTOs on
  submit, cover-art save-first-then-upload ordering, surfaces `StoryValidationException` and
  `UnauthorizedAccessException` into `ViewModel.ServerValidationErrors`). Pattern 1 edit side (view
  side is WU25). Admin/edit controls: no named component — inline `@if` and edit-page links per the
  "Owner-Conditional Edit Affordances" convention (see `layer3.5-structure.md`).
- **L4-Style — Stage 5.** **WU24 (2026-06-23):** all form markup uses Tailwind v4 design tokens
  (`--color-text`, `--color-surface`, `--color-border`, `--color-primary`, `--color-danger`), consistent
  with the locked token set. Responsive: `grid-cols-1 md:grid-cols-2` for Rating/Status row; single
  column otherwise. Visual sign-off pending human review (Stage-6 gate: cannot verify Tailwind layout in
  bUnit).
- **L5 — Stage 2 (2026-06-27, filter revamp).** `HttpStoryWriteService` and `HttpStoryReadService`
  deleted — they were dead code calling endpoints `StoryEndpoints` never mapped (divergence since WU12,
  confirmed by the Stage-4 note above). Client `Program.cs:16-17` DI registrations removed. MVP is
  `InteractiveServer`-only; L5 for story read/write is a genuine post-MVP build-to-spec item. No
  architectural blocker remains. Tests: build green (Server + Client); `dotnet test` all 1232 pass.
- **L6 — Stage 2.** Story search indexes deferred ("to be added by query need").

### Feature 4 / Feature 5 — Filter revamp Stage note (2026-06-27)

**What changed:** All four named EF display/visibility filters (`"ContentRating"` on `Story`,
`"GroupAudience"` on `Group`, `"IsTakenDown"` on `Story`/`BaseComment`/`BaseBlogPost`/`Recommendation`)
were moved from `ApplicationDbContext.OnModelCreating` to `ReadOnlyApplicationDbContext.OnModelCreating`.
`_activeUser` on `ApplicationDbContext` changed from `private` to `protected` to allow the subclass to
close over it in its own `OnModelCreating`. The write context (`ApplicationDbContext`) now carries no
visibility filters — it sees ground truth. All display/visibility filters live on the read context only.

**Latent bug closed:** `ServerStoryWriteService.UpdateStoryAsync:51` loaded the story via `writeDb.Stories`
with no `IgnoreQueryFilters` bypass. An author with `ShowMatureContent=false` editing their own M-rated
story got a null result (ContentRating filter on the write context filtered it out) → `KeyNotFoundException`
"Story not found" on their own edit. Fixed by construction: the write context no longer has any filter.

**Bypasses removed (~15):** All `IgnoreQueryFilters` on `writeDb` — 11 in `ServerModerationWriteService`
(`IsTakenDown` on all 4 roots), 1 in `ServerRecommendationWriteService` (`ContentRating`), 2 in
`ServerGroupWriteService` (`GroupAudience` + `ContentRating`), 1 in `ServerBlogPostWriteService`
(`GroupAudience`). None of these were defensive; all existed solely because the write context inherited
filters that writes should never see.

**Elevated reads kept (~7):** Moderation read queue (`IsTakenDown` on Story/Comment/BlogPost/Recommendation)
and `GetStoryIdsByAuthorAsync` (`ContentRating`). Each annotated `// elevated read:`.

**Migration tree removed:** `Migrations/ReadOnlyApplicationDb/` deleted — the read context is never
migrated separately (both contexts share the same schema); this had been accumulating dead artifacts.

**Tests (Integration tier):** `ContentRatingFilterTests` extended with 5 new tests:
- `TakenDownStory_IsInvisible_OnPublicRead` — IsTakenDown filter on read path
- `TakenDownStory_WriteContext_CanStillBeUpdated` — write path sees ground truth after takedown
- `MatureRatedStory_IsVisible_OnWriteContext_WhenAuthorHasMatureContentOff` — line-51 bug regression
- `MatureRatedStory_IsInvisible_OnReadContext_WhenViewerHasMatureContentOff` — companion filter check
`ModerationServiceTests.ResolveWithRemovalAsync_SoftHides_DropsFromPublicQuery_VisibleWithIgnoreFilter`
updated to use `ReadOnlyApplicationDbContext` for the public-visibility assertion (was incorrectly using
the unfiltered write context). All 1232 tests pass.

### WU-CounterAtomicity Stage note (2026-06-27)

**CS9107 eliminated — `ServerStoryReadService` / `ServerStoryWriteService`:**

`ServerStoryWriteService` was double-capturing the primary-constructor `activeUser` parameter — once in
its own body (lines using `activeUser.UserId`) and once passed to the base ctor
`ServerStoryReadService(readDb, activeUser)`. C# primary-constructor semantics count both captures,
raising CS9107. Fix mirrors the pattern already established in `ServerBlogPostReadService`:

- `ServerStoryReadService` now exposes `protected IActiveUserContext ActiveUser { get; } = activeUser;`
  (same comment about CS9107/CS9124 as the blog-post service). Internal reference in `ApplyFilters`
  updated to `ActiveUser.UserId`.
- `ServerStoryWriteService` two `activeUser.UserId` references (auth guard in `CreateStoryAsync`,
  ownership check in `UpdateStoryAsync`) updated to `ActiveUser.UserId`. The ctor parameter now only
  appears in the base-ctor argument — not a captured field — so the warning disappears.

No behavior change. `dotnet build` zero warnings. `dotnet test` 1232/1232 pass (Integration tier,
same existing Story tests — no new tests needed; this is a compiler-warning fix, not a behavioral
change).

## Feature 5 — Story Browsing & Display

- **L1 — Stage 5.** `StoryListing` warm partition is the projection anchor; sound.
- **L2 — Stage 5** (was Stage 2, reclassified from 4 before that). `ServerStoryReadService` (renamed
  from `DbStoryReadService` — see Feature 4's L2 RESOLVED note for the `IDbContextFactory` →
  direct-injection fix, same cell) has `GetStoryByIdAsync` (→ `StoryDetailsDTO`) and
  `GetStoryForEditAsync`, both correct `ReadOnlyApplicationDbContext` `.Select()` projections — they
  *work* and match spec/conventions. **WU12 (2026-06-22) closed the listing gap:** the content-rating
  filter is a global EF named query filter (`ApplicationDbContext.OnModelCreating`, named
  `"ContentRating"`) sourced from a new `IActiveUserContext` (see `cross-cutting.md` "Content Rating
  Filtering"/"Active-User Context"), not a per-method `.Where` — so it applies automatically to every
  `Stories` query, including these listing methods, with no per-call vigilance required. Listing scope
  landed minimal as planned: `StoryListingDto` minted, plus `GetListingsByIdsAsync(int[])` (the §6.6
  building block, reorders results to match input id order, silently drops ids the filter excludes) and
  `GetRecentListingsAsync(page, pageSize)` (one unfiltered-by-criteria browse projection, ordered by
  `LastUpdatedDate DESC`). `GetListingsAsync(StoryFilterDto)` remains explicitly deferred to WU23 — its
  filter shape isn't real until `ResultsFilterPanel` exists, and adding it later is purely additive.
  **WU13 (2026-06-23) additive DTO extension:** `StoryListingDto` gains `ShortDescription (string?)`
  projected from `StoryListing.ShortDescription` (warm partition, already stored, MaxLength 500).
  No migration. Additive — does not contradict the WU12 contract; L2 remains Stage 5.
  **How verified (DTO extension):** via `/dev/wu12/listings/recent` and `/dev/wu12/listings/by-ids` (kept as standing
  dev tools, not removed) against fixture stories (ids 5/6/7, fixture tags 10/11, kept per explicit user
  instruction for later analysis rather than deleted as the original plan's step 4 specified) —
  confirmed the content filter both directions: anonymous (`/dev/wu12/whoami` unauthenticated) saw only
  the Teen-rated fixture story, while `TestUser` (claims `ShowMatureContent=true`, via
  `/dev/wu12/login-as/TestUser`) saw all 4 stories including the Mature-rated ones.
  `GetListingsByIdsAsync` confirmed reordering a shuffled id list back to input order and silently
  dropping an id excluded by the active filter, per spec. **WU12.5 (2026-06-22)** migrated this
  verification into asserted, CI-runnable tests — `ContentRatingFilterTests` and `RecentListingsTests`
  in `TheCanalaveLibrary.Tests.Integration` cover the same both-directions filter check and
  reorder/drop behavior against a real Postgres; the dev-diagnostics endpoints are no longer the
  source of truth for this behavior (see `canalave-conventions/testing.md`).
  **WU13 (2026-06-23) StoryCard slice complete (Stage 5 for this slice):** `SharedUI/Stories/StoryCard.razor`
  built as a pure leaf (no service injection). Contract: `[EditorRequired] StoryListingDto Story`,
  `UserStoryInteractionStateDto? InteractionState` (batch-loaded by parent, forwarded to
  `UserStoryInteractionPanel`), `bool IsOwnStory`, 4 gated `EventCallback`s (OnDiscoverFromStory,
  OnCopyLink, OnReport, OnDownload). Composes `TagChip` (read-only) + `UserStoryInteractionPanel` in
  Listing context. Author byline is a plain hyperlink — NOT `UserCard` (spec §5.30.7). Cover art uses
  stored `CoverArtRelativeUrl` verbatim with `_coverArtFailed` `@onerror` fallback placeholder. Computed
  display: `WordCountDisplay` (3-tier K/M suffix), `StatusLabel`/`StatusBadgeClass` (switch over all 9
  `StoryStatusEnum` values), `RatingLabel`/`RatingBadgeClass` (switch over `Rating` E/T/M). Caret: always-
  present "View Story" link + `HasDelegate`-gated optional items, `InvokeAndClose` helper (mirrors
  `UserCard`). Root is `relative flex flex-col` with internal padding, no outer margin (Outer Margin Rule).
  *This slice only*: L3-Logic / L3.5-Structure / L4-Style cells for Feature 5 stay at 4/4/1 because
  `StoryPage` dispatcher (flicker, catch-all route), `StoryDesktop`/`StoryMobile` layout, and `StoryDeck`
  still hold those cells. The StoryCard contract is now minted, so WU14 (`StoryDeck`) and all listing
  consumers have their key dep satisfied. Step 0 of WU13 also renamed the WU16 component from
  `StoryInteractionPanel` to `UserStoryInteractionPanel` (all references updated repo-wide before any
  StoryCard work, so the card references the final name from the start).
  **How verified (WU13 slice):** `dotnet build` green (8 projects, 0 errors, 2 pre-existing warnings).
  `dotnet test` green: 105 Unit + 107 RazorComponents + 109 Integration = 321 total. RazorComponents tier
  (`StoryCardTests.cs`, 30 tests, JSInterop.Loose, registers `FakeUserStoryInteractionWriteService`):
  title link, author byline link/plain-text null-author, tag count + read-only, ShortDescription
  tooltip/null, WordCountDisplay theory (8 InlineData cases), cover-art fallback/lazy-loading, status/
  rating badge theories, panel composition (blank-slate + IsOwnStory), caret HasDelegate gating.
  L4-Style visual sign-off pending — requires live server check (fixture stories 5/6/7; cover art with/
  without; with/without tags/ShortDescription; one IsOwnStory; caret; gap spacing in a grid parent).
  **WU14 (2026-06-23) StoryDeck deck-slice built (Stage 5 for this slice — cells stay 4/4/1):**
  `SharedUI/Stories/StoryDeck.razor` built as a pass-through layout composite. Three-state
  internally: `Stories is null` → loading (inline text, skeleton-card upgrade is additive); `Count == 0`
  → customisable `EmptyMessage`; populated → `grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6`
  of `StoryCard` leaves + unconditional `PaginationControls` (self-hides at `TotalPages ≤ 1`).
  Contract: `[EditorRequired] IReadOnlyList<StoryListingDto>? Stories`, `IReadOnlyDictionary<int,
  UserStoryInteractionStateDto>? InteractionStates` (batch-loaded by parent, keyed by `StoryId`),
  `int? CurrentUserId` (deck computes `IsOwnStory` per card), `string EmptyMessage`, pagination
  forwards (`CurrentPage`, `PageSize`, `TotalCount`, `EventCallback<int> OnPageChanged`). No service
  injection. Caret callbacks deferred — additive when first consumer (WU28/34/38) needs them.
  Pre-implementation doc-touch (moment 1): fixed two stale areas in `layer3.5-structure.md` —
  deleted a duplicate StoryDeck paragraph; corrected the "Loading States" code example (old example
  showed the *page* doing the null/empty branch and passing only a populated array into `<StoryDeck>`,
  contradicting the plan; new example shows the page passing nullable `Stories` to the deck, which
  branches internally). **Test tier:** RazorComponents — `StoryDeckTests.cs` (14 tests): null →
  loading/no-card; empty → default+custom EmptyMessage/no-card; populated → N cards + grid; IsOwnStory
  match/non-match/null CurrentUserId; InteractionStates IsFavorite forwarding (active span appears);
  null states → no active span; PaginationControls shown/hidden/self-hidden; OnPageChanged bubbles.
  Mutation sanity confirmed: inverting the populated branch (`Count >= 0`) → 6 tests fail; reverted,
  suite green. L4 visual sign-off pending (Stage-6 gate): bUnit cannot verify Tailwind responsive
  breakpoints or the loading visual; requires live-server render check (1→2→3-col grid, empty message,
  loading state, pager shown/hidden). **How verified:** `dotnet build` green (8 projects, 0
  warnings/errors). `dotnet test` green: 112 Unit + 136 RazorComponents + 133 Integration = 381 total.
- **L3-Logic — Stage 5 (WU25, 2026-06-24).** `StoryPage` rebuilt: route `/story/{StoryId:int}/{*StorySlug}`
  (catch-all cosmetic slug); `[PersistentState]` on `Story` and `Chapters` (kills prerender→interactive
  double-fetch flicker); anonymous-safe `[CascadingParameter] Task<AuthenticationState>? AuthState`
  to resolve `_currentUserId`; loads `GetStoryByIdAsync` + `GetChapterListAsync`; computes
  `_isAuthor = _currentUserId.HasValue && Story.AuthorId == _currentUserId`.
  `StoryDetailsDTO` extended (additive): `int? AuthorId`, `string? CoverArtRelativeUrl`, `Rating Rating`,
  `StoryStatusEnum Status`, `IReadOnlyList<TagChipDto> Tags`. `GetStoryByIdAsync` projection updated to
  two-step intermediate row (mirrors listing service; sprites resolved in memory via existing `ToTagChip`).
  `GetChapterListAsync(int storyId)` added to `IChapterReadService` + `ServerChapterReadService`:
  two-step query (chapters + non-primary versions via `SelectMany`, grouped in memory per the
  `GetChapterVersionsAsync` pattern); returns `IReadOnlyList<ChapterListEntryDto>`.
  `ChapterListEntryDto` minted in `Core/Chapters/` — `(int ChapterNumber, string Title, int WordCount,
  bool IsPublished, IReadOnlyList<ChapterVersionDto> AlternateVersions)` (non-primary accessible versions
  only; reuses existing `ChapterVersionDto`).
  **How verified (2026-06-24):** `dotnet build` green (0 errors, 0 new warnings). Integration tier:
  `StoryDetailTests` (15 tests, Testcontainers Postgres) — `GetStoryByIdAsync` new fields (`AuthorId`,
  `CoverArtRelativeUrl`, `Rating`, `Status`, empty tags, sprite-resolved tags, mature-content filter both
  directions, null-story); `GetChapterListAsync` (empty list, ordering, single-version empty alternates,
  non-primary alternate present/absent by rating ceiling, mature viewer vs. non-mature, unpublished chapter
  `IsPublished=false`). All 15 pass.
- **L3.5-Structure — Stage 5 (WU25, 2026-06-24).** `StoryDesktop`/`StoryMobile` rebuilt: params
  `StoryDetailsDTO Story`, `IReadOnlyList<ChapterListEntryDto> Chapters`, `int? CurrentUserId`,
  `bool IsAuthor`. §5.28 layout: title → metadata row (author link, rating, status, word count,
  publish/updated dates, tag chips, author-only "Edit Story" link) → cover art (with `@onerror`
  fallback) → long description (`RichTextView`) → chapter selection (`ChapterList` leaf) →
  recommendations (`RecommendationSection`).
  **`ChapterNavigation` is NOT used on the story landing page.** It is reading-context-only
  (`CurrentChapterNumber` is `[EditorRequired]`; renders prev/next + "Chapter N" dropdown). The story
  landing page uses the new `ChapterList` leaf (`SharedUI/Chapters/`): one row per chapter linking to
  the primary version, with non-primary alternates as indented sub-rows labeled `Title - VersionName`.
  `ShowDrafts=true` for authors (shows unpublished chapter rows with a "Draft" marker).
  **How verified (2026-06-24):** RazorComponents tier — `ChapterListTests` (14 tests): single-version
  row + primary URL, multi-version sub-rows with `Title — VersionName` label + version URLs, fallback to
  `"Version {N}"` when `VersionName=null`, `ShowDrafts=false` hides unpublished / `=true` shows with
  "Draft" marker, empty list message, word-count display theory (3 cases). `StoryDesktopTests` (22 tests):
  title in `<h1>`, author link/plain-text, Edit Story link gated by `IsAuthor`, status/rating badge
  theories (4+3), word-count theory (3), tag chips read-only, cover img vs. null, long description content,
  chapter list composition, chapters section absent/present by `IsAuthor`, `RecommendationSection`
  composition (GetForStory called with StoryId; anonymous no CTA). `StoryMobileTests` (21 tests): same
  coverage as Desktop. All 57 RazorComponents tests pass.
- **L4-Style — Stage 1.** All Tailwind tokens; visual sign-off pending human review (Stage-6 gate,
  consistent with WU13/WU14/WU24 precedent — bUnit cannot verify layout/responsive breakpoints).
- **L5 — Stage 4.** Only `GET /api/stories/{id}` mapped; `HttpStoryReadService.GetStoryForEditAsync`
  calls `/{id}/edit` which is unmapped. Listing endpoints absent.
- **L6 — Stage 2.** Story-centric filtered indexes pending.

## Feature 8 — Story Arcs
- **L1 — Stage 5.** `StoryArc` + unique indexes `(StoryId,Title)`, `(StoryId,SortOrder)`. Overlap/gap
  validation is C#-side (not yet written).
- **L2 — Stage 2.** No arc service.
- **L3 / L3.5 — Stage 1 (conceptual, §8.2).** Arc-management UI was never designed. Resolve in chat.
- **L4 — Stage 1** (blocked). **L5 — Stage 2.**

## Feature 9 — Series & Ordering
- **L1 — Stage 5.** `Series` + `SeriesEntry` (composite key `(SeriesId,StoryId)`, `OrderIndex`), unique
  `(AuthorId,Name)`. **L2/L3/L3.5 — Stage 2.** **L4 — Stage 1. L5 — Stage 2.**

## Feature 10 — Story Relationships
- **L1 — Stage 5.** `StoryRelationship` composite key `(Source,Target,Type)`, type lookup seeded
  (Inspired By/Prequel/Sequel/Companion), `StatusId` enum→short. One-way directional per §5. Cascade from
  both source and target story. **L2/L3/L3.5 — Stage 2.** **L4 — Stage 1. L5 — Stage 2.**

## Feature 45 — View Count Tracking
- **L1 — Stage 5.** `Story.ViewCount`. **L2 — Stage 2** (MVP direct increment unbuilt).
- **L3-Logic — Stage 2.** Client ping (5s timer / first scroll) unbuilt. **L3.5 — N/A** (no dedicated
  component). **L4 — N/A. L5 — Stage 2.**
- **L7 — Stage 2.** Redis `INCR` + drain worker (write-behind pattern 1). Interface unchanged from MVP.

---

### Cluster-level notes
- The commented-out `[NotMapped] IReadOnlyCollection<IStoryTag> StoryTags => StoryTags.ToList();` in
  `Story.cs` would have been infinitely recursive — correctly left disabled; the explicit-interface version
  is the live one.
