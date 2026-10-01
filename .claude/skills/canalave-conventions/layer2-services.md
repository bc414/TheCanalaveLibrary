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

### Registering an inherited pair: forward, never register the class twice

When one concrete class serves both interfaces (the write impl inherits the read impl), register the
class **once** and forward the other interface to it. Registering the same class against both
interfaces mints **two instances per scope** — two `ApplicationDbContext`-holding services where the
code reads as one, and any per-scope state one of them accumulates is invisible to the other:

```csharp
// Right — one instance per scope, whichever interface is injected.
builder.Services.AddScoped<IStoryArcWriteService, ServerStoryArcWriteService>();
builder.Services.AddScoped<IStoryArcReadService>(sp => sp.GetRequiredService<IStoryArcWriteService>());

// Wrong — two instances per scope.
builder.Services.AddScoped<IStoryArcReadService, ServerStoryArcWriteService>();
builder.Services.AddScoped<IStoryArcWriteService, ServerStoryArcWriteService>();
```

Where the concrete class serves an interface it does *not* inherit through the read/write pair —
`ISavedTagSelectionWriteService` (whose write interface does not inherit the read one) and Moderation
(`ServerModerationWriteService` also serves the member-facing `IReportSubmissionService`, owner ruling
D9's split, WU-ModerationIntegrity) are the two such clusters — register the concrete class once and
forward **every** interface to it (Moderation forwards three) — the same shape
`ServerTagHierarchyCache` uses. Two separate classes (Story, Chapter, Comment, …) keep the plain
two-line registration: there is no shared instance to preserve.

Unified across all clusters 2026-09-20 (MA-107); `Program.cs` is the reference.

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

Settled WU12 (the "Aspire orchestration during MVP dev" resolution, carried in `middle_plan_v2.md` §Resolved — narrower correction): register
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
   hard to diagnose. *The same principle governs clears under a hidden parent* (owner ruling D6,
   built WU-AccessGateSweep2): un-favoriting, mark-unread, unliking — any clear or lower on the
   caller's own existing row — is never refused by the viewer's settings or the parent's
   visibility, on any axis; only raises carry the parent-visibility guard
   (`identity-and-authorization.md` §"Parent-visibility guards" → "Raises vs clears"). The
   interaction panel's guarded clears were this rule's standing contradiction until then.

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

## Hard deletes of content parents (owner ruling D10, WU-TptHardDelete 2026-09-30)

A content parent that owns TPT children (a chapter, a blog post, a story via its chapters, a profile
wall) is deleted in this order, inside **one** `CreateExecutionStrategy().ExecuteAsync` +
`BeginTransactionAsync`:
1. **`TptDelete` first** (`Server/Data/TptDelete.cs`) — the base rows of every TPT child, set-based raw
   SQL (`ChapterCommentsAsync`, `StoryCommentsAsync`, `ProfileWallCommentsAsync`,
   `BlogPostDependentsAsync`, `BlogPostAsync`). The base → child CASCADE takes each child row; likes,
   poll options and poll votes cascade off the base rows.
2. **Then the parent**, by EF `Remove` on a loaded entity or by the helper's own base-row `DELETE`
   (`TptDelete.BlogPostAsync` deletes the post through `base_blog_posts`).
3. **Commit, then `UserStats` counters and notifications** — outside the retried delegate (D22), so a
   retry never double-counts or double-notifies. The one exception is the moderation resolve's
   `ActiveReportCount` move, which runs inside its transaction because the report row lock needs one
   (§"Resolve paths — lock, guard, then transition"); `ApplyHardDeleteAsync` sits inside that
   transaction and moves no `UserStats` counter.

Why the order is forced, and why the FKs are RESTRICT: `layer1-data-model.md` §"Hard-deleting a content
parent". Skip step 1 and the delete fails with 23001 (`restrict_violation` — Postgres reports an
`ON DELETE RESTRICT` refusal as 23001, not the 23503 a NO ACTION FK gives); the database refuses the
partial delete.
- **The helper's SQL runs immediately; EF's `Remove` waits for `SaveChangesAsync`.** Both must be in
  the same transaction, or a failure between them leaves the children gone and the parent standing.
- **A delegate that `Remove`s the parent through EF starts with `writeDb.ChangeTracker.Clear()` and
  loads the parent inside the delegate** (the existence/owner gate before the delegate is a projection).
  Two reasons. The write context is scoped per **circuit** under InteractiveServer, so it can still
  track a TPT child added earlier in the circuit — an author replies to a comment, then deletes that
  chapter. The raw `DELETE` never reaches the tracker, and EF refuses to delete a principal while a
  tracked dependent of a RESTRICT relationship remains (`InvalidOperationException`, "the association
  … has been severed"). And a retried delegate must not inherit the failed attempt's tracked state.
  Callers that delete the parent by raw SQL (`TptDelete.BlogPostAsync`) are immune to the first reason.
- **Never `Include` the TPT children on a delete path** — the same refusal, from the delete's own load.
- **A 0-row base delete is a lost race, not a success.** Two deletes of one post can both pass the
  owner probe; `TptDelete.BlogPostAsync` returns the rows deleted, and the loser (0) throws
  `KeyNotFoundException` inside the delegate, so it rolls back and never reaches the counter
  decrement — the counter moves only on the actual delete.
- **Callers today:** `ServerChapterWriteService.DeleteChapterAsync` (clears the tracker since the
  WU-TptHardDelete review fixes, 2026-09-30);
  `ServerBlogPostWriteService.DeleteBlogPostAsync`/`DeleteGroupBlogPostAsync`/`DeleteSiteBlogPostAsync`
  (raw SQL for the post too); `ServerModerationWriteService.ApplyHardDeleteAsync` (Story, BlogPost —
  `InResolveTransactionAsync` clears the tracker); `UserDeletionService` (the profile wall; clears the
  tracker too). A comment, poll or recommendation delete removes a loaded entity of its own and needs
  no helper.

**Blog-post lifecycle methods are per subtype.** Each subtype has its own update/delete pair with its
own gate: profile posts (`UpdateBlogPostAsync`/`DeleteBlogPostAsync`, author-only), group posts
(`UpdateGroupBlogPostAsync`/`DeleteGroupBlogPostAsync`, author-only, no membership recheck — the
author-owns-own-row precedent of `EditCommentAsync`), site posts (`UpdateSiteBlogPostAsync`/
`DeleteSiteBlogPostAsync`, moderator-gated). Each method **keys its existence-and-owner probe on its
own child set** (`writeDb.ProfileBlogPosts…Select(p => new { p.AuthorId })`), never on the base set: an
id of another subtype is `KeyNotFoundException`, never a half-applied base-table update. An authorless
post (`AuthorId` NULL after account deletion) is owned by nobody — `UnauthorizedAccessException`.

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
the owner's `ProfileTab.TagSelections` tab and at its permalink (below) — there is no public
browse/gallery surface. Another
viewer's "Add to my filters" (`ISavedTagSelectionWriteService.CopyPublicSelectionAsync`) creates a new,
independently-owned `SavedTagSelection` + copied `SavedTagSelectionEntry` rows; the copy and the source
never affect each other afterward (no many-to-many "subscription" model — rejected because editing a
shared row would silently change it for every subscriber).

**A permalink does not make a selection a saved query (decision row 13, 2026-07-28).**
`/discover/selection/{SelectionId:int}/{*Slug}` renders *results* for a public selection, but the
artifact still contributes **only the tag axis**. Sort, free text and interaction exclusions come
from the **viewer's own** §8.7 defaults via `IDiscoveryDefaultsReadService`, exactly as they do on a
bare `/discover` visit — so two people opening the same permalink can legitimately see different
orderings and exclusions. That is the point: the shared object is the tag combination, not the
viewer's session. Nothing about the permalink widens what the entity persists.

`GetSelectionDetailAsync` stays authenticated (owner-or-public, for Load/copy). The permalink is fed
by a **separate anonymous-callable read** that enforces both `IsPublic` **and** the owner's
`ProfileVisibility` server-side — Class A access control, not a UX nicety
(`design/access-gating-first-principles.md`). Gate failure and "no such selection" are the same
contractual `null`: never distinguish them in the response.

**Device-local filter restore is not a competing persistence mechanism.** The "transient viewer
intent" ruling above is about what a *named, server-side, shareable artifact* carries. `/discover`
separately restores the viewer's last-applied filter from **browser localStorage** (ids only,
rehydrated through the existing batch reads, unseeable entities pruned — `layer3.5-structure.md`
§"The shared tree canvas" is the established shape). Device-local, per-browser, never synced, works
for anonymous viewers, and invisible to every other user. It answers "don't lose my work on a back
navigation"; the artifact answers "let me name and share this combination." Keep them separate:
nothing restored from localStorage may ever be written to `SavedTagSelection`, and `[PersistentState]`
is not usable for restore (`error-handling.md` — prerender-handoff only).

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

**Security consequence — the allow-list is also a live vuln mitigation.** The sanitizer's parser
(AngleSharp) is pinned to 0.17.1, which carries CVE-2026-54570 (mXSS via MathML `<annotation-xml>`;
see `security.md` "Dependency Vulnerability Scan Cadence" and the `AngleSharp` pin comment in
`TheCanalaveLibrary.Server.csproj`). This restrictive allow-list is what neutralizes it: stripping
every element/attribute outside the 13-tag + `href` set removes the `<annotation-xml>` element and
its `encoding` attribute before they can round-trip. So loosening the allow-list is not purely a
UX/toolbar decision — adding `svg`, `math`, `style`, or broad attributes re-opens the mXSS surface
until AngleSharp is off 0.17.1 (tracked as a Phase 7 root-cause fix in `roadmap.md`).

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

**`INotificationWriteService` is a fully independent service** with its own injected contexts. Fan-out
methods resolve their recipient sets with direct queries on its own write context — ground truth,
Personal plane, never rating- or audience-filtered — and it injects no other feature's service, read or
write, which keeps the DAG acyclic — see "The DAG rule" below. (Corrected WU-InertFeatures,
2026-09-30: an earlier wording said it "composes read services for recipient resolution"; no
implementation ever did.)

**Semantic methods land incrementally** — each method is co-delivered with the work-unit that builds its triggering feature; fan-out methods (new chapter, new story, etc.) land with their respective work-units when the triggering feature is Stage 5.

### Filtering semantics

**In-app delivery is always-on.** The private create-core applies exactly two universal rules: **drop
self** and **dedup** (both defined below). No per-type in-app mute exists in the model.

**Fan-out eligibility (relationship-level gate):** the gate depends on which relationship drives the
type. **Author-follow** types (`NewStoryByFollowedUser` 11, `NewRecommendationByFollowedUser` 12,
`NewBlogPostByFollowedUser` 13) go only to followers where `FollowedUser.ReceiveAlerts == true` — a
user-to-user follow carries a per-row opt-in. **Story-relationship** types (`NewChapterOnFollowedStory`
10, `NewBlogPostOnFollowedStory` 14, `…OnFavoritedStory` 15, `…OnReadItLaterStory` 16) have no per-row
opt-in: presence of the `UserStoryInteraction` flag is the signal. The filter is part of each semantic
method's recipient query — not a per-type setting. (Corrected WU-InertFeatures, 2026-09-30: this
paragraph used to put the new-chapter type under `ReceiveAlerts`, a column that does not exist on a
story follow.)

**A null source means "no actor" (owner ruling D4, WU-InertFeatures 2026-09-30).** `CreateCoreAsync`
takes `int? sourceUserId`. Null = system-sourced or self-caused: nobody did this *to* the recipient, or
the one who did must not be named (D5 below). The column was always nullable; only the write path
could not express it.
- **Drop-self is conditional:** a target is skipped only when `sourceUserId is int s && recipientId == s`.
  A null source never drops anyone — a notification with no actor has no self to echo.
- **Guardrail — null is not a convenience.** Drop-self used to protect call sites silently; a null
  source removes that protection, so a type with a real actor must always pass it. Concretely, the
  moderator-initiated account action (`ApplyAccountActionToUserAsync`, the report row where
  `ReporterUserId == ModeratorUserId`) must **never** send `ReportReceived` (80) or `ReportResolved`
  (81): under a null source they would deliver, mailing moderators receipts for their own actions.
- **The general rule for the de-identified band (WU-InertFeatures review fixes, 2026-09-30).** Where
  D5 forces a null source on a type that *has* a real actor (70–82 and 26, below), the call site does
  drop-self's job explicitly: **the acting moderator is never a recipient of a notification about
  their own act.** Every such call site compares the recipient with the acting moderator's id and
  skips the match — a moderator resolving a report they filed through the ordinary Report button
  gets no 81/82; removing their own content, no 70; approving or rejecting their own story, no
  75/71; verifying their own account or link, no 76–79; a report-driven account action on
  themselves, no 72–74; canonizing a fanon name they used, no 26. This restores exactly what drop-self
  did before D5 — de-identification changes what the row says, never who receives it. `ReportReceived`
  (80) is the exception that proves it: its "actor" is the reporter themselves, a self-caused event
  D4 restored on purpose.
- **Two nulls share one column.** "Actor deleted" (SET NULL on account deletion) and "no actor" are
  indistinguishable in storage. Disambiguate **by notification type at display time, never by the
  column**: actor-free types compose actor-free text ("Your account has been suspended"), and the
  presenter's `?? "Someone"` fallback survives only for types that genuinely had an actor since
  deleted. "Someone banned your account" must not be reachable.

**Moderation-band de-identification (owner ruling D5, WU-InertFeatures 2026-09-30).** Every
notification in the moderation band 70–82 (`ContentRemoved`, `StoryRejected`, `AccountWarning`,
`AccountSuspended`, `AccountBanned`, `StoryApproved`, the four external-verification outcomes, and the
three report outcomes) and `TagUpdateSuggestion` (26 — a moderator's adoption invitation is a
moderation act; D5's routed sub-edge, taken per the owner's recommendation) is **null-sourced**. The
acting moderator's id and name never reach the recipient: the id would ship in `NotificationDto` over
a WASM-reachable endpoint whether or not any UI rendered it, so suppressing it at the presenter is not
a fix. The `Report` row (and `ReviewedByModeratorUserId` on the verification rows) remains the internal
ledger. **Type-level enforcement: no `INotificationWriteService` method in this band may take a
moderator id parameter** — that is what stops a future call site from reintroducing the leak. The
band is uniform on purpose, good news included: if only sanctions were anonymous, a name's presence
would itself say "you're fine". `SpotlightSlotGranted` (90) sits outside D5's stated band and keeps its
granting-moderator source (unruled — roadmap decision row 19).

**Dedup.** The cross-existing key is `(type, source, related entity, unread)`: a recipient who already
holds an unread row with that key is skipped. A null source matches a null source (EF's C# null
semantics emit `IS NULL`), so a null source strips the key's discriminating power — which is why D4
enumerated the band per type:
- **`ContentRemoved` (70), `ReportReceived` (80), `ReportResolved` (81), `ReportResolvedNoAction` (82)
  carry the report id** (`Report.ReportId`, globally unique across reported-entity kinds). The reported
  entity's id would collapse a report on story 5 with a report on comment 5. Accepted: a sequential
  report id reaches its own reporter/author in `NotificationDto.RelatedEntityId`, which reveals rough
  report volume; it relies on mod-only report reads staying gated.
- **`AccountWarning` (72), `AccountSuspended` (73), `AccountBanned` (74), `ExternalAccountVerified`
  (76), `ExternalAccountRejected` (77) and `SpotlightSlotGranted` (90) are exempt from cross-existing
  dedup.** They have no related entity (the target is the account itself), so two warnings while the
  first is unread must produce two rows. The exemption set lives in `ServerNotificationWriteService`;
  any future dedup unique index must respect it and the null source.
- Within-batch dedup (one row per recipient per call, first wins) applies to every type.

**`RelatedEntityId` stays non-nullable; 0 means "no related entity".** It carries no FK, is interpreted
per type (see §"Polymorphic RelatedEntityId" below), and 0 is never a valid id in this schema. NULL
would buy only a second nullable dedup component and a null branch in every enricher lookup.

**New-chapter fan-out (`NewChapterOnFollowedStory` = 10; WU-InertFeatures, 2026-09-30).**
`NotifyNewChapterAsync(storyId, chapterId, authorId)` is called by `IChapterWriteService.SetPublishedAsync`
best-effort post-commit **iff that call performed the `Chapter.FirstPublishedDate` null→non-null
stamp** (D2's chapter anchor). Recipients: `UserStoryInteraction.IsFollowed` on the story — no per-row
opt-in, and no rating filter (Personal plane; D6 already lets a follower who can no longer see the
story unfollow it). A hidden favorite does not enter: there is no private-follow flag (D17); if this
fan-out is ever widened to favoriters it takes `IsFavorite || IsHiddenFavorite`, never bare
`IsFavorite`. `RelatedEntityId = chapterId` (D16-conformant: the chapter joins up to its story); the
source is the author, so a self-follow is dropped. **First publication only, permanently, per
artifact** (D1's anti-bump rider): unpublish→republish, adding or promoting an alternate version, and
editing content never notify — republication is not a publication event (D2). **Default, not an owner
ruling:** the fan-out is suppressed when the story is not publicly published at that moment (status
outside `StoryLifecycle.IsPublished`'s set, or `IsTakenDown`), using the status predicate rather than
`StoryVisibilityGuard` (whose author clause would make a Draft "visible" to the publishing author). The
consequence — a chapter first-published while its story is unpublished never notifies, because its
anchor is already stamped when the story later goes live — is open as `roadmap.md` decision row 17.

**`UserNotificationSetting` governs email and display, not in-app generation.** The sparse-override
table stores exactly two user-settable fields per type — `EmailEnabled` (the email side-channel; see
"Email fan-out" below) and `Collapsed` (display override for the panel — a per-user override of
`NotificationType.DefaultCollapsed`). NULL for either field means "use the type's default."
No in-app mute column exists; that toggle was deliberately dropped from spec §5.18 (recorded in
`audit/Notifications.md`).

9 categories, ~35 types with gap-based numbering. `DefaultEmailEnabled` and `DefaultCollapsed` are
required non-nullable on all types.

### Email fan-out (WU-NotifEmail, 2026-07-31)

**Create-core enqueues; it never sends.** After the in-app rows commit, `CreateCoreAsync` pushes the
inserted rows onto `NotificationEmailBuffer` (in-process, bounded, singleton) and returns. A
`BackgroundService` drains it and sends the batch over **one** pooled SMTP connection. This is the
standard buffer/flusher/worker trio — see §"Signal Buffering" and `ReadingProgressFlushWorker.cs`.

Inline sending is **rejected**, not deferred: ~22 seeded types default `EmailEnabled = true` and
several fan out to every follower of a story or author, so an inline send puts N ×
(connect → auth → send → disconnect) inside a SignalR circuit write path.

Rules that bind the flusher:

- **Eligibility is the flusher's job, not the enqueuer's.** Effective `EmailEnabled` = the sparse
  `UserNotificationSetting` row's value, falling back to `NotificationType.DefaultEmailEnabled` —
  the same LEFT JOIN `GetSettingsAsync` implements. Resolve it at drain time, not enqueue time, so
  a user who unsubscribes between the two is honored.
- **Gate on `EmailConfirmed`. Do not gate on account status.** `AccountWarning`,
  `AccountSuspended`, and `AccountBanned` default `EmailEnabled = true` and are exactly the
  notifications a restricted user must receive.
- **Skip rows already `IsRead` at drain time** — the user saw it in-app first.
- **Mail failure never touches the in-app row.** Log with batch size, restore the batch, let the
  worker survive. Same best-effort posture as the rest of create-core.
- **Never silently drop.** If the buffer's bound is hit, log and increment a counter — a fan-out
  that quietly stops emailing is indistinguishable from one that works.

**Transport.** Notification mail does not ride `IEmailSender<User>` (Identity's three-method
confirmation/reset contract). Both paths sit on `IMailTransport` (`Server/Identity/`), so the
`Email:Provider` switch, `EmailOptions`, and the `CanalaveTelemetry.Email` span/counters are
single-sourced. Selecting a provider is a config change; no code depends on which one.

**Absolute links.** Use `IPublicUrlProvider.AbsolutePageUrl` (`Core/Seo/`, backed by
`Site:PublicBaseUrl`). A worker has no `HttpContext`, and a request-derived base is wrong here for
the same reason it is wrong for Open Graph tags. No email-specific base-URL config exists — do not
add one.

**Unsubscribe.** Every notification email carries RFC 8058 `List-Unsubscribe` +
`List-Unsubscribe-Post` headers pointing at an anonymous Data-Protection-signed endpoint, plus a
visible footer link. Unsubscribing flips one type via the same sparse upsert/delete semantics as
`SetSettingAsync` — reuse that method's body, never reimplement the "matches default ⇒ delete the
row" rule.

**Email bodies reuse `NotificationPresenter.Compose`** (SharedUI) for message text — the ~40-type
switch is not forked. Bodies are table-based inline-CSS HTML and are the one markup surface
**exempt** from the design-token rules in `layer4-style.md`: email clients do not support custom
properties, and `check-design-tokens.ps1` governs app markup only.

### Comment & blog-post semantic methods (WU-B2, 2026-07-25)

The comment and profile-blog seams are wired through five semantic methods. Rules that bind them:

- **Replies carry the *context* id (current behavior).** `CommentReply` stores the context entity id
  per seam (chapterId / blogPostId / groupId / profileOwnerId), and `KindFor` maps it to `None`.
  Accepted dedup consequence: two replies from one user to your different comments in the same
  context, while the first is unread, collapse to one notification — consistent with the generic
  "replied to your comment" presenter text. (Rewritten WU-InertFeatures, 2026-09-30: the old rationale
  — "`RelatedEntityId` is `int`, `CommentId` is `long`" — stopped being true when the column widened
  to `bigint`. Re-pointing type 34 at the comment through the `BaseComments` TPT root is on D16's
  conformance list in §"Polymorphic RelatedEntityId" below; it is not a physical impossibility any
  more.)
- **Reply/container-suppress rule:** on a reply, the container owner (story author / blog author /
  profile owner) is *not* sent the container-level type when they are also the parent-comment author —
  they get exactly one notification (`CommentReply`). Never two notifications to one person for one event.
- **Null-skip:** `Story.AuthorId`, `BaseBlogPost.AuthorId`, `BaseComment.UserId` are `int?` (SET NULL
  on user deletion) — seams skip any notify whose resolved recipient is null.
- **Group comments notify replies only.** A group wall has no single comment-owner and no
  `NotifyForNewComment` membership flag; top-level group comments generate nothing.
- **Profile blog fan-out fires on the publish transition** (`IsPublished` false→true in
  `UpdateBlogPostAsync`), never on draft create. Republish re-notifies (unread-dedup absorbs
  back-to-back duplicates) — intentional.
- **A Private author's profile post fans out to nobody** (WU-AccessGateSweep2 review fixes,
  2026-09-30). A profile post is profile-tab data and is exactly as visible as its author's profile
  (`identity-and-authorization.md` guard table, `BlogPostVisibilityGuard`), and every fan-out
  recipient is someone other than the author — so under `ProfileVisibility.Private` no recipient can
  open the post, and a notification would only disclose its title beside a link that 404s.
  `NotifyNewProfileBlogPostAsync` reads the author's setting and returns before resolving
  recipients. `UsersOnly` needs no check: every recipient is signed in. This is the one recipient-side
  visibility rule in the fan-outs — the "recipients are ground truth, Personal plane" posture is about
  rating and audience (Class B); profile privacy is Class A and is never bypassed.
- **Fan-out precedence-dedup:** `NotifyNewProfileBlogPostAsync` resolves four recipient sets —
  author-followers (`FollowedUser.ReceiveAlerts`, type 13) and, when story-linked, story
  followers/favoriters/read-it-later (`UserStoryInteraction.IsFollowed/IsFavorite/IsReadItLater`,
  types 14/15/16) — made disjoint by precedence 13 > 14 > 15 > 16 (most-direct relationship wins), so
  each user receives exactly one notification per publish event. Story-interaction sets have no
  per-row opt-in; presence of the flag is the signal. The favoriter set (15) is
  `IsFavorite || IsHiddenFavorite` — all three favoriter states (public, hidden-from-visitors,
  private-only) are favoriters for fan-out purposes.
- **A hidden favorite suppresses public-plane consequences only; it never suppresses personal-plane
  ones** (owner ruling D17, built WU-InertFeatures 2026-09-30). Spec §5.7's list of what the flag
  withholds — public profile display, the public favorite count, tree-search/Also-Favorited edges absent
  `AllowDiscoveryFromHiddenFavorites` — is all things *other people* see. A notification delivered to
  the favoriter alone leaks nothing, so withholding it only punishes choosing privacy. **Mirror, same
  principle, opposite outcome:** `NewStoryFavorite` (20, "someone favorited your story") is
  *author-plane*, so a hidden favorite (either `IsHiddenFavorite` state) must **not** fire it — recorded
  before that producer exists (only `SeedGraph` mints type 20 today). The plane, not the flag, decides.
  Read paths are unaffected: the profile Favorites query's narrower
  `IsFavorite && (includePrivate || !IsHiddenFavorite)` is a public-plane display filter, not the
  definition of favoriting.
- **Blog `StoryId` is ownership-validated at write time:** `CreateProfileBlogPostAsync` /
  `UpdateBlogPostAsync` reject a `StoryId` whose `Story.AuthorId` isn't the blog author
  (`UnauthorizedAccessException`) — the editor's own-stories dropdown is affordance, the service gate
  is the control. This is what keeps the story fan-out unspoofable. Group blog posts have no
  `StoryId` at all (removed 2026-07-25, restoring the original TPT design — group posts are group
  topics; only profile posts speak about a specific story).

## Polymorphic RelatedEntityId — Two-Pass Batch Enrichment (WU33)

`NotificationDto.RelatedEntityId` is a single `long` column (`bigint` since WU-InertFeatures,
2026-09-30 — report and comment ids are `bigint`) that points at different entity tables depending on
`NotificationTypeEnum` (story, chapter, user, group, group-story pairing, blog post, tag, report, or
nothing — 0). The type-ambiguity makes a single SQL JOIN projection impossible.

**What the column names — one anchor per event (owner ruling D16, WU-InertFeatures 2026-09-30).**
`RelatedEntityId` names the **single most specific entity of the event**: the node from which every
other entity the display needs is reachable by FK join. If an event appears to need two entities, the
pairing *is* an entity — point at the junction row (give it a surrogate PK if it lacks one).
Genuinely polymorphic targets anchor on a TPT root (`BaseComments`, `BlogPosts` — the `BlogPostDirect`
precedent), never on a discriminator column. **A second id column is never added to `notifications`.**
A notification row is already (recipient, actor, object); where two objects appear, one owns the other
(chapter ⊂ story, recommendation ⊂ story) or a junction owns both, so one FK root suffices everywhere.
Nothing structural would stop a widened design at two rather than three, so the stop is a stated rule:
each extra column costs a `KindFor` arm per type, a second batch-load pass, a dedup-key ruling, a
NULL-semantics answer and a presenter phrasing per *combination* of resolved targets. A type that
cannot name one anchor has not been designed yet. First application: `NewGroupStory` (60) and
`YourStoryAddedToGroup` (25) carry the `GroupStory` junction row's id (distinct stories stop
collapsing under dedup; re-adding the same story still does).

**Conformance backlog (D16 — "none urgent", one later sweep, not point fixes; tracker B23).** Types
that point at a less specific node today: `HiddenGem` (23) stores the recommender's user id (a
duplicate of `SourceUserId`) where `recommendationId` would name the story; the recommendation family
(22/27/40/41/42/43) and `RecommendationSpotlighted` (92) store `storyId` where `recommendationId` adds a
rec anchor; the comment types (24/31/33) store the context entity where `commentId` gives context plus an
in-page anchor; `CommentReply` (34) has no target at all (the `BaseComments` TPT-root case);
`ExternalLinkVerified`/`Rejected` (78/79) store `storyId` where the link row is the real object;
`PollUpdated` (100) stores the owning blog post and leaves site polls at 0 where `pollId` resolves both.
`ContentRemoved` (70) is resolved by D4: its anchor is the report row. Story lineage (50/51) is the
one event with two genuinely disjoint roots — the fix is a surrogate PK on `story_lineages` (a schema
item, recorded as an implication, not decided work).

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
   `RelatedEntityKind` is an internal enum: `None | User | Story | Chapter | Group | GroupStory | BlogPost | BlogPostDirect | Tag`.
3. **Batch-load** each kind present on the page in one query per kind:
   - Group the materialized row ids by kind; skip empty sets. Every kind's PK is `int`, so each
     `long` id set is narrowed to `int` first (ids above `int.MaxValue` simply miss) — the SQL stays
     `int = int` and keeps its index.
   - `Stories.Where(s => ids.Contains(s.StoryId)).Select(s => new {s.StoryId, s.Title})` → url = `$"/story/{id}"`.
   - `Chapters.Where(...)` → url = `$"/story/{storyId}/{chapterNumber}"` (Chapter carries both fields).
   - `Users.Where(...)` → url = `$"/user/{id}"`.
   - Group/BlogPost → respective routes. `None` → no query; null title/url.
   - Produce `Dictionary<long,(string? Title, string? Url, string? ContextTitle)>` per kind.
4. **Stitch** each DTO row with its `(TargetTitle, TargetUrl, TargetContextTitle)` from the relevant
   dictionary; return enriched array.

**`TargetContextTitle` — the second name a junction or child anchor yields (WU-InertFeatures,
2026-09-30).** Resolving the one anchor can name a second object for free; the DTO carries it as an
optional trailing `TargetContextTitle` (null unless the kind supplies one). `GroupStory` →
Title = group name, Url = `/group/{GroupId}` (link target unchanged from the group-id era),
ContextTitle = story title; the group read is the same elevated `IgnoreQueryFilters(["GroupAudience"])`
as the `Group` kind. `Chapter` → Title = chapter title, ContextTitle = story title (used by type 10).
A deleted junction row (`RemoveStoryAsync`) is a miss → title-less, non-navigating — the designed
graceful path, accepted by D16.

**Extra queries:** at most as many as distinct kinds appearing on the page (max 8 — one per non-`None` kind; typically 1–3). Never N+1.

**Forward-compat:** kinds whose triggering feature isn't built yet produce no rows, but their `KindFor` branch
is coded now — dormant branches compile and need no future edit.

**Two blog-post kinds (WU-B2, 2026-07-25):** `BlogPost` resolves via `GroupBlogPosts` and deliberately
links to the *group* (`/group/{GroupId}`) — used by `NewGroupBlogPost` only. `BlogPostDirect` resolves
via the TPT-root `BlogPosts` DbSet and links to the *post* (`/blog/{BlogPostId}` — the unified
`BlogPostPage` route serves both post kinds) — used by the followed-content blog types (13–16),
`NewCommentOnBlog`, and `PollUpdated` (remapped: its group-only lookup left profile-post poll
notifications title-less). The `BlogPostDirect` lookup applies **no `IgnoreQueryFilters`**: blog posts
carry no audience/rating global filter (rating is an explicit `.Where` in the blog read service), and
the `IsTakenDown` named filter stays active deliberately — a taken-down post drops out → null target →
graceful fallback text. `NewStoryComment` maps to `Chapter` (deep-links the comment to its chapter);
`NewCommentOnYourProfile` maps to `User`; `CommentReply` stays `None` (one cross-context type cannot
map to one table — non-navigating, known minor UX gap).

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
TagExpansionMap expansion = await ResolveExpansionAsync(filter); // ValidateShipShape + cached lookup
IQueryable<Story> q = ApplyFilters(readDb.Stories, filter, expansion, ActiveUser.UserId, hasFts);
int[] ids = await q.OrderBy(_ => EF.Functions.Random()).Take(batchSize).Select(s => s.StoryId).ToArrayAsync();
return await GetListingsByIdsAsync(ids);
```

**No `excludeStoryIds` parameter. No shown-id tracking. No TotalCount.** "Give me more" is a
second call that appends a fresh draw to the display list — repeats are acceptable. Sorted-mode
pagination uses offset (`Skip`/`Take` on `GetListingsAsync`); the random path never does.

Interaction exclusions flow through `filter.ExcludedInteractions` the same as any other filter.
The page seeds those from the §8.7 defaults read service; the random path is not special-cased.

**`ApplyFilters(IQueryable<Story> query, StoryFilterDto filter, TagExpansionMap expansion, int?
viewerId, bool hasFts) → IQueryable<Story>`** is a private, **synchronous, side-effect-free**
helper extracted from `GetListingsAsync` so the random path and the sorted path share it (DRY). It
applies: tag include (AND loop / OR Any by `filter.IncludeMode`), tag exclude, ship filters, FTS
Matches, and interaction-state exclusions. It does **not** add `OrderBy` or pagination — those live
in the caller. **It takes no `DbContext` and does no I/O** (WU-ApplyFiltersPurity, closes
hidden-deferrals-tracker B12) — the hierarchy roll-up map and the viewer id are explicit arguments,
so the method is a pure function of its inputs. Callers obtain the map via the sibling
`ResolveExpansionAsync(filter)`, which runs `ValidateShipShape` first and then resolves a cached
process-local map from `ITagHierarchyReadService` — see §"Tag Hierarchy Roll-Up" and
§"Reference-Data Caching" below.

### §8.7 Discovery Defaults — `IDiscoveryDefaultsReadService`

New service in `Core/Discovery/` / `Server/Discovery/`:

```csharp
Task<IReadOnlyList<UserStoryInteractionTypeEnum>> GetDefaultExcludedInteractionsAsync(string searchModeKey);
```

**Algorithm:** load `DefaultUserStoryInteractionFilterSetting` rows for `searchModeKey` (the system
matrix). If `activeUser.UserId` is non-null, load the user's `UserStoryInteractionFilterSetting`
rows for the same mode and **overlay** (user value wins per key). Anonymous → system defaults only.
Keep keys where effective `IsEnabled == true`; map filter-key string → enum via a static
Server-side map (keys live in `Core/Discovery/SiteSearchModes.cs` — moved out of
`SiteConstants.cs` in WU28). **`HasStarted` is not in the enum** (the catalog
has 7 keys but `UserStoryInteractionTypeEnum` has 6 values) — drop it from the mapped output,
documented in the service.

**Seed is authoritative and unchanged** (Ignored=true on the 5 discovery surfaces; profiles=none).
No migration.

**Per-user override *editing*** ships as a separate service, `IDiscoveryFilterSettingsService`
(`Core/Discovery/`, WU-DiscoveryOverrideUI, 2026-07-31) — deliberately not added to
`IDiscoveryDefaultsReadService` above, which stays anonymous-callable and a pure read.
`GetMyMatrixAsync()` returns one row per (confirmed-consumer search mode × mappable filter key)
with its system default, effective value, and override-exists flag; `SetOverrideAsync(searchModeKey,
filterKey, isEnabled)` is a sparse upsert/delete — mirrors `INotificationWriteService.SetSettingAsync`'s
contract exactly (when the value matches the system default the override row is deleted, so absence
means "use default"). Surfaces on `/settings` (`DiscoverySettingsForm.razor`), not
`ResultsFilterPanel` — see `audit/Discovery.md` §"Note on search-result narrowing" for why.

### Optional caller-supplied exclusions — `ICoOccurrenceReadService` (F61, WU-RelatedStories)

`GetAlsoFavoritedAsync`/`GetAlsoRecommendedAsync` originally resolved the viewer's §8.7 defaults
internally with no way for a caller to override them — fine for a static read, but the embedded
story-page sections need a live `UserStoryInteractionFilter` toggle to actually change the result
set. Both methods gained an additive optional parameter:

```csharp
Task<IReadOnlyList<RelatedStoryScoreDto>> GetAlsoFavoritedAsync(
    int storyId, int take = 10,
    IReadOnlyList<UserStoryInteractionTypeEnum>? excludedInteractions = null,
    CancellationToken ct = default);
```

**`null` (the default) preserves existing behavior** — resolve `IDiscoveryDefaultsReadService`
internally, exactly as before (the dev-diagnostics probe and every existing caller pass no
argument). **Non-null bypasses the defaults lookup entirely** and is used as-is — the caller
(`RelatedStoriesSection`) seeds its filter checkboxes from the same §8.7 defaults read up front,
then passes the user's live edits straight through. This is the general pattern for adding
"caller can override the server-resolved default" to a read service: an optional trailing
parameter defaulting to `null`/"use the server default," never a second overloaded method.

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

**`GetListingsAsync` two-step** (formerly "mirrors `GetRecentListingsAsync`" — that method was
removed 2026-07-28, WU-Home; see `layer6-indexes.md` §"stories — the two discovery sort spines"):

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
   // body: TagExpansionMap expansion = await ResolveExpansionAsync(filter);
   //       return ApplyFilters(readDb.Stories.Where(s => candidateIds.Contains(s.StoryId)),
   //                            filter, expansion, ActiveUser.UserId, hasFts)
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

### Publish-immediately + the Recommendation Lifecycle

**(WU-RecLifecycle, 2026-07-25 — supersedes the
WU29 "auto-approve MVP / deferred to WU34" note):** `SubmitAsync` writes `StatusId = Approved`
directly — permanently, not as a shortcut. The pre-publication gate (and any moderator approval) was
**rejected** on first-principles review: recommendations are discovery, not feedback; a gate delays
discovery, dead-weights inactive authors, and merges two distinct author intents into one harsh
mechanism. Statuses: `NeedsRevision=1` / `Approved=2` / `Rejected=3` (`PendingApproval` and
`UnderReview` removed — nothing ever produced them). The lifecycle:

- **Submit:** self-rec guard (`storyAuthorId == userId` → validation error); live immediately;
  best-effort `NotifyNewRecommendationOnYourStoryAsync` to the story author.
- **`RequestRevisionAsync(recId, note)`** (story-author-gated, the "fix an earnest flaw" path):
  from Approved/NeedsRevision; non-empty note required, stored on hot
  `Recommendation.RevisionRequestNote` (same shape/placement as `TakedownReason`); publicly hidden;
  recommender notified (`RecommendationRevisionRequested`). **Not sticky** — the recommender's
  `EditAsync` auto-returns it to Approved (note cleared, author notified via `RecommendationRevised`;
  the recommender is NOT self-notified).
- **`RemoveAsync(recId)`** (story-author-gated, the "remove a troll" path): from either state →
  `Rejected`. Silent, hidden, **sticky**: `EditAsync`/`DeleteAsync` refuse on Rejected, and the
  `(RecommenderId, StoryId)` unique index blocks resubmission — the persisted Rejected row IS the
  block record. Only **`UnblockAsync`** (author, from Rejected only) reverses it → straight to
  Approved + `RecommendationApproved` notification (its only trigger).
- **Flag invariant:** `IsHiddenGem`/`IsHighlightedByAuthor` are only ever true on Approved recs.
  Both lifecycle exits clear both flags (slots freed); return to Approved does NOT restore them;
  both setters refuse on non-Approved.
- Story-author authorization uses the `SetHighlightedByAuthorAsync` ownership pattern
  (`Stories.AnyAsync(s => s.StoryId == rec.StoryId && s.AuthorId == userId)`); co-authors
  deliberately excluded until the dormant `CoAuthor` feature is built.
- Deliberate asymmetry with comment removal (hard-delete, re-postable): a Rejected rec's slot stays
  occupied. Actor-class framing: `content-safety.md` §"Moderation Model".

**Count-limit enforcement (Hidden Gem and author-highlight):** Both limits are checked against
`writeDb` (write-side read, Case 1 — constraint check, for consistency), then rejected via
`RecommendationValidationException`. `MaxHiddenGemsPerUser = 5`; `MaxHighlightedPerStory = 5`.
Mirrors the Vouch 5-limit pattern (`FollowingConstants.MaxVouchesPerUser`). No auto-evict, no swap —
the user must explicitly un-designate first. **Settled — do not revisit** (resolved Phase B,
"Hidden Gem at-limit behavior" — carried in `middle_plan_v2.md` §Resolved).

**Like toggle (no notification):** `ToggleLikeAsync` returns `RecommendationLikeResultDto(int LikeCount,
bool IsLiked)` so the UI reconciles optimistic state without a re-read. No notification fires on a
recommendation like — anti-addictive design (§6.11), same as `CommentLike`.

### Attribution (Feature 30) — metadata on the Read-It-Later bit (owner ruling D3, WU-InertFeatures 2026-09-30)

**The defining sentence:** the attribution (`UserStoryRecommendationSource`, keyed on the USI composite
PK) records **how the viewer's `IsReadItLater` bit came to be set**. RIL clicked on a recommendation card
→ store the source; RIL set anywhere else (the story page panel) → store nothing. It is not a "where did
this reader come from" event log. Every rule below follows from that sentence.

**The coupling is correct — do not decouple.** The sources row FKs to the USI row (cascade) and to the
recommendation (cascade). The 2025-design-session record shows provenance deliberately coupled to the
interaction; the FK failure the service audit found (§2.3.3) came from a *placeholder caller* — a
`?rec=`-on-load write that ran before any USI row could exist — not from the schema. The fault was a
missing producer. **Cascade on the recommendation FK is the faithful translation**, not an EF accident:
the 2025 SQL Server DDL's `NO ACTION` was a multi-cascade-path workaround for an intended
`SET NULL` ("deleting a recommendation clears the attribution and leaves the interaction alone"), and in
the two-table shape "null the attribution" and "delete the sources row" are the same operation.
**RESTRICT is rejected:** attribution must never block a recommender from deleting their own
recommendation. (The unpaired `Recommendation.UserStoryInteractions` collection nav, which minted a
shadow `user_story_interactions.recommendation_id` FK, was a fossil of the pre-split
`SourceRecommendationID` column — removed WU-InertFeatures; see `layer1-data-model.md`.)

**Two entry points — one integrity rule ("a sources row exists").**
1. **RIL from the card** — `IUserStoryInteractionWriteService.SetReadItLaterFromRecommendationAsync(recId)`,
   the durable path. One `SaveChangesAsync`: upsert the USI row (`IsReadItLater = true`,
   `ReadItLaterDate ??= now`, other bits untouched) and insert the sources row **in the same unit of
   work**, so the FK-ordering hazard is structurally unreachable. The card cannot reuse
   `SetUserStoryInteractionStateAsync` (six-bit absolute set — it would clobber the viewer's other
   flags). The sources row is inserted only when this call flipped the bit false→true (or created the
   row): a bit already set elsewhere was not "set from the card". A raise, so the full story-visibility
   guard applies (D6); an unknown, non-`Approved` or taken-down rec is `KeyNotFoundException`.
2. **Direct link** (`/story/{id}/1?rec={recId}`, the "Read now" anchor) — the same-session read-now
   path. **Nothing is written on page load.** The attribution is persisted at the 90%-of-Chapter-1
   moment, inside `MarkStartedAsync(storyId, attributedRecommendationId)` — the parent row is guaranteed
   there, and a URL-farm attempt must reach 90% of Chapter 1 per target. The URL is untrusted and
   `MarkStartedAsync` is the primary action, so an unattributable parameter is **silently ignored**. A
   reader who stops early and comes back without the parameter gets no attribution — correct.
   **The carrier is consumed once** (WU-InertFeatures review fixes, 2026-09-30): once
   `MarkStartedAsync` has run with it, the reading page drops `?rec=` from the address
   (`NavigateTo(..., replace: true)` — the history entry is replaced, so neither a reload nor Back
   carries it again; on .NET 10 a query-only change does not reset the scroll position). Without this,
   X deleted the row and a reload of the same address minted a fresh one, so the dismissed prompt came
   back — what D3's X ruling forbids. Following a recommendation's "Read now" link again is a new
   deliberate act and starts a new attribution, the direct-link twin of "a re-RIL after a clear starts
   a new one".

**Write gates (both entry points, shared helper `RecommendationAttribution.IsAttributableAsync`).** The
rec exists, belongs to the story, is `Approved` and not taken down; the caller is **not the story's
author** (the RIL itself is allowed — only the attribution is skipped, so no row exists that can never
be consumed); **first attribution wins** within one attribution's life (no row is overwritten; a re-RIL
after a clear starts a new one); and **no success is already recorded** for (caller, rec) — the
prompt's fourth gate hides such a row forever, so it could never be consumed (the author gate's own
reasoning; derived WU-InertFeatures review fixes, 2026-09-30, not owner text). Anonymous card click →
login nudge (UI), `InvalidOperationException` (service).

**Removal — five triggers.** The attribution describes the RIL bit, so it dies when the bit does.
Cascade-only is rejected: it would let a stale attribution outlive its RIL, and an un-RIL→re-RIL from a
different rec would credit the wrong recommender.
1. `IsReadItLater` true→false (`SetUserStoryInteractionStateAsync`) → delete, same unit of work.
2. USI row swept or deleted → FK cascade.
3. Prompt answered, either control → delete (consume — the prompt must not reappear on a re-read).
4. Recommendation deleted → FK cascade.
5. Recommendation author-`Rejected` (`RemoveAsync`) or taken down (`ServerModerationWriteService`'s
   removal path) → service-level sweep of every sources row naming it. A rec that cannot be displayed
   cannot be reminded, and an invisible rec must not collect credit. **Accepted:** `UnblockAsync` and a
   takedown reversal do not restore destroyed attributions. `NeedsRevision` is **not** a trigger — the
   read-time gate hides the prompt while the row survives the revision.

**The prompt (`IRecommendationReadService.GetHelpfulPromptAsync(storyId)` → `RecommendationDto?`).**
Returns the recommendation itself, because the widget shows it as a reminder — the RIL may be long ago.
Four gates, all at read time: the caller's sources row exists; the rec is visible (`Approved`, not taken
down, parent story passes `StoryVisibilityGuard`); `RecommenderId` is non-null (anonymous or
since-deleted recommenders get no prompt — gating at read time covers a deletion between RIL and read);
no `RecommendationSuccess` exists for (caller, rec). Two controls only: **Yes** →
`RecordSuccessAsync`; **X** → `DismissHelpfulPromptAsync` (a clear of the caller's own row: no
visibility guard, idempotent). There is no "No thanks" — X is the decline.

**`RecordSuccessAsync` requires and consumes the sources row** (service audit §2.4.1's credit-faucet
fix). Beyond the existing visibility guard, it requires the caller's sources row for that rec and the
rec `Approved ∧ ¬IsTakenDown`, else `KeyNotFoundException` (non-disclosure); the success row and the
sources-row delete commit in one `SaveChangesAsync`. An already-recorded success stays an idempotent
no-op but still deletes a lingering sources row.

**Unruled edge, status quo (roadmap decision row 18):** a recommender who RILs or direct-links their
*own* recommendation can collect a `SuccessfulRecCount` +1 on it; only the badge counter has an
anti-self-farm check.

## Structured Tag Authoring — Routing and Validation (WU37, reshaped by WU-TagFanon 2026-07-26)

### The overlay model — custom name vs. nuance (WU-TagFanon)

Every per-story tag association can carry a two-part **overlay**:

- **`CustomName`** (`[MaxLength(128)]`, nullable) — the author names a specific instance of an
  archetype tag ("Saura" on the `Bulbasaur` tag, "Aethon Region" on `Original Setting`). **Gated** by
  `Tag.AllowCustomName` (single flag; replaced `AllowOCDetails` + `AllowSettingDetails`), mod-set per
  tag wherever naming an instance makes sense. On `StoryCharacter`, a non-null `CustomName`
  additionally requires `IsOc` (an unnamed OC — `IsOc` true, `CustomName` null — is legitimate:
  "features an OC Bulbasaur"). Fanonized tags get `AllowCustomName = false` — a specific entity, like
  `Ash Ketchum`, must not be re-declared as someone else's character.
- **`Nuance`** (`[MaxLength(2048)]`, nullable, plain text, NOT sanitized HTML, NOT in FTS) — what
  *this story* does with the tag: an OC bio, "competent Ash", "slow burn, no love triangle" on
  `Romance`. **Never gated**, available on every tag type. Renders via indicator + hover/tap reveal
  on story page AND cards (load-bearing browse data — listing projections carry it).

**Do not call custom naming "identity"** — that word belongs to ASP.NET Core Identity sitewide.

### Per-story routing table

| Tag type | Per-story target | Entity |
|---|---|---|
| Genre, Setting, ContentWarning, CrossoverFandom | Flat junction, overlay on-row | `StoryTag` (`CustomName`, `Nuance` nullable columns) |
| Character | Dedicated entity (replaces StoryTag) | `StoryCharacter` (`IsOc`, `CustomName`, `Nuance`) |
| Pairing (ship) | Structural, named members | `StoryCharacterPairing` + `StoryCharacterPairingMember` |

`SettingDetail` is **deleted** (WU-TagFanon): the overlay is 0-or-1 per `(story, tag)` so it lives on
the junction row. `StoryCharacter` keeps its own table because the character overlay is 1-to-many —
a story may hold several custom-named characters of one species
(`UNIQUE (StoryId, CharacterTagId, CustomName)` with nulls-not-distinct) — and pairings must
reference *which* one (stable `StoryCharacterId`).

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
*type-name* level, but the table is kept as it still disambiguates the two *concepts* (character
pairing vs. story-to-story link). Scope caveat (MA-118): the WU42 rename stopped at type level —
member identifiers still carry "Relationship" (`StoryLineage.RelationshipTypeId`, nav
`RelationshipType`, `StoryLineageType`'s PK) because renaming them means a column-rename migration;
deferred as cosmetic, revisit pre-launch if at all.

### Story Lineage service (WU42, `Core/Stories/` + `Server/Stories/`)

`IStoryLineageReadService`/`IStoryLineageWriteService` — a cross-author request/approve workflow
(spec §939, Feature 10). A link where the requester owns only the source story is created `Pending`
and requires the **target** story's author to approve/reject via the owner-wide `/story-lineages`
page before it displays; a link where the requester owns both stories is created already `Approved`
(no notification — matches the notification drop-self invariant). Public reads
(`GetLineageForStoryAsync`) return only `Approved` rows where the queried story is the source — and
nothing at all unless the source story itself passes `StoryVisibilityGuard` (WU-AccessGateSweep2;
the bare `SourceStoryId` filter had made it an existence oracle) — joined through `Story` so a link
never survives display when its target fails the viewer's
`ContentRating`/`StoryStatus`/`IsTakenDown` filters (mirrors `ServerSeriesReadService.GetMembershipsForStoryAsync`'s
join-not-bare-projection rule — generalized as conditionality kind (g),
`identity-and-authorization.md` §"Parent-visibility guards"). Target-story selection goes through a new reusable
`IStoryReadService.SearchStoriesByTitleAsync` (`ILike` substring typeahead) — deliberately not the
discovery FTS (`StoryListing.SearchVector`, a whole-word-ranked GIN index tuned for browse relevance,
not incremental substring matching); the same search method also retrofits Groups' add-story picker.

### Write path — route by tag type

`StoryMappers.UpdateStoryEditableProperties` clears and rebuilds each per-story collection.
Route by `TagTypeId` on the incoming DTO:

```csharp
// StoryMappers.cs — structured routing (WU37 shape, reshaped WU-TagFanon 2026-07-26)
// Flat rows (Genre/ContentWarning/CrossoverFandom/Setting): overlay lives ON the junction row.
foreach (IStoryTag tempTag in tempStory.StoryTags)          // carries CustomName + Nuance
{
    StoryTag st = tempTag.ToStoryTag();
    if (tempTag.TagTypeEnum == TagTypeEnum.ContentWarning)
        st.Priority = TagPriority.Primary;                  // no priority picker for warnings
    actualStory.StoryTags.Add(st);
}

// Characters: dedicated entity, rebuilt in DTO order (IsOc / CustomName / Nuance on-row).
List<StoryCharacter> rebuilt = new();
foreach (StoryCharacterDto c in tempStory.StoryCharacters)
    rebuilt.Add(new StoryCharacter { CharacterTagId = c.CharacterTagId, Priority = c.Priority,
                                     IsOc = c.IsOc, CustomName = c.CustomName, Nuance = c.Nuance });

// Pairings reference rebuilt StoryCharacter rows BY INDEX, not tag id — WU-TagFanon: tag ids
// are ambiguous once two custom-named OCs share a species.
foreach (StoryCharacterPairingDto p in tempStory.StoryCharacterPairings)
    foreach (int index in p.MemberIndexes)
        pairing.Members.Add(new StoryCharacterPairingMember { StoryCharacter = rebuilt[index] });
```

**Character never touches `StoryTag`.** The former `SettingDetail` side-table is gone
(WU-TagFanon folded it onto the junction): a Setting's per-story custom name/description is just
`StoryTag.CustomName`/`Nuance` on its flat row, same as every other flat type.

### Validation — server re-reads gates from Tag

The write service calls `ServerStoryWriteService.ValidateStructuredTagsAsync` (or extends `CanSave()`)
after loading `Tag` rows for all referenced TagIds. **Never trust a DTO-carried gate value
(`AllowCustomName` — the single flag that replaced `AllowOCDetails`/`AllowSettingDetails`)** —
load fresh from the `Tag` table. Full rules table below.

### Legality rules — enforced at service layer

All rules are enforced by `ServerStoryWriteService` via `StoryValidationException` (same pattern as
`CanSave()` / author-gate). Server re-reads gates from `Tag` — never trusts client DTO values.

| Rule | Condition | Error |
|---|---|---|
| OC flag requires gate | `IsOc == true` but `Tag.AllowCustomName == false` | Reject |
| Character custom name requires OC flag | `StoryCharacter.CustomName != null` but `IsOc == false` | Reject |
| Flat custom name requires gate | `StoryTag.CustomName != null` but `Tag.AllowCustomName == false` | Reject |
| Nuance | — | Never gated, any tag type, either table |
| ContentWarning priority coercion | Priority != Primary | Coerce to Primary (not an error) |
| Pairing member count | Members < 2 | Reject |
| Pairing members in-story | Member `StoryCharacterId` must exist in this story's `StoryCharacters` | Reject |

No DB trigger (`TR_StoryCharacters_EnforceOCLogic` is SQL-Server-era; superseded). A DB CHECK is
post-MVP defense-in-depth if wanted.

### Priority

`TagPriority { Primary=0, Supporting=1 }`. Primary default. No `None` value. ContentWarning gets no
priority picker and its priority is coerced to `Primary` at service layer.

### Per-type filter branch in `ApplyFilters` (historical WU37 sketch)

**Historical design sketch — the two-arg signature below predates WU-ApplyFiltersPurity's current
signature (`ApplyFilters(query, filter, expansion, viewerId, hasFts)`, see "Random batch" above)
and the shipped code never partitioned by type this way** (the real predicate ORs `StoryCharacters`
and `StoryTags` per id in one pass — see `ServerStoryReadService.ApplyFilters`). Kept for the
provenance trail on why Character ids don't appear in `StoryTags`, not as current implementation
guidance.

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

### Tag Hierarchy Roll-Up (WU-TagFanon, 2026-07-26)

Filtering by a **parent tag matches the parent plus its children** — the query the rejected
`Cache_TagHierarchy` closure table presumed would exist (hierarchy is one level deep, so expansion is
a single lookup, no CTE). Rules:

- **Symmetric**: roll-up applies to include AND exclude. Excluding `Bulbasaur` excludes
  `Saura`-tagged stories — avoiding a species (or a parent ContentWarning) means avoiding its
  children.
- **Independent AND terms**: each included tag id expands to `{self} ∪ children` and becomes one
  `Any(expanded.Contains(...))` predicate. A story tagged only `Saura` satisfies a filter naming
  both `Bulbasaur` AND `Saura` (one row satisfies both terms).
- **Expansion comes from a cached `TagExpansionMap`, resolved before `ApplyFilters` runs (WU-
  ApplyFiltersPurity, 2026-07-30)**, never in the UI, so every consumer (discover, random batch,
  bookshelves, profile tabs) inherits it. `ServerStoryReadService.ResolveExpansionAsync(filter)`
  validates ship shape, then — only if the filter names any tag id — asks
  `ITagHierarchyReadService.GetExpansionMapAsync()` for the whole parent→children map. That map is a
  **process-local cache**: refreshed on any `Tag` write and bounded by a short absolute TTL, so
  reads are **eventually consistent with a bounded staleness window** (moderator edits land near-
  instantly at N=1 through the write service; up to the TTL for writes made outside it, e.g. a
  seeder or direct SQL, or across nodes at N≥2). One cycle of staleness is an accepted cost — see
  §"Reference-Data Caching" below for the full rationale and the conditions that license this.
  `ApplyFilters` itself receives the resolved map as a plain argument and is pure/synchronous — it
  does no I/O and knows nothing about caching.
- **Silent in the UI** — child tags share the parent's sprite (render-time fallback); the chip
  tooltip + sr-only text + child-ring carry the relationship (`layer4-style.md`).
- **Saved Tag Selections broaden over time** as children are added under a stored parent id —
  intended behavior of a persisted artifact, not a bug.
- **Ship filter** (`StoryFilterDto` pairing axis) inherits roll-up on its member character ids; ship
  filters are transient viewer intent, **never** persisted in `SavedTagSelection` (tag-axis-only,
  settled F15 scope).

### Reference-Data Caching — the tag-hierarchy precedent (WU-ApplyFiltersPurity)

The codebase's first in-process cache. Before caching any reference data, check it against all four
conditions below — **all four must hold**, not just some:

- **Tiny.** The whole dataset is small enough to hold in memory without a size/eviction policy (the
  tag hierarchy is ~136 rows today).
- **Viewer-independent.** No global query filter or per-viewer scoping applies to the underlying
  entity — verify this in the DbContext's `OnModelCreating`, don't assume it. A cache loaded in a
  background DI scope (no `HttpContext`, no authenticated viewer) must return exactly what a
  per-request query would return for *any* viewer, or the cache is unsound. (`Tag` has no
  `HasQueryFilter` and no soft-delete column — checked against
  `ReadOnlyApplicationDbContext.OnModelCreating`.)
- **Single write choke point.** Exactly one service (or a small, enumerable set) is the only path
  that writes the data, so invalidation has one place to hook.
- **One cycle of staleness is harmless.** The write is a rare, deliberate action (a moderator edit),
  and a reader briefly seeing the pre-write state is an acceptable, bounded cost — not a correctness
  violation.

**Mechanism:** a plain `volatile` snapshot field on a singleton, reloaded through a
`SemaphoreSlim(1, 1)` double-checked gate (warm reads take zero locks; concurrent cold reads collapse
to one load), invalidated by the write service after a successful `SaveChangesAsync`, and bounded by
a short absolute TTL on top (`Stopwatch`-based, monotonic — never wall-clock) so the cache also
self-heals against writes made outside the write service (seeders, direct SQL) and converges across
nodes at N≥2 with **no shared store**. This matches the repo's existing in-process-state idiom —
`ViewCountBuffer`/`ReadingProgressBuffer`/`UserActivityBuffer` are all plain fields on a singleton,
not a caching abstraction.

**Not `IMemoryCache` or `HybridCache`.** Neither has any precedent in this codebase (repo-wide grep
for `IMemoryCache|HybridCache|IDistributedCache` turns up nothing but a comment noting Redis is
post-MVP). `IMemoryCache` would need an `AddMemoryCache()` registration this project doesn't carry,
and buys nothing for a single entry with no eviction pressure. `HybridCache`'s entire value is its L2
(distributed) tier and cross-node stampede protection — there is no L2 store at N=1
(`horizontal-scaling.md`), and a `SemaphoreSlim` gives single-node stampede protection in a few lines.
Reach for either only once an actual distributed L2 exists to justify it.

**Singletons cannot inject scoped services.** `IDbContextFactory<ReadOnlyApplicationDbContext>` is
registered scoped, so a singleton cache takes `IServiceScopeFactory` and opens a short-lived scope
per load — the same discipline `ViewCountFlusher` already uses for its periodic flush.

**The anti-precedent stands as written.** `ISiteSettingsReadService`'s "Deliberately uncached: reads
are single-row…" is the contrast case, not something this convention supersedes — site settings and
tag hierarchy make opposite calls because they satisfy different subsets of the four conditions
above (settings reads are cheap enough that "instant on next read" beats caching; a settings write
is not necessarily rare). Do not cite this section to justify caching settings, or vice versa.

**Mandatory obligations for any cache built this way:**
- **Register its invalidation in `IntegrationTestBase.ResetSharedHostState`.** The integration test
  host is one process shared collection-wide (`testing.md` §"Integration test host is shared
  collection-wide"); a process-lifetime cache outlives Respawn's per-test row reset and will leak
  state across tests unless explicitly cleared. This is not optional — `ResetSharedHostState`'s own
  doc comment claims to enumerate every stateful singleton in the host, and a cache added without a
  matching reset falsifies that claim silently.
- **Re-verify viewer-independence if the cached entity ever gains a query filter.** The whole design
  is only sound as long as the "viewer-independent" condition holds; a future migration adding a
  filter to the cached entity (e.g. a soft-delete column) would silently break it.

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

## `AllowProfileComments` Gate (WU-AccessGateSweep2, 2026-09-30)

`User.PrivacySettings.AllowProfileComments` is the same `SocialInteractionPermission` enum, with the
same four tiers, and is enforced the same way: **in the write service**
(`ServerCommentWriteService.PostUserProfileCommentAsync`), on every post by anyone but the wall's
owner — root posts and replies alike. Until this WU the setting was honored only by the
`ProfilePage` dispatcher (which hides the wall for `Nobody`), so a direct POST bypassed it — the
affordance-not-control anti-pattern (`identity-and-authorization.md` §"Security vs affordance").

| Tier | Enforcement |
|---|---|
| `Public` / `UsersOnly` | Allow (posting already requires authentication). |
| `Following` | The owner must follow the commenter: `writeDb.FollowedUsers.AnyAsync(f => f.UserId == ownerId && f.FollowedUserId == commenterId)`. Refused otherwise: "This user only accepts profile comments from people they follow." |
| `Nobody` | Refuse: "This user isn't accepting profile comments." |
| unknown value | Refuse (fail closed), same message as `Nobody`. |

- **Order:** after the authentication check, the rate-limit token and `CanSave`, and after the
  `ProfileVisibility` guard — a hidden profile stays an indistinguishable `KeyNotFoundException`
  (404), never a "not accepting comments" message that would confirm it — and before the
  reply-parent check and sanitization.
- **Refusal type:** `CommentValidationException` (400 with user-facing detail), which
  `ClientCommentWriteService` already reconstructs — so WASM parity needs no client work. Messaging's
  dedicated `MessagingPermissionException` (403) was not copied: a new exception type would need its
  own `EndpointHelpers` arm and client reconstruction for no behavioral gain.
- **Reads are not gated by it.** `GetUserProfileCommentsAsync` is `ProfileVisibility`-gated only;
  the UI hides the wall for `Nobody`. Whether `Nobody` should also withhold existing wall comments at
  the API is unruled (tracker **F10**).

## Conversation Archiving Is Sticky, Never Auto-Cleared (WU-MsgArchive)

`ConversationParticipant.IsArchived` is a **per-participant, unilateral, non-destructive** flag —
archiving affects only the caller's own row (`SetArchivedAsync` scopes by `viewerId`); the other
party's view of the thread is untouched and they can still read and reply. Since no delete exists
for a conversation and no block exists for an *established* thread (the `AllowPrivateMessages` gate
above applies only at `StartConversationAsync`), **archive is the only disposal gesture a user has.**
Three rules follow, all already true in code — do not "fix" any of them:

| Rule | Where | Why |
|---|---|---|
| A new inbound message **never** clears `IsArchived`. | Nothing in `SendMessageAsync` touches it. | Gmail-style raise-on-reply was considered and **rejected**: with no block available, it lets a persistent unwanted correspondent drag a thread back into the inbox indefinitely, so archiving would grant no actual relief. |
| `GetUnreadConversationCountAsync` excludes archived rows. | `ServerMessagingReadService` — `.Where(cp => … && !cp.IsArchived)`. | This is the *muting* half. The global nav badge stays quiet for archived threads by design. |
| Per-conversation `UnreadCount` **stays populated** for archived rows. | The `GetConversationsAsync` projection computes it for every returned row, archived or not. | This is what keeps sticky archiving honest — the Archived view surfaces the unread count, so a legitimate reply is discoverable rather than silently lost. Do not suppress it in the archived projection. |

Net semantic: **archiving mutes, it does not delete or defer.** Unarchiving is always an explicit
user act.

**Conversation listing is scoped, ID-first, and unpaged (WU-MsgReadPath, 2026-07-26).**
`GetConversationsAsync(ConversationScope scope = Active)` — scopes are **disjoint** (`Active` =
inbox, `Archived` = archived only; deliberately no "all" member — no UI merges the lists, and a
merged mode would reinvite fetch-everything-and-filter-client-side). `ConversationSummaryDto`
carries **no `IsArchived`**: scope implies it; the per-thread flag lives on
`ConversationThreadDto.IsArchived` for direct-URL navigation.

The read is **two-step** (the same page-the-ids-then-hydrate idiom as the thread query):

1. **Metadata step** — filter `(user_id, is_archived)`, project `conversation_id` +
   `MAX(date_sent)`, `ORDER BY (max IS NOT NULL) DESC, max DESC`, return ids only (~4 bytes/row).
   **The first ordering key is load-bearing:** PostgreSQL defaults to `NULLS FIRST` for
   `ORDER BY … DESC`, so a single-key sort would promote message-less conversations to the top;
   the contract is they sort **last**. Any future page window is a `Skip/Take` on this step —
   nothing else changes.
2. **Hydration step** — for exactly those ids: other participant, unread count, and the last
   message's `SUBSTRING(message_text, 1, 2048)` (`PreviewFetchPrefixChars`) — never the whole
   body; the preview is ≤100 plain-text chars, so unbounded `MessageText` transfer is waste.
   `MakePreview` guards against a SQL-bisected trailing tag fragment.
   No SQL ORDER BY here; rows are reassembled in step-1 order in C#.

> **Where the preview `Substring` sits is measured, not stylistic — do not "simplify" it.**
> Write it in the **outer projection**, applied to a scalar subquery result:
> `…PrivateMessages.OrderByDescending(m => m.DateSent).Select(m => m.MessageText).FirstOrDefault()!.Substring(0, N)`.
> Two things follow from that exact shape, both verified 2026-07-26 (`ToQueryString` + PerfBaseline
> + `EXPLAIN (ANALYZE, BUFFERS)` on a 400-conversation / 8.5k-message / 28 MB inbox):
> - EF emits a **correlated `ORDER BY … LIMIT 1` subquery** (an index seek on
>   `ix_private_messages_conversation_id_date_sent`), *not* a `ROW_NUMBER()` window over the whole
>   `private_messages` table. Selecting multiple columns in one `FirstOrDefault` projection is what
>   triggers the window form — splitting date and text into two scalar subqueries is what avoids it.
> - Putting the `Substring` **inside** the `FirstOrDefault` projection pushes it into that window,
>   where Postgres evaluates it on **every** message row before row elimination — forcing a detoast
>   per row. Measured cost of that mistake: hydration 2.53 ms → 10.55 ms (`WindowAgg` 0.88 ms →
>   5.96 ms); the whole listing went from a 45 % improvement to an 88 % regression versus the
>   pre-rework single query. This exact regression shipped once and was caught only by measuring.
>
> Numbers, both runs, and EXPLAIN plans: `TheCanalaveLibrary.PerfBaseline/results/msgreadpath*.json`
> (+ `explain-*` dirs). Regenerate with
> `TheCanalaveLibrary.PerfBaseline/seed-messaging-volume.sql` — SeedTool generates no messaging
> volume, which is why F49's L6 cells were flipped unmeasured in the first place.

Unpaged stays correct because conversation counts are bounded by human effort (someone must start
each one), unlike machine-generated notifications, which are paged. Step 1's two ordering keys emit
as correlated `MAX()` subqueries (min/max seeks on the same index).

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
    Task UpdateAppearanceAsync(int themeId, bool prefersAnimated);
    Task<string> UploadProfilePictureAsync(Stream content, string contentType);
}
```

**Rules that make this exception safe:**
1. **No `userId` parameter on any method.** The service resolves the target entirely from
   `IActiveUserContext` — it is, by contract, "the currently authenticated user's settings."
2. **Authentication guard is mandatory.** Every method must call the shared `ActiveUser.RequireUserId()` (or
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

## Story Lifecycle — transition table, trust waiver, publish anchors (WU-StoryLifecycle; D1/D2)

Owner rulings D1 and D2 (`.claude/design/audit-decision-worksheet.md`, answered 2026-08-04), built
WU-StoryLifecycle 2026-09-30. **The approval queue exists for spam prevention only** — not editorial
standards, not tag/rating sanity. Its guarantee is that no spam story ever reaches a reader; it gates an
author's *first* submission, because spam is an account-level property.

**Status is never a property edit.** `StoryStatusId` is not on `IEditableStoryProperties` or
`CreateStoryDTO`, and the shared mapper never copies it. `StoryUpdateDTO.StoryStatusId` survives only as a
read echo from `GetStoryForEditAsync`; `UpdateStoryAsync` ignores it. A new story is always `Draft`,
server-stamped in `CreateStoryAsync` beside `AuthorId`. Every lifecycle move goes through
`IStoryWriteService.TransitionStatusAsync` (author) or `ApproveStoryAsync`/`RejectStoryAsync` (moderator).
`PostApprovalStatus` *is* a property: editable in every status, and `CanSave` accepts any **defined**
value. Entry-set membership is checked only at submit and at approve — checking it at save would break
every legacy published story whose value is, say, `OnHiatus`. Editing a `PendingApproval` story is
allowed and does not re-queue (the moderator approves the row as it stands).

**Terms** (`Core/Stories/StoryLifecycle.cs`): **published set** = `InProgress(2)..OpenBeta(7)`
(`IsPublished`); **entry set** = `{InProgress, Completed, OpenBeta}` (`IsEntryStatus` —
`CanSubmitForApproval` calls it); **trusted** = `User.ApprovedStorySubmissions >= 1 && User.CanAutoApprove`.

**Author transition table** — `StoryLifecycle.ResolveAuthorTransition(current, target,
postApprovalStatus, trusted)`, a pure function the server applies (Unit-covered exhaustively):

| current | target | result |
|---|---|---|
| any | undefined enum value | error |
| X | X | no-op (returns X, writes nothing) |
| `Draft` | `PendingApproval` | error unless `IsEntryStatus(postApprovalStatus)` (the `CanSubmitForApproval` text); **trusted → `postApprovalStatus`** (the waiver); otherwise `PendingApproval` |
| `PendingApproval` | `Draft` | `Draft` (withdraw) |
| `Rejected` | `Draft` | `Draft` (revise — uncapped) |
| published P | published P′ | P′ (any→any within the set; supersedes spec §5.1's narrower graph) |
| published P | `Draft` | `Draft` (unpublish) |
| anything else | — | error — covers Draft→published directly, →`Rejected`, `PendingApproval`→published (self-approve), `Rejected`→`PendingApproval`/published |

The waiver is **routing, not a second action**: there is one author "submit", and a trusted author's
submit lands at `PostApprovalStatus`. So unpublish re-enters the gate, and the waiver skips it for trusted
authors — approve-once-then-rewrite is closed only for an author's **first** story;
`CanAutoApprove` revocation is the lever if that matters. The waiver never increments
`ApprovedStorySubmissions` — only moderator approval does.

**Moderator transitions — only from `PendingApproval`.** Approve → `PostApprovalStatus`, re-validated with
`IsEntryStatus` (closes the approve-into-`Draft` hole). Reject → `Rejected`. **`Rejected` is reachable
only from `PendingApproval`**; published content is removed only via `IsTakenDown`. Confining rejection
to pre-publication is what D1 relies on to keep the two invisibility mechanisms from overlapping, so the
non-overlap is **enforced, not assumed** (WU-StoryLifecycle review fixes, 2026-09-30 — derived from D1's
rationale, flagged): **a taken-down story's status is frozen until the takedown is reversed.**
`TransitionStatusAsync` refuses every author move on an `IsTakenDown` row (without this, an author could
unpublish a taken-down story over the API, resubmit it, and put it in front of a moderator), approve and
reject refuse an `IsTakenDown` row (reject would otherwise overwrite the takedown's own
`TakedownReason`/`TakedownDate` and leave a `Rejected` story under the takedown), and the pending queue
keeps the `IsTakenDown` filter on, so such a row never appears there. All three conditional updates also
carry `!IsTakenDown` in their `WHERE`, so a takedown landing between the read and the write affects 0
rows. Reversing a takedown therefore always restores the story exactly as it was. Approve also requires a **live author** and refuses when
`AuthorId` is null (deleted — D13 hard delete leaves the FK `SetNull`), the author is `Banned`, or the
author is `Suspended` with a null or future `SuspendedUntilUtc` (deliberately stricter than
`CanalaveSignInManager` on the null-date case). Reject is unguarded, so a moderator can always clear the
queue. Refusals are `ModerationValidationException` (400, user-facing); a missing story is
`KeyNotFoundException`.

**Guard shape — every lifecycle write is conditional on the status it read.** One `ExecuteUpdateAsync`
`WHERE story_id = @id AND story_status_id = @current`; 0 rows affected throws a user-facing validation
exception ("reload" for the author, "already handled" for a moderator) instead of overwriting. This closes
double-approve, approve-vs-withdraw and the author's own lost update. Approve wraps the status flip and the
author's `ApprovedStorySubmissions + 1` in one `CreateExecutionStrategy().ExecuteAsync` + transaction (0
rows → throw, nothing incremented); the `StoryApproved` notification stays best-effort after commit. No
optimistic-concurrency token — D30 is pending.

**Publish anchors (D2).** `Story.PublishedDate` is nullable, and **NULL = never published on this site**.
It is stamped `PublishedDate ?? now` on every transition into the published set (trusted submit,
moderator approve) and therefore **never re-stamped**: a story unpublished and later republished keeps
its original date. Republication is not a publication event, and never re-stamping is what enforces the
anti-bump rule. `Story.SubmittedDate` (nullable) is stamped on each →`PendingApproval`; the moderator
queue orders by it. For chapters the anchor is chapter-level: `Chapter.FirstPublishedDate` is stamped once
on the chapter's first `IsPublished` false→true and never moved (unpublish and republish both keep it). It
is the "New"-badge input and the new-chapter fan-out anchor. `ChapterContent.PublishDate` (nullable) is
**per-version provenance only** — when *this version* became publicly readable (stamped on first chapter
publish for every version still null, and at creation for an alternate added to an already-published
chapter). No discovery or recency surface reads it: adding or promoting a version is an update, never a
publish event. Invariants, maintained in code (the CHECK constraints are routed to WU-SchemaHardening):
published status ⇒ `PublishedDate != null`; `IsPublished ⇒ FirstPublishedDate != null`. (D2 wrote
`IsPublished == (FirstPublishedDate != null)`, which cannot hold together with "never moved" and the
legal chapter unpublish.)

**Site-local vs. provenance — imports never backdate.** `PublishedDate`/`FirstPublishedDate` always mean
"went live on this site". `OriginalPublishedDate`/`ChapterContent.OriginalPublishDate` are display-only
provenance and are never copied into a site-local column; an import's arrival sorts as a genuine
publication.

**A status move is not a content update.** `TransitionStatusAsync` never touches `LastUpdatedDate`
(touching it would reopen the unpublish/republish bump vector) and never writes `IsTakenDown` (an
orthogonal axis — it only reads it, to refuse moves on a taken-down story; see above).

**Ratified riders (D1).** A post-approval rating raise is handled reactively only (the report path).
Chapter-level gating never happens. **Import verification never takes the waiver** — true today because
authorship verification is the decoupled per-link ExternalVerification queue, which consults no trust
state; no future code may make it consult one.

**Accepted sort consequence.** Postgres `DESC` puts NULLs first. Because null-dated rows are visible only
to their own author (the invariant plus the `StoryStatus` filter), an author sees their never-published
drafts at the top of their own DatePublished views. Do not add a NULLS LAST tweak — it would defeat
`ix_stories_published_date`.

The trust counter itself is a record of a decision, not a derived counter — see §"UserStats Updates"
→ "Records of a decision are not counters".

## Moderation Services

Three interfaces in `Core/Moderation/`, server impls in `Server/Moderation/`:
- **`IReportSubmissionService`** — `GetReportReasonsAsync` + `SubmitReportAsync`, the only two
  operations a member performs. `ReportDialog` (a cross-cutting leaf on public pages) injects this,
  never a mod interface.
- **`IModerationReadService`** — the moderator queues and the per-user history.
- **`IModerationWriteService : IModerationReadService`** — the moderator actions.

The split is owner ruling **D9** (2026-08-06, built WU-ModerationIntegrity 2026-09-30): P1's "inject
the narrowest interface" applied at the type level, so a public page never compiles against the
hard-delete/ban surface. It is a compile-time discipline, **not** a runtime control — the read gates
below are the control. One concrete class, `ServerModerationWriteService`, implements all three; it is
registered once and all three interfaces forward to it (§"Registering an inherited pair" — the
`ISavedTagSelectionWriteService` shape). This follows the current registration rule; D36 (canonical DI
shape) is pending and may revisit it. On WASM the twins are `ClientReportSubmissionService` (its
`rateLimitedAction: WriteActionKind.Report` makes a 429 a `WriteRateLimitExceededException`) and
`ClientModerationRead/WriteService`.

**DAG position.** `ServerModerationWriteService` injects `INotificationWriteService` (the standard
cross-feature dep), `IWriteRateLimitService`, `UserManager<User>` (the security-stamp bump) and
`IActiveUserContext`. It injects **no** feature service, read or write: target and author resolution
read the unfiltered write context directly (`LoadModeratableAsync`, `ResolveAnswerableUserIdAsync`).
(Corrected WU-ModerationIntegrity, 2026-09-30: this paragraph used to say it composes feature read
services such as `IStoryReadService`; no version of the service ever did.)

**Every moderator-only operation gates in the service — reads included (D9).** The read service takes
`IActiveUserContext` and exposes it as `protected ActiveUser` (§"CS9107/CS9124"); `GetReportQueueAsync`,
`GetPendingSubmissionsAsync` and `GetUserModerationHistoryAsync` open with `ActiveUser.RequireModerator()`,
the shared guard in `Core/Identity/ActiveUserContextExtensions.cs` (anonymous →
`InvalidOperationException` → 401; signed in without the Moderator or Admin role →
`UnauthorizedAccessException` → 403; returns the moderator's id). Every write uses the same guard.
`GetReportReasonsAsync` stays ungated: it feeds `ReportDialog` for any reporter. The full mod-only
surface and the one deliberate non-gate are listed in `identity-and-authorization.md` §"Role-Based
(Moderator) Gating".

**Soft-delete (takedown) visibility filter `"IsTakenDown"`.** Each removable entity registers
`HasQueryFilter("IsTakenDown", e => !e.IsTakenDown)` in `OnModelCreating`. Public reads go through the
filter automatically. Author views and mod review paths use `IgnoreQueryFilters(["IsTakenDown"])` (one
deliberate exception: the pending-submissions queue keeps the filter on, so a taken-down story never
reaches approve/reject — §"Story Lifecycle"). The
filter composes alongside `"ContentRating"` and `"GroupAudience"` on entities that have multiple filters.
See `content-safety.md` "Content Removal" for the column naming rationale and moderator filter behavior.

**`IModeratableContent` interface.** `Story`, `BaseComment`, `BaseBlogPost`, `Recommendation` each implement
this interface exposing `IsTakenDown`, `TakedownDate`, `TakedownReason`, `ActiveReportCount`, and
`AuthorUserId`. `ServerModerationWriteService` loads via a single per-type switch (`LoadModeratableAsync`)
then mutates through the interface — no repeated switch per operation.

**The entity carries current state; the `Report` row carries history (owner ruling D8(b)).** No
takedown-history table, ever. When takedown reversal is built (it does not exist yet — nothing writes
`IsTakenDown = false`), it nulls `TakedownDate` and `TakedownReason` along with the flag rather than
leaving stale metadata behind as a pseudo-history, and it does **not** reopen the sibling reports the
removal closed. Content that is again problematic is reported again.

### `ActiveReportCount` — what it means (owner ruling D7)

`ActiveReportCount` on `Story`, `BaseComment`, `BaseBlogPost`, `Recommendation` and `User` is **a cache
of `COUNT(*) FROM reports WHERE (reported_entity_type, reported_entity_id) = target AND
report_status_id IN (Open, UnderReview)`** — how many unanswered questions stand against the target,
which is what the queue's triage sort claims to rank. It is a *derived* counter in D21's sense: that
`COUNT(*)` is its ground truth, so it is recomputable (the WU_ModerationIntegrity migration ran the
recompute once; the standing reconciler belongs to WU-CounterSymmetry, on the partial index
`ix_reports_open_target`). `PrivateMessage` has no column.

**`AdjustActiveReportCountAsync(type, id, delta)`** is the single authority on mutating it: a
per-DbSet `ExecuteUpdateAsync` on the unfiltered write context (no `IgnoreQueryFilters` needed — the
write context has no filters), a no-op for `Message`. Never increment or decrement at a call site.
(Corrected WU-ModerationIntegrity, 2026-09-30: this paragraph used to say the switch calls
`IgnoreQueryFilters`.)

### Report submission (`SubmitReportAsync`)

In order: the target-type allow-set (`ModerationValidationException`, 400) → the authenticated-axis
throttle → kind (g) target existence and visibility (`KeyNotFoundException`; `identity-and-authorization.md`)
→ the duplicate check → resolve `ReportedUserId` → `Reports.Add` → `SaveChangesAsync` → **then**
`AdjustActiveReportCountAsync(+1)` → the `ReportReceived` receipt.
- **Primary write first, counter second (D22).** The row commits, then the counter moves. No
  transaction wraps the pair: a missing `+1` after a crash is transient drift the recompute heals,
  whereas the old order (counter first) could leave a `+1` with no row and nothing to recompute from.
- **One open report per reporter per target (service §2.4.4(c)).** The partial unique index
  `ix_reports_open_reporter_target` on `(reporter_user_id, reported_entity_type, reported_entity_id)
  WHERE report_status_id IN (0, 1)` enforces it; the service checks first and also catches the
  index's `23505` on save (the race), answering both with `ModerationValidationException("You've already
  reported this — a moderator will review your open report.")`. Once that report is resolved the
  reporter may file again. Anonymous reports are not deduped: a NULL reporter is distinct in the index.
  The index is also what makes D7's "notify every sibling reporter" exactly-once by construction.

**`ReportedUserId` (owner ruling D8) — the account answerable for the reported artifact at the moment
the report was filed.** Nullable FK → `AspNetUsers`, `ON DELETE SET NULL` (reports outlive the accounts
they name). Populated at write time **for every target type**: `User` → that user; `Message` → the
sender; `Story`/`Comment`/`BlogPost`/`Recommendation` → the author. It is a **snapshot, never
re-resolved** — nothing that reassigns ownership rewrites report history, and a later session must not
"correct" it as drift. NULL means unknown, anonymized or deleted, never "has no owner" (a founderless
`Group`, once D13 adds that target, is the one designed NULL — WU-UserDeletion adds its resolver arm).
Every producer sets it: submission (resolved), and the moderator-filed rows (`ApplyAccountActionToUserAsync`,
`SetCanAutoApproveAsync`, `ReinstateUserAsync`), which set it to their target.
- **One resolver, two forms.** `ResolveAnswerableUserIdAsync(type, id)` returns `int?`; its default arm
  **throws**, so a new `ReportedEntityType` member fails loudly until it gets its arm. Submission uses the
  nullable form and never throws on NULL (anonymous or deleted-author content stays reportable). The
  account-action path wraps it — `ResolveActionTargetUserIdAsync` — and throws
  `ModerationValidationException` on NULL ("there's no account to act on").
- **The per-user history is one predicate.** `GetUserModerationHistoryAsync` reads
  `Reports.Where(r => r.ReportedUserId == userId)` — every target type, newest first, each row labelled
  through the queue's batch enrichment. The ledger outlives its targets, so a row whose target no longer
  materializes is kept and labelled `[deleted {type}]` with no link.

### Resolve paths — lock, guard, then transition (service §2.1.2)

`ResolveNoActionAsync`, `ResolveWithRemovalAsync` and `ApplyAccountActionAsync` each run inside one
execution-strategy transaction (the Spotlight template: `CreateExecutionStrategy().ExecuteAsync`,
`ChangeTracker.Clear()` first, `BeginTransactionAsync` inside) and open with
`LockResolvableReportAsync`: `SELECT * FROM reports WHERE report_id = … FOR UPDATE` via `FromSql`
(materialized with `ToListAsync`, never composed further — EF would wrap the locking query as a
subquery). Missing → `KeyNotFoundException` (404); status not `Open`/`UnderReview` →
`ModerationValidationException("This report has already been resolved.")` (400). The row lock
serializes a second moderator: they block, then read the committed status and are refused — so the
counter is decremented only on the actual transition, without a concurrency token (D30 pending). The
counter adjustment runs **inside** the transaction (the row lock needs one anyway; compatible with
D22). **Notifications run after the strategy call returns, never inside the retried delegate**, each in
its own best-effort `try/catch`. A `ClaimReportAsync` claim is triage bookkeeping, not a lock.

### Sibling closing on removal (owner ruling D7)

**`ResolveWithRemovalAsync` closes every other Open|UnderReview report on the same target, in the same
transaction — soft takedown and hard delete alike.** The target is the pair `(ReportedEntityType,
ReportedEntityId)`: never the id alone (story 5 and comment 5 are different targets) and never the
target's author.
- **Removal only — the criterion is answerability.** After a removal a sibling has no decision left in
  it. `ResolveNoActionAsync` does not close siblings (one moderator's "no" is a ruling on one complaint);
  neither account-action path bulk-closes (the content stays live and each report still asks a live
  question). A sibling claimed `UnderReview` by another moderator closes anyway.
- **Mechanism, in order:** lock and guard the primary → refuse a `User` target
  (`ModerationValidationException`: "User reports are resolved with an account action, not a removal" —
  `ApplyRemovalAsync` has no `User` branch, and D7 forbids bulk-closing a user's reports) → apply the
  removal or hard delete → set the primary report's fields (tracked) → lock the siblings
  (`FOR UPDATE`, capturing `(ReportId, ReporterUserId)`) → one `ExecuteUpdateAsync` over those ids that
  are still Open|UnderReview, setting `ResolvedActionTaken`, the acting moderator, the **same**
  `DateResolved`, and `ActionTaken = "Closed with report #{id}: {reason}"` (truncated to the column's
  1024) → **N = rows affected** → `AdjustActiveReportCountAsync(target, −(1 + N))` → `SaveChangesAsync`
  → commit. The primary row is tracked and excluded from the bulk update (no tracked-versus-set
  collision). Nothing is deleted from `reports`.
- **A hard delete clears the target's TPT dependents first** (owner ruling D10, WU-TptHardDelete):
  `ApplyHardDeleteAsync` runs `TptDelete.StoryCommentsAsync` (Story) or
  `TptDelete.BlogPostDependentsAsync` (BlogPost) before `Remove`, inside the same transaction — the
  helper's SQL lands at once, the `Remove` at the save above. A Comment, Recommendation or Message is a
  loaded entity whose own removal deletes both its rows. See §"Hard deletes of content parents".
- **Derive the delta from rows transitioned; never zero the column.** A report filed concurrently keeps
  its own `+1` and stays open. Hard delete's counter step is vacuous (the row dies in the same save);
  `Message` gets the sibling half and the no-op counter half.
- **Siblings resolve as `ResolvedActionTaken`** — action was taken on their target; "no action" would
  be false. **Every distinct non-null sibling reporter is notified** with `ReportResolved` (81) carrying
  *their own* report id (D4's dedup never collapses it), and the content author gets `ContentRemoved`
  (70); the acting moderator is skipped everywhere (§"Notification Generation" → guardrail).
- **Accepted consequences, not to be re-litigated:** a takedown reversal does not reopen the closed
  siblings (D8(b)); a reporter whose report was closed by another report's resolution is told
  "resolved, action taken" without being told which report drove it (D5's posture).

### Zombie reports — closed at the source (D7 sub-edge, WU-ModerationIntegrity's pick)

Reports carry no FK to their polymorphic target, so a report outlives a target destroyed outside
moderation — and the queue silently drops rows whose target no longer materializes, leaving them Open,
invisible and unresolvable. **Close them where the target is destroyed, in that transaction, never by
a later reconciler** (D13's "clean up at the source where the source is knowable and transactional";
the counter reconciler recomputes counts, not statuses). The helper is
`ReportLedger.CloseForDestroyedTargetsAsync(db, type, ids, note)` (`Server/Moderation/ReportLedger.cs`):
one `ExecuteUpdateAsync` setting `ResolvedNoAction`, a NULL moderator, `DateResolved = now` and the note.
**No notification and no counter call** — the counters die with their rows, and both outcome texts
(81, 82) would claim a moderator review that never happened. `ResolvedNoAction` with a NULL moderator is
the honest ledger entry: `ResolvedActionTaken` would read as a prior sanction in D8's history (the ban
signal), and the NULL moderator plus the note distinguish it from a moderator's "no".
- **Wired at:** `UserDeletionService.DeleteUserAsync` — the comments on the deleted user's profile and
  the `User` target itself.
- **Not wired (owner-open, tracker F13):** the author self-delete sites (comment, recommendation, blog
  post, site post, chapter, the D15 story delete) and the TPT child comments a hard delete destroys.
  Each is a one-line call once the owner rules on author-delete report status.

### Account actions — target resolution and the report-as-audit-record rule (WU-UserModeration)

**Every account action leaves a `Report` row, and that row IS the audit record.** There is no separate
moderation-action table: `ModeratorUserId`, `ActionTaken`, and `DateResolved` live on `Report`. Two
consequences bind all new work:

1. **A moderator acting without a member report creates one.** `ApplyAccountActionToUserAsync` opens a
   `Report` and resolves it in the same unit of work — `ReportedEntityType.User`, `ReporterUserId = modId`,
   `ReportedUserId = target`, `ReportStatusId = ResolvedActionTaken`. **`ReporterUserId ==
   ModeratorUserId` is what marks a report as
   moderator-initiated** — do not add a flag column or a synthetic "Moderator-initiated" `ReportReason`
   seed row; the moderator picks a real reason from the existing seeded set, which is more useful in the
   audit trail than a generic one. Because the row is opened and resolved together, `ActiveReportCount`
   is untouched (no +1/−1 pair).
   **Auto-approve revoke/restore follows the same rule (WU-StoryLifecycle, 2026-09-30; D1).**
   `SetCanAutoApproveAsync(targetUserId, canAutoApprove, reasonId, reason)` is a moderator-initiated
   action on a user, so it files the same kind of row: `User` target, `ReporterUserId == ModeratorUserId`,
   `ResolvedActionTaken`, `ActionTaken = "Auto-approve revoked: {reason}"` / `"…restored: {reason}"`.
   Same guards as `ApplyAccountActionToUserAsync` (moderator gate, self-target and unknown reason →
   `ModerationValidationException`, unknown user → `KeyNotFoundException`); an unchanged value is a no-op
   that writes no row. No notification (owner silent). Restore exists because a one-way moderator lever is
   the "irreversible in-app" defect class. See §"Story Lifecycle" for what the flag gates.
2. **The action's target user is resolved from the report, not assumed to be the report's target.**
   `ResolveActionTargetUserIdAsync` maps `User` → the reported user; `Story`/`Comment`/`BlogPost`/
   `Recommendation` → the reported content's author; `Message` → the message's sender (the throwing form
   of `ResolveAnswerableUserIdAsync`, above). This is what a moderator means when warning or suspending
   over a reported story. An unresolvable author (anonymous or deleted) throws a
   `CanalaveValidationException` — a user-facing type, so the moderator sees the real reason rather than
   `ExceptionPresenter`'s generic message.

   *(Supersedes the WU34 rule "account actions require the report target to be a User." Under that rule
   the Warn control on `/mod/reports` threw for every report the app could actually produce.)*

**The report-driven account action is a resolve path.** `ApplyAccountActionAsync` takes the lock and
guard above, decrements `ActiveReportCount` by 1, and — like the other two resolve paths — tells a
member reporter the outcome: `ReportResolved` (81) with the report id, after commit, skipped when the
reporter is the acting moderator (spec §5.21 "reporters always learn the outcome"; built
WU-ModerationIntegrity). It closes no siblings (D7). Inside the transaction the counter step runs after
the security-stamp bump: `UserManager` writes every column of the tracked user, so a `User`-target
decrement made before it would be overwritten with the loaded value.

**The account-status transition table (service §2.1.3; built WU-ModerationIntegrity).** Enforced in
both `ApplyAccountAction*` entry points before any mutation; a violation is
`ModerationValidationException`. A *live* suspension is `Suspended` with `SuspendedUntilUtc > now`.

| Action | Allowed from | Refused |
|---|---|---|
| Warn | Active, Warned, an expired suspension | a live suspension *(derived — decision row 20)*; Banned |
| Suspend | any status except Banned; re-dating a live suspension is allowed | Banned; a missing or past end date ("Choose a suspension end date in the future.") |
| Ban | any status except Banned | Banned, on the moderator-initiated path ("already banned" — *derived, decision row 20*: a second type-74 row, since D4 exempts 74 from dedup). On the report-driven path a Ban on a banned account is **not** refused — see "A standing ban answers a report" below. |
| Reinstate | Warned, Suspended (live or expired), Banned | Active ("already active") |

- **Banned is leavable only via Reinstate** (literal §2.1.3). A warning can no longer silently unban
  anyone.
- **A standing ban answers a report** (*derived, decision row 20*; WU-ModerationIntegrity review fixes,
  2026-09-30). `ApplyAccountActionAsync(report, BanUser)` on an account that is already banned resolves
  the report against the ban in place: `ResolvedActionTaken`, the moderator's reason, −1 on the count,
  and 81 to the member reporter — but no status write, no security-stamp bump and no second 74. Without
  it, account actions closing no siblings (D7) plus Warn/Suspend refused on Banned (literal) plus
  removal refused for a `User` target would leave every other report about a banned account closable
  only as "no action" (82) — a false outcome for the reporter, and a `ResolvedNoAction` row in D8's
  per-user history. The moderator-initiated path keeps the refusal: it has no report to answer.
- **`SuspendedUntilUtc` is set only when the resulting status is `Suspended`, and cleared to NULL
  otherwise** — so "set only while Suspended" (`UserModerationHistoryDto`) is true of every row a
  moderator action writes. A suspension with a NULL or past date can no longer be written.
- **`ReinstateUserAsync(targetUserId, reason)`** — the only path that writes `Active` back. Guards:
  moderator; not self; unknown user → `KeyNotFoundException`; a non-blank reason within the 1024-character
  `ActionTaken` column. Effect: `Active`, `SuspendedUntilUtc = NULL`, plus one opened-and-resolved
  moderator-filed `Report` (`User` target, reason **Other** — seeded id 1, the administrative-row
  precedent; `Notes = ActionTaken = reason`; `ReportedUserId = target`). `ActiveReportCount` is
  untouched; no security-stamp bump; **no notification** (no type exists — tracker F14). Both
  `ApplyAccountAction*` paths refuse `ModeratorActionType.ReinstateUser` ("use Reinstate").
- Expired suspensions do **not** normalize to `Active` on their own (`security.md`, "no lazy restore") —
  Reinstate is the lever.

### Exception translation in these files (D9 sub-edge, WU-ModerationIntegrity's pick)

The 401-instead-of-404/400 class (service §2.12) is fixed **at the throw sites**, not in the shared
`EndpointHelpers` table: a client-supplied id that does not exist is `SingleOrDefaultAsync(...) ??
throw new KeyNotFoundException(...)` (404), and a business rule is the feature's validation exception —
`ModerationValidationException`, or `ExternalVerificationValidationException` for Feature 53 (400).
`InvalidOperationException → 401` stays the auth safety net (`ActiveUserContextExtensions` says "do not
change it"; `layer5-wasm.md` §"The Error-Translation Contract"); precedent
`modernization-audit/deferred-work.md` §4 (MA-505/MA-611, the typed `*ValidationException` route).

## Synchronous Inline Badge Awards (WU36; no-tiers model WU-StatBadgeProducers)

A write service that triggers a badge-eligible event calls `IBadgeWriteService.AwardAsync` after the
primary `SaveChangesAsync`, **best-effort** — inside a `try/catch` so a badge failure never rolls back
the primary operation. `AwardAsync` is **idempotent** (no-op if already earned); the caller does NOT
need a separate "has badge" check first.

**No tiers (settled, WU-StatBadgeProducers, 2026-07-30 — supersedes WU36's Bronze/Silver model).** A
badge is earned at **≥1** and displays its `UserBadge.EarnedCount`. The tier paradigm had no design
provenance (traced to a single unrequested AI transcript turn, never revisited — see
`audit/Badges.md` "Tier paradigm — RETIRED site-wide") and a published threshold is definitionally a
grind target. Anti-farm protection now lives at the **gate** (the event itself must require another
person's cooperation — an acknowledgment accepted, a lineage link approved), not at a count.

**Pattern:**

```csharp
// After primary SaveChangesAsync + counter ExecuteUpdateAsync:
int total = await writeDb.UserStats
    .Where(us => us.UserId == targetUserId)
    .Select(us => us.SomeCounter)
    .FirstOrDefaultAsync();

try
{
    // AwardAsync is idempotent and sets UserBadge.EarnedCount = total in the same call —
    // no separate write keeps the two in step.
    if (total >= 1) await badgeService.AwardAsync(targetUserId, SiteBadges.SomeBadge, total);
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Badge award failed for user {UserId} — swallowed.", targetUserId);
}
```

**Anti-self-farm guard:** when the mechanic is social (e.g., a reader marking a recommendation
helpful, an author crediting a beta reader), guard `actorId != beneficiaryId` AND
`nullableFk != null` **before** incrementing or calling `AwardAsync`. Violations skip silently — no
throw, no log.

**A write service MAY depend on `IBadgeWriteService`** (inject it in the primary constructor; no DAG
cycles since `IBadgeWriteService` depends only on `ApplicationDbContext` /
`ReadOnlyApplicationDbContext`).

**Newly awarded badges are visible by default** (`DisplayOrder = max+1`). The curation UI lets users
hide or reorder. `UserCard.razor` caps to 3 badges.

**Post-MVP:** a background worker will replace inline checks without changing callers' interface.

Live `SiteBadges` constants: `Patron`, `Recommender`, `BetaReader`, `Architect`, `Artist`.
`RecommenderSilver` is **retired** (WU-StatBadgeProducers) — see `scripts/check-doc-hygiene.ps1`'s
retired-name registry. Keys are `public const string` fields on the top-level `SiteBadges` static
class, moved to `Server/Badges/SiteBadges.cs` (WU-StatBadgeProducers closed MA-108; it previously
lived in `Server/Data/SiteConstants.cs`, not nested inside a `SiteConstants` type).

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
| `BlogPostsWritten` | author | `ServerBlogPostWriteService` **profile** create/delete only — group posts are untracked, site posts deliberately excluded, while the recompute counts every `base_blog_posts` row (the three disagree; owner-open, tracker **F16**) | ±1 |
| `GroupsJoined` | member | `ServerGroupWriteService` join/leave | ±1 |
| `FavoritesOnStories` | story author | `ServerUserStoryInteractionWriteService` | **transition-delta** |
| `StoriesRead`, `StoriesIgnored` | acting user | `ServerUserStoryInteractionWriteService.SetUserStoryInteractionStateAsync` / `MarkCompletedAsync` | **transition-delta** |
| `StoriesInProgress` | acting user | `ServerUserStoryInteractionWriteService.MarkStartedAsync` (+1 on a genuine `HasStarted` flip, not already completed) **and** `SetUserStoryInteractionStateAsync`/`MarkCompletedAsync` (−1 on completing) | **transition-delta** |

**Counters deferred — producer not yet built:**
- `ViewsOnStories` — WU38 (story view events); recomputable today only via raw SQL over the
  `daily_story_stats` L8 mart (no EF model) — WU-UserStatRecalc reads it that way.
- `SpotlightCount` — deferred to **tracker B8** (Spotlight donation/payment pipeline). No badge
  consumes it (Patron is a settled manual grant); it rides with donations, not with this WU.
- ~~Acknowledgment counters (`AcknowledgedAsBetaReaderCount`, `AcknowledgedAsInspirationCount`)~~ —
  **being built (WU-StatBadgeProducers).** `AcknowledgedAsInspirationCount` is a producer hook onto
  the already-built `StoryLineage` "Inspired By" approval (not a new feature). Source ambiguity
  resolved: `AcknowledgedAsBetaReaderCount` is sourced from `StoryAcknowledgment` role 1 (consent-
  gated — the recipient must accept), not the dormant `BetaReader` authorization entity, which
  remains a separate, unbuilt concept (draft-access grant, not credit — see `audit/Stories.md`).
- ~~`FeatureContributions` — producer is Feature 56.~~ **Removed 2026-07-18:** Feature 56 was cut
  and the `UserStat.FeatureContributions` column dropped — no longer a deferred counter. See
  `audit/BlogPosts.md` Feature 56 CUT note.

**`ActiveReportCount` — dropped (WU-UserStatRecalc, 2026-07-15).** `UserStat.ActiveReportCount` was
an orphaned duplicate column that no write path ever populated — the live moderation path writes
`User.ActiveReportCount` on `AspNetUsers` instead. Removed via migration rather than wired; see
`audit/Profiles.md` Feature 58.

### Records of a decision are not counters — `User.ApprovedStorySubmissions` (WU-StoryLifecycle, D1)

`User.ApprovedStorySubmissions` (with its companion flag `User.CanAutoApprove`) is **a record of a
decision, not a derived counter**. Each increment records a moderator's approval of a submission — a
decision whose evidence a later deletion can destroy — so there is no ground truth to recompute it from.
D21's own closing paragraph ("'No authoritative counter class' is a statement about counters, not about
records of a decision") places it **outside D21 and D22**. It is **monotonic** (D1): nothing decrements it —
not story deletion (D15), not takedown, not revoke — because a decrementable trust counter is farmable. It
has **no recompute**, and it lives on `AspNetUsers`, not `user_stats`, so `UserStatRecalculator` can
never "correct" it. Its `+1` is committed **atomically with the approve status flip**, in one transaction,
**because** no recompute exists to heal a split — the one place in the counter family where D22's
post-commit contract deliberately does not apply. Full lifecycle context: §"Story Lifecycle".

### Recalculation worker (F58) — mirror the wired formula

A recompute aggregate must reproduce the **exact** semantics the real-time delta path maintains, not
just "COUNT the obvious rows" — otherwise the worker fights the increment path and "corrects" a
value that was already right. Settled nuances (`WU-UserStatRecalc`):
- `StoriesInProgress` — `HasStarted && !IsCompleted` (the wired path does not additionally exclude
  `IsIgnored`; neither does the recompute).
- `FavoritesOnStories` — counts public `IsFavorite` only, never `IsHiddenFavorite`.
- `CommentsWritten` — counts all extant `BaseComment` rows by the user; moderation takedown doesn't
  delete the row or decrement the wired counter, so the recompute doesn't exclude it either.
- `RecommendationSuccessesEarned` — anti-self-farm join (`RecommendationSuccess.UserId ≠
  Recommendation.RecommenderId`); anonymous recs (null `RecommenderId`) drop out.

Recompute is set-based raw SQL (`UserStatRecalculator`, `Server/Profiles/`), following
`SiteDailyStatAggregator`'s style — one `UPDATE ... FROM (SELECT ... GROUP BY owner_id)` per counter
family, not a per-user loop. Step 1 inserts any missing `UserStat` rows first (real-time
`ExecuteUpdateAsync` silently no-ops when the row doesn't exist).

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

### `IsCompleted` auto-producer — durable direct write, never the reading buffer

`IsCompleted` has two producers: the interaction panel (`SetUserStoryInteractionStateAsync`, "mark as
read elsewhere") and `IUserStoryInteractionWriteService.MarkCompletedAsync(int storyId)` — the
application-side producer for spec §5.12's *"set `IsCompleted` when the user reads the last chapter of
a story the author has marked Complete."*

**Mirrors `MarkStartedAsync` exactly, for the same reason.** `HasStarted` is set by a durable direct
write triggered from the reading page at Ch.1 ≥90% scroll — deliberately bypassing the
`ReadingProgressBuffer`/`ReadingProgressFlusher`, because that buffer's contract is *loss-tolerant,
high-frequency, coalescable* signals only ("deliberate actions… take the durable direct-write path,
never this buffer" — see Feature 44 L2 body-swap note). Story completion is just as durable and
load-bearing (it drives the Completed bookshelf tab, `StoriesRead`/`StoriesInProgress`, and the F26
spoiler-reveal gate), so `MarkCompletedAsync` is built the same way: a direct write from
`ChapterReadingPage.OnScrollProgress` (Completed-story final chapter, ≥90%, guarded so it fires once
per page visit) and from the manual mark-read path (`ServerChapterReadMarkWriteService`, when the
marked chapter is the story's last published chapter). **The reading-progress buffer/flusher are never
touched by this producer.**

**Gate — Completed stories only.** `MarkCompletedAsync` only ever fires when
`Story.StoryStatusId == StoryStatusEnum.Completed`. For an ongoing story, "caught up with everything
published" stays the existing query-time computation (`user_chapters_read < PublishedChapterCount`) —
never a stored-flag auto-set. This is deliberate: the V3 reading-status design already considered and
rejected a stored `CaughtUp` state specifically because it required a publish-time background worker to
un-set it when a new chapter posts. Gating the producer to Completed stories keeps that worker
unnecessary — an ongoing story publishing a new chapter never needs any completion state touched.

**No auto-clear.** `MarkCompletedAsync` only ever sets `IsCompleted = true`; nothing clears it
automatically. The rare case of a Completed story reopening and gaining a new chapter accepts brief
staleness (a previously-auto-completed reader stays marked complete) rather than reintroduce the
rejected publish-time worker. The flag remains user-mutable via the panel.

**Idempotent.** No-op if the row is already `IsCompleted = true` (no double counter increment) and
no-op for anonymous callers.

**Bug found via CompletionProducerTests, fixed same session (A3, 2026-07-24):** `MarkCompletedAsync`'s
`StoriesInProgress` decrement (mirroring the panel's own transition-delta) assumes the counter was
previously incremented when `HasStarted` went true. That increment was missing — `MarkStartedAsync`
never touched `StoriesInProgress` before this fix — so for the common case (a user who reads via
scroll/`MarkStartedAsync` and is later auto-completed, never having touched the panel), the decrement
drove the counter negative. Fixed by giving `MarkStartedAsync` the matching +1 transition-delta on a
genuine `HasStarted` false→true flip (guarded: only when not already completed). This also closes the
same latent underflow risk in the pre-existing panel-only completion path (`SetUserStoryInteractionStateAsync`
completing a row whose `HasStarted` came from the reading path, not the panel).

## Site Settings (`ISiteSettingsService`) — DB-Backed Mod-Editable Runtime Knobs (WU-Spotlight)

`SiteSettings/` is a cross-cutting cluster (see `SKILL.md` "Code Organization"): **runtime tuning
values mods change from a mod surface without a deploy.** Distinct from `appsettings` (deploy-time
config: connection strings, provider switches — things only an operator changes) and from
`Profiles/` user settings (per-user). First consumer: Community Spotlight's five knobs.

- **Entity:** `SiteSetting { SettingKey (string PK, MaxLength 128), Value (string, MaxLength 256) }`
  — the string-key lookup pattern (`layer1-data-model.md` enum/lookup framework). Values are stored
  as strings; typing lives in the service accessors. Seeded via `HasData` in
  `SiteSettingsConfigurations.cs`.
- **Keys + defaults live in Core** (`SiteSettingKeys` in `Core/SiteSettings/`): each key constant is
  paired with its default value; the EF seed and the read-fallback both reference the same constant
  — one source of truth, and a missing/unparseable row degrades to the default instead of throwing.
- **CQRS split as usual:** `ISiteSettingsReadService.GetIntAsync(key, fallback)` /
  `ISiteSettingsWriteService.SetIntAsync(key, value)` (write inherits read). The write side calls
  `RequireModerator()` (the `ServerModerationWriteService` pattern) — mod-gating is enforced at the
  service, the `[Authorize]` on the mod page is affordance. Only `int` accessors exist today; add
  typed accessors when a non-int knob appears, don't pre-build them.
- **No caching.** Reads are single-row PK lookups on tiny tables; the whole point is that a mod
  edit takes effect on the next read. Revisit only with measured need.
- **Editor UI lives on the consuming feature's mod surface** (e.g. `ModSpotlightPage` edits the
  spotlight knobs) — there is no central "all settings" page; a knob without a feature surface has
  no reason to exist.

## Community Spotlight — Slot Allocator Seam + Block Booking (WU-Spotlight, Feature 55)

Intent settled 2026-07-11 — `audit/Spotlight.md` holds the requirements record (Gemini discussions
= spirit only; donations deferred). The L2 shape:

**Two entities, two concerns** (`Core/Spotlight/`): `SpotlightSlot` is the *entitlement* (who was
granted the right to spotlight, by which source, redeemed or not); `CommunitySpotlight` is the
*placement* (which story + optional recommendation occupies which booked block). Never conflate
them — the donation era changes only how slots are granted, never what a placement is.

**The seam — `ISpotlightSlotAllocator`** (`Core/Spotlight/`): `GrantSlotAsync(toUserId, source)`,
`RevokeSlotAsync(slotId)`, `GetRemainingMonthlyGrantCapacityAsync()`. The mod-grant implementation
(`ServerSpotlightSlotAllocator`) enforces `RequireModerator()` for `SpotlightSlotSource.ModAward`
and the monthly grant cap (`site_settings`); `SpotlightSlotSource.Donation` throws until the
payment pipeline lands — the enum value and `SpotlightSlot.PaymentId` are the reserved seam, not
dead code. Grant sends `SpotlightSlotGranted` best-effort post-commit (standard notification
pattern). Slot grants do not expire (deferred — revoke is the mod escape hatch).

**Block grid is computed, never stored.** `SpotlightBlocks` (`Core/Spotlight/`, pure static — the
`ChapterText.CountWords` precedent) owns the grid math: blocks of `BlockDurationDays` tile forward
from a fixed epoch (`SpotlightConstants.BlockEpoch`, a Monday, so 7-day blocks align to calendar
weeks). A block's capacity is `PositionCount` (site setting); its booked count is a query
(placements overlapping the block), so changing `PositionCount` or `BlockDurationDays` requires no
data rewrite — existing placements keep their concrete `StartDate`/`EndDate`. Bookable blocks run
from the *current* block (starts immediately, partial remaining window) through
`BookingHorizonDays`.

**Redemption is the concurrency-sensitive write.** `ISpotlightWriteService.RedeemSlotAsync(dto)`
validates inside one transaction serialized by a Postgres advisory lock
(`pg_advisory_xact_lock(hashtext('canalave_spotlight_booking'))`), wrapped in
`CreateExecutionStrategy().ExecuteAsync(...)` because `EnableRetryOnFailure` refuses bare
`BeginTransactionAsync` (the `UserDeletionService` precedent). Two users racing for the last
opening in a block must not both succeed — count-then-insert is only safe under the lock.
Validation set (server-authoritative; UI affordances are not gates): slot is mine + `Available`;
story exists, `AuthorId != me` (no self-spotlight — the UI's `StoryTitlePicker ExcludeStoryId`
can't express "all my stories", so this is service-enforced), status ∉ {Draft, PendingApproval,
Rejected}, `!IsTakenDown`; optional recommendation belongs to the picked story, is Approved and
not taken down (**any** recommender — self-recommendation is allowed; only self-*story* is
banned); block start is on-grid, not fully past, within the horizon; per-story cooldown
(`CooldownDays` around the new window in both directions — also prevents double-booking the same
story into overlapping blocks); block has an opening (< `PositionCount` overlapping placements).
Rejections throw `SpotlightValidationException` (the `RecommendationValidationException` pattern).
No `IWriteRateLimitService` on redemption — the consumed slot *is* the rate limit; grants are
mod-gated.

**Display reads join through the navs so the viewer's filters do the work.** The homepage read
(`GetActiveSpotlightsAsync`: `StartDate <= now < EndDate`) projects through the required `Story`
nav and optional `Recommendation` nav on the read context — `ContentRating`/`IsTakenDown` named
filters apply to the joined entities, so a placement whose story the viewer can't see simply drops
out (same join-not-bare-projection rule as `ServerStoryLineageReadService`; the general form is
conditionality kind (g), `identity-and-authorization.md` §"Parent-visibility guards"), and a taken-down
recommendation nulls back to the blank-rec display state. Composition for presentation:
`IStoryReadService.GetListingsByIdsAsync` for the story cards, `IRecommendationReadService` for
rec DTOs — the spotlight service never re-implements those projections.

**Go-live notifications come from a worker, not the write path.** Placements are booked for
*future* blocks; `StorySpotlighted`/`RecommendationSpotlighted` must land when the window opens.
`SpotlightGoLiveWorker` (Server/Spotlight/, `BackgroundService` — the `SiteDailyStatWorker`
conventions) periodically sweeps `StartDate <= now < EndDate AND GoLiveNotifiedUtc IS NULL`,
notifies via `INotificationWriteService`, and stamps `GoLiveNotifiedUtc` (fires-once idempotency
is the stamp, not the dedup heuristic). A placement whose whole window elapsed while the server
was down is never notified late — the `EndDate > now` condition ages it out silently.
`TestAppFactory` removes the worker; integration tests drive the sweep body directly.

## Naming

- Server impl prefix `Server...`, client impl prefix `Client...`.
- Async methods end in `Async`.
- Method names express query/command intent, not storage (`GetListingsAsync`, not `QueryStoriesFromDb`).
- **Location:** interfaces, server impls, and client impls each live in their feature's cluster folder
  in their respective project (`Core/{Feature}/I{Feature}ReadService.cs`,
  `Server/{Feature}/Server{Feature}ReadService.cs`, `Client/{Feature}/Client{Feature}ReadService.cs`) —
  never in a shared `ServiceInterfaces/`/`Services/` folder. See `SKILL.md` "Code Organization" for the
  legacy-folder migration rule.
