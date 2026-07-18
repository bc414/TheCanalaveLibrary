# Render Mode, Routing & Layout

Blazor render-mode setup, route-parameter conventions, device-driven layout architecture, and the
ThemeContext cascading bridge that lets SharedUI resolve sprite theme without touching server-only
identity state. Split out of `cross-cutting.md` (2026-07-07) as its own coherent theme.

## Render Mode: Global InteractiveAuto

Set the render mode **once**, on `<Routes>` and `<HeadOutlet>` in `App.razor` — not on `<RouteView>` in
`Routes.razor`, and not per-component:

```razor
@* Routes.razor — no @rendermode here; use AuthorizeRouteView (not RouteView) so [Authorize]
   attributes are honoured (RouteView silently ignores them — see "Authorization" section below) *@
<AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(MainLayout)" />
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

**L5 rollout (settled):** per-feature endpoints + client impls land incrementally (headless,
inert until a WASM pass runs); the render-mode conversion happens **once, globally** — no
long-lived mixed-mode pages. `InteractiveAuto` requires both impls behind every reachable
interface (client cache state decides the runtime; missing client DI = browser crash, no
fallback). A single page CAN run as a WASM island (`[ExcludeFromInteractiveRouting]` +
`@rendermode RenderMode.InteractiveWebAssembly`, both required) — a debugging/staged-rollout
technique only. Strategy, Auto semantics, island recipe, and flip checklist: `layer5-wasm.md`.

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

## Social Meta Tags (Open Graph) — Prerender Requirement (settled 2026-07-11)

Social crawlers (Discord, Twitter/X, Facebook, iMessage, Slack) never execute JavaScript — they
read only the raw HTML of the first response. `<SocialMetaTags>` (`Seo/` cluster) emits Open
Graph/Twitter/description tags via `<HeadContent>`, which routes to the root `<HeadOutlet>` in
`App.razor` regardless of render mode, **as long as the emitting page's data loads during the
server prerender pass** (the same pass `<PageTitle>` already relies on).

**This is not endangered by the global InteractiveServer → InteractiveAuto flip** (Axiom 8 above):
InteractiveAuto still prerenders on the server by default (first visit renders server-side while
the WASM runtime downloads in the background) — a crawler's single unauthenticated GET always
lands on that prerendered HTML either way. **The only thing that breaks crawler visibility is
explicitly setting `prerender: false`** on a shareable page (`StoryPage`, `ChapterReadingPage`,
`ProfilePage`, `SeriesPage`, `BlogPostPage`, `GroupPage`) — don't do that.

**Absolute-URL rule:** `og:url`/`og:image` must be absolute. Resolve them via `IPublicUrlProvider`
(`Seo/`, Core) — **never** `NavigationManager.BaseUri` server-side. The site sits behind Cloudflare
in front of DigitalOcean droplets (heading toward N≥2): request-derived URLs depend on
`ForwardedHeaders` plumbing being exactly right or an internal `http://`/host leaks into
`og:image` and crawlers silently reject the card; a configured base (`Site:PublicBaseUrl`) is the
same canonical value on every droplet by construction. `IPublicUrlProvider.AbsoluteImageUrl`
additionally reads `ImageStorage:PublicBaseUrl` (falls back to the site base when unset) — this is
the same seam that will carry a future direct-R2/CDN image base (see `audit/Seo.md`).

## String-Segment Route Parameters ({Tab}, {*Slug})

**`{Tab}` route convention (WU27):** `/bookshelves/{Tab?}` is the first route in this codebase
that uses a string segment as a tab/mode selector. Pattern:
```razor
@page "/bookshelves/{Tab?}"

[Parameter] public string? Tab { get; set; }

protected override async Task OnInitializedAsync()
{
    var activeTab = BookshelfTabSlug.Parse(Tab);   // null Tab → default (MyStories)
    if (activeTab is null) { Nav.NotFound(); return; }   // invalid slug
    // ... load data ...
}
```
`BookshelfTabSlug.Parse` returns `BookshelfTab?` — `null` for an invalid slug, a valid enum value
for a known slug, and the default (`BookshelfTab.MyStories`) for `null`/empty. Navigation between
tabs is plain `<a href="/bookshelves/{slug}">` links; the router intercepts and re-renders without a
full reload. The `{Tab?}` optional marker lets `/bookshelves` alone navigate to the default tab via
a redirect inside `OnInitializedAsync`.

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

Relevant for: Quill.js editor integration, scroll tracking.

## Responsive Layout Architecture (rewritten 2026-07-18, WU-ResponsiveMerge)

**Single responsive site — one component tree, one DOM, every viewport.** Layout adapts via CSS
only (Tailwind mobile-first, `md:` at 768px, two tiers, no tablet tier — full ladder:
`layer4-style.md` §"Responsive Adaptivity Ladder"). There is **no device detection**: no
`IDeviceDetectionService`, no UA sniffing, no `matchMedia` service, no `{X}Desktop`/`{X}Mobile`
component forks, no dispatcher branching. `MainLayout.razor` is the only layout — single sticky
top bar (wordmark, nav links, chrome group) that wraps gracefully at narrow widths — set as
`DefaultLayout` in `Routes.razor`. Login/logout are triggers on the persistent layout, not
separate navigation targets.

**Why this is settled (recorded so the ratchet isn't rebuilt):** the original Oct-2025 design
(UA-sniffing `ServerDeviceDetectionService` + sync-JS `WasmDeviceDetectionService` +
`DeviceLayout` fork + nine `{X}Desktop`/`{X}Mobile` page pairs) was a *flicker-fix ratchet*:
JS detection fails during static SSR → deferring to `OnAfterRenderAsync` flickers → UA sniffing
cures the flicker → whole-page forks generalize the mechanism. Each step fixed the previous
step's symptom; the premise — C# choosing between component trees before knowing the viewport —
was the actual bug. CSS media queries have no prerender problem (the browser applies them at
paint), so the whole problem class dissolves with the paradigm. The removed design was Google's
legacy "dynamic serving" pattern in component form. Chronicle of the original sessions:
`GeminiDiscussions/7 Building a Responsive and Maintainable Blazor Application.txt`.

**Spec supersession (this axis):** spec §3.9 (device detection), §3.10 (dispatcher pattern), and
the MAUI positioning in §5.5/§5.9 are superseded. Two corrections of record: (1) §5.9's claim
that JS interop doesn't work in MAUI Blazor Hybrid is factually wrong — Hybrid renders in a
platform WebView with a real DOM (sync `IJSInProcessRuntime` is the only WASM-exclusive part);
(2) the native-app direction is **PWA** (rides the L5/WASM flip: manifest/installability first,
offline shell later; store-gated MAUI only if a concrete store/push requirement ever materializes).

**The ONLY sanctioned path back to C#-level viewport logic (rung-3 trigger):** if the future
mobile phase proves a DOM-existence fork necessary — canonical example: a bottom nav bar, since
CSS-hiding two navs would double chrome service calls (`NotificationBell` etc.) — build a
**reactive `ViewportState` cascade**: one root provider subscribing to `matchMedia` change events
via async JS interop, cascading an immutable record to consumers; UA sniff at most as a prerender
seed corrected on first interactive callback. Never UA-sniff for behavior, never fork whole
pages. Until that trigger fires, CSS carries everything.

**Notification bell** (`SharedUI/Notifications/NotificationBell.razor`, WU33) — legitimate cross-cutting injection:
`INotificationReadService` is injected directly into this layout element (N+1 exception; confirmed in
`grid_axes.md`). Rules for this component:
- Wrapped in `<AuthorizeView><Authorized>` — renders only when logged in.
- **Does NOT inject `IActiveUserContext`** — server-only service, will not exist post-WASM-split. The
  underlying read service self-scopes via `IActiveUserContext` internally.
- **UserCard caret pattern** — `relative` container + `@onclick="Toggle"` button (with unread-count badge) +
  `@if (_open)` absolute `top-full z-10` flyout panel. NOT the `fixed inset-0` modal pattern (notifications
  are a glanceable peripheral feed, not a blocking action).
- Panel shows recent `NotificationItem`s + "Mark all read" + "See all → `/notifications`".
- No live push (SignalR is permanently ruled out site-wide — see `cross-cutting.md` §"Private Messaging
  Architecture"); count refreshes on render/navigation.
- Inserted in `MainLayout.razor`'s chrome group.

**Messages nav link** (`SharedUI/Messaging/MessagesNavLink.razor`, WU35) — the second legitimate
cross-cutting layout injection, parallel to the bell: injects `IMessagingReadService` for the unread
badge, wrapped in `<AuthorizeView>`, refreshes on every navigation via a `LocationChanged`
subscription, inserted beside the bell in `MainLayout.razor`.

**Layout-chrome concurrency (applies to both, and to any future chrome component):** these
components render on *every* authenticated page and initialize/refresh **concurrently with each
other and with the page dispatcher's own loads** — Blazor Server interleaves sibling async init on
one circuit-scoped DI scope. Services reachable from layout chrome must therefore follow the
read-context factory rule (`layer2-services.md` §"Read-Context Concurrency: Factory Per Method");
a circuit-scoped `ReadOnlyApplicationDbContext` shared across them crashes every authenticated
load (found via browser debugging 2026-07-01; regression: `Tests.Integration/ConcurrentReadAccessTests.cs`).

## ThemeContext Cascading Provider

Sprite-rendering components in SharedUI need the viewer's theme slug and animation preference but
must **not** inject `IActiveUserContext` (server-only, no WASM impl). The bridge is a lightweight
cascading value fed by the root `ThemeContextProvider` component.

```csharp
// Core/Sprites/ or SharedUI/
public record ThemeContext(string Slug, bool PrefersAnimated);
```

**Provider** (`SharedUI/Sprites/ThemeContextProvider.razor` — moved from `Server/Components/`
in WU-L5Pilot so WASM islands can render it too) — nested directly inside
`CascadingAuthenticationState` in `Routes.razor`, outside `AuthorizeRouteView`. Reads
`canalave:theme` and `canalave:prefers_animated_sprites` claims off the cascaded
`Task<AuthenticationState>`; falls back to `("pokemon", true)` for anonymous users.
Exposes `<CascadingValue Value="@_themeContext">`.

**Why claims, not IActiveUserContext:** claims are present in **both** the prerender pass
(static SSR) and the interactive pass. Because both passes read from the same claim source,
the resolved sprite URL is byte-identical across the SSR→interactive handoff → **no flicker**.
This carries into WASM: the server serializes all claims into the persisted auth state
(`AddAuthenticationStateSerialization(o => o.SerializeAllClaims = true)`), so the same two
claims are readable client-side. Routes' cascade can't cross into a WASM island, so an island
page wraps its own content in `<ThemeContextProvider>` (see `TagDirectoryPage`,
`layer5-wasm.md` §"ThemeContext in WASM").

**Consumer pattern** (`TagChip.razor`, `TagSelector.razor`, `CharacterEntry.razor`):

```razor
@inject ISpriteReadService Sprites
[CascadingParameter] private ThemeContext ThemeCtx { get; set; } = default!;

// Resolve at render — no per-request cache, no service round-trip
private string? SpriteUrl => Tag.SpriteIdentifier is null
    ? null
    : Sprites.GetSpriteUrl(ThemeCtx.Slug, Tag.SpriteIdentifier, ThemeCtx.PrefersAnimated);
```

**SharedUI may inject `ISpriteReadService`** — it is NOT `IActiveUserContext`. `ISpriteReadService`
has a shared Core impl (`OptimisticSpriteReadService`, pure string logic) registered as a singleton
on both Server and Client. The `IActiveUserContext`-never-in-SharedUI rule is unaffected
(`identity-and-authorization.md`).

**Claims are stamped at sign-in and stale until the next one — by design.** Every claims-baked
preference (theme slug, `prefers_animated_sprites`, `show_mature_content`) is a point-in-time copy
made by `ApplicationUserClaimsPrincipalFactory` when the auth cookie is issued. A settings save
updates the database, not the cookie: an interactive circuit **cannot** reissue the cookie
(`RefreshSignInAsync` needs an HTTP response to write to — only the static-SSR Identity pages call
it). Consequence: changes to these preferences take effect at the next sign-in. Do not chase the
gap as a bug, and do not attempt a circuit-side refresh hack; if a future requirement demands
immediacy, the shape of the fix is an SSR round-trip that reissues the cookie. (Flagged in the
factory's header since WU12; browser-verified end-to-end in `audit/Sprites.md`, 2026-07-02.)

**`SpriteBaseUrl` config seam** (`appsettings` key `Sprites:BaseUrl`, default `/sprites/themes`).
The resolver builds: `{SpriteBaseUrl}/{slug}/{static|animated}/{id}.{ext}`. Changing this one
setting + Rclone syncing assets is the complete R2/CDN cutover — zero code change. This is the same
public-asset-base seam `IImageStorageService` will adopt at `S3ImageStorageService` time; sprites
and uploads converge on one CDN/base-URL config but do **not** share a storage service (sprites have
no runtime write path — assets are provisioned out-of-band).
