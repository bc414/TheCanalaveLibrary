# L6 Intent Ledger — Reconstructed Original Index Design Intent

> **Status: access-pattern checklist, NOT authority.** Mined from `GeminiDiscussions/MyActivity
> September to November 2025_filtered.md` (1146 entries, ~76k lines, read in full across 7 slices)
> and the 11 `GeminiDiscussions/*.txt` chronicle files, covering the project's SQL-Server-era design
> phase before the Postgres migration. Every row below is a **historical assertion**, not a current
> requirement. Its only job is to make sure [[L6-reconciliation-matrix]] doesn't miss an access
> path the original designer cared about — the *rationale* is frequently SQL-Server-specific or
> naive and must be re-derived, not trusted, for the Postgres+MVCC system this project actually
> runs on. Where a listed index or its stated reasoning is stale, that is called out explicitly
> per-row and again in "Stale principles" below.
>
> Companion document: [[L6-reconciliation-matrix]] (first-principles current-code needs,
> reconciled against this ledger + the declared schema).

## How to read this ledger

Columns: **entity/table** (current or historical name), **index / access pattern discussed**,
**stated rationale** (quoted), **era-tech** (SQLServer / Postgres / agnostic — which engine the
reasoning assumes), **naive-or-stale?** (Y/N + why — Y means the *reasoning*, not necessarily the
*index*, doesn't survive under Postgres+MVCC), **source** (MyActivity entry # or chronicle #).

Entries are grouped by entity in the rough order the schema evolved (SQL-Server single-column FK
era → composite/filtered era → Postgres re-derivation era). Many rows are superseded by later rows
on the same entity — later entries in the conversation history reflect closer-to-final intent.

---

## Stories

| index / access pattern | stated rationale | era-tech | naive-or-stale? | source |
|---|---|---|---|---|
| `IX_Stories_AuthorID` (nonclustered, single-column) | FK lookup: author's stories / author profile page | SQLServer | Partly — `NONCLUSTERED` keyword meaningless under Postgres (no clustered/non-clustered split); intent (author-scoped listing) survives | #1538, #2038 |
| `IX_Stories_Slug` UNIQUE NONCLUSTERED `WHERE Slug IS NOT NULL` (filtered) | fast `/story/{slug}` lookup; filtered so multiple NULLs don't violate uniqueness | SQLServer | Y (partial) — Postgres treats NULLs as distinct by default, so the filter is often unnecessary; ported anyway as `ix_story_details_slug` (filtered unique) — matches declared schema | #1339, #1219 |
| index on `StoryStatusID` (tinyint FK to lookup table) | "most important read query" filters on status | agnostic (SQLServer tinyint framing) | Partly — low-cardinality filter; a partial index may beat a plain one for hot statuses only | #1350, #1354 |
| covering `IX_Stories_AuthorID_Covering ON (AuthorID) INCLUDE (StoryTitle)` | answer author-listing query from index alone, no heap touch | SQLServer (`INCLUDE`) | Y (partial) — `INCLUDE` ports to Postgres 11+, but index-only scan requires the visibility map to be current (recent VACUUM); "never touch main table" overstates it under MVCC | #1252, #1244, #1226 |
| niche filtered `(StoryID DESC) INCLUDE(StoryTitle) WHERE PublishedDate > X` for "recent" hot subset | pre-sorted/pre-filtered subset eliminates the Sort | SQLServer | Y — "can't filter a clustered index/PK" reasoning is SQL-Server-specific; Postgres has no clustered index at all, so the whole framing doesn't transfer | #1224 |
| composite search indexes: `(rating, story_status_id, last_updated_date DESC)`, `(rating, story_status_id, view_count DESC)`, `(author_id, last_updated_date DESC)` | "carefully crafted composite indexes that match your UI" (discovery sort spines) | Postgres (snake_case) | N — this is the direct ancestor of the shipped `ix_stories_published_date` / `ix_stories_last_updated_date` (WU-L6 kept them single-column + residual filters rather than composite; see reconciliation) | #883, #861, #894 |
| covering `(Rating, LastUpdatedDate DESC) INCLUDE(StoryTitle, CoverArtRelativeUrl, ShortDescription, AuthorId)` — later **reversed** in favor of a 3-table hot/warm/cold partition (Story / StoryListing / StoryDetail) | "never has to touch the main table" | SQLServer-tinted | Y — self-superseded within the same conversation (#877/#892 override #900); covering-only-scan claim weaker under Postgres MVCC regardless | #900, #877, #892 |
| "golden index" `(Rating, LastUpdatedDate DESC)` turns O(n log n) sort into O(log n) seek | eliminates in-memory SORT | agnostic | N — this reasoning is the direct conceptual ancestor of the WU-L6 comment "golden index" naming | #874 |
| `FavoriteCount` denormalized column + triggers | live favorite count "would require a very slow query with multiple JOINs" | SQLServer | Y — trigger-maintained hot counters cause row contention/bloat under Postgres MVCC; project instead denormalizes via `UserStat.FavoritesOnStories` read-mostly, not trigger-maintained | #1424 |
| `IsHiddenGem BIT` site-wide flag on Stories | admin-curated flag as a column | SQLServer | N — superseded; hidden-gem is a per-(user,story) `Recommendation` attribute, not a Stories column, in the shipped schema | #1400 |
| Elasticsearch/Azure AI Search offload for advanced search; SQL indexes deliberately NOT expected to carry composite/multi-filter search | "a SQL index... helps you find a specific row quickly" vs ES's separate inverted index | SQLServer-era assumption | **Y — major stale principle.** The original design deliberately under-invested in composite/covering search indexes, assuming an external search engine would carry advanced filtering. The shipped system instead uses **Postgres GIN full-text (`tsvector`/`ix_story_listing_search_vector`)** and correlated-EXISTS tag/character filters directly in SQL — no external search engine exists. Every "search will be slow, offload it" assertion from this era is void. | #2095, #2090 |
| `LIKE '%term%'` cannot use a B-tree; use SQL Server FULLTEXT/`FREETEXT` | leading wildcard forces full scan | SQLServer (FULLTEXT/FREETEXT engine) | Y — Postgres has no FULLTEXT catalog; the shipped answer is `tsvector` + GIN (`story_listings.search_vector`) for metadata, and `pg_trgm` GIN would be needed for arbitrary substring/tag-chip search (not yet built — see reconciliation) | #1166, #1165, #1164, #1105 |
| dedicated `StorySearchData` table with `TagIds INT[]` / `CharacterIds INT[]` + GIN, `WHERE tag_ids @> ARRAY[1,5]` | "GIN index... blazing fast... arrays unique to PostgreSQL" | Postgres-specific | N — a genuinely Postgres-native idea that was **never built**; shipped design uses correlated EXISTS against `story_tags`/`story_characters` junctions instead. Worth flagging as a rejected-but-not-measured alternative. | #901, #861 |
| `datetime2(2)` precision (6 bytes) chosen to save RAM/index size | "~7.45 GB less RAM to hold that index in memory" | SQLServer | **Y — fully abandoned, and the conversation itself corrects it** (#990): Postgres `timestamptz` is a fixed 8 bytes regardless of declared precision — no storage benefit exists. This got removed from the codebase per chronicle #8. | #1140-1146, corrected at #990 |

## Chapters / ChapterContents

| index / access pattern | stated rationale | era-tech | naive-or-stale? | source |
|---|---|---|---|---|
| `Chapters` unique composite `(StoryId, ChapterNumber)` | "already creates an index... perfect for quickly finding all chapters for a story and ordering them"; "a story cannot have two chapters with the same number" | SQLServer/agnostic → Postgres | N — sound in both eras; **matches the shipped `ix_chapters_story_id_chapter_number`** unique index exactly | #2038, #824 |
| `ChapterContents` unique composite `(ChapterId, SortOrder)` | uniqueness via O(log n) index seek vs O(n) scan; concern about index cost on unbounded-text rows resolved by TOAST | Postgres (TOAST named explicitly) | N — correctly reasons that large chapter text is TOASTed out-of-row so indexing this composite is cheap; **matches shipped `ix_chapter_contents_chapter_id_sort_order`** | #819 |
| never index the HTML chapter body column; keep separate stripped-text vs HTML columns if FTS is ever wanted | "index will become polluted with useless noise words (strong, em, p...)"; "storage and I/O cost will be catastrophic" | agnostic | N — correctly anticipated the shipped decision to exclude chapter body from `story_listings.search_vector` (title + short description only) | #1163, #853, #852 |
| `IX_UserChapterInteractions_ChapterID` reverse index on composite-PK 2nd column | find "all users who read a chapter" for stats | SQLServer | N — sound reverse-lookup reasoning; **matches shipped `ix_user_chapter_interactions_chapter_id`** | #1538 |
| split `ReadProgress` double into `IsRead bool` + `float ReadProgress`; filtered index `WHERE IsRead=1` for "Continue Reading" (`WHERE UserID AND IsRead=0 ORDER BY LastInteractionDate DESC`) | indexing a double is "notoriously bad for exact match"; filtered index stays tiny | SQLServer | Partly — index-on-double concern is sound; but the shipped system took a different path entirely: `user_chapter_interactions` hot-updated columns (`read_progress`, `last_interaction_date`) are **deliberately left unindexed** to preserve HOT-update eligibility under Postgres MVCC (see `layer6-indexes.md` MVCC section) — the filtered-index idea was superseded by a no-index-at-all strategy once Postgres HOT semantics were understood | #1148, #1147 |
| `LastReadDate` high-frequency timestamp → Redis hash, not SQL; `IsInProgress` stays SQL-indexed | "hammering that row with UPDATEs... massive database locking" | SQLServer+Redis-era plan | Y (partial) — the locking rationale is SQL-Server-flavored (Postgres MVCC readers/writers don't block each other), but the *volatility* concern was valid and evolved into the shipped **L2 in-process signal buffer** (not Redis — L7 was dissolved 2026-07-06) for reading-progress, per `layer2-services.md` §"Signal Buffering" | #1007 |

## Comments (BaseComment / TPT children)

| index / access pattern | stated rationale | era-tech | naive-or-stale? | source |
|---|---|---|---|---|
| pre-TPT single `Comments` table: `IX_Comments_ParentEntity (ParentEntityType, ParentEntityID)`, `IX_Comments_UserID`, `IX_Comments_ChapterID`/`StoryID` | polymorphic parent lookup + FK indexes; "read from constantly... indexes are not optional" | SQLServer | Y (structurally) — this entire single-table polymorphic design was **later replaced by TPT** (`base_comments` + `chapter_comments`/`blog_post_comments`/`group_comments`/`user_profile_comments`); the `(ParentEntityType, ParentEntityID)` composite has no TPT equivalent — chronicle #10 records the polymorphic-parent index as *dangling* (referenced columns "no longer existed") during the refactor | #1538, #1465, chronicle #10 |
| denormalize `DatePosted` onto each TPT child table rather than base, "to improve query performance by avoiding joins" | avoid a join for date-sorted paging | SQLServer-era / agnostic | Y — **naive per chronicle #8's own retrospective**: TPT still joins for every other base column (`parent_comment_id`, `is_taken_down`, `user_id`), so denormalizing only the date column is a marginal, unmeasured win. This is exactly the shape the WU-L6 "golden index" work later formalized correctly: composite `(chapter_id, date_posted)` on the child + a base-side `parent_comment_id` filter, joined — TPT boundary acknowledged rather than wished away. | chronicle #8 |
| golden index `(chapter_id, date_posted DESC)` to eliminate SORT during pagination ("trade one giant slow SORT for 20 tiny fast key lookups") | pagination without an in-memory sort | agnostic (some examples shown in SQL-Server `NONCLUSTERED...INCLUDE` syntax) | Partly — direct conceptual ancestor of the shipped comment goldens (`ix_chapter_comments_chapter_id_date_posted` et al.); syntax examples are SQL-Server but the shape ported cleanly and was independently re-measured in WU-L6 (−98.8%) | #911, #924, #921–919 |
| `CommentLikes` — index for "most likes in last 24h" trending sort | surface hot comments | SQLServer | N (concept), but **later rejected on product-scope grounds within the same conversation** — no "trending comments" feature shipped | #1074 |

## user_story_interactions (USI)

| index / access pattern | stated rationale | era-tech | naive-or-stale? | source |
|---|---|---|---|---|
| avoid storing a row for every (user, story) pair including "Viewed" — table would reach ~1,000,000 rows and slow queries | table-size concern | SQLServer/agnostic | N — valid concern; shipped design only materializes a row when at least one flag is set (implicit from PK sparsity) | #1515 |
| bit-packed `FavoriteStatus`/`ReadStatus` tinyint vs separate bools, framed around "billions of rows (users × stories)" | "index on a 1-byte integer is smallest/fastest"; varchar much larger/slower | SQLServer | **Y — significantly naive.** "Billions of rows" wildly overestimates a fanfiction site's realistic scale (SeedTool's own extended dataset is 2k users × 3k stories ≈ 6M max pairs, and real occupancy is far sparser). The tinyint-vs-bool micro-optimization is immaterial at this scale. | #1356, #1355 |
| SQL-Server bit-packing: N booleans pack into 1 byte, so "8 filtered covered indexes ≈ 8 separate tables but shared storage" | row stays narrow regardless of bool count | SQLServer | **Y — explicitly corrected within the same conversation** (#991, #993): Postgres has no bit-packing; each `bool` column costs its own byte (with alignment padding), so the row-size math differs. The conversation's own later entry states "the SQL-Server-specific bit packing optimization is gone... it's a tie, so clarity wins" — i.e. use plain bools, which is what shipped. | #1040, #1039, #1037, corrected at #991/#993 |
| 7 separate filtered+covering indexes, one per boolean flag: `HasIndex(UserId).IncludeProperties(StoryId).HasFilter("\"is_X\"=true")` | "this table will have MANY filtered indexes on the boolean flags" | Postgres (snake_case, quoted identifiers) | N — **this is the direct design ancestor of the shipped seven USI partial indexes**, and also foreshadows the exact WU-L6 failure mode: the conversation already flags the risk that multiple unnamed filtered indexes on the same columns are easy to collide, which is precisely what happened (six of seven silently collapsed to one until 2026-07-07) | #916, #931 |
| combined index `WHERE IsFavorite=1 OR IsPrivateFavorite=1` for an "All Favorites" view; reverse `ON(StoryID) INCLUDE(UserID)` for per-story favorite counts | serve a combined-flag query and a story-centric count | SQLServer | Partly — the combined-OR index was never built (no "All Favorites across favorite+hidden" query exists in the shipped read service); the reverse story-centric index was **explicitly rejected in WU-L6's R4 audit** ("no story-centric interaction query exists... favorite counts are denormalized via UserStat") — same conclusion, arrived at twice independently | #1030 |
| `UserStoryInteractionDates` — separate sparse table with filtered date indexes (`WHERE FavoriteDate IS NOT NULL`) for date-sorted lists | keep the hot flag table narrow; filtered index stays small | SQLServer | Y — **superseded entirely**: the shipped `user_story_interaction_dates` table exists but WU-L6's audit found **the table is never read** — "the profile favorites tab returns an unordered id list; no query touches favorite_date." The original access pattern (date-sorted favorites) was never actually built into a UI feature. | #1042, #1011, #1010; rejection confirmed in current `layer6-indexes.md` |
| `SourceRecommendationId` moved to a sparse 1-to-1 table to keep the hot table narrow | nullable FK still occupies bytes even when NULL | SQLServer (byte/page-math framing) | Partly stale reasoning (8KB-page/rows-per-page math is SQL-Server-specific) but the sparse-column instinct is sound; shipped schema keeps `recommendation_id` inline on `user_story_interactions` with its own FK index (`ix_user_story_interactions_recommendation_id`) rather than splitting it out | #1046 |
| shared composite PK `(UserId,StoryId)` reused across Interactions/Dates/Source tables "physically sorted in the exact same order... merge join... fastest join possible" | co-locate related sparse tables for a merge join | SQLServer | Y — merge-join-via-clustered-index reasoning is SQL-Server-specific; Postgres tables are heaps with no clustered order, so no merge-join guarantee exists regardless of shared PK shape | #1044 |

## Recommendations

| index / access pattern | stated rationale | era-tech | naive-or-stale? | source |
|---|---|---|---|---|
| `IX_Recommendations_StoryID` | story's recommendations tab | SQLServer/agnostic | N — matches shipped `ix_recommendations_story_id` | #1538, #2035-area |
| composite `(StoryId, DatePosted)` to serve WHERE + ORDER BY together | "a single composite index can't be used to sort by a different column" so pick per-sort composites | agnostic | N — sound reasoning; foreshadows the shipped `ix_recommendations_recommender_id_story_id` unique + the reconciliation-surfaced need for a `(story_id, status_id, is_highlighted_by_author DESC, date_posted DESC)` composite (not yet built — see reconciliation) | #969, #968 |
| `(RecommenderId, DatePosted) WHERE recommender_id IS NOT NULL` | recommender is nullable (anonymized recs) | Postgres (snake_case filter) | N — matches the shipped anonymized-recommendation design (AD4: null recommender contributes no edge) | #969 |
| 3-column composite `(StoryId, IsHighlightedByAuthor, LikeCount/DatePosted)` to match an author-highlighted-first sort exactly | "composite index that exactly matches your ORDER BY clause" | agnostic | N — this is exactly the shape the reconciliation matrix flags as currently MISSING for `ServerRecommendationReadService.GetForStoryAsync` | #964 |
| unique `(RecommenderId, StoryId)` + `IX_Recommendations_StoryID` | prevent duplicate recs from the same user, one per pair | agnostic | N — matches shipped `ix_recommendations_recommender_id_story_id` unique | #1214 |

## Notifications

| index / access pattern | stated rationale | era-tech | naive-or-stale? | source |
|---|---|---|---|---|
| composite `(RecipientUserID, IsRead, DateCreated DESC)` for the notification bell (unread newest-first) | "index already sorted... eliminates the Sort operation" | SQLServer/agnostic | N — direct ancestor of the shipped `ix_notifications_recipient_read_date`; WU-L6 measured it (−47% unread count) | #1527/schema, #1248, #1179, #1538 |
| live `COUNT(*) WHERE RecipientUserID AND IsRead=0` instead of a denormalized counter, "extremely fast due to your database index" | avoid a data-integrity-risking counter column | agnostic (index shape unspecified in this recap) | Y (partial) — "extremely fast" assumes near-index-only access; under Postgres MVCC a COUNT still performs heap visibility checks per matched row. What's actually wanted is a **partial** index `(recipient_user_id) WHERE is_read=false`, narrower than the composite that shipped — worth noting as a possible refinement, not a correction, since the composite already serves this acceptably (WU-L6 measured 0.09ms) | #660 |

## Messaging (PrivateMessages / Conversations)

| index / access pattern | stated rationale | era-tech | naive-or-stale? | source |
|---|---|---|---|---|
| composite `(ConversationID, DateSent DESC)` ("covering index") | load all messages in a conversation pre-sorted by date | SQLServer/agnostic | N — direct ancestor of shipped `ix_private_messages_conversation_id_date_sent`; the WU-L6 doc notes this is unmeasured (SeedTool generates no messages) — the reconciliation flags this as the highest-priority "always measure" gap per Brian's locked decision | #1538, #1527/schema |

## StoryTags / Tags

| index / access pattern | stated rationale | era-tech | naive-or-stale? | source |
|---|---|---|---|---|
| index on `TagID` alone to find stories for a tag | "quickly find all stories associated with a specific tag" | agnostic | Partly — TagID-only index (matches shipped `ix_story_tags_tag_id`) serves single-tag lookup, but doesn't consider multi-tag AND filtering (a real fanfic access pattern) — the composite-PK `(story_id, tag_id)` plus this FK index together cover both directions in the shipped design | #2036 |
| reverse composite `(TagId, StoryId) INCLUDE(Priority)` covering index; "table basically exists in both forms" | full symmetric coverage of both lookup directions | SQLServer (`INCLUDE`, "physical sort order" framing) | Y — **directly tested and rejected in WU-L6's R4 audit**: "tag probes are correlated EXISTS on `(story_id, tag_id)` = the PK... no probe reads priority... measured: tag filter unchanged (+7% noise) — the PK was already optimal." The original "double the storage for both directions" intent was empirically unnecessary once the access pattern (correlated EXISTS, not a value scan) was understood. | #959, #958, #956, #955 |
| `Tags.ParentTagID` filtered `WHERE ParentTagID IS NOT NULL` for tag-hierarchy browsing | "filtered index to find sub-tags of a parent" | SQLServer (filtered) | Partly — ported as plain (non-filtered) `ix_tags_parent_tag_id`; the filter's marginal value depends on what fraction of tags are root-level (unmeasured either way) | #1538, #1225 |
| `TagName` autocomplete via a plain index — "milliseconds fast" | server-side type-ahead filtering | agnostic | **Y (potential) — flagged as a live gap.** A plain B-tree on `tag_name` only accelerates prefix (`LIKE 'x%'`) matches. The shipped `ServerTagReadService.SearchTagChipsAsync` does `tag_name ILIKE '%term%'` (substring, leading wildcard) — this needs `pg_trgm` GIN, not a plain B-tree, confirmed independently by the Phase-1 code derivation. | #307 |
| dedicated `Cache_TagHierarchy` closure table `(AncestorTagId, DescendantTagId)` to avoid recursive queries | pre-computed ancestor/descendant pairs | agnostic | N — **explicitly rejected within the same conversation** once tags were confirmed one-level-deep: "a simple, fast, indexed SELECT `WHERE ParentTagId=5`... doesn't need a complex pre-calculated cache." Matches the shipped (non-hierarchical-cache) design. | #1071, #1070, #1067 |

## Groups

| index / access pattern | stated rationale | era-tech | naive-or-stale? | source |
|---|---|---|---|---|
| unique index on `GroupName` | enforce group name uniqueness (scaffold artifact) | SQLServer (attribute-based) | N — matches shipped `ix_groups_group_name` unique | #1630 |
| `GroupStory` unique `(GroupId, StoryId)` + missing reverse index on `StoryId` ("phone book problem") | group→stories fast; story→groups needed a second index | SQLServer | N — matches shipped `ix_group_stories_group_id` + `ix_group_stories_story_id` (both directions present) | #1119 |
| merged folder table vs separate `GroupFolders` join — "all stories in a group" should be a fast seek on GroupId, not a forced join through folders | avoid a join tax on the group's most common query | SQLServer | N — the reconciliation confirms the shipped design still forces this join (`BuildFolderTreeAsync`) for the folder-tree view specifically, though a direct group→story index exists for the flat case | #1118 |

## Following / Vouches

| index / access pattern | stated rationale | era-tech | naive-or-stale? | source |
|---|---|---|---|---|
| `FollowedUsers.IsVouched` bit + two filtered indexes (one per direction) to enforce "at most 5" | direction-specific lookups; "bit-packed into the same byte" | SQLServer | Y (partial) — bit-packing rationale is SQL-Server-only (void under Postgres); the *two-directions* insight is real and independently surfaces in Phase 1: incoming vouches filter on `vouched_user_id`, the **non-leading** PK column, so the composite PK does NOT serve it — a genuine live gap the reconciliation flags | #1121 |

## Tree Search / Discovery Marts

| index / access pattern | stated rationale | era-tech | naive-or-stale? | source |
|---|---|---|---|---|
| `UserStoryTreeSearchEntry` — mirrored filtered indexes both directions (User→Story, Story→User) per public edge type, daily-rebuilt | "you need a mirrored set of filtered indexes for both access patterns" | SQLServer/Postgres (snake_case examples shown) | N — direct ancestor of the shipped `user_story_tree_search_entries` mart covering indexes (raw SQL, owned by the L8 worker per current `layer6-indexes.md`) | #916, #931, #1029, #1015 |
| `RelatedFavoritesCache`/`Cache_RelatedFavorite`: `(SourceStoryID) INCLUDE(RecommendedStoryID, Score)`, store BOTH (A,B) and (B,A) directions, PK `(StoryId, RelatedStoryId)` | one-directional covering read; storing both directions avoids a UNION at read time | SQLServer/agnostic | N — direct ancestor of the shipped `also_favorited_scores`/`also_recommended_scores` mart tables and their covering indexes (also raw SQL, L8-worker-owned) | #1388, #1365, #1071, #1069 |
| tree search powered ONLY by `IsPublicFavorite` edges, never private, via a daily-rebuilt (not live) table — the time delay deliberately breaks a differential-attack link | privacy: prevent inferring a private favorite from before/after graph diffs | agnostic | N — this privacy reasoning survives unchanged and matches the shipped consent-gated hidden-favorite design | #1020, #1026, #1028 |
| recursive CTE traversal unifying edges via `UNION ALL`, "fast indexed UNIONs... highly optimized recursive CTE" | graph traversal across favorite/authored/recommended edges | SQLServer (T-SQL CTE) | Y — Postgres needs `WITH RECURSIVE` (different syntax, similar semantics); graph traversal at real depth is inherently heavy in both engines regardless of indexing | #1399 |

## SavedTagSelections

| index / access pattern | stated rationale | era-tech | naive-or-stale? | source |
|---|---|---|---|---|
| unique `(UserId, Nickname)`; unique `(SavedTagSelectionId, TagId)` | "a user cannot have two selections with the same name" | Postgres | N — matches shipped `ix_saved_tag_selections_user_id_nickname` unique + `ix_saved_tag_selection_entries_saved_tag_selection_id_tag_id` unique | #841 |

## General / cross-cutting principles

| principle | stated rationale | era-tech | naive-or-stale? | source |
|---|---|---|---|---|
| PK is auto-indexed; FK is **not** auto-indexed by default and needs an explicit index for joins + referential-integrity checks | "most database systems (including SQL Server) do not automatically create an index on a Foreign Key" | SQLServer (PK-as-clustered-index framing) | Partly — the FK-not-auto-indexed rule holds in Postgres too (and EF Core's convention now auto-creates these); the "PK is a clustered index" half is SQL-Server-only — Postgres PKs are ordinary non-clustered B-trees over a heap | #1405, #1404 |
| "index every foreign key" is a naive default; index design should be query-first (JOIN / WHERE / ORDER BY shape), not column-first | "'index every foreign key' rule is a default guess that is almost always wrong" | agnostic | N — this is the single most durable principle in the whole history and is exactly the standard the reconciliation matrix applies | #963, #953-950 |
| composite index `(B, C)` beats two separate single-column indexes when both columns are used together | avoids index-merge/bitmap-AND overhead; a single composite serves filter+sort together | agnostic | N — sound, still the operative principle in `layer6-indexes.md`'s existing golden-index sections | #960 |
| covering (`INCLUDE`) index lets the planner skip the heap entirely | "gets the result instantly without ever touching the main table" | SQLServer, later corrected for Postgres (#961 acknowledges `ctid` vs clustered PK) | Y (partial, self-corrected) — Postgres index-only scans require the **visibility map** to be current; the conversation itself later distinguishes SQL-Server's clustered-PK model from Postgres's `ctid`-based heap, but repeats the "never touch the table" claim uncritically elsewhere | #1402, #962, #961, #982, #979 |
| `HasDatabaseName`/explicit naming recommended for filtered indexes to avoid unreadable auto-names and migration churn | readability + stability | Postgres/EF Core | N — this is the exact rule WU-L6 re-learned the hard way as **load-bearing, not optional**, after six unnamed indexes silently collapsed to one | #915 |
| B+Tree leaf-linked structure; "PK is the clustered index, leaf nodes hold the actual row data" | range queries follow leaf links; O(log n) lookups | SQLServer | **Y — the single most repeated stale assumption across the whole history.** Postgres has no clustered index concept: tables are unordered heaps, every index (including the PK) is a secondary B-tree pointing at heap tuples via `ctid`. Any original reasoning that assumed "the PK lookup already has the whole row, no second lookup needed" (index designs at #1214, #1224, #1220, #956, #1006, #1044) should be re-examined — Postgres always does a heap fetch after a B-tree descent unless the query is a true index-only scan with a fresh visibility map. | #1404, #1250, #1244, #1243, #1255 |
| int/enum FK to a lookup table beats a string column for filter/comparison speed and storage | "comparing simple integers is extremely fast"; "reduced storage space" | SQLServer-origin / agnostic | Partly Y — at this project's realistic row counts (thousands, not millions/billions), the int-vs-string storage/speed argument is overstated; the *design* (int FK + lookup table, join only for display) is sound and shipped, but the perfor­mance justification for it was inflated | chronicle #2 |
| `timestamp` column precision configured for storage savings (carried over from SQL Server `datetime2(2)`) | space-saving optimization | SQLServer → Postgres (later corrected) | **Y — explicitly abandoned.** "PostgreSQL's timestamp data type uses a fixed 8 bytes... regardless of precision, so the configuration was no longer providing its original space-saving benefit." Removed from the codebase. | chronicle #8, confirmed at MyActivity #990 |
| dangling/stale index definitions found during schema evolution: a `BaseComments` polymorphic-parent index referencing removed columns; a `Reports` index referencing a `Status` column later renamed to `ReportStatusID` | index definitions drift from the schema during refactors if not swept | SQLServer-era | Y — direct evidence that index-vs-schema drift is a **recurring failure mode** for this project specifically, not a one-off. This is the same failure class (declared-vs-actual mismatch) as the WU-L6 six-index collapse, just caught earlier and by a different mechanism (compile/migration failure vs. silent runtime gap). | chronicle #10 |

---

## Stale principles (cross-cutting summary)

These are the load-bearing assumptions from the SQL-Server design era that are **void or
materially weakened** under Postgres + MVCC. Any index decision in [[L6-reconciliation-matrix]]
that traces its rationale to one of these should be re-derived from the current engine, not
inherited:

1. **Elasticsearch/external-search offload assumption.** The original schema deliberately
   under-invested in composite/covering search indexes on the theory that an external search
   engine (Elasticsearch or Azure AI Search) would carry advanced multi-filter search. No such
   engine exists in the shipped system — Postgres GIN full-text (`tsvector`) and correlated-EXISTS
   tag filtering carry that load directly in SQL. This is the single biggest intent-vs-shipped gap.
2. **Clustered-index / "PK leaf holds the row" model.** Repeated across dozens of entries. Postgres
   has no clustered index; every table is an unordered heap and every index (PK included) is a
   secondary B-tree resolving to a heap tuple via `ctid`. Reasoning built on "the PK lookup already
   has everything" or "rows are physically sorted by PK" does not hold.
3. **SQL-Server bit-packing of boolean columns.** Corrected within the source conversation itself
   (#991/#993) — Postgres has no sub-byte packing; each `bool` costs its own byte. The seven USI
   partial indexes shipped as plain bools, matching the corrected guidance, not the original.
4. **`timestamp(2)` precision as a storage optimization.** Abandoned — Postgres `timestamptz` is a
   fixed 8 bytes regardless of declared precision. Confirmed removed from the codebase.
5. **"Filtered index" terminology / `INCLUDE` syntax assumed SQL-Server-native.** Ports cleanly to
   Postgres as a **partial index** (`WHERE` predicate) and `.IncludeProperties()` respectively —
   translatable, but any claim of "this SQL Server feature doesn't exist in Postgres" in older
   entries is simply wrong; the concepts are first-class in Postgres 11+.
6. **"Index-only / covering scan never touches the table" as an unconditional guarantee.** Depends
   on Postgres's per-page visibility map being current (recent `VACUUM`/autovacuum); on a
   high-churn table this guarantee silently degrades to a heap fetch. `layer6-indexes.md`'s
   existing MVCC section already correctly reasons about this — treat that section, not the
   Gemini-era claims, as authoritative here.
7. **"Billions of rows" / massive-scale framing for `user_story_interactions` and similar tables.**
   Realistic scale for this project (SeedTool's own extended dataset: 2k users, 3k stories) is
   orders of magnitude smaller than several entries assumed, which inflated the perceived urgency
   of micro-optimizations (tinyint vs. bool, byte-packing) that don't matter at actual scale.
8. **Index-definition drift from schema refactors.** Chronicle #10 shows this happening twice
   during the SQL-Server phase (dangling `BaseComments` polymorphic index, renamed `Reports.Status`
   column) — independent evidence that this project's schema evolves faster than its index
   definitions get re-audited, the same root cause as the 2026-07-07 USI six-index collapse.
