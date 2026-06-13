# The Canalave Library — Definitive Project Specification

> **Synthesized from:** Five batch extraction documents covering design conversations from approximately September 10 through November 28, 2025. Where batches conflict, later decisions override earlier ones. Superseded decisions are preserved as "Previously considered" notes where they provide useful context.

---

## 1. Project Mission & Philosophy

### What This Site Is

The Canalave Library is a Pokémon-fandom fanfiction website. The name references the library in Canalave City from the Pokémon games. It is designed from the ground up to promote deep thought, deep engagement, and critical thinking among readers and writers.

### Core Design Principles

**Discoverability over virality.** The site's primary goal is to help readers find high-quality stories and help writers — especially underappreciated ones — gain readers. Features like Tree Search, Recommendations, Hidden Gems, and Vouches are all oriented toward discovery rather than popularity contests. This is a direct response to the "popularity snowball" effect on existing fanfiction platforms where early popular stories dominate and newer/niche works never surface.

**High-effort actions over low-effort ones.** The site deliberately avoids the addictive social-media patterns that plague platforms like FanFiction.Net. Notifications are reserved for meaningful interactions (new comments, new follows, new recommendations), not trivial ones (likes). Story "likes" have been removed entirely. Comment likes exist but generate no notifications and carry no `DateLiked` timestamp, intentionally preventing trending/activity-feed mechanics.

**Transparency in moderation.** Users who submit reports receive closure notifications for all outcomes, including "Resolved — No Action Taken." This is a direct response to the "report to a black hole" model of FanFiction.Net, which destroys community trust. Every report is acknowledged, and users know their voice was heard.

**User empowerment through multiple contribution types.** Users can contribute as authors, readers, recommenders, beta readers, co-authors, group organizers, and more. The site values diverse forms of engagement beyond just writing. The term "Followed Users" (not "Followed Authors") reflects this — not everyone is an author.

**Performance-driven database design.** The schema is designed "query-first," meaning indexes and table structures are chosen to optimize for known access patterns rather than theoretical normalization purity. Vertical partitioning, denormalization, and filtered indexes are used aggressively where they improve performance on real query patterns.

### Reference Sites

| Site | Reference For |
|---|---|
| **Fimfiction.net** | Primary inspiration. Engaging visual design with cover art (unlike AO3). Dedicated single-fandom community. In-place editing model. |
| **Archive of Our Own (AO3)** | Tagging/filtering systems. In-place editing UX. |
| **FanFiction.Net (FFN)** | Reference for scale. What NOT to do: popularity snowball, report-to-black-hole moderation, separate editing dashboard. |
| **Steam** | Curator model and author-picked reviews. |
| **Spotify** | Automated graph-based recommendation (Discover Weekly concept). |
| **LinkedIn** | Degrees-of-connection concept for tree search. |

---

## 2. Technology Stack

### Language & Framework

| Layer | Choice | Rationale |
|---|---|---|
| **Language** | C# / .NET (latest stable) | Strong typing, mature ecosystem, excellent tooling. All projects must match the same major .NET version. |
| **Web Framework** | Blazor Web App | Global `InteractiveAuto` mode: SSR prerender on first request, then SPA via WebAssembly for all subsequent navigation. Chosen because fanfiction readers click "Next Chapter" dozens of times — instant SPA transitions are critical. |
| **ORM** | Entity Framework Core (Code-First) | Industry standard for .NET. Code-first with Fluent API for all non-trivial configuration. Hybrid scaffold-to-code-first workflow. |
| **Local Dev Orchestration** | .NET Aspire | Manages Docker containers (PostgreSQL, Redis, MinIO) and connection strings during development. Does NOT go to production. |

> **Previously considered render strategy:** Component-level `@rendermode InteractiveWasm` with Static SSR as default (islands of interactivity). Rejected because each standard `<a>` link triggered a full page reload, destroying client-side state and feeling clunky for heavy readers. Global `InteractiveAuto` creates a true SPA with instant client-side routing.

> **Development shortcut:** Start with `InteractiveServer` in `App.razor` during active development (faster debugging, no need for API controllers yet). Switch to `InteractiveAuto` when ready to ship WASM.

### Database

| Choice | Detail | Rationale |
|---|---|---|
| **Engine** | **PostgreSQL** | Free read replicas via built-in streaming replication (decisive — SQL Server locks this behind Enterprise Edition). Native MVCC for better concurrency. JSONB support. tsvector/GIN for full-text search. Zero licensing cost. |
| **Naming Convention** | snake_case via `EFCore.NamingConventions` plugin | PostgreSQL folds unquoted identifiers to lowercase. The plugin auto-converts C# PascalCase to snake_case, preventing quoting headaches. |

> **Previously considered:** SQL Server (Express for dev). Was the original choice through early design. Offered bit-packing (8 BOOLs in 1 byte), TINYINT (1 byte), and variable-precision DATETIME2. Abandoned because read replicas require Enterprise Edition and the storage micro-optimizations don't justify the licensing cost at any scale.

**Key PostgreSQL trade-offs accepted:**
- No bit packing: each `boolean` is 1 byte (vs SQL Server's 8 `BIT`s in 1 byte). UserStoryInteraction row is 16 bytes instead of 9.
- No TINYINT: C# `byte` maps to `smallint` (2 bytes instead of 1).
- Fixed timestamp size: `timestamp with time zone` is always 8 bytes (vs SQL Server's variable 6–8 byte `datetime2(n)`).
- These costs are negligible compared to free read replicas and MVCC advantages.

### Cache

| Choice | Detail | Rationale |
|---|---|---|
| **Engine** | Redis | Write-behind queue for high-frequency UserStoryInteraction updates. View count `INCR` buffering. LastReadDate ephemeral storage. Distributed cache for expensive query results. Session cache. |

### Authentication & Authorization

| Choice | Detail |
|---|---|
| **Framework** | ASP.NET Core Identity with Roles |
| **User ID Type** | `int` (not GUID) — smaller composite keys, better index performance on high-traffic junction tables |
| **User Class** | `User` (extends `IdentityUser<int>`) |
| **Role Class** | `ApplicationRole` (extends `IdentityRole<int>`) |
| **Configuration** | `AddIdentityCore<User>()` with `.AddRoles<ApplicationRole>()`. Cookie auth via `AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()`. |
| **Cookie Config** | Returns 401/403 status codes instead of 302 redirects (critical for Blazor WASM API calls) |
| **Email Confirmation** | `RequireConfirmedAccount = true` |

> **Previously considered:** Custom Users table with manual JWT auth (access tokens + refresh tokens), Argon2/bcrypt password hashing, and a RefreshTokens/UserSessions table. All replaced by ASP.NET Core Identity which handles password hashing, sessions, roles, and 2FA out of the box.

### Hosting & Infrastructure

| Service | Provider | Rationale |
|---|---|---|
| **Web Server** | DigitalOcean Droplet (Linux) | Flat-fee pricing, good egress free tier |
| **Database** | DigitalOcean Managed PostgreSQL | Automated backups, patching, failover. One-click read replicas. |
| **Blob Storage (user uploads)** | Cloudflare R2 | S3-compatible, **$0 egress fees** when served through Cloudflare CDN. Critical for serving cover art and profile pictures at scale. |
| **Blob Storage (dev)** | MinIO via .NET Aspire Docker | Local S3 emulator. Same AWS SDK code, different endpoint config. |
| **CDN** | Cloudflare (Free tier) | Global edge caching for `wwwroot` static assets and R2-served images. DDoS protection. SSL/TLS. |
| **Domain Registrar** | Cloudflare Registrar | At-cost pricing. Plan to reserve `.com`, `.net`, `.org` TLDs (~$32/year total). Seamless DNS integration, free WHOIS privacy, automatic DNSSEC. |

**Infrastructure connections:**
- DigitalOcean App Droplet → DB Droplet via private IP.
- Cloudflare DNS A record → App Droplet IP (Proxied/orange cloud).
- Cloudflare SSL/TLS set to "Full (Strict)" with Origin Certificate on Droplet.
- R2 bucket as origin for CDN subdomain (e.g., `cdn.thecanalavelibrary.net`).
- Developer-controlled sprites served from `wwwroot`, cached by Cloudflare CDN identically to R2.

### IDE & Tooling

| Tool | Purpose |
|---|---|
| **JetBrains Rider** | Primary C# IDE. Built-in Database tool for PostgreSQL management. Non-commercial license for hobbyist use. |
| **Gemini Code Assist plugin** | AI coding assistant integrated into Rider. Uses existing consumer Gemini AI Pro subscription. |
| **Jules (Google)** | Async AI coding agent for background tasks (PR generation). |
| **Gemini Web App** | Long-context AI planning sessions. Massive file upload for architecture analysis. |
| **pgAdmin** | Alternative PostgreSQL GUI (bundled with PostgreSQL installer). |
| **Git + GitHub** | Version control via Rider's built-in VCS integration. |
| **Rclone** | CLI tool for syncing sprites to R2 in CI/CD pipelines. |

> **Previously considered:** Visual Studio Community Edition as primary IDE. Replaced by Rider for cross-platform support, superior .NET intelligence (built-in ReSharper), and Gemini Code Assist plugin support. Visual Studio may still be used for specific scaffolding wizards if needed.

### NuGet Packages

| Package | Purpose |
|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | PostgreSQL EF Core provider |
| `EFCore.NamingConventions` | Auto snake_case mapping for PostgreSQL |
| `NpgsqlTypes` | NpgsqlTsVector type for full-text search |
| `AWSSDK.S3` | S3-compatible client for Cloudflare R2 and MinIO |
| `AWSSDK.Extensions.NETCore.Setup` | DI integration for AWS SDK |
| `Aspire.Hosting.Redis` | Redis container in Aspire AppHost |
| `Aspire.Hosting.MinIO` | MinIO container in Aspire AppHost |
| `HtmlSanitizer` | Server-side HTML sanitization for all user-submitted rich text |
| `Blazored.Typeahead` | Typeahead/autocomplete component for tag selection UI |
| `Blazored TextEditor (Quill.js)` | WYSIWYG editor for chapter text, descriptions, notes |

### Future/Phase 2+ Technologies

| Technology | Purpose | Timing |
|---|---|---|
| **Elasticsearch or OpenSearch** (managed) | Denormalized search replica for advanced discovery, faceted filtering | When PostgreSQL FTS becomes a bottleneck |
| **.NET MAUI Blazor Hybrid** | Native iOS/Android app reusing SharedUI Razor components | Post-launch; template chosen but no implementation |
| **Semantic search / RAG** | Intelligent story search based on meaning (embeddings, vector databases) | Exploratory only; no implementation decisions |

---

## 3. Architecture

### Project Structure

```
TheCanalaveLibrary (Solution)
├── TheCanalaveLibrary.Server       — Main server project (ASP.NET Core, Blazor Server host)
│                                     Contains: ApplicationDbContext, ReadOnlyApplicationDbContext,
│                                     concrete service implementations (ServerStoryService, etc.),
│                                     background workers, API controllers, Identity components,
│                                     DataSeeder, Program.cs
├── TheCanalaveLibrary.Client       — WebAssembly client project
│                                     Contains: client Program.cs, client service implementations
│                                     (ClientStoryService, WasmSpriteService, etc.), HttpClient config
├── TheCanalaveLibrary.SharedUI     — Razor Class Library: all shared Blazor components and pages
│                                     Contains: MainLayout.razor, all routable @page components,
│                                     shared CSS, ComponentIdentifier marker class
├── TheCanalaveLibrary.Core         — Shared class library
│                                     Contains: EF Core model classes (POCOs), service interfaces
│                                     (IStoryService, ISpriteService, etc.), enum definitions,
│                                     DTOs/ViewModels, SiteConstants.cs
├── TheCanalaveLibrary.AppHost      — .NET Aspire orchestrator (dev only, never deployed)
│                                     Contains: Redis, MinIO, PostgreSQL container definitions
├── TheCanalaveLibrary.ServiceDefaults — Aspire shared configuration (logging, health checks)
└── TheCanalaveLibrary.Tests        — xUnit v3 test project
```

### Project References

| Project | References |
|---|---|
| AppHost | Main server, Client, ServiceDefaults |
| Main server (TheCanalaveLibrary.Server) | Core, SharedUI, Client, ServiceDefaults |
| Client | Core, SharedUI |
| SharedUI | Core |

### Project Roles

- **Core**: Entity/model classes (POCOs), service interfaces, enum definitions, DTOs, constants. NO EF Core packages except `Microsoft.EntityFrameworkCore.Abstractions` (for `[Index]` attribute). NO server-side dependencies. This project is shared between server and WASM client.
- **SharedUI**: All `.razor` components and pages, `MainLayout.razor`, `_Imports.razor`, shared CSS. Contains `ComponentIdentifier` marker class for assembly discovery via `typeof(ComponentIdentifier).Assembly`. NO WebAssembly-specific NuGet packages — render mode directives are metadata only.
- **Main server**: ASP.NET Core host. Contains `App.razor` (HTML shell with global `InteractiveAuto`), `ApplicationDbContext` and `ReadOnlyApplicationDbContext`, concrete server service implementations, background workers, API controllers, Identity components (Login, Register), seed classes. Identity components MUST stay in the server project — they use `UserManager`/`SignInManager`/`HttpContext` which don't exist in WASM.
- **Client**: WebAssembly bootstrapper. Contains client-side `Program.cs` (WASM host builder, HttpClient registration, client service registrations). Client service implementations inject `HttpClient` and call API endpoints. Can be deferred — develop server-only first.
- **AppHost**: Aspire orchestrator. Defines Redis, MinIO, PostgreSQL container configuration and project relationships. Does NOT go to production.

### Render Strategy: Global InteractiveAuto (SPA)

Global `InteractiveAuto` mode is set on `<Routes>` and `<HeadOutlet>` in `App.razor`. This creates a Single Page Application model:

1. **First request:** Server-side prerendering (SSR) for fast initial paint and SEO.
2. **Background:** WASM payload downloads and caches in the browser.
3. **Subsequent navigation:** Client-side routing via WASM (no page reloads). Blazor Router intercepts clicks, swaps `@Body` component, preserves layout state.

**PersistentComponentState** is required to prevent the "double fetch" problem with `InteractiveAuto` (server prerender + client hydration both call `OnInitializedAsync`). Server-side: register `OnPersisting` callback, fetch data, call `_state.PersistAsJson("key", data)`. Client-side: check `_persistentComponentState.TryTakeFromJson<T>("key", out var data)` before making HTTP call.

### Service Pattern: Interface-Based with Dual Implementations

All Razor components inject interfaces (e.g., `IStoryService`), never concrete implementations or `DbContext`.

- **Server implementation** (`ServerStoryService`): Injects `ApplicationDbContext`, queries DB directly. Registered in server `Program.cs`: `builder.Services.AddScoped<IStoryService, ServerStoryService>();`
- **Client implementation** (`ClientStoryService`): Injects `HttpClient`, calls API endpoints. Has additional responsibilities: local caching, optimistic UI updates, connection status checking, browser API integration. Registered in client `Program.cs`: `builder.Services.AddScoped<IStoryService, ClientStoryService>();`

### CQRS-Lite Pattern

- **Query Path (Reads, ~90%):** Service methods return DTOs/ViewModels. Use EF Core `.Select()` projections to retrieve only needed columns. Bypasses change tracker. Generates optimized SQL. Uses `ReadOnlyApplicationDbContext` (configured with `QueryTrackingBehavior.NoTracking`).
- **Command Path (Writes, ~10%):** Service methods use full EF Core model classes. Uses `.FindAsync()`, `.Include()` for tracked entities. Generates targeted UPDATE statements. Uses `ApplicationDbContext`.
- **Firewall rule:** UI (Razor components) NEVER sees full EF Core model classes. Only DTOs and service interfaces. Full model classes are never sent "over the wire" — prevents change tracker issues, over-posting security risks, serialization loops, and excessive payloads.

### DTO Strategy

- DTOs for all read operations (service return types).
- Primitives for simple write operations (e.g., `UpdateStoryTitleAsync(int storyId, string newTitle)`).
- DTOs for complex write operations with 3+ parameters.
- ValueTuples acceptable for 2–3 property returns from read operations.
- Anonymous types only for intermediate data handling within methods (cannot cross service boundaries).
- Out parameters avoided — don't work with async/await.

### Write Path: Redis Queue + Background Worker

For high-frequency writes (UserStoryInteraction updates from Ignore/Favorite/Follow buttons):

1. **Client (Blazor WASM component):** Optimistic UI update on click. 2-second debounce timer per component to absorb click/unclick churn. When timer fires, sends single API call for that one story.
2. **API Controller ("fast and dumb"):** Validates request, `LPUSH`es update message into Redis list (`interaction-queue`). Returns `202 Accepted` immediately. Does NOT touch DbContext.
3. **Background Worker (`IHostedService`):** Wakes every 5 seconds. Pulls all pending messages from Redis queue. Consolidates across ALL users into a `Dictionary<(UserId, StoryId), LatestState>`. Performs one efficient batch MERGE/UPDATE to PostgreSQL.

Why not client-side batching? In the SPA model, navigating via Blazor Router preserves WASM state, but per-component debouncing with immediate server dispatch is still safer and simpler than managing a global client-side queue.

### Read Path: PostgreSQL Read Replica

- **Primary server:** Handles all INSERT, UPDATE, DELETE (from background workers and direct writes).
- **Read replica:** Handles all SELECT queries from the application's search, filtering, and display features.
- **Replication lag:** Near-real-time but not instant. UI must handle eventual consistency (e.g., showing optimistic state locally for a few seconds after a write).

### Background Workers

| Worker | Schedule | Purpose |
|---|---|---|
| **Write-behind worker** | Every 5 seconds | Drains Redis interaction queue, consolidates, batch writes to PostgreSQL |
| **View count worker** | Every 5 seconds | Reads Redis INCR keys, resets to zero, batch-updates DB view counts |
| **TreeSearch data mart worker** | Daily (off-hours) | Rebuilds `UserStoryTreeSearchEntries` table with zero-downtime swap |
| **AlsoFavorited/AlsoRecommended worker** | Daily (off-hours) | Rebuilds collaborative filtering cache tables with zero-downtime swap |
| **Badge awarding worker** | Event-driven | Receives events (e.g., `NewCommentPosted`), checks stats, awards badges |
| **Notification cleanup worker** | Daily | Deletes read notifications older than 60 days |
| **UserStat recalculation** | Periodic | Pre-calculates denormalized counters (WordsRead, FollowerCount, etc.) |
| **Daily site stats** | Daily | Aggregates into `SiteDailyStat` table |

**Zero-downtime cache refresh pattern (table swap):**
1. Two physical tables: `cache_table_a` and `cache_table_b`.
2. A PostgreSQL view (or function) called `cache_table_live` points to whichever is currently active.
3. Worker identifies the inactive ("staging") table, TRUNCATEs it, populates it with fresh data.
4. Atomically swaps via `ALTER TABLE ... RENAME` within a transaction.
5. Next run uses the other table as staging.

> **Previously considered:** SQL Server SYNONYMs for the swap. PostgreSQL doesn't have SYNONYMs; atomic `ALTER TABLE ... RENAME` within a transaction is the equivalent.

Background worker queries use raw SQL via `_context.Database.ExecuteSqlRawAsync()`, not EF Core LINQ. EF Core LINQ is used for live application queries.

### Caching Architecture (Multi-Layer)

1. **Redis:** View count `INCR` operations, write-behind queue, LastReadDate ephemeral storage, distributed cache for expensive query results (tree search, Also Favorited).
2. **PostgreSQL cache tables:** `UserStoryTreeSearchEntries`, `AlsoFavoritedScore`, `AlsoRecommendedScore` — rebuilt by background workers.
3. **Cloudflare CDN:** Free tier, caches all `wwwroot` static assets and R2-served images at edge nodes globally.
4. **Browser cache:** HTTP `ResponseCache` headers for tag data and other static API responses.
5. **In-memory service cache:** Tag lists cached in service instances after first fetch.
6. **PostgreSQL internal:** Buffer pool and query plan cache managed automatically by the engine.

### Sprite/Image Delivery

**Developer-controlled sprites (tags, themes):** Stored in `wwwroot/images/themes/{theme_name}/static/` and `wwwroot/images/themes/{theme_name}/animated/`. Served via Cloudflare CDN. Git is the source of truth — sprites are version-controlled and atomically deployed with code.

**URL builder pattern:** The `Tag` table stores a `SpriteIdentifier` (e.g., "fluff", "angst"). The client builds the full path at render time. Adding a new theme requires zero database changes — just create a new folder under `images/themes/`.

**`ISpriteService` implementations:**
- `ServerSpriteService`: Uses `IWebHostEnvironment` for `File.Exists()` to check whether a theme has a sprite, with fallback to `unknown_sprite.png`.
- `WasmSpriteService`: Constructs URL strings optimistically (no disk access, no HTTP call for URLs).

**Animated sprites:** Format is Animated WebP (not GIF) for full alpha transparency, smaller file size, and modern browser support. Logic checks `User.PrefersAnimatedSprites` to choose between `/static/` and `/animated/` paths.

**User uploads (cover art, profile pictures):** Stored in Cloudflare R2 via AWS SDK. R2 key convention: `stories/[StoryID]/cover-[uuid].jpg`, `users/[UserID]/profile-[uuid].jpg`. Organized keys enable easy bulk deletion for GDPR compliance. In dev, MinIO (local S3 emulator) runs via Aspire Docker container.

> **Previously considered:** Storing full URLs (`SpriteURL`, `AnimatedSpriteURL`) directly on the Tag table. Rejected because: if CDN domain ever changes, millions of rows would need updating. Also considered Cloudflare R2 for developer-controlled sprites, but `wwwroot` is simpler (Git-controlled, atomic deployment, cached by CDN identically).

### LastReadDate: Redis Hybrid Pattern

The `LastReadDate` for "In Progress" stories is too volatile for SQL (constant updates while reading). Instead:
- Stored in a Redis Hash: `user:{userId}:lastread` with field = storyId, value = timestamp.
- Updated via a lightweight API endpoint (`POST /api/reading/ping/{storyId}`) that only touches Redis.
- When rendering the "In Progress" page: (1) query SQL for the list of in-progress StoryIds, (2) one Redis `HGETALL` call for all timestamps, (3) merge and sort in C# memory.

### Security

- **HTML sanitization:** Server-side via `HtmlSanitizer` library on all user-submitted HTML before saving. Allow-list approach: strip `<script>`, `<style>`, `<iframe>`, `onerror`, `onload`, etc. Never save raw user HTML.
- **Rich text editing:** WYSIWYG editor (Blazored TextEditor wrapping Quill.js).
- **HTTPS:** Enforced via middleware.
- **Anti-forgery:** Via Blazor EditForm.
- **Over-posting prevention:** Via DTOs (not full model classes) at service boundaries.

### EF Core Configuration

- **Enum-to-short conversion:** `.HasConversion<short>()` on every enum property in `OnModelCreating`. Explicit per-property (no global loop possible due to C# generic constraints).
- **DateTime optimization:** All `DateTime` properties map to `timestamp(2) with time zone` (8 bytes). All `DateOnly` properties map to `date` (4 bytes). Creation timestamps use `HasDefaultValueSql("CURRENT_TIMESTAMP")`.
- **CHECK constraints:** NOT auto-generated by migrations — must be manually added via `migrationBuilder.Sql()` in migration `Up()` and `Down()` methods.
- **Delete policies:** Explicitly configured with `.OnDelete(DeleteBehavior.X)` on every relationship — never rely on EF Core defaults.
- **TPT inheritance:** Configured with `.ToTable()` per type.
- **Filtered indexes:** Use `HasFilter("column_name = true")` with snake_case column names and PostgreSQL `true`/`false` (not `1`/`0`). Use `HasDatabaseName()` for clarity and migration stability.
- **Read-only queries:** Use `ReadOnlyApplicationDbContext` with `.AsNoTracking()` globally.
- **Split queries:** `.AsSplitQuery()` to mitigate Cartesian Explosion when using `.Include()`.
- **Lazy Loading:** NOT used. Hides N+1 query problem. Explicit `.Include()` or `.Select()` projections only.

### Development Workflow (Scaffold-to-Code-First Hybrid)

1. Create DB with ASP.NET Core Identity (int keys) as starting point.
2. Run SQL script to add all custom tables and foreign keys.
3. Run `dotnet ef scaffold` to generate model classes (into temporary location to avoid overwriting `ApplicationDbContext`).
4. Delete the database.
5. Clean up models: convert magic bytes to enums, add `[Required]` and `[MaxLength]` annotations, fix TPT inheritance (`BaseComment` abstract, child classes inherit), merge scaffolded DbSets and `OnModelCreating` configurations into real `ApplicationDbContext`.
6. Generate migration: `dotnet ef migrations add InitialSchema`.
7. Edit migration: add `CREATE TRIGGER` in `Up()`, `DROP TRIGGER` in `Down()`, CHECK constraints via `migrationBuilder.Sql()`.
8. Apply: run app (auto-migrates + seeds).

**Pre-launch:** "Nuke and rebuild" workflow — delete Migrations folder, drop DB, regenerate. **Post-launch:** Never delete Migrations folder; create incremental migrations for every schema change.

### Scaling Architecture (Synergistic Layers)

1. **Vertical Partitioning:** Makes queries fundamentally efficient (reduces data per page read).
2. **Read Replicas:** Distributes SELECT queries across multiple DB instances.
3. **Background Workers:** Pre-calculate expensive aggregations offline.
4. **Redis Cache:** Prevents repetitive reads from hitting the DB at all.

### Server Deployment Topology

**Start:**
- Server 1: Web Application + Redis Cache
- Server 2: Managed PostgreSQL (DigitalOcean)

**Scale:**
- Servers 1–N: Multiple web app instances, load balanced
- Dedicated Redis server
- PostgreSQL Primary + Read Replicas (DigitalOcean managed)
- Optional: Elasticsearch/OpenSearch (managed) when FTS becomes a bottleneck

---

## 4. Database Schema

### Conventions

- **Naming:** snake_case in PostgreSQL via `EFCore.NamingConventions` plugin. C# code uses PascalCase.
- **Primary keys:** Single-column `int` identity where possible. `long` for "event" tables that grow indefinitely (comments, notifications, messages, chapter contents). Composite keys for junction and interaction tables.
- **Timestamps:** All `DateTime` properties map to `timestamp(2) with time zone` (8 bytes). All `DateOnly` properties map to `date` (4 bytes). Creation timestamps use `HasDefaultValueSql("CURRENT_TIMESTAMP")`.
- **Strings:** `[Required]` + non-nullable `string` for mandatory fields. `string?` without `[Required]` for optional. `[MaxLength(n)]` on all bounded strings.
- **Booleans:** `NOT NULL DEFAULT false`. PostgreSQL stores each as 1 byte (no bit-packing).
- **Enums:** Stored as `smallint` in PostgreSQL. Conversion via `.HasConversion<short>()`. Enums are 0-indexed (C# default).
- **URLs:** `[MaxLength(512)]` for CDN/relative URLs. `[MaxLength(2048)]` for arbitrary external URLs.
- **Filtered indexes:** Use `HasFilter("column_name = true")` with snake_case and PostgreSQL `true`/`false`.

### Enum / Lookup Table Decision Framework

| Pattern | When Used | Examples |
|---|---|---|
| **Magic enum** (C# enum, no lookup table) | Tiny, stable list tightly coupled to app logic; no display name needed | `Rating`, `FavoriteStatus`, `ReadStatus`, `RelationshipType` on `StoryCharacterRelationships` |
| **Lookup table** (no C# enum) | Content-only display; want flexibility to add/rename without deployment | `ReportReason`, `AcknowledgmentRole`, `StoryRelationshipType` |
| **Hybrid** (lookup table + C# enum) | Both flexible display AND rigid C# business logic needed | `StoryStatus`, `TagType`, `ReportStatus`, `NotificationType`, `RecommendationStatus` |
| **String key** (NVARCHAR PK) | Table is tiny; key used directly in C# code as identifier | `SearchMode.SearchModeKey`, `Badge.BadgeKey`, `UserInteractionFilter.InteractionFilterKey` |

### Inheritance Strategy: Table-per-Type (TPT) with Denormalization

All three inheritance hierarchies use TPT:
- `BaseComment` → `ChapterComment`, `UserProfileComment`, `GroupComment`, `BlogPostComment`
- `BaseBlogPost` → `ProfileBlogPost`, `GroupBlogPost`
- `BasePoll` → `SitePoll`, `BlogPostPoll`

**Rationale:** TPT provides NOT NULL guarantees on child FKs (e.g., `ChapterId` on `ChapterComment`). TPH requires these to be nullable, breaking data integrity. TPT's child tables create natural vertical partitions. The "extra join" is on primary keys (the fastest possible operation) and is negligible for the primary query pattern (get comments for one chapter).

**Denormalization:** `DatePosted` is duplicated from base into each child comment table. This enables composite "golden indexes" like `(ChapterId, DatePosted DESC)` directly on the small child table, eliminating expensive cross-table sort operations.

> **Previously considered:** TPH (single table with discriminator). Extensively debated and rejected because nullable FK columns in TPH break data integrity, and the child tables in TPT function as natural vertical partitions of hot metadata vs cold text content.

### Vertical Partitioning Strategy

| Entity | Hot Table | Warm Table | Cold Table | Rationale |
|---|---|---|---|---|
| Story | `Story` (~70 bytes/row) | `StoryListing` (~254 bytes/row) | `StoryDetail` (blob) | High-growth; hot must fit in RAM. 3-table split. |
| User | `User` (~150 bytes/row, hot+warm) | — | `UserProfile` (ProfileText blob) | Low-growth (~15MB for 100k users). 2-table split. |
| Recommendation | `Recommendation` (hot) | — | `RecommendationDetail` (Text blob) | True O(n×m) growth; hot for worker scans. 2-table split. |
| UserStoryInteraction | `UserStoryInteraction` (filtering) | `UserStoryInteractionDate` (user lists) | `UserStoryRecommendationSource` (workers) | Query-pattern partitioning. |

**RAM Budget at scale (100k users, 200k stories, 5M comments, 20M interactions):** Total hot data ~2.1 GB, fits in 4 GB buffer pool. Without partitioning, stories with LongDescription would be ~60 GB and would NOT fit.

---

### Core Content Tables

#### Story (Hot)

| Column | Type | Constraints | Notes |
|---|---|---|---|
| StoryId | int | PK, Identity | |
| AuthorId | int? | FK → User, SET NULL | Nullable for anonymization on user deletion |
| Rating | smallint (enum) | | Magic enum, no lookup table |
| StoryStatusId | smallint | FK → StoryStatus, RESTRICT | Hybrid enum-mirror pattern |
| WordCount | int | Default 0 | Sum of chapter word counts |
| ViewCount | int | Default 0 | Updated by Redis background worker |
| PublishedDate | DateTime? | Default CURRENT_TIMESTAMP | |
| LastUpdatedDate | DateTime? | | Updated by application logic |
| OriginalPublishedDate | DateOnly? | | For imported works |
| OriginalLastUpdatedDate | DateOnly? | | For imported works |
| ActiveReportCount | int | Default 0 | For excluding stories exceeding report threshold |
| IsComplete | bool | Default false | |
| ChapterCount | int | Default 0 | Denormalized |
| CommentCount | int | Default 0 | Denormalized for pagination |
| FavoriteCount | int | Default 0 | Denormalized; includes both public and hidden favorites |

Story statuses: Draft, PendingApproval, InProgress, Completed, OnHiatus, Cancelled, Rewriting, OpenBeta, Rejected.

#### StoryListing (Warm)

| Column | Type | Constraints | Notes |
|---|---|---|---|
| StoryId | int | PK, FK → Story, CASCADE | |
| StoryTitle | string | Required, MaxLength(255) | |
| ShortDescription | string? | MaxLength(500) | For previews and tooltips; must stay in-row |
| CoverArtRelativeUrl | string? | MaxLength(512) | Relative path appended to CDN base URL |
| SearchVector | NpgsqlTsVector | Generated computed column | GIN indexed for full-text search |

The `SearchVector` column combines `StoryTitle` and `ShortDescription` for PostgreSQL full-text search via `tsvector`.

#### StoryDetail (Cold)

| Column | Type | Constraints | Notes |
|---|---|---|---|
| StoryId | int | PK, FK → Story, CASCADE | |
| LongDescription | string? | No MaxLength (maps to TEXT) | Only loaded on story detail page |
| Slug | string? | MaxLength(255), Unique filtered (IS NOT NULL) | URL-friendly; routing uses StoryId, slug for SEO |
| PostApprovalStatus | smallint (enum) | | Used once when story exits moderation queue |

**Slug behavior:** Hybrid URL pattern `/story/{StoryId:int}/{*StorySlug}` — StoryId used for DB lookup, slug for SEO. Redirect to canonical URL if slug doesn't match. Generated from title in C# code; uniqueness enforced with appended number on collision.

#### Chapter

| Column | Type | Constraints | Notes |
|---|---|---|---|
| ChapterId | int | PK, Identity | |
| StoryId | int | FK → Story, CASCADE | |
| ChapterNumber | int | | |
| Title | string? | MaxLength(255) | UI shows "Chapter X" placeholder if null |
| PrimaryContentId | long | FK → ChapterContent, RESTRICT | Cannot delete the current primary version |
| IsPublished | bool | Default false | |
| VersionCount | int | Default 1 | Denormalized; synchronized in ChapterService |

Constraints: Unique composite on `(StoryId, ChapterNumber)`.

Two relationships with `ChapterContent`:
- 1-to-many: `Chapter.ChapterContents` (CASCADE delete — deleting a chapter deletes all its versions)
- 1-to-1: `Chapter.PrimaryContent` via `PrimaryContentId` (RESTRICT delete, `WithMany()` for unidirectional)

#### ChapterContent

| Column | Type | Constraints | Notes |
|---|---|---|---|
| ChapterContentId | long | PK, Identity | long for high volume |
| ChapterId | int | FK → Chapter, CASCADE | |
| AuthorId | int? | FK → User, SET NULL | Supports co-author versioning |
| SortOrder | int | | Unique with ChapterId |
| ChapterText | string | No MaxLength (TEXT) | HTML from WYSIWYG editor, sanitized server-side |
| ContentRaw | string | No MaxLength (TEXT) | Markdown/editor source format |
| WordCount | int | Default 0 | Calculated on sanitized, tag-stripped plain text |
| Rating | smallint? (enum) | | Nullable; if NULL, inherits story rating |
| TopAuthorsNote | string? | | |
| BottomAuthorsNote | string? | | |
| PublishDate | DateTime? | Default CURRENT_TIMESTAMP | |

Constraints: Unique composite on `(ChapterId, SortOrder)`.

**Business rule:** Only T-rated stories can contain M-rated chapter versions. Enforced in application logic, not the database.

> **Previously considered name:** `ChapterVersions`. Renamed to `ChapterContents` because 99% of chapters have only one version; "Contents" is more intuitive. This table supports live alternate versions (e.g., T-rated and M-rated versions of the same chapter), not revision history.

#### StoryArc

| Column | Type | Constraints | Notes |
|---|---|---|---|
| StoryArcId | int | PK, Identity | |
| StoryId | int | FK → Story, CASCADE | |
| Title | string | Required, MaxLength(255) | |
| SortOrder | int | | |
| StartChapterNumber | int | | |
| EndChapterNumber | int | | |

Validation for overlaps/gaps handled in C# application code.

#### Series / SeriesEntries

Dedicated `Series` table for author-defined canonical series. `SeriesEntries` junction table with `OrderIndex` defines reading order.

| Column (Series) | Type | Constraints |
|---|---|---|
| SeriesId | int | PK, Identity |
| AuthorId | int? | FK → User, SET NULL |
| Title | string | Required, MaxLength(255) |

| Column (SeriesEntries) | Type | Constraints |
|---|---|---|
| SeriesId | int | PK (composite), FK → Series, CASCADE |
| StoryId | int | PK (composite), FK → Story, CASCADE |
| OrderIndex | int | Reading order |

#### StoryRelationships

One-way directional links between stories. The source story displays the link; the target is linked to.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| SourceStoryId | int | PK (composite), FK → Story, CASCADE | The story displaying the link |
| TargetStoryId | int | PK (composite), FK → Story, NO ACTION | The story being linked to |
| RelationshipTypeId | int | FK → StoryRelationshipType, RESTRICT | |
| StatusId | smallint (enum) | | Pending, Approved, Rejected |

Seeded relationship types: "Inspired By", "Prequel", "Sequel", "Companion Piece".

The absence of a reverse entry means "don't show the link on the target story's page." This solves the "precious real estate" problem for famous stories with many minor prequels.

> **Column name evolution:** `SourceStoryID`/`TargetStoryID` → `ChildStoryID`/`ParentStoryID` → back to `SourceStoryID`/`TargetStoryID`. "Source/Target" describes the function (source displays the link, target is linked to) without confusing temporal relationships in prequels.

---

### Tag System Tables

#### Tag

| Column | Type | Constraints | Notes |
|---|---|---|---|
| TagId | int | PK, Identity | |
| TagName | string | Required, MaxLength(100), Unique | |
| TagTypeId | smallint | FK → TagType, RESTRICT | Enum: Character, Setting, Genre, ContentWarning, CrossoverFandom, Relationship |
| IsFanon | bool | Default false | Community-created vs official canon |
| ParentTagId | int? | FK → Tag (self-ref), SET NULL | One level deep only |
| SpriteIdentifier | string? | MaxLength(100) | Key for URL builder pattern; NOT a URL |
| AllowOCDetails | bool | Default false | Whether stories can attach OC details to this tag |
| Description | string? | MaxLength(500) | Used as tooltip on story cards |

Staff-managed, curated tag system. Users cannot create new tags. `TagOrigin` distinction uses the `IsFanon` boolean rather than a separate column.

> **Previously considered:** Table splitting (Tags + TagDetails) for Description. Rejected because Description is needed as a tooltip on story cards (same hot path as TagName and SpriteIdentifier).

#### TagType (Lookup, Seeded)

Values: Character, Setting, Genre, ContentWarning, CrossoverFandom, Relationship.

> **Previously considered name:** "Universe" instead of "Setting." Rejected because Pokémon has multiple official canons; "Alternate Universe" is confusing in this context. "Setting" is clearer.

#### StoryTag (Junction)

| Column | Type | Constraints | Notes |
|---|---|---|---|
| StoryId | int | PK (composite), FK → Story, CASCADE | |
| TagId | int | PK (composite), FK → Tag, RESTRICT | |
| Priority | smallint | | Sort order/weight of tags on story |

**Indexes:** Reverse index on `(TagId, StoryId) INCLUDE (Priority)` — enables efficient tag-based filtering with merge join.

> **Priority column name evolution:** `TagRole` → `Emphasis` → `Focus` → `Weight` → `Priority`. Final name is `Priority` as a generic sort/importance indicator.

#### StoryCharacter

Unified table for both canon characters and OCs. Needed as a separate table (not just StoryTags) because characters participate in relationship member linking.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| StoryCharacterId | int | PK, Identity | |
| StoryId | int | FK → Story, CASCADE | |
| CharacterTagId | int | FK → Tag, RESTRICT | Base character/species tag |
| Priority | smallint | | Primary/Supporting |
| IsOC | bool | Default false | |
| OC_Name | string? | MaxLength(100) | Only populated for OCs |
| OC_Bio | string? | MaxLength(1000) | Only populated for OCs |

OC details only allowed on tags with `AllowOCDetails = true` (enforced by trigger `TR_StoryCharacters_EnforceOCLogic`). No custom sprites for OCs — sprites come from the base tag only.

#### StoryCharacterRelationship / StoryCharacterRelationshipMembers

| Column (StoryCharacterRelationship) | Type | Notes |
|---|---|---|
| StoryCharacterRelationshipId | int | PK, Identity |
| StoryId | int | FK → Story, CASCADE |
| RelationshipType | smallint (enum) | 1=Romantic ('/'), 2=Platonic ('&') |
| Priority | smallint | Primary/Supporting |

`StoryCharacterRelationshipMembers` is a pure junction table linking `StoryCharacterRelationshipId` to `StoryCharacterId`.

#### SettingDetails

Optional override details for any setting/universe tag applied to a story.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| SettingDetailId | int | PK, Identity | |
| StoryId | int | FK → Story, CASCADE | |
| BaseTagId | int | FK → Tag, RESTRICT | Must be a Setting-type tag |
| Name | string? | MaxLength(255) | Custom universe/setting name |
| Description | string? | MaxLength(1000) | |

> **Previously considered name:** `AUs` / `StoryUniverses`. Renamed to `SettingDetails` because "Setting" is clearer than "Universe" in the Pokémon multi-canon context.

#### SavedTagSelection / SavedTagSelectionEntry

Named sets of tags users can save for reuse when searching.

| Column (SavedTagSelection) | Type | Constraints |
|---|---|---|
| SavedTagSelectionId | int | PK, Identity |
| UserId | int | FK → User, CASCADE |
| Nickname | string | Required, MaxLength(100) |
| IsPublic | bool | Default false |

Unique constraint on `(UserId, Nickname)`. Sharing is copy-on-write: User B gets a personal, editable copy of User A's selection. `DeleteBehavior.Restrict` on `TagId` in the join table — cannot delete a tag that is in a saved selection.

---

### User & Identity Tables

#### User (extends IdentityUser\<int\>)

| Column | Type | Notes |
|---|---|---|
| ProfilePictureRelativeUrl | string?, MaxLength(512) | |
| Tagline | string?, MaxLength(256) | |
| ShowMatureContent | bool, default false | HOT filter — used in site-wide search queries. Kept as direct column, not in JSON. |
| PrefersDataSaverMode | bool, default false | |
| PrefersAnimatedSprites | bool, default true | |
| AllowDiscoveryFromHiddenFavorites | bool, default false | Opt-in consent for anonymous boost |
| ThemeId | int (FK → Theme) | |
| ReaderSettings | jsonb | FontName, FontSize, LineHeight, TextWidth, JustifyText, AutoLoadNextChapter, CollapseCommentThreads, DefaultPaginationSize, DefaultSearchSort |
| PrivacySettings | jsonb | ProfileVisibility, ShowActivityStatus, AllowProfileComments, AllowPrivateMessages, ShowUserStats, ShowCurrentlyReading |
| AuthorSettings | jsonb | DefaultStoryRating, DefaultCommentModeration, AllowStoryRecommendations |
| DateCreated | DateTime | |
| ActiveReportCount | int | |

User settings stored as JSON (`jsonb`) columns grouped by concern. Enums inside JSON use `HasConversion<short>()`. Chosen over separate table (User table is small, fits in RAM; extra JOIN for zero benefit) and over flat columns (keeps the class clean and organized; new settings don't require migrations).

Identity columns (`PasswordHash`, `SecurityStamp`, etc.) stay on the `User` table because the Identity framework expects one table.

> **Previously considered:** Separate `UserProfile` table for application-specific data. Rejected because every page load needs theme/sprite/SFW preferences — JOIN overhead on every request not worth schema purity. Also considered: `UserSettings` as a separate 1-to-1 partitioned table. Rejected for same reason.

#### UserProfile (Cold)

| Column | Type | Constraints |
|---|---|---|
| UserId | int | PK, FK → User, CASCADE |
| ProfileText | string? | Large text blob |

Only `ProfileText` moved to cold table. User table is ~15MB for 100k users — too small for further splitting.

#### UserStats

| Column | Type | Notes |
|---|---|---|
| UserId | int | PK, FK → User, CASCADE |
| StoryCount | int | Default 0 |
| ChapterCount | int | Default 0 |
| TotalWordCount | bigint | Default 0 |
| RecommendationCount | int | Default 0 |
| CommentCount | int | Default 0 |
| LikesReceived | int | Default 0 |
| FollowerCount | int | Default 0 |
| StoriesFavorited | int | Default 0 |
| StoriesRead | int | Default 0 |
| WordsRead | bigint | Default 0 |
| (22+ counter fields total) | | |

Denormalized counters updated in real-time by application logic. Background workers read these for badge checks — avoids expensive COUNT(*) queries.

> **Previously considered:** Key-value `(UserID, StatName, StatValue)` pattern. Rejected for terrible read performance (pivot queries), no type safety, complex C# code.

---

### User Interaction Tables

#### UserStoryInteraction (The "Hot" Table)

Highest-traffic table in the system. Stores current boolean state of a user's relationship to a story. Sparse pattern: no row = all defaults false. Row created only on first interaction.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| UserId | int | PK (composite), FK → User, CASCADE | |
| StoryId | int | PK (composite), FK → Story, CASCADE | |
| IsInProgress | bool | Default false | Split from ReadStatus enum |
| IsCompleted | bool | Default false | Split from ReadStatus enum |
| IsFavorite | bool | Default false | Public, on-profile |
| IsHiddenFavorite | bool | Default false | Private, off-profile, opt-in discovery |
| IsActivelyReading | bool | Default false | Manual "active library" toggle |
| IsFollowed | bool | Default false | Receive notifications on story updates |
| IsReadItLater | bool | Default false | |
| IsIgnored | bool | Default false | |

**Row size (PostgreSQL):** 4 + 4 + 8×1 = **16 bytes**.

**Filtered indexes:** Each boolean gets user-centric and story-centric filtered covered indexes:
```
-- User-centric (for "my favorites", "my followed", etc.)
ON (user_id) INCLUDE (story_id) WHERE (is_ignored = true)
ON (user_id) INCLUDE (story_id) WHERE (is_favorite = true)
ON (user_id) INCLUDE (story_id) WHERE (is_hidden_favorite = true)
ON (user_id) INCLUDE (story_id) WHERE (is_followed = true)
ON (user_id) INCLUDE (story_id) WHERE (is_read_it_later = true)
ON (user_id) INCLUDE (story_id) WHERE (is_completed = true)
ON (user_id) INCLUDE (story_id) WHERE (is_in_progress = true)

-- Combined "All Favorites"
ON (user_id) INCLUDE (story_id) WHERE (is_favorite = true OR is_hidden_favorite = true)

-- Story-centric (for public stats)
ON (story_id) INCLUDE (user_id) WHERE (is_favorite = true)
ON (story_id) INCLUDE (user_id) WHERE (is_followed = true)
```

> **ReadStatus/FavoriteStatus evolution:** Originally `ReadStatus` enum (Unread/InProgress/Completed) and `FavoriteStatus` enum (NotFavorited/Public/Private). Split into separate booleans because in PostgreSQL the row size is identical, but booleans enable cleaner 1-to-1 filtered indexes without enum value comparisons.

> **Table name evolution:** `UserStoryInteractions` → `UserStoryEngagement` → `UserStoryInteractions` → `UserStoryInteraction` (singular C# class). Final PostgreSQL table name: `user_story_interactions`.

> **Previously considered:** System lists (Favorites, Read It Later, Tracking) as rows in the `UserLists` table. Rejected because it requires complex JOINs through UserLists → UserListEntries for common operations like "Is this story favorited?" The wide-table approach gives a single BIT column check.

#### UserStoryInteractionDate (The "Warm" Table)

1-to-1 vertical partition of UserStoryInteraction. Only created when a user first performs a date-worthy action.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| UserId | int | PK (composite), FK → UserStoryInteraction, CASCADE | |
| StoryId | int | PK (composite) | |
| FavoriteDate | DateTime? | | |
| HiddenFavoriteDate | DateTime? | | Separate from FavoriteDate for independent filtered indexes |
| FollowedDate | DateTime? | | |
| ReadItLaterDate | DateTime? | | |
| IgnoredDate | DateTime? | | |
| CompletedDate | DateTime? | | |

**Filtered indexes for sorting user lists by date:**
```
ON (user_id, favorite_date DESC) WHERE (favorite_date IS NOT NULL)
ON (user_id, followed_date DESC) WHERE (followed_date IS NOT NULL)
ON (user_id, read_it_later_date DESC) WHERE (read_it_later_date IS NOT NULL)
ON (user_id, completed_date DESC) WHERE (completed_date IS NOT NULL)
```

> **Why not normalized?** A `(UserId, StoryId, DateType, ActionDate)` table would force a single massive index shared across all date types. Wide table with nullable columns enables small, specialized filtered indexes. 99% sparsity is irrelevant because filtered indexes skip NULL rows.

#### UserStoryRecommendationSource (The "Sparse" Table)

1-to-1 vertical partition. Only rows for interactions originating from a recommendation.

| Column | Type | Constraints |
|---|---|---|
| UserId | int | PK (composite), FK → UserStoryInteraction, CASCADE |
| StoryId | int | PK (composite) |
| SourceRecommendationId | int | FK → Recommendation, RESTRICT |

Reverse index on `SourceRecommendationId`. Moved from UserStoryInteraction because >99% of interactions don't come from recommendations, saving ~4 bytes per row × potentially 100M+ rows.

#### UserChapterInteraction

| Column | Type | Constraints | Notes |
|---|---|---|---|
| UserId | int | PK (composite), FK → User, CASCADE | |
| ChapterId | int | PK (composite), FK → Chapter, CASCADE | |
| IsRead | bool | Default false | Explicit "I read this" checkbox |
| ReadProgress | float | Default 0 | Scroll tracker (0.0–1.0), REAL type (4 bytes) |
| LastInteractionDate | DateTime? | | For "Continue Reading" feature |

**Design note:** Originally a single `double ReadProgress` which caused a UX bug (auto-scroll tracker would overwrite manual "mark as read" toggles). Split into `IsRead` (user's explicit state, only set to true by auto-tracker at >90% progress, never set to false) + `ReadProgress` (automatic scroll tracker). `float`/`REAL` chosen over `double` for space savings (4 vs 8 bytes).

---

### Community & Social Tables

#### FollowedUser

| Column | Type | Constraints | Notes |
|---|---|---|---|
| UserId | int | PK (composite), FK → User, CASCADE | The follower |
| FollowedUserId | int | PK (composite), FK → User, RESTRICT | The followed (resolved in C#) |
| DateFollowed | DateTime | Default CURRENT_TIMESTAMP | |
| ReceiveAlerts | bool | Default true | "Bell icon" toggle |
| IsVouched | bool | Default false | "Hidden Gem Author" endorsement |

**"Vouches" feature:** Users can designate up to 5 followed users as "vouched for" — a scarce, personal endorsement to promote discoverability. The 5-limit is enforced in the C# service layer. Filtered indexes on `(user_id) WHERE (is_vouched = true)` and `(followed_user_id) WHERE (is_vouched = true)`.

> **Previously considered:** Separate `UserFollows` table with `FollowType` column (Favorite/Track). Merged into single `FollowedUsers` with `ReceiveAlerts` toggle. Also considered separate `UserVouches` junction table — rejected; boolean-on-FollowedUsers is simpler with zero additional storage.

> **Terminology:** "Tracked"/"Track" globally replaced with "Followed"/"Follow" for user familiarity (modern social media convention).

#### Recommendation (Hot)

| Column | Type | Constraints | Notes |
|---|---|---|---|
| RecommendationId | int | PK, Identity | |
| StoryId | int | FK → Story, CASCADE | |
| RecommenderId | int? | FK → User, SET NULL | Nullable for anonymization |
| StatusId | int | FK → RecommendationStatus, RESTRICT | Pending, Approved, Rejected, Under Review |
| IsHiddenGem | bool | Default false | Max 5 per user (enforced in C#) |
| IsHighlightedByAuthor | bool | Default false | Author spotlight, max 5 per story (enforced in C#) |
| SuccessfulRecCount | int | Default 0 | Denormalized |
| LikeCount | int | Default 0 | Denormalized |
| DatePosted | DateTime | Default CURRENT_TIMESTAMP | |
| ActiveReportCount | int | Default 0 | |

Unique constraint: `(RecommenderId, StoryId)` — one recommendation per user per story.

**Indexes:** `(story_id, is_highlighted_by_author, like_count)`, `(story_id, is_highlighted_by_author, date_posted)`, filtered `(recommender_id, date_posted) WHERE recommender_id IS NOT NULL`.

#### RecommendationDetail (Cold)

| Column | Type | Constraints |
|---|---|---|
| RecommendationId | int | PK, FK → Recommendation, CASCADE |
| Text | string, Required | The full recommendation text |

#### RecommendationSuccess

| Column | Type | Constraints |
|---|---|---|
| RecommendationSuccessId | int | PK, Identity |
| RecommendationId | int | FK → Recommendation |
| UserId | int | FK → User |
| DateConfirmed | DateTime | Default CURRENT_TIMESTAMP |

Tracks when a user confirms a recommendation was useful (popup after reading Chapter 1).

#### RecommendationLike (Junction)

`(UserId, RecommendationId)` composite PK. Both FKs CASCADE.

---

### Comment Tables (TPT)

#### BaseComment

| Column | Type | Constraints |
|---|---|---|
| CommentId | long | PK, Identity |
| UserId | int? | FK → User, SET NULL |
| ParentCommentId | long? | FK → BaseComment (self-ref), SET NULL |
| CommentText | string | No MaxLength (TEXT) |
| LikeCount | int | Default 0 |

`ParentCommentId` self-reference uses SET NULL — if parent deleted, replies become orphaned (displayed as children of "[Deleted Comment]").

#### ChapterComment

| Column | Type | Constraints |
|---|---|---|
| CommentId | long | PK, FK → BaseComment, CASCADE |
| ChapterId | int | FK → Chapter, CASCADE |
| DatePosted | DateTime | Default CURRENT_TIMESTAMP (denormalized) |

**Golden index:** `(chapter_id, date_posted DESC)` enables efficient pagination without cross-table sort.

#### UserProfileComment

| Column | Type | Constraints |
|---|---|---|
| CommentId | long | PK, FK → BaseComment, CASCADE |
| ProfileUserId | int | FK → User, CASCADE |

#### GroupComment

| Column | Type | Constraints |
|---|---|---|
| CommentId | long | PK, FK → BaseComment, CASCADE |
| GroupId | int | FK → Group, CASCADE |

#### BlogPostComment

| Column | Type | Constraints |
|---|---|---|
| CommentId | long | PK, FK → BaseComment, CASCADE |
| BlogPostId | long | FK → BaseBlogPost, CASCADE |

#### CommentLike (Junction)

Pure many-to-many: `(UserId, CommentId)`, both FKs CASCADE. **No `DateLiked` column.** Navigation: `BaseComment.LikedByUsers` ↔ `User.LikedComments`.

Anti-addictive design: no trending, no activity feeds, no notifications for likes. All features enabled by `DateLiked` are features the site deliberately does not want.

---

### Blog Post Tables (TPT)

#### BaseBlogPost

| Column | Type | Constraints |
|---|---|---|
| BlogPostId | long | PK, Identity |
| AuthorId | int? | FK → User, SET NULL |
| Title | string | Required, MaxLength(255) |
| Content | string | No MaxLength (TEXT) |
| IsPublished | bool | Default false |

`DateCreated` and `LastUpdatedDate` denormalized to derived tables (same rationale as comments).

#### ProfileBlogPost

`BlogPostId` (PK/FK to BaseBlogPost, CASCADE). Optional `StoryId` (FK → Story, SET NULL). `DateCreated`, `LastUpdatedDate`.

#### GroupBlogPost

`BlogPostId` (PK/FK to BaseBlogPost, CASCADE). `GroupId` (FK → Group, CASCADE). `DateCreated`, `LastUpdatedDate`.

CHECK constraint: A blog post links to either a Story OR a Group, but not both.

#### BlogPostLike (Junction)

`(UserId, BlogPostId)` composite PK. Both FKs CASCADE.

---

### Poll Tables (TPT)

#### BasePoll

| Column | Type | Constraints |
|---|---|---|
| PollId | int | PK, Identity |
| Title | string | Required |
| DateOpened | DateTime | Default CURRENT_TIMESTAMP |
| DateClosed | DateTime? | |

`DateOpened` and `DateClosed` remain in `BasePoll` (not denormalized). SitePolls table will be tiny; BlogPostPoll usually queried by BlogPostId not date.

#### SitePoll / BlogPostPoll

Derived types with their specific FKs.

#### PollOption / PollVote

Standard poll structure tables.

---

### Group Tables

#### Group

| Column | Type | Constraints |
|---|---|---|
| GroupId | int | PK, Identity |
| CreatorId | int? | FK → User, SET NULL |
| GroupName | string | Required, MaxLength(100), Unique |
| Description | string? | MaxLength(1000) |
| AudienceRating | smallint | Group visibility filter |
| MaxContentRating | smallint | What stories can be added |
| DateCreated | DateTime | |

#### GroupMember (Junction)

| Column | Type | Constraints |
|---|---|---|
| UserId | int | PK (composite), FK → User, CASCADE |
| GroupId | int | PK (composite), FK → Group, CASCADE |
| Role | smallint (enum) | Member=0, Moderator=1, Admin=2 |

#### GroupStory (First-Class Entity)

Promoted from junction table to first-class entity with its own `GroupStoryId` PK to support many-to-many with GroupFolders.

| Column | Type | Constraints |
|---|---|---|
| GroupStoryId | int | PK, Identity |
| GroupId | int | FK → Group, CASCADE |
| StoryId | int | FK → Story, CASCADE |
| AddedByUserId | int? | FK → User, SET NULL |
| DateAdded | DateTime | Default CURRENT_TIMESTAMP |

Unique constraint on `(GroupId, StoryId)`.

#### GroupFolder

Supports nesting via nullable `ParentFolderId` (self-referencing, SET NULL on delete). Unique constraint on `(GroupId, ParentFolderId, Name)`.

`GroupFolder ↔ GroupStory`: Many-to-many. EF Core auto-generates a pure junction table `GroupFolderGroupStory`.

---

### Notification System Tables

#### NotificationCategory (Lookup, Seeded)

9 categories: SiteNews, YourFollows, YourStories, YourProfile, YourRecommendations, Collaborations, Groups, Warnings, YourReports.

#### NotificationType (Lookup, Seeded)

~35 types with gap-based numbering (0, 10–16, 20–28, 30–33, 40–42, 50–52, 60–61, 70–74, 80–82) for grouping and future extensibility. Each type belongs to a category and has a `DefaultEmailEnabled` flag.

**Notification philosophy:** Low-effort actions (likes) → NO notifications. High-effort actions (comments, follows, recommendations) → YES. All report outcomes → YES (transparency).

#### Notification

| Column | Type | Constraints |
|---|---|---|
| NotificationId | long | PK, Identity |
| RecipientUserId | int | FK → User, CASCADE |
| NotificationTypeId | smallint | FK → NotificationType |
| SourceUserId | int? | FK → User, RESTRICT (resolved in C#) |
| RelatedEntityId | int | Polymorphic (no FK) |
| IsRead | bool | Default false |
| DateCreated | DateTime | Default CURRENT_TIMESTAMP |

Index: `(recipient_user_id, is_read, date_created DESC)`.

Read notifications auto-deleted after 60 days by background worker. Unread notifications never auto-deleted.

#### UserNotificationSetting

Sparse override model: empty for new users. Only stores rows where user's preference differs from default.

| Column | Type | Constraints |
|---|---|---|
| UserId | int | PK (composite), FK → User, CASCADE |
| NotificationTypeId | smallint | PK (composite), FK → NotificationType |
| EmailEnabled | bool | |

---

### Search & Filter Tables

#### SearchMode (Lookup, String Key)

Seeded values: "TreeSearch", "RandomSearch", "AlsoFavorited".

#### UserInteractionFilter (Lookup, String Key)

Seeded values: "Ignored", "Completed", "InProgress", "ReadItLater", "Favorite", "HiddenFavorite", "Followed".

#### DefaultSearchSetting (Matrix)

`(SearchModeKey, InteractionFilterKey)` composite PK with `DefaultValue` (bool). Stores site-wide default checkbox states. Highly cacheable.

#### UserSearchSetting (Sparse Override)

`(UserId, SearchModeKey, InteractionFilterKey)` composite PK with `UserValue` (bool). On user creation: zero rows written.

#### UserCustomFilter

User-selected list/group-based inclusion/exclusion filters.

| Column | Type | Constraints |
|---|---|---|
| UserCustomFilterId | int | PK, Identity |
| UserId | int | FK → User, CASCADE |
| SearchModeKey | string | FK → SearchMode |
| FilterTypeId | smallint (enum) | 1=UserList, 2=Group |
| EntityId | int | Polymorphic |
| Include | bool | 0=blacklist, 1=whitelist |

---

### Reports & Moderation Tables

#### Report

| Column | Type | Constraints |
|---|---|---|
| ReportId | long | PK, Identity |
| ReporterUserId | int? | FK → User, SET NULL |
| ReportedEntityTypeId | smallint (enum) | User, Story, Recommendation, Comment, BlogPost |
| ReportedEntityId | long | Polymorphic (no FK) |
| ReportReasonId | int | FK → ReportReason, RESTRICT |
| ReportStatusId | smallint | FK → ReportStatus, RESTRICT |
| ModeratorUserId | int? | FK → User, SET NULL |
| Notes | string? | |
| ActionTaken | string? | |
| DateReported | DateTime | |

**Critical:** No database-level FK for polymorphic EntityId. Application code 100% responsible for cleanup before parent deletion.

Reports and Notifications are kept as polymorphic tables (NOT switched to TPT). Rationale: primary use case is consolidated view (moderator queue, user notification feed). TPT would require expensive UNION queries.

Filtered index: `WHERE report_status_id = 0` (Open status).

#### ReportReason (Lookup, Seeded)

6 values: Other, Spam, Hate Speech, Harassment, Illegal Content, Plagiarism.

#### ReportStatus (Lookup, Hybrid Enum)

4 values: Open, UnderReview, ResolvedNoAction, ResolvedActionTaken.

---

### Monetization & Community Tables

#### CommunitySpotlight

| Column | Type | Constraints |
|---|---|---|
| CommunitySpotlightId | int | PK, Identity |
| StoryId | int | FK → Story |
| SponsoringUserId | int? | FK → User, SET NULL |
| StartDate | DateTime | |
| EndDate | DateTime | |

Restrictions: no self-spotlighting, cooldown per story. Index on `(StartDate, EndDate)`.

#### FeatureContribution

Tracks admin attributions for accepted feature suggestions.

| Column | Type | Constraints |
|---|---|---|
| FeatureContributionId | int | PK, Identity |
| UserId | int? | FK → User, SET NULL |
| Description | string | |
| DateAwarded | DateTime | |

#### StoryImport

| Column | Type | Constraints |
|---|---|---|
| StoryImportId | int | PK, Identity |
| StoryId | int | FK → Story |
| SourcePlatform | string | |
| SourceURL | string, MaxLength(2048) | |
| VerificationStatus | smallint (enum) | Pending, Verified, Rejected |

MVP: manual mod verification. Future: polite, slow, cached scraper (high risk of blocking).

---

### Private Messaging Tables

#### Conversation / ConversationParticipant / PrivateMessage

Standard three-table model.

| Column (ConversationParticipant) | Type | Constraints |
|---|---|---|
| ConversationId | int | PK (composite), FK → Conversation, CASCADE |
| UserId | int | PK (composite), FK → User, CASCADE |
| LastReadTimestamp | DateTime? | |
| IsArchived | bool | Default false |

| Column (PrivateMessage) | Type | Constraints |
|---|---|---|
| PrivateMessageId | long | PK, Identity |
| ConversationId | int | FK → Conversation |
| SenderUserId | int? | FK → User, SET NULL |
| MessageText | string | |
| DateSent | DateTime | |

Real-time delivery via ASP.NET Core SignalR (WebSockets with graceful fallback). Index: `(conversation_id, date_sent DESC)`.

---

### Badge Tables

#### Badge (String Key)

| Column | Type | Constraints |
|---|---|---|
| BadgeKey | string, MaxLength(50) | PK |
| Name | string | |
| Description | string | |
| IconURL | string? | |
| SortOrder | int | |

Seeded examples: "beta-reader", "first-story", "word-count-100k".

#### UserBadge (Junction)

| Column | Type | Constraints |
|---|---|---|
| UserId | int | PK (composite), FK → User, CASCADE |
| BadgeKey | string | PK (composite), FK → Badge, CASCADE |
| DateEarned | DateTime | |
| DisplayOrder | int | Default 0. 0 = not displayed, 1+ = display position |

---

### Cache / Data Mart Tables (NOT in EF Core Migrations)

These tables have NO EF Core model classes, no foreign keys, are created dynamically by background workers.

#### UserStoryTreeSearchEntry

Pre-calculated denormalized table for graph traversal. Rebuilt daily. Contains NO private data.

| Column | Type | Notes |
|---|---|---|
| UserId | int | PK (composite) |
| StoryId | int | PK (composite) |
| IsAuthoredByUser | bool | From Stories table |
| IsPublicFavorite | bool | From UserStoryInteraction.IsFavorite |
| IsRecommendation | bool | From Recommendations table |
| IsHiddenGem | bool | From Recommendations.IsHiddenGem |
| IsAuthorSpotlighted | bool | From Recommendations.IsHighlightedByAuthor |
| IsHiddenFavorite | bool | Consent-based: only when user opted in |

Mirrored filtered indexes in both directions (User→Story and Story→User) for each boolean flag.

**Privacy model:** `IsHiddenFavorite` populated only when `UserStoryInteraction.is_hidden_favorite = true AND User.allow_discovery_from_hidden_favorites = true`. The 24-hour rebuild delay makes differential attacks impractical.

#### AlsoFavoritedScore / AlsoRecommendedScore

| Column | Type |
|---|---|
| StoryId | int | PK (composite) |
| AlsoFavoritedStoryId (or AlsoRecommendedStoryId) | int | PK (composite) |
| Score | int | Co-occurrence count |

Full matrix stored both directions (A→B and B→A) for fast single-column lookups. Background worker runs self-join on UserStoryInteraction where `IsFavorite = true`. Redis cache layer stores Top 100 per story; service applies real-time user-specific exclusion filters in C#.

#### SiteDailyStat

| Column | Type | Notes |
|---|---|---|
| StatDate | DateOnly | PK |
| NewUsers | int | |
| TotalUsers | int | |
| NewStories | int | |
| TotalStories | int | |
| NewWords | bigint | |
| TotalWords | bigint | |
| PageViews | int | |
| ActiveUsers | int | |

---

### Delete Policy Summary

**Philosophy:**
- **Content** (stories, comments, blog posts, recommendations): **SET NULL** on author deletion → anonymize but preserve. Application renders NULL authors as "[Deleted User]".
- **Interaction data** (follows, interactions, group memberships, badges, custom lists, settings): **CASCADE** on user deletion → delete with user.
- **Lookup tables** (tags, themes, statuses, badges): **RESTRICT** → cannot delete if in use.
- **Self-references** (parent comments, parent tags, parent folders): **SET NULL** → children become top-level.

**Direct conflicts requiring C# code (two FKs to same User table):**

| Table | Conflicting FKs | Resolution | App Code Required |
|---|---|---|---|
| FollowedUser | Both → User | `FollowedUserId` → RESTRICT | DELETE follower records where user is the followed one |
| Notification | Both → User | `SourceUserId` → RESTRICT | SET NULL on sent notifications before deletion |
| UserProfileComment | Both → User | `UserId` (author) → RESTRICT | SET NULL on authored profile comments before deletion |
| StoryRelationships | Both → Story | `TargetStoryId` → NO ACTION | Delete inbound relationships before story deletion |
| BaseComments | Self-reference | `ParentCommentId` → NO ACTION/SET NULL | Handle reply chains |
| UserStoryInteraction | Indirect via Recommendations | `SourceRecommendationId` → RESTRICT | Set NULL before deleting recommendation |

---

### Constraint Naming Convention

| Type | Pattern | Example |
|---|---|---|
| Primary Keys | `PK_TableName` | `PK_Stories` |
| Foreign Keys | `FK_Source_Target` | `FK_Stories_Users` |
| Unique | `UQ_Table_Columns` | `UQ_User_ListName` |
| Check | `CK_Table_Column_Condition` | `CK_StoryTags_Priority` |
| Default | `DF_Table_Column` | `DF_Stories_IsComplete` |
| Index | `ix_{table}_{columns}` | `ix_user_story_interactions_is_ignored` |

---

### Seed Data (Lookup Tables)

**Enum-backed tables (seeded via `HasData()`):**
- `StoryStatus` (9 values, 0-indexed)
- `TagType` (6 values)
- `ReportStatus` (4 values)
- `NotificationCategory` (9 categories)
- `NotificationType` (~35 types with gap-based numbering)

**Non-enum tables (seeded with explicit IDs):**
- `ApplicationRole` (Admin=1, Moderator=2, User=3)
- `AcknowledgmentRole` (Beta Reader, Planner, Cover Artist, Editor, Inspiration)
- `RecommendationStatus` (Pending Approval, Approved, Rejected, Under Review)
- `ReportReason` (Other, Spam, Hate Speech, Harassment, Illegal Content, Plagiarism)
- `StoryRelationshipType` (Inspired By, Prequel, Sequel, Companion Piece)
- `SearchMode` (TreeSearch, RandomSearch, AlsoFavorited)
- `UserInteractionFilter` (Ignored, Completed, InProgress, ReadItLater, Favorite, HiddenFavorite, Followed)
- `Theme` (Default Light, Default Dark)
- `Badge` (beta-reader, first-story, word-count-100k, etc.)
- `DefaultSearchSetting` (SearchMode × InteractionFilter matrix)

---

## 5. Feature Specifications

### Story Lifecycle

Stories follow a status workflow: **Draft** → **PendingApproval** → moderator approves to author's chosen `PostApprovalStatus` (**InProgress**, **Completed**, **OnHiatus**, **Cancelled**, **Rewriting**, **OpenBeta**) or **Rejected**. New stories default to PendingApproval. Unverified imported stories display with an "Unverified" banner and are excluded from homepage/spotlight.

### Content Ratings

Story-level `Rating` enum: E (Everyone), T (Teen), M (Mature). Default is T. Individual chapter contents can override via `ChapterContent.Rating` (nullable — NULL inherits story rating). If a chapter is rated M in a T-rated story, the UI shows a content warning with a "Skip to next chapter" button. Business rule: only T-rated stories can contain M-rated chapter versions (enforced in application logic, not DB).

**Content filtering master rule:** "If the active user has mature content disabled, there should be no visible trace of mature content anywhere on the site." Enforced at query time in C# application layer. Every query returning stories, groups, or folders must include the user's max rating filter.

### Discoverability — Random Search

User sets tag filters → results returned in random order → stories the user has marked as `IsIgnored` or already interacted with are excluded. Two search paradigms supported:

- **Discovery Model** (exclude filters): "Show everything except..." Default mode. Checkboxes unchecked by default.
- **Library Model** (include filters): "Show me only..." Separate page. Results start empty until filters applied.

### Discoverability — Tree Search

A user-story graph visualization system with two modes:

**Manual Tree Search:** User picks a starting node (story or user) and a criteria (e.g., "Favorited by"). UI shows connected nodes. Clicking a node expands the next level. Only uses `IsPublicFavorite` edges.

**Automatic Tree Search:** User picks a root, degree count, and edge criteria. A recursive CTE runs against the `UserStoryTreeSearchEntries` cache table. Results filtered by the active user's own `UserStoryInteraction` flags (for exclusion/highlighting).

**Edge criteria:** IsPublicFavorite (manual and automatic), IsRecommendation, IsHiddenGem (capped at 5 per user for narrow traversal), IsAuthorSpotlighted (author rewards quality recommenders), IsAuthoredByUser.

**Two traversal strategies:**
- **Wide search:** Low MaxDegrees (2–3), any edge type (favorites, recommendations). Finds stories close to root.
- **Deep search:** Higher MaxDegrees (5–6), only capped-length criteria (Hidden Gems, Author Spotlighted). Creates "chain of trust" through curated, limited-entry lists.

**Privacy rule:** Tree search table contains ONLY public edges. Hidden favorites are NEVER used as traversal edges for other users unless the user has opted in via `AllowDiscoveryFromHiddenFavorites`, and even then the contribution is anonymized and delayed by 24 hours.

Each "pivot" (clicking a new root story) triggers a fresh, stateless search — previous results NOT passed to the database. Application-side duplicate filtering is acceptable.

### "Also Favorited" / "Also Recommended"

Pre-calculated collaborative filtering: "Users who favorited Story A also favorited Story B." Stored in cache tables with `Score` (count of shared users). Full matrix stored both directions. Background worker runs weekly/daily. Redis cache layer stores Top 100 per story for instant retrieval. Service fetches from Redis, applies real-time user exclusion filters in C#, returns requested count.

> **Previously considered:** Incremental updates. Rejected as fragile and error-prone. Full rebuild is self-healing and guaranteed accurate.

### Recommendations System

High-effort written reviews. Minimum character count enforced in application. One per user per story. Status workflow: PendingApproval → Approved/Rejected/UnderReview. Auto-approval option for inactive authors or after time limit (e.g., 7 days).

**Hidden Gem flag:** Each user can mark up to 5 of their recommendations as Hidden Gems (enforced in C# service layer). These power the narrow "deep search" traversal path.

**Author Spotlight:** Authors can highlight up to 5 recommendations on their story (enforced in C# service layer). Displayed prominently on the story page.

**Recommendation Attribution ("Source Recommendation" feature):** When a user clicks "Read It Later" from a recommendation's UI, the `UserStoryRecommendationSource` table records which recommendation led them. After reading Chapter 1, a popup asks "Was this recommendation useful?" If confirmed, a `RecommendationSuccess` record is created, rewarding the recommender. User-facing term: "Successful Recs."

### Hidden Favorite with Opt-in Discovery

`IsHiddenFavorite` is a private bookmark that doesn't appear on the user's public profile. By default, it has zero public effect. Users can enable `AllowDiscoveryFromHiddenFavorites` in settings. When enabled, the daily background worker includes their hidden favorites as anonymous entries in the tree search data mart. The 24-hour rebuild delay prevents differential attacks.

> **Previously considered:** "Private Favorites" used directly in algorithms (rejected: differential attack vulnerability), combining `IsHiddenFavorite` and anonymous boost into one column (rejected: removes secret bookmark capability).

### Vouches

Users can designate up to 5 followed users as "vouched for." This is a scarce, personal endorsement stored as `IsVouched` on the `FollowedUser` table. The scarcity (5 limit) is semantically meaningful — "I'm personally putting my name behind these creators." Used as a discoverability signal.

> **Previously considered name:** "Hidden Gem Authors", "Endorsements", "Rising Stars". "Vouch" chosen because it conveys personal, binding assurance.

### Comments

Chapter-level threaded discussions. Self-referencing `ParentCommentId` for reply threading. TPT inheritance enables separate tables per comment context (chapter, user profile, group, blog post) with direct FKs for CASCADE deletes.

**Pagination:** Load chapter page with denormalized `CommentCount`. First query: `Skip(0).Take(20)` with join to BaseComment and User. The child table's golden index on `(ChapterId, DatePosted DESC)` makes this efficient.

### Blog Posts

Can be linked to either a Story OR a Group (not both; CHECK constraint). Three display modes: general → author profile; story-linked → author profile + story "Author's Notes" tab; group-linked → group page. TPT inheritance with `ProfileBlogPost` and `GroupBlogPost`.

### Groups

Community spaces with folders. Three group types based on audience/content rating combinations: Standard (visible to all, allows all content), SFW Only (visible to all, Teen max content), Mature (hidden from SFW users, allows all content). GroupFolders support nesting. GroupStory is a first-class entity (not a pure junction) to support many-to-many with folders.

### Reading Progress Tracking

Client-side JavaScript tracks scroll percentage. Throttled/debounced API calls update `UserChapterInteraction.ReadProgress` via Redis batching. At >90% progress, `IsRead` is automatically set to true (but never set to false). UI displays read/unread checkboxes per chapter in the table of contents. Users can manually toggle.

### View Count Tracking

View counts are NOT incremented on page load. They trigger on the first client-side "ping" (5-second timer or first scroll) to filter bots and bounces. Architecture: Redis `INCR` per view → background worker every 5 seconds batches to primary DB. Reduces 100,000 individual transactions to ~720 batch updates per hour.

### Full-Text Search

PostgreSQL `tsvector` generated computed column on `StoryListing` combining `StoryTitle` and `ShortDescription`. `GIN` index on the `SearchVector` column. Queried via `EF.Functions.ToWebSearchQuery()` and `SearchVector.Matches()`. FTS NOT applied to chapter body text (too expensive for writes, 99% of users don't need it). `LIKE '%...%'` queries explicitly forbidden.

### User Lists (Custom Only)

`CustomList` / `CustomListEntry` tables for user-created, fully custom lists. System lists (Favorites, Read It Later, Tracking, etc.) are handled by `UserStoryInteraction` boolean flags. Custom lists can be public or private. `ListAlerts` for email alerts on tracked list updates.

### Tag Selection UI

Small categories (Genres, Warnings, Settings — 10–100 items): client-side filtering, load all on init. Medium categories (Characters — up to ~2000 items for single-fandom): also client-side (100KB payload, smaller than one image). Typeahead/autocomplete with 300ms debounce. Separate API methods per tag type for progressive rendering and granular caching.

### Story Editing UX

In-place editing model (like AO3/Fimfiction, not separate dashboard like FFN). "Edit" buttons appear on the normal story viewing page only for the story author. Lazy-load heavy editor components only when user clicks "Edit." Encapsulated in an `<AdminControls>` component.

### Notification Settings Page

Driven by database data, not the C# enum. Query `NotificationCategories` with `Include(c => c.NotificationTypes)`, render category headers with toggle switches. The C# `NotificationTypeEnum` mirrors lookup table keys for type-safe switch statements only.

### Private Messaging

Standard conversation model with participants and messages. SignalR for real-time delivery. `LastReadTimestamp` on participants for unread tracking.

### Badge System

Event-driven: main app fires event (e.g., `NewCommentPosted(UserID)`), background worker checks stats and awards badges asynchronously. Users select which badges to display via `DisplayOrder` on `UserBadge`.

### Content Moderation

Story approval queue: new stories default to PendingApproval. Report-based auto-hiding: 3+ reports from different users in 24h → auto-flag for review. `ActiveReportCount` on content tables for threshold checking. User roles managed via ASP.NET Core Identity roles (Admin, Moderator, User). All report outcomes generate notifications (transparency principle).

### Roles and Authorization

Three roles seeded via `HasData()`: **Admin** (manage users, lookup tables, site configuration), **Moderator** (handle reports, approve/reject stories, edit/delete content, manage spotlights, lock threads), **User** (create/edit own content, post stories, write comments, follow, recommend).

### Story Import & Verification

`StoryImports` table. Two-way link verification: system generates unique code, author places on original platform, mod checks. MVP: manual verification. Unverified stories show "Unverified" banner and are excluded from homepage/spotlight.

### Content Download/Export

Download button for .epub, .mobi, .pdf of entire story. Pure application-layer feature using C# libraries (EpubSharp, ITextSharp). No schema impact.

### Feature Suggestions

Dedicated "Site Development" Group. Suggestions as BlogPosts, discussed via Comments. `FeatureContributions` table tracks admin attributions for accepted suggestions.

### Monetization

Website runs ads for guests only (via Blazor `<AuthorizeView>`). No ads for logged-in users. Donations accepted (Ko-fi discussed). Weekly pledge drive model with site banner showing progress toward operating costs. Single-Member LLC for personal liability protection. Separate business bank account.

---

## 6. Design Rationale & Rejected Alternatives

### Database Engine: PostgreSQL over SQL Server

**Decision:** PostgreSQL. **Alternatives:** SQL Server Express, SQL Server on Linux. **Reason:** Free built-in read replicas (streaming replication) are decisive. SQL Server locks this behind Enterprise Edition. PostgreSQL also offers native MVCC, JSONB, PostGIS, tsvector/GIN FTS, and zero licensing cost. The storage micro-optimizations lost (bit packing, TINYINT, datetime2(n)) are negligible.

### Inheritance: TPT over TPH

**Decision:** Table-per-Type for all three hierarchies. **Alternatives:** TPH. **Reason:** TPT provides NOT NULL guarantees on child FKs. TPH requires nullable, breaking data integrity. TPT child tables are natural vertical partitions. The join on primary keys is negligible. Extensive analysis showed TPT is actually faster for the primary query pattern because child table indexes are smaller than scanning a bloated TPH table.

### UserStoryInteraction: Booleans over Enums

**Decision:** 8 separate booleans. **Alternatives:** TINYINT enums (ReadStatus, FavoriteStatus) + booleans. **Reason:** In PostgreSQL, row size identical (both 16 bytes). Booleans enable cleaner 1-to-1 filtered indexes. Each boolean maps directly to one user-facing list.

### Vertical Partitioning of UserStoryInteraction

**Decision:** Three tables sharing (UserId, StoryId) PK/FK. **Alternatives:** Single wide table. **Reason:** Hot table (state booleans) is only 16 bytes/row. More rows fit per 8KB page. Dates rarely needed during primary filtering. RecommendationSource is >99% NULL. ~4.3x reduction in I/O.

### Sprites: wwwroot over R2 for Developer-Controlled Assets

**Decision:** `wwwroot` for theme sprites. **Alternatives:** Cloudflare R2, CDN-stored URLs in database. **Reason:** Git is the source of truth. Atomic deployment. Cached by Cloudflare CDN identically to R2. No `StorageSeeder` needed. Only switch to R2 if `wwwroot` exceeds ~100MB.

### Comment Likes: No DateLiked

**Decision:** Pure junction with no timestamp. **Alternatives:** Include DateLiked for trending/activity. **Reason:** All features enabled by DateLiked (trending comments, grouped like notifications, activity feeds) are features the site deliberately does not want. Anti-addictive design.

### SPA over Islands of Interactivity

**Decision:** Global InteractiveAuto. **Alternatives:** Static SSR with InteractiveWasm islands. **Reason:** Fanfiction readers click "Next Chapter" dozens of times. Full page reloads waste bandwidth and feel clunky. SPA gives instant transitions.

### Separate Database for Metadata vs Text

**Rejected.** Use separate tables (vertical partitioning) in the same database. Maintains FK integrity, enables transactions, simpler management. PostgreSQL TOAST handles large strings out-of-row automatically.

### Graph Database (Neo4j) for Discovery

**Rejected for now.** Recursive CTE in PostgreSQL is sufficient and avoids adding a new technology. Graph database could be considered at massive scale.

### Lazy Loading (EF Core)

**Rejected.** Hides N+1 query problem. Explicit `.Include()` or `.Select()` projections preferred.

### `Include()` for Read Queries

**Rejected.** Fetches entire entities (over-fetching). `.Select()` projection to DTOs fetches only needed columns, bypasses change tracker.

### Dense User Settings (Pre-populate on Registration)

**Rejected.** Creating 30+ rows on registration is terrible write performance. Sparse override model writes zero rows.

### Separate UserProfile Table for All User Data

**Rejected.** Every page load needs theme/sprite/SFW preferences. JOIN overhead on every request not worth schema purity.

### Incremental Updates for "Also Favorited" Cache

**Rejected.** Procedural row-by-row updates are fragile and error-prone. Full rebuild is self-healing and guaranteed accurate.

---

## 7. Naming Conventions & Deliberated Names

### Database Naming

- **PostgreSQL tables:** snake_case, plural (e.g., `user_story_interactions`). Auto-converted by EFCore.NamingConventions.
- **PostgreSQL columns:** snake_case (e.g., `is_hidden_favorite`). Auto-converted.
- **Index names:** `ix_{table}_{columns}` with `HasDatabaseName()` for explicit control.
- **Constraint names:** Auto-generated by EF Core except where explicitly overridden.

### C# Naming

- **Model classes:** Singular PascalCase (e.g., `UserStoryInteraction`, `ChapterComment`).
- **DbSet properties:** Plural (e.g., `UserStoryInteractions`, `ChapterComments`).
- **Enum suffix:** `...Enum` to avoid collision with lookup table model classes (e.g., `StoryStatusEnum` vs `StoryStatus` model class).
- **Enum indexing:** 0-indexed (C# default). Old SQL 1-indexed values are not the source of truth.
- **Enum underlying type:** `: byte` converted to `smallint` in PostgreSQL.
- **String key constants:** `public const string` fields in `SiteConstants.cs` (e.g., `SiteBadges.FirstStory = "first-story"`).
- **Navigation properties:** Descriptive of the relationship (e.g., `BaseComment.LikedByUsers`, `Chapter.ChapterContents`, `Story.AU` (singular for one-to-one)).

### Deliberated Table Names

| Final Name | Alternatives Considered | Rationale |
|---|---|---|
| `UserStoryInteraction` | UserStoryEngagement, UserStoryMap, UserStoryStatus, UserStoryState | "Interaction" best describes data (favorites, follows, ignores). "Map" describes table not row. |
| `ChapterContent` | ChapterVersion | 99% have one version; "Contents" more intuitive |
| `StoryCharacter` | OC, StoryCharacterTag | Unified table for canon + OC characters |
| `SettingDetails` | AU, UniverseDetails, StoryUniverse | "Setting" preferred for Pokémon's multi-canon context |
| `FollowedUser` | FavoriteAuthor, FollowedAuthor, TrackedAuthor | "Authors" too narrow; site encourages non-author contributions |
| `CustomList` | UserList | Renamed since system lists moved to UserStoryInteraction |
| `RecommendationSuccess` | RecommendationConfirmation, RecommendationConversion | "Success" captures that the recommendation converted a reader. User-facing: "Successful Recs" |
| `SavedTagSelection` | TagGroup, FilterPreset, TagSet, TagBookmark | "TagGroup" ambiguous with Group entity. "Filter" implies exclusion. |
| `StoryListing` | StoryProjection, StoryDisplay, StoryCard | "Listing" clearly communicates "data for a search result" |

### Deliberated Column Names

| Final Name | Table | Alternatives | Rationale |
|---|---|---|---|
| `SourceStoryId` / `TargetStoryId` | StoryRelationships | ParentStoryId/ChildStoryId | Source/Target describes function without confusing temporal relationships |
| `SpriteIdentifier` | Tag | SpriteURL, SpriteUrl | Clearly indicates key for URL building, not a URL |
| `CoverArtRelativeUrl` | StoryListing | CoverArtURL, CoverArtPath | Indicates relative path appended to CDN base |
| `IsFollowed` / `FollowedDate` | UserStoryInteraction | IsTracked/TrackedDate | Global rename from "Track" to "Follow" for user familiarity |
| `SearchModeKey`, `BadgeKey` | Lookup tables | SearchModeID, BadgeID | `...Key` signals NVARCHAR logical string key; `...ID` implies INT |

### User-Facing Feature Names

| Internal Name | User-Facing Term | Rationale |
|---|---|---|
| `IsVouched` on FollowedUser | **Vouches** | Personal, binding assurance. Scarcity meaningful. |
| `IsHiddenFavorite` | **Hidden Favorite** | "Hidden" describes visibility accurately |
| `RecommendationSuccesses` | **Successful Recs** | "Recs" parallels "Fics" in fanfic culture |
| `IsFollowed` (stories) | **Follow** | Universal modern term |
| `ReceiveAlerts` | Bell icon toggle | Like Twitter/YouTube |
| "Primary"/"Supporting" | Tag weight display | Not the column name "Priority" |

### Notification Category Names

| Category | Rejected Alternatives | Rationale |
|---|---|---|
| **Your Follows** | Subscriptions | Monetary connotation |
| **Your Profile** | Interactions, Social, Your Activity | Ambiguous or vague |
| **Moderation & Safety** | Account & Moderation | One unified category trains users that all admin messages are important |

---

## 8. Open Questions

### Genuinely Unresolved

1. **Blog post comments:** Blog posts were added to the TPT comment hierarchy (`BlogPostComment`) in Part 3+, but whether all blog post types support comments equally was not fully specified.

2. **Chapter Arcs implementation:** `StoryArcs` table is defined but the UI for managing arcs (creating, editing, assigning chapters) was not designed.

3. **Monetization tables:** `OperatingCosts`, `Pledges`, `SpotlightCredits` mentioned as required but SQL not generated. The weekly pledge drive model needs schema work.

4. **Email service provider:** SendGrid and Mailgun both mentioned but never chosen. Transactional email (password reset, verification, notification digests) is needed.

5. **PostgreSQL zero-downtime cache refresh:** SQL Server SYNONYMs were the original design. PostgreSQL equivalent (atomic `ALTER TABLE ... RENAME` within transaction or view swap) needs confirmation and testing.

6. **Full-text search on cold tables:** FTS on `StoryDetail.LongDescription`, `ChapterContent.ChapterText`, and `UserProfile.ProfileText` was recommended but not implemented. Left as "future indexes."

7. **Hidden Gem Recommendations entry limit:** Confirmed as 5 per user in later conversations, but the specific enforcement mechanism (C# service layer, which was stated) may need edge-case handling (what happens when a user tries to mark a 6th?).

8. **Password complexity requirements:** Noted as configurable in Identity options but specific policy not decided.

9. **PostgreSQL Managed vs Self-Managed:** DigitalOcean Managed (~$15–60/month) recommended for reliability. Final choice depends on budget at launch.

10. **Polls feature detail:** Schema outlined (`BasePoll`/`SitePoll`/`BlogPostPoll`/`PollOption`) but detailed UI behavior, voting mechanics, and result display were not specified.

11. **Custom lists feature detail:** Schema exists (`CustomList`, `CustomListEntry`) but detailed feature specification (UI, limits, sharing beyond copy-on-write) was not discussed.

12. **Recursive tag/folder hierarchies:** Adjacency list model. "Get all descendants" requires recursive CTEs. If slow, could switch to materialized path or nested sets.

13. **Multi-author chapters:** Mentioned early as a desired feature. `ChapterContent.AuthorId` supports co-author versioning, and `StoryCoAuthors` table was mentioned. Detailed workflow not specified.

14. **MAUI Blazor Hybrid:** Discussed as future possibility. Template chosen (`.NET MAUI Blazor Hybrid App`) but no implementation decisions made.

---

## 9. Implementation Roadmap

### Phase 1: Foundation (Schema & Models)

1. **Model Cleanup:** Refine all scaffolded C# model classes. Add `[Required]`, `[MaxLength]`. Replace magic bytes with enums. Use `string?` for nullable, `string` + `[Required]` for non-nullable.
2. **SQL Reference Script:** Maintain a PostgreSQL reference script for understanding. Not used for deployment — EF Core migrations handle that.
3. **New Models:** Create all model classes not in original scaffold: UserSettings (JSON), UserStoryInteractionDate, UserStoryRecommendationSource, AlsoFavoritedScore, AlsoRecommendedScore, NotificationCategory, all TPT child classes, poll classes, SavedTagSelection, StoryArc, etc.
4. **DbContext Cleanup:** Remove scaffolding noise from `OnModelCreating`. Organize into logical blocks: composite keys and unique constraints, TPT inheritance, delete policies, DateTime/DateOnly configuration, enum conversions. All `Entity<>` blocks sorted alphabetically.
5. **Fluent API:** Complete all configuration — every relationship, delete behavior, filtered index.
6. **Migration:** Run `dotnet ef migrations add InitialSchema` and `dotnet ef database update`. Fix multiple-cascade-path errors.

### Phase 2: Core Services

7. **`DeleteUserAsync` service:** Handle the three RESTRICT conflicts before deleting a user.
8. **Write-behind infrastructure:** Redis queue, "fast and dumb" API endpoints, background worker with batch consolidation.
9. **Core CRUD services:** Story management (with vertical partition awareness), chapter management, comment services, recommendation services.
10. **Identity & Auth:** Login, Register, role-based authorization, cookie configuration, admin seeding.

### Phase 3: Advanced Features

11. **Tree Search:** Recursive CTE implementation, data mart worker, manual and automatic UI.
12. **Also Favorited / Also Recommended:** Background workers, cache tables, story page integration.
13. **Sprite/Theme system:** wwwroot structure, `SpriteService` implementations, `ThemeStateService`, tag selection UI.
14. **Notification system:** Real-time delivery, settings page, category-based grouping, cleanup worker.
15. **Reading progress tracking:** Client-side scroll tracker, Redis batching, chapter interaction UI.

### Phase 4: Polish & Launch

16. **Full-text search** implementation (tsvector + GIN on StoryListing).
17. **Moderation tools** (reports, warnings, content review, approval queue).
18. **View count system** (Redis INCR + background worker).
19. **Badge system** (event-driven background worker).
20. **Custom lists** feature.
21. **Polls** feature.
22. **Story import** and verification.
23. **Performance testing** with read replicas.
24. **Domain setup** (Cloudflare Registrar, DNS, SSL, R2 bucket).
25. **DigitalOcean deployment** (Droplet + Managed PostgreSQL).

### Post-Launch

- Elasticsearch/OpenSearch for advanced search (when PostgreSQL FTS becomes a bottleneck).
- MAUI Blazor Hybrid mobile app.
- Content download/export (.epub, .mobi, .pdf).
- Beta reader in-line annotations (deferred to v2.0+).
- Semantic search / RAG (exploratory).
