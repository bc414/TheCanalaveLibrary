# L6 Reconciliation Matrix — First-Principles Needed Indexes vs. Declared Schema

> Companion document: [[L6-intent-ledger]] (reconstructed original design intent — cross-referenced
> here where it matters). This matrix instead starts from **current code** (the 27
> `Server*ReadService.cs` files) as ground truth, and reconciles against the **declared schema**
> (`ApplicationDbContextModelSnapshot.cs` + `20260707133244_L6_IndexBatch.cs`) and the current
> `layer6-indexes.md` claims. No code, migrations, or grid/status edits were made producing this
> report — it is evidence for a later build/measure pass, not the pass itself.
>
> **`pg_indexes` live reconciliation: PENDING.** Everything below compares code-needs against the
> *declared* migration snapshot, not the running database. The WU-L6 lesson (six `user_story_interactions`
> indexes silently collapsed to one because unnamed `HasIndex` calls overwrite each other — the
> config file said one thing, the database said another) means a snapshot-only pass **cannot rule
> out** another such collapse. A `SELECT indexname FROM pg_indexes` sweep against a running DB is a
> separate, later step.

## Verdict legend
- **HAVE** — the exact or functionally-equivalent index is declared and serves the query as analyzed.
- **PK-served** — a primary key (or its column-prefix) already serves the query; no secondary index needed.
- **MISSING** — first-principles analysis finds a real access path with no declared index serving it.
- **WRONG** — an index is declared on the right table but with a column order/set that doesn't match the query's actual filter+sort shape.
- **RESOLVED-N/A** — previously flagged or plausible-sounding, but reasoned not to need an index (documented why).
- **needs-live-check** — the table is raw-SQL/worker-owned (not in the EF snapshot) or the verdict depends on data not available statically.

---

## Comments (Feature 23/24 — chapter/blog/group/profile comment posting & display; TPT)

| query | needed index (first principles) | declared? | verdict | note |
|---|---|---|---|---|
| roots-page (all 4 comment types): `chapter_id`/`blog_post_id`/`group_id`/`profile_user_id` (child) + `parent_comment_id IS NULL` + `date_posted DESC` (base) | child `(fk_col, date_posted)` + base `(parent_comment_id, ...)` | `ix_chapter_comments_chapter_id_date_posted`, `ix_blog_post_comments_blog_post_id_date_posted`, `ix_group_comments_group_id_date_posted`, `ix_user_profile_comments_profile_user_id_date_posted` (child); `ix_base_comments_parent_comment_id` (base) | **HAVE** (chapter measured −98.8% in WU-L6; blog/group/profile inherited the shape, unmeasured) | TPT boundary means no single index spans filter+sort — the child composite + base FK index is the best available shape, matching WU-L6's own TPT note |
| roots+replies OR-of-two-PKs + comment_likes correlated EXISTS | `base_comments(parent_comment_id)`; PK-served on `comment_id`; `comment_likes(comment_id, user_id)` | `ix_base_comments_parent_comment_id`; PK `base_comments.comment_id`; PK `comment_likes(comment_id, user_id)` | **HAVE / PK-served** | `comment_likes`' PK is exactly `(comment_id, user_id)` — the correlated "is this liked by viewer" EXISTS is PK-served, no gap |
| roots-count (all 4 types) | same child+base shape as paging | same as above | **HAVE** | shares the paging index; WU-L6 measured the chapter count 21.0→0.61ms |

**Cross-check against [[L6-intent-ledger]]:** the ledger's chronicle-#8 finding (`DatePosted`
denormalized onto TPT children "to avoid joins," never actually eliminating the base join) matches
what shipped — the composite lives on the child, the `parent_comment_id`/`is_taken_down` filter
still requires a base join. WU-L6's own TPT note already states this correctly; no further gap.

---

## Chapters (Features 6/7 — the two open L6 cells)

| query (`ServerChapterReadService`) | needed index (first principles) | declared? | verdict |
|---|---|---|---|
| `GetChapterForReadingAsync`, `GetChapterTocAsync`, `GetChapterVersionsAsync`, `GetChapterListAsync` — filter `story_id` (+ `is_published`), order `chapter_number` | `chapters(story_id, chapter_number)` | `ix_chapters_story_id_chapter_number` (unique) | **HAVE** |
| version/content ordering — filter `chapter_id`, order `sort_order` | `chapter_contents(chapter_id, sort_order)` | `ix_chapter_contents_chapter_id_sort_order` (unique) | **HAVE** |
| read-state / last-interaction lookup — filter `user_id` + `chapter_id` | `user_chapter_interactions(user_id, chapter_id)` | PK `(user_id, chapter_id)` | **PK-served** |
| `GetChaptersForExportAsync` — filter `story_id, is_published`, order `chapter_number` | `chapters(story_id, is_published, chapter_number)` | only `(story_id, chapter_number)` — `is_published` is a residual filter, not in the key | **RESOLVED-N/A** — per-story chapter counts are small (SeedTool geometric distribution, mean ≈5, cap 20); a residual boolean filter over ≤20 rows doesn't justify widening the unique composite |
| `GetChapterForEditAsync` | PK lookup by `chapter_content_id` | PK | **PK-served** |
| rating `COALESCE(cc.rating, story.rating)` ceiling checks (appear in 5 of 7 methods) | none — expression-based predicate, not indexable as written | n/a | **RESOLVED-N/A** (inherent to the design; not an index gap) |

**Finding: F6/F7's L6 need is already fully served by existing indexes.** WU-L6 explicitly deferred
these two cells ("chapter-read queries were not assessed this pass") — this first-principles pass
finds **no missing index**. F6/F7 L6 can likely move directly to Stage 5 pending only the
verification-band question (pure DDL — no new DDL to measure here, since nothing needs building;
the "measurement" would be confirming the *existing* indexes are hit by `EXPLAIN`, not adding new ones).

---

## Stories / Discovery listings (Features 5, 31, 32, 34, 59, 61 — story browsing, search, discovery)

| query | needed index | declared? | verdict | note |
|---|---|---|---|---|
| `GetRecentListingsAsync`, `GetListingsAsync` (DatePublished/LastUpdated/Random sorts) — filter `rating, is_taken_down`, order `published_date`/`last_updated_date` DESC | `stories(published_date)` / `(last_updated_date)`, residual rating/is_taken_down | `ix_stories_published_date`, `ix_stories_last_updated_date` | **HAVE** (WU-L6 measured −76%, 0.39→0.09ms) |
| `GetListingsAsync` (Relevance sort) — FTS `Matches` predicate | GIN on `story_listings.search_vector` | `ix_story_listing_search_vector` (GIN) | **HAVE** |
| `GetListingsAsync` (RecentlyRead sort) — correlated `MAX(last_interaction_date)` per story across chapters | `user_chapter_interactions(user_id, chapter_id)` INCLUDE `last_interaction_date` | PK `(user_id, chapter_id)` only — `last_interaction_date` not included/indexed | **RESOLVED-N/A** — deliberate per `layer6-indexes.md`'s MVCC section: indexing this hot-updated column would defeat HOT-update eligibility on the highest-churn table; per-user row counts are small. Confirmed consistent, not a gap. |
| tag include/exclude correlated EXISTS | `story_tags(tag_id, story_id)` + `(story_id, tag_id)`; `story_characters(character_tag_id, story_id)` + `(story_id, character_tag_id)` | `story_tags`: PK `(story_id, tag_id)` + `ix_story_tags_tag_id`; `story_characters`: `ix_story_characters_story_id` + `ix_story_characters_character_tag_id` | **HAVE** (both directions present) |
| `ApplyFilters` interaction-exclusion NOT EXISTS | PK `(user_id, story_id)` | PK | **PK-served** |
| **`SearchStoriesByTitleAsync` — `story_title ILIKE '%term%'`** | `pg_trgm` GIN trigram index on `story_listings.story_title` | **none** — only the unrelated `search_vector` GIN exists | **MISSING** | Leading-wildcard ILIKE cannot use a B-tree; this is a live gap on an **already-Stage-5** feature (F31 Search Page / F32 Full-Text Search). Cross-references the ledger's own flag (#307, #1166) that plain/FTS indexes don't cover arbitrary substring match. |
| `ServerCoOccurrenceReadService` (Also Favorited/Recommended) — mart reads + NOT EXISTS on USI | mart covering `(story_id, score DESC, ...)`; `user_story_interactions(user_id, story_id)` | mart indexes are raw SQL owned by the L8 worker (not in EF snapshot); USI is PK-served | **needs-live-check** (mart) / **PK-served** (USI) | current `layer6-indexes.md` asserts these "measured healthy" — outside this pass's scope to re-verify |
| `ServerManualTreeSearchReadService` / `ServerTreeSearchReadService` — mart traversal | `user_story_tree_search_entries` mirrored covering indexes | raw SQL, L8-worker-owned | **needs-live-check** | |
| `ServerManualTreeSearchReadService:GetStoryNeighborsAsync` (favoriters-of-a-story) — filter `story_id, is_favorite, NOT is_hidden_favorite` | `user_story_interactions(story_id, is_favorite, is_hidden_favorite)` incl. `user_id` | **none** — the seven USI partials are all `user_id`-leading (`ON user_id INCLUDE story_id WHERE is_X`); none is `story_id`-leading | **MISSING — see "Rejected-vs-live conflict" below** | |
| same file, recommendation-family neighbor queries — filter `story_id`/`recommender_id, status_id`, order `date_posted DESC` | `recommendations(story_id, status_id, date_posted DESC, recommendation_id)` and `(recommender_id, status_id, date_posted DESC, recommendation_id)` | **none** — only `ix_recommendations_story_id`, `ix_recommendations_status_id`, `ix_recommendations_recommender_id_story_id` (unique, no date) | **MISSING** — see cross-cutting Recommendations section below |

### ⚠ Rejected-vs-live conflict: story-centric USI index

WU-L6's 2026-07-07 R4 audit **rejected** a story-centric mirror of the USI filtered indexes with
the stated reason: *"No story-centric interaction query exists. Story favorite counts are
denormalized (`UserStat.FavoritesOnStories`); the discovery/F59 exclusion probes bind
`(user_id, story_id)` = the PK."* (`layer6-indexes.md`, Rejected table.)

**That premise no longer holds.** `ServerManualTreeSearchReadService.GetStoryNeighborsAsync`'s
"favoriters" branch — built in **WU40, 2026-07-12/13, after the R4 rejection was written** — does
exactly the rejected access pattern: `WHERE story_id = @id AND is_favorite AND NOT is_hidden_favorite`,
paged. This table currently has no `story_id`-leading favorite-filtered index; the query rides a
full `user_story_interactions` scan filtered by three columns with no supporting index.

This is exactly the kind of drift the Doc-Touch Timing rules exist to catch: a Stage-2 cell (F33)
built new code whose data need contradicts a "rejected" note recorded against a *different* feature's
audit file, and nothing flagged it at build time. **This should be resolved explicitly** (build the
story-centric partial index after all, or find another shape) before F33 reaches Stage 5.

---

## Recommendations (Feature 27 — cross-cutting composite gap)

| query | needed index | declared? | verdict |
|---|---|---|---|
| `GetForStoryAsync` — filter `story_id, status_id`, order `is_highlighted_by_author DESC, date_posted DESC` | `recommendations(story_id, status_id, is_highlighted_by_author DESC, date_posted DESC)` | **none** | **MISSING** |
| `GetRecommendedStoryIdsAsync` / `GetHiddenGemStoryIdsAsync` — filter `recommender_id, status_id` (+ `is_hidden_gem`) | `(recommender_id, status_id)` | only the unique `(recommender_id, story_id)` — no `status_id` | **MISSING** (partial credit — recommender_id is at least indexed as the PK-adjacent unique's leading column) |
| `GetByIdAsync`, likes/success gates | PK / composite PK | PK-served throughout | **PK-served** |

**This one composite gap is cross-cutting** — it affects three features simultaneously:
- **F27 Recommendation Display** (already **Stage 5**) — `ServerRecommendationReadService.GetForStoryAsync` is the story-page recommendation list; currently unindexed for its actual filter+sort shape.
- **F33 Manual Tree Search** (Stage 2, open) — the recommendation-family neighbor queries above.
- **F55 Community Spotlight** (already **Stage 5**) — `GetMyPickCandidatesAsync` reuses the recommender-scoped shape.

The [[L6-intent-ledger]] shows this exact composite shape was designed and discussed twice in the
Gemini history (#964, #969) but never implemented as such — the shipped index only got as far as
the plain FK + one unique constraint.

---

## Blog Posts (Feature 35 — one of the five open L6 cells)

| query | needed index | declared? | verdict |
|---|---|---|---|
| `GetByAuthorAsync` — filter `author_id` (base) + `is_published, rating` (child `profile_blog_posts`), order `date_created DESC` | ideal single-table `(is_published, rating, date_created DESC)` on the child, since the composite can't span the TPT join anyway | **none** — only `ix_base_blog_posts_author_id` (base) and `ix_profile_blog_posts_story_id` (child, unrelated column) | **MISSING** |
| `GetByGroupAsync` — filter `group_id, is_published, rating` (child `group_blog_posts`), order `date_created DESC` (base) | `group_blog_posts(group_id, is_published, rating)` | **none** — only `ix_group_blog_posts_group_id` (plain) | **MISSING** |
| `GetByIdAsync` / `GetForEditAsync` | PK `blog_post_id` (TPT base+child both keyed on it) | PK | **PK-served** |
| `blog_post_likes` EXISTS | `(blog_post_id, user_id)` | PK `(blog_post_id, user_id)` | **PK-served** |
| Poll vote/option subqueries (`ServerPollReadService`, shared across site/blog polls) | `poll_options(poll_id, sort_order)`; `poll_votes(poll_option_id, user_id)` | `ix_poll_options_poll_id_sort_order` (unique); PK `poll_votes(poll_option_id, user_id)` | **HAVE / PK-served** |
| `GetPollsForBlogPostAsync` — filter child `blog_post_poll.blog_post_id` | FK index | **none declared** on `blog_post_polls` beyond `ix_blog_post_polls_blog_post_id` which is actually the child's own PK (poll_id), not a `blog_post_id` column index | **needs-live-check** — schema naming is ambiguous from the snapshot alone; flag for the live pass |

**F35's real gap:** both paged blog-post listings (by-author, by-group) filter+sort across the
TPT base/child boundary with no index shaped for either — this is a genuine, previously
undiscovered need, not just an unmeasured-but-correct index.

---

## Groups (Feature 38 — one of the five open L6 cells)

| query | needed index | declared? | verdict |
|---|---|---|---|
| `GetListingsAsync` — global `GroupAudience` filter injects `audience_rating, max_content_rating` into every read; order `date_created DESC` | `groups(audience_rating, date_created DESC)` (or partial) | **none** — only `ix_groups_creator_id`, `ix_groups_group_name` (unique) | **MISSING** |
| `GetMembersAsync` — filter `group_id`, order `date_joined ASC` | `group_members(group_id, date_joined)` | only `ix_group_members_group_id` (plain, no sort column) | **MISSING** (minor — sort-elimination only, filter is already served) |
| `GetCurrentUserRoleAsync`, member-count subqueries — filter `group_id, user_id` | composite PK | PK `(user_id, group_id)` | **PK-served** (full-equality point lookups are PK-served regardless of declared column order) |
| `BuildFolderTreeAsync` — filter `group_id`, order `sort_order` | `group_folders(group_id, sort_order)` | `ix_group_folders_group_id_parent_folder_id_name` (unique) — group_id-leading but ordered by `(parent_folder_id, name)`, not `sort_order` | **WRONG** (partial credit — filter is served, sort is not) |
| per-folder story-id lookup | `group_stories` by folder FK | `ix_group_stories_group_id`, `ix_group_stories_story_id` declared; no distinct folder-FK column visible in the snapshot | **needs-live-check** — depends on whether `GroupStories` carries a `group_folder_id` column at all vs. deriving membership through `GroupFolder` navigation |

**F38's real gap:** the audience-filter composite is the highest-value one — it's injected into
*every* group listing read via the global query filter, so it's the group-cluster equivalent of the
story discovery sort spines.

---

## Notifications (Feature 41/42 — already Stage 5)

| query | needed index | declared? | verdict |
|---|---|---|---|
| unread count, total count, feed (NewestFirst/OldestUnreadFirst) | `(recipient_user_id, is_read, date_created)` | `ix_notifications_recipient_read_date` | **HAVE** (WU-L6 measured; matches ledger's #1527/#1538 ancestor design exactly) |

No gap. Included for completeness — this is the cleanest intent→shipped match in the whole sweep.

---

## Messaging (Feature 49 — already Stage 5, but unmeasured)

| query | needed index | declared? | verdict |
|---|---|---|---|
| thread paging/count — filter `conversation_id`, order `date_sent DESC` | `(conversation_id, date_sent)` | `ix_private_messages_conversation_id_date_sent` | **HAVE, but unmeasured** — SeedTool generates zero message rows; WU-L6 explicitly noted "R4-justified by the query; not measurable yet" |
| inbox listing / unread badge — filter `user_id` (+ `is_archived`) | `conversation_participants(user_id, is_archived)` | only plain `ix_conversation_participants_user_id` | **MISSING** (new candidate — not previously discussed anywhere in the doc or the ledger) |

Per Brian's locked "always measure" decision, **F49 does not currently meet the bar** it was
flipped to Stage 5 under — it needs both a SeedTool message generator and a PerfBaseline scenario
before the Stage-5 claim is earned, plus the new `is_archived` composite above.

---

## Following / Vouches (Features 18/19 — already Stage 5)

| query | needed index | declared? | verdict |
|---|---|---|---|
| outgoing vouches, `isFollowing`/`isVouched` checks, follow list | composite PK (leading column matches) | PK `(vouching_user_id, vouched_user_id)` / `(user_id, followed_user_id)` | **PK-served** |
| **`GetIncomingVouchesAsync`** — filter `vouched_user_id` (the **non-leading** PK column), order `date_vouched ASC` | `vouches(vouched_user_id, date_vouched)` | only `ix_vouches_vouched_user_id` (plain FK-convention index, no `date_vouched`) | **WRONG / partial gap** | Filter is served by the FK-convention index; the sort is not. Cross-references [[L6-intent-ledger]]'s own flag of this exact issue (#1121: "two filtered indexes, one for each direction" — the reverse direction shipped as a plain single-column index, not the sort-serving composite the original design called for). |

---

## Saved Tag Selections (Feature 15 — already Stage 5, but unmeasured)

| query | needed index | declared? | verdict |
|---|---|---|---|
| `GetMySelectionsAsync` (DateCreated sort), `GetPublicSelectionsByUserAsync` | `(user_id, date_created)`, `(user_id, is_public)` | `ix_saved_tag_selections_user_id_date_created`, `ix_saved_tag_selections_user_id_is_public` | **HAVE, but unmeasured** — WU43 built these without a SeedTool generator; no before/after number exists |
| Nickname sort | `(user_id, nickname)` | `ix_saved_tag_selections_user_id_nickname` (unique) | **HAVE, but unmeasured** |
| entries hydration | `(saved_tag_selection_id, tag_id)` | `ix_saved_tag_selection_entries_saved_tag_selection_id_tag_id` (unique) | **HAVE** |

Same "always measure" gap as Messaging above — a real SeedTool generator + PerfBaseline scenario
is needed before this cell's Stage-5 status is fully earned under the locked decision.

---

## Tags (Features 11/12/13/14 — already Stage 5)

| query | needed index | declared? | verdict |
|---|---|---|---|
| `SearchTagChipsAsync` — `tag_name ILIKE '%term%'` (autocomplete) | `pg_trgm` GIN on `tags.tag_name` | **none** | **MISSING** | Both the code derivation and [[L6-intent-ledger]] (#307) independently flag this — a plain B-tree only serves prefix search, not the substring match the code actually performs. |
| `GetTagsByTypeAsync` (+ character/setting/genre/content-warning variants) — filter `tag_type_id`, order `tag_name` | `(tag_type_id, tag_name)` | `ix_tags_tag_name_tag_type_id` (unique, but **`tag_name`-leading**, not `tag_type_id`-leading) + separate `ix_tags_tag_type_id` | **WRONG column order** (partial credit — the type filter is served by the separate single-column index; the composite that would serve filter+sort together has its columns reversed from what the query needs) |

---

## Series & Story Lineage (Features 9/10 — currently N/A / Stage 5)

| query | needed index | declared? | verdict |
|---|---|---|---|
| `GetSeriesByAuthorAsync` — filter `author_id`, order `date_created DESC` | `(author_id, date_created DESC)` | `ix_series_author_id_name` (unique) — **keyed by `name`, not `date_created`** | **WRONG** | F9's L6 column is currently `N/A` in the grid; this finding suggests it should be reconsidered rather than skipped — the declared index doesn't serve the actual sort. |
| `series_entries` reverse lookup (story→series) | `(story_id)` incl. `series_id` | `ix_series_entries_story_id` | **HAVE** |
| `series_entries` ordered membership — filter `series_id`, order `order_index` | `(series_id, order_index)` | only PK `(series_id, story_id)` — no `order_index` | **MISSING** (minor — per-series entry counts are small) |
| `story_lineages` outgoing/incoming — filter `source_story_id`/`target_story_id` + `status_id` | PK prefix (`source_story_id`) / `(target_story_id, status_id)` | PK `(source, target, relationship_type)`; `ix_story_lineages_target_story_id` (plain) | **PK-served (outgoing, partial) / MISSING status_id composite (incoming)** — low priority, lineages are rare per story |

---

## Low-volume / config tables — confirmed no index needed

Cross-checked and found correctly seq-scan-appropriate (no gap): `report_reasons`, `themes`,
`site_settings`, `default_user_story_interaction_filter_settings` / `user_story_interaction_filter_settings`,
`notification_types`/`notification_categories`, `search_modes`, `external_platforms`,
`story_lineage_types`. `community_spotlights` (start/end range) and `spotlight_slots`
(`granted_to_user_id, status`) both already have their needed indexes declared — **HAVE**.

---

## The five open L6 cells — summary verdict

| Feature | Prior status | This pass's finding |
|---|---|---|
| **F6/F7 Chapter Writing/Reading** | Stage 2 ("not assessed") | **No missing index found.** Existing `chapters`/`chapter_contents`/`user_chapter_interactions` indexes already serve every query. Candidate for Stage 5 with no new DDL — only needs the verification-band question answered (what "DDL verified" means when there's nothing new to measure). |
| **F33 Manual Tree Search** | Stage 2 ("pivots ride existing indexes; R4 measurement deferred") | **Two genuine gaps**, one of which directly contradicts a prior R4 rejection (see "Rejected-vs-live conflict" above): a story-centric USI favorite-filter index, and the cross-cutting recommendations composite. Needs both new DDL and a resolution of the R4 conflict before Stage 5. |
| **F35 Blog Post Writing** | Stage 2 | **Two genuine gaps** — TPT-child composites for both by-author and by-group listings. Needs new DDL. |
| **F38 Group Management** | Stage 2 | **Two genuine gaps** — the audience-filter composite (highest value, hits every group listing via the global query filter) and the member-listing sort composite. Needs new DDL. |

## New candidates surfaced (not previously discussed anywhere, including the ledger)

- `conversation_participants(user_id, is_archived)` — Messaging inbox read.
- The cross-cutting `recommendations(story_id/recommender_id, status_id, ..., date_posted DESC)` composite touching F27/F33/F55.
- `pg_trgm` GIN on both `story_listings.story_title` and `tags.tag_name` (two independent ILIKE-substring gaps, same missing-extension root cause).

## Gaps found in already-Stage-5 cells

- **F49 Messaging, F15 Saved Tag Selections** — unmeasured (no SeedTool generator ever existed); fails the "always measure" bar just locked in.
- **F19 Vouches** — incoming-vouches sort not covered by the declared index (filter is; sort isn't).
- **F27 Recommendation Display, F55 Community Spotlight** — share the missing recommendations composite above.
- **F31/F32 Discovery** — `SearchStoriesByTitleAsync`'s ILIKE search has no supporting index at all.
- **F11-F14 Tags** — tag-chip autocomplete ILIKE search has no supporting index; `GetTagsByTypeAsync`'s composite has its columns in the wrong order for the filter+sort it needs to serve.
- **F23/F24 Comments** — blog/group/profile comment goldens were built in the same migration as the measured chapter one, but only the chapter shape was actually measured; the other three inherited the claim by similarity, not by number.

## Not assessed by this pass (explicitly out of scope)

- **`pg_indexes` live reconciliation** against a running database — this is the exact check that
  caught the original six-index collapse, and a snapshot-only pass cannot substitute for it.
- Any code change, migration, SeedTool extension, PerfBaseline scenario, or grid/doc edit — per
  the narrowed scope of this task, these are evidence for a later build/measure phase, not actions
  taken here.
