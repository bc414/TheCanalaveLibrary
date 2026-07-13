# Audit — Discovery/

**Features:** 31 (search page), 32 (FTS), 33 (manual tree search), 34 (tag directory), plus the
below-the-line 59 (automatic tree search), 60 (tree-search data-mart worker), 61 (also favorited/
recommended). Layer-8 data-mart workers live here. Includes search-result narrowing (user-level overrides
of per-search-mode defaults).

## Shared Context

**Entities (Core/Models/):** `SearchMode`, `UserInteractionFilter`, `DefaultSearchSetting` (matrix),
`UserSearchSetting` (overrides), `UserCustomFilter`. Data marts: `AlsoFavoritedScore`,
`AlsoRecommendedScore`, `UserStoryTreeSearchEntry`. FTS lives on `StoryListing.SearchVector`
(generated `tsvector` column + GIN index `ix_story_listing_search_vector`, both in `OnModelCreating`).

**Nothing in Layers 2–7 of search/discovery is built.** The three-axis search engine (Source × Filter ×
Sort) exists only as spec §5.3.

---

## The two cross-cutting divergences

Both are **stale-code traps** (audit-summary §0), not intent contests: the spec is the recent refinement,
the relevant code is non-working, so the spec wins and the Stage-4 flags are warnings with a fixed
resolution direction (build to spec).

1. **Search/sort vocabulary predates the three-axis model** (shared root with Lookups L1): seeded
   `SearchMode` keys and the `DefaultSortOrder` enum are pre-revision. "RandomSearch" should not be a mode;
   `Favorites`/`LastUpdated`/`ViewCount` sorts are excluded by §5.3.3. The data model that the discovery
   queries will sit on therefore needs reconciliation first.

2. **Data marts are modeled as EF entities** (`AlsoFavoritedScore`, `AlsoRecommendedScore`,
   `UserStoryTreeSearchEntry` have POCOs, `DbSet`s, `HasKey`, and many filtered indexes in
   `OnModelCreating`). `layer8-data-marts.md` requires the opposite: **no EF model classes, no DbSets, no
   migrations** — these tables are raw-SQL-built and table-swapped. This is the divergence behind the
   Stage-4 L1/L6 calls on features 59/60/61.

---

## WU23 Shared Context — §8.7 Entity Renames + AllowInteractions (2026-06-23)

The three §8.7 entities are renamed in WU23 Phase 0. A **data-preserving rename migration**
(`RenameTable`/`RenameColumn` ops only — no drop/recreate) carries all three. Schema aligned with
C# names:

| Old name | New name | File moved to |
|---|---|---|
| `UserInteractionFilter` | `UserStoryInteractionFilterType` | `Core/Discovery/UserStoryInteractionFilterType.cs` |
| `DefaultSearchSetting` | `DefaultUserStoryInteractionFilterSetting` | `Core/Discovery/DefaultUserStoryInteractionFilterSetting.cs` |
| `UserSearchSetting` | `UserStoryInteractionFilterSetting` | `Core/Discovery/UserStoryInteractionFilterSetting.cs` |

Field rename: `InteractionFilterKey` → `UserStoryInteractionFilterKey` on all three entities
(column `interaction_filter_key` → `user_story_interaction_filter_key`).
Nav property: `InteractionFilterKeyNavigation` → `UserStoryInteractionFilterType`.
DbSet renames in `ApplicationDbContext` match.
Config class renames in `DiscoveryConfigurations.cs` match.

`AllowInteractions` (enum in `Core/Lookups/ModelEnums.cs`) → `SocialInteractionPermission`.
Stored as `short`; C#-only rename (no column change on `users.allow_profile_comments` /
`users.allow_private_messages`). `User.AllowProfileComments` / `User.AllowPrivateMessages`
property types updated.

**Still open (deferred post-WU23):** the per-`SearchMode` default-settings matrix
(`DefaultUserStoryInteractionFilterSetting` ×`UserStoryInteractionFilterSetting`) — §8.7, out of
scope for WU23. Random-preload / "give me more" discovery pagination → WU28.
Narrowing-within-fixed-source query → WU27/WU30.

## Feature 31 — Search Page (`/discover`)
- **L1 — N/A** (queries Story/USI/StoryListing). **L2 — Stage 2** (Source=All query; random preload /
  "give me more" remains WU28). **L3/L3.5 — Stage 5 (WU23, 2026-06-23).** **L4 — Stage 1.**
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; search page verified in a real WASM runtime during the
  flip's browser wave (random batch + filtered `POST /query` + sort switch). Full wave narrative +
  the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.
- **L6 — Stage 5 (WU-L6, 2026-07-07):** the DatePublished sort spine + restored USI partials serve
  the page-1 sorted listing (p50 0.39→0.09 ms) and the §8.7 exclusion probe (0.68→0.22 ms, −68%)
  at SeedTool volume; FTS GIN pre-existed; tag-probe additions rejected (PK already optimal,
  measured neutral). Detail: `layer6-indexes.md`, `TheCanalaveLibrary.PerfBaseline/results/`.

  **Settled for WU23 (2026-06-23, do not revisit):**
  - **Filter axes are the unit of reuse, not the panel.** `TagFilter` and `UserStoryInteractionFilter`
    are standalone axis components — the tree search page (WU28) reuses them directly without the panel.
    `ResultsFilterPanel` is one assembler of those axes, not the reusable unit.
  - **Both consumers use a batched Apply button** (live re-filtering would cause graph relayout in tree
    search as edge counts change).
  - **`StoryFilterDto`** (`Core/Discovery/`) shape: `TextQuery`, `IncludedTagIds`, `ExcludedTagIds`,
    `ExcludedInteractions (UserStoryInteractionTypeEnum list)`, `Sort (DefaultSortOrder)`, `Page`,
    `PageSize`. Content rating excluded (global EF filter). Source axis excluded (`GetListingsAsync`
    is Source=All; other sources use `GetListingsByIdsAsync`).
  - **`GetListingsAsync(StoryFilterDto)`** added to `IStoryReadService` / `ServerStoryReadService`
    as a two-step (filtered IQueryable → scalar ID page → delegate to `GetListingsByIdsAsync`).
    Mirrors `GetRecentListingsAsync`'s shape. "Deferred to WU23" note removed from the interface XML.
  - **`ResultsFilterPanel`** (`SharedUI/Discovery/`): coordination composite, injection-free, no
    ViewModel. Params: `ShowTagFilter`, `ShowTextSearch`, `ShowInteractionFilters`,
    `IReadOnlyList<DefaultSortOrder> AvailableSorts`, `StoryFilterDto? InitialFilter`,
    `EventCallback<StoryFilterDto> OnSearch`. Assembles `TagFilter` + `UserStoryInteractionFilter`
    + FTS input + sort select + "Apply Filters" button. Outer Margin Rule applies.
  - **Composition is page-level** — `ResultsFilterPanel` and `StoryDeck` are NOT bundled into a single
    composite. Spec §5.27 explicitly rejected a bundled `UserListPage`.

  **Still open (not settled in WU23, closed by WU28):** random-preload / "give me more" pagination
  (→ WU28, now settled — see below); narrowing-within-fixed-source queries (WU27/WU30 ✓);
  per-`SearchMode` default-settings matrix (§8.7 → WU28 Phase 1b, now settled).

  **L2 note (WU23):** partially advanced — `GetListingsAsync(StoryFilterDto)` (Source=All filtered
  query) is built. The random-preload and "give me more" pagination remain Stage 2 → WU28.
  **L2 (WU28, in progress):** `GetRandomBatchAsync` + `IDiscoveryDefaultsReadService` are the
  remaining L2 deliverables.

  **WU23 Stage note — L3/L3.5 (2026-06-23):**
  Built: `Core/Discovery/StoryFilterDto.cs` (sealed record: `TextQuery?`, `IncludedTagIds`,
  `ExcludedTagIds`, `ExcludedInteractions`, `Sort`, `Page`, `PageSize`); `Core/Tags/TagFilterSelection.cs`
  (axis emit contract); `SharedUI/Tags/TagFilter.razor` (include/exclude tag axis, cross-dedup,
  injection-free, emits `EventCallback<TagFilterSelection> OnChanged`);
  `SharedUI/UserStoryInteractions/UserStoryInteractionFilter.razor` (checkboxes "Hide stories I've…",
  injection-free, emits `EventCallback<IReadOnlyList<UserStoryInteractionTypeEnum>> OnChanged`);
  `SharedUI/Discovery/ResultsFilterPanel.razor` (coordination composite, `@code`-buffered,
  "Apply Filters" emits `EventCallback<StoryFilterDto> OnSearch`; Relevance hidden from dropdown when
  `_textQuery` is empty; `AvailableSorts` owner-supplied; defaults to `[DatePublished, Random]`).

  **How verified (WU23, 2026-06-23):** `dotnet build` green (8 projects, 0 errors). `dotnet test`
  green: 112 Unit + 198 RazorComponents + 142 Integration = **452 total**.
  - **Integration** (`StoryListingsTests.cs`, 9 tests, Testcontainers Postgres): tag include (AND),
    tag exclude (NONE), FTS text filter, interaction exclusion (authenticated viewer; verifies FK-safe
    via DataSeeder TestUser), interaction exclusion (anonymous viewer sees all), DatePublished sort
    order, paging / TotalCount independence from PageSize, content-rating global filter still applied,
    mutation sanity (exclusion test fails when predicate dropped, reverted).
  - **RazorComponents** (`ResultsFilterPanelTests.cs`, 8 tests): `ShowTextSearch`/`ShowTagFilter`/
    `ShowInteractionFilters` hide their axis; Apply emits correct DTO with default state; trimmed
    TextQuery; Relevance hidden when no text, appears after input; `InitialFilter` seeds sort +
    excluded-interactions; Apply resets Page to 1 regardless of `InitialFilter.Page`.
    (`UserStoryInteractionFilterTests.cs`, 7 tests): default render (one checkbox per `DefaultKinds`);
    no checkboxes pre-checked; toggle Ignore adds to excluded list; toggle two kinds emits both;
    uncheck removes from list; `ExcludedKinds` seed pre-checks correct checkboxes; `AvailableKinds`
    restricts rendered checkboxes.
  - **L4-Style sign-off:** pending live-server visual check (Stage-6 gate, consistent with WU8/WU13
    precedent). Cells stay Stage 1 in `status.md` until that check is done.

  **WU28 settled-vs-open (2026-06-25, do not revisit):**

  *Random batch:* `GetRandomBatchAsync(StoryFilterDto filter, int batchSize)` — plain random draw
  from the post-filter valid set (`OrderBy(EF.Functions.Random()).Take(batchSize)`). No shown-id
  parameter, no dedup. "Give me more" appends a fresh draw (repeats acceptable). StoryDeck pagination
  suppressed in random mode (`TotalCount = Items.Count`). Sorted modes use offset pagination.
  Interaction exclusions are **not** random-specific — they're whatever the viewer's effective §8.7
  settings say, applied as an ordinary filter. The seeded SearchPage default excludes Ignored;
  because it is user-overridable, the "Ignored story disappears from results" effect is a filter
  consequence, not shown-id bookkeeping.

  *§8.7 defaults read/merge:* `IDiscoveryDefaultsReadService.GetDefaultExcludedInteractionsAsync(string searchModeKey)`
  — system-matrix rows overlaid with sparse per-user `UserStoryInteractionFilterSetting` overrides in
  C# (user value wins). Anonymous → defaults only. 7 catalog keys map to 6 enum values (`HasStarted`
  has no enum counterpart and is dropped from mapping — documented in the service). Seed unchanged
  (Ignored=true on the 5 discovery surfaces; profiles none). No migration.

  *Tag include-mode (AND/OR):* `TagIncludeMode { And, Or }` enum in `Core/Discovery/`. `StoryFilterDto`
  gains `IncludeMode = And` (default preserves all existing behavior). The AND/OR toggle is surfaced
  on the **include** selectors only on `/discover`; the exclude axis has no toggle. Default `And`
  keeps Bookshelves/Profile unchanged. Interaction state stays **exclude-only as a filter** — inclusion
  is a Bookshelves Source concern. This OR-include is a deliberate net-new extension: per the original
  deliberations §11, OR-across-tags was "never deliberated" and AND is the intended default; the toggle
  is gated to `/discover`.

  *Interaction-filter UX (settled, first-principles — not the incumbent component's rendering):*
  each kind is presented as **"Hide stories I've X"** with **checked ⇒ excluded** (`ExcludedInteractions`).
  Rationale: discovery's engine is subtractive over the full catalog; the gesture must map monotonically
  to the engine op (no inversion); "hide these" is honest while "show only these" implies a whitelist
  the engine doesn't have (the §8 trap); a mostly-empty default truthfully signals "maximally open";
  one subtractive polarity across the panel (matching the tag-exclude axis). The positive "show/
  whitelist" framing is the Library Model, served on Bookshelves by the **Source** axis.

  *Reconciliation with original deliberations (`Boolean_Logic_Search_Filter_Deliberations.md`):*
  - **Superseded — do not resurrect:** "Random Search" as a mode (→ `Sort=Random`); `Viewed`/
    `ReadStatus` family (→ `HasStarted`, WU0/A1 remodel); early "exclude all rows together" (→ per-type
    §8.7 matrix); 6 filter criteria (→ 7 catalog keys + `HiddenFavorited`/`Followed`); all T-SQL
    (→ Postgres / `EF.Functions.Random()`); §6 Search Templates (→ post-MVP; no entity — adjacent to
    deferred Custom Lists Feature 51).
  - **Corroborated:** §5 two-query C# merge IS Phase 1b. §8 Discovery-Model-exclude/Library-Model-
    include split IS the origin of "interaction exclude-only on discovery." §2 stateless-fresh-search
    IS the random-batch design. The deliberations **rejected a per-criterion include/exclude semantics
    toggle** — the tag AND/OR toggle is set-combination *within* a fixed include selector, not that
    rejected flip; they remain separate selectors. OR-include has §9 whitelist-union precedent.

  *Deferred:* per-user override editing UI (no settings surface in MVP — entity supports it);
  per-user random batch size (`User.ReaderSettings`) — MVP uses constant 20.

- **WU8 Stage note (2026-06-21):** the **pagination slice** of this feature's L3.5/L4 is built —
  `PaginationControls` (`SharedUI/Pagination/`), a leaf settled per spec §3.11.1/
  `layer3.5-structure.md` (the `audit-summary.md` "Composite" classification is stale, superseded).
  Contract: `CurrentPage`/`PageSize`/`TotalCount` (primitives only — no `StoryListingDto` or
  paged-result type dependency) + `EventCallback<int> OnPageChanged`; fully stateless per §5.3.4 —
  raises the requested page, never queries itself. Fixed 7-slot sliding window, no outer margin;
  detail in `layer4-style.md` Pattern Accumulation. **Not used in random/discovery mode** — "give me
  more" + interaction buttons remain the pagination mechanism there (unchanged from the L2 note
  above); this control targets Date Published / Relevance (sorted) modes, plus Comments. This is a
  *slice*, not the whole feature — the rest of Feature 31 (ResultsFilterPanel, StoryDeck, the actual
  search query) remains Stage 2/1 as recorded above; cell numbers in `status.md` are unchanged.
  Verified: `dotnet build` green (4 projects); user-confirmed visual check via a throwaway harness on
  `HomeDesktop.razor` (12-page sliding window, 3-page no-ellipsis/centered, single-page renders
  nothing, active highlight follows clicks) — harness removed after confirmation.
  **2026-06-22 (WU12.5 backfill):** verification migrated into asserted tests — `PaginationControlsTests`
  in `TheCanalaveLibrary.Tests.RazorComponents` (tier: **RazorComponents**). Covers: nothing rendered
  at TotalPages ≤ 1; ≤7-page window (all shown, no ellipsis); near-start window (one trailing
  ellipsis); middle window (two ellipses); active page `aria-current="page"` set correctly; inactive
  buttons carry no `aria-current`; active-page CSS token `text-white` vs. inactive; Prev/Next disabled
  on first/last page; range summary text (`11–20 of 47`); `OnPageChanged` callback fires with correct
  page. Mutation-sanity confirmed: inverting the `aria-current` condition (`!=` instead of `==`) →
  three tests fail (`aria-current` value wrong, inactive button `text-white`, `aria-current` on wrong
  button). CSS custom property rendering (`--color-primary` box fill) is NOT testable in bUnit —
  markup-level evidence is correct; the visual rendering still requires human sign-off for Stage 6.
  `dotnet test` green.

  **WU37 `ApplyFilters` type-branch (2026-06-25 — built and verified):**
  WU37 Phase 2 moved Character tags from `StoryTag` to `StoryCharacter`. The existing flat
  `s.StoryTags.Any(st => st.TagId == tid)` predicate would silently miss Character-type filters.
  Fixed: `StoryFilterDto` carries `IncludedTagIdsByType` / `ExcludedTagIdsByType`
  (`Dictionary<TagTypeEnum, IReadOnlyList<int>>`); `ApplyFilters` routes Character ids →
  `s.StoryCharacters.Any(sc => sc.CharacterTagId == id)`, all others → `s.StoryTags.Any(...)`.
  - **Integration** (`StoryTaggingTests` — `GetListingsAsync_IncludeByCharacterTagId_MatchesViaStoryCharacters`
    + `SanityCheck_CharacterFilter_CharacterIsNotInStoryTags`): confirms the character branch is live
    and that character tags are absent from `StoryTags`. 348 integration tests green (2026-06-25).
  See `layer2-services.md` "Structured Tag Authoring — Per-Type Filter Branch."

  **WU28 Stage note — F31 L2 (2026-06-25):**
  Built: `GetRandomBatchAsync(StoryFilterDto filter, int batchSize)` in `IStoryReadService` /
  `ServerStoryReadService`. The method is fed through a new `ApplyFilters` private helper that is
  also used by `GetListingsAsync`, replacing the inlined filter code (DRY; the existing nine
  `StoryListingsTests` integration tests prove the helper is behaviourally identical).
  `ApplyFilters` branches on `filter.IncludeMode` (Or → single `WHERE EXISTS IN (...)`; And → per-tag
  conjunctive loop, unchanged). `TagIncludeMode { And, Or }` enum added to `Core/Discovery/`.
  `StoryFilterDto` gains `IncludeMode = And` (default; preserves all prior calls). `TagFilterSelection`
  gains `IncludeMode` parameter. `TagFilter.razor` and `ResultsFilterPanel.razor` extended with
  AND/OR toggle support (gated by `AllowIncludeModeToggle` param; false by default — Bookshelves and
  Profile pages are unchanged).

  `IDiscoveryDefaultsReadService` / `ServerDiscoveryDefaultsReadService` new: two-query merge
  (system matrix + sparse per-user overrides in C#). `HasStarted` key dropped from the enum mapping
  (documented in service). Registered as `AddScoped` in `Program.cs`. `SiteSearchModes` and
  `UserStoryInteractionFilters` moved from `TheCanalaveLibrary.Server` (`SiteConstants.cs`) to
  `TheCanalaveLibrary.Core/Discovery/SiteSearchModes.cs` so SharedUI components and the interface
  can access them without a circular dependency.

  UI: `SearchPage.razor` (dispatcher, `@page "/discover"`, `[AllowAnonymous]`, nullable auth cascade,
  `OnInitializedAsync` seeds §8.7 defaults and loads random batch); `SearchDesktop.razor` /
  `SearchMobile.razor` (injection-free composites — random mode: "Give me more" + suppressed
  pagination; sorted mode: real pagination). Pre-existing `BookshelvesDesktopTests`,
  `BookshelvesMobileTests`, `CommentSectionTests`, `CommentSectionGroupTests` all had
  `IModerationWriteService` missing — fixed here by adding `FakeModerationWriteService` to each
  test context.

  **How verified (WU28, 2026-06-25):** `dotnet build` green (8 projects, 0 errors). `dotnet test`:
  - **Unit:** 429 passing (unchanged).
  - **Integration:** 329 passing (7 pre-existing `ModerationServiceTests` DI failures unrelated to
    WU28). WU28-specific new tests:
    - `DiscoveryDefaultsReadServiceTests` (5 tests): SearchPage default = {Ignore}; anonymous matches
      authenticated-no-overrides; per-user enable adds key; per-user disable removes key; `HasStarted`
      key silently dropped from output.
    - `RandomBatchTests` (7 tests): batch respects `batchSize` cap; tag filter scopes the valid set;
      interaction exclusion respected for authenticated viewer; content-rating filter still applies;
      OR-include returns stories matching any included tag; AND-include still requires all tags;
      mutation-sanity (OR and AND produce different results, confirming OR branch is reached).
  - **RazorComponents:** 428 passing (0 failures; the prior 37 pre-existing failures were all
    `IModerationWriteService` missing — fixed). WU28-specific new tests:
    - `SearchDesktopTests` (8 tests): random mode has Give-me-more/no-pagination; sorted mode has
      pagination/no-Give-me-more; `ResultsFilterPanel` present; deck renders supplied items;
      `OnLoadMore` fires; mutation-sanity (switching mode changes controls).
    - `SearchMobileTests` (9 tests): filter toggle button present; overlay not rendered when closed;
      clicking toggle opens overlay; overlay contains `ResultsFilterPanel`; random mode has
      Give-me-more; sorted mode has no Give-me-more; deck renders supplied items; `OnLoadMore` fires;
      mutation-sanity.
  - **L4 visual sign-off:** pending (consistent with WU8/WU13/WU23/WU27 precedent; cells stay
    Stage 1 in `status.md` until human check).

## Feature 32 — Full-Text Search
- **L1 — Stage 5.** `StoryListing.SearchVector` as a stored generated column from `to_tsvector('english',
  title || short_description)` — exactly the spec pattern (§5.3.2: FTS is a *filter*). Sound.
- **L6 — Stage 5.** GIN index `ix_story_listing_search_vector` written and correct (awaiting migration).
- **L2 — Stage 5** (WU28). **L3/L3.5 — Stage 5** (WU28). **L4 — Stage 1.**
  **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13)** — endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; the filtered `POST /query` path that carries the FTS filter
  and sort verified in a real WASM runtime during the flip's browser wave. Full wave narrative +
  the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.

  **WU28 Stage note — F32 L2/L3/L3.5 (2026-06-25):**
  FTS was already built in WU23 (`ServerStoryReadService.GetListingsAsync` — `EF.Functions.PlainToTsQuery`
  filter + `Rank()` relevance sort). WU28 completes F32 by consuming FTS through `/discover`:
  - The existing `GetListingsAsync` `Relevance` sort branch powers FTS on the sorted search page.
  - The `ApplyFilters` helper extracted in WU28 shares the same FTS predicate with `GetRandomBatchAsync`.
  - The new `SearchDesktop`/`SearchMobile` composites expose the `Relevance` sort option in
    `AvailableSorts` (gated behind `ResultsFilterPanel` which hides Relevance when `_textQuery` is empty).
  - Verified end-to-end through `RandomBatchTests` (tag filter via `ApplyFilters`) and the nine
    pre-existing `StoryListingsTests` (FTS + Relevance sort already covered there, now via the
    extracted helper). No new dedicated FTS integration tests were needed — existing coverage is
    sufficient.
  - L3 (SearchDesktop/SearchMobile composites) and L3.5 (same files + `SearchPage` dispatcher) are now
    built and verified. All tier assertions the same as Feature 31 WU28 note above.

## Feature 33 — Manual Tree Search
- **L1 — N/A** (stateless graph pivots over live tables, plus one new field —
  `User.PinnedStoryId`, migration `WU40_PinnedStory`). **L2 — Stage 5 (WU40, 2026-07-12 — see
  Stage note).** **L3/L3.5 — Stage 5 (WU40)** (distinct graph/node visualization — NOT
  `StoryDeck`). **L4 — Stage 1** (visual sign-off pending, WU8/WU13/WU23/WU28/WU44 precedent).
  **L4.5 — Stage 5 (WU40, behavioral browser verification — see Stage note).**
  **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13)** — endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto (Explore tab not browser-driven in the flip's wave; the
  sibling Automatic tab was, see F59). Full wave narrative + the 7 bugs found/fixed: `workplan.md`
  WU-GlobalFlip. **L6 — Stage 2** (no
  index work done — the pivots ride existing recommendation/USI/story indexes; whether a
  dedicated index is justified awaits an R4-rule measurement pass).

  **Settled for WU40 (2026-07-12 — supersedes the WU28/WU-Marts-era notes below in their entirety;
  do not revisit unless the Phase 1 mock changes the interaction design):**

  - **Three paradigms, not one graph view:** Automatic (Feature 59, built) | **Explore** |
    **Deep Dive** — three top-level tabs on the Unified Tree Search Page. Diverges from spec
    §5.26's literal "two tabs" (spec is read-only; this is the record of the deliberate
    divergence).
  - **Privacy model corrected.** §5.4's "graph never reveals identity" protects exactly one
    thing: anonymized *hidden*-favorite contributors, whose reach is merged into the mart's
    indistinguishable `Favorite` edge for Automatic. It does NOT mean manual tree search hides
    identity. Manual excludes hidden favorites entirely (unchanged), so **every edge available in
    manual is a genuinely public action** — authors, recommenders, public favoriters, hidden-gem/
    spotlight participants, and vouchers all render as named, clickable nodes.
  - **The `allow_discovery_consent` gate on Hidden Gem never existed in code** — verified against
    `DiscoveryMartSchema.TreeSearchBuild` (Server/Discovery/): the only gate on the HiddenGem edge
    is `EligibleRecommendation` (author-approved, not taken down, not anonymized). A Hidden Gem is
    an approved recommendation the story's author already consented to by approving it — there is
    no second consent field anywhere in `User`/`Story`. The prior note claiming this gate existed
    was stale draft language that never had an implementation; deleted, not superseded.
  - **Corollary: the Automatic tab's chain-of-trust path chip (`TreeSearchResultBadge`, WU44) was
    over-anonymized** and is corrected as part of WU40 — see the WU44 Stage note below and the
    corrected line in `layer3.5-structure.md`.
  - **Edge × direction boundedness** (governs what's usable in Deep Dive vs. Explore-only — most
    edges are asymmetric: bounded one way, unbounded the other):

    | Edge | Bounded direction | Unbounded direction |
    |---|---|---|
    | AuthoredBy | story → author (1, identity) | author → their stories (unbounded) |
    | Favorite | — | both directions unbounded |
    | Recommendation | — | both directions unbounded |
    | Vouch | user → ≤5 vouchees | vouchee → their stories (unbounded — the *projected* user→story edge is unbounded even though vouchee selection itself is capped) |
    | HiddenGem | user → their own ≤5 gems | story → everyone who ever gemmed it (unbounded — no cap on how many different readers pick the same story) |
    | AuthorSpotlight | story → its own ≤5 spotlighted recommenders | user → every story that ever spotlighted them (unbounded — no cap across authors) |
    | **Pinned** (new) | user → their 1 pinned story | n/a (exactly one owner) |

  - **New edge: Pinned Story.** Exactly one self-chosen story per user
    (`User.PinnedStoryId`, nullable FK → `Story`, `ON DELETE SET NULL`). Fills the structural gap
    symmetric to how `AuthoredBy` lets the Hidden Gem chain self-sustain (story→author is a free
    1:1 connector) — Author Spotlight had no equivalent connector back from a spotlighted
    recommender to a story. Capped at 1, not 5: preserves the alternating "curated-≤5 /
    identity-1" chain shape and keeps the self-promotion surface minimal — entry is earned via
    someone else's Spotlight endorsement, never surfaced as a count, cannot become a second
    popularity list. Reinforces rather than conflicts with the site's anti-attention-hoarding
    philosophy (§5.5). Displayed as a badge within the Authored section (sorted first when
    present), not a separate section. Author-facing edit surface: extends
    `AuthorSettingsForm.razor`/`AuthorSettingsDto` (same shape as the existing Default Story
    Rating field). **Sequencing:** manual (live tables) ships in WU40; the mart/Automatic-tab
    integration (a 7th UNION arm in the frozen `DiscoveryMartSchema`, reopening Feature 59/60) is
    explicitly deferred — see the forward-pointer in `workplan.md`.
  - **Edge modeling stays independent of the frozen mart enum.** `TreeSearchEdgeType` is the
    mart's raw-SQL `smallint` column contract ("never renumber" — Feature 59/60). Manual tree
    search introduces its own `Core/Discovery/ManualTreeEdgeType` (the same six edges + Pinned),
    used only for UI tagging and pivot-request shaping. It never touches the mart.
  - **Stateless pivot over live tables** (reaffirmed, unchanged in substance from the prior
    direction): each node selection in either manual paradigm is a fresh, stateless query — no
    traversal state on the server. Manual is degree-1 interactive and must be fresh (the mart is a
    daily rebuild and IDs-only — it cannot carry edge *detail*, e.g. recommendation blurbs).
    Client-side accumulation (the curated tree, below) is a separate, compatible concern.

  **Shared tree visualization (settled through the Phase-1 mock, four iterations, 2026-07-12):**
  a **2D top-down node-link diagram** — root at top, children fan out in a row beneath, straight
  SVG lines colored by edge type — NOT a nested/indented DOM-outline list. Tidy-tree layout:
  leaves get sequential horizontal slots, each parent centers over its own children, depth = row;
  validated at the taxonomy's real ≤5 fan-out. Nodes are compact ~56px squares (circles for
  users) holding the thumbnail, caption below. One shared node-chip leaf + layout renderer serves
  both modes; the pane *arrangements* are separate composites (embedded ~50/50 pane vs.
  full-screen pannable canvas — structurally different, per "separate composites, shared leaf").

  **Edge selection is per (edge, direction) pair — in BOTH modes, every pair independently
  toggleable.** "Recommendation from a story" (who recommended it) and "Recommendation from a
  user" (what they recommended) are different traversal semantics and never share one flag.
  This includes Author(story→author) and Pinned(user→story): an earlier draft made Author
  "always-on identity, never toggled," which composed with Pinned into an unavoidable identity
  round-trip (a pinned story is self-authored: user → their pinned story → author → same user).
  Toggles on every pair are the fix. Explore's toggle row swaps wholesale per anchor direction
  (a story anchor shows only story→user controls, etc.); Deep Dive shows all four whitelisted
  pairs at once with direction-annotated labels (the next click can land on either node type).

  **Explore mode** (the primary "build your own map" paradigm):
  - Two-pane ~50/50 layout: left = the persistent, client-curated tree canvas (root seeded from
    the route, same resolution `TreeSearchPage` already does; drag-to-pan); right = disposable,
    stateless "candidate results" pane for the selected node's neighbors under the active
    (edge, direction) toggles, with the anchor pinned above the results as a reminder of context.
  - Newly-added-but-unexplored left-tree nodes render in a **ghost state** (dashed) until
    selected — a visible frontier of "where I could go next"; selecting solidifies.
  - Persistence: browser `localStorage`, one JSON document per (mode, root entity) — not a DB
    entity in this WU. **Shape: IDs + edges only** ({entityId, entityType, edgeLabel, ghost,
    children}) — never display snapshots. On load, one batch rehydration (existing
    `GetListingsByIdsAsync` + user-header reads) restores titles/covers; entities that are gone,
    taken down, or rating-gated for the viewer prune silently (small "N removed" note). A
    durable, cross-device "saved trees" feature was considered (the Gemini deliberations' "come
    back to results later") and deferred; the *search* stays stateless per the settled direction.
  - **Section model** replaces any flat deck or priority ranking — one section per underlying
    table, because most "competing" edges turn out to be flags on the same rows, not independent
    signals to rank. **Every section is toggle-gated (none hardcoded on), and unbounded sections
    are paged: first page ≈10 + per-section totalCount + "Show more" paging that section only.**
    - **Author** (story anchor) — identity, singular.
    - **Recommendation family** (either anchor) — one query against `recommendations`; the
      Recommendation / Hidden Gem / Author Spotlight toggles only widen or narrow its `WHERE`
      clause (`is_hidden_gem`, `is_highlighted_by_author`); a row matching multiple flags shows
      once with badges stacked, never once per flag. **Rendered as compound rows:** the
      recommended story's `StoryCard` and the recommendation panel side by side as ONE row,
      compounds stacked vertically — not a grid of separate cards. The rec half omits
      `RecommendationCard`'s embedded story reference (the real StoryCard sits beside it) — an
      additive `ShowStoryReference` param (default true) on `RecommendationCard`, not verbatim
      reuse. This section is also where the two *unbounded* reverse readings surface as
      Explore-only informational signals: "who gemmed this story" (story anchor) and "which
      stories spotlighted this recommender" (user anchor).
    - **Favorite** — always its own section (separate table; the same target can legitimately
      also appear in the Recommendation family — two independent signals, not a duplicate).
    - **Authored** (user anchor) — the user's full catalog; their Pinned Story, if set, is badged
      and always sorted first within this same list — not a separate section.
    - **Vouch** (user anchor, forward direction ONLY) — its own section, a genuinely distinct
      mechanism (voucher → vouchee → vouchee's stories); never usable from a story anchor
      (incoming vouches are owner-private per §5.8 — traversing from a story to "who vouches for
      this author" would leak that private view).
  - **Card reuse, not bespoke cards.** Story results render the real `StoryCard` (triage —
    favorite/RIL/ignore — stays primary, no immediate-hide since the pane is stateless/
    disposable). User results render the real `UserCard`. "Add to tree" is a new composed
    **sibling** action (context-specific augmentation per `layer3.5-structure.md`) —
    co-important alongside `StoryCard`'s triage row (not in the caret menu), primary action for
    `UserCard` results (which have no triage equivalent).
  - Mobile: Tree ⇄ Results toggle (they can't sit side-by-side on small viewports).

  **Deep Dive mode** (bounded chain-of-trust deep-discovery paradigm):
  - Manual, one click at a time — NOT a recursive/auto-expanding server traversal. **Clicking a
    node is the ONLY gesture: it selects the node (info display) AND auto-adds its whitelisted
    bounded connections in the same action.** There is no separate deliberate "add" step (that
    deliberateness is Explore's defining trait; Deep Dive's is momentum — every edge is ≤5 or 1,
    so bulk-adding is always safe). No "Explore more" button, no blocking modal.
  - The info surface is a **floating, resizable panel starting top-right over the canvas** —
    non-blocking; the canvas stays pannable/clickable beneath it. It shows the selected node's
    composed card (same StoryCard/UserCard + rec panel composition as Explore's results) plus
    which toggle-gated groups were skipped.
  - **Anti-bounce guard:** auto-add skips a child whose entity equals the clicked node's
    *parent's* entity — kills the default A→B→A ring (e.g. pinned story → author → same user)
    independent of the toggles. Deeper cycles and cross-branch duplicates remain allowed (dedup
    stays per-node, per the path-reflecting design).
  - **Edge whitelist — exactly the four (edge, direction) pairs bounded to ≤1 or ≤5** (from the
    table above): `AuthoredBy` (story→author, 1) · `HiddenGem` (user→their own ≤5 gems) ·
    `AuthorSpotlight` (story→its own ≤5 spotlighted recommenders) · `Pinned` (user→their 1 pinned
    story). Every other (edge, direction) pair — including the two unbounded reverse readings
    used as Explore-only signals, and Vouch (bounded voucher selection, unbounded resulting
    stories) — is excluded. This whitelist does not reopen or change the "degree-1 interactive"
    settled direction; nothing here is auto-expanding or multi-degree on the server.
  - Full-screen pannable/zoomable canvas (desktop: drag + zoom controls; mobile: native panning).
  - Separate `localStorage` tree from Explore (different mode, different edge scope).

  **Blazor-gap arbitrations (2026-07-12, settled — do not revisit):**
  - **Thin JS module + C# layout.** One small `manual-tree-search.js` owns per-frame gestures
    (CSS-transform pan/zoom, floating-panel drag/resize) and localStorage. The tidy-tree layout,
    node rendering, SVG lines, and section rendering are C#/Razor over the circuit — the layout
    is deterministic math (Unit-tier testable) recomputed only on structural change; per-frame
    gestures never touch SignalR.
  - **One pivot = one service call.** All sections of a pivot return in one
    `ManualTreeNeighborsDto`. Direction is enforced by two request types
    (`StoryNeighborsRequest` / `UserNeighborsRequest`) — the type system, not flag naming,
    disambiguates direction at the service boundary.

  **Service-layer gap analysis** (confirmed against the actual code, not assumed — 2026-07-12):

  | Section / need | Status |
  |---|---|
  | Author (story anchor) | Built — `StoryListingDto.AuthorId`/`AuthorName` |
  | Recommendation family, story anchor | Built — `IRecommendationReadService.GetForStoryAsync(storyId)` |
  | Recommendation family, user anchor | **Missing** — needs `GetByRecommenderAsync(userId)` mirroring `GetForStoryAsync`'s shape (existing `GetRecommendedStoryIdsByUserAsync` is IDs-only, can't drive `RecommendationCard`) |
  | Favorite, story anchor (who favorited it) | **Missing** — only the reverse direction exists today |
  | Favorite, user anchor | Built — `GetFavoriteStoryIdsAsync(userId, includePrivate:false)` |
  | Authored, user anchor | Built — `GetStoryIdsByAuthorAsync(userId)` |
  | Pinned | **New** — field + read exposure + write via `AuthorSettingsDto` |
  | Vouch, user anchor forward | Partial — `GetOutgoingVouchesAsync(userId)` exists; needs composing with authored-stories-per-vouchee (no new primitive, just a join) |
  | Hydration | Built — `GetListingsByIdsAsync`, `GetProfileHeaderAsync`, `GetStatesByStoryIdsAsync` |

  Net new: two read-service methods, one new persisted field, one composition — everything else,
  including the entire "recommendation family with stacked badges" mechanism, already exists.

  - Corroborated by the original deliberations: §2 stateless-fresh-search, §3 hidden-gem chain-of-
    trust, §12 "traversal cost dominated by rCTE, not by excluding a few hundred IDs."

  **WU40 Stage note — L2 / L3-Logic / L3.5 / L4.5 (2026-07-12):**

  Built exactly the settled design above. **L2:** `Core/Discovery/` `ManualTreeEdge` (the ten
  (edge, direction) pairs — UI/tree-state enum, never touches the frozen mart numbering),
  `StoryNeighborsRequest`/`UserNeighborsRequest` (direction enforced by type),
  `ManualTreeSectionDto<T>`/`ManualTreeRecItemDto`/`ManualTreeNeighborsDto`/
  `ManualTreeNodeDisplayDto(s)`, `IManualTreeSearchReadService`;
  `Server/Discovery/ServerManualTreeSearchReadService` — one call per pivot, all sections; the
  recommendation family is ONE flag-composed query (rows constrained to viewer-visible stories
  via a captured filtered-DbSet subquery — global filters don't apply to navigations); per-section
  count+page in SQL; authored orders pinned-first; vouch traverses forward only; instrumented
  `Discovery.ManualTreePivot` spans. Also: `User.PinnedStoryId` (+`AuthorSettingsDto`/
  `AuthorSettingsForm` picker + write gate: own/published/visible only) and the WU44 path-hop
  hydration (`TreeSearchPathHopDto`, `AttachPathHopsAsync` composing `GetNodeDisplaysAsync` —
  rating-gated bridge hops stay label-less/opaque).

  **L3/L3.5 (`SharedUI/Discovery/` + `wwwroot/js/manual-tree-search.js`):** `ManualTreeNode`
  (client tree model; IDs+edges-only JSON; prune-on-rehydrate) + `ManualTreeLayout` (tidy-tree
  math, C#, Unit-tier) + `ManualTreeCanvas` (shared 2D top-down diagram: SVG edge lines colored
  per edge token, square/circular chips, ghost state) + `ManualTreeEdgeToggles` (shared pill
  row) + `ExploreTab` (two-pane; direction-swapped toggles; sectioned results with compound
  rec rows + per-section Show-more; Add-to-tree sibling action; mobile Tree⇄Results toggle) +
  `DeepDiveTab` (full-viewport pannable canvas; click = select + auto-add; anti-bounce guard;
  floating resizable non-blocking panel showing the composed card + the rec that earned the
  edge) + `ManualTreeStore` (typed JS wrapper: gestures + localStorage). `TreeSearchTab` →
  three values; strip/Desktop/Mobile route the tabs. JS module loaded in `App.razor`.

  **How verified (2026-07-12):** `dotnet build` green. `dotnet test` green — **Unit 647**
  (`ManualTreeLayoutTests`: slot/depth at the real ≤5 fan-out, non-overlapping subtrees, chain
  shape, canvas sizing; `ManualTreeNodeTests`: JSON round-trip, no-display-data-in-storage
  contract, corrupt-payload nulls — this test caught a real NPE on legacy payloads with a
  missing children property, fixed same-session; prune semantics; per-node-not-cross-branch
  dedup). **Integration 574** (`ManualTreeSearchTests`, Testcontainers Postgres: author +
  public-only favoriters; family flag widen/narrow with badges stacked on one row; toggles-off
  suppress sections incl. Author — the bounce fix; pinned-first authored + `PinnedStoryId`;
  public-only favorites + vouch forward projection; honest disjoint paging; rating-gated
  rehydration displays; Pinned write gate accepts own/rejects foreign/null unpins; `SearchAsync`
  path hops carry usernames+titles). **RazorComponents 608** (`ManualTreeCanvasTests`,
  `ManualTreeEdgeTogglesTests`, `ExploreTabTests` — direction-swapped toggle rows, compound row
  with stacked badges, honest Show-more, ghost add + dedup, IncludeAuthor=false round-trip,
  pinned badge; `DeepDiveTabTests` — auto-add on open, four direction-labeled toggles,
  toggle-gated future walks, anti-bounce A→B→A guard, non-blocking panel with no backdrop,
  gem-node panel shows its earning rec; updated `TreeSearchTabStrip/Desktop/Mobile` +
  `TreeSearchResultBadge` PathHops tests). Suite total: 1,829.

  **L4.5-Browser (real circuit, `run-server/SKILL.md`, SeedTool-volume dev DB):** fixture:
  pinned story 265 for its author (user 310, 15 stories, 3 gems, 5 recs written). Verified live
  at `/discover/story/265`: three-tab strip; **Explore** — root chip, Author section + Add (ghost
  node with dashed connector + "✓ In tree" dedup), ghost click pivots to the user anchor
  (toggle row swaps to user direction), compound rec rows (5), Favorites paged "Show more (37
  more)" of 47, **Authored (15) sorts the pinned story first with the 📌 PINNED badge**, vouch
  empty state; **Deep Dive** — four direction-labeled pills, root auto-open auto-added the
  author, clicking the author auto-added exactly the 3 Hidden Gem stories and **skipped the
  pinned story (= the parent): the anti-bounce guard confirmed on real data**; walking a gem
  auto-added *its* author (the chain continues); floating panel updates per selection ("Root" /
  "Reached via Author" / "Reached via Hidden Gem" + the earning recommendation), non-blocking;
  **localStorage persistence** — separate `canalave.tree.{explore|deepdive}.story.265` keys,
  full-reload rehydration restored 2/6 nodes respectively via `GetNodeDisplaysAsync`. No console
  errors. `check-design-tokens.ps1`: WU40 surfaces clean (fixed `shadow-sm`→`shadow-medium`,
  `z-10`→`z-(--z-dropdown)` during the pass); the one remaining finding (`ImportReviewPanel`,
  UGC-outside-ContentSurface) pre-exists this WU (untouched Import-cluster file). Surface
  registry: WU40 element-kind rows appended. **Deferred:** Pinned-Story mart/Automatic-tab
  integration (forward pointer in `workplan.md`); tag/interaction filter axes on the candidate
  pane (recorded as not-built in `layer3.5-structure.md`); L6 index measurement.

## Feature 34 — Tag Directory (`/tags`)
- **L1 — N/A.** **L2 — Stage 2** (browse query). **L3/L3.5 — Stage 2** (`TagDirectoryPage`: user browse +
  mod CRUD behind `AuthorizeView`; mobile browse, desktop-only edit). Depends on the `TagChip` atom owned
  by Tags/. **L4 — Stage 1. L5 — Stage 5 (WU-L5Pilot, see Stage note below).**

  **Settled for WU27.5 (2026-06-24, do not revisit):**
  - **Browse layout:** sections per type (enum order), parent→child nesting everywhere (TOC-style,
    mirroring ChapterNavigation alternate-versions disclosure). Bounded types (Setting, Genre,
    ContentWarning) render expanded; unbounded types (Character, Relationship, CrossoverFandom)
    additionally get collapsibility + type jump-nav. `TagTypeLayout` static helper classifies which.
  - **Desktop-only mod controls:** inside `<AuthorizeView Roles="Moderator,Admin">`, hover ✎/✕ per
    chip and "+ New Tag". Edit/new open a WU9-shell modal with `TagEditorForm`; delete opens
    `ConfirmDialog`. `TagDirectoryDesktop` emits `EventCallback`s up to the page.
  - **Mobile:** browse-only, no edit controls, unbounded sections collapsed by default.
  - **Dispatcher pattern:** `TagDirectoryPage` (public, no `[Authorize]`) injects `ITagReadService` +
    `IDeviceDetectionService`, owns write calls, branches mobile/desktop.

  **WU27.5 Stage note — L2/L3/L3.5 (2026-06-25):**

  Built: `Core/Tags/ITagReadService.GetTagDirectoryAsync()` (new method — returns
  `List<TagDirectoryGroupDto>` with all tags, parent→child nesting, per-group alphabetical order);
  `Core/Tags/TagDirectoryGroupDto.cs`, `Core/Tags/TagDirectoryNodeDto.cs` (one-level tree nodes).
  `Server/Tags/ServerTagReadService.GetTagDirectoryAsync()`: single EF projection over `readDb.Tags`
  (including `IsFanon`/`AllowOCDetails`/`ParentTagId`), materialize, resolve sprites post-materialization,
  build tree in memory; groups emit in `Enum.GetValues<TagTypeEnum>()` order.
  UI: `SharedUI/Tags/TagDirectoryPage.razor` (dispatcher, `@page "/tags"`, public); `SharedUI/Tags/TagDirectoryDesktop.razor`
  (browse sections + `<AuthorizeView Roles="Moderator,Admin">` "+ New Tag" button; emits create/update/delete
  `EventCallback`s to page; hosts editor modal + `ConfirmDialog`; catches `TagValidationException` inline);
  `SharedUI/Tags/TagDirectoryMobile.razor` (browse only, no create modal); `SharedUI/Tags/TagDirectorySection.razor`
  (shared parent→child section rendering; `<AuthorizeView>` per-chip ✎/✕ buttons visible to Moderators/Admins).

  **Note on mobile mod controls:** `TagDirectorySection` (shared by desktop + mobile) has per-chip
  `<AuthorizeView>` edit/delete buttons — these are visible to authenticated Moderators/Admins on mobile too.
  What mobile suppresses is the "+ New Tag" button (desktop-only) and the modal-wiring (no `OnEditTag`/
  `OnDeleteTag` callbacks are provided by `TagDirectoryMobile`). This distinction is captured in the mobile
  test (`Mobile_DoesNotRenderNewTagButton`).

  **How verified (2026-06-25):** `dotnet build` green (8 projects, 3 pre-existing warnings, 0 errors).
  - **Integration** (extended `TagReadServiceTests.cs`, Testcontainers Postgres, 5 new tests):
    all 6 `TagTypeEnum` groups present; parent→child nesting (child not top-level); alphabetical ordering
    (relative assertions — shared-state safe); `SpriteUrl` null when `SpriteIdentifier` null;
    admin fields (`IsFanon`/`AllowOCDetails`) accurately populated.
  - **RazorComponents** (`TagDirectoryTests.cs`, 15 tests): section headings render; parent + nested child
    chips present; unbounded type (Character) renders in `<details>`; bounded type (Genre) renders in
    `<section>` not `<details>`; anonymous user sees no "New Tag" button or edit/delete buttons; Moderator
    auth shows "New Tag" button; Admin auth shows "New Tag" button; mobile renders both chips; mobile
    unbounded in `<details>`; mobile suppresses "New Tag" button.

  **WU-L5Pilot Stage note — L5 (2026-07-04):**

  `/tags` is the project's first WASM page — deliberately chosen as the `layer5-wasm.md`
  battle-test pilot (public read + mod-gated write + sprite theming in one small surface).
  `TagDirectoryPage` now carries `[ExcludeFromInteractiveRouting]` + `@rendermode
  RenderMode.InteractiveWebAssembly` (the island pattern — both directives load-bearing, see
  `layer5-wasm.md` §"Render-Mode Topology"), `[PersistentState] Directory` (prerendered data
  hydrates into WASM with zero refetch — the "Loading…" branch keys off `Directory is null`,
  replacing the old `_isLoading` field), and wraps its content in `ThemeContextProvider` (now in
  `SharedUI/Sprites/`) because Routes-level cascades don't cross the island boundary. Endpoints +
  client services: see `audit/Tags.md` F11/F13 WU-L5Pilot notes.

  **How verified (2026-07-04):** browser band (real WASM runtime): anonymous browse renders the
  full directory from persisted state (no `/api/` call on load — verified via network log), WASM
  `AuthorizeView` correctly hides mod controls anonymously and shows them for AdminUser
  (serialized role claims), full mod CRUD round trip with psql ground-truth checks, and
  cross-navigation both directions (island → home via enhanced nav; home → island via the
  interactive router's full-page reload — without `[ExcludeFromInteractiveRouting]` that click
  crashed the InteractiveServer circuit, the pilot's biggest catch). Automated: Integration
  `TagEndpointsTests` + Unit `ClientTagServiceTests` (see `audit/Tags.md`); bUnit tier unchanged
  (`TagDirectoryTests` render the Desktop/Mobile composites, which are untouched).

  **2026-07-04 (same day, post-verification):** island directives + page-level
  `ThemeContextProvider` wrap removed from `TagDirectoryPage` per the settled single-flip
  rollout strategy (`middle_plan.md` Resolved "L5 rollout strategy") — `/tags` rides global
  `InteractiveServer` until the site-wide `InteractiveAuto` flip; `[PersistentState]` kept
  (prevents the prerender double fetch under the circuit too). L5 remains Stage 5: endpoints,
  client impls, and tests are live and green, and the WASM-runtime verification above is the
  standing record. Re-islanding recipe, if a flip-wave bisect needs it: `layer5-wasm.md`
  §"The Island Recipe".

## Feature 59 — Automatic Tree Search (formerly below the line — line crossed 2026-07-07)
- **L1 — N/A** (Phase A removed the EF model; `user_story_tree_search_entries` is a raw-SQL mart — divergence
  resolved). **L2 — Stage 5 (WU-Marts, 2026-07-07 — see Stage note).** **L3/L3.5 — Stage 2** (unified with
  manual tree search; degree controls + edge-type selector — UI deferred past WU-Marts). **L4 — Stage 1.**
  **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13)** — endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; the Automatic tree-search tab verified in a real WASM
  runtime during the flip's browser wave, incl. the stale-root dispatcher fix (workplan bug 6).
  Full wave narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.
  **L6 — N/A** (mart indexes are raw-SQL, see implementation notes below). **L8 — Stage 5**
  (live rCTE over the edge-list mart — built with L2, same Stage note).

  **WU-Marts Stage note — L2/L8 (2026-07-07):**
  Built: `Core/Discovery/` `TreeSearchEdgeType` (smallint enum 0–5), `TreeSearchRequest`/`TreeSearchHitDto`/
  `TreeSearchResultDto`/`TreeSearchSortOrder`, `ITreeSearchReadService`;
  `Server/Discovery/ServerTreeSearchReadService` — one static-SQL recursive CTE over the narrow edge-list
  mart (PG14 `SEARCH`-free/`CYCLE`-based: `CYCLE is_story, node_id SET is_cycle USING path` gives pruning +
  native path materialization; LATERAL union with per-arm `LIMIT` = the per-node fan-out cap, each direction
  riding its covering index; edge selection is `edge_type = ANY(@edges)` — the AD3 payoff, no dynamic SQL).
  Min degree per story via `GROUP BY`; two sort orders (random / by-degree) over one result set; path
  returned only for chain-of-trust edge sets; rating + viewer §8.7 exclusions (AutoTreeSearch mode) at the
  presentation join (raw ADO — EF query filters don't apply, both applied manually and tested);
  `CanalaveTelemetry.Discovery` span + duration/degrees/results histograms + cap-truncation counter.
  **How verified:** **Integration** (`DiscoveryMartTests`, Testcontainers Postgres): wide degree-2
  co-favorite discovery (root excluded, viewer-Ignored excluded), deep hidden-gem chain reached at degrees
  2/4/6 with shortest paths + depth-4 cutoff, mature-story-as-silent-bridge (SFW viewer routes through but
  never sees it; mature viewer sees it), vouch projection from a root user. **Unit**
  (`TreeSearchRequestValidationTests`, 14 asserts with `MartsTelemetryTests`): request-shape rules incl.
  the paths-only-on-{HiddenGem, AuthorSpotlight} rule. **Headless live check** (2026-07-07, dev DB +
  SeedTool data via `/dev/discovery/tree-search`): deep chain from a niche story surfaced stories at
  degrees 2/4/6 with legible paths; wide mode from a hub story returned degree-2 hits with
  `resultCapTruncated: true` (the flooding indicator firing as designed). Suite: 1398/1398.

  **Settled (2026-07-07, WU-Marts Phase 0 — do not revisit):**
  - **rCTE affirmed** after first-principles re-audit against the original deliberations: live
    recursive traversal over the precomputed *edge list* (never precomputed traversal *results* —
    combinatorial across edge-selection × depth, destroys paths/freshness). Modernized: PG14+
    `SEARCH BREADTH FIRST`/`CYCLE`, lean recursive term (IDs + degree only), fan-out `LIMIT` in the
    recursive step. Apache AGE and pgvector/embeddings recorded as deferred future options only.
  - **Every edge worth 1 — no edge-type weights, no path score.** *Diverges from spec §5.4* ("applies
    scoring weights to edge types, returns ranked results") — spec is read-only; this note is the
    record. Instead: each story carries min degree-to-reach; **two sort orders over one result set**
    (random shuffle / by-degree ascending), user-toggleable.
  - **Path materialization** is a required service capability (searcher opts in per request),
    available only when selected edges ⊆ {HiddenGem, AuthorSpotlight} (truly-capped chain-of-trust);
    unbounded edges (Favorite, AuthoredBy, Recommendation, Vouch) and private favorites never
    return paths (combinatorial noise; privacy).
  - **Rating + active-user exclusion filter at the presentation join** (`story_listings` + live
    `user_story_interactions`), not in traversal — mature stories may be silent bridge nodes
    (never shown); strict no-mature-routing is a deferred toggle.
  - Full conventions: `layer8-data-marts.md` §"The Automatic Tree Search consumer".

  **Settled (2026-07-11, WU44 Phase 0 — do not revisit):** UI scope + the spec §5.26 filter-
  composition gap. Spec §5.26 places Automatic Tree Search as one tab on a shared **Unified Tree
  Search Page** (`/discover/me`, `/discover/user/{userId}`, `/discover/story/{storyId}`) — the
  other tab is Manual Tree Search (Feature 33, WU40, not yet built; its graph component is
  designed from scratch there). **WU44 ships the page shell (`TreeSearchPage` dispatcher,
  root-entity header, two-tab strip) + the working Automatic tab now**; the Manual tab is a
  "Graph view coming soon" placeholder that WU40 fills into the existing shell — this unblocks the
  three routes without waiting on the from-scratch graph component. Results reuse `StoryDeck` + a
  degree badge, **not** a bespoke tree-results list (`StoryDeck` already documents itself as "NOT
  manual tree search" — the automatic tab is a flat, deck-shaped list, so it is exactly StoryDeck's
  shape).

  Spec §5.26 also says tags/FTS/interaction filters "compose with the data mart query," but the
  Stage-5 `TreeSearchRequest` (above) accepts only root/degrees/edge-types/sort/cap — the rCTE
  applies rating + the viewer's §8.7 `AutoTreeSearch` interaction exclusions internally, with no
  tag/FTS parameter. First-principles resolution: **reachability parameters** (edge types, degrees)
  are intrinsic to the walk and stay in `TreeSearchRequest`; **relevance filters** (rating,
  interaction, tags, FTS) are properties of the *destination story*, not the edges, and pruning the
  walk on them would sever silent-bridge connections (the same reason a mature story is allowed to
  be an unshown bridge node today) — so they must apply *after* traversal, never inside the
  recursive term. This is the project's **Source × Filter × Sort** three-axis model applied to tree
  search: Source = the rCTE (with edge-types + degrees as its own parameters), Filter = rating +
  interaction + tags + FTS, Sort = Random / ByDegree.

  The EF `ApplyFilters` predicate (`ServerStoryReadService.cs`) and the rCTE are different
  execution engines (LINQ vs. static ADO SQL) — the predicate cannot be shared verbatim into the
  SQL, so composition (not duplication) is the only DRY option, and the cap must apply to the
  *filtered* set (not the raw traversal) or `ResultCapTruncated` becomes misleading. Settled shape,
  additive to the Stage-5 service:
  - **`ITreeSearchReadService.SearchAsync(TreeSearchRequest, StoryFilterDto, ct)`** — new method,
    injects `IStoryReadService`; server-side, integration-testable. `TraverseAsync` is unchanged
    and remains the low-level primitive (still used by the `/dev/discovery/tree-search` probe).
  - A defaulted **raw-reached traversal path** inside the service: the rCTE runs with no
    rating/interaction filter and no cap (bounded by the existing per-node fan-out `LIMIT`),
    returning `(story_id, degree, path)` minus the root.
  - **`IStoryReadService.FilterCandidateIdsAsync(candidateIds, filter)`** — new thin method reusing
    the existing `ApplyFilters` predicate verbatim; owns rating (global query filter), interaction
    (seeded from §8.7 `AutoTreeSearch` defaults, user-editable via the panel exactly like
    `/discover`), tags, and FTS — **and** the cap. `SearchAsync` joins survivors against the degree
    map, applies Random or ByDegree (`GetListingsAsync`'s `DefaultSortOrder` cannot express
    ByDegree, so that bundle alone was rejected), computes `ResultCapTruncated` on the filtered
    set, then hydrates via the existing `GetListingsByIdsAsync`.
  - Rejected alternatives (kept here, not repeated at length): hand-writing tag/FTS SQL into the
    rCTE presentation join (reopens the frozen SQL, duplicates the filter in two engines); reusing
    the whole `GetListingsAsync(filter, restrictToStoryIds)` bundle unmodified (cannot express
    ByDegree); post-hydration C# filtering over the already-capped `TraverseAsync` output (wrong
    cap cardinality — filtering after a cap can show far fewer results than actually exist).

  **WU44 Stage note — L2 (additive) / L3-Logic / L3.5 / L4.5-Browser (2026-07-11):**

  Built exactly the settled shape above. **L2 (additive, cell stays Stage 5):**
  `ServerTreeSearchReadService.SearchAsync` + private `GetRawReachedAsync` (raw-reached traversal,
  duplicated recursive-term SQL rather than refactoring `TraverseAsync` — deliberately, to leave
  the frozen/tested query untouched; kept in sync manually per the method's doc comment);
  `ServerStoryReadService.FilterCandidateIdsAsync`. `Core/Discovery/TreeSearchListingResultDto.cs`
  (`TreeSearchListingItemDto` + result wrapper), `Core/Discovery/TreeSearchPathParser.cs`
  (dependency-free parser for the Postgres `CYCLE ... USING path` composite-array text —
  `StoryIdsOnly` drops user-typed hops entirely, never surfacing an id for them), `Core/Discovery/
  TreeSearchControlsSelection.cs` (axis-emit contract, mirrors `TagFilterSelection`), `Core/
  Discovery/TreeSearchTab.cs` (Automatic/Manual, mirrors `BookshelfTab`/`ProfileTab`).

  **L3/L3.5 (`SharedUI/Discovery/`):** `TreeSearchPage` (dispatcher; routes `/discover/me`,
  `/discover/user/{userId}`, `/discover/story/{storyId}`; resolves the root via
  `GetListingsByIdsAsync`/`GetProfileHeaderAsync`; seeds §8.7 `AutoTreeSearch` defaults; seeds
  degrees/edges/sort from `[SupplyParameterFromQuery]` when present; pushes `NavigateTo(...,
  replace: true)` on Apply per spec §5.26 URL-state rules) → `TreeSearchDesktop`/`TreeSearchMobile`
  (root header via `StoryCard`/`UserCard`; `TreeSearchTabStrip`; Automatic tab composes
  `TreeSearchControls` + `ResultsFilterPanel` (`AvailableSorts=[]` suppresses its own, differently-
  scoped sort dropdown) + `StoryDeck`; Manual tab is the "Graph view coming soon" placeholder WU40
  will fill in). `TreeSearchControls` (degree slider, grouped edge-type checkboxes, Random/ByDegree
  select, `IncludePaths` auto-derived — never a raw checkbox). `TreeSearchResultBadge` (degree
  label + chain-of-trust-only path chip, story hops only, rendered via a new additive
  `StoryDeck.CardOverlay` `RenderFragment<StoryListingDto>?` slot — default `null` preserves every
  existing `StoryDeck` consumer unchanged). **Corrected by WU40 (2026-07-12):** "story hops only"
  was over-anonymization — chain-of-trust paths (the only paths this badge ever renders) carry no
  anonymized contributor, so user hops should render as named, clickable users, not be collapsed.
  See Feature 33's WU40 settled note above.

  **Runtime bug found + fixed during L4.5 verification (`canalave-conventions/debugging.md`):**
  `TreeSearchControls` originally snapshotted its `Initial*` parameters once in `OnInitialized()`.
  Because `TreeSearchPage.OnInitializedAsync` resolves the root and query-string-seeded
  degrees/edges/sort *asynchronously*, Blazor's synchronous first render passed the component
  its field defaults, and the one-time snapshot then never picked up the real values — the
  controls sidebar showed stale defaults (2 / AuthoredBy+Favorite / Random) even though the
  traversal underneath had correctly used the query-string values (proved by the results: correct
  degree badges and a correct 3-hop path chip). Fixed by moving the snapshot into
  `OnParametersSet()`, re-syncing on every parameter set until a `_userHasInteracted` flag flips
  (set by any control handler) — re-sync during the async race, freeze once the user has an
  opinion. Regression tests: `TreeSearchControlsTests.ReSyncsFromInitialParams_BeforeUserInteracts`
  / `StopsResyncing_AfterUserInteracts_PreservingTheirEdit`. **Note (out of scope for this WU):**
  the same one-time-`OnInitialized()`-snapshot shape exists in the already-Stage-5
  `ResultsFilterPanel`/`SearchPage` pairing (Feature 31) — not touched here; flagging for whoever
  next works that cell.

  **How verified (2026-07-11):** `dotnet build` green (0 errors). `dotnet test`: **Unit** 530
  passing (+`TreeSearchPathParserTests` — composite-array parsing, long-form boolean text,
  user-hop dropping, is_story-flag mutation sanity). **Integration** 424 passing
  (`TreeSearchComposeTests`, Testcontainers Postgres — `SearchAsync` composition: tag filter over
  a co-favorite graph + mutation-sanity untagged control, interaction exclusion applied via the
  composed filter not the traversal, mature story hidden from a restricted viewer via the EF global
  rating filter (proving the raw-reached mode carries no rating check of its own), ByDegree sort
  ordering over a hidden-gem chain, `ResultCapTruncated` computed against the *filtered* set with a
  cap smaller than the raw reachable set). **RazorComponents** 513 passing
  (`TreeSearchControlsTests`, `TreeSearchTabStripTests`, `TreeSearchResultBadgeTests`,
  `TreeSearchDesktopTests`, `TreeSearchMobileTests` — root header story-vs-user branching, Manual
  placeholder hides controls/deck, degree badge per result item, flooding indicator, Apply/tab
  callbacks, the async-race regression pair above).

  **L4.5-Browser (real circuit, `run-server/SKILL.md`):** driven against the persistent
  server-only dev DB already loaded with SeedTool volume data (2,007 users / 3,012 stories /
  46,565 tree edges) and a fresh mart rebuild. Verified live: `/discover/story/{id}` on a
  high-favorite-degree hub story (65 favorite edges) — root header, degree badges ("2nd-degree
  connection") on every result, the flooding indicator banner, and the Filters panel (tags +
  interaction, no misleading sort dropdown) all render correctly; `/discover/story/{id}` on a
  hidden-gem-chain root with only Hidden Gem selected — exactly the two expected chain stories
  returned, each with a correct anonymized path chip (`#20 → #949`, `#20 → #423`) and **no** user
  id ever rendered (**this specific anonymization is corrected by WU40, 2026-07-12** — chain-of-
  trust path chips carry no private contributor and should show usernames; see Feature 33's WU40
  settled note above and the corrected line in `layer3.5-structure.md`); increasing degrees to 4
  reached a 3-hop chain (`#20 → #423 → #1390`) at
  "4th-degree connection", sorted ByDegree; the Manual tab shows the placeholder; `/discover/me`
  resolves the authenticated viewer as a user root (`UserCard` header) and returns the viewer's own
  1st-degree AuthoredBy/Favorite connections correctly, including `IsOwnStory` "Edit Story" on
  their own authored story. No console errors from the feature itself (only stale SignalR-reconnect
  noise from an in-session dev-server restart, unrelated to the app). **L4-Style stays Stage 1**
  (visual/CSS sign-off pending, consistent with WU8/WU13/WU23/WU28 precedent) — **L4.5 flips to
  Stage 5** on the strength of the behavioral verification above, which is independent of the
  aesthetic pass. **L5 stays Stage 2** (rides the future site-wide `InteractiveAuto` flip, per
  `/tags`'s precedent — no WASM work done here; superseded 2026-07-13 by WU-GlobalFlip — see the
  F59 L5 Stage-5 line above).

## Feature 60 — Tree-Search Data-Mart Worker (formerly below the line — line crossed 2026-07-07)
- **L1 — N/A** (raw-SQL mart). **L6 — N/A.** **L8 — Stage 5 (WU-Marts, 2026-07-07 — see Stage note)**
  (narrow edge-list schema settled 2026-07-07 — see implementation notes below, which supersede the
  wide-boolean design preserved earlier; privacy: public edges + edge-owner-consented hidden favorites
  as plain Favorite edges + vouch projection). All other layers **N/A**.

  **WU-Marts Stage note — L8 (2026-07-07):**
  Built: `Server/Discovery/` `DiscoveryMartSchema` (all DDL/build/swap SQL as constants),
  `DiscoveryMartRebuilder` (scoped; per-mart fresh-staging rebuild — DROP+CREATE staging → bulk
  INSERT build → index → one-transaction swap with PK/index RENAMEs; the renames are load-bearing,
  see the schema class doc — instrumented root spans `Marts.{Mart}Rebuild` +
  duration/rows/swap-outcome metrics), `DiscoveryMartWorker` (hosted: bootstrap live tables at
  startup, initial rebuild when empty, then daily at `Marts:RebuildHourUtc` 03:00 UTC; a failed
  rebuild logs Error and the previous live table keeps serving). TestAppFactory removes the worker
  (tests call the rebuilder directly — same treatment as the flush workers). One deliberate
  refinement over the earlier TRUNCATE-and-reuse design: fresh-staging-each-run, because index
  names travel with renamed tables and `CREATE INDEX IF NOT EXISTS` would silently no-op against a
  name owned by the other table.
  **How verified:** **Integration** (`DiscoveryMartTests`): the six-edge projection matrix in one
  test (AuthoredBy / Favorite / consented-hidden-Favorite / no-consent-silent / Recommendation /
  HiddenGem / AuthorSpotlight / Vouch / anonymized-rec-excluded / draft-story-excluded), plus
  rebuild-twice (the rename dance against already-swapped tables). **Unit:** `MartsTelemetryTests`
  (RecordRebuild metric shape). **Headless live check:** `/dev/marts/rebuild` over SeedTool data →
  46,571 tree edges / 463,090 also-favorited / 527,176 also-recommended pairs. Suite: 1398/1398.

## Feature 61 — Also Favorited / Also Recommended (formerly below the line — line crossed 2026-07-07)
- **L1 — N/A** (Phase A removed the EF models; `also_*_scores` are raw-SQL marts — divergence resolved).
  **L2 — Stage 5 (WU-Marts, 2026-07-07 — see Stage note).**
  **L3/L3.5 — Stage 2** (embedded sections on story detail, not separate pages — UI deferred past
  WU-Marts). **L4 — Stage 1.**
  **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13)** — endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto (co-occurrence sections not browser-driven in the flip's
  wave). Full wave narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.
  **L6 — N/A (reclassified 2026-07-07** — mart indexes are
  raw-SQL inside the worker, same treatment as F59; the ranked read rides
  `ix_also_*_scores_story_score`, created by the rebuild, and no EF-migration index is justified —
  the daily batch self-join is an off-hours sequential scan, which the R4 index-audit rule
  deliberately does not index for). **L8 — Stage 5** (co-occurrence scoring workers — built with the
  F60 worker, same rebuild pattern and Stage note; consented hidden favorites contribute per the same
  edge-owner rule as F60 — spec §5.7 corroborates; anonymized recommendations excluded).

  **WU-Marts Stage note — L2/L8 (2026-07-07):**
  Built: `Core/Discovery/ICoOccurrenceReadService` + `RelatedStoryScoreDto`;
  `Server/Discovery/ServerCoOccurrenceReadService` — ranked ADO reads of `also_favorited_scores` /
  `also_recommended_scores` (score DESC, covering index), visibility + rating + viewer §8.7
  exclusions (AlsoFavorited/AlsoRecommended modes) applied at read time; a missing mart degrades to
  empty-with-Warning (never a 500 on a story page — the worker bootstraps the schema at startup so
  this is a should-never state, not a code path relied on); `canalave.cooccurrence.read.duration`
  metric. **How verified:** **Integration** (`DiscoveryMartTests`): shared-user ranking both
  directions, viewer-Ignored exclusion (and anonymous seeing the full list), hidden-favorite
  consent split (consented pair scored, non-consented silent), Also-Recommended mirror with
  anonymized recs excluded. **Headless live check:** `/dev/discovery/also-favorited/176` returned a
  rankable top-5 (scores 17/16/16/16/15) over SeedTool data — the horizontal-line "rankable, not
  just non-empty" bar. Suite: 1398/1398.
- **L7 — removed with the layer (WU-SignalBuffering, 2026-07-06): the mart IS the cache.** The
  planned "Redis in front of the precomputed tables" hot tier is dead — the L8 mart is already the
  daily cache over ground truth; services read `also_*_scores` directly (indexed, buffer-pool-hot)
  and apply real-time exclusion filters in C#. No app-tier read cache, ever (settled). An L1+L2
  cache abstraction (HybridCache/FusionCache) is considered only if a *measured*-hot read path ever
  appears — see `middle_plan_v2.md` Resolved "Layer 7 dissolved".

---

### Note on search-result narrowing
The user-level filter-override UI (formerly tracked as `Filtering/`) is **Missing/Stage 2** (§8.7) — it
likely composes into `ResultsFilterPanel`. Distinct from `CustomLists/` (personal organization).

---

## Layer-8 data-mart implementation notes

In Phase A these tables were removed as EF entities (spec §"Cache / Data Mart Tables" — raw-SQL,
no EF model / DbSet / migration). Names are snake_case. **2026-07-07 (WU-Marts Phase 0): the
tree-search schema below supersedes the wide-boolean design formerly preserved here** (PK
`(user_id, story_id)` + six `is_*` edge booleans + ~10 mirrored filtered partial indexes). The
wide shape was optimal for single-flag access; the rCTE's actual access pattern is multi-type
dynamic ("from a frontier, follow a *selected subset* of edge types, both directions"), which a
narrow edge list serves with one covering range scan per direction instead of a BitmapOr across
partial indexes. Authoritative conventions: `layer8-data-marts.md`.

### `user_story_tree_search_entries` (Features 59/60) — narrow edge list
One row per edge instance: `(user_id int, story_id int, edge_type smallint)`,
PK `(user_id, story_id, edge_type)`. Edge types (each worth 1, no weights): 0 AuthoredBy,
1 Favorite (public + edge-owner-consented hidden), 2 Recommendation, 3 Vouch (projection:
voucher → each of the vouchee's published stories), 4 HiddenGem (≤5/user),
5 AuthorSpotlight (≤5/story). IDs only — no story attributes; rating/display resolved at the
presentation join. Two mirrored covering indexes:

| Index | Shape |
|---|---|
| `ix_tree_search_user_edge` | `(user_id, edge_type) INCLUDE (story_id)` |
| `ix_tree_search_story_edge` | `(story_id, edge_type) INCLUDE (user_id)` |

Build rules: public edges only; hidden favorite → plain Favorite edge iff the *owner* has
`allow_discovery_from_hidden_favorites = true` (no separate boosted flag — jargon removed);
anonymized recs excluded; published/approved content only. Daily zero-downtime `_a`/`_b` swap.

*Spec §5.8 divergence record (spec read-only):* the spec's "vouches strengthen Tree Search edge
weights" is dead — there are no edge weights. Vouch participates in both tree searches as the
projection above (automatic = precomputed `Vouch` edge type; manual = same projection computed
live, WU40). Vouch has no story→story or people-ranking matrix (anti-popularity philosophy,
§5.5 decision 16).

### `also_favorited_scores` / `also_recommended_scores` (Feature 61)
- `also_favorited_scores`: PK `(story_id, also_favorited_story_id)` + `score` (co-occurrence count).
- `also_recommended_scores`: PK `(story_id, also_recommended_story_id)` + `score`.

Full matrix both directions; the mart is read directly — no cache tier in front of it (Layer 7
dissolved 2026-07-06; see Feature 61's L7 note). Algorithm: self-join `user_story_interactions`
WHERE `is_favorite = true` (consented hidden favorites included per the F60 edge-owner rule);
per `(story_a, story_b)` pair, count overlapping users = score. Also-Recommended mirrors on
`recommendations.recommender_id` (anonymized recs excluded).

### `site_daily_stats` (Feature 62)
PK `stat_date` (one row/day). Counters: `new_users, total_users, new_stories, total_stories, new_words,
total_words, page_views, active_users`. Daily aggregation worker.

## L4.5-Browser verification (2026-07-02) — F31 + F32 + F34 → Stage 5

- **F31 Search Page:** filter panel drives the deck end-to-end — genre tag chip + Apply narrowed
  to exactly the tagged stories (see Tags audit); WU28 interaction-exclusion defaults live (the
  viewer's ignored/favorited seed story is absent from default results); Random mode + "Give me
  more" appends fresh draws; Date published sort available; interaction filter checkboxes render.
- **F32 FTS:** typing "versioning" + Apply returned exactly the one story whose listing matches,
  via `plainto_tsquery` against the generated SearchVector; the Sort dropdown gains "Relevance"
  only while a text query is present (panel contract).
- **F34 Tag Directory:** curated post-reseed taxonomy renders grouped by type with counts and
  type jump-nav (see Tags audit for the editor overlay on the same page).
