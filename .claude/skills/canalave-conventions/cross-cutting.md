# Cross-Cutting Concerns

These touch many features and are woven into implementation rather than built as standalone features.
Also covers global architectural decisions that span multiple layers.

## Render Mode: Global InteractiveAuto

Set the render mode **once**, on `<RouteView>` in `Routes.razor` — not per-component:

```razor
<RouteView RouteData="routeData" DefaultLayout="typeof(DeviceLayout)"
           @rendermode="RenderMode.InteractiveAuto" />
```

**Dev shortcut (spec-sanctioned):** during active development, use `InteractiveServer` globally
(faster debugging, no API controllers needed yet). Switch to `InteractiveAuto` when shipping WASM.

**Two valid syntaxes for render mode directives:**

```razor
@* Full form — always works *@
@rendermode RenderMode.InteractiveServer

@* Shorthand — requires @using static Microsoft.AspNetCore.Components.Web.RenderMode
   in _Imports.razor (Blazor templates include this by default) *@
@rendermode InteractiveServer
```

Both are correct. The Blazor template's `_Imports.razor` includes the static using, so the shorthand
works out of the box. Use whichever is clearer in context; be consistent within the project.

**Assembly discovery:** Both the Router (`Routes.razor`) and `app.MapRazorComponents` must know
about external assemblies via `AdditionalAssemblies`. Marker components
(`SharedUIAssemblyIdentifier.razor`, `WasmClientAssemblyIdentifier.razor`) provide stable type references.

**ReconnectModal:** Must be conditionally rendered only when an interactive render mode is active,
checked via `HttpContext` endpoint metadata in `App.razor`.

**Blazor .NET 10:** `blazor.web.js` dropped from 183 KB to 43 KB (76% reduction). Served as a
static asset with automatic compression and fingerprinting. No code changes required.

## NavigationManager.NotFound() (.NET 10)

New in .NET 10: call `NavigationManager.NotFound()` from any component to return a 404. The
framework redirects to the designated Not Found page. Use in page dispatchers when the requested
entity doesn't exist (story not found, invalid slug, user not found).

```csharp
@inject NavigationManager Nav

protected override async Task OnInitializedAsync()
{
    var story = await StoryService.GetDetailAsync(StoryId);
    if (story is null)
    {
        Nav.NotFound();
        return;
    }
    // ...
}
```

Replaces manual navigation to error pages or returning empty state with a loading spinner.

## JS Interop Improvements (.NET 10)

**Direct property access** — read and write JS object properties from C# without wrapper functions:

```csharp
var width = await JS.GetValueAsync<int>("window.innerWidth");
```

**`InvokeConstructorAsync`** — instantiate JS objects directly from C#.

**`CancellationToken` support** — all async JS interop methods accept cancellation tokens for
timeout control.

Relevant for: device detection (`isMobile()` call), Quill.js editor integration, scroll tracking.

## Device Detection & Layout Architecture

Dual implementation behind `IDeviceDetectionService`:

| Implementation | Project | Mechanism |
|---|---|---|
| `ServerDeviceDetectionService` | Server | `User-Agent` via `IHttpContextAccessor` (SSR) |
| `WasmDeviceDetectionService` | Client | `isMobile()` JS via `IJSInProcessRuntime` (sync) |

JS file: `SharedUI/js/device.js`, loaded in `App.razor`. Uses `window.matchMedia` at 768px breakpoint.

**Layout routing:** `DeviceLayout.razor` inherits `LayoutComponentBase`, injects
`IDeviceDetectionService`, conditionally renders `DesktopLayout.razor` or `MobileLayout.razor`.
Set as `DefaultLayout` in `Routes.razor`.

**Persistent layout:** Desktop top bar (logo, nav links, notification bell, profile).
Mobile hamburger menu. Notification bell injects `INotificationReadService` directly —
legitimate cross-cutting injection. Login/logout are triggers on the persistent layout,
not separate navigation targets.

## Content Rating Filtering

Every read service returning story data must filter by content rating:

```csharp
.Where(s => s.Rating <= (currentUser.ShowMatureContent
    ? Rating.Mature
    : Rating.Teen))
```

`User.ShowMatureContent` is a hot boolean on the User table — direct column, not in jsonb.
This is the most frequently evaluated filter in the system.

**Named query filters (EF Core 10)** are a natural fit:

```csharp
modelBuilder.Entity<Story>()
    .HasQueryFilter("ContentRating", s => s.Rating <= maxRating);

// Admin query that needs to see all ratings:
var allStories = await context.Stories
    .IgnoreQueryFilters(["ContentRating"])
    .ToListAsync();
```

## Notification Creation

Every user-facing action that produces a notification calls `INotificationService.CreateAsync()`
at the end of the service method. The notification service is injected into the feature service.

Pattern: feature write method completes its primary work, then fires notification creation.
Notification logic does NOT block the primary operation.

9 categories, ~35 types with gap-based numbering. `default_collapsed` is required non-nullable
on all types.

**Sparse override model:** `UserNotificationSetting` only stores rows where the user differs
from `NotificationType.DefaultEmailEnabled`. Query: LEFT JOIN settings onto types;
NULL means "use default."

## Badge Checks

**MVP:** synchronous inline checks in service methods after qualifying actions.
Service reads `UserStats` counters and awards badge if threshold met.

```csharp
if (userStats.StoryCount >= 1 && !await HasBadgeAsync(userId, SiteBadges.FirstStory))
    await AwardBadgeAsync(userId, SiteBadges.FirstStory);
```

**Future:** async worker scans for newly qualified users. This is a Layer 2 optimization
(reads from PostgreSQL), not Layer 7 (Redis).

Badge keys are string constants in `SiteConstants.cs`:
```csharp
public static class SiteBadges
{
    public const string FirstStory = "first-story";
    public const string WordSmith100K = "100k-words";
}
```

## UserStats Updates

22+ denormalized counter fields. Updated in real-time by application logic within the same
transaction as the primary write:

```csharp
await writeDb.UserStats
    .Where(us => us.UserId == story.AuthorId)
    .ExecuteUpdateAsync(s => s.SetProperty(us => us.StoryCount, us => us.StoryCount + 1));
```

Background worker periodically recalculates to correct drift.

## Identity & Auth

`AddIdentityCore<User>().AddRoles<ApplicationRole>()` with `int` keys.
Cookie auth via `AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()`.
Configured for **401/403 status codes, not 302 redirects** (critical for WASM API calls).
`RequireConfirmedAccount = true`.

Data Protection: `PersistKeysToFileSystem` (dev), `PersistKeysToDbContext` (prod).
Email: `IdentityNoOpEmailSender` (dev), SendGrid (prod, conditionally registered).

**Identity pages are permanent exceptions to the layer model:**
- They live in the Server project, not SharedUI.
- They use form-POST-to-endpoint, not `@onclick` → service call.
- They are Layer 4 (presentation) but permanently N/A for Layer 5 (WASM).
- Login/logout are triggers on the persistent layout, not separate navigation targets.
- Settings route (`/settings`) follows the standard Blazor component pattern (separate from
  Identity's `/account` routes).

## Rich Text & Sanitization

All user-submitted HTML is sanitized **server-side** with `HtmlSanitizer` (allow-list) before saving.
Never trust client sanitization, never persist raw user HTML.

**EditorView** (universal across all text surfaces): chapters, comments, author notes, descriptions,
recommendations, profile bios, blog posts, AND private messages. Desktop shows full toolbar; mobile
shows compact toolbar with overflow for less-used formatting.

## .NET Aspire 13 Configuration

AppHost defines dev containers; it **never deploys**:

```csharp
var postgres = builder.AddPostgres("postgres").AddDatabase("canalavedb");
var redis = builder.AddRedis("redis");
var minio = builder.AddMinIO("minio");

builder.AddProject<Projects.TheCanalaveLibrary_Server>("web")
    .WithReference(postgres)
    .WithReference(redis)
    .WithReference(minio);
```

- **ServiceDefaults** holds shared cross-cutting config (telemetry, health checks, resilience).
- Consume references by **logical name** (`builder.AddNpgsqlDbContext<ApplicationDbContext>("canalavedb")`).
- MinIO uses the **same AWS S3 SDK code** as Cloudflare R2 in prod.

**Dual-Configuration Strategy:** `dotnet ef` CLI cannot see AppHost's configuration. The connection
string must exist in both AppHost's user secrets (runtime) and server project's user secrets
(design-time EF tooling).

## Read Replica Awareness

Reads go to the PostgreSQL read replica; writes hit the primary. Replication is
near-real-time but **eventually consistent** — UI shows optimistic local state for a few seconds
after a write rather than immediately re-reading.

## Delete Policy Summary

- **Content** (stories, comments, blog posts, recs): SET NULL on author → anonymize, preserve.
- **Interaction data** (follows, interactions, badges, settings): CASCADE on user.
- **Lookup tables** (tags, themes, statuses): RESTRICT → cannot delete if in use.
- **Self-references** (parent comments, parent tags, parent folders): SET NULL → children become top-level.

## Error Handling Strategy (Gap — Not Yet Designed)

Three dimensions identified but not fully designed:
1. **API error envelope:** `ProblemDetails`-based responses from endpoints.
2. **Global Blazor error boundary:** `<ErrorBoundary>` in the layout.
3. **Client-side HTTP error handling:** how client services translate non-2xx responses.

`NavigationManager.NotFound()` (.NET 10) addresses the 404 case specifically. The remaining
error presentation inherits the design language and can wait for Tailwind config.
