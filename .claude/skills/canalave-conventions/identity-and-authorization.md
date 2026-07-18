# Identity & Authorization

The scoped active-user abstraction, the two-identity-source rule that keeps SharedUI WASM-safe, the
six kinds of active-user conditionality, and cookie/role-based authorization. Split out of
`cross-cutting.md` (2026-07-07) as its own coherent theme.

## Active-User Context

`IActiveUserContext` (Core/Identity/) is the scoped "who is the current viewer" companion to the `User`
entity — minted WU12, because the content-rating filter (`content-safety.md` §"Content Rating
Filtering") needs a per-request source and no such abstraction existed yet. Holds only hot scalar
fields, never the full entity (defeats the hot/cold partition design and the DTO Firewall):

```csharp
public interface IActiveUserContext
{
    int? UserId { get; }              // null = anonymous
    bool IsAuthenticated { get; }
    bool ShowMatureContent { get; }    // feeds the content-rating filter (content-safety.md)
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

Two consumers justify the abstraction independently: the content-rating query filter
(`content-safety.md`) and the `Theme` slug + `PrefersAnimatedSprites` arguments passed to the root
`ThemeContextProvider` (see `render-and-layout.md` §"ThemeContext Cascading Provider") — both
previously had no defined source for "the current user."

## Active-User-Conditional Handling

### The two identity sources — which to use where

The app reads the same underlying claims through two deliberately separate layers. They are not
interchangeable; which one you use is determined by where you are:

| Source | Lives in | Lifetime | Use it for | Never for |
|---|---|---|---|---|
| **`IActiveUserContext`** (Core/Identity) | **Server services** (scoped, claims-only, no DbContext) | Per-circuit/request | Query-shaping (content-rating filter, sprite theme), per-viewer DB projections, **and server-side authorization** (`UserId` equality, `IsModerator/IsAdmin`) | Injecting into any SharedUI component |
| **Blazor `AuthenticationState` / `<AuthorizeView>`** | **UI** (routable pages + components) | Cascading | Showing/hiding affordances, role-gated markup, resolving `CurrentUserId` at the page level to pass down as a parameter | Any decision you actually rely on for security |

**Rule:** *SharedUI components resolve identity from the `AuthenticationState` cascade, not by
injecting `IActiveUserContext`.* The dispatcher/routable page resolves identity from
`[CascadingParameter] Task<AuthenticationState>` and passes ownership down as a parameter (bool
`IsOwnStory`, `IsOwnComment`, `IsEditable`, or `int? CurrentUserId`). `StoryDeck`, `StoryCard`,
`CommentItem`, and `VouchList` already do exactly this.

*Why the rule survives the Global Flip (BB-03, 2026-07-18):* the original justification — "the
interface won't exist in a WASM Client" — is dead: `WasmActiveUserContext` is registered in
`Client/Program.cs`, so injecting it no longer breaks the WASM build. The rule is retained as a
**consistency/testability discipline**, not a compile constraint: one identity source per layer
keeps components parameter-driven and bUnit-testable without fake user-context registrations.
**Ratified bounded exceptions (the only two):** `Layout/UserActivityTracker.razor` (fire-and-forget
activity ping — needs the hot scalar, renders nothing) and `Profiles/SettingsPage.razor`
(self-scoped settings dispatcher). Don't add a third without recording it here.

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
Progressive disclosure of the version concept on the edit page: `layer3.5-structure.md` §"Chapter
Versioning — Progressive Disclosure".

**Pattern 2 — Lightweight embedded content (comments, recs, vouch text): in-place inline edit.**
One page; edit mode is parent-owned (e.g. `CommentSection._editingId`). The item being edited swaps
`EditorView` (via `CommentEditor`) in place; siblings render `RichTextView`. Both renderers co-exist
by design. No dedicated edit route. [`CommentItem`](../../../../TheCanalaveLibrary.SharedUI/Comments/CommentItem.razor#L42-L78)
is the reference.

## Identity & Auth

`AddIdentityCore<User>().AddRoles<ApplicationRole>()` with `int` keys.
Cookie auth via `AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies()`.
Configured for **401/403 status codes, not 302 redirects** (critical for WASM API calls).
`RequireConfirmedAccount = true`.

Data Protection: `PersistKeysToFileSystem` (dev), `PersistKeysToDbContext` (prod).
Email: pluggable SMTP seam (`Email:Provider` = `Smtp`/`NoOp`, mirrors `ImageStorage:Provider`) —
`IdentityNoOpEmailSender` when unconfigured (server-only dev; its `RegisterConfirmation.razor`
on-page link auto-hides once a real sender is registered), `SmtpEmailSender` (MailKit) against
Mailpit under the Aspire dev path, and against the real transactional provider (host/credentials
only, chosen at Phase 7 — decision row 8) in production. See `middle_plan_v2.md` Resolved "Email
mechanism."

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

**Operative posture (recorded 2026-07-18, MA-104): the app runs default-allow.** The MVP
fallback-policy block below was never implemented — anonymous browsing has been the verified-normal
behavior since the audit trail began, and public reads are deliberate (stories, profiles, discovery).
The doc's companion requirement for the default-allow posture — "re-audit every endpoint's
`.RequireAuthorization()`" — was satisfied by the 2026-07-18 systematic endpoint-authorization sweep
(all 38 `*Endpoints.cs` files; see `workplan.md` WU-AuditFixPass-2). The block below is retained as
the recipe should a default-deny phase ever be wanted, not as a description of current state.

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
uniformly to mod pages and their backing endpoints. **This policy is registered** (Program.cs,
MA-702 fix 2026-07-18) with its name exposed as the `AuthorizationPolicies.RequireModerator`
constant (`Server/Identity/AuthorizationPolicies.cs`) — every mod-only endpoint group
(Moderation writes + queue reads, SiteDailyStat, SpotlightSlotAllocator, SiteSettings) uses it as
the edge half of the defense-in-depth pair; the service-side `RequireModerator()` remains the
enforcement point of record. Distinct from `<AuthorizeView Roles="...">`
(layer3.5-structure.md) — that's for moderator-only controls embedded in an otherwise-public page,
not for gating the dedicated `/mod/*` routes.

**Role infrastructure status (updated WU27.5, 2026-06-24):** Role *rows* (`User`, `Moderator`, `Admin`)
are seeded via `ApplicationRoleConfiguration.HasData` in `IdentityConfigurations.cs` — they exist at
migration time. `DataSeeder.cs` previously only assigned `AdminUser` to `"Admin"`; **WU27.5 closes the
gap by also assigning `AdminUser` to `"Moderator"`**, so the role gate is exercisable end-to-end in
dev. `IsInRole` is literal — there is no automatic Admin-inherits-Moderator hierarchy, so every gate
that should accept either role must list both: `Roles="Moderator,Admin"` / `.RequireRole("Moderator",
"Admin")` / `IsModerator || IsAdmin`.
