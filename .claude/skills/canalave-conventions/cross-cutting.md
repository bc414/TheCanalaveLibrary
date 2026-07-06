# Cross-Cutting Concerns

These touch many features and are woven into implementation rather than built as standalone features.
Also covers global architectural decisions that span multiple layers.

## Render Mode: Global InteractiveAuto

Set the render mode **once**, on `<Routes>` and `<HeadOutlet>` in `App.razor` — not on `<RouteView>` in
`Routes.razor`, and not per-component:

```razor
@* Routes.razor — no @rendermode here; use AuthorizeRouteView (not RouteView) so [Authorize]
   attributes are honoured (RouteView silently ignores them — see "Authorization" section below) *@
<AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(DeviceLayout)" />
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
Mobile hamburger menu. Login/logout are triggers on the persistent layout, not separate navigation targets.

**Notification bell** (`SharedUI/Notifications/NotificationBell.razor`, WU33) — legitimate cross-cutting injection:
`INotificationReadService` is injected directly into this layout element (N+1 exception; confirmed in
`grid_axes.md`). Rules for this component:
- Wrapped in `<AuthorizeView><Authorized>` — renders only when logged in.
- **Does NOT inject `IActiveUserContext`** — server-only service, will not exist post-WASM-split. The underlying
  read service self-scopes via `IActiveUserContext` internally.
- **UserCard caret pattern** — `relative` container + `@onclick="Toggle"` button (with unread-count badge) +
  `@if (_open)` absolute `top-full z-10` flyout panel. NOT the `fixed inset-0` modal pattern (notifications
  are a glanceable peripheral feed, not a blocking action).
- Panel shows recent `NotificationItem`s + "Mark all read" + "See all → `/notifications`".
- No live push (a WU-SignalR-era possibility, deliberately unbuilt); count refreshes on render/navigation.
- Inserted before `<LoginDisplay />` in both `DesktopLayout.razor` and `MobileLayout.razor`.

**Messages nav link** (`SharedUI/Messaging/MessagesNavLink.razor`, WU35) — the second legitimate
cross-cutting layout injection, parallel to the bell: injects `IMessagingReadService` for the unread
badge, wrapped in `<AuthorizeView>`, refreshes on every navigation via a `LocationChanged`
subscription, inserted beside the bell in both layouts.

**Layout-chrome concurrency (applies to both, and to any future chrome component):** these
components render on *every* authenticated page and initialize/refresh **concurrently with each
other and with the page dispatcher's own loads** — Blazor Server interleaves sibling async init on
one circuit-scoped DI scope. Services reachable from layout chrome must therefore follow the
read-context factory rule (`layer2-services.md` §"Read-Context Concurrency: Factory Per Method");
a circuit-scoped `ReadOnlyApplicationDbContext` shared across them crashes every authenticated
load (found via browser debugging 2026-07-01; regression: `Tests.Integration/ConcurrentReadAccessTests.cs`).

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
    string Theme { get; }              // URL-safe theme SLUG (e.g. "pokemon") — feeds ThemeContext / sprite URL builder
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
the `Theme` slug + `PrefersAnimatedSprites` arguments passed to the root `ThemeContextProvider`
(see "ThemeContext Cascading Provider" below) — both previously had no defined source for "the current user."

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
on both Server and Client. The `IActiveUserContext`-never-in-SharedUI rule is unaffected.

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

## Active-User-Conditional Handling

### The two identity sources — which to use where

The app reads the same underlying claims through two deliberately separate layers. They are not
interchangeable; which one you use is determined by where you are:

| Source | Lives in | Lifetime | Use it for | Never for |
|---|---|---|---|---|
| **`IActiveUserContext`** (Core/Identity) | **Server services** (scoped, claims-only, no DbContext) | Per-circuit/request | Query-shaping (content-rating filter, sprite theme), per-viewer DB projections, **and server-side authorization** (`UserId` equality, `IsModerator/IsAdmin`) | Injecting into any SharedUI component |
| **Blazor `AuthenticationState` / `<AuthorizeView>`** | **UI** (routable pages + components) | Cascading | Showing/hiding affordances, role-gated markup, resolving `CurrentUserId` at the page level to pass down as a parameter | Any decision you actually rely on for security |

**`IActiveUserContext` is server-only** and will not exist in a future WASM Client.
SharedUI survives the L5 WASM split only because it never injects it.

**Rule:** *SharedUI components never inject `IActiveUserContext`.* The dispatcher/routable page
resolves identity from `[CascadingParameter] Task<AuthenticationState>` and passes ownership down
as a parameter (bool `IsOwnStory`, `IsOwnComment`, `IsEditable`, or `int? CurrentUserId`).
`StoryDeck`, `StoryCard`, `CommentItem`, and `VouchList` already do exactly this.

### Six kinds of active-user conditionality

| # | Kind | Mechanism | Established |
|---|---|---|---|
| **(a)** | Data filtering / query-shaping ("mature off ⇒ no trace"; sprite theme) | Server: `IActiveUserContext` in read service / EF global query filter | WU12 |
| **(b)** | Authentication gate ("is anyone logged in?") | UI: `<AuthorizeView>`. Server write: `IsAuthenticated` guard before any mutation | WU1 |
| **(c)** | Role gate (mod/admin-only surfaces) | UI: `<AuthorizeView Roles="Moderator,Admin">`. Mod pages: `[Authorize(Policy=…)]` | WU28/WU34 |
| **(d)** | Ownership gate ("is the viewer the owner of *this specific entity*?") | UI: page computes bool, passes down; component uses plain `@if`. Server: service loads entity, compares `entity.OwnerId != activeUser.UserId`, throws | UI: WU13/WU14. Server: WU24+ |
| **(e)** | Per-viewer state ("has the viewer favorited / liked / started this?") | Server read service projects per-viewer flags via `IActiveUserContext.UserId` into the DTO | WU15/WU19 |
| **(f)** | Owner-or-staff gate — **does not exist in this codebase.** | Editing is **author-only** (strict identity-equality). Moderation is a **separate code path** (WU34 admin service). Never an `OR` fold. | WU24 |

### Security vs affordance — the load-bearing principle

**Authorization is server-side, in the write service. UI affordances are visibility only, never trusted.**

Every write path must load the entity and verify `entity.OwnerId == IActiveUserContext.UserId`,
throwing `UnauthorizedAccessException` on mismatch. The UI `@if` (hiding the edit button) is convenience
UX — it is not a control. These two layers are complementary; neither substitutes for the other.

The comment write service is the reference implementation:
```csharp
if (comment.UserId != userId)
    throw new UnauthorizedAccessException("You can only edit your own comments.");
// Moderation delete is a separate method/admin service — not a role OR here.
```

### Two content-editing patterns, by content weight

There are two patterns. `RichTextView` and `EditorView` co-existing on a page is **normal** for
pattern 2; the "separate pages" rule applies only to pattern 1.

**Pattern 1 — Primary long-form content (Story, Chapter): view-page / edit-page split.**

| | View page (read-only, everyone) | Edit page (author-only) |
|---|---|---|
| Rich text | **`RichTextView`** | **`EditorView`** |
| Features | consumption (comments, scroll %, recommendations, tags display) | authoring (tags, cover, versioning, spotlighting) |
| Auth | public (content filters apply) | `[Authorize]` + on-load ownership redirect + service gate backstop |
| Bridge | inline `@if (_isOwner) { <a href="…/edit">Edit</a> }` | — |

The two renderers do not co-exist **because they are on different routes**, not because of a global rule.
Story: `/story/{id}/edit` (WU24) vs `/story/{id}/{slug}` (WU25).
Chapter routes (WU26):

| Purpose | Route |
|---|---|
| Read primary (public) | `/story/{StoryId:int}/{ChapterNumber:int}` |
| Read alternate (public) | `/story/{StoryId:int}/{ChapterNumber:int}/{VersionOrder:int}` |
| New chapter (author) | `/story/{StoryId:int}/chapter/new` |
| Edit primary (author) | `/story/{StoryId:int}/chapter/{ChapterNumber:int}/edit` |
| Edit alternate (author) | `/story/{StoryId:int}/chapter/{ChapterNumber:int}/{VersionOrder:int}/edit` |

Reading routes have no `/chapter/` literal segment (fixed by spec §5.30.3 + shipped `ChapterNavigation` URLs).
The `/chapter/` literal on edit routes prevents collision with the int-constrained reading routes.
`VersionOrder` = `ChapterContent.SortOrder` (readable 0/1/2…); omitting it selects the primary version.

**Pattern 2 — Lightweight embedded content (comments, recs, vouch text): in-place inline edit.**
One page; edit mode is parent-owned (e.g. `CommentSection._editingId`). The item being edited swaps
`EditorView` (via `CommentEditor`) in place; siblings render `RichTextView`. Both renderers co-exist
by design. No dedicated edit route. [`CommentItem`](../../../../TheCanalaveLibrary.SharedUI/Comments/CommentItem.razor#L42-L78)
is the reference.

## Chapter Versioning — Progressive Disclosure (WU26)

`ChapterContent` rows are **live alternates**, not revision history — one is the reader's default
(`Chapter.PrimaryContentId`). The guiding principle: **the version concept is invisible until a
second version exists** (settled WU26).

**Edit page (author):**
- Single-version (or new): the editor is a plain chapter form with one low-emphasis
  **"Add an alternate version"** link as the only versioning affordance.
- Multi-version (`VersionCount > 1`): reveals a compact **version switcher** (which version you're
  editing + links to per-version edit routes) plus per-version controls: rename, **set as primary**,
  delete (disabled for the primary — enforced by the Restrict FK), add another.

**"Primary" badge driven by `IsPrimary` DTO field, never by `SortOrder == 0`.** The primary can
change; SortOrder is stable identity within a chapter's version list and should not carry semantic load.

**Rating floor invariant (WU26):** a version's effective rating must be ≥ the story rating. An M story
is mature throughout; a T story allows T or M versions, not E. NULL = inherit story rating (always
passes the floor). The **primary** version's effective rating must equal the story rating (naturally
satisfied by NULL/inherit) — guarantees any reader who can see the story can always read its primary
chapters without a content-gate block.

## Group Audience-Visibility Filter

Groups carry an **`AudienceRating`** property (renamed from `Rating` in the WU32 migration — column
renamed, enum mapping unchanged). It enforces the same "zero visible trace of mature content" rule
as the story `ContentRating` filter, applied to group listings:

- **`AudienceRating = E` (General):** group is visible to all users (mature on or off).
- **`AudienceRating = M` (Mature):** group is hidden from users where `ShowMatureContent = false`.

This is enforced as a **named EF Core query filter `"GroupAudience"`** on `Group`, registered in
`ReadOnlyApplicationDbContext.OnModelCreating` (not the base `ApplicationDbContext` — see "Content Rating
Filtering" below for the principle: display filters live only on the read context):

```csharp
modelBuilder.Entity<Group>()
    .HasQueryFilter("GroupAudience", g => _activeUser.ShowMatureContent || g.AudienceRating != Rating.M);
```

`_activeUser` is the `protected readonly IActiveUserContext` field on `ApplicationDbContext`, accessible to
the subclass. Anonymous and mature-disabled users cannot see Mature groups; mature-enabled users can.

**Write paths are unfiltered by design** — the write context (`ApplicationDbContext`) carries no
`GroupAudience` filter, so load-by-id on write paths sees all groups without any `IgnoreQueryFilters` call.
The mod/creator *read* path that legitimately needs to surface a taken-down or audience-gated group goes
through the read context with an explicit elevated-read bypass (annotated `// elevated read:`).

**Named filter applies to `Group` only.** `GroupStory`, `GroupFolder`, `GroupComment`, and
`GroupBlogPost` are accessed only in the context of their parent group — when the parent group is
invisible (filter applied), none of its children are reachable. No child-table filter needed.

**Three audience presets are a UI/write convention, not stored.**  
`GroupAudienceType { Standard, SfwOnly, Mature }` is a C# enum in `Core/Lookups/ModelEnums.cs`
used only at the write/display boundary. The DB stores just `(AudienceRating, MaxContentRating)`.
A static mapper in `Core/Groups/GroupAudienceTypeMapper` converts both ways:

| `GroupAudienceType` | `AudienceRating` | `MaxContentRating` |
|---------------------|------------------|--------------------|
| Standard | E | M |
| SfwOnly | E | T |
| Mature | M | M |

Non-M stories can be added to a Mature group — the audience rating defines the group's topic and
audience, not a floor on story content. A T-rated story that fits can always be added to a Mature
group; safe because mature-disabled users cannot see the Mature group at all (filtered at listing).

## Group Membership and Role Model

Groups are **not gated communities.** The membership and role model is deliberately simple:

- **Open join:** any authenticated user may join any group (subject to `GroupAudience` visibility —
  you can't join a Mature group if you can't see it). No approval, no invitation, no waitlist.
- **Permanent membership:** no kicking mechanism. If a member misbehaves, they are handled by site
  moderators (WU34) exactly as in any other area of the site. No per-group moderator role.
- **Two roles: Member and Admin.**
  - `GroupRole.Member` — can browse the group, add stories (subject to content-rating waterfall),
    post comments.
  - `GroupRole.Admin` — additionally can remove stories, manage folders (create/rename/delete/reorder,
    set `MaxRating ≤ group.MaxContentRating`), and edit the group's name/description/audience type.
  - The group creator is automatically inserted as Admin on group creation. There is currently no
    way to transfer Admin status — that is post-MVP if ever needed.
- **No `GroupRole.Moderator` category.** Do not add one — the decision is permanent, not a
  deferral. Site moderators handle group-level misconduct.

**Server-side enforcement:** admin-gated write methods load the caller's `GroupMember` row and check
`role == GroupRole.Admin`, throwing `UnauthorizedAccessException` on mismatch. UI affordances (folder
management, remove-story buttons) are visibility-only `@if` wired to a page-computed `bool IsAdmin`
passed down from the dispatcher — not a security gate.

**Leave:** any member may leave. The `GroupMember` row is deleted. If the last admin leaves, the
group remains but has no admin — currently acceptable (post-MVP: warn on last-admin leave or
auto-promote).

## Content Rating Filtering

Every read service returning story data must filter by content rating. The ceiling is a **global EF Core named query filter** sourced from `IActiveUserContext`, registered on `ReadOnlyApplicationDbContext` only — not the base `ApplicationDbContext`.

### The principle: display filters live only on the read context

> **The write context (`ApplicationDbContext`) sees ground truth and is never filtered. All display/
> visibility filters — `ContentRating`, `GroupAudience`, `IsTakenDown` — live only on
> `ReadOnlyApplicationDbContext`.** A bypass (`IgnoreQueryFilters`) on the read context is therefore always
> a deliberate, auditable "this read intentionally sees more than a normal viewer." There is nothing to
> forget on a write.

This makes the content-safety boundary **structurally safe** instead of discipline-safe. Write services
load entities by id to mutate them; they must see ground truth. If a visibility filter sits on the write
context, every write path must *remember* to bypass it — a discipline that silently breaks (WU31_5b,
post-WU38 audit). Moving all filters to the read context eliminates that entire class of bug.

`ReadOnlyApplicationDbContext` inherits from `ApplicationDbContext` and overrides `OnModelCreating` to add
the four named filters, closing over the `protected readonly IActiveUserContext _activeUser` field exposed
by the base class:

```csharp
// ReadOnlyApplicationDbContext.OnModelCreating (after base.OnModelCreating):
modelBuilder.Entity<Story>().HasQueryFilter("ContentRating",
    s => s.Rating <= (_activeUser.ShowMatureContent ? Rating.M : Rating.T));

modelBuilder.Entity<Group>().HasQueryFilter("GroupAudience",
    g => _activeUser.ShowMatureContent || g.AudienceRating != Rating.M);

modelBuilder.Entity<Story>().HasQueryFilter("IsTakenDown", s => !s.IsTakenDown);
modelBuilder.Entity<BaseComment>().HasQueryFilter("IsTakenDown", c => !c.IsTakenDown);
modelBuilder.Entity<BaseBlogPost>().HasQueryFilter("IsTakenDown", b => !b.IsTakenDown);
modelBuilder.Entity<Recommendation>().HasQueryFilter("IsTakenDown", r => !r.IsTakenDown);
```

EF Core caches models per context *type*, so the read context gets a filtered model; the base type
(`ApplicationDbContext`) remains unfiltered. Query filters don't touch schema — no migration impact.

Anonymous viewers get the `Rating.T` ceiling (never `IsAuthenticated` ⇒ never `ShowMatureContent`).
`User.ShowMatureContent` is a hot boolean on the User table — direct column, not in jsonb.

### Elevated reads (the only surviving bypasses)

Legitimate "see more than a normal viewer" read paths call `IgnoreQueryFilters` **by name** and are
annotated `// elevated read:` so they read as deliberate:

```csharp
// elevated read: author always sees their own stories regardless of personal rating setting
await readDb.Stories
    .IgnoreQueryFilters(["ContentRating"])
    .Where(s => s.AuthorId == userId)
    .ToListAsync();

// elevated read: mod queue must see taken-down content to act on it
await readDb.Stories
    .IgnoreQueryFilters(["IsTakenDown"])
    .Where(s => s.StoryId == id)
    .FirstOrDefaultAsync();
```

EF Core 10 named filters allow per-filter opt-out, leaving other named filters active on the same query.

### TPT blog-post exception

`HasQueryFilter` on a TPT root (`BaseBlogPost`) generates broken EF Core 10 SQL on entity
materialization, so the `"IsTakenDown"` and blog-post content-rating filters are **not** applied
model-level to `BaseBlogPost`. Instead, each blog-post read service enforces them via an explicit
`.Where(p => !p.IsTakenDown)` / `.Where(p => p.Rating <= max)` in the projection. Blog-post delete uses
the change-tracker stub: `writeDb.Remove(new ProfileBlogPost { BlogPostId = id });
await writeDb.SaveChangesAsync();` — EF issues child-then-base DELETE in one transaction. See
`audit/BlogPosts.md` §Feature 35 Stage-5 note (WU31.5) for full rationale.

## Notification Creation

### Generation mechanism

See `layer2-services.md` "Notification Generation" for the wiring pattern (semantic per-event methods, best-effort post-commit ordering, semantic-methods rationale, DAG rule).

### Filtering semantics

**In-app delivery is always-on.** The private create-core applies exactly two universal rules: **drop
self** (`recipient == sourceUser`) and **dedup**. No per-type in-app mute exists in the model.

**Fan-out eligibility (relationship-level gate):** follow-driven notification types (new chapter on a
followed story, new story by a followed user, etc.) are sent only to followers where
`FollowedUser.ReceiveAlerts == true`. That filter is part of the recipient-resolution query for each
semantic method — not a per-type setting.

**`UserNotificationSetting` governs email and display, not in-app generation.** The sparse-override
table stores exactly two user-settable fields per type — `EmailEnabled` (post-MVP email side-channel)
and `Collapsed` (display override for the panel — a per-user override of
`NotificationType.DefaultCollapsed`). NULL for either field means "use the type's default."
No in-app mute column exists; that toggle was deliberately dropped from spec §5.18 (recorded in
`audit/Notifications.md`).

9 categories, ~35 types with gap-based numbering. `DefaultEmailEnabled` and `DefaultCollapsed` are
required non-nullable on all types.

## Structured Tag Authoring & Legality Enforcement (WU37)

### Per-story routing table

Every tag type uses a **different per-story association table**. Route by `TagChipDto.TagTypeId`:

| Tag type | Per-story target | Entity |
|---|---|---|
| Genre, ContentWarning, CrossoverFandom | Flat junction | `StoryTag` |
| Setting | Flat junction + optional side-row | `StoryTag` + `SettingDetail` |
| Character | Dedicated entity (replaces StoryTag) | `StoryCharacter` |
| Pairing (ship) | Structural, named members | `StoryCharacterPairing` + `StoryCharacterPairingMember` |

Character never routes to `StoryTag`. A pairing is not a catalog tag (no `Tag` row; its name derives
from its members). `TagTypeEnum.Relationship` is removed.

### Table naming — disambiguation from story↔story relationships (Feature 10)

| Concept | Entity | Note |
|---|---|---|
| Character-in-story | `StoryCharacter` | Per-story; links to `Tag` (Character type) |
| Ship/pairing of characters | `StoryCharacterPairing` | Per-story; NOT a catalog tag |
| Members of a pairing | `StoryCharacterPairingMember` | First-class join; was auto-generated shadow table |
| **Story-to-story** relationship | `StoryRelationship` | Feature 10; unrelated; leave untouched |
| Story relationship type | `StoryRelationshipType` | Feature 10; unrelated; leave untouched |

The `Story…Pairing` prefix marks the concept as per-story and eliminates grep collision with the
Feature-10 `StoryRelationship` / `StoryRelationshipType` entities.

### Legality rules — enforced at service layer

All rules are enforced by `ServerStoryWriteService` via `StoryValidationException` (same pattern as
`CanSave()` / author-gate). Server re-reads gates from `Tag` — never trusts client DTO values.

| Rule | Condition | Error |
|---|---|---|
| OC details require gate | `IsOc == true` but `Tag.AllowOCDetails == false` | Reject |
| SettingDetail requires gate | `SettingDetail` submitted but `Tag.AllowSettingDetails == false` | Reject |
| ContentWarning priority coercion | Priority != Primary | Coerce to Primary (not an error) |
| Pairing member count | Members < 2 | Reject |
| Pairing members in-story | Member `StoryCharacterId` must exist in this story's `StoryCharacters` | Reject |

No DB trigger (`TR_StoryCharacters_EnforceOCLogic` is SQL-Server-era; superseded). A DB CHECK is
post-MVP defense-in-depth if wanted.

### Priority

`TagPriority { Primary=0, Supporting=1 }`. Primary default. No `None` value. ContentWarning gets no
priority picker and its priority is coerced to `Primary` at service layer.

## Badge Checks

**MVP (WU36):** synchronous inline, best-effort. Call `IBadgeWriteService.AwardAsync` after the
counter `ExecuteUpdateAsync` and after the primary `SaveChangesAsync`, inside a `try/catch` — never
fail the parent operation on a badge error. See `layer2-services.md` "Synchronous Inline Badge
Awards" for the full pattern, idempotency guarantee, anti-self-farm guard, and post-MVP swap note.

Live `SiteBadges` constants: `Patron`, `Recommender`, `RecommenderSilver`, `BetaReader`, `Architect`,
`Artist`. Keys are `public const string` fields on `SiteConstants.SiteBadges`.

## UserStats Updates

22+ denormalized counter fields. Updated in real-time by application logic within the same
transaction as the primary write (same-transaction `ExecuteUpdateAsync`):

```csharp
await writeDb.UserStats
    .Where(us => us.UserId == story.AuthorId)
    .ExecuteUpdateAsync(s => s.SetProperty(us => us.StoryCount, us => us.StoryCount + 1));
```

Background worker (F58, post-MVP) periodically recalculates to correct drift.

### Counter mutation rule — all denormalized counters

Every denormalized counter — `LikeCount` on `Recommendation` / `BaseComment`, and every `UserStats.*`
field — must be adjusted with an **atomic** `ExecuteUpdateAsync`:

```csharp
// ✓ Correct — one SQL `SET counter = counter + delta`; concurrent callers can't collide
await writeDb.Recommendations
    .Where(r => r.RecommendationId == id)
    .ExecuteUpdateAsync(s => s.SetProperty(r => r.LikeCount, r => r.LikeCount + delta));

// ✗ Wrong — tracked read-modify-write; two concurrent readers both see the old value → lost update
rec.LikeCount++;
await writeDb.SaveChangesAsync();
```

The tracked `++` form reads a value into memory, increments it, and writes it back. Two concurrent
callers reading the same stale value both produce the same written result: one increment is lost.
`ExecuteUpdateAsync` issues a single `SET like_count = like_count + delta` that the database
serializes correctly under any isolation level.

### Counter ↔ event map (WU30, wired into already-built write services)

| `UserStat` counter | Owning user | Event / write service | Δ |
|---|---|---|---|
| `FollowerCount` | target user | `ServerFollowingWriteService.FollowAsync / UnfollowAsync` | ±1 |
| `AuthorsFollowed` | acting user | `ServerFollowingWriteService.FollowAsync / UnfollowAsync` | ±1 |
| `StoriesWritten` | author | `ServerStoryWriteService.CreateStoryAsync` | +1 |
| `WordsWritten` | author | `ServerChapterWriteService` publish / new version | ± word delta |
| `CommentsWritten` | commenter | `ServerCommentWriteService.Post*/Delete` (all 4 contexts: chapter/blogpost/group/userprofile) | ±1 |
| `RecommendationsWritten` | recommender | `ServerRecommendationWriteService.SubmitAsync` | +1 |
| `RecommendationsReceived` | story author | `ServerRecommendationWriteService.SubmitAsync` | +1 |
| `RecommendationSuccessesEarned` | recommender | `ServerRecommendationWriteService.RecordSuccessAsync` (new column, WU36) | +1 |
| `BlogPostsWritten` | author | `ServerBlogPostWriteService` create/delete | ±1 |
| `GroupsJoined` | member | `ServerGroupWriteService` join/leave | ±1 |
| `FavoritesOnStories` | story author | `ServerUserStoryInteractionWriteService` | **transition-delta** |
| `StoriesRead`, `StoriesInProgress`, `StoriesIgnored` | acting user | `ServerUserStoryInteractionWriteService` | **transition-delta** |

**Counters deferred to their own WU** (source feature not yet built):
- `ViewsOnStories` — WU38 (story view events)
- `SpotlightCount` — post-MVP
- `ActiveReportCount` — WU34 (moderation)
- Acknowledgment/contribution counters — WU37

### Transition-delta rule for UserStoryInteraction-derived counters

`ServerUserStoryInteractionWriteService` toggles boolean columns (`IsFavorite`, `IsCompleted`,
`IsIgnored`, etc.) rather than appending records. A simple ±1 on every call would double-count;
the correct rule is **increment or decrement only when the boolean flips**:

```csharp
// Before writing:
bool wasFavorite = existing?.IsFavorite ?? false;
bool willBeFavorite = dto.IsFavorite;

// After SaveChangesAsync:
if (willBeFavorite && !wasFavorite)
    // +1 FavoritesOnStories on story.AuthorId
else if (!willBeFavorite && wasFavorite)
    // −1 FavoritesOnStories on story.AuthorId
```

The same flip-check governs `StoriesRead`/`StoriesInProgress`/`StoriesIgnored` — each maps to one
boolean column (`IsCompleted`, `HasStarted`+`!IsCompleted`, `IsIgnored`); the counter moves only
when the effective derived state actually changes. Never increment/decrement if the boolean is being
written to its current value (idempotent call from an optimistic-UI retry).

## Razor Attribute Quoting

**Inner double-quotes inside `@onchange="..."` terminate the attribute early and cause CS1525.**

If you write `@onchange="e => Cast((e.Value ?? "0"))"` the inner `"0"` closes the attribute — the
Razor parser sees the attribute end there and treats `))"` as stray characters, producing a cryptic
CS1525 parse error.

**Fix: use a block lambda and `int.TryParse` instead of a default-fallback literal:**

```razor
@onchange="e => { if (int.TryParse(e.Value?.ToString(), out int rv)) _myEnum = (MyEnum)rv; }"
```

This avoids any string literal inside the attribute value. The `int.TryParse` block lambda is the
canonical form for all enum `<select>` onchange handlers in this codebase (e.g. `AuthorSettingsForm`,
`PrivacySettingsForm`, `AppearanceSettingsForm`).

### Enum `<select>` — two valid patterns, never mix their halves

`@bind` on an enum-typed property **does** work on a `<select>`, but it serializes the current
value as the enum **name** (`.ToString()`), so it only ever matches `<option>` values that are
also the enum name. The two coherent patterns:

1. **`@onchange` block lambda + numeric option values** (`value="@((int)v)"` + the `int.TryParse`
   lambda above) — the canonical settings-form pattern.
2. **`@bind` on the enum property + name option values** (`value="@type"` inside an
   `Enum.GetValues<T>()` loop) — `TagEditorForm` is the reference.

Mixing them — `@bind` with numeric option values — compiles clean and renders a **blank select**
whenever the bound value should be pre-selected, because nothing matches. Found live in the L4.5
browser pass (`audit/Tags.md`, 2026-07-01).

### String parameters take attribute text literally

For a **string-typed** component parameter, `Param="_myField"` passes the literal text
`"_myField"` — not the field's value. Only `@`-prefixed values (`Param="@_myField"`) compile as
expressions. Non-string-typed parameters compile the attribute text as an expression either way —
that asymmetry is why the trap survives review: identical syntax is correct on the `bool`
parameter above and wrong on the `string` parameter below it. Rule: **always `@`-prefix identifier
values regardless of the parameter's type.** Sweep for the bug class with `="_\w+"` (plus
suspicious PascalCase values on string params) — it was hit seven times before being swept
(`audit/Messaging.md`, `audit/Tags.md`).

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

**Role infrastructure status (updated WU27.5, 2026-06-24):** Role *rows* (`User`, `Moderator`, `Admin`)
are seeded via `ApplicationRoleConfiguration.HasData` in `IdentityConfigurations.cs` — they exist at
migration time. `DataSeeder.cs` previously only assigned `AdminUser` to `"Admin"`; **WU27.5 closes the
gap by also assigning `AdminUser` to `"Moderator"`**, so the role gate is exercisable end-to-end in
dev. `IsInRole` is literal — there is no automatic Admin-inherits-Moderator hierarchy, so every gate
that should accept either role must list both: `Roles="Moderator,Admin"` / `.RequireRole("Moderator",
"Admin")` / `IsModerator || IsAdmin`.

## Moderation Model

### Mission-Driven Defaults — Opposite of Industry Standard

The moderation tooling is deliberately inverted from commercial/attention-economy defaults. Understanding
*why* prevents re-introducing industry patterns that are load-bearing only for scale, ad-safety, or
engagement-maximization — none of which apply here.

| Industry driver | Industry pattern | This site's inversion |
|---|---|---|
| Ad-safety/brand pressure | Fast automated removal; err toward takedown | Soft-hide default (reversible); author told why |
| Engagement maximization | Lax on borderline-toxic but engaging content | Deliberately anti-engagement design |
| Adversarial spam scale | Auto-hide on threshold | Human-in-the-loop (affordable at this volume) |
| Hard-delete for storage/legal finality | Opaque permanent takedowns | Soft-hide default; hard-delete only for illegal content |
| Appeals cost money → no real appeal | Shadowban (user doesn't know) | **No shadowban — ever** |

### Content Removal

**Soft-delete (takedown) default.** Normal mod action sets `IsTakenDown = true`, `TakedownDate`, and
`TakedownReason` on the target entity. All three share the "takedown" stem; do not confuse with
`IsHiddenFavorite` (UserStoryInteraction, private-favorites), `IsHiddenGem` (Recommendation, a curator
label), or the `ContentRating`/`GroupAudience` filters (which hide by rating/audience, not by mod action).
The content is invisible on public reads (named query filter `"IsTakenDown"`) but visible to the author
(who reads with `IgnoreQueryFilters(["IsTakenDown"])`) and to moderators (same). The author receives a
`NotifyContentRemovedAsync` notification with the stated reason so they can often fix it (e.g. re-rate
mature content) rather than just being punished.

Applicable targets: `Story`, `BaseComment`, `BaseBlogPost`, `Recommendation`. `User` is handled by account
actions (not a takedown column). `PrivateMessage` is not soft-hidable — DM reports go straight to the queue
for moderator judgment only.

**Narrow hard-delete escape hatch.** A separate explicit "illegal content" action (CSAM, piracy) hard-deletes
via a distinct `ApplyHardDeleteAsync(type, id)` path in `ServerModerationWriteService`. This is not the
default and is presented as a distinct moderator choice, not the same action as soft takedown.

**Named query filter `"IsTakenDown"`.** Each removable entity registers the `"IsTakenDown"` filter via
`HasQueryFilter` in `ReadOnlyApplicationDbContext.OnModelCreating` (composable alongside `"ContentRating"`
and `"GroupAudience"` — all display/visibility filters live on the read context only; see "Content Rating
Filtering" above). The write context sees ground truth; `IgnoreQueryFilters` is only needed on elevated
*read* paths (mod queue, author viewing own taken-down content).

**Moderator filter behavior (settled 2026-06-26).** A moderator's content-rating reach equals their
personal `ShowMatureContent` setting, **both** when browsing the site and when reviewing the report queue.
The report queue is a shared global pool scoped by the `ContentRating` filter — a T-only mod sees only E/T
Story reports, not M ones. Review/entity-load paths bypass **only** `"IsTakenDown"` (so already-taken-down
content stays reviewable); `ContentRating` and `GroupAudience` stay live. Reports on entities filtered out
by ContentRating are *dropped* from the queue (not shown as placeholders).

### Auto-Hide Policy — None

`ActiveReportCount` drives mod-only triage ordering (most-reported items first in the queue) and an inline
queue badge. It is **never an automatic action trigger**. No threshold-based auto-hide or auto-flag logic.

Rationale: auto-hide-on-threshold in a small fandom community becomes a brigading weapon (the mass-report
heckler's veto). Human review is affordable and necessary at this site's volume. The deliberations'
"3 distinct reporters in 24h" rule is permanently dropped.

**Report counts are mod-only.** No public-facing "reported N times" display — that gamifies reporting and
enables coordinated harassment. `ActiveReportCount` on entities and `User` is queried only by moderator
surfaces.

### Account Actions

**State model:** `User.AccountStatus` enum (Active / Warned / Suspended / Banned — **no Shadowbanned**) +
`User.SuspendedUntilUtc` (nullable `DateTime?`).

**Action workflow:** each action (warn / suspend / ban) sets `AccountStatus`, records on `Report`
(`ActionTaken` + `DateResolved` + `ModeratorUserId`), and sends the appropriate notification
(`NotifyAccountWarningAsync` / `Suspended` / `Banned`). The user is always told why — transparency is
non-negotiable per §13.

**Shadowban is permanently rejected.** Deception-as-moderation directly contradicts the site's §13
philosophy ("close the loop on ALL reports — the user is entitled to know they were heard"). A community
that shadowbans lies to its members. This is a design axiom, not a deferred decision.

**Login enforcement is staged.** WU34 ships the state columns and notifications. Actual login-blocking
(block Suspended users until `SuspendedUntilUtc`; block Banned users permanently; surface Warned banner
in layout chrome) is a dedicated follow-up WU — it's a security surface that deserves its own careful slice.

### Report Targets and `ActiveReportCount`

**Reportable set (WU34):** Story, User, Comment (covers all TPT subtypes coarsely), BlogPost, Recommendation,
PrivateMessage. `Report.ReportedEntityId` is `long` (widened from `int` in WU34 migration). `ReportedEntityType`
enum value `Message = 5` added.

**`ActiveReportCount` exists on:** Story, BaseComment, BaseBlogPost, Recommendation, **and User** (added WU34
for symmetry). **`PrivateMessage` has no counter** — DMs are 1:1 sensitive; no surface would display a
per-message report count.

**Uniform `AdjustActiveReportCount(type, id, delta)`** private switch in `ServerModerationWriteService`
increments on report submit, decrements on resolve. The switch skips `Message` targets (no counter).

### Notification Loop (§13 Transparency)

Every report outcome notifies the reporter — including no-action outcomes. This is more work per report
than industry standard, and it is affordable precisely because volume is low and the community is the
point. **The "close the loop" philosophy is a project axiom; do not trade it away for efficiency.**

Semantic methods:
- `NotifyReportReceivedAsync` — immediate confirmation on submit.
- `NotifyReportResolvedAsync` — action taken (links to affected entity if applicable).
- `NotifyReportResolvedNoActionAsync` — no action taken, still closes the loop.
- `NotifyContentRemovedAsync` — target author notified with reason.
- `NotifyStoryApprovedAsync` / `NotifyStoryRejectedAsync` — submission outcomes.
- `NotifyAccountWarningAsync` / `AccountSuspendedAsync` / `AccountBannedAsync` — disciplinary actions.

All are best-effort post-commit (try/catch swallows; notification failure never rolls back the primary
moderation action).

## Private Messaging Architecture

### Stateless MVP — SignalR is post-MVP

The spec described "real-time via SignalR" for messaging. That framing is **reversed for MVP**:
messaging is request/response, identical in shape to every other feature. The recipient sees new
messages on navigate/refresh; the global unread badge refreshes on layout render (navigation). The
practical rationale: the use-case is substantive and infrequent — the same "Discord handles ambient
chat, this site handles substantive writing" decision that constrains group conversations. SignalR
realtime buys very little here and costs a lot (first app-level hub, new test harness, reconnection
handling — with no existing template).

**Post-MVP:** SignalR push is an additive layer behind the **unchanged** write service. The message
write path (sanitize → persist → return DTO) doesn't change; a broadcast via `IHubContext` is wired
alongside it. No L1–L4 rework is needed. See `workplan.md` Post-MVP section.

### Two unread systems by design — do not unify

The app has two entirely separate unread-state systems, each the right shape for its domain:

| System | Model | Read-state unit | Cleared by |
|---|---|---|---|
| **Notifications** | Event rows in `Notification` table | Per-event boolean | Individual dismiss / "mark all read" |
| **Messaging** | `ConversationParticipant.LastReadTimestamp` | Per-conversation high-water mark | Timestamp write on thread open |

**Do not unify them.** Notification event-rows and conversation watermarks answer structurally
different questions:
- A notification is a discrete point-in-time fact; once read, it stays done.
- A conversation is a durable object you reopen; "unread" means "messages after where I left off,"
  and marking-read is one timestamp write that clears an unbounded set in O(1).

Generating a `Notification` row on every incoming PM would create **two unread truths** (the
watermark AND the notification's read flag) that must be kept in sync, cleared in two inboxes, with
the attendant sync bugs. A notification would also be a pointer with no content of its own, layered
over a message that already has a first-class home and its own unread boundary.

**Rule:** private messages **never** create `Notification` rows. `INotificationWriteService` is not
injected by `IMessagingWriteService`. The global unread-messages badge (a `MessagesNavLink` component
in the Desktop/Mobile layout chrome) calls `IMessagingReadService.GetUnreadConversationCountAsync()`
directly — that badge is the cross-cutting signal; the notification bell is for social/content events.

### 1-on-1 only

Group conversations are out of scope. The N-participant data model (`ConversationParticipant`) is
kept, but the compose flow always targets a single recipient and every conversation exactly two
participants. Group discussions happen in public on-site (`/group/…`) or off-site.

## Rich Text & Sanitization

All user-submitted HTML is sanitized **server-side** with `HtmlSanitizer` (allow-list) before saving.
Never trust client sanitization, never persist raw user HTML.

**EditorView** (universal across all text surfaces): chapters, comments, author notes, descriptions,
recommendations, profile bios, blog posts, AND private messages. Desktop shows full toolbar; mobile
shows compact toolbar with overflow for less-used formatting **(deferred — WU6 shipped desktop only;
not MVP-blocking, see `layer3.5-structure.md` "Third-Party Wrapper Composite")**.

## Aspire 13 Configuration

Live since 2026-07-05 (WU-Aspire). AppHost defines dev containers; it **never deploys**. The
authoritative resource graph is `AppHost/AppHost.cs` itself (Postgres 18 on host port 5433 →
`canalavedb`; Redis as `cache` on 6379; MinIO pinned-image `AddContainer` on 9000/9001; web =
Server's `http` launch profile → 5028). Run/stop/wipe workflow: `run-server/SKILL.md` "Aspire path".

Standing rules, all applied in `AppHost.cs`:

- **Version alignment is a correctness constraint:** the `Aspire.AppHost.Sdk` (the csproj's
  top-level SDK since 13.x — it encapsulates `Aspire.Hosting.AppHost`, no explicit package),
  every `Aspire.Hosting.*` package, and any app-side `Aspire.*` client package ride the same
  version (13.4.6). DCP + dashboard are delivered through the SDK's NuGet pin — a mismatched SDK
  runs mismatched orchestration binaries. `aspire update` bumps the whole set.
- **Persistent-lifetime containers + named volumes + stable secrets.** All three backing services
  use `WithLifetime(ContainerLifetime.Persistent)`, `WithContainerName("canalave-…")`, and named
  `-data` volumes. Passwords are `AddParameter(..., secret: true)` from AppHost **user secrets**
  (`Parameters:postgres-password`, `Parameters:minio-password`) — a stable password is mandatory
  once a data volume exists (a regenerated one no longer matches the initialized cluster).
- **The Server consumes plain connection strings.** `WithReference(canalaveDb)` injects
  `ConnectionStrings__canalavedb` as an env var, which overrides `appsettings.Development.json`;
  plain `GetConnectionString("canalavedb")` + `AddDbContext` just works. **No Aspire Npgsql EF
  client package** — `AddNpgsqlDbContext` stays banned per `layer2-services.md` "DbContext
  Registration" (pooling vs. Scoped `IActiveUserContext`). The `cache` container is provisioned
  but consumed by nothing at N=1 — it exists for the deferred N≥2 Valkey/RESP body-swap of the
  in-process signal buffers (`layer2-services.md` §"Signal Buffering"; Layer 7 dissolved
  2026-07-06).
- **The dev S3 endpoint is Garage (`dxflrs/garage`), a plain `AddContainer`** — settled 2026-07-05,
  superseding the spec's MinIO (OSS archived 2026-02; its Aspire toolkit package deprecated).
  `--single-node --default-bucket` bootstraps layout/key/bucket from `GARAGE_DEFAULT_*` env vars
  (restart-idempotent); config bind-mounted from `AppHost/garage.toml`, whose `s3_region` must
  match the injected `ImageStorage:S3:Region` ("garage"). The client side is **the same AWS S3 SDK
  code** as Cloudflare R2 in prod — interchangeable only under the three wire-format constraints
  centralized in `S3ImageStorageService.CreateClient` (unchunked uploads, WHEN_REQUIRED checksums,
  path-style); rationale + R2 specifics: `audit/ImageStorage.md` "R2 interchangeability".
- **ServiceDefaults** holds shared cross-cutting config (telemetry, health checks, resilience);
  under the AppHost the OTLP env vars light up the dashboard's logs/traces/metrics automatically.

**Dual-Configuration Strategy:** `dotnet ef` CLI cannot see AppHost's configuration. Design-time EF
tooling and the server-only run path both read the Server project's own configuration
(`appsettings.Development.json` → local Postgres 5432); the AppHost env-var injection only exists
at Aspire runtime. The two run paths therefore point at **different databases** by design.

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
