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

- Install Tailwind into the build (`package.json` + `tailwind.config.js` + the SharedUI/Server pipeline).
- Lock the design tokens (palette, type scale, spacing, the Pokémon theme) — this is the human-driven
  decision the whole Style column waits on.
- Decide the Bootstrap exit: existing components (`StoryPropertiesForm`, `TagSelector`, Identity scaffold)
  are Bootstrap and will be **restyled, not just styled**, when their L4 cells come up.

**Gate:** `tailwind.config.js` tokens are locked and the build emits Tailwind CSS. Until then, every L4 cell
stays Stage 1 — and that's expected.

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
  `StoryInteractionPanel`, `ChapterNavigation`, `CommentSection`, `ConfirmDialog`), then page/dispatchers and
  consumers.
- **Stage 4 → use the resolved direction.** Per §0, Stage-4 cells are stale-code traps resolving to Stage 2
  (build to spec); sequence them by that implied stage, and flag the code as discard-not-reuse so a building
  session doesn't preserve it. (The rare working-code exception, e.g. Sprites naming, is a light rename.)
- **Stage 3 is minted here-and-after, not found.** Expect ~0 Stage-3 cells at the start; opusplan passes in
  Phase E *create* them by locking atom contracts, after which consumers flip 2→3.
- **Foundational re-models already done in Phase A** lead the plan's data dependencies; the migration/build
  pass is effectively work-unit zero (already executed).
- Remaining unresolved Stage-1 cells go in a "blocked/deferred" section with no sequence number.

**Gate:** read the preamble — the ordering should put atoms before composites before consumers, with nothing
depending on something later.

---

## Phase E — Build per workplan

**Goal:** working, convention-conformant code, one work-unit at a time.
**Tool per cell (per CLAUDE.md Per-Stage Guidance):** opusplan for Stage 2, Sonnet in Claude Code for
Stage 3 (once minted), Opus for any residual Stage 4. **Relax permissions first** (allow `.cs`/`.razor`/
`.csproj` writes — see config below).

Loop: pick the next entry → read its `.claude/audit/<Folder>.md` pointer → invoke the entry's tool →
build + verify (`dotnet build`, and run the relevant slice) → update `.claude/status.md` (cell → Stage 5)
and `.claude/workplan.md` (entry complete). The conventions skill loads automatically as the
paradigm-correctness guardrail.

Guardrails:
- **opusplan:** feed the audit file's settled constraints as explicit "do not revisit." If the plan proposes
  changing a settled constraint, that's a misclassification signal — stop and flag.
- **Spec supersedes:** for any Stage-4 entry, the existing code is reference-for-what-to-replace, not a
  design to preserve.
- **Conventions are living:** if implementation reveals a convention that should change, update the skill
  file rather than silently diverging.

---

## Decisions that need you (resolve before/early in Phase A)

| Decision | Default (per spec/§0) | Why it's yours |
|----------|----------------------|----------------|
| *(none currently open)* | | |

**Resolved:**

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
`tailwind.config.js`. Keep them denied during B/D if you want a hard read-only-on-code guarantee.

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
