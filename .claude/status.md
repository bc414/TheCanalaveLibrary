# Status Grid — Feature × Layer → Stage

Dashboard only. Stage values per CLAUDE.md (1–6 or N/A). Rows are the dependency-ordered
features from `grid_axes.md`, grouped by folder cluster. Columns:

`L1 | L2 | L3-Logic | L3.5-Structure | L4-Style | L5 | L6 | L7 | L8`

Global conditions affecting many cells — kept terse; detail lives at the pointer, not here:
- **Spec supersedes stale code.** Most Stage-4 cells are stale-code traps (build to spec, code is
  salvage), not two-way adjudications. Detail: `audit-summary.md` §0/§3.
- **L4-Style blocker cleared (Tailwind v4 tokens locked).** L4-Style cells are intent-settled,
  non-blocking; each rides along inside its feature's Phase-E work-unit rather than being
  sequenced separately. Detail: `layer4-style.md` §"Prerequisite: Design Tokens",
  `forward_plan.md` Phase C "Resolved."
- **L1 migration-verified.** Fluent config lives in `IEntityTypeConfiguration<T>` classes;
  `InitialSchema` generated clean. Every L1 Stage 5 below is migration-verified. Detail:
  `layer1-data-model.md` §"Fluent API Organization."
- **Rows 19, 29 reclassified (Phase B).** Detail: `audit/Following.md`, `audit/Recommendations.md`.
- **Workplan exists.** `.claude/workplan.md` sequences the build (WU0 → atoms → composites →
  pages); rows 8/37/51/55 blocked/deferred (confirmed no dependents); Layers 5–8 batched post-MVP.
  Planning artifact — no cell Stage changed by this.
- **First real app run (WU0) found and fixed 3 startup bugs, pivoted dev off Aspire for MVP.**
  Detail split by where each fix lives: Stories L2 DI-registration fix → `audit/Stories.md` row
  4/5; render-mode/interactive-routing fix → `cross-cutting.md` "Render Mode" (current pattern);
  Aspire-off-for-MVP decision → `forward_plan.md` "Aspire orchestration during MVP dev" Resolved
  entry; start/stop + verification procedure → `.claude/skills/run-server/SKILL.md`. WU0 itself is
  closed — see `workplan.md` WU0.
- **Legacy technical-layer folders are being retired to vertical feature clusters, just-in-time
  (WU2).** `Core/Models/`, `Core/ServiceInterfaces/`, `Server/Services/`, `Client/Services/`,
  `Server/Endpoints/` are deprecated — no new file is added to them, and each work-unit moves the
  files it touches into that feature's cluster folder as part of finishing the work. Detail:
  `canalave-conventions/SKILL.md` "Code Organization".
- **Cross-cutting infra minted (WU12): `IActiveUserContext`, the content-rating named query filter,
  `IImageStorageService`.** All three are now load-bearing for every future read service touching
  `Story` or user-uploaded images, not Stories-specific. Also: the Aspire Npgsql EF Core *client*
  package is removed from `TheCanalaveLibrary.Server` (pooled DbContexts are incompatible with
  `IActiveUserContext`'s Scoped lifetime) — plain `AddDbContext` is now the standing registration
  pattern for both DbContexts. Detail: `cross-cutting.md` "Active-User Context"/"Content Rating
  Filtering", `layer2-services.md` "DbContext Registration", `audit/ImageStorage.md`,
  `forward_plan.md` "Aspire orchestration during MVP dev" narrower correction.
- **TPT denormalization retrofitted (WU31.5, 2026-06-24).** Discovery columns (`DateCreated`,
  `LastUpdatedDate`, `Rating`, `IsPublished` on blog-post children; `DatePosted` on comment children)
  moved from base tables to child tables. Named filter removed from `BaseBlogPost`. F35/F36/F23–F26
  L1/L2 momentarily reopened and re-closed to Stage 5 on green `dotnet test`. Detail:
  `layer1-data-model.md` §"Denormalization with TPT", `audit/BlogPosts.md`, `audit/Comments.md`.
- **Three-tier automated test suite in place (WU12.5 + 2026-06-22 backfill).** Three test projects in
  the `.sln` — `dotnet test` runs all. Organized by *kind* (not production project): `Tests.Unit`
  (directly-constructed, no host/DB — refs Core + Server), `Tests.Integration` (real Testcontainers
  Postgres + `WebApplicationFactory`), `Tests.RazorComponents` (bUnit component render tests). Per-unit
  loop and Phase E loop now name `dotnet test`; obligation is advisory ("should add tests") — no Stage
  gate. No cell Stage changes from this; it's tooling every future work-unit should add tests to.
  Detail: `canalave-conventions/testing.md`, `workplan.md` WU12.5.
- **Integration test isolation overhaul (2026-06-24).** Respawn DB reset before every test; all 19
  integration test classes migrated to `IntegrationTestBase` (Respawn reset, factory lifecycle, GUID-
  suffixed user seeding via `SeedUserAsync`). `[assembly: CollectionBehavior(DisableTestParallelization
  = true)]` made deliberate. `RecommendationStatusEnum` added to `Core/Lookups/ModelEnums.cs`. This
  unlocked reliable L5 Stage-5 verification for F7/16/17/18/19/23/24/25/26/27/28/29/42/43 (all
  integration tests that existed but had order-dependent shared-state). Detail: `canalave-conventions/
  testing.md` §"Integration tests reset between every test", `forward_plan.md` "Integration test
  isolation foundation" Resolved. F4/F5 L5 stay Stage 4 (architectural disagreement pending Opus
  reconcile; unrelated to this overhaul).
- **TestAppFactory DB-wiring fix + TPT phantom-nav fix (2026-06-25, WU31_5b).** `TestAppFactory`
  was silently connecting to the dev DB (`localhost:5432`) instead of the Testcontainers container
  because `ConfigureAppConfiguration` fires too late with `WebApplicationBuilder`. Fixed by
  re-registering both `DbContextOptions` in `ConfigureServices` (doc: `testing.md` §"Driving the
  content-rating filter"). Separately: four phantom down-navigation properties on `BaseComment`
  (`BlogPostComment`, `ChapterComment`, `GroupComment`, `UserProfileComment`) caused EF to create
  phantom FK columns on `base_comments` pointing back to child comment tables — backwards TPT edges
  that formed FK cycles, breaking Respawn's topological sort and leaving the `groups` table
  uncleaned between tests. Migration `WU31_5b_DropPhantomBaseCommentFKs` drops the 4 phantom
  columns/indexes/FKs. Three additional service bugs exposed by the clean DB: `ServerGroupWriteService
  .AddStoryAsync` missing `IgnoreQueryFilters(["ContentRating"])` on story lookup; `ServerRecommendation
  WriteService.SubmitAsync` (a) using `Select((int?)s.AuthorId).FirstOrDefault()` which confuses null-
  author-id with "row not found", (b) unconditional `.Value` on a nullable AuthorId. All fixed; all 298
  integration tests green. Detail: `audit/Comments.md`, `audit/Groups.md`, `testing.md`.

| # | Feature | Folder | L1 | L2 | L3-Logic | L3.5-Struct | L4-Style | L5 | L6 | L7 | L8 |
|---|---------|--------|----|----|----------|-------------|----------|----|----|----|----|
| 1 | Identity & Auth | Identity | 5 | 5 | 5 | 5 | 1 | N/A | N/A | N/A | N/A |
| 2 | Lookup Tables & Seed Data | Lookups | 5 | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 3 | Sprite & Theme System | Sprites | 5 | 5 | 5 | 5 | 1 | 4 | N/A | N/A | N/A |
| 4 | Story Creation & Editing | Stories | 5 | 5 | 5 | 5 | 5 | 4 | 2 | N/A | N/A |
| 5 | Story Browsing & Display | Stories | 5 | 5 | 5 | 5 | 1 | 4 | 2 | N/A | N/A |
| 6 | Chapter Writing & Versioning | Chapters | 5 | 5 | 5 | 5 | 5 | 5 | 2 | N/A | N/A |
| 7 | Chapter Reading | Chapters | 5 | 5 | 5 | 5 | 5 | 5 | 2 | N/A | N/A |
| 8 | Story Arcs | Stories | 5 | 2 | 1 | 1 | 1 | 2 | N/A | N/A | N/A |
| 9 | Series & Ordering | Stories | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 10 | Story Relationships | Stories | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 11 | Tag Administration | Tags | 5 | 5 | 5 | 5 | 1 | 2 | 2 | N/A | N/A |
| 12 | Story Tagging | Tags | 5 | 5 | 5 | 5 | 1 | 5 | 2 | N/A | N/A |
| 13 | Tag Display & Sprites | Tags | 5 | 5 | 5 | 5 | 5 | 2 | N/A | N/A | N/A |
| 14 | Tag Filtering & Selection UI | Tags | N/A | 5 | 5 | 5 | 5 | 2 | N/A | N/A | N/A |
| 15 | Saved Tag Selections | Tags | 5 | 2 | 2 | 2 | 1 | 2 | N/A | N/A | N/A |
| 16 | Story Interaction State Writes | UserStoryInteractions | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 2 | N/A |
| 17 | Interaction Lists & Bookshelves | UserStoryInteractions | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 18 | User Following | Following | 5 | 5 | 5 | 5 | 5 | 5 | 2 | N/A | N/A |
| 19 | Vouches | Following | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 20 | User Profile Editing | Profiles | 5 | 5 | 5 | 5 | 5 | 2 | N/A | N/A | N/A |
| 21 | User Profile Display | Profiles | 5 | 5 | 5 | 5 | 5 | 2 | N/A | N/A | N/A |
| 22 | User Stats | Profiles | 5 | 5 | 5 | 5 | 5 | 2 | N/A | N/A | N/A |
| 23 | Comment Posting | Comments | 5 | 5 | 5 | 5 | 5 | 5 | 2 | N/A | N/A |
| 24 | Comment Display & Pagination | Comments | 5 | 5 | 5 | 5 | 5 | 5 | 2 | N/A | N/A |
| 25 | Comment Likes | Comments | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A | N/A |
| 26 | Spoiler Comments | Comments | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A | N/A |
| 27 | Recommendation Submission | Recommendations | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 28 | Recommendation Display | Recommendations | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A | N/A |
| 29 | Hidden Gem Management | Recommendations | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A | N/A |
| 30 | Recommendation Attribution | Recommendations | 5 | 5 | 5 | 5 | 5 | 2 | N/A | N/A | N/A |
| 31 | Search Page | Discovery | N/A | 5 | 5 | 5 | 1 | 2 | 2 | N/A | N/A |
| 32 | Full-Text Search | Discovery | 5 | 5 | 5 | 5 | 1 | 2 | 5 | N/A | N/A |
| 33 | Manual Tree Search | Discovery | N/A | 2 | 2 | 2 | 1 | 2 | 2 | N/A | N/A |
| 34 | Tag Directory | Discovery | N/A | 5 | 5 | 5 | 1 | 2 | N/A | N/A | N/A |
| 35 | Blog Post Writing | BlogPosts | 5 | 5 | 5 | 5 | 1 | 2 | 2 | N/A | N/A |
| 36 | Blog Post Display | BlogPosts | 5 | 5 | 5 | 5 | 1 | 2 | N/A | N/A | N/A |
| 37 | Polls | BlogPosts | 5 | 2 | 1 | 1 | 1 | 2 | N/A | N/A | N/A |
| 38 | Group Management | Groups | 5 | 5 | 5 | 5 | 5 | 5 | 2 | N/A | N/A |
| 39 | Group Content & Folders | Groups | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A | N/A |
| 40 | Group Display | Groups | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A | N/A |
| 41 | Notification Generation | Notifications | 5 | 5 | N/A | N/A | N/A | N/A | 2 | N/A | N/A |
| 42 | Notification Display | Notifications | 5 | 5 | 5 | 5 | 1 | 5 | 2 | N/A | N/A |
| 43 | Notification Settings | Notifications | 5 | 5 | 5 | 5 | 1 | 5 | N/A | N/A | N/A |
| 44 | Reading Progress Tracking | Chapters | 5 | 5 | 5 | 5 | N/A | N/A | N/A | 2 | N/A |
| 45 | View Count Tracking | Stories | 5 | 2 | 2 | N/A | N/A | 2 | N/A | 2 | N/A |
| 46 | Content Reporting | Moderation | 5 | 5 | 5 | 5 | 3 | 2 | 5 | N/A | N/A |
| 47 | Moderation Queue & Actions | Moderation | 5 | 5 | 5 | 5 | 3 | N/A | 5 | N/A | N/A |
| 48 | Story Approval Workflow | Moderation | 5 | 5 | 5 | 5 | 3 | N/A | N/A | N/A | N/A |
| 49 | Private Messaging | Messaging | 5 | 5 | 5 | 5 | 5 | N/A | 2 | N/A | N/A |
| 50 | Badge System | Badges | 5 | 5 | 5 | 5 | 1 | 2 | N/A | N/A | N/A |
| 51 | Custom Lists | CustomLists | 5 | 2 | 1 | 1 | 1 | 2 | N/A | N/A | N/A |
| 52 | User Account Deletion | Identity | 5 | 5 | 5 | 5 | 1 | N/A | N/A | N/A | N/A |
| 53 | Story Import & Verification | Moderation | 5 | 2 | 2 | 2 | 1 | N/A | N/A | N/A | N/A |
| 54 | Content Download/Export | Export | N/A | 2 | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 55 | Community Spotlight | Spotlight | 1 | 1 | 1 | 1 | 1 | N/A | N/A | N/A | N/A |
| 56 | Feature Contributions | BlogPosts | 5 | 2 | 2 | 2 | 1 | N/A | N/A | N/A | N/A |
| 57 | Notification Cleanup Worker | Notifications | N/A | 2 | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 58 | UserStat Recalculation Worker | Profiles | N/A | 2 | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 59 | Automatic Tree Search | Discovery | N/A | 2 | 2 | 2 | 1 | 2 | N/A | N/A | 2 |
| 60 | Tree Search Data Mart Worker | Discovery | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | 2 |
| 61 | Also Favorited / Also Recommended | Discovery | N/A | 2 | 2 | 2 | 1 | 2 | 2 | 2 | 2 |
| 62 | SiteDailyStat Worker | Moderation | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | 2 |
