# Audit — Lookups/

**Feature:** 2 (Lookup Tables & Seed Data). **Layer-1-only cluster** — no service interfaces, no CQRS
split, no components. Every other folder FK-references these. L2/L3/L3.5/L4/L5/L6/L7/L8 are **N/A**.

## Shared Context

**Artifacts:** `Core/Lookups/ModelEnums.cs` (all enums), `Server/Data/SiteConstants.cs` (string-key
constants: `SiteBadges`, `SiteSearchModes`, `UserStoryInteractionFilters`), and the lookup/enum-mirror
entities scattered in `Core/Models/` (`StoryStatus`, `TagType`, `ReportReason`, `ReportStatus`,
`NotificationCategory`, `NotificationType`, `AcknowledgmentRole`, `RecommendationStatus`,
`StoryRelationshipType`, `SearchMode`, `UserInteractionFilter`, `Theme`, `Badge`, `DefaultSearchSetting`).
All seed data lives inline in `ApplicationDbContext.OnModelCreating` via `HasData`.

---

## Feature 2 — Lookup Tables & Seed Data — **L1 Stage 4**

Most of this is excellent and would be Stage 5 on its own: the `~35` `NotificationType` rows with
gap-based numbering (10/20/30… per category), `StoryStatus`, `TagType`, `ReportReason`/`ReportStatus`,
`RecommendationStatus`, `StoryRelationshipType`, `AcknowledgmentRole`, `Theme`, role/badge seeds — all
match spec §4/§5 and use `HasConversion<short>()` enum mirrors correctly.

It is **Stage 4** because of concrete divergences from the revised model — but per the audit-summary §0
principle these are **stale code, not competing intent**: the spec is the recent refinement and the seed
data isn't working/validated, so the spec wins outright. Stage 4 here is a trap-warning, and the
resolution direction below is fixed (conform to spec), i.e. effectively Stage 2 build-to-spec.

1. **`SearchMode` seed predates the three-axis model.** Seeded keys (`SiteSearchModes`):
   `DefaultSearch / TreeSearch / RandomSearch / AlsoFavorited`. The revised model (§5.3, and the
   `grid_axes`/`folder_clusters` notes) requires:
   `SearchPage / TreeSearch / AutoTreeSearch / AlsoFavorited / AlsoRecommended /
   ProfilePublishedStories / ProfileFavorites / ProfileRecommendations`.
   Critically, **"RandomSearch" as a distinct mode contradicts** "Random = Source=All + Sort=Random on the
   SearchPage surface, not a mode" (§5.3).

2. **`DefaultSortOrder` enum offers excluded sorts.** It defines
   `LastUpdated, PublishDate, Favorites, ViewCount, Relevance, Random`. §5.3.3 explicitly excludes sort by
   favorites / last-updated / rec-count. The valid sort axis is `Random / Date Published / Relevance /
   Score`. The enum is from the pre-three-axis era.

3. **Vestigial reading/favorite enums.** `ReadStatus { Unread, InProgress, Completed }` and
   `FavoriteStatus { None, Favorite, PrivateFavorite }` are leftovers from the enum/junction model that
   Settled Axiom #3 (boolean columns) replaced. They tie into the UserStoryInteractions divergence.

4. **`UserStoryInteractionFilters.InProgress`** mirrors the same pre-revision "in progress" concept.

5. **Incomplete seed data.** `DefaultSearchSetting` HasData has only a handful of rows with
   `// ... etc. for all combinations`; `SiteBadges`/`Badge` seed ends with `// ... add other badges`. The
   SearchMode × InteractionFilter matrix is not fully populated.

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

**Implied resolution:** Stage 2 — re-derive `SearchMode`/sort vocabulary from §5.3, retire vestigial
enums, complete the seed matrix. (The `IEntityTypeConfiguration` extraction itself is no longer open — see
item 6 above.)
