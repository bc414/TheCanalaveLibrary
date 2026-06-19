# Layer 1 — Data Model

EF Core 10, `Npgsql.EntityFrameworkCore.PostgreSQL` 10, `EFCore.NamingConventions` 10.
Code-First with Fluent API. Models are POCOs in **Core** (references only
`Microsoft.EntityFrameworkCore.Abstractions`); DbContexts live in **Server**.

## Provider & Naming

Configure snake_case globally — never hand-name tables or columns:

```csharp
builder.AddNpgsqlDbContext<ApplicationDbContext>("canalavedb",
    configureDbContextOptions: options =>
        options.UseSnakeCaseNamingConvention());
```

`EFCore.NamingConventions` auto-converts `UserStoryInteraction` → `user_story_interactions`.
PostgreSQL folds unquoted identifiers to lowercase. Identity tables (`AspNetUsers`, etc.) retain
PascalCase — the convention method does not override explicit `IdentityDbContext` configurations.

## Two DbContexts (CQRS-Lite Read/Write Split)

| Context | Tracking | Used by |
|---|---|---|
| `ApplicationDbContext` | tracked (default) | Command path (writes), migrations, background workers |
| `ReadOnlyApplicationDbContext` | `NoTracking` globally | Query path (reads) — search, filtering, display |

```csharp
// ReadOnlyApplicationDbContext
protected override void OnConfiguring(DbContextOptionsBuilder options)
    => options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
```

Both map the same model. Keep `OnModelCreating` in one place — use `IEntityTypeConfiguration<T>` classes
with `ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly())` for vertical-slice organization.

## TPT Inheritance (Settled — Not TPH)

Call `.ToTable()` on the base and every derived type:

```csharp
modelBuilder.Entity<BaseComment>().ToTable("base_comments");
modelBuilder.Entity<ChapterComment>().ToTable("chapter_comments");
modelBuilder.Entity<UserProfileComment>().ToTable("user_profile_comments");
modelBuilder.Entity<GroupComment>().ToTable("group_comments");
modelBuilder.Entity<BlogPostComment>().ToTable("blog_post_comments");
```

Hierarchies: `BaseComment → {Chapter, UserProfile, Group, BlogPost}Comment`,
`BaseBlogPost → {Profile, Group}BlogPost`, `BasePoll → {Site, BlogPost}Poll`.
Base classes are `abstract`. Child FKs (e.g. `ChapterComment.ChapterId`) are **non-nullable** —
the NOT NULL guarantee is the point of choosing TPT.

**ChapterComment additional column:** `IsSpoiler` (bool, default false). Spoilers are a
chapter-discussion concept — NOT on `BaseComment`.

**Denormalization with TPT:** `DatePosted` is duplicated from base into each child table to enable
composite "golden indexes" like `(chapter_id, date_posted DESC)` on the small child table.
EF Core config: define the property on the base C# model, then provide explicit Fluent API on
**each derived entity** to override the default base-table mapping.

## Enum / Lookup Table Decision Framework

| Pattern | When | Examples |
|---|---|---|
| **Magic enum** (no table) | Tiny, stable, app-coupled, no display name | `Rating`, `ReportedEntityType`, `CharacterRelationshipType`, `FilterEntityType`, `ProfileVisibility` |
| **Lookup table** (no enum) | Content-only display; rename/add without deploy | `ReportReason`, `AcknowledgmentRole`, `StoryRelationshipType` |
| **Hybrid** (table + enum with `...Enum` suffix) | Both flexible display AND rigid C# logic | `StoryStatusEnum`, `ReportStatusEnum`, `NotificationCategoryEnum`, `NotificationTypeEnum` |
| **String key** (string PK) | Tiny table; key used directly in C# | `SearchMode.SearchModeKey`, `Badge.BadgeKey`, `UserInteractionFilter.InteractionFilterKey` |

**Magic enums:** stored as `smallint` via `.HasConversion<short>()`. Underlying type `: short`, 0-indexed:

```csharp
public enum Rating : short { Everyone = 0, Teen = 1, Mature = 2 }

modelBuilder.Entity<Story>()
    .Property(s => s.Rating)
    .HasConversion<short>();
```

**Exception — SiteRoles:** uses `: int` (matching Identity's int PK) and is 1-indexed
(`User = 1, Moderator = 2, Admin = 3`).

## UserStoryInteraction (Hot Table)

Highest-traffic table. Sparse: no row = all defaults false. 16 bytes/row.

| Column | Type | Prefix Convention |
|---|---|---|
| UserId | int | PK (composite) |
| StoryId | int | PK (composite) |
| HasStarted | bool | `Has-` — permanent past event |
| IsCompleted | bool | `Is-` — current mutable state |
| IsFavorite | bool | `Is-` — current mutable state |
| IsHiddenFavorite | bool | `Is-` — current mutable state |
| IsFollowed | bool | `Is-` — current mutable state |
| IsReadItLater | bool | `Is-` — current mutable state |
| IsIgnored | bool | `Is-` — current mutable state |

**Has-/Is- prefix convention:**
- `Has-` prefix (`HasStarted`): permanent past event. Set by application at 90% scroll of Chapter 1.
  Only cleared by deliberate user action. Records that reading *began*, not that reading is *current*.
- `Is-` prefix (`IsCompleted`, `IsIgnored`): current mutable state. Can be toggled.

**Zero coupling rules:** No bit automatically drives any other bit. Each is set and cleared
independently. The service layer rejects logically impossible write combinations but does not cascade.

**Vertical partitions:** `UserStoryInteractionDate` (warm: nullable date columns),
`UserStoryRecommendationSource` (sparse: FK to Recommendation).

## Column Conventions

- **Keys:** single-column `int` identity where possible; `long` for event tables (comments,
  notifications, messages, chapter contents); composite keys for junction/interaction tables.
- **Timestamps:** all `DateTime` → `timestamp(2) with time zone` (8 bytes); all `DateOnly` → `date` (4 bytes).
  Creation timestamps use `.HasDefaultValueSql("CURRENT_TIMESTAMP")`.
- **Strings:** `[Required]` + non-nullable `string` for mandatory; `string?` for optional;
  `[MaxLength(n)]` on every bounded string. URLs: `[MaxLength(512)]` for CDN/relative, `[MaxLength(2048)]`
  for external.
- **Booleans:** `NOT NULL DEFAULT false` (1 byte each in PostgreSQL — no bit-packing, accepted trade-off).

## Relationships & Queries

- **Delete behavior is always explicit** — `.OnDelete(DeleteBehavior.X)` on every relationship.
- **No lazy loading.** Use explicit `.Include()` (commands) or `.Select()` projections (reads).
- **Cartesian explosion:** add `.AsSplitQuery()` when `.Include()` fans out across collections.
- **Relationship config:** set navigation properties (`story.Author = user`) rather than FK IDs.

## EF Core 10 Query Features

- **`LeftJoin` / `RightJoin`** — first-class LINQ operators. Use instead of `GroupJoin`+`SelectMany`+`DefaultIfEmpty`.
- **Named query filters** — attach multiple named filters per entity, selectively ignore specific ones.
  Use an enum for filter names (avoid hardcoded strings).
- **`ExecuteUpdateAsync`** — accepts non-expression lambda bodies for set-based writes without entity loading.
- **Explicit default constraint naming** — default constraints can be named explicitly for migration clarity.

## JSON Complex Types (EF Core 10 + Npgsql 10)

EF Core 10 introduced first-class support for mapping .NET complex types to JSON columns via
`.ToJson()`. Npgsql 10 supports this for PostgreSQL `jsonb` columns. This is now the recommended
approach — the previous owned-entity JSON mapping is deprecated.

```csharp
// Define a complex type (no key, not an entity)
public class ReaderSettings
{
    public string FontName { get; set; } = "Georgia";
    public int FontSize { get; set; } = 16;
    public float LineHeight { get; set; } = 1.6f;
    public int TextWidth { get; set; } = 700;
    public bool JustifyText { get; set; } = false;
    public bool AutoLoadNextChapter { get; set; } = true;
    public bool CollapseCommentThreads { get; set; } = false;
    public int DefaultPaginationSize { get; set; } = 20;
}

// Configure in Fluent API
modelBuilder.Entity<User>(entity =>
{
    entity.ComplexProperty(u => u.ReaderSettings, b => b.ToJson());
    entity.ComplexProperty(u => u.PrivacySettings, b => b.ToJson());
    entity.ComplexProperty(u => u.AuthorSettings, b => b.ToJson());
});
```

**Benefits over raw jsonb string columns:**
- EF is aware of the JSON structure — LINQ queries against nested properties translate to SQL.
- Type-safe C# access (no `JsonSerializer.Deserialize` at the call site).
- Proper change tracking — EF detects mutations inside the JSON.
- New settings still don't require migrations (add properties with defaults to the C# type).

**Current project state:** The `User` entity has `ReaderSettings`, `PrivacySettings`, and
`AuthorSettings` as jsonb columns. Whether these use the new complex type mapping or the older
delegate-to-System.Text.Json approach is a Layer 1 implementation decision. The complex type
approach is preferred for new code.

**Limitation:** Enums inside JSON complex types still need `.HasConversion<short>()` configured
on the containing entity's Fluent API.

## Covering Indexes

PostgreSQL and Npgsql support covering indexes via `.IncludeProperties()`:

```csharp
modelBuilder.Entity<UserStoryInteraction>()
    .HasIndex(i => i.UserId)
    .HasFilter("is_favorite = true")
    .IncludeProperties(i => i.StoryId)
    .HasDatabaseName("ix_user_story_interactions_user_id_favorite_incl_story");
```

**Note:** The method is `.IncludeProperties()` — this is an Npgsql extension method
(`NpgsqlIndexBuilderExtensions.IncludeProperties`). SQL Server has an identically-named method.
Do NOT use a bare `.Include()` on `IndexBuilder` — that method doesn't exist for index
configuration. See [layer6-indexes.md](layer6-indexes.md) for the full index strategy.

## Vertical Partitioning

Hot/warm/cold splits are deliberate — keep them. Don't merge a cold blob back into a hot table.

| Entity | Hot | Warm | Cold |
|---|---|---|---|
| Story | `Story` (~70 B) | `StoryListing` (~254 B) | `StoryDetail` (blob) |
| User | `User` (hot+warm) | — | `UserProfile` (ProfileText blob) |
| Recommendation | `Recommendation` | — | `RecommendationDetail` (Text blob) |
| UserStoryInteraction | `UserStoryInteraction` (filtering) | `UserStoryInteractionDate` (lists) | `UserStoryRecommendationSource` |

## Fluent API Organization

Use `IEntityTypeConfiguration<T>` classes, one per entity or cluster, with
`ApplyConfigurationsFromAssembly()` in `OnModelCreating`.

## Seed Data

Use `HasData()` with **anonymous types** (not entity instances) to prevent
`PendingModelChangesWarning`. Integer literals for `short` must be explicitly cast `(short)1`.
PKs must be non-zero for auto-increment tables.

## Migrations

**Pre-launch:** "nuke and rebuild" — delete Migrations folder, drop DB, regenerate
`InitialSchema`. **Post-launch:** incremental migrations only.

Key manual edits EF won't generate:
- **CHECK constraints:** `migrationBuilder.Sql(...)` in `Up()`, drop in `Down()`.
- **Triggers:** `CREATE TRIGGER` (PL/pgSQL) in `Up()`, `DROP TRIGGER` in `Down()`.
  `HasTrigger` Fluent API is SQL Server-specific — do not use.
- **Migration commands:** `--context ApplicationDbContext` required when two DbContext types exist.

Cache/data-mart tables (`UserStoryTreeSearchEntries`, `AlsoFavoritedScore`, `AlsoRecommendedScore`)
are **NOT in EF Core migrations** — managed by background workers via raw SQL.
