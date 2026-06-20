# Audit — Profiles/

**Features:** 20 (profile editing), 21 (profile display), 22 (user stats), 58 (UserStat recalculation
worker).

## Shared Context
**Entities:** `UserProfile` (cold partition — `ProfileText`, 1-to-1 cascade from `User`), `UserStat`
(PK `UserId`, 22+ denormalized counters, 1-to-1 cascade). Settings (Reader/Privacy/Author) live as owned
JSON on `User` (see Identity audit). Spec calls for `IUserProfileReadService` (public profile) and
`IUserSettingsService` (the self-referential integrated read+write exception, §3.5). **No services or
components built.**

## Feature 20 — User Profile Editing
- **L1 — Stage 5** (`UserProfile.ProfileText`; JSON settings on `User`). **L2 — Stage 2**
  (`IUserSettingsService` self-referential exception unbuilt). **L3/L3.5 — Stage 2** (settings page
  grouped by concern; `/settings` route). **L4 — Stage 1. L5 — Stage 2.** Profile-picture upload to
  R2/MinIO unbuilt (no blob SDK referenced).

## Feature 21 — User Profile Display
- **L1 — Stage 5.** **L2 — Stage 2** (`IUserProfileReadService`; own-vs-other is a `bool includePrivate`
  filter, not a source switch). **L3-Logic — Stage 2.** **L3.5-Structure — Stage 2** — the two-half
  structure (§5.27): top identity (bio/tagline/stats/badges/outgoing vouches); bottom tabbed story lists
  composing `ResultsFilterPanel` + `StoryDeck` (the same atoms the search page uses). Route
  `/user/{UserId:int}/{*Tab}`. Live tables, not data mart. **L4 — Stage 1. L5 — Stage 2.**

## Feature 22 — User Stats
- **L1 — Stage 5** (`UserStat`, keyed on `UserId`). **L2 — Stage 2** (real-time counter updates by app
  logic; read for badge checks). **L3/L3.5 — Stage 2** (stats display block in profile top-half).
  **L4 — Stage 1. L5 — Stage 2.**

## Feature 58 — UserStat Recalculation Worker
- **L2 — Stage 2.** Periodic `IHostedService`/`BackgroundService` reconciling the denormalized counters.
  Pure background computation — Layer 2 *is* the worker (grid_axes). All UI layers **N/A**; **L8 — N/A**
  (EF-based recalculation, not a raw-SQL data mart).
