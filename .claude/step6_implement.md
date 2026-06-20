# Step 6: Implementation

> **Tool:** Per entry — opusplan for Stage-2 cells, Sonnet in Claude Code for Stage-3 cells, Opus 4.8 for Stage-4 reconciliation
> **Input:** `workplan.md` (pick entries in order), `features/<name>.md` (for the entry's cells), `conventions.md` (loaded as a skill)
> **Output:** Working code, updated `status.md`, updated `workplan.md`

CLAUDE.md's Per-Stage Process Guidance is the primary reference for this step. Each workplan entry tells you what cells to work on, which tool to use, and where to find the relevant notes.

## Workflow per entry

1. Read the workplan entry — it names the cell(s), the tool, and points to `features/<name>.md`.
2. Read the relevant feature file section(s) — they contain the settled constraints (Stage 2), the validated spec pointer (Stage 3), or the diagnosis (Stage 4).
3. Invoke the right tool per CLAUDE.md's per-stage guidance.
4. After completion: update `status.md` (cell advances to Stage 5) and `workplan.md` (mark entry complete).

## Per-tool notes

**opusplan (Stage 2).** Opus plans, Sonnet executes, you review the plan once before execution begins. Feed settled constraints from `features/<name>.md` as explicit "do not revisit" context. If the plan proposes changing a settled constraint, that's a misclassification signal — the cell may need Stage-4 reconciliation instead.

**Sonnet direct (Stage 3).** Mechanical build from the spec section pointed to in `features/<name>.md`. Sonnet doesn't need to make design decisions — they're already made. If it hits a real design gap (not a typo), stop and flag rather than improvising.

**Opus reconciliation (Stage 4).** The feature file's diagnosis describes the gap. Resolution may produce a Stage-2 plan (needs opusplan), a direct fix (cell reaches Stage 5), or reveal deeper ambiguity (cell drops to Stage 1). Update the feature file with the resolution narrative.

## Conventions as guardrail

`conventions.md` is loaded as a skill for every code-writing session. It's the paradigm-correctness reference — code produced in Step 6 should conform to it. If implementation experience reveals a convention that should change, update `conventions.md` rather than silently diverging.
