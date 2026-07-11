# Audit — Stories/

**Features:** 4, 5, 8, 9, 10, 45 (Story creation/editing, browsing/display, arcs, series, relationships,
view-count). Largest cluster.

## Shared Context

**Entities (Core/Stories/):** `Story` (hot partition — status, counts, dates, FKs), `StoryListing`
(warm — title, short desc, cover art URL; carries the FTS `SearchVector` generated column), `StoryDetail`
(cold — long desc, slug, `PostApprovalStatus`). Vertical-partition trio with 1-to-1 cascade. `Story`
implements `IEditableStoryProperties` via explicit interface implementation (`[NotMapped]` projections
across the three partitions) — a deliberate, somewhat advanced pattern that lets one object satisfy the
edit contract. Also here: `StoryArc`, `StoryRelationship` (+type lookup), `StoryTag`/`StoryCharacter`
live in the model set but belong to Tags. **`Series`/`SeriesEntry` moved to their own `Core/Series/`
vertical cluster (WU41, 2026-07-11)** — same namespace (`TheCanalaveLibrary.Core`), separate folder;
see Feature 9 below.

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

**WU-ErrorHandling note (2026-07-06).** `StoryEditorPage` embeds `DraftAutosave`
(`draft:story:{id|new}`; prose fields only — Title/ShortDescription/LongDescription; structured
tag/character picker state deliberately excluded, re-selecting is cheap, re-typing is not);
backup cleared on successful submit. `StoryPropertiesForm` renders errors via `InlineAlert` and
gained `SetLongDescriptionAsync` (Quill push for restore). The page's generic catch routes
through `ExceptionPresenter` + `LogError`. `StoryDeck` wraps each `StoryCard` in a compact
`story-card` boundary island — one broken card degrades to a tile, the deck survives. Strategy:
`error-handling.md` §"Error Handling Strategy"; detail: `workplan.md` WU-ErrorHandling.

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
  mod/admin OR in this gate — moderation is a separate WU34 path (see `identity-and-authorization.md` "Security vs
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
- **L6 — Stage 5 (WU-L6, 2026-07-07 — resolved, no creation-side DDL).** The write path needs no
  index beyond the existing PK/unique/slug set; the story-table read indexes ("to be added by
  query need") landed as the sort spines under Feature 5 (see its L6 note).

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

- **WU38c/WU38d additive touches (2026-07-11), cells stay Stage 5:**
  - **Download links (Feature 54's trigger):** new `StoryDownloadLinks` leaf (six per-format
    anchors with the `download` attribute — a file download must bypass the circuit,
    `layer2-services.md` §"File Downloads Bypass the Circuit") mounted in the metadata block of
    `StoryDesktop`/`StoryMobile`; `StoryCard`'s dead `OnDownload` EventCallback replaced by an
    expandable Download submenu of the same links (parameter removed; `StoryCardTests` updated).
  - **"Also posted on" (Feature 53's display + edit surfaces):** `StoryExternalLinksRow` after the
    chapter list / before `RecommendationSection` on both story layouts;
    `StoryPropertiesForm`/`StoryEditorPage` gained the link rows + original-dates section and
    `ExternalPlatforms` plumbing; `StoryDetailsDTO.ExternalLinks` + `GetExternalPlatformsAsync`
    added to the read service. Detail + verification: `audit/Moderation.md` Feature 53.
  - **Bulk chapter import entry point (Feature 63):** `StoryChapterImport` section on
    `StoryEditorPage` (edit mode). Detail: `audit/Import.md`.
- **L1 — Stage 5.** `StoryListing` warm partition is the projection anchor; sound.
- **L2 — Stage 5** (was Stage 2, reclassified from 4 before that). `ServerStoryReadService` (renamed
  from `DbStoryReadService` — see Feature 4's L2 RESOLVED note for the `IDbContextFactory` →
  direct-injection fix, same cell) has `GetStoryByIdAsync` (→ `StoryDetailsDTO`) and
  `GetStoryForEditAsync`, both correct `ReadOnlyApplicationDbContext` `.Select()` projections — they
  *work* and match spec/conventions. **WU12 (2026-06-22) closed the listing gap:** the content-rating
  filter is a global EF named query filter (`ApplicationDbContext.OnModelCreating`, named
  `"ContentRating"`) sourced from a new `IActiveUserContext` (see `content-safety.md` "Content Rating
  Filtering", `identity-and-authorization.md` "Active-User Context"), not a per-method `.Where` — so it applies automatically to every
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
- **L5 — Stage 2**, same resolution as Feature 4 above — see WU-FilterRevamp (`HttpStoryReadService`/
  `HttpStoryWriteService` were deleted as dead code, not left with an unmapped endpoint).
- **L6 — Stage 5 (WU-L6, 2026-07-07).** The two discovery sort spines built in `L6_IndexBatch`:
  `ix_stories_published_date` (+ `ix_stories_last_updated_date` for `GetRecentListingsAsync` /
  Relevance tie-break). Measured at 3k seeded stories: DatePublished page-1 p50 0.39→0.09 ms
  (−76%); the §8.7 exclusion-probe page −68% (riding these + the restored USI `ignored` partial).
  Story-centric USI mirror indexes were REJECTED under R4 — no story-centric interaction query
  exists (favorite counts are denormalized on `UserStat`). Detail: `layer6-indexes.md`.

## Feature 8 — Story Arcs
- **L1 — Stage 5.** `StoryArc` + unique indexes `(StoryId,Title)`, `(StoryId,SortOrder)`. Overlap/gap
  validation is C#-side (not yet written).
- **L2 — Stage 2.** No arc service.
- **L3 / L3.5 — Stage 1 (conceptual, §8.2).** Arc-management UI was never designed. Resolve in chat.
- **L4 — Stage 1** (blocked). **L5 — Stage 2.**

## Feature 9 — Series & Ordering

- **L1 — Stage 5.** `Series` (`Core/Series/Series.cs` as of WU41) + `SeriesEntry` (composite key
  `(SeriesId,StoryId)`, `OrderIndex`), unique `(AuthorId,Name)`.

### WU41 settled-vs-open note (2026-07-11, before build — Doc-Touch moment 1)

The historical Gemini planning discussions only ever modeled "series" as a `StoryRelationship`
*type* (`RelationshipType = 'Series'` with an `OrderIndex`); the first-class `Series`/`SeriesEntry`
tables were a later architectural decision, so those notes don't answer WU41's UX questions. The
following were settled with the user 2026-07-11 and must not be revisited without going back through
the user:

1. **Membership scope:** a series contains **only the owner's own stories**. The write service gates
   every add on `story.AuthorId == series.AuthorId == ActiveUser.UserId`.
2. **Management:** a **dedicated series page** (`/series/{id}/edit`) — no field added to the story
   editor/`StoryPropertiesForm`. Mirrors `GroupCreateEditPage`.
3. **Browse surfaces:** public per-series page (`/series/{id}`) + a **Series tab on the profile**
   (`ProfileTab.Series`) + a **"My Series"** owner list (`/series`). No global `/series` directory
   (post-MVP, not scoped here).
4. **Story-page display:** a "Part of series X — Part N of M" box **with prev/next in-series
   navigation**, rendered on `StoryDesktop`/`StoryMobile`.
5. **A story may belong to multiple series.** The existing `SeriesEntry` PK `(SeriesId, StoryId)` has
   no unique constraint on `StoryId` alone — the schema already permits this, so no L1 migration was
   needed. `StoryPage` therefore renders a *list* of `SeriesMembershipBox` (one per series), each with
   its own position + prev/next. AO3-style semantics.
6. **Viewer-visible counting:** `StorySeriesMembershipDto`'s Position/Count/PrevStoryId/NextStoryId are
   computed only over series members that survive the viewer's `ContentRating`/`IsTakenDown` read
   filters (an explicit join through `Story` in `ServerSeriesReadService.GetMembershipsForStoryAsync`,
   not a bare `SeriesEntry.StoryId` projection) — so "Part 2 of 3" always matches what the viewer can
   actually reach and prev/next never link to a story hidden from them. `SeriesListingDto.StoryCount`
   and `SeriesDetailDto.OrderedStoryIds`, by contrast, are the raw (unfiltered) entry set — mirroring
   `GroupDetailDto.StoryIds`/`Group.MemberCount`'s existing precedent of "count is a cheap raw number,
   the hydrated list (`IStoryReadService.GetListingsByIdsAsync`) is the authority and silently drops
   what the viewer can't see." This mismatch (a card might say "3 stories" but the deck shows 2) is the
   same accepted tradeoff Groups already ships with.

**Done (2026-07-11):** built per the above. `Core/Series/` (entities + DTOs + `ISeriesReadService`/
`ISeriesWriteService`), `Server/Series/` (`ServerSeriesReadService`/`ServerSeriesWriteService`,
CQRS-lite inheritance mirroring `ServerGroupReadService`/`ServerGroupWriteService`; owner-gate via
`RequireOwnerAsync`/`RequireAuthenticatedUser`; `Description` sanitized once on save;
pre-insert duplicate-name check surfaces as `SeriesValidationException`, matching
`ServerTagWriteService`'s pattern, rather than surfacing the raw unique-index `DbUpdateException`).
`SharedUI/Series/` — `SeriesCard` (leaf), `SeriesMembershipBox` (leaf), `SeriesPage` (public detail +
`StoryDeck`), `SeriesCreateEditPage` (owner-gated create/edit/add/remove/reorder/delete),
`MySeriesPage` (`/series`, owner listing). Integrated into `StoryPage`/`StoryDesktop`/`StoryMobile`
(membership boxes), `ProfilePage`/`ProfileDesktop`/`ProfileMobile` (Series tab), `CreateMenu` ("New
Series"), `UserMenu` ("My Series"). `ExceptionPresenter` extended with `SeriesValidationException`.
`ProfileTab` extended with `Series = 5` / slug `"series"` (additive — not persisted, so no migration
concern). One test-fixture gap found and fixed during full-suite verification (not anticipated in the
plan): `ProfilePageTests`/`FakeProfileTestServices` didn't register the newly-required
`ISeriesReadService`, breaking `TabSwitch_OnSameInstance_ReloadsTabPayload` — added
`FakeSeriesReadService` (all-empty defaults, same shape as the other Fake*ReadServices there).

**Two real runtime bugs found and fixed via the L4.5 browser-verification pass (not anticipated in
the plan; both are the same dispatcher-reload class as WU-ComponentSoundness's F1 StoryPage fix):**
1. `SeriesCreateEditPage` maps two `@page` routes ("/series/new" and "/series/{SeriesId:int}/edit")
   onto the same component type. `HandleSubmitAsync`'s post-create `Nav.NavigateTo($"/series/{newId}/edit")`
   reuses the same component instance — `OnInitializedAsync` (which loaded the create-mode blank
   state) never re-fires; the page only ever implemented that one lifecycle method. Symptom observed
   live: after creating a series, the "edit" page rendered as if still empty/create-mode (blank name,
   no member list, no add-story picker) even though the series had actually been created correctly in
   the DB. Fixed by adding `OnParametersSetAsync` with an initialized-guard + a route-changed guard
   (`SeriesId` vs. create-mode), mirroring `StoryPage`/`ProfilePage`/`GroupPage`'s dispatcher pattern.
   Regression test added: `SeriesCreateEditPageTests.PostCreateRedirect_OnSameInstance_ReloadsEditModeData`
   (bUnit `cut.Render(...)` re-set, same technique as `ProfilePageTests.TabSwitch_OnSameInstance_ReloadsTabPayload`).
2. `StoryPage.OnParametersSetAsync` (the existing in-place-story-navigation reload path, added
   WU-ComponentSoundness) reloads `Story`/`Chapters`/`_usiState` on a `StoryId` change but the new
   `_seriesMemberships` load was only wired into `OnInitializedAsync`. Symptom observed live: clicking
   the series membership box's "Next" link (an in-place `/story/{id}` navigation, same StoryPage
   instance) left the *previous* story's series box on screen — "Part 1 of 2" with Next pointing back
   at the story the viewer was now on. Fixed by adding the `SeriesReadService.GetMembershipsForStoryAsync`
   reload to `OnParametersSetAsync` alongside the other per-story state. No dedicated bUnit regression
   test — `StoryPage` has no bUnit suite of its own even for its original F1 fix (WU-ComponentSoundness);
   covered by this L4.5 pass instead, consistent with that precedent.

- **L2/L3-Logic/L3.5-Structure — Stage 5.** See test tiers below.
- **L4-Style — Stage 1** (visual sign-off pending, per WU8/WU13/WU23/WU28/WU37/WU42/WU43 precedent —
  functionally verified live, below, but no dedicated visual/token polish pass).
- **L4.5-Browser — Stage 5 (2026-07-11).** Real-circuit verification via `mcp__claude-in-chrome__*`
  against the dev server (`AuthorAlpha`/`TestUser` fixtures): My Series (empty state, owner-scoped) →
  Create Series → post-create redirect into edit mode (bug 1 above, found + fixed here) → add two
  stories via the picker → reorder via ↑ (persisted) → public SeriesPage (`StoryDeck` in reordered
  order, owner Edit link) → StoryPage membership box ("Part 1 of 2", disabled Previous, active Next)
  → click Next → in-place navigation to the second story (bug 2 above, found + fixed here) — box now
  correctly reads "Part 2 of 2" with active Previous, no Next → profile Series tab as owner (Edit
  links present) and as a different viewer (`TestUser`, Edit links correctly absent) → `UserMenu`
  "My Series" and `CreateMenu` "New Series" entries present → duplicate-name create attempt surfaces
  "You already have a series named …" inline → delete via `ConfirmDialog` (destructive variant) →
  redirected to My Series, series gone from the list. Ground truth confirmed via `psql`: the deleted
  series' `SeriesEntry` rows cascaded away; its two member stories (`story_id` 1, 3, 7 range) survived
  untouched; the surviving series' `series_entries.order_index` matched the reorder performed in the UI.
- **L5 — Stage 2** (rides the future site-wide WASM interactivity flip, not WU41-specific).
- **Verified (2026-07-11):** `dotnet build` 0 errors/warnings (8 projects). `dotnet test` full
  solution green: 541 Unit + 533 RazorComponents + 462 Integration = **1536 tests**. Mutation-sanity:
  temporarily replaced the `Story`-joined query in `GetMembershipsForStoryAsync` with a bare
  `SeriesEntry.StoryId` projection (bypassing `ContentRating`/`IsTakenDown`) —
  `GetMemberships_MatureMemberHiddenFromMatureDisabledViewer_ExcludedFromPositionCountAndNext` failed
  as expected; reverted, suite green again. `check-design-tokens.ps1`: the only 2 findings are
  pre-existing in `TreeSearchResultBadge.razor` (WU44), unrelated to this unit.
- **Tests:** Unit (`SeriesValidationsTests` — 11 tests, `CreateSeriesDto`/`UpdateSeriesDto.CanSave`);
  Integration (`SeriesServiceTests` — 26 tests, Testcontainers Postgres — CRUD owner-gating,
  cross-author add rejection, append/reorder/remove `OrderIndex`, duplicate-name rejection (per-author,
  not global), cascade delete, multi-series membership, and the content-filter-drop case for
  Position/Count/Next); RazorComponents (`SeriesCardTests` + `SeriesMembershipBoxTests` — 15 tests,
  leaf components; `SeriesCreateEditPageTests` — 1 test, the same-instance-reload regression above).
  **Scope note:** no *general* `SeriesCreateEditPageTests` CRUD-UI coverage was added beyond that one
  regression test — matching the existing precedent that `GroupCreateEditPage` (the closest analog)
  has no page-level bUnit suite either; the owner-gate and CRUD logic are exercised at the Integration
  tier (the real authority — the page's pre-check is only a UX nicety).

## Feature 10 — Story Relationships
- **L1 — Stage 5.** `StoryRelationship` composite key `(Source,Target,Type)`, type lookup seeded
  (Inspired By/Prequel/Sequel/Companion), `StatusId` enum→short. One-way directional per §5. Cascade from
  both source and target story. **L2/L3/L3.5 — Stage 2.** **L4 — Stage 1. L5 — Stage 2.**

## Feature 45 — View Count Tracking

**Built end-to-end (WU-SignalBuffering, 2026-07-06).** Divergence from spec (§7 `Story.ViewCount`
"Updated by Redis background worker"; §5.3's view-count sort exclusion now closes the last gap):
views are a **non-sortable, on-demand informational metric** — never a sort key, never a permanent
badge (view-count-not-a-sort settled 2026-07-06, the anti-popularity-snowball philosophy applied to
the last popularity-shaped surface). `Story.ViewCount` (and the never-written
`ChapterContent`/`BaseBlogPost` copies) **dropped** — a hot mutable counter on the hot read row is
a write-amplification trap; accumulation lives in **`daily_story_stats`** (per-story/day,
migration-managed raw DDL, no EF model — ground truth, NOT a rebuildable mart; PK
`(story_id, stat_date)`, partition-ready; FK CASCADE cleans history on story delete). Migration:
`R2_ViewCountToDailyStoryStats`.

- **L1 — Stage 5.** `daily_story_stats` DDL applied + verified (Testcontainers migrate + live dev DB).
- **L2 — Stage 5.** `IViewCountWriteService.RecordViewAsync` (anonymous counts — no auth gate) →
  `ViewCountBuffer` (per-story sum, O(1)) → `ViewCountFlusher` (batched `unnest … ON CONFLICT`
  additive upsert into today's UTC row — the `+=` merge accepts a rare retry-replay over-count on
  a lossy metric, documented in-file; EXISTS guard vs deleted stories; restore-on-failure) →
  `ViewCountFlushWorker` (5 s, shutdown drain, removed from the test host by `TestAppFactory`).
  Lifetime total = `IStoryReadService.GetStoryTotalViewsAsync` (raw `SqlQuery` SUM — on-demand
  only, never in a listing projection). Telemetry: `CanalaveTelemetry.ViewCount`.
- **L3-Logic — Stage 5.** `view-ping.js` (fires once on first scroll OR 5 s dwell, never page
  load — bots/bounces filtered) registered by `StoryPage` (re-arms on in-place story change;
  `IAsyncDisposable`); `StoryViewStats` composite (sanctioned user-input-driven injection) in
  `StoryCard`'s caret dropdown — fetches the SUM only when the user asks.
- **L3.5/L4 — Stage 5.** Dropdown item reuses the caret menu's structure/classes.
- **L4.5-Browser — Stage 5 (2026-07-06).** Story-page scroll fired the ping → `daily_story_stats`
  row landed post-flush (psql ground truth); "View stats" reveal showed "1 view" (matches SUM);
  sort dropdowns verified view-free (`/discover`: Random | Date published only).
- **L5 — Stage 2** (WASM ping endpoint lands with the global flip). **L8 — N/A.**
- **Tests:** Unit (`ViewCountBufferTests`), Integration (`ViewCountFlushTests` — buffering,
  same-day accumulation, cross-day SUM, zero default, deleted-story guard, FK cascade),
  RazorComponents (`StoryViewStatsTests` — no fetch until asked, formatted reveal,
  singular/plural, reset on story change).

### WU-ComponentSoundness Stage note (2026-06-27)

**Cells affected:** F5 L3-Logic (StoryPage, StoryDeck) + F7 L3-Logic (ChapterReadingPage) — correctness
polishes inside already-aligned Stage-5 cells; no stage transition.

**F2 — StoryDeck list-keying (data-corruption bug, now closed):**

`StoryDeck.razor` now carries `@key="story.StoryId"` on `<StoryCard>` in the `@foreach` loop.

Root cause: `UserStoryInteractionPanel.OnParametersSet` caches `State → _localState` once (`if
(_localState is null) _localState = State;`) and stops syncing. Without `@key`, Blazor matched `<StoryCard>`
instances **positionally**, so paginating or filter-swapping recycled the position-0 instance for a new
story. The recycled panel's `_localState` still held the prior story's state; `FlushAsync` subsequently
wrote those booleans to the wrong `StoryId`, silently corrupting the interaction row.

Fix: `@key="story.StoryId"` forces Blazor to destroy and recreate the keyed component tree whenever the
`StoryId` in that slot changes — the fresh instance starts with `_localState = null` and seeds correctly
from the new story's `State` parameter. (Convention recorded in `layer3.5-structure.md`
§"`@key` on `@foreach` over stateful children".)

Covering tier: **RazorComponents** — `StoryDeckTests.KeyedList_WhenStorySwapped_PanelReflectsNewStorysState_NotPreviousStorysState`.

---

**F1 — StoryPage lifecycle reload (`[PersistentState]` gotcha):**

`StoryPage.razor` now implements the MessagesPage route-dispatcher pattern: `_initialized` flag +
`_loadedStoryId = int.MinValue` sentinel; `OnInitializedAsync` keeps auth-resolution + first load (using
`??=` for anti-flicker); `OnParametersSetAsync` guards on `StoryId == _loadedStoryId` then does a plain
reassignment (`Story = await …;` — **not** `??=`) before loading.

Root cause: Blazor reuses the same component instance on same-route-template navigation; `OnInitializedAsync`
does not re-fire. The prior code loaded only in `OnInitializedAsync`, so navigating from
`/story/1/slug-a` to `/story/2/slug-b` without a full page reload left the old story's data on screen.

`[PersistentState]` gotcha: the `??=` guard in `OnInitializedAsync` is correct (anti-flicker across
prerender → interactive). But in `OnParametersSetAsync` the field is already non-null from the prior
prerender payload — `??=` would short-circuit the DB call for the new StoryId. Plain assignment
(`Story = await …`) is required there.

Covering tier: **manual boot gate** (no bUnit test — `[PersistentState]` prerender-to-interactive
handoff cannot be asserted in bUnit). Listed in verification checklist for the upcoming human E2E pass.

---

**F1 — ChapterReadingPage lifecycle reload + scroll-JS re-registration:**

`ChapterReadingPage.razor` now implements the MessagesPage pattern with composite key
`(StoryId, ChapterNumber, VersionOrder)` (sentinel: `int.MaxValue` for `VersionOrder` because `null` is
a valid value meaning "primary version").

`DisposeJsRegistrationAsync()` extracted from `DisposeAsync()` and called at the top of
`OnParametersSetAsync` before `LoadChapterAsync()`, so `readingProgress.dispose` releases the old
chapter's scroll subscription before the new chapter DOM is rendered. `OnAfterRenderAsync` dropped the
`firstRender` gate in favor of the `_jsRegistered` flag alone — re-registration fires on the render
after the new chapter loads, binding to the fresh `#chapter-body` element. `_progressReached90` is
reset inside `LoadChapterAsync()` so the 90%-scroll → `MarkStartedAsync` milestone fires correctly for
new Chapter 1s.

Covering tier: **manual boot gate** (JS-interop + multi-service; listed in E2E verification checklist).
Convention for the route-dispatcher pattern recorded in `layer3-logic.md`
§"Route-parameter dispatchers reload in `OnParametersSetAsync`".

---

### Cluster-level notes
- The commented-out `[NotMapped] IReadOnlyCollection<IStoryTag> StoryTags => StoryTags.ToList();` in
  `Story.cs` would have been infinitely recursive — correctly left disabled; the explicit-interface version
  is the live one.

## L4.5-Browser verification (2026-07-01) — F4 + F5 → Stage 5, one bug fixed same-session

Real-form pass as TestUser: `/story/new` (title, short desc, Quill long description, character
typeahead → Cynthia w/ Primary priority row, setting → Canalave City, genre → Adventure) →
Create Story landed on `/story/{id}/edit` with all values retained → title edit → Save Changes →
story page shows updated title, tags, and description. Discover cards and story-page display (F5)
exercised across the seeded corpus and the newly created story.

**Bug fixed:** `CreateStoryAsync` never stamped `PublishedDate`/`LastUpdatedDate` (the mapper
covers only `IEditableStoryProperties`), so UI-created stories carried `DateTime.MinValue` →
Postgres `-infinity` → story pages showed "Published Jan 1, 0001". Fixed: server-stamps both at
create (like `AuthorId`) and bumps `LastUpdatedDate` in `UpdateStoryAsync`. Verified via psql
after a browser round-trip.

**Coverage exception:** the cover-art `InputFile` → `IImageStorageService` browser interaction
could not be driven (browser-automation file-upload API mismatch in this session's tooling —
not an app defect); the storage service itself is Integration-covered and was WU12
endpoint-verified. Re-verify the InputFile wiring when a browser pass with working file upload
is available.
