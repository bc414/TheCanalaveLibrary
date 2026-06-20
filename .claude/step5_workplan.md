# Step 5: Produce the Workplan

> **Tool:** Opus 4.8 in Claude Code (direct — producing the workplan IS the deliverable, no plan-then-handoff)
> **Input:** `status.md`, `features/<name>.md` files, `conventions.md` — all reflecting post-Step-4 state
> **Output:** `workplan.md`

CLAUDE.md defines the workplan schema. It is already loaded.

## Workplan Entry Validity

Each entry in `workplan.md` must satisfy two properties:

1. **Sequencing safety.** The entry's assigned tool can complete it without the outcome depending on something a later entry produces. All dependencies must already be at Stage ≥ 3.

2. **Multi-cell coherence.** For entries covering multiple cells, the cells must belong together — splitting them into separate entries would leave one entry's tool working without information it needs from the other.

## Stage-4 Cells in Sequencing

Stage-4 cells have a diagnosis (from Step 3) that implies a resolution direction: "code needs to change" reads as Stage-2-shaped, "intent needs updating" reads as near-Stage-5. For workplan sequencing purposes, use the *implied* stage from the diagnosis rather than the literal Stage-4 value. Note in the entry that the cell's actual Stage value gets updated to match once that work is done.

## Grouping by Tool Type

For opusplan entries (Stage 2): cells are grouped when planning one in isolation would require guessing about the other's interface — they share design decisions that must be made together.

For Opus reconciliation entries (Stage 4): cells are grouped when their diagnoses point at the same underlying discrepancy — reconciling them separately risks inconsistent conclusions about one root cause.

For Sonnet entries (Stage 3): grouping is less critical since these are mechanical builds from validated specs, but cells in the same feature that share code files may benefit from one session.

## Workplan Format

Each entry names:
- The cell(s) it covers (Feature × Layer references)
- The tool implied by its stage
- A pointer to the relevant `features/<name>.md` section(s)
- Its position in the list = its sequencing

Include a short **preamble** at the top of `workplan.md` explaining the ordering rationale — why the workplan starts where it does and how the sequencing was determined. This replaces the workplan-rationale section that would otherwise go in `audit-summary.md` — the workplan is the artifact most directly about its own ordering, so it explains itself.

## Remaining Stage-1 Cells

Any Stage-1 cells that weren't resolved in Step 4 are not valid workplan entries — they're blocked on intent clarification, which is a different kind of work. Note them at the bottom of `workplan.md` as a "blocked/deferred" section so they aren't forgotten, but don't assign them entry numbers or sequencing positions.
