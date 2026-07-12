# Layer 2 — Services

The service layer is the firewall between UI and EF Core. Interfaces and DTOs live in **Core**;
server impls in **Server**; HTTP impls in **Client**.

## CQRS-Lite with Inheritance

Every feature cluster gets two interfaces in Core. The write interface inherits the read interface:

```csharp
// Core
public interface IStoryReadService
{
    Task<StoryListingDto[]> GetListingsAsync(StoryFilterDto filter);
    Task<StoryDetailDto?> GetDetailAsync(int storyId);
    Task<StoryListingDto[]> GetListingsByIdsAsync(int[] storyIds); // building-block method
}

public interface IStoryWriteService : IStoryReadService
{
    Task UpdateTitleAsync(int storyId, string newTitle);
    Task SetHiddenGemAsync(int recommendationId, bool isHiddenGem);
}
```

Razor components inject the *narrowest* applicable interface: a story viewer injects
`IStoryReadService`; the story editor injects `IStoryWriteService`. Least-privilege at the type level.

## Server Implementation — Compile-Time DbContext Safety

```csharp
public class ServerStoryReadService(ReadOnlyApplicationDbContext readDb) : IStoryReadService
{
    // readDb is private — invisible to derived classes
}

public class ServerStoryWriteService(ReadOnlyApplicationDbContext readDb, ApplicationDbContext writeDb)
    : ServerStoryReadService(readDb), IStoryWriteService
{
    // readDb forwarded to base, not stored here
    // writeDb is this class's only DbContext field
}
```

Read methods can't accidentally use the write context; write methods can't accidentally hit the
read replica. Misuse requires a visible, reviewable act.

### CS9107/CS9124: shared constructor parameters in inherited primary-constructor pairs

When the read and write service use C# primary constructors and the write service passes a shared
parameter (e.g. `IActiveUserContext activeUser`) to the base constructor, the compiler emits
CS9107 ("parameter captured in the derived class is also passed to the base constructor") and
CS9124 (if the base class also captures it in both a field/property and the constructor itself).
The fix is a `protected` property on the base class initialised from the constructor parameter:

```csharp
// Base read service — exposes the shared dep as a protected property
public class ServerChapterReadService(
    ReadOnlyApplicationDbContext readDb,
    IActiveUserContext activeUser) : IChapterReadService
{
    // Initialiser-only property breaks the double-capture: the compiler sees one owner.
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    public async Task<ChapterReadingDto?> GetChapterForReadingAsync(...)
    {
        Rating ceiling = ActiveUser.ShowMatureContent ? Rating.M : Rating.T; // use the property
        ...
    }
}

// Derived write service — uses ActiveUser (property), never activeUser (parameter)
public class ServerChapterWriteService(
    ReadOnlyApplicationDbContext readDb,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IHtmlSanitizationService sanitizer)
    : ServerChapterReadService(readDb, activeUser), IChapterWriteService
{
    public async Task<int> CreateChapterAsync(CreateChapterDto dto)
    {
        var contentRow = new ChapterContent { AuthorId = ActiveUser.UserId, ... }; // not activeUser.UserId
        ...
    }
}
```

The rule is: **the base class owns the shared dep via its property; the derived class uses the
property, never the constructor parameter.** Referencing `activeUser.X` in the derived class body
re-triggers CS9107 because `activeUser` is now captured in two scopes.

**DI registration:**
```csharp
builder.Services.AddScoped<IStoryReadService, ServerStoryReadService>();
builder.Services.AddScoped<IStoryWriteService, ServerStoryWriteService>();
```

### DbContext Registration: Plain `AddDbContext`, Never `AddNpgsqlDbContext`/Pooled

Settled WU12 (`forward_plan.md` "Aspire orchestration during MVP dev" — narrower correction): register
both DbContexts with the plain EF Core API, never the `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`
package's `AddNpgsqlDbContext<T>` helper:

```csharp
string connectionString = builder.Configuration.GetConnectionString("canalavedb")!;
builder.Services.AddDbContext<ApplicationDbContext>(options => options
    .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
    .UseSnakeCaseNamingConvention());
// Read context registers a SCOPED factory, not AddDbContext — see the next section.
builder.Services.AddDbContextFactory<ReadOnlyApplicationDbContext>(options => options
    .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
    .UseSnakeCaseNamingConvention()
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking),
    ServiceLifetime.Scoped);
```

**Why:** `AddNpgsqlDbContext<T>` always registers via EF Core's `DbContextPool` — its settings type has
no pooling opt-out (confirmed against the package source). Pooled contexts are constructed from the
*root* service provider (instances are rented/returned across scopes, not built per-scope), so they
cannot take a Scoped constructor dependency. `ApplicationDbContext` takes `IActiveUserContext` (Scoped)
for the content-rating filter — pooling and that dependency are simply incompatible, and this also
contradicts spec §6.6's resolved "plain `AddScoped<>`, DI manages DbContext lifetime" decision (pooling
is a *stronger*, different lifetime model than "one instance per scope"). `EnableRetryOnFailure()` is
written explicitly to preserve the resilience behavior the Aspire helper used to provide for free (see
WU0's audit note on a retrying-execution-strategy/manual-transaction interaction `UserDeletionService`
already had to account for — retries are relied upon, not optional).

This is unrelated to the Aspire *orchestration* question (AppHost, deferred post-MVP) — that's
genuinely additive/swappable dev infra; this is a composition-root lifetime choice every
DbContext-consuming service is written against, architectural in the same sense `IActiveUserContext`
is. It's also unrelated to the Postgres primary/read-replica axis: which connection string a context
points at is orthogonal to whether the .NET-side object is pooled.

### Read-Context Concurrency: Factory Per Method (supersedes spec §6.6)

**Every read-service method creates its own short-lived `ReadOnlyApplicationDbContext` from a
scoped `IDbContextFactory<ReadOnlyApplicationDbContext>` — never holds one for the service's
lifetime** (settled 2026-07-01, found via browser debugging; regression net:
`Tests.Integration/ConcurrentReadAccessTests.cs`):

```csharp
public class ServerStoryReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IStoryReadService
{
    public async Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        return await readDb.Stories ...;   // method body otherwise unchanged
    }
}
```

**Why:** in Blazor Server the DI scope is per-*circuit*, not per-request, and sibling components'
async initialization interleaves. Layout chrome (`NotificationBell`, `MessagesNavLink`) queries
concurrently with every page dispatcher's load, and pages themselves parallel-load via
`Task.WhenAll` (ChapterReadingPage, SettingsPage, GroupPage, NotificationsPage). A single
circuit-scoped context instance shared by all of them throws
`InvalidOperationException: A second operation was started on this context instance` on the first
authenticated page load. `DbContext` is not concurrency-safe; the container manages *lifetime*, not
*concurrency*. This is EF's documented Blazor Server pattern.

**This supersedes spec §6.6** ("Why Direct DbContext Injection over IDbContextFactory"). §6.6's
claim that "with scoped registration the thread-safety concern doesn't apply" is true for
per-request scopes and false for per-circuit scopes — the app is `InteractiveServer`, so circuits
are the operative model. What §6.6 actually valued survives intact:
- **Compile-time read/write separation** — write services still hold only `writeDb`; read access
  is a method-local `await using` variable.
- **Scoped dependencies** — `ServiceLifetime.Scoped` on the factory (NOT the default Singleton)
  makes factory-created contexts resolve the circuit's scoped `IActiveUserContext` for the named
  query filters. This is the same scoped-deps constraint that rules out pooling above.

Mechanics:
- **Local name stays `readDb`** so method bodies read identically to the old pattern.
- **Expression-bodied query methods become block-bodied** — returning a bare `Task` would dispose
  the context before the query streams (same lifetime pitfall as `testing.md` §"async methods that
  create a scope must await the call inside it").
- **Private/protected helpers that query take the context as a parameter** when called inside a
  method that already opened one (`BatchLoadEntitiesAsync`, `BatchLoadTargetsAsync`), or open
  their own when standalone (`BuildFolderTreeAsync`).
- **Base/derived (CS9107):** the base read service exposes
  `protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; }`; derived
  write services use it for their read-side lookups.
- **The write context stays plain `AddDbContext` (scoped).** Writes are triggered by discrete user
  actions, effectively serialized per circuit, and a scoped `ApplicationDbContext` never collides
  with factory-created read contexts (different instances). Revisit only if a real write-vs-write
  interleaving surfaces.
- **Non-circuit consumers may still inject `ReadOnlyApplicationDbContext` directly**
  (`ApplicationUserClaimsPrincipalFactory` — sign-in request scope, no concurrency):
  `AddDbContextFactory` also registers the context type itself as a scoped service.
- Component-side corollary: `Task.WhenAll` parallel loading in pages/components is *sanctioned* by
  this pattern — do not sequentialize awaits to dodge context sharing.

### Content-Rating Filtering Lives on the Read DbContext, Not in Each Service

Read services do **not** add a `.Where(s => s.Rating <= ...)` clause themselves. The ceiling is a global
EF Core named query filter on `Story` (and `GroupAudience` on `Group`, `IsTakenDown` on four roots),
sourced from `IActiveUserContext` and registered in `ReadOnlyApplicationDbContext.OnModelCreating` only
(post-WU38 revamp — write context is unfiltered by design). See `content-safety.md` "Content Rating
Filtering" for the principle and mechanism (model invariant vs. per-method
vigilance). A read service projecting `Story` rows gets the filter automatically; it never re-derives
it. The two cases where a service deliberately bypasses it:
1. **Mod/admin/author read paths** that must surface content regardless of rating — call
   `.IgnoreQueryFilters(["ContentRating"])` explicitly.
2. **Any write-service entity lookup by ID** — e.g. `AddStoryAsync`, `SubmitAsync` (Recommendation).
   A user should be able to recommend or add an M-rated story to a group even if their own
   `ShowMatureContent` is false. The *caller's viewer settings* must not prevent the service from
   confirming the entity exists; the downstream business rule (rating-ceiling check, "story not found"
   error) applies after the lookup. Always use `.IgnoreQueryFilters(["ContentRating"])` when
   fetching a specific entity by primary key inside a write path. Omitting it causes the service to
   throw `KeyNotFoundException` for entities that exist but are filtered, a silent mismatch that is
   hard to diagnose.

## Scalar projections on nullable FK columns — use anonymous-type, not `(int?)`

When a write service needs to read a single nullable FK column from a row (e.g. `Story.AuthorId`
to gate a counter update) and also wants to distinguish "row exists, column is null" from "row does
not exist at all", **project to an anonymous reference type**, not to `(int?)`:

```csharp
// WRONG — FirstOrDefault<int?> returns null for both "row not found" and "AuthorId IS NULL".
int? authorId = await writeDb.Stories
    .Where(s => s.StoryId == id)
    .Select(s => (int?)s.AuthorId)
    .FirstOrDefaultAsync();
if (authorId is null) throw new KeyNotFoundException(...); // fires even for authorless stories!

// CORRECT — reference-type result is null only when no row exists.
var row = await writeDb.Stories
    .IgnoreQueryFilters(["ContentRating"])
    .Where(s => s.StoryId == id)
    .Select(s => new { s.AuthorId })
    .FirstOrDefaultAsync();
if (row is null) throw new KeyNotFoundException(...);
int? authorId = row.AuthorId; // may still be null (authorless story) — guard before .Value
```

Also guard any downstream `.Value` access on `authorId` — authorless stories are valid (`AuthorId`
may be null even when the story exists).

## The DTO Firewall (Non-Negotiable)

UI (Razor components) **NEVER** sees full EF Core model classes — only DTOs and service interfaces.

- DTOs and ViewModels live in **Core** so both server and client share them.
- The boundary is the service method signature: entities behind the service, DTOs out.
- No DTO inheritance. Write and read DTOs are separate classes.

## Query Path (Reads, ~90%)

Use `ReadOnlyApplicationDbContext` (`NoTracking`) and project straight to DTOs with `.Select()`:

```csharp
public Task<StoryListingDto[]> GetListingsAsync(StoryFilterDto filter) =>
    readDb.Stories
        .Where(s => s.Status == StoryStatusEnum.Ongoing)
        .Select(s => new StoryListingDto(s.Id, s.Title, s.CoverArtRelativeUrl, s.WordCount))
        .ToArrayAsync();
```

**Avoid:** materializing entities then mapping, or returning entities and projecting in the component.

## Command Path (Writes, ~10%)

Use `ApplicationDbContext` with tracked entities. Load → mutate → save:

```csharp
public async Task UpdateTitleAsync(int storyId, string newTitle)
{
    var story = await writeDb.Stories.FindAsync(storyId);
    if (story is null) return;
    story.Title = newTitle;
    await writeDb.SaveChangesAsync();
}
```

Durable user intent — including Favorite/Follow/Ignore toggles — always takes this direct path
(the old "Redis write-behind for interactions" plan was a SQL-Server-era artifact: its
protect-reads-from-write-locks rationale is void under Postgres MVCC, and the 2s client debounce
already absorbs the churn). Only **loss-tolerant, coalescable signals** are buffered — see
"Signal Buffering" below.

## Signal Buffering — in-process write buffers for loss-tolerant signals

The L2 body pattern for **high-frequency · loss-tolerant · coalescable** writes (reading-progress
pings, view pings, `User.LastActiveUtc` stamps — WU-SiteDailyStat, feeds Feature 62's
`active_users`/"last seen" — see `layer8-data-marts.md` §`site_daily_stats`). All three criteria
must hold — durable intent (interactions, comments,
content) never buffers. The signal lands in an in-process coalescing store instead of the
database; a worker batch-flushes on a fixed cadence. Behavior is identical to direct writes
within the loss window (one flush interval; hard crash loses at most that).

**The four pieces per signal** (canonical pair: `ReadingProgress*` in `Server/Chapters/`,
`ViewCount*` in `Server/Stories/`; a third, `LastActive*` in `Server/Identity/`, follows the same
shape keyed per-user with a latest-timestamp merge — see `layer8-data-marts.md`):

1. **Buffer** — singleton `ConcurrentDictionary` keyed per signal identity, merged O(1) in
   `Record(...)` (max+latest for progress and last-active; sum for views). `Drain()` removes-and-returns all
   entries (a racing ping lands in this batch or the next — never lost); `Restore(batch)` merges
   a failed flush back for retry; `Clear()` is test-only. The constructor registers a buffer-depth
   `ObservableGauge` on the feature's `CanalaveTelemetry` meter.
2. **Flusher** — singleton taking the buffer + `IServiceScopeFactory` (fresh scope per flush —
   never capture a scoped DbContext in a singleton). One batched raw-SQL upsert:
   `unnest(@arrays…) … ON CONFLICT DO UPDATE` — the Postgres replacement for SQL-Server TVP+MERGE
   (`MERGE` itself would demand PG 15+; `unnest`+`ON CONFLICT` needs nothing). Guard each batch
   with `WHERE EXISTS` on FK parents so one mid-window-deleted row can't fail the batch. Prefer
   idempotent merge functions (`GREATEST`, `OR`) so `EnableRetryOnFailure` replays are safe;
   an additive `+=` (view counts) accepts a rare replay over-count — say so in a comment.
   Records flush batch-size + duration histograms; on failure: `Restore`, log, rethrow.
3. **Worker** — `BackgroundService` + `PeriodicTimer` (5 s), delegating to the flusher; catches and
   logs so the loop survives a failed cycle; **drains once more after cancellation** (graceful
   shutdown must not eat the loss window). Singleton-worker discipline: exactly one per process.
4. **Scoped write service** — the unchanged `I{Feature}WriteService` body becomes
   `buffer.Record(...)`. The interface doc states the honest contract: *eventually-durable,
   may lose the last flush interval, not read-your-own-write*.

**Testing** (`testing.md` tiers): the buffer's merge semantics are Unit tests (direct
construction). The flush path is Integration — `TestAppFactory` **removes the timer workers**
(they'd race the Respawn reset); tests call `flusher.FlushAsync()` deterministically. Never
assert on timer behavior.

**N≥2 seam:** at more than one web node, each in-process buffer swaps for a shared RESP store
behind the same interface — body swap only, no interface/caller/UI/schema change. Until that day,
the in-process body is strictly better (no network hop, no dependency). Full detail (why a shared
store is needed, what it swaps to, load-balancer session affinity, why no SignalR backplane is
needed): `horizontal-scaling.md`.

## DTO Strategy: Partition-Anchored

Default: one DTO record per vertical-partition table. `StoryListingDto` ≈ columns of `StoryListing`.

For cross-partition needs (a card needing `StoryListing` fields + `IsFavorite`):
separate fetches, merge in C# at the call site. A dedicated composite DTO is a deliberate exception.

| Return / param shape | Use |
|---|---|
| Read operation result | **DTO** (record) |
| Simple write (1–2 params) | **Primitives** — `UpdateTitleAsync(int storyId, string newTitle)` |
| Complex write (3+ params) | **DTO** |
| 2–3 property read return | **ValueTuple** acceptable — `Task<(int Words, int Chapters)>` |

Prefer `record` types for DTOs (value equality, concise, immutable):

```csharp
public record StoryListingDto(int Id, string Title, string? CoverArtRelativeUrl, int WordCount);
```

### Id-Batch Parameters Use `IReadOnlyList<T>`, Never `List<T>`

Settled WU12 (`GetListingsByIdsAsync`, spec §6.6's building-block pattern): when a parameter is a
read-only batch of ids (or any opaque values the method will only enumerate/`.Contains()`-check, never
mutate or grow), declare it `IReadOnlyList<T>` — not `List<T>`, not `T[]`.

- **Never `List<T>` in a public signature.** Microsoft's Framework Design Guidelines say this flatly,
  not as a preference: `List<T>` is a concrete, mutable implementation type. Requiring it forces every
  caller to materialize that *specific* class even when they're holding an array, a `Span`, or any other
  `IEnumerable<T>` — and it exposes `Add`/`Remove`/`Capacity` the method has no business calling.
- **`IReadOnlyList<T>` beats `T[]` too**, for the same reason one level up: an array is still one
  concrete type. `IReadOnlyList<T>` accepts an array, a `List<T>`, an `ImmutableArray<T>` — anything —
  with zero copying at the call site, while still giving the method `Count`/indexed access. It says
  exactly what's true ("read-only, indexable, known count") and nothing more.
- EF Core translates `someParam.Contains(x)` (via `Enumerable.Contains`) into a SQL `IN (...)` the same
  way regardless of the parameter's declared collection-interface type — this costs nothing in the
  `.Where(s => storyIds.Contains(s.StoryId))` pattern.
- The natural *producer* of an id batch is usually `.Select(x => x.Id).ToArrayAsync()` — that array
  satisfies `IReadOnlyList<T>` with no allocation, so this rule never costs a caller anything.

### Sprite URLs Are Resolved At Render Time, In the Component

Display DTOs that include a sprite (`TagChipDto.SpriteIdentifier`, and any future sprite-bearing DTO)
carry the **raw `SpriteIdentifier` key** — not a resolved URL. Resolution happens **in the rendering
component** via two injected/cascaded values:

1. **`[CascadingParameter] ThemeContext`** — a `record ThemeContext(string Slug, bool PrefersAnimated)`
   cascaded from a root `ThemeContextProvider` component (see `render-and-layout.md` "ThemeContext
   Cascading Provider"). The provider reads `canalave:theme` and `canalave:prefers_animated_sprites`
   claims off the cascaded `ClaimsPrincipal`; those claims are present in both the prerender and
   interactive passes, so the resolved `<img src>` is byte-identical across the SSR→interactive
   handoff — **no flicker**.
2. **`@inject ISpriteReadService`** — a render-pure URL builder with a single method
   `GetSpriteUrl(string slug, string id, bool prefersAnimated)` producing
   `{SpriteBaseUrl}/{slug}/{static|animated}/{id}.{ext}`. **SharedUI components may inject
   `ISpriteReadService`** — it is not `IActiveUserContext`. `ISpriteReadService` is implemented in
   Core (no server/host dependency), registered on both server and client; the
   `IActiveUserContext`-never-in-SharedUI rule is unchanged.

Components resolve: `id is null ? null : Sprites.GetSpriteUrl(ctx.Slug, id, ctx.PrefersAnimated)` and
render `<img src>` with a plain-HTML `onerror` fallback chain (`webp → static .png → unknown.png`).

**`ISpriteAssetProbe` is server-only** (`Core/Sprites/ISpriteAssetProbe.cs`, `ExistsAsync(slug, id)`)
— used only in `ServerTagWriteService` to validate a sprite identifier exists on disk/R2 at mod-write
time, returning a **non-blocking warning** (the save still succeeds). It is **never injected** by render
components. Render-time misses are handled by the `onerror` chain, not the probe.

**Why DTOs carry the identifier, not the resolved URL:** the resolved URL depends on the requesting
viewer's theme and animation preference. Carrying `SpriteIdentifier` keeps the DTO per-content (the
same DTO is valid for all viewers with the same content), and places the per-viewer computation at the
correct layer (render time). The DTO is therefore freely cacheable across users of the same content.

### Saved Tag Selections Persist Only the Tag Axis (WU43, Feature 15)

A `SavedTagSelection` (`Core/Tags/`) exists solely to populate the tag include/exclude axis of a
discovery filter — it is **not** a saved query. It deliberately excludes everything else
`StoryFilterDto` carries:

- **Free-text search / sort order** — transient viewer intent for a single visit, not something worth
  naming and reusing.
- **Interaction exclusions** — already have their own persistence mechanism,
  `UserStoryInteractionFilterSetting` (a sparse per-`(User × SearchMode × filter-kind)` override of
  `DefaultUserStoryInteractionFilterSetting`, merged into `StoryFilterDto.ExcludedInteractions` by
  `IDiscoveryDefaultsReadService`). Duplicating that into Saved Tag Selections would create two
  competing sources of truth for the same per-user setting.
- **AND/OR include-mode** — a per-request toggle on the include axis (`TagFilter.AllowIncludeModeToggle`),
  not part of the saved combination.

**One unified selection spans every tag type.** `TagFilter` renders one `TagSelector` per
`TagTypeEnum` purely as a type-scoped typeahead input surface — that per-type split is not a data
boundary. Its `EmitAsync` already flattens every type's picks into one pair of id-lists
(`TagFilterSelection.IncludedTagIds`/`ExcludedTagIds`), and `StoryFilterDto` carries no per-type
grouping. `SavedTagSelectionEntry` is correspondingly a **flat `(TagId, IsExcluded)` row** — each tag's
type is recovered from its own `Tag` row when hydrating chips for display/apply. A per-type saved
selection would fragment the very combination the feature exists to preserve, and has no backing in
either the filter DTO or the entity.

**Load and Save are separate UI surfaces**, both mounted once in `TagFilter`'s header (so every
`ResultsFilterPanel` consumer — `/discover`, Tree Search, Bookshelves, Profile story tabs — gets them
without per-surface wiring): a searchable/sortable **`SavedTagSelectionLoadFlyout`** (destructively
replaces the on-screen tag selection; owner-gated ⋯ row menu for overwrite/rename/publish/delete) and a
separate compact **`SavedTagSelectionSaveDialog`** (captures the current tags as a new selection). They
are not combined into one component — Load and Save are opposite operations (overwrite vs. capture), and
folding a state-mutating save form into the same list that exists to overwrite that state was assessed
as unneeded fragility, not a simplification.

**Sharing is copy-on-write, not subscription.** `SavedTagSelection.IsPublic=true` surfaces a selection on
the owner's `ProfileTab.TagSelections` tab only — there is no public browse/gallery surface. Another
viewer's "Add to my filters" (`ISavedTagSelectionWriteService.CopyPublicSelectionAsync`) creates a new,
independently-owned `SavedTagSelection` + copied `SavedTagSelectionEntry` rows; the copy and the source
never affect each other afterward (no many-to-many "subscription" model — rejected because editing a
shared row would silently change it for every subscriber).

**`SpriteBaseUrl` is a config seam** (`appsettings` key `Sprites:BaseUrl`, default `/sprites/themes`
for wwwroot). At R2/CDN time, changing this one config value — together with an Rclone sync of the
assets — is the complete cutover; no code changes. This is the same public-asset-base seam
`IImageStorageService` will adopt when `S3ImageStorageService` lands; the two features converge on
one base-URL config and one CDN but do **not** share a storage service (sprites have no runtime
write path — assets are provisioned out-of-band via Rclone).

**Avatars are a related but distinct case (settled WU10):** `UserCardDto.AvatarUrl` is *not* produced
by `ISpriteReadService` — it's the read service copying `User.ProfilePictureRelativeUrl` (a
user-uploaded blob path stored verbatim on the entity) into the DTO, or substituting a service-chosen
default when null. No theme/animation resolution is involved; the DTO carries the resolved URL
directly. See `layer4-style.md` §"Avatars Are Stored URLs, Not Sprite Keys".

**Cover art is the same pattern as avatars, produced by a different write-side source (settled WU12):**
`StoryListingDto.CoverArtRelativeUrl` is also copied verbatim, never resolved through
`ISpriteReadService`. The difference from sprites/tags is *how the relative path got there in the first
place* — `IImageStorageService.SaveAsync` (Core/Images/) is the write-side counterpart that turns an
uploaded file into the relative key stored on the entity. `LocalImageStorageService` (MVP) writes under
`wwwroot/uploads/`; the interface is the seam for the Post-MVP `S3ImageStorageService` swap (Garage/R2).
See `audit/ImageStorage.md` for the full contract and URL conventions.

### User HTML Is Sanitized Once, On Save — Never On Display

Any write path that accepts user-authored rich text (chapters, **vouch text**, comments, recommendations,
blog posts, profile bios, messages — everywhere `EditorView` is used) runs it through `HtmlSanitizer`'s
allow-list
(§3.21) **in the write service, before persisting.** Stored HTML is therefore already trusted.
`RichTextView` (the universal display leaf, see `layer3.5-structure.md` "Universal Components") renders
that stored HTML directly via `MarkupString` and performs **no sanitization of its own** — it isn't a
service, doesn't inject one, and re-sanitizing on every render would be redundant work duplicated across
every display site. If a future write path produces HTML that bypasses the allow-list step, that's a
bug in that write service, not something `RichTextView` should compensate for.

**The allow-list is the inverse of the toolbar.** What `EditorView`'s toolbar can produce is exactly
what the sanitizer must permit — the two are one contract, not two independently-maintained lists.
Minted together in WU6: `IHtmlSanitizationService` (`Core/RichText/`) /
`ServerHtmlSanitizationService` (`Server/RichText/`, wraps a configured `HtmlSanitizer`, registered
`AddSingleton` — config is immutable and thread-safe) permits exactly `p, br, strong, em, u, s, h2, h3,
blockquote, ul, ol, li, a` (+ `a[href]` with safe schemes, normalized `rel`/`target`) — no `style`,
`class`, `id`, script, or event-handler attributes beyond what the toolbar emits. Every write service
that persists `EditorView` output injects `IHtmlSanitizationService` and calls it before persisting;
if the toolbar ever gains a button, extend the allow-list in the same change.

### Word Count Is Computed Server-Side, On Save — From Stripped Text

Any write path that persists a content body with a `WordCount` column (chapters, and any future
feature with a word-count display) computes the count **in the write service, before persisting, on
the already-sanitized HTML.** Never count on raw editor output (markup inflates the count) and never
count on display (redundant work on every render).

The strip+count helper lives in **Core** — dependency-free, no NuGet beyond the standard library,
unit-testable with no host or DbContext — parallel to `StorySlug.Slugify` in `Core/Stories/`. The
canonical example is `ChapterText.CountWords(string?)` in `Core/Chapters/`. The three-step sequence:

1. `sanitizedHtml = sanitizer.Sanitize(rawHtml)`
2. `wordCount = ChapterText.CountWords(sanitizedHtml)`
3. Persist both.

`WordCount` therefore always reflects *readable* words — what `RichTextView` would render — not a
count of markup tokens.

### Export & Import — the Allowlist Is the Interchange Contract (WU38c/WU38d)

The 13-tag sanitizer allowlist is not just a security boundary — it is the **fidelity contract for
every format conversion** in both directions:

- **Export** (`Export/` cluster, `IExportService`): per-format writers (EPUB/PDF/HTML/TXT/
  Markdown/DOCX) map exactly the allowlist tags to their format's constructs. What the editor can
  produce is what exports render. If the toolbar/allowlist ever grows, the writers grow in the
  same change (third leg of the toolbar↔allowlist contract above).
- **Import** (`Import/` cluster, `IContentImportService`): per-format readers (Mammoth for DOCX,
  VersOne.Epub, AngleSharp for HTML, Markdig for Markdown, plain TXT) convert *toward* the
  allowlist, then **every imported chapter's HTML passes through `IHtmlSanitizationService`
  before it reaches the editor or a write service** — the sanitizer is the single trust boundary
  for file-derived content, exactly as it is for editor output. Unrepresentable source formatting
  is stripped **with an `ImportWarning` surfaced to the author** (e.g. images dropped — the
  allowlist has no `img`), never silently.

**Export permission rule: "export = what you can read."** `ExportStoryAsync` composes the existing
read services, so the content-rating master filter is the only gate — no author-only restriction,
no `[Authorize]` on the endpoint. Anyone who can read a story may download it.

**Licenses:** QuestPDF (Community — free under $1M revenue; `QuestPDF.Settings.License` set once at
startup in Program.cs), Mammoth (BSD-2), Markdig (BSD-2), VersOne.Epub (free OSS),
DocumentFormat.OpenXml (MIT). AngleSharp is referenced explicitly (was transitive via
HtmlSanitizer) because writers/readers use it directly.

### File Downloads Bypass the Circuit

A file download is an ordinary HTTP GET whose response carries `Content-Disposition: attachment`.
The InteractiveServer SignalR circuit **cannot produce that** — an `EventCallback` runs C# on the
server and diffs DOM back over the socket; there is no HTTP response for the browser to save. So
download affordances are **plain `<a href>` anchors pointing at a minimal-API endpoint**
(`Results.File(bytes, contentType, fileName)` sets the header), never `@onclick` handlers. The
anchor is a real browser navigation that carries the auth cookie, so `IActiveUserContext` and all
query filters resolve normally. Canonical example: `Server/Export/ExportEndpoints.cs`
(`GET /api/stories/{id}/export/{format}`). Do not reach for JS-interop blob downloads
(base64/`DotNetStreamReference`) when a plain endpoint works — heavier, worse for large files.

## Group Rating Waterfall — Enforcement at Write Time

Group content addition (`AddStoryAsync`, folder assignment) enforces a **three-tier waterfall** at
write time. Tiers are checked in order; a violation at any tier throws `ContentRatingExceededException`:

| Tier | Rule | Enforcement location |
|------|------|---------------------|
| 1 | User's site-wide filter (mature off → T ceiling) | Existing `ContentRating` named query filter on `Story`; already model-level — free, never bypassed |
| 2 | `story.Rating > group.MaxContentRating` | Checked in `ServerGroupWriteService.AddStoryAsync` before inserting `GroupStory` |
| 3 | `story.Rating > folder.MaxRating` (when a folder is specified) | Checked in `ServerGroupWriteService` before inserting the story↔folder join |

Tier 1 means a write service call that resolves `story` from the write DbContext will already have
the content-rating filter applied — the story row simply won't load if it exceeds the user's
ceiling (anon + mature-off users get `Rating.T` ceiling; authenticated + mature-on users get `Rating.M`).
Tiers 2/3 are explicit `if` guards in the write service.

**Folder `MaxRating` ≤ group `MaxContentRating`:** the folder-create path (admin-only) enforces that
a folder's ceiling cannot exceed the group ceiling. Attempting to create a folder with a higher
`MaxRating` than the group's `MaxContentRating` throws `GroupValidationException`.

**`ContentRatingExceededException`** lives in `Core/Groups/` (not `Core/` root) — it is a domain
exception specific to the group content model, not a general cross-cutting concern.

## Group Comments — Per-Context Method Pattern

Group comments follow the **per-context method** pattern established for blog-post comments in WU31.
The comment service exposes one pair of methods per comment context (chapter / blog post / group),
rather than a generic context enum:

```csharp
// ICommentReadService — group branch (mirrors GetBlogPostCommentsAsync)
Task<(CommentDto[] Comments, int TotalCount)> GetGroupCommentsAsync(
    int groupId, int page, int pageSize);

// ICommentWriteService — group branch
Task<long> PostGroupCommentAsync(PostGroupCommentDto dto);
// PostGroupCommentDto: { int GroupId; long? ParentCommentId; string CommentText; }
// No IsSpoiler — spoilers are a chapter-only concept (ChapterComment.IsSpoiler).
```

`ServerCommentReadService` uses `readDb.GroupComments` (the typed `DbSet<GroupComment>`) with the
same two-step root-paging + per-viewer like-EXISTS projection as the blog-post and chapter branches.
`ServerCommentWriteService.PostGroupCommentAsync` copies `PostBlogPostCommentAsync`, substituting
`GroupComment` / `writeDb.GroupComments` for the entity and DbSet.

**Why per-context, not a generic context enum:** each context differs in its verification step
(blog-post branch verifies `writeDb.BlogPosts`; group branch verifies `writeDb.Groups`; chapter
branch verifies `writeDb.Chapters` + parent-same-chapter cross-check). Sharing a single generic
method with a `target` parameter does not simplify the verification logic and would produce a branchy
switch that obscures what each context requires. The per-context pattern already exists; extend it.

## Group Membership and Role Model

Groups are **not gated communities.** The membership and role model is deliberately simple:

- **Open join:** any authenticated user may join any group (subject to `GroupAudience` visibility —
  you can't join a Mature group if you can't see it; see `content-safety.md` "Group
  Audience-Visibility Filter"). No approval, no invitation, no waitlist.
- **Permanent membership:** no kicking mechanism. If a member misbehaves, they are handled by site
  moderators (WU34) exactly as in any other area of the site. No per-group moderator role.
- **Two roles: Member and Admin.**
  - `GroupRole.Member` — can browse the group, add stories (subject to content-rating waterfall),
    post comments.
  - `GroupRole.Admin` — additionally can remove stories, manage folders (create/rename/delete/reorder,
    set `MaxRating ≤ group.MaxContentRating`), and edit the group's name/description/audience type.
  - The group creator is automatically inserted as Admin on group creation. There is currently no
    way to transfer Admin status — that is post-MVP if ever needed.
- **No `GroupRole.Moderator` category.** Do not add one — the decision is permanent, not a
  deferral. Site moderators handle group-level misconduct.

**Server-side enforcement:** admin-gated write methods load the caller's `GroupMember` row and check
`role == GroupRole.Admin`, throwing `UnauthorizedAccessException` on mismatch. UI affordances (folder
management, remove-story buttons) are visibility-only `@if` wired to a page-computed `bool IsAdmin`
passed down from the dispatcher — not a security gate.

**Leave:** any member may leave. The `GroupMember` row is deleted. If the last admin leaves, the
group remains but has no admin — currently acceptable (post-MVP: warn on last-admin leave or
auto-promote).

## Notification Generation

Feature write services that trigger notifications inject `INotificationWriteService` as a standard
scoped dependency and call a **semantic per-event method** after their primary `SaveChangesAsync`:

```csharp
public class ServerFollowingWriteService(
    ReadOnlyApplicationDbContext readDb,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IHtmlSanitizationService sanitizer,
    INotificationWriteService notifications)   // ← ordinary scoped dep
    : ServerFollowingReadService(readDb, activeUser), IFollowingWriteService
{
    public async Task FollowAsync(int targetUserId)
    {
        // ... primary work ...
        await writeDb.SaveChangesAsync();   // primary commit first

        try { await notifications.NotifyNewFollowerAsync(ActorId, targetUserId); }
        catch (Exception ex) { logger.LogError(ex, "Notification failed"); }
    }
}
```

**Why best-effort post-commit:** the feature service and notification service share the same scoped
`ApplicationDbContext`. By committing the primary work first, the change tracker is clean when
`INotificationWriteService` runs its own `SaveChangesAsync` (covering only notification rows). A
notification failure then can't roll back the already-durable primary action.

**Why semantic methods (not a generic `CreateAsync`):** recipient resolution, drop-self, and dedup
must never be accidentally bypassed. A public generic `CreateAsync(recipientId, type, sourceId,
relatedId)` would require every caller to re-implement filtering. Semantic methods (`NotifyNewFollowerAsync`,
`NotifyNewChapterAsync`, etc.) are thin wrappers over one private create-core that owns all invariants
— the same "property of the model" principle behind the content-rating named query filter.

**`INotificationWriteService` is a fully independent service** with its own injected contexts. It
composes *read* services for recipient resolution (e.g. `IFollowingReadService.GetFollowedUsersAsync`),
keeping the DAG acyclic — see "The DAG rule" below.

**Semantic methods land incrementally** — each method is co-delivered with the work-unit that builds its triggering feature; fan-out methods (new chapter, new story, etc.) land with their respective work-units when the triggering feature is Stage 5.

### Filtering semantics

**In-app delivery is always-on.** The private create-core applies exactly two universal rules: **drop
self** (`recipient == sourceUser`) and **dedup**. No per-type in-app mute exists in the model.

**Fan-out eligibility (relationship-level gate):** follow-driven notification types (new chapter on a
followed story, new story by a followed user, etc.) are sent only to followers where
`FollowedUser.ReceiveAlerts == true`. That filter is part of the recipient-resolution query for each
semantic method — not a per-type setting.

**`UserNotificationSetting` governs email and display, not in-app generation.** The sparse-override
table stores exactly two user-settable fields per type — `EmailEnabled` (post-MVP email side-channel)
and `Collapsed` (display override for the panel — a per-user override of
`NotificationType.DefaultCollapsed`). NULL for either field means "use the type's default."
No in-app mute column exists; that toggle was deliberately dropped from spec §5.18 (recorded in
`audit/Notifications.md`).

9 categories, ~35 types with gap-based numbering. `DefaultEmailEnabled` and `DefaultCollapsed` are
required non-nullable on all types.

## Polymorphic RelatedEntityId — Two-Pass Batch Enrichment (WU33)

`NotificationDto.RelatedEntityId` is a single `int` column that points at different entity tables
depending on `NotificationTypeEnum` (story, chapter, user, group, blog post, comment, or nothing).
The type-ambiguity makes a single SQL JOIN projection impossible.

**Why not a conditional JOIN:** EF Core cannot translate a JOIN whose target table varies by row value
across heterogeneous tables. Even with raw SQL, the column set differs per branch.

**Why not DTO inheritance:** the codebase has no DTO-inheritance precedent; the DTO firewall favors flat
projections; a heterogeneous `NotificationDto[]` would force the UI into type-switches. The solution is to
normalize the polymorphic target into one `(TargetTitle?, TargetUrl?)` pair — one slot regardless of kind.

**The pattern (applied in `GetNotificationsAsync`):**

1. **Materialize the page** via normal LINQ with LEFT JOINs (`UserNotificationSettings` for effective
   Collapsed; `Users` on `SourceUserId` for `SourceUserName`). Apply ordering before `Skip/Take`.
2. **Classify** each materialized row's `RelatedEntityId` by a private
   `static RelatedEntityKind KindFor(NotificationTypeEnum)` switch.
   `RelatedEntityKind` is an internal enum: `User | Story | Chapter | Group | BlogPost | Comment | None`.
3. **Batch-load** each kind present on the page in one query per kind:
   - Group the materialized row ids by kind; skip empty sets.
   - `Stories.Where(s => ids.Contains(s.StoryId)).Select(s => new {s.StoryId, s.Title})` → url = `$"/story/{id}"`.
   - `Chapters.Where(...)` → url = `$"/story/{storyId}/{chapterNumber}"` (Chapter carries both fields).
   - `Users.Where(...)` → url = `$"/user/{id}"`.
   - Group/BlogPost/Comment → respective routes. `None` → no query; null title/url.
   - Produce `Dictionary<int,(string Title,string Url)>` per kind.
4. **Stitch** each DTO row with its `(TargetTitle, TargetUrl)` from the relevant dictionary; return enriched array.

**Extra queries:** at most as many as distinct kinds appearing on the page (max 6, typically 1–3). Never N+1.

**Forward-compat:** kinds whose triggering feature isn't built yet produce no rows, but their `KindFor` branch
is coded now — dormant branches compile and need no future edit.

## Service Composition

Feature services that span domains inject foundational services, not duplicate query logic:

```csharp
public class ServerInteractionReadService(
    ReadOnlyApplicationDbContext readDb,
    IStoryReadService storyReadService) : IInteractionReadService
{
    public async Task<StoryListingDto[]> GetFavoritesAsync(int userId)
    {
        var storyIds = await readDb.UserStoryInteractions
            .Where(i => i.UserId == userId && i.IsFavorite)
            .Select(i => i.StoryId)
            .ToArrayAsync();

        return await storyReadService.GetListingsByIdsAsync(storyIds);
    }
}
```

**Building-block methods:** Foundational services expose methods designed for consumption by other
services (`GetListingsByIdsAsync(int[] storyIds)`), not just by components.

**The DAG rule:** Service dependencies form a directed acyclic graph. Composite services inject
foundational services, never the reverse. `ServerInteractionReadService` → `IStoryReadService`
is correct. `ServerStoryReadService` → `IInteractionReadService` is a design smell.

**Hot-path escape hatch:** For performance-critical queries, a single optimized JOIN that bypasses
composition is permitted as a documented exception. Interface and DTO don't change; only the method
body does. This is the same "body swap behind a stable interface" principle that governs Layers 5–7.

## Discovery Defaults + Random Batch (WU28)

### Random batch — plain draw from the post-filter set

`GetRandomBatchAsync(StoryFilterDto filter, int batchSize)` on `IStoryReadService`:

```csharp
IQueryable<Story> q = ApplyFilters(readDb.Stories, filter); // shared helper, see below
int[] ids = await q.OrderBy(_ => EF.Functions.Random()).Take(batchSize).Select(s => s.StoryId).ToArrayAsync();
return await GetListingsByIdsAsync(ids);
```

**No `excludeStoryIds` parameter. No shown-id tracking. No TotalCount.** "Give me more" is a
second call that appends a fresh draw to the display list — repeats are acceptable. Sorted-mode
pagination uses offset (`Skip`/`Take` on `GetListingsAsync`); the random path never does.

Interaction exclusions flow through `filter.ExcludedInteractions` the same as any other filter.
The page seeds those from the §8.7 defaults read service; the random path is not special-cased.

**`ApplyFilters(IQueryable<Story> q, StoryFilterDto filter) → IQueryable<Story>`** is a private
helper extracted from `GetListingsAsync` so the random path and the sorted path share it (DRY).
It applies: tag include (AND loop / OR Any by `filter.IncludeMode`), tag exclude, FTS Matches,
and interaction-state exclusions. It does **not** add `OrderBy` or pagination — those live in
the caller.

### §8.7 Discovery Defaults — `IDiscoveryDefaultsReadService`

New service in `Core/Discovery/` / `Server/Discovery/`:

```csharp
Task<IReadOnlyList<UserStoryInteractionTypeEnum>> GetDefaultExcludedInteractionsAsync(string searchModeKey);
```

**Algorithm:** load `DefaultUserStoryInteractionFilterSetting` rows for `searchModeKey` (the system
matrix). If `activeUser.UserId` is non-null, load the user's `UserStoryInteractionFilterSetting`
rows for the same mode and **overlay** (user value wins per key). Anonymous → system defaults only.
Keep keys where effective `IsEnabled == true`; map filter-key string → enum via a static
Server-side map (keys live in `SiteConstants.cs`). **`HasStarted` is not in the enum** (the catalog
has 7 keys but `UserStoryInteractionTypeEnum` has 6 values) — drop it from the mapped output,
documented in the service.

**Seed is authoritative and unchanged** (Ignored=true on the 5 discovery surfaces; profiles=none).
No migration. Per-user override *editing* UI is deferred post-MVP (entity supports it).

### Tag include-mode boolean lattice

The 2×2 lattice (Include × Exclude), with the dead ALL-exclude cell intentionally unbuilt:

| | Include | Exclude |
|---|---|---|
| **AND (all)** | Default. `Where(has t)` per id (conjunctive loop). | N/A — dead cell. "Exclude all" has no practical meaning. |
| **OR (any)** | Optional; toggle on `/discover` only. `Where(s => s.StoryTags.Any(st => ids.Contains(st.TagId)))`. | N/A — same dead cell. Exclude is always ANY/none. |

`TagIncludeMode { And, Or }` lives in `Core/Discovery/`. `StoryFilterDto` gains
`TagIncludeMode IncludeMode { get; init; } = TagIncludeMode.And` — default preserves all existing
callers. The OR branch is gated at the page level (only `/discover` passes `ShowTagIncludeModeToggle`);
the `StoryFilterDto` property is unconditional so the filter service handles it anywhere.

**Why interaction state is exclude-only here (Discovery Model vs Library Model):** tags are
story-intrinsic (any viewer can filter by them) — both include and exclude are meaningful.
Interaction state is a viewer relationship — "show only stories I've completed" implies a
whitelist over the full catalog, which is not what `/discover` does. Interaction *inclusion* is
the Library/Bookshelves Source concern (`restrictToStoryIds`), not a discovery filter.

**OR-include has precedent** in the original deliberations §9 whitelist-union of entity-filter
lists. The AND/OR toggle is set-combination *within* a fixed include selector — it is not the
per-criterion include/exclude *semantics* toggle the deliberations (§8) rejected as confusing
(which would flip include↔exclude per checkbox). Include and exclude remain separate selectors.
OR-across-tags was "never deliberated" (§11); this toggle is a deliberate net-new extension.

## `StoryFilterDto` + `GetListingsAsync` (WU23)

**`StoryFilterDto`** (`Core/Discovery/`) is the source-agnostic filter criteria that `ResultsFilterPanel`
emits and `GetListingsAsync` accepts:

```csharp
public record StoryFilterDto(
    string? TextQuery,                                       // FTS — Matches(); enables Relevance sort
    IReadOnlyList<int> IncludedTagIds,                      // must have all (AND join)
    IReadOnlyList<int> ExcludedTagIds,                      // must have none
    IReadOnlyList<UserStoryInteractionTypeEnum> ExcludedInteractions, // viewer-relative exclusions
    DefaultSortOrder Sort,
    int Page,
    int PageSize);
```

**Excluded by design:**
- Content rating — applied automatically by `ApplicationDbContext`'s named query filter (`IActiveUserContext`); not a caller concern.
- The per-`SearchMode` default-settings matrix (§8.7, `DefaultUserStoryInteractionFilterSetting`/`UserStoryInteractionFilterSetting`) — deferred post-WU23 — **built in WU28**, see "Discovery Defaults + Random Batch (WU28)" above.
- The **Source** axis — `GetListingsAsync` is `Source=All` only. Narrowed sources (bookshelves, profiles, groups) pass pre-selected IDs to `GetListingsByIdsAsync` instead.

**`GetListingsAsync` two-step (mirrors `GetRecentListingsAsync`):**

```csharp
// Step 1 — build filtered IQueryable<Story>, page on scalar IDs, capture TotalCount.
IQueryable<Story> q = readDb.Stories.AsQueryable();
if (filter.IncludedTagIds.Count > 0)
    foreach (var tagId in filter.IncludedTagIds)
        q = q.Where(s => s.StoryTags.Any(st => st.TagId == tagId));
if (filter.ExcludedTagIds.Count > 0)
    q = q.Where(s => !s.StoryTags.Any(st => filter.ExcludedTagIds.Contains(st.TagId)));
if (!string.IsNullOrWhiteSpace(filter.TextQuery))
    q = q.Where(s => s.StoryListing!.SearchVector.Matches(filter.TextQuery));
// viewer-relative interaction exclusions scoped to IActiveUserContext.UserId
// ... sort by DefaultSortOrder; Relevance only when TextQuery is set (Rank()) ...
int totalCount = await q.CountAsync();
int[] ids = await q.Select(s => s.StoryId).Skip(...).Take(filter.PageSize).ToArrayAsync();

// Step 2 — delegate presentation projection to the building-block method.
StoryListingDto[] items = await GetListingsByIdsAsync(ids);
return (items, totalCount);
```

**Npgsql traps to avoid (already hit in earlier WUs):**
- `string.Contains(string, StringComparison)` — untranslatable overload; use `Matches()` for FTS or
  `EF.Functions.ILike()` for simple LIKE.
- `OrderBy` on a projected DTO field after a `SelectMany` — keep `OrderBy` on entity fields before
  the projection step.
- `Relevance` sort via `Rank()` only when `TextQuery` is non-empty — guard this or the SQL fails.

## Tree Search — Automatic Tab Composition (WU44)

Feature 59's `ITreeSearchReadService.TraverseAsync` (WU-Marts, Stage 5) is a live recursive CTE
over the `user_story_tree_search_entries` mart, returning story IDs + degree-to-reach + optional
path. Spec §5.26 says tags/FTS/interaction filters "compose with the data mart query," but
`TraverseAsync`'s only filters are rating + the viewer's §8.7 `AutoTreeSearch` interaction
exclusions, applied inside the SQL. Full first-principles resolution: `audit/Discovery.md`
Feature 59. Summary of the settled shape:

**Two axes, not one.** Edge types + degrees are *reachability* parameters (intrinsic to the walk —
they decide whether/how-far a story is connected) and stay on `TreeSearchRequest`. Rating,
interaction, tags, and FTS are *relevance* filters (properties of the destination story) and must
apply **after** traversal — pruning the walk on them would sever silent-bridge connections, the
same reason a mature story is already allowed to be an unshown bridge node. This is the Source ×
Filter × Sort model applied to tree search: **Source** = the rCTE (edge-types + degrees as its own
params), **Filter** = rating + interaction + tags + FTS (`StoryFilterDto` / `ResultsFilterPanel`),
**Sort** = Random / ByDegree.

**Composition, not duplication, because the two engines differ.** `ApplyFilters`
(`ServerStoryReadService.cs`, `IQueryable<Story>` LINQ) cannot be shared verbatim into the rCTE's
static ADO SQL. Hand-writing an equivalent tag/FTS predicate into the SQL would duplicate the
filter logic in two places and reopen the frozen Stage-5 query. Instead:

```csharp
// ITreeSearchReadService — new method, additive; TraverseAsync unchanged
Task<TreeSearchListingResultDto> SearchAsync(
    TreeSearchRequest request, StoryFilterDto filter, CancellationToken ct = default);
```

`SearchAsync` (injects `IStoryReadService`):
1. Runs a defaulted **raw-reached** traversal path — same rCTE, but with no rating/interaction
   filter and no `ResultCap` (bounded by the existing per-node fan-out `LIMIT`, so still tractable)
   — returning `(story_id, degree, path)` minus the root.
2. Calls a new thin read on `IStoryReadService`:
   ```csharp
   Task<IReadOnlyList<int>> FilterCandidateIdsAsync(IReadOnlyCollection<int> candidateIds, StoryFilterDto filter);
   // body: return ApplyFilters(readDb.Stories.Where(s => candidateIds.Contains(s.StoryId)), filter, hasFts)
   //           .Select(s => s.StoryId).ToListAsync();
   ```
   reusing the existing `ApplyFilters` verbatim — the single implementation of rating (global query
   filter), interaction exclusion (seeded from §8.7 `AutoTreeSearch` defaults, user-editable via the
   panel exactly like `/discover`), tag include/exclude, and FTS.
3. Joins survivors against the degree map, applies `TreeSearchSortOrder` (Random shuffle, or
   ByDegree ascending — `GetListingsAsync`'s `DefaultSortOrder` has no ByDegree, so reusing that
   whole bundle instead of just the predicate was rejected), caps on the **filtered** set, and
   computes `ResultCapTruncated` from the filtered count vs. the cap (capping the raw traversal
   first, as `TraverseAsync` does, would make truncation misleading once a Filter is layered on).
4. Hydrates the capped page via the existing `GetListingsByIdsAsync`, then zips degree/path back
   onto each `StoryListingDto` in `TreeSearchListingResultDto`.

`TraverseAsync` itself is untouched (still backs the `/dev/discovery/tree-search` probe). The only
change to the Stage-5 tree-search service is additive: the raw-reached mode + `SearchAsync`.

## Write-Side Reads — Four Cases

| Case | Example | Context used |
|---|---|---|
| Constraint check | Hidden Gem ≤5 count | `writeDb` (primary, consistency) |
| Edit form loads read DTO | Editor needs current title | `readDb` (via inherited read method) |
| Edit-only fields | `OriginalPublishedDate` | `writeDb` via dedicated `GetStoryForEditAsync()` |
| Display hint | `CommentDto.IsLikedByCurrentUser` | Computed by the **read service** in its projection (per-viewer EXISTS subquery on `CommentLike`, always false for anonymous); the result then flows *down* to the `CommentItem` leaf as a `[Parameter]`. The leaf never injects a service. |

## Recommendation Write Conventions (WU29)

Three write-side patterns settled for the Recommendations cluster — record them here so future
sessions don't re-derive them:

**Min-length validation (strip-then-count):** `RecommendationConstants.MinLength = 500`. The write
service strips HTML and decodes entities before counting characters — same Core helper pattern as
`ChapterText.CountWords` (strip→decode→whitespace-split). Reject with
`RecommendationValidationException` if the count is below the threshold. The minimum is enforced on
the **sanitized** text (after `sanitizer.Sanitize(rawHtml)`) so markup inflation never passes through.

**Auto-approve on submit (MVP):** `SubmitAsync` writes `StatusId = Approved` directly. Spec §5.6's
Pending→author-approval/moderation lifecycle is deferred to WU34. See `forward_plan.md` Resolved.
The status enum seed (1=Pending, 2=Approved, 3=Rejected, 4=Under Review) is unchanged; this is a
write-service choice, not a schema change.

**Count-limit enforcement (Hidden Gem and author-highlight):** Both limits are checked against
`writeDb` (write-side read, Case 1 — constraint check, for consistency), then rejected via
`RecommendationValidationException`. `MaxHiddenGemsPerUser = 5`; `MaxHighlightedPerStory = 5`.
Mirrors the Vouch 5-limit pattern (`FollowingConstants.MaxVouchesPerUser`). No auto-evict, no swap —
the user must explicitly un-designate first. **Settled — do not revisit** (resolved Phase B,
`forward_plan.md` "Hidden Gem at-limit behavior").

**Like toggle (no notification):** `ToggleLikeAsync` returns `RecommendationLikeResultDto(int LikeCount,
bool IsLiked)` so the UI reconciles optimistic state without a re-read. No notification fires on a
recommendation like — anti-addictive design (§6.11), same as `CommentLike`.

## Structured Tag Authoring — Routing and Validation (WU37)

### Per-story routing table

Every tag type uses a **different per-story association table**. Route by `TagChipDto.TagTypeId`:

| Tag type | Per-story target | Entity |
|---|---|---|
| Genre, ContentWarning, CrossoverFandom | Flat junction | `StoryTag` |
| Setting | Flat junction + optional side-row | `StoryTag` + `SettingDetail` |
| Character | Dedicated entity (replaces StoryTag) | `StoryCharacter` |
| Pairing (ship) | Structural, named members | `StoryCharacterPairing` + `StoryCharacterPairingMember` |

Character never routes to `StoryTag`. A pairing is not a catalog tag (no `Tag` row; its name derives
from its members). `TagTypeEnum.Relationship` is removed.

### Table naming — disambiguation from story↔story lineage (Feature 10)

| Concept | Entity | Note |
|---|---|---|
| Character-in-story | `StoryCharacter` | Per-story; links to `Tag` (Character type) |
| Ship/pairing of characters | `StoryCharacterPairing` | Per-story; NOT a catalog tag |
| Members of a pairing | `StoryCharacterPairingMember` | First-class join; was auto-generated shadow table |
| **Story-to-story** link | `StoryLineage` | Feature 10; unrelated; leave untouched |
| Story lineage type | `StoryLineageType` | Feature 10; unrelated; leave untouched |

The `Story…Pairing` prefix marks the concept as per-story and eliminates grep collision with the
Feature-10 `StoryLineage`/`StoryLineageType` entities. **WU42 (2026-07-12) additionally renamed
Feature 10 itself** from `StoryRelationship`/`StoryRelationshipType` to `StoryLineage`/
`StoryLineageType` — the near-collision this table originally worked around no longer exists at the
identifier level (neither name contains "Relationship" anymore), but the table is kept as it still
disambiguates the two *concepts* (character pairing vs. story-to-story link).

### Story Lineage service (WU42, `Core/Stories/` + `Server/Stories/`)

`IStoryLineageReadService`/`IStoryLineageWriteService` — a cross-author request/approve workflow
(spec §939, Feature 10). A link where the requester owns only the source story is created `Pending`
and requires the **target** story's author to approve/reject via the owner-wide `/story-lineages`
page before it displays; a link where the requester owns both stories is created already `Approved`
(no notification — matches the notification drop-self invariant). Public reads
(`GetLineageForStoryAsync`) return only `Approved` rows where the queried story is the source, joined
through `Story` so a link never survives display when its target fails the viewer's
`ContentRating`/`IsTakenDown` filters (mirrors `ServerSeriesReadService.GetMembershipsForStoryAsync`'s
join-not-bare-projection rule). Target-story selection goes through a new reusable
`IStoryReadService.SearchStoriesByTitleAsync` (`ILike` substring typeahead) — deliberately not the
discovery FTS (`StoryListing.SearchVector`, a whole-word-ranked GIN index tuned for browse relevance,
not incremental substring matching); the same search method also retrofits Groups' add-story picker.

### Write path — route by tag type

`StoryMappers.UpdateStoryEditableProperties` clears and rebuilds each per-story collection.
Route by `TagTypeId` on the incoming DTO:

```csharp
// StoryMappers.cs — structured routing (WU37)
foreach (var sc in dto.StoryCharacters)
    story.StoryCharacters.Add(new StoryCharacter { CharacterTagId = sc.TagId, ... });

foreach (var st in dto.FlatTags)   // Genre/ContentWarning/CrossoverFandom/Setting
    story.StoryTags.Add(new StoryTag { TagId = st.TagId, Priority = st.Priority });

foreach (var sd in dto.SettingDetails)
    story.SettingDetails.Add(new SettingDetail { BaseTagId = sd.BaseTagId, ... });

foreach (var sp in dto.StoryCharacterPairings)
{
    var pairing = new StoryCharacterPairing { PairingType = sp.PairingType, Priority = sp.Priority };
    foreach (var memberId in sp.MemberStoryCharacterIds)
        pairing.Members.Add(new StoryCharacterPairingMember { StoryCharacterId = memberId });
    story.StoryCharacterPairings.Add(pairing);
}
```

**Character never touches `StoryTag`.** Setting appears in both `StoryTag` (for catalog association)
and optionally `SettingDetail` (for per-story custom name/description).

### Validation — server re-reads gates from Tag

The write service calls `ServerStoryWriteService.ValidateStructuredTagsAsync` (or extends `CanSave()`)
after loading `Tag` rows for all referenced TagIds. **Never trust DTO-carried `AllowOCDetails` or
`AllowSettingDetails`** — load fresh from the `Tag` table. Full rules table below.

### Legality rules — enforced at service layer

All rules are enforced by `ServerStoryWriteService` via `StoryValidationException` (same pattern as
`CanSave()` / author-gate). Server re-reads gates from `Tag` — never trusts client DTO values.

| Rule | Condition | Error |
|---|---|---|
| OC details require gate | `IsOc == true` but `Tag.AllowOCDetails == false` | Reject |
| SettingDetail requires gate | `SettingDetail` submitted but `Tag.AllowSettingDetails == false` | Reject |
| ContentWarning priority coercion | Priority != Primary | Coerce to Primary (not an error) |
| Pairing member count | Members < 2 | Reject |
| Pairing members in-story | Member `StoryCharacterId` must exist in this story's `StoryCharacters` | Reject |

No DB trigger (`TR_StoryCharacters_EnforceOCLogic` is SQL-Server-era; superseded). A DB CHECK is
post-MVP defense-in-depth if wanted.

### Priority

`TagPriority { Primary=0, Supporting=1 }`. Primary default. No `None` value. ContentWarning gets no
priority picker and its priority is coerced to `Primary` at service layer.

### Per-type filter branch in `ApplyFilters`

`ApplyFilters(IQueryable<Story> q, StoryFilterDto filter)` must partition tag ids by type **before**
building the include/exclude predicates, because Character ids no longer appear in `StoryTags`:

```csharp
// WU37 change to ApplyFilters — partition by TagTypeId
var characterIds = filter.IncludedTagIds.Where(IsCharacterTag).ToList();
var otherIds     = filter.IncludedTagIds.Where(t => !IsCharacterTag(t)).ToList();

// Character branch
foreach (var id in characterIds)
    q = q.Where(s => s.StoryCharacters.Any(sc => sc.CharacterTagId == id));

// Flat-tag branch (Setting/Genre/ContentWarning/CrossoverFandom)
foreach (var id in otherIds)
    q = q.Where(s => s.StoryTags.Any(st => st.TagId == id));
```

`IsCharacterTag` resolves from `TagTypeId` carried on `StoryFilterDto` tag-metadata (or a helper that
queries `Tag.TagTypeId` for a given id set). The same partition applies to `ExcludedTagIds`.

`StoryFilterDto` gains the per-id type metadata to support this without an extra DB round-trip; or the
write path sends `(TagId, TagTypeId)` tuples rather than flat ids.

## `AllowPrivateMessages` Gate

`User.PrivacySettings.AllowPrivateMessages` is a **`SocialInteractionPermission` enum** (not a bool)
with four tiers. It is enforced in **`ServerMessagingWriteService.StartConversationAsync` only** —
not re-checked on replies to an existing thread:

| Tier | Enforcement |
|---|---|
| `Public` | Allow any authenticated sender. |
| `UsersOnly` | Allow any authenticated sender. (Default — most users.) |
| `Following` | **Write-side existence check:** `writeDb.FollowedUsers.AnyAsync(f => f.FollowedUserId == senderId && f.UserId == recipientId)` — the recipient must follow the sender. Use `writeDb` (Case 1 — constraint check, for consistency), not `IFollowingReadService`. |
| `Nobody` | Throw `MessagingPermissionException` (defined in `Core/Messaging/`). |

`PrivacySettings` is stored as a jsonb complex property on `User` — the `AllowPrivateMessages` field
is inside that JSON blob, not an indexed column. Querying by it in SQL requires a JSON path query;
for MVP, load the recipient's `PrivacySettings` navigation and evaluate in C# (single-row lookup,
not a filter over many rows). The gate check comes **after** the self-message guard and **before**
validation/sanitization of the message body.

## Self-Referential Editing Exception — `IUserSettingsService` (spec §3.5)

When the reader and writer populations are **identical by definition** — a user editing only
their own settings, never anyone else's — a single integrated read+write service is sanctioned.
This is a narrow, named exception to the CQRS-lite split:

```csharp
// Core/Profiles/
public interface IUserSettingsService
{
    Task<UserSettingsDto> GetMySettingsAsync();
    Task UpdateProfileAsync(UpdateProfileDto dto);
    Task UpdateReaderSettingsAsync(ReaderSettingsDto dto);
    Task UpdatePrivacySettingsAsync(PrivacySettingsDto dto);
    Task UpdateAuthorSettingsAsync(AuthorSettingsDto dto);
    Task UpdateAppearanceAsync(int themeId, bool prefersAnimated, bool prefersDataSaver);
    Task<string> UploadProfilePictureAsync(Stream content, string contentType);
}
```

**Rules that make this exception safe:**
1. **No `userId` parameter on any method.** The service resolves the target entirely from
   `IActiveUserContext` — it is, by contract, "the currently authenticated user's settings."
2. **Authentication guard is mandatory.** Every method must call `RequireAuthenticatedUser()` (or
   equivalent) before doing anything — there is no unauthenticated path.
3. **Self-only scope is the invariant.** The moment a method takes a `userId` it's no longer
   self-referential and must become a pair of `I{Feature}ReadService` + `I{Feature}WriteService`.

**Contrast with `IUserProfileReadService`** (public display — read-only, separate interface):
- Returns data about *any* user profile by `int userId`.
- Own-vs-other visibility differences are expressed as a `bool includePrivate` predicate passed by
  the dispatcher (who computes `includePrivate = viewerId == profileUserId`), not as a separate
  service or a source switch.
- Server impl uses `ReadOnlyApplicationDbContext` (`NoTracking`), never the write DbContext.

## Three-Tiered Validation

**Tier 1 (Client + Server):** `DataAnnotations` on ViewModels. Immediate UX feedback via
`EditForm` / `DataAnnotationsValidator`.

**Tier 2 (Client + Server):** Shared interface (`IEditableStoryProperties`) implemented by both
ViewModel and EF model. Validation in **static extension methods** in Core.

**Tier 3 (Server only):** Database context checks in service. On failure, throws
`StoryValidationException` containing `List<string>` of errors. Server-side only.

## Moderation Services

`IModerationReadService` / `IModerationWriteService` live in `Core/Moderation/`. Server impls live in
`Server/Moderation/`. The write service inherits the read service (CQRS-lite inheritance pattern).

**DAG position.** `ServerModerationWriteService` injects:
- `INotificationWriteService` (notifications — already the standard cross-feature dep).
- Feature *read* services for target/author resolution per `ReportedEntityType` — e.g.
  `IStoryReadService` to look up a story's title and author when notifying `ContentRemoved`. Never feature
  *write* services (DAG rule: moderation is a peer of features, not above them in the write graph).
- `IActiveUserContext` for moderator ID.

**Soft-delete (takedown) visibility filter `"IsTakenDown"`.** Each removable entity registers
`HasQueryFilter("IsTakenDown", e => !e.IsTakenDown)` in `OnModelCreating`. Public reads go through the
filter automatically. Author views and mod review paths use `IgnoreQueryFilters(["IsTakenDown"])`. The
filter composes alongside `"ContentRating"` and `"GroupAudience"` on entities that have multiple filters.
See `content-safety.md` "Content Removal" for the column naming rationale and moderator filter behavior.

**`IModeratableContent` interface.** `Story`, `BaseComment`, `BaseBlogPost`, `Recommendation` each implement
this interface exposing `IsTakenDown`, `TakedownDate`, `TakedownReason`, `ActiveReportCount`, and
`AuthorUserId`. `ServerModerationWriteService` loads via a single per-type switch (`LoadModeratableAsync`)
then mutates through the interface — no repeated switch per operation.

**`AdjustActiveReportCountAsync(ReportedEntityType type, long id, int delta)` private switch.** Lives in
`ServerModerationWriteService`. Called on report submit (+1) and report resolve (-1). Uses per-DbSet
`ExecuteUpdateAsync` (set-based, no load) with `IgnoreQueryFilters(["IsTakenDown"])`. Skips `Message`
(no counter on `PrivateMessage`). This is the single authority on counter mutation — do not
increment/decrement at call-sites.

**`ApplyRemovalAsync` / `ApplyHardDeleteAsync` — collapsed to interface.** Both call `LoadModeratableAsync`
once, then mutate via `IModeratableContent`. `ApplyRemovalAsync` sets `IsTakenDown = true`, `TakedownDate`,
`TakedownReason`. A parallel `ApplyHardDeleteAsync` calls `writeDb.Remove((object)entity)` for illegal
content — a distinct action, not a flag.

**Notification dedup key.** `CreateCoreAsync` dedups on `(NotificationTypeId, SourceUserId, RelatedEntityId,
!IsRead)` — `RelatedEntityId` was missing from the original WHERE clause (WU34 fix). This ensures two
moderation notifications about *different* targets both reach the recipient.

## Synchronous Inline Badge Awards (WU36)

A write service that triggers a badge-eligible event calls `IBadgeWriteService.AwardAsync` after the
primary `SaveChangesAsync`, **best-effort** — inside a `try/catch` so a badge failure never rolls back
the primary operation. `AwardAsync` is **idempotent** (no-op if already earned); the caller does NOT
need a separate "has badge" check first.

**Pattern:**

```csharp
// After primary SaveChangesAsync + counter ExecuteUpdateAsync:
int total = await writeDb.UserStats
    .Where(us => us.UserId == targetUserId)
    .Select(us => us.SomeCounter)
    .FirstOrDefaultAsync();

try
{
    if (total >= 10) await badgeService.AwardAsync(targetUserId, SiteBadges.SomeBadge);
    if (total >= 50) await badgeService.AwardAsync(targetUserId, SiteBadges.SomeBadgeSilver);
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Badge award failed for user {UserId} — swallowed.", targetUserId);
}
```

**Anti-self-farm guard:** when the mechanic is social (e.g., a reader marking a recommendation
helpful), guard `actorId != beneficiaryId` AND `nullableFk != null` **before** incrementing or
calling `AwardAsync`. Violations skip silently — no throw, no log.

**A write service MAY depend on `IBadgeWriteService`** (inject it in the primary constructor; no DAG
cycles since `IBadgeWriteService` depends only on `ApplicationDbContext` /
`ReadOnlyApplicationDbContext`).

**Newly awarded badges are visible by default** (`DisplayOrder = max+1`). The curation UI lets users
hide or reorder. `UserCard.razor` caps to 3 badges.

**Post-MVP:** a background worker will replace inline checks without changing callers' interface.

Live `SiteBadges` constants: `Patron`, `Recommender`, `RecommenderSilver`, `BetaReader`, `Architect`,
`Artist`. Keys are `public const string` fields on `SiteConstants.SiteBadges`.

## UserStats Updates

22+ denormalized counter fields. Updated in real-time by application logic within the same
transaction as the primary write (same-transaction `ExecuteUpdateAsync`):

```csharp
await writeDb.UserStats
    .Where(us => us.UserId == story.AuthorId)
    .ExecuteUpdateAsync(s => s.SetProperty(us => us.StoryCount, us => us.StoryCount + 1));
```

Background worker (F58, post-MVP) periodically recalculates to correct drift.

### Counter mutation rule — all denormalized counters

Every denormalized counter — `LikeCount` on `Recommendation` / `BaseComment`, and every `UserStats.*`
field — must be adjusted with an **atomic** `ExecuteUpdateAsync`:

```csharp
// ✓ Correct — one SQL `SET counter = counter + delta`; concurrent callers can't collide
await writeDb.Recommendations
    .Where(r => r.RecommendationId == id)
    .ExecuteUpdateAsync(s => s.SetProperty(r => r.LikeCount, r => r.LikeCount + delta));

// ✗ Wrong — tracked read-modify-write; two concurrent readers both see the old value → lost update
rec.LikeCount++;
await writeDb.SaveChangesAsync();
```

The tracked `++` form reads a value into memory, increments it, and writes it back. Two concurrent
callers reading the same stale value both produce the same written result: one increment is lost.
`ExecuteUpdateAsync` issues a single `SET like_count = like_count + delta` that the database
serializes correctly under any isolation level.

### Counter ↔ event map (WU30, wired into already-built write services)

| `UserStat` counter | Owning user | Event / write service | Δ |
|---|---|---|---|
| `FollowerCount` | target user | `ServerFollowingWriteService.FollowAsync / UnfollowAsync` | ±1 |
| `AuthorsFollowed` | acting user | `ServerFollowingWriteService.FollowAsync / UnfollowAsync` | ±1 |
| `StoriesWritten` | author | `ServerStoryWriteService.CreateStoryAsync` | +1 |
| `WordsWritten` | author | `ServerChapterWriteService` publish / new version | ± word delta |
| `CommentsWritten` | commenter | `ServerCommentWriteService.Post*/Delete` (all 4 contexts: chapter/blogpost/group/userprofile) | ±1 |
| `RecommendationsWritten` | recommender | `ServerRecommendationWriteService.SubmitAsync` | +1 |
| `RecommendationsReceived` | story author | `ServerRecommendationWriteService.SubmitAsync` | +1 |
| `RecommendationSuccessesEarned` | recommender | `ServerRecommendationWriteService.RecordSuccessAsync` (new column, WU36) | +1 |
| `BlogPostsWritten` | author | `ServerBlogPostWriteService` create/delete | ±1 |
| `GroupsJoined` | member | `ServerGroupWriteService` join/leave | ±1 |
| `FavoritesOnStories` | story author | `ServerUserStoryInteractionWriteService` | **transition-delta** |
| `StoriesRead`, `StoriesInProgress`, `StoriesIgnored` | acting user | `ServerUserStoryInteractionWriteService` | **transition-delta** |

**Counters deferred to their own WU** (source feature not yet built):
- `ViewsOnStories` — WU38 (story view events)
- `SpotlightCount` — post-MVP
- `ActiveReportCount` — WU34 (moderation)
- Acknowledgment/contribution counters — WU37

### Transition-delta rule for UserStoryInteraction-derived counters

`ServerUserStoryInteractionWriteService` toggles boolean columns (`IsFavorite`, `IsCompleted`,
`IsIgnored`, etc.) rather than appending records. A simple ±1 on every call would double-count;
the correct rule is **increment or decrement only when the boolean flips**:

```csharp
// Before writing:
bool wasFavorite = existing?.IsFavorite ?? false;
bool willBeFavorite = dto.IsFavorite;

// After SaveChangesAsync:
if (willBeFavorite && !wasFavorite)
    // +1 FavoritesOnStories on story.AuthorId
else if (!willBeFavorite && wasFavorite)
    // −1 FavoritesOnStories on story.AuthorId
```

The same flip-check governs `StoriesRead`/`StoriesInProgress`/`StoriesIgnored` — each maps to one
boolean column (`IsCompleted`, `HasStarted`+`!IsCompleted`, `IsIgnored`); the counter moves only
when the effective derived state actually changes. Never increment/decrement if the boolean is being
written to its current value (idempotent call from an optimistic-UI retry).

## Naming

- Server impl prefix `Server...`, client impl prefix `Client...`.
- Async methods end in `Async`.
- Method names express query/command intent, not storage (`GetListingsAsync`, not `QueryStoriesFromDb`).
- **Location:** interfaces, server impls, and client impls each live in their feature's cluster folder
  in their respective project (`Core/{Feature}/I{Feature}ReadService.cs`,
  `Server/{Feature}/Server{Feature}ReadService.cs`, `Client/{Feature}/Client{Feature}ReadService.cs`) —
  never in a shared `ServiceInterfaces/`/`Services/` folder. See `SKILL.md` "Code Organization" for the
  legacy-folder migration rule.
