# Audit Summary

Human-facing overview of the Step 3 classification pass. Five sections per `step3_classify.md`.

The headline: **Layer 1 is broad and largely finished; Layers 2–4 are a thin vertical slice through
Stories + Tags only; Layers 5–8 are stubs or pre-staged fragments.** The developer front-loaded the
data model — ~80 entities with full Fluent API configuration in one `OnModelCreating` — then built one
end-to-end column (Stories create/edit/display) as a proof of the architecture before pausing. Most of
the grid is therefore "Stage 5 at L1, Stage 2 everywhere else."

---

## 0. Adjudication Principle (read first)

**The spec supersedes the code, unless the code is demonstrably working.** The spec is the *recent*
consolidation and refinement; the codebase is ~7 months old, was paused, and — as this audit shows — is
mostly non-working below Layer 1. So where spec and code disagree, the spec is authoritative *unless* the
relevant code actually functions and matches intent (e.g. Stories L1/L2).

This corrects a default the classification framework is internally inconsistent about: `step3_classify.md`
calls the spec "the end of the design-conversation arc" (recent) in one place and calls the code "the later
artifact" (authoritative) in another. For *this* project the first reading is correct.

**Consequence for how to read the grid:** most cells marked **Stage 4** are not genuine two-way
adjudications — they are **stale-code traps**. The resolution direction is already known (build to spec;
the existing code is salvage at best). The Stage-4 flag is retained on those cells as a *warning*: there is
plausible-looking code there that a building session will copy if not told to discard it. Only a small
residue of Stage-4 cells are *genuine* reconciliations, and those are **mechanical** (an unfinished
refactor), not contests of intent — see §3.

---

## 1. Stage Distribution (sense of scale)

Across 62 features × 9 layers (≈ 380 applicable cells after N/A):

| Stage | Rough share | Where it lives |
|-------|-------------|----------------|
| **Stage 1** | ~5% | A few genuinely-open features (Vouches L1, Spotlight, §8 open questions: Story Arcs UI, Polls UI, Hidden Gem limit, Custom Lists). |
| **Stage 2** | ~56% | The bulk. Intent is settled (spec §4/§5/§5.30 covers it) but no code — every unbuilt L2/L3/L3.5/L5/L6/L7/L8 cell with a clear spec. |
| **Stage 4** | ~11% | See §3. **But split it in two:** ≈2% are *genuine mechanical* reconciliations (Identity post-move references; Story L5 endpoint wiring). The other ≈9% are **stale-code traps** — divergent code whose resolution direction is already known (build to spec). Under the strict CLAUDE.md reading (Stage 4 = "needs diagnosis"), those collapse to Stage 2, since the diagnosis is trivial; the flag is kept only as a copy-this-and-regret-it warning. |
| **Stage 5** | ~27% | Almost entirely **L1** (the data model) plus Stories L2 and FTS L6. "Sound, awaiting verification." |
| **Stage 6** | 0% | Out of range for this step. |

**L1 is ~85% Stage 5.** **L4-Style is 100% Stage 1** (expected — Tailwind not installed). **L7/L8 are
entirely Stage 2** where they apply. The Stage-4 share dropped from an earlier ~12% after recategorizing
**Story Browsing L2** (5) as Stage 2 — that read service is correct as far as it goes and merely lacks the
listing projection (unbuilt extension, not a divergence).

---

## 2. Genuinely Surprising Findings

1. **No migrations exist at all.** `Microsoft.EntityFrameworkCore.Design` is referenced and a
   `DataSeeder` runs at startup, but the `Migrations/` folder is absent. The entire Layer-1 corpus has
   never been materialized against PostgreSQL. Every L1 "Stage 5" is contingent on the first
   `dotnet ef migrations add` succeeding. This is the single biggest verification gap.

2. **The reading-status model is the *pre-revision* design.** `UserStoryInteraction` has stored
   `IsInProgress`, `IsActivelyReading`, and `IsCompleted` — but **no `HasStarted`**. The spec's revised
   model (§4, §5.12) makes "actively reading" a *derived* state (`HasStarted AND NOT IsCompleted AND NOT
   IsIgnored`) and mandates the `Has-`/`Is-` prefix convention. Vestigial `ReadStatus` and
   `FavoriteStatus` enums (in `ModelEnums.cs`) survive from before the boolean-column axiom. The filtered
   indexes target `is_in_progress`, not `has_started`. This is a coherent, finished implementation of a
   *superseded* design — and per the §0 principle it is not an "intent divergence" (there is only one
   intent, the spec's); it is the most consequential instance of **code staleness** in the codebase. The
   spec wins; these columns are discarded and re-modeled, not adjudicated.

3. **Data marts are modeled as first-class EF entities.** `AlsoFavoritedScore`, `AlsoRecommendedScore`,
   and `UserStoryTreeSearchEntry` have POCOs, `DbSet`s, `HasKey`, and elaborate filtered indexes in
   `OnModelCreating`. The Layer-8 convention is explicit: data-mart tables have **no EF Core model
   classes, no DbSets, no migrations**. They're built the opposite way from how they should be.

4. **The search/sort vocabulary predates the three-axis model.** Seeded `SearchMode` keys are
   `DefaultSearch / TreeSearch / RandomSearch / AlsoFavorited`; the revised model (§5.3) wants
   `SearchPage / TreeSearch / AutoTreeSearch / AlsoFavorited / AlsoRecommended / Profile*`. "RandomSearch"
   as a distinct mode directly contradicts "Random is Source=All + Sort=Random, not a mode." The
   `DefaultSortOrder` enum offers `Favorites`, `LastUpdated`, `ViewCount` — all three explicitly excluded
   by §5.3.3.

5. **All existing UI is Bootstrap, not Tailwind.** Every built component (`StoryPropertiesForm`,
   `TagSelector`, `StoryDesktop`) uses `form-control`, `badge bg-primary`, `alert alert-danger`,
   `mb-3`/`mb-4`. Tailwind is a settled axiom but isn't installed. This means L4 is not merely "blocked on
   tokens" — the components that exist will need restyling, not just styling.

6. **Identity was physically moved but not fully re-wired.** Classes now under `Identity/` still declare
   `namespace TheCanalaveLibrary.Server.Components.Account`; `App.razor` loads
   `Components/Account/Shared/PasskeySubmit.razor.js` (a path that no longer exists on disk); the endpoint
   extension `using`s `...Components.Account.Pages`. Namespaces needn't match folders, so this may
   *compile*, but the asset path is a runtime 404 and the whole area is mid-refactor. Recent git history
   ("Fix all folder structure…", "Move folder to Identity outside Components") confirms this is live.

7. **A leaf-shaped concern injects a service, and a service-shaped concern doesn't exist.**
   `TagSelector` and `StoryPropertiesForm` both `@inject ITagRetrievalService`, but no implementation of
   that interface exists and it is **not registered in DI** — these components would throw at runtime.
   Meanwhile the interface is named `ITagRetrievalService` (convention: `ITagReadService`).

8. **`RandomNumberGenerator` is load-bearing in the story page.** `StoryDesktop` renders
   `<RandomNumberGenerator />` where the story body should be — a scaffolding probe left in place. Story
   "display" is a title + author + short description and nothing else.

---

## 3. Reconciliation Index (Stage-4 cells)

Per §0, the Stage-4 cells fall into two genuinely different kinds. Read **3a** as real work-to-decide and
**3b** as "code is stale, the spec already won, don't copy the code."

### 3a — Genuine reconciliations (mechanical; no intent contest)

These are the only Stage-4 cells that need an actual diagnosis pass — and even they are about finishing an
unfinished refactor, not choosing between spec and code.

| Cell(s) | What to reconcile |
|---------|-------------------|
| **Identity L2/L3/L3.5** (1, 52) | Unfinished folder move: classes under `Identity/` still declare `namespace ...Components.Account`; `App.razor` loads a `Components/Account/...PasskeySubmit.razor.js` path that no longer exists; endpoint extension `using`s `...Components.Account.Pages`. Normalize namespaces + asset path, then verify build. Also wire login/logout as layout triggers (§3.19). |
| **Story L5 endpoint wiring** (4, 5) | `HttpStory{Read,Write}Service` call `/{id}/edit` and write routes that `StoryEndpoints` never maps. Add the missing endpoints from the (stable, working) `IStory*Service` contracts. Pure plumbing. |

### 3b — Stale-code traps (spec wins; existing code is salvage, not authority)

Resolution direction is **known** for every row: build to spec, discard/replace the divergent code. The
Stage-4 flag stays only so a building session doesn't mistake the stale code for intent. (Strictly these
are Stage 2 with a warning.)

| Cell(s) | Stale code → spec target |
|---------|--------------------------|
| **UserStoryInteractions L1** (16, 17) | `IsInProgress`/`IsActivelyReading`, no `HasStarted` → re-model to the spec's `HasStarted` + derived "actively reading" (§4/§5.12). Discard the vestigial columns. |
| **UserStoryInteractions L6** (16, 17) | Filtered indexes on `is_in_progress`/`is_completed` → regenerate against the re-modeled columns. Follows L1. |
| **Lookups L1** (2) | `SearchMode` seed keys + `DefaultSortOrder` enum + vestigial `ReadStatus`/`FavoriteStatus` enums → conform to the three-axis vocabulary (§5.3); retire the vestigial enums; complete the seed matrix. |
| **Tags L2/L3/L3.5** (12, 13, 14) | `TagSelector` (native `<datalist>`, list-mutation, inline Bootstrap badges, outer-margin violation, no impl for `ITagRetrievalService`) → rebuild to §5.30.4 (`TagChip` leaf + Blazored.Typeahead + `OnSelectionChanged` DTO callback); implement + rename + register `ITagReadService`. The existing component is discardable scaffolding. |
| **Comment Likes L1** (25) | Implicit EF many-to-many (`LikedComments`/`LikedByUsers`) → spec's explicit `CommentLike` junction. |
| **Data marts L1/L6/L8** (59, 60, 61, 62) | `AlsoFavoritedScore`, `AlsoRecommendedScore`, `UserStoryTreeSearchEntry`, `SiteDailyStat` modeled as EF entities with DbSets + indexes → spec/Layer-8 say raw-SQL marts with **no EF model**. (Skill is mildly self-inconsistent on `SiteDailyStat` — flag for user.) |
| **Sprites L2/L5** (3) | `ISpriteService.GetSpriteUrl(...)` → convention `ISpriteReadService` + add `GetInteractionIcon()`. *Edge case:* the impls actually work, so this is the most "reconcile" of 3b — but naming/contract still defer to spec. |
| **Story Creation L3/L3.5** (4) | `StoryPropertiesForm` EditForm+ViewModel skeleton is sound but Bootstrap + incomplete (tags/cover-art TODO) → finish to spec; restyle in L4. |
| **Story Browsing L3/L3.5** (5) | `StoryPage` omits `[PersistentState]` (flicker) + uses `{Slug?}` not catch-all; `StoryDesktop`/`Mobile` are `RandomNumberGenerator` stubs → build the spec layout (§5.28) with `StoryCard`/`StoryDeck`. |

*Not in this index:* **Vouches L1** (19) is **Stage 1**, not 4 — §8.13 is a genuinely open design decision
(bool vs. dedicated table), not stale code. **Story Browsing L2** (5) was reclassified **Stage 2** — the
read service is correct and merely lacks the listing projection (unbuilt extension, no divergence).

---

## 4. Stage-1 Landscape (grouped by layer and pattern)

Stage 1 is rare and almost entirely **conceptual** (spec never resolved it), not code-relationship or
blocked.

**Conceptual gaps — open §8 questions (resolve in chat with skill files):**
- **Story Arcs UI** (8, L3/L3.5) — §8.2: arc-management UI was never designed.
- **Polls UI** (37, L3/L3.5) — §8.6: detailed poll UI unspecified.
- **Hidden Gem limit behavior** (29, L3) — §8.4: what happens at the 5-item limit.
- **Custom Lists** (51, L3/L3.5) — §8.7: creation flow + filter composition mostly TBD.
- **Vouches data shape** (19, L1) — §8.13: bool vs. dedicated table. A Layer-1 decision that blocks
  its own downstream layers.

**Whole-feature TBD:**
- **Community Spotlight** (55, all layers) — §5.26 donation infrastructure is TBD; `CommunitySpotlight`
  entity is a placeholder.

**Blocked (note the blocker, no action):**
- **All L4-Style cells** — blocked on `tailwind.config.js` design tokens *and* on Tailwind being
  installed at all. Counted as Stage 1 per the layer model. This is expected and not alarming.

There are **no code-relationship Stage-1 cells** — where code and spec diverge, there's enough code to
make it a Stage-4 reconciliation rather than a Stage-1 gap.

---

## 5. UI Component Inventory

Components are classified by tier (leaf / composite / page-dispatcher). "Status" is the component's own
build state. Universal = consumed by multiple feature folders.

### Components that exist in code

| Component | Tier | Owner folder | Consumed by | Status |
|-----------|------|--------------|-------------|--------|
| `StoryPage` | Page/Dispatcher | Stories | route `/story/{id}/{slug?}` | Built; missing `[PersistentState]`, route not catch-all (Stage 4) |
| `StoryDesktop` | Composite (pass-through) | Stories | StoryPage | Stub — renders `RandomNumberGenerator` placeholder (Stage 4) |
| `StoryMobile` | Composite (pass-through) | Stories | StoryPage | Stub (Stage 4) |
| `StoryPropertiesForm` | Composite (coordination) | Stories | create/edit routes | Partial EditForm+ViewModel; Bootstrap; tag/cover TODO (Stage 4) |
| `TagSelector` | Composite (coordination) | Tags | StoryPropertiesForm | Divergent (datalist, no TagChip, list mutation, outer margin) (Stage 4) |
| `DeviceLayout` / `DesktopLayout` / `MobileLayout` | Layout | (SharedUI/Layout) | Routes default layout | Built scaffold |
| `HomePage` / `HomeDesktop` / `HomeMobile` | Page/Dispatcher | (SharedUI/Pages/Home) | route `/` | Scaffold |
| `NotFound` | Page | (SharedUI/Pages) | router NotFoundPage | Built |
| `RandomNumberGenerator` | Leaf | (SharedUI/Shared) | StoryDesktop | Scaffolding probe — should be deleted |
| `Component1` | Leaf | (SharedUI) | — | Template leftover — should be deleted |
| Identity pages (`Login`, `Register`, `Manage/*`, …) | Page | Identity | route `/Account/*` | Default scaffold; post-move references unreconciled (Stage 4) |

### Universal components specified but NOT yet built (Stage 2 unless noted)

From spec §5.30. These are the integration points the workplan should prioritize (Phase-1 atoms):

| Component | Tier | Spec owner | Intended consumers |
|-----------|------|-----------|--------------------|
| `StoryCard` | Leaf | Stories | StoryDeck → Stories, Discovery, Bookshelves, Profiles |
| `StoryDeck` | Composite (pass-through) | Stories | Search page, Bookshelves, Profile tabs, Also-Favorited, Group listing |
| `TagChip` | Leaf | Tags | StoryCard, story detail, TagSelector dropdown, Tag Directory |
| `UserStoryInteractionButton` | Leaf | UserStoryInteractions | StoryInteractionPanel, StoryCard caret menu |
| `StoryInteractionPanel` | Composite (coordination) | UserStoryInteractions | Story detail, listings |
| `ResultsFilterPanel` | Composite (coordination) | Discovery | Search page, Profile tabs, Bookshelves |
| `EditorView` | Composite (third-party wrapper, Quill) | Chapters | Chapters, BlogPosts, Messaging, Recommendations |
| `RichTextView` | Leaf | Chapters | Chapter reading, previews |
| `ChapterNavigation` | Composite (coordination) | Chapters | Chapter page (top + bottom) |
| `CommentItem` / `CommentSection` | Leaf / Composite (coordination) | Comments | Chapter/profile/group/blog comment contexts |
| `ConfirmDialog` | Composite (container) — universal | Comments (§5.30.9) | Spoiler reveal, destructive actions site-wide |
| `UserCard` | Leaf | Following | Vouch display, member lists |
| `AdminControls` | Composite (wraps `AuthorizeView`) | Stories | Author-only story UI |
| `PaginationControls` | Composite | Discovery | Any paged listing |
| `TagDirectoryPage` | Page | Tags/Discovery | route `/tags` |
| `BookshelvesPage` | Page/Dispatcher | UserStoryInteractions | route `/bookshelves/{Tab}` |

**Axis-integrity note:** No features or layers were found in the codebase that aren't captured in the
Step-2 axes. The grid was not silently expanded. The only structural observations are the *vestigial*
artifacts (template stubs `Class1`, `Component1`, `RandomNumberGenerator`, `ExampleJsInterop`) — these
are noise to delete, not new axes.
