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

Both map the same schema. `ApplicationDbContext.OnModelCreating` calls `base.OnModelCreating(...)` then
`modelBuilder.ApplyConfigurationsFromAssembly(...)`. All entity shape/index/FK configuration lives in
`IEntityTypeConfiguration<T>` classes (location rules in §"Fluent API Organization" below).

`ReadOnlyApplicationDbContext.OnModelCreating` calls `base.OnModelCreating(modelBuilder)` first, then adds
the four named visibility/display query filters (`"ContentRating"`, `"GroupAudience"`, `"IsTakenDown"` ×4
roots). These filters don't touch schema and live on the read context **only** — the write context sees
ground truth with no filters. See `content-safety.md` "Content Rating Filtering" for the principle.

**Migrations are `ApplicationDbContext`-only.** `ReadOnlyApplicationDbContext` owns no migration history
(its `Migrations/ReadOnlyApplicationDb/` folder was deleted post-WU38 revamp). Always generate and apply
migrations against `ApplicationDbContext`. The read context picks up schema changes automatically
(it inherits the same model). Migration commands: `dotnet ef migrations add <Name> --context
ApplicationDbContext`.

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
`BaseBlogPost → {Profile, Group, Site}BlogPost`, `BasePoll → {Site, BlogPost}Poll`.
Base classes are `abstract`. Child FKs (e.g. `ChapterComment.ChapterId`) are **non-nullable** —
the NOT NULL guarantee is the point of choosing TPT.

**ChapterComment additional column:** `IsSpoiler` (bool, default false). Spoilers are a
chapter-discussion concept — NOT on `BaseComment`.

**Denormalization with TPT:** To land a sort/filter column on a TPT child table (enabling golden
indexes like `(chapter_id, date_posted DESC)` on `chapter_comments`), declare the property on
**each derived class** — not the base. A property declared on the base C# type always maps to the
base table in EF Core 10; a `builder.Property()` call on a derived entity for an *inherited*
property only sets facets (default value, conversion) — it does not relocate the column.
This corrects spec §4.3 line 839, which describes a "configure on derived to override base-table
mapping" technique that does not work in EF Core 10. The implemented pattern: remove the property
from the base class, declare it independently on each derived class, and provide Fluent config
(e.g. `.HasDefaultValueSql("CURRENT_TIMESTAMP")`) in each derived entity's config class.

**No down-navigations on TPT base classes:** A TPT base class must **not** declare reference
navigation properties pointing at its own derived types (e.g. `BaseComment.GroupComment`). EF Core
does not interpret these as TPT linkage — it materialises each as a separate optional 1-to-many
with the **base** table as the dependent, producing spurious nullable FK columns on the base table
pointing at the child tables. These phantom FKs create circular FK dependencies
(`base ↔ child` via the phantom FK, plus the correct child→base PK FK), which break Respawn's
table-deletion ordering and can prevent test-database resets from cleaning all tables.

The correct way to reach the concrete subtype from a base instance is to query the **typed child
`DbSet<T>`** (`DbSet<ChapterComment>`, `DbSet<GroupComment>`, etc.) — the child table is the
discriminator (spec §4.3). Reference: `BaseBlogPost` and `BasePoll` are the correct model — they
carry no navigations to `ProfileBlogPost`/`GroupBlogPost`/`SitePoll`/`BlogPostPoll`.

**Collection navigations TO a TPT child are typed to the child, not the base:** the sibling trap
on the *other* side of the relationship. Declaring `ICollection<BasePoll> Polls` on
`BaseBlogPost` prevented EF from pairing it with `BlogPostPoll.BlogPost` — it minted a second
shadow-FK relationship on `base_polls` that let a `SitePoll` point at a blog post. Type the
collection to the concrete child (`ICollection<BlogPostPoll>`) and pair it explicitly
(`HasOne(p => p.BlogPost).WithMany(b => b.Polls)`). Fixed in the WU-Polls L1 reconcile
(migration `WU_Polls_ConfigLifecycleAndShadowFkFix`, 2026-07-12).

**Unpaired collection navs mint shadow FKs — not only on TPT.** The same mechanism bites ordinary
entities: a collection navigation with no inverse reference on the other side makes EF invent a
nullable shadow FK column on the *dependent* table. `Recommendation.UserStoryInteractions` did exactly
that — it minted `user_story_interactions.recommendation_id` (indexed, FK'd, always NULL) with no
property behind it. It was a **fossil**, not a modelling slip: the 2025 DDL carried
`SourceRecommendationID INT NULL` directly on `UserStoryInteractions` before attribution moved to the
sparse `user_story_recommendation_sources` partition (a mostly-null column on a hot table for a feature
absent from its main filtering job). Removed with migration `WU_InertFeatures` (2026-09-30), which also
made the partition's recommendation FK explicit (`HasOne(SourceRecommendation).WithMany()`, cascade —
the rationale is in `layer2-services.md` §"Attribution (Feature 30)"). Check: every collection nav
either has its inverse named in a `WithMany(...)`/`WithOne(...)` call or is deleted; a snapshot column
with no C# property is the symptom.

**Cross-child casts in projections need a base-typed source:** a projection that branches across
sibling child types (`p is SitePoll && ((SitePoll)p).IsArchived`, `p is BlogPostPoll ?
((BlogPostPoll)p).BlogPostId : null`) only translates when the `IQueryable`'s **static element
type is the base**. Passing an `OfType<TChild>()` queryable into such a shared projection (legal
C# via `IQueryable<out T>` covariance) makes EF's expression preprocessor coerce the *other*
child's cast onto the child-typed parameter and throw `No coercion operator is defined between
types 'SitePoll' and 'BlogPostPoll'` at runtime. Filter with `Where(p => p is TChild)` (+
`((TChild)p).Column` predicates) instead of `OfType<TChild>()` whenever the result feeds a
base-typed shared projection. Reference: `ServerPollReadService` (found live in WU-Polls browser
verification, 2026-07-12; regression net: `PollServiceTests` list tests).

### Hard-deleting a content parent (owner ruling D10, WU-TptHardDelete 2026-09-30)

**No arrangement of `ON DELETE CASCADE` can make a content-parent delete reach the base rows.** A TPT
child row carries two FKs, and only one of them points the useful way:

| FK on `chapter_comments` | Direction | Effect of deleting the principal |
|---|---|---|
| `comment_id → base_comments` | base → child | Removes both rows. Correct; stays CASCADE. |
| `chapter_id → chapters` | parent → child | Would remove the child row only, orphaning its `base_comments` row. |

For `DELETE FROM chapters` to reach `base_comments`, the base table would need an FK to `chapters`. It
cannot have one: it is the polymorphic base shared by four parents, with no column to hang a
`chapter_id` on. Cascades flow along FKs, so the service layer **must** delete the base rows itself.
This is forced by the structure, not chosen. The same holds for every content parent that owns TPT
children.

**The content-parent → TPT-child FKs are `RESTRICT`.** A RESTRICT FK deletes nothing. It refuses a
parent delete while children exist, so a path that forgets the cleanup fails loudly instead of
orphaning base rows. It is the guardrail; the service cleanup is the repair. The five:
- `chapter_comments.chapter_id → chapters`
- `blog_post_comments.blog_post_id → base_blog_posts`
- `blog_post_polls.blog_post_id → base_blog_posts`
- `group_comments.group_id → groups`
- `group_blog_posts.group_id → groups`

`user_profile_comments.profile_user_id → AspNetUsers` was already RESTRICT; it is the precedent, not an
exception. **Not flipped, deliberately:**
- every base → child PK FK (`fk_*_base_comments_comment_id`, `fk_*_base_blog_posts_blog_post_id`,
  `fk_*_base_polls_poll_id`) — that direction is correct and is what the cleanup relies on;
- `base_comments.parent_comment_id`, which stays **SET NULL** (owner ruling D12): it is the reparent
  mechanism, so a reply that ever escaped its scope is reparented instead of blocking the delete;
- `poll_options`, `poll_votes` and the like junctions, which cascade off the deleted base rows.

**The cleanup is `TptDelete`** (`Server/Data/TptDelete.cs`): set-based raw SQL keyed off the child
table, one statement per child kind, run inside the caller's transaction —
`DELETE FROM base_comments WHERE comment_id IN (SELECT comment_id FROM chapter_comments WHERE chapter_id = @id)`.
- **Never LINQ.** `ExecuteDeleteAsync` is unsupported on a TPT base-type `DbSet`.
- **Never materialized.** Loading the children only to `RemoveRange` them (the pre-D10 chapter shape)
  costs a round-trip per row and is the shape three sites forgot to copy.
- Reply sets are closed under each parent scope (the `Post*CommentAsync` methods refuse a parent from
  another scope), so one statement per scope removes every reply too.
- **A future group-delete path must add a group-scope method** (`group_comments` and `group_blog_posts`
  plus each post's dependents). None exists today because nothing deletes a group (D47(b) is pending);
  until it is written, the RESTRICT FKs make such a path fail on its first group with children.

**No scheduled orphan sweep** (D10(d)). With RESTRICT in place an orphaned base row is a state the
database refuses to enter, so a sweep would poll for an impossibility. Whoever loosens RESTRICT
re-opens that question.

**TPH stays closed, on read-shape grounds.** Collapsing a hierarchy into one discriminated table would
make cascades sufficient, but the base/child split is load-bearing for the warm/cold vertical partition:
the narrow base table serves the polymorphic scans, the wide child tables the detail reads. Do not
propose it again on cascade grounds. The service rule (callers, transaction order, per-subtype
lifecycle methods) is in `layer2-services.md` §"Hard deletes of content parents".

## Enum / Lookup Table Decision Framework

| Pattern | When | Examples |
|---|---|---|
| **Magic enum** (no table) | Tiny, stable, app-coupled, no display name | `Rating`, `ReportedEntityType`, `CharacterPairingType`, `ProfileVisibility` |
| **Lookup table** (no enum) | Content-only display; rename/add without deploy | `ReportReason`, `AcknowledgmentRole`, `StoryLineageType`, `Theme` |
| **Hybrid** (table + enum with `...Enum` suffix) | Both flexible display AND rigid C# logic | `StoryStatusEnum`, `ReportStatusEnum`, `NotificationCategoryEnum`, `NotificationTypeEnum` |
| **String key** (string PK) | Tiny table; key used directly in C# | `SearchMode.SearchModeKey`, `Badge.BadgeKey`, `UserInteractionFilter.InteractionFilterKey`, `SiteSetting.SettingKey` (mod-editable runtime knobs — value stored as string, typed in `ISiteSettingsService`; see `layer2-services.md` §"Site Settings") |

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
- **Theme.Slug** (`[Required][MaxLength(64)]`, unique index) — URL-safe identifier for a theme (e.g.
  `"pokemon"`). Distinct from `Theme.Name` (display-only, e.g. `"Pokémon"`). The slug is the value
  baked into the `canalave:theme` claim and used as the path segment in sprite URLs. Both columns must
  exist; neither substitutes for the other.
- **Booleans:** `NOT NULL DEFAULT false` (1 byte each in PostgreSQL — no bit-packing, accepted trade-off).
  **A bool that defaults to `true`** (first: `User.CanAutoApprove`, WU-StoryLifecycle 2026-09-30) is
  written `HasDefaultValue(true).HasSentinel(true)` plus a `= true` CLR initializer. EF omits a property
  whose value equals its **sentinel** from the INSERT and lets the DB default fill it, so the sentinel
  must be `true`: a `false` sentinel would silently turn an explicit `false` into `true`. EF Core 10
  already infers `Sentinel = true` from `HasDefaultValue(true)` — probed 2026-09-30 (WU-StoryLifecycle
  review fixes): with `HasSentinel` removed the property still reports `Sentinel == true` and an explicit
  `false` still inserts — so `HasSentinel(true)` is an explicit statement of intent, not the mechanism.
  **The initializer is the load-bearing half:** with the sentinel at `true`, a `new User()` without it
  carries `false` ≠ sentinel and INSERTs an explicit `false`. Both halves are pinned in Integration
  (`ModerationServiceTests.NewUser_DefaultsToCanAutoApprove_WithNoApprovals` — mutation-checked against
  removing the initializer — and `NewUser_InsertedWithCanAutoApproveFalse_KeepsFalse`). The DB default
  itself is required whenever a raw COPY/INSERT path (SeedTool) omits the column.
- **`User.CreatedUtc`** (`datetime`, `.HasDefaultValueSql("CURRENT_TIMESTAMP")`) and
  **`User.LastActiveUtc`** (`datetime?`, nullable) — added WU-SiteDailyStat (2026-07-10).
  `CreatedUtc` sources Feature 62's `new_users`/`total_users`; pre-existing rows backfill to the
  migration's deploy date (real registration date is unrecoverable). `LastActiveUtc` is stamped
  for **authenticated requests only** via the Signal Buffering pattern
  (`layer2-services.md` §"Signal Buffering") — never a tracked per-request write — and sources
  `active_users` + the profile "last seen on" display (gated by the pre-existing
  `PrivacySettings.ShowActivityStatus`). Full reasoning: `layer8-data-marts.md` §`site_daily_stats`.
- **`SiteDailyStat`** (PK `stat_date`) is Layer 8's one EF-modeled mart table — an append-only
  ground-truth time-series, not a rebuildable mart; schema/reads go through EF, writes are raw SQL
  by the daily worker. See `layer8-data-marts.md` §`site_daily_stats` for why this table alone
  breaks L8's "no EF model" rule.
- **Per-story tag overlay pair (WU-TagFanon, 2026-07-26):** `CustomName` (`[MaxLength(128)]`,
  nullable, gated by `Tag.AllowCustomName`) + `Nuance` (`[MaxLength(2048)]`, nullable, ungated,
  plain text — not sanitized HTML, not FTS-indexed). Lives on `StoryTag` (0-or-1 per junction row)
  and on `StoryCharacter` (1-to-many per base tag: `UNIQUE (StoryId, CharacterTagId, CustomName)`
  NULLS NOT DISTINCT; non-null `CustomName` requires `IsOc`). Never name this concept "identity" —
  that word is ASP.NET Core Identity's. `SettingDetail` was deleted by the same WU (cardinality
  rule: a 0-or-1 overlay belongs on the junction row; a separate table needs 1-to-many or
  referencing rows — `StoryCharacter` has both, via pairings).

## Relationships & Queries

- **Delete behavior is always explicit** — `.OnDelete(DeleteBehavior.X)` on every relationship.
- **A content owner FK is nullable + `SET NULL`** (the delete policy in `cross-cutting.md`): stories,
  comments, blog posts, recommendations and — since owner ruling D11 (WU-TptHardDelete, 2026-09-30) —
  polls (`base_polls.owner_id`). A NULL owner means "nobody": every ownership check must fail for it.
  Compare with a pattern (`poll.OwnerId is int ownerId && viewerId == ownerId`), never `==`/`!=` on two
  `int?` values — C# treats `null == null` as true, so an anonymous caller (`UserId` null) would "own"
  every ownerless row.
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
`AuthorSettings` as jsonb columns — all three use the EF Core 10 complex type mapping
(`ComplexProperty(...).ToJson()` ×3 in `Server/Data/Configurations/IdentityConfigurations.cs`),
matching the example above. The complex type approach is the standing rule for new JSON columns.

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

One `IEntityTypeConfiguration<T>` class per entity, named `{Entity}Configuration`. Files are grouped
**one per folder-cluster** (per `folder_clusters.md`'s `Lookups/`, `Stories/`, `Identity/`, etc.), each
file holding the config classes for that cluster's entities — e.g. `StoryConfigurations.cs` contains
`StoryConfiguration`, `StoryListingConfiguration`, `StoryDetailConfiguration`, `SeriesConfiguration`, etc.

**All config files are colocated in `TheCanalaveLibrary.Server/Data/Configurations/`** — *not* in the
feature cluster folders, even though the files are grouped by cluster name. This is deliberately
**unlike** service implementations (`Server{Feature}ReadService`, etc.), which live in their cluster
folders to optimize per-feature edit-locality. EF configuration is a different kind of artifact: it's one
cross-cluster *graph* — foreign keys, delete behaviors, the diamond-breaking `SetNull`s — that is edited at
*migration time*, not per-feature. Keeping it in one location preserves the ability to reason about the
whole delete-cascade graph at once, which is the dominant activity when touching this code. Scattering it
into cluster folders would force a tree-walk to answer "what happens to X's children when X is deleted?"

`OnModelCreating` is reduced to:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder); // first — sets up the Identity model
    modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
}
```

**Relationship ownership:** each relationship is configured exactly **once** — on whichever side groups
it with the principal/aggregate-root's delete-policy reasoning (e.g. `User`'s `Configure` declares its
`HasMany(...).OnDelete(...)` for owned/authored content). Never configure the same relationship from both
sides.

**Seed data placement:** each entity's `HasData(...)` call lives inside that entity's own
`{Entity}Configuration.Configure` method (see "Seed Data" below for the `HasData` rules themselves).

## Seed Data

Use `HasData()` with **anonymous types** (not entity instances) to prevent
`PendingModelChangesWarning`. Integer literals for `short` must be explicitly cast `(short)1`.
PKs must be non-zero for auto-increment tables.

## Migrations

**Pre-launch:** "nuke and rebuild" — delete Migrations folder, drop DB, regenerate
`InitialSchema`. **Post-launch:** incremental migrations only.

**Re-append the non-model DDL after every regeneration.** `ef migrations add InitialSchema`
emits only what the EF *model* knows; the raw-DDL items below live in no model and are **silently
dropped** by regeneration. After regenerating, re-append them by hand to the end of `Up()` (and their
teardown to the start of `Down()`) — copy from the prior `InitialSchema` before deleting it. The
current set (folded in at the 2026-07-18 migration collapse; originals were migrations
`R2_ViewCountToDailyStoryStats` and `R4_MvccStorageTuning`):
- **`daily_story_stats`** — migration-managed ground-truth stat table, deliberately outside the EF
  model (accumulated, not a rebuildable L8 mart): raw `CREATE TABLE` + `COMMENT ON TABLE`. Ordered
  before the MVCC block, which tunes it too.
- **MVCC storage tuning** — `ALTER TABLE … SET (fillfactor / autovacuum_vacuum_scale_factor)` on
  `user_chapter_interactions`, `daily_story_stats`, `user_story_interactions`.

Verify a collapse preserved everything with a schema diff: `pg_dump --schema-only` before and after
the regeneration must differ only in `__EFMigrationsHistory` row contents.

Key manual edits EF won't generate (re-append per the rule above if ever introduced):
- **CHECK constraints:** `migrationBuilder.Sql(...)` in `Up()`, drop in `Down()`. *(none present)*
- **Triggers:** `CREATE TRIGGER` (PL/pgSQL) in `Up()`, `DROP TRIGGER` in `Down()`.
  `HasTrigger` Fluent API is SQL Server-specific — do not use. *(none present)*
- **Migration commands:** always `--context ApplicationDbContext` (the read context owns no migrations).

Cache/data-mart tables (`UserStoryTreeSearchEntries`, `AlsoFavoritedScore`, `AlsoRecommendedScore`)
are **NOT in EF Core migrations** — managed by background workers via raw SQL.
