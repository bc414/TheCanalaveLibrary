# Status Grid — Feature × Layer → Stage

Dashboard only. Stage values per CLAUDE.md (1–6 or N/A). Rows are the dependency-ordered
features from `grid_axes.md`, grouped by folder cluster. Columns:

`L1 | L2 | L3-Logic | L3.5-Structure | L4-Style | L4.5-Browser | L5 | L6 | L8`

(L7 — formerly "Redis Integration" — dissolved 2026-07-06, WU-SignalBuffering: redistributed into
L2 signal buffers / L6 MVCC tuning / L8 marts; L8 keeps its historical number. Detail:
`grid_axes.md` "Layer 7 — dissolved".)

L4.5-Browser is the end-to-end browser-verification band — defined in `grid_axes.md`
"Layer 4.5 — Browser Verification".

Global conditions — **standing constraints only** (genre rule, 2026-07-27): each bullet states a
fact that currently binds new work. When it stops binding, delete it — the event history lives in
`workplan.md`/`workplan-archive.md`'s dated entries, and per-cell narrative lives in audit files.

- **Orientation:** `roadmap.md` is the live master plan (2026-07-27; retired the unsustainably-named
  chain `middle_plan_v2.md` ← `middle_plan.md` ← `forward_plan.md`, all now historical references —
  full historical Resolved log stays in `middle_plan_v2.md`); `workplan.md` is the work-unit ledger
  and carries the Position block (DONE entries older than the recent window: `workplan-archive.md`).
- **No Stage-4 cells remain.** The spec-supersedes-stale-code adjudication doctrine is retained in
  `audit-summary.md` §0/§3 for any future reopening.
- **L1 migration-verified.** Every L1 Stage-5 cell has an applied migration. Detail:
  `layer1-data-model.md` §"Fluent API Organization".
- **The site runs global `InteractiveAuto`** (Global Flip, 2026-07-13). L5 column semantics:
  Stage 5 = endpoint + client impl built and registered — behavioral verification is tracked
  separately. Detail: `layer5-wasm.md` §"L5 Stage Semantics".
- **Single responsive site; L4.5 claims cover desktop width** (WU-ResponsiveMerge, 2026-07-18).
  No device detection, no `{X}Desktop`/`{X}Mobile` forks; narrow-viewport rendering is provisional
  pending the future mobile phase. Detail: `render-and-layout.md` §"Responsive Layout Architecture".
- **Design system locked and enforced.** Element-role constitution + `@theme` token manifest
  (locked 2026-07-10); `scripts/check-design-tokens.ps1` (local + CI) fails violations;
  `/dev/design-gallery` is the living reference. Visual sign-off of individual pages is the
  standing Phase-3 human pass — that is why L4 cells sit at Stage 1/3 with built, functional UI.
  Detail: `layer4-style.md`, `.claude/design/surface-registry.md`.
- **Parent-visibility invariant** (conditionality kind (g)): child content is never more visible,
  nor more writable, than the parent hosting it. Enforced by `ParentVisibilityContractTests`
  (the enrolment list is the mechanism — adding a parent-scoped read/write means adding a row).
  Detail: `identity-and-authorization.md` §"Parent-visibility guards".
- **Viewer access gating (Feature 66):** the three-plane model (Discovery zero-trace / Direct-nav
  consent interstitial / Personal never rating-filtered) governs every M-rated read. Model:
  `design/access-gating-first-principles.md`; ledger: `audit/AccessGate.md`.
- **Feature 56 CUT (2026-07-18).** Its grid row is gone — the number is retired, not renumbered.
- **Dev run paths:** server-only against local Postgres is the default; the Aspire path adds
  containerized Postgres/Redis/Garage S3/Mailpit; `SeedTool` volume is on-demand only, never on
  startup or test paths. Detail: `run-server/SKILL.md`.
- **Code organization:** vertical folder-per-feature clusters; no new files in deprecated legacy
  technical-layer folders (they retire just-in-time). Detail: `canalave-conventions/SKILL.md`
  §"Code Organization".
- **CI is PR-only + manual dispatch (no master-push trigger) — deliberate until launch.** Detail:
  `.github/workflows/ci.yml` header comment; `middle_plan_v2.md` §Resolved.
- **Doc-hygiene gate:** `scripts/check-doc-hygiene.ps1` (local + CI) fails on retired terms,
  session-relative language, and retired-plan pointers in live docs; retirement WUs append their
  term to its registry (rule in CLAUDE.md §"Retiring or closing").

| # | Feature | Folder | L1 | L2 | L3-Logic | L3.5-Struct | L4-Style | L4.5-Browser | L5 | L6 | L8 |
|---|---------|--------|----|----|----------|-------------|----------|--------------|----|----|----|
| 1 | Identity & Auth | Identity | 5 | 5 | 5 | 5 | 1 | 5 | N/A | N/A | N/A |
| 2 | Lookup Tables & Seed Data | Lookups | 5 | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 3 | Sprite & Theme System | Sprites | 5 | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 4 | Story Creation & Editing | Stories | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 5 | Story Browsing & Display | Stories | 5 | 5 | 5 | 5 | 1 | 5 | 5 | 5 | N/A |
| 6 | Chapter Writing & Versioning | Chapters | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 2 | N/A |
| 7 | Chapter Reading | Chapters | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 2 | N/A |
| 8 | Story Arcs | Stories | 5 | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 9 | Series & Ordering | Stories | 5 | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 10 | Story Lineage | Stories | 5 | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 11 | Tag Administration | Tags | 5 | 5 | 5 | 5 | 1 | 5 | 5 | 5 | N/A |
| 12 | Story Tagging | Tags | 5 | 5 | 5 | 5 | 1 | 5 | 5 | 5 | N/A |
| 13 | Tag Display & Sprites | Tags | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 14 | Tag Filtering & Selection UI | Tags | N/A | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 15 | Saved Tag Selections | Tags | 5 | 5 | 5 | 5 | 1 | 1 | 5 | 5 | N/A |
| 16 | Story Interaction State Writes | UserStoryInteractions | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 17 | Story Interaction Lists & Bookshelves | UserStoryInteractions | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 18 | User Following | Following | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 19 | Vouches | Following | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 20 | User Profile Editing | Profiles | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 21 | User Profile Display | Profiles | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 22 | User Stats | Profiles | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 23 | Comment Posting | Comments | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 24 | Comment Display & Pagination | Comments | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 25 | Comment Likes | Comments | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 26 | Spoiler Comments | Comments | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 27 | Recommendation Submission | Recommendations | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 28 | Recommendation Display | Recommendations | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 29 | Hidden Gem Management | Recommendations | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 30 | Recommendation Attribution | Recommendations | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 31 | Search Page | Discovery | N/A | 5 | 5 | 5 | 1 | 5 | 5 | 5 | N/A |
| 32 | Full-Text Search | Discovery | 5 | 5 | 5 | 5 | 1 | 5 | 5 | 5 | N/A |
| 33 | Manual Tree Search | Discovery | N/A | 5 | 5 | 5 | 1 | 5 | 5 | 2 | N/A |
| 34 | Tag Directory | Discovery | N/A | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 35 | Blog Post Writing | BlogPosts | 5 | 5 | 5 | 5 | 1 | 5 | 5 | 2 | N/A |
| 36 | Blog Post Display | BlogPosts | 5 | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 37 | Polls | BlogPosts | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 38 | Group Management | Groups | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 2 | N/A |
| 39 | Group Content & Folders | Groups | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 40 | Group Display | Groups | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 41 | Notification Generation | Notifications | 5 | 5 | N/A | N/A | N/A | 5 | N/A | 5 | N/A |
| 42 | Notification Display | Notifications | 5 | 5 | 5 | 5 | 1 | 5 | 5 | 5 | N/A |
| 43 | Notification Settings | Notifications | 5 | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 44 | Reading Progress Tracking | Chapters | 5 | 5 | 5 | 5 | N/A | 5 | 5 | N/A | N/A |
| 45 | View Count Tracking | Stories | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 46 | Content Reporting | Moderation | 5 | 5 | 5 | 5 | 3 | 5 | 5 | 5 | N/A |
| 47 | Moderation Queue & Actions | Moderation | 5 | 5 | 5 | 5 | 3 | 5 | 5 | 5 | N/A |
| 48 | Story Approval Workflow | Moderation | 5 | 5 | 5 | 5 | 3 | 5 | 5 | N/A | N/A |
| 49 | Private Messaging | Messaging | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 50 | Badge System | Badges | 5 | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 51 | Custom Lists | CustomLists | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 52 | User Account Deletion | Identity | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A | N/A |
| 53 | External Story Links & Verification | Stories | 5 | 5 | 5 | 5 | 1 | 5 | N/A | N/A | N/A |
| 54 | Content Download/Export | Export | N/A | 5 | N/A | N/A | N/A | 5 | N/A | N/A | N/A |
| 55 | Community Spotlight | Spotlight | 5 | 5 | 5 | 5 | 3 | 5 | 5 | N/A | N/A |
| 57 | Notification Cleanup Worker | Notifications | N/A | 5 | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 58 | UserStat Recalculation Worker | Profiles | N/A | 5 | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 59 | Automatic Tree Search | Discovery | N/A | 5 | 5 | 5 | 1 | 5 | 5 | N/A | 5 |
| 60 | Tree Search Data Mart Worker | Discovery | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | 5 |
| 61 | Also Favorited / Also Recommended | Discovery | N/A | 5 | 5 | 5 | 1 | 5 | 5 | N/A | 5 |
| 62 | SiteDailyStat Worker | Moderation | 5 | 5 | 5 | 5 | 3 | 5 | N/A | N/A | 5 |
| 63 | Chapter Import (file ingestion) | Import | N/A | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 64 | Site SEO | Seo | N/A | 5 | 5 | 5 | N/A | 5 | N/A | N/A | N/A |
| 65 | Accessibility | — | N/A | N/A | N/A | N/A | 1 | 1 | N/A | N/A | N/A |
| 66 | Viewer Access Gating | ContentGate | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 |
