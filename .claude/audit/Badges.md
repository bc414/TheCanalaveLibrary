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
- **L5 — Stage 2.** Client-side badge service deferred (batch post-MVP).

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
| `Patron` | Deferred | `FeatureContributions` counter not populated (WU37) |
| `BetaReader` | Deferred | `AcknowledgedAsBetaReaderCount` counter not populated (WU37) |
| `Architect` | Deferred | `FeatureContributions` counter not populated (WU37) |
| `Artist` | Deferred | `FeatureContributions` counter not populated (WU37) |

**Open:** none. All WU36 decisions are settled.
