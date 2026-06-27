> **Provisional — Stage 2 (unbuilt).** This file records design intent for post-MVP work validated against the spec, not against built code. Re-verify all details when this layer is implemented.

# Layer 8 — Data Mart Workers

Non-EF-Core background workers: raw SQL table creation, zero-downtime swap, recursive CTEs.
These tables have NO EF Core model classes, no DbSets, no migrations.

## When This Layer Applies

Data mart workers produce pre-calculated data for consumption by search and discovery features.
They operate on a different cadence than live queries — daily rebuilds during off-hours.
Features requiring real user data are deferred past beta.

## Workers

| Worker | Schedule | Purpose | Folder |
|---|---|---|---|
| TreeSearch data mart rebuild | Daily off-hours | Rebuild `UserStoryTreeSearchEntries` | `Discovery/` |
| AlsoFavorited/AlsoRecommended rebuild | Daily off-hours | Rebuild collaborative-filter caches | `Discovery/` |
| SiteDailyStat aggregation | Daily | Aggregate into `SiteDailyStat` | `Moderation/` |

All are `IHostedService` / `BackgroundService` in the server project.
Worker queries use **raw SQL via `_context.Database.ExecuteSqlRawAsync()`**, not EF Core LINQ.

## Zero-Downtime Cache Refresh (Table Swap)

Two physical tables (`_a`/`_b`), a view/function points at the active one:

1. Worker identifies the inactive ("staging") table.
2. `TRUNCATE` the staging table.
3. Populate with fresh data via raw SQL `INSERT INTO ... SELECT ...`.
4. Atomically swap via `ALTER TABLE ... RENAME` within a PostgreSQL transaction.
5. Next run uses the other table as staging.

```sql
BEGIN;
ALTER TABLE user_story_tree_search_entries RENAME TO user_story_tree_search_entries_old;
ALTER TABLE user_story_tree_search_entries_staging RENAME TO user_story_tree_search_entries;
ALTER TABLE user_story_tree_search_entries_old RENAME TO user_story_tree_search_entries_staging;
COMMIT;
```

## Data Mart Tables

### UserStoryTreeSearchEntry

Pre-calculated graph traversal table. Contains only public edges:
- `IsAuthoredByUser`, `IsPublicFavorite`, `IsRecommendation`, `IsHiddenGem`,
  `IsAuthorSpotlighted`, `IsHiddenFavorite` (consent-based only: `AllowDiscoveryFromHiddenFavorites`).
- Mirrored filtered indexes both directions.

### AlsoFavoritedScore / AlsoRecommendedScore

`(StoryId, AlsoFavoritedStoryId)` + `Score` (co-occurrence count).
Full matrix both directions. Redis Top 100 per story.

**Algorithm:** Self-join on `UserStoryInteraction` WHERE `IsFavorite = true`. For each pair
`(StoryA, StoryB)`, count overlapping users = Score.

### SiteDailyStat

`StatDate` (PK), counters: NewUsers, TotalUsers, NewStories, TotalStories, NewWords,
TotalWords, PageViews, ActiveUsers.

## Deferred Past Beta (Needs Real User Data)

These features fail the horizontal-line test ("can a human tell if it's working with seed data?"):
- **Automatic Tree Search** — recursive CTE against seed data produces degenerate graphs.
- **Also Favorited / Also Recommended** — co-occurrence scoring needs real patterns.
- **SiteDailyStat Worker** — nothing to aggregate yet.

Features that *produce the signal* these consume (Favorites, Recommendations, Manual Tree Search,
Following, Vouches) ship in the MVP.

## Layer 2 Workers (NOT Layer 8)

These operate on EF-modeled tables and belong in Layer 2, not here:

| Worker | Layer | Why |
|---|---|---|
| Notification cleanup (60-day) | 2 | Operates on EF-modeled `Notification` table |
| UserStat recalculation | 2 | Corrects drift in EF-modeled `UserStats` |
| Badge awarding (MVP inline) | 2 | Synchronous inline check in service methods |
