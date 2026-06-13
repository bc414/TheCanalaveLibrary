# EF Core / Npgsql / PostgreSQL Conventions

Target: **EF Core 10**, `Npgsql.EntityFrameworkCore.PostgreSQL` 10, `EFCore.NamingConventions` 10.
Code-First with Fluent API for all non-trivial configuration. Models are POCOs in the **Core** project,
which references only `Microsoft.EntityFrameworkCore.Abstractions` (for `[Index]`); the DbContexts live in
the **server** project.

## Provider & naming setup

Configure snake_case globally — never hand-name tables or columns. With Aspire, the Npgsql provider is
wired via `AddNpgsqlDbContext`, but the EF options (naming, tracking) are still applied:

```csharp
builder.AddNpgsqlDbContext<ApplicationDbContext>("canalavedb",
    configureDbContextOptions: options =>
        options.UseSnakeCaseNamingConvention());
```

`EFCore.NamingConventions` auto-converts `UserStoryInteraction` → `user_story_interactions`,
`IsHiddenFavorite` → `is_hidden_favorite`. PostgreSQL folds unquoted identifiers to lowercase, so this
prevents quoting headaches. **Avoid:** `[Table("...")]` / `[Column("...")]` attributes to force names —
let the convention do it.

## Two DbContexts (CQRS-lite read/write split)

| Context | Tracking | Used by |
|---|---|---|
| `ApplicationDbContext` | tracked (default) | Command path (writes), migrations, background workers |
| `ReadOnlyApplicationDbContext` | `NoTracking` globally | Query path (reads) — search, filtering, display |

```csharp
// ReadOnlyApplicationDbContext
protected override void OnConfiguring(DbContextOptionsBuilder options)
    => options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
```

Both map the same model. Keep `OnModelCreating` configuration in one place (a shared base or applied
`IEntityTypeConfiguration<T>` classes) so the two contexts can't drift.

## TPT inheritance (settled — not TPH)

EF Core defaults to TPH. This project uses **TPT**: call `.ToTable()` on the base and every derived type.

```csharp
modelBuilder.Entity<BaseComment>().ToTable("base_comments");
modelBuilder.Entity<ChapterComment>().ToTable("chapter_comments");
modelBuilder.Entity<UserProfileComment>().ToTable("user_profile_comments");
modelBuilder.Entity<GroupComment>().ToTable("group_comments");
modelBuilder.Entity<BlogPostComment>().ToTable("blog_post_comments");
```

Hierarchies: `BaseComment → {ChapterComment, UserProfileComment, GroupComment, BlogPostComment}`,
`BaseBlogPost → {ProfileBlogPost, GroupBlogPost}`, `BasePoll → {SitePoll, BlogPostPoll}`. Base classes are
`abstract`; child classes inherit. Child FKs (e.g. `ChapterComment.ChapterId`) are **non-nullable** — that
NOT NULL guarantee is the whole point of choosing TPT.

**Denormalization with TPT:** `DatePosted` is duplicated from the base into each child table so you can put
a composite "golden index" like `(chapter_id, date_posted DESC)` directly on the small child table and
avoid a cross-table sort.

## Enums (settled: smallint, byte-backed, suffix `Enum`)

C# enums map to `smallint`. Declare `: byte`, 0-indexed, and configure conversion **per property** in
`OnModelCreating` (no global loop — C# generic constraints prevent it):

```csharp
public enum StoryStatusEnum : byte { Draft = 0, Ongoing = 1, Completed = 2, Hiatus = 3 }

modelBuilder.Entity<Story>()
    .Property(s => s.Status)
    .HasConversion<short>();
```

The `Enum` suffix avoids colliding with lookup-table model classes (`StoryStatusEnum` the enum vs
`StoryStatus` the lookup table). **Avoid:** storing enums as strings, mapping to `int`, or relying on old
1-indexed SQL values — C# 0-indexing is the source of truth.

### Enum vs lookup table decision

| Pattern | When | Examples |
|---|---|---|
| Magic enum (no table) | Tiny, stable, app-coupled, no display name | `Rating`, `FavoriteStatus`, `ReadStatus` |
| Lookup table (no enum) | Content-only display; rename/add without deploy | `ReportReason`, `AcknowledgmentRole` |
| Hybrid (table + enum) | Need both flexible display AND rigid C# logic | `StoryStatus`, `TagType`, `NotificationType` |
| String key (string PK) | Tiny table; key used directly in C# | `SearchModeKey`, `BadgeKey` |

## Column conventions

- **Keys:** single-column `int` identity where possible; `long` for ever-growing "event" tables (comments,
  notifications, messages, chapter contents); composite keys for junction/interaction tables.
- **Timestamps:** all `DateTime` → `timestamp(2) with time zone` (8 bytes); all `DateOnly` → `date` (4 bytes).
  Creation timestamps use `.HasDefaultValueSql("CURRENT_TIMESTAMP")`.
- **Strings:** `[Required]` + non-nullable `string` for mandatory; `string?` (no `[Required]`) for optional;
  `[MaxLength(n)]` on every bounded string. URLs: `[MaxLength(512)]` for CDN/relative, `[MaxLength(2048)]`
  for arbitrary external.
- **Booleans:** `NOT NULL DEFAULT false` (1 byte each in PG — no bit-packing, accepted trade-off).

```csharp
modelBuilder.Entity<Story>().Property(s => s.Title).HasMaxLength(200).IsRequired();
modelBuilder.Entity<Story>().Property(s => s.CreatedDate)
    .HasColumnType("timestamp(2) with time zone")
    .HasDefaultValueSql("CURRENT_TIMESTAMP");
modelBuilder.Entity<UserStoryInteraction>().Property(i => i.IsFavorited)
    .HasDefaultValue(false);
```

## Relationships & queries

- **Delete behavior is always explicit** — `.OnDelete(DeleteBehavior.X)` on every relationship. Never rely
  on EF Core defaults.
- **No lazy loading.** It hides N+1. Use explicit `.Include()` (commands) or `.Select()` projections (reads).
- **Cartesian explosion:** add `.AsSplitQuery()` when an `.Include()` fans out across collections.
- **Filtered indexes:** `HasFilter("is_favorited = true")` — snake_case column, PostgreSQL `true`/`false`
  (not `1`/`0`). Name with `HasDatabaseName("ix_...")` for migration stability.

```csharp
modelBuilder.Entity<UserStoryInteraction>()
    .HasIndex(i => i.UserId)
    .HasFilter("is_favorited = true")
    .HasDatabaseName("ix_user_story_interactions_user_id_favorited");
```

### EF Core 10 query features worth using

- `LeftJoin` / `RightJoin` are first-class LINQ operators now — use them instead of the old
  `GroupJoin`+`SelectMany`+`DefaultIfEmpty` dance for outer joins.
- **Named query filters** — when an entity needs multiple global filters, name them so a query can ignore
  one selectively via `IgnoreQueryFilters([...])` rather than dropping all filters.
- `ExecuteUpdateAsync` accepts non-expression lambda bodies (custom logic inline). Good for set-based writes
  that don't need entity loading.

## Vertical partitioning

Hot/warm/cold splits are deliberate — keep them. Don't merge a cold blob column back into a hot table.

| Entity | Hot | Warm | Cold |
|---|---|---|---|
| Story | `Story` (~70 B) | `StoryListing` (~254 B) | `StoryDetail` (blob) |
| User | `User` (hot+warm) | — | `UserProfile` (ProfileText blob) |
| Recommendation | `Recommendation` | — | `RecommendationDetail` (Text blob) |
| UserStoryInteraction | `UserStoryInteraction` (filtering) | `UserStoryInteractionDate` (lists) | `UserStoryRecommendationSource` |

These map as one-to-one relationships (shared PK). The hot table must stay small enough to fit the buffer pool.

## Migrations

Hybrid scaffold-to-code-first, then pure code-first. Key manual edits EF won't generate:

- **CHECK constraints:** add via `migrationBuilder.Sql(...)` in `Up()`, drop in `Down()`. Not auto-generated.
- **Triggers:** `CREATE TRIGGER` in `Up()`, `DROP TRIGGER` in `Down()`.
- **Constraint naming:** EF auto-generates except where explicitly overridden; follow spec §7 when overriding.

**Pre-launch:** "nuke and rebuild" is allowed — delete Migrations folder, drop DB, regenerate
`InitialSchema`. **Post-launch:** never delete the Migrations folder; every schema change is an incremental
migration.

Cache/data-mart tables (`UserStoryTreeSearchEntries`, `AlsoFavoritedScore`, `AlsoRecommendedScore`) are
**NOT in EF Core migrations** — they're managed by background workers via raw SQL. Don't add DbSets that
pull them into the model graph for writes.
