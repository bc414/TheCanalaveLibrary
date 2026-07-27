# Audit — Lookups/

**Feature:** 2 (Lookup Tables & Seed Data). **Layer-1-only cluster** — no service interfaces, no CQRS
split, no components. Every other folder FK-references these. All layers other than L1 are **N/A**
(L7 itself dissolved 2026-07-06).

## Shared Context

**Artifacts:** `Core/Lookups/ModelEnums.cs` (all enums), `Server/Data/SiteConstants.cs` (string-key
constants: `SiteBadges`, `SiteSearchModes`, `UserStoryInteractionFilters`), and the lookup/enum-mirror
entities scattered in `Core/Models/` (`StoryStatus`, `TagType`, `ReportReason`, `ReportStatus`,
`NotificationCategory`, `NotificationType`, `AcknowledgmentRole`, `RecommendationStatus`,
`StoryLineageType` (renamed from `StoryRelationshipType` in WU42, 2026-07-12 — moved to
`Core/Stories/`, not `Core/Models/`), `SearchMode`, `UserInteractionFilter`, `Theme`, `Badge`,
`DefaultSearchSetting`). All seed data lives inline in `ApplicationDbContext.OnModelCreating` via
`HasData`.

---

## Feature 2 — Lookup Tables & Seed Data — **L1 Stage 5**

**Stage note (2026-07-27):** the WU26-era Stage-4 divergence list below was resolved by WU28's
seed-vocabulary correction (see `audit/Discovery.md` "WU28 Stage note") and never revisited here;
this headline lagged the grid (which has said 5 since). Verified against code 2026-07-27:

1. **`SearchMode` three-axis model — RESOLVED.** `Core/Discovery/SiteSearchModes.cs` carries the
   revised catalog (`SearchPage / TreeSearch / AutoTreeSearch / AlsoFavorited / AlsoRecommended /
   ProfilePublishedStories / ProfileFavorites / ProfileRecommendations`) and its own doc comment
   states "RandomSearch is not a mode — it's Source=All + Sort=Random on SearchPage".
2. **`DefaultSortOrder` — RESOLVED.** Reworked to the §5.3.3 axis (`DatePublished / Relevance /
   Score / Random`, `LastUpdated` retained only as a non-text-query fallback per
   `IStoryReadService` sort rules); excluded popularity sorts gone.
3. **Vestigial `ReadStatus`/`FavoriteStatus` enums — RESOLVED.** Removed; `ModelEnums.cs` carries
   the removal note. Interaction filters use the boolean-column vocabulary
   (`HasStarted / Completed / Favorited / HiddenFavorited`).
4. **`UserStoryInteractionFilters.InProgress` — RESOLVED** with item 3 (now `HasStarted`).
5. **Seed matrix completeness — RESOLVED.** The `// ... etc.` placeholder seeds were completed as
   part of the same pass; badge seeds live in the badge system's own seed path.

Covered by the Integration tier via every FK-consuming suite (seed data is exercised by all
service tests); no dedicated Lookups test class applies — pure seed data, no service surface.

The original Stage-4 adjudication reasoning (stale code vs. spec, per `audit-summary.md` §0) is
retained in git history; it resolved as predicted — conform to spec.

6. **RESOLVED — `IEntityTypeConfiguration<T>` extraction.** Previously: `folder_clusters.md` described
   this folder as containing "lookup `IEntityTypeConfiguration<T>` classes," and the conventions named them
   `{Entity}Configuration`, while in reality **all** configuration was inline in one 1600-line
   `OnModelCreating`. The user has now made the organizational decision (see `forward_plan.md`'s former
   open item, now resolved): config classes are extracted into `IEntityTypeConfiguration<T>` files grouped
   one-per-folder-cluster, but **colocated in `TheCanalaveLibrary.Server/Data/Configurations/`** —
   *not* in this folder. `folder_clusters.md`'s `Lookups/` row has been corrected accordingly. See
   [layer1-data-model.md](../skills/canalave-conventions/layer1-data-model.md) §"Fluent API Organization"
   for the authoritative rule and rationale (EF config is a cross-cluster delete-graph kept together for
   migration-time reasoning, unlike service impls which live in cluster folders).

(The former "Implied resolution: Stage 2 — re-derive the vocabulary from §5.3" was executed by WU28;
nothing remains open in this cluster.)
