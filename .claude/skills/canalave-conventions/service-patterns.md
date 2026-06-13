# Service Layer & DTO Conventions (CQRS-lite)

The service layer is the firewall between the UI and EF Core. Interfaces and DTOs live in **Core** (shared
by server + WASM); the server impl lives in the **server** project, the HTTP impl in the **Client** project.

## Interface-based with dual implementations

Every service is an interface in Core with two implementations injected by the same registration shape:

```csharp
// Core
public interface IStoryService
{
    Task<StoryListingDto[]> GetListingsAsync(StoryFilterDto filter);
    Task<StoryDetailDto?> GetDetailAsync(int storyId);
    Task UpdateTitleAsync(int storyId, string newTitle);
}

// Server project — talks to the database
public sealed class ServerStoryService(ReadOnlyApplicationDbContext readDb, ApplicationDbContext writeDb)
    : IStoryService { /* EF Core queries */ }

// Client project — talks to the API
public sealed class ClientStoryService(HttpClient http) : IStoryService { /* HTTP calls */ }
```

```csharp
// Server Program.cs
builder.Services.AddScoped<IStoryService, ServerStoryService>();
// Client Program.cs
builder.Services.AddScoped<IStoryService, ClientStoryService>();
```

Components inject `IStoryService` and never know which impl they got. The **client impl carries extra
responsibilities** beyond the server impl: local caching, optimistic UI updates, connection-status checks,
browser API integration.

**Avoid:** injecting `DbContext`, a concrete service, or `HttpClient` directly into a component.

## The DTO firewall (non-negotiable)

UI (Razor components) **NEVER** sees full EF Core model classes — only DTOs and service interfaces. Full
entities are never sent over the wire. This prevents change-tracker bleakage, over-posting, serialization
loops, and bloated payloads.

- DTOs and ViewModels live in **Core** so both server and client share them.
- The boundary is the service method signature: entities in/behind the service, DTOs out.

## Query path (reads, ~90%)

Use `ReadOnlyApplicationDbContext` (`NoTracking`) and project straight to DTOs with `.Select()` — pull only
the columns you need; this bypasses the change tracker and generates lean SQL.

```csharp
public Task<StoryListingDto[]> GetListingsAsync(StoryFilterDto filter) =>
    readDb.Stories
        .Where(s => s.Status == StoryStatusEnum.Ongoing)
        .Select(s => new StoryListingDto(s.Id, s.Title, s.CoverArtRelativeUrl, s.WordCount))
        .ToArrayAsync();
```

**Avoid:** materializing entities then mapping in memory (`.ToList()` then `select new`), or returning
entities and projecting in the component.

## Command path (writes, ~10%)

Use `ApplicationDbContext` with tracked entities. Load via `.FindAsync()` / `.Include()`, mutate, save —
EF emits a targeted UPDATE.

```csharp
public async Task UpdateTitleAsync(int storyId, string newTitle)
{
    var story = await writeDb.Stories.FindAsync(storyId);
    if (story is null) return;
    story.Title = newTitle;
    await writeDb.SaveChangesAsync();
}
```

High-frequency writes (Favorite/Follow/Ignore) do **not** go through this path — they go to the Redis
write-behind queue. See [infrastructure.md](infrastructure.md).

## DTO vs primitive vs ValueTuple

| Return / param shape | Use |
|---|---|
| Read operation result | **DTO** (record) |
| Simple write (1–2 params) | **Primitives** — `UpdateTitleAsync(int storyId, string newTitle)` |
| Complex write (3+ params) | **DTO** |
| 2–3 property read return | **ValueTuple** acceptable — `Task<(int Words, int Chapters)>` |
| Intermediate data inside a method | Anonymous types OK (can't cross a service boundary) |
| `out` parameters | **Avoid** — don't compose with async/await |

Prefer `record` types for DTOs (value equality, concise, immutable):

```csharp
public record StoryListingDto(int Id, string Title, string? CoverArtRelativeUrl, int WordCount);
```

## API endpoints (server side of the client impl)

The client service calls API endpoints the server exposes. Register them via an extension method (the
pattern already in the repo) rather than scattering controller registration.

- **Read endpoints:** return the same DTOs the service returns.
- **High-frequency write endpoints ("fast and dumb"):** validate, `LPUSH` to the Redis queue, return
  `202 Accepted`. Do **not** touch `DbContext`. See [infrastructure.md](infrastructure.md).
- **Cookie auth returns 401/403, not 302 redirects** — required so WASM API calls fail cleanly instead of
  following a redirect to an HTML login page.

## Naming services & methods

- Server impl prefix `Server...`, client impl prefix `Client...` / `Wasm...` (e.g. `WasmSpriteService`).
- Async methods end in `Async`.
- Method names express the query/command intent, not the storage (`GetListingsAsync`, `UpdateTitleAsync`).
