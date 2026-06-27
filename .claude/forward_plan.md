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

- **WU37 Story Tagging — architecture, scope split, and naming** — resolved (2026-06-25, WU37
  scoping):
  (1) **Scope split.** Features 9 (Series), 10 (story↔story Relationships), 15 (Saved Tag
  Selections) carved from WU37 into WU41/WU42/WU43. Each is independently L1-settled with no
  design coupling to Feature 12.
  (2) **Shared catalog / differentiated association.** `Tags` table stays unified; per-story
  routing is differentiated: Genre/ContentWarning/CrossoverFandom → `StoryTag`; Setting →
  `StoryTag` + optional `SettingDetail`; Character → `StoryCharacter` (never `StoryTag`); pairing
  (ship) → `StoryCharacterPairing` (new name). Character stays in the catalog because it is the
  primary user of sprite/hierarchy/`IsFanon`/`AllowOCDetails` and the §14 fanonize flow.
  (3) **`TagTypeEnum.Relationship` removed.** A pairing is not a catalog tag; its name derives
  from its members. Last enum value; no renumber. `TagType` seeded row dropped by migration.
  (4) **Naming disambiguation** — `StoryCharacterRelationship` renamed to `StoryCharacterPairing`
  (story-scoped; parallels `StoryCharacter`; eliminates near-collision with Feature 10's unrelated
  story-to-story `StoryRelationship` / `StoryRelationshipType`). Shadow join promoted to first-class
  `StoryCharacterPairingMember`. Enum `CharacterRelationshipType` → `CharacterPairingType`.
  See `cross-cutting.md` "Structured Tag Authoring & Legality Enforcement."
  (5) **2-value `TagPriority`.** Keep existing `{ Primary=0, Supporting=1 }`; Primary default;
  no `None`, no renumber. ContentWarning gets no priority picker — enforced at service layer.
  (6) **Service-layer enforcement only.** OC-gate, SettingDetail-gate, ContentWarning-priority
  coercion, pairing-member count — all via `StoryValidationException` in `ServerStoryWriteService`.
  The spec's SQL-Server-era `TR_StoryCharacters_EnforceOCLogic` trigger is superseded; a DB
  CHECK is post-MVP defense-in-depth if wanted.
  (7) **`ApplyFilters` character branch.** Because Character leaves `StoryTag`, discovery filter
  must partition included/excluded ids by `TagTypeId` and route Character ids to
  `s.StoryCharacters.Any(...)`. See `audit/Discovery.md` Feature 31 and `layer2-services.md`
  "Structured Tag Authoring — Per-Type Filter Branch."

- **WU28 Discovery defaults + random-preload design** — resolved (2026-06-25, WU28 planning):
  (1) **§8.7 default-settings matrix read path** ("complete the `DefaultSearchSetting` matrix"): the
  read/merge service (`IDiscoveryDefaultsReadService`) is the WU28 Phase 1b deliverable. System
  defaults overlaid with sparse per-user overrides in C#; anonymous viewers see defaults only. Seed
  stays as-is (Ignored=true on the 5 discovery surfaces). This closes the "deferred post-WU23" item
  in `audit/Discovery.md`.
  (2) **Random-preload / "give me more" pagination**: random batch = a plain random selection out of
  the post-filter valid set (`OrderBy(Random()).Take(batchSize)`). No shown-id tracking, no dedup —
  "give me more" is a stateless re-draw that appends to the display list (repeats acceptable).
  Interaction exclusions come from the §8.7 defaults, not random-specific logic. Sorted modes
  (DatePublished/Relevance) use offset pagination. StoryDeck pagination is suppressed in random mode.
  (3) **Feature 33 tree search**: carved into WU40 (stateless pivot / four clean edges direction settled).
  See `audit/Discovery.md` Feature 31 and Feature 33; `canalave-conventions/layer2-services.md`
  "Discovery Defaults + Random Batch" section.

- **WU36 Badges — mechanism, scope, and Tastemaker tiers** — resolved (2026-06-25, WU36 planning):
  (1) **Mechanism:** synchronous inline award-check; `IBadgeWriteService.AwardAsync` (idempotent,
  best-effort try/catch after primary `SaveChangesAsync`). Background worker is post-MVP.
  (2) **Scope in WU36:** one live award trigger only — Recommender / "Tastemaker." All other badges
  deferred to their source-feature WUs.
  (3) **New `UserStat` column:** `RecommendationSuccessesEarned` (int, author-side). Do not reuse
  `RecommendationsFoundUseful` (reader-side).
  (4) **Two tiers:** `SiteBadges.Recommender` (threshold 10, existing) + `SiteBadges.RecommenderSilver`
  (threshold 50, new constant + seed row in WU36 migration).
  (5) **Default visibility on award:** `DisplayOrder = max+1` (visible by default; curation UI lets
  users hide/reorder; `UserCard.razor` caps to 3 badges).
  (6) **Anti-self-farm:** `RecordSuccessAsync` increments/awards only when `RecommenderId != null &&
  RecommenderId != userId`.
  See `canalave-conventions/layer2-services.md` "Synchronous Inline Badge Awards" and
  `audit/Badges.md` "WU36 Settled Decisions."

- **WU34 Moderation — eight design decisions** — resolved (2026-06-25, WU34 planning):
  (1) **Content removal:** soft-delete default (`IsHidden + DateModeratedRemoved + ModerationRemovalReason`,
  reversible, author notified) across Story/BaseComment/BaseBlogPost/Recommendation; separate explicit
  hard-delete path for illegal content (CSAM/piracy). Rationale: archive mission — mistakes are reversible,
  authors deserve a reason. See `cross-cutting.md` "Moderation Model."
  (2) **No auto-hide.** `ActiveReportCount` is a mod-only triage sort key / queue badge — never an automatic
  action trigger. Deliberations' "3 distinct reporters in 24h" rule dropped (brigading risk). Report counts
  are mod-only (no public counter). See `cross-cutting.md` "Moderation Model."
  (3) **Account actions: state + notify now; login enforcement staged.** Add `AccountStatus` (Active/Warned/
  Suspended/Banned — **no Shadowbanned**) + `SuspendedUntilUtc` to `User`. Warn/suspend/ban set status,
  record on `Report`, send notification. Login-blocking enforcement is a dedicated follow-up WU (see
  workplan.md deferred note after WU39). Shadowban permanently rejected — deception-as-moderation,
  contradicts §13 transparency philosophy. See `cross-cutting.md` "Moderation Model."
  (4) **`User.ActiveReportCount` added** — symmetric with other authored-content targets; uniform
  `AdjustActiveReportCount` switch; `PrivateMessage` has no counter. See `cross-cutting.md` "Moderation Model."
  (5) **`Report.ReportedEntityId int→long`; `ReportedEntityType` +`Message = 5`.** Reportable set for WU34 =
  Story, User, Comment, BlogPost, Recommendation, PrivateMessage.
  (6) **Notification dedup-key fix.** Widen `CreateCoreAsync` dedup key from `(type, sourceUserId, !IsRead)`
  to include `RelatedEntityId`. Regression-test follow/vouch/group. See `layer2-services.md` "Notification
  Generation."
  (7) **`StoryApproved` notification type added** (`NotificationTypeEnum.StoryApproved = 75`, `YourStories=2`,
  `KindFor → Story`). Seeded `NotificationType` row + migration. See `layer2-services.md` "Notification
  Generation."
  (8) **WU34/WU39 scope split.** Story import + import verification are Feature 53 (WU39, deps WU24 + WU34).
  `/mod/submissions` in WU34 builds a tabbed shell; the import-verification tab drops in with WU39.
  See `audit/Moderation.md` Feature 53 + `workplan.md` WU39.

- **Moderator role assignment in dev seed** — resolved (2026-06-24, WU27.5): role *rows* are already
  seeded via `ApplicationRoleConfiguration.HasData`. WU27.5 assigns `AdminUser` to both `"Moderator"`
  and `"Admin"` in `DataSeeder.cs` — role gate is now exercisable end-to-end. Admin-inheritance
  expressed by listing both roles (IsInRole is literal). See `cross-cutting.md` "Role-Based
  (Moderator) Gating."

- **WU32 Groups — four design decisions** — resolved (2026-06-24, WU32 planning):
  (1) **Rating model:** `AudienceRating` (group visibility) and `MaxContentRating` (content ceiling)
  are two distinct properties; three `GroupAudienceType` presets (Standard/SfwOnly/Mature) are a
  UI/write convention mapped by `GroupAudienceTypeMapper`, not stored. `Group.Rating` renamed to
  `AudienceRating` in WU32 migration. `GroupAudience` named query filter (EF model-level) hides
  Mature groups from mature-disabled users. Content waterfall (three tiers) enforced at write time in
  `ServerGroupWriteService`; violations → `ContentRatingExceededException`. Non-M stories allowed in
  Mature groups (audience rating defines topic, not a content floor).
  (2) **Membership:** open join, permanent — no approval, no kicking, no per-group moderator role.
  (3) **Roles:** `GroupRole.Member` and `GroupRole.Admin` only. Creator auto-added as Admin. No
  `GroupRole.Moderator` — permanent decision.
  (4) **Group blog posts:** in scope for WU32, building on WU31 `BaseBlogPost` infrastructure.
  (5) **Group comments:** per-context method pattern (mirrors WU31 blog-post precedent); no generic
  context enum.
  See `cross-cutting.md` "Group Audience-Visibility Filter" / "Group Membership and Role Model";
  `layer2-services.md` "Group Rating Waterfall" / "Group Comments"; `audit/Groups.md`
  "WU32 Settled Decisions."

- **Active-user-conditional handling + two content-editing patterns** — resolved (2026-06-23, WU24
  planning): `IActiveUserContext` is server-only (query-shaping + server-side authz); SharedUI components
  never inject it — the dispatcher reads `AuthenticationState` and passes ownership down as a bool.
  Ownership is identity-equality (`entity.AuthorId == currentUserId`), not a role — plain inline `@if`,
  no `AdminControls` component (spec §5.17 reference is stale; that component was never built and should
  not be). Editing is **author-only, server-enforced**; moderation is a separate WU34 path.
  Two content-editing patterns by content weight: (1) Story/Chapter → **view-page / edit-page split**
  (separate routes; `RichTextView` on view, `EditorView` on edit); (2) comments/recs/vouch text →
  **in-place inline edit** (one page, parent-owned edit mode, both renderers co-exist normally).
  See `cross-cutting.md` "Active-User-Conditional Handling" and
  `layer3.5-structure.md` "Owner-Conditional Edit Affordances."

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

- **Notification generation mechanism** — resolved (2026-06-23, WU22): **direct injected call +
  semantic per-event methods + best-effort post-commit.** Feature write services inject
  `INotificationWriteService` and call a semantic method (e.g. `NotifyNewFollowerAsync`) after their
  primary `SaveChangesAsync`; the semantic method is the only public generation surface; a private
  create-core owns drop-self + dedup + bulk-insert, unbypassable per-caller (same "property of the
  model" principle as the content-rating named query filter). In-process domain events, EF interceptors,
  and outbox rejected (infra cost, inconsistency with Badges/UserStats, MediatR commercial-license).
  See `cross-cutting.md` "Notification Creation" and `layer2-services.md` "Notification Generation."

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
- **Aspire orchestration during MVP dev** — resolved (2026-06-20): not used day-to-day while the MVP
  stays `InteractiveServer`-only with no Redis/WASM (matches spec's MVP boundary, Layers 1–4 only —
  Layers 5–8 including Redis write-behind are post-MVP). Run `TheCanalaveLibrary.Server` directly;
  `ConnectionStrings:canalavedb` in `appsettings.Development.json` points at a local Postgres instance.
  `builder.AddRedisDistributedCache("cache")` is removed from `Program.cs` (nothing consumed
  `IDistributedCache` yet) with a comment marking where to re-add it. `AppHost.cs` keeps its
  `AddPostgres("postgres").AddDatabase("canalavedb")` wiring dormant in the tree — this part is
  genuinely additive/swappable, like Redis/L6 indexes: no application service knows or cares whether
  AppHost orchestrated the Postgres it's talking to, so there's nothing to undo when Aspire-orchestrated
  dev comes back post-MVP.
  **Narrower correction (WU12, 2026-06-22):** "Aspire" is not one decision — the line above only ever
  examined AppHost/orchestration, which stays correctly deferred. It separately assumed the *client
  integration package* (`Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`'s `AddNpgsqlDbContext<T>`, called
  directly in `Server/Program.cs`) was equally inert plumbing — "reads `ConnectionStrings:canalavedb`
  from plain config either way." That assumption was wrong: `AddNpgsqlDbContext<T>` registers the
  DbContext via EF Core's `DbContextPool` with no opt-out (confirmed against the package's own settings
  type — no `DbContextPooling` property exists), and pooled contexts are constructed from the *root*
  provider, so they cannot take a Scoped constructor dependency. This directly broke `IActiveUserContext`
  (WU12's content-rating query filter, itself sourced into `ApplicationDbContext`'s constructor) and
  directly contradicts spec §6.6's already-resolved "plain `AddScoped<>`, DI manages DbContext lifetime"
  decision. Unlike orchestration, this is a composition-root choice every DbContext-consuming service is
  written against — **architectural, not swappable**, the same category as `IActiveUserContext` itself.
  Resolved: the Aspire Npgsql *client* package is removed from `TheCanalaveLibrary.Server`; both
  DbContexts register via plain `AddDbContext<T>` + `UseNpgsql(...)` (retries preserved explicitly via
  `EnableRetryOnFailure()` — WU0's audit note already relies on retry behavior existing). See
  `layer2-services.md` for the registration pattern. This does **not** reopen the orchestration
  question above, and does not affect the Postgres primary/read-replica axis (a connection-string
  concern, orthogonal to whether the *.NET-side DbContext object* is pooled).
  **Durability — holds in production too, not just MVP.** Unlike the orchestration question (genuinely
  MVP-scoped, AppHost is meant to return post-MVP), this one doesn't expire: `IActiveUserContext`/the
  content-rating filter is permanent functional architecture, not an MVP shortcut, so the
  pooling-vs-Scoped-dependency incompatibility never goes away on its own. Actual production (DigitalOcean
  Droplet + Managed Postgres, spec's resolved hosting decision) never runs AppHost either — it's the same
  plain `AddDbContext` registration in both environments, not a dev-only stand-in. What's genuinely lost
  (not reopened, just no longer free): Aspire's auto-registered DB health check and Npgsql-specific OTel
  command tracing — both addable independently later if wanted, never bundled-or-nothing. Re-check this
  decision only if a future version of the Aspire package adds a pooling opt-out to its settings type
  (none exists today).
- **Interaction-icon design** (Feature 16 L4, previously Stage-1 blocked) — resolved WU7 (2026-06-21):
  inline SVG shapes, not theme-swappable sprite URLs — a permanent, deliberate carve-out from the
  "never inline SVG" rule (which still governs tags/covers/avatars). Square button, three visual
  states (gray inactive → accent-fill-on-hover → inverted accent-background/white-shape when active).
  `UserStoryInteractionButton` takes `IconPath`/`AccentColor` `[Parameter]`s and stays dumb; the
  `InteractionTypeEnum → (IconPath, AccentColor)` mapping is left for the owning composite (WU16).
  Supersedes the WU2-era `GetInteractionIcon`/sprite-key plan. See
  [layer4-style.md](skills/canalave-conventions/layer4-style.md) §"Interaction Icons Are Inline SVG"
  and [audit/UserStoryInteractions.md](audit/UserStoryInteractions.md) Feature 16.
- **WU26 chapter routes, versioning UX, and rating model** — resolved (2026-06-24, WU26 planning):
  Reading routes: `/story/{id}/{ch}` + optional `/{versionOrder}` (no `/chapter/` literal; fixed by
  spec §5.30.3 + shipped ChapterNavigation). Edit/new: `/story/{id}/chapter/new`,
  `/story/{id}/chapter/{ch}/edit[/{versionOrder}/]`. Version token = `SortOrder`, not ContentId.
  Versioning UX: progressive disclosure (plain editor + one "Add alternate version" link until
  `VersionCount > 1`). Rating: `ChapterContent.Rating → Rating?` (nullable, NULL=inherit); floor
  invariant (version ≥ story); primary invariant (primary's effective rating = story rating, via NULL).
  HasStarted blocker resolved: column + property exist from WU15/InitialSchema.
  See `cross-cutting.md` "Chapter Versioning — Progressive Disclosure" and "Two content-editing patterns."

- **WU33 Notification UI — presentation decisions** — resolved (2026-06-24, WU33 planning):
  (1) **Rich messages, flat DTO, normalized target pair:** `NotificationDto` extended with `SourceUserName?`,
  `TargetTitle?`, `TargetUrl?` (a single resolved `(title, url)` pair for the polymorphic `RelatedEntityId`);
  message text composed in UI by static `NotificationPresenter` (per-`NotificationTypeEnum` templates). No DTO
  inheritance — no codebase precedent; flat projection wins at the DTO firewall.
  (2) **Two-pass batch enrichment:** materialize page → classify by `RelatedEntityKind` → batch-load each kind
  → stitch. See `layer2-services.md` "Polymorphic RelatedEntityId — Two-Pass Batch Enrichment."
  (3) **Both grouped-by-category and flat date feed:** view toggle + sort toggle (Newest first / Oldest unread
  first). `NotificationCategoryVisuals` maps 9 categories to icons/labels, reusing existing icon constants as
  single source of truth; new glyphs only for SiteNews/YourProfile/Collaborations/Groups/YourReports.
  (4) **Bell flyout:** UserCard caret pattern (not modal). `<AuthorizeView><Authorized>`. No `IActiveUserContext`.
  (5) **Settings:** per-row immediate save; `EmailEnabled` + `Collapsed` only (no in-app toggle per audit
  correction #2).
  See `cross-cutting.md` "Notification bell"; `audit/Notifications.md` Feature 42 WU33 additive note;
  `layer3.5-structure.md` "Notification Presentation Model."

- **WU30 Profiles + theme-selection — settled decisions** — resolved (2026-06-24, WU30 planning):
  (1) **`IUserSettingsService` self-referential exception** (spec §3.5): single integrated
  read+write service is sanctioned only when reader=writer population; resolves target from
  `IActiveUserContext`, never takes a `userId`; every method throws if unauthenticated.
  Contrast with `IUserProfileReadService` (public display, read-only, own-vs-other =
  `bool includePrivate` predicate). See `layer2-services.md` "Self-Referential Editing Exception."
  (2) **UserStats counter wiring:** built-event counters wired now into the already-built write
  services (Following, Stories, Chapters, Comments×4, Recommendations, BlogPosts, Groups,
  UserStoryInteractions). Transition-delta rule for USI-derived counters (increment/decrement only
  on boolean flip, not every call). Counters for unbuilt features (ViewsOnStories WU38,
  acknowledgments WU37, SpotlightCount post-MVP, ActiveReportCount WU34) deferred.
  See `cross-cutting.md` "UserStats Updates — Counter ↔ event map."
  (3) **Profile comment wall:** `UserProfileComment` wall rendered inside the Profile tab (not
  beside story decks). Generalize `CommentSection` to a 4th context (`ProfileUserId` param +
  `CommentTarget.UserProfile`), gated by `PrivacySettings.AllowProfileComments`.
  See `layer3.5-structure.md` "CommentSection — Multi-Context Dispatch."
  (4) **Profile page shape:** persistent metadata banner (avatar/name/tagline/stats/badges/vouches/
  relationship actions) on top, then a tabbed body. Five tabs: Profile (bio + CommentSection),
  Favorites, Recommendations, Authored (each a StoryDeck + ResultsFilterPanel), Blog
  (paginated BlogPostCard list). Comments and StoryDecks never share a view.
  See `layer3.5-structure.md` "Profile Page Composition."
  (5) **Blog tab owner/viewer distinction + GetByAuthorAsync extension:** owner sees drafts
  ("Draft" badge), per-card Edit affordance, and "+ New Post" tab button; viewers see published
  only. Requires: `bool IsPublished` added to `BlogPostListingDto`; `includeUnpublished` flag on
  `IBlogPostReadService.GetByAuthorAsync` (default `false`); `BlogPostCard` de-nested anchor +
  optional owner affordances (gated by `IsOwner` param). `GroupDesktop` usage unaffected.
  (6) **`IThemeReadService.GetThemesAsync()` in `Core/Sprites/`** (Feature 3 owns Theme); Server
  impl reading `Themes` table. Surfaced in `/settings` Appearance section.
  (7) **`Profiles/` cluster** added to Code Organization: `Core/Profiles/`, `Server/Profiles/`,
  `SharedUI/Profiles/`. `Core/Identity/` keeps the `User` entity + `IActiveUserContext`;
  `Core/Profiles/` holds the projection/edit services over it.
  See `canalave-conventions/SKILL.md` "Code Organization" and `layer3.5-structure.md."

- **Integration test isolation foundation** — resolved (2026-06-24, post-WU29): **Respawn
  reset between every test.** The integration suite had no reset mechanism; tests shared one
  Postgres container with accumulating state, making absolute-count and absolute-emptiness
  assertions untestable and producing order-dependent failures (e.g. `SetHiddenGem_RejectAtFive`,
  `GetChapterComments_EmptyChapter`). Fix: `PostgresFixture` holds a `Respawner` (FK-ordered
  deletes, lookup tables excluded); `IntegrationTestBase.InitializeAsync` calls `ResetAsync`
  before creating the factory. Each test seeds its own users/stories via base helpers;
  `DataSeeder` creates `TestUser`/`AdminUser` per-factory (harmless — no test references them;
  Respawn removes them before the next test); `Environments.Development` is kept so that
  `appsettings.Development.json` supplies the connection string to `Program.cs`.
  `[assembly: CollectionBehavior(DisableTestParallelization
  = true)]` makes serial execution deliberate. `RecommendationStatusEnum` added to
  `Core/Lookups/ModelEnums.cs` (missing enum mirror). Magic literals replaced with named enums.
  See [canalave-conventions/testing.md](skills/canalave-conventions/testing.md)
  §"Integration tests reset between every test."

- **WU31.5 TPT denormalization + blog-post content-rating refactor** — resolved (2026-06-24,
  WU31.5 planning): (1) Spec §4.3 line 839's "configure on derived to override base-table mapping"
  technique does not work in EF Core 10 — a property on the base type always maps to the base table.
  Correct technique: declare the property on each derived class, remove from base. (2) Blog posts:
  `DateCreated`, `LastUpdatedDate`, `Rating`, `IsPublished` moved base→child; comments: `DatePosted`
  moved base→child. Child-only, no duplication. (3) Named query filter removed from `BaseBlogPost`;
  content rating enforced via explicit `.Where(p => p.Rating <= max)` projection checks (TPT +
  named-filter generates broken EF Core 10 SQL on derived entity materialization). (4) Change-tracker
  stub delete replaces raw-SQL workaround (`ExecuteDeleteAsync` unsupported on TPT base-type DbSets).
  See `layer1-data-model.md` §"Denormalization with TPT", `cross-cutting.md` §"Content Rating
  Filtering", `audit/BlogPosts.md` §Feature 35, `audit/Comments.md`.

- **WU35 Messaging architecture** — resolved (2026-06-24, WU35 planning):
  (1) **1-on-1 only** — group conversations are out of scope for MVP; conversations always have exactly
  two participants. The N-participant data model is kept. (2) **Stateless MVP, SignalR deferred post-MVP**
  — reverses the spec's "real-time via SignalR" framing; messaging is request/response like every other
  feature (recipient sees messages on navigate/refresh; global unread badge refreshes on navigation).
  SignalR push is a post-MVP additive layer behind the unchanged write service; no L1–L4 rework needed.
  Feature 49 L5 stays N/A. See Post-MVP section below for the deferred item. (3) **Global unread badge
  in layout chrome** — a `MessagesNavLink` beside `LoginDisplay` in Desktop/Mobile layouts, derived from
  `IMessagingReadService.GetUnreadConversationCountAsync()`. (4) **No PM Notification rows** —
  messaging's `LastReadTimestamp` watermark is its only bookkeeping; the Notification cluster is
  never touched. Rationale: event-rows and conversation-watermark are differently shaped read-state;
  unifying creates two unread truths to sync. Substantive/infrequent use case provides none of the
  value the notification dedup/batch machinery adds. See `cross-cutting.md` "Private Messaging
  Architecture"; `audit/Messaging.md` WU35 Settled Decisions.

- **WU31 Blog Post settled decisions** — resolved (2026-06-24, WU31 planning):
  (1) **Feature 56 (admin feature-contribution attribution) deferred post-MVP** — not in WU31;
  stays Stage 2 in `audit/BlogPosts.md`; `FeatureContribution` entity/FKs/DbSet unchanged.
  (2) **Content-editing Pattern 1 for blog posts:** `/blog/new` + `/blog/{id}/edit` (form/auth),
  `/blog/{id}/{*slug}` (read-only view) — overrides spec §5 line ~1585 "in-place editing" because
  a blog post is a multi-field form, not lightweight embedded content. See `cross-cutting.md`
  "Two content-editing patterns."
  (3) **Profile blog posts only in WU31;** `GroupBlogPost` UI built in WU32 (Groups) — confirmed
      in scope (2026-06-24, WU32 planning). Reuses WU31 `BaseBlogPost` infrastructure;
      `IBlogPostWriteService` gains `CreateGroupBlogPostAsync`; `IBlogPostReadService` gains
      `GetByGroupAsync`. See `audit/Groups.md` §"WU32 Settled Decisions."
  (4) **Optional story-link picker** via `IStoryReadService.GetStoryIdsByAuthorAsync(int authorId)`
  (`IgnoreQueryFilters` — author always sees own mature stories). Method confirmed present
  (parallel session delivered it; [IStoryReadService.cs:55](TheCanalaveLibrary.Core/Stories/IStoryReadService.cs)).
  (5) **Content-rating filter extended to `BaseBlogPost`** (same "no trace" rule as Story, WU12).
  (6) `{*slug}` URL segment is cosmetic — no `Slug` column on `BaseBlogPost`; `BlogPostId` (int) is
  the sole key. See `audit/BlogPosts.md` Features 35/36/56.

- **Test strategy** — resolved (2026-06-22, post-WU12 post-mortem): the project had zero automated
  tests; WU12's create-path bugs were caught only by manual reading of `/dev/wu12/*` probe output,
  which asserts nothing. Two-layer regime: a **unit** test project (`TheCanalaveLibrary.Tests.Unit`,
  Core-only, no DB/host) for pure logic (`StoryValidations`, `StoryMappers`, slug `Slugify`); an
  **integration** test project (`TheCanalaveLibrary.Tests.Integration`, references Server) against a
  **real Testcontainers Postgres** — never EF InMemory/SQLite, because the invariants worth protecting
  (the `"ContentRating"` named query filter's SQL translation, the slug unique-filtered index, snake-
  case naming, FTS) are Postgres-specific and a different provider would give false confidence on
  exactly those. Integration tests drive a `WebApplicationFactory` with `IActiveUserContext` swapped
  for a settable fake — sidesteps the one genuinely manual-only band (auth-cookie claim baking,
  `SecurityStampValidator` timing, SignalR circuit init), which stays Playwright/manual. Dev-
  diagnostics endpoints (`DevDiagnosticsEndpoints.cs`) remain **interactive probes, not the regression
  net** — they're `Development`-only, never run in CI, and assert nothing; the WU12 fixtures/endpoints
  stay in place per prior user instruction but are no longer the source of truth once these tests
  exist. See [canalave-conventions/testing.md](skills/canalave-conventions/testing.md).
  **Updated (2026-06-22, post-WU12.5-evaluation):** The two-tier model is now three tiers by *kind*:
  Unit (directly-constructed, no host/DB — references Core **and** Server), Integration
  (`WebApplicationFactory`/Testcontainers Postgres), and RazorComponents (bUnit component render tests,
  references SharedUI/Client). Unit's "Core-only reference" proxy is replaced by a behavioral rule:
  if you can `new` the type without a real host or DB it's Unit, even if it lives in Server. The
  Phase E loop and per-unit loop in `workplan.md` now name `dotnet test` as a required verification
  step. Obligation remains advisory ("should add tests") — no Stage-5 gate. `Tests.RazorComponents`
  (bUnit) added to the sln for SharedUI/Client component render tests.

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
