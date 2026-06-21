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

**Client side:** `ClientStoryWriteService : ClientStoryReadService` mirrors the inheritance.

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
