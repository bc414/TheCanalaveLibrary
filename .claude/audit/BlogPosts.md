# Audit — BlogPosts/

**Features:** 35 (writing), 36 (display), 37 (polls), 56 (feature contributions). Universal `EditorView`
for composition (§5.30.2; stale §5.19 reference corrected 2026-06-24).

## Shared Context
**Entities (moving Core/Models/ → Core/BlogPosts/ as part of WU31):** `BaseBlogPost` (TPT root,
`ToTable("base_blog_posts")`, `Rating`→short, M:N `LikedByUsers`) → `ProfileBlogPost` (optional
`StoryId`, `SetNull`), `GroupBlogPost` (`GroupId`). `BasePoll` (TPT) → `SitePoll` / `BlogPostPoll`;
`PollOption` (unique `(PollId,Text)` and `(PollId,SortOrder)`). `FeatureContribution` (`SetNull`
diamond-breaking to blog/comment). All blog posts support comments (see Comments cluster).
WU31 delivers L2/L3/L3.5/L4 for features 35/36 (profile blog posts only); Feature 56 deferred.

## Feature 35 — Blog Post Writing
- **L1 — Stage 5.** TPT split sound. **L2 — Stage 5.** **L3/L3.5 — Stage 5.** **L4 — Stage 1** (visual sign-off pending; same pattern as WU13/WU24). **L5 — Stage 2. L6 — Stage 2.**
- **WU-ErrorHandling note (2026-07-06).** Both editors (`BlogPostEditorPage`,
  `GroupBlogPostEditorPage`) embed `DraftAutosave` (`draft:blogpost:{id|new}`,
  `draft:groupblogpost:new:{groupId}`; Title + Content), cleared on successful submit;
  `BlogPostPropertiesForm` renders errors via `InlineAlert` and gained `SetContentAsync` (Quill
  push for restore); generic catches route through `ExceptionPresenter` + `LogError`. Strategy:
  `cross-cutting.md` §"Error Handling Strategy".
- **Settled constraints (2026-06-24, WU31):**
  - Content-editing Pattern 1: `/blog/new` + `/blog/{id}/edit` (write/auth), `/blog/{id}/{*slug}` (read-only).
    Spec §5 line ~1585 said "in-place editing" — overridden (blog is a multi-field form like Story).
  - No `Slug` column on `BaseBlogPost` (confirmed `InitialSchema`); `{*slug}` is cosmetic; `BlogPostId` (int) is the sole key.
  - Profile blog posts only for WU31; `GroupBlogPost` UI → WU32.
  - ~~Content-rating named query filter extends to `BaseBlogPost`~~ **SUPERSEDED by WU31.5
    (2026-06-24):** named filter removed from `BaseBlogPost`; blog-post content rating enforced via
    explicit `.Where(p => p.Rating <= max)` projection checks (see `cross-cutting.md` "Content Rating
    Filtering"). The EF Core 10 TPT + named-filter combination generates broken entity-materialization
    SQL and blocks `ExecuteDeleteAsync`; projection checks + change-tracker stub deletes replace it.
  - Optional story-link picker via `IStoryReadService.GetStoryIdsByAuthorAsync` (bypasses content-rating filter;
    author always sees own mature stories). Method confirmed present (parallel session, IStoryReadService.cs:55).
  - `AuthorId` server-stamped in write service; absent from create DTO (mirrors `CreateStoryDTO`).
  - `UserStats.BlogPostsWritten` incremented via `ExecuteUpdateAsync` on create (same UserStats pattern).
- **Stage-5 note (2026-06-24, WU31):** `dotnet build` green; `dotnet test` 691 tests all green.
  - **L2** (`ServerBlogPostReadService` / `ServerBlogPostWriteService`): Integration tier —
    `BlogPostWriteServiceTests` (20 tests; covers create/update/delete auth gates, like toggle, content-rating
    filter both directions, draft visibility, UserStats increment). **WU31 L2 note (superseded by WU31.5):**
    used two-query scalar split + raw-SQL delete as workarounds for EF Core 10 TPT + named-filter bugs;
    WU31.5 removes these workarounds.
  - **L3/L3.5** (`BlogPostPropertiesForm`, `BlogPostEditorPage`, `BlogPostPropertiesViewModel`):
    RazorComponents tier — `BlogPostPropertiesFormTests` (10 tests; covers title/rating/spoilers/publish
    inputs, story-picker show/hide, submit callback, server-validation errors, IsLoading disables button).
    `BlogPostEditorPage` has `@inject` services — page-level logic and navigation tested end-to-end via
    Integration + manual server smoke.
  - **WU31.5 Stage-5 note (2026-06-24):** `DateCreated`, `LastUpdatedDate`, `Rating`, `IsPublished`
    moved from `BaseBlogPost` → `ProfileBlogPost`/`GroupBlogPost` (declared on derived classes so EF
    Core 10 maps them to child tables). Named query filter removed from `BaseBlogPost`. Services
    refactored: single-projection query on `ProfileBlogPosts` in `GetByIdAsync`/`GetForEditAsync`
    (one TPT join instead of two separate queries); explicit `.Where(p => p.Rating <= max)` rating
    check replaces named filter in `GetByAuthorAsync`; change-tracker stub delete replaces raw SQL.
    Migration `WU31_5_DenormalizeTptDiscoveryColumns` adds columns to child tables, copies data,
    then drops from base tables (correct add→copy→drop order). `dotnet test` 691/691 green.
    Covering tier: **Integration** — existing `GetById_MaturePost_HiddenFromNonMatureViewer`,
    `GetById_MaturePost_VisibleToMatureViewer`, `GetById_MaturePost_VisibleToAuthorRegardlessOfMatureSetting`
    confirm content-rating projection path. L1/L2 return to Stage 5.

## Feature 36 — Blog Post Display
- **L1 — Stage 5.** **L2 — Stage 5** (profile context for WU31; story/group contexts → WU30/WU32).
  **L3/L3.5 — Stage 5. L4 — Stage 1** (visual sign-off pending). **L5 — Stage 2.**
- **Settled constraints (2026-06-24, WU31):** `BlogPostCard` (leaf, profile feed), `BlogPostPage`
  (read-only view dispatcher), `BlogPostListingDto` for the feed. Author byline: plain hyperlink,
  not `UserCard` (mirrors StoryCard — too compact). `CommentSection` generalized for blog-post context
  (chapter XOR blog post; see `layer3.5-structure.md` "CommentSection — Multi-Context Dispatch").
  Draft posts (`IsPublished = false`) return null/NotFound to non-authors; author reads own
  mature/unpublished content via `IgnoreQueryFilters()` on derived scalar projections.
- **Stage-5 note (2026-06-24, WU31):** `dotnet build` green; `dotnet test` 691 tests all green.
  - **L2** (`ServerBlogPostReadService.GetByIdAsync` / `GetByAuthorAsync` / `GetForEditAsync`): Integration
    tier — `BlogPostWriteServiceTests` (covers per-viewer `IsLikedByCurrentUser`, content-rating filter,
    draft visibility). Two-query scalar split (Query 1 on `BlogPosts.IgnoreQueryFilters()` for base columns +
    visibility check; Query 2 on `ProfileBlogPosts.IgnoreQueryFilters()` scalar for derived columns) avoids
    the EF Core 10 TPT + named filter entity-materialization bug without losing author bypass.
  - **L3/L3.5** (`BlogPostPage`, `BlogPostCard`): No `@inject` on leaf components (bUnit-compatible);
    `BlogPostPage` is a page-level dispatcher — L4 visual sign-off required to leave Stage 1.
    `CommentSection` blog-post context additions covered by `CommentSection` tests.

### WU-ComponentSoundness Stage note (2026-06-27)

**Cell affected:** F36 L3-Logic (BlogPostPage) — correctness polish inside an already-aligned Stage-5
cell; no stage transition.

**F1 — BlogPostPage lifecycle reload (in-place BlogPostId change stale content, now closed):**

`BlogPostPage.razor` now implements the MessagesPage route-dispatcher pattern with key `BlogPostId`:
- `private bool _initialized;` + `private int _loadedBlogPostId = int.MinValue;` sentinel.
- `OnInitializedAsync`: auth-resolution (one-time); calls `LoadPostAsync()` then sets `_initialized = true`.
- `OnParametersSetAsync`: guards `BlogPostId == _loadedBlogPostId`; resets `_notFound = false` and
  `_likeError = null`, then calls `LoadPostAsync()`.
- `LoadPostAsync()`: sets `_loadedBlogPostId = BlogPostId` first, then loads the post and seeds like state.

Root cause: same-template navigation (e.g., clicking a `BlogPostCard` link from a profile page) reuses
the component instance; `OnInitializedAsync` does not re-fire.

Covering tier: **manual boot gate** (no bUnit test — `BlogPostPage` injects `IBlogPostReadService` and
auth services; listed in E2E checklist). Convention in
`layer3-logic.md` §"Route-parameter dispatchers reload in `OnParametersSetAsync`".

---

## Feature 37 — Polls
- **L1 — Stage 5** (`BasePoll`/`SitePoll`/`BlogPostPoll`, `PollOption` with `Voters` M:N + unique
  constraints). **L2 — Stage 2.** **L3 / L3.5 — Stage 1 (conceptual, §8.6):** detailed poll UI was never
  specified — resolve in chat. **L4 — Stage 1. L5 — Stage 2.**

## Feature 56 — Feature Contributions
- **L1 — Stage 5** (`FeatureContribution`; SetNull diamond-breaking FKs to `BaseBlogPost`/`BaseComment`).
  **L2 — Stage 2** (admin attribution of accepted suggestions; tied to "Site Development" group).
  **L3/L3.5 — Stage 2. L4 — Stage 1. L5 — N/A** (admin-only server surface).
- **Settled (2026-06-24, WU31):** Deferred post-MVP. Not part of WU31 scope. Will ship in a dedicated
  post-MVP work-unit (no sequence number yet). `FeatureContribution` entity, SetNull FKs, `DbSet`,
  and `BaseBlogPost.FeatureContributions` navigation property remain as-is (L1 Stage 5, unchanged).
  Only L2+ implementation is deferred. See `forward_plan.md` "WU31 Blog Post settled decisions."

## L4.5-Browser verification (2026-07-02) — F35 + F36 → Stage 5

F35: created a post via `/blog/new` (title + Quill body, rating select, Linked Story dropdown
correctly listing only the author's own stories) → landed on the post page as a Draft (author
sees own drafts per the includeUnpublished rule) → `/blog/{id}/edit` round-trip → Publish checkbox
+ Save → `is_published=t` (psql). F36: post page renders title/author/date/rating/draft badge,
body, like affordance, and the comment section; seeded published+draft posts behave per the
author-visibility rules on the profile Blog tab. (Post-save the edit page stays put rather than
redirecting to the post — mild UX polish candidate, not unsound.)
