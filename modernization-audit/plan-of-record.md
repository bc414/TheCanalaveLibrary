# Pre-Lock-In Modernization Audit — Plan (v3, ratified structure)

## Context

Before serious human testing and lock-in, audit this largely Claude-generated codebase (~57k non-migration product LOC + ~30k test LOC, 1400+ tests green) against modern C#/Blazor/EF Core practice. Output is a prioritized findings report, not fixes.

Settled (2026-07-17, in chat):
- **Dual baseline:** Bucket A = code violates `canalave-conventions`; Bucket B = a convention diverges from current ecosystem practice (Brian's decision via doc-touch); Bucket C = pure idiom.
- **Full tiered inventory:** Tier 1 must-fix / 2 should-fix / 3 cosmetic. Findings against Stage 5/6 cells are proposed reopens.
- **Two-part hybrid:** Part 1 = wide calibration by the main session, ending at a **human checkpoint** that ratifies Part 2 agent scopes. Part 2 = sequential fresh-context slice sweeps + synthesis.
- **Part 1 formally audits the shared atom clusters** (highest fan-out code gets the strongest reader); S1 is slimmed accordingly.
- **Workspace:** repo-root directory, tracked in git.

## Reference-frame rule (governs every finding)

No code is ever a reference frame — not atoms, not majority patterns, not the calibration baseline. The frames are:
1. The ratified conventions (Bucket A) — themselves challengeable via Bucket B.
2. Current ecosystem practice (Bucket B) — every claim verified against live official docs, never from training memory.
3. Internal consistency — **symmetric**: a divergence between two pieces of code is a lead citing both sides with direction undetermined; the minority is not presumed wrong, the majority is not presumed right.

Seam-mismatch findings (consumer vs shared atom) always cite both sides: `route: seam — direction undetermined`.

## Workspace

`modernization-audit/` at repo root, committed:
```
modernization-audit/
  README.md          ← progress header: phase, frozen slices, next action (the resume point)
  report.md          ← executive report (written last; the only doc Brian must read)
  calibration.md     ← Part 1: unwritten-pattern baseline + descriptive atom seam records (non-normative) + proposed slices
  hypotheses.md      ← hypothesis list = coverage matrix (single writer: main session)
  dimensions.md      ← ratified patterns-inventory section list
  slices/<n>-inventory.md   ← per-slice patterns inventory (written by that slice's agent)
  slices/<n>-findings.md    ← per-slice findings ledger (written by that slice's agent)
  slices/0-atoms-findings.md ← Part 1's atom-audit findings (written by main session)
  bucket-b.md        ← conventions-vs-ecosystem findings
  verification.md    ← Tier-1 adversarial verdicts
```

**Write policy (three-way):** source code / tests / configs — never written. Ratified process docs (`status.md`, `workplan.md`, `.claude/audit/*`, skills, spec, plans) — never written. `modernization-audit/` — the sole write target.

---

## Part 1 — Calibration (main session, effort `xhigh` — Brian sets via `/model` before starting)

Reads:
- All 19 `canalave-conventions` files (skim) + `audit-summary.md` + `folder_clusters.md`.
- **Atom clusters in full, as their formal audit** (~1.8k LOC: RichText, Errors, Layout, Controls, Pagination, Users, Toasts, Dialogs, Indicators, Drafts): full lens checklist applied, findings filed to `slices/0-atoms-findings.md` (same schema, same suspicion, same Tier-1 verification later as any code), plus a **descriptive** seam record per atom in `calibration.md` (what its contract *is* — parameters, expectations, invariants — for later consumer-slice comparison; explicitly non-normative).
- 2–3 representative files per remaining cluster (~10–15% of LOC). Baseline-building and hypothesis generation only — anomalies are logged, not chased.

Outputs (all written before the checkpoint):
1. `calibration.md` — unwritten-pattern baseline, atom seam records, entanglement observations, proposed slice amendments with reasons.
2. `slices/0-atoms-findings.md` — atom findings.
3. `hypotheses.md` — per entry: pattern, origin, check procedure, empty per-slice result cells. Seeded with the known historical failure classes: WU-ComponentSoundness F1–F3 (route-param reload in `OnParametersSetAsync`, `@key` on stateful list children), WU-L6 (unnamed `HasIndex` overwrite), WU-BrowserPass (read-context concurrency), bare-name dead Tailwind classes.
4. `dimensions.md` — fixed inventory headings (seed: pagination, DTO mapping, error surfacing, form patterns, flyout/dropdown mechanics, optimistic updates, disposal, query shape, debounce; extend with what calibration shows actually varies).
5. Per-cluster **test** LOC measured; each provisional slice's total load (product + test) stated in the proposal.

## CHECKPOINT — Human review (hard stop)

Present: calibration summary, atom findings so far, hypothesis list, proposed slice scopes with total (product+test) loads. **Brian ratifies or amends scopes and hypotheses; nothing in Part 2 launches before sign-off.** After sign-off, slices freeze (mid-sweep re-slicing forbidden — it breaks checkpoint semantics and read-once). Ratified scopes recorded in `README.md`.

---

## Part 2 — Slice sweeps and synthesis

### 2a. Slice sweeps — sequential `general-purpose` agents, effort `high` (set per launch by main session)

One agent per ratified slice. Prompt embeds: lens checklist, relevant convention-file paths (read first), `hypotheses.md`, `dimensions.md`, the atom seam records, **all prior slices' inventories**, and both artifact templates. The agent:
- fully reads every product + test file in its slice (migrations excluded; targeted L1/L6 checks only);
- writes `slices/<n>-inventory.md` (headings exactly per `dimensions.md`, ≤ ~3k tokens) and `slices/<n>-findings.md`;
- reports hypothesis-check results **inside its findings file** (main session transcribes into `hypotheses.md` — matrix stays single-writer);
- judges code only against project conventions; suspected ecosystem drift becomes a `B-flag` entry, never a ruling. Web use is prohibited by instruction and enforced at validation: any Bucket-B ruling in a slice file is rejected.

Main session validates each pair on receipt (citations present, evidence quoted verbatim, headings match, hypothesis results reported), transcribes matrix cells, updates `README.md`, launches the next agent.

**Lens checklist (every agent, every file):**
1. EF Core / data access — N+1, projection discipline, tracking, factory-per-method compliance, SaveChanges/transaction use, raw-SQL hygiene, `HasIndex` naming.
2. Blazor component correctness — lifecycle (`OnParametersSetAsync` reload), `IDisposable`/`IAsyncDisposable`, `@key`, `EventCallback` patterns, `StateHasChanged`, `[PersistentState]`, InteractiveAuto/WASM safety.
3. Service/DI/async — lifetimes vs capture, `async void`/`.Result`/fire-and-forget, CancellationToken plumbing, DTO-firewall and project-boundary violations.
4. Modern C# idiom (C# 14/.NET 10) — primary constructors, collection expressions, records vs mutable DTOs, pattern matching, nullability honesty. Mostly Bucket C/Tier 3; inconsistency *between* files is Tier 2.
5. Dead & vestigial code — unreferenced members/components, stale comments describing superseded designs, legacy-folder stragglers.
6. Feature-local conventions — `@namespace`, naming rules (incl. `UserStoryInteraction` prefix), component-tier discipline, colocated-asset paths.
7. Test quality (lighter) — setup duplication, over-specified assertions, tier misassignment per `testing.md`.
8. Seam usage — compare consumption of shared atoms against the descriptive seam records; mismatches are symmetric findings per the reference-frame rule.

### 2b. Conventions-vs-ecosystem agent — one agent, effort `xhigh`

Reads the 19 convention files + all `B-flag` entries. Compares against current .NET 10 / Blazor / EF Core 10 guidance; every drift claim verified via WebFetch/WebSearch of official docs before assertion. Never relitigates the ten Settled Architectural Axioms on their merits; may report that an axiom's *stated rationale* is invalidated by framework evolution. Writes `bucket-b.md`.

### 2c. Cross-slice comparison + lead-chasing (main session)

Mechanically diff inventories (same headings). Each divergence and each seam mismatch is a lead: main session re-reads only the cited spans side by side, then promotes to a finding (with evidence, direction undetermined where applicable) or dismisses with a note.

### 2d. Tier-1 adversarial verification — batch of fresh agents, effort `xhigh`

Every Tier-1 finding (including Part 1's atom findings) gets one independent agent prompted to REFUTE it, reading only the cited evidence spans + relevant `.claude/audit/*` settled notes. Verdicts to `verification.md`; refuted findings dropped or demoted with a note. Only confirmed findings keep Tier 1.

### 2e. Executive report (main session)

`report.md`: executive summary; findings tiered and bucketed, deduplicated; each with grid-cell impact + reopen flags + effort size (S/M/L) + route (Stage-4 reconcile / mechanical sweep / doc-touch decision / seam — direction undetermined); every finding cross-checked against the 27 `.claude/audit/*.md` settled-vs-open notes and `middle_plan_v2.md` open decision rows 1–4, 6, 8, 10–12 (collisions marked "gated by decision row N"); a "deliberately not flagged" section with settling references; coverage statement per area, sourced from the `hypotheses.md` matrix.

---

## Artifact schemas

**Inventory entry** (one per heading in `dimensions.md`):
```markdown
### Pagination
mechanism: <what this slice's clusters actually do>
exemplar: <file:line>
deviations: <intra-slice divergences, file:line> | none observed
```

**Finding** (append-only; `B-flag` is a legal tier value):
```markdown
### MA-<id> | Tier <1|2|3|B-flag> | Bucket <A|B|C> | Slice <0..7>
claim: <one-sentence defect statement>
evidence: `<file:line>` — "<verbatim offending line(s)>"   ← both sides for seam findings
cells: <feature# layer> (+ "proposes reopen" if Stage 5/6)
effort: <S|M|L> | route: <Stage-4 reconcile | mechanical sweep | doc-touch decision | seam — direction undetermined>
verify: [pending|confirmed|refuted|demoted]
```

**Hypothesis entry** (matrix written only by main session):
```markdown
### H-<id>: <pattern description>
origin: <what triggered it>
check: <procedure — "for every X verify Y">
results: S1[ ] S2[ ] S3[ ] S4[ ] S5[ ] S6[ ] S7[ ]   ← "clean" | MA-ids | "n/a"
```

## Standing rules

- Write policy as above; codebase and ratified docs are read-only.
- Agents sequential; each product/test file fully read exactly once across the audit. Sanctioned span-level exceptions: Part 1's representative samples, 2c lead-chasing, 2d verification re-reads of cited evidence.
- Checkpoint after every agent; `README.md` is the resume point after any interruption.
- Effort — two knobs: main session `xhigh` (Brian sets once via `/model`); subagent effort passed per Agent launch (2a `high`; 2b/2d `xhigh`). No manual changes needed between steps.
- Reference-frame rule applies to every finding (see top).
- Excluded from reads: `Migrations/` (except targeted checks), `Fimfiction/`, `GeminiDiscussions/`, screenshot folders, `.idea/`.

## Provisional Part 2 slices (checkpoint input — NOT final)

Measured non-migration product LOC; test LOC measured and added during Part 1. Atom clusters (~1.8k) moved to Part 1.

| # | Provisional slice | Clusters | ~product LOC |
|---|---|---|---|
| S1 | Foundation | Data, Server root/Program, Images, Sprites, Lookups, SiteSettings, Security, Diagnostics, DevTools, Components, Http, Telemetry, Seo, Home, legacy folders (Models, Services, ServiceInterfaces, Pages, Account) | ~6.3k |
| S2 | Stories & structure | Stories, Series (incl. Arcs, Lineage) | ~7.0k |
| S3 | Chapters & ingestion | Chapters, Import, Export | ~6.6k |
| S4 | Discovery & interaction | Discovery, Tags, UserStoryInteractions, Bookshelves, CustomLists | ~10.0k |
| S5 | Social | Comments, Recommendations, Following, Messaging, Groups | ~8.3k |
| S6 | Identity & profiles | Identity, Profiles, Badges, Notifications | ~9.2k |
| S7 | Publishing & moderation | BlogPosts, Moderation, Spotlight | ~7.5k |

Test-file mapping rule: a test file belongs to the slice owning the cluster it exercises; ambiguous files assigned at checkpoint. S1 runs first; remaining order S2→S7, amendable at checkpoint.

## Verification of the audit itself

- 2d adversarial pass is the correctness gate; findings without citations are rejected at validation.
- `hypotheses.md` matrix + per-area coverage statement make gaps explicit, never silent.
- Evidence quotes make every finding spot-verifiable by Brian without re-reading files.

## After the report (out of scope)

Brian picks findings from `report.md`; accepted ones become work-units through the normal Stage machinery: Bucket A → Stage-4 reconcile or mechanical-sweep WUs; Bucket B → doc-touch moment-1 decisions; Tier 3 → optional batched idiom sweeps; seam findings → direction decided per finding. Stage 5/6 reopens only with per-finding sign-off.
