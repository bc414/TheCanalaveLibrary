# Audit — Comments/

**Features:** 23 (posting), 24 (display & pagination), 25 (likes), 26 (spoiler comments).

## Shared Context
**Entities (Core/Comments/):** `BaseComment` (TPT root, `ToTable("base_comments")`, `ParentCommentId`
self-ref `SetNull`, `LikeCount`, explicit `CommentLike` junction) + four children, each `.ToTable()`:
`ChapterComment` (+ `IsSpoiler`, `DatePosted`), `UserProfileComment` (`DatePosted`), `GroupComment`
(`DatePosted`), `BlogPostComment` (`DatePosted`). `DatePosted` is declared on each derived class (not
`BaseComment`) so it physically lands on the child table, enabling the golden index
`(chapter_id, date_posted DESC)` on `chapter_comments`. **WU31.5 (2026-06-24):** this denormalization
was retrofitted — before WU31.5, `DatePosted` was on `base_comments` despite the spec and derived
Fluent config comments implying otherwise (spec §4.3 prescribed a "configure on derived to override"
technique that does not work in EF Core 10; see `layer1-data-model.md` §"Denormalization with TPT").
TPT is Settled Axiom #2. Cluster moved from `Core/Models/` → `Core/Comments/` in WU19
(2026-06-23) — organizational only, namespace unchanged. Services (`ICommentRead/WriteService` +
`ServerComment{Read,Write}Service`) live in `Core/Comments/` and `Server/Comments/` respectively.

## Feature 23 — Comment Posting
- **L1 — Stage 5.** TPT hierarchy + per-child `DatePosted` (declared on each derived class, lands on
  child table — denormalized per §1117); orphan handling via `SetNull`. Matches §5.9.
  **WU31.5 Stage-5 note (2026-06-24):** `DatePosted` moved from `BaseComment` → each derived class
  (`ChapterComment`, `BlogPostComment`, `GroupComment`, `UserProfileComment`); migration
  `WU31_5_DenormalizeTptDiscoveryColumns` copies data (base→child) before dropping base column;
  `dotnet test` 691/691 green — Integration tier confirms write + read round-trip.
  **WU31_5b Stage-5 note (2026-06-25):** Four phantom down-navigation properties remained on
  `BaseComment` (`BlogPostComment`, `ChapterComment`, `GroupComment`, `UserProfileComment`) — the
  twin defect to the `DatePosted`-on-base issue WU31.5 fixed. EF materialized each as a
  separate optional 1-to-many with `base_comments` as the dependent, creating four spurious FK
  columns (`{type}_comment_comment_id`) plus their indexes on `base_comments`, pointing **base
  → child** (backwards TPT). These formed FK cycles (`base_comments ↔ {child}_comments`) that
  broke Respawn's topological sort, leaving `groups` uncleaned between integration tests and
  causing `GroupServiceTests` to fail with `duplicate key` on `ix_groups_group_name`. Fix:
  removed the four nav properties from `BaseComment.cs`; migration
  `WU31_5b_DropPhantomBaseCommentFKs` drops the 4 columns / 4 indexes / 4 FK constraints.
  Convention recorded in `canalave-conventions/layer1-data-model.md`. Verified: `dotnet test`
  → 298 integration / 414 unit / 397 RazorComponents = 1,109 total, all green.
  **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13)** — endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; comment posting verified in a real WASM runtime during the
  flip's browser wave (sanitized row 323825, psql ground truth). Full wave narrative + the 7 bugs
  found/fixed: `workplan.md` WU-GlobalFlip. **L6 — Stage 5 (WU-L6, 2026-07-07)** — golden index
  `ix_chapter_comments_chapter_id_date_posted` built in `L6_IndexBatch` (+ the three sibling
  contexts, identical query shape). Measured at 324k seeded comments: roots page p50
  24.32→0.29 ms, p95 136.82→0.38 ms (−98.8% — the before-plan burned ~20 ms on parallel-worker
  launch + sort; after-plan is an ordered backward index scan into the LIMIT). Detail + plans:
  `layer6-indexes.md` §"Comment golden indexes", `TheCanalaveLibrary.PerfBaseline/results/`.
- **L3-Logic / L3.5-Structure / L4-Style — Stage 5 (WU20, 2026-06-23):** See Feature 24 Stage-5 note
  (WU20 is a single integrated work-unit covering 23/24/25/26 L3/L3.5/L4; the components,
  tests, and visual sign-off are described there).
- **L2 — Stage 5 (WU19, 2026-06-23):** `ICommentWriteService.PostChapterCommentAsync(PostChapterCommentDto)`
  in `Server/Comments/ServerCommentWriteService.cs`. Requires authenticated user; validates via
  `CommentValidations.CanSave()` (throws `CommentValidationException`); verifies chapter exists; if replying,
  verifies parent is on same chapter; sanitizes HTML via `IHtmlSanitizationService`; inserts `ChapterComment`
  row; returns new `CommentId`. Notification seam left as `// TODO(WU22)`. Chapter context only for MVP.
  **Verified:** `dotnet build` green; `dotnet test` green — 367 tests total (112 Unit, 122 RazorComponents,
  133 Integration). Covering tier: **Unit** (`CommentValidationsTests` — 7 tests for `CanSave` empty/
  whitespace/valid/spoiler-flag) + **Integration** (`CommentWriteServiceTests` — 18 tests via Testcontainers
  Postgres, covering post root, IsSpoiler round-trip, script tag stripped on save, reply, cross-chapter
  reply guard, empty text validation, anonymous guard). Server booted clean; DI resolved
  `ICommentReadService`/`ICommentWriteService` without exception.

## Feature 24 — Comment Display & Pagination
- **L1 — Stage 5.** **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13)** — endpoints + client impl live
  (WU-L5Sweep) and the site now runs global InteractiveAuto; comment display + pagination verified
  in a real WASM runtime during the flip's browser wave. Full wave narrative + the 7 bugs
  found/fixed: `workplan.md` WU-GlobalFlip. **L6 — Stage 5 (WU-L6, 2026-07-07** — golden index built +
  measured −98.8% on the roots page; see the Feature 23 L6 note).
- **L3-Logic / L3.5-Structure / L4-Style — Stage 5 (WU20, 2026-06-23):**
  Built `CommentEditor` leaf (shared editing surface), `CommentItem` leaf, and `CommentSection`
  coordination composite, all in `TheCanalaveLibrary.SharedUI/Comments/`. The three components cover all
  four features (23–26) in one integrated work-unit.
  - **`CommentEditor.razor`** (pure leaf — no service injection): wraps `EditorView` (pull-on-submit
    via `@ref` + `GetHtmlAsync()`); `SaveLabel` drives the primary button label ("Save" / "Reply" /
    "Post Comment"); Cancel renders only when `OnCancel.HasDelegate` (`.HasDelegate` idiom — persistent
    composer leaves it unwired, so no Cancel appears); optional `ShowSpoilerToggle` + `@bind-Spoiler`
    for the new-comment composer; `Busy` disables both buttons. Primary button carries
    `aria-label="@SaveLabel"` and cancel carries `aria-label="Cancel"` for reliable test selection
    (BlazoredTextEditor renders its own toolbar buttons in the same DOM subtree; aria-label is the
    only collision-free selector).
  - **`CommentItem.razor`** (pure leaf — no service injection): author block (avatar + link when
    `AuthorId != null`; "[deleted user]" span when null); body swaps `RichTextView` ↔ `CommentEditor`
    on `IsEditing` (section-owned `_editingId` flag — one edit at a time); spoiler blur/cover overlay
    with completion-gated reveal (`ConfirmDialog` inside item for the "haven't finished" dialog path;
    no service needed); like/reply/edit/delete affordances gated by `.HasDelegate` + `IsOwnComment`.
  - **`CommentSection.razor`** (coordination composite — injects `ICommentWriteService : ICommentReadService`
    for the coordinated-paginated-region pattern recorded in `layer3-logic.md`): owns paginated load,
    two-level tree assembly (roots + replies from flat `ParentCommentId`), optimistic like
    reconciliation (flip optimistically → `ToggleLikeAsync` → reconcile from `CommentLikeResultDto`),
    `_editingId` / `_replyingToId` coordination, delete `ConfirmDialog` (section-owned, `IsDestructive`),
    persistent new-comment composer (`OnCancel` intentionally unwired), inline reply + edit composers.
  - **Test-finding lesson recorded:** `aria-label` selectors are required when a component embeds
    BlazoredTextEditor — its toolbar renders empty-text buttons in the same subtree, and text-content
    scanning (`First(b => b.TextContent.Trim() == "Save")`) hits them first, finding buttons with no
    Blazor onclick or disabled attribute. Added `aria-label="@SaveLabel"` / `aria-label="Cancel"` to
    both CommentEditor buttons; all tests use `cut.Find("button[aria-label='…']")`.
  - **Covering test tier: RazorComponents** — `FakeCommentWriteService` (records calls, canned results),
    `CommentEditorTests` (11 tests: SaveLabel, Cancel HasDelegate, OnSave/OnCancel callbacks, spoiler
    toggle, Busy state), `CommentItemTests` (21 tests: author block, edit-mode swap, spoiler
    blur+reveal both paths, all affordance callbacks, HasDelegate + IsOwnComment gating),
    `CommentSectionTests` (13 tests: initial load, empty state, tree assembly, like + reconcile, post
    with IsSpoiler, reply carries ParentCommentId, edit save, delete+confirm, page change, anonymous
    hides composer). 181/181 RazorComponents pass; 112/112 Unit; 133/133 Integration (verified on
    independent run — full-suite has one intermittent Npgsql connection hiccup, unrelated to WU20).
  - **L4 visual sign-off (2026-06-23):** throwaway harness added to `HomeDesktop.razor`
    (`<CommentSection ChapterId="1" CurrentUserId="1" ... />`), server booted
    (`http://localhost:5028` → `200`), confirmed section renders (loading state → "No comments yet."
    for an empty chapter, new-comment composer with EditorView + spoiler checkbox, PaginationControls
    suppressed at ≤1 page), harness removed. Note: `ChapterPage` host wiring (Feature 7 L3-Logic)
    will supply live `ChapterId`, `CurrentUserId`, `UserHasCompletedStory` when that work-unit builds;
    `CommentSection` is a standalone self-contained injectable region, so standalone-build sign-off
    is sufficient here.
- **L2 — Stage 5 (WU19, 2026-06-23; column placement verified WU31.5):**
  `ICommentReadService.GetChapterCommentsAsync(chapterId, page, pageSize)` in
  `Server/Comments/ServerCommentReadService.cs`. Two-step load: (1) count roots + paginate root ids via
  `ChapterComments` DbSet (`DatePosted DESC`); (2) fetch roots-on-page + direct replies in one query,
  projecting to `CommentDto` with per-viewer `IsLikedByCurrentUser`. In-memory ordering restores
  roots newest-first, replies oldest-first beneath each root.
  **WU31.5 (2026-06-24):** `DatePosted` is now physically on `chapter_comments` — the Step-1
  pagination order is child-table-served (no join to `base_comments` for sorting). Queries through
  typed child DbSets (`ChapterComments`, `BlogPostComments`) are unchanged in code; the column
  just comes from the right table now. `dotnet test` 691/691 green.
  **Verified:** Integration tier (`CommentReadServiceTests` — 6 tests: empty chapter, TotalRootCount,
  DatePosted DESC order, reply under root, pagination, IsLikedByCurrentUser per-viewer).

## Feature 25 — Comment Likes
- **L3-Logic / L3.5-Structure / L4-Style — Stage 5 (WU20, 2026-06-23):** See Feature 24 Stage-5 note.
  Optimistic like reconciliation + `CommentItem` like affordance (`.HasDelegate` gated) built as part
  of the integrated WU20 delivery.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto (like toggle itself not browser-driven in the flip's wave;
  the comment read/post flows were). Full wave narrative + the 7 bugs found/fixed: `workplan.md`
  WU-GlobalFlip.
- **L1 — Stage 5 (WU19, 2026-06-23; stale-code trap resolved).** Prior note: the code used an implicit EF
  many-to-many (`BaseComment.LikedByUsers` ⇄ `User.LikedComments`) instead of the explicit `CommentLike`
  junction called for in spec §6.11. WU19 introduced `CommentLike.cs` in `Core/Comments/` (with `CommentId`
  + `UserId` PK, configured in `CommentConfigurations.cs`) and updated `BaseComment` to use `ICollection<CommentLike> Likes`.
  The implicit M:N nav properties (`LikedByUsers`/`LikedComments`) were removed. Schema already generated
  the `comment_likes` table clean in `InitialSchema` — no migration needed for this fix (the table shape was
  always correct; only the entity model was wrong). **Verified:** `dotnet test` green (see Feature 23 note);
  `ToggleLikeAsync` tested by `CommentWriteServiceTests` (like increments `LikeCount` + creates junction row;
  unlike decrements + removes row; anonymous guard; delete cascades `CommentLike`).
- **L2 — Stage 5 (WU19, 2026-06-23):** `ICommentWriteService.ToggleLikeAsync(commentId)` in
  `ServerCommentWriteService`. Loads `BaseComment` + its `CommentLike` for the current user in one round-trip
  (filtered `Include`); toggles presence + adjusts denormalized `LikeCount` (floor 0 on decrement); saves;
  returns `CommentLikeResultDto(int LikeCount, bool IsLiked)`. No notification, no `DateLiked` — §6.11
  anti-addictive design. **Verified:** Integration tier (see Feature 23).
  **WU-CounterAtomicity Stage note (2026-06-27):** `ToggleLikeAsync` previously used tracked
  read-modify-write (`comment.LikeCount++` / `Math.Max(0, ... - 1)`) — the lone deviation from the
  codebase's atomic-counter pattern. Replaced with `ExecuteUpdateAsync(SetProperty(c => c.LikeCount,
  c => c.LikeCount + delta))` after the join-row `SaveChangesAsync`. Returned DTO value unchanged
  (optimistic `loaded + delta`). Concurrency fix not automatable (no parallel-request seam); covered by
  existing sequential `ToggleLikeAsync` integration tests confirming correct counter behavior + code review
  that the SQL is now `SET like_count = like_count + delta`. Convention documented in
  `layer2-services.md §"Counter mutation rule"`. `dotnet test` 1232/1232 pass.

## Feature 26 — Spoiler Comments
- **L3-Logic / L4-Style — Stage 5 (WU20, 2026-06-23):** See Feature 24 Stage-5 note.
  Completion-gated reveal built in `CommentItem`: `HandleRevealClick()` → immediate reveal if
  `UserHasCompletedStory`, else sets `_showSpoilerConfirm` (in-item `ConfirmDialog` — no service
  needed); `_isRevealed` is ephemeral (re-hides on page load, §5.9.1). Blur cover uses Tailwind
  `blur-md` + `pointer-events-none` overlay; "Reveal spoiler" button carries `aria-label="Reveal spoiler"`.
  **L3.5 — Stage 5** (WU9, 2026-06-21 — see note below). **L5 — Stage 5 (WU-GlobalFlip,
  2026-07-13)** — endpoints + client impl live (WU-L5Sweep) and the site now runs global
  InteractiveAuto (spoiler reveal not browser-driven in the flip's wave; the comment read/post flows
  were). Full wave narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.
- **L1 — Stage 5 (gap found and closed in WU19, 2026-06-23).** `IsSpoiler` is on `ChapterComment` only
  (chapter-scoped, not `BaseComment`) — exactly §5.9.1. However the property was **missing from the entity**
  and the DB had no `is_spoiler` column despite L1 being previously marked Stage 5. WU19 added
  `public bool IsSpoiler { get; set; }` to `ChapterComment.cs` and generated migration
  `20260623222518_AddIsSpoilerToChapterComment` (`is_spoiler boolean NOT NULL DEFAULT false`). The same
  migration also corrected a stale FK name: `fk_base_comments_base_comments_chapter_comment_comment_id` →
  `fk_base_comments_chapter_comments_chapter_comment_comment_id` (was pointing at `base_comments` instead
  of `chapter_comments`). Applied via `DataSeeder.MigrateAsync()` on startup.
- **L2 — Stage 5 (WU19, 2026-06-23):** `IsSpoiler` flows through `PostChapterCommentDto.IsSpoiler` →
  `PostChapterCommentAsync` sets `ChapterComment.IsSpoiler` → stored in DB → projected into `CommentDto.IsSpoiler`
  in the read service → surfaces to WU20's `CommentItem` as a `[Parameter]`. No separate write method —
  spoiler flag is set at post time only; editing a comment does not change its spoiler state (WU20 may
  revisit). **Verified:** Integration tier — `PostChapterComment_IsSpoilerTrue_RoundTrips` confirms
  `IsSpoiler = true` persists and reads back correctly.
### WU-ComponentSoundness Stage note (2026-06-27)

**Cell affected:** F26 L3-Logic (CommentSection, CommentItem) — correctness polish inside an
already-aligned Stage-5 cell; no stage transition.

**F3 — CommentSection list-keying (spoiler-state leak, now closed):**

`CommentSection.razor` now carries `@key="root.CommentId"` on the root `<CommentItem>` loop and
`@key="reply.CommentId"` on the nested reply `<CommentItem>` loop.

Root cause: `CommentItem` holds `private bool _isRevealed` as ephemeral private state (per spec §5.9.1
— spoiler re-hides on every page load). Without `@key`, Blazor matched `<CommentItem>` instances
positionally. When the user revealed a spoiler at position 0 (`_isRevealed = true` on that instance) and
then paginated to page 2 (new comments loaded into the same DOM slots), the position-0 instance was
reused — `_isRevealed` stayed `true` from the previous comment, so the new comment's spoiler rendered
as already-revealed without the user ever clicking Reveal. This bypassed the spec's completion-gate
requirement (§5.9.1).

Fix: `@key="root.CommentId"` forces Blazor to destroy and recreate the keyed CommentItem whenever the
CommentId in that slot changes — the fresh instance starts with `_isRevealed = false` and the spoiler
is hidden until the user explicitly clicks Reveal.

Covering tier: **RazorComponents** —
`CommentSectionTests.KeyedList_WhenSpoilerPaginates_NewCommentStartsHidden_NotRevealedFromPreviousInstance`.
Convention recorded in `layer3.5-structure.md` §"`@key` on `@foreach` over stateful children."

---

- **WU9 Stage-5 note (2026-06-21):** built the universal `ConfirmDialog` container composite at
  `SharedUI/Dialogs/ConfirmDialog.razor` (new cross-cutting cluster — no owning feature, mirrors
  `RichText/`/`Lookups/`). Contract: `@bind-IsOpen` (two-way `IsOpen`/`IsOpenChanged`), `Title`/
  `Message` for simple bodies, `ChildContent` for rich bodies (wins over `Message` when set),
  `ConfirmText`/`CancelText`, `IsDestructive` (red confirm button vs. green), `OnConfirm`/`OnCancel`
  EventCallbacks. Renders nothing when `!IsOpen`. Backdrop click cancels; panel uses
  `@onclick:stopPropagation`. Overlay shell (backdrop + panel) reuses the convention `EditorView`'s
  preview popup already established — recorded once in `layer3.5-structure.md`'s Container Composite
  section, not duplicated. This flips only `26 L3.5-Structure` — the spoiler-specific consumer wiring
  (blur/cover, completion-gated reveal calling this dialog) is WU20's job, not this work-unit's; flips
  `26 L3-Logic`/`L4` are unaffected. **Verified:** `dotnet build` green (4 projects, 0 new warnings);
  Tailwind JIT picked up `bg-danger` (theme token already existed, just unused) on `npm run css:build`;
  live server run, homepage `200`; user-confirmed visual check via a throwaway harness on
  `HomeDesktop.razor` (message-only dialog, `ChildContent` dialog, `IsDestructive` variant, backdrop
  click and Confirm/Cancel all round-tripping `@bind-IsOpen` correctly) — harness removed immediately
  after confirmation (self-contained, no missing-producer dependency, unlike WU4's TagChip harness).

**Browser-pass fix (2026-07-01) — CommentSection composer clear-after-post (L3, stages unchanged):**
Posting from the persistent "Leave a comment" composer left the just-posted text in the Quill editor
(Quill retains content across renders; `Html` is seed-only) — inviting accidental double-posts.
`EditorView` gained `SetHtmlAsync` (wraps `LoadHTMLContent`), `CommentEditor` gained `ClearAsync`,
and `CommentSection.HandlePostAsync` clears the composer via `@ref` after a successful post (reply
and edit composers unmount, so only the persistent composer needed this). **Verified:** browser —
post → comment appears, composer shows placeholder; second post confirms. Not bUnit-coverable
(Quill JS interop is faked in that tier).

## L4.5-Browser verification (2026-07-01) — F23 + F24 + F25 + F26 → Stage 5, no new bugs

Driven on the seeded chapter-1 thread as TestUser (plus the prior wave's root-post +
composer-clear verification): threaded reply posted via the inline Reply composer (renders under
the root, oldest-first within the thread, newest-root-first across roots — the two-step ordering
live); owner affordances correct per comment (Edit/Delete on own, Reply/Report on others'); like
toggled on another user's reply (optimistic heart + `comment_likes` row + `LikeCount` counter via
psql; an accidental like/unlike round-trip also confirmed toggle-off). Spoiler comment renders
covered; clicking it raised the completion-gated ConfirmDialog ("You haven't finished the story
yet…") since TestUser hasn't completed the story; Reveal shows the text — the full WU20 gate.
Pagination controls present but not depth-exercised (seed corpus is under one page; ordering
logic is bUnit/Integration-covered). **Server-side observation (not blocking):** the like write
path accepts likes on one's own comment (UI renders the button on own comments too) — flag for a
future rules pass if self-likes should be rejected.

## WU-ErrorHandling note (2026-07-06) — error surfaces normalized, rate-limit gap closed

`CommentSection`'s five catch sites now route through `ExceptionPresenter` (single generic
`catch` + translate; unexpected → `LogError` with entity IDs) instead of surfacing raw
`ex.Message` — `KeyNotFoundException`'s framework text no longer reaches users, and the old
*filtered* catches let `WriteRateLimitExceededException` (comment posting is throttled,
`security.md`) escape to circuit teardown; it now shows its wait-N-seconds message inline. The
inline error renders via `InlineAlert`; every CommentSection consumer site (chapter / blog post /
group ×2 / profile ×2) is wrapped in a compact `comments` `CanalaveErrorBoundary` island.
Strategy + placement map: `error-handling.md` §"Error Handling Strategy". Covered by existing
CommentSection bUnit suites (message text unchanged for typed validation) + the new
`CanalaveErrorBoundaryTests`/`InlineAlertTests` (RazorComponents tier).
