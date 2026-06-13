# Infrastructure: Redis, Workers, Aspire, Sprites, Organization

## Code organization (vertical / folder-per-feature)

Target structure groups by **feature**, not technical layer:

```
Stories/
  IStoryService.cs            (Core)
  ServerStoryService.cs       (server)
  ClientStoryService.cs       (Client)
  StoryListingDto.cs          (Core)
  StoryCard.razor             (SharedUI)
```

Not `Services/`, `Interfaces/`, `Dtos/` folders split across the codebase. The repo is **mid-transition**
from horizontal to vertical — when you touch a feature, pull its pieces toward a feature folder rather than
extending the old layer folders. Pieces still land in their owning project (interface in Core, impls in
server/Client, components in SharedUI) — vertical grouping is conceptual, enforced by namespace/folder, not
by collapsing project boundaries.

## .NET Aspire (dev orchestration only)

AppHost defines the dev containers and wires connection strings; it **never deploys**. Aspire 9.5.

```csharp
// AppHost
var postgres = builder.AddPostgres("postgres").AddDatabase("canalavedb");
var redis = builder.AddRedis("redis");
var minio = builder.AddMinIO("minio");           // local S3 emulator

builder.AddProject<Projects.TheCanalaveLibrary_Server>("web")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(minio);
```

- **ServiceDefaults** holds shared cross-cutting config (telemetry, health checks, resilience). Each service
  calls `builder.AddServiceDefaults()`.
- Consume references by **logical name** in the consuming project
  (`builder.AddNpgsqlDbContext<ApplicationDbContext>("canalavedb")`,
  `builder.AddRedisDistributedCache("redis")`), never hard-coded connection strings.
- MinIO in dev uses the **same AWS S3 SDK code** as Cloudflare R2 in prod — only the endpoint config differs.

## Redis write-behind queue (high-frequency writes)

The path for `UserStoryInteraction` updates (Favorite / Follow / Ignore buttons):

1. **Blazor WASM component:** optimistic UI update on click + a **2-second per-component debounce** to
   absorb click/unclick churn. When the timer fires, one API call for that one story.
2. **API controller ("fast and dumb"):** validate → `LPUSH` a message onto Redis list `interaction-queue`
   → return `202 Accepted`. **Never touches `DbContext`.**
3. **Background worker (`IHostedService`):** wakes every **5 seconds**, drains all pending messages,
   consolidates across all users into `Dictionary<(UserId, StoryId), LatestState>`, performs **one** batch
   MERGE/UPDATE to PostgreSQL.

**Avoid:** writing interactions synchronously through `IStoryService` → `DbContext`. That defeats the queue.

### Other Redis uses

- **View counts:** Redis `INCR` per story; a 5-second worker reads keys, resets to zero, batch-updates DB.
- **LastReadDate** (volatile "in progress" reading position): Redis Hash `user:{userId}:lastread`
  (field = storyId, value = timestamp), updated by a lightweight `POST /api/reading/ping/{storyId}` that
  touches **only Redis**. Render "In Progress": query SQL for in-progress StoryIds, one `HGETALL` for
  timestamps, merge/sort in C#.
- **Distributed cache** for expensive query results (tree search, Also Favorited).

## Background workers

All are `IHostedService` / `BackgroundService` in the server project. Worker queries use **raw SQL via
`_context.Database.ExecuteSqlRawAsync()`**, not EF Core LINQ (LINQ is for live app queries).

| Worker | Schedule | Purpose |
|---|---|---|
| Write-behind | 5 s | Drain interaction queue, consolidate, batch write |
| View count | 5 s | Read Redis INCR keys, reset, batch-update |
| TreeSearch data mart | Daily off-hours | Rebuild `UserStoryTreeSearchEntries`, zero-downtime swap |
| AlsoFavorited/AlsoRecommended | Daily off-hours | Rebuild collaborative-filter caches, zero-downtime swap |
| Badge awarding | Event-driven | React to events, check stats, award badges |
| Notification cleanup | Daily | Delete read notifications older than 60 days |
| UserStat recalculation | Periodic | Pre-calc denormalized counters |
| Daily site stats | Daily | Aggregate into `SiteDailyStat` |

**Zero-downtime cache refresh (table swap):** two physical tables (`_a`/`_b`), a view/function points at the
active one. Worker TRUNCATEs the inactive table, populates it, then atomically `ALTER TABLE ... RENAME`
within a transaction. PostgreSQL has no SYNONYMs — the transactional rename is the equivalent. These cache
tables are **not in EF migrations**.

## Sprites & blob storage

**Developer-controlled sprites (tags, themes):** stored in
`wwwroot/images/themes/{theme}/static/` and `.../animated/`, served via Cloudflare CDN. **Git is the source
of truth** — version-controlled, atomically deployed with code.

- The `Tag` table stores a `SpriteIdentifier` (e.g. `"fluff"`), **not** a URL. The client builds the full
  path at render time, so adding a theme is zero DB changes — just a new folder.
- `ISpriteService`: `ServerSpriteService` uses `IWebHostEnvironment` + `File.Exists()` with fallback to
  `unknown_sprite.png`; `WasmSpriteService` builds URL strings optimistically (no disk/HTTP).
- Animated sprites are **Animated WebP** (not GIF) — alpha transparency, smaller, modern support. Choose
  `/static/` vs `/animated/` from `User.PrefersAnimatedSprites`.
- **Avoid:** storing full `SpriteURL`/`AnimatedSpriteURL` on the Tag table — a CDN domain change would
  rewrite millions of rows.

**User uploads (cover art, profile pics):** Cloudflare R2 via AWS S3 SDK; MinIO in dev. Key convention
`stories/{StoryId}/cover-{uuid}.jpg`, `users/{UserId}/profile-{uuid}.jpg` — organized keys enable bulk
deletion for GDPR.

## Identity & auth wiring

`AddIdentityCore<User>().AddRoles<ApplicationRole>()` with `int` keys. Cookie auth via
`AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()`, configured to return
**401/403 status codes, not 302 redirects** (critical for WASM API calls). `RequireConfirmedAccount = true`.

## Read replica awareness

Reads go to the PostgreSQL read replica; writes (workers + direct) hit the primary. Replication is
near-real-time but **eventually consistent** — UI shows optimistic local state for a few seconds after a
write rather than immediately re-reading.
