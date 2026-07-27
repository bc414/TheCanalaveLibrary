---
name: doc-audit
description: >
  Fresh-eyes audit of The Canalave Library's process docs (.claude/ corpus + CLAUDE.md).
  Use after any doc restructure, at phase gates, or when doc drift is suspected. Runs the
  three probe shapes that catch what the mechanical gates cannot: contradictions, stale
  tense, headline-vs-Stage-note drift, dead identifiers, and broken routing. The gates
  (check-doc-hygiene.ps1, check-design-tokens.ps1) catch registered retired terms and
  missing files; this skill catches everything that requires reading.
---

# Doc Audit — fresh-eyes probes for the process-doc corpus

## Why this exists

On 2026-07-27, three audit rounds fixed ~110 confirmed doc defects. The mechanical gates
caught only the *registered-term* class. Every other confirmed defect — contradictions,
tense drift, headline lines contradicted by later Stage notes in the same file, wrong
constant/type names, false file claims, routing dead ends — was found by an agent
**reading with fresh eyes**, not by lint. This skill packages those probes so any session
can rerun them without re-inventing the method.

**When to run:** after any restructuring of the doc corpus (splits, moves, section
rewrites — run the Integrity probe the same session); at phase-gate transitions in
`roadmap.md` (all three probes); or on suspicion. Not on a timer — retirement WUs
sweep at source now (CLAUDE.md §"Retiring or closing"), so drift accumulates slowly.

## Ground rules (all probes)

- **Fresh eyes beat author eyes.** Run probes as subagents that have NOT seen the session's
  edits. A session auditing its own restructuring missed defects that a cold agent found
  within minutes (the 2026-07-27 Position-block and folder_clusters-column damage).
- **Only report CONFIRMED defects** — both sides cited (`file:line` for the claim and for
  what contradicts it), with the *current* side identified via a third source (code, the
  grid, a dated later note). Verify suspicious claims against the codebase (Glob/Grep for
  named components/entities/methods) before reporting. Label anything unverified SUSPECTED
  with what would confirm it.
- **Fix findings the same session** where scope allows (the project's fix-same-session
  discipline), or mint a WU; either way sweep all three open-work ledgers per CLAUDE.md.
- **Derived-state blocks get claim-by-claim re-verification** — the Position block, the
  status.md standing constraints, any summary "as of" paragraph. Their defects come from
  claims written from memory, so audit them against sources, not against plausibility.

## The three probes

Run as parallel background Explore agents. Adapt file lists to what changed; keep the
probe *shapes*.

### Probe 1 — Cold-session orientation walk

Roleplay a session that has never seen the project, arriving to do feature work. Read
CLAUDE.md, follow its read order (status.md → workplan.md Position → roadmap.md), then
pick two plausible next tasks (one from the Position block, one from
`hidden-deferrals-tracker.md`) and walk each task's full doc trail. Report: friction
points (anywhere you guessed or reconciled two docs), contradictions verified against a
third source, dead ends (pointers to sections/files that don't exist or don't contain
what's promised), which docs earned their place vs. read cost, and a
solid/adequate/shaky verdict with the top 3 concrete improvements. This probe tests the
corpus's actual job — routing a cold reader to a task's inputs.

### Probe 2 — Restructure integrity check

Scope: whatever the recent surgery touched. Verify the surgery introduced no damage:
completeness (nothing lost at cut seams — for splits/moves, diff unique lines of the
pre-change file against the post-change pieces), directional language ("above/below/end
of this file" that now crosses a file boundary), line-number citations from OTHER files
into the restructured one, summary blocks whose claims were carried instead of
re-verified, and content that rode along into the wrong destination (the WU39-into-archive
class). Then explicitly list what was verified sound — the absence report is half the value.

### Probe 3 — Untouched-tail staleness probe

Scope: files recent work did NOT touch (they escape both the gates' registries and the
session's attention). Probe for the known defect classes: (1) headline/summary lines
contradicted by later dated notes in the same file, (2) claims superseded by paradigm
shifts (check the status.md standing-constraints list for the current shift inventory),
(3) references to deleted/renamed code (verify against the codebase, not other docs),
(4) internal cross-references that don't resolve. Read small files fully; for large files
read all headers + spot-check N claims against code. Report a coverage note (read fully
vs. skimmed) and a verdict on whether the tail is dirtier or cleaner than the recently
swept files.

## Known standing exemptions (don't re-report)

- `surface-registry.md` — paused artifact, bannered, gate-exempt, ground-up rewrite
  scheduled post-foundation (Brian, 2026-07-27). Its per-component inventory is knowingly
  stale; do not audit it until the rewrite.
- `workplan*.md` dated DONE entries and `audit/*.md` dated Stage notes are as-of-date
  records — dead names inside them are legitimate history. Headline stage lines and
  section headers ARE auditable (the moment-3 rule keeps them current).
- Retired/discharged docs (banner in first lines) are out of scope entirely.

## After the audit

Fold results into: fixes (same session or a WU), gate-registry additions for any newly
retired term, a `check-doc-hygiene.ps1` scope change if a blind spot was structural, and
one line in the WU entry recording probe coverage — so the next audit knows what was
last verified and when.
