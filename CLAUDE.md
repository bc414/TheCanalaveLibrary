# The Canalave Library — CLAUDE.md

## Project Identity

The Canalave Library is a Pokémon-fandom fanfiction website built with Blazor (.NET), EF Core (Code-First), PostgreSQL, Redis, and .NET Aspire for local orchestration. The full design history lives in `canalave_library_spec.md` — an unchanging ~1600-line snapshot covering mission/philosophy, tech stack, architecture, database schema, features, and implementation roadmap.

## Stage Definitions

Each cell in the tracking system (a Feature × Layer intersection) holds exactly one Stage value. Stages encode both a **state** (what's true now) and a **directive** (what happens next). Stages are not strictly monotonic — a Stage 4 cell may resolve to Stage 2, 1, or 5.

| Stage | State | Directive | Tool |
|-------|-------|-----------|------|
| **1** | Intent has a gap too fundamental to proceed | Iterate to clarify intent | Sonnet in chat or Claude Code (see cell's note for which) |
| **2** | Intent is settled, but no plan or code exists (or what existed was not confirmed) | Plan and build | opusplan (Opus plans, Sonnet executes, one review checkpoint) |
| **3** | Intent settled AND a validated plan/spec already exists | Build directly from the existing spec | Sonnet in Claude Code |
| **4** | Code/plan exists but disagrees with the determined-correct architecture | Diagnose and reconcile | Opus 4.8 diagnoses; resolution routes to opusplan (Stage 2) or Sonnet (Stage 3) per outcome |
| **5** | Aligned with intent, technically sound, compiles | Dormant — awaiting human/runtime verification | — |
| **6** | Human-verified and frozen | Do not touch | — |

## The SLF Table

A 2D grid: **Layers** as columns (bounded set), **Features** as rows (dependency-ordered, foundational at top). Each cell holds a Stage value.

## File Schema

| File | Purpose | Updated by |
|------|---------|------------|
| `status.md` | Feature × Layer → Stage grid. Dashboard only — no prose, no dependencies. | Any session completing work on a cell |
| `workplan.md` | Ordered list of work-units. Each names its cell(s), tool, pointer to `features/<name>.md`, and position = sequencing. Grouping expressed as multi-cell entries. | Any session completing a work-unit |
| `features/<name>.md` | Per-feature notes across all layers, plus current file locations in the repo. | Initially by audit; working sessions as they resolve cells |
| `conventions.md` | Cross-cutting code patterns: naming, EF Core/Npgsql, Blazor render-mode, service/DTO shape, code organization. Authoritative paradigm-correctness reference. | Initially from spec + current framework docs; refined through implementation |
| `audit-summary.md` | Write-once human-facing overview: stage distribution, surprising findings, reconciliation index, Stage-1 landscape. | Written once during audit (never updated afterward) |

### What goes in `features/<name>.md` per stage

| Stage | Note contains |
|-------|--------------|
| **1** | Gap description, whether conceptual or code-relationship, and whether to resolve in chat or Claude Code |
| **2** | Which constraints are settled (do not revisit) vs. genuinely open for opusplan to resolve |
| **3** | Pointer to the specific section of `canalave_library_spec.md` that serves as the validated spec |
| **4** | Diagnosis: what exists, what correct looks like, the nature of the gap, and the implied resolution stage |
| **5** | Confirmation note — how verified (build, tests, audit inspection) |

### Relationship to `canalave_library_spec.md`

The spec is an unchanging historical snapshot. Feature files point into it (section references, not copies) and add a status layer on top. For cells where code was determined more authoritative than the spec, the feature file carries both: a pointer to what the spec said, and the narrative of what changed and why. The spec's text doesn't change.

## Per-Stage Process Guidance

**Stage 2 (opusplan).** Before approving the plan, check it against this cell's settled-vs-open note in `features/<name>.md`. If the plan proposes changing something marked "settled," stop and flag — this cell may actually be Stage 4. Include settled constraints as explicit "do not revisit" context, not ambient background.

**Stage 3 (Sonnet direct).** Build from the spec section pointed to in `features/<name>.md`. If the code doesn't compile in a way that suggests more than a typo — an actual design gap — stop and flag. This may be a misclassification (should be Stage 2 or 4).

**Stage 4 (Opus reconcile).** Use the diagnosis note in `features/<name>.md` as the starting point. The diagnosis describes the gap; resolution determines a resulting stage. If code needs to change → the cell becomes Stage 2 (plan and rebuild via opusplan). If intent needs updating to match code → update the feature file and the cell may reach Stage 5. If deeper ambiguity surfaces → the cell becomes Stage 1. Update both `status.md` and `features/<name>.md`.

**Stage 1 encountered as a dependency.** If a cell you need depends on a Stage-1 cell, surface it to the user rather than guessing past it.

**After completing any work-unit.** Update `status.md` (cell's stage value) and `workplan.md` (mark entry complete). This is part of finishing the work, not separate bookkeeping.

## Reference Documents

| Document | Role | Location |
|----------|------|----------|
| `canalave_library_spec.md` | Unchanging design-history snapshot | Repo root |
| `conventions.md` | Authoritative code patterns (loaded as a skill when writing code) | `.claude/skills/canalave-conventions/SKILL.md` |
| `status.md` | Stage dashboard | Repo root or docs/ |
| `workplan.md` | Ordered work-units | Repo root or docs/ |
| `features/<name>.md` | Per-feature notes | `features/` directory |
| `audit-summary.md` | Write-once audit overview | Repo root or docs/ |
