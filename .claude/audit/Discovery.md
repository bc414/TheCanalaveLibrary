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

## Feature 31 — Search Page (`/discover`)
- **L1 — N/A** (queries Story/USI/StoryListing). **L2 — Stage 2** (Source=All query, random preload,
  "give me more" where interaction buttons *are* pagination). **L3/L3.5 — Stage 2** (`ResultsFilterPanel`
  + `StoryDeck`). **L4 — Stage 1. L5 — Stage 2. L6 — Stage 2.**

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
- **L1 — Stage 4** (data-mart-as-EF-entity divergence on `UserStoryTreeSearchEntry`). **L2 — Stage 2.**
  **L3/L3.5 — Stage 2** (unified with manual tree search; degree controls + edge-type selector).
  **L4 — Stage 1. L5 — Stage 2. L6 — Stage 4** (mirrored graph indexes written against the EF-modeled
  mart). **L8 — Stage 2** (recursive CTE).

## Feature 60 — Tree-Search Data-Mart Worker (below the line)
- **L1 — Stage 4** (same). **L6 — Stage 4.** **L8 — Stage 2** (daily rebuild, zero-downtime `_a/_b`
  swap, privacy model: only public edges + consented hidden favorites). All other layers **N/A**.

## Feature 61 — Also Favorited / Also Recommended (below the line)
- **L1 — Stage 4** (`AlsoFavoritedScore`/`AlsoRecommendedScore` modeled as EF entities). **L2 — Stage 2.**
  **L3/L3.5 — Stage 2** (embedded sections on story detail, not separate pages). **L4 — Stage 1.
  L5 — Stage 2. L7 — Stage 2** (read-side cache pattern 3: Redis in front of precomputed tables, real-time
  exclusion filters in C#). **L8 — Stage 2** (co-occurrence scoring worker).

---

### Note on search-result narrowing
The user-level filter-override UI (formerly tracked as `Filtering/`) is **Missing/Stage 2** (§8.7) — it
likely composes into `ResultsFilterPanel`. Distinct from `CustomLists/` (personal organization).
