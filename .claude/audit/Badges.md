# Audit — Badges/

**Feature:** 50 (badge system). MVP: synchronous inline award-checking (§5.20).

## Shared Context
**Entities (Core/Models/):** `Badge` (string PK `BadgeKey`, `DisplayName` unique, `Description`,
`IconBaseUrl`, `SortOrder`; seeded — BetaReader/Patron/Recommender/Architect/Artist, with a
`// ... add other badges` gap), `UserBadge` (composite `(UserId,BadgeKey)`, `DisplayOrder` curation where
0 = hidden, `DateEarned` default, Restrict on Badge). `SiteBadges` string constants in `SiteConstants.cs`.
**No services or components built.**

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

**Tier definitions (settled):**
- Tier 1: `SiteBadges.Recommender` (existing constant + seed row) — threshold 10.
- Tier 2: `SiteBadges.RecommenderSilver` (new constant + seed row added in WU36 migration) — threshold 50.
Both checks run on every qualifying `RecordSuccessAsync` (idempotent re-calls are no-ops).

**Default visibility on award (settled):** newly earned badges get `DisplayOrder = (max existing
DisplayOrder for that user) + 1` — visible by default. The curation UI lets users hide or reorder.
`UserCard.razor` caps the badge row to 3.

**Deferred award triggers:**
| Badge | Status | Blocking reason |
|---|---|---|
| `Patron` | Manual/future | No automated producer. (Formerly cited the `FeatureContributions` counter — **removed 2026-07-18** with the Feature 56 cut; that citation was a stale copy-paste anyway.) Grant via direct `user_badges` insert. |
| `BetaReader` | Deferred | `AcknowledgedAsBetaReaderCount` counter not populated (acknowledgment/beta-reader producer has no assigned WU) |
| `Architect` | Manual grant | **Feature 56 (its intended automated producer) was CUT 2026-07-18** — the `FeatureContributions` counter no longer exists. The badge is deliberately **retained** in the catalogue as the site-stewardship recognition lever; grant it by direct `user_badges` insert (psql). `IBadgeWriteService.AwardAsync` stays unmapped (no admin HTTP route). See `audit/BlogPosts.md` Feature 56 CUT note. |
| `Artist` | Manual/future | No automated producer. (Formerly cited the `FeatureContributions` counter — **removed 2026-07-18** with the Feature 56 cut; that citation was a stale copy-paste anyway.) Grant via direct `user_badges` insert. |

**Open:** none. All WU36 decisions are settled. (Feature 56 cut 2026-07-18 removed the never-built
`FeatureContributions` counter that three of the above rows had cited as their producer — the
Architect badge is retained as a manual grant; see the CUT note in `audit/BlogPosts.md`.)

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
