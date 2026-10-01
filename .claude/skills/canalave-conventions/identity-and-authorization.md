# Identity & Authorization

The scoped active-user abstraction, the two-identity-source rule that keeps SharedUI WASM-safe, the
seven kinds of active-user conditionality, and cookie/role-based authorization. Split out of
`cross-cutting.md` (2026-07-07) as its own coherent theme. The Class-A/Class-B viewer-permission
split this file enforces is modeled in `.claude/design/access-gating-first-principles.md`
(authoritative) with the per-surface inventory in `access-gating-audit.md`.

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
    bool IsModerator { get; }          // server-side enforcement input (RequireModerator) + query shaping — the UI affordance half lives in AuthorizeView
    bool IsAdmin { get; }
}
```

`ServerActiveUserContext` (Server/Identity/) is scoped, populated once per circuit from
`AuthenticationState`/claims. `IsModerator`/`IsAdmin` are not the *UI* auth surface — `AuthorizeView`/
policies own affordance (what renders) — but they ARE the server-side enforcement input: service-layer
`RequireModerator()` and `entity.OwnerId == UserId` checks are the enforcement point of record (see
§"Authorization Has Two Enforcement Surfaces" below), and they also decide when a query legitimately
calls `IgnoreQueryFilters`.

**The two shared guards** live in `Core/Identity/ActiveUserContextExtensions.cs`, one copy each (owner
ruling D9):
- `activeUser.RequireUserId()` — anonymous → `InvalidOperationException` (→ 401); returns the id.
- `activeUser.RequireModerator()` — anonymous → `InvalidOperationException` (→ 401); signed in without
  the Moderator or Admin role → `UnauthorizedAccessException` (→ 403); returns the id. One pair of
  exception semantics for every role gate, read or write: before the extraction, three copies answered
  an anonymous caller 403 and the rest 401.

Every moderator role gate, read or write, calls `RequireModerator()` — WU-ModerationIntegrity
(2026-09-30) removed every private role-guard copy and the six private `RequireAuthenticatedUser`
*methods*. **Not yet uniform:** inline authentication checks of the same meaning (`if (ActiveUser.UserId
is not int id) throw new InvalidOperationException(…)`) remain in several write services (Comments,
BlogPosts, Polls, Groups, Spotlight, Stories, Fanon); they behave exactly like `RequireUserId()`. New
code calls the extension; converting the inline sites is service audit §4's pattern-uniformity residue
(WU-DocCorrections files it on the tracker).

**What stays out, deliberately:** display name/avatar URL (presentation — comes via `UserCardDto` per
view); `ReaderDisplaySettings` (already a separate cascading slim bag, a UI-layer concern — see
`layer3.5-structure.md` "Ambient Viewer Settings via Cascading Slim Bags"); notification/messaging
prefs (feature-local, read where needed). Collapsing those back into this context would make it grow
without bound.

Two consumers justify the abstraction independently: the content-rating query filter
(`content-safety.md`) and the `Theme` slug + `PrefersAnimatedSprites` arguments passed to the root
`ThemeContextProvider` (see `render-and-layout.md` §"ThemeContext Cascading Provider") — both
previously had no defined source for "the current user."

## Viewer Consent State (WU-AccessGate, 2026-07-19)

Three additions extend the viewer context for the three-plane access model
(`content-safety.md` §"The Three-Plane Access Model"):

**Anonymous prefs cookie.** One first-party JSON cookie —
`{ mature: bool, revealedStories: [ids], revealedGroups: [ids], revealedBlogPosts: [ids] }` —
**SameSite=Lax** (it must ride inbound external links; Strict would re-gate every Discord/search
arrival), **not HttpOnly** (WASM parity), 180-day sliding expiry, ~50-reveal LRU cap. For
anonymous viewers `ServerActiveUserContext` resolves `ShowMatureContent` from `cookie.mature`
(lazily, same guarded pattern as its principal cache — never in the constructor; the
`SecurityStampValidator` early-capture hazard applies to any constructor-time HttpContext read).
The cookie also feeds the `GroupAudience` filter — an anon mature toggle widens group visibility
too, by design. **WASM parity is deliberately not implemented** (refined at build time,
2026-07-19): `WasmActiveUserContext` has no anon-cookie read because nothing consumes it there —
SharedUI components never inject `IActiveUserContext` (the two-identity-source rule below), and
every data read crosses HTTP to the server, where the cookie rides the request and the server
context enforces. If a WASM-side consumer ever appears, `PersistentComponentState` is the
channel (the framework's auth-state serialization cannot carry anonymous data) — do not reach
for `document.cookie` interop, which clashes with the context's synchronous resolution.

**Reveals.** Logged-in consent is durable in `user_content_reveals`
(user id, entity type Story/Group/BlogPost, entity id) — *not* a cookie (cross-device, no size
cap, server-readable on every path including export anchor GETs, revocable from `/settings`).
Anonymous consent lives in the cookie lists above; on login, anon reveals are discarded
(re-consent is one click). Reveal checks compose in read-service queries (the context itself
stays claims/cookie-only and DbContext-free — no circular dependency). Reveals affect the
Direct-navigation plane and the revealed item's own subtree only; they never widen Discovery.

**Responsive `ShowMatureContent` (MA-605 closed).** The claim is reissued the moment the setting
changes: the set-mature action (settings form and interstitial "Always show mature content"
alike) runs through a full-document server endpoint that writes the DB, calls
`SignInManager.RefreshSignInAsync`, and 303-redirects — the redirect also rebuilds the circuit,
which is required anyway (a circuit's viewer context is frozen; see `ServerActiveUserContext`'s
principal cache). Anonymous: same endpoint shape, Set-Cookie instead of Identity. Consent
endpoints (reveal story/group/blog-post) follow the identical POST→303 pattern.

**Verified crawlers.** `VerifiedBotMiddleware` stamps `HttpContext.Items` (pattern:
`SecurityHeadersMiddleware`), surfaced as `IsVerifiedBot` on the context. Config-gated by
`Seo:TrustVerifiedBots` (default **false**) — do not enable before Phase 7's Cloudflare
ForwardedHeaders/origin-lockdown lands, or the elevation is spoofable by direct-to-origin
requests. Crawlers only ever hit the SSR/prerender pass, which always has an HttpContext.

## Account Status Is Display-Only, Read Live — Not a Claim-Freshness Problem (WU-AccountEnforcement, 2026-07-30)

`AccountStatus` looks like it belongs in the same "baked claim, needs a cookie reissue to update"
family as `ShowMatureContent` above — it is not. `ShowMatureContent` is a **query-shaping** claim
(`IActiveUserContext.ShowMatureContent` feeds the content-rating filter and other query decisions),
so a stale value silently changes what a request returns — that's what forces the
`RefreshSignInAsync` + 303 round-trip. `AccountStatus` (`ActiveUserClaimTypes.AccountStatus`) has
exactly one consumer, `AccountStatusBanner`, and is never used for query-shaping or authorization
(enforcement is `CanalaveSignInManager.CanSignInAsync` at login + the security-stamp bump at
Suspend/Ban — see `security.md` "Account-Status Enforcement" — neither reads this claim).

Because the value is purely a display concern, `AccountStatusBanner` doesn't need the cookie
reissued at all — it needs a **fresh read**, which is a much smaller problem than a fresh *claim*.
It re-queries `IAccountStatusReadService.GetMyAccountStatusAsync()` on
`NavigationManager.LocationChanged`, following the same in-circuit refresh pattern
`MessagesNavLink` already used for its unread-count badge (no document reload, works identically on
the server circuit and on WASM). The baked claim still supplies the first-paint value only, so
there's no flash before the first live read lands.

**Rule of thumb when a new baked claim goes stale:** if the claim shapes a query or an authorization
decision, it needs the `/content-gate/refresh-claims`-style full-document round-trip. If it's purely
what gets displayed, prefer a small live-read service re-queried on navigation instead — cheaper,
and it works the same on both render modes without touching the cookie.

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

### Seven kinds of active-user conditionality

| # | Kind | Mechanism | Established |
|---|---|---|---|
| **(a)** | Data filtering / query-shaping ("mature off ⇒ no trace"; sprite theme) | Server: `IActiveUserContext` in read service / EF global query filter | WU12 |
| **(b)** | Authentication gate ("is anyone logged in?") | UI: `<AuthorizeView>`. Server write: `IsAuthenticated` guard before any mutation | WU1 |
| **(c)** | Role gate (mod/admin-only surfaces) | UI: `<AuthorizeView Roles="Moderator,Admin">`. Mod pages: `[Authorize(Policy=…)]`. Server: every mod-only read **and** write calls `ActiveUser.RequireModerator()` in the service; the endpoint's `RequireModerator` policy is the edge half (§"Role-Based (Moderator) Gating") | WU28/WU34; service half on reads D9, WU-ModerationIntegrity |
| **(d)** | Ownership gate ("is the viewer the owner of *this specific entity*?") | UI: page computes bool, passes down; component uses plain `@if`. Server: service loads entity, compares `entity.OwnerId != activeUser.UserId`, throws | UI: WU13/WU14. Server: WU24+ |
| **(e)** | Per-viewer state ("has the viewer favorited / liked / started this?") | Server read service projects per-viewer flags via `IActiveUserContext.UserId` into the DTO | WU15/WU19 |
| **(f)** | Owner-or-staff gate — **does not exist in this codebase.** | Editing is **author-only** (strict identity-equality). Moderation is a **separate code path** (WU34 admin service). Never an `OR` fold. | WU24 |
| **(g)** | **Parent-visibility inheritance** ("is the content that *hosts* this child visible to the viewer?") | Child read service calls the parent's guard and returns empty; child write service calls it **on raises** and throws `KeyNotFoundException`; clears on the caller's own existing row are unguarded (D6). See §"Parent-visibility guards" below | WU-ParentVisibility; raise/clear split WU-AccessGateSweep2 |

### Parent-visibility guards

**The invariant: child content is never more visible, nor more *raisable*, than the parent content
that hosts it.** A poll is exactly as visible as its blog post; a comment as its chapter; a group's
member roster as the group. A write that creates or raises state under a hidden parent is refused; a
write that clears or lowers the caller's own existing state under it is always permitted — see
§"Raises vs clears" below.

This rule was violated on **38 surfaces** before WU-ParentVisibility (13 reads, 25 writes) for two
structural reasons, both worth internalizing:

1. **The bare-FK shape defeats query filters.** `readDb.Children.Where(c => c.ParentId == id)` filters
   on the *scalar FK column*, so the parent entity type never enters the query — EF emits no join, and
   therefore **no named query filter** (`ContentRating`, `GroupAudience`, `StoryStatus`, `IsTakenDown`)
   and no reveal check can possibly apply. A filter on `Group` protects nothing in a query that only
   mentions `GroupComment.GroupId`. This is the same insight as the *join-not-bare-projection rule*
   (`layer2-services.md` §StoryLineage / §Spotlight) — stated here because this is the file developers
   consult when writing a service.
2. **`writeDb` has no filters at all.** The visibility filters are declared only on
   `ReadOnlyApplicationDbContext`. `ApplicationDbContext` is unfiltered by design, so every
   `writeDb.X.AnyAsync(x => x.Id == id)` existence check sees drafts, M-rated, and taken-down rows.
   **An existence check is not a visibility check.** Never write a comment claiming a filter protects a
   write path — two such comments were shipped and both were false.

**Satisfy the invariant one of three ways** (in preference order): call the parent's guard explicitly;
join the *filtered* parent DbSet (`join s in readDb.Stories`); or constrain against a captured filtered
subquery (`visible.Any(s => s.StoryId == …)`).

**The guard set.** One guard per parent kind — deliberately *not* one universal guard, since the parents
differ in columns and in reveal target:

| Guard | Parent hidden when |
|---|---|
| `BlogPostVisibilityGuard` | unpublished (to non-authors) · **profile post whose author's `ProfileVisibility` hides it from this viewer** (Class A — a blog post is profile-tab data, so it is not bot-bypassable; group and site posts carry no profile check — WU-AccessGateSweep2) · rating above viewer ceiling without a reveal (`RevealedEntityType.BlogPost` for profile posts, `.Group` for group posts) · group audience M with mature off · taken down |
| `StoryVisibilityGuard` | rating above ceiling without a `RevealedEntityType.Story` reveal · status `Draft`/`PendingApproval`/`Rejected` · taken down. `IsChapterVisibleAsync` adds: chapter unpublished (to non-authors) |
| `GroupVisibilityGuard` | audience rating M and the viewer has mature off |
| `ProfileVisibilityGuard` | Private to non-owners · UsersOnly to anonymous viewers (predates this WU). Has the pure overload `IsVisible(ProfileVisibility?, viewer, profileUserId)` since WU-AccessGateSweep2, which `BlogPostVisibilityGuard` calls with the author's projected setting |

Every guard follows one contract shape:

```csharp
static Task<bool> Is{Parent}VisibleAsync(ReadOnlyApplicationDbContext, IActiveUserContext, int id);
static bool IsVisible(<Facts> facts, IActiveUserContext, bool isRevealed);   // pure overload
```

The **pure overload owns the rule**; the id overload loads the facts and delegates. Callers that
already project the parent's columns (e.g. `ServerBlogPostReadService.GetByIdAsync`) pass facts and pay
no extra query; callers holding only an id use the loader. One copy of the rule, no forced round-trip.

**Four rules that are easy to get wrong:**

- **Non-disclosure.** A hidden parent and an absent parent must be indistinguishable — same status,
  same body, same error text. Reads return empty/null; writes throw `KeyNotFoundException` (never
  `UnauthorizedAccessException`, which would confirm the id exists). Precedent: D3.1's cross-group
  folder id.
- **Authors keep their drafts.** The author of an unpublished parent always passes. An over-broad guard
  that breaks the blog editor's poll panel or a chapter draft flow is a worse bug than the leak.
- **Takedown outranks authorship.** A taken-down parent hides from its author too — `IsTakenDown` is
  not author-conditional.
- **Exemptions are narrow.** Moderator work surfaces bypass this entirely (`content-safety.md`
  §"Moderator review surfaces are work surfaces"); verified bots follow the existing `IsVerifiedBot`
  elevation. Buffered lossy writes (view counts, reading progress) validate at **drain time** in the
  flush worker, never at entry — a per-request query would negate the buffer's reason for existing.

#### Raises vs clears (owner ruling D6, 2026-08-04; built WU-AccessGateSweep2, 2026-09-30)

**A flag raise keeps the full guard; a clear or lower on the caller's own existing row is never
visibility-guarded — on all three axes (content rating, lifecycle status, takedown).**
Authentication is unchanged: an anonymous clear still throws first.

*Why.* A guard exists to stop **new disclosure** and **new entanglement**: raising a bit on a guessed
id increments another user's public counter and enrolls the actor in a hidden parent's fan-out. A
clear does neither — it removes the actor's own row, lowers a counter, and withdraws them from a set.
Refusing it protects nothing: the read plane already tells the client which hidden stories it has
rows on (`GetStatesByStoryIdsAsync` is a bare-FK query on an unfiltered entity,
`ServerUserStoryInteractionReadService`), so the guard only costs the user their own data — a
taken-down story would otherwise freeze every reader's rows permanently, and since D1 any author can
freeze them by unpublishing. The site already applied this rule wherever a verb pair exists
(`FollowAsync` guarded / `UnfollowAsync` not; `JoinAsync` / `LeaveAsync`); the whole-state setters
diverged only because one guard at the top covered both directions.

*Mechanism — load first, diff, then decide.* Load the caller's existing row, diff it against the
payload, and call the guard only if some bit goes **false→true**. Riders:
- **Mixed payloads:** any raise anywhere guards the whole call — no per-bit partial application, so
  the guard runs before the first property is assigned.
- **No row ⇒ every true bit is a raise ⇒ guarded.** The guessed-id enumeration stays blocked.
- **No row + all-false payload ⇒ silent no-op, returned before any guard,** so hidden, absent and
  nonexistent parents are indistinguishable (no oracle).
- **Sparse cleanup is a clear.** An all-false update deleting the row is permitted on a hidden parent.
- **A clear path performs no parent existence check either** — `writeDb` sees hidden rows, so "hidden
  but real succeeds, absent 404s" is itself an oracle. A clear on a nonexistent parent is a silent no-op.

*Toggles.* A toggle whose caller already holds the row (a like) is a clear by definition: an existing
like row makes the call an unlike, which skips the guard; the row's FK proves the parent exists.

*A clear's response is a read, and stays gated.* Ungating the write does not ungate what the call
returns. Where a clear's contract returns parent data, the response goes through the same guard as any
read: on a hidden parent it carries nothing — an unlike returns `(LikeCount: 0, IsLiked: false)`, a
poll-vote withdrawal returns `null` — so the clear lands and the caller learns nothing about the
hidden parent's current state (D6: "deleting your own row teaches them nothing"). *(Corrected by the
WU-AccessGateSweep2 review fixes, 2026-09-30: the build first returned the hidden parent's
post-toggle `LikeCount`, justified as "the caller's row proves prior access" — a derivation, not owner
text, and it disclosed a current aggregate the guard withholds from every read.)*

*Poll votes.* `VoteAsync` is a replace-set, so it is diffed like the USI panel: a **pure withdrawal**
— the caller holds votes on the poll and the payload only drops some or all of them, keeping the
kept rows' anonymity choice unchanged — is a clear and skips the guard. Adding an option, or flipping
anonymity on a kept row, edits what others see and guards the whole call (the anonymity half is the
review fixes' conservative reading, not owner text: it keeps the guard that was already there, and
only row deletion is unambiguously "deleting your own row"). With no existing vote there
is nothing to withdraw, so the guard runs and a hidden poll answers exactly like a missing one
(`KeyNotFoundException`). The "poll must be Open" rule is not a visibility guard and still applies to
withdrawals.

*Read-mark asymmetry.* `SetChapterReadAsync(id, false)`/`SetAllChaptersReadAsync(id, false)` are
ungated clears; the `isRead: true` paths keep the full guard, and their cascade into
`MarkStartedAsync`/`MarkCompletedAsync` stays guarded because those only ever *set* bits (raises by
construction). `HasStarted` remains non-clearable by any surface (`Has-` = permanent past event).

*Counter consequence (accepted).* Clearing a favorite on a hidden story decrements the author's
`FavoritesOnStories` — the favorite is genuinely withdrawn; recompute (D21) treats
withdrawal-while-hidden as ground truth, not drift.

*Raises on the caller's own content are out of scope.* `SetHiddenGemAsync(true)` and
`SetHighlightedByAuthorAsync(true)` raise flags on the caller's own recommendation with no parent
guard; which axis applies to them is unruled (tracker **F10**).

**Enrolment is the enforcement.** `Tests.Integration/ParentVisibilityContractTests.cs` holds a table of
every governed surface and asserts, per hidden-parent kind, that reads come back empty and raises are
refused — and, since WU-AccessGateSweep2, that clears on the caller's own row succeed (one test per
axis, plus the conformance of every unguarded sibling clear). A new parent-scoped read or write is
registered by adding a row. Docs alone already failed
once: the rule existed in `layer2-services.md` and the WU-AccessGate sweep still missed
`GetUserNeighborsAsync`, leaving a Private profile's contents anonymously readable.

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
Story: `/story/{id}/edit` (WU24) vs `/story/{id}/{slug}` (WU25). The third segment is shared by
the editor literal, the chapter number and the slug, so `StorySlug.IsReservedRouteSegment` (`edit`,
any integer) is the one rule both sides read: the canonical-slug 301 middleware never treats such a
segment as a stale slug, and the slug generator never mints one (it suffixes: `edit-2`). Before the
2026-09-30 browser pass the middleware 301'd every full load of a published story's editor to the
story page.
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
Configured for **401/403 status codes, not 302 redirects** (critical for WASM API calls). **401 is
a session-expiry signal, not an authorization denial** — the API client reconstructs every 401
(cookie-handler bare 401 or a service's `InvalidOperationException`→401 arm alike) as
`SessionExpiredException`, presented as "sign in again," never conflated with a genuine
authenticated-but-forbidden 403 (`UnauthorizedAccessException`). See `error-handling.md` §"The API
error envelope" / `layer5-wasm.md` §"The Error-Translation Contract" (WU-ErrorHandling2,
2026-07-30).
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

### Authorization Posture: Default-Allow (a Default-Deny Recipe Retained, Never Implemented)

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
(Moderation writes + queue reads, ExternalVerification queues, SiteDailyStat, SpotlightSlotAllocator,
SiteSettings) uses it as the edge half of the defense-in-depth pair; the service-side
`RequireModerator()` remains the enforcement point of record. Distinct from `<AuthorizeView Roles="...">`
(layer3.5-structure.md) — that's for moderator-only controls embedded in an otherwise-public page,
not for gating the dedicated `/mod/*` routes.

**Mod-only reads gate in the service too (owner ruling D9, 2026-08-06; built WU-ModerationIntegrity
2026-09-30).** A page attribute does not protect the circuit: on the SSR circuit no endpoint exists, so
a mod page's in-process read service call had only the page's `[Authorize]` between it and the caller,
and these are the reads that turn every query filter off (taken-down content, M-rated stories, Private
profiles, other users' report history). So every mod-only read opens with `ActiveUser.RequireModerator()`
and **throws** — never returns empty: a mod queue's existence is not a secret, and `[]` would make a
mis-registered surface look empty rather than broken. The "reads gate at the edge, writes in the
service" split is not available as a rule (it would contradict P2 and the two read-side gates already in
`ServerBlogPostReadService`). The recorded sweep:

| Service | Gated reads |
|---|---|
| `ServerModerationReadService` | `GetReportQueueAsync`, `GetPendingSubmissionsAsync`, `GetUserModerationHistoryAsync` |
| `ServerExternalVerificationReadService` | `GetPendingAccountVerificationsAsync`, `GetPendingLinkVerificationsAsync` |
| `ServerSiteDailyStatReadService` | `GetLatestAsync`, `GetSeriesAsync` |
| `ServerSpotlightSlotAllocator` | `GetRemainingMonthlyGrantCapacityAsync`, `GetRecentGrantsAsync` |
| `ServerBlogPostReadService` | `GetSiteAnnouncementForEditAsync` (and `GetSiteAnnouncementsAsync`'s unpublished view, which *downgrades* a forged flag rather than throwing) |

**The one deliberate non-gate:** `ISiteSettingsReadService.GetIntAsync` is not a mod-only read at the
service layer — Spotlight's read and write services, the allocator and the Fanon services compose it
server-side for public flows. Only its HTTP route (`GET /api/site-settings/{key}`) is mod-only, and that
route keeps its edge policy. `IModerationReadService` no longer carries `GetReportReasonsAsync`: report
submission moved to `IReportSubmissionService` (any signed-in reporter), so the mod interfaces hold only
gated members.

**Role infrastructure status (updated WU27.5, 2026-06-24):** Role *rows* (`User`, `Moderator`, `Admin`)
are seeded via `ApplicationRoleConfiguration.HasData` in `IdentityConfigurations.cs` — they exist at
migration time. `DataSeeder.cs` previously only assigned `AdminUser` to `"Admin"`; **WU27.5 closes the
gap by also assigning `AdminUser` to `"Moderator"`**, so the role gate is exercisable end-to-end in
dev. `IsInRole` is literal — there is no automatic Admin-inherits-Moderator hierarchy, so every gate
that should accept either role must list both: `Roles="Moderator,Admin"` / `.RequireRole("Moderator",
"Admin")` / `IsModerator || IsAdmin`.
