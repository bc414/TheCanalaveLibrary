# The Canalave Library — Unified Project Specification (v4)

> **Synthesized from:** Four source documents spanning September 2025 through June 2026.
> - **Web Chat spec** (Sep–Nov 2025): Design conversations covering schema, features, and philosophy.
> - **Code Assist spec** (Nov 2025): Implementation conversations from JetBrains Rider covering framework constraints, patterns, and practical architecture.
> - **Step 2 Insights Report** (June 2026): Architectural refinement covering the 8-layer model, service inheritance, DTO strategy, and implementation roadmap.
> - **Step 2 Deliberations** (June 2026): Extended deliberation sessions covering UI layer analysis, component taxonomy, search architecture, reading status booleans, page inventory, Tailwind decision, and feature-level component specifications.
> - **Reading Status Design** (June 2026): Detailed reasoning document for UserStoryInteraction reading status booleans, consolidating Gemini session design work.
>
> Where sources conflict, resolutions are documented with rationale. Superseded decisions are preserved as "Previously considered" notes. Framework-level facts from implementation experience are carried regardless of source.
>
> **This document is the authoritative specification.** A reader should not need to consult any source document separately.

---

## 1. Project Mission & Philosophy

### What This Site Is

The Canalave Library is a Pokémon-fandom fanfiction website. The name references the library in Canalave City from the Pokémon games. It is designed from the ground up to promote deep thought, deep engagement, and critical thinking among readers and writers.

### Site Mascot: Torterra

Torterra carries an entire ecosystem on its back — the community resting on a living foundation. Stories rest on a living community of readers who carry them. The tree growing from Torterra's back is the center of the ecosystem: interconnected through roots and branches.

Torterra is the site mascot, not just a tree search sprite. Its presence on tree search indicates that tree search is one of the most important and differentiating features of the site. "Tree search" keeps its name — Torterra imagery makes it feel welcoming rather than technical.

### Core Design Principles

**Discoverability over virality.** The site's primary goal is to help readers find high-quality stories and help writers — especially underappreciated ones — gain readers. Features like Tree Search, Recommendations, Hidden Gems, and Vouches are all oriented toward discovery rather than popularity contests. This is a direct response to the "popularity snowball" effect on existing fanfiction platforms where early popular stories dominate and newer/niche works never surface.

**High-effort actions over low-effort ones.** The site deliberately avoids the addictive social-media patterns that plague platforms like FanFiction.Net. Notifications are reserved for meaningful interactions (new comments, new follows, new recommendations), not trivial ones (likes). Story "likes" have been removed entirely. Comment likes exist but generate no notifications and carry no `DateLiked` timestamp, intentionally preventing trending/activity-feed mechanics.

**Transparency in moderation.** Users who submit reports receive closure notifications for all outcomes, including "Resolved — No Action Taken." Every report is acknowledged, and users know their voice was heard.

**User empowerment through multiple contribution types.** Users can contribute as authors, readers, recommenders, beta readers, co-authors, group organizers, and more. The term "Followed Users" (not "Followed Authors") reflects this — not everyone is an author.

**App-like experience for returning users.** Because the target audience is returning community members (not search-engine-driven one-time visitors), the site prioritizes smooth, instant in-app navigation and rich interactivity over minimal first-paint times.

**Performance-driven database design.** The schema is designed "query-first," meaning indexes and table structures are chosen to optimize for known access patterns rather than theoretical normalization purity. Vertical partitioning, denormalization, and filtered indexes are used aggressively where they improve performance on real query patterns.

**Community-sustained, not ad-supported.** The site runs no advertisements. Revenue comes entirely from voluntary community donations tied to the Community Spotlight feature. This reflects the principle that the platform serves its community, not advertisers.

> **Previously considered:** Ads for guest users only (via Blazor `<AuthorizeView>`), with no ads for logged-in users. Rejected in favor of a fully ad-free experience. A weekly pledge drive model with site banner showing operating cost progress was also considered but replaced by the simpler donations-tied-to-Spotlight model.

### Design Principles (Priority Order)

1. **Correctness and data integrity** — Lookup tables with foreign keys over CHECK constraints; three-tiered validation; DTOs at every boundary.
2. **Maintainability** — Vertical slice file organization; interface-driven services; strict project dependency rules.
3. **Developer experience** — Rich domain models in C#; enum-backed lookup keys; strong IntelliSense and compile-time safety.
4. **User experience** — Adaptive device layouts; instant navigation for returning users; form validation at every tier.
5. **Performance** — Read-only DbContext pattern; filtered indexes; `AsNoTracking`; lean DTOs for read paths; background worker patterns for expensive computations.

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
| **ORM** | Entity Framework Core (Code-First) | Industry standard for .NET. Code-first with Fluent API for all non-trivial configuration. |
| **CSS Framework** | Tailwind CSS | Utility-first CSS with zero visual opinions. No component library (MudBlazor, Blazorise, etc.). See §2.1 for rationale. |
| **Local Dev Orchestration** | .NET Aspire | Manages Docker containers (PostgreSQL, Redis, MinIO) and connection strings during development. Does NOT go to production. |

> **Previously considered render strategy:** Component-level `@rendermode InteractiveWasm` with Static SSR as default (islands of interactivity). Rejected because each standard `<a>` link triggered a full page reload, destroying client-side state and feeling clunky for heavy readers. Global `InteractiveAuto` creates a true SPA with instant client-side routing.

> **Development shortcut:** Start with `InteractiveServer` on `<RouteView>` in `Routes.razor` during active development (faster debugging, no need for API controllers yet). Switch to `InteractiveAuto` when ready to ship WASM.

### 2.1 CSS Methodology: Tailwind

**Decision:** Tailwind CSS. No component library.

**Rationale:** The site needs a unique visual identity reflecting a Pokémon-fandom community with a non-corporate, non-predatory feel. Component libraries impose Material Design or similar visual gravity. Tailwind provides utility classes with zero visual opinions. The downside of more manual work is mitigated by: establishing patterns upfront via skill files, Claude Code implementation, and Razor components being inherently reusable.

**Consequences:**
- `tailwind.config.js` design token layer (colors, type scale, spacing, radii, shadows) must be locked before any Style implementation begins. This is the first act of code generation, not a spec task.
- Blazored TextEditor (Quill.js) ships its own CSS. Test its interaction with Tailwind's Preflight resets early — the editor is a dependency for many features.
- Accessibility for complex components (dialogs, dropdowns) is the developer's responsibility. But the two most complex interaction components (Blazored.Typeahead, Blazored TextEditor) already handle ARIA/keyboard behavior.

**Design intent (prose direction for the config session):** Non-corporate, warm community feel. Pokémon-fandom identity. Engaging visual design with cover art (Fimfiction reference, not AO3 plainness). Not predatory, not generic. Themes support different visual flavors without DB changes.

> **Previously considered:** MudBlazor or another component library. Rejected because the visual gravity toward Material Design would make the site feel generic and corporate, undermining the community identity goal.

### Database

| Choice | Detail | Rationale |
|---|---|---|
| **Engine** | **PostgreSQL** | Free read replicas via built-in streaming replication (decisive — SQL Server locks this behind Enterprise Edition). Native MVCC for better concurrency. JSONB support. tsvector/GIN for full-text search. Zero licensing cost. |
| **Naming Convention** | snake_case via `UseSnakeCaseNamingConvention()` | PostgreSQL folds unquoted identifiers to lowercase. The plugin auto-converts C# PascalCase to snake_case. ASP.NET Identity tables retain their PascalCase names (`AspNetUsers`, etc.) — the convention method does not override explicit configurations from `IdentityDbContext`. |
| **Timestamp type** | `timestamp(2) with time zone` | Explicitly chosen over `without time zone` for correct UTC storage. PostgreSQL uses fixed 8-byte storage regardless of precision, so the `(2)` limits decimal places without affecting storage. Global precision configuration loops are unnecessary and have been removed. |
| **Connection string key** | `"DefaultConnection"` | Inside the `"ConnectionStrings"` section of configuration. |

> **Previously considered:** SQL Server (Express for dev). Offered bit-packing (8 BOOLs in 1 byte), TINYINT (1 byte), and variable-precision DATETIME2. Abandoned because read replicas require Enterprise Edition and the storage micro-optimizations don't justify the licensing cost at any scale.

**Key PostgreSQL trade-offs accepted:**
- No bit packing: each `boolean` is 1 byte (vs SQL Server's 8 `BIT`s in 1 byte).
- No TINYINT: C# `byte` maps to `smallint` (2 bytes instead of 1).
- Fixed timestamp size: `timestamp with time zone` is always 8 bytes.
- These costs are negligible compared to free read replicas and MVCC advantages.

### Cache

| Choice | Detail | Rationale |
|---|---|---|
| **Engine** | Redis | Write-behind queue for high-frequency UserStoryInteraction updates. View count `INCR` buffering. LastReadDate ephemeral storage. Distributed cache for expensive query results. |

**Critical architectural constraint:** Clients (Blazor WASM) NEVER connect to Redis directly. The server acts as a trusted intermediary. For server-rendered components, inject `IDistributedCache`. For WASM components, call a server API endpoint that handles caching internally.

> **Anti-pattern rejected:** Creating an HTTP-based `IDistributedCache` implementation for the WASM client. Rejected due to extreme performance overhead and security complexity.

### Authentication & Authorization

| Choice | Detail |
|---|---|
| **Framework** | ASP.NET Core Identity with Roles |
| **User ID Type** | `int` (not GUID) — smaller composite keys, better index performance on high-traffic junction tables |
| **User Class** | `User` (extends `IdentityUser<int>`) — deliberately renamed from Identity scaffold default `ApplicationUser` |
| **Role Class** | `ApplicationRole` (extends `IdentityRole<int>`) |
| **Configuration** | `AddIdentityCore<User>()` with `.AddRoles<ApplicationRole>()`. Cookie auth via `AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()`. |
| **Cookie Config** | Returns 401/403 status codes instead of 302 redirects (critical for Blazor WASM API calls) |
| **Email Confirmation** | `RequireConfirmedAccount = true` |
| **Email (dev)** | `IdentityNoOpEmailSender` — writes confirmation URLs to console log |
| **Email (prod)** | SendGrid via a custom `EmailSender` class, conditionally registered only in production. API key stored in user secrets or environment variables. |
| **Data Protection (dev)** | `PersistKeysToFileSystem` (local directory) |
| **Data Protection (prod)** | `PersistKeysToDbContext<ApplicationDbContext>` (requires `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` and the context implementing `IDataProtectionKeyContext`) |

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
| **Visual Studio 2026 Insiders Community Edition** | Primary C# IDE for running the project and manual coding. |
| **Visual Studio Code** | For running Claude Code with a chat-like interface via the VS Code extension. |
| **Claude Code** | AI coding assistant via CLI and VS Code extension. |
| **Claude Web Chat** | For iterating on design decisions in text before moving to implementation because the web chat has better reasoning block visibility. |
| **Git + GitHub** | Version control. |
| **pgAdmin** | Alternative PostgreSQL GUI. |
| **Rclone** | CLI tool for syncing sprites to R2 in CI/CD pipelines. |
| **EF CLI** | `dotnet-ef` (globally installed). Version **must** match the EF Core library version. |

> **Previously considered:** Gemini Code Assist plugin and Jules (Google async agent) were previously used but replaced by Claude Code.

### NuGet Packages

| Package | Project | Purpose |
|---|---|---|
| `Npgsql.EntityFrameworkCore.PostgreSQL` | Server | EF Core PostgreSQL provider |
| `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` | Server | Aspire integration for PostgreSQL |
| `Aspire.Hosting.PostgreSQL` | AppHost | Aspire infrastructure definition |
| `EFCore.NamingConventions` | Server | Auto snake_case mapping for PostgreSQL |
| `NpgsqlTypes` | Server | NpgsqlTsVector type for full-text search |
| `AWSSDK.S3` | Server | S3-compatible client for Cloudflare R2 and MinIO |
| `AWSSDK.Extensions.NETCore.Setup` | Server | DI integration for AWS SDK |
| `Aspire.Hosting.Redis` | AppHost | Redis container in Aspire |
| `HtmlSanitizer` | Server | Server-side HTML sanitization for user-submitted rich text |
| `Blazored.Typeahead` | SharedUI | Typeahead/autocomplete for tag selection UI |
| `Blazored TextEditor (Quill.js)` | SharedUI | WYSIWYG editor for chapter text, descriptions, notes |
| `Microsoft.AspNetCore.Components.Web` | SharedUI | Blazor APIs including `RenderMode` class (NOT the framework reference) |
| `Microsoft.Extensions.Identity.Stores` | Core | Lightweight Identity types (avoids WASM compilation conflicts from full Identity package) |
| `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` | Server | Production key persistence |

**Version alignment warning:** The Npgsql package version must match the .NET framework version. During development, a persistent `Include` method compilation error was traced to a version mismatch between the Npgsql package and the target framework. Any future package update must verify version compatibility.

### Future/Phase 2+ Technologies

| Technology | Purpose | Timing |
|---|---|---|
| **Elasticsearch or OpenSearch** (managed) | Denormalized search replica for advanced discovery, faceted filtering | When PostgreSQL FTS becomes a bottleneck |
| **.NET MAUI Blazor Hybrid** | Native iOS/Android app reusing SharedUI Razor components | Post-launch; architecture supports it via `IDeviceDetectionService` |
| **Semantic search / RAG** | Intelligent story search based on meaning (embeddings, vector databases) | Exploratory only |
| **HybridCache** (.NET 10) | Replacement for `IDistributedCache`, unifying in-memory L1 and distributed L2 with stampede protection. Integrates with `[PersistentState]` circuit persistence. | Plan to adopt in Layer 7 caching work. Previously: spec referenced only `IDistributedCache`. Revised to plan HybridCache adoption because .NET 10 introduced it as a superior unified caching primitive with built-in stampede protection. |

---
## 3. Architecture

### 3.2 Project Dependency Rules (Strict and Inviolable)

| Project | May Depend On | Must NOT Depend On |
|---|---|---|
| **Core** | No project dependencies. May reference `Microsoft.Extensions.Identity.Stores` for Identity model types. | Server, Client, SharedUI, AppHost. Must never reference `Microsoft.EntityFrameworkCore` as a package (model classes use EF attributes only from `System.ComponentModel.DataAnnotations`). |
| **SharedUI** | Core | Server, Client. Must never reference any server-only package. Uses `Microsoft.AspNetCore.Components.Web` package reference (NOT `Microsoft.AspNetCore.App` framework reference). |
| **Client** | Core, SharedUI | Server. |
| **Server** | Core, SharedUI | Client (references Client project assembly only for Blazor endpoint mapping, not for consuming its types). |
| **AppHost** | Server, Client (for orchestration) | Core, SharedUI (no direct code dependency). |

**Critical:** SharedUI has **zero server-only dependencies**. Any future component that requires `UserManager`, `HttpContext`, `DbContext`, or similar server-only types **must go in the Server project**, not SharedUI.

### 3.3 Eight-Layer Architecture

Project progress is tracked in an SLF grid (Stage-Layer-Feature grid). The grid has eight layers (columns) and many features (rows). Each cell holds a Stage value which describes what development stage it is at. Some cells for some features can have N/A (this layer is not applicable for this feature) and that is valid. Layers 1–4 are the MVP on Blazor Server (`InteractiveServer` globally, the spec-sanctioned dev shortcut). Layers 5–8 are additive optimizations that change implementations behind stable interfaces but never alter contracts established in 1–4.

| Layer | Name | Scope |
|---|---|---|
| 1 | Data Model | EF Core POCOs, Fluent API, migrations |
| 2 | Server Implementation | IXReadService/IXWriteService + ServerXReadService/ServerXWriteService, DTOs, EF Core query patterns |
| 3 | Interaction Logic & State | @code blocks: event handlers, state, [PersistentState], EditForm, debounce, optimistic updates. Parameters, services injected, EventCallbacks, lifecycle handlers, `RendererInfo.IsInteractive` branching. |
| 3.5 | Structure (Composition & Skeleton) | Markup skeleton: which child Razor components are composed, what HTML elements exist, `@if`/`@foreach` conditions, `@ChildContent` slots, data flow through `[Parameter]`, `<AuthorizeView>` placement. |
| 4 | Style (Visual & Layout) | Tailwind utility classes, sprite resolution, responsive variants, images, conditional class expressions. |
| — | **MVP boundary** | Everything past this line is a body-swap behind a stable interface |
| 5 | WASM Enablement | API endpoints, ClientXService impls, PersistentAuthenticationStateProvider |
| 6 | SQL Indexes | Filtered, composite, golden, GIN indexes — pure DDL, zero code changes |
| 7 | Redis Integration | Write-behind buffer, ephemeral store, read-side cache, associated IHostedService workers |
| 8 | Data Mart Workers | Non-EF-Core background workers: raw SQL table creation, zero-downtime swap, recursive CTEs |

> **Previously: Layer 3 was "Interaction Logic & State" and Layer 4 was "Presentation," defined as two layers.** Revised to three dimensions (Logic → Structure → Style) forming a strict dependency chain because analysis revealed each original layer contained two orthogonal concerns. Layer 3 (Logic) is decidable from the spec and data model alone — no visual design dependency. Layer 3.5 (Structure) is decidable once the component system is known — before visual design. Layer 4 (Style) is decidable only after design tokens are locked (`tailwind.config.js`). This split affects the SLF grid columns: `L1 | L2 | L3 | L3.5 | L4 | L5 | L6 | L7 | L8`.

**The vertical-line test (MVP boundary after Layer 4):** Can this feature's Layer 1–4 contract — DTO shapes, service method signatures, component props — be fully defined now, with *some* correct implementation behind it, such that Layers 5–8 only ever change what's *behind* the contract?

Layers 5–8 are naturally batchable: indexes are pure DDL applied across many tables; WASM applies the same endpoint + HttpClient wrapper to N stable interfaces; Redis swaps method bodies behind stable signatures; data mart workers are standalone classes with no interface callers.

**The horizontal-line test (features requiring real user data):** Can a human tell whether the feature is working well using only seed data? Features that fail this test are deferred past a beta period:
- **Automatic Tree Search** — recursive CTE against seed data produces degenerate graphs
- **Also Favorited / Also Recommended** — co-occurrence scoring needs real patterns
- **Tree Search Data Mart Worker** — serves Automatic Tree Search
- **SiteDailyStat Worker** — nothing to aggregate yet

Features that *produce the signal* these consume (Favorites, Recommendations, Manual Tree Search, Following, Vouches) are all above the line and ship in the MVP.

**Worker disambiguation across layers:**

| Worker | Layer | Why |
|---|---|---|
| Write-behind drain (UserStoryInteraction) | 7 | Redis list → EF-modeled table |
| View count drain | 7 | Redis INCR → EF-modeled `Story.ViewCount` |
| Notification cleanup | 2 | Operates on EF-modeled `Notification` table |
| UserStat recalculation | 2 | Corrects drift in EF-modeled `UserStats` |
| Badge awarding (MVP) | 2 | Synchronous inline check in service methods |
| TreeSearch data mart rebuild | 8 | Non-EF table, raw SQL, table swap |
| AlsoFavorited/AlsoRecommended rebuild | 8 | Non-EF tables, raw SQL, table swap |
| SiteDailyStat aggregation | 8 | Aggregation into table with no interactive service surface |

### 3.4 Render Strategy: Global InteractiveAuto (SPA)

**Production mode:** Global `InteractiveAuto` set on `<RouteView>` in `Routes.razor`:
```razor
<RouteView RouteData="routeData" DefaultLayout="typeof(DeviceLayout)" 
           @rendermode="RenderMode.InteractiveAuto" />
```

**Current development mode:** `RenderMode.InteractiveServer` (the dev shortcut). Avoids building API endpoints and client-side services until Layer 5.

**Correct directive syntax:** `@rendermode RenderMode.InteractiveServer` or `@rendermode RenderMode.InteractiveWebAssembly` (requires static instance from the `RenderMode` class — bare tokens like `InteractiveServer` are incorrect).

**Render mode precedence:** A render mode set directly on a component overrides the global default from the Router. This allows mixed-mode pages.

**How InteractiveAuto works:**
1. **First request:** Server-side prerendering (SSR) for fast initial paint and SEO.
2. **Background:** WASM payload downloads and caches in the browser.
3. **Subsequent navigation:** Client-side routing via WASM (no page reloads). Blazor Router intercepts clicks, swaps `@Body` component, preserves layout state.

**Assembly discovery:** Both the Router (`Routes.razor`) and `app.MapRazorComponents` must be told about external assemblies via `AdditionalAssemblies`. Marker components (`SharedUIAssemblyIdentifier.razor`, `WasmClientAssemblyIdentifier.razor`) exist in each library to provide stable type references.

**ReconnectModal:** Must be conditionally rendered only when an interactive render mode is active, checked via `HttpContext` endpoint metadata in `App.razor`. Unconditional rendering causes a JavaScript initialization error on static pages.

**`PersistentComponentState`** prevents the "double fetch" problem (server prerender + client hydration both calling `OnInitializedAsync`). This is a Layer 3 concern — applied from the start even under InteractiveServer, so components don't need restructuring when WASM arrives.

#### Identity and Settings Routes

Identity pages are already scaffolded by ASP.NET Identity under `/account/*`. They use form-POST-to-endpoint and live in the Server project permanently. Work needed: styling only.

Custom user settings (reader preferences, privacy, notifications) live at `/settings` and follow the standard Blazor component pattern. These are separate from Identity's `/account` routes.

Login/logout are triggers on the persistent layout, not separate navigation targets. Login links to `/account/login`. Logout is a form POST (Identity security requirement).

### 3.5 Service Architecture: CQRS-Lite with Inheritance

Every feature cluster gets two interfaces in Core:

```csharp
public interface IStoryReadService
{
    Task<StoryListingDto[]> GetListingsAsync(StoryFilterDto filter);
    Task<StoryDetailDto?> GetDetailAsync(int storyId);
}

public interface IStoryWriteService : IStoryReadService
{
    Task UpdateTitleAsync(int storyId, string newTitle);
    Task SetHiddenGemAsync(int recommendationId, bool isHiddenGem);
}
```

Razor components inject the *narrowest* applicable interface: a story viewer injects `IStoryReadService`; the story editor injects `IStoryWriteService` (inherits all reads, adds writes). This is least-privilege enforced at the type level.

> **Previously considered:** Single `IStoryService` combining reads and writes. Rejected in favor of the CQRS split for clearer intent, more focused interfaces, independent scalability, and security.

**Server implementation with compile-time DbContext safety:**

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

Read methods can't accidentally use the write context (it doesn't exist in scope). Write methods can't accidentally use the read replica (it's not stored). Misuse requires a visible, reviewable act.

> **Previously considered:** `IDbContextFactory<ApplicationDbContext>` for thread safety and controlled DbContext lifetime. Superseded by the direct-injection pattern above, which provides compile-time safety. With scoped service registration (`AddScoped<>`), the thread-safety concern that motivated the factory is addressed by the DI container's lifetime management.

**DI registration:**
```csharp
builder.Services.AddScoped<IStoryReadService, ServerStoryReadService>();
builder.Services.AddScoped<IStoryWriteService, ServerStoryWriteService>();
```

**Client side:** `ClientStoryWriteService : ClientStoryReadService` mirrors the inheritance. No DbContext safety concern (everything is HttpClient), but keeps implementations structurally parallel.

**The four cases of write-side reads:**

| Case | Example | Where | Context used |
|---|---|---|---|
| 1. Constraint check | Hidden Gem ≤5 count | Internal to write method | `writeDb` (primary, for consistency) |
| 2. Edit form loads read DTO | Story editor needs current title | Inherited `IStoryReadService.GetDetailAsync()` | `readDb` (replica, via base) |
| 3. Edit-only fields | `OriginalPublishedDate` for imports | Dedicated `GetStoryForEditAsync()` on write service | `writeDb` |
| 4. Display hint | `CommentDto.IsLikedByCurrentUser` | Not a read — flows as `[Parameter]` from parent | N/A |

**Exception — self-referential editing:** When reader and writer populations are identical by definition (a user editing their own settings), a single integrated `IUserSettingsService` with both read and write methods is acceptable. This does NOT apply to public profile display (`IUserProfileReadService`).

### 3.6 Data Flow & Object Model

The architecture defines four object types, each scoped to a layer. Data moves between them through explicit mapping — no type ever crosses into a layer it doesn't belong to.

```
[UI Layer]           [Network Boundary]        [Server Layer]           [Database]
@code / ViewModel →→ DTO (Create/Edit)    →→→  Rich Domain Model   →→→  EF Core Entity
  ↑ bind               ↑ serialize               ↑ validate              ↑ persist
  ↓                    ↓ deserialize             ↓ map                   ↓ query
@code / ViewModel ←←← DTO (Read/Summary)  ←←←  EF Core + Projection ←←← Tables
```

#### The Four Object Types

| Object | Location | Purpose | Rules |
|---|---|---|---|
| **`@code` block** | SharedUI (component) | The default UI-side model. Receives DTOs or primitives as `[Parameter]`s. Holds computed display properties, ephemeral UI state (toggles, loading flags, expanded/collapsed), and display enrichment on top of DTO data. | Never sent over the wire. Never persisted. Never used on the server. Every Razor component already has one — no extra class needed. |
| **ViewModel class** | SharedUI (separate `.cs`) | Binding target for Blazor `EditForm`. Contains `DataAnnotations` that `<DataAnnotationsValidator>` inspects. | Same boundary rules as `@code`. Only created when `EditForm` demands it (see criteria below). The exception, not the default. |
| **DTO** | Core | Data transfer across the client-server boundary. Simple property bags. Stable API contract. | No business logic. No navigation properties. No change tracker. Write and read DTOs are separate classes. No DTO inheritance. |
| **Rich Domain Model** | Core (meaningfully *used* on server) | EF Core entity with business logic methods (e.g., `story.Publish()`). Encapsulates invariants. | May have private setters. May implement `IEditableStoryProperties` via explicit interface implementation. Must never reference `DbContext` or server-only services. |

The DTO firewall separates EF Core entities from the UI — not DTOs from the UI. DTOs live in Core (shared by both Server and Client projects). They flow directly into components as parameters.

#### DTOs Flow to Component Parameters

The designed data flow for display components:

```
Service → DTO → Dispatcher [PersistentState] → Composite → Leaf [Parameter] → renders
```

Pass the DTO when a component renders multiple fields from it. Pass primitives when a component only needs one value. Do not decompose a DTO into individual primitive parameters just to avoid passing an object — that creates fragile, wide parameter lists and loses the DTO's cohesion.

#### The `@code` Block as Display Model

For display components — which are the majority — the `@code` block serves the ViewModel role. Computed display properties, ephemeral UI state, and display enrichment live here. None of it crosses the service boundary.

```razor
@code {
    [Parameter] public StoryListingDto Story { get; set; } = default!;

    private string WordCountDisplay => Story.WordCount switch {
        < 1_000     => $"{Story.WordCount} words",
        < 1_000_000 => $"{Story.WordCount / 1000.0:F0}K words",
        _           => $"{Story.WordCount / 1_000_000.0:F1}M words"
    };

    private bool _synopsisExpanded = false;
    private bool _coverArtFailed = false;
}
```

This component has a model — it's the `@code` block itself. `WordCountDisplay` is a computed view property. `_synopsisExpanded` is ephemeral UI state. `Story` is the data source flowing in as a parameter. No separate class required.

#### When a Separate ViewModel Class IS Needed

A dedicated ViewModel class (a `.cs` file in SharedUI) is warranted only in these cases:

1. **EditForm binding** — `EditForm` requires a bound model object; `DataAnnotations` need a class that `<DataAnnotationsValidator>` inspects. Applies to: story create/edit (title, short description), profile tagline, any form the user submits.
2. **Shared form shape** — two pages share the same editable fields and should share their validation model.
3. **Complex testable logic** — computed properties worth unit testing without rendering a component.
4. **Service-owned persisted state** — `RegisterPersistentService<T>` requires a class instance to persist across circuit reconnections.

Everything else — WYSIWYG editor surfaces (Quill doesn't use `InputText`), toggle interactions, selection state (`List<Tag>` in `@code`), navigation controls, settings toggles — uses the `@code` block directly. No ViewModel class.

#### Context-Specific Data Augmentation

When a parent has data that doesn't belong in the child's DTO (e.g., `FavoriteDate` alongside a `StoryCard`), the parent renders it as a sibling element rather than contaminating the DTO or the child component's parameter surface:

```razor
@* Parent renders context it owns; child renders what it knows *@
<div class="...">
    <StoryCard Story="@story" />
    <span>Favorited @FavoriteDate.ToShortDateString()</span>
</div>
```

The DTO stays clean. The child stays reusable. The parent owns the arrangement.

#### Anti-Patterns

- Using EF Core models directly as view models in Razor components.
- Having a DTO inherit from an EF model class (heavyweight, bloated DTOs).
- Instantiating rich domain models on the client for validation reuse (massive WASM payload bloat).
- Creating a ViewModel class for every component regardless of whether `EditForm` is involved.
- Decomposing a DTO into individual primitive `[Parameter]`s to avoid passing an object.
- Rendering context-specific data (dates, badges, rankings) by adding fields to a shared DTO.


### 3.7 Three-Tiered Validation Strategy

**Tier 1 — Property-level (Client + Server):** `DataAnnotations` on ViewModels (`[Required]`, `[StringLength]`). Immediate UX feedback via `EditForm` / `DataAnnotationsValidator`.

**Tier 2 — Intra-object invariants (Client + Server):** A shared interface (`IEditableStoryProperties`) implemented by both the ViewModel and the EF model, with validation rules in **static extension methods** in Core. Complex multi-property rules (e.g., "if rating is Mature, content warning required") defined once, executed in both environments. Trade-off: slightly more anemic (logic outside objects), but keeps client payload lean.

**Tier 3 — Cross-model / set-based (Server only):** Full database context checks in server-side service. On failure, throws `StoryValidationException` containing a `List<string>` of error messages. Used only for rules requiring other data (slug uniqueness, post limits).

**Exception usage:** Throwing exceptions for routine client-side validation is an anti-pattern. Exceptions are server-side only (Tier 3).

### 3.8 DTO Strategy: Partition-Anchored

Default: one DTO record per vertical-partition table. `StoryListingDto` ≈ the columns of `StoryListing`.

This sidesteps both "big kitchen-sink DTO" and "over-fit to current UI" failure modes: the partition boundary is already the answer to "what belongs together," chosen for buffer-pool/hot-path reasons that correlate with what UI naturally wants together.

For cross-partition needs (a card needing `StoryListing` fields + `IsFavorited` from `UserStoryInteraction`): separate fetches, merge in C# at the call site. A dedicated composite DTO is a deliberate exception (hot path, merge cost too high), not the default.

Additional DTO conventions from the original design:
- Primitives for simple write operations (e.g., `UpdateStoryTitleAsync(int storyId, string newTitle)`)
- DTOs for complex write operations with 3+ parameters
- ValueTuples acceptable for 2–3 property returns from read operations
- Anonymous types only for intermediate data within methods (cannot cross service boundaries)
- Out parameters avoided — don't work with async/await

### 3.9 Device Detection Architecture

Dual implementation behind a shared interface:

| Interface | Implementation | Project | Mechanism |
|---|---|---|---|
| `IDeviceDetectionService` | `ServerDeviceDetectionService` | Server | Inspects `User-Agent` header via `IHttpContextAccessor` (works during SSR). |
| `IDeviceDetectionService` | `WasmDeviceDetectionService` | Client | Calls `isMobile()` JS function via `IJSInProcessRuntime` (synchronous). |

JS file: `TheCanalaveLibrary.SharedUI/js/device.js`, loaded in `App.razor`. Uses `window.matchMedia` with 768px breakpoint. "Request Desktop Site" works correctly (reports wider viewport).

**MAUI compatibility:** JS Interop doesn't work in MAUI (no browser `window` object). A future `MauiDeviceDetectionService` would use `Microsoft.Maui.Devices.DeviceInfo`.

### 3.10 Page Organization — Dispatcher Pattern

```
Pages/
  Story/
    StoryPage.razor          ← Routable dispatcher (@page "/stories/{StoryId:int}")
    StoryDesktop.razor        ← "Dumb" presentation (receives data via parameters)
    StoryMobile.razor         ← "Dumb" presentation
```

The dispatcher (smart component) owns `@page` directives, injects services and `IDeviceDetectionService`, loads data, chooses presentation component, handles shared event logic. Presentation components (dumb) have no `@page`, receive all data via `[Parameter]`, raise events via `EventCallback`.

**Layout routing:** `DeviceLayout.razor` inherits from `LayoutComponentBase`, injects `IDeviceDetectionService`, conditionally renders `DesktopLayout.razor` or `MobileLayout.razor`. Set as `DefaultLayout` in `Routes.razor`.

**Moderator pages exception:** All pages gated to moderator or admin roles skip the dispatcher/desktop/mobile pattern. Desktop only, single layout. Applies to: Reports, Story Submissions, User Management.

### 3.11 Component Taxonomy

Every Razor component falls into one of three tiers. Logic (L3), Structure (L3.5), and Style (L4) apply to all tiers but with different weight distributions.

#### 3.11.1 Leaf Components

No child Razor components. No service injection. Parameters and EventCallbacks only.

- **Logic:** Thin — parameter declarations, simple EventCallbacks, possibly one trivial internal state field (`_isExpanded`). Computed display properties derived from DTO parameters.
- **Structure:** Full weight — the actual HTML elements, `@if`/`@foreach` driven by parameters.
- **Style:** Full weight — all visual identity lives here.

Examples: `TagChip`, `UserStoryInteractionButton`, `RichTextView`, `PaginationControls`, `UserCard`, `StoryCard`.

#### 3.11.2 Composite Components

Compose child components and/or manage coordination state. Four subtypes:

1. **Pass-through layout** (`StoryDesktop`, `StoryMobile`, `ChapterNavigation`): receive parameters, arrange children. Logic thin, Structure is the main job, Style is layout Tailwind.
2. **Coordination** (`StoryInteractionPanel`, `EditorView`): own state spanning children (debounce, mode toggles). Logic is the main job.
3. **Container** (hypothetical `Card`, `Panel`): provide visual vessel via `@ChildContent`. Style is the main job.
4. **Third-party wrapper** (`EditorView` wrapping Quill, tag selector wrapping Blazored.Typeahead): adapt an external library to the Blazor parameter/callback model.

**Composite introduction criteria:** Introduce only when: it has children (leaf cannot), it manages coordination state spanning children, it appears multiple times with identical structure, or it wraps a third-party component. If something appears in one place with no coordination logic, it belongs inline in its parent.

> **Previously considered:** "Intermediate components" as a separate category between leaves and pages. Rejected because composites have four distinct subtypes with different characteristics, and the leaf/composite/page taxonomy with subtypes is more precise.

#### 3.11.3 Page/Dispatcher Components

`@page` directives, service injection, route parameters, data loading, device detection, `[PersistentState]`, event coordination for child writes.

- **Logic:** Heavy.
- **Structure:** Thin — usually `@if (isMobile) { <Mobile /> } else { <Desktop /> }`.
- **Style:** Near zero. Loading/error state skeletons only.

### 3.12 Service Injection Rules

#### The Underlying Principle

Inject a service when the component has a genuinely independent concern that cannot or should not be coordinated from above.

#### The Rigid Constraint

Pure display components showing pre-loaded data must NEVER inject read services. This prevents N+1 query behavior when rendering lists.

#### Legitimate Non-Page Injection

- **Cross-cutting layout elements** — notification bell in header (no parent dispatcher owns this data).
- **User-input-driven queries** — tag typeahead (bubbling keystrokes to parent is absurd indirection).
- **Self-contained writes** — follow button, comment like (parent doesn't need the result).

#### The Default

"Only pages inject services" is a useful default, not a rigid rule. The default holds for the vast majority of components. The exceptions above are the named, justified departures.

### 3.13 Tailwind Component Conventions

#### Outer Margin Rule (Non-Negotiable)

Components own their internal padding but NEVER their outer margin. Parent containers control spacing between siblings via `gap`.

**Forbidden on component root elements:** `mt-`, `mb-`, `mx-`, `my-`, `m-`.
**Parents use:** `gap-`, `space-y-`, `space-x-` for child spacing.

**Rationale:** A component with `mb-6` inside a grid with `gap-6` produces doubled bottom spacing.

#### Parent-Owns-Arrangement

Dispatchers and pass-through composites control layout: `grid`, `flex`, `gap`, column spans, responsive breakpoints. Children are agnostic about their placement.

#### Responsive Prefix vs. Separate Component

**If the difference is layout** (same elements, different sizing): responsive prefixes in one component. **If the difference is structure** (different elements, hierarchy, interactions): separate components (`StoryDesktop.razor` / `StoryMobile.razor`).

#### Parameter-Based Variants, Not Class Overrides

Tailwind class conflicts from parent overriding child are unpredictable (stylesheet order, not markup order). Components expose typed parameters (`Compact`, `Highlighted`) mapping to internal utility classes. `AdditionalClass` parameter is an additive-only escape hatch (width constraint, `hidden` toggle), never for overriding internal styles.

#### Pattern Accumulation

Visual conventions (e.g., `rounded-xl` for card surfaces, `rounded-md` for inputs, `rounded-full` for chips) must be captured in skill files after each implementation session. Without this, future sessions drift.

### 3.14 API Endpoint Organization

Minimal API endpoints in feature-specific extension method classes:
```csharp
// StoryEndpoints.cs
public static class StoryEndpoints
{
    public static WebApplication MapStoryEndpoints(this WebApplication app) { ... }
}

// Program.cs
app.MapStoryEndpoints();
```

> **Previously considered:** MVC API controllers. Minimal API is lighter and the modern .NET approach.

### 3.15 Background Workers

| Worker | Schedule | Purpose |
|---|---|---|
| **Write-behind worker** | Every 5 seconds | Drains Redis interaction queue, consolidates, batch writes to PostgreSQL |
| **View count worker** | Every 5 seconds | Reads Redis INCR keys, resets to zero, batch-updates DB view counts |
| **TreeSearch data mart worker** | Daily (off-hours) | Rebuilds `UserStoryTreeSearchEntries` table with zero-downtime swap |
| **AlsoFavorited/AlsoRecommended worker** | Daily (off-hours) | Rebuilds collaborative filtering cache tables with zero-downtime swap |
| **Badge awarding** | Inline (MVP) | Synchronous check in service methods; async worker is a later optimization |
| **Notification cleanup worker** | Daily | Deletes read notifications older than 60 days |
| **UserStat recalculation** | Periodic | Pre-calculates denormalized counters; safety net for drift |
| **Daily site stats** | Daily | Aggregates into `SiteDailyStat` table |

**Write path for high-frequency interactions (target architecture):**
1. Client: Optimistic UI update on click. 2-second debounce per component.
2. API endpoint ("fast and dumb"): Validates, `LPUSH`es to Redis. Returns `202 Accepted`. No DbContext.
3. Background worker: Wakes every 5s, pulls all pending, consolidates to `Dictionary<(UserId, StoryId), LatestState>`, batch writes.

**MVP temporary state:** Write-behind features use direct EF writes through the service interface during MVP. The interface never changes; only the method body does when Redis is introduced.

**Zero-downtime cache refresh (table swap):**
1. Two physical tables: `cache_table_a` and `cache_table_b`.
2. Worker identifies the inactive ("staging") table, TRUNCATEs it, populates with fresh data.
3. Atomically swaps via `ALTER TABLE ... RENAME` within a PostgreSQL transaction.
4. Next run uses the other table as staging.

### 3.16 Caching Architecture (Multi-Layer)

1. **Redis:** View count `INCR`, write-behind queue, LastReadDate ephemeral, distributed cache (tree search, Also Favorited). Cached Repository / Decorator pattern (e.g., `CachedStoryService` wraps real service).
2. **PostgreSQL cache tables:** `UserStoryTreeSearchEntries`, `AlsoFavoritedScore`, `AlsoRecommendedScore` — rebuilt by background workers.
3. **Cloudflare CDN:** Caches `wwwroot` static assets and R2-served images at edge nodes.
4. **Browser cache:** HTTP `ResponseCache` headers for tag data and static API responses.
5. **In-memory service cache:** Tag lists cached in service instances after first fetch.
6. **PostgreSQL internal:** Buffer pool and query plan cache managed automatically.

### 3.17 Sprite/Image Delivery

**Developer-controlled sprites (tags, themes):** Stored in `wwwroot/images/themes/{theme_name}/static/` and `animated/`. Git is the source of truth — atomic deployment. Cloudflare CDN caches identically to R2.

**URL builder pattern:** `Tag.SpriteIdentifier` (e.g., "Bulbasaur", "Charmander") → client builds full path at render time. Adding a new theme requires zero database changes.

**`ISpriteService` implementations:**
- `ServerSpriteService`: Uses `IWebHostEnvironment` for `File.Exists()` with fallback to `unknown_sprite.png`.
- `WasmSpriteService`: Constructs URL strings optimistically (no disk access).

**Animated sprites:** Animated WebP (not GIF). Logic checks `User.PrefersAnimatedSprites` for path selection.

**User uploads (cover art, profile pictures):** Cloudflare R2 via AWS SDK. Key convention: `stories/[StoryID]/cover-[uuid].jpg`, `users/[UserID]/profile-[uuid].jpg`. In dev, MinIO via Aspire.

> **Previously considered:** Storing full URLs on Tag table, Cloudflare R2 for dev sprites. Rejected: CDN domain change would require updating millions of rows; `wwwroot` is simpler.

### 3.18 LastReadDate: Redis Hybrid Pattern

`LastReadDate` for "In Progress" stories is too volatile for SQL. Instead:
- Stored in Redis Hash: `user:{userId}:lastread`, field = storyId, value = timestamp.
- Updated via lightweight API endpoint (`POST /api/reading/ping/{storyId}`) that only touches Redis.
- Rendering "In Progress" page: (1) query SQL for in-progress StoryIds, (2) one Redis `HGETALL`, (3) merge and sort in C# memory.

### 3.19 Persistent Layout

#### Desktop: Top Bar

Logo/site name (links to home), navigation links (Home, Discover, Groups, Tags, Users, Messages), notification bell with unread count and flyout preview, login button (unauthenticated) or profile avatar + username with dropdown (authenticated).

#### Mobile: Hamburger Menu

Top bar: logo, notification bell, hamburger toggle. Hamburger menu expands to show full navigation and profile section.

#### Service Injection

The notification bell injects `INotificationReadService` directly — legitimate cross-cutting injection, no parent dispatcher owns this data. Authentication state via `<AuthorizeView>` or `AuthenticationStateProvider` (framework-provided).

#### Desktop and Mobile Layouts Are Structurally Different

Top bar vs. hamburger menu is different component trees, not responsive prefixes. This is the `DeviceLayout` → `DesktopLayout` / `MobileLayout` split already defined in §3.10.

### 3.20 Error Handling Strategy

Three dimensions identified but not yet fully designed:
1. **API error envelope:** `ProblemDetails`-based responses from endpoints.
2. **Global Blazor error boundary:** `<ErrorBoundary>` in the layout.
3. **Client-side HTTP error handling:** how client services translate non-2xx responses.

Technical plumbing (1–2) is independent of visual design. Should be decided before implementation. Error presentation inherits the design language and can wait for Tailwind config.

### 3.21 Security

- **HTML sanitization:** Server-side via `HtmlSanitizer` on all user HTML before saving. Allow-list approach.
- **Rich text editing:** Blazored TextEditor wrapping Quill.js.
- **HTTPS:** Enforced via middleware.
- **Anti-forgery:** Via Blazor EditForm.
- **Over-posting prevention:** Via DTOs at service boundaries.

### 3.22 EF Core Configuration

- **Fluent API organization:** `IEntityTypeConfiguration<T>` classes with `ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly())` in `OnModelCreating`. Supports vertical-slicing file organization.
- **Enum-to-short conversion:** `.HasConversion<short>()` on every magic-enum property. Hybrid enums use integer FKs to lookup tables.
- **DateTime:** `timestamp(2) with time zone` (8 bytes). `DateOnly` maps to `date` (4 bytes). Creation timestamps use `HasDefaultValueSql("CURRENT_TIMESTAMP")`.
- **CHECK constraints:** NOT auto-generated by migrations — manually added via `migrationBuilder.Sql()`.
- **Delete policies:** Explicitly configured with `.OnDelete(DeleteBehavior.X)` on every relationship.
- **TPT inheritance:** Configured with `.ToTable()` per type.
- **Filtered indexes:** Use `HasFilter("column_name = true")` with snake_case and PostgreSQL `true`/`false`. Use Npgsql `.Include()` method (NOT `.IncludeProperties()` — doesn't exist in Npgsql).
- **Read-only queries:** `ReadOnlyApplicationDbContext` with `QueryTrackingBehavior.NoTracking` globally.
- **Split queries:** `.AsSplitQuery()` for `.Include()` chains.
- **Lazy Loading:** NOT used. Hides N+1 query problem.
- **Relationship configuration:** Set navigation properties (`story.Author = user`) rather than FK IDs. Change tracker manages FK.
- **Triggers:** Must use PostgreSQL PL/pgSQL. `HasTrigger` Fluent API is SQL Server-specific.
- **Seed data:** `HasData()` using **anonymous types** (not entity instances) to prevent `PendingModelChangesWarning`. Integer literals for `short` must be explicitly cast `(short)1`. PKs must be non-zero for auto-increment tables.
- **Migration commands:** `--context ApplicationDbContext` required when two DbContext types exist.

### 3.23 Aspire Configuration

**Dual-Configuration Strategy:** The `dotnet ef` CLI runs only in the server project context and **cannot see the AppHost's configuration**. The connection string must exist in both the AppHost's user secrets (for runtime) and the server project's user secrets (for design-time EF tooling).

### 3.24 Development Workflow

Pre-launch: "Nuke and rebuild" workflow — delete Migrations folder, drop DB, regenerate. Commit migrations to source control for documentation. Post-launch: incremental migrations only, never delete Migrations folder.

### 3.25 Scaling Architecture

1. **Vertical Partitioning:** Reduces data per page read.
2. **Read Replicas:** Distributes SELECT across instances.
3. **Background Workers:** Pre-calculate expensive aggregations offline.
4. **Redis Cache:** Prevents repetitive reads from hitting DB.

**Start:** Server 1 (Web App + Redis), Server 2 (Managed PostgreSQL).
**Scale:** Servers 1–N (load balanced), dedicated Redis, Primary + Read Replicas, optional Elasticsearch/OpenSearch.

### 3.26 Service Composition

Feature services that need data from outside their own domain obtain it by injecting foundational services, not by duplicating query logic or reaching directly into another domain's tables.

**The pattern:** A composite service injects a foundational service's read interface and calls its building-block methods. The composite service owns the domain query (which entity IDs match the feature's criteria). The foundational service owns the presentation query (IDs → DTOs with correct projection logic). Neither duplicates the other.

```csharp
public class ServerInteractionReadService(
    ReadOnlyApplicationDbContext readDb,
    IStoryReadService storyReadService) : IInteractionReadService
{
    public async Task<StoryListingDto[]> GetFavoritesAsync(int userId)
    {
        // This service's domain: which stories are favorited
        var storyIds = await readDb.UserStoryInteractions
            .Where(i => i.UserId == userId && i.IsFavorite)
            .Select(i => i.StoryId)
            .ToArrayAsync();

        // Foundational service's domain: turn IDs into display-ready DTOs
        return await storyReadService.GetListingsByIdsAsync(storyIds);
    }
}
```

**Building-block methods:** Foundational services expose methods designed for consumption by other services, not just by components. `IStoryReadService.GetListingsByIdsAsync(int[] storyIds)` takes an already-filtered set of IDs and returns the projection — it doesn't know or care why those IDs were selected. These building-block methods use `.Select()` projection (per the settled preference over `.Include()`), returning partition-anchored DTOs.

**The DAG rule:** Service dependencies form a directed acyclic graph. Composite services inject foundational services, never the reverse. `ServerInteractionReadService` → `IStoryReadService` is correct. `ServerStoryReadService` → `IInteractionReadService` is a design smell — story presentation should not couple to interaction state. When a component needs data from two unrelated domains (a story card with a "favorited" indicator), it calls both services independently and merges at the call site. This is S2's "separate fetches, merge in C#" pattern, applied at the component level (Layer 3), and it coexists with service-level composition (Layer 2).

**Where each pattern applies:**

| Situation | Pattern | Layer |
|---|---|---|
| A feature's read logic naturally spans two domains (favorites list needs interaction IDs + story listings) | Service composition — the feature service injects the foundational service | Layer 2 |
| A component displays data from two unrelated domains (a story card showing listing data + interaction state) | Merge at the call site — the component calls both services and combines | Layer 3 |

**Hot-path escape hatch:** For performance-critical queries where the composition overhead (two database round-trips instead of one) measurably matters, a composite service may write a single optimized query that joins across tables directly. This is a deliberate, documented decision — the method body bypasses composition in favor of a hand-tuned JOIN that leverages specific indexes. The interface and DTO don't change; only the method body does. This is the same "body swap behind a stable interface" principle that governs Layer 5 (WASM enablement) and Layer 7 (Redis integration).

The escape hatch is the exception. If many services bypass composition, it means the foundational services don't expose the right building-block methods.

---

### 3.27 Query Evolution Strategy

The schema and the query implementations serve different timelines. The schema is designed upfront and locked down. The query implementations start simple and evolve toward optimization only when profiling warrants it.

**Layer 1 (schema) is forward-looking by design.** The extensive schema work — boolean columns on `UserStoryInteraction` chosen because they enable clean filtered indexes, `INCLUDE (story_id)` covering index designs, `(user_id, favorite_date DESC)` composite indexes on `UserStoryInteractionDate`, the `StoryListing` warm partition sized to fit the buffer pool — locks down the *shape* of the data so that efficient query paths exist as options. None of it dictates that every service method must use a single optimized JOIN from day one. It guarantees that when you need that JOIN, the schema supports it.

**Layer 2 MVP uses composition.** Services compose through injection. `GetFavoritesAsync()` issues two simple queries: one against `UserStoryInteraction` (hitting the covering index), one against `StoryListing` (hitting the primary key). Both are index-only operations, trivially correct, and don't duplicate projection logic. The code is clean and small.

**Layer 6 adds indexes and optimizes hot queries.** When profiling identifies a hot path, the optimization has two parts:

1. **Add the index (pure DDL, zero code changes).** The filtered composite index that the schema was designed to support gets added via a migration.
2. **Optionally optimize the query implementation (method body swap).** The composed two-query method is replaced with a single JOIN that leverages the new index. The interface doesn't change. The DTO doesn't change. The component doesn't change.

This means Layer 6 is more precisely described as: "add indexes (DDL) and, where profiling warrants, optimize the query implementations that use them (method body swaps behind stable interfaces)." The DDL and the query optimization are paired — the index enables the optimization — but neither changes any contract.

**The lifecycle of a query:**

| Stage | What the query looks like | When |
|---|---|---|
| MVP (Layers 1–4) | Two composed queries via service injection. Simple, correct, DRY. | Initial implementation |
| Post-profiling (Layer 6) | Index added (DDL). Method body optionally replaced with single optimized JOIN. | When measurement shows the composed query is a bottleneck |
| At scale (Layer 7) | Redis cache in front of the query, or write-behind replacing the query entirely. | When database load warrants caching |

This progression mirrors the project's other evolution paths: `InteractiveServer` → `InteractiveAuto` (Layer 5 body swap), direct EF write → Redis write-behind (Layer 7 body swap). The pattern is always the same: start with the simplest correct implementation, evolve the implementation behind a stable interface when measurement justifies it.

---
## 4. Database Schema

### 4.1 Conventions

- **Naming:** snake_case in PostgreSQL via `UseSnakeCaseNamingConvention()`. C# uses PascalCase. Identity tables stay PascalCase.
- **Primary keys:** Single-column `int` identity where possible. `long` for event tables (comments, notifications, messages, chapter contents). Composite keys for junction and interaction tables.
- **Timestamps:** `timestamp(2) with time zone` (8 bytes). `DateOnly` maps to `date` (4 bytes). Creation timestamps: `HasDefaultValueSql("CURRENT_TIMESTAMP")`.
- **Strings:** `[Required]` + non-nullable `string` for mandatory. `string?` for optional. `[MaxLength(n)]` on all bounded strings.
- **Booleans:** `NOT NULL DEFAULT false`. PostgreSQL stores each as 1 byte.
- **Enums:** See §4.2 for the decision framework.
- **URLs:** `[MaxLength(512)]` for CDN/relative URLs. `[MaxLength(2048)]` for external URLs.
- **FK columns:** `<referenced_table_singular>_id` convention (e.g., `status_id`, `author_id`).
- **Filtered indexes:** `HasFilter("column_name = true")` with snake_case, PostgreSQL `true`/`false`. Use `HasDatabaseName()` for explicit control.

### 4.2 Enum / Lookup Table Decision Framework

Four categories, each serving a different purpose:

| Pattern | When Used | Examples |
|---|---|---|
| **Magic enum** (C# enum, no lookup table) | Tiny, stable list tightly coupled to app logic; no display name needed | `Rating`, `FavoriteStatus`, `ReadStatus`, `ReportedEntityType`, `CharacterRelationshipType`, `StoryRelationshipStatus`, `FilterEntityType`, `ProfileVisibility`, `AllowInteractions`, `DefaultSortOrder` |
| **Lookup table** (no C# enum) | Content-only display; want flexibility to add/rename without deployment | `ReportReason`, `AcknowledgmentRole`, `StoryRelationshipType` |
| **Hybrid** (lookup table + C# enum with `...Enum` suffix) | Both flexible display AND rigid C# business logic needed | `StoryStatusEnum`, `ReportStatusEnum`, `NotificationCategoryEnum`, `NotificationTypeEnum` |
| **String key** (NVARCHAR PK) | Table is tiny; key used directly in C# code as identifier | `SearchMode.SearchModeKey`, `Badge.BadgeKey`, `UserInteractionFilter.InteractionFilterKey` |

**Magic enums:** Stored as `smallint` via `.HasConversion<short>()`. 0-indexed (C# default). Underlying type `: short`.

**Hybrid enums:** C# enum mirrors lookup table PKs. Enum suffix `...Enum` prevents naming collision with the lookup table's model class (e.g., `StoryStatusEnum` vs `StoryStatus`). Underlying type `: short`.

> **Open question (S-03):** Hybrid enums in the codebase are currently 0-indexed (`Draft = 0`), but EF Core uses 0 as the sentinel for "let the database generate the key" in `HasData()` seeding. The `SiteRoles` enum is already 1-indexed for this reason. Hybrid enums that mirror auto-increment lookup table PKs should likely be 1-indexed to match. If lookup tables use `ValueGeneratedNever()`, 0-indexing works but is unconventional. This needs verification against the current migration and seeding code.

**Exception — SiteRoles:** Uses `: int` (matching Identity's int PK) and 1-indexed (User=1, Moderator=2, Admin=3).

> **Previously considered (from Code Assist):** All categorical data as dedicated lookup tables with integer FKs, no exceptions. Rejected in favor of the 4-category framework because it avoids creating lookup tables for trivially stable data like `Rating` (E/T/M, 3 values, never changes).

### 4.3 Inheritance Strategy: Table-per-Type (TPT) with Denormalization

All three inheritance hierarchies use TPT:
- `BaseComment` → `ChapterComment`, `UserProfileComment`, `GroupComment`, `BlogPostComment`
- `BaseBlogPost` → `ProfileBlogPost`, `GroupBlogPost`
- `BasePoll` → `SitePoll`, `BlogPostPoll`

**Rationale:** TPT provides NOT NULL guarantees on child FKs. TPH requires nullable, breaking data integrity. TPT child tables are natural vertical partitions. The join on primary keys is negligible.

**Denormalization:** `DatePosted` is duplicated from base into each child table, enabling composite "golden indexes" like `(ChapterId, DatePosted DESC)` directly on the small child table. EF Core configuration: define the property on the base C# model, then provide explicit Fluent API configuration on **each derived entity** (this overrides EF Core's default of mapping it to the base table).

### 4.4 Vertical Partitioning Strategy

| Entity | Hot Table | Warm Table | Cold Table | Rationale |
|---|---|---|---|---|
| Story | `Story` (~70 bytes/row) | `StoryListing` (~254 bytes/row) | `StoryDetail` (blob) | 3-table split; hot must fit in RAM |
| User | `User` (~150 bytes/row) | — | `UserProfile` (ProfileText blob) | 2-table split; User table is small |
| Recommendation | `Recommendation` (hot) | — | `RecommendationDetail` (Text blob) | 2-table split |
| UserStoryInteraction | `UserStoryInteraction` (filtering) | `UserStoryInteractionDate` (user lists) | `UserStoryRecommendationSource` (workers) | Query-pattern partitioning |

**RAM Budget at scale (100k users, 200k stories, 5M comments, 20M interactions):** Total hot data ~2.1 GB, fits in 4 GB buffer pool. Without partitioning, stories with LongDescription would be ~60 GB.

---

### Core Content Tables

#### Story (Hot)

| Column | Type | Constraints | Notes |
|---|---|---|---|
| StoryId | int | PK, Identity | |
| AuthorId | int? | FK → User, SET NULL | Nullable for anonymization on user deletion |
| Rating | smallint (enum) | | Magic enum |
| StoryStatusId | smallint | FK → StoryStatus, RESTRICT | Hybrid enum-mirror |
| WordCount | int | Default 0 | Sum of chapter word counts |
| ViewCount | int | Default 0 | Updated by Redis background worker |
| PublishedDate | DateTime? | Default CURRENT_TIMESTAMP | |
| LastUpdatedDate | DateTime? | | Updated by application logic |
| OriginalPublishedDate | DateOnly? | | For imported works |
| OriginalLastUpdatedDate | DateOnly? | | For imported works |
| ActiveReportCount | int | Default 0 | For report threshold |
| IsComplete | bool | Default false | |
| ChapterCount | int | Default 0 | Denormalized |
| CommentCount | int | Default 0 | Denormalized for pagination |
| FavoriteCount | int | Default 0 | Includes both public and hidden |

Story statuses: Draft, PendingApproval, InProgress, Completed, OnHiatus, Cancelled, Rewriting, OpenBeta, Rejected.

#### StoryListing (Warm)

| Column | Type | Constraints | Notes |
|---|---|---|---|
| StoryId | int | PK, FK → Story, CASCADE | |
| StoryTitle | string | Required, MaxLength(255) | |
| ShortDescription | string? | MaxLength(500) | Previews/tooltips; must stay in-row |
| CoverArtRelativeUrl | string? | MaxLength(512) | Relative path appended to CDN base |
| SearchVector | NpgsqlTsVector | Generated computed column | GIN indexed for FTS |

#### StoryDetail (Cold)

| Column | Type | Constraints | Notes |
|---|---|---|---|
| StoryId | int | PK, FK → Story, CASCADE | |
| LongDescription | string? | No MaxLength (TEXT) | Only on story detail page |
| Slug | string? | MaxLength(255), Unique filtered (IS NOT NULL) | URL-friendly; hybrid routing |
| PostApprovalStatus | smallint (enum) | | Used once exiting moderation queue |

**Slug policy:** Server-generated from title during creation. **Never client-editable.** The `IEditableStoryProperties` interface excludes slug. Hybrid URL: `/story/{StoryId:int}/{*StorySlug}` — StoryId for lookup, slug for SEO. Redirect to canonical if slug doesn't match.

> **Previously considered:** Fully user-editable slugs; "Generate and Lock" (allow initial editing). Simplified to "never editable" for URL stability.

#### Chapter

| Column | Type | Constraints | Notes |
|---|---|---|---|
| ChapterId | int | PK, Identity | |
| StoryId | int | FK → Story, CASCADE | |
| ChapterNumber | int | | Unique with StoryId |
| Title | string? | MaxLength(255) | "Chapter X" placeholder if null |
| PrimaryContentId | long | FK → ChapterContent, RESTRICT | Cannot delete current primary |
| IsPublished | bool | Default false | |
| VersionCount | int | Default 1 | Denormalized |

Two relationships with ChapterContent: 1-to-many (CASCADE) and 1-to-1 primary (RESTRICT, `WithMany()`).

#### ChapterContent

| Column | Type | Constraints | Notes |
|---|---|---|---|
| ChapterContentId | long | PK, Identity | long for high volume |
| ChapterId | int | FK → Chapter, CASCADE | |
| AuthorId | int? | FK → User, SET NULL | Co-author versioning |
| SortOrder | int | Unique with ChapterId | |
| ChapterText | string | TEXT | HTML, sanitized server-side |
| ContentRaw | string | TEXT | Markdown/editor source |
| WordCount | int | Default 0 | Calculated on stripped text |
| Rating | smallint? (enum) | | Nullable; NULL inherits story rating |
| TopAuthorsNote | string? | | |
| BottomAuthorsNote | string? | | |
| PublishDate | DateTime? | Default CURRENT_TIMESTAMP | |

> **Previously considered name:** `ChapterVersions`. Renamed to `ChapterContents` because 99% have one version; this table supports live alternate versions (e.g., T-rated and M-rated), not revision history.

#### StoryArc / Series / SeriesEntries / StoryRelationships

`StoryArc`: Author-defined chapter groupings with `Title`, `SortOrder`, `StartChapterNumber`, `EndChapterNumber`.

`Series` / `SeriesEntries`: Author-defined canonical series with `OrderIndex` for reading order.

`StoryRelationships`: One-way directional links with `SourceStoryId`/`TargetStoryId`. Absence of reverse entry means "don't show on target story." Seeded types: "Inspired By", "Prequel", "Sequel", "Companion Piece."

---

### Tag System Tables

#### Tag

| Column | Type | Constraints | Notes |
|---|---|---|---|
| TagId | int | PK, Identity | |
| TagName | string | Required, MaxLength(100), Unique | |
| TagTypeId | smallint | FK → TagType, RESTRICT | Character, Setting, Genre, ContentWarning, CrossoverFandom, Relationship |
| IsFanon | bool | Default false | Canon vs community-created |
| ParentTagId | int? | FK → Tag (self-ref), SET NULL | One level deep only |
| SpriteIdentifier | string? | MaxLength(100) | Key for URL builder; NOT a URL |
| AllowOCDetails | bool | Default false | Whether OC details allowed on this tag |
| Description | string? | MaxLength(500) | Tooltip on story cards |

Staff-managed, curated. Users cannot create tags.

#### StoryTag (Junction)

Composite PK `(StoryId, TagId)`. `Priority` column (smallint) for sort/weight. Reverse index on `(TagId, StoryId) INCLUDE (Priority)`.

#### StoryCharacter

Unified table for canon + OC characters. `StoryCharacterId` (PK), `StoryId` (FK), `CharacterTagId` (FK → Tag), `Priority`, `IsOC`, `OC_Name`, `OC_Bio`. OC details enforced by trigger `TR_StoryCharacters_EnforceOCLogic` (PL/pgSQL).

#### StoryCharacterRelationship / StoryCharacterRelationshipMembers

Relationship type (Romantic '/' or Platonic '&') with Priority. Members junction links to StoryCharacter.

#### SettingDetails / SavedTagSelection

`SettingDetails`: Optional overrides for setting tags on a story. `SavedTagSelection`: Named tag sets for search reuse. Copy-on-write sharing.

---

### User & Identity Tables

#### User (extends IdentityUser\<int\>)

| Column | Type | Notes |
|---|---|---|
| ProfilePictureRelativeUrl | string?, MaxLength(512) | |
| Tagline | string?, MaxLength(256) | |
| ShowMatureContent | bool, default false | HOT filter — direct column, not in JSON |
| PrefersDataSaverMode | bool, default false | |
| PrefersAnimatedSprites | bool, default true | |
| AllowDiscoveryFromHiddenFavorites | bool, default false | Opt-in for anonymous boost |
| ThemeId | int (FK → Theme) | |
| ReaderSettings | jsonb | FontName, FontSize, LineHeight, TextWidth, JustifyText, AutoLoadNextChapter, CollapseCommentThreads, DefaultPaginationSize, DefaultSearchSort |
| PrivacySettings | jsonb | ProfileVisibility, ShowActivityStatus, AllowProfileComments, AllowPrivateMessages, ShowUserStats, ShowCurrentlyReading |
| AuthorSettings | jsonb | DefaultStoryRating, DefaultCommentModeration, AllowStoryRecommendations |
| DateCreated | DateTime | |
| ActiveReportCount | int | |

Settings as jsonb columns grouped by concern. Enums inside JSON use `HasConversion<short>()`. New settings don't require migrations.

Identity columns (`PasswordHash`, `SecurityStamp`, etc.) stay on User. `NpgsqlTsVector` for FTS configured as shadow property in `ApplicationDbContext`.

> **Previously considered:** Separate `UserSettings` table (extra JOIN on every request for zero benefit). Flat columns for all settings (messy, every new setting needs migration).

#### UserProfile (Cold)

`UserId` (PK, FK → User, CASCADE) + `ProfileText` (TEXT blob). Only loaded on profile view.

#### UserStats

22+ denormalized counter fields: `StoryCount`, `ChapterCount`, `TotalWordCount` (bigint), `RecommendationCount`, `CommentCount`, `LikesReceived`, `FollowerCount`, `StoriesFavorited`, `StoriesRead`, `WordsRead` (bigint), etc. Updated in real-time by application logic. Background workers read for badge checks.

---

### User Interaction Tables

#### UserStoryInteraction (Hot)

Highest-traffic table. Sparse: no row = all defaults false. 16 bytes/row.

| Column | Type | Constraints |
|---|---|---|
| UserId | int | PK (composite), FK → User, CASCADE |
| StoryId | int | PK (composite), FK → Story, CASCADE |
| HasStarted | bool | Default false |
| IsCompleted | bool | Default false |
| IsFavorite | bool | Default false |
| IsHiddenFavorite | bool | Default false |
| IsFollowed | bool | Default false |
| IsReadItLater | bool | Default false |
| IsIgnored | bool | Default false |

> **Previously: the reading status columns were `IsInProgress`, `IsCompleted`, `IsActivelyReading`.** Revised to `HasStarted`, `IsCompleted`, `IsIgnored` (absorbing abandonment) because:
> - `IsInProgress` → renamed to `HasStarted`. Records a permanent past event (`Has-` prefix). The original name collided semantically with `StoryStatus.InProgress` (author's publication status). The `Is-` prefix incorrectly implied current state for what is actually a past event.
> - `IsActivelyReading` → **eliminated**. Was a derived concept (`HasStarted AND NOT IsIgnored AND NOT IsCompleted`). Storing it required coupling rules or stale acceptance.
> - Abandonment is absorbed by `IsIgnored` — `HasStarted` distinguishes context: `HasStarted=0, IsIgnored=1` = discovery reject; `HasStarted=1, IsIgnored=1` = abandoned.
>
> See `UserStoryInteractions_ReadingStatus_Design.md` for the full displaced-ideas reasoning (seven alternatives considered and rejected) and the complete 8-state truth table.

**Naming convention for reading status booleans:**
- `Has-` prefix (`HasStarted`): permanent past event. Set by application at 90% scroll of Chapter 1. Only cleared by deliberate user action.
- `Is-` prefix (`IsCompleted`, `IsIgnored`): current mutable state. Can be toggled.

**Zero coupling rules:** No bit automatically drives any other bit. Each is set and cleared independently. The service layer rejects logically impossible write combinations but does not cascade.

**The eight valid states (HasStarted / IsCompleted / IsIgnored):**

| H | C | I | Meaning |
|:-:|:-:|:-:|---|
| 0 | 0 | 0 | **Unread.** Default. No interaction recorded. |
| 0 | 0 | 1 | **Discovery reject.** Never wants to see it again. Has not read it. |
| 0 | 1 | 0 | **Read elsewhere, open to engagement.** Read on FFN/AO3 before joining. |
| 0 | 1 | 1 | **Read elsewhere, opted out.** Read on another platform; no engagement wanted. |
| 1 | 0 | 0 | **Reading.** Started, not completed or abandoned. Sub-state (mid-read vs caught-up) computed at query time. |
| 1 | 0 | 1 | **Abandoned.** Started but gave up. Excluded from Continue Reading. |
| 1 | 1 | 0 | **Completed, open to engagement.** Default post-completion state. |
| 1 | 1 | 1 | **Completed, opted out.** Finished but does not want further engagement. |

Note: `(0,1,x)` states are only reachable through an explicit "mark as read elsewhere" user action. They cannot occur via application logic.

**Key queries:**
- Discovery exclusion: `WHERE HasStarted = 1 OR IsIgnored = 1`
- Continue Reading: `WHERE HasStarted = 1 AND IsCompleted = 0 AND IsIgnored = 0 AND user_chapters_read < Stories.PublishedChapterCount`
- Abandoned list: `WHERE IsIgnored = 1 AND HasStarted = 1`
- Active library: `WHERE HasStarted = 1 AND IsIgnored = 0`

Filtered indexes per boolean, both user-centric `(user_id) INCLUDE (story_id) WHERE (flag = true)` and story-centric `(story_id) INCLUDE (user_id) WHERE (flag = true)`.

> **Previously considered:** ReadStatus/FavoriteStatus enums (identical row size in PostgreSQL, but booleans enable cleaner filtered indexes). System lists as rows in UserLists table (requires complex JOINs). Normalized rows with `interaction_type_id FK → interaction_types` lookup (loses single-boolean-check simplicity; found in earlier Code Assist conversations before the boolean model was finalized). `CaughtUp` as a fourth ReadStatus value (required background workers for stale state transitions). `IsAbandoned` as a separate bit (redundant with `IsIgnored` + `HasStarted` context).

#### UserStoryInteractionDate (Warm)

1-to-1 partition. Nullable date columns: `FavoriteDate`, `HiddenFavoriteDate`, `FollowedDate`, `ReadItLaterDate`, `IgnoredDate`, `CompletedDate`. Filtered indexes for sorted user lists.

#### UserStoryRecommendationSource (Sparse)

1-to-1 partition. Only for interactions from a recommendation. `SourceRecommendationId` (FK → Recommendation, RESTRICT).

#### UserChapterInteraction

`IsRead` (bool, explicit checkbox) + `ReadProgress` (float, scroll tracker 0.0–1.0) + `LastInteractionDate`. Auto-set `IsRead = true` at >90% progress, never auto-set to false.

---

### Community & Social Tables

#### FollowedUser

| Column | Type | Constraints |
|---|---|---|
| UserId | int | PK (composite), FK → User, CASCADE |
| FollowedUserId | int | PK (composite), FK → User, RESTRICT |
| DateFollowed | DateTime | Default CURRENT_TIMESTAMP |
| ReceiveAlerts | bool | Default true (bell toggle) |
| IsVouched | bool | Default false (max 5 per user, C# enforced) |

Filtered indexes on `(user_id) WHERE (is_vouched = true)` and `(followed_user_id) WHERE (is_vouched = true)`.

#### Recommendation (Hot) / RecommendationDetail (Cold)

Hot: `RecommendationId`, `StoryId`, `RecommenderId?`, `StatusId` (FK → RecommendationStatus), `IsHiddenGem` (max 5/user), `IsHighlightedByAuthor` (max 5/story), `SuccessfulRecCount`, `LikeCount`, `DatePosted`, `ActiveReportCount`. Unique: `(RecommenderId, StoryId)`.

Cold: `Text` (the full recommendation).

#### RecommendationSuccess / RecommendationLike

Success: tracks when user confirms recommendation was useful (popup after Chapter 1). Like: pure junction `(UserId, RecommendationId)`.

---

### Comment Tables (TPT)

#### BaseComment

`CommentId` (long PK), `UserId?` (SET NULL), `ParentCommentId?` (self-ref, SET NULL — orphan as "[Deleted Comment]"), `CommentText` (TEXT), `LikeCount` (int). No `parent_entity_type_id` — TPT child table IS the discriminator.

#### ChapterComment / UserProfileComment / GroupComment / BlogPostComment

Each has: `CommentId` (PK, FK → BaseComment, CASCADE), specific FK (`ChapterId`/`ProfileUserId`/`GroupId`/`BlogPostId`), denormalized `DatePosted`. Golden index example: `(chapter_id, date_posted DESC)`.

**ChapterComment additional column:** `IsSpoiler` (bool, default false). Marks comments containing spoilers for future chapters. See §5.9.1 for the full spoiler comment design.

All blog post types support comments.

#### CommentLike (Junction)

`(UserId, CommentId)`, CASCADE. **No `DateLiked` column** — anti-addictive design. No trending, no activity feeds, no like notifications.

---

### Blog Post Tables (TPT)

#### BaseBlogPost

`BlogPostId` (long PK), `AuthorId?`, `Title`, `Content` (TEXT), `IsPublished`. `DateCreated`/`LastUpdatedDate` denormalized to derived tables.

#### ProfileBlogPost / GroupBlogPost

Profile: optional `StoryId` (SET NULL). Group: `GroupId` (CASCADE). Both support comments via `BlogPostComment`.

---

### Poll Tables (TPT)

`BasePoll` → `SitePoll` / `BlogPostPoll`. Standard structure with `PollOption` and `PollVote`.

---

### Group Tables

`Group`: `GroupId`, `GroupName` (unique), `Description`, `AudienceRating`, `MaxContentRating`. Three types: Standard, SFW Only, Mature.

`GroupMember`: Composite PK, `Role` as magic enum (Member=0, Moderator=1, Admin=2).

`GroupStory`: First-class entity (not junction) with own PK. Unique `(GroupId, StoryId)`.

`GroupFolder`: Nesting via nullable `ParentFolderId` (self-ref, SET NULL). Many-to-many with GroupStory.

---

### Notification System Tables

#### NotificationCategory (9 seeded)

SiteNews, YourFollows, YourStories, YourProfile, YourRecommendations, Collaborations, Groups, Warnings, YourReports.

#### NotificationType (~35 seeded, gap-based numbering)

Each type belongs to a category and has `DefaultEmailEnabled`. Uniqueness enforced on names — earlier "New Follower" duplication fixed by renaming to "New Story Follower" / "New Profile Follower". `default_collapsed` is a required non-nullable field on all notification types.

#### Notification

`NotificationId` (long), `RecipientUserId`, `NotificationTypeId`, `SourceUserId?` (RESTRICT), `RelatedEntityId` (polymorphic, no FK), `IsRead`, `DateCreated`. Index: `(recipient_user_id, is_read, date_created DESC)`.

#### UserNotificationSetting (Sparse Override)

Only stores rows where user differs from default. `(UserId, NotificationTypeId)` composite PK + `EmailEnabled`.

---

### Search & Filter Tables

`SearchMode` (string key): SearchPage, TreeSearch, AutoTreeSearch, AlsoFavorited, AlsoRecommended, ProfilePublishedStories, ProfileFavorites, ProfileRecommendations.

> **Previously: SearchMode entries were TreeSearch, RandomSearch, AlsoFavorited — conflating source and sort.** Revised to discovery-surface-based entries because the Three-Axis Search model (§5.3) clarified that "Random Search" is Source=All + Sort=Random, not a distinct search mode. Entries now map to pages/surfaces where different filter defaults make sense.

`UserInteractionFilter` (string key): Ignored, Completed, InProgress, etc.
`DefaultSearchSetting`: Matrix `(SearchModeKey, InteractionFilterKey)` → `DefaultValue`.
`UserSearchSetting`: Sparse override `(UserId, SearchModeKey, InteractionFilterKey)`.
`UserCustomFilter`: User list/group inclusion/exclusion filters.

---

### Reports & Moderation Tables

`Report`: `ReportId` (long), `ReportedEntityTypeId` (enum), `ReportedEntityId` (polymorphic, no FK), `ReportReasonId` (FK → ReportReason), `ReportStatusId` (FK → ReportStatus), `ModeratorUserId?`, `Notes`, `ActionTaken`. Reports and Notifications stay polymorphic (not TPT) — primary use is consolidated moderator queue/user feed.

---

### Private Messaging Tables

`Conversation` / `ConversationParticipant` (`LastReadTimestamp`, `IsArchived`) / `PrivateMessage` (`PrivateMessageId` long, `ConversationId`, `SenderUserId?`, `MessageText`, `DateSent`). Real-time via SignalR. Index: `(conversation_id, date_sent DESC)`.

---

### Badge Tables

`Badge` (string key PK): `BadgeKey`, `Name`, `Description`, `IconURL`, `SortOrder`.
`UserBadge`: `(UserId, BadgeKey)` + `DateEarned`, `DisplayOrder` (0 = not displayed).

---

### Cache / Data Mart Tables (NOT in EF Core Migrations)

These tables have NO EF Core model classes, no DbSets, no migrations. Created by background workers via raw SQL.

#### UserStoryTreeSearchEntry

Pre-calculated graph traversal table. Contains only public edges. Booleans: `IsAuthoredByUser`, `IsPublicFavorite`, `IsRecommendation`, `IsHiddenGem`, `IsAuthorSpotlighted`, `IsHiddenFavorite` (consent-based only). Mirrored filtered indexes both directions.

#### AlsoFavoritedScore / AlsoRecommendedScore

`(StoryId, AlsoFavoritedStoryId)` + `Score` (co-occurrence count). Full matrix both directions. Redis Top 100 per story.

#### SiteDailyStat

`StatDate` (PK), counters: NewUsers, TotalUsers, NewStories, TotalStories, NewWords, TotalWords, PageViews, ActiveUsers.

---

### Delete Policy Summary

- **Content** (stories, comments, blog posts, recommendations): SET NULL on author → anonymize, preserve.
- **Interaction data** (follows, interactions, memberships, badges, lists, settings): CASCADE on user.
- **Lookup tables** (tags, themes, statuses, badges): RESTRICT → cannot delete if in use.
- **Self-references** (parent comments, parent tags, parent folders): SET NULL → children become top-level.

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
## 5. Feature Specifications

### 5.1 Story Lifecycle

**Creation flow:** `/stories/new` route. `StoryPropertiesForm.razor` shared form component handles both create and edit paths via multiple `@page` directives on a single dispatcher component. On create: title required, all other fields optional. Server generates slug from title (never client-editable). Initial status: `Draft`. Author can freely edit while in Draft.

**Publication flow:** Author clicks "Publish" → status transitions to `PendingApproval` → enters moderation queue. Moderator approves → `InProgress`. Moderator rejects → `Rejected` + notification with reason. Author can revise and resubmit.

**Status transitions:** Draft → PendingApproval → InProgress/Rejected. InProgress → Completed/OnHiatus/Cancelled/Rewriting. OnHiatus → InProgress. Rewriting → InProgress. Completed ↔ InProgress (author reopens). OpenBeta is pre-release feedback (only for invited readers).

**Edit UX:** In-place editing on the normal story viewing page (edit buttons appear for owner/mod), NOT a separate "dashboard." Lazy-load the editor components only when editing mode is activated.

**`IEditableStoryProperties` interface:** Shared between ViewModel and EF model via explicit interface implementation. Excludes slug, author, creation date, and computed fields. Static extension methods in Core provide validation rules usable in both client and server.

### 5.2 Content Ratings

Three ratings: **E** (Everyone), **T** (Teen), **M** (Mature). Per-story rating. Optional per-chapter rating override (nullable — NULL inherits story rating). `User.ShowMatureContent` is a hot boolean on the User table, checked on every filtered query. Content rating filtering is a cross-cutting concern that touches every read service that returns story data.

### 5.3 Three-Axis Search Architecture

> **Previously: the spec defined "Random Search" and "Tree Search" as separate search modes in a `SearchMode` lookup table, and FTS was implicitly treated as its own search mechanism.** Revised to the three-axis model because analysis revealed the original `SearchMode` table conflated three independent dimensions: where candidates come from (Source), how they're narrowed (Filters), and how they're ordered (Sort). Random search is Source=All + Sort=Random, not a separate mode. FTS is a filter (a WHERE clause), not a source. The three-axis model makes every search query expressible as a Source × Filter × Sort combination.

#### 5.3.1 The Three Axes

**Source** — where candidate stories come from:
- **Stories table** — direct EF Core query against `StoryListing`.
- **Relationship graph** — `UserStoryTreeSearchEntry` data mart. Single-hop (user lists at degree 1) or multi-hop (tree search at degree 2+).
- **Co-occurrence** — `AlsoFavoritedScore` / `AlsoRecommendedScore` data marts.

**Filters** — predicates narrowing candidates. All composable, all apply to any source:
- Tag inclusion/exclusion (via TagSelector / StoryTag joins)
- FTS text match (`SearchVector.Matches()` — a WHERE predicate on StoryListing, not a source)
- User story interaction state (exclude ignored, completed, etc.)
- Content rating (cross-cutting, always applied)
- Custom filters (list/group inclusion/exclusion via UserCustomFilter)

**Sort** — ordering of filtered results (available options depend on source + active filters):
- Random
- Date Published (newest first)
- Relevance (only when FTS text match filter is active — `SearchVector.Rank()`)
- Score (natural ordering for data mart sources — traversal score, co-occurrence count)

#### 5.3.2 FTS Is a Filter, Not a Source

`SearchVector.Matches()` is a WHERE clause on `StoryListing` using the GIN index. `Rank()` produces a relevance score for sorting. FTS queries the same table as Source=All with one extra predicate. FTS can apply to any source because every source eventually joins to `StoryListing`.

#### 5.3.3 Sort Order Exclusions (Deliberate)

**No sort by favorites.** Creates the popularity snowball — the spec's core anti-pattern. Stories at the top accumulate favorites faster than anything below, calcifying the top 50 permanently.

**No sort by last updated.** Creates an update-frequency arms race. Authors publish filler chapters to stay visible. Quality degrades in favor of output frequency.

**No sort by recommendation count.** Could create recommendation farming. Even though recommendations are higher-effort than favorites, ranking by count still incentivizes gaming the metric.

#### 5.3.4 Pagination Architecture

Standard offset pagination for sorted results. The server does the full query each time — the component holds only the current page of DTOs. Completely stateless. Each page navigation fires a new query with the full filter state + page number.

Random mode: batches of 20 (user-configurable via `User.ReaderSettings`). "Give me more" replaces pagination. The user's interaction actions (Ignore, Read It Later) modify DB state, and the next batch query automatically excludes newly-dismissed stories. The interaction buttons ARE the pagination mechanism for random mode.

> **Previously considered:** Client-side ID list pagination (loading all matching IDs upfront, fetching display data page by page). Rejected — the database handles offset pagination efficiently with indexes.

### 5.4 Discoverability: Tree Search

Graph traversal engine for exploring story relationships.

**Manual mode:** User picks seed story → sees connected stories as expandable graph nodes. Connection types displayed on edges. User decides which nodes to follow. Interactive, exploratory.

**Automatic mode (post-beta, needs real data):** Algorithm traverses the graph from seed story, applies scoring weights to edge types, returns ranked results. Uses the pre-calculated `UserStoryTreeSearchEntries` data mart table.

**Privacy model:** The tree search table contains only public edges. `IsHiddenFavorite` edges appear only when `User.AllowDiscoveryFromHiddenFavorites = true` (opt-in). The graph never reveals identity — "3 users favorited this" not "User X favorited this."

### 5.5 Also Favorited / Also Recommended

Collaborative filtering: "Users who favorited Story A also favorited Story B."

**Algorithm:** Self-join on `UserStoryInteraction` WHERE `IsFavorite = true`. For each pair `(StoryA, StoryB)`, count overlapping users = Score. Full matrix stored in `AlsoFavoritedScore` cache table. Redis Top 100 per story.

**Rebuild:** Daily background worker. Zero-downtime table swap.

**Also Recommended:** Same concept on recommendations instead of favorites.

**Deferred past beta:** Needs real user data to produce meaningful results.

#### User Favoriting: Deliberately Omitted

The spec does not include a "favorite user" interaction. This is intentional. Adding it creates a public popularity metric on people, contradicting the philosophy of avoiding popularity snowballs. Follows signal interest in new content. Vouches (bounded to 5) signal genuine personal endorsement. A third mechanism would just be a like count on people.

### 5.6 Recommendations System

Users write recommendations for stories (one per story per user). A recommendation is a substantive, multi-paragraph piece explaining why a story is worth reading. NOT a "review" — the framing matters for community culture.

**Moderation flow:** New recommendations enter `Pending` status → auto-approved after author approval OR moderator review → `Approved` / `Rejected`. Author can highlight up to 5 recommendations on their story page (curated endorsements, like Steam curator picks).

**Hidden Gem:** A recommender can designate one recommendation as a "Hidden Gem" (max 5 per user, C# enforced in service). Hidden Gems boost underappreciated stories in discoverability. A notification fires to the story author when their story receives a Hidden Gem designation.

**Successful Recommendation tracking:** After a user reads a recommended story (completes Chapter 1 via `UserChapterInteraction.IsRead`), a popup asks "Was this recommendation helpful?" Positive response creates a `RecommendationSuccess` row and increments the recommender's `SuccessfulRecCount`.

**Recommendations cannot have spoilers:** Recommendations are pitches to prospective readers who haven't read the story. A spoiler in a recommendation undermines its purpose. No `IsSpoiler` field, no spoiler UI, no spoiler checkbox on the recommendation submission form. This is a deliberate absence.

### 5.7 Hidden Favorite with Opt-in Discovery

Users can mark a story as a Hidden Favorite (private — doesn't appear on public profile, doesn't increment public favorite count). `AllowDiscoveryFromHiddenFavorites` (opt-in boolean on User) allows the hidden favorite to contribute anonymously to Tree Search edges and Also Favorited scores. The user's identity is never revealed.

### 5.8 Vouches

A user can vouch for up to 5 other users (stored as `IsVouched` on `FollowedUser`). A vouch is a stronger signal than a follow — it means "I personally recommend this person's taste and contributions." Vouches appear on the vouched user's profile and strengthen Tree Search edge weights.

**Possible schema change (under consideration):** Promote Vouch to its own junction table with optional `VouchText` (`MaxLength(280)`) rather than `IsVouched` boolean on `FollowedUser`. Rationale: if vouches are high-signal actions from established authors spotlighting newcomers, a brief text note increases discoverability value. The 5-per-user limit already signals these should be deliberate; appropriate friction is correct design.

**Display asymmetry (decided):**
- Outgoing vouches (users you vouch for): publicly visible on your profile.
- Incoming vouches (users who vouch for you): visible only to the profile owner. Prevents gaming (don't vouch a celebrity to appear on their page) and prevents incoming count from becoming a status symbol.

### 5.9 Comments

TPT inheritance (see §4.3). Threaded via `ParentCommentId` (self-ref). `[Deleted Comment]` placeholder when author deletes but replies exist (SET NULL).

**Comment likes:** Pure junction table, no `DateLiked`, no notifications. Anti-addictive by design.

**All blog posts support comments** via `BlogPostComment`.

#### 5.9.1 Spoiler Comments

**`IsSpoiler` on ChapterComment:** Binary boolean on `ChapterComment` (not `BaseComment` — spoilers are a chapter-discussion concept that doesn't apply to profile comments or group comments).

**Label:** "Contains spoilers for future chapters" — explicitly scopes the flag to forward references, not discussion of the current chapter. A comment discussing what happened in this chapter is not a spoiler because the reader got there by reading the chapter.

**Checkbox:** Next to the "Post Comment" button. Optional, default unchecked.

**Completion-gated reveal:** When `IsSpoiler = true`, the comment renders behind a blur/cover. Reveal behavior depends on the viewer's `UserStoryInteraction.IsCompleted` for the story:
- **IsCompleted = true:** Single click reveals immediately.
- **IsCompleted = false (or no interaction row):** Click triggers ConfirmDialog: "You haven't finished the story. Are you sure?" User must confirm to reveal.

`IsCompleted` is independent of `Story.Status`. A reader's "completed" means "caught up with everything published," not "the author marked the story finished."

**Data flow:** The Chapter Page dispatcher already loads `UserStoryInteraction` for the interaction panel. It passes `UserHasCompletedStory: bool` down to `CommentSection` as a parameter. No additional query.

`IsRevealed` is ephemeral component state — every page load re-hides spoilers.

**Guest/logged-out spoiler behavior (deferred past MVP):** Logged-out visitors have no `UserStoryInteraction` row. Gating spoiler reveal behind account creation is a legitimate community-joining nudge (not predatory). Deferred to post-MVP.

### 5.10 Blog Posts

TPT: `ProfileBlogPost` (on user profile) and `GroupBlogPost` (in group). Profile blog posts can optionally be linked to a specific story (`StoryId` FK, SET NULL). Both types support comments.

### 5.11 Groups

Three audience types (Standard, SFW Only, Mature) controlling `MaxContentRating`. `GroupFolder` supports nesting via `ParentFolderId`. Groups can contain stories (`GroupStory`) and blog posts (`GroupBlogPost`). Member roles: Member, Moderator, Admin (magic enum).

### 5.12 Reading Progress Tracking

**Per-chapter:** `UserChapterInteraction.ReadProgress` (float 0.0–1.0) tracks scroll position. Auto-sets `IsRead = true` at >90% progress; never auto-unsets. `IsRead` also has an explicit checkbox for manual override.

**Per-story reading status:** Tracked via `HasStarted`, `IsCompleted`, and `IsIgnored` on `UserStoryInteraction` (see §4 User Interaction Tables for the complete 8-state design).

> **Previously: the spec described per-story tracking as "UserStoryInteraction booleans: IsInProgress, IsCompleted, IsActivelyReading."** Revised to the `HasStarted` / `IsCompleted` / `IsIgnored` three-bit design. `IsInProgress` renamed to `HasStarted` (permanent past event). `IsActivelyReading` eliminated (derived concept, not stored). Abandonment absorbed by `IsIgnored`. See `UserStoryInteractions_ReadingStatus_Design.md` for the complete reasoning that displaced seven alternative designs.

**`HasStarted` trigger:** Application sets `HasStarted = true` when user's scroll position reaches 90% of Chapter 1. The 90% threshold ensures intentional reading, not accidental clicks.

**`IsCompleted` triggers:**
- Application: when user reads the last chapter of a story the author has marked Complete.
- User action: "Mark as read elsewhere" for stories read on FFN/AO3 prior to joining. In this case, `HasStarted` is NOT set — the two paths are intentionally distinct.

**`LastReadDate`:** Stored in Redis, not SQL (too volatile). See §3.18.

**Design principles from reading status work:**
1. Computed distinctions don't need stored bits (mid-read vs. caught-up computed at query time).
2. Coupling rules are a smell — zero coupling rules between stored bits.
3. A cross-cutting flag absorbs multiple roles cleanly when `HasStarted` provides context.

### 5.13 View Count Tracking

Redis `INCR story:{storyId}:views` per page view. Background worker drains every 5 seconds, batch-updates `Story.ViewCount`. No per-user tracking — a view is a view. Worker reads `GETSET` (atomic read-and-reset) to prevent lost counts.

### 5.14 Full-Text Search

PostgreSQL tsvector + GIN index on `StoryListing.SearchVector` (generated computed column). Searches title and short description. FTS is NOT applied to chapter body text (too much noise, too expensive for updates). FTS is a filter axis in the three-axis search model (§5.3), not a standalone source.

Tag filtering uses standard WHERE clauses on junction tables, combined with FTS results.

### 5.15 User Lists and Bookshelves

**System lists** backed by `UserStoryInteraction` booleans (Favorites, Hidden Favorites, Completed, Read It Later, Ignored, Followed). Each is a filtered query on the interaction table.

**Bookshelves** are the personal reading management dashboard. The Canalave Library is the whole site; bookshelves are personal collections within it. **Route:** `/bookshelves/{Tab}`. Active user only (authenticated).

**Bookshelf tabs (system-defined):**
- **Favorites** — `IsFavorite = true`
- **Private Favorites** — `IsHiddenFavorite = true` (visible only to owner)
- **Read It Later** — `IsReadItLater = true`
- **Actively Reading** — `HasStarted = true AND IsCompleted = false AND IsIgnored = false` (derived)
- **Completed** — `IsCompleted = true`
- **Ignored** — `IsIgnored = true`
- **Abandoned** — `IsIgnored = true AND HasStarted = true`
- **Following** — `IsFollowed = true` (stories followed for update notifications)
- **My Stories** — `Story.AuthorId = currentUserId` (from Stories table, not interactions)
- **Custom Lists** — user-created collections from `CustomLists/` feature.

**Bookshelves are not a discovery surface.** No SearchMode entries. Tags and interaction filters may be available for narrowing within a bookshelf, but this is management (organizing things you already know about), not discovery.

**Custom lists** via `UserCustomFilter` with inclusion/exclusion rules for personal lists, public lists, groups, and group folders.

### 5.16 Tag Selection UI and Tag Directory

`Blazored.Typeahead` for autocomplete. `SavedTagSelection` allows users to save and reuse tag combinations for search. Copy-on-write sharing: sharing creates a clone, modifications don't affect the original.

**Tag Directory** (`/tags`): The tag management page is not moderator-only. It is `TagDirectoryPage` — a user-facing reference page for browsing tags organized by type with descriptions and sprites. Moderators see create, edit, and delete controls behind `<AuthorizeView>`. One page, two experiences.

Mobile support for browse mode. Edit controls desktop-only for moderators.

> **Previously considered:** `TagLibraryPage` as a moderator-only desktop-only page. Rejected because readers benefit from browsing tag descriptions and sprites as a reference.

### 5.17 Story Editing UX

In-place editing (Fimfiction model): edit buttons appear on the normal viewing page for the author and moderators. No separate dashboard. Lazy-load editor components only when entering edit mode. `<AdminControls>` component encapsulates author/mod-only UI.

### 5.18 Notification Settings

User preferences stored as sparse overrides on `UserNotificationSetting`. Only rows where the user differs from the default are stored. UI: grouped by `NotificationCategory`, toggles for in-app and email per type. Categories can be collapsed in the notification panel (controlled by `NotificationType.DefaultCollapsed`).

### 5.19 Private Messaging

`Conversation` / `ConversationParticipant` / `PrivateMessage`. Unread indicator via `LastReadTimestamp` comparison. Archive support via `IsArchived` on participant. Real-time via SignalR. Respects `User.PrivacySettings.AllowPrivateMessages`.

**Rich text in messages:** Private messaging uses the same `EditorView` + `RichTextView` components as all other text surfaces. PM on this site is for substantive conversation between people who found each other through shared engagement with content — not ambient social chat. The unified editor reinforces that all writing on the site is treated with the same respect.

> **Previously considered:** A stripped-down messaging composer with minimal toolbar. Rejected because it contradicts the "all contributors are first-class citizens" philosophy.

### 5.20 Badge System

String-key badges (e.g., `"first_story"`, `"100k_words"`). MVP: synchronous inline checks in service methods after relevant actions. Future: async worker scans for newly qualified users. `UserBadge.DisplayOrder` (0 = not displayed) lets users curate which badges appear on their profile.

### 5.21 Content Moderation

**Report flow:** User submits report → `ReportReceived` notification sent to reporter. Moderator reviews → action taken or no action. `ReportResolved` or `ReportResolvedNoAction` notification sent to reporter. Transparency: reporters always learn the outcome.

**Moderator actions:** Content removal (`ContentRemoved` notification), story rejection (`StoryRejected`), account warning/suspension/ban (`AccountWarning`/`AccountSuspended`/`AccountBanned`).

**Report threshold:** `ActiveReportCount` on Story and User for auto-flagging.

### 5.22 Roles and Authorization

Three roles seeded in `AspNetRoles`: User (1), Moderator (2), Admin (3). Admin inherits all Moderator permissions. Role-based visibility via `<AuthorizeView>` in Blazor.

### 5.23 Story Import & Verification

`OriginalPublishedDate` / `OriginalLastUpdatedDate` (DateOnly) for imported works. Verification system: imported stories enter `PendingApproval` queue, linked to original source URL. Moderator reviews that the account holder is the original author.

### 5.24 Content Download/Export

Allow users to download their stories in standard formats (ePub, PDF). Serves portability and data ownership.

### 5.25 Feature Suggestions

User-submitted feature requests with community voting. Lightweight: a simple upvote list, not a full issue tracker.

### 5.26 Community Spotlight & Donations

The site's sole revenue model. Community Spotlight is a periodic feature highlighting outstanding community contributions (stories, recommendations, recommenders). Donations are tied to this feature — donors support the community and the platform's operating costs through voluntary contributions. No ads, no premium tiers, no paywalled features.

> **Previously considered:** Operating cost transparency via site banner showing weekly pledge progress toward server costs. Pledge system with `OperatingCosts`/`Pledges`/`SpotlightCredits` tables. Simplified to direct donation model tied to Community Spotlight.

### 5.27 Profile Page Architecture

#### Two-Half Structure

**Top half:** Identity — bio, tagline, stats, badges, outgoing vouches. Static content loaded once.

**Bottom half:** Tabbed story lists at degree-1 from the profile user. Tabs: Favorites, Recommendations, Authored Stories. ResultsFilterPanel available on each tab (tags, interaction filters, FTS, sort). The profile page composes the same ResultsFilterPanel and StoryDeck components the search page uses.

#### Live Tables, Not Data Mart

Profile degree-1 queries use the live `UserStoryInteraction`, `Recommendation`, and `Story` tables, not the data mart. The data mart rebuilds daily; profile data should be immediately fresh (a story favorited five minutes ago should appear). The data mart is for degree 2+ traversal only, where pre-calculation earns its cost.

Own-profile vs other-profile is a privacy filter, not a source switch:
- Own profile: include hidden favorites (`WHERE IsFavorite = true`)
- Other profile: exclude hidden favorites (`WHERE IsFavorite = true AND NOT IsHiddenFavorite`)

Same table, same service, one extra predicate. The service takes a `bool includePrivate` derived from whether the requesting user matches the profile user ID.

#### Profile Tabs Are Not the Search Page

The profile page composes search components (ResultsFilterPanel + StoryDeck) inline — users stay in the profile URL space (`/user/{id}/favorites`). This preserves the community feel of visiting someone's space. The search page (`/discover`) serves the general discovery use case.

> **Previously considered:** Profile tabs as redirects to the search page (lost the "visiting someone's space" feel). Profile tabs using the data mart for degree-1 (24-hour staleness problem). A separate `UserListPage` component (folded into the profile page composing search components directly).

### 5.28 Search Pages and Discovery Routes

#### Search Page

**Route:** `/discover`

Source=All only. Random sort preloaded on entry (the area is never blank). User can switch to Date Published. FTS text input available — when active, Relevance sort option appears. ResultsFilterPanel with all filter types. StoryDeck for results. "Give me more" button in random mode replaces pagination. Standard offset pagination in Date Published / Relevance modes.

#### Unified Tree Search Page

**Routes:** `/discover/me`, `/discover/user/{userId:int}`, `/discover/story/{storyId:int}`

Manual tree search (graph visualization) and automatic tree search (scored flat list) are tabs on the same page. Auto results make more sense when the user can see the graph that produced them.

Root entity metadata displayed at top (user identity or story metadata, depending on which route matched). Degree controls and edge type selector for auto tree search tab. ResultsFilterPanel available. All filters (tags, FTS, interaction state) compose with the data mart query.

`/discover/me` is a clean URL for "discover from myself" — loads the current user's ID internally. Story detail pages link to `/discover/story/{id}` for "discover from this story."

**URL state:** Anchor (root entity) in path. Filters and degree in query params. Expanded graph nodes are ephemeral (not in URL — refreshing resets to root). Filter changes use `replace-state` (no back-button history clutter). Anchor changes use `push-state` (back button returns to previous root).

#### Also Favorited / Also Recommended (Embedded)

Embedded sections on the story detail page, not separate pages. Co-occurrence data mart query scoped to the currently viewed story. User's interaction filter defaults applied (the SearchMode-specific sparse override). ResultsFilterPanel may be simplified in this context (interaction filters relevant; tag and FTS filters available but may default to hidden).

> **Previously considered:** `/discover` pointing to tree search (inverted priority). Source=All search living at `/search` separately from `/discover`. Auto tree search as a separate page from manual tree search.

#### Story Page Layout Order

Title → cover art → long description → chapter selection → recommendations (author-spotlighted first). No comments at story level — comments are chapter-scoped only (`ChapterComment`, no `StoryComment`).

### 5.29 Page Inventory and Routes

#### Discovery Surfaces

| Page | Route | Notes |
|---|---|---|
| Home Page | `/` | Community Spotlight stories |
| Search Page | `/discover` | Source=All, random default, FTS |
| Unified Tree Search | `/discover/me` | Self as root |
| | `/discover/user/{userId:int}` | User as root |
| | `/discover/story/{storyId:int}` | Story as root |

#### Content Pages (with in-place editing)

| Page | Route | Notes |
|---|---|---|
| Story Page | `/story/{StoryId:int}/{*StorySlug}` | Detail + Also Favorited + Recommendations |
| Chapter Page | `/story/{StoryId:int}/{ChapterNumber:int}/{VersionOrder:int?}` | Reading + navigation + comments |
| Story Create | `/stories/new` | StoryPropertiesForm |
| Story Edit | `/story/{StoryId:int}/edit` | StoryPropertiesForm |
| Blog Post Page | `/blog/{PostId:int}/{*Slug}` | In-place editing |

#### Community Pages

| Page | Route | Notes |
|---|---|---|
| Group Page | `/group/{GroupId:int}/{*GroupSlug}` | Detail + members + stories + folders |
| Groups Directory | `/groups` | Browse/search groups |
| Tag Directory | `/tags` | Browse for all, edit for mods behind AuthorizeView |
| Users Search | `/users` | Search users by name |

#### User Pages

| Page | Route | Notes |
|---|---|---|
| Profile Page | `/user/{UserId:int}/{*Tab}` | Identity + tabbed degree-1 content |
| Bookshelves | `/bookshelves/{Tab}` | Personal reading management, active user only |
| Settings | `/settings` | Custom user preferences |
| Notifications | `/notifications` | Full notification center |
| Messaging | `/messages/{ConversationId:int?}` | Conversation list + thread, SignalR |

#### Moderation (Desktop-Only, No Dispatcher Pattern)

| Page | Route | Notes |
|---|---|---|
| Reports | `/mod/reports` | Review reports, take action |
| Story Submissions | `/mod/submissions` | Review pending, approve/reject |
| User Management | `/mod/users` | Account actions |

#### Identity (Server Project, ASP.NET Scaffold)

| Page | Route | Notes |
|---|---|---|
| Login, Register, etc. | `/account/*` | Already scaffolded, just need styling |

### 5.30 Feature-Level Component Specifications

#### 5.30.1 Blazored TextEditor and Blazored Typeahead

Both chosen because they're behavioral without being visual — they solve hard browser interaction problems without bringing a design system. Blazored TextEditor wraps Quill.js for WYSIWYG rich text. Blazored Typeahead provides autocomplete/search-as-you-type.

Both expose item templates, so the visual rendering is entirely yours (Tailwind classes). Quill ships its own CSS (`quill.snow.css` or `quill.bubble.css`) — test with Tailwind's Preflight resets early.

#### 5.30.2 EditorView and RichTextView

`EditorView` (coordination composite / third-party wrapper): wraps Blazored TextEditor with toolbar, editable area, and preview toggle. Preview uses `RichTextView`, so what the author sees in preview is identical to what readers see.

`RichTextView` (leaf): takes `string? HtmlContent` parameter, optionally reader settings (`FontName`, `FontSize`, `LineHeight`, `TextWidth`, `JustifyText`), renders sanitized HTML.

**Universal across ALL text surfaces:** chapters, comments, author notes, story descriptions, recommendations, profile bios, blog posts, AND private messages. No content-type axis for the editor. The only legitimate axis is device — desktop shows full toolbar, mobile shows a more compact toolbar with overflow for less-used formatting. This applies uniformly everywhere EditorView is used.

**Inline Pokémon sprites in the editor:** requires a custom Quill blot (JS interop). Must serialize to something `HtmlSanitizer` can process. Flagged as a distinct non-trivial implementation task.

#### 5.30.3 ChapterNavigation

Coordination composite encompassing four concerns: previous/next buttons, chapter select dropdown, version picker, and version switch button. Appears at top and bottom of the chapter page.

**Data flow:** Dispatcher loads chapter list and version data once, passes as parameters to both instances. ChapterNavigation does not inject services.

**Version nuance:** The dropdown shows alt-version indicators on chapters that have accessible alternate versions (filtered by user's T/M setting from `ShowMatureContent`). Next/prev buttons navigate the primary version by default with extra flourish for version switching when applicable.

**URL structure:** `/story/{storyId:int}/{chapterNumber:int}` for primary, `/story/{storyId:int}/{chapterNumber:int}/{versionOrder:int}` for alternates. Multiple `@page` directives on the chapter page dispatcher.

#### 5.30.4 TagChip and TagSelector

`TagChip` (leaf): tag name, sprite, background color from `TagType`, optional `EventCallback OnRemove`. When `OnRemove.HasDelegate` is true, the X button appears; when absent, the chip is display-only.

**Naming note:** "Chip" is the standard term (Material Design). Alternatives considered: pill (implies non-removable), token (Atlassian), badge (implies status). "Chip" is correct and well understood.

`TagSelector` (coordination composite): selected chips accumulate above the Blazored.Typeahead text box. Dropdown items are lightweight rows (color accent dot + sprite + name), NOT full chips — scannable list format. On selection, item becomes a chip above the input. Raises `EventCallback<IReadOnlyList<Tag>> OnSelectionChanged` — parent decides what to do with the result.

#### 5.30.5 UserStoryInteractionButton

Leaf component tied to the `UserStoryInteraction` table concept. The verbose name `UserStoryInteractionButton` is deliberate — other things are "interactive buttons" but this is specifically about the user-story interaction domain.

**EventCallback-driven behavior (no mode enum):** Absence of `OnToggle` EventCallback means read-only. Read-only buttons render only when `IsActive` is true (passive indicator). When `OnToggle` is provided, the button is always rendered and clickable. The owner composes buttons and assigns properties to get the behavior needed.

```razor
@code {
    [Parameter] public bool IsActive { get; set; }
    [Parameter] public EventCallback<bool> OnToggle { get; set; }
    [Parameter] public string IconIdentifier { get; set; } = "";
    private bool IsReadOnly => !OnToggle.HasDelegate;
}
@if (!IsReadOnly || IsActive)
{
    <button @onclick="HandleClick" ...>...</button>
}
```

**Two presentation contexts:**
- **Listing context** (StoryCard in search results, bookshelves, etc.): Ignore and ReadItLater receive `OnToggle` (clickable). Favorite, Followed, HiddenFavorite receive no `OnToggle` (read-only, visible only when true).
- **Detail context** (story page): all receive `OnToggle` (all clickable).

**Visibility rules:** Ignore and ReadItLater are visible only when the story is a blank slate (no positive engagement) OR when already active. "Blank slate" means: not favorited, not private favorited, not followed, not completed, not actively reading.

**Own story:** The author's own story replaces the interaction panel with an Edit Story button. The panel composite receives `IsOwnStory` parameter.

**Theme-swappable icons:** Resolved via `ISpriteService.GetInteractionIcon(InteractionTypeEnum, theme)`. Default: star (follow), heart (favorite). Pokémon theme: Staryu, Luvdisc, etc.

**Debounce:** `SiteConstants.InteractionDebounceMs = 2000` (developer-configurable). Managed by the coordination composite (`StoryInteractionPanel`), not by individual buttons.

> **Previously considered:** A mode enum (Bootstrap approach) for triage/browse vs full mode. A generic `InteractionButton` name. Collapsing the panel entirely for the author's own story (edit button is better).

#### 5.30.6 StoryCard and StoryDeck

`StoryCard` (leaf): takes `StoryListingDto`, renders with computed display properties in `@code` (formatted word count, status badge class, cover art fallback). Composes `TagChip` instances. Includes a caret dropdown button for secondary navigation options.

**StoryCard caret options:** View Story, Discover from this Story, Copy link, Report, Download/Export.

**StoryDeck** (pass-through layout composite): the container holding StoryCards. Three-state pattern (loading/empty/populated). Grid layout. Used by: search page, bookshelves, profile page tabs, Also Favorited section, group story listing.

**MVP scope:** StoryCard only. No StoryRow for list view. Fimfiction has three view options (card, list, detailed) but MVP ships card only.

> **Previously considered:** `StoryList` as the container name (confused with `StoryListingDto`). `StoryCatalog`, `StoryGallery`, `StoryGrid` considered. `StoryDeck` chosen — a deck is a curated ordered set of cards.

#### 5.30.7 UserCard

Three display levels, mirroring StoryCard's three levels:
1. **Hyperlink only** — username text with link to profile. Used in: StoryCard author byline, notification items, comment author attribution.
2. **UserCard** (leaf) — compact rich preview: avatar, bold username with link, caret dropdown, badges on display (curated subset only, `DisplayOrder > 0`). Used in: Following section, vouch display, group member list, comment items, recommendation display, messaging, users search page, tree search nodes, moderator tools.
3. **Full Profile Page** — all details.

**UserCard caret options:** View Profile, Discover from this User, Copy link, Report, Send PM.

StoryCard should NOT contain UserCard — only a username hyperlink. StoryCard is too frequent and compact for a nested card. The Story Page can show UserCard for the author.

#### 5.30.8 ResultsFilterPanel

Coordination composite. Contains: tag selection (TagSelector), FTS text input (debounced), user story interaction filter toggles, sort order selector (options conditional on owner via `AvailableSorts` parameter), "Apply Filters" button (not "Search" — misleading when source is already determined).

Owner configures which sections are visible via parameters (`ShowTagFilter`, `ShowTextSearch`, `ShowInteractionFilters`). The component raises `OnSearch` with a filter criteria DTO. It does not know which source the parent is querying.

Does NOT need a ViewModel class — `@code` holds current selections (computed coordination state).

#### 5.30.9 ConfirmDialog

Universal composite for actions requiring confirmation. Takes a message, confirm action, cancel action, renders as a modal. Needed for: spoiler reveal, account deletion, leaving a group, deleting a custom list, unpublishing a story.

---
## 6. Design Rationale & Rejected Alternatives

### 6.1 Why PostgreSQL over SQL Server

**Chosen:** PostgreSQL.
**Decisive factor:** Free read replicas via built-in streaming replication. SQL Server locks this behind Enterprise Edition ($15k+/year).
**Additional factors:** MVCC for better concurrency, JSONB for settings, tsvector/GIN for FTS, zero licensing cost.
**Accepted trade-offs:** No bit packing (1 byte per boolean vs 1 bit), no TINYINT, fixed 8-byte timestamps.

### 6.2 Why Global InteractiveAuto over Island Mode

**Chosen:** Global `InteractiveAuto` on `<RouteView>`.
**Alternative rejected:** Component-level `@rendermode InteractiveWasm` with Static SSR default. Rejected because standard `<a>` links triggered full page reloads, destroying client state. For a site where users click "Next Chapter" repeatedly, instant SPA transitions are non-negotiable.

### 6.3 Why Boolean-Column UserStoryInteraction over Normalized Rows

**Chosen:** 7 boolean columns on a single wide row. Sparse creation. 16 bytes/row.
**Alternative rejected:** Normalized rows with `interaction_type_id FK → interaction_types`. Found in earlier Code Assist conversations, superseded by the boolean model. Rejected because: (a) booleans enable single-column filtered indexes; (b) common operations are single-boolean checks; (c) PostgreSQL row size is identical either way; (d) avoids JOINs through junction tables.
**Other alternatives considered:** ReadStatus/FavoriteStatus enums (identical size, less flexible indexing). System lists as UserList rows (complex JOINs, lost filtered-index simplicity).

### 6.4 Why 4-Category Enum Framework over All-Lookup-Tables

**Chosen:** Magic enum / lookup-only / hybrid / string-key decision framework.
**Alternative rejected:** All-lookup-tables (every categorical column gets a dedicated lookup table with integer FK). Found in Code Assist conversations. Rejected because it creates unnecessary lookup tables for trivially stable data like `Rating` (E/T/M, 3 values, never changes), requiring unnecessary JOINs and seed data.

### 6.5 Why CQRS-Lite Split over Single Interface

**Chosen:** `IStoryReadService` / `IStoryWriteService` with inheritance.
**Evolution:** Single `IStoryService` → Split without inheritance (Code Assist) → Split with inheritance and compile-time DbContext safety (Step 2 insights).
**Rationale:** Least-privilege interfaces, independent scalability potential, clearer developer intent, DbContext safety via primary constructor privacy.

### 6.6 Why Direct DbContext Injection over IDbContextFactory

**Chosen:** Direct primary constructor injection of `ReadOnlyApplicationDbContext` + `ApplicationDbContext`.
**Alternative superseded:** `IDbContextFactory<ApplicationDbContext>` (chosen in Code Assist for thread safety). Superseded because with scoped service registration (`AddScoped<>`), the DI container manages DbContext lifetime — the thread-safety concern doesn't apply. Direct injection enables compile-time safety via primary constructor field privacy.

### 6.7 Why Table-per-Type over Table-per-Hierarchy for Comments

**Chosen:** TPT with denormalized `DatePosted`.
**Alternative rejected:** TPH (single table, discriminator column). Rejected because TPH forces all child FKs (`ChapterId`, `ProfileUserId`, `GroupId`, `BlogPostId`) to be nullable, breaking NOT NULL guarantees. TPT guarantees data integrity at the schema level.

### 6.8 Why Vertical Partitioning for Story

**Chosen:** 3-table split (Story/StoryListing/StoryDetail).
**Rationale:** `LongDescription` is a TEXT blob loaded only on detail pages. Without partitioning, every story card render would pull this blob into the buffer pool. At 200k stories, keeping LongDescription in the hot table bloats buffer pool requirements from ~2 GB to ~60 GB.

### 6.9 Why ChapterContents over ChapterVersions

**Chosen:** `ChapterContent` (table name: `chapter_contents`).
**Alternative rejected:** `ChapterVersions`. Rejected because 99% of chapters have exactly one "version." The table supports live alternate versions (e.g., T-rated and M-rated variants), not revision history. "Contents" is more intuitive for this use case.

### 6.10 Why jsonb Settings over Flat Columns

**Chosen:** jsonb columns grouped by concern (`ReaderSettings`, `PrivacySettings`, `AuthorSettings`) plus hot booleans directly on User for filter-critical settings.
**Alternative rejected:** All settings as flat columns on User (Code Assist approach). Rejected because new settings require migrations, and the User row becomes unwieldy. Hot booleans stay as direct columns for query performance.

### 6.11 Why No Comment Like Notifications or DateLiked

**Chosen:** Comment likes generate no notifications and have no timestamp.
**Rationale:** Deliberately anti-addictive. Prevents trending/activity-feed mechanics. Likes are quiet affirmation, not engagement bait. This is a core philosophical choice, not an oversight.

### 6.12 Why Server/Client Prefix over Db/Http Prefix

**Chosen:** `ServerStoryReadService` / `ClientStoryReadService` (Step 2 pattern).
**Alternative superseded:** `DbStoryReadService` / `HttpStoryReadService` (Code Assist pattern). The `Db`/`Http` prefix communicates implementation mechanism. The `Server`/`Client` prefix communicates deployment target. Deployment target was chosen as more semantically useful — the same class could theoretically switch from EF Core to Dapper without a rename.

### 6.13 Why Minimal API over Controllers

**Chosen:** Minimal API endpoints in feature-specific extension method classes.
**Alternative superseded:** MVC API controllers. Minimal API is lighter, produces less ceremony, and is the modern .NET approach.

### 6.14 Why IEntityTypeConfiguration over Alphabetical OnModelCreating

**Chosen:** `IEntityTypeConfiguration<T>` with `ApplyConfigurationsFromAssembly()`.
**Alternative superseded:** All Fluent API in `OnModelCreating` sorted alphabetically. The configuration-class approach is more modular, supports vertical slicing, and prevents `OnModelCreating` from growing to hundreds of lines.

### 6.15 Why No Ads

**Chosen:** Fully ad-free. Revenue from community donations tied to Community Spotlight.
**Alternatives rejected:** Ads for guest users only (via `<AuthorizeView>`). Weekly pledge drive with operating cost banner. Both rejected because the platform exists to serve its community, not to monetize attention. The community-donation model aligns with the philosophical commitment to anti-addictive design.

### 6.16 Why Tailwind over Component Libraries

**Chosen:** Tailwind CSS with no component library.
**Alternatives rejected:** MudBlazor, Blazorise, and similar component libraries. Rejected because they impose Material Design or similar visual gravity, making the site feel generic and corporate. The Canalave Library needs a unique visual identity reflecting Pokémon-fandom community with a warm, non-predatory feel.

### 6.17 Why HasStarted over IsInProgress

**Chosen:** `HasStarted` with `Has-` prefix (permanent past event).
**Alternative superseded:** `IsInProgress`. Rejected for two reasons: (1) name collision with `StoryStatus.InProgress` (author's publication status), and (2) the `Is-` prefix incorrectly implied current mutable state when the bit actually records a permanent past event. The rename also enabled the insight that completed stories should have `HasStarted=1, IsCompleted=1` (truthful) rather than `HasStarted=0, IsCompleted=1` (the mutual-exclusion artifact).

### 6.18 Why Three-Axis Search over SearchMode Lookup

**Chosen:** Source × Filter × Sort three-axis model.
**Alternative superseded:** `SearchMode` lookup table with entries like TreeSearch, RandomSearch, AlsoFavorited. Superseded because the original table conflated three independent dimensions. "Random Search" is Source=All + Sort=Random, not a distinct search mode. FTS is a filter, not a source. The three-axis model makes every query expressible as a combination, and `SearchMode` entries now map to discovery surfaces (pages with different default filter settings).

---

## 7. Naming Conventions & Deliberated Names

### 7.1 C# Naming

| Element | Convention | Example |
|---|---|---|
| Project names | `TheCanalaveLibrary.{Layer}` | `TheCanalaveLibrary.Server`, `.Client`, `.SharedUI`, `.Core` |
| Service interfaces (read) | `I{Feature}ReadService` | `IStoryReadService` |
| Service interfaces (write) | `I{Feature}WriteService` | `IStoryWriteService` |
| Server implementations | `Server{Feature}ReadService` | `ServerStoryReadService` |
| Client implementations | `Client{Feature}ReadService` | `ClientStoryReadService` |
| Cached decorators | `Cached{Feature}Service` | `CachedStoryService` |
| EF configurations | `{Entity}Configuration` | `StoryConfiguration : IEntityTypeConfiguration<Story>` |
| API endpoints | `{Feature}Endpoints` | `StoryEndpoints.MapStoryEndpoints(this WebApplication app)` |
| DTOs | `{Entity}{Purpose}Dto` | `StoryCreateDto`, `StoryEditDto`, `StorySummaryDto` |
| ViewModels | `{Feature}ViewModel` | `StoryEditViewModel` |
| Shared validation | `IEditable{Feature}Properties` | `IEditableStoryProperties` |
| Mapping classes | `{Feature}Mappers` | `StoryMappers` |
| Assembly markers | `{Project}AssemblyIdentifier.razor` | `SharedUIAssemblyIdentifier.razor` |
| Magic enums | `{Name} : short` | `Rating : short`, `ReadStatus : short` |
| Hybrid enums | `{Name}Enum : short` | `StoryStatusEnum : short` |
| User model | `User` | NOT `ApplicationUser` (deliberately renamed) |
| Role model | `ApplicationRole` | Extends `IdentityRole<int>` |
| Custom exception | `{Feature}ValidationException` | `StoryValidationException` |

### 7.2 PostgreSQL Naming

| Element | Convention | Example |
|---|---|---|
| Tables | snake_case plural | `stories`, `story_listings`, `user_story_interactions` |
| Columns | snake_case | `story_id`, `is_favorite`, `date_posted` |
| FK columns | `{referenced_table_singular}_id` | `author_id`, `status_id`, `rating_id` |
| Constraint names | See §4 constraint table | `PK_Stories`, `FK_Stories_Users` |
| Indexes | `ix_{table}_{columns}` | `ix_user_story_interactions_is_favorite` |
| Identity tables | PascalCase (retained by EF) | `AspNetUsers`, `AspNetRoles` |

### 7.3 Deliberately Settled Names

| Name | What It Is | Previously Considered |
|---|---|---|
| `ChapterContent` | Table for chapter text/versions | `ChapterVersions` (rejected: implies revision history) |
| `User` | Identity user class | `ApplicationUser` (Identity default, replaced for distinction) |
| `StoryStatusEnum` | Hybrid enum with `...Enum` suffix | `StoryStatus` without suffix (causes collision with lookup model class) |
| `ServerStoryReadService` | Server implementation prefix | `DbStoryReadService` (mechanism-based, replaced by deployment-based) |
| `FollowedUser` | User follow relationship | `UserFollows` (found in Code Assist; WC's deliberated design prevails) |
| `IStoryReadService` / `IStoryWriteService` | CQRS-lite split | `IStoryService` single interface |
| `Recommendation` | Substantive endorsement | "Review" (wrong connotation for the culture being built) |
| `StoryCharacter` | Unified canon + OC table | `OCs` separate table |
| `UserStoryInteraction` | Boolean-column interaction table | `UserStoryInteractions` with `interaction_type_id` FK |
| "Followed Users" | User-facing terminology | "Followed Authors" (not everyone is an author) |
| `HasStarted` | Reading progress flag | `IsInProgress` (name collision, wrong prefix semantics) |
| `StoryDeck` | Container for StoryCards | `StoryList` (confused with `StoryListingDto`), `StoryCatalog`, `StoryGallery`, `StoryGrid` |
| `TagChip` | Tag display component | pill, token, badge (less standard) |
| `UserStoryInteractionButton` | Interaction toggle button | `InteractionButton` (too generic) |

### 7.4 Folder Clusters — Codebase Maintenance Reference

Feature-based vertical slicing. Files grouped by feature domain, not technical type.

> **Namespace rule:** One namespace per project, regardless of folder organization. `TheCanalaveLibrary.Core` for everything in Core, `TheCanalaveLibrary.SharedUI` for everything in SharedUI, `TheCanalaveLibrary.Server` for everything in Server, `TheCanalaveLibrary.Client` for everything in Client. Folders are purely for human navigation and Claude Code file placement. Claude Code must use the project-level namespace on every new file regardless of which folder the file is in.

> **Clustering principle:** Reads and writes for the same feature live in the same folder. The folder is determined by the domain concept the feature belongs to. "My Favorites" is a `UserStoryInteractions/` feature — both the toggle and the list view live there — even though it returns `StoryListingDto` (a type originating from `Stories/`). When a feature needs data from outside its own domain, it uses service composition: the feature service injects a foundational service and calls its building-block methods (see §3.26). This keeps features cohesive without duplicating query logic.
>
> **Composition direction:** Composite services inject foundational services, never the reverse. The dependency graph flows from domain-specific features toward foundational ones (`UserStoryInteractions/` → `Stories/`, `Searches/` → `Stories/`, `Groups/` → `Stories/`). Foundational services never depend on the features that consume them. When a component needs to display data from two unrelated domains (a story card with a "favorited" indicator), it calls both services independently and merges at the call site — a Layer 3 concern, not service composition.
>
> **Example classes are non-exhaustive.** They list types we already know will exist based on the spec. Claude Code will need to make judgment calls for new types not listed here. The developer reviews placement choices during code review.

| Folder | Description | Example Classes |
|--------|-------------|-----------------|
| `Lookups/` | All seeded lookup and enum-mirror tables. Layer-1-only — no service interfaces. Every other folder queries these directly via FK. Contains seed data configurations and the `ModelEnums.cs` enum definitions. No CQRS split (no read/write services). | `StoryStatus`, `TagType`, `ReportReason`, `ReportStatus`, `NotificationCategory`, `NotificationType`, `RecommendationStatus`, `AcknowledgmentRole`, `StoryRelationshipType`, `SearchMode`, `UserInteractionFilter`, `DefaultSearchSetting`, `SiteConstants.cs`, lookup `IEntityTypeConfiguration<T>` classes |
| `Identity/` | ASP.NET Identity integration. This folder is an **exception to the standard layer patterns** — it does not follow the CQRS-lite service split. Identity uses `UserManager`/`SignInManager` directly, not `IXReadService`/`IXWriteService`. Login, register, and 2FA pages use form-POST-to-endpoint (not `@onclick` → service call) because `SignInManager` sets auth cookies via `HttpContext.Response`, which isn't available inside an active Blazor circuit. All Identity pages live exclusively in the Server project, permanently ineligible for Layer 5 (WASM). The `User` entity class lives here because it extends `IdentityUser<int>`. Account deletion logic also lives here (primarily an Identity teardown — cascading deletes, resolving RESTRICT FK conflicts). Custom user data (profile, settings, stats) follows the standard pattern and lives in `Profiles/`, not here. | `User`, `ApplicationRole`, `SiteRoles` enum, `DeleteUserService`, Identity scaffolded pages (`Login.razor`, `Register.razor`, `Manage/` pages, etc.), cookie auth configuration, Data Protection configuration |
| `Sprites/` | The sprite and theme system. Read-only — `ISpriteReadService` only, no write half, because sprites are git-managed static assets in `wwwroot`, not database content. Dual implementation: server checks disk with fallback to `unknown_sprite.png`; WASM constructs URLs optimistically. Theme selection UI also lives here. | `ISpriteReadService`, `ServerSpriteReadService`, `WasmSpriteReadService`, `Theme` entity |
| `Stories/` | The largest cluster. Covers the Story entity and its vertical partitions, the full story lifecycle (create, edit, publish, status transitions), story arcs, series, story-to-story relationships, view count tracking. In-place editing UX with shared form components. View count ping endpoint and its future Redis worker also originate here. Series (grouping stories) and StoryArcs (grouping chapters within a story, which is a property of the story) belong here. | `Story`, `StoryListing`, `StoryDetail`, `StoryArc`, `Series`, `SeriesEntry`, `StoryRelationship`, `IStoryReadService`, `IStoryWriteService`, `ServerStoryReadService`, `ServerStoryWriteService`, `StoryListingDto`, `StoryDetailDto`, `StoryCreateDto`, `StoryEditDto`, `IEditableStoryProperties`, `StoryMappers`, `StoryEndpoints`, `StoryPropertiesForm.razor`, `StoryPage.razor`, `StoryDesktop.razor`, `StoryMobile.razor` |
| `Chapters/` | Chapter creation, versioning (live alternate versions, not revision history), WYSIWYG editing, HTML sanitization, and the reading experience. Reading progress tracking folds in because it's triggered from the chapter reading page — `UserChapterInteraction` is mutated while reading chapters, not from a standalone UI. | `Chapter`, `ChapterContent`, `UserChapterInteraction`, `IChapterReadService`, `IChapterWriteService`, `ServerChapterReadService`, `ChapterContentDto`, reader settings application logic |
| `Tags/` | Staff-managed tag CRUD, story tagging (the `StoryTag` junction), character tagging with OC support, character relationships (romantic/platonic pairings), tag display with sprites, tag filtering with typeahead, and saved tag selections. Admin, author, and user-facing methods coexist with different authorization gates. Includes the Tag Directory page. | `Tag`, `StoryTag`, `StoryCharacter`, `StoryCharacterRelationship`, `StoryCharacterRelationshipMembers`, `SettingDetails`, `SavedTagSelection`, `SavedTagSelectionEntry`, `ITagReadService`, `ITagWriteService`, tag selection typeahead component, `TagDirectoryPage` |
| `UserStoryInteractions/` | The `UserStoryInteraction` boolean-column hot table and its two vertical partitions (`UserStoryInteractionDate`, `UserStoryRecommendationSource`). Write operations (toggle favorite, follow, ignore, etc.) and the corresponding read operations that produce interaction-filtered story lists ("My Favorites," "In Progress," etc.). The verbose folder name distinguishes this from the many other kinds of user interactions (comments, recommendations, follows) that live in their own folders. Note: some read methods may return `StoryListingDto` (a type from `Stories/`) — those read methods may live on `IStoryReadService` in `Stories/` depending on what they primarily query. Bookshelves page components also live here. | `UserStoryInteraction`, `UserStoryInteractionDate`, `UserStoryRecommendationSource`, `IInteractionWriteService`, `ServerInteractionWriteService`, interaction toggle endpoints, `BookshelvesPage.razor` |
| `Following/` | User-to-user following and vouching. Distinct from story interactions — `FollowedUser` is a separate entity from `UserStoryInteraction`. Alert toggle (bell icon) and the 5-vouch-per-user limit live here. | `FollowedUser`, `IFollowingReadService`, `IFollowingWriteService`, `ServerFollowingWriteService` |
| `Profiles/` | User profile display (public-facing), user settings editing (self-referential), and the `UserStats` denormalized counter table. Follows the standard CQRS-lite pattern (unlike `Identity/`). `IUserProfileReadService` serves the public profile page (many readers, one writer). `IUserSettingsService` is the one exception — integrated read+write because the reader and writer are always the same person. UserStat recalculation worker also lives here. | `UserProfile`, `UserStats`, `IUserProfileReadService`, `IUserSettingsService`, `ServerUserProfileReadService`, `ServerUserSettingsService`, `UserProfileDto`, `UserStatsDto`, UserStat recalculation `IHostedService` |
| `Comments/` | All four TPT comment types, threading via `ParentCommentId`, comment likes (no `DateLiked`, no notifications — anti-addictive), the "[Deleted Comment]" orphan handling, and spoiler comment gating. Comment posting triggers notification and badge side-effects via cross-cutting patterns. | `BaseComment`, `ChapterComment`, `UserProfileComment`, `GroupComment`, `BlogPostComment`, `CommentLike`, `ICommentReadService`, `ICommentWriteService`, `ServerCommentWriteService`, `CommentDto` |
| `Recommendations/` | Recommendation submission, display, author spotlighting (highlight up to 5), Hidden Gem designation (5-per-user limit), and the recommendation attribution system (tracking which recommendation led a reader to a story, with "Was this useful?" popup after Chapter 1). | `Recommendation`, `RecommendationDetail`, `RecommendationSuccess`, `RecommendationLike`, `IRecommendationReadService`, `IRecommendationWriteService`, `ServerRecommendationWriteService`, `RecommendationDto` |
| `Searches/` | The different mechanisms for surfacing stories. Text-based FTS (tsvector + GIN on `StoryListing`), the search page (Source=All), manual tree search (stateless pivot queries), and the post-beta features — automatic tree search (recursive CTE against data mart) and Also Favorited / Also Recommended (collaborative filtering display). Layer 8 data mart workers (tree search rebuild, AlsoFavorited/AlsoRecommended rebuild) live here as `IHostedService` classes because they produce the data these search services consume. Search services *consume* filter state from `Filtering/` but don't manage filter definitions. | `ITextSearchService`, `IRandomSearchService`, `ITreeSearchService`, tree search data mart rebuild worker, AlsoFavorited rebuild worker, AlsoRecommended rebuild worker, `UserStoryTreeSearchEntries` (raw SQL, no EF model), `AlsoFavoritedScore` (raw SQL), `AlsoRecommendedScore` (raw SQL), search result page components, `ResultsFilterPanel`, `StoryCard`, `StoryDeck` |
| `Filtering/` | The different mechanisms for narrowing which stories appear in search results. User-level overrides of per-search-mode defaults (`UserSearchSetting`), custom inclusion/exclusion rules based on personal lists, public lists, groups, and group folders (`UserCustomFilter`), and the filter configuration UI where users manage these rules. Lookup tables for search modes and interaction filters live in `Lookups/`; this folder owns the user-facing override layer. Distinct from `CustomLists/` — that folder is about personal story collections ("organize my reading"), this folder is about search-result narrowing ("control what search shows me"). | `UserSearchSetting`, `UserCustomFilter`, `IFilterConfigReadService`, `IFilterConfigWriteService`, filter configuration panel components |
| `BlogPosts/` | Blog post creation and display across TPT types (profile-linked, story-linked, group-linked). Polls fold in here because they're attached to blog posts or site-wide announcements, not standalone. Feature Contributions (admin attribution for accepted community suggestions via the Site Development group) also fold in. | `BaseBlogPost`, `ProfileBlogPost`, `GroupBlogPost`, `BasePoll`, `SitePoll`, `BlogPostPoll`, `PollOption`, `PollVote`, `FeatureContribution`, `IBlogPostReadService`, `IBlogPostWriteService` |
| `Groups/` | Group creation, member management (Member/Moderator/Admin roles), story addition, and nested folder browsing. Content rating enforcement based on group type (Standard, SFW Only, Mature). | `Group`, `GroupMember`, `GroupStory`, `GroupFolder`, `GroupFolderGroupStory`, `IGroupReadService`, `IGroupWriteService`, `ServerGroupReadService` |
| `Notifications/` | Notification creation, display (grouped by category, sorted by date, mark as read), user notification settings (sparse override model), and the notification cleanup worker (delete read notifications older than 60 days). The cross-cutting notification generation pattern — how write services in other folders trigger notification creation — is defined here and consumed everywhere. | `Notification`, `UserNotificationSetting`, `INotificationReadService`, `INotificationWriteService`, `ServerNotificationWriteService`, notification cleanup `IHostedService`, `NotificationDto` |
| `Moderation/` | Content reporting, the moderator queue and action workflow, story approval/rejection, and story import verification. SiteDailyStat aggregation also folds in here (admin/operational concern). Report-based auto-flagging via `ActiveReportCount` thresholds. | `Report`, `StoryImport`, `IModerationReadService`, `IModerationWriteService`, `ServerModerationWriteService`, `SiteDailyStat`, SiteDailyStat aggregation `IHostedService` |
| `Messaging/` | Private messaging via the `Conversation`/`ConversationParticipant`/`PrivateMessage` three-table model. Real-time delivery via SignalR. Unread tracking via `LastReadTimestamp`. Respects `PrivacySettings.AllowPrivateMessages`. Uses the full EditorView for rich-text composition. | `Conversation`, `ConversationParticipant`, `PrivateMessage`, `IMessagingReadService`, `IMessagingWriteService`, SignalR hub class, `MessageDto` |
| `Badges/` | Badge definitions (string-key PK), user badge tracking, and the cross-cutting badge award-checking pattern. MVP: synchronous inline checks in service methods reading `UserStats` counters. Future: async worker (a Layer 2 optimization, not Layer 7 — reads from PostgreSQL, not Redis). | `Badge`, `UserBadge`, `IBadgeReadService`, `IBadgeWriteService`, badge-checking logic |
| `CustomLists/` | User-created custom story collections beyond the system lists (which are `UserStoryInteraction` booleans). Public/private toggle, list alerts. Distinct from `Filtering/` — this is about personal reading organization ("I want to group these stories"), not search-result narrowing. Mostly Stage 1 — detailed UI, composition rules, and sharing behavior beyond copy-on-write are open questions per spec §8.11. | Custom list entities (TBD), `ICustomListReadService`, `ICustomListWriteService` |
| `Export/` | Story download/export in standard formats (ePub, PDF). Read-only — no write half. Generates files from existing data using C# libraries. No schema impact. | `IExportService`, epub/pdf generation classes |
| `Spotlight/` | Community donation infrastructure tied to the Community Spotlight feature. This folder covers the donation tracking and pledge management that funds the platform. Schema and detailed design TBD. | Donation tracking entities (TBD), pledge management service (TBD) |

---

## 8. Open Questions

### Actively Open

| # | Topic | Context | Options Under Consideration |
|---|---|---|---|
| 1 | **S-03: Hybrid enum indexing** | Part 2 hybrid enums in `ModelEnums.cs` are 0-indexed, but EF Core uses 0 as sentinel for auto-generated PKs. `SiteRoles` is already 1-indexed. | **Suggested:** Shift hybrid enums to 1-indexed for lookup table PK compatibility. OR configure lookup tables with `ValueGeneratedNever()` and keep 0-indexed. Verify against current migration/seeding code. |
| 2 | **Chapter Arcs UI** | How to present StoryArcs in the reading interface | No proposals yet |
| 3 | **FTS on cold tables** | Whether full-text search should cover UserProfile.ProfileText or StoryDetail.LongDescription | Concern: expensive to maintain tsvector on large text blobs |
| 4 | **Hidden Gem limit enforcement edge case** | What happens when a user has 5 Hidden Gems and wants to move the designation | Remove old first? Swap atomically? |
| 5 | **Password complexity rules** | Beyond ASP.NET Identity defaults | Not yet discussed |
| 6 | **Polls feature detail** | How polls integrate with blog posts and site-wide announcements | Schema exists, UX not designed |
| 7 | **Custom lists detail** | How UserCustomFilter rules compose (AND/OR) and the limits on list complexity | Not yet designed |
| 8 | **Multi-author chapters** | Whether ChapterContent.AuthorId is sufficient or whether a junction table is needed for true co-authorship | Relevant for collaborative writing |
| 9 | **Mapping library** | Manual mapping (`StoryMappers.cs`) vs AutoMapper vs Mapster | Current: manual. Decision deferred. |
| 10 | **Blog post comment cross-TPT query** | How to query "all comments on all blog posts" efficiently across `ProfileBlogPost` and `GroupBlogPost` derived tables | May need a view or denormalized flag |
| 11 | **Error handling strategy** | Three dimensions identified but not yet designed: (1) API error envelope (`ProblemDetails`-based responses from endpoints), (2) Global Blazor error boundary (`<ErrorBoundary>` in the layout), (3) Client-side HTTP error handling (how client services translate non-2xx responses). Technical plumbing (1–2) is independent of visual design. Should be decided before implementation. Error presentation inherits the design language and can wait for Tailwind config. | No proposals yet. Step 2 deliberation identified the gap. |
| 12 | **Guest/logged-out spoiler behavior** | Logged-out visitors have no `UserStoryInteraction` row, so spoiler-reveal gating has no `IsCompleted` signal. Gating behind account creation is a legitimate community-joining nudge (not predatory — gating community content behind community membership, not site-produced content behind payment). | Deferred past MVP. The first beta requires accounts anyway. |
| 13 | **Vouch schema promotion** | Whether to promote Vouch to its own junction table with optional `VouchText` (`MaxLength(280)`) rather than keeping `IsVouched` as a boolean on `FollowedUser`. Rationale for promotion: if vouches are high-signal actions from established authors spotlighting newcomers, a brief text note increases discoverability value. The 5-per-user limit already signals these should be deliberate. | Schema change vs. keep current boolean. Display asymmetry (decided separately in §5.8) is independent of this question. |

### Resolved (Previously Open)

| # | Topic | Resolution | Resolved By |
|---|---|---|---|
| 1 | Email provider | SendGrid (prod), IdentityNoOpEmailSender (dev) | Code Assist implementation |
| 2 | Zero-downtime cache refresh | Atomic `ALTER TABLE ... RENAME` in PostgreSQL transaction | Code Assist PostgreSQL confirmation |
| 3 | Recursive tag/folder hierarchies | Tags: one level deep (ParentTagId). Folders: support nesting. | Step 2 scoping |
| 4 | MAUI support | Architecture supports it via `IDeviceDetectionService`. JS Interop won't work. Post-launch. | Code Assist analysis |
| 5 | Redis implementation | Write-behind queue, INCR view counts, ephemeral LastReadDate, decorator caching | Web Chat design |
| 6 | Hosting & deployment | DigitalOcean Droplet + Managed PostgreSQL, Cloudflare CDN/R2 | Web Chat decision |
| 7 | Search implementation | PostgreSQL tsvector + GIN on StoryListing.SearchVector | Web Chat design |
| 8 | File/image storage | Cloudflare R2 (prod), MinIO via Aspire (dev) | Web Chat design |
| 9 | Also Favorited computation | Self-join on IsFavorite, co-occurrence matrix, daily rebuild, Redis Top 100 | Web Chat algorithm |
| 10 | Managed vs self-managed PostgreSQL | DigitalOcean Managed recommended | Web Chat decision |
| 11 | Monetization model | No ads, no premium tiers. Community donations tied to Community Spotlight only. | June 2026 philosophy update |
| 12 | CSS methodology | Tailwind CSS via `@apply` in component-scoped `<style>`. Chosen over component libraries (MudBlazor, Radzen) to avoid coupling, vendor risk, and bloated JS payloads. Over raw CSS for utility consistency, responsive prefix conventions, and design-token discipline. See §2.1, §6.16. | Step 2 deliberation |
| 13 | UI layer split | Layer 3 (Logic) / Layer 3.5 (Structure) / Layer 4 (Style) three-tier split. Previously two tiers (3 and 4 only). Split introduced because the composition skeleton and the visual styling are distinct concerns that change for different reasons. See §3.3 revision note. | Step 2 deliberation |
| 14 | Reading status booleans | `HasStarted` / `IsCompleted` / `IsIgnored` three-boolean system replacing previous `IsInProgress` / `IsCompleted` / `IsActivelyReading` design. See §4.1 `UserStoryInteraction` table, §5.12, §6.17. Full displaced-ideas reasoning in companion document `UserStoryInteractions_ReadingStatus_Design.md`. | Step 2 deliberation + dedicated design session |
| 15 | Search architecture | Three-Axis model (Source / Filter / Sort) replacing SearchMode-as-source lookup. FTS treated as a filter, not a source. See §5.3, §6.18. | Step 2 deliberation |
| 16 | User Favoriting | Deliberately omitted. "Favorite users" would create a public popularity metric, conflicting with the anti-addictive philosophy. User-to-user signal is expressed through Following (private counts) and Vouches (deliberate, limited). See §5.5. | Step 2 deliberation |
| 17 | Implementation ordering | Three-Phase model (Atoms → Integration Points → Consumers). Specification flows top-down; implementation flows bottom-up. The phase system sequences when to work on features; the stage system (1–6) describes the current state of each cell. See §9.2. | Step 2 deliberation |

---

## 9. Implementation Roadmap

### 9.1 Layer-First, Then Feature-by-Feature

The implementation follows the 8-layer architecture. Within Layers 1–4 (the MVP), work proceeds feature-by-feature in dependency order. Each feature is built through its layers before moving to the next feature.

### 9.2 Three-Phase Implementation Ordering

> **New in v4.** Step 2 deliberation refined the dependency-ordered sequence (§9.3) into a three-phase model that accounts for component reuse across features. The original dependency graph remains correct for data-model ordering; the phase model layers on a component-level build sequence.

**Specification flows top-down.** Knowing what pages need tells you what composites must exist. Knowing what composites need tells you what atoms must exist. Knowing what atoms need as parameters tells you what DTOs must contain.

**Implementation flows bottom-up.** Atoms must compile before composites reference them. Composites must compile before pages compose them. The implementation order is the reverse of the specification order.

**Phase 1 — Atoms.** Components and services consumed by many features but consuming nothing feature-specific. Building blocks that are depended on by everything and blocked by nothing.

- Sprites L2 (ISpriteReadService)
- Tags L2 + TagChip + TagSelector
- EditorView + RichTextView
- PaginationControls, UserCard, UserStoryInteractionButton

**Phase 2 — Integration Points.** Features connecting to many others simultaneously. Consume Phase 1 atoms, produce surfaces Phase 3 embeds.

- Stories L2 + StoryCard + StoryDeck
- UserStoryInteractions L2 + StoryInteractionPanel
- Chapters L2 + ChapterNavigation + reading experience
- Comments (CommentSection uses EditorView from Phase 1)
- Following/Vouches
- Notifications

**Phase 3 — Consumers and Endpoints.** Aggregate Phase 2 outputs. Can be built in any internal order.

- Page dispatchers (StoryPage, ChapterPage, SearchPage, etc.)
- Search/Discovery, Recommendations, Profiles
- Groups, BlogPosts, Moderation, Messaging, Badges

**Post-beta:** Automatic tree search data mart, Also Favorited/Recommended co-occurrence, SiteDailyStat. Requires real user data.

**Displaced:** Pure layer sweeps (all L1 then all L2 then all L3). The diagonal/wave approach validates services by their consuming components. Also displaced: pure feature-vertical completion (can't finish story page without tags, interactions, and comments existing).

### 9.3 Dependency-Ordered Feature Sequence

Features are ordered so that no feature depends on one that hasn't been built yet. This is the data-model dependency graph; the three-phase model (§9.2) refines it with component-level sequencing.

**Foundation (no dependencies):**
- User/Auth (Identity, roles, login/register)
- Theme (lookup table, FK from User)

**Core content (depends on User):**
- Story lifecycle (create, edit, publish, status transitions)
- Chapter system (content, ordering, publishing)
- Tags (staff-managed, tag types, story-tag junction)

**Interactions (depends on Story):**
- UserStoryInteraction (favorites, follows, reading status)
- UserChapterInteraction (read progress, completion)
- View count tracking
- Reading progress / LastReadDate

**Community (depends on User + Story):**
- Following / Vouches
- Comments (TPT, threading, likes)
- Recommendations (with Hidden Gem, Successful Rec)
- Blog posts (profile + group, with comments)

**Organization (depends on interactions):**
- Groups (with folders, member roles)
- Series
- Story relationships
- User lists / saved searches

**Discovery (depends on interactions + community):**
- Full-text search
- Manual tree search (stateless pivot queries)
- Tag Directory (dual-use: browse-by-tag and tag information)

**Notifications (cross-cutting, depends on all above):**
- Notification types + settings
- Notification creation wired into all relevant service methods

**Moderation (cross-cutting):**
- Reports + moderator workflow
- Role-based authorization across all features

**Post-MVP (Layers 5–8):**
- WASM enablement (API endpoints + client services)
- SQL indexes (filtered, composite, GIN)
- Redis integration (write-behind, caching, LastReadDate)
- Data mart workers (TreeSearch, AlsoFavorited, AlsoRecommended, SiteDailyStat)

**Post-beta (needs real user data):**
- Automatic tree search
- Also Favorited / Also Recommended display
- Community Spotlight

### 9.4 Cross-Cutting Concerns

These touch many features and are woven in during implementation rather than built as standalone features:

| Concern | What It Affects | Pattern |
|---|---|---|
| **Content rating filtering** | Every read service returning story data | WHERE clause + `User.ShowMatureContent` check |
| **Notification creation** | Every user-facing action | Service method calls `INotificationService.CreateAsync()` at end of action |
| **Badge checks** | Service methods for qualifying actions | Inline synchronous check (MVP); async worker later |
| **UserStats updates** | Writes to stories, chapters, comments, recommendations | Increment/decrement counters in same transaction |

### 9.5 Identity Layer Exceptions

Identity pages (login, register, 2FA, email confirmation) are permanent exceptions to the layer model:
- They live in the Server project, not SharedUI (need `UserManager`, `SignInManager`, `HttpContext`).
- They use form-POST-to-endpoint, not `@onclick` → service call (because `SignInManager` sets auth cookies via `HttpContext.Response`, which isn't available inside an active Blazor circuit).
- They are Layer 4 (presentation) but are permanently N/A for Layer 5 (WASM).

### 9.6 Audit and Process

> **New in v4.** Step 2 deliberation established the feature tracking infrastructure that makes the implementation roadmap executable.

**Feature tracking files.** Location: `.claude/audit/<FolderName>.md` — one per folder cluster from the folder inventory (§7.4). Each file opens with shared context (entities, services, components, current file locations) then per-feature sections with per-layer stage classifications. Not in source folders because each conceptual folder (e.g., `Stories/`) exists in four projects (Core, SharedUI, Server, Client). `.claude/audit/` provides a single canonical location.

**Stage system.** Stages 1–6 per CLAUDE.md definitions, plus N/A for cells where a layer doesn't apply. No "Blocked" stage. Cells blocked on prerequisites (e.g., L4 Style blocked on design tokens) are classified by their actual state (Stage 2 — intent settled, can't build yet) with a note naming the blocker. The workplan handles sequencing; stages describe state.

**Stages vs. workplan.** The stage system (1–6) describes the state of each cell. The workplan describes when to work on it, ordered by the phase/wave dependency graph (§9.2). A cell at Stage 2 that depends on unbuilt dependencies is correctly Stage 2 — the workplan handles sequencing. These are different systems for different purposes.

### 9.7 Skill File Organization

The conventions established during this process are maintained as Claude Code skill files:

```
.claude/skills/canalave-conventions/
  SKILL.md                    — Hub file: triggers, pointers, cross-references
  layer1-data-model.md        — EF Core entities, Fluent API, migrations
  layer2-services.md          — Service interfaces, CQRS split, DTOs, DbContext
  layer3-logic.md       — @code blocks, state, PersistentState, EditForm
layer3.5-structure.md
  layer4-style.md      
  layer5-wasm.md              — API endpoints, client services
  layer6-indexes.md           — Filtered, composite, GIN indexes
  layer7-redis.md             — Write-behind, caching, ephemeral storage
  layer8-data-marts.md        — Background workers, table swap, raw SQL
  cross-cutting.md            — Notifications, badges, UserStats, content rating
```

These files are living documents — updated as implementation experience reveals corrections or additions.
