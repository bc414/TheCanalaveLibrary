# Audit — Recommendations/

**Features:** 27 (submission), 28 (display), 29 (Hidden Gem), 30 (attribution). Framing: "recommendation,"
never "review." Recommendations **cannot** have spoilers — deliberate absence of `IsSpoiler` (§5.6).

## Shared Context
**Entities (Core/Recommendations/ after WU29):** `Recommendation` (hot — `StoryId`, `RecommenderId`,
`StatusId`, `LikeCount`, `RevisionRequestNote` after WU-RecLifecycle), `RecommendationDetail` (cold —
text body, 1-to-1 cascade, PK=FK), `RecommendationStatus` (seeded — WU-RecLifecycle 2026-07-25:
NeedsRevision/Approved/Rejected; the WU29-era Pending Approval and Under Review rows were removed,
never written by production code), `RecommendationLike` (junction, minted WU29),
`RecommendationSuccess` (PK `(UserId,RecommendationId)`), `UserStoryRecommendationSource` (sparse
partition off `UserStoryInteraction`). Migrated out of `Core/Models/` just-in-time (WU29) per the
vertical-cluster rule. Services, DTOs, and components fully built in WU29 (L2/L3/L3.5/L4 across all
four features).

### WU-RecLifecycle settled design (2026-07-25) — the recommendation lifecycle

**Spec §5.6 divergence, recorded per the spec-relationship rule.** The spec says recs are
"auto-approved after author approval OR moderator review." Two corrections, both settled with the
owner (2026-07-24/25): (1) "moderator review" was a spec mis-rewording — the source deliberation
(`GeminiDiscussions/MyActivity September to November 2025_filtered.md` ~17260) specified an
*author*-approval workflow with time-based auto-approval; there is **no moderator approval gate**
(mods act only reactively via the WU34 report→takedown path). (2) The pre-publication gate itself
(and its 7-day auto-approve timer) was **rejected outright** on first-principles review:
recommendations are a *discovery* feature, not feedback; a gate delays discovery, dead-weights
inactive authors, and merges two distinct author intents (fix an earnest flaw vs. remove a troll)
into one harsh mechanism. What shipped instead:

**Model: Publish + Request-Revision + Remove.** Statuses: `Approved` (live, the submit default) /
`NeedsRevision` / `Rejected`.
- **Live on submit** — no pending state, no timer, no worker.
- **Request Revision** (author, story-ownership-gated): requires a note (plain text, stored on hot
  `Recommendation.RevisionRequestNote` beside the same-shaped `TakedownReason`); rec hidden publicly;
  recommender notified (`RecommendationRevisionRequested`, deep-links to the story). The
  recommender's **edit auto-returns it to Live** (note cleared, author notified via
  `RecommendationRevised`). Not sticky.
- **Remove** (author): from Live or NeedsRevision → `Rejected`. Silent, hidden, **sticky** — the
  recommender cannot edit/delete/resubmit (the `(RecommenderId, StoryId)` unique index + the
  persisted Rejected row are the block record). Only the author's **Unblock** reverses it (straight
  to Live; `RecommendationApproved` fires — its only trigger).
- **Flag invariant:** `IsHiddenGem`/`IsHighlightedByAuthor` are only ever true on Live recs — both
  Request Revision and Remove clear both flags (slots freed); return to Live does NOT restore them;
  both setters refuse on non-Approved.
- **Self-recommendation blocked** at submit (peer endorsement by definition); SeedTool mirrors the
  invariant. Authorless-story recs simply go Live (nothing to gate).
- **Submit now notifies the story author** (`NewRecommendationOnYourStory` — type existed since
  WU22-era seeding but production never sent it).
- **Recommender status surfaces:** Bookshelves Recommendations tab "Needs attention" section
  (rec-level rows with note) + own-rec status display in `RecommendationSection`. The *public*
  profile tab stays Approved-only for everyone including the owner.
- **Deliberate asymmetry with comments:** comment removal is hard-delete with no uniqueness
  constraint (a removed commenter can re-post); a Rejected rec's slot stays occupied. Intentional.
- Conventions detail: `layer2-services.md` §"Publish-immediately + the Recommendation Lifecycle";
  actor-class framing: `content-safety.md` §"Author-Controlled Content Actions".

**WU-RecLifecycle Stage note (2026-07-25) — built.** Migration `RecLifecycle`: status seed rows
(1→"Needs Revision", 3 reworded, 4 deleted), `recommendations.revision_request_note`
(varchar 500, hot table beside `TakedownReason`), notification-type seeds 27
(`RecommendationRevised` → author, YourStories) + 43 (`RecommendationRevisionRequested` →
recommender, YourRecommendations). Server: `RequestRevisionAsync`/`RemoveAsync`/`UnblockAsync` on
`IRecommendationWriteService` (story-ownership via the `SetHighlightedByAuthorAsync` pattern);
`SubmitAsync` gains the self-rec guard + wires `NewRecommendationOnYourStory` (type 22's first
production sender); `EditAsync` auto-relives NeedsRevision (note cleared, author notified via
`RecommendationRevised`) and refuses on Rejected; `DeleteAsync` refuses on Rejected; both curation
setters refuse on non-Approved; `RecordAttributionSourceAsync` validates rec↔story ownership
(**D3.2 closed**); `GetRecommendedStoryIdsByUserAsync` gains the Approved filter (**D1 closed** —
regression-tested for the first time); `GetForStoryAsync` is per-viewer (public=Approved;
story author +NeedsRevision/Rejected; recommender +own hidden rec with note); new
`GetMyRecommendationsNeedingAttentionAsync`. The three `ApprovedStatusId = 2` magic-number consts
now derive from `RecommendationStatusEnum`; SeedTool's `AddRecommendation` skips self-recs
(`MarkGem` null-guarded accordingly). L5: endpoints (3 authorized POSTs + needing-attention GET) +
client impls + `ClientNotificationWriteService` stubs. UI: `RecommendationCard` status
strip/note/dashed border + three author-action callbacks; `RecommendationSection` wires actions by
status+ownership, inline revision-note panel (ModSubmissionsPage reject-panel pattern), Remove
`ConfirmDialog`, direct Unblock; Bookshelves Recommendations tab "Needs attention" section
(rec-level rows, titles via `GetListingsByIdsAsync`). **Verified:** `dotnet build` green;
`dotnet test` green — covering tiers: Integration (`RecommendationWriteServiceTests` +15
lifecycle/self-rec/flag-invariant/stickiness/notification tests; `RecommendationReadServiceTests`
+6 per-viewer visibility + D1 regression + needing-attention) and RazorComponents
(`RecommendationSectionTests` +5 author actions/note display; fakes extended). Spotlight needed no
changes (`GetByIdAsync` null = blank-rec display state, already documented there).

**L4.5-Browser verification (2026-07-25) — F27/F28/F30 stay Stage 5.** Full lifecycle driven against
the dev DB (server-only path), every step `psql`-confirmed. As AuthorAlpha on story 1 with TestUser's
seeded Author's-Pick rec: **Request Revision** → card shows "Revision requested — hidden from readers
until it's edited" + the note, `status_id 2→1`, note stored, **`is_highlighted_by_author t→f`** (the
flag invariant, observed live — the AUTHOR'S PICK ribbon and the Spotlight toggle both vanished),
notification 43 → TestUser. As TestUser: the note renders on the story card *and* in the Bookshelves
Recommendations "NEEDS ATTENTION" section; **edit → auto-relive** (`status 1→2`, note cleared,
notification 27 → AuthorAlpha, "Mark as Hidden Gem" reappears; flags correctly NOT auto-restored).
**Remove** → "Removed by the story author", `status→3`, **silent** (no notification row), author left
with Unblock only; stickiness enforced *server-side*, not just by affordance — direct API calls as
TestUser returned **edit 403, delete 403, resubmit 401** ("You have already submitted a
recommendation for this story"). **Unblock** → `status→2`, notification 40 → TestUser. **Self-rec
blocked**: `POST /api/recommendations` for own story → **400 "You cannot recommend your own story."**
A fresh rec from ReaderGamma published **immediately** (visible to a third party, rec count 1→2) and
fired notification 22 to AuthorAlpha — type 22's first production send. **D1 confirmed end-to-end**:
while TestUser's only rec was Rejected, ReaderGamma's view of `/user/1/recommendations` showed "No
recommendations given yet."
**Two runtime defects found and fixed in-session** (per CLAUDE.md's fix-same-session rule): (1) the
`GetListingsAsync` empty-restrict bug — see `audit/Stories.md` Feature 5 WU-RecLifecycle note; (2)
the "Recommend this story" composer CTA was offered to the story's own author, an affordance that
could only fail — now gated on `CurrentUserId != StoryAuthorId`
(`RecommendationSectionTests.RecommendationSection_StoryAuthor_SeesNoRecommendCTAOnOwnStory`).
Verification data cleaned up afterward (throwaway rec + comment removed, rec 1's seed body/flag and
the four generated notifications restored/deleted — workbench left at seed state).

## Feature 27 — Recommendation Submission

**WU-ModerationIntegrity Stage note (2026-09-30) — no flip:** the shared `ActiveUser.RequireUserId()`
guard (`Core/Identity/ActiveUserContextExtensions.cs`) replaces this service's private
`RequireAuthenticatedUser` copy — owner ruling D9's "one shared guard", WU-ModerationIntegrity
2026-09-30. Same `InvalidOperationException` → 401, so no behavior change; the existing Integration
tests for the anonymous refusal stay green. The per-action messages ("Submitting a recommendation
requires an authenticated user.") became the shared text; no test asserted them, and
`RequireStoryAuthorAsync` lost its now-unused `action` parameter.

- **L1 — Stage 5.** Hot/cold vertical partition + status lifecycle; no `IsSpoiler` (correct).
  Unique `(RecommenderId, StoryId)` index added by WU29 migration; NULL `RecommenderId` (anonymized
  recs) are each distinct under Postgres NULL semantics — correct.
- **L2 — Stage 5 (WU29, 2026-06-23; shared auth guard WU-ModerationIntegrity, 2026-09-30).** `SubmitAsync` enforces: (a) the authenticated-user guard (the shared `RequireUserId()` since WU-ModerationIntegrity);
  (b) **minimum 500 characters** on stripped text (strip helper mirrors `ChapterText.CountWords`,
  `RecommendationConstants.MinLength`; value settled WU29 — no prior spec value, see
  `forward_plan.md` Resolved); (c) sanitize-once-on-save; (d) writes hot+cold partition via `Add`
  (not `Attach`, heed WU12 lesson); (e) **publish-immediately:** `StatusId = Approved` on create —
  originally the WU29 auto-approve MVP shortcut ("deferred to WU34"), ratified as the permanent
  design by WU-RecLifecycle (2026-07-25; see the settled-design section above — the gate was
  rejected, not merely deferred). WU-RecLifecycle also added the self-rec guard and the
  submit-time author notification. `EditAsync`/`DeleteAsync` author-only (and refused on Rejected
  recs — sticky). One-per-user enforced by the DB unique index (duplicate → friendly
  `RecommendationValidationException`).
- **L3/L3.5/L4 — Stage 5 (WU29, 2026-06-23).** `RecommendationEditor` leaf: wraps `EditorView`
  (pull-on-submit `@ref`/`GetHtmlAsync()`), Save/Cancel/`Busy` shell, live 500-char meter, no spoiler
  checkbox (deliberate absence). Own leaf, not a shared `EditorForm` abstraction (only two
  rich-text editor shells exist; defer abstraction until a 3rd — BlogPosts/Messaging/Profiles —
  clarifies the shared part; WU9 ConfirmDialog precedent). Covering tier: RazorComponents.
- **L4 — Stage 5 (WU29, visual sign-off 2026-06-23).**
- **L5 — Stage 2 (corrected 2026-07-12 — was mismarked Stage 5).** The Stage-5 mark below described
  `RecommendationWriteServiceTests` (Integration tier, service-layer soundness only) — no
  endpoint/client impl ever existed. Per `layer5-wasm.md` §"L5 Stage Semantics", L5 Stage 5 means
  the HTTP body-swap (endpoints + client impl) exists and compiles; service-only soundness is
  Stage 2, same as every other not-yet-built L5 cell. Prior text, retained as the L2/L3 test
  record: `RecommendationWriteServiceTests` (Integration tier): `SubmitAsync` creates row with
  correct fields; one-per-user unique-index guard (duplicate → `RecommendationValidationException`);
  `EditAsync` author-only; `DeleteAsync` author-only, cascades likes; `ToggleLikeAsync` increments/
  decrements `LikeCount` + creates/removes `RecommendationLike` row. 190/190 green twice.
  Enabled by Respawn isolation overhaul (2026-06-24).
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13; supersedes the 2026-07-12 correction above — the gap
  it named is now filled).** Endpoints + client impl live (WU-L5Sweep) and the site now runs global
  InteractiveAuto; the recommendations section rendered under WASM on the story page during the
  flip's browser wave (submission writes not driven). Full wave narrative + the 7 bugs found/fixed:
  `workplan.md` WU-GlobalFlip.
- **L6 — Stage 5 (WU29, 2026-06-23).** Unique index `ix_recommendations_recommender_id_story_id` on
  `(recommender_id, story_id) WHERE recommender_id IS NOT NULL` in `RecommendationLikesAndConstraints`
  migration. Verified via integration test: duplicate submit raises `RecommendationValidationException`.

## Feature 28 — Recommendation Display
- **L1 — Stage 5 (reconciled WU29, 2026-06-23).** Pre-WU29 gap: `RecommendationLike` entity/table
  didn't exist; `LikeCount` column missing from `Recommendation`. Both added by WU29 migration
  (`RecommendationLikesAndConstraints`). Now fully stage 5.
- **L2 — Stage 5 (WU29, 2026-06-23; an unlike is a clear since WU-AccessGateSweep2, 2026-09-30, disclosing no count under a hidden story since its review fixes — see its Stage note).** `GetForStoryAsync`: Approved only; highlighted/spotlighted
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
  `layer2-services.md §"Counter mutation rule"`. `dotnet test` 1232/1232 pass.
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
- **L5 — Stage 2 (corrected 2026-07-12 — was mismarked Stage 5; see F27's L5 note for the general
  correction).** Prior text, retained as the L2/L3 test record: `RecommendationReadServiceTests`
  (Integration): `GetForStoryAsync` returns Approved-only recs with correct projection (spotlighted
  first, per-viewer `IsLikedByCurrentUser`). `RecommendationWriteServiceTests`: `ToggleLikeAsync`
  like/unlike updates `LikeCount` + `RecommendationLike` row. `BookshelfStoryIdsTests`: approved
  recs visible in bookshelf; pending recs excluded. 190/190 green twice.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13; supersedes the 2026-07-12 correction above).**
  Endpoints + client impl live (WU-L5Sweep) and the site now runs global InteractiveAuto;
  recommendations display verified in a real WASM runtime during the flip's browser wave (section
  rendered 4 recs on the story page). Full wave narrative + the 7 bugs found/fixed: `workplan.md`
  WU-GlobalFlip.

### Feature 28 L2 — WU-AccessGateSweep2 Stage note (2026-09-30): an unlike is a clear (owner ruling D6)

**No cell flips — F28 stays Stage 5.** `ToggleLikeAsync` calls `RequireRecommendationVisibleAsync`
only when the caller holds no like row, so a reader can unlike a recommendation whose story was later
taken down, unpublished or put above their ceiling. *Review fixes (same day):* the build's response
still carried the rec's post-toggle `LikeCount` (a flagged WU derivation that contradicted D6's
premise); a clear's response is a read and stays gated, so an unlike under a hidden story now returns
`(0, false)` — the counter still moves. The "Counter mutation rule"
cite now points at `layer2-services.md`. Recorded as D6 conformance, unchanged:
`SetHiddenGemAsync(false)` and `SetHighlightedByAuthorAsync(false)` are unguarded clears. Their
`true` raises carry no story guard either; which guard should apply is owner-open (tracker **F10**
item 2), so they were left alone.

**How verified:** Integration — `ParentVisibilityContractTests`: unlike succeeds on a taken-down
story (fails against the pre-fix service) and returns `(0, false)` while the stored count goes 3 → 2
(fails against the build's version, which returned 2); both curation-flag clears succeed on a
taken-down story (conformance). `dotnet build` green, no new warnings in touched files; `dotnet test`
green — Unit 1,022, RazorComponents 703, Integration 1,180 (after the review fixes); all four
PowerShell gates pass.

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
- **L5 — Stage 2 (corrected 2026-07-12 — was mismarked Stage 5; see F27's L5 note for the general
  correction).** Prior text, retained as the L2/L3 test record: `RecommendationWriteServiceTests`
  (Integration): `SetHiddenGemAsync` sets `IsHiddenGem=true`; emits `HiddenGem` notification;
  rejects at 5 (`InvalidOperationException` citing the limit) — mutation-tested (disabled guard →
  test fails; re-enabled → passes). `SetHighlightedByAuthorAsync` spotlight ≤5 limit enforced.
  190/190 green twice.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13; supersedes the 2026-07-12 correction above).**
  Endpoints + client impl live (WU-L5Sweep) and the site now runs global InteractiveAuto (Hidden-Gem
  toggle writes not driven in the flip's browser wave; the recommendations section rendered under
  WASM). Full wave narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.

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

**Settled — owner ruling D3 (answered 2026-08-04, consumed WU-InertFeatures 2026-09-30; do not
revisit).** Rule text: `layer2-services.md` §"Attribution (Feature 30)".
- **The attribution is metadata on the `IsReadItLater` bit** — how that bit came to be set (RIL on the
  rec card → store the source; RIL elsewhere → nothing). Not an event log.
- **The service audit's three-way fork is void — do not decouple.** Keep the composite FK to the USI
  PK and both cascades. The FK failure (service audit §2.3.3) came from a placeholder caller (the
  `?rec=`-on-load write in `ChapterReadingPage`); the specified 2025 producer — a Read It Later button
  on the recommendation card — had never been built. Cascade on the rec FK is the faithful translation
  of the 2025 intent (`SET NULL` → SQL-Server `NO ACTION` workaround); **RESTRICT is rejected**.
- **Two entry points, both built:** RIL-from-card (one unit of work: USI upsert + sources row) and the
  direct `?rec=` link, persisted at the Ch.1 ≥90% `MarkStartedAsync` moment — never on page load.
- **Five removal triggers:** RIL true→false; USI row gone (cascade); prompt answered (either control);
  rec deleted (cascade); rec author-`Rejected` or taken down (service sweep — not restored by
  unblock/reversal; `NeedsRevision` is not a trigger).
- **Prompt:** a DTO (the rec as reminder), four read-time gates (sources row, rec visible,
  `RecommenderId` non-null, no success yet), two controls (Yes / X — "No thanks" deleted).
- **Write gates:** no attribution for the story's author (the RIL itself is allowed); anonymous card
  click → login nudge; first attribution wins; `RecordSuccessAsync` requires and consumes the
  sources row (service audit §2.4.1). *Derived (review fixes, 2026-09-30), not owner text:* no
  attribution for a rec the caller already credited (the prompt's fourth gate would hide it forever),
  and the `?rec=` carrier is consumed once — the reading page drops it from the address after
  `MarkStartedAsync`, so X's deletion cannot be undone by a reload.
- **Unruled, status quo:** a recommender attributing their *own* rec (roadmap row 18).

**Stages (updated 2026-09-30, WU-InertFeatures, its review fixes and its browser pass):** L1, L2, L3-Logic, L3.5, L4,
L4.5, L5 = 5 (the feature was rebuilt beneath them — see the WU-InertFeatures Stage note and its review-fixes note at the end
of this feature). **L4.5** went 5→1 when the WU shipped UI with no browser available, and back to 5 when its browser pass
drove every entry point on both render phases (tracker **H14** closed — browser-verification note at the end of this
feature). L6/L8 = N/A. Tracker **B22** closed.

- **L1 — Stage 5** (`UserStoryRecommendationSource` sparse; `RecommendationSuccess`). **L2 — Stage 5
  (WU29, 2026-06-23 — surface minted; trigger deferred to WU26; **rebuilt WU-InertFeatures 2026-09-30 —
  the paragraph below is history: the on-load attribution write it describes was retired, see the
  Stage note at the end of this feature**).** `RecordAttributionSourceAsync`
  writes `UserStoryRecommendationSource`; `RecordSuccessAsync` writes `RecommendationSuccess`
  (idempotent on composite PK) + `SuccessfulRecCount++`. **Trigger wiring deferred to WU26:**
  the after-Ch.1-`IsRead` trigger lives in the chapter reading page (WU26 must call these methods
  after the user passes 90% of Ch.1); the surface is minted and callable now. Mirrors WU5's cascade-
  provider deferral to its first consumer.
- **L3-Logic — Stage 5 (WU29, 2026-06-23; **rebuilt WU-InertFeatures 2026-09-30 — takes the
  `RecommendationDto` as a reminder card, Yes/X only; the description below is history**).**
  `RecommendationHelpfulPrompt` leaf: **inline,
  non-blocking, dismissible banner** — NOT a `ConfirmDialog` overlay (must not interrupt the reading
  experience). Renders at the bottom of Ch.1 content; gating (show only when
  `UserStoryRecommendationSource` exists for viewer+story, Ch.1 `IsRead` true, no existing
  `RecommendationSuccess`) owned by WU26's reading page. Takes `recommendationId`; raises
  `OnRespond(bool helpful)` / `OnDismiss`. Spec §5.6 wording "helpful" is canonical (not "useful").
- **L3.5/L4 — Stage 5 (WU29, 2026-06-23).** Covering tier: RazorComponents (dismiss + respond events).
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto (attribution writes not driven in the flip's browser wave —
  trigger lives in the chapter reading page, WU26, which was verified under WASM). Full wave
  narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.

### Feature 30 — WU-InertFeatures Stage note (2026-09-30): attribution rebuilt around the RIL-from-card producer (owner ruling D3; tracker B22 closed)

**What was wrong.** The feature never worked: the only attribution write ran on chapter **load** from
`?rec=`, before any interaction row could exist, so it FK-failed (logged and swallowed) for every new
reader; nothing in the app generated a `?rec=` link anyway; the specified Read It Later button on the
card had never been built; and `RecordSuccessAsync` credited `SuccessfulRecCount` and the recommender
badge to any signed-in reader for any visible rec (service audit §2.3.3, §2.4.1).

**What changed.** L1: the phantom `Recommendation.UserStoryInteractions` nav and its shadow
`user_story_interactions.recommendation_id` column are gone (migration `WU_InertFeatures`); the
partition's rec FK is explicit (cascade, rationale in the configuration's doc comment). L2: the two
producers live on the USI write service (F16's Stage note); `RecordAttributionSourceAsync` and its
endpoint are **retired** (the FK-order hazard stays unreachable — registered in
`check-doc-hygiene.ps1`); `GetHelpfulPromptRecommendationIdAsync` became `GetHelpfulPromptAsync` →
`RecommendationDto?` with the four read-time gates; new `DismissHelpfulPromptAsync` (X — a clear, no
guard, idempotent; `POST /api/recommendations/{id}/helpful-prompt/dismiss`); `RecordSuccessAsync`
requires the caller's attribution for that rec on an `Approved`, not-taken-down rec and consumes it in
the same save (an already-recorded success still clears a lingering row); `RemoveAsync` and the
moderator takedown path sweep every attribution naming the rec (trigger 5). L3/L3.5/L4:
`RecommendationHelpfulPrompt` takes the `RecommendationDto`, renders it as a read-only reminder card,
and has exactly Yes (thumbs up — new `RecommendationIcons.HelpfulIconPath`) and X; "No thanks" is
deleted. `RecommendationCard` gains `OnReadItLater` / `IsReadItLaterSaved` / `ShowReadNow` (both only
on `Approved` recs; the saved state renders pressed and inert); new composite
`ReadItLaterRecommendationCard` (login nudge, the call, an `ErrorAlert`) serves Explore, Deep Dive and
the homepage spotlight; `RecommendationSection` wires the card directly and raises
`OnReadItLaterSaved` so the story page re-reads the viewer's state. `ChapterReadingPage` parses
`?rec=` but writes nothing on load; at the Ch.1 ≥90% moment it calls `MarkStartedAsync(storyId, rec)`
**then** fetches the prompt; Yes/X close the widget whatever the outcome, failures logged. L5: client
impls and endpoints mirror every change 1:1. **Left alone (unruled, status quo):** a recommender
attributing their own rec (roadmap row 18); throttling the new write endpoints (WU-ThrottleCoverage).

**How verified:** Integration — `RecommendationWriteServiceTests` (every existing `RecordSuccess_*`
test now seeds the reader's attribution as the producers leave it; new: no attribution → refused with
no credit row and both counters unmoved; an attribution to another rec → refused; success consumes the
row and keeps the RIL; NeedsRevision / Rejected / taken-down → refused; dismiss deletes only the
caller's matching row and is idempotent; `RemoveAsync` sweeps and unblock doesn't restore; rec delete
cascades the attribution and leaves the RIL; the two `RecordAttributionSource_*` tests and their
false-premise comment deleted), `RecommendationReadServiceTests` (+9: the four gates, anonymous, hidden
story, happy path with the recommender card), `ParentVisibilityContractTests` (RecordSuccess on a
taken-down story now seeds a real attribution so the visibility guard is what refuses; the prompt on a
hidden story is null; dismiss on a hidden story is permitted), `ModerationServiceTests` (+1: a
Recommendation takedown sweeps its attributions, RIL kept), and F16's `RecommendationAttributionTests`.
RazorComponents — `RecommendationHelpfulPromptTests` (rewritten: reminder renders body and
recommender; exactly two answer controls; Yes → `OnHelpful`, X → `OnDismiss`, both hide),
`RecommendationCardTests` (+6), `RecommendationSectionTests` (+3: card call, anonymous login nudge,
saved state + Read now link), new `ChapterReadingPageAttributionTests` (5: nothing written or fetched on
load; at 90% `MarkStarted(1, 5)` then the prompt; Yes → success; X → dismiss; anonymous → no rec, no
prompt fetch — `CommentSection` stubbed). Mutation-checked: removing the credit gate, the trigger-5
sweeps (service and moderation) or trigger 1 each fails its tests. **Browser: not run** —
WU-InertFeatures (2026-09-30) ran with no browser available; L4.5 → 1, tracker H14. Totals in the
workplan entry.

### Feature 30 — WU-InertFeatures review fixes (2026-09-30): the dismissed prompt stays dismissed; the card's saved state is the host's

**No cell flips** (L4.5 stays 1 — tracker **H14**, extended).

- **X could be undone by a reload.** X deletes the attribution, but the address still read
  `/story/{id}/1?rec={recId}`. A reload or Back, then 90% of Chapter 1 again, made `MarkStartedAsync`
  mint a fresh row, and the dismissed prompt came back — what D3's X ruling forbids. After a Yes the
  same reload minted a row the prompt's fourth gate hides forever: a dormant row of the kind the author
  gate exists to prevent. **Now:** `ChapterReadingPage` drops `?rec=` from the address once
  `MarkStartedAsync` has used it (`NavigateTo(..., replace: true)`, so the history entry is replaced,
  and on .NET 10 a query-only change keeps the scroll position — checked in `blazor.web.js`). And
  `RecommendationAttribution.IsAttributableAsync` refuses a rec the caller has already credited, on both
  entry points. Following "Read now" again is a new deliberate act and starts a new attribution, as a
  re-RIL after a clear does.
- **The card's "Saved for later" was latched.** `ReadItLaterRecommendationCard` and
  `RecommendationSection` both latched their own success. A Read It Later cleared in the interaction
  panel beside them left the button disabled; in Deep Dive the latch even carried to the next opened
  recommendation. Both now render the host's state only, and the hosts keep it current from the panel's
  new `OnStateSaved` (`audit/UserStoryInteractions.md` F16's review-fixes note). `OnSaved` is
  `[EditorRequired]` on the composite, and Deep Dive/Explore key it on the rec id.
- **How verified:** Integration — `RecommendationAttributionTests.AnAlreadyCreditedRec_IsNotAttributable_OnEitherEntryPoint`
  (direct link and card: HasStarted/RIL land, no attribution) and
  `RecommendationWriteServiceTests.RecordSuccess_AlreadyRecorded_StillDeletesALingeringAttribution_AndCreditsNothing`
  (the already-recorded branch deletes a live row, keeps the RIL and credits nothing. The existing
  idempotency tests reached that branch only after their first call had already consumed the row).
  RazorComponents — new `ReadItLaterRecommendationCardTests` (5: anonymous → login nudge with no call;
  signed-in → call + `OnSaved(storyId)`; refused → inline alert, nothing raised; expired session → sign-in
  link; the saved state follows the host, never latched), `RecommendationSectionTests` (the save test
  now asserts no latch; +2: cleared elsewhere re-enables the button; a refused save shows the refusal and
  raises nothing — the fake's throw hook is now used), `ChapterReadingPageAttributionTests` +2 (the
  carrier is replaced out of the address and a reload after X marks started with no rec; no carrier → no
  navigation). Every fix-specific test fails against its reverted fix (mutation-checked); the nudge and
  error-path tests are coverage the WU owed for behavior it built. **Not browser-driven** — H14.

### Feature 30 L4.5 — WU-InertFeatures browser verification (2026-09-30): attribution driven end to end; L4.5 1→5

**Cells:** L4.5 1→5 (tracker **H14** closed). The pass found no bug, so no code changed.

- **Setup.**
  - Aspire path, because the type-10 email goes through Mailpit. The Aspire DB was wiped first: the
    schema had moved since its last use.
  - The phase was read from the network log. The circuit shows `_blazor/negotiate` and no `/api`
    call; it was forced by removing the Auto-mode localStorage hash before a full load. WASM shows
    `/api` calls and no negotiate.
  - `psql` was checked after every write.
- **Card Read It Later, in all four hosts and on both phases.**
  - Hosts and users: Explore (TestUser on the circuit, ReaderGamma on WASM); the story page's section
    (TestUser, both phases); the homepage spotlight (ReaderGamma on the circuit, TestUser on WASM);
    Deep Dive's gem node (LurkerDelta on the circuit, AuthorBeta on WASM).
  - Each save: `is_read_it_later` true and one `user_story_recommendation_sources` row naming the rec.
    The circuit save on the story page created the interaction row and the sources row in one save,
    with no prior row (the FK-order case).
  - The StoryCard panel beside the card showed Read It Later active, and still active after the
    debounce (the clobber closure). On WASM the spotlight panel showed TestUser's real flags, from the
    batch `by-ids` read.
  - Clearing Read It Later in the panel deleted the sources row (trigger 1). The card offered
    **Read It Later** again, enabled, not latched. A re-save after an X started a new attribution.
- **Prompt.** The seed chapters fit one screen, so Chapter 1 reaches 90% on load.
  - **Yes** (TestUser on the circuit, ReaderGamma on WASM): a `recommendation_successes` row,
    `successful_rec_count` 0→1, the sources row gone. A re-read showed no prompt.
  - **X** (TestUser on both phases): the sources row gone, no success row.
  - The reminder card renders the recommendation, with exactly two answer controls.
- **Direct link** (`?rec=`). As a fixture, story 1's Chapter 1 text was lengthened through `psql`, so
  that 90% needs a scroll. Driven by LurkerDelta (circuit) and AuthorBeta (WASM), neither with an
  interaction row.
  - Nothing was written on load.
  - At 93%: `has_started` set and the sources row written. On WASM the call was
    `POST …/1/started?recommendationId=1`.
  - The address dropped `?rec=`. The scroll position held and `history.length` did not change.
  - X, then a reload and 90% again: no new row and no prompt. Chapter 2, then Back: `/story/1/1`.
  - A bogus `?rec=999`: `has_started` set, no row, no error.
- **Refusals are readable.**
  - Anonymous: the card sends you to `/Account/Login?ReturnUrl=…` (spotlight, Explore and story
    section). Signing in returned to `/discover/user/6`.
  - Stale page: a second session (AuthorBeta, the story author) moved the rec to NeedsRevision behind
    an open story page. The card then showed "That content couldn't be found — it may have been
    removed." inline, and nothing was written. On WASM this is a 404 mapped by the client.
- **Trigger 5.** The author's remove (`POST /api/recommendations/2/remove`, second session) swept
  LurkerDelta's attribution and kept the Read It Later bit. Unblock did not restore the row (D3's
  accepted consequence).
- **Logs.** The flows logged no `fail:`/`crit:`. The one `fail:` (antiforgery, at the first request) was
  a browser cookie from before the wipe. The console showed no errors once tracking started.
- **Not driven** (Integration covers both): the moderator-takedown sweep and the story-author gate.
- GIF: `e2e-WU-InertFeatures.gif` (ReaderGamma, WASM: Explore card Read It Later → prompt → Yes).
  Dev-DB state left behind: the workplan entry.

## L4.5-Browser verification (2026-07-01/02) — F27 + F28 + F29 + F30 → Stage 5

- **F27:** submitted a rec via "Recommend this story" — 500-char minimum gate live (meter counts
  up every 500 ms sample, submit disabled until "minimum met"); card appears with owner
  Edit/Delete affordances. **Tooling note:** the char-meter's PeriodicTimer keeps CDP screenshots
  from settling on this page (captureScreenshot times out while the composer is open) — automation
  must drive this page textually; not a user-facing defect. **Correction (2026-07-02):** cause
  misattributed — the pass's screenshot timeouts traced to Chrome throttling backgrounded tabs,
  not this component's 500 ms sampler. Current guidance: `run-server/SKILL.md` §"Driving the UI
  reliably".
- **F28:** cards render recommender UserCard (live tagline), like count, date, and the
  author's-pick highlight styling (seeded rec on the flagship).
- **F29:** "Mark as Hidden Gem" on own rec → `is_hidden_gem=t` + HiddenGem (type 23) notification
  to the story author (psql-verified). **Observation to re-check in a later pass:** when the STORY
  AUTHOR views another user's rec, the card appeared to offer Edit/Delete/Mark-gem affordances
  (seen on the flagship as AuthorAlpha) — affordance-only concern (server gates own the authority),
  but the `IsOwn` gating deserves a look.
- **F30:** full attribution loop — opened `/story/5/1?rec=3` as TestUser (source row written),
  revisited → "Was the recommendation that brought you here helpful?" inline banner at chapter
  bottom → "Yes, it was!" → `recommendation_successes (1,3)` + `SuccessfulRecCount=1` (psql).

### WU-AuditFixPass note (2026-07-18)

MA-502 closed: `RecordSuccessAsync`'s tracked `SuccessfulRecCount++` (lost-update race under
concurrent readers) replaced by an atomic `ExecuteUpdateAsync` delta AFTER the success-row insert
commits. `RecommendationSection` fully adopted CommentSection's `InlineAlert` + `Translate`/
`ExceptionPresenter` pattern (raw `ex.Message` eliminated, unexpected failures now logged);
`RecommendationEditor`'s per-tick sample swallow annotated `sanctioned-silent` + registered in
`logging.md`. Full detail: `workplan.md` WU-AuditFixPass.

### MA-505 status-code seam note (2026-07-18)

Status-code seam closed (F29/F30, cells stay Stage 5 — status semantics only): `SetHiddenGemAsync`'s
reject-at-5 and `SetHighlightedByAuthorAsync`'s spotlight-at-5 limits now throw
`RecommendationValidationException` → **400** instead of `InvalidOperationException` → 401 (the auth
safety net, now reserved for the genuine unauthenticated guard). No client change needed —
`ClientRecommendationWriteService` already reconstructs `RecommendationValidationException` from a 400
body (shared MA-008 shape). Covered by Integration tier (`RecommendationWriteServiceTests` — the two
reject-at-five tests retyped to `RecommendationValidationException`, `_ThrowsInvalidOperation`
renamed `_ThrowsValidation`). Full detail: `modernization-audit/deferred-work.md` §4.

---

**WU-ParentVisibility slice (2026-07-26) — F27/F28/F30.** Recommendations are now exactly as
visible as the story they endorse. Reads: `GetForStoryAsync` and `GetByIdAsync` (the latter enumerable
by rec id, and its DTO discloses the parent `StoryId`). Note the pre-existing `isStoryAuthor` probe did
use the filtered `Stories` set, but it only ever returned false — it never gated the main query.
Writes: `SubmitAsync`, `ToggleLikeAsync`, `RecordSuccessAsync`, `RecordAttributionSourceAsync`.
**`RecordSuccessAsync` was the sharpest surface in the whole sweep** — it awards real site badges
(`Recommender`/`RecommenderSilver`) off an unverified parent, so a loop over guessed recommendation ids
could farm another user's `SuccessfulRecCount` and badges without ever being able to see the stories
involved; the existing anti-self-farm check is not a substitute. `SubmitAsync` gates on the
**confidentiality axis only**, preserving the settled WU29 decision (and its test) that a reader with
mature content off may still recommend an M-rated story — what was never intended is a rec on an
unpublished story, which takes the one-per-user slot permanently and notifies the author. D3.2
established that the attributed rec must belong to the claimed story; neither was checked for
visibility until now.

Invariant, guards, and the two root causes: `identity-and-authorization.md` §"Parent-visibility guards" (conditionality kind (g)). Enforcement: `Tests.Integration/ParentVisibilityContractTests.cs`. Full narrative: `workplan.md` WU-ParentVisibility. **No Stage number changed — every affected cell was already Stage 5 and remains 5.**
