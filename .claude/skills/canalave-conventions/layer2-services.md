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
builder.Services.AddDbContext<ReadOnlyApplicationDbContext>(options => options
    .UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure())
    .UseSnakeCaseNamingConvention()
    .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
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

**Client side:** `ClientStoryWriteService : ClientStoryReadService` mirrors the inheritance.

### Content-Rating Filtering Lives on the DbContext, Not in Each Service

Read services do **not** add a `.Where(s => s.Rating <= ...)` clause themselves. The ceiling is a global
EF Core named query filter on `Story`, sourced from a scoped `IActiveUserContext` injected into
`ApplicationDbContext`'s constructor (settled WU12) — see `cross-cutting.md` "Content Rating Filtering"
and "Active-User Context" for the full mechanism and rationale (model invariant vs. per-method
vigilance). A read service projecting `Story` rows gets the filter automatically; it never re-derives
it. The one place a service deliberately bypasses it is a mod/admin/author path that needs all ratings —
that calls `.IgnoreQueryFilters(["ContentRating"])` explicitly, a visible opt-out.

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

High-frequency writes (Favorite/Follow/Ignore) go to Redis write-behind queue, not this path.
See [layer7-redis.md](layer7-redis.md).

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

### Sprite URLs Are Resolved Server-Side, At Projection Time

Display DTOs that include a sprite (`TagChipDto.SpriteUrl`, and any future sprite-bearing DTO) follow
the same pattern as `CoverArtRelativeUrl` above: the **read service** calls
`ISpriteReadService.GetSpriteUrl(theme, spriteIdentifier, animated)` inside its `.Select()`/mapping
step — using the current user's theme + animated-sprite preference — and the DTO carries the
**resolved relative path** (e.g. `/sprites/themes/pokemon/static/bulbasaur.png`), not the raw
`SpriteIdentifier` key. The browser fetches that path in its own request (Cloudflare/MinIO-cacheable);
the DTO itself is never cached. Components never inject `ISpriteReadService` to resolve a sprite for
display — only services do (consistent with the DTO Firewall and the Leaf-tier "never inject a
service" rule in `SKILL.md`'s Component Taxonomy).

**Consequence:** because the resolved URL depends on the requesting user's theme/animation prefs, any
DTO carrying one is **per-user and request-scoped — never cache it across users or themes.**

**Per-keystroke typeahead search is the one case where sprite resolution can't live inside `.Select()`**
(settled WU11): `ISpriteReadService.GetSpriteUrl` is plain C# string-building, not a SQL-translatable
expression, so a method backing a Blazored.Typeahead `SearchMethod` — e.g.
`Task<List<TagChipDto>> SearchTagChipsAsync(TagTypeEnum type, string term)` — must `.Select()` into a
**lean intermediate projection** (id/name/type/description/sprite-identifier only), `Take()`-cap it,
materialize with `.ToListAsync()`, and only then map each row to the display DTO in-memory, calling
`GetSpriteUrl` per row. The "resolved at projection time" rule still holds in spirit — the read
service resolves it before the DTO leaves the service, never the component — it's just that
"projection" here is the in-memory mapping step that follows materialization, not the EF `.Select()`
itself. The same per-user/never-cache consequence below still applies.

**Avatars are a related but distinct case (settled WU10):** `UserCardDto.AvatarUrl` is *not* produced
by `GetSpriteUrl` — it's the producing read service copying `User.ProfilePictureRelativeUrl` (a
user-uploaded blob path stored verbatim on the entity) into the DTO, or substituting a service-chosen
default when null. No theme/animation resolution is involved, but the request-scoping discipline still
applies the same way: the DTO is per-user and isn't cached across users. See `layer4-style.md`
§"Avatars Are Stored URLs, Not Sprite Keys".

**Cover art is the same pattern as avatars, produced by a different write-side source (settled WU12):**
`StoryListingDto.CoverArtRelativeUrl` is also copied verbatim, never resolved through
`ISpriteReadService`. The difference from sprites/tags is *how the relative path got there in the first
place* — `IImageStorageService.SaveAsync` (Core/Images/) is the write-side counterpart that turns an
uploaded file into the relative key stored on the entity, distinct from `ISpriteReadService` which only
*resolves* keys that already exist as git-managed static assets. `LocalImageStorageService` (MVP) writes
under `wwwroot/uploads/`; the interface is the seam for the Post-MVP `S3ImageStorageService` swap
(MinIO/R2). See `audit/ImageStorage.md` for the full contract and URL conventions.

### User HTML Is Sanitized Once, On Save — Never On Display

Any write path that accepts user-authored rich text (chapters, comments, recommendations, blog posts,
profile bios, messages — everywhere `EditorView` is used) runs it through `HtmlSanitizer`'s allow-list
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

## Write-Side Reads — Four Cases

| Case | Example | Context used |
|---|---|---|
| Constraint check | Hidden Gem ≤5 count | `writeDb` (primary, consistency) |
| Edit form loads read DTO | Editor needs current title | `readDb` (via inherited read method) |
| Edit-only fields | `OriginalPublishedDate` | `writeDb` via dedicated `GetStoryForEditAsync()` |
| Display hint | `CommentDto.IsLikedByCurrentUser` | N/A — flows as `[Parameter]` from parent |

## Self-Referential Editing Exception

When reader and writer are identical by definition (user editing own settings), a single
`IUserSettingsService` with both read and write methods is acceptable. Does NOT apply to
public profile display (`IUserProfileReadService`).

## Three-Tiered Validation

**Tier 1 (Client + Server):** `DataAnnotations` on ViewModels. Immediate UX feedback via
`EditForm` / `DataAnnotationsValidator`.

**Tier 2 (Client + Server):** Shared interface (`IEditableStoryProperties`) implemented by both
ViewModel and EF model. Validation in **static extension methods** in Core.

**Tier 3 (Server only):** Database context checks in service. On failure, throws
`StoryValidationException` containing `List<string>` of errors. Server-side only.

## Naming

- Server impl prefix `Server...`, client impl prefix `Client...`.
- Async methods end in `Async`.
- Method names express query/command intent, not storage (`GetListingsAsync`, not `QueryStoriesFromDb`).
- **Location:** interfaces, server impls, and client impls each live in their feature's cluster folder
  in their respective project (`Core/{Feature}/I{Feature}ReadService.cs`,
  `Server/{Feature}/Server{Feature}ReadService.cs`, `Client/{Feature}/Client{Feature}ReadService.cs`) —
  never in a shared `ServiceInterfaces/`/`Services/` folder. See `SKILL.md` "Code Organization" for the
  legacy-folder migration rule.
