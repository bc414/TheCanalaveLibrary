# Step 4: Resolve Stage-1 Cells

> **Tool:** Sonnet in chat (conceptual gaps) or Claude Code (code-relationship gaps), plus direct human editing of `features/<name>.md`
> **Input:** `status.md` and `features/<name>.md` from Step 3, `conventions.md`, `audit-summary.md`'s Stage-1 landscape section
> **Output:** Updated `status.md` (cells advance past Stage 1) and updated `features/<name>.md` (gap notes replaced with settled-intent notes)

This step is iterative and human-driven, not a single prompt. The audit-summary's Stage-1 landscape (section 4) gives you a map of what needs resolving, grouped by pattern — use it to decide what to tackle in what order.

## How to resolve each gap type

**Conceptual gaps** (the spec never addressed this — e.g., email provider, poll UX, notification preferences): Open a Sonnet chat session with `conventions.md` uploaded as context. Explore the decision space, settle on an answer, then update the cell's `features/<name>.md` note from a Stage-1 gap description to Stage-2 content (settled constraints vs. open questions for opusplan) or Stage-3 content (if the conversation produced a complete-enough spec to build from directly). Update `status.md` to match.

**Code-relationship gaps** (spec and code diverge, understanding the code is needed): Open Claude Code with codebase access. Investigate the specific divergence described in the cell's note, determine which side is correct, and update the feature file with the resolution. The cell likely advances to Stage 2, 3, or 4 depending on what you find.

## Practical expectations

Most Stage-1 cells are expected to be conceptual gaps in UI or peripheral features — things the design conversation deferred because they weren't load-bearing for the architecture. These tend to resolve quickly in chat because the decision space is small and the stakes are low.

Some gaps may cluster — resolving one (e.g., "how do notifications work") may settle several related cells at once. The Stage-1 landscape grouping in `audit-summary.md` is designed to surface these clusters.

You may also choose to leave some Stage-1 cells unresolved if they're far downstream with no current dependencies — they don't block any work that's ready now and can be resolved when their turn comes in the workplan.

## When you're done

Step 5 (workplan production) needs meaningful variation in Stage values across the grid to produce useful output. "Done enough" for Step 4 means: the cells that are foundational or mid-dependency-chain have been resolved past Stage 1, even if leaf-node/downstream cells remain.
