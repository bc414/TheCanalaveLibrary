# Documentation Audit — Stale & Contradictory Content

**Date:** 2026-07-07. **Scope:** cross-checked `CLAUDE.md`, `.claude/status.md`, `.claude/grid_axes.md`,
`.claude/folder_clusters.md`, `.claude/workplan.md`, `.claude/middle_plan_v2.md` (+ retired
`middle_plan.md`/`forward_plan.md`/`next_steps.md`), `.claude/audit-summary.md`, all 23
`.claude/audit/<Folder>.md` files, and all 13 `.claude/skills/canalave-conventions/*.md` +
`run-server/SKILL.md` files against each other for staleness and contradiction. This file is a
one-time audit snapshot — once its items are actioned, its findings become historical and this
file doesn't need to be kept current itself.

Findings are grouped by how confidently they can be resolved without you: **§1 fixes I'd make
without hesitation** (verified, low-ambiguity), **§2 items that need your call** (genuine
ambiguity or a product/process decision), **§3 cosmetic/low-priority**.

---

## §1 — High-confidence fixes

### 1.1 `status.md`: L5 column shows Stage 5 for 11 rows that have no client implementation

**Rows:** 6 (Chapter Writing), 7 (Chapter Reading), 16 (Story Interaction State Writes), 17
(Interaction Lists & Bookshelves), 18 (User Following), 19 (Vouches), 23 (Comment Posting), 24
(Comment Display), 25 (Comment Likes), 26 (Spoiler Comments), 42 (Notification Display), 43
(Notification Settings).

**Verified directly** (not just from audit-file text): `TheCanalaveLibrary.Client/` contains
exactly four `.cs` files — `ClientTagReadService.cs`, `ClientTagWriteService.cs` (the Tags-cluster
WU-L5Pilot work), `WasmDeviceDetectionService.cs`, and `Program.cs`. There is **no client
implementation at all** for Chapters, Comments, Following/Vouches, UserStoryInteractions, or
Notifications. Per `grid_axes.md`'s own Layer-5 definition, "Stage-5 means: the feature works
identically whether DI resolves the server impl or client impl" — impossible with zero client
code. Every one of these 11 audit files independently states "L5 — Stage 2" in its own text (e.g.
`Chapters.md` F6/F7: *"L5 — Stage 2. L6 — Stage 2."*), and in every row the grid's L6 number
correctly matches its audit file — only L5 is wrong, and always inflated to match the neighboring
L4.5-Browser=5 column. That pattern (one column bleeding into its neighbor, consistently, across
unrelated feature clusters) is a copy/fill error, not 11 independent silent promotions.

**Fix:** set L5 → 2 for rows 6, 7, 16, 17, 18, 19, 23, 24, 25, 26, 42, 43 in `status.md`. No audit
file changes needed — they already say the correct thing.

### 1.2 `middle_plan_v2.md` Phase 1 item 3 claims already-closed scope

Item 2 (**WU-SignalBuffering — DONE**) states: *"MVCC churn managed by `R4_MvccStorageTuning`
(fillfactor + autovacuum; USI index audit: all 7 partial indexes justified, none dropped)."* Item
3 (**WU-L6 index batch + performance baseline**, not marked done) still lists its scope as *"the
v1 Phase 4 item 2 DDL (**UserStoryInteraction filtered indexes**, comment golden index, StoryTag
reverse index)."* Two independent agents flagged this same overlap from different angles
(`middle_plan_v2.md` itself, and separately `layer6-indexes.md`'s file banner) — `status.md`
confirms UserStoryInteractions L6 (rows 16/17) is already Stage 5, and `layer6-indexes.md`'s
"MVCC Storage Tuning (R4, 2026-07-06)" section documents the audit in the past tense. The USI
filtered-index work is done; only the comment golden index, StoryTag reverse index, and the
performance baseline remain open for item 3.

**Fix:** trim item 3's scope line to "comment golden index, StoryTag reverse index [+ performance
baseline]" and add "(UserStoryInteraction indexes: already audited/closed under item 2 — see
R4_MvccStorageTuning)".

### 1.3 `layer6-indexes.md`: file-level banner contradicts the file's own built sections

Line 1 reads: *"**Provisional — Stage 2 (unbuilt).** This file records design intent for post-MVP
work validated against the spec, not against built code."* This is wrong for at least the
"UserStoryInteraction Index Strategy" section, the FTS GIN index, the Vouch indexes, and the R4
MVCC-tuning section — all already built (`status.md` shows L6=5 for rows 16, 17, 19, 32, 46, 47).
It's still accurate for the golden comment index / StoryTag reverse index / notification index
sections, which remain Stage 2.

**Fix:** replace the single blanket banner with a per-section status note, or move the banner to
sit only above the sections that are genuinely still unbuilt.

### 1.4 `cross-cutting.md` "Aspire 13 Configuration" contradicts itself on MinIO vs. Garage

The section's opening summary of "the authoritative resource graph" (~line 987) still names
*"MinIO pinned-image `AddContainer` on 9000/9001"* as the dev S3 endpoint. Twenty lines later, the
same section correctly states *"The dev S3 endpoint is Garage... settled 2026-07-05, superseding
the spec's MinIO (OSS archived 2026-02)."* One file, two contradictory claims about the same
running container.

**Fix:** update the opening summary to name Garage and its actual port (S3 API 3900, per
`run-server/SKILL.md`), removing the MinIO/9000/9001 mention.

### 1.5 `layer2-services.md`: two smaller staleness spots

- Line ~416 names *"MinIO/R2"* as the swap target for `S3ImageStorageService` — should read
  "Garage/R2" (cosmetic, but in the same file as the more consequential item below).
- The "`StoryFilterDto` + `GetListingsAsync` (WU23)" section's "excluded by design" list says the
  per-`SearchMode` default-settings matrix is *"deferred post-WU23,"* but the file's own earlier
  "Discovery Defaults + Random Batch (WU28)" section documents `IDiscoveryDefaultsReadService`
  fully built against exactly that matrix. The WU23-era "deferred" note was never updated once
  WU28 closed it — a reader hitting the WU23 section first would wrongly conclude the matrix is
  still unbuilt.

**Fix:** swap "MinIO" → "Garage"; amend the WU23 bullet to "deferred post-WU23 — **built in
WU28**, see above."

### 1.6 `cross-cutting.md` "Read Replica Awareness" describes a replica that doesn't exist yet

This section is written in the present tense — *"Reads go to the PostgreSQL read replica...
Replication is near-real-time but eventually consistent"* — as if a physical read replica is
deployed today. It isn't: the conventions `SKILL.md` axiom itself frames it as *"read replica
**when scale demands**"* — the `ReadOnlyApplicationDbContext`/`ApplicationDbContext` split is
architectural *readiness* for a future replica, not an active replication topology. Neither run
path (`run-server/SKILL.md` server-only or Aspire) provisions more than one Postgres instance.

**Fix:** reword to describe the DbContext split as readiness for a future replica, and drop the
"eventually consistent / replication lag" framing — today it's just two connections to the same
database.

### 1.7 `workplan.md` preamble is stale v1-era framing with no pointer to what superseded it

Three separate problems in the same ~56-line preamble block:

- Lines 3–4 say new work-units are sequenced by `.claude/middle_plan.md` — but `middle_plan.md`
  is **retired**, superseded by `middle_plan_v2.md` (per CLAUDE.md's own file table). Confirmed
  in-file: entries through WU-Aspire (2026-07-05) cite `middle_plan.md`; every entry from WU-CI
  onward correctly cites `middle_plan_v2.md`. The switchover happened silently mid-ledger and the
  preamble was never updated.
- Lines 16–23 claim *"Scope of the numbered sequence = Layers 1–4... Layers 5–8 are post-MVP...
  every numbered work-unit below builds L2/L3/L3.5/L4."* This is directly contradicted by the
  file's own later entries — WU-L5Pilot (L5), WU-Aspire, WU-SignalBuffering (L7→L2/L6/L8),
  WU-Marts (L8) all landed pre-beta under `middle_plan_v2.md`'s platform-first inversion. A reader
  starting at the top of the file gets an actively wrong mental model before reaching the parts of
  the same file that contradict it.
- Line 20, *"MVP is InteractiveServer-only, no Redis/WASM,"* is doubly stale: WASM shipped
  (WU-L5Pilot) and Redis-as-a-concept was dissolved outright (WU-SignalBuffering), not merely
  deferred.

This is the same underlying staleness as **§1.8** below, independently confirmed by two different
agents reading two different files — strong signal it's real.

**Fix:** replace the preamble's scope/sequencing claim with a short pointer: "Sequencing prior to
2026-07-05 followed `middle_plan.md`'s features-first ordering (Layers 1–4 numbered, 5–8 batched
post-MVP); from WU-CI (2026-07-05) onward, `middle_plan_v2.md`'s platform-first inversion governs
— see that file's 'Why v2 exists' and its v1→v2 phase-mapping table." Point the "sequenced by" line
at `middle_plan_v2.md`, not the retired `middle_plan.md`.

### 1.8 `status.md`: "Layers 5–8 batched post-MVP" is stale terminology

Line 24's global-conditions note says *"Workplan exists... Layers 5–8 batched post-MVP."* This
reflects v1's ordering. `middle_plan_v2.md`'s "Why v2 exists" explicitly inverts it: platform
build-out (including L8 marts, done 2026-07-07, and most L2 signal-buffering work) now lands
*before* several MVP-surface-completeness rows (WU-Home, Series, Manual Tree Search, Account
Deletion UI) that are still pending. "Batched post-MVP" no longer describes what happened.

**Fix:** *"Workplan exists. `.claude/workplan.md` sequences the build; rows 8/37/51/55
blocked/deferred. Per `middle_plan_v2.md`'s inversion (2026-07-05), platform-layer work (L2 signal
buffering, L6 tuning, L8 marts) landed in Phase 1 ahead of several MVP-surface-completeness rows
still pending in Phase 2 — 'post-MVP' no longer describes the actual sequencing; see
`middle_plan_v2.md` 'Why v2 exists'."*

### 1.9 `workplan.md`: "WU-Redis" referenced as a live future work-unit after it was dissolved

Inside **WU-Observability** (DONE 2026-07-06): *"custom spans only where auto-instrumentation is
blind. **WU-Redis** consumes the seams as the named observability pilot for worker metrics"* and
*"...worker + hub stubs for **WU-Redis**/WU-SignalR."* Later the same date, **WU-SignalBuffering**
(also DONE 2026-07-06) states it *"Supersedes... middle_plan_v2's WU-Redis... Layer 7 dissolved."*
One file, same date, treats "WU-Redis" as both a forward-looking named unit and a retired name.

**Fix:** amend WU-Observability's two "WU-Redis" mentions to read "WU-Redis (later dissolved into
WU-SignalBuffering — see below)" or simply "the signal-buffering work" if rewriting in place is
easier than annotating.

### 1.10 `workplan.md` Post-MVP section lists an item WU-S3Garage already closed

The "Post-MVP — Layers 5–8" section still lists *"Image storage cloud backend... MinIO endpoint
via Aspire in dev, Cloudflare R2 endpoint in prod"* as pending/unsequenced. **WU-S3Garage** (DONE
2026-07-05) built exactly this and says so explicitly: *"F4 L2 and F20 L2 stay Stage 5 (cloud
backend was those cells' recorded open item — now closed)."* The ledger simultaneously claims the
item is open (Post-MVP list) and closed (WU-S3Garage entry), and still names MinIO instead of
Garage.

**Fix:** remove the bullet from Post-MVP (or strike it with "DONE — see WU-S3Garage").

### 1.11 `audit/Stories.md` Feature 5's L5 note describes already-deleted code

Feature 4's L5 note (dated 2026-06-27) records `HttpStoryWriteService`/`HttpStoryReadService`
being **deleted** as dead code (F4/F5 L5 → Stage 2). But Feature 5's own L5 bullet, a few lines
below, still reads: *"L5 — Stage 4. Only `GET /api/stories/{id}` mapped;
`HttpStoryReadService.GetStoryForEditAsync` calls `/{id}/edit` which is unmapped"* — describing a
client service that no longer exists. `status.md`'s current value (row 5, L5=2) matches Feature
4's resolution, not Feature 5's stale text.

**Fix:** replace Feature 5's L5 bullet with a pointer to Feature 4's note ("Stage 2, same
resolution as Feature 4 above — see WU-FilterRevamp").

### 1.12 `audit/Tags.md` Feature 11 intro line never updated after WU27.5

The Feature 11 section opens with *"L2 — Stage 2 (no admin/write service). L3/L3.5 — Stage 2..."*
but the same section's WU27.5 Stage note (a few paragraphs down) describes `ITagWriteService`,
`ServerTagWriteService`, `TagEditorForm`, and full test coverage — matching the grid's current
L2/L3/L3.5 = 5. The file's own L5 line was correctly rewritten ("Stage 5 (WU-L5Pilot, see Stage
note below)") but the L2/L3/L3.5 intro text got the same treatment for L5 only, not for L2/L3/L3.5.

**Fix:** rewrite the intro line to match the pattern already used for L5: "L2 — Stage 5 (WU27.5,
see Stage note below)," etc.

### 1.13 `audit/Chapters.md` Feature 44: dangling live-sounding Redis/L7 line

The original stage block still contains an unstruck line: *"L7 — Stage 2. Redis batching of
progress writes (write-behind pattern 1; MVP direct DB; L7 swaps body)."* A separate, later
section in the same file documents the real resolution and says it *"Supersedes the 'L7 Redis
write-behind' plan,"* but the original line was never struck. Other audit files in this project
use `~~struck~~` text for exactly this situation (e.g. Sprites.md, BlogPosts.md) — this one wasn't
brought in line with that convention.

**Fix:** strike the original line: `~~L7 — Stage 2. Redis batching...~~ superseded — see below.`

---

## §2 — Needs your arbitration

### 2.1 `audit/Lookups.md` says Stage 4; `status.md` grid says Stage 5 (row 2, L1)

`Lookups.md`'s Feature 2 section header literally reads **"Feature 2 — Lookup Tables & Seed Data
— L1 Stage 4"** and lists five concrete, still-open divergences: `SearchMode` seed still uses the
pre-three-axis keys (`DefaultSearch/TreeSearch/RandomSearch/AlsoFavorited` instead of
`SearchPage/TreeSearch/AutoTreeSearch/AlsoFavorited/AlsoRecommended/Profile*`), `DefaultSortOrder`
still offers the three explicitly-excluded sorts (Favorites/LastUpdated/ViewCount), vestigial
`ReadStatus`/`FavoriteStatus` enums, stale `UserStoryInteractionFilters.InProgress`, and an
incomplete `DefaultSearchSetting`/`Badge` seed matrix. The file's own closing line states
*"Implied resolution: Stage 2 — re-derive `SearchMode`/sort vocabulary from §5.3."* Nothing in the
file or in `Discovery.md`'s later WU23/WU28 notes (which rename *entities* but don't touch the
seeded `SearchMode` keys or the `DefaultSortOrder` enum) closes this out.

**Why this needs you:** I can't tell from the docs alone whether (a) the seed data actually got
fixed in some later work-unit and `Lookups.md` simply missed its Doc-Touch update, or (b) it's
still genuinely divergent and `status.md`'s "5" is the wrong number. This is a data/seed-content
question that needs a look at the actual `DataSeeder.cs`/`SiteConstants.cs` state, not just doc
cross-referencing — worth a quick Claude Code session pointed at those two files before deciding
which document is wrong.

### 2.2 `audit/Notifications.md` Feature 42: enrichment described broken, then later shown working

The "WU35 correction (2026-06-24)" note says `SourceUserName`/`TargetTitle`/`TargetUrl` are
*"always null in production until the enrichment batch lands"* (not yet implemented). The later
"L4.5-Browser verification (2026-07-01/02)" section for the same feature shows the notification
page working with live enrichment resolved. No Stage note bridges the two dates. This isn't a
Stage-number error (L2=5 is presumably right either way) — it's a narrative gap: nothing records
*when or how* the "still pending" caveat was closed, which is exactly the kind of thing
CLAUDE.md's Doc-Touch Timing moment-3 is supposed to capture.

**Why this needs you:** the fix is easy once you tell me which work-unit built the enrichment
batch (WU33 Notification UI, most likely, per the Resolved-index dates) — I'd rather you confirm
than guess and write a Stage note that names the wrong work-unit.

---

## §3 — Cosmetic / low-priority

### 3.1 CLAUDE.md's "Project Files" table omits `grid_axes.md` and `folder_clusters.md`

Both are live, load-bearing structural references (not superseded like `next_steps.md`/
`forward_plan.md`/`middle_plan.md`, not a write-once snapshot like `audit-summary.md`).
`status.md` itself depends on `grid_axes.md` ("Rows are the dependency-ordered features from
`grid_axes.md`") but CLAUDE.md's table never discloses that dependency. Nothing explains the
omission — it reads as an oversight, not a deliberate exclusion.

**Suggested fix** — add two rows to CLAUDE.md's Project Files table:

| File | Purpose | Updated by |
|------|---------|------------|
| `.claude/grid_axes.md` | Defines the 9 grid layers (columns) and 62 features (rows) in detail, including the MVP-line and post-MVP-line rationale. | Rarely — only if a layer/feature axis itself changes (e.g. the 2026-07-06 Layer-7 dissolution) |
| `.claude/folder_clusters.md` | Folder → feature → per-layer (L3/L3.5/L4) description mapping, used to route work to the right audit file/skill section. | Rarely — only if folder clustering or feature-to-folder assignment changes |

### 3.2 `next_steps.md` has no retirement banner

`middle_plan.md` and `forward_plan.md` both open with an explicit `> **RETIRED (date) —
historical reference only. Superseded by...**` banner. `next_steps.md` — an older bootstrapping
doc referencing `claude_v3.md`, `features/<name>.md`, and a step1–6 process that no longer matches
the current file names — has no such banner. `forward_plan.md` line 7 says next_steps.md is "kept
only as historical reference," but that statement lives in `forward_plan.md`, not in
`next_steps.md` itself, so a reader who opens `next_steps.md` directly gets no signal it's dead.

**Suggested fix** — add as the new first line of `next_steps.md`: `> **RETIRED (2026-07-03) —
historical reference only. Superseded by the successor chain now at `.claude/middle_plan_v2.md`.
The step1–6 process and `claude_v3.md`/`features/<name>.md` file names described here no longer
match the current process — see `CLAUDE.md` for current file names/roles.**`

### 3.3 Dangling reference to `step3_classify.md`

`grid_axes.md` ("Companion to `step3_classify.md`") and `audit-summary.md` ("Five sections per
`step3_classify.md`") both point at a file confirmed absent anywhere in the repo — it was a
one-time prompt file from the original bootstrapping process, deliberately never committed (per
`next_steps.md`'s own explanation: "Step instruction files do NOT go in the repo"). Low priority,
but reads as a broken link to anyone without memory of that process.

**Suggested fix** (opportunistic, next time either file is touched): append a parenthetical, e.g.
"`step3_classify.md` (a one-time prompt file from the original bootstrapping process, never
committed to this repo — see the retired `next_steps.md`)."

### 3.4 `audit-summary.md` reads as current-state diagnostics rather than a frozen snapshot

CLAUDE.md's table entry ("Written once during audit") is accurate but passive — it doesn't warn
the reader that the file's stage percentages (~56% Stage 2, ~27% Stage 5) and findings ("no
migrations exist," "all UI is Bootstrap," `RandomNumberGenerator` placeholder) describe the
project's state from early in the build, long since superseded by the live `status.md` grid.

**Suggested fix** — add one header line: *"Snapshot as of the initial Step-3 classification pass
(pre-2026-06 build-out). Superseded by `.claude/status.md` for current stage numbers and by
`.claude/workplan.md`/`middle_plan_v2.md` for current sequencing — retained only for its
historical findings (§2) and reconciliation index (§3)."*

### 3.5 `workplan.md`: minor formatting/title staleness

- **WU37**'s section header has no "DONE ✓ (date)" marker (every other entry's marker sits in the
  header; WU37's only appears mid-body) — breaks the skimmable-header convention, not a factual
  error.
- **WU-Aspire**'s title still reads *"Postgres/Redis/MinIO"* — defensible as a historical snapshot
  of what that work-unit stood up before WU-S3Garage replaced MinIO with Garage a few entries
  later, but it reads as "current" at a glance. Lower priority than §1.10's version of the same
  MinIO staleness, since the body text here doesn't misrepresent anything.

---

## Summary

13 fixes I'd make without hesitation (§1), 2 that need a decision or a quick codebase check from
you (§2), and 5 cosmetic/low-priority items (§3). The most consequential is **§1.1** (verified: 11
grid cells overstate Layer-5/WASM completion where no client code exists at all) and the paired
**§1.7/§1.8** (workplan.md's preamble and status.md's global note both still describe the
pre-2026-07-05 features-first sequencing as if it's current, when `middle_plan_v2.md` inverted it
on 2026-07-05). Everything else is narrower in blast radius.
