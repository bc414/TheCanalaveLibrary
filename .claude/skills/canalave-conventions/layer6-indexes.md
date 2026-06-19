# Layer 6 — SQL Indexes

Filtered, composite, golden, GIN indexes — pure DDL, zero code changes.
This layer pairs with optional query optimization (method body swaps behind stable interfaces).

## When This Layer Applies

Layer 6 activates when profiling identifies slow queries. The schema was designed in Layer 1 to
*support* efficient indexes; Layer 6 actually adds them. Every index here is a migration containing
only DDL — no service code changes unless profiling also warrants a query body swap.

## Filtered Indexes

Use snake_case column names and PostgreSQL `true`/`false` (not `1`/`0`) in filters.
Always name explicitly with `HasDatabaseName()` for migration stability:

```csharp
modelBuilder.Entity<UserStoryInteraction>()
    .HasIndex(i => i.UserId)
    .HasFilter("is_favorite = true")
    .HasDatabaseName("ix_user_story_interactions_user_id_favorite");
```

**Covering indexes via Npgsql `.IncludeProperties()`** — this is the correct method name
(extension method from `NpgsqlIndexBuilderExtensions`). SQL Server has an identically-named
extension. Do NOT use a bare `.Include()` on `IndexBuilder` — that method doesn't exist for
index configuration:

```csharp
modelBuilder.Entity<UserStoryInteraction>()
    .HasIndex(i => i.UserId)
    .HasFilter("is_favorite = true")
    .IncludeProperties(i => i.StoryId)
    .HasDatabaseName("ix_user_story_interactions_user_id_favorite_incl_story");
```

## UserStoryInteraction Index Strategy

Both user-centric and story-centric filtered indexes per boolean:

```
-- User-centric (user's favorites list)
ix_user_story_interactions_user_id_favorite
  ON (user_id) INCLUDE (story_id) WHERE (is_favorite = true)

-- Story-centric (story's favorite count / list)
ix_user_story_interactions_story_id_favorite
  ON (story_id) INCLUDE (user_id) WHERE (is_favorite = true)
```

Repeat this pattern for: `is_followed`, `is_ignored`, `is_read_it_later`,
`is_hidden_favorite`, `has_started`, `is_completed`.

**Key composite queries:**
```
-- Continue Reading: HasStarted AND NOT IsCompleted AND NOT IsIgnored
ix_user_story_interactions_user_id_reading
  ON (user_id) INCLUDE (story_id) WHERE (has_started = true AND is_completed = false AND is_ignored = false)

-- Abandoned: IsIgnored AND HasStarted
ix_user_story_interactions_user_id_abandoned
  ON (user_id) INCLUDE (story_id) WHERE (is_ignored = true AND has_started = true)

-- Discovery exclusion: HasStarted OR IsIgnored
ix_user_story_interactions_user_id_discovery_exclude
  ON (user_id) INCLUDE (story_id) WHERE (has_started = true OR is_ignored = true)
```

## Golden Indexes (Composite on TPT Child Tables)

The denormalized `DatePosted` on TPT child tables enables "golden indexes":

```
ix_chapter_comments_chapter_id_date_posted
  ON chapter_comments (chapter_id, date_posted DESC)
```

This avoids a cross-table sort — the query hits only the small child table.

## UserStoryInteractionDate Indexes

Sorted user lists (e.g. favorites sorted by date):

```
ix_user_story_interaction_date_user_id_favorite_date
  ON (user_id, favorite_date DESC) WHERE (favorite_date IS NOT NULL)
```

## GIN Indexes (Full-Text Search)

`StoryListing.SearchVector` is an `NpgsqlTsVector` generated computed column with a GIN index:

```csharp
modelBuilder.Entity<StoryListing>()
    .HasIndex(sl => sl.SearchVector)
    .HasMethod("GIN")
    .HasDatabaseName("ix_story_listings_search_vector");
```

FTS covers title and short description only. NOT chapter body text.

## StoryTag Reverse Index

```
ix_story_tags_tag_id_story_id
  ON story_tags (tag_id, story_id) INCLUDE (priority)
```

Enables efficient "all stories with tag X" queries.

## FollowedUser Vouch Indexes

```
ix_followed_users_user_id_vouched
  ON followed_users (user_id) WHERE (is_vouched = true)

ix_followed_users_followed_user_id_vouched
  ON followed_users (followed_user_id) WHERE (is_vouched = true)
```

## Notification Index

```
ix_notifications_recipient_read_date
  ON notifications (recipient_user_id, is_read, date_created DESC)
```

## Query Optimization (Paired with Index Addition)

| Stage | Query looks like | When |
|---|---|---|
| MVP (Layers 1–4) | Two composed queries via service injection | Initial implementation |
| Post-profiling (Layer 6) | Index added + optional single optimized JOIN | Measurement shows bottleneck |
| At scale (Layer 7) | Redis cache in front of the query | Database load warrants caching |

The escape hatch (direct JOIN bypassing composition) is the exception, not the default.
