# Layer 7 — Redis Integration

Write-behind buffer, ephemeral store, read-side cache, and associated `IHostedService` workers.
This layer swaps method bodies behind stable interfaces — no contract changes from Layers 1–4.

## Critical Constraint

Clients (Blazor WASM) **NEVER** connect to Redis directly. The server is the trusted intermediary.
- For server-rendered components: inject `IDistributedCache`.
- For WASM components: call a server API endpoint that handles caching internally.

**Anti-pattern rejected:** creating an HTTP-based `IDistributedCache` for the WASM client.

## Write-Behind Queue (UserStoryInteraction)

The path for high-frequency interaction updates (Favorite / Follow / Ignore buttons):

1. **Blazor WASM component:** optimistic UI update on click + a **2-second per-component debounce**
   (`SiteConstants.InteractionDebounceMs`) to absorb click/unclick churn. When the timer fires,
   one API call for that one story.
2. **API endpoint ("fast and dumb"):** validate → `LPUSH` a message onto Redis list
   `interaction-queue` → return `202 Accepted`. **Never touches `DbContext`.**
3. **Background worker (`IHostedService`):** wakes every **5 seconds**, drains all pending messages,
   consolidates across all users into `Dictionary<(UserId, StoryId), LatestState>`, performs **one**
   batch MERGE/UPDATE to PostgreSQL.

**MVP temporary state:** write-behind features use direct EF writes through the service interface.
The interface never changes; only the method body does when Redis is introduced.

## View Count Tracking

```
Redis:  INCR story:{storyId}:views
Worker: every 5 seconds → GETSET (atomic read-and-reset) → batch UPDATE Story.ViewCount
```

No per-user tracking — a view is a view. Trigger: first client-side ping (5-second timer or
first scroll), NOT page load (filters bots and bounces).

## LastReadDate: Redis Hybrid Pattern

`LastReadDate` for "Actively Reading" stories is too volatile for SQL:

- Stored in Redis Hash: `user:{userId}:lastread`, field = storyId, value = timestamp.
- Updated via lightweight endpoint: `POST /api/reading/ping/{storyId}` — **touches only Redis**.
- Rendering Bookshelves "Actively Reading" tab:
  1. Query SQL for in-progress StoryIds (`HasStarted = true AND IsCompleted = false AND IsIgnored = false`).
  2. One Redis `HGETALL` for the user's lastread hash.
  3. Merge and sort in C# memory.

## Distributed Cache

For expensive query results (tree search, Also Favorited):

```csharp
public class CachedStoryService(IStoryReadService inner, IDistributedCache cache) : IStoryReadService
{
    public async Task<StoryListingDto[]> GetListingsAsync(StoryFilterDto filter)
    {
        var key = $"story-listings:{filter.ToKey()}";
        var cached = await cache.GetStringAsync(key);
        if (cached is not null)
            return JsonSerializer.Deserialize<StoryListingDto[]>(cached)!;

        var result = await inner.GetListingsAsync(filter);
        await cache.SetStringAsync(key, JsonSerializer.Serialize(result),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
        return result;
    }
}
```

## Redis Workers

| Worker | Schedule | Purpose |
|---|---|---|
| Write-behind drain | 5 s | Drain interaction queue, consolidate, batch write |
| View count drain | 5 s | Read Redis INCR keys, reset, batch-update |

Both are `IHostedService` / `BackgroundService` in the server project.

## Aspire Configuration

```csharp
// AppHost
var redis = builder.AddRedis("redis");

builder.AddProject<Projects.TheCanalaveLibrary_Server>("web")
    .WithReference(redis);

// Server Program.cs
builder.AddRedisDistributedCache("redis");
```

Consume by **logical name**, never hard-coded connection strings.

## Multi-Layer Caching Architecture

1. **Redis:** Write-behind queue, view count INCR, LastReadDate, distributed cache.
2. **PostgreSQL cache tables:** Data mart tables rebuilt by Layer 8 workers.
3. **Cloudflare CDN:** `wwwroot` static assets and R2-served images.
4. **Browser cache:** `ResponseCache` headers for tag data and static API responses.
5. **In-memory service cache:** Tag lists cached in service instances.
6. **PostgreSQL internal:** Buffer pool and query plan cache.
