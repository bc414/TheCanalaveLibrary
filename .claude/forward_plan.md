# Forward Plan — The Canalave Library

> Successor to the last-gen `next_steps.md` + `step4/5/6`. Those are kept only as historical reference;
> this is the live plan. It picks up **after Step 3 (classification)** — the audit is complete and on disk.

## Where you are

Steps 1–3 of the original arc are done:
- **Step 1 (conventions):** `.claude/skills/canalave-conventions/` — SKILL.md + 10 layer files.
- **Step 2 (axes):** `.claude/grid_axes.md`, `.claude/folder_clusters.md`.
- **Step 3 (classification):** `.claude/status.md` (62-feature × 9-layer grid), `.claude/audit-summary.md`,
  `.claude/audit/<FolderName>.md` ×21.

Everything below is the road from "we know the state" to "features are built."

## Two rules that govern this whole plan

1. **`CLAUDE.md` is the single source of truth** for file paths, artifact names, and Stage semantics.
   This plan *references* it; it does not restate it. (Restating-then-drifting is what produced every
   contradiction we found in the last-gen files — don't reintroduce it.)
2. **Spec supersedes stale code, unless the code is demonstrably working** (`audit-summary.md` §0). The
   spec is the recent consolidation; the ~7-month-old code is mostly non-working. Where they disagree,
   build to spec and treat the existing code as salvage — *except* where it actually functions and matches
   intent (Stories L1/L2, Sprite/Theme L1, the partition trio).

## The shape of the remaining work

The audit reorders the priorities the old plan assumed. Stage 1 is small and peripheral; the real backlog
is **foundational Stage-4 stale-code re-models + an unverified build**. So the sequence is:

```
A. Fix the foundation  →  B. Resolve blocking Stage-1  →  C. Lock styling tokens
        (data model + build-green)         (small)              (parallel track)
                                   ↓
            D. Produce the atoms-first workplan  →  E. Build per workplan
```

Phases A–C clear the prerequisites; D sequences; E executes. C runs in parallel with A/B.

---

## Phase A — Fix the foundation, then take the first migration

**Goal:** a correct data model, a green build, and a migration that applies — so nothing downstream is
built on stale columns or an unproven schema.
**Tool:** Opus or Sonnet in Claude Code (code-writing). **Inputs:** `audit-summary.md` §2–§3,
`.claude/audit/{UserStoryInteractions,Lookups,Comments,Discovery,Identity}.md`, `layer1-data-model.md`.

This is the audit's #1 gap: **no migrations exist and the build is unverified.** Do the blocking
re-models *before* the first migration so the initial schema is born correct (the layer-1 skill's
"pre-launch: nuke and rebuild" applies — there's no DB to preserve).

**A1 — Reconcile the foundational Stage-4 stale-code traps (spec wins, direction known):**
- **Reading status** (`UserStoryInteraction.cs` + `ApplicationDbContext`): add `HasStarted`; drop
  `IsInProgress`/`IsActivelyReading`; retire vestigial `ReadStatus`/`FavoriteStatus` enums
  (`ModelEnums.cs`) and `UserStoryInteractionFilters.InProgress`; regenerate the 7 filtered indexes off the
  corrected columns. (§4/§5.12.)
- **Search/sort vocabulary** (`SiteConstants.cs`, `SearchMode` seed, `DefaultSortOrder` enum): conform to
  the three-axis model (§5.3) — `SearchPage/TreeSearch/AutoTreeSearch/AlsoFavorited/AlsoRecommended/
  Profile*`; sorts `Random/DatePublished/Relevance/Score`; complete the `DefaultSearchSetting` matrix.
- **Comment likes** (`Comments.md`): replace the implicit EF many-to-many with the explicit `CommentLike`
  junction (§6.11).
- **Data marts** (`Discovery.md`): per §0 + `layer8`, marts should have **no EF model**. Remove the
  `DbSet`/config for `AlsoFavoritedScore`, `AlsoRecommendedScore`, `UserStoryTreeSearchEntry` — *pending
  your decision below on `SiteDailyStat`*.

**A2 — Clear the build-blockers / template debris:**
- Delete leftovers: `Class1.cs`, `Component1.razor(.css)`, `RandomNumberGenerator.razor`,
  `ExampleJsInterop.cs` + `exampleJsInterop.js` (confirm unused first).
- Fix the Identity post-move references: normalize `namespace ...Components.Account` →
  `...Identity` (or add `@namespace`), and correct the `App.razor` asset path
  (`Components/Account/Shared/PasskeySubmit.razor.js` → `Identity/Shared/...`).

**A3 — Take the migration and prove it:**
- `dotnet ef migrations add InitialSchema --context ApplicationDbContext`
- `dotnet build` (green), then apply against the Aspire-orchestrated Postgres and run `DataSeeder`.
- Add the manual migration edits EF won't generate where already implied (the OC-detail trigger on
  `StoryCharacter`, any CHECK constraints) — or log them as follow-ups.

**A4 — Update the artifacts:** advance the re-modeled L1 cells in `.claude/status.md` (Stage 4 → 5) and
note the resolution in the relevant `.claude/audit/<Folder>.md`.

**Gate before moving on:** `dotnet build` is green; the migration applies cleanly; the seeder runs; the app
starts and Identity pages load. This is the moment every "Stage 5 at L1, awaiting verification" becomes real.

**Addendum (2026-06-20):** Phase A is code/schema-complete — `InitialSchema` is generated, `dotnet build`
is green, template debris is cleared, Identity namespaces are normalized. It is **not yet
runtime-verified**: the migration hasn't been applied to a live Aspire-orchestrated Postgres,
`DataSeeder` hasn't run, app boot and Identity-page load aren't confirmed, and the NuGet package set
hasn't been audited. This is your own next action — close it before treating Phase D's "work-unit zero
already executed" framing (below) as fully proven.

---

## Phase B — Resolve the blocking Stage-1 gaps (only)

**Goal:** clear the few Stage-1 cells that sit on the dependency chain; defer the rest.
**Tool:** Sonnet in chat for *conceptual* gaps (skill files as context); Claude Code for *code-relationship*
gaps. **Inputs:** `audit-summary.md` §4 (Stage-1 landscape).

Stage 1 is only ~5% and almost all leaf/peripheral (Polls UI §8.6, Story Arcs UI §8.2, Hidden Gem limit
§8.4, Custom Lists §8.7, Spotlight §5.26). **Resolve only what blocks something ready to build now** —
chiefly **Vouches L1** (§8.13, a real Layer-1 decision) since it gates the Following cluster. The leaf UI
gaps can wait for their turn in the workplan. Update `status.md` + the audit file as each resolves; a
resolved conceptual gap becomes Stage 2 (or Stage 3 if the conversation produced a build-ready spec).

**Gate:** foundational/mid-chain Stage-1 cells are resolved; leaf cells may remain (note them, don't block).

---

## Phase C — Lock the styling foundation (parallel track)

**Goal:** unblock the entire L4-Style column (currently 100% Stage 1 — Tailwind isn't even installed).
**Tool:** Claude Code + your design input on tokens. **Runs in parallel with A/B.**

- Install Tailwind v4 into the build (`package.json` + npm dev deps + an MSBuild target invoking the
  v4 CLI; CSS-first config — tokens in `Styles/app.css`'s `@theme` block, not `tailwind.config.js`).
- Lock the design tokens (palette, type scale, spacing, the Pokémon theme) — this is the human-driven
  decision the whole Style column waits on.
- Decide the Bootstrap exit: existing components (`StoryPropertiesForm`, `TagSelector`, Identity scaffold)
  are Bootstrap and will be **restyled, not just styled**, when their L4 cells come up. Phase C itself
  removes only the dead `_Layout.cshtml` Bootstrap `<link>`, not component class names.

**Gate:** tokens are locked in `Styles/app.css`'s `@theme` block and the build emits Tailwind CSS. Until
then, every L4 cell stays Stage 1 — and that's expected.

---

## Phase D — Produce the atoms-first workplan

**Goal:** `.claude/workplan.md` — ordered work-units, each naming cell(s), tool, and an
`.claude/audit/<Folder>.md` pointer (schema per CLAUDE.md). **Tool:** Opus in Claude Code.
**Inputs:** post-Phase-A `status.md` + all audit files, **spec §9.2** (Atoms → Integration Points →
Consumers), and `audit-summary.md` §5 (the universal-component inventory).

Ordering rules (corrected from the last-gen step5):
- **Topological, not stage-gated.** A cell's dependencies must appear *earlier in the workplan* (so they're
  at Stage 5 by the time you reach it) — not "already Stage ≥3 at planning time," which nothing satisfies
  yet.
- **Phase by §9.2:** universal leaf atoms first (`TagChip`, `StoryCard`, `UserStoryInteractionButton`,
  `RichTextView`), then composites (`StoryDeck`, `EditorView`, `ResultsFilterPanel`,
  `UserStoryInteractionPanel`, `ChapterNavigation`, `CommentSection`, `ConfirmDialog`), then page/dispatchers and
  consumers.
- **Stage 4 → use the resolved direction.** Per §0, Stage-4 cells are stale-code traps resolving to Stage 2
  (build to spec); sequence them by that implied stage, and flag the code as discard-not-reuse so a building
  session doesn't preserve it. (The rare working-code exception, e.g. Sprites naming, is a light rename.)
- **Stage 3 is minted here-and-after, not found.** Expect ~0 Stage-3 cells at the start; opusplan passes in
  Phase E *create* them by locking atom contracts, after which consumers flip 2→3.
- **Foundational re-models already done in Phase A** lead the plan's data dependencies; the migration/build
  pass is effectively work-unit zero (already executed) — schema/build-wise. Runtime verification (migration
  applied live, seeder run, app boots) is still open; see Phase A's addendum.
- **Genuine intent-gap Stage-1 cells** (rows 8, 37, 51, 55 — Story Arcs, Polls, Custom Lists, Spotlight) go
  in a "blocked/deferred" section with no sequence number.
- **L4-Style Stage-1 cells are a different case — do not defer them.** `layer4-style.md`'s locked tokens +
  Leaf/Composite/Page tier rules already constitute a validated generic spec, and nothing downstream
  depends on a component's styling resolving. So these cells don't get a sequence number of their own at
  all: each one folds into the same work-unit as its feature's L3/L3.5 cell (see Phase E). Only the four
  rows above (where the underlying feature's UI isn't designed yet) keep their L4 cell genuinely deferred.

**Gate:** read the preamble — the ordering should put atoms before composites before consumers, with nothing
depending on something later.

**Addendum (2026-06-20):** Phase D complete — `.claude/workplan.md` written. 38 numbered work-units across
three phases (Phase 1 atoms WU1–11, Phase 2 composites WU12–22, Phase 3 pages WU23–38), plus WU0
(Phase A foundation, runtime verification still open), a blocked/deferred section (rows 8/37/51/55), and a
post-MVP Layers-5–8 batched section. Scope call recorded in the preamble: per `grid_axes.md` §"The Two
Boundaries" + the resolved MVP/Aspire decision, the **numbered sequence is Layers 1–4 only**; L5–L8 are
gathered post-MVP, not dropped. Read the workplan preamble before starting Phase E — if that scoping is
wrong it reshapes the sequence.

---

## Phase E — Build per workplan

**Goal:** working, convention-conformant code, one work-unit at a time.
**Tool per cell (per CLAUDE.md Per-Stage Guidance):** opusplan for Stage 2, Sonnet in Claude Code for
Stage 3 (once minted), Opus for any residual Stage 4. **Relax permissions first** (allow `.cs`/`.razor`/
`.csproj` writes — see config below).

Loop: pick the next entry → read its `.claude/audit/<Folder>.md` pointer → invoke the entry's tool →
build + verify (`dotnet build` + `dotnet test` green; add asserted tests for any new testable surface per
`canalave-conventions/testing.md`'s tier rules; run the relevant slice) → update `.claude/status.md`
(cell → Stage 5) and `.claude/workplan.md` (entry complete). Record the covering test tier (Unit /
Integration / RazorComponents) or state why none applies in the audit Stage note. The conventions skill
loads automatically as the paradigm-correctness guardrail.

**L4-Style within a work-unit:** per Phase D, a feature's L4-Style cell is not sequenced separately — when
a work-unit's L3/L3.5 build is Stage 3 (or implied-Stage-3 per a resolved Stage-4 direction) and tokens are
locked (the post-Phase-C default), Sonnet writes the component's markup and its Tailwind classes in the
same pass, from `layer4-style.md`'s tier rules and tokens, not as a later or separately-invoked step. For
the four rows still genuinely Stage-1 (8, 37, 51, 55), leave L4 untouched until that feature's gap resolves
elsewhere.

"Build + verify" for any cell touching L4-Style means more than `dotnet build` green — run the app and
visually inspect the rendered component. If the render doesn't match `layer4-style.md`'s tier/token rules,
that's a Stage 4 moment (diagnose and reconcile, not a build failure): fix and re-render. If the fix reveals
a new convention, write it into `layer4-style.md`'s "Pattern Accumulation" section in the same work-unit.
Mark the cell Stage 5, not Stage 6, until you've personally looked at the rendered result — Stage 6
("human-verified and frozen") needs your visual sign-off, not just the agent's self-assessment that it
compiles.

Guardrails:
- **opusplan:** feed the audit file's settled constraints as explicit "do not revisit." If the plan proposes
  changing a settled constraint, that's a misclassification signal — stop and flag.
- **Spec supersedes:** for any Stage-4 entry, the existing code is reference-for-what-to-replace, not a
  design to preserve.
- **Conventions are living:** if implementation reveals a convention that should change, update the skill
  file rather than silently diverging — this is exactly what happens when L4-Style's "Pattern Accumulation"
  step above fires.

---

## Decisions that need you (resolve before/early in Phase A)

| Decision | Default (per spec/§0) | Why it's yours |
|----------|----------------------|----------------|
| **Non-story report-target rating routing** — how a T-only moderator's queue excludes non-Story M content. Blog posts carry their own `Rating` column (on `ProfileBlogPost`/`GroupBlogPost` child tables, not the `BaseBlogPost` root — EF-root query-filter wrinkle); recommendations derive one-hop from parent `Story.Rating`; chapter/blog-post comments derive two hops; profile/group comments and private messages are genuinely un-rated. Delivery: extend `GetReportQueueAsync` so non-story targets whose effective rating exceeds the mod's cap are dropped (not placeholder-labelled). Decide: (a) join-based scoping at query time in the `BatchLoadTargetsAsync` arms; or (b) a post-load rating check in the stitching step using a join or parent re-fetch. Also decide how to handle the EF-root child-table filter for blog posts — note: adding a named query filter on `BaseBlogPost` at the root is possible but the `Rating` column lives on the discriminated child tables, requiring a `(b as ProfileBlogPost).Rating` or a shadow-column approach. | Deferred from pre-integration cleanup (2026-06-26) — story-target rating scoping shipped; this is net-new behavior for the other target types. | Own work-unit; surface when moderation queue is under human review. |

**Resolved:**

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

---

## Practical setup (corrected)

**Permissions** — the last-gen `settings.json` pointed at root paths and would deny every real write. Use
`.claude/`-relative paths. Phases A/C/E write code; B/D are markdown-only.

```json
{
  "permissions": {
    "allow": [
      "Read(**)", "Glob(**)", "Grep(**)",
      "Bash(dotnet build*)", "Bash(dotnet test*)", "Bash(dotnet ef*)", "Bash(dotnet run*)",
      "Write(.claude/**)", "Edit(.claude/**)"
    ],
    "deny": []
  }
}
```
For Phases A/C/E, also allow `Write`/`Edit` on `**/*.cs`, `**/*.razor`, `**/*.csproj`, `package.json`,
`Styles/app.css`, plus `Bash(npm *)`. Keep them denied during B/D if you want a hard read-only-on-code
guarantee.

**Platform notes (this machine):**
- Shell is **PowerShell**, not bash — `$env:ANTHROPIC_API_KEY` (not `echo $VAR`), `New-Item`/`Remove-Item`
  (not `mkdir -p`/`rm`). The Claude Code Bash tool also runs Git Bash if you prefer POSIX syntax.
- Default branch is **`master`**, not `main`. Snapshot before Phase A:
  `git switch -c pre-build-snapshot && git add -A && git commit -m "snapshot before build phase" && git switch master`.
- Confirm `ANTHROPIC_API_KEY` is unset so Claude Code uses your Pro subscription, not API billing.

**Usage/model:** Opus is 1× usage; Phases A, D, and Stage-4 work in E lean on it — check `/usage` for a
fresh 5-hour window before long sessions. Phase B (chat) and markdown bookkeeping are light.

## If something goes wrong

- **Session ends mid-phase:** on-disk artifacts are the checkpoint. Open a fresh session, point it at
  `.claude/status.md` + the relevant audit file, continue.
- **A Stage turns out wrong:** reclassify and re-route per CLAUDE.md's per-stage guidance — "stop and flag,"
  don't improvise.
- **The spec evolves again:** re-run classification for the *affected* cells only, and update `status.md` +
  the audit file. The audit reflects the spec as of this pass.
- **An atom's contract shifts during E:** that's expected — it flips its consumers from Stage 2 to Stage 3.
  Update their cells when it lands.
