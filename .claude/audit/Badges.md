# Audit — Badges/

**Feature:** 50 (badge system). MVP: synchronous inline award-checking (§5.20).

## Shared Context
**Entities (Core/Badges/):** `Badge` (string PK `BadgeKey`, `DisplayName` unique, `Description`,
`IconBaseUrl`, `SortOrder`; seeded — BetaReader/Patron/Recommender/Architect/Artist), `UserBadge`
(composite `(UserId,BadgeKey)`, `DisplayOrder` curation where 0 = hidden, `DateEarned` default,
`EarnedCount` — the no-tiers display count, WU-StatBadgeProducers — Restrict on Badge).
`SiteBadges` string constants moved to `Server/Badges/SiteBadges.cs` (WU-StatBadgeProducers,
closing MA-108; formerly the top-level `SiteConstants.cs`).

## Feature 50 — Badge System
- **L1 — Stage 5.** String-keyed `Badge` + `UserBadge` junction with curation ordering. Seed is partially
  complete (placeholder comment) but the shape is sound. Awaiting migration.
- **L2 — Stage 5 (2026-06-25, WU36).**
  - Created `Core/Badges/`: `EarnedBadgeDto`, `IBadgeReadService`, `IBadgeWriteService`.
  - Created `Server/Badges/`: `ServerBadgeReadService`, `ServerBadgeWriteService` (primary-ctor chaining;
    CS9107-safe). Registered in `Server/Program.cs` (write service scoped, read forwarded).
  - `UserStat.RecommendationSuccessesEarned` column added (migration `20260625234308_WU36_Badges`).
    `SiteBadges.RecommenderSilver` constant + seed row added (same migration).
  - `ServerRecommendationWriteService.RecordSuccessAsync` now increments `RecommendationSuccessesEarned`
    for the recommender (anti-self-farm guard, anonymous-rec guard) and fires
    `IBadgeWriteService.AwardAsync` for Recommender (≥10) / RecommenderSilver (≥50), best-effort.
  - `ServerUserProfileReadService`, `ServerFollowingReadService`, `ServerRecommendationReadService`:
    `UserCardDto.Badges` now projects curated visible subset (`DisplayOrder > 0`, ordered by
    `DisplayOrder`) at all 6 card-producer sites; `UserCard.razor` caps the display row to 3.
  - Verified: `dotnet build` green (0 errors). Integration tier: `BadgeServiceTests` (11 tests) and
    6 new Tastemaker award-chain tests in `RecommendationWriteServiceTests` — all 317 integration
    tests pass (7 pre-existing `ModerationServiceTests` DI failures unrelated to WU36).
  - **WU-StatBadgeProducers (2026-07-31) — no-tiers model + BetaReader auto-award.** `RecommenderSilver`
    retired outright (const/seed row/threshold literal removed — see "Tier paradigm — RETIRED
    site-wide" below); `Recommender` changed from threshold 10 to ≥1, displaying `EarnedCount`.
    `IBadgeWriteService.AwardAsync` gained an `earnedCount` parameter, setting `UserBadge.EarnedCount`
    on both first award and every repeat qualifying event (one call keeps both in step). New
    `Core/Collaboration/` + `Server/Collaboration/` cluster:
    `IStoryAcknowledgmentReadService`/`WriteService`, mirroring `IStoryLineageReadService`/
    `WriteService`'s shape exactly (request/accept/decline/revoke, consent-gated, composite-PK row
    reuse on re-request-after-decline). `StoryAcknowledgment` gained `StatusId`
    (`StoryAcknowledgmentStatus`: Pending/Accepted/Declined) + `DateResponded`. Producer:
    `ServerStoryAcknowledgmentWriteService.AcceptAsync` increments
    `UserStat.AcknowledgedAsBetaReaderCount` (role Beta Reader only, anti-self-farm — self-credit is
    rejected outright at request time) and awards `BetaReader` at ≥1; `RevokeAsync` decrements only
    when the credit was Accepted (transition-delta). Relocated `StoryAcknowledgment`,
    `AcknowledgmentRole`, `BetaReader`, `CoAuthor` from `Core/Models/` to `Core/Collaboration/`
    (MA-112). `AcknowledgmentRole` id 5 "Inspiration" retired — see `audit/Stories.md` Feature 10.
    `UserStatRecalculator` gained a third drift-correction pass syncing `UserBadge.EarnedCount` from
    the corrected `UserStat` columns (deliberately does not award — see the class doc).
  - Verified: `dotnet build` green. Full suite green — Unit 776/776, RazorComponents 626/626,
    Integration 1012/1012 (2,414 total, up from the 2,330 baseline). New Integration coverage:
    `StoryAcknowledgmentServiceTests` (request/accept/decline/revoke lifecycle, self-credit
    rejection, kind-(g) recipient gating, counter inc/dec, `BetaReader` badge award +
    `EarnedCount`), `RecommendationWriteServiceTests` (≥1 award + `EarnedCount` tracking replacing
    the retired 10/50 boundary pair), `UserStatRecalculatorTests` (new aggregates for both
    counters, the `EarnedCount` sync pass, and a "does not award missing badges" guard),
    `UserProfileEndpointsTests` (`SearchUsersByNameAsync` substring/case/cap/Private-still-returned).
    Browser-verified end to end against the dev DB: full credit→notify→accept→badge round trip
    (`×1` display, no tier), public `StoryAcknowledgmentsBox` on the story page, `Recommender×12`
    seeded count rendering, and `RecommenderSilver` absent from the live `badges` table.
- **L3-Logic — Stage 5 (2026-06-25, WU36).**
  - `SharedUI/Profiles/BadgeSettingsForm.razor`: `_seeded`-guarded `OnParametersSet`; `_visibleKeys`
    list mutated by `Hide`, `Show`, `MoveUp`, `MoveDown`; `HandleSave` emits ordered visible-key list.
  - `SharedUI/Profiles/SettingsPage.razor`: injects `IBadgeWriteService`; loads badges in
    `OnInitializedAsync` concurrently; adds `_badgesBusy` flag; wires `HandleSaveBadgesAsync` via
    existing `RunWithFeedbackAsync`.
  - Verified: RazorComponents tier `BadgeSettingsFormTests` (14 tests, all pass): empty-state, visible/
    hidden sections, Hide/Show toggle, MoveUp/MoveDown reorder-emit, Save callback, Busy state.
- **L3.5-Structure — Stage 5 (2026-06-25, WU36).** `BadgeSettingsForm.razor` markup: two sections
  (Visible/Hidden), move-up/down + Hide/Show buttons, Save button, empty-state; parameter-driven leaf,
  no `@inject`. Verified same RazorComponents tier as L3.
- **L4 — Stage 1.** Visual sign-off pending. UI renders but full design-token / responsive pass not done.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto (badge surfaces not browser-driven in the flip's wave). Full
  wave narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.

- **L4.5-Browser verification (2026-07-02) — Feature 50 → L4.5=5.** Browser-verifiable surface
  exercised as TestUser against the seeded dev DB:
  - Empty state: `/settings` Badges section rendered "You haven't earned any badges yet."
  - Curation: after awarding `Recommender` (direct `user_badges` insert — the award *trigger*
    thresholds are Integration-covered by `BadgeServiceTests` + the Tastemaker chain tests, and need
    10 distinct reader successes, impractical to drive by hand), the Visible section rendered the
    badge with ↑/↓/Hide; Hide → Save persisted `DisplayOrder=0`, Show → Save restored `1`
    (psql-verified both ways).
  - Display: the `UserCard` on TestUser's recommendation (story 1 `RecommendationSection`) projected
    the curated badge (`<img title="Recommender">`) — the card-producer projection works live.
  - **Resilience fix (same-session):** badge icon assets are provisioned out-of-band like sprites,
    and none exist in dev (`wwwroot/icons/` absent), so badge imgs rendered broken-image glyphs.
    `UserCard` and `BadgeSettingsForm` imgs now carry `onerror="this.style.display='none'"` —
    consistent with the optimistic-asset philosophy; the settings rows still show name+description
    text. Icon provisioning itself remains an out-of-band task.
  - `DataSeeder` now awards TestUser the `Recommender` badge (`DisplayOrder=1`) so the curation UI
    and card badge row render a populated state on a fresh DB.
  - L4 stays 1 (visual sign-off pending) — nothing unusable found.

## WU36 Settled Decisions (2026-06-25)

**Mechanism:** synchronous inline, best-effort. Badge award fires in the write service that triggers
it, after the primary `SaveChangesAsync`, in a `try/catch` — never fails the parent operation.
`IBadgeWriteService.AwardAsync` is idempotent (no-op if already earned; returns `true` only on first
award). No background worker for MVP. **Settled — do not revisit.**

**Scope — one live award trigger in WU36:** the Recommender / "Tastemaker" badge. All other catalogue
badges remain deferred to the WUs that build their source features. **Settled — do not revisit.**

**Tier paradigm — RETIRED site-wide (WU-StatBadgeProducers, 2026-07-30).** The Bronze/Silver tier
decision below (originally "Settled — do not revisit") is **superseded**. Provenance investigation
found the Bronze/Silver tier table has no design basis: it originates in a single Gemini transcript
turn (Entry #1577, 2025-10-25 11:59, `Badge_Deliberations.md` §1) produced in response to a pure
document-transcription request ("create a detailed document of features... I want an organized,
verbose document"), with Gemini's own column headers reading `Badge Name (Suggestion)` / `Tiers
(Example)` — hedges dropped when copied into `Badge_Deliberations.md`. An identical synthesis run
four minutes earlier (#1578, same source files) produced zero tier data. Bronze/Silver occurs exactly
once in the ~75,000-line transcript and is never revisited, justified, or affirmed by the owner — the
same shape as the retired `AutoLoadNextChapter` feature (tracker A2). **New model: a badge is earned
at ≥1 and displays its `UserBadge.EarnedCount`, no tiers.** Anti-farm protection moved from the
threshold to the *gate* — every badge added under this model requires another person's cooperation
per increment (an acknowledgment must be accepted; a lineage link must be approved). `Recommender`'s
threshold changed from 10 to ≥1; `RecommenderSilver` (threshold 50) is **retired outright** — its
constant, seed row, and threshold literal are removed, and `RecommenderSilver` is added to
`scripts/check-doc-hygiene.ps1`'s retired-name registry. This is a documentation record of the
supersession; the removal itself is described in `workplan.md`'s WU-StatBadgeProducers entry.

**The Tastemaker chain (WU26/WU29 already built):** `?rec={id}` URL param →
`RecordAttributionSourceAsync` → `UserStoryRecommendationSource`; reading Ch.1 to ≥90% →
`RecommendationHelpfulPrompt`; "Yes" → `RecordSuccessAsync`. WU36 wires the missing tail:
`RecordSuccessAsync` now also increments `UserStat.RecommendationSuccessesEarned` for the recommender
(author-side aggregate), then checks badge thresholds.

**Anti-self-farm guard (settled):** `RecordSuccessAsync` increments and awards only when
`rec.RecommenderId != null && rec.RecommenderId != userId` (the reader recording the success).
Anonymous recs and self-recorded successes skip silently.

**New `UserStat` column (settled):** `RecommendationSuccessesEarned` (int, default 0, added in WU36
migration). Do NOT reuse `RecommendationsFoundUseful` — that is a reader-side concept with different
semantics.

**Tier definitions — RETIRED, see "Tier paradigm — RETIRED site-wide" above (WU-StatBadgeProducers,
2026-07-30).** Historical record of what WU36 originally shipped: Tier 1 `SiteBadges.Recommender` at
threshold 10, Tier 2 `SiteBadges.RecommenderSilver` (threshold 50), both checked on every qualifying
`RecordSuccessAsync`. `RecommenderSilver` no longer exists in the catalogue; `Recommender` now awards
at ≥1 and displays its count.

**Default visibility on award (settled):** newly earned badges get `DisplayOrder = (max existing
DisplayOrder for that user) + 1` — visible by default. The curation UI lets users hide or reorder.
`UserCard.razor` caps the badge row to 3.

**Deferred award triggers:**
| Badge | Status | Blocking reason |
|---|---|---|
| `Patron` | Manual/future | No automated producer. (Formerly cited the `FeatureContributions` counter — **removed 2026-07-18** with the Feature 56 cut; that citation was a stale copy-paste anyway.) Grant via direct `user_badges` insert. |
| `BetaReader` | **Built (WU-StatBadgeProducers, 2026-07-31)** | Auto-awards at ≥1 accepted `StoryAcknowledgment` (role Beta Reader). See "WU-StatBadgeProducers Stage note" below. |
| `Architect` | Manual grant | **Feature 56 (its intended automated producer) was CUT 2026-07-18** — the `FeatureContributions` counter no longer exists. The badge is deliberately **retained** in the catalogue as the site-stewardship recognition lever; grant it by direct `user_badges` insert (psql). `IBadgeWriteService.AwardAsync` stays unmapped (no admin HTTP route). See `audit/BlogPosts.md` Feature 56 CUT note. |
| `Artist` | Manual/future | No automated producer. (Formerly cited the `FeatureContributions` counter — **removed 2026-07-18** with the Feature 56 cut; that citation was a stale copy-paste anyway.) Grant via direct `user_badges` insert. |

**Open:** none. All WU36 decisions are settled except the tier paradigm (see above, retired
2026-07-30). (Feature 56 cut 2026-07-18 removed the never-built `FeatureContributions` counter that
three of the above rows had cited as their producer — the Architect badge is retained as a manual
grant; see the CUT note in `audit/BlogPosts.md`.)

## WU-StatBadgeProducers Stage note (2026-07-31)

Closed tracker B4 in full and B3's two acknowledgment-counter rows (`SpotlightCount` re-filed
under B8, not built here). Full narrative: `workplan.md` WU-StatBadgeProducers; `audit/Profiles.md`
Feature 22/58 and `audit/Stories.md` Feature 10 for the counter-producer side;
`audit/Messaging.md` and `audit/Spotlight.md` for the `UserPicker` retrofits;
`design/access-gating-first-principles.md` for the search's visibility exclusion. `dotnet test`
green (Unit 776, RazorComponents 626, Integration 1012 — 2,414 total); browser-verified end to
end (see the L2 note above for both the automated and manual verification detail).

### WU-AuditFixPass note (2026-07-18)

Security fix MA-601 closed: `BadgeEndpoints` no longer accepts a client-supplied `userId` on any
route — curation read and display-order derive the caller from `IActiveUserContext`, and the
`/award` route is REMOVED entirely (awards are earned; the only production caller is
`ServerRecommendationWriteService`, in-process — a mapped route would let any WASM caller self-mint
Patron/Architect; same unmapped-generation decision as Notifications, and deliberately NOT the
service-level caller==target check the audit verification sketched, which would break the
in-process award-to-other-user path). `ClientBadgeWriteService.AwardAsync` throws
`NotSupportedException`. Covered by Integration tier (`BadgeEndpointsTests` — attacker-shaped
query strings + unmapped-route pin). Full detail: `workplan.md` WU-AuditFixPass.

### MA-611 status-code seam note (2026-07-18)

Status-code seam closed (F50, cells stay Stage 5 — status semantics only):
`SetDisplayOrderAsync`'s unowned-key guard (a requested display key the caller hasn't earned) now
throws the new `BadgeValidationException` (a `CanalaveValidationException`) → **400** instead of
`InvalidOperationException` → 401 (the auth safety net, now reserved for the `RequireUserId` guard).
`ClientBadgeWriteService` gained a 400 arm reconstructing `BadgeValidationException` from
`ProblemDetails.Detail`. Covered by Integration tier (`BadgeServiceTests` —
`SetDisplayOrderAsync_UnownedBadgeKey_ThrowsValidation`, retyped from `InvalidOperationException`).
Full detail: `modernization-audit/deferred-work.md` §4.
