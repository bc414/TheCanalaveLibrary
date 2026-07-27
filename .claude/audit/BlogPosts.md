# Audit — BlogPosts/

**Features:** 35 (writing), 36 (display), 37 (polls). (56 — feature contributions — **CUT
2026-07-18**; see the Feature 56 CUT note below.) Universal `EditorView`
for composition (§5.30.2; stale §5.19 reference corrected 2026-06-24).

## Shared Context
**Entities (moving Core/Models/ → Core/BlogPosts/ as part of WU31):** `BaseBlogPost` (TPT root,
`ToTable("base_blog_posts")`, `Rating`→short, M:N `LikedByUsers`) → `ProfileBlogPost` (optional
`StoryId`, `SetNull`), `GroupBlogPost` (`GroupId`). `BasePoll` (TPT) → `SitePoll` / `BlogPostPoll`;
`PollOption` (unique `(PollId,Text)` and `(PollId,SortOrder)`). All blog posts support comments
(see Comments cluster). WU31 delivers L2/L3/L3.5/L4 for features 35/36 (profile blog posts only).
(The `FeatureContribution` entity and its `SetNull` diamond-breaking FKs were removed 2026-07-18
when Feature 56 was cut — see the Feature 56 CUT note below.)

## Feature 35 — Blog Post Writing
- **L1 — Stage 5.** TPT split sound. **L2 — Stage 5.** **L3/L3.5 — Stage 5.** **L4 — Stage 1** (visual sign-off pending; same pattern as WU13/WU24). **L6 — Stage 2.**
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; blog-post editor got the create→edit `forceLoad` fix for
  Quill-hosting pages (editor page not browser-driven in the flip's wave). Full wave narrative +
  the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.
- **WU-ErrorHandling note (2026-07-06).** Both editors (`BlogPostEditorPage`,
  `GroupBlogPostEditorPage`) embed `DraftAutosave` (`draft:blogpost:{id|new}`,
  `draft:groupblogpost:new:{groupId}`; Title + Content), cleared on successful submit;
  `BlogPostPropertiesForm` renders errors via `InlineAlert` and gained `SetContentAsync` (Quill
  push for restore); generic catches route through `ExceptionPresenter` + `LogError`. Strategy:
  `error-handling.md` §"Error Handling Strategy".
- **Settled constraints (2026-06-24, WU31):**
  - Content-editing Pattern 1: `/blog/new` + `/blog/{id}/edit` (write/auth), `/blog/{id}/{*slug}` (read-only).
    Spec §5 line ~1585 said "in-place editing" — overridden (blog is a multi-field form like Story).
  - No `Slug` column on `BaseBlogPost` (confirmed `InitialSchema`); `{*slug}` is cosmetic; `BlogPostId` (int) is the sole key.
  - Profile blog posts only for WU31; `GroupBlogPost` UI → WU32.
  - ~~Content-rating named query filter extends to `BaseBlogPost`~~ **SUPERSEDED by WU31.5
    (2026-06-24):** named filter removed from `BaseBlogPost`; blog-post content rating enforced via
    explicit `.Where(p => p.Rating <= max)` projection checks (see `content-safety.md` "Content Rating
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
- **WU-B2 settled decisions (2026-07-25):**
  - **Follower notifications fire on the publish transition** (`IsPublished` false→true in
    `UpdateBlogPostAsync`), never on draft create (the MA-709 seam). Fan-out: author-followers
    (`ReceiveAlerts`, type 13) plus — when story-linked — story followers/favoriters/read-it-later
    (14/15/16), disjoint by precedence 13 > 14 > 15 > 16. Republish re-notifies (intentional; unread-dedup
    absorbs bursts). Detail: `layer2-services.md` §"Comment & blog-post semantic methods" +
    `audit/Notifications.md` WU-B2 slice.
  - **`StoryId` is ownership-validated at write time** (create + update): a `StoryId` whose
    `Story.AuthorId` isn't the blog author throws `UnauthorizedAccessException`. The editor's
    own-stories dropdown was affordance only; the service gate closes the fan-out spam vector
    (forge `StoryId` → spam a popular story's audience).
  - **`GroupBlogPost.StoryId` removed** (entity + column via migration + `CreateGroupBlogPostDto` +
    editor picker + read-service group-branch projection). Group posts are for group topics; only
    profile posts speak about a specific story. Restores the original TPT design (Gemini Entry #930:
    `GroupBlogPosts (BlogPostId, GroupId)` only — the shipped `StoryId` was a deviation). Group
    spoiler posts therefore always take the interstitial's immediate-reveal path.

## Feature 36 — Blog Post Display
- **L1 — Stage 5.** **L2 — Stage 5** (profile context for WU31; story/group contexts → WU30/WU32).
  **L3/L3.5 — Stage 5. L4 — Stage 1** (visual sign-off pending).
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto (blog-post display not browser-driven in the flip's wave;
  the F37 poll blocks that render on it were). Full wave narrative + the 7 bugs found/fixed:
  `workplan.md` WU-GlobalFlip.
- **Settled constraints (2026-06-24, WU31):** `BlogPostCard` (leaf, profile feed), `BlogPostPage`
  (read-only view dispatcher), `BlogPostListingDto` for the feed. Author byline: plain hyperlink,
  not `UserCard` (mirrors StoryCard — too compact). `CommentSection` generalized for blog-post context
  (chapter XOR blog post; see `layer3.5-structure.md` "CommentSection — Multi-Context Dispatch").
  Draft posts (`IsPublished = false`) return null/NotFound to non-authors; author reads own
  mature/unpublished content via `IgnoreQueryFilters()` on derived scalar projections.
- **WU-B2 settled decisions (2026-07-25) — `HasSpoilers` now gates content, not just a badge:**
  - **Completion-gated spoiler interstitial on `BlogPostPage`** — full parity with the chapter-comment
    flow (`CommentItem.razor` §5.9.1 pattern, *not* the mature content-gate: a spoiler is a soft
    curtain, not a server-consent access boundary). `HasSpoilers` post renders blur-covered for
    non-authors; "⚠ Reveal spoiler" click reveals immediately when the post isn't story-linked or the
    viewer completed the linked story (`BlogPostDto.ViewerHasCompletedStory`, projected in
    `GetByIdAsync` from `UserStoryInteraction.IsCompleted`); otherwise `ConfirmDialog` first.
    Author auto-reveals; reveal state is ephemeral (re-hides on reload); inline markup, no shared
    `SpoilerCover` extraction at two consumers.
  - **`BlogPostCard` snippet suppressed under `HasSpoilers`** — the body-derived `ContentSnippet`
    leaked spoiler content in listings; replaced with a muted "Content hidden — contains spoilers"
    line (title/date/rating/badge stay).
  - Notifications always show the blog title — `HasSpoilers` is never threaded into the notification
    layer (a title is a broad descriptor; the spoiler is the content).
  - **Verified (2026-07-25):** `dotnet test` full suite green (2157/2157). Covering tiers:
    **RazorComponents** — `BlogPostPageTests.cs` (9: curtain shown to non-author/anonymous, absent
    for author and non-spoiler posts; immediate reveal for non-story-linked and completed viewers;
    ConfirmDialog open/confirm/cancel flow) + `BlogPostCardTests.cs` (2: snippet suppressed /
    shown); **Integration** — `CommentAndBlogNotificationTests.cs` `GetById_*` ×4
    (`ViewerHasCompletedStory` true/false/anonymous/non-story-linked) and the ownership-gate ×2
    (create/update with a foreign `StoryId` → `UnauthorizedAccessException`).
    `scripts/check-design-tokens.ps1`: no new findings (curtain uses `shadow-medium` + unflagged
    `blur-md`/`pointer-events-none`; `RichTextView` stays inside `ContentSurface` in both branches).
    **L4.5 browser pass — DONE (2026-07-25, real circuit vs. dev DB, `psql`-confirmed).** Created a
    story-linked `HasSpoilers` profile post as AuthorAlpha (draft → publish via edit); the
    publish transition fired the fan-out (`psql`: recipients TestUser + ReaderGamma each got
    **exactly one** type-13 row — they also favorite the linked story, so this confirmed the
    13>15 precedence-dedup live, not two rows). Bell rendered "AuthorAlpha posted a blog entry:
    {title}" and clicking navigated to `/blog/{id}` (BlogPostDirect enrichment). Curtain flow
    verified for every branch: blurred for the non-author; not-completed viewer → "Reveal spoiler"
    opens the "you haven't finished the linked story" `ConfirmDialog` → Reveal shows content;
    marking the viewer's `is_completed=true` → single click reveals with **no** dialog; reload
    re-hides (ephemeral); author sees content with no curtain. Also confirmed: chapter-comment →
    NewStoryComment bell "TestUser commented on {chapter title}" deep-linking to
    `/story/{id}/{ch}` (Chapter enrichment); the group blog editor renders **no** Linked Story
    picker; a group blog post renders **no** "About:" row. All verification rows cleaned up
    afterward.
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

### Requirements settled 2026-07-12 (chat deliberation; closes spec Open Question #6)

The Gemini discussions (2025-10-31, entries ~#1076) specified schema only — never behavior. The
detailed-UI Stage-1 gap was resolved in chat 2026-07-12. **These are settled — do not revisit:**

- **Per-poll owner-set config (new columns on `BasePoll`):** `AllowMultiple` (single vs
  multi-select), `ResultsVisibility` (`AfterVote` / `Always` / `AfterClose`),
  `AnonymityMode` (`Anonymous` / `Public` / `VoterChoice`; VoterChoice adds
  `PollVote.IsAnonymous` per-voter opt-in).
- **Config locks after first vote.** `AllowMultiple`/`ResultsVisibility`/`AnonymityMode` freeze
  once any vote exists (prevents retroactive anonymity exposure and multi→single vote
  invalidation). Name/description/options stay editable while open.
- **Lifecycle:** `DateOpened` may be future (scheduled open; not votable until then).
  `DateClosed` nullable — null = indefinite, open until manual close (manual close = stamp
  `DateClosed` = now; no extra flag). Votes changeable/retractable until closed. SitePoll
  `IsArchived` is display-only (moves it to the `/polls` archive list) — orthogonal to closed.
- **Options:** min 2 (write-service enforced), no upper cap. Fully editable while open; deleting
  a voted-on option cascades its votes.
- **Edit notification:** any material edit to an open voted-on poll notifies prior voters via a
  **30-minute quiet-period batch** (edits mark the poll dirty; a background sweep notifies once
  no further edit occurred for 30 min; one notification per burst). `NotificationTypeEnum.PollUpdated = 100`.
- **Results semantics:** `AfterVote` = viewer sees results iff they *currently* have a vote
  (retract → hidden again); guests see a "sign in to vote and see results" prompt. Tallies are
  optimistic-local only (own vote reflected instantly; others' on reload — no SignalR).
- **Permissions:** SitePolls created/managed by moderators+admins, inline on `/polls` (no
  separate admin area). BlogPostPolls by the blog post's author, managed in the blog editor,
  rendered as blocks after post content (multiple per post allowed — matches schema). Voting:
  any authenticated user.
- **Surface scope:** `/polls` page (active + archived SitePolls). **Open intent:** SitePolls
  should eventually surface on the home page — belongs to `roadmap.md` decision row 2
  (homepage sections), not this feature's work-unit.

### L1 reconcile note (2026-07-12)

The settled requirements reopened frozen L1 (Stage 5 → 4 → resolved same session):
1. **Config columns added** to `base_polls` (`AllowMultiple`, `ResultsVisibility`,
   `AnonymityMode` — enums `: short`) and `poll_votes.IsAnonymous`; `DateClosed` → nullable.
2. **Shadow-FK diamond fixed:** `BaseBlogPost.Polls` was `ICollection<BasePoll>`, which EF could
   not pair with `BlogPostPoll.BlogPost` — the snapshot carried a spurious second relationship
   (`base_polls.base_blog_post_blog_post_id`, letting a SitePoll point at a blog post). Retyped
   to `ICollection<BlogPostPoll>` + explicit pairing; shadow column dropped.
3. Poll entities moved `Core/Models/` → `Core/BlogPosts/` (legacy-folder rule).

### Stage-5 note (2026-07-12, WU-Polls)

`dotnet build` green; `dotnet test` green across all three tiers (final run after browser-found
fixes; ~38 new poll tests). (This note's original "L5 stays Stage 2" is superseded by the L5
bullet below, 2026-07-13.)

- **L1 — Stage 5** (post-reconcile; see L1 reconcile note above). Covering tier: Integration
  (`PollServiceTests` delete-cascade + FK paths exercise the migrated schema).
- **L2 — Stage 5** (`ServerPollReadService`/`ServerPollWriteService`, `Server/BlogPosts/`).
  Covering tier: **Integration** — `PollServiceTests` (18 tests: create permissions both kinds,
  validation, single/multi vote + replace/retract, pending/closed vote rejection, AfterVote
  visibility zeroing incl. retract-hides-again, Anonymous/VoterChoice name filtering, config lock
  pre/post votes, option reconcile (rename/delete/add/reorder with vote preservation),
  LastEditedAt material-vs-reorder stamping, close/archive orthogonality, delete cascade,
  non-owner manage gates). Rules helpers covered by **Unit** (`PollRulesTests`,
  `PollEditDtoTests`).
- **Edit-notification sweep — Stage 5** (`PollEditNotificationSweeper`/`PollEditNotificationWorker`,
  the SpotlightGoLiveSweeper worker/body split; `TestAppFactory` removes the worker). Covering
  tier: **Integration** (quiet-period elapsed → voters notified once, owner drop-self'd,
  RelatedEntityId = blog post id, idempotent re-sweep; not-elapsed → no-op).
- **L3/L3.5 — Stage 5** (`PollView` self-contained vote composite — follow-button precedent for
  the service injection; `PollEditorForm` presentational no-inject; `PollsPage` `/polls` with
  inline mod management; `BlogPostPage` poll blocks after content; `BlogPostEditorPage` Polls
  manage section). Page/dispatcher logic covered by Integration + the browser band (components
  inject services — no bUnit tier, same posture as `BlogPostPage`).
- **L4 — Stage 5** (element-role compliant: Container card, semantic-tint status badges,
  Indicator progress bars in `--color-progress`, action-vs-mission button split, ConfirmDialog
  destructive delete; `check-design-tokens.ps1` — no new findings; the one failing row is
  Import's pre-existing in-flight-WU finding). Purely-visual polish remains subject to the
  design-solidification sweeps (5→6 on sign-off), like every other L4 cell.
- **L4.5 — Stage 5 (browser band, 2026-07-12, server-only path, standing dev DB kept).**
  As AdminUser: created a Single/AfterVote/VoterChoice site poll inline on `/polls` (mission-blue
  New Site Poll, mod-only), voted publicly. As TestUser: AfterVote gate verified (no tallies/
  names/manage row pre-vote), voted anonymously (Piplup name suppressed in the public list while
  AdminUser's showed — psql: `is_anonymous` t/f per voter), retract → results hid again. Blog
  flow: created post → editor Polls section (`/blog/new` shows the save-first hint) → created a
  Multiple/Always/Public poll → flipped config pre-vote → published → poll block rendered on the
  view page after content, multi-vote counted both options with names, 1 distinct voter. Config
  lock affordance verified (3 selects + open date disabled once votes exist). Sweep verified
  end-to-end via the REAL worker: material rename stamped `last_edited_at`; after psql-backdating
  31 min the 1-min worker delivered `PollUpdated=100` to the voter (owner drop-self'd,
  `related_entity_id` = blog post id) and stamped `edit_notified_at` — all psql-ground-truthed.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; polls verified end-to-end in a real WASM runtime during the
  flip's browser wave (vote → results with public voter list → retract; the `[FromQuery]`
  vote-binding fix, workplan bug 2). Full wave narrative + the 7 bugs found/fixed: `workplan.md`
  WU-GlobalFlip.
- **Two runtime bugs found via the browser band and fixed same-session** (per the
  fix-same-session rule), both with regression coverage:
  1. `OfType<TChild>()` sources fed into the shared base-typed projection threw `No coercion
     operator is defined between types 'SitePoll' and 'BlogPostPoll'` — `/polls` crashed on
     first render. Fixed with base-typed `Where(p => p is TChild)` sources; convention recorded
     in `layer1-data-model.md` §TPT; regression net: `PollServiceTests` list tests.
  2. Bool `<select @bind>` with lowercase `value="true|false"` options silently failed
     (case-sensitive DOM match vs C# `"True"`) — `AllowMultiple` never persisted. Fixed with
     explicit domain-word values + `@onchange`; convention recorded in `layer3-logic.md`
     §"Bool `<select>`".
- **Deferred:** home-page SitePoll surfacing (open intent → homepage-sections decision,
  `roadmap.md` row 2). (The formerly-deferred L5 WASM enablement landed with WU-GlobalFlip —
  see the L5 bullet above.)

## Feature 56 — Feature Contributions — **CUT 2026-07-18**

**Verdict (Brian, in chat; decision row 3's last item).** Cut from the roadmap entirely, not
deferred. The feature was admin attribution of accepted site-feature suggestions — a user posts a
suggestion (as a blog post / comment in a "Site Development" group), an admin records a
`FeatureContribution` crediting them, incrementing `UserStats.FeatureContributions` and earning the
Architect badge (origin: GeminiDiscussions Sept–Nov 2025, lines 17036 / 17386 / 20861).

**Why cut.** The prosocial-recognition space is already covered by shipped features (Beta Reader
acknowledgments, Muse/Inspiration, Recommendations, Community Spotlight, Custom Lists); a formal
on-site attribution loop has near-zero value at trusted-beta scale (suggestions arrive via Discord);
and `FeatureContribution` was the single most complex delete-path in the schema (the three-way
`SetNull` "diamond": User→FC, User→BlogPost→FC, User→Comment→FC). The prosocial-badge intent that
motivated it is preserved by **keeping the Architect badge** as a manual grant.

**What was removed (2026-07-18).** `FeatureContribution` entity (`Core/Models/`), its `DbSet`, the
three `SetNull` FK configs + the `FeatureContributions` navigations on `User`/`BaseBlogPost`/
`BaseComment`, the `UserStat.FeatureContributions` counter column (+ its `UserStatRecalculator`
deferred-list mention and insert-column entry), the delete-path notes in
`ServerCommentWriteService`/`ServerBlogPostWriteService`, and the `UserStatRecalculatorTests` seed/
assertion. The sole pre-launch `InitialSchema` migration was regenerated (no shipped data; no
`.razor` UI ever referenced the feature). L1's former Stage 5 is void — the cell no longer exists.

**What was kept.** The **Architect badge** seed (`BadgeConfigurations` / `SiteBadges.Architect`,
"Helped develop a site feature.") stays in the catalogue. Grant path is a direct `user_badges` insert
(psql) — `IBadgeWriteService.AwardAsync` stays deliberately unmapped (no admin HTTP route; see
`BadgeEndpoints` class doc). No admin-grant UI was built; that would be a separate future decision.
See `audit/Badges.md` for the retained-badge note.

## L4.5-Browser verification (2026-07-02) — F35 + F36 → Stage 5

F35: created a post via `/blog/new` (title + Quill body, rating select, Linked Story dropdown
correctly listing only the author's own stories) → landed on the post page as a Draft (author
sees own drafts per the includeUnpublished rule) → `/blog/{id}/edit` round-trip → Publish checkbox
+ Save → `is_published=t` (psql). F36: post page renders title/author/date/rating/draft badge,
body, like affordance, and the comment section; seeded published+draft posts behave per the
author-visibility rules on the profile Blog tab. (Post-save the edit page stays put rather than
redirecting to the post — mild UX polish candidate, not unsound.)

### WU-AuditFixPass note (2026-07-18)

MA-705 closed: `ToggleLikeAsync` writes `LikeCount` as an atomic ±1 delta (0-clamped) instead of a
C#-computed absolute, and re-reads the landed value for the returned count. `BlogPostPage`'s like
error normalized to `InlineAlert` + `ExceptionPresenter` (raw `ex.Message` gone, unexpected logged);
`BlogPostEditorPage` missing-post branch uses `NotFound()` (BlogPostPage's inline soft message
deliberately kept — missing-vs-hidden ambiguity). Full detail: `workplan.md` WU-AuditFixPass.

### WU-AuditFixPass-2 note (2026-07-18)

Endpoint-authz sweep + MA-706, F35/F36 (cells stay Stage 5 — behavior corrected in place):
`GetByAuthorAsync` now owner-derives `includeUnpublished` (a forged `includeUnpublished=true` no longer
leaks drafts to anyone; degrades to the public view); `GetForEditAsync` enforces ownership server-side
(was client-check-only — any authed user could read any blog draft's full content), throwing →403 with
client + editor page mirroring the chapter forbidden pattern. **L2 MA-706:** `IBlogPostReadService` DI
rebound to the read impl (`ServerBlogPostReadService`), not the derived write class. Covered:
`BlogPostEndpointsTests` (Integration) + browser (cross-author `/edit`→403). Full detail:
`workplan.md` WU-AuditFixPass-2.

---

**WU-ParentVisibility slice (2026-07-26) — F35/F36/F37.** Closed `hidden-deferrals-tracker.md`
**D2**, and found it was the tip of a 38-surface class. Polls: `GetPollsForBlogPostAsync` and
`GetPollAsync` now gate on `BlogPostVisibilityGuard`, and `VoteAsync` does too — the write half was
the more damaging one, since a vote on a draft's poll sets `ConfigLocked` and freezes
`AllowMultiple`/`ResultsVisibility`/`AnonymityMode` before the author ever publishes. The leak was
wider than D2 stated: the full option-text list, the owner's username, and `ConfigLocked` as a
has-anyone-voted side-channel, leaking past the draft gate *and* the rating/reveal gate *and*
`IsTakenDown`. Blog posts: `GetByGroupAsync` gates on `GroupVisibilityGuard` (an M-audience group's
E/T posts were anonymously readable), and `ToggleLikeAsync` on `BlogPostVisibilityGuard` (a non-author
could inflate `LikeCount` on someone's unpublished draft). `GetByIdAsync` now **delegates** its
draft+consent gate to the guard and owns none of the rule — facts come from its existing projection,
so it pays no extra query. `PollEndpoints`' class doc previously argued that server-side tally blanking
made an endpoint gate unnecessary; that was the reasoning that produced D2, and it has been rewritten.
F37's settled note is extended: a blog-post poll is exactly as visible, and exactly as votable, as its
post.

Invariant, guards, and the two root causes: `identity-and-authorization.md` §"Parent-visibility guards" (conditionality kind (g)). Enforcement: `Tests.Integration/ParentVisibilityContractTests.cs`. Full narrative: `workplan.md` WU-ParentVisibility. **No Stage number changed — every affected cell was already Stage 5 and remains 5.**
