# Slice 5 — Patterns Inventory (Social)

Clusters: Comments, Recommendations, Following/Vouches, Messaging, Groups. Product LOC ≈ 6.9k
(Core ~2.0k, Server ~2.9k, SharedUI ~4.0k incl. shared, Client ~0.7k — measured non-migration).

## 1. Pagination
mechanism: offset (`Skip/Take`). Comments/messages page on scalar ids first (two-step), then hydrate.
Groups return `(T[], TotalCount)` tuples → `PagedResult<T>` at the HTTP boundary; comments return a
`CommentPageDto(Comments, TotalRootCount)` record. `PaginationControls` in CommentSection; MessageThread
uses a "load older" accumulate-append instead of page controls; recommendations are unpaged (per-story set).
exemplar: `ServerCommentReadService.cs:39-45` (root-id page). deviations: none material.

## 2. DTO mapping
mechanism: direct `.Select()` to `record` DTOs; per-viewer flags via correlated EXISTS (`c.Likes.Any(l =>
l.UserId == currentUserId)`) short-circuited on null viewer. Group folder tree = flat load + in-memory
recursive nest. `record` DTOs throughout; `with`-expression optimistic updates in sections.
exemplar: `ServerCommentReadService.cs:59-72`. deviations: none.

## 3. Error surfacing
mechanism: **split** — Comments normalized (WU-ErrorHandling): `InlineAlert` + `Translate`→`ExceptionPresenter`
(logs unexpected) + `CanalaveErrorBoundary` islands. Recommendations/Messaging/Groups NOT normalized: raw
`ex.Message` or generic literals into hand-rolled danger `<p>`/`<div role=alert>`. GroupPage routes
service errors to `IToastService` via `ExceptionPresenter.GetUserMessage` (no unexpected-logging).
exemplar (good): `CommentSection.razor:118,275-284`. deviations: **MA-501** (RecommendationSection raw
ex.Message), **MA-504** (MessageThread/ComposeConversationModal/GroupCreateEditPage hand-rolled).

## 4. Form patterns
mechanism: `@code`-state composers (CommentEditor/RecommendationEditor/MessageComposer wrap EditorView,
pull-on-submit `@ref.GetHtmlAsync()`); validation server-side via `{Feature}Validations.CanSave/Validate`
→ typed `*ValidationException`. Group create/edit uses radio → `GroupAudienceTypeMapper` preset round-trip.
exemplar: `VouchButton.razor:78-88` (pull-on-submit). deviations: hand-rolled alert markup (MA-504).

## 5. Flyout/overlay mechanics
mechanism: `ConfirmDialog` (WU9 atom) for destructive confirms (comment/rec delete) and for the
vouch-note EditorView host; `ComposeConversationModal` is a fixed-overlay composite. `RecommendationCard`
Hidden-Gem/spotlight are inline toggles, not overlays.
exemplar: `VouchButton.razor:44-59` (ConfirmDialog hosting EditorView). deviations: none.

## 6. Optimistic updates & debounce
mechanism: optimistic like with reconcile-from-result + rollback-on-throw (`CommentSection.HandleLike`,
`RecommendationSection.HandleLike`); FollowButton/VouchButton flip local `_isFollowing`/`_isVouched` after
awaited write (no rollback — self-contained composite, errors bubble to boundary). **No debounce timer** in
slice (contrast S4 MA-401). exemplar: `CommentSection.razor:236-268`. deviations: RecommendationSection's
rollback uses raw ex.Message (MA-501).

## 7. Disposal & lifecycle
mechanism: `MessagesNavLink @implements IDisposable` unsubscribes `LocationChanged` (`:66-69`);
`RecommendationEditor @implements IAsyncDisposable` disposes its `PeriodicTimer`. Dispatchers guard
`_initialized` + sentinel for OnParametersSetAsync reloads.
exemplar: `MessagesNavLink.razor:66-69`. deviations: none (both subscriptions/timers disposed).

## 8. Query shape
mechanism: factory-per-method (`await using ReadDbFactory.CreateDbContextAsync()`) universally; two-step
id-page then hydrate for comments/messages; typed TPT child DbSets (`ChapterComments` etc.) avoid the
base_comments shadow-FK trap; correlated subqueries for last-message/unread-count (no N+1). No `AsSplitQuery`
(projections, not Includes). exemplar: `ServerGroupReadService.BuildFolderTreeAsync:155-195` (flat + nest).
deviations: none; MA-510 flags read-method duplication (economy, not correctness).

## 9. Write-method skeleton
mechanism: auth-guard → (`EnsureAllowed` where throttled) → `dto.CanSave()`/`Validate()` → existence check
on `writeDb` → `sanitizer.Sanitize` → construct `+ DateTime.UtcNow` → `SaveChangesAsync` → atomic counter
`ExecuteUpdateAsync` → best-effort post-commit notify (try/`LogWarning`). Author/participant/admin gate
loads the entity and compares before mutate. exemplar: `ServerCommentWriteService.cs:20-68`. deviations:
**MA-502** (`RecordSuccessAsync` tracked `++`); **MA-508** (CreateGroupAsync no throttle); **MA-507**
(SetHiddenGem `(int?)` projection).

## 10. Endpoint & client shape
mechanism: thin `{Feature}Endpoints` route groups, every write wrapped in shared
`EndpointHelpers.ExecuteWriteAsync` (single exception→status map) + `.RequireAuthorization()`; reads public
except messaging (all-authed) and incoming-vouches. Client impls uniform: `ThrowIfWriteFailedAsync` switch,
`ReadFromJsonAsync` for non-null writes, `GetNullableFromJsonAsync` for `Task<T?>` reads.
exemplar: `CommentEndpoints.cs:55-58`. deviations: **MA-505** (business-rule `InvalidOperationException`→401
not 400 in the shared helper — self-documented in Following/Recommendation endpoints).

## 11. Sanitization & derived fields
mechanism: sanitize-once-on-save on every EditorView-fed field (6/6 paths — coverage table in findings);
`RecommendationText.CountPlainTextLength` for the 500-char min gate (stripped text); MessagePreview strips
tags off already-sanitized HTML for display only. exemplar: `ServerRecommendationWriteService.cs:57-60`.
deviations: **none** — MA-201 class clean (the headline).

## 12. Notification triggering
mechanism: best-effort post-commit `try { await notifications.Notify*Async(...) } catch { LogWarning }`
AFTER the primary `SaveChangesAsync` (clean change-tracker); semantic methods (`NotifyNewFollowerAsync`,
`NotifyNewVouchAsync`, `NotifyStoryHiddenGemAsync`, `NotifyNewGroupStoryAsync`). Messaging deliberately never
notifies (settled two-unread-systems). exemplar: `ServerFollowingWriteService.cs:59-64`. deviations:
**MA-506** — comment-author notifications unwired behind stale `TODO(WU22/33)`, inconsistent across contexts.

## 13. Counter updates
mechanism: atomic `ExecuteUpdateAsync(SetProperty(x => x.C, x => x.C + delta))` for likes and all UserStats
(CommentsWritten, RecommendationsWritten/Received, FollowerCount, AuthorsFollowed, GroupsJoined); floor-0 on
decrement in the returned DTO. exemplar: `ServerCommentWriteService.cs:306-308`. deviations: **MA-502** —
`RecordSuccessAsync` uses tracked `rec.SuccessfulRecCount++` (the lone non-atomic counter, multi-reader race).

## 14. Test idioms
mechanism (from audit notes + tier records): Integration (Testcontainers-Postgres) for write/read services
with FK-parent seeding (comments need chapter+story+user; group waterfall needs rated story); reject-at-limit
tests call the service the natural N times (vouch-6th, hidden-gem-6th) relying on Respawn reset;
RazorComponents with `Fake{Feature}WriteService` + `aria-label` selectors (BlazoredTextEditor collision rule);
Unit for validations + `GroupAudienceTypeMapper`. deviations: not independently re-run (read-only pass) —
tier assignments taken from audit Stage notes.

## 15. Code economy (dim 15 — fixed feature set)

**(a) Per-cluster product+test LOC & pattern tax**

| Cluster | Product (Core/Server/SharedUI/Client) | Pattern-tax share |
|---|---|---|
| Comments | ~350 / ~680 / ~760 / ~130 ≈ 1.9k | 4× per-context read methods (MA-510); 4× per-context post methods (settled write-side); endpoints/client boilerplate |
| Recommendations | ~330 / ~630 / ~680 / ~180 ≈ 1.8k | 8 write methods (submit/edit/delete/like/gem/spotlight/success/attribution); endpoints |
| Following | ~200 / ~285 / ~290 ≈ 0.8k | small; endpoints/client |
| Messaging | ~250 / ~460 / ~600 / ~120 ≈ 1.4k | Desktop/Mobile pair; compose modal; nav-link chrome |
| Groups | ~430 / ~730 / ~1050 / ~180 ≈ 2.4k | Desktop/Mobile pair; folder-tree render ×2; audience-badge ×3 (MA-509); 12 write methods |

**(b) Compression candidates**

| Candidate | LOC saved | Sites | New machinery | Class |
|---|---|---|---|---|
| Audience-badge statics → shared (MA-509) | ~28 | 3→1 | one static helper | **pure win** |
| Comment read 4× → generic `PageCommentsAsync<T>` (MA-510) | ~120 | 4→1+4 thin | generic over BaseComment children | **trade** |
| `CardShadowClass` dead conditional (MA-511) | ~2 | 1 | none | pure win |
| EndpointHelpers 401→400 for business rules (MA-505) | ~0 (correctness) | shared helper | typed exceptions | trade |

**(c) Near-identical pairs — Desktop/Mobile measured against "separate only when structurally different"**
- **MessagesDesktop (89) vs MessagesMobile (103): JUSTIFIED SPLIT.** Markup genuinely differs — Desktop is a
  persistent two-pane (`aside` sidebar + `section` thread, both always visible); Mobile is a conditional
  single-pane (`@if (Thread is null)` list-view else thread-view-with-back-button). Only the 32-line `@code`
  param block is verbatim-shared — the unavoidable tax of the injection-free-layout-pair contract (both must
  accept the same params the dispatcher passes). Merging would reintroduce a device `@if` inside one file —
  the anti-pattern the split exists to avoid. **False economy to merge.**
- **GroupDesktop (264) vs GroupMobile (206): JUSTIFIED SPLIT.** Desktop = two-column grid with a sidebar
  folder tree + creator attribution + folder MaxRating badges; Mobile = single column, collapsible folders,
  no MaxRating, no creator line. Genuine structural divergence. Extractable duplication is narrow: the
  audience-badge statics (MA-509) and `HandleAddStoryAsync`/`ToggleAddStory` (~8 LOC). `RenderFolders`
  differs (Desktop shows MaxRating) — not a clean extraction. **False economy to merge; extract only the
  badge statics.**

**(d) Mechanical repetition w/ fixable root cause**
- Comment read 4× duplication (MA-510) — root cause: per-context method pattern applied to the read side
  where its write-side verification justification doesn't hold.
- Client service impls mechanically uniform (per calibration WU-L5Sweep) — intended, not a defect.

**(e) False economies considered & rejected**
- Merging Desktop/Mobile pairs (both) — rejected: genuine structural difference (above).
- Collapsing the 4 per-context comment **write** methods (post/verify) — rejected: **settled** design; each
  differs in its existence/parent-cross-check verification (`layer2-services.md` §"Group Comments — Per-Context
  Method Pattern"). Only the *read* side (no verification) is a candidate (MA-510).
- Extracting a 3rd `EditorForm` shell from CommentEditor/RecommendationEditor/MessageComposer — the settled
  "defer until a real 3rd consumer clarifies the shared part" (WU29 note) now has 3 consumers, but the
  differences (spoiler toggle, char-meter, send-label) are real; premature abstraction risk. Noted, not filed.
