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
  "give me more" remains WU28). **L3/L3.5 — Stage 5 (WU23, 2026-06-23).** **L4 — Stage 1. L5 — Stage 2.
  L6 — Stage 2.**

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

  **Still open (not settled in WU23):** random-preload / "give me more" pagination (WU28);
  narrowing-within-fixed-source queries (WU27/WU30); per-`SearchMode` default-settings matrix (§8.7).

  **L2 note:** partially advanced — `GetListingsAsync(StoryFilterDto)` (Source=All filtered query)
  is built. The random-preload and "give me more" interaction-button pagination remain Stage 2 (WU28).

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

## Feature 32 — Full-Text Search
- **L1 — Stage 5.** `StoryListing.SearchVector` as a stored generated column from `to_tsvector('english',
  title || short_description)` — exactly the spec pattern (§5.3.2: FTS is a *filter*). Sound.
- **L6 — Stage 5.** GIN index `ix_story_listing_search_vector` written and correct (awaiting migration).
- **L2 — Stage 2** (`Rank()` relevance, WHERE-clause filter usage). **L3/L3.5 — Stage 2. L4 — Stage 1.
  L5 — Stage 2.**

## Feature 33 — Manual Tree Search
- **L1 — N/A** (stateless graph pivots over live tables). **L2 — Stage 2** (per-node stateless query;
  privacy: graph never reveals identity, §5.4). **L3/L3.5 — Stage 2** (distinct graph/node visualization —
  NOT `StoryDeck`). **L4 — Stage 1. L5 — Stage 2. L6 — Stage 2.**

## Feature 34 — Tag Directory (`/tags`)
- **L1 — N/A.** **L2 — Stage 2** (browse query). **L3/L3.5 — Stage 2** (`TagDirectoryPage`: user browse +
  mod CRUD behind `AuthorizeView`; mobile browse, desktop-only edit). Depends on the `TagChip` atom owned
  by Tags/. **L4 — Stage 1. L5 — Stage 2.**

## Feature 59 — Automatic Tree Search (below the line)
- **L1 — N/A** (Phase A removed the EF model; `user_story_tree_search_entries` is a raw-SQL mart — divergence
  resolved). **L2 — Stage 2.** **L3/L3.5 — Stage 2** (unified with manual tree search; degree controls +
  edge-type selector). **L4 — Stage 1. L5 — Stage 2. L6 — N/A** (mart indexes are raw-SQL, see
  implementation notes below). **L8 — Stage 2** (recursive CTE).

## Feature 60 — Tree-Search Data-Mart Worker (below the line)
- **L1 — N/A** (raw-SQL mart). **L6 — N/A.** **L8 — Stage 2** (daily rebuild, zero-downtime `_a/_b`
  swap, privacy model: only public edges + consented hidden favorites; index design preserved below).
  All other layers **N/A**.

## Feature 61 — Also Favorited / Also Recommended (below the line)
- **L1 — N/A** (Phase A removed the EF models; `also_*_scores` are raw-SQL marts — divergence resolved).
  **L2 — Stage 2.**
  **L3/L3.5 — Stage 2** (embedded sections on story detail, not separate pages). **L4 — Stage 1.
  L5 — Stage 2. L7 — Stage 2** (read-side cache pattern 3: Redis in front of precomputed tables, real-time
  exclusion filters in C#). **L8 — Stage 2** (co-occurrence scoring worker).

---

### Note on search-result narrowing
The user-level filter-override UI (formerly tracked as `Filtering/`) is **Missing/Stage 2** (§8.7) — it
likely composes into `ResultsFilterPanel`. Distinct from `CustomLists/` (personal organization).

---

## Layer-8 data-mart implementation notes (preserved from removed EF config)

In Phase A these tables were removed as EF entities (spec §"Cache / Data Mart Tables" — raw-SQL,
no EF model / DbSet / migration). The schema + index design is preserved verbatim below so the Layer-8
workers can recreate it in raw SQL when built. Names are snake_case.

### `user_story_tree_search_entries` (Features 59/60)
PK `(user_id, story_id)`. Edge booleans (public edges only; hidden favorites consent-gated via
`users.allow_discovery_from_hidden_favorites`):
`is_authored_by_user`, `is_public_favorite`, `is_recommendation`, `is_hidden_gem`,
`is_author_spotlighted`, `is_hidden_favorite`.

Mirrored filtered covering indexes (both traversal directions):

**Pattern 1 — User → Stories** (key `user_id` INCLUDE `story_id`):

| Index | Filter |
|---|---|
| `ix_user_tree_user_authored` | `is_authored_by_user = true` |
| `ix_user_tree_user_public_favorite` | `is_public_favorite = true` |
| `ix_user_tree_user_recommendation` | `is_recommendation = true` |
| `ix_user_tree_user_hidden_gem` | `is_hidden_gem = true` |
| `ix_user_tree_user_hidden_favorite` | `is_hidden_favorite = true` |

**Pattern 2 — Story → Users** (key `story_id` INCLUDE `user_id`):

| Index | Filter |
|---|---|
| `ix_user_tree_story_authored` | `is_authored_by_user = true` |
| `ix_user_tree_story_public_favorite` | `is_public_favorite = true` |
| `ix_user_tree_story_recommendation` | `is_recommendation = true` |
| `ix_user_tree_story_spotlighted` | `is_author_spotlighted = true` |
| `ix_user_tree_story_hidden_favorite` | `is_hidden_favorite = true` |

Note the deliberate asymmetry: `is_hidden_gem` is indexed only User→Stories; `is_author_spotlighted` only
Story→Users — matching how each edge is queried. Daily rebuild uses a zero-downtime `_a`/`_b` table swap.

### `also_favorited_scores` / `also_recommended_scores` (Feature 61)
- `also_favorited_scores`: PK `(story_id, also_favorited_story_id)` + `score` (co-occurrence count).
- `also_recommended_scores`: PK `(story_id, also_recommended_story_id)` + `score`.

Full matrix both directions; Redis caches the Top-100 per story (Layer-7 read-side cache). Algorithm:
self-join `user_story_interactions` WHERE `is_favorite = true`; per `(story_a, story_b)` pair, count
overlapping users = score.

### `site_daily_stats` (Feature 62)
PK `stat_date` (one row/day). Counters: `new_users, total_users, new_stories, total_stories, new_words,
total_words, page_views, active_users`. Daily aggregation worker.
