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
- **L1 — Stage 5.** Hot/cold vertical partition + status lifecycle; no `IsSpoiler` (correct).
  Unique `(RecommenderId, StoryId)` index added by WU29 migration; NULL `RecommenderId` (anonymized
  recs) are each distinct under Postgres NULL semantics — correct.
- **L2 — Stage 5 (WU29, 2026-06-23).** `SubmitAsync` enforces: (a) `RequireAuthenticatedUser()`;
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
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto (attribution writes not driven in the flip's browser wave —
  trigger lives in the chapter reading page, WU26, which was verified under WASM). Full wave
  narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.

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
