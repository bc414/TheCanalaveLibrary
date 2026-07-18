# Modernization Audit — Progress

Plan of record: `~/.claude/plans/this-codebase-was-made-parsed-dawn.md` (v3, approved 2026-07-17).
Workspace layout, artifact schemas, standing rules, and the reference-frame rule live in that plan.

## State

| Field | Value |
|---|---|
| Phase | **Part 1 — Calibration** (in progress) |
| Slices | Provisional S1–S7 (see plan); NOT frozen — ratified at the human checkpoint |
| Convention-doc skim | DONE (all 19 files + audit-summary + folder_clusters, 2026-07-17) |
| Atom audit (slice 0) | DONE — 13 findings in `slices/0-atoms-findings.md`; seam records in `calibration.md` |
| Cluster sampling | DONE — observations + unwritten baseline in `calibration.md` |
| Test LOC measured | DONE — per-slice totals in `calibration.md` slice proposal |
| hypotheses.md / dimensions.md | DONE — 20 hypotheses (S0 cells filled), 14 dimensions |
| Checkpoint | **RATIFIED by Brian, 2026-07-17.** 7 slices FROZEN (S4 kept whole — deliberate: clusters intertwined); hypotheses (20) + dimensions (15) frozen. Dimension 15 reframed same day per Brian: **code economy, NOT feature scope** — compression candidates (LOC saved / sites collapsed / machinery cost, classified pure-win vs trade vs false-economy) for the fixed feature set. S1's agent received the correction mid-run. |
| Phase | **Part 2 — Slice sweeps** (sequential) |
| Model note | Session moved off Fable 5 (out of credits) → **Opus 4.8** as of 2026-07-17. Subagents inherit Opus. |
| Phase | **AUDIT COMPLETE (2026-07-17).** Read `report.md` — the executive deliverable. All supporting files final. |
| Slices done | **S0(13) S1(23) S2(13) S3(9) S4(9) S5(11) S6(11) S7(9, MA-701..709) — ALL ACCEPTED + transcribed.** Coverage matrix 100% (20 H × 8 slices, no empty cells). ~98 findings total. |
| S7 result | Endpoint-authz class **does NOT recur** in Moderation/Spotlight (highest-privilege) — all gated server-side. Blog sanitizes; polls plain-text; TPT projections safe; MA-123 ruled (Moderation's 401 is the wrong side, should be 403). No Tier-1. |
| ⚠ SYSTEMIC THEME (verification.md) | **L5 endpoint authorization-deferral class — the audit's #1 structural finding.** MA-301 (Chapters write), MA-601 (Badges IDOR), MA-602 (UserProfile anon private-read) all CONFIRMED, all same root cause: WU-L5Sweep added endpoints with blanket `RequireAuthorization()` but DEFERRED per-resource authz ("flagged for the browser wave"); Global Flip made them live. **Service layer is clean; the mechanically-added endpoint layer is where authz holes cluster.** Report #1 rec = systematic authz audit of ALL ~40 endpoint files, not just the 3 found. S7 primed to hunt it. |
| Security-class status | MA-201 (XSS) localized to Stories. Profile bio (S6) sanitizes correctly. MA-102 (User.Roles phantom nav) CONFIRMED by S6. The AUTHZ class is NOT localized — it's the endpoint-layer theme above (4 Tier-1s: MA-301/601/602 + verify S7). |
| Tier-1 so far | MA-101 (ReconnectModal stale asset path); **MA-201 (stored XSS, Story LongDescription — CONFIRMED); MA-301 (broken access control, 5/7 chapter writes + GetChapterForEditAsync — CONFIRMED).** S4 = NO Tier-1 (cleanest slice; both security classes clean, mart raw-SQL parameterized). Verdicts in verification.md. |
| H-10 RESOLVED (S4) | UserStoryInteractionPanel.Dispose() **drops** the pending debounced write (MA-401, Tier 2) — toggle-then-navigate within 2s loses durable interaction intent. |
| Cross-slice resolutions | S1↔S0: MA-009/MA-006 downgraded (checker exempts). S2↔S0: H-08/H-09 StoryPage leads both real (MA-202/203). S3: **MA-201 class is CLEAN in Chapters/Import (sanitize properly) → XSS localized to Stories.** MA-304 extends the not-found divergence (now 9 sites across S2+S3). MA-308 cross-refs MA-210 (RequireUserId dup). Handoffs open: MA-102 (User.cs→S6), MA-107 (DI→many), MA-123 (Mod throw→S7), **H-10 USI-panel-dispose→S4.** |
| Code-economy so far | Pure wins small (dead files/pkg/column). Trades: HomeDesktop/Mobile, **StoryDesktop/StoryMobile (strongest, byte-identical @code)**, export-writer DOM-walk (~100 LOC). False economies rejected: DI-scan, TestImages-merge, **import-readers merge (genuinely per-format), export per-format emit.** |
| Next action | Synthesis: (1) conventions-vs-ecosystem (Bucket B) agent — web-verified, ~30 doc-vs-code flags accumulated; (2) cross-slice inventory comparison + lead-chasing; (3) Tier-1 verify batch (4 of 5 already spot-verified in verification.md; MA-101 ReconnectModal remains); (4) executive report.md. |
| Tier-1 ledger | MA-201 (Stories XSS) ✓CONFIRMED · MA-301 (Chapters authz) ✓CONFIRMED · MA-601 (Badges IDOR) ✓CONFIRMED · MA-602 (UserProfile anon-read) ✓CONFIRMED · MA-101 (ReconnectModal path) — pending verify. |

## Resume instructions (for a session picking this up cold)

1. Read the plan file above in full.
2. Read this README's State table.
3. Continue from "Next action". Never re-read files already covered by a completed artifact —
   `calibration.md`, `slices/0-atoms-findings.md`, and `slices/<n>-*.md` are the durable memory.
