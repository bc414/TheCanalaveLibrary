# Cross-Cutting Concerns

These touch many features and are woven into implementation rather than built as standalone features.
Also covers global architectural decisions that span multiple layers.

## Render Mode: Global InteractiveAuto

Set the render mode **once**, on `<Routes>` and `<HeadOutlet>` in `App.razor` — not on `<RouteView>` in
`Routes.razor`, and not per-component:

```razor
@* Routes.razor — no @rendermode here *@
<RouteView RouteData="routeData" DefaultLayout="typeof(DeviceLayout)" />
```

Identity pages (`Identity/Pages/_Imports.razor`) carry `@attribute [ExcludeFromInteractiveRouting]` —
they need a real per-request `HttpContext` for `SignInManager`/cookie auth, which an interactive
circuit doesn't have. `App.razor` must read that via `HttpContext.AcceptsInteractiveRouting()`, not
hardcode the render mode — confirmed current for .NET 9/10/11 against `render-modes.md`:

```razor
@* App.razor *@
<HeadOutlet @rendermode="PageRenderMode" />
...
<Routes @rendermode="PageRenderMode" />

@code {
    [CascadingParameter]
    private HttpContext HttpContext { get; set; } = default!;

    private IComponentRenderMode? PageRenderMode
        => HttpContext.AcceptsInteractiveRouting() ? InteractiveServer : null;
}
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

## Active-User Context

`IActiveUserContext` (Core/Identity/) is the scoped "who is the current viewer" companion to the `User`
entity — minted WU12, because the content-rating filter below needs a per-request source and no such
abstraction existed yet. Holds only hot scalar fields, never the full entity (defeats the hot/cold
partition design and the DTO Firewall):

```csharp
public interface IActiveUserContext
{
    int? UserId { get; }              // null = anonymous
    bool IsAuthenticated { get; }
    bool ShowMatureContent { get; }    // feeds the content-rating filter below
    string Theme { get; }              // feeds ISpriteReadService.GetSpriteUrl
    bool PrefersAnimatedSprites { get; }
    bool IsModerator { get; }          // query-shaping hint only — NOT the auth authority
    bool IsAdmin { get; }
}
```

`ServerActiveUserContext` (Server/Identity/) is scoped, populated once per circuit from
`AuthenticationState`/claims. `IsModerator`/`IsAdmin` exist only to decide when a query legitimately
calls `IgnoreQueryFilters` or shows admin/author UI — real access control stays with `AuthorizeView`/
policies, not this context.

**What stays out, deliberately:** display name/avatar URL (presentation — comes via `UserCardDto` per
view); `ReaderDisplaySettings` (already a separate cascading slim bag, a UI-layer concern — see
`layer3.5-structure.md` "Ambient Viewer Settings via Cascading Slim Bags"); notification/messaging
prefs (feature-local, read where needed). Collapsing those back into this context would make it grow
without bound.

Two consumers justify the abstraction independently: the content-rating query filter (below) and
sprite resolution's `theme`/`animated` arguments (`layer2-services.md` "Sprite URLs Are Resolved
Server-Side") — both previously had no defined source for "the current user."

## Content Rating Filtering

Every read service returning story data must filter by content rating. **Settled (WU12): this is a
global EF Core named query filter, sourced from `IActiveUserContext` — not a per-method `.Where`.**
A per-method filter only holds "no trace anywhere" as strong as every future read method remembering to
add it; a model-level filter makes it a property of the model. `ApplicationDbContext` takes
`IActiveUserContext` in its constructor and closes over the resulting ceiling:

```csharp
public class ApplicationDbContext : DbContext
{
    private readonly Rating _maxRating;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IActiveUserContext activeUser)
        : base(options)
    {
        _maxRating = activeUser.ShowMatureContent ? Rating.M : Rating.T;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Story>()
            .HasQueryFilter("ContentRating", s => s.Rating <= _maxRating);
    }
}
```

Anonymous viewers get the `Rating.T` ceiling (never `IsAuthenticated` ⇒ never `ShowMatureContent`).
`User.ShowMatureContent` is a hot boolean on the User table — direct column, not in jsonb. This is the
most frequently evaluated filter in the system.

Mod/author/admin read paths that must see all ratings call `IgnoreQueryFilters` **by name** — EF Core 10
named filters let this opt-out target only the content-rating filter, leaving any other named filter
(e.g. a future soft-delete filter) intact on the same query:

```csharp
// Mod queue / author viewing their own unpublished mature story:
var allRatings = await context.Stories
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

### Authorization Has Two Enforcement Surfaces — Neither Substitutes for the Other

**Page-level (UX gate):** `AuthorizeRouteView` in `Routes.razor` reads `[Authorize]`/`[AllowAnonymous]`
on the matched `@page` component and decides whether to render it. **`Routes.razor` must use
`AuthorizeRouteView`, never plain `RouteView`** — `RouteView` silently ignores authorization
attributes entirely, making any `[Authorize]` declared anywhere in the app a no-op.

**Endpoint-level (the actual security boundary):** minimal-API route groups (`StoryEndpoints.cs` and
similar) need their own `.RequireAuthorization(...)`, independent of whatever the Blazor router does.
The WASM Client calls these endpoints directly over HTTP — gating a page never gates the data it
fetches. Every endpoint group's authorization must be set deliberately; it does not inherit from the
page that happens to call it.

`[Authorize]`/`[AllowAnonymous]` are not gates on non-routable child components — they only affect
the type matched by the router. To gate part of an otherwise-public page (e.g., a moderator-only
edit button on a public `StoryPage`), use `<AuthorizeView Roles="...">` around just that markup, not
a page-level attribute.

### Default-Deny for MVP, Default-Allow Post-Launch

**MVP posture (everything requires login):**
```csharp
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});
```
This makes login the default for every page and endpoint with no explicit attribute. The
pre-authentication Identity flow (`Login`, `Register`, `ForgotPassword`, `ResetPassword`,
`ConfirmEmail`, `ExternalLogin`, etc. — everything under `Identity/Pages` excluding `Manage`) must
opt out via a single `@attribute [AllowAnonymous]` on `Identity/Pages/_Imports.razor`, which cascades
to the whole pre-auth flow at once. `Identity/Pages/Manage/_Imports.razor`'s existing `[Authorize]`
becomes redundant under the fallback policy but is harmless to keep — it documents intent.

**Post-MVP posture (public browsing, gated actions):** remove the fallback policy (flips the default
back to allow) and add `[Authorize]` explicitly only where login is actually required (`Bookshelves`,
`Messaging`, mod pages, posting/writing flows). For pages that mix public viewing with login-gated
actions, keep the page open and use `<AuthorizeView>` around the gated controls instead of gating the
whole route. This is a posture flip, not an additive patch — re-audit every endpoint's
`.RequireAuthorization()` at the same time, since the two surfaces must move together.

### Role-Based (Moderator) Gating

Same `[Authorize]` mechanism, parameterized:
```razor
@* On a _Imports.razor at the root of a mod-only folder, e.g. mod/_Imports.razor *@
@attribute [Authorize(Roles = "Moderator,Admin")]
```
Prefer a named policy over repeating role lists once more than one or two pages need it:
```csharp
options.AddPolicy("RequireModerator", p => p.RequireRole("Moderator", "Admin"));
```
`[Authorize(Policy = "RequireModerator")]` / `.RequireAuthorization("RequireModerator")` then apply
uniformly to mod pages and their backing endpoints. Distinct from `<AuthorizeView Roles="...">`
(layer3.5-structure.md) — that's for moderator-only controls embedded in an otherwise-public page,
not for gating the dedicated `/mod/*` routes.

**Gap — role infrastructure is not yet real:** `ApplicationRole` is currently a bare
`IdentityRole<int>` with nothing added, and `DataSeeder.cs` only ever assigns `"Admin"` (with a
comment merely *assuming* it's been seeded, not guaranteeing it). No `"Moderator"` role exists yet.
Role-based gating above is the intended pattern but is not functional until roles are actually
created and assigned (`RoleManager.CreateAsync`, `UserManager.AddToRoleAsync`).

## Rich Text & Sanitization

All user-submitted HTML is sanitized **server-side** with `HtmlSanitizer` (allow-list) before saving.
Never trust client sanitization, never persist raw user HTML.

**EditorView** (universal across all text surfaces): chapters, comments, author notes, descriptions,
recommendations, profile bios, blog posts, AND private messages. Desktop shows full toolbar; mobile
shows compact toolbar with overflow for less-used formatting **(deferred — WU6 shipped desktop only;
not MVP-blocking, see `layer3.5-structure.md` "Third-Party Wrapper Composite")**.

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

## Dev-Only Diagnostic Endpoints

When a code path is hard to exercise through the real UI/auth flow during local verification
(e.g. an operation scoped to "the currently authenticated user," which would otherwise require
logging in as a throwaway fixture user), add a Development-only minimal-API endpoint that calls
the service method directly instead of reaching for a one-off temporary endpoint inline in
`Program.cs`.

**Home:** `TheCanalaveLibrary.Server/Endpoints/DevDiagnosticsEndpoints.cs`
(`MapDevDiagnosticsEndpoints`), same `{Feature}Endpoints.Map{Feature}Endpoints` shape as
`StoryEndpoints`. Mapped exactly once, inside the existing `if (app.Environment.IsDevelopment())`
block in `Program.cs` — never reachable outside local dev. Add new diagnostic routes to this one
file rather than creating new ad-hoc endpoint files or inlining lambdas in `Program.cs`; it's the
single auditable place reviewers (and future agents) check for "what dev-only backdoors exist."

See `.claude/skills/run-server/SKILL.md` "Dev diagnostics endpoints" for the verification workflow
(pairs with direct `psql` fixture setup/assertions).

## Error Handling Strategy (Gap — Not Yet Designed)

Three dimensions identified but not fully designed:
1. **API error envelope:** `ProblemDetails`-based responses from endpoints.
2. **Global Blazor error boundary:** `<ErrorBoundary>` in the layout.
3. **Client-side HTTP error handling:** how client services translate non-2xx responses.

`NavigationManager.NotFound()` (.NET 10) addresses the 404 case specifically. The remaining
error presentation inherits the design language and can wait for Tailwind config.
