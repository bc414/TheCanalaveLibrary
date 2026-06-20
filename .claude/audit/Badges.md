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
- **L2 — Stage 2.** MVP synchronous inline award-check; `UserStat`-driven (read by checks). Background
  award-checking is later.
- **L3-Logic — Stage 2.** **L3.5-Structure — Stage 2** (badge grid; `DisplayOrder` curation UI; each
  `Badge` has an `IconBaseUrl`). **L4 — Stage 1. L5 — Stage 2.**
