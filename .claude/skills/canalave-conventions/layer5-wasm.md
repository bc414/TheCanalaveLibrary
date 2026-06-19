# Layer 5 — WASM Enablement

API endpoints, client-side service implementations, `PersistentAuthenticationStateProvider`.
This is a body-swap behind stable interfaces — DTO shapes, service method signatures, and
component props don't change. Only the method bodies behind the interfaces change.

## When This Layer Applies

Layers 1–4 are the MVP on `InteractiveServer`. Layer 5 is additive: it introduces the server-side
endpoints and client-side `HttpClient` implementations needed for `InteractiveAuto` (WASM).

**Dev shortcut:** start with `InteractiveServer` globally. When switching to `InteractiveAuto`,
Layer 5 adds:
1. API endpoints on the server for every service method.
2. Client implementations that call those endpoints via `HttpClient`.
3. `PersistentAuthenticationStateProvider` for WASM auth state.

## API Endpoint Organization

Minimal API endpoints in feature-specific extension method classes:

```csharp
// StoryEndpoints.cs
public static class StoryEndpoints
{
    public static WebApplication MapStoryEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/stories");

        group.MapGet("/", async (IStoryReadService service, [AsParameters] StoryFilterDto filter) =>
            Results.Ok(await service.GetListingsAsync(filter)));

        group.MapGet("/{storyId:int}", async (IStoryReadService service, int storyId) =>
        {
            var result = await service.GetDetailAsync(storyId);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });

        group.MapPut("/{storyId:int}/title", async (IStoryWriteService service,
            int storyId, string newTitle) =>
        {
            await service.UpdateTitleAsync(storyId, newTitle);
            return Results.NoContent();
        });

        return app;
    }
}

// Program.cs
app.MapStoryEndpoints();
```

- **Read endpoints:** return the same DTOs the service returns.
- **High-frequency write endpoints ("fast and dumb"):** validate → `LPUSH` to Redis queue →
  return `202 Accepted`. Do NOT touch `DbContext`. See [layer7-redis.md](layer7-redis.md).
- **Cookie auth returns 401/403, not 302 redirects** — required so WASM API calls fail cleanly.

## Client Service Implementations

Mirror the server inheritance structure:

```csharp
public sealed class ClientStoryReadService(HttpClient http) : IStoryReadService
{
    public async Task<StoryListingDto[]> GetListingsAsync(StoryFilterDto filter) =>
        await http.GetFromJsonAsync<StoryListingDto[]>($"/api/stories?{filter.ToQuery()}")
        ?? [];

    public async Task<StoryDetailDto?> GetDetailAsync(int storyId) =>
        await http.GetFromJsonAsync<StoryDetailDto>($"/api/stories/{storyId}");
}

public sealed class ClientStoryWriteService(HttpClient http)
    : ClientStoryReadService(http), IStoryWriteService
{
    public async Task UpdateTitleAsync(int storyId, string newTitle) =>
        await http.PutAsJsonAsync($"/api/stories/{storyId}/title", newTitle);
}
```

**Client impls carry extra responsibilities beyond the server impl:**
- Local caching / session-lifetime memoization
- Optimistic UI updates
- Connection-status checks
- Optimistic URL construction (`WasmSpriteReadService` can't do `File.Exists()`)

## Authentication in WASM

Cookie auth via `AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()`.
Configured to return **401/403 status codes**, not 302 redirects.

`PersistentAuthenticationStateProvider` carries auth state from server prerender to WASM
interactive mode, preventing an unauthenticated flash.

## The Contract Boundary

The vertical-line test: can this feature's Layer 1–4 contract be fully defined now, with *some*
correct implementation behind it, such that Layer 5 only changes what's *behind* the contract?

Layer 5 is naturally batchable: the same endpoint + `HttpClient` wrapper pattern applies to N
stable interfaces. Build all endpoints in one pass after MVP.

## Avoid

- Injecting `DbContext`, a concrete service, or `HttpClient` directly into a component.
- Creating an HTTP-based `IDistributedCache` for the WASM client (rejected: extreme overhead).
- Instantiating rich domain models on the client for validation reuse (WASM payload bloat).
