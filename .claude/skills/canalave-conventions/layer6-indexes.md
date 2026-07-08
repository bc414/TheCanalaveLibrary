> **Built and measured (WU-L6, 2026-07-07).** This file was rewritten against reality: the
> `L6_IndexBatch` migration, a rerunnable perf fixture (`TheCanalaveLibrary.PerfBaseline`), and
> before/after numbers at SeedTool volume (2k users / 3k stories / 38k interactions / 324k
> comments / 20k notifications). Everything below is either measured, PK-served, or explicitly
> rejected with a reason — no aspirational DDL remains.

# Layer 6 — SQL Indexes

Filtered, composite, golden, GIN indexes — pure DDL, zero code changes.
This layer pairs with optional query optimization (method body swaps behind stable interfaces).

## When This Layer Applies

Layer 6 activates when profiling identifies slow queries — and profiling requires volume:
**toy dev-seed data cannot exercise the planner** (a 13-row table seq-scans no matter what you
index). Run the SeedTool extended dataset first (`run-server/SKILL.md` §"Extended seed"), then
the baseline fixture:

```powershell
dotnet run --project TheCanalaveLibrary.PerfBaseline -- --label before
# ... apply the index migration (start the app once) ...
dotnet run --project TheCanalaveLibrary.PerfBaseline -- --label after
dotnet run --project TheCanalaveLibrary.PerfBaseline -- --compare before after
```

The fixture runs the app's real hot query shapes (provenance comments name the service methods),
reports p50/p95, and captures `EXPLAIN (ANALYZE, BUFFERS)` per scenario into
`TheCanalaveLibrary.PerfBaseline/results/`. It is deliberately dependency-free (no NBomber —
v5 licensing needs registration; no k6 — external binary): a baseline fixture must stay
`dotnet run`-able forever.

**Index audit rule (R4):** every index must be justified by a *current query or a spec'd planned
feature's* query pattern — each one taxes every write and every vacuum. Daily off-hours batch
scans (the L8 mart builds) do NOT justify OLTP indexes; sequential scans are correct there.

## ⚠ Multiple indexes on the same columns — the HasIndex name argument is LOAD-BEARING

**The most expensive lesson of WU-L6.** Multiple *unnamed* `HasIndex` calls on the SAME property
set do not declare multiple indexes — each call re-opens the ONE unnamed index definition and
silently overwrites its filter and database name. **Only the last call survives into the
migration.** The seven `user_story_interactions` filtered indexes were declared unnamed from WU0
onward; the database contained only the last one (`has_started`) until 2026-07-07, while six
bookshelf tabs ran unindexed — and the 2026-07-06 R4 audit ("all 7 justified, none dropped")
audited the *config file*, not the database. Two rules fall out:

1. **Distinct indexes on the same columns need the name argument:**
   ```csharp
   builder.HasIndex(e => e.UserId, "ix_user_story_interactions_favorite")   // name = load-bearing
       .IncludeProperties(e => e.StoryId)
       .HasFilter("\"is_favorite\" = true")
       .HasDatabaseName("ix_user_story_interactions_favorite");
   ```
2. **Index claims are verified against the migration snapshot or `pg_indexes`, never against
   the configuration file.** `SELECT indexname FROM pg_indexes WHERE tablename = '…'` is the
   ground truth; a config file states intent.

Other syntax notes that survive from the original design:
- Filters use snake_case column names and PostgreSQL `true`/`false` (not `1`/`0`).
- Covering indexes use Npgsql's **`.IncludeProperties()`** (a bare `.Include()` doesn't exist on
  `IndexBuilder`).
- Always name explicitly with `HasDatabaseName()` for migration stability.
- Composite indexes whose leading columns are equality-bound don't need `IsDescending` for a
  DESC ordering — Postgres scans the b-tree backward (`Index Scan Backward` in the plans below).

## The built set (L6_IndexBatch, 2026-07-07) — with measurements

All numbers: SeedTool extended dataset, local PG18, p50 over 40 iterations on the hottest ids
(`PerfBaseline/results/{before,after}.json`; plans in `results/explain-{before,after}/`).

### user_story_interactions — seven filtered covering indexes (six restored)

One per bookshelf-tab flag, all `ON (user_id) INCLUDE (story_id) WHERE (<flag> = true)`:
`ignored, favorite, hidden_favorite, followed, read_it_later, completed, has_started`.
Justification: one per bookshelf tab + profile favorites + the §8.7 discovery-exclusion probes.
Measured: favorites tab p50 0.19→0.13 ms; the win is modest at 38k rows because the PK
`(user_id, story_id)` prefix already narrows per-user scans — these indexes are about the
*flag-filtered* scan staying index-only at real scale, and about the discovery-exclusion probe
(−68%, below).

### Comment golden indexes — the headline (−98.8%)

```
ix_chapter_comments_chapter_id_date_posted        ON chapter_comments (chapter_id, date_posted)
ix_blog_post_comments_blog_post_id_date_posted    ON blog_post_comments (blog_post_id, date_posted)
ix_group_comments_group_id_date_posted            ON group_comments (group_id, date_posted)
ix_user_profile_comments_profile_user_id_date_posted ON user_profile_comments (profile_user_id, date_posted)
```

The four comment contexts share a byte-identical roots-page query
(`ServerCommentReadService`: filter by owner id + `parent_comment_id IS NULL`, order
`date_posted DESC`, LIMIT). At 324k comments the before-plan on a hub chapter was
bitmap-scan → sort → **Gather Merge with parallel worker launch (~20 ms of pure startup)**;
p50 24.32 ms, p95 136.82 ms. The composite index turns it into
`Index Scan Backward … streaming into the LIMIT`: **p50 0.29 ms, p95 0.38 ms (−98.8%)**;
the roots COUNT fell 21.0→0.61 ms. Each supersedes its plain FK index (prefix-covered — EF
drops the convention index automatically).

TPT note: `parent_comment_id`/`is_taken_down` live on `base_comments`, so one covering index
across the TPT boundary is impossible — the child-side composite + per-row PK probe into the
base is the best available shape, and at 0.05 ms execution it is enough.

### stories — the two discovery sort spines (−76%)

```
ix_stories_published_date      ON stories (published_date)
ix_stories_last_updated_date   ON stories (last_updated_date)
```

DatePublished is /discover's sorted default; LastUpdated drives `GetRecentListingsAsync` and the
Relevance tie-break. Top-N pages walk the index in order with the global rating/`is_taken_down`
filters as cheap residuals (deliberately NOT prefixed into the index — a rating-prefixed variant
would fragment across ceiling values). Measured: 0.39→0.09 ms p50 at 3k stories; this is the
index whose value grows fastest with story count. The discovery-exclusion probe page
(NOT EXISTS against USI) fell 0.68→0.22 ms (−68%) riding this + the restored `ignored` partial.

### notifications — one composite for count + feed

```
ix_notifications_recipient_read_date  ON notifications (recipient_user_id, is_read, date_created)
```

Serves the unread count (recipient + is_read prefix: 0.17→0.09 ms, −47%) and the
OldestUnreadFirst ordering exactly. NewestFirst measured +6.7% (noise): it narrows by recipient
then sorts the per-user residual — accepted, per-user notification counts are bounded (60-day
cleanup worker). Supersedes the recipient FK index.

### private_messages — thread paging

```
ix_private_messages_conversation_id_date_sent  ON private_messages (conversation_id, date_sent)
```

The thread page (`WHERE conversation_id = @c ORDER BY date_sent DESC LIMIT n`) walks it in
order. R4-justified by the query; not measurable yet (SeedTool generates no messages) — volume
justification inherits from the identical comment-paging shape. Supersedes the FK index.

## Verified pre-existing (unchanged by WU-L6)

- **GIN FTS**: `ix_story_listing_search_vector` on the generated `SearchVector` tsvector column
  (title + short description only, NOT chapter body).
- **Golden mart indexes** (raw SQL, owned by the L8 workers, not EF —
  `layer8-data-marts.md`): the two tree-search covering composites + the ranked
  `ix_also_*_scores_story_score`. Measured healthy: tree-search rCTE p50 ~2.0 ms wide /
  ~1.5 ms deep; ranked co-occurrence read ~0.2–0.5 ms.
- **Vouches**: PK `(vouching_user_id, vouched_user_id)` covers outgoing;
  `ix_vouches_vouched_user_id` (EF convention FK index) covers incoming.
- **EF convention FK indexes** exist on every FK not superseded above — check `pg_indexes`
  before assuming a gap.

## Rejected — no matching query (R4), reconfirmed against the 2026-07-07 codebase survey

| Candidate | Why rejected |
|---|---|
| Story-centric USI mirrors `ON (story_id) … WHERE flag` | **No story-centric interaction query exists.** Story favorite counts are denormalized (`UserStat.FavoritesOnStories`); the discovery/F59 exclusion probes bind `(user_id, story_id)` = the PK. Revisit only if a "who favorited this story" surface ships. |
| `user_story_interaction_dates` date indexes | **The table is never read.** The profile favorites tab returns an unordered id list; no query touches `favorite_date`. Index when a date-sorted list ships, not before. |
| USI composite-boolean partials (ActivelyReading / Abandoned) | Both tabs measured ≤0.13 ms p50 riding the restored `has_started` partial + residual flag checks; per-user row counts are small. |
| `story_tags (tag_id, story_id) INCLUDE (priority)` reverse composite | The tag probes are correlated EXISTS on `(story_id, tag_id)` = the PK, and no probe reads `priority`. The convention `ix_story_tags_tag_id` covers tag-driving scans. Measured: tag filter unchanged (+7% noise) — the PK was already optimal. |
| `followed_users (user_id, date_followed)` | List query narrows by the PK prefix; per-user follow counts are small (same rationale as the Bookshelves recency ruling below). |
| Tag-name trigram (ILIKE '%term%') | Tag table is tiny (hundreds of rows); would need the `pg_trgm` extension for a leading-wildcard match. Revisit only if tags grow by orders of magnitude. |
| Rating/`is_taken_down` prefixes on the story sort spines | Cheap residuals; a prefixed index fragments across rating-ceiling variants. |

## MVCC Storage Tuning — fillfactor, HOT updates, autovacuum (R4, 2026-07-06)

Postgres reasoning the SQL-Server-era design never did (under MVCC writers don't block readers —
the real UPDATE costs are dead tuples, vacuum pressure, and index write-amplification):

- **Every UPDATE writes a new row version.** It is HOT (Heap-Only Tuple — no index maintenance,
  dead tuple invisible to indexes) only when (a) no indexed column changed — **partial-index
  predicate columns count** — and (b) the page has free room. Corollaries:
  - `user_story_interactions`: every flag toggle changes a partial-index predicate → HOT is
    structurally defeated → `fillfactor` is wasted space there; aggressive autovacuum is the lever.
  - `user_chapter_interactions` and `daily_story_stats` (the signal-buffer flush targets — the
    highest-UPDATE tables): their hot-updated columns are deliberately unindexed → HOT-eligible →
    `fillfactor = 90` reserves same-page room. **Do not index `read_progress` /
    `last_interaction_date` / `view_count` without re-weighing this** — an index on a hot-updated
    column re-imposes full index maintenance on every flush.
  - The new comment/notification/message composites index INSERT-only columns (`date_posted`,
    `date_created`, `date_sent` never change after insert) — no HOT impact.
- **Index-only scans need a fresh visibility map** — covered partial indexes only skip the heap
  when vacuum has run recently. High-churn tables get `autovacuum_vacuum_scale_factor = 0.05`
  (vacuum at 5% dead rows, vs the 20% default).
- Both knobs live in the `R4_MvccStorageTuning` migration (raw `ALTER TABLE … SET (…)`).
  `fillfactor` applies to future page writes only — no table rewrite.
- The Bookshelves "Actively Reading" recency sort (`MAX(uci.last_interaction_date)` per story)
  deliberately rides the `user_chapter_interactions` PK prefix `(user_id, …)` rather than a new
  index — per-user row counts are small, and indexing `last_interaction_date` would defeat HOT.

## Query Optimization (Paired with Index Addition)

| Stage | Query looks like | When |
|---|---|---|
| MVP (Layers 1–4) | Two composed queries via service injection | Initial implementation |
| Post-profiling (Layer 6) | Index added + optional single optimized JOIN | Measurement shows bottleneck |
| At scale | In-process cache (HybridCache/FusionCache) in front of the query | Only if a *measured*-hot read path appears (marts already cover precompute; see `middle_plan_v2.md` "Layer 7 dissolved") |

The escape hatch (direct JOIN bypassing composition) is the exception, not the default.
WU-L6's measured outcome reinforces the default: every hot path was fixed with DDL alone —
zero service-code changes.
