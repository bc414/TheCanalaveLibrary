# Audit — Recommendations/

**Features:** 27 (submission), 28 (display), 29 (Hidden Gem), 30 (attribution). Framing: "recommendation,"
never "review." Recommendations **cannot** have spoilers — deliberate absence of `IsSpoiler` (§5.6).

## Shared Context
**Entities (Core/Models/):** `Recommendation` (hot — `StoryId`, `RecommenderId`, `StatusId`),
`RecommendationDetail` (cold — text body, 1-to-1 cascade, PK=FK), `RecommendationStatus` (seeded:
Pending/Approved/Rejected/Under Review), `RecommendationSuccess` (PK `(UserId,RecommendationId)`),
`UserStoryRecommendationSource` (sparse partition off `UserStoryInteraction`). **No services or components
built.**

## Feature 27 — Recommendation Submission
- **L1 — Stage 5.** Hot/cold vertical partition + status lifecycle; no `IsSpoiler` (correct). One-per-
  user-per-story uniqueness is implied (verify unique constraint when migrating — currently not configured
  on `Recommendation`). **L2 — Stage 2** (high-effort min-character write; Pending→Approved/Rejected).
  **L3/L3.5 — Stage 2. L4 — Stage 1. L5 — Stage 2. L6 — Stage 2.**

## Feature 28 — Recommendation Display
- **L1 — Stage 5.** **L2 — Stage 2** (author spotlight ≤5; `RecommendationLike` reader likes — the
  like junction shape is unbuilt). **L3/L3.5 — Stage 2** (rec card, spotlight display). **L4 — Stage 1.
  L5 — Stage 2.**

## Feature 29 — Hidden Gem Management
- **L1 — Stage 5** (`IsHiddenGem`). **L2 — Stage 2** (5-per-user limit in C#).
- **L3-Logic — Stage 2 (reconciled Phase B, 2026-06-20; was Stage 1).** Spec §8 Open Question #4
  ("what happens at the 5-item limit") is resolved: **reject + remove-first.** When a user already has
  5 Hidden Gems and designates a 6th, the write fails (e.g. "you have 5 Hidden Gems — remove one before
  adding another"); no atomic swap, no auto-evict. Mirrors the Vouches 5-limit pattern (§5.8). **Settled
  constraint — do not revisit:** the write method checks the count against `writeDb` and fails the
  request when `count == 5`; the user must explicitly un-designate before designating a new story. (Spec
  itself is not edited — read-only snapshot; this note carries the resolution.)
- **L3.5 — Stage 2. L4 — Stage 1. L5 — Stage 2.**

## Feature 30 — Recommendation Attribution
- **L1 — Stage 5** (`UserStoryRecommendationSource` sparse; `RecommendationSuccess`). **L2 — Stage 2.**
  **L3-Logic — Stage 2** — "Was this recommendation useful?" popup after Ch.1 `IsRead` ⇒
  `RecommendationSuccess`. **L3.5 — Stage 2. L4 — Stage 1. L5 — Stage 2.**
