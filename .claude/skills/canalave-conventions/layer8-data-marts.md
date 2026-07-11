> **Built and battle-tested (WU-Marts, 2026-07-07).** Design settled after a first-principles
> audit against the original deliberations (`Boolean_Logic_Search_Filter_Deliberations.md` §2–3,
> `GeminiDiscussions/MyActivity …_filtered.md`); the discovery mart family, worker, and F59/F61
> consumers below are implemented and verified (1398/1398 tests; `audit/Discovery.md` Stage notes).
>
> **`SiteDailyStat` requirements settled (2026-07-10, WU-SiteDailyStat plan); build follows.** The
> earlier "raw-SQL mart, no EF model, matching the other three" framing is **superseded** — see
> §"`site_daily_stats`" below for the reversal and why. Design source: the same Gemini deliberation
> above (2025-10-29 entry, `GeminiDiscussions/…_filtered.md:38146`) plus a full counter-by-counter
> source audit against the current schema. See `audit/Moderation.md` Feature 62.

# Layer 8 — Data Mart Workers

Non-EF-Core background workers: raw SQL table creation, zero-downtime swap, recursive-CTE consumers.
These tables have NO EF Core model classes, no DbSets, no migrations.

**One documented exception: `site_daily_stats`.** It is not a rebuildable mart — it is an
append-only time-series of durable ground truth (one immutable row per completed day, never
swapped/rebuilt). It gets a normal EF entity + `DbSet` + migration so a dashboard can read it with
LINQ like every other read model; the daily worker still writes it via raw `INSERT … ON CONFLICT`,
never through the EF change tracker. See §"`site_daily_stats`" below for the full reasoning — don't
extend this exception to any other table in this layer without the same reasoning applying.

## When This Layer Applies

Data mart workers produce pre-calculated data for consumption by search and discovery features.
They operate on a different cadence than live queries — daily rebuilds during off-hours.

**The horizontal line was crossed deliberately (2026-07-07).** The old rule ("features requiring
real user data are deferred past beta") is superseded: the extended-seed track
(`TheCanalaveLibrary.SeedTool`) generates *synthetic but realistically-clustered* data —
taste-communities, power-law story popularity, supernode recommenders, wired hidden-gem chains,
author-spotlight edges, vouches toward low-volume authors, consent-flagged hidden favorites, plus
threaded chapter comments and action-derived notifications (added for WU-L6 index measurability) —
so the mart family and its consumers (F59/F61) are buildable and verifiable pre-beta. Uniform-random
volume is NOT sufficient (co-occurrence stays near-equal noise; graphs stay degenerate); the
clustered *distribution* is what makes output human-legible.

## Workers

| Worker | Schedule | Purpose | Folder |
|---|---|---|---|
| `DiscoveryMartWorker` (hosted) → `DiscoveryMartRebuilder` (scoped) | Daily off-hours (`Marts:RebuildHourUtc`, default 03:00 UTC) + bootstrap/rebuild-when-empty at startup | Rebuild `user_story_tree_search_entries` + `also_favorited_scores` + `also_recommended_scores` sequentially (no concurrent DDL) | `Discovery/` |
| `SiteDailyStatWorker` (hosted) → `SiteDailyStatAggregator` (scoped) | Daily off-hours (`Marts:RebuildHourUtc`) + bounded startup gap-fill | Upsert the previous completed UTC day's row into `site_daily_stats` (`INSERT … ON CONFLICT (stat_date) DO UPDATE` — append-only, not a swap rebuild) | `Moderation/` |

The hosted worker/scoped rebuilder split is deliberate: integration tests and the dev probe
(`POST /dev/marts/rebuild`) trigger rebuilds deterministically via the rebuilder;
`TestAppFactory` removes the hosted worker (same treatment as the signal-buffer flush workers).
A failed rebuild logs Error and the loop continues — the previous live table keeps serving.
Worker queries use **raw SQL via `_context.Database.ExecuteSqlRawAsync()`**, not EF Core LINQ.
Every rebuild is instrumented via `CanalaveTelemetry.Marts` (root span per rebuild + duration /
row-count / swap-outcome metrics — see `logging.md` §"Custom instrumentation").

## Zero-Downtime Cache Refresh (Table Swap)

Two physical tables (`_a`/`_b`); the live name points at the active one:

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

Reference scaffold: `TheCanalaveLibrary.Server/ReferenceSQL/AlsoFavoritedStaging.sql`.

## Data Mart Tables

### `user_story_tree_search_entries` — the traversal edge list (Features 59/60)

**Narrow edge list, not a wide boolean table** (settled 2026-07-07, superseding the earlier
wide-boolean design preserved in `audit/Discovery.md`): one row per edge instance —

```sql
user_id    int      NOT NULL,
story_id   int      NOT NULL,
edge_type  smallint NOT NULL,   -- TreeSearchEdgeType enum value
PRIMARY KEY (user_id, story_id, edge_type)
```

Rationale: the recursive CTE's access pattern is *"from a frontier of node IDs, follow a
dynamically-selected subset of edge types, both directions."* Narrow serves that as one covering
index range scan per direction (`node_id = ANY(frontier) AND edge_type = ANY(selected)`) with
exactly **two mirrored covering indexes**:

```sql
-- traversal User → Stories (PK prefix already covers (user_id, ...); keep explicit for clarity)
ix_tree_search_user_edge   ON (user_id, edge_type) INCLUDE (story_id)
-- traversal Story → Users
ix_tree_search_story_edge  ON (story_id, edge_type) INCLUDE (user_id)
```

A wide boolean table is optimal only for single-flag access (why `user_story_interactions` is
wide — one flag per bookshelf tab); multi-type dynamic traversal over booleans would need a
`UNION`/BitmapOr across ~12 partial indexes and inflate the daily rebuild. The mart is a
read-only daily-rebuilt read model (no OLTP/HOT concerns): its shape follows its read pattern.

**IDs only.** No story attributes (rating, title, status) live in the mart; the consumer joins
results to `story_listings` at presentation time for rating filtering + display. Traversal
helpers (degree, path, cycle-mark) are the **rCTE's runtime columns**, never stored.

**Edge taxonomy** (every edge worth 1 — no edge-type weights, ever; differentiation is
provenance + fan-out cap):

| `edge_type` | Edge | Source of truth | Projected fan-out | Tier |
|---|---|---|---|---|
| 0 | AuthoredBy | `stories.author_id` | an author's many stories | wide |
| 1 | Favorite | `user_story_interactions.is_favorite` (+ consented hidden favorites) | unbounded | wide |
| 2 | Recommendation | `recommendations.recommender_id` | unbounded (power recommenders) | mid |
| 3 | Vouch | `vouches` ⨝ vouchee's published stories | ≤5 vouchees × unbounded stories | mid |
| 4 | HiddenGem | hidden-gem recommendations | **≤5 stories / user** | deep (chain of trust) |
| 5 | AuthorSpotlight | author-spotlighted recommendations | **≤5 recommenders / story** | deep (chain of trust) |

- **AuthorSpotlight is first-class alongside HiddenGem** (settled 2026-07-07 — supersedes any
  doc marking it deferred). It is "hidden gem in reverse": the author confers the honor on the
  (recommender, story) pair; the recommender self-confers HiddenGem. Same shape, same ≤5 cap.
- **Vouch is projected, not raw**: a vouch `A→B` has no story, so the worker emits
  `(A, s, Vouch)` for every published story `s` authored by `B`. This encodes vouch's purpose
  (promote an up-and-comer's work through the voucher's discoverability). Because the vouchee's
  story count is unbounded, Vouch is **mid-tier** — not chain-of-trust. Never a live `vouches`
  join inside the rCTE; the projection is precomputed here.
- Only HiddenGem and AuthorSpotlight are hard-capped on the projection → they are the **deep /
  chain-of-trust** edges that make `MaxDegrees` 5–6 traversal tractable.

**Privacy & consent (build rules):**
- Public edges only, except: a user's **hidden favorite** is included as a plain `Favorite` edge
  **iff that user** (the edge owner, not the searcher) has `allow_discovery_from_hidden_favorites
  = true`. No separate "boosted"/"anonymous" edge type — the daily-rebuild delay plus
  IDs-only results already sever the action↔identity link, and there is no searcher-side toggle
  (no differential attack surface).
- **Anonymized recommendations** (`recommender_id IS NULL`) contribute no edge.
- Build predicate: only published/visible stories, only approved non-taken-down recommendations.
- Traversal never returns edge-owner identity.

### `also_favorited_scores` / `also_recommended_scores` — co-occurrence result marts (Feature 61)

A different *shape* from the edge list: precomputed story→story **results**, not edges.

- `also_favorited_scores`: PK `(story_id, also_favorited_story_id)` + `score` (co-occurrence count).
- `also_recommended_scores`: PK `(story_id, also_recommended_story_id)` + `score`.

Full matrix both directions; the mart is read directly, ranked by `score` — no cache tier in
front of it (Layer 7 dissolved 2026-07-06: the mart IS the cache).

**Algorithm:** self-join `user_story_interactions` WHERE `is_favorite = true` (plus consented
hidden favorites — same edge-owner consent rule as the tree mart); per `(story_a, story_b)` pair,
count overlapping users = score. Also-Recommended mirrors on `recommendations.recommender_id`
(anonymized recs excluded). Exclude self-pairs; store both directions.

### `site_daily_stats` (Feature 62) — EF read model, not a rebuildable mart

**Requirements settled 2026-07-10** (design source: Gemini deliberation 2025-10-29,
`GeminiDiscussions/…_filtered.md:38146`, reconciled against the live schema; full audit and
ambiguity resolution in `audit/Moderation.md` Feature 62).

**Shape:** an **append-only time-series of durable ground truth** — one immutable row per
*completed* UTC day, written once by the daily worker and never rebuilt. This is the opposite of
the three discovery marts above: there is no staging table, no swap, and past days cannot be
reconstructed once written (a `stat_date` row is the only record of that day's `active_users`).

**EF model (the one Layer-8 exception):** `SiteDailyStat` gets a normal `DbSet<SiteDailyStat>`
(PK `stat_date`) and an EF migration owns the schema — unlike every other table in this file. This
is deliberate: low-volume ground truth (~365 rows/year) with **rich time-series reads** (date-range
queries, ordering, latest-N) is exactly EF/LINQ's job, and is a poor fit for the swap-DDL machinery
the other marts need (which would fight an EF-tracked table) or for the SUM-only access pattern
`daily_story_stats` (its own migration-managed-no-EF-model neighbor) was built for. **The worker
still writes only via raw `INSERT … ON CONFLICT (stat_date) DO UPDATE`, never through the EF change
tracker** — EF owns schema + reads, the worker owns writes. State this split in the entity's XML
doc comment so no one adds a tracked write path.

**Columns — flow (`new_`, per-day) vs. stock (`total_`, as-of end-of-day):** a `total_` column
exists **only** where both hold: (1) it's a headline platform-size curve worth charting, and
(2) the population can shrink (deletions/takedowns), so the stored snapshot carries information a
running sum of `new_` can't reconstruct. That's true for exactly three: `total_users`,
`total_stories` (published/visible only), `total_words`. Everything else is flow-only — a lifetime
total is either meaningless (`active_users`), already stored elsewhere (`story_views` lifetime =
`SUM(daily_story_stats)`), or cheaply derivable at read time via a windowed cumulative sum.

| Column | Kind | Source |
|---|---|---|
| `stat_date` | PK | the completed UTC day |
| `total_users` | stock | `COUNT(User WHERE created_utc <= end-of-day)` |
| `total_stories` | stock | `COUNT(Story WHERE published_date <= end-of-day AND published/visible)` |
| `total_words` | stock | `SUM(Story.WordCount)` as-of-day |
| `new_users` | flow | `User.CreatedUtc` |
| `new_stories` | flow | `Story.PublishedDate` |
| `new_chapters` | flow | `ChapterContent.PublishDate` |
| `new_words` | flow | `SUM(ChapterContent.WordCount)` on `PublishDate` |
| `new_comments` | flow | UNION of the 4 TPT comment children's `DatePosted` |
| `new_blog_posts` | flow | `ProfileBlogPost`/`GroupBlogPost.DateCreated` |
| `new_groups` | flow | `Group.DateCreated` |
| `new_follows` | flow | `FollowedUser.DateFollowed` |
| `new_recommendations_written` | flow | `Recommendation.DatePosted` |
| `new_recommendation_successes` | flow | `RecommendationSuccess.DateRecorded` |
| `reports_filed` | flow | `Report.DateReported` |
| `reports_resolved` | flow | `Report.DateResolved` |
| `favorites_added` | flow | `UserStoryInteractionDate.FavoriteDate` OR `HiddenFavoriteDate` — confirmed at build (`ServerModerationWriteService`/`UserStoryInteractionDate.cs`) |
| `chapters_read` | flow (proxy) | `UserChapterInteraction.LastInteractionDate` — mutable/overwritten stamp, documented as approximate, go-forward only |
| `story_views` | flow | `SUM(daily_story_stats.view_count)` for the day — **story reads only**, not site-wide page views (no such stream exists) |
| `active_users` | flow (go-forward only) | `COUNT(DISTINCT User WHERE last_active_utc` is within the day`)` — requires `User.LastActiveUtc`, a new signal-buffered column (see `layer2-services.md` §"Signal Buffering"); nullable/absent for any day before that column existed |

**Deliberately excluded** (settled, not oversights): `new_series` (rearranges existing stories, not
new content), `new_vouches` (capped ≤5/user and swapped out as authors "make it" — not an
accumulation metric), `new_badges_awarded` (badge semantics too non-uniform to sum meaningfully),
message/conversation counts (private content, not tracked), and any **like** count — `CommentLike`
and its siblings carry no date **by deliberate anti-addictive design**.

**Dropped at build time:** `stories_approved` — confirmed no dated column exists anywhere on the
approval path (`ServerModerationWriteService.ApproveStoryAsync` flips `Story.StoryStatusId` with no
timestamp write; no `DateApproved` field on `Story`/`StoryDetail`). Adding one is a schema change
outside this build's scope — the moderation-health panel's approval signal comes from
`reports_resolved` only for now. Revisit if a `DateApproved` column is ever added for other reasons.

**Privacy stance for `active_users`/"last seen" (settled):** `User.LastActiveUtc` is stamped for
**authenticated requests only**, riding the existing strictly-necessary auth-session cookie — no
new tracking cookie, no third party, no logged-out fingerprinting. First-party functional data is
consent-exempt under GDPR/ePrivacy; this is the same category of "last seen" feature common to
first-party community platforms, not ad-tech tracking. The aggregate `active_users` count includes
every authenticated user regardless of setting (it reveals no individual); the *public per-profile*
"last seen on …" display alone is gated by the pre-existing `PrivacySettings.ShowActivityStatus`
toggle.

**Worker:** `SiteDailyStatWorker` (hosted) → `SiteDailyStatAggregator` (scoped) — same
hosted/scoped split as the discovery marts (deterministic Integration/`/dev`-probe triggering,
`TestAppFactory` drops the hosted worker), but the aggregator **upserts one day's row**, it does not
rebuild a table. Runs at the configured off-hours hour against the previous completed UTC day;
bounded startup gap-fill backfills the backfillable columns for missed days (`active_users` stays
null for gap-filled days — honest, not reconstructable). Raw SQL via `ExecuteSqlRawAsync`, not LINQ,
matching every other mart worker's write path. Instrumented via `CanalaveTelemetry` (root span +
duration/row/outcome), matching the discovery marts.

**Dashboard (Feature 62 flourish, beyond MVP):** a mod/admin-gated `/mod/stats` page reads
`SiteDailyStat` via LINQ (headline totals, growth-over-time charts, content/community activity, a
moderation-health panel of reports filed vs. resolved — `stories_approved` dropped, see above).
Charts follow the `dataviz` skill + the element-role design system; no external chart CDN (CSP) —
self-contained rendering only.

## The Automatic Tree Search consumer (Feature 59) — rCTE conventions

The traversal runs **live at query time** against the edge-list mart (the mart precomputes edges,
never traversal results — precomputing results would explode combinatorially across
edge-selection × depth and destroy path semantics):

- **PostgreSQL 14+ `SEARCH BREADTH FIRST` / `CYCLE` clauses** for degree tracking and cycle
  handling — not hand-rolled `DISTINCT`/visited-set logic.
- Recursive term stays **lean**: node IDs + degree only; no score column (every edge is worth 1),
  no display fields. Tag filters, rating filter, and the active user's own exclusion filter apply
  **after** traversal at the presentation join (`story_listings` + live `user_story_interactions`).
  Accepted consequence: a mature story can be a silent *bridge* node in a SFW searcher's walk —
  it is never shown; strict "no mature routing" is a deferred future toggle.
- **Fan-out `LIMIT` inside the recursive step** protects wide mode (supernodes); deep mode is
  structurally protected by the ≤5 caps.
- **Result ordering:** each story carries its minimum degree-to-reach; the service exposes two
  sort orders over the same result set — random shuffle, or by degree ascending. No path score.
- **Path materialization** is a required service capability the *searcher* opts into per request,
  available **only when the selected edge set ⊆ {HiddenGem, AuthorSpotlight}** (the truly-capped
  edges where a single path is meaningful). Unbounded edges (Favorite, AuthoredBy, Recommendation,
  Vouch) yield combinatorially noisy paths — path is not offered there.
- Instrumented via `CanalaveTelemetry.Discovery` (`Discovery.TreeSearchTraverse` span; duration /
  fan-out / degrees-reached / result-count histograms + cap-truncation counter — `logging.md`).

**Manual Tree Search (F33, WU40) deliberately does NOT use the mart** — resolved 2026-07-03,
reaffirmed 2026-07-07: it is degree-1 interactive (must be fresh; the mart is a daily rebuild) and
needs edge detail (recommendation blurb, spotlight context) the IDs-only mart cannot carry. It
reads the same edge *semantics* from live tables, including the vouch projection computed live.
See `audit/Discovery.md` Feature 33.

## Layer 2 Workers (NOT Layer 8)

These operate on EF-modeled tables and belong in Layer 2, not here:

| Worker | Layer | Why |
|---|---|---|
| Notification cleanup (60-day) | 2 | Operates on EF-modeled `Notification` table |
| UserStat recalculation | 2 | Corrects drift in EF-modeled `UserStats` |
| Badge awarding (MVP inline) | 2 | Synchronous inline check in service methods |

Also NOT Layer 8: `daily_story_stats` (view-count flush target) — migration-managed raw DDL but
**ground truth**, not a rebuildable mart (see `layer2-services.md` §"Signal Buffering").
