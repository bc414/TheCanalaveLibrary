# Grid Axes — Layers and Features for the SLF Table

Companion to `step3_classify.md`. Defines the columns (layers) and rows (features) of the
Feature × Layer grid. Each cell holds a Stage value (1–6) or N/A per CLAUDE.md's definitions.

---

## The Two Boundaries

The grid has two boundary lines that encode intentional deferral, not priority ranking.

### Vertical boundary: after Layer 4 (the MVP line)

Layers 1–4 are the minimum viable product on Blazor Server (`InteractiveServer` globally, the
spec-sanctioned dev shortcut). Everything a feature needs to function end-to-end: data in Postgres,
a service that reads/writes it, components with working interaction logic, correct component
composition, and visible styled UI. The original two-layer UI split (Layer 3 + 4) is refined into
three dimensions: Layer 3 (Logic), Layer 3.5 (Structure), Layer 4 (Style).

The **vertical-line test**: can this feature's Layer 1–4 contract — DTO shapes, `IXService` method
signatures, component parameter contracts — be fully defined now, with *some* correct implementation
behind it, such that everything past the line only ever changes what's *behind* the contract and
never the contract itself?

Layers 5–8 are additive. They change method bodies, add new classes, or add DDL — but never alter the
interfaces, DTOs, or component contracts established in 1–4. Nothing in 1–4 has to change to add them,
and nothing in 1–4 has to change again if you redo them. Layers 5–8 are also naturally batchable: indexes
are pure DDL applied across many tables at once; WASM enablement applies the same endpoint + HttpClient
wrapper pattern to N stable interfaces; data mart workers are standalone classes with no interface
callers. (The same body-swap property is how a signal-buffer body — an L2 concern — later swaps its
in-process store for a shared Valkey store at N≥2 nodes, with no contract change.)

### Horizontal boundary: features requiring real user data

Most features can reach a meaningful Stage 5 with seed data and synthetic test users. A small set cannot
— even a correct Layer 1–4 implementation can't be meaningfully validated because the feature's output is
only interesting with real cross-user interaction patterns:

- **Automatic Tree Search** — a recursive CTE against five test users produces a degenerate one-node graph
  regardless of how correct the query is.
- **Also Favorited / Also Recommended** — co-occurrence scoring with two test users tells you nothing about
  ranking quality, exclusion-filter behavior, or UI density.

The trigger for un-deferring these is concrete and observable: "real interaction data exists now," after a
beta period. The *signal-producing* features they depend on (Recommendations, Favorites, Following, Manual
Tree Search) all belong above the line and ship in the MVP pass.

A few other features naturally sort late but for a different reason — there's nothing to operate on yet
(Notification Cleanup has nothing 60 days old to clean; SiteDailyStat has no usage to aggregate). These
don't need their own line; they fall out of normal workplan sequencing.

---

## Layers (Columns)

### Layer 1 — Data Model

EF Core POCOs in the **Core** project, Fluent API configuration in `OnModelCreating`, and migrations.

**Stage-2 planning covers:** table/column/relationship shape, TPT `.ToTable()` calls, vertical-partition
splits (hot/warm/cold), enum vs lookup-table vs hybrid classification, delete behaviors
(`.OnDelete(DeleteBehavior.X)` on every relationship), column types (`timestamp(2) with time zone`,
`string` with `[MaxLength]`), `HasDefaultValueSql` for creation timestamps, CHECK constraints and
triggers (manual migration edits), and composite-key/unique-constraint definitions.

**Stage-5 means:** POCOs compile, migration applies cleanly, column/table names match spec §7's
deliberated names after `EFCore.NamingConventions` conversion, delete behaviors are explicit on every
relationship, enum conversions use `.HasConversion<short>()`.

**Governed by:** `layer1-data-model.md`

### Layer 2 — Server Implementation

`IXService` interfaces + DTOs/records in **Core**, `ServerXService` implementations against
`ReadOnlyApplicationDbContext` (reads) and `ApplicationDbContext` (writes) in the **server** project.

**Stage-2 planning covers:** method signatures expressing query/command intent, DTO shapes (default: one
record per vertical-partition table, composed at the call site for cross-partition needs), the read/write
split per method (`.Select()` projection on `NoTracking` context vs tracked-entity load/mutate/save),
`.AsSplitQuery()` for multi-collection includes, and the key branch point for features with high-frequency
writes: durable intent writes directly through EF Core; a **loss-tolerant, coalescable signal** takes the
in-process signal-buffer body instead (buffer + `BackgroundService` flush — `layer2-services.md`
§"Signal Buffering"; the buffer store swaps to Valkey at N≥2 behind the same interface).

For features whose *purpose* is background computation — data-mart rebuilds, stat aggregation — Layer 2
is the worker itself (`IHostedService`/`BackgroundService` with raw SQL), not a service method fronting
a DbContext. These features may have no interactive service layer at all.

**Stage-5 means:** interface + DTOs + server impl compile, reads use `ReadOnlyApplicationDbContext` with
`.Select()` projections (never materializing entities then mapping), writes use `ApplicationDbContext`
with tracked entities, DTO firewall is intact (no EF Core model classes cross the service boundary).

**Governed by:** `layer2-services.md`

### Layer 3 — UI Logic (Contract + Behavior)

The `@code` block of Razor components in **SharedUI**: parameters, service injection, event handlers,
state management, lifecycle hooks. Pure C# — decidable from the spec and data model with no visual
design dependency.

**Contract** = what the component needs from outside and promises back: `[Parameter]` properties,
`EventCallback` declarations, `@inject` service references. **Behavior** = what the component does:
`OnInitializedAsync`, `[PersistentState]`, debounce timers, optimistic state updates, `EditForm` binding,
`RendererInfo.IsInteractive` branching.

**Component tiers** determine Logic weight:
- **Leaf** (TagChip, UserStoryInteractionButton, StoryCard): Parameters and EventCallbacks only. No service injection.
- **Composite** (ChapterNavigation, UserStoryInteractionPanel, ResultsFilterPanel): Pass-through parameters plus coordination
  state (debounce, mode toggles). Service injection only for genuinely independent concerns.
- **Page/Dispatcher** (StoryPage, ChapterPage, SearchPage): Service injection, route parameters, data loading,
  device detection, `[PersistentState]` on loaded DTOs, event coordination for child writes.

**Service injection principle:** inject when the component has a genuinely independent concern that cannot
be coordinated from above. The rigid constraint: pure display components showing pre-loaded data must
never inject read services (prevents N+1 in lists). Legitimate non-page injection: cross-cutting layout
elements (notification bell), user-input-driven queries (tag typeahead), self-contained writes (follow button).

**Stage-2 planning covers:** component tier classification, parameter/event contract, which services are
injected and by what principle, `[PersistentState]` decisions, debounce intervals, optimistic update
strategy, `EditForm` model binding to ViewModels/DTOs (never EF entities), and — for high-frequency
writes — whether the component's writes are durable intent (direct EF) or a buffered lossy signal
(interface unchanged either way; `layer2-services.md` §"Signal Buffering").

**Stage-5 means:** no double-fetch flicker (`??=` guard on persisted state), interactive elements work under
current render mode, forms submit with anti-forgery via `EditForm`, components inject interfaces only.

**Governed by:** `layer3-logic.md`

### Layer 3.5 — UI Structure (Composition + Skeleton)

The markup skeleton in **SharedUI** `.razor` files: which child Razor components form the tree, what
HTML elements exist, `@if`/`@foreach` conditions, `@ChildContent` slots, data flow through `[Parameter]`
to children, `<AuthorizeView>` placement. Decidable once the component system is known — before visual
design.

**Component tiers** determine Structure weight:
- **Leaf** (TagChip, RichTextView): Only raw HTML elements. `@if`/`@foreach` driven by parameters.
  No child Razor components by definition. Full structural weight.
- **Composite** (StoryDesktop, EditorView): Child Razor components composed with layout wrappers.
  `@if` for conditional children. `@ChildContent` for container composites. Main structural job.
- **Page/Dispatcher** (StoryPage): Thin — usually just `@if (isMobile) { <Mobile /> } else { <Desktop /> }`.

**Composite subtypes:**
1. Pass-through layout (StoryDesktop, StoryDeck) — arranges children, thin logic.
2. Coordination (UserStoryInteractionPanel, ResultsFilterPanel) — owns state spanning children.
3. Container (Card, Panel) — provides visual vessel via `@ChildContent`.
4. Third-party wrapper (EditorView) — adapts Quill/Typeahead to Blazor model.

**Composite introduction criteria:** introduce only when it has children, manages coordination state,
appears multiple times, or wraps a third-party component. If something only appears in one place with no
coordination logic, it belongs inline in its parent.

**Desktop/mobile decision rule:** if the difference is layout (same elements, different sizing), use
responsive prefixes in one component. If the difference is structure (different elements, hierarchy,
interactions), use separate components.

**Stage-2 planning covers:** component hierarchy (which components compose which), desktop/mobile branching
decisions, which DTO fields each component needs (feeds back into DTO shape), `<AuthorizeView>` gate
placement, three-state pattern (loading/empty/populated) per data-driven component, and universal
component identification (EditorView, RichTextView, TagChip, StoryCard, StoryDeck, PaginationControls).

**Stage-5 means:** component composition matches intended hierarchy, data flows correctly through parameter
chains, conditional rendering logic is correct, no WASM-specific NuGet in SharedUI.

**Governed by:** `layer3.5-structure.md`

### Layer 4 — UI Style (Visual + Layout)

Tailwind utility classes, sprite URL resolution, responsive variants, images, conditional class
expressions. **Blocked on design tokens** (`tailwind.config.js`) being locked.

**Component tiers** determine Style weight:
- **Leaf** (TagChip, UserStoryInteractionButton): Full visual weight. All colors, typography, borders, shadows,
  hover/focus/transition states, sprite rendering, active/inactive/disabled conditional styling.
- **Composite** (StoryDesktop, ChapterNavigation): Light — layout Tailwind (`flex`, `grid`, `gap`, column
  spans, breakpoints). Container visual framing if the composite is a vessel (card surface, border).
- **Page/Dispatcher** (StoryPage): Near zero. Possibly a loading skeleton.

**Outer margin rule (non-negotiable):** components own internal padding but never outer margin. Parents
control spacing via `gap`. Forbidden on component root: `mt-`, `mb-`, `mx-`, `my-`, `m-`.

**Parameter-based variants, not class overrides:** Tailwind class conflicts from parent override are
unpredictable. Components expose typed parameters (`Compact`, `Highlighted`) that map to specific utility
classes internally. `AdditionalClass` parameter is an additive-only escape hatch.

**Stage-2 planning covers:** which components need Tailwind design attention (leaf components carry the
most), theme-swappable icon identifiers, sprite service URL construction, responsive breakpoint strategy,
and any Quill.js stylesheet interaction concerns.

**Stage-5 means:** components render with correct visual treatment, layout responds correctly to viewport
changes, sprite resolution works, conditional styling reflects state correctly, outer margin rule honored.

**Governed by:** `layer4-style.md`

───────── **MVP boundary (vertical)** ─────────

### Layer 5 — WASM Enablement

API endpoints in the **server** project and `ClientXService` implementations in the **Client** project
that let WASM-hosted components reach server-side data via HTTP instead of direct DbContext access.

**Stage-2 planning covers:** endpoint shape (derived from the stable `IXService` contract — usually
mechanical; a buffered-signal write's WASM endpoint simply calls the same `IXService` method, whose
server body already does the buffer merge), `ClientXService` responsibilities beyond transport
(session-lifetime memoization of rarely-changing reference data like tag lists, optimistic URL
construction for sprites where `WasmSpriteService` can't do `File.Exists()`), and connection-status
handling.

Most features' Layer 5 is thin — an endpoint whose shape falls out of the interface, and a client impl
that's `HttpClient` wearing the same method signatures. The exceptions are worth tracking because they're
the cells with real Stage-2 content:

- **Buffered-signal features:** lightweight ping endpoints (reading progress, view pings) — fast,
  no DbContext, land in the server's in-process buffer.
- **Resource-gap features:** where WASM can't do what Server does (sprites, file checks) and the
  alternative strategy needs explicit design.
- **Caching features:** where the client impl memoizes data within the WASM session lifetime.

**Stage-5 means:** the feature works identically whether DI resolves the server impl or client impl,
endpoints return 401/403 (not 302 redirects), cookie auth configured correctly.

**Governed by:** `layer5-wasm.md`

### Layer 6 — SQL Indexes

Filtered indexes, composite indexes, golden indexes, covering indexes. DDL added via migration
edits.

**Stage-2 planning covers:** which indexes this feature's query patterns need (user-centric vs
story-centric filtered indexes on `UserStoryInteraction`, golden `(chapter_id, date_posted DESC)` on
`ChapterComment`, reverse `(tag_id, story_id) INCLUDE (priority)` on `StoryTag`, GIN on
`StoryListing.SearchVector`), index naming via `HasDatabaseName("ix_...")`, and `HasFilter()` expressions
using snake_case column names with PostgreSQL `true`/`false`.

Best done in batch once Layer 2 query patterns across multiple features are stable — an index may span
tables belonging to different feature rows (a composite index on a junction table serves both sides).

**Stage-5 means:** indexes exist in the migration, use correct snake_case filter expressions, cover the
query patterns identified in Layer 2.

**Governed by:** `layer6-indexes.md`

### Layer 7 — dissolved (2026-07-06)

The former "Layer 7 — Redis Integration" was a SQL-Server-era + Aspire-template overfit and no longer
exists as a layer or grid column; L8 keeps its historical number. Its three patterns redistributed on
first-principles re-derivation against PostgreSQL (MVCC voided the "batch writes to protect readers
from locks" rationale; what survives is per-pattern):

1. **Write-behind buffer** → split by loss-tolerance. Lossy coalescable signals (reading progress,
   view counts) are **L2 signal buffers** — in-process store + `BackgroundService` flush
   (`layer2-services.md` §"Signal Buffering"), built and live. Durable intent
   (UserStoryInteraction toggles) is **L2 direct EF + L6 partial indexes** — no buffer, ever lossy.
2. **Ephemeral store** (LastReadDate) → not stored at all: "Actively Reading" recency is **derived**
   (`MAX(user_chapter_interactions.last_interaction_date)` per story — the R1 flush already lands
   that column; `DefaultSortOrder.RecentlyRead`).
3. **Read-side cache** (AlsoFavorited top-100) → **the L8 mart is the cache**; services read the
   precomputed table directly. No app-tier read cache.

The only Redis-shaped remnant is a forward constraint: at **N≥2 web nodes** each in-process buffer
body swaps for a shared RESP store (**Valkey** — open-licensed, DO-managed) behind its unchanged
interface. The Aspire-provisioned `cache` container exists for that day; nothing consumes it at N=1.

### Layer 8 — Data Mart Workers

Non-EF-Core background workers producing pre-calculated data for search and discovery. Raw SQL table
creation, zero-downtime swap, recursive CTEs. These tables have NO EF Core model classes, no DbSets,
no migrations.

**Stage-2 planning covers:** table schema (raw SQL DDL), worker algorithm, zero-downtime swap strategy
(`_a`/`_b` tables, atomic `ALTER TABLE ... RENAME`), schedule/trigger, and privacy model (which edges
are public, hidden-favorite consent gating).

**Stage-5 means:** worker runs on schedule, table swap is atomic, data is correct and privacy-compliant.

**Governed by:** `layer8-data-marts.md`

---

## Features (Rows) — Dependency-Ordered

Features are ordered by dependency: a feature appears below its dependencies. Implementation flows
top-to-bottom. Read paths and write paths are listed as separate features even when they touch the same
data structures, because they have qualitatively different Layer 2 content (different service methods,
different DTOs, different query patterns) and different Layer 3/4 content (forms vs display pages).

Not every layer applies to every feature. Most features have no Layer 8 content; simple CRUD
features may have trivial or empty Layer 5; features that are purely background computation may have no
Layer 3/4. An N/A cell is not a gap — it's "this layer doesn't apply here."

Where the spec has open questions (§8), the affected feature notes it. These are likely Stage-1 cells.


### Foundation

**1. Identity & Auth** — `User : IdentityUser<int>`, `ApplicationRole : IdentityRole<int>`, cookie
auth configured for 401/403 (not redirects), `RequireConfirmedAccount = true`, role seeding
(Admin/Moderator/User). Login, Register, and other Identity UI stay in the server project
(`UserManager`/`SignInManager`/`HttpContext`). Includes the User model's hot columns: `ShowMatureContent`,
`PrefersAnimatedSprites`, `ThemeId`, and the three JSON settings columns (`ReaderSettings`,
`PrivacySettings`, `AuthorSettings`). Everything else in the grid depends on this.

**2. Lookup Tables & Seed Data** — All seeded lookup and enum-mirror tables: `StoryStatus`, `TagType`,
`ReportReason`, `ReportStatus`, `NotificationCategory`, `NotificationType` (~35 types with gap-based
numbering), `AcknowledgmentRole`, `RecommendationStatus`, `StoryRelationshipType`, `SearchMode`,
`UserInteractionFilter`, `Theme`, `Badge`, `DefaultSearchSetting` (SearchMode × InteractionFilter
matrix). Seeded via `HasData()` for enum-backed tables, explicit IDs for non-enum tables. Includes
`SiteConstants.cs` string-key constants.

SearchMode entries (revised per three-axis model §5.3): `SearchPage`, `TreeSearch`, `AutoTreeSearch`,
`AlsoFavorited`, `AlsoRecommended`, `ProfilePublishedStories`, `ProfileFavorites`,
`ProfileRecommendations`. These map to discovery surfaces (pages where different filter defaults
make sense), not sources or sorts. "Random Search" is Source=All + Sort=Random on the SearchPage
surface, not a distinct search mode.

**3. Sprite & Theme System** — `ISpriteReadService` with dual implementations: `ServerSpriteReadService` uses
`IWebHostEnvironment` + `File.Exists()` with fallback to `unknown_sprite.png`; `WasmSpriteReadService`
constructs URLs optimistically (no disk/HTTP). `Tag.SpriteIdentifier` stores a key, not a URL — the
client builds the full path at render time from `wwwroot/images/themes/{theme}/static|animated/`.
Animated WebP format, chosen via `User.PrefersAnimatedSprites`. Adding a theme = a new folder, zero
DB changes. Theme selection UI.


### Core Content

**4. Story Creation & Editing** — Create/edit story metadata across the three-table vertical partition:
`Story` (hot: status, counts, dates), `StoryListing` (warm: title, short description, cover art URL),
`StoryDetail` (cold: long description, slug, post-approval status). Story status workflow:
Draft → PendingApproval → moderator approves to author's chosen PostApprovalStatus. In-place editing
model (edit buttons on the normal story page, not a separate dashboard). `<AdminControls>` component
for author-only UI. Cover art upload to R2/MinIO. Slug generation (server-side, never client-editable).
`StoryPropertiesForm` shared form component for create and edit.

**5. Story Browsing & Display** — Story detail page (loads cold partition only when needed), story cards
(warm partition projection), browsing/listing with status and rating filters. Content rating filtering
master rule: if the user has mature content disabled, no trace of mature content anywhere. `StoryListingDto`
anchored to the `StoryListing` vertical partition. Story page layout order: title → cover art → long
description → chapter selection → recommendations (author-spotlighted first). No comments at story level —
comments are chapter-scoped only.

**6. Chapter Writing & Versioning** — Create/edit chapters with `ChapterContent` versioning (live
alternate versions, not revision history — e.g., T-rated and M-rated versions of the same chapter).
WYSIWYG editor (Blazored TextEditor / Quill.js) via the universal `EditorView` component. Server-side
HTML sanitization with `HtmlSanitizer` (allow-list) before saving. Word count calculated on sanitized,
tag-stripped plain text. `PrimaryContentId` FK for the active version.

**7. Chapter Reading** — Read chapter content, next/prev chapter navigation, table of contents with
chapter list. Reader settings applied from `User.ReaderSettings` JSON (font, size, line height, text
width, justify, auto-load next chapter, collapse comment threads, pagination size). Content rating
warning with "Skip to next chapter" button when a chapter's rating exceeds the story's.
`ChapterNavigation` coordination composite appears at top and bottom.

**8. Story Arcs** — `StoryArc` table: title, sort order, start/end chapter numbers per story.
Validation for overlaps/gaps in C# application code. *Spec §8.2 notes the UI for managing arcs was
never designed — likely Stage 1 for Layers 3–4.*

**9. Series & Ordering** — `Series` / `SeriesEntries` for author-defined canonical reading order across
multiple stories. `OrderIndex` on the junction table.

**10. Story Relationships** — One-way directional `StoryRelationships` links (Inspired By, Prequel,
Sequel, Companion Piece). Source story displays the link; absence of a reverse entry means the target
doesn't show it. Status workflow: Pending → Approved/Rejected.


### Tag System

**11. Tag Administration** — Staff-managed, curated tag CRUD. Users cannot create tags. `Tag` table
with `TagName`, `TagTypeId` (Character/Setting/Genre/ContentWarning/CrossoverFandom/Relationship),
`IsFanon` boolean, `ParentTagId` for one-level-deep hierarchy, `SpriteIdentifier` for URL-builder
pattern, `AllowOCDetails` flag, `Description` for tooltips. Tag Directory page (`/tags`) is user-facing
for browsing, moderator-facing for editing (behind `<AuthorizeView>`).

**12. Story Tagging** — Apply tags to stories via `StoryTag` junction with `Priority` (sort/weight).
`StoryCharacter` table for character tagging (unified canon + OC, with `OC_Name`/`OC_Bio` when
`AllowOCDetails` is true, enforced by trigger). `StoryCharacterRelationship` / members for romantic
('/') and platonic ('&') pairings. `SettingDetails` for custom universe/setting overrides.

**13. Tag Display & Sprites** — Render tags with sprites in story cards and detail pages. URL
construction via `ISpriteReadService` using `SpriteIdentifier` + current theme + animated preference.
Tag tooltips from `Description` field. Tag type grouping in display. `TagChip` leaf component.

**14. Tag Filtering & Selection UI** — Tag selection with typeahead/autocomplete (300ms debounce,
Blazored.Typeahead). `TagSelector` coordination composite: selected chips above typeahead input,
dropdown items as lightweight rows (color dot + sprite + name, NOT full chips). Raises
`EventCallback<IReadOnlyList<Tag>> OnSelectionChanged`. Small categories loaded entirely on init;
medium categories client-side.

**15. Saved Tag Selections** — `SavedTagSelection` / `SavedTagSelectionEntry` for reusable named tag
filter presets. Public/private toggle. Sharing is copy-on-write. `DeleteBehavior.Restrict` on TagId.


### User Interactions

**16. Story Interaction State Writes** — Toggle `IsFavorite`, `IsHiddenFavorite`, `IsFollowed`,
`IsIgnored`, `IsReadItLater`, `HasStarted`, `IsCompleted` on the `UserStoryInteraction` hot table
(sparse: no row = all false). Corresponding date writes to `UserStoryInteractionDate` warm partition.
**Durable user intent → direct EF Core write through the service, permanently** (the old "Redis
write-behind" plan was a SQL-Server lock-model artifact — void under Postgres MVCC; settled
2026-07-06, see `audit/UserStoryInteractions.md`). Component-level 2-second debounce timer absorbs
click/unclick churn; churn-driven MVCC bloat is managed by autovacuum tuning (L6).

**Reading status booleans (§4, §5.12):**
- `HasStarted` (`Has-` prefix: permanent past event). Set at 90% scroll of Chapter 1. Only cleared by
  deliberate user action. Records that reading began, not that reading is current.
- `IsCompleted` (`Is-` prefix: current mutable state). Set when last chapter read of a Complete story,
  or explicitly via "mark as read elsewhere."
- `IsIgnored` (`Is-` prefix: current mutable state). Cross-cutting: absorbs discovery rejection
  (`HasStarted=0`) and abandonment (`HasStarted=1`).

**Zero coupling rules:** no bit automatically drives any other bit. Each is set and cleared
independently. The service layer rejects logically impossible write combinations but does not cascade.

**`UserStoryInteractionButton` (leaf):** EventCallback-driven behavior — absence of `OnToggle` means
read-only (rendered only when `IsActive`). When `OnToggle` is provided, button is always rendered and
clickable. Two presentation contexts: listing context (Ignore/ReadItLater clickable, others read-only)
and detail context (all clickable). Debounce managed by `UserStoryInteractionPanel` coordination composite.

**17. Story Interaction Lists & Bookshelves** — **Bookshelves** (`/bookshelves/{Tab}`) are the personal
reading management dashboard. Active user only. Not a discovery surface — no SearchMode entries.
System-defined tabs backed by `UserStoryInteraction` booleans: Favorites, Private Favorites, Read It
Later, Actively Reading (derived: `HasStarted AND NOT IsCompleted AND NOT IsIgnored`), Completed,
Ignored, Abandoned (`IsIgnored AND HasStarted`), Following, My Stories (`Story.AuthorId = currentUserId`),
Custom Lists. Each tab composes `StoryDeck` for display. Tags and interaction filters available for
narrowing within a bookshelf (management, not discovery).

**18. User Following** — `FollowedUser` table: follow/unfollow users, `ReceiveAlerts` toggle (bell
icon), `DateFollowed`. Not author-specific — "Followed Users" reflects that not everyone is an author.

**19. Vouches** — dedicated `Vouch` table (promoted off `FollowedUser`, resolved Phase B — see
`audit/Following.md`), with optional `VouchText` (`MaxLength(1000)`). Scarce personal endorsement:
5-per-user limit enforced in C# service layer. Indexes: composite PK covers outgoing lookups,
`ix_vouches_vouched_user_id` covers incoming. Display asymmetry: outgoing vouches public, incoming
vouches private to owner.


### User Profile

**20. User Profile Editing** — Edit `UserProfile.ProfileText` (cold partition), privacy/author/reader
settings (JSON columns on `User`), profile picture upload (R2/MinIO), tagline. Settings grouped by
concern: `ReaderSettings`, `PrivacySettings`, `AuthorSettings`. `IUserSettingsService` self-referential
editing exception.

**21. User Profile Display** — Public profile page (`/user/{UserId:int}/{*Tab}`). Two-half structure:
top half is identity (bio, tagline, stats, badges, outgoing vouches); bottom half is tabbed story lists
at degree-1 from the profile user (Favorites, Recommendations, Authored Stories). Each tab composes
`ResultsFilterPanel` and `StoryDeck` — same components the search page uses. Live tables, not data mart
(profile data should be immediately fresh). Own-profile vs other-profile is a privacy filter, not a
source switch.

**22. User Stats** — `UserStats` table: 22+ denormalized counters. Updated in real-time by application
logic. Background workers read these for badge checks.


### Comments

**23. Comment Posting** — Write comments across all four TPT contexts: `ChapterComment`,
`UserProfileComment`, `GroupComment`, `BlogPostComment`. All inherit from `BaseComment`. `DatePosted`
denormalized from base into each child. Server-side sanitization.

**24. Comment Display & Pagination** — Threaded comment view with parent/child rendering. Pagination
using the golden index `(chapter_id, date_posted DESC)`. Orphaned replies displayed as children of
"[Deleted Comment]."

**25. Comment Likes** — Toggle like via `CommentLike` junction table (no `DateLiked` — anti-addictive).
No notifications. Denormalized `LikeCount` on `BaseComment`.

**26. Spoiler Comments** — `IsSpoiler` boolean on `ChapterComment` (not `BaseComment` — chapter-scoped
concept). Label: "Contains spoilers for future chapters." Checkbox next to post button. Completion-gated
reveal: `IsCompleted = true` → single click reveals. `IsCompleted = false` → `ConfirmDialog` ("You
haven't finished the story. Are you sure?"). Data flow: `ChapterPage` dispatcher passes
`UserHasCompletedStory: bool` to `CommentSection`. `IsRevealed` is ephemeral component state.


### Recommendations

**27. Recommendation Submission** — Write a recommendation (high-effort, minimum character count).
One per user per story (unique constraint). Status workflow: PendingApproval → Approved/Rejected.
`Recommendation` (hot) + `RecommendationDetail` (cold) vertical partition. Recommendations cannot
have spoilers — no `IsSpoiler` field (deliberate absence per §5.6).

**28. Recommendation Display** — View recommendations on story page. Author Spotlight: authors
highlight up to 5 per story. `RecommendationLike` junction for reader likes.

**29. Hidden Gem Management** — Mark/unmark recommendations as Hidden Gems (`IsHiddenGem`).
5-per-user limit enforced in C# service. *Spec §8.4: edge case at limit needs resolution.*

**30. Recommendation Attribution** — `UserStoryRecommendationSource` (sparse partition): records which
recommendation led user to story. After reading Chapter 1, popup asks "Was this recommendation useful?"
→ `RecommendationSuccess` record created.


### Discovery

**31. Search Page** — Route: `/discover`. Source=All. Random sort preloaded on entry (never blank).
User can switch to Date Published. FTS text input available — when active, Relevance sort appears.
`ResultsFilterPanel` with all filter types. `StoryDeck` for results. "Give me more" button in random
mode replaces pagination (interaction buttons ARE the pagination mechanism — Ignore/ReadItLater modify
DB state, next batch excludes dismissed stories). Standard offset pagination in other modes.

**32. Full-Text Search** — PostgreSQL `tsvector` + GIN index on `StoryListing.SearchVector`.
FTS is a filter axis (WHERE clause), not a source (§5.3.2). `Rank()` produces relevance score for
sorting. Searches title and short description only.

**33. Manual Tree Search** — User picks a starting node (story or user) and a criterion. UI shows
connected nodes as expandable graph. Each pivot is a fresh, stateless query. Unified Tree Search page
(`/discover/me`, `/discover/user/{userId}`, `/discover/story/{storyId}`) — manual and automatic tabs
on the same page.

**34. Tag Directory** — Route: `/tags`. User-facing reference page for browsing tags organized by type
with descriptions and sprites. Moderators see CRUD controls behind `<AuthorizeView>`. One page, two
experiences. Mobile support for browse mode; edit controls desktop-only.


### Community Content

**35. Blog Post Writing** — Create/edit blog posts. TPT: `BaseBlogPost` → `ProfileBlogPost` (optional
`StoryId` link), `GroupBlogPost` (`GroupId` link). Universal `EditorView` for composition.

**36. Blog Post Display** — View blog posts in context (profile, story, group). All blog posts
support comments.

**37. Polls** — `BasePoll` → `SitePoll` / `BlogPostPoll` (TPT). *Spec §8.6: detailed UI not
specified — likely Stage 1 for most cells.*

**38. Group Management** — Create/edit groups with three audience types. `GroupMember` junction with
Role enum.

**39. Group Content & Folders** — `GroupStory` first-class entity. `GroupFolder` nesting via
`ParentFolderId`. Rating enforcement at write time.

**40. Group Display** — Group page (`/group/{GroupId:int}/{*GroupSlug}`) with member list, story listing,
folder browsing.


### Notifications

**41. Notification Generation** — Create `Notification` rows on high-effort events (NOT likes).
Polymorphic `RelatedEntityId`. ~35 notification types across 9 categories.

**42. Notification Display** — Route: `/notifications`. Grouped by category, sorted by date, mark as
read. Panel in layout with flyout preview via notification bell.

**43. Notification Settings** — Sparse override model. Settings page driven by database data.


### Reading Experience

**44. Reading Progress Tracking** — Client-side JavaScript tracks scroll percentage (300 ms
throttle). L2 body = the **reading-progress signal buffer** (in-process coalescing store keeping
max progress + latest timestamp per (user, chapter); 5 s `BackgroundService` flush via
`unnest … ON CONFLICT`; loss-tolerant contract). `UserChapterInteraction.ReadProgress` (0.0–1.0),
high-water. `IsRead = true` at ≥90%, sticky. `HasStarted` on `UserStoryInteraction` at 90% of
Chapter 1 takes the **durable** direct path (never the buffer). Bookshelves "Actively Reading"
sorts by derived `MAX(uci.last_interaction_date)` (`DefaultSortOrder.RecentlyRead`) — no stored
LastReadDate anywhere.

**45. View Count Tracking** — Trigger: first client-side ping (5-second timer or first scroll),
NOT page load; anonymous views count. L2 body = the **view-count signal buffer** (per-story sum,
5 s flush) accumulating into **`daily_story_stats`** (per-story/day; migration-managed raw DDL,
no EF model — ground truth, not a mart; partition-ready by stat_date). Lifetime total =
SUM, read on demand only (`GetStoryTotalViewsAsync` → StoryCard dropdown "View stats" reveal).
**Never a sort key, never a permanent badge** — non-sortable informational metric
(anti-popularity-snowball; `DefaultSortOrder`'s exclusion note). No `ViewCount` column exists on
`Story`/`ChapterContent`/`BaseBlogPost` (dropped in `R2_ViewCountToDailyStoryStats`).


### Moderation

**46. Content Reporting** — Submit `Report` with `ReportReasonId`. Polymorphic entity reference.

**47. Moderation Queue & Actions** — Route: `/mod/reports`. Desktop-only, no dispatcher pattern.
Moderator reviews, actions, auto-flagging via `ActiveReportCount`.

**48. Story Approval Workflow** — Route: `/mod/submissions`. PendingApproval → approve/reject.
Import verification.


### Private Communication

**49. Private Messaging** — Route: `/messages/{ConversationId:int?}`. Three-table model. SignalR
real-time. `LastReadTimestamp` for unread tracking. Uses full `EditorView` for rich-text composition.
Respects `AllowPrivateMessages`.


### Auxiliary Features

**50. Badge System** — `Badge` table with string PK. `UserBadge` junction with `DisplayOrder`.
MVP: synchronous inline checks.

**51. Custom Lists** — User-created collections beyond system lists. Public/private. *Spec §8.7:
detailed design mostly TBD — likely Stage 1.*

**52. User Account Deletion** — `DeleteUserService` handling RESTRICT FK conflicts before deletion.
Content anonymization vs interaction CASCADE.

**53. Story Import & Verification** — `StoryImport` table. Two-way link verification. MVP: manual
mod verification.

**54. Content Download/Export** — Download .epub/.pdf. Pure application-layer, no schema impact.

**55. Community Spotlight** — Donation infrastructure. Schema TBD.

**56. Feature Contributions** — Admin attribution of accepted suggestions. Tied to "Site Development"
Group.

**57. Notification Cleanup Worker** — `IHostedService` deleting read notifications older than 60 days.

**58. UserStat Recalculation Worker** — Periodic background recalculation of `UserStats` counters.


───────── **Horizontal boundary (requires real user data)** ─────────


**59. Automatic Tree Search** — Recursive CTE against `UserStoryTreeSearchEntries` data mart.
Degree controls and edge type selector. Unified with Manual Tree Search on the same page.

**60. Tree Search Data Mart Worker** — Daily rebuild of `UserStoryTreeSearchEntries`. Zero-downtime
table swap. Privacy model: only public edges, hidden favorites with consent.

**61. Also Favorited / Also Recommended** — Co-occurrence scoring. Embedded sections on story detail
page (not separate pages). `AlsoFavoritedScore` / `AlsoRecommendedScore` cache tables.

**62. SiteDailyStat Worker** — Daily aggregation into `SiteDailyStat` table. No user-facing UI in MVP.
