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
| `.claude/status.md` | Feature × Layer → Stage grid. Dashboard only — no prose. | Any session completing work on a cell |
| `.claude/workplan.md` | Ordered work-units. Each names cell(s), tool, audit file pointer, position. | Any session completing a work-unit |
| `.claude/audit-summary.md` | Write-once audit overview: stage distribution, surprises, reconciliation index, Stage-1 landscape, UI component inventory. | Written once during audit |
| `.claude/audit/<FolderName>.md` | Per-folder-cluster notes. Shared context header, then per-feature sections with per-layer stages. | Audit creates; working sessions update |
| `.claude/skills/canalave-conventions/SKILL.md` | Authoritative code conventions (hub file + layer files). Loaded as a skill when writing code. | Refined through implementation |

### Audit file content per stage

| Stage | Note contains |
|-------|--------------|
| **1** | Gap description (conceptual / code-relationship / blocked), resolution venue (chat / Claude Code) |
| **2** | Settled constraints (do not revisit) vs. open for opusplan |
| **3** | Pointer to spec section serving as validated plan |
| **4** | What exists, what's correct, nature of gap, implied resolution stage |
| **5** | How verified (build, tests, audit inspection) |
| **N/A** | Why the layer doesn't apply |

### Spec relationship

The spec is a read-only snapshot. Audit files point into it (section references, not copies). When code is more authoritative than the spec, the audit file carries both: what the spec said, and what changed and why.

## Per-Stage Process Guidance

**Stage 2 (opusplan).** Check the cell's settled-vs-open note in `.claude/audit/<FolderName>.md` before approving the plan. If the plan changes something marked "settled," stop and flag — may be Stage 4.

**Stage 3 (Sonnet direct).** Build from the spec section in the audit file. If a design gap surfaces (not just a typo), stop — may be Stage 2 or 4.

**Stage 4 (Opus reconcile).** Start from the diagnosis note. Resolution determines resulting stage: code must change → Stage 2; intent updates to match code → may reach Stage 5; deeper ambiguity → Stage 1. Update both `.claude/status.md` and the audit file.

**Unresolved dependency encountered.** If a cell you need depends on another cell that hasn't reached Stage 5, surface it to the user. This applies regardless of the dependency's current stage — don't assume any unresolved dependency's outcome. Name it, state its stage, let the user decide.

**After completing any work-unit.** Update `.claude/status.md` and `.claude/workplan.md`. This is part of finishing the work, not separate bookkeeping.
