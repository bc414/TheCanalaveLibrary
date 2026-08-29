# Database Schema — First-Principles Foundation Audit (2026-08-02)

> **Status: evidence document, not authority.** Companion to [[L6-intent-ledger]] and
> [[L6-reconciliation-matrix]], which own the index layer; this document owns the **logical
> schema** (tables, columns, types, constraints, delete graph, integrity model). Method: the full
> physical DDL was regenerated from the migration chain (`dotnet ef migrations script`, 98 tables /
> 142 indexes as of migration `20260731155929_DropUserCustomFilter`) and reconciled against three
> sources — spec §4 (Database Schema), the shipped EF model + configurations, and external
> first-principles reasoning about what a greenfield Postgres foundation for this site ought to be.
> No code, migration, or grid edit was made producing this report. Findings feed later work-units;
> each finding cites its evidence so it can be re-verified before acting.
>
> **Why now:** no human has used the site; there is no production data. Every structural change is
> currently free — a column widening, a PK reshape, or an FK addition is a migration against empty
> tables. The moment human testing begins, that stops being true. This audit's job is to separate
> (1) genuine defects to fix while free, (2) decisions to settle deliberately before lock-in,
> (3) strengths to ratify so later sessions don't "fix" them, and (4) known-open items already
> tracked elsewhere.

---

## 0. Verdict summary

**The foundation is fundamentally sound.** The schema's load-bearing decisions — hot/warm/cold
vertical partitioning, the sparse boolean interaction model, TPT with denormalized sort columns,
`timestamptz` everywhere, explicit delete behaviors reasoned about in one place, jsonb complex-type
settings, MVCC-aware storage tuning, and append-only stat tables outside the EF model — survive
first-principles re-derivation. Most of what the SQL-Server-era design got wrong was already caught
and corrected during the Postgres migration (documented in [[L6-intent-ledger]] §"Stale
principles").

What remains falls into a short list of genuine defects (§2 — one phantom column on the hottest
table, a systemic TPT-cascade orphan hazard with two unguarded live code paths, one polymorphic-id
width mismatch, one missing FK, one anonymization-policy contradiction, zero CHECK constraints) and
a longer list of decisions that are cheap now and expensive later (§3 — case-insensitive
uniqueness, FTS language config, optimistic concurrency, a write-only table, counter
reconcilability). The index layer's open gaps are [[L6-reconciliation-matrix]]'s; §5 re-confirms
them against the 2026-08-02 DDL without re-deriving them.

---

## 1. What the foundation gets right (ratified — do not "fix")

These are not merely acceptable; on re-derivation from first principles they are the *correct*
choices for this workload, and several are better than what the spec originally described.

1. **Vertical partitioning by access temperature** (`stories`/`story_listings`/`story_details`;
   `recommendations`/`recommendation_details`; `AspNetUsers`/`user_profiles`;
   `user_story_interactions`/`_dates`/`_sources`). First-principles check: Postgres TOASTs
   oversized values out of row anyway, so the naive objection is "partitioning is redundant." It
   isn't here — TOAST only helps values >~2KB; a 500-char `short_description` and a 255-char title
   stay in-row and would fatten every discovery scan. Splitting the *listing projection* into its
   own row keeps the filter table (~70B rows) dense in shared_buffers. The RAM-budget arithmetic in
   spec §4.4 holds up.

2. **The sparse boolean interaction model** (`user_story_interactions`: no row = all false; seven
   named partial covering indexes, one per flag). The 8-state truth table with zero coupling rules
   is a genuinely good piece of domain modeling — `Has-`/`Is-` prefix semantics carry real meaning
   (permanent event vs. mutable state), and the "derived states are computed, never stored" rule
   (`ActivelyReading`) prevents the stale-flag class of bug outright. The 2026-07-07 six-index
   collapse lesson is now encoded in the config file itself.

3. **TPT for comments/blog posts/polls, with per-child `date_posted`.** The NOT-NULL child FK is
   the point; the denormalized date enables the measured golden indexes (−98.8% on chapter comment
   paging). The known cost — hard deletes must traverse both tables — is real and produces finding
   §2.2, but the *choice* is right; TPH would trade a compile-time guarantee for nullable FKs on
   every row.

4. **The four-quadrant enum/lookup framework** (magic enum / lookup table / hybrid / string key)
   with `smallint` conversions. This is a better-articulated policy than most production codebases
   have, and the shipped schema follows it consistently.

5. **MVCC-aware physical decisions.** `fillfactor=90` + aggressive autovacuum on the hot-update
   tables (`user_chapter_interactions`, `daily_story_stats`, `user_story_interactions`);
   `read_progress`/`last_interaction_date` deliberately unindexed to preserve HOT updates; no
   trigger-maintained counters anywhere. Each of these reverses a SQL-Server-era instinct correctly.

6. **Ground-truth stat tables outside the EF model** (`daily_story_stats`, the L8 marts), with
   `site_daily_stats` as the one documented EF-visible exception. The accumulated-vs-rebuildable
   distinction is drawn correctly and recorded in table comments in the DDL itself.

7. **jsonb settings via EF Core 10 complex types** (`reader_settings`/`privacy_settings`/
   `author_settings`), with hot filter flags (`show_mature_content`) kept as real columns. Right
   split: queryable-by-index facts as columns, per-user preference bags as jsonb.

8. **Delete-graph centralization.** All `OnDelete` policy lives in `Data/Configurations/` with the
   explicit rationale that the cascade graph is edited at migration time as one artifact. The
   diamond-breaking pattern (content → SET NULL anonymization; interactions → CASCADE; lookups →
   RESTRICT; four documented RESTRICT conflicts resolved in `UserDeletionService`) is coherent.
   (One contradiction inside it: §2.5.)

9. **Modern-Postgres literacy where it counts:** `NULLS NOT DISTINCT` on the story-character
   custom-name unique; generated `tsvector` column + GIN; partial unique on nullable
   `slug`/`verification_code`; `setval` guards after `HasData` seeds; identity columns rather than
   serials; Data-Protection keyring persisted and excluded from Respawn.

10. **Unique email as deliberate policy** (Identity's default non-unique `EmailIndex` flipped to
    unique, with `RequireUniqueEmail` validating before the constraint throws — MA-103).

11. **The spec-to-shipped deltas that were improvements** (consolidated in §6): dropping the
    denormalized `ViewCount`/`FavoriteCount`/`ChapterCount`/`CommentCount` columns from `stories`
    (each had a drift risk and a better home), promoting vouches from a bool on `followed_users` to
    a first-class `vouches` table with text, folding `SettingDetails` into the `StoryTag` overlay
    pair, and replacing the OC-logic trigger with app-level validation (this codebase has no
    triggers at all — one less hidden control path).

---

## 2. Defects — fix while the database is empty

Ordered by severity. Each is a migration against empty tables today.

### 2.1 Phantom shadow FK: `user_story_interactions.recommendation_id`

**Evidence.** `Recommendation.cs:41` declares `ICollection<UserStoryInteraction>
UserStoryInteractions` with no inverse navigation and no Fluent pairing. EF therefore mints a
shadow FK — the DDL shows `recommendation_id integer` on `user_story_interactions`, an FK with **no
delete behavior specified** (→ NO ACTION), and a secondary index
`ix_user_story_interactions_recommendation_id`. The entity class has no such property; no service
reads or writes it (repo grep: zero hits outside migrations). The *real* recommendation-provenance
home is the deliberate sparse partition `user_story_recommendation_sources`.

**Why it matters.** This is the exact failure class `layer1-data-model.md` documents for TPT
navs ("phantom FKs"), landed on the single hottest table in the system: it widens the "16-byte"
row the whole design brags about, adds a ninth index that must be maintained on every write, and —
because NO ACTION violates the project's own "delete behavior is always explicit" rule — would
block recommendation deletes if the column were ever populated. It also silently contradicts the
documented reason the sources partition exists ("keep the hot table narrow").

**Fix.** Remove the unpaired collection nav (or pair it explicitly to
`UserStoryRecommendationSource` — but that relationship already exists via the composite-key 1:1),
regenerate the model, and confirm the column, FK, and index disappear from the snapshot. One-line
entity change + migration.

### 2.2 TPT cascade-orphan hazard — two unguarded hard-delete paths

**The structural fact.** DB-level cascades cannot maintain TPT integrity. Every content-parent →
comment-child FK (`chapters → chapter_comments`, `base_blog_posts → blog_post_comments`,
`groups → group_comments`, `AspNetUsers ← user_profile_comments` via RESTRICT) deletes **child
rows only**; the abstract `base_comments` row survives with no subtype. An orphaned base row is
poison: any polymorphic query over `BaseComments` fails to materialize (abstract type, no
discriminating child), and counts/likes drift. The same shape exists for
`groups → group_blog_posts` (orphaning `base_blog_posts`) and `base_blog_posts → blog_post_polls`
(orphaning `base_polls`).

**Where it's guarded.** `ServerChapterWriteService.DeleteChapterAsync` (line ~314) loads and
EF-deletes the chapter's comments first, with a comment naming the trap explicitly.

**Where it is NOT guarded (live defects):**
- `ServerBlogPostWriteService.DeleteBlogPostAsync` (line 179) — stub-deletes the TPT blog post and
  lets the DB cascade take `blog_post_comments`. Its own comment ("BlogPostLike / BlogPostComment
  rows cascade") is true only of the child table; the base rows orphan. Same for the post's polls
  (`blog_post_polls` cascades; `base_polls` rows orphan). `DeleteSiteBlogPostAsync` (line 408)
  shares the shape.
- `ServerModerationWriteService.ApplyHardDeleteAsync` (line 564) — the illegal-content path. Hard
  deleting a **Story** cascades `stories → chapters → chapter_comments` (orphaning every base
  comment on every chapter) and `stories → recommendations` (fine, no TPT). Hard deleting a
  **BlogPost** hits the same trap as above.

**Fix options (first principles).**
1. *App-rule + fix the two call sites now* — cheapest; extend the DeleteChapterAsync pattern.
   Fragile long-term: every future hard-delete path must remember (this audit found 1 of 3 sites
   remembered).
2. *Invert the FK posture:* make content-parent → comment-child FKs RESTRICT so the DB refuses the
   partial delete and forces the app to do it right. Turns silent corruption into a loud error —
   attractive for a solo-maintained codebase; costs an extra delete round-trip on legitimate paths.
3. *Integrity sweep:* a scheduled `LEFT JOIN`-all-children orphan check (base rows with no child)
   as a safety net regardless of 1/2.

Recommendation: do (1) now (it fixes live bugs), adopt (2) for the comment/blog/poll hierarchies
in the same migration (empty DB makes the FK flip free), and record the rule in
`layer1-data-model.md` beside the existing TPT traps. Whatever is chosen, this belongs in one WU —
it is a cross-cutting invariant, not three point fixes.

### 2.3 `notifications.related_entity_id` is `integer`; comment ids are `bigint`

Notification types 24/31/33/34 (comment events) store a `base_comments.comment_id` — a `bigint` —
in an `int` column. `Report` solved the identical problem correctly: `ReportedEntityId` is `long`
with a doc comment explaining why (`Report.cs:13-17`). The mismatch is invisible until comment id
2,147,483,648, at which point it becomes a data-corrupting overflow on a table that can't be
retro-widened without rewriting history. Realistic? Comment volume makes it unlikely for years —
but the schema *chose* `bigint` for comments precisely to never think about this, and the
notification column defeats that choice. Widen `RelatedEntityId` to `long` now (one property + one
migration; `user_content_reveals.entity_id` is fine — its `RevealedEntityType` domain is
Story/Group/BlogPost, all `int` PKs).

### 2.4 `group_folders.parent_folder_id` has no FK; root-folder names aren't unique

Spec §"Group Tables" calls for a self-referencing FK with SET NULL. The shipped column is a bare
`integer` — no FK at all (`GroupFolder.cs` has the property but no navigation; no Fluent
relationship). Deleting a folder (`ServerGroupWriteService.cs:332` — a live path) leaves children
pointing at a nonexistent parent; nothing in the DB or (verify before trusting) the service
re-parents them.

Second defect on the same table: the unique index `(group_id, parent_folder_id, name)` uses
default `NULLS DISTINCT`, so two root folders (parent NULL) named "Favorites" in the same group
are both legal — precisely the duplicate the index exists to prevent. The codebase already knows
the cure (`NULLS NOT DISTINCT` on `ix_story_characters_story_id_character_tag_id_custom_name`).

**Fix:** add the self-FK (SET NULL per spec, matching the parent-comment/parent-tag convention) and
rebuild the unique index with `NULLS NOT DISTINCT`. Also note: name uniqueness here is
case-sensitive — see §3.1.

### 2.5 `base_polls.owner_id` CASCADE contradicts the anonymization policy

The delete-policy doctrine (spec §"Delete Policy Summary", `IdentityConfigurations.cs`) is:
*content* survives with SET NULL; *interaction data* cascades. Blog posts are content
(`author_id` SET NULL) — but the polls embedded in those blog posts cascade away with the user
(`fk_base_polls_asp_net_users_owner_id ... ON DELETE CASCADE`), together with everyone else's
votes on them. Result: a surviving anonymized blog post with a hole where its poll was, and other
users' participation destroyed — the exact outcome the SET NULL policy exists to avoid. (Poll
*votes* cascading with the voting user is correct; that's interaction data.)

**Fix:** `owner_id` → nullable + SET NULL, matching `base_blog_posts.author_id`. One migration.
If instead poll-death-with-owner is genuinely intended (polls as ephemeral author artifacts),
record that as a deliberate exception in the audit file — it currently reads as an oversight.

### 2.6 Zero CHECK constraints

`layer1-data-model.md` §Migrations notes CHECK constraints as a supported manual-DDL category —
"*(none present)*". For a pre-launch schema that's a gap, not a style choice: services enforce
these today, but raw-SQL workers, future maintenance scripts, and the moderation hard-delete path
all write around the service layer, and a violated invariant in these families is silently
corrupting rather than loudly failing. The cheap, high-value set:

| Constraint | Table | Invariant |
|---|---|---|
| `CK_story_arcs_bounds` | story_arcs | `start_chapter_number <= end_chapter_number` |
| `CK_uci_read_progress` | user_chapter_interactions | `read_progress BETWEEN 0 AND 1` |
| `CK_followed_users_no_self` | followed_users | `user_id <> followed_user_id` |
| `CK_vouches_no_self` | vouches | `vouching_user_id <> vouched_user_id` |
| `CK_story_lineages_no_self` | story_lineages | `source_story_id <> target_story_id` |
| `CK_fanon_links_no_self` | fanon_links | `base_tag_id <> target_tag_id` |
| `CK_group_folders_no_self` | group_folders | `group_folder_id <> parent_folder_id` |
| `CK_base_polls_dates` | base_polls | `date_closed IS NULL OR date_closed >= date_opened` |
| `CK_users_suspension` | AspNetUsers | `account_status = <Suspended> OR suspended_until_utc IS NULL` (verify enum value before writing) |

Deliberately *not* proposed: non-negativity on denormalized counters (`like_count >= 0` etc.) —
concurrent decrement races would turn a drift bug into a user-facing 500; drift belongs to the
reconciliation policy (§3.6) instead. Per the migration rules, these are `migrationBuilder.Sql`
manual DDL and must join the re-append registry in `layer1-data-model.md`.

### 2.7 Minor consistency defects (batch into any nearby migration)

- **`user_story_interaction_filter_settings`** carries a surrogate identity PK plus a unique on
  `(user_id, search_mode_key, filter_key)`; its sibling `default_..._settings` uses the natural
  composite PK. No query needs the surrogate. Align on the composite PK.
- **Mixed-case PK constraint names on TPT children** (`"PK_blog_post_comments"` vs
  `pk_comment_likes`) — cosmetic fallout of explicit `.ToTable()` bypassing the naming convention;
  harmless, but a one-time rename now costs nothing and removes a permanent "why is this one
  different?" in every future `\d` output.
- **`site_daily_stats`**: `new_users` and `active_users` are nullable, every other counter is NOT
  NULL. If NULL means "unmeasurable for backfilled days," document it in the entity; otherwise make
  them `NOT NULL DEFAULT 0`. An append-only ground-truth table should not have accidental
  nullability semantics.

---

## 3. Decisions to settle deliberately before lock-in

These are not defects; each is a fork where the schema currently sits on a default nobody chose
explicitly, and where changing after real data exists costs 10–100× what it costs now.

### 3.1 Case-insensitive uniqueness for community-facing names

Every non-Identity name uniqueness in the schema is byte-wise case-sensitive: `groups.group_name`,
`custom_lists(user_id, list_name)`, `saved_tag_selections(user_id, nickname)`,
`series(author_id, name)`, `tags(tag_name, tag_type_id)`, `badges.display_name`,
`group_folders(...name)`. Identity solved this problem for usernames/emails with normalized shadow
columns; `fanon_links.normalized_name` shows the pattern is already in the vocabulary — but the
community-facing tables got neither.

The one that matters from first principles is **`groups.group_name`**: it is a site-global
namespace where "Pokemon Fans" vs "pokemon fans" is at best confusing and at worst deliberate
impersonation — a moderation problem the DB could have prevented. Per-user scopes (lists,
selections, series) are self-inflicted-confusion-only; tags are staff-curated. Options: `citext`
(simplest; one extension), unique index on `lower(name)` (no extension, manual DDL, must join the
re-append registry), or normalized columns (heaviest, Identity-style). Recommendation: `lower()`
expression indexes on `groups.group_name` at minimum; decide the per-user scopes cheaply in the
same breath and record the choice either way. Empty DB = no collision backfill risk, which is
exactly the migration hazard this dodges by moving now.

### 3.2 FTS configuration is hardcoded `'english'` in a generated column

`story_listings.search_vector` is `GENERATED ALWAYS AS (to_tsvector('english', title || ' ' ||
short_description))`. Two embedded decisions to ratify or revisit:

1. **Stemming config.** `'english'` stems and drops stopwords — right for prose, questionable for
   a corpus dominated by proper nouns ("Cynthia", "Sinnoh", ship names) and title-case neologisms.
   `'simple'` (no stemming) trades recall on English words for exactness on names. A dual-vector
   (`'english' || 'simple'`) is the usual fanwork-archive answer and costs one more generated
   column + GIN. Whichever wins, it's a column rebuild — trivial now, a full-table rewrite under
   load later.
2. **Scope.** Title + short description only — long descriptions and chapter bodies are
   deliberately excluded (the ledger's #1163 reasoning: HTML noise, catastrophic index cost).
   First principles agree. Ratify the exclusion explicitly so it isn't "discovered" as a gap.

Related but distinct: substring/autocomplete search (`ILIKE '%term%'`) is a **trigram** problem,
not an FTS one, and is already a confirmed MISSING in [[L6-reconciliation-matrix]] (story titles +
tag chips). The `CREATE EXTENSION pg_trgm` decision is schema-adjacent and belongs to whichever WU
takes that finding.

### 3.3 No optimistic concurrency on any content table

The only concurrency tokens in the schema are Identity's `concurrency_stamp` columns. Chapter
editing, story metadata, blog posts, group settings — all last-write-wins. Today that's
defensible (single author per story; co-author tables exist but the feature is dormant). But
`chapter_contents.author_id` exists precisely because multi-writer editing is anticipated, and a
lost chapter revision is the worst data-loss event this site can inflict on a user.

Postgres offers a zero-schema-cost answer: map `xmin` as a concurrency token
(`.Property(...).HasColumnName("xmin").IsRowVersion()` via Npgsql) on `chapter_contents`,
`stories`/`story_details`, and `base_blog_posts`. No column, no migration, no write cost — only a
409-on-stale-edit path in the services. Decide now whether the edit surfaces should detect
conflicts, because the *UX* contract (silent overwrite vs. "someone else saved") is what gets
locked in by habit once humans are editing.

### 3.4 `stories.published_date` is NOT NULL for drafts

Spec §Story had `PublishedDate` nullable; shipped is `NOT NULL` with no default. A Draft
(status 0) story therefore carries a "published" timestamp that means "row created" — and
discovery sorts (`ix_stories_published_date`) key on it. The write path presumably re-stamps at
actual publication (verify in `ServerStoryWriteService` before acting), but the schema can't
distinguish "never published" from "published at creation time," which matters for: the approval
workflow (F48 pending→approved transitions), "new stories" surfaces, and any future
publish-scheduling. Either restore nullability (NULL = never published; partial indexes already
exclude nothing since discovery filters on status) or rename the semantic in the audit file to
"date_created-until-published" and confirm every consumer re-stamps on publish. The nullable form
is the honest model.

### 3.5 `user_story_interaction_dates` is write-only — wire it or cut it

Confirmed against 2026-08-02 code: the write service diligently maintains the partition
(`ServerUserStoryInteractionWriteService` lines 43–219) and **no read service anywhere touches
it** ([[L6-reconciliation-matrix]] flagged this 2026-07-27; still true). The design intent —
date-sorted bookshelf tabs ("favorited on…") — is a real feature fanfic users expect. Three
coherent positions, pick one: (a) build the date-sorted list surfaces (the table then earns the
partial date indexes the spec sketched); (b) keep writing it as cheap future-proofing and record
that as deliberate (it *is* cheap — same row lifecycle as the parent); (c) cut it. The
incoherent position is the current one: paying the write cost with no consumer and no recorded
decision.

### 3.6 Denormalized counters need a stated reconciliation policy

Inventory: `base_comments.like_count`, `base_blog_posts.like_count`,
`recommendations.like_count/successful_rec_count`, `chapters.version_count`,
`stories.word_count`, `user_badges.earned_count`, `active_report_count` ×4 tables, all 22
`user_stats` counters. Every one is service-maintained (correctly — no triggers, per the MVCC
reasoning), which means every one can drift (WU-UserModeration found exactly this:
`ActiveReportCount` leaking on account actions).

The principle to lock: **every denormalized counter must be recomputable from ground truth, and
the recompute must exist as code.** Today only `user_stats` has that (`UserStatRecalculator`,
Feature 58). `like_count` (junctions exist), `version_count`, `word_count`,
`active_report_count` (derivable from `reports` by status) all have ground truth but no
reconciler. A periodic or admin-triggered recount for the cheap ones is a few dozen lines; what
matters pre-lock-in is *ratifying which columns are authoritative vs. derived*, so a future drift
incident has a defined answer ("recount is truth") instead of a data-archaeology project.

### 3.7 Moderation audit trail — ratify report-as-audit-record, decide the author column now

The settled rule (`layer2-services.md` §"Account actions") is that `Report` rows ARE the
moderation ledger (self-reported rows for ad-hoc actions). First-principles check: this holds —
reports survive user deletion (both FKs SET NULL, comment says "must survive for audit"), takedown
metadata lives on the entity, and the ephemeral notification history (pruned by Feature 57's
cleanup worker) is display-only, not the record. Two consequences worth pinning:

- **Tracker B18's open question** (a user's history omits reports against their *content*) has a
  schema-side answer whose cost is asymmetric: denormalizing `reported_author_user_id` onto
  `reports` at write time is a nullable column + one write-path line **now**, versus a four-table
  backfill join later. If B18 is ever going to be built — and for ban decisions it's the primary
  signal — add the column while the table is empty, even if the UI waits.
- **Takedown reversal erases history:** `is_taken_down` flipping back leaves
  `takedown_date/reason` populated-but-stale or nulled (whichever the service does — verify).
  Either is fine *because* the Report row persists; record that reasoning so nobody adds a
  takedown-history table reflexively.

### 3.8 User-deletion survival set — ratify what remains

`UserDeletionService` + the cascade graph produce a specific post-deletion world; it should be a
ratified list rather than an emergent one, because it is the site's de-facto GDPR/erasure answer:

- **Survives, anonymized (SET NULL):** stories + chapters/contents, comments, blog posts,
  recommendations, series, groups created, group-story additions, private message *bodies*
  (sender nulled — the other participant's thread remains readable; conversations with zero
  remaining participants linger as unreachable rows), spotlight grants, reports filed/resolved.
- **Destroyed (CASCADE):** interactions, follows outgoing, vouches given, custom lists, badges,
  settings, group memberships, poll votes, content reveals, external identities — and, per §2.5,
  currently their *polls* (probably wrongly).
- **Destroyed by service code (RESTRICT conflicts):** comments on their profile, follows/vouches
  incoming, notification source links.

Two unratified edges: (a) empty `conversations` rows accumulate (harmless; note or sweep); (b) a
group whose every admin deletes their account becomes unadministered — `creator_id` SET NULL is
right for attribution, but nothing hands the group to anyone (app-layer concern; tracker-class
item, not schema).

### 3.9 `chapter_contents` has no editor source column — ratify the divergence

Spec §ChapterContent specifies both `ChapterText` (sanitized HTML) and `ContentRaw`
(markdown/editor source). Shipped has only `chapter_text` (grep: `ContentRaw` appears nowhere).
Consequence: the sanitized HTML *is* the canonical source — edits round-trip through
Quill-parsing-HTML, and export (F54) serves HTML-derived output. That's a coherent position
(Quill's HTML is lossless for its own feature set), but it forecloses ever migrating editors
losslessly. Pre-lock-in is the moment to either add the column (cheap now, unrecoverable later —
you cannot backfill source that was never stored) or record the ratified position that HTML is
source-of-truth. Given A1 (sprite blot) will extend the sanitizer allow-list, the HTML-as-truth
position is workable; it just needs to be *chosen*, in `audit/Chapters.md`, not defaulted into.

### 3.10 `group_folder_group_story` — EF-generated join with no same-group guarantee

The implicit M:N produces column names like `group_folders_group_folder_id` and, structurally,
permits a folder of group A to contain a `group_stories` row of group B — no DB constraint ties
the two sides to the same group. The service layer surely scopes correctly, but this is the
polymorphic-adjacent class of invariant that a composite-FK redesign *can* express: give the join
explicit columns `(group_id, group_folder_id, group_story_id)` with composite FKs
`(group_id, group_folder_id) → group_folders` and `(group_id, group_story_id) → group_stories`
(both tables would need the corresponding unique indexes — they're cheap). Decide whether that
rigor is worth an explicit join entity; if not, record the app-invariant. The naming cleanup alone
argues for an explicit entity while the table is empty.

### 3.11 Public integer IDs — accepted, say so

All public URLs expose sequential int IDs (`/story/{id}/{slug}`). Enumeration is thus trivial
(site size, growth rate, unlisted-content guessing). The access-gate design already treats
direct-nav as a consented plane, drafts are status-gated server-side, and slugs are decorative —
so sequential IDs are *fine here*, but it's a classic "wish we'd used something else" regret
vector, so the acceptance deserves one recorded sentence. (Switching to anything else post-launch
is a URL-breaking event; switching now costs the id-based routing contract. Keep ints; record it.)

---

## 4. Type & scale audit (verified sound — no action)

- **PK widths:** `int` for users/stories/groups/tags/recommendations (fanfic-scale correct — the
  ledger's "billions of rows" framing was already debunked); `bigint` for the event tables
  (comments, notifications, messages, chapter_contents, reports). One deliberate divergence from
  spec: `base_blog_posts.blog_post_id` is `int` (spec said long) — defensible; blog posts are
  authored artifacts, not events. `smallint` identity for lookup keys; string keys where the key
  is code-facing. Consistent with the framework.
- **Timestamps:** `timestamptz` universally; `date` for date-only facts
  (`original_published_date`, `stat_date`). The SQL-Server `datetime2(2)` precision hangover is
  fully gone. `CURRENT_TIMESTAMP` defaults on creation columns only.
- **Text bounds:** every bounded string carries a max length; 512 CDN / 2048 external URL split is
  applied consistently; unbounded `text` only on genuine blobs (chapter text, profile text,
  message text, rec text, jsonb settings).
- **Booleans:** NOT NULL throughout; the 1-byte cost is accepted and documented.
- **`real` for `read_progress`:** float4 for a 0–1 scroll fraction — correct (precision
  irrelevant, width matters on the churn table).

---

## 5. Index layer — confirmation against 2026-08-02 DDL

[[L6-reconciliation-matrix]] (2026-07-27) remains the authority; this pass re-checked its open
verdicts against the regenerated DDL. **All still open — none were silently fixed or invalidated
by the five migrations since** (`WU_SiteNews_SiteBlogPost` through `DropUserCustomFilter`):

- MISSING: `pg_trgm` GIN ×2 (`story_listings.story_title`, `tags.tag_name`); the cross-cutting
  `recommendations(story_id|recommender_id, status_id, …, date_posted DESC)` composites;
  `groups(audience_rating, date_created DESC)`; blog TPT child composites (by-author, by-group);
  `conversation_participants(user_id, is_archived)`; `group_members(group_id, date_joined)`;
  `series_entries(series_id, order_index)`; story-centric USI partials (the "Rejected-vs-live
  conflict" — now with WU-B2's three additional call sites).
- WRONG: `ix_tags_tag_name_tag_type_id` column order vs. `GetTagsByTypeAsync`;
  `ix_series_author_id_name` vs. date-sorted listing; vouches incoming sort;
  `ix_group_folders_group_id_parent_folder_id_name` vs. `sort_order` tree build.
- Note: `20260731033333_WU_StatBadgeProducers` *dropped* `ix_story_acknowledgments_acknowledged_user_id`
  in favor of `(acknowledged_user_id, status_id)` — a correct composite upgrade, consistent with
  the matrix's method.
- The **live `pg_indexes` reconciliation is still PENDING** (the six-index-collapse class cannot be
  ruled out from the snapshot alone). Any WU that touches indexes should run it first.

One new index-layer observation from this pass, ledger-consistent: the notification unread badge
would be served by a narrower partial (`(recipient_user_id) WHERE is_read = false`) than the
shipped composite — the ledger (#660) already notes this as refinement-not-correction; leave to a
measured pass.

---

## 6. Spec §4 vs. shipped — consolidated schema-level divergence ledger

The spec is a read-only snapshot; per doctrine, divergences live in audit files. This table
consolidates the schema-level ones found by full comparison, with a first-principles verdict on
each. Rows marked ✅ need no action beyond (where noted) an audit-file line; rows marked ⚠ are the
subjects of §2/§3 findings.

| Spec §4 says | Shipped | Verdict |
|---|---|---|
| Story: `ViewCount`, `IsComplete`, `ChapterCount`, `CommentCount`, `FavoriteCount` columns | All absent — views in `daily_story_stats`, completion in status, counts computed/`user_stats` | ✅ improvement (drift-prone hot counters removed) |
| Story: `PublishedDate` nullable | NOT NULL | ⚠ §3.4 |
| ChapterContent: `ContentRaw` editor source | Never built | ⚠ §3.9 |
| Chapter: `PrimaryContentId` NOT NULL | Nullable (circular-FK break, two-step insert) | ✅ correct trade; RESTRICT preserved |
| StoryCharacter OC trigger `TR_..._EnforceOCLogic` | No triggers anywhere; app validation + `NULLS NOT DISTINCT` unique | ✅ improvement |
| `SettingDetails` table | Folded into `StoryTag` custom_name/nuance overlay (WU-TagFanon) | ✅ recorded already |
| Tag: `TagName` globally unique; `Relationship` tag type | Unique per `(tag_name, tag_type_id)`; ships modeled via pairings, no Relationship type | ✅ deliberate (WU-TagFanon lineage); case-sensitivity → §3.1 |
| FollowedUser: `IsVouched` bool + filtered indexes | First-class `vouches` table with `vouch_text` | ✅ improvement |
| USI: story-centric filtered indexes both directions | User-centric only | ⚠ known — reconciliation matrix conflict row |
| UserStoryInteractionDate: filtered date indexes + sorted lists | Table written, never read, no date indexes | ⚠ §3.5 |
| `UserCustomFilter` | Cut entirely (2026-07-31) | ✅ recorded |
| BaseBlogPost: `long` PK | `int` PK | ✅ acceptable; record in audit file |
| Notification: `RelatedEntityId` polymorphic | `int` column | ⚠ §2.3 (width) |
| GroupFolder: `ParentFolderId` self-FK SET NULL | No FK at all | ⚠ §2.4 |
| Delete policy: content SET NULL | `base_polls.owner_id` CASCADE | ⚠ §2.5 |
| Constraint naming `PK_TableName`/`FK_Source_Target` | snake_case EF conventions (+ mixed-case TPT PKs) | ✅ convention superseded spec; cosmetic note §2.7 |
| Cache/mart tables incl. Redis Top-100 | Marts raw-SQL as spec'd; Redis layer dissolved (L7, 2026-07-06) | ✅ recorded elsewhere |

---

## 7. Recommended sequencing

**One "schema hardening" WU before first human data** (all §2 items — they compose into a single
migration + small service fixes): remove the phantom USI column (2.1), fix the two TPT hard-delete
paths + FK-posture decision (2.2), widen `related_entity_id` (2.3), folder FK + `NULLS NOT
DISTINCT` (2.4), poll-owner SET NULL (2.5), CHECK constraint set (2.6), consistency batch (2.7).
Everything touches empty tables; the riskiest part is the TPT service-code fix, which has an
existing in-repo pattern to copy.

**Decision batch for the owner** (§3 — each is a one-paragraph ruling, some spawn small WUs):
group-name case-insensitivity (3.1), FTS config (3.2), concurrency policy (3.3), published_date
semantics (3.4), interaction-dates fate (3.5), counter-reconciliation principle (3.6), report
author column (3.7), deletion survival-set ratification (3.8), HTML-as-source ratification (3.9),
folder-join rigor (3.10), public-ID acceptance (3.11).

**Explicitly not this audit's scope:** building any of the L6 missing indexes (§5 — owned by the
reconciliation matrix's later build/measure pass, per the locked "always measure" rule), the live
`pg_indexes` sweep, and SeedTool/PerfBaseline gaps (tracker C-items).
