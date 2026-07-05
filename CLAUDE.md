# The Canalave Library — CLAUDE.md

## Project Identity

Pokémon-fandom fanfiction website. Blazor (.NET 10), EF Core (Code-First), PostgreSQL, Redis, .NET Aspire.

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

Grid columns: `L1 | L2 | L3-Logic | L3.5-Structure | L4-Style | L5 | L6 | L7 | L8`

## Project Files

All process artifacts live under `.claude/`. The spec and this file live at repo root.

| File | Purpose | Updated by |
|------|---------|------------|
| `canalave_library_unified_spec.md` | Single authoritative specification (read-only) | Never (historical snapshot) |
| `.claude/status.md` | Feature × Layer → Stage grid, plus a "Global conditions" note section above it for cross-cutting facts that don't change any single cell's Stage (e.g. a build/tooling verification, a blocked layer). No other prose — when a cell's Stage *does* change, the "how verified"/"what changed" narrative goes in that cell's audit file Stage note (see "Audit file content per stage"), never here; this file gets only the updated number. | Any session completing work on a cell, or recording a cross-cutting condition |
| `.claude/workplan.md` | Ordered work-units. Each names cell(s), tool, audit file pointer, position. | Any session completing a work-unit |
| `.claude/audit-summary.md` | Write-once audit overview: stage distribution, surprises, reconciliation index, Stage-1 landscape, UI component inventory. | Written once during audit |
| `.claude/audit/<FolderName>.md` | Per-folder-cluster notes. Shared context header, then per-feature sections with per-layer stages. | Audit creates; working sessions update |
| `.claude/skills/canalave-conventions/SKILL.md` | Authoritative code conventions (hub file + layer files). Loaded as a skill when writing code. | Refined through implementation |
| `.claude/middle_plan.md` | Live phased master plan (MVP → beta → launch) + "Decisions that need you" table (open items) and a "Resolved" list (closed items, each pointing at the convention doc that now states the rule). Supersedes the retired `.claude/forward_plan.md` (historical reference). | Whoever resolves a decision or advances a phase |

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
| **1. Pre-implementation** | Plan resolves a `middle_plan.md` "Decisions that need you" row, would contradict a "settled" audit note, or needs a convention not yet recorded anywhere | Settle it (ask the user if genuinely open), then update every doc that states or defers it — as an explicit first phase of the plan, completed before any code change | Skill file(s); audit file's settled-vs-open note; `middle_plan.md` (move row to "Resolved", point at the doc) |
| **2. Mid-implementation** | Building reveals a convention should change | Update the skill file in the same work-unit — conventions are living; don't silently diverge | Skill file(s) |
| **3. Post-implementation** | A work-unit completes | Run `dotnet test` (should be green). Flip the affected cell(s)' number(s) in `status.md`'s grid (no narrative there); write the "how it was verified" / "what changed" detail into each affected cell's audit file Stage note — include which test tier covers the behavior (Unit / Integration / RazorComponents) or state why none applies. Only write a `status.md` Global Conditions note when the fact is genuinely cross-cutting and doesn't attach to any single cell — and keep that note short, a pointer to the skill/audit file for detail, not the detail itself. | `status.md` (grid number only), `workplan.md`, audit Stage note (the narrative) |

Audit files appear in both 1 and 3: a settled-vs-open note is an *input* checked before a plan is
approved; a Stage note is an *output* recorded after the work lands.

## Per-Stage Process Guidance

**Stage 2 (opusplan).** Check the cell's settled-vs-open note in `.claude/audit/<FolderName>.md` before approving the plan. If the plan changes something marked "settled," stop and flag — may be Stage 4. If the plan instead *resolves* an open item, do that as Doc-Touch Timing's moment 1 before building.

**Stage 3 (Sonnet direct).** Build from the spec section in the audit file. If a design gap surfaces (not just a typo), stop — may be Stage 2 or 4.

**Stage 4 (Opus reconcile).** Start from the diagnosis note. Resolution determines resulting stage: code must change → Stage 2; intent updates to match code → may reach Stage 5; deeper ambiguity → Stage 1. Flip the grid number in `.claude/status.md`; write the resolution/verification detail in the audit file's Stage note, not in `status.md`.

**Unresolved dependency encountered.** If a cell you need depends on another cell that hasn't reached Stage 5, surface it to the user. This applies regardless of the dependency's current stage — don't assume any unresolved dependency's outcome. Name it, state its stage, let the user decide.

**Runtime bug surfaces during verification.** When manual or browser-based checking turns up a bug the automated tiers didn't catch, diagnose it per `canalave-conventions/debugging.md` and fix it in the same session — don't leave a cell's Stage number describing code that isn't actually sound while the fix waits. This is a debugging technique (mechanics in `run-server/SKILL.md`), not a new verification band or Stage gate.

**After completing any work-unit.** Run `dotnet test` (should be green; add tests for any new testable surface per `canalave-conventions/testing.md`'s tier rules). Flip the grid number(s) in `.claude/status.md`, write the verification narrative — including which test tier covers the behavior or why none applies — in the affected audit file's Stage note, and update `.claude/workplan.md`. This is part of finishing the work, not separate bookkeeping.

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
