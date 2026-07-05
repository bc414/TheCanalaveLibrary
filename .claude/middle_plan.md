# Middle Plan — The Canalave Library (MVP → Beta → Launch)

> Successor to `forward_plan.md` (now historical reference, the same treatment it gave
> `next_steps.md`). This is the live master plan, and it carries forward the "Decisions that
> need you" table and the Resolved index. `CLAUDE.md` remains the single source of truth for
> file paths, artifact names, and Stage semantics; `workplan.md` remains the work-unit ledger —
> new work-units are *sequenced* here and *recorded* there.

## Where you are (as of 2026-07-03)

The MVP build arc `forward_plan.md` governed is complete: Phases A–D done; every numbered
Phase-E work-unit (WU0–WU38 plus the cleanup/browser waves) built and green (`dotnet test`
1238/1238); the integrated app browser-verified feature-by-feature (WU-L45Pass, 2026-07-02 —
see the L4.5 column in `status.md`). A 2026-07-03 codebase sweep confirmed the grid matches
reality: every cell marked unbuilt is genuinely unbuilt (WU38a/b/c, WU39–43, the four Stage-1
designs), and the home page is a placeholder.

**The MVP cutoff is revised (settled 2026-07-03).** The original rationale for stopping at
L1–L4 (get community feedback in before building L5–L8 on top; see a partway finish line for a
big project) is superseded — implementation friction proved far lower than planned. The
formerly-post-MVP layers (L5 WASM, L6 indexes, L7 Redis, L8 marts) now land **before the beta**
(a small audience from the existing community). `grid_axes.md`'s "Two Boundaries" remain true
as *architecture* — L5–L8 stay additive behind contracts frozen in L1–L4 — only the
*scheduling* boundary moved. Accepted cost, eyes open: beta feedback that changes an L2/L3
contract afterward also touches its L5 endpoint + Http impl (mechanical, ~3 sites instead
of 1); L6/L7/L8 churn only on schema/signature changes. L5 WASM is deliberately last in the
pre-beta batch for exactly this reason.

## The shape of the remaining work

```
0. Hygiene → 0.5 Convention mini-pass → 1. MVP-surface completeness
      → 2. Full L4 sweep + Stage-6 freezes → 3. Beta-scope decisions
      → 4. Platform build-out (Aspire, L6, S3, L7, SignalR, L5, L8)
      → 5. Beta → 6. Launch (DigitalOcean)
```

Phases 2 and 3 can interleave with the tail of Phase 1; Phase 4's entries are strictly ordered
among themselves. The per-unit build loop (pick → read audit pointer → build → `dotnet build` +
`dotnet test` green → update `status.md`/`workplan.md`/audit Stage note) is unchanged from
`workplan.md`'s preamble.

---

## Phase 0 — Hygiene (do first, cheap)

- Commit the modified doc files sitting uncommitted in the working tree (the WU-L45Pass docs
  follow-up of 2026-07-02: two audit files, five convention skill files, `run-server/SKILL.md`,
  `workplan.md`).
- Merge `phase-a-foundation` → `master`. The branch is 25+ commits ahead and master contains
  nothing newer; the entire MVP lives unmerged. Settle the go-forward branch convention
  (decision row 5 below — default: feature branches off master, merge per work-unit).

## Phase 0.5 — Convention-settling visual mini-pass (early, small)

Brian + a live server over 3–4 representative screens: a deck page (`/discover`), a form page
(`/story/new`), the reading page, a settings/Identity page. Goal: surface taste-level
corrections and lock them into `layer4-style.md` Pattern Accumulation *before* Phase 1 builds
new features to those conventions. This is **not** a Stage-6 sweep — no freezes here; the
exhaustive freeze pass is Phase 2, run once on final surface. Rationale: corrections found late
propagate across every feature's markup; corrections found now propagate into code that doesn't
exist yet, for free.

## Phase 1 — MVP-surface completeness

Ordered: homepage first (the front door, and the only *unplanned* gap found in the 2026-07-03
sweep), then feature work-units by user-visible value. All deps are Stage 5; settled directions
live in the audit files named in each `workplan.md` entry.

1. **WU-Home** *(new work-unit — gap found 2026-07-03; no prior WU covers the real home page;
   `HomeDesktop`/`HomeMobile` are placeholders).* Spec §5.28's route table: `/` shows
   **Community Spotlight stories** — so this WU includes the Feature 55 spotlight *selection +
   display* slice. The curation model needs a design conversation first (decision row 2): who
   selects spotlighted content, on what cadence, stored how; plus what else the home shows
   (anonymous vs. logged-in). Donation infrastructure is explicitly out of scope here (Phase 3).
2. **WU41 Series, WU42 Story↔Story Relationships, WU43 Saved Tag Selections** — greenfield
   L2+UI; see their `workplan.md` entries.
3. **WU40 Manual Tree Search** — stateless pivot over live tables (settled WU28 Phase 0);
   distinct graph visualization, NOT `StoryDeck`. **Does not wait on the marts** (settled
   2026-07-03 — marts feed only F59/F61 and their workers).
4. **WU38a Account Deletion UI** — scope note: the service and the Identity-area page already
   exist and work (F52 L3/L3.5 Stage 5, exercised in WU1); this WU *surfaces* deletion from the
   product's own `/settings` UI, where today no link exists.
5. **WU-AccountEnforcement** *(promoted from `workplan.md`'s deferred follow-up)* — block
   Suspended (until `SuspendedUntilUtc`) / Banned users at login; Warned banner in layout
   chrome. Pairs naturally with WU38a (both Identity-adjacent).
6. **WU38b View Count** — L2 direct increment + first client ping. The Redis write-behind body
   swap arrives in Phase 4 behind the same signature.
7. **WU39 Story Import & Verification** — fills the `/mod/submissions` Imports tab, which is
   an explicit placeholder today.
8. **WU38c Export (epub/pdf)** — lowest launch value; last, may slip past beta without
   blocking anything.

## Phase 2 — Full L4 sweep + Stage-6 freezes, one open moderation decision

Runs *after* Phase 1 so freezes happen once, on final surface (WU43 reopens
`ResultsFilterPanel`, WU42 touches `StoryPage`, WU-Home is net-new chrome territory — freezing
those clusters first would just churn 6→5→6).

- **L4-Style freeze sweep** (Brian-driven): every feature at L4 = 1 pending visual sign-off
  (`status.md` rows 1, 3, 5, 11, 12, 31, 32, 34, 35, 36, 42, 43, 50, 52, plus whatever Phase 1
  built) and the Moderation rows 46–48 at L4 = 3 (build from spec). Per-cluster loop: render →
  fix → Pattern-Accumulate any new convention → Stage 5→6 freeze on sign-off. These are the
  project's first Stage-6 mints.
- **Non-story report-target rating routing** — the one open decision row carried verbatim from
  `forward_plan.md` (row 1 below); surface it when the moderation queue is under human review
  during this sweep.

## Phase 3 — Beta-scope decisions (design-or-defer)

The remaining Stage-1 undesigned features plus F56: **Story Arcs UI (8), Polls (37), Custom
Lists (51), Spotlight donation infrastructure (55 remainder — WU-Home takes the display
slice), Feature Contributions (56)**. Per feature: design it now (chat with skill files, per
CLAUDE.md's Stage-1 venue) or explicitly defer it past beta. None of these blocks Phases 4–5;
the point of this phase is that each one gets a *deliberate* verdict instead of drifting.

## Phase 4 — Platform build-out (pre-beta; formerly "post-MVP")

Order rationale: orchestration first (everything after runs inside it), cheap DDL next, then
storage and realtime, WASM last (largest contract-sync surface), marts at the end (they benefit
most from beta data but must exist before it).

1. **WU-Aspire** — the AppHost returns for orchestration: Postgres + Redis + MinIO resources,
   service discovery, connection-string wiring. Constraints to honor: plain `AddDbContext`
   stays (WU12's anti-pooling ruling holds — no Aspire Npgsql EF client package); the
   dev-without-Aspire path (`scripts/` + `run-server` skill) remains supported.
2. **L6 index batch** — regenerate the UserStoryInteraction filtered indexes off the
   re-modeled `has_started` columns (16/17 L6), comment golden index
   `(chapter_id, date_posted DESC)`, StoryTag reverse index. Pure DDL; contracts frozen.
   Pointers: per-folder audit L6 notes; `layer6-indexes.md`.
3. **S3/R2 image storage** — `S3ImageStorageService` (`AWSSDK.S3`) behind the frozen
   `IImageStorageService`; MinIO endpoint in dev (via Aspire), Cloudflare R2 in production.
   Settled 2026-07-03: this lands **before launch**. Pointer: `audit/ImageStorage.md`.
4. **L7 Redis** — write-behind (16/17 interaction writes, 45 view counts via `INCR`),
   ephemeral store (44 LastReadDate hash), read-side caches. Method-body swaps behind
   unchanged signatures. Governed by `layer7-redis.md`.
5. **SignalR messaging push** — settled WU35 design; first app-level Hub (`MessagesHub`).
   Needs a hub integration-test harness (none exists yet). Pointer: `cross-cutting.md`
   "Private Messaging Architecture".
6. **L5 WASM enablement** — endpoints + `Client*Service` impls from the frozen `IXService`
   contracts, including the two genuine mechanical Stage-4 cells (Story L5 endpoint wiring,
   Sprites L5). Deliberately last in this batch: once it lands, every subsequent L2 contract
   change also costs an endpoint + client-impl touch. Governed by `layer5-wasm.md` — battle-
   tested as of WU-L5Pilot (2026-07-04, Tags cluster: F11/F13/F34 L5 Stage 5), so this batch is
   pattern-application, not discovery. Shape (rollout strategy settled 2026-07-04, see Resolved):
   per-feature endpoint + client-pair builds, headless (Integration + Unit tiers); then the
   **single global `InteractiveAuto` flip** + one whole-site browser debug wave per
   `layer5-wasm.md` §"The Global Flip" (Routes.razor moves client-side there).
7. **L8 data marts + consumers + workers** — raw-SQL marts, no EF model (settled): then
   Automatic Tree Search (59), Also Favorited/Recommended (61), workers 57/58/60/62.
   Sparse-data results expected until beta traffic accumulates; that's fine — beta is what
   makes them meaningful. Pointers: `audit/Discovery.md` L8 notes, `audit/Moderation.md`
   Feature 62; `layer8-data-marts.md`.

## Phase 5 — Beta

Small audience from the existing community (logistics: decision row 6). Entry gate: Phases
0–2 and 4 done; every Phase 3 item resolved or explicitly deferred. Expectation written down
so nobody is surprised: L2/L3 changes from feedback are *normal and planned for*; each one now
also touches its L5 endpoint/client impl — an accepted, mechanical cost (settled 2026-07-03).

## Phase 6 — Production launch (DigitalOcean)

Topology settled 2026-07-03: one droplet running the server + Redis, DigitalOcean managed
PostgreSQL, Cloudflare R2 for images. Remaining unknowns stay as decision row 4: deploy
mechanism (manual vs. CI), TLS/domain, backup policy for managed PG, environment-config
strategy (user secrets → env vars).

---

## Decisions that need you

| # | Decision | Default (per spec/§0) | Why it's yours |
|---|----------|----------------------|----------------|
| 1 | **Non-story report-target rating routing** — how a T-only moderator's queue excludes non-Story M content. Blog posts carry their own `Rating` column (on `ProfileBlogPost`/`GroupBlogPost` child tables, not the `BaseBlogPost` root — EF-root query-filter wrinkle); recommendations derive one-hop from parent `Story.Rating`; chapter/blog-post comments derive two hops; profile/group comments and private messages are genuinely un-rated. Delivery: extend `GetReportQueueAsync` so non-story targets whose effective rating exceeds the mod's cap are dropped (not placeholder-labelled). Decide: (a) join-based scoping at query time in the `BatchLoadTargetsAsync` arms; or (b) a post-load rating check in the stitching step using a join or parent re-fetch. Also decide how to handle the EF-root child-table filter for blog posts. | Deferred from pre-integration cleanup (2026-06-26) — story-target rating scoping shipped; this is net-new behavior for the other target types. | Own work-unit; surface during the Phase 2 moderation-queue review. |
| 2 | **Homepage design, incl. the spotlight curation model** — who selects Community Spotlight content, on what cadence, stored how; what else `/` shows for anonymous vs. logged-in visitors. | Spec §5.28 route table says `/` = Community Spotlight stories; §5.26 has no curation mechanics. | Front-door product design and the revenue anchor's public face. Gates WU-Home (Phase 1 item 1). |
| 3 | **Beta scope for Story Arcs (8) / Polls (37) / Custom Lists (51) / donation infra (55 remainder) / Feature Contributions (56)** — design now or explicitly defer past beta, per feature. | None — these are the genuine Stage-1 intent gaps. | Product-scope judgment; each needs a design conversation (CLAUDE.md Stage-1 venue) if built. Phase 3. |
| 4 | **DigitalOcean deployment mechanics** — deploy mechanism (manual vs. CI), TLS/domain, managed-PG backup policy, env-config strategy. | Topology itself is settled (droplet: server+Redis; managed PG; R2). | Operational cost/effort trade-offs. Phase 6. |
| 5 | **Branch convention going forward** — `phase-a-foundation` carried the whole MVP; after the Phase 0 merge, what's the pattern? | Feature branches off `master`, merged per work-unit. | Your workflow preference. Phase 0. |
| 6 | **Beta logistics** — who, how many, invite mechanism, feedback channel. | None. | Community relationships are yours. Phase 5 gate. |

---

## Resolved

Newest first. Every entry points at the doc that now states the rule. Entries up to 2026-07-01
are carried verbatim from `forward_plan.md`.

- **L5 rollout strategy — single global flip, no long-lived mixed mode** — resolved
  (2026-07-04, WU-L5Pilot): per-feature endpoint/client-impl work lands incrementally and
  headlessly; the render-mode conversion to `InteractiveAuto` happens in one whole-site pass
  followed by one browser debug wave. `InteractiveAuto` requires dual implementations behind
  every reachable interface (no fallback for missing client DI), and mixed-mode pages cost UX
  degradations + a circuit-crash hazard for no early user value. The pilot's island directives
  on `/tags` were removed accordingly; the island recipe survives as a debugging/staged-rollout
  technique. Rule: `layer5-wasm.md` §"Rollout Strategy" / §"The Global Flip".

- **Revised MVP cutoff — L5–L8 land pre-beta** — resolved (2026-07-03): the L1–L4 scheduling
  boundary is retired; see "Where you are" above for rationale and accepted costs.
  `grid_axes.md`'s architectural boundaries are unchanged.
- **DigitalOcean launch topology** — resolved (2026-07-03): one droplet (server + Redis),
  managed PostgreSQL, Cloudflare R2. Mechanics still open (decision row 4). See Phase 6.
- **S3 image storage before launch** — resolved (2026-07-03): `S3ImageStorageService`, MinIO
  in dev via Aspire, R2 in prod, behind the frozen `IImageStorageService`. See Phase 4 item 3,
  `audit/ImageStorage.md`.
- **Aspire returns for orchestration** — resolved (2026-07-03): AppHost with Postgres + Redis +
  MinIO, pre-beta. The MVP-era pivot off Aspire (2026-06-20) governed the MVP only; WU12's
  anti-pooling ruling (plain `AddDbContext`, no Aspire EF client package) survives inside the
  orchestrated setup. See Phase 4 item 1.
- **Marts not required for Manual Tree Search** — resolved (2026-07-03, reaffirming WU28
  Phase 0): WU40 pivots statelessly over live tables; marts feed only F59/F61 + workers. See
  `audit/Discovery.md` Feature 33.
- **Community Spotlight display slice belongs to the homepage** — resolved (2026-07-03): spec
  §5.28 puts spotlight stories on `/`; F55 splits into selection + display (WU-Home) vs.
  donation infrastructure (deferred, Phase 3). See Phase 1 item 1.
- **Style-pass sequencing** — resolved (2026-07-03): hybrid — early convention-settling
  mini-pass over representative screens (Phase 0.5), exhaustive Stage-6 freeze sweep after
  Phase 1 on final surface (Phase 2).

- **Read-context lifetime under Blazor Server circuits (supersedes spec §6.6)** — resolved
  (2026-07-01, browser-debugging wave): `ReadOnlyApplicationDbContext` is registered via
  `AddDbContextFactory<…>(…, ServiceLifetime.Scoped)` and every read-service method creates its
  own short-lived context (`await using`). Spec §6.6's direct-injection rationale ("scoped DI
  addresses the thread-safety concern") holds for per-request scopes but not per-circuit scopes —
  layout chrome + page dispatchers query concurrently on one circuit, crashing a shared scoped
  context on every authenticated load. Compile-time read/write separation and scoped
  `IActiveUserContext` filter resolution are preserved; the write context stays plain scoped
  `AddDbContext`; WU12's anti-pooling ruling is unaffected. Convention: `layer2-services.md`
  "Read-Context Concurrency: Factory Per Method"; regression:
  `Tests.Integration/ConcurrentReadAccessTests.cs`.

- **Content-visibility filter placement** — resolved (2026-06-27, WU-FilterRevamp):
  All named display/visibility EF Core query filters (`"ContentRating"`, `"GroupAudience"`,
  `"IsTakenDown"`) live on `ReadOnlyApplicationDbContext.OnModelCreating` only. The write context
  (`ApplicationDbContext`) carries no filters and sees ground truth. A `readDb` bypass
  (`IgnoreQueryFilters`) is always a deliberate elevated read, annotated `// elevated read:`.
  Convention: `cross-cutting.md` "Content Rating Filtering."
- **Read context migration tree** — resolved (2026-06-27, WU-FilterRevamp):
  `ReadOnlyApplicationDbContext` owns no schema and has no migration tree. Deleted
  `Migrations/ReadOnlyApplicationDb/`. Future migrations always target `ApplicationDbContext`.
  Convention: `layer1-data-model.md` §"Two DbContexts."
- **HttpStory{Read,Write}Service (Client) dead-code removal** — resolved (2026-06-27, WU-FilterRevamp):
  Deleted. MVP is `InteractiveServer`-only. F4/F5 L5 reclassified `4 → 2`. Convention: post-MVP
  L5 WASM enablement section in `workplan.md`.

- **Sprite system redesign — full decision set** — resolved (2026-06-27, 8 decisions): Theme.Slug column; optimistic URL + onerror; singleton `OptimisticSpriteReadService` in Core; component-level resolution via `ThemeContext` + `ISpriteReadService`; `SpriteBaseUrl` config seam; assets provisioned out-of-band; `ISpriteAssetProbe` write-time checker; image-orphan fix. See `cross-cutting.md` "ThemeContext Cascading Provider", `layer2-services.md` "Sprite URLs Are Resolved At Render Time", `audit/ImageStorage.md`.

- **WU37 Story Tagging — architecture, scope split, naming** — resolved (2026-06-25): F9/10/15 carved to WU41/WU42/WU43; Character→`StoryCharacter` (not `StoryTag`); pairing→`StoryCharacterPairing`; `TagTypeEnum.Relationship` removed; service-layer enforcement only; `ApplyFilters` character branch. See `cross-cutting.md` "Structured Tag Authoring & Legality Enforcement", `layer2-services.md` "Structured Tag Authoring — Per-Type Filter Branch."

- **WU28 Discovery defaults + random-preload** — resolved (2026-06-25): `IDiscoveryDefaultsReadService` merges system defaults + sparse per-user overrides; random batch = stateless re-draw from post-filter set; F33 tree search carved to WU40. See `layer2-services.md` "Discovery Defaults + Random Batch", `audit/Discovery.md` Features 31/33.

- **WU36 Badges** — resolved (2026-06-25): synchronous inline `AwardAsync`; Recommender + RecommenderSilver tiers; `RecommendationSuccessesEarned` column; anti-self-farm guard. See `layer2-services.md` "Synchronous Inline Badge Awards", `audit/Badges.md` WU36.

- **WU34 Moderation — eight design decisions** — resolved (2026-06-25): soft-delete default; no auto-hide; `AccountStatus`+`SuspendedUntilUtc`; `ActiveReportCount` on User; `ReportedEntityId int→long`; dedup-key fix; `StoryApproved` notification type; WU34/WU39 scope split (F53 → WU39). See `cross-cutting.md` "Moderation Model", `layer2-services.md` "Notification Generation", `audit/Moderation.md` Feature 53.

- **Moderator role assignment in dev seed** — resolved (2026-06-24, WU27.5): role *rows* are already
  seeded via `ApplicationRoleConfiguration.HasData`. WU27.5 assigns `AdminUser` to both `"Moderator"`
  and `"Admin"` in `DataSeeder.cs` — role gate is now exercisable end-to-end. Admin-inheritance
  expressed by listing both roles (IsInRole is literal). See `cross-cutting.md` "Role-Based
  (Moderator) Gating."

- **WU32 Groups — five decisions** — resolved (2026-06-24): `AudienceRating`/`MaxContentRating` split; open join, permanent; Member+Admin only (no Moderator — permanent); group blog posts in WU32; per-context comment methods. See `cross-cutting.md` "Group Audience-Visibility Filter"/"Group Membership and Role Model", `layer2-services.md` "Group Rating Waterfall"/"Group Comments", `audit/Groups.md` WU32.

- **Active-user-conditional handling + two content-editing patterns** — resolved (2026-06-23): `IActiveUserContext` server-only; ownership = identity equality, inline `@if`; view/edit-page split for Story/Chapter; in-place inline for comments/recs/vouch. See `cross-cutting.md` "Active-User-Conditional Handling", `layer3.5-structure.md` "Owner-Conditional Edit Affordances."

- **`UserStoryInteraction` nomenclature rule** — resolved (2026-06-23, WU23 Phase 0): every identifier
  meaning *user×story interaction* must be spelled `UserStoryInteraction…`, never bare `Interaction…`.
  Full codebase sweep ran in WU23 Phase 0 (Tier 1: type/enum/entity renames; Tier 2: DB column
  renames via rename migration; Tier 3: member renames). Deliberate leave-list: `UserChapterInteraction`
  / `LastInteractionDate` (chapter domain); prose in comments/seeds. See `canalave-conventions/SKILL.md`
  "UserStoryInteraction prefix rule."

- **`StoryFilterDto` shape + `GetListingsAsync` two-step** — resolved (2026-06-23, WU23): DTO in
  `Core/Discovery/`; fields: `TextQuery`, `IncludedTagIds`, `ExcludedTagIds`,
  `ExcludedInteractions (UserStoryInteractionTypeEnum list)`, `Sort`, `Page`, `PageSize`. Content
  rating and Source axis excluded by design. `GetListingsAsync(StoryFilterDto)` in
  `IStoryReadService` / `ServerStoryReadService` uses the two-step pattern (filter IQueryable → scalar
  IDs → `GetListingsByIdsAsync`). See `canalave-conventions/layer2-services.md`
  "StoryFilterDto + GetListingsAsync."

- **`ResultsFilterPanel` composition + axis extraction** — resolved (2026-06-23, WU23): filter axes
  (`TagFilter`, `UserStoryInteractionFilter`) are the unit of reuse — extracted as standalone
  components; `ResultsFilterPanel` is one assembler. Panel + StoryDeck kept separate at page level
  (spec §5.27 rejected a bundled composite). Both panel and tree search use a batched Apply button.
  See `canalave-conventions/layer3.5-structure.md` "Filter-Axis Component Pattern."

- **§8.7 entity renames** — resolved (2026-06-23, WU23 Phase 0): `UserInteractionFilter` →
  `UserStoryInteractionFilterType`, `DefaultSearchSetting` → `DefaultUserStoryInteractionFilterSetting`,
  `UserSearchSetting` → `UserStoryInteractionFilterSetting`. Real rename migration (no pinning). See
  `audit/Discovery.md` "WU23 Shared Context."

- **`AllowInteractions` → `SocialInteractionPermission`** — resolved (2026-06-23, WU23 Phase 0):
  disambiguates from `UserStoryInteraction`. C#-only; column names unchanged. See `audit/Discovery.md`.

- **Notification generation mechanism** — resolved (2026-06-23): semantic per-event methods injected into write services; best-effort post-commit; private create-core owns drop-self + dedup. See `cross-cutting.md` "Notification Creation", `layer2-services.md` "Notification Generation."

- **Notification in-app toggle dropped (§5.18 deviation)** — resolved (2026-06-23, WU22): the spec
  §5.18 "in-app toggle" is not implemented. `UserNotificationSetting` stores only `EmailEnabled` and
  `Collapsed`; in-app delivery is always-on (after drop-self, dedup). No `InAppEnabled` column will be
  added. Deviation recorded in `audit/Notifications.md` (both what the spec said and what changed/why).

- **`Story.ChapterCount`** — resolved (2026-06-22, WU17): **not a denormalized column.** A count of
  published chapters is computable via `c.Chapters.Count(ch => ch.IsPublished)` in any EF projection,
  translating to a correlated `COUNT(*) WHERE is_published` subquery Postgres handles efficiently.
  Denormalizing would require maintenance in `CreateChapterAsync`, `SetPublishedAsync` (both directions),
  and any future delete — with no accuracy benefit. `Chapter.IsPublished` already captures the
  published/unpublished distinction; the filter is free at query time. If the subquery becomes a hotspot
  in listing queries, the remedy is an L6 partial index on `(story_id) WHERE is_published`, not a
  counter column. See `audit/Chapters.md` Feature 6 L2 Stage note.

- **`SiteDailyStat`/`DailyStoryStat`** — resolved: raw-SQL marts, no EF model, matching the other three
  Layer-8 marts (`AlsoFavoritedScore`, `AlsoRecommendedScore`, `UserStoryTreeSearchEntry`). `DailyStoryStat`
  was dropped entirely. See [audit/Moderation.md](audit/Moderation.md) Feature 62 and
  [audit/Discovery.md](audit/Discovery.md)'s Layer-8 implementation notes (schema preserved there for all
  four marts together).
- **JSON settings mapping** — resolved: `ComplexProperty(...).ToJson()`, migrated off the older
  `OwnsOne(...).ToJson()` approach. See [audit/Identity.md](audit/Identity.md) Feature 1 and
  [layer1-data-model.md](skills/canalave-conventions/layer1-data-model.md) §"JSON Complex Types."

- **`IEntityTypeConfiguration<T>` extraction** — resolved: extracted now (before the first migration),
  not deferred. One `{Entity}Configuration` class per entity, files grouped one-per-folder-cluster, but
  **all colocated** in `TheCanalaveLibrary.Server/Data/Configurations/` (not split into the feature
  cluster folders — that's reserved for service impls, a different edit-locality concern). See
  [layer1-data-model.md](skills/canalave-conventions/layer1-data-model.md) §"Fluent API Organization" and
  [audit/Lookups.md](audit/Lookups.md) item 6.
- **Vouches L1 shape** (§8.13) — resolved Phase B (2026-06-20): dedicated `Vouch` table with optional
  `VouchText`, `MaxLength(1000)` (not the spec's proposed 280 — code is authoritative, spec not edited).
  Was already implemented in Phase A's migration; the audit/status framing was stale, not the decision
  itself. See [audit/Following.md](audit/Following.md) Feature 19.
- **Hidden Gem at-limit behavior** (§8#4) — resolved Phase B (2026-06-20): reject + remove-first at the
  5-item limit; no atomic swap, no auto-evict. See [audit/Recommendations.md](audit/Recommendations.md)
  Feature 29.
- **Recommendation minimum length** — resolved WU29 (2026-06-23): **500 characters**, measured on
  HTML-stripped, entity-decoded plain text (same strip helper as `ChapterText.CountWords`). No value
  appeared in the spec (§5.6 only says "substantive, multi-paragraph"); 500 is the standing constant in
  `RecommendationConstants.MinLength`. See [audit/Recommendations.md](audit/Recommendations.md) Feature 27
  and [layer2-services.md](skills/canalave-conventions/layer2-services.md) §"Recommendation Write Conventions".
- **Recommendation approval lifecycle for MVP** — resolved WU29 (2026-06-23): new recommendations are
  written directly as **Approved** (`StatusId = Approved`) so the display surface is exercisable before
  the moderation queue is built. Spec §5.6's Pending→author-approval/moderator-review lifecycle is deferred
  to WU34 (Moderation). Code is authoritative; spec is a read-only snapshot. See
  [audit/Recommendations.md](audit/Recommendations.md) Feature 27.
- **Tailwind version + build tooling** (Phase C) — resolved Phase C (2026-06-20): **Tailwind v4**,
  CSS-first config (`@theme` block in `TheCanalaveLibrary.Server/Styles/app.css`), not the spec
  §2.1-era `tailwind.config.js` model. Build via **npm + an MSBuild target** invoking the v4 CLI
  (`Bash(npm *)` needed going forward). Color palette: green, rooted in Pokémon Gen 4/5 (Torterra,
  GBA/DS-era grass textures) — explicitly not blue. Font-scope rule: Tailwind fonts cover site chrome
  only; `RichTextView`/`RichTextEditor` (all user-generated content) use the user's `ReaderSettings`
  font instead. See [layer4-style.md](skills/canalave-conventions/layer4-style.md) §"Prerequisite:
  Design Tokens" and §"Reader Settings as CSS."
- **Aspire orchestration during MVP dev** — resolved (2026-06-20, narrowed WU12): AppHost deferred for MVP; Aspire Npgsql EF client package removed (pooling incompatible with Scoped `IActiveUserContext`); plain `AddDbContext` is permanent (holds in production too). See `layer2-services.md` "DbContext Registration."
- **Interaction-icon design** (Feature 16 L4, previously Stage-1 blocked) — resolved WU7 (2026-06-21):
  inline SVG shapes, not theme-swappable sprite URLs — a permanent, deliberate carve-out from the
  "never inline SVG" rule (which still governs tags/covers/avatars). Square button, three visual
  states (gray inactive → accent-fill-on-hover → inverted accent-background/white-shape when active).
  `UserStoryInteractionButton` takes `IconPath`/`AccentColor` `[Parameter]`s and stays dumb; the
  `InteractionTypeEnum → (IconPath, AccentColor)` mapping is left for the owning composite (WU16).
  Supersedes the WU2-era `GetInteractionIcon`/sprite-key plan. See
  [layer4-style.md](skills/canalave-conventions/layer4-style.md) §"Interaction Icons Are Inline SVG"
  and [audit/UserStoryInteractions.md](audit/UserStoryInteractions.md) Feature 16.
- **WU26 chapter routes, versioning, rating** — resolved (2026-06-24): `/story/{id}/{ch}[/{versionOrder}]`; edit routes use `/chapter/`; version token = SortOrder; progressive disclosure UX; `ChapterContent.Rating?` nullable. See `cross-cutting.md` "Chapter Versioning — Progressive Disclosure."

- **WU33 Notification UI** — resolved (2026-06-24): rich flat DTO + normalized target pair; two-pass batch enrichment; grouped + flat feeds; bell flyout (UserCard caret pattern); per-row settings save. See `layer2-services.md` "Polymorphic RelatedEntityId", `layer3.5-structure.md` "Notification Presentation Model", `audit/Notifications.md` Feature 42.

- **WU30 Profiles + theme-selection — seven decisions** — resolved (2026-06-24): `IUserSettingsService` self-referential exception; UserStats counter wiring (transition-delta rule); profile comment wall as 4th `CommentSection` context; tabbed page shape; blog-tab owner/viewer distinction + `GetByAuthorAsync` extension; `IThemeReadService.GetThemesAsync`; `Profiles/` cluster added. See `layer2-services.md` "Self-Referential Editing Exception", `cross-cutting.md` "UserStats Updates", `layer3.5-structure.md` "Profile Page Composition"/"CommentSection".

- **Integration test isolation foundation** — resolved (2026-06-24): Respawn reset + `IntegrationTestBase` + GUID-suffixed seeding across all 19 classes; serial execution deliberate. See [canalave-conventions/testing.md](skills/canalave-conventions/testing.md) §"Integration tests reset between every test."

- **WU31.5 TPT denormalization** — resolved (2026-06-24): discovery/date columns base→child; named filter removed from `BaseBlogPost`; change-tracker stub delete. See `layer1-data-model.md` §"Denormalization with TPT", `audit/BlogPosts.md` Feature 35, `audit/Comments.md`.

- **WU35 Messaging architecture** — resolved (2026-06-24): 1-on-1 only; stateless MVP, SignalR post-MVP; global unread badge in chrome; no PM Notification rows (watermark only). See `cross-cutting.md` "Private Messaging Architecture", `audit/Messaging.md` WU35.

- **WU31 Blog Post** — resolved (2026-06-24): F56 deferred; edit-page pattern for blog posts; `GroupBlogPost` UI in WU32; optional story-link picker via `GetStoryIdsByAuthorAsync`; content-rating filter on `BaseBlogPost`; `{*slug}` cosmetic only. See `audit/BlogPosts.md` Features 35/36/56, `cross-cutting.md` "Two content-editing patterns."

- **Test strategy** — resolved (2026-06-22, updated post-WU12.5): three tiers by kind — Unit (directly-constructed, no host/DB), Integration (Testcontainers Postgres + `WebApplicationFactory` + `IActiveUserContext` fake), RazorComponents (bUnit); never EF InMemory/SQLite. See [canalave-conventions/testing.md](skills/canalave-conventions/testing.md).

