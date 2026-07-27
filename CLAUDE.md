# The Canalave Library — CLAUDE.md

## Project Identity

Pokémon-fandom fanfiction website. Blazor (.NET 10), EF Core (Code-First), PostgreSQL, .NET Aspire.

## Stage Definitions

Each cell in the Feature × Layer grid holds a Stage or N/A. Stages encode state + directive.

| Stage | State | Directive | Tool |
|-------|-------|-----------|------|
| **1** | Gap too fundamental to proceed | Clarify intent | Sonnet in chat or Claude Code (per cell note) |
| **2** | Intent settled, no plan/code | Plan and build | opusplan |
| **3** | Intent settled + validated spec exists | Build from spec | Sonnet in Claude Code |
| **4** | Code/plan disagrees with correct architecture | Diagnose and reconcile | Opus diagnoses → opusplan (Stage 2) or Sonnet (Stage 3) |
| **5** | Aligned, sound, compiles | Dormant — awaiting verification | — |
| **6** | Human-verified and frozen | Do not touch | — |
| **N/A** | Layer does not apply to this feature | Skip | — |

Grid columns: `L1 | L2 | L3-Logic | L3.5-Structure | L4-Style | L4.5-Browser | L5 | L6 | L8`
(L7 — formerly "Redis Integration" — was dissolved 2026-07-06 and redistributed into L2/L6/L8;
L8 keeps its historical number. See `grid_axes.md` "Layer 7 — dissolved".)

## Project Files

Process artifacts live under `.claude/`. Repo root holds the spec, this file, the
`modernization-audit/` record, and four `*_Deliberations.md` files (pre-spec 2025 design-session
records the spec cites — historical, never updated).

**Cold-session read order:** this file → `.claude/status.md` (grid + standing constraints) →
`.claude/workplan.md` (Position block) → `middle_plan_v2.md` (phases + open decisions) — then the
task's audit file per the loop in `workplan.md`'s preamble.

| File | Purpose | Updated by |
|------|---------|------------|
| `canalave_library_unified_spec.md` | Single authoritative specification (read-only) | Never (historical snapshot) |
| `.claude/status.md` | Feature × Layer → Stage grid, plus a "Global conditions" section above it holding **standing constraints only**: each bullet states a fact that currently binds new work (never a past event — history lives in the workplan ledger), and is deleted when it stops binding. No other prose — when a cell's Stage *does* change, the "how verified"/"what changed" narrative goes in that cell's audit file Stage note (see "Audit file content per stage"), never here; this file gets only the updated number. | Any session completing work on a cell, or adding/deleting a standing constraint |
| `.claude/workplan.md` | The live work-unit ledger: a Position block (current phase, next WUs, blockers — updated at moment 3), the ordering preamble, blocked/planned/post-MVP sections, and the most recent DONE entries. Each entry names cell(s), tool, audit file pointer. | Any session completing a work-unit |
| `.claude/workplan-archive.md` | Older DONE work-unit entries, moved out of `workplan.md` to keep the live ledger navigable. Append-only; "workplan.md WU-X" citations resolve here for archived entries. | Periodic archival sweeps only |
| `.claude/audit-summary.md` | Historical snapshot of the original (pre-2026-06-20) audit; superseded by `status.md` for stage numbers and by later audit-file notes for per-feature detail. Still-live content: §0's spec-vs-stale-code adjudication principle. Carries its own snapshot banner. | Never (banner amendments only) |
| `.claude/hidden-deferrals-tracker.md` | Off-grid deferral checklist: items invisible to a grid scan because their cells are (or stay) Stage 5 — the "built but inert / fed by a hardcode / no restore path" class. Explicitly non-authoritative (a snapshot, not a process doc); work-units cite its item IDs (A3, B6, D2, …) as triggers and tick items closed. | Any WU closing or discovering an item |
| `.claude/middle-addendum.md` | One-time external-readiness findings (2026-07-07): what a live website needs that no grid row owned. §3's numbered items are the provenance for Features 64–65 and feed decision rows 10/12 and the Phase-7 checklist; §2's table carries later DONE/superseded annotations. | Only to annotate items as resolved/superseded |
| `modernization-audit/` (repo root) | The 2026-07-17 whole-codebase modernization audit's durable record: `report.md` (executive deliverable), `fix-status.md` (finding → resolution map), `plan-of-record.md`, slices. Fix passes closed 2026-07-18 (WU-AuditFixPass ×2); open remainders live in `deferred-work.md` + tracker item H8. | Never (complete; banner on its README) |
| `.claude/audit/<FolderName>.md` | Per-folder-cluster notes. Shared context header, then per-feature sections with per-layer stages. | Audit creates; working sessions update |
| `.claude/skills/canalave-conventions/SKILL.md` | Authoritative code conventions (hub file + layer files). Loaded as a skill when writing code. | Refined through implementation |
| `.claude/design/surface-registry.md` | The element-role design system's audit + ratification record: every visual element classified into one of seven roles (Canvas / Wayfinding / Container / Content Surface / Control / Indicator / Overlay). **Any work touching UI markup follows the role system** — rules in `canalave-conventions/layer4-style.md` §"Element Roles"/"Interaction States"; `scripts/check-design-tokens.ps1` (local + CI) fails builds on violations; `/dev/design-gallery` is the live composition reference. **Caveat:** the per-component inventory is a paused artifact pending a ground-up rewrite after the foundation work — trust the taxonomy/ratifications, not its component sections (banner in the file). | On the planned ground-up rewrite (post-foundation); until then, only banner amendments |
| `.claude/design/` (other files) | Standing cross-cutting analyses that back build passes: `access-gating-first-principles.md` (the authoritative Feature-66 viewer-permission model; `access-gating-audit.md` is its surface inventory — first-principles wins on disagreement) and the two L6 evidence reports (`L6-intent-ledger.md`, `L6-reconciliation-matrix.md` — moved from `audit/` 2026-07-27; the matrix's live-`pg_indexes` reconciliation is still PENDING). | The pass that consumes them |
| `.claude/middle_plan_v2.md` | Live phased master plan (platform-first → features → beta → launch) + "Decisions that need you" table (open items) and a "Resolved" list (closed items, each pointing at the convention doc that now states the rule). Supersedes the retired `.claude/middle_plan.md` and `.claude/forward_plan.md` (both historical references; v1 phase-number pointers resolve via v2's mapping table). | Whoever resolves a decision or advances a phase |
| `.claude/grid_axes.md` | Defines the grid layers (columns, incl. the L4.5-Browser band) and 66 features (rows) in detail, including the MVP-line and post-MVP-line rationale. `status.md`'s rows are drawn from this file. | Rarely — only if a layer/feature axis itself changes (e.g. the 2026-07-06 Layer-7 dissolution, the 2026-07-15 addition of Features 64–65 from `middle-addendum.md` §3, or the 2026-07-19 addition of Feature 66) |
| `.claude/folder_clusters.md` | Folder → feature → per-layer (L3/L3.5/L4) **structural** mapping (which components/patterns live where), used to route work to the right audit file/skill section. Columns carry structural facts and settled distinctions only — stage/status lives in `status.md`'s grid and the audit files. | Rarely — only if folder clustering, feature-to-folder assignment, or a cluster's structural shape changes |

### Audit file content per stage

| Stage | Note contains |
|-------|--------------|
| **1** | Gap description (conceptual / code-relationship / blocked), resolution venue (chat / Claude Code) |
| **2** | Settled constraints (do not revisit) vs. open for opusplan |
| **3** | Pointer to spec section serving as validated plan |
| **4** | What exists, what's correct, nature of gap, implied resolution stage |
| **5** | How verified: `dotnet build` green; `dotnet test` green (or a note stating which tier covers the behavior — Unit / Integration / RazorComponents — and, for cells where no automated test applies, why: e.g. purely visual L4, auth-cookie/claims manual band per `canalave-conventions/testing.md`) |
| **N/A** | Why the layer doesn't apply |

### Spec relationship

The spec is a read-only snapshot. Audit files point into it (section references, not copies). When code is more authoritative than the spec, the audit file carries both: what the spec said, and what changed and why.

### No session-relative language in persistent docs

`status.md`, `workplan.md`, and `audit/<FolderName>.md` are read in later sessions with no memory of
this one. Never write "this session," "just now," "recently," or similar — by the next read it's
meaningless or actively misleading. Use the absolute date (`YYYY-MM-DD`, from the session's `currentDate`)
or the work-unit ID (`WU2`) instead — both already the convention in `workplan.md` (e.g. "DONE ✓
(2026-06-20)").

## Doc-Touch Timing

Three distinct moments touch process docs. Keep them separate — don't fold moment 1 into moment 3, and don't defer moment 1 past the start of implementation. If a task spans multiple folder clusters, make sure all audit files that are relevant to the task are reviewed and edited.

| Moment | Trigger | Action | Files touched |
|---|---|---|---|
| **1. Pre-implementation** | Plan resolves a `middle_plan_v2.md` "Decisions that need you" row, would contradict a "settled" audit note, or needs a convention not yet recorded anywhere | Settle it (ask the user if genuinely open), then update every doc that states or defers it — as an explicit first phase of the plan, completed before any code change | Skill file(s); audit file's settled-vs-open note; `middle_plan_v2.md` (move row to "Resolved", point at the doc) |
| **2. Mid-implementation** | Building reveals a convention should change | Update the skill file in the same work-unit — conventions are living; don't silently diverge | Skill file(s) |
| **3. Post-implementation** | A work-unit completes | Run `dotnet test` (should be green). Flip the affected cell(s)' number(s) in `status.md`'s grid (no narrative there); write the "how it was verified" / "what changed" detail into each affected cell's audit file Stage note — include which test tier covers the behavior (Unit / Integration / RazorComponents) or state why none applies — **and update that feature's headline stage line in the same edit** (never append-only). Only write a `status.md` Global Conditions note when the fact is a *standing constraint* that binds future work and doesn't attach to any single cell — never a past event; delete a bullet when it stops binding. | `status.md` (grid number only), `workplan.md` (entry + Position block), audit Stage note (the narrative) + headline line |

Audit files appear in both 1 and 3: a settled-vs-open note is an *input* checked before a plan is
approved; a Stage note is an *output* recorded after the work lands.

## Per-Stage Process Guidance

**Stage 2 (opusplan).** Check the cell's settled-vs-open note in `.claude/audit/<FolderName>.md` before approving the plan. If the plan changes something marked "settled," stop and flag — may be Stage 4. If the plan instead *resolves* an open item, do that as Doc-Touch Timing's moment 1 before building.

**Stage 3 (Sonnet direct).** Build from the spec section in the audit file. If a design gap surfaces (not just a typo), stop — may be Stage 2 or 4.

**Stage 4 (Opus reconcile).** Start from the diagnosis note. Resolution determines resulting stage: code must change → Stage 2; intent updates to match code → may reach Stage 5; deeper ambiguity → Stage 1. Flip the grid number in `.claude/status.md`; write the resolution/verification detail in the audit file's Stage note, not in `status.md`.

**Unresolved dependency encountered.** If a cell you need depends on another cell that hasn't reached Stage 5, surface it to the user. This applies regardless of the dependency's current stage — don't assume any unresolved dependency's outcome. Name it, state its stage, let the user decide.

**Runtime bug surfaces during verification.** When manual or browser-based checking turns up a bug the automated tiers didn't catch, diagnose it per `canalave-conventions/debugging.md` and fix it in the same session — don't leave a cell's Stage number describing code that isn't actually sound while the fix waits. This is a debugging technique (mechanics in `run-server/SKILL.md`), not a new verification band or Stage gate.

**Retiring or closing.** When a WU retires a pattern, term, or component (a package, a component pair, a paradigm like device forks), add the retired name to `scripts/check-doc-hygiene.ps1`'s registry and grep all process docs for it in the same WU — the script (local + CI) enforces it from then on. When a WU closes a deferral or resolves a decision, sweep all three open-work ledgers in the same WU: `middle_plan_v2.md` (decision table + phase items), `hidden-deferrals-tracker.md`, and `workplan.md`'s blocked/planned sections — a closure recorded in only one of them is how stale "pending" claims form.

**After completing any work-unit.** Run `dotnet test` (should be green; add tests for any new testable surface per `canalave-conventions/testing.md`'s tier rules). Flip the grid number(s) in `.claude/status.md`, write the verification narrative — including which test tier covers the behavior or why none applies — in the affected audit file's Stage note, **and update that feature's headline stage line in the same edit** (appending a Stage note while the headline still claims the old state is the most common doc defect — six files had it on 2026-07-27), and update `.claude/workplan.md` (including its Position block). This is part of finishing the work, not separate bookkeeping.

**Phase 4 (integration tests) plan completeness.** Integration tests reset between every test
(Respawn — see `canalave-conventions/testing.md`). Each test seeds what it needs via
`IntegrationTestBase` helpers; the production `DataSeeder` does not run. Before implementation
begins, the plan must answer for each integration test class:
- **Per-test seeding:** which users and stories does each test seed via `SeedUserAsync` /
  `SeedStoryAsync`? Tests that depend on a shared user across methods seed it in `InitializeAsync`
  via the base helpers, not by querying seeded names or hardcoding `userId: 1`.
- **FK parent rows:** for every service call under test that writes to a constrained table, name
  which parent rows must exist and where they come from — base helper, `SeedStoryAsync`, or an
  inline `ApplicationDbContext` seed earlier in the same test. Missing parents produce FK violations
  at runtime. See `canalave-conventions/testing.md` "FK parents" rule.
- **Count-sensitive tests:** reject-at-limit tests call the service the natural number of times;
  the reset guarantees a clean count. No top-up logic or direct-insert workarounds are needed.
