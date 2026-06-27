# Audit — Recommendations/

**Features:** 27 (submission), 28 (display), 29 (Hidden Gem), 30 (attribution). Framing: "recommendation,"
never "review." Recommendations **cannot** have spoilers — deliberate absence of `IsSpoiler` (§5.6).

## Shared Context
**Entities (Core/Recommendations/ after WU29):** `Recommendation` (hot — `StoryId`, `RecommenderId`,
`StatusId`, `LikeCount`), `RecommendationDetail` (cold — text body, 1-to-1 cascade, PK=FK),
`RecommendationStatus` (seeded: Pending/Approved/Rejected/Under Review), `RecommendationLike`
(junction, minted WU29), `RecommendationSuccess` (PK `(UserId,RecommendationId)`),
`UserStoryRecommendationSource` (sparse partition off `UserStoryInteraction`). Migrated out of
`Core/Models/` just-in-time (WU29) per the vertical-cluster rule. Services, DTOs, and components
fully built in WU29 (L2/L3/L3.5/L4 across all four features).

## Feature 27 — Recommendation Submission
- **L1 — Stage 5.** Hot/cold vertical partition + status lifecycle; no `IsSpoiler` (correct).
  Unique `(RecommenderId, StoryId)` index added by WU29 migration; NULL `RecommenderId` (anonymized
  recs) are each distinct under Postgres NULL semantics — correct.
- **L2 — Stage 5 (WU29, 2026-06-23).** `SubmitAsync` enforces: (a) `RequireAuthenticatedUser()`;
  (b) **minimum 500 characters** on stripped text (strip helper mirrors `ChapterText.CountWords`,
  `RecommendationConstants.MinLength`; value settled WU29 — no prior spec value, see
  `forward_plan.md` Resolved); (c) sanitize-once-on-save; (d) writes hot+cold partition via `Add`
  (not `Attach`, heed WU12 lesson); (e) **auto-approve (MVP):** `StatusId = Approved` on create —
  spec §5.6's Pending→author-approval/moderation lifecycle deferred to WU34, recorded in
  `forward_plan.md` Resolved, code is authoritative. `EditAsync`/`DeleteAsync` author-only.
  One-per-user enforced by the DB unique index (duplicate → friendly `RecommendationValidationException`).
- **L3/L3.5/L4 — Stage 5 (WU29, 2026-06-23).** `RecommendationEditor` leaf: wraps `EditorView`
  (pull-on-submit `@ref`/`GetHtmlAsync()`), Save/Cancel/`Busy` shell, live 500-char meter, no spoiler
  checkbox (deliberate absence). Own leaf, not a shared `EditorForm` abstraction (only two
  rich-text editor shells exist; defer abstraction until a 3rd — BlogPosts/Messaging/Profiles —
  clarifies the shared part; WU9 ConfirmDialog precedent). Covering tier: RazorComponents.
- **L4 — Stage 5 (WU29, visual sign-off 2026-06-23).**
- **L5 — Stage 5 (2026-06-24).** `RecommendationWriteServiceTests` (Integration tier): `SubmitAsync`
  creates row with correct fields; one-per-user unique-index guard (duplicate → `RecommendationValidationException`);
  `EditAsync` author-only; `DeleteAsync` author-only, cascades likes; `ToggleLikeAsync` increments/
  decrements `LikeCount` + creates/removes `RecommendationLike` row. 190/190 green twice.
  Enabled by Respawn isolation overhaul (2026-06-24).
- **L6 — Stage 5 (WU29, 2026-06-23).** Unique index `ix_recommendations_recommender_id_story_id` on
  `(recommender_id, story_id) WHERE recommender_id IS NOT NULL` in `RecommendationLikesAndConstraints`
  migration. Verified via integration test: duplicate submit raises `RecommendationValidationException`.

## Feature 28 — Recommendation Display
- **L1 — Stage 5 (reconciled WU29, 2026-06-23).** Pre-WU29 gap: `RecommendationLike` entity/table
  didn't exist; `LikeCount` column missing from `Recommendation`. Both added by WU29 migration
  (`RecommendationLikesAndConstraints`). Now fully stage 5.
- **L2 — Stage 5 (WU29, 2026-06-23).** `GetForStoryAsync`: Approved only; highlighted/spotlighted
  first then DatePosted desc; per-viewer `IsLikedByCurrentUser` via short-circuited EXISTS subquery
  (EF Core anonymous-safe pattern). `ToggleLikeAsync`: load rec with filtered `Likes` include,
  add/remove, atomic counter update, return `RecommendationLikeResultDto`. **No notification
  on like** (anti-addictive design — same as `CommentLike`, §6.11). Author-highlight `≤5/story`
  enforced in `SetHighlightedByAuthorAsync` (story-author-only).
  **WU-CounterAtomicity Stage note (2026-06-27):** `ToggleLikeAsync` previously used tracked
  read-modify-write (`rec.LikeCount++` / `Math.Max(0, ... - 1)`) — the lone deviation from the
  codebase's atomic-counter pattern. Replaced with `ExecuteUpdateAsync(SetProperty(r => r.LikeCount,
  r => r.LikeCount + delta))` after the join-row `SaveChangesAsync`. Returned DTO value unchanged
  (optimistic `loaded + delta`). Concurrency fix not automatable (no parallel-request seam); covered by
  existing sequential `ToggleLikeAsync` integration tests confirming correct counter behavior + code review
  that the SQL is now `SET like_count = like_count + delta`. Convention documented in
  `cross-cutting.md §"Counter mutation rule"`. `dotnet test` 1232/1232 pass.
- **L3/L3.5 — Stage 5 (WU29, 2026-06-23).** `RecommendationCard` leaf: `UserCard` (attribution
  variant, §5.30.7 #2) + `RichTextView` (body) + like button + successful-rec count. Two visual
  states: **Author-spotlighted** (accent border/glow + "Author's Pick" ribbon, Roserade Green or
  Arceus Gold — confirmed at visual sign-off) + **Hidden Gem** (gem badge, Torterra Emerald
  `#1FA37A`). Both states can coexist. Icons are inline SVG (same WU7 pattern); constants in
  `SharedUI/Recommendations/RecommendationVisuals.cs`. `RecommendationSection` coordination
  composite: injects `IRecommendationWriteService` (read+write, sanctioned coordinated-region
  exception), spotlight ordering, submission composer (gated on auth + no existing rec), optimistic
  like with rollback (`CommentSection.HandleLike` pattern), Hidden-Gem toggle, `ConfirmDialog` delete.
  This is the surface WU25 embeds. Covering tier: RazorComponents.
- **L4 — Stage 5 (WU29, visual sign-off 2026-06-23).**
- **L5 — Stage 5 (2026-06-24).** `RecommendationReadServiceTests` (Integration): `GetForStoryAsync`
  returns Approved-only recs with correct projection (spotlighted first, per-viewer `IsLikedByCurrentUser`).
  `RecommendationWriteServiceTests`: `ToggleLikeAsync` like/unlike updates `LikeCount` + `RecommendationLike` row.
  `BookshelfStoryIdsTests`: approved recs visible in bookshelf; pending recs excluded. 190/190 green twice.

## Feature 29 — Hidden Gem Management
- **L1 — Stage 5** (`IsHiddenGem`). **L2 — Stage 5 (WU29, 2026-06-23).** 5-per-user limit in C#:
- **L3-Logic — Stage 5 (WU29, 2026-06-23; reconciled Phase B, 2026-06-20; was Stage 1).** Spec §8
  Open Question #4 resolved: **reject-at-5.** `SetHiddenGemAsync(recId, true)` counts current Hidden
  Gems against `writeDb` (Case 1 — constraint check, spec §6.6 write-side-reads table); fails when
  `count == 5` with a `RecommendationValidationException`. **Settled constraint — do not revisit.**
  On successful designation: best-effort post-commit `NotifyStoryHiddenGemAsync(storyAuthorId,
  recommenderId)` in try/catch (WU22 seam pattern). `NotifyStoryHiddenGemAsync` added to
  `INotificationWriteService` / `ServerNotificationWriteService` in WU29.
- **L3.5/L4 — Stage 5 (WU29, 2026-06-23).** Inline Hidden-Gem toggle on `RecommendationCard`
  (recommender-only via `OnToggleHiddenGem.HasDelegate`). Covering tier: RazorComponents.
- **L5 — Stage 5 (2026-06-24).** `RecommendationWriteServiceTests` (Integration): `SetHiddenGemAsync`
  sets `IsHiddenGem=true`; emits `HiddenGem` notification; rejects at 5 (`InvalidOperationException`
  citing the limit) — mutation-tested (disabled guard → test fails; re-enabled → passes).
  `SetHighlightedByAuthorAsync` spotlight ≤5 limit enforced. 190/190 green twice.

### WU-ComponentSoundness Stage note (2026-06-27)

**Cell affected:** F28 L3.5-Structure (RecommendationSection) — hygiene fix, no data-corruption risk;
no stage transition.

`RecommendationSection.razor` now carries `@key="rec.RecommendationId"` on `<RecommendationCard>` in
the `@foreach` loop.

The recommendation list has no per-card ephemeral state (pure-display leaf), so positional reuse does not
cause data corruption here. The key was added because the highlight re-sort occasionally reorders the list
(spotlighted recommendations float to the top); without `@key`, reordered slots receive new `[Parameter]`
values but Blazor may diff the DOM less efficiently. The convention drawn from the two buggy cases
(`StoryDeck F2`, `CommentSection F3`) is: key any `@foreach` over components that could be reordered or
whose identity matters for correct diffing. Pure-display leaves that only receive `[Parameter]` values are
self-healing (no cache guard on private state), so the key is a hygiene guard rather than a bug fix here.

Covering tier: **no automated test** — `RecommendationCard` is a pure-display leaf; all observable
behavior is exercised by existing `RecommendationSectionTests`.

---

## Feature 30 — Recommendation Attribution
- **L1 — Stage 5** (`UserStoryRecommendationSource` sparse; `RecommendationSuccess`). **L2 — Stage 5
  (WU29, 2026-06-23 — surface minted; trigger deferred to WU26).** `RecordAttributionSourceAsync`
  writes `UserStoryRecommendationSource`; `RecordSuccessAsync` writes `RecommendationSuccess`
  (idempotent on composite PK) + `SuccessfulRecCount++`. **Trigger wiring deferred to WU26:**
  the after-Ch.1-`IsRead` trigger lives in the chapter reading page (WU26 must call these methods
  after the user passes 90% of Ch.1); the surface is minted and callable now. Mirrors WU5's cascade-
  provider deferral to its first consumer.
- **L3-Logic — Stage 5 (WU29, 2026-06-23).** `RecommendationHelpfulPrompt` leaf: **inline,
  non-blocking, dismissible banner** — NOT a `ConfirmDialog` overlay (must not interrupt the reading
  experience). Renders at the bottom of Ch.1 content; gating (show only when
  `UserStoryRecommendationSource` exists for viewer+story, Ch.1 `IsRead` true, no existing
  `RecommendationSuccess`) owned by WU26's reading page. Takes `recommendationId`; raises
  `OnRespond(bool helpful)` / `OnDismiss`. Spec §5.6 wording "helpful" is canonical (not "useful").
- **L3.5/L4 — Stage 5 (WU29, 2026-06-23).** Covering tier: RazorComponents (dismiss + respond events).
- **L5 — Stage 2.** Attribution trigger wired in WU26 (chapter reading page, after 90% of Ch.1 `IsRead`).
  Integration test pending until WU26 is done. Service methods callable now; not yet exercised from UI.
