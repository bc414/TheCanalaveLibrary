# Step 3: Classify the Grid

> **Tool:** Opus in Claude Code (direct, not opusplan — every cell's Stage assignment is a judgment call)
> **Input:** Grid axes from Step 2, skill files from Step 1, `canalave_library_unified_spec.md`, the codebase
> **Output:** `.claude/status.md`, `.claude/audit/<FolderName>.md` (one per folder cluster), `.claude/audit-summary.md`. May also refine skill files.
> **Constraint:** Read-only on the codebase. All findings go into markdown artifacts only.

CLAUDE.md defines the Stage framework and file schemas. It is already loaded.

## Codebase Context

The codebase was started approximately 7 months ago, then paused. It reflects decisions made *during*
implementation that may not appear in the spec — the spec captures the end of the design-conversation
arc, while code embodies earlier refinements.

The developer was transitioning from .NET 4.5/WPF/WCF to modern Blazor/EF Core. Gemini 2.5 Pro (the
AI model used during implementation) sometimes produced outdated patterns. Both factors mean code may be
functionally correct but paradigmatically unsound — patterns that compile today but won't compose well.

Code organization is mid-transition from horizontal (folder-per-type) to vertical (folder-per-feature).

## Layer Model

The grid has 8 columns (with the UI layers as a three-part split from Step 2):

| Layer | Name | Concern | Blocked On |
|---|---|---|---|
| 1 | Data Model | EF Core entities, Fluent API, migrations | Nothing |
| 2 | Server Implementation | Service interfaces, DTOs, server impls | Layer 1 |
| 3 | UI Logic | Parameters, services, events, @code behavior | Layer 2 |
| 3.5 | UI Structure | Component composition, HTML skeleton, @if/@foreach | Layer 3 + knowing what components exist |
| 4 | UI Style | Tailwind classes, sprites, responsive variants | Layer 3.5 + design tokens locked |
| 5 | WASM Enablement | API endpoints, client services | Layers 1–4 stable |
| 6 | SQL Indexes | Filtered, composite, GIN indexes (pure DDL) | Layer 2 query patterns stable |
| 7 | Redis Integration | Write-behind, cache, ephemeral store, workers | Layers 1–2 stable |
| 8 | Data Mart Workers | Non-EF-Core background workers: raw SQL, table swap, recursive CTEs | Layers 1–2 stable |

**The dependency chain within UI layers is strict:** Logic → Structure → Style. A cell cannot be
Stage 5 at Structure if its Logic cell is Stage 1. A cell cannot be Stage 5 at Style if Structure is
unstable.

**Layer 4 (Style) has a global blocker:** `tailwind.config.js` must be locked before any Style cell
can advance past Stage 2. This is expected — nearly all Style cells will be Stage 1 or Stage 2 during
this classification pass, and that is not a gap to be alarmed about.

## Valid Cell Values

Each cell holds one of: **Stage 1**, **Stage 2**, **Stage 3**, **Stage 4**, **Stage 5**, **Stage 6**, or **N/A**.

**N/A** means the layer genuinely does not apply to this feature. Examples: Lookups/ has no UI components (L3, L3.5, L4 are N/A). Export/ has no schema impact (L1 is N/A). Identity pages are permanently ineligible for WASM (L5 is N/A).

**N/A is not a synonym for "not yet needed" or "blocked."** A cell blocked on prerequisites (e.g., L4 Style blocked on design tokens) is classified by its actual state (Stage 2 — intent settled, can't build yet) with a note naming the blocker.

## Evaluation Framework: Two Lenses Per Cell

Evaluate each cell through two independent lenses. A cell failing either lens is not Stage 5.

**Intent-alignment.** Does what exists match the project's design intent? When spec and code disagree,
code is the more likely authority on intent (it's the later artifact), but this is a default, not a
rule — adjudicate case by case.

**Paradigm-correctness.** Is the code idiomatic and sound? Use the skill files as the reference.
Code can match intent perfectly and still be technically unsound.

### Additional Context for UI Layers

For Layers 3, 3.5, and 4, the classification also considers:

- **Component tier classification:** Is the component correctly a leaf, composite, or page? Does it
  inject services when it shouldn't (leaf injecting a read service), or fail to inject when it should
  (cross-cutting component receiving data through unnecessary prop drilling)?
- **Outer margin rule:** Do components apply outer margins on their root element? This is a paradigm
  violation per the conventions.
- **Desktop/mobile split rationale:** If a component has separate desktop/mobile variants, is it
  because of genuine structural difference, or could responsive prefixes handle it?

## Stage Assignment Guidance

The Stage Definitions table in CLAUDE.md is the authoritative reference. Additional elaborations:

**Stage 1 gap types.** For each Stage-1 cell, note whether the gap is *conceptual* (spec never
addressed it), *code-relationship* (spec and code diverge), or *blocked* (depends on a prerequisite
that hasn't been met — e.g., all Layer 4 Style cells blocked on design tokens). This determines the
resolution venue:
- Conceptual → Sonnet in chat with skill files as context
- Code-relationship → Claude Code investigation
- Blocked → note the blocker; no action until prerequisite resolves

**Stage 4 neutrality.** Describe *what disagrees and why*, and note the *implied resolution stage*.
Don't pre-judge the resolution.

**Layer 4 (Style) expected state.** Most or all Style cells will be Stage 1 (design tokens not yet
locked) or Stage 2 (structural decisions exist but no implementation). This is correct and expected.
Do not treat it as alarming in the audit summary.

**Layer 3.5 (Structure) expected state.** Many Structure cells will be Stage 1 or Stage 2 because
the component system hasn't been fully enumerated. The spec's §5.30 contains detailed component
specifications for chapters, tags, interactions, search, and universal components — classify those
cells using that input.

**N/A classification.** Use N/A only when the layer genuinely does not apply. Most features have
some content in most layers. A feature with no UI (Lookups/) has N/A for L3, L3.5, L4. A feature
with no high-frequency writes has no Layer 7 content — but whether to mark it N/A or Stage 2
("could benefit from caching later") is a judgment call.

**Stage 6 is outside this step's output range.**

## Axis Integrity

If the codebase reveals a feature or layer-candidate not captured in Step 2's axes, **flag it
separately** in `audit-summary.md` rather than silently expanding the grid.

## Feature-Level Input Documents

The foundational input is `canalave_library_unified_spec.md`, which consolidates all prior source documents (web chat spec, code assist spec, Step 2 insights, Step 2 deliberations, reading status design). Key sections for classification:

- §3.3 — Eight-layer architecture definitions
- §3.11 — Component taxonomy (leaf/composite/page with subtypes)
- §3.12 — Service injection rules
- §3.13 — Tailwind component conventions (outer margin rule, parameter-based variants)
- §4 — Complete database schema with UserStoryInteraction reading status design
- §5.3 — Three-axis search architecture (Source/Filter/Sort)
- §5.29 — Page inventory with routes
- §5.30 — Feature-level component specifications
- §7.4 — Folder clusters with example classes
- §9.2 — Three-phase implementation ordering (Atoms → Integration Points → Consumers)

## Required Outputs

All outputs conform to the schemas in CLAUDE.md's File Schema section.

**`.claude/status.md`** — The Feature × Layer → Stage grid. The Layer columns are:
`L1 | L2 | L3-Logic | L3.5-Structure | L4-Style | L5 | L6 | L7 | L8`

Cells hold Stage 1–6 or N/A. No prose, no dependencies — dashboard only.

**`.claude/audit/<FolderName>.md`** — One per folder cluster. Opens with shared context (entities,
services, components, current file locations in the repo). Then per-feature sections with per-layer
stage classifications. For UI layers, note the component tier classification (leaf/composite/page)
for each component the feature contributes. Stage-specific content per CLAUDE.md's "What goes in
`.claude/audit/<FolderName>.md` per stage" table.

**`.claude/audit-summary.md`** — Human-facing, bounded to five sections:
1. Rough stage-distribution across the grid (sense of scale).
2. Genuinely surprising findings.
3. Reconciliation index: Stage-4 cells with one-line reason each.
4. Stage-1 landscape: breakdown grouped by layer and pattern.
5. **UI component inventory:** list of all components identified across features, classified by tier
   (leaf/composite/page), noting which are universal (used by multiple features) vs. feature-specific.
   Include the component name, tier, which folder it belongs to, and which other folders consume it.

**Skill file refinements** — If the codebase reveals project-specific patterns worth encoding that the
baseline didn't cover, add them.
