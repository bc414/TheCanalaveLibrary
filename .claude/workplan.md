# Workplan — Ordered Work-Units (atoms-first)

> New work-units are sequenced by `.claude/roadmap.md` (the live master plan, since 2026-07-27 —
> it superseded `.claude/middle_plan_v2.md`, which had itself superseded `.claude/middle_plan.md`,
> which had itself superseded `forward_plan.md`); this file remains the work-unit ledger. Recent entries live here; DONE entries older than
> the recent window are moved wholesale to `workplan-archive.md` and "workplan.md WU-X"
> citations resolve there. **Sweep trigger:** when this file exceeds ~1,500 lines, archive the
> DONE entries older than ~2 weeks (never edit them in transit).

Produced by Phase D (`forward_plan.md`, now retired). This is the build sequence for Phase E. Each work-unit names
its **cell(s)** (Feature # + layer, per `status.md`), its **tool** (per CLAUDE.md Per-Stage Guidance),
its **audit pointer** (`.claude/audit/<Folder>.md`, section), and its **deps** (work-units that must be
at Stage 5 first). CLAUDE.md is the source of truth for stage semantics and file paths — this file
references it, does not restate it.

---

## Position (updated at Doc-Touch moment 3 — the "you are here" block. Every claim here is re-verified against its source at write time, never carried forward from the previous version.)

- **Last landed:** WU-StoryLifecycle (2026-09-30) — the first build of the 2026-08-04 worksheet
  answers: **D1** (the approval queue is mandatory for an author's *first* submission only; a
  server-side transition table, `Core/Stories/StoryLifecycle`, now governs every status move) and
  **D2** (`stories.published_date` nullable, NULL = never published, stamped once and never
  re-stamped; new chapter anchor `Chapter.FirstPublishedDate`). Status left the property-edit path;
  new `TransitionStatusAsync` + `/api/stories/{id}/status`; approve/reject are guarded (400 not
  401), transactional and conditional on `PendingApproval`; new monotonic trust record
  `User.ApprovedStorySubmissions` + moderator revoke/restore of `CanAutoApprove`. One migration
  (`WU_StoryLifecycle`), checked on a populated dev-DB clone. No cell flips. `dotnet test`: Unit
  1,010, RazorComponents 686, Integration 1,117. Not browser-verified (tracker **H12**); owner-open
  residue in tracker **F9**. **Pointers:** its DONE entry; `layer2-services.md` §"Story Lifecycle".
  Before that, 2026-09-20: WU-QuickFixes — four no-deliberation closures (MA-107, MA-408, H6's VouchButton staleness, MA-007/MA-211); see its DONE entry.
  Before that, 2026-08-01: WU-UserModeration — closed tracker **B13**, which was filed as
  `polish · low` ("`ModUsersPage`'s `{UserId:int?}` route parameter is declared and never read") and
  turned out to be the visible tip of a **moderation feature that could not be used at all**.
  Three findings, each verified in code: nothing in the app could report a User (every
  `ReportDialog.OpenAsync` call site passed Story or Comment), so `/mod/users` — which filters the
  queue to user-targeted reports — rendered "No reported users." permanently for every moderator;
  `/mod/reports`' "Warn user" passed a content report into a method that demanded a User-targeted
  report, so it **threw on every report the app could actually produce**, surfacing as the generic
  error because `InvalidOperationException` isn't user-facing; and therefore the entire
  account-action capability (built WU34, sign-in-blocked WU38a, banner-surfaced
  WU-AccountEnforcement, integration-tested throughout) was **unreachable from any in-app path —
  nobody could be warned, suspended, or banned.** Brian settled the scope: cover the whole seam in
  one WU, let moderators act on never-reported users via a **mod-filed report** (`ReporterUserId ==
  ModeratorUserId` is the marker — no new audit table, no migration, no synthetic reason row), and
  give `/mod/reports` the full Warn/Suspend/Ban set acting on the reported content's **author**.
  That last decision is what closed B13: the route parameter went **live**, not deleted —
  `/mod/users` is now lookup (`UserPicker`) and `/mod/users/{id}` is standing + history + actions.
  Also fixed: `ApplyAccountActionAsync` never decremented `ActiveReportCount` (leaking the counter
  the triage sort orders on), and `UserMenu` linked only `/mod/reports`, leaving the other four
  `/mod/*` pages URL-typed-only. `AccountActionPanel` was extracted because it owns the 2026-07-30
  `SpecifyKind` Npgsql fix — a second Suspend surface would otherwise have re-derived it. No cell
  flips (F46/F47 stay L1–L3.5=5, L4=3, L5=5, L6=5 — gaps closed *beneath* sound cells, the
  B0/B4/B12 shape). `dotnet test` green: Unit 793, RazorComponents 662, Integration 1063 (2,518).
  Browser-verified with `psql` ground truth, including the previously-throwing path (Suspend from a
  Story report correctly suspended the story's author) and `CanalaveSignInManager` then blocking
  that account's sign-in. Opens **B17**/**B18**/**B19**. **Dev-DB note:** the browser pass left
  AuthorBeta Suspended and LurkerDelta Banned on the persistent workbench — `reset-dev-db.ps1`
  restores it.
  Before that, 2026-07-31: WU-ExploreFilterAxes — closed tracker **A6**: the Explore
  candidate-results pane scoped candidates by (edge, direction) toggles alone, so a prolific
  author's "Authored (15)" was a wall you could only page through, on the one site where tag
  filtering is the point. The entry read like a small build; it was a **design reopen** —
  `layer3.5-structure.md` carried a *settled* 2026-07-12 note stating flatly that neither manual
  tab is filtered by `TagFilter`/`UserStoryInteractionFilter`, so the first move was owner
  adjudication (WU40 **scope cut, not design objection**) and rewriting that paragraph as
  moment-1 work before any code. Settled three ways: Explore filtered on **user anchors only**;
  **Deep Dive permanently unfiltered** (a standing design rule — filtering would break the ≤5/≤1
  boundedness its edge whitelist promises, so it must not be "finished" later); filter state
  **session-only**, leaving the persisted-tree contract untouched. Story anchors get no axes
  because none of their sections is story-valued — `Author`/`Favoriters` are users and every
  recommendation row's story *is* the anchor, so a filter there is inert or blanks the pane.
  Built: `StoryFilterPredicates` (tag roll-up + ship + FTS + interaction exclusions extracted out
  of `ServerStoryReadService`, purity invariant preserved) now shared by both read services — the
  alternative was a second transcription of exactly the semantics most likely to drift;
  `UserNeighborsRequest.Filter` composed onto the **one** `visible` queryable, so a single
  substitution covers all four story-valued sections and every section keeps count and page on the
  same predicate; the user endpoint wrapped in `ExecuteAsync` (malformed ship input was a 500 in
  waiting — the same defect WU-ErrorHandling2 fixed on the story-listing reads); and an
  Apply-batched `<details>` on `ExploreTab` composing the three axes individually rather than
  `ResultsFilterPanel`. Found in passing: `SiteSearchModes.TreeSearch` had been seeded in the §8.7
  matrix since the matrix existed with **no UI surface consuming it** — this WU is its first
  consumer. No cell numbers changed — F33's L2/L3.5/L4.5 were already 5 and now cover more.
  `dotnet test` green: **2,502** (Unit 793, RazorComponents 655 (+5), Integration 1,054 (+5)).
  Browser-verified against psql ground truth (AuthorAlpha's 4 stories / TestUser's ignored one /
  the single Cynthia-tagged story): the §8.7 default hid a story with an honest count on load, one
  tag chip narrowed Authored 4→1 *and* Recommendations and Vouched to 0 in the same pivot, Clear
  restored all four, and the disclosure vanished on a story anchor. `check-design-tokens.ps1` +
  `check-doc-hygiene.ps1` clean. **Pointers:** `audit/Discovery.md` F33 WU-ExploreFilterAxes Stage
  note; `layer3.5-structure.md` §"Filter-Axis Component Pattern" (rewritten — now also records
  that breadth axes batch behind Apply while edge toggles deliberately don't) and §"Explore tab".
  Before that, 2026-07-31: WU-H10Fix — closed tracker **H10**: the entire `/Account/*` funnel
  had been returning a raw 500 since the Global Flip (`539c4f24`, 2026-07-13), eighteen days,
  against a green `dotnet test` the whole time. Root cause confirmed live (not the routing
  hypothesis the tracker entry carried): `Identity/Pages/*` are `[ExcludeFromInteractiveRouting]`
  and declare no `@layout`, so they render static-SSR under `AuthorizeRouteView`'s ambient
  `DefaultLayout="typeof(MainLayout)"` — and two of that chrome's components, `MessagesNavLink`
  (2026-07-13) and `NotificationBellInner` (2026-07-15), had been given `[PersistentState]`, whose
  persistence callback has no render mode to infer on a static render, so
  `ComponentStatePersistenceManager.InferRenderModes` throws and 500s the page. **The same defect
  class the Global Flip wave had already found once** (`ReaderDisplayProvider`) and already written
  down in `layer5-wasm.md` — the entry's own "three `[PersistentState]`-bearing descendants" claim
  was wrong on both counts and is corrected everywhere it appeared. Blast radius was wider than the
  two named pages: every `Identity/Pages/*` outside `Manage/`, plus direct navigation to any
  `/status-code/{code}` URL since 2026-07-24. `/Error`, `/Account/Manage/*` and the 401/403/404
  re-execute path stayed healthy, which is what pins the cause to the render mode rather than the
  layout. Fixed by converting both components to
  `RegisterOnPersisting(callback, RenderMode.InteractiveAuto)`, mirroring `ReaderDisplayProvider`.
  **Because a written rule had already failed twice, the durable output is two guards, both
  verified to actually catch it** (red on the pre-fix tree, green after): `StaticSsrPageRenderTests`
  (Integration — 10 tests; the bUnit tier structurally cannot see this class, since it renders
  everything with no render mode) and `scripts/check-render-modes.ps1` (local + CI — computes the
  static-SSR component closure and fails on `[PersistentState]` inside it; flagged exactly the two
  offenders, no false positives, 60 components in the closure). No cell flips — F1's cells already
  claimed Stage 5 and this restores the state they claimed. Deleted `status.md`'s H10 standing
  constraint and replaced it with the rule that now binds. Also corrected H9's false "no Identity
  audit file exists" claim. `dotnet test` green. **Pointers:** `audit/Identity.md` Stage note
  ("The H10 outage and its fix") — the narrative of record; `layer5-wasm.md` §"Components that ALSO
  render on static-SSR pages" (rule + named surface); `testing.md` §"What the three tiers
  structurally can't see" (the render-mode blind spot).
  Before that, 2026-07-31: WU-NotifEmail — closed tracker **B1**: `UserNotificationSetting.EmailEnabled`
  had stored, rendered and persisted since WU22 while driving no mail, and the deferral's stated
  blocker (an unchosen email provider) turned out never to touch the code path — `Email:Provider` is
  a config switch over plain SMTP and Mailpit made the whole flow locally verifiable, so it was
  pulled off the Phase-6 gate. Built: a new `Server/Email/` cross-cutting cluster holding an
  `IMailTransport` seam extracted from `SmtpEmailSender` (Identity's `IEmailSender<User>` is a fixed
  three-method contract and could not carry notification mail); a buffer/flusher/worker trio draining
  every 30s, enqueued from `CreateCoreAsync` so drop-self and dedup carry through for free;
  eligibility (effective `EmailEnabled`, `EmailConfirmed`, still-unread) resolved at drain time, with
  account status deliberately NOT a gate since suspension/ban notices default email-on;
  RFC 8058 one-click unsubscribe over a Data-Protection token (no schema, no migration), split
  GET-confirms/POST-acts so link scanners can't unsubscribe anyone; and `NotificationEnricher`
  lifted out of `ServerNotificationReadService` so the ~40-arm `RelatedEntityId` switch is shared
  with the in-app panel rather than forked. Doc-Touch moment 1 superseded the audit file's own
  settled "build inline first and measure" note (Brian-ratified) and fixed a stale "folds in H5"
  claim across three docs. No cell numbers changed — F41/F42/F43 L2 were already 5 and are now
  truthful rather than inert. `dotnet test` green: Unit 793 (+17), Integration 1039 (+18),
  RazorComponents 650. Live SMTP pass against Mailpit confirmed delivery, headers, absolute links,
  and post-unsubscribe suppression. **Tracker F4 / decision row 8 remain open** — provider, sending
  domain and SPF/DKIM/DMARC are Phase 7, and no mail reaches a real inbox until they land.
  Before that, 2026-07-31: WU-A11y (Structure) — decision row 12 resolved same day (sweep by
  defect class not by page; no fourth test tier; extract the shared `Modal` primitive; Identity in
  scope), then split into two work units the dividing line itself produced: ARIA that names
  existing structure (this WU) vs. ARIA that promises an interaction model not yet built
  (WU-A11y-Keyboard, deferred, paired with the Phase-3 L4 sweep below). Built: the `Modal`
  primitive (`SharedUI/Dialogs/Modal.razor`, 9 prior hand-rolled overlays migrated onto it, no
  trap/`aria-modal` — `ModalTests` asserts the absence directly); labelling swept by defect class
  across 17 files (43 orphan `<label>`s + 2 dangling `for=` targets, hand-classified not
  estimated) via three shapes (`for=`/`id=` pairs, `role="group"`/`aria-labelledby` for
  no-single-control composites, a new `CanalaveTypeahead.AriaLabel` parameter for the
  picker-owns-its-name case); all 43 `ValidationMessage`s wired to `aria-describedby`; the avatar
  `alt=` convention standardized on `alt=""` (5 sites fixed off `alt="@Username"`, which
  double-announced against adjacent visible name text); `BadgeSettingsForm`'s redundant `title=`
  removed; a `prefers-reduced-motion` CSS block added (documented gap: doesn't reach animated
  sprites, tracker item **A9**); `scripts/check-a11y.ps1` (7 gates, each mutation-tested by hand)
  wired into CI; a new bUnit `AccessibleNameAssertions` helper applied to the 4 densest label
  files — it caught one real bug a static gate structurally can't (`ChapterPropertiesForm`'s
  Version-Rating `role="group"` named the group, not the `<select>` inside it). Cell flip:
  Feature 65 L4-Style 1 → 5; L4.5-Browser stays 1. `dotnet test` green: RazorComponents 650
  (+8 new). **Addendum, same day:** Chrome browser tooling became available mid-session, so the
  axe-DevTools pass the plan scoped as this WU's own step *did* run after all, over all six pages
  (real axe-core 4.10.2, loaded via a `<script>` tag — the extension's isolated-JS-world `eval`
  silently failed to attach `window.axe`, a real `<script src>` tag worked). Found and fixed:
  missing `<h1>`/`<PageTitle>` (Home, Discover — sr-only, no visual change), two sidebar `<aside>`s
  nested inside `<main>` (Discover, Messages — changed to `<div>`), `ChapterNavigation`'s duplicate
  `<nav aria-label>` (top/bottom now distinguished via a new `NavLabel` parameter) and its disabled
  spans' `aria-prohibited-attr` (added `role="link"`, the ARIA APG pattern for a disabled link).
  Confirmed as accepted, not fixed: Quill's own toolbar chrome (matches the already-documented
  exemption), the not-yet-rendered `ValidationMessage`/`aria-describedby` id (matches the plan's
  own prediction verbatim — axe reports it "incomplete," not a violation), and **real
  measured contrast failures in Brian's locked design tokens** (`--color-tagtype-character`
  white-on-blue 3.07:1, `--color-tagtype-genre` white-on-pink 3.11:1, the Indicator success-tint
  recipe 2.88:1 — all below the ratified 4.5:1; not changed unilaterally, flagged for Brian's
  token decision). New, unrelated, out-of-scope discovery this pass surfaced and did **not**
  fix: `/Account/Login` and `/Account/Register` both return a raw 500
  (`PersistProperty must be associated with a component or define an explicit render mode type`)
  for every visitor, authenticated or not — the entire Identity/auth funnel was broken. Not
  diagnosed further here (real risk of a rabbit hole unrelated to accessibility) — flagged at high
  priority as tracker **H10**, and **fixed the same day by WU-H10Fix** (see Last landed; the
  guess recorded at the time — three offenders including `ReaderDisplayProvider` — was wrong on
  both counts; `audit/Identity.md`'s Stage note is the confirmed account). Full evidence table:
  `audit/Accessibility.md` Stage note Addendum.
  Before that, 2026-07-31: WU-SweepRiders — closed tracker items H1, E4, H8 ahead of the
  Phase 3 L4 sweep rather than riding alongside it (H8 gated whether the sweep needs to style
  ~1,325 LOC of Identity scaffold at all). H8: keep the 2FA/passkey/external-login scaffold
  (Brian) — corrected the entry's "no provider configured" over-generalization (only external
  login is provider-dependent) and opened **H9** (never verified end-to-end) rather than treating
  "keep" as "verified." H1: closed with **no code change** — the tracker's "Development Mode
  boilerplate"/no-layout premise was stale; `/Error` already gets `MainLayout` via
  `AuthorizeRouteView`'s ambient `DefaultLayout`, and the wire status is already 500. E4: built —
  a real 1200×630 `og-default.png` (ImageSharp, site tokens, visible "AI-generated placeholder"
  caption) replaces the SVG OG fallback at all 8 call sites behind a new `SeoDefaults` constant.
  No cell flips. `dotnet test` green, unchanged count.
  (Before that, same day: WU-DiscoveryOverrideUI — built the §8.7 per-user filter-override
  editing surface `IDiscoveryDefaultsReadService`'s read/merge never carried: a new self-referential
  `IDiscoveryFilterSettingsService` (sparse upsert/delete, mirrors
  `INotificationWriteService.SetSettingAsync`) surfaced on `/settings` via `DiscoverySettingsForm`,
  not `ResultsFilterPanel` (supersedes an earlier guess in `audit/Discovery.md`). Cut
  `UserCustomFilter` entirely (both directions unbuilt/unrequested — a migration drops the table).
  Wired the two inert discovery `ReaderSettings` found alongside it (`DefaultPaginationSize`,
  `DefaultSearchSort`, the latter with a validity clamp against surfaces where it doesn't apply).
  Closes tracker item **B7**; opens **B15** (`CollapseCommentThreads` inert — deliberately left for
  Brian to design after using the site) and **B16** (`ResultsFilterPanel`'s separate `PageSize=20`
  hardcode — a cross-cutting fix across its several consumers, not folded in as a point fix). No
  cell flips (F31, F21, F22 all stay Stage 5 — closing an invisible gap under an already-sound
  cell, same shape as B0/B4/B12). `dotnet test` green: Unit 776, RazorComponents 635, Integration
  1021 (2,432 total).)
  (Before that, same day: WU-DataSaver — `PrefersDataSaverMode` removed end to end rather than
  wired up: measurement before building showed B0's own "suppress sprites, or cut the setting"
  framing was wrong on the numbers (sprites are a rounding error; cover art/avatars are the real
  weight and no derivative-sizing mechanism exists to honor the checkbox's promise). Closes tracker
  item **B0**; opens **B14** (image derivative sizing, sequenced with Phase 7). No cell flips.
  Before that, same day: WU-StatBadgeProducers — Story Acknowledgments (consent-gated credit
  feature) + a producer hook on the already-built `StoryLineage` approval, closing tracker item
  **B4** in full and **B3**'s two acknowledgment-counter rows (`SpotlightCount` re-filed under
  **B8**). Surfaced and retired the Bronze/Silver badge-tier paradigm site-wide along the way (no
  design provenance — see `audit/Badges.md`); a badge now displays `UserBadge.EarnedCount` instead.
  Retrofitted `ComposeConversationModal`/`ModSpotlightPage` onto a new `UserPicker` (owed work, per
  their own stopgap comments). No cell flips — producer/plumbing under already-Stage-5 cells.
  Before that, 2026-07-30: WU-ApplyFiltersPurity — `ServerStoryReadService.ApplyFilters` reverts to
  pure/synchronous via a new cached `ITagHierarchyReadService`, closing tracker item **B12**. Before
  that, same day: WU-ErrorHandling2 — the `ProblemDetails` API error envelope + full client HTTP
  error translation, closing tracker item **E1** and D5's behavior-change half. Before that, same
  day: WU-AccountEnforcement — mid-session account-status responsiveness, closing tracker item
  **G1**'s residual and Phase 2's last open item. Before that, 2026-07-28: WU-DiscoveryFilterRestore
  + WU-SelectionPermalink — decision row 13 resolved, closing tracker item **B11**; earlier the same
  day, WU-Home + WU-SiteNews — decision row 2 resolved, closing tracker item F1.)
- **Phase (`roadmap.md`):** **Phase 2 is DONE ✓ (2026-07-30).** Phases 0, 1, 2, and 5 are all DONE.
  **Phase 3 is next** — Brian-driven L4 freeze sweep + WU-A11y-Keyboard (paired; decision row 12,
  which gated this, resolved 2026-07-31 — WU-A11y itself split in two the same day, and the
  static half, WU-A11y (Structure), is DONE outside the sweep — see Last landed) — nothing
  further blocks starting it. WU-SweepRiders and WU-A11y (Structure) were both pulled *ahead of*
  Phase 3 rather than riding alongside it: WU-SweepRiders because H8 was a decision the sweep's
  own scope depended on (keep vs. prune the Identity scaffold it would otherwise style); WU-A11y
  (Structure) because its half of the work doesn't need Brian's browser pass the way
  WU-A11y-Keyboard and the sweep both do. WU-DataSaver, WU-StatBadgeProducers,
  WU-ApplyFiltersPurity, and WU-ErrorHandling2 were all Tier-1/Tier-2 between-phase work (below),
  not phase gates.
- **Between-phase work:** `hidden-deferrals-tracker.md` closures land as ad-hoc WUs — open items
  exist in **every group A–H** (fewer now that A6/B0/B1/B3/B4/B7/B12/H1/H10/E4/H8 are closed, and
  **D4** is down to MA-006 alone, with H6's VouchButton bullet struck — WU-QuickFixes, 2026-09-20;
  B14/B15/B16/H9 newly opened — **H9 is now unblocked**, since H10 had the funnel it needs to
  drive returning 500), including two **high-priority security items: E2 and E3**. **A7** is the
  remaining half of `roadmap.md`'s Tier-6 discovery pair now that A6 is closed; it is a heavier
  lift (reopens the frozen `DiscoveryMartSchema` for a 7th UNION arm) and unchanged by this work. WU-ErrorHandling2 also
  left a named follow-up: the 8 SOLO editor pages' error surfaces still want `ErrorAlert` adoption
  (see its DONE entry). WU-StatBadgeProducers' `SeedTool` follow-up landed the same day — see its
  DONE entry.
- **Blocked on Brian:** decision rows 4, 6, 8, and 10 (`roadmap.md` §"Decisions
  that need you"; rows 2 and 13 resolved 2026-07-28, row 12 resolved 2026-07-31). Separately, not a
  numbered decision row: WU-A11y-Keyboard's browser pass (focus/Escape/keyboard-only) and the
  now-also-outstanding axe-DevTools pass WU-A11y (Structure) didn't reach need Brian's own
  browser session — see that WU's DONE entry and `audit/Accessibility.md`.

---

## Read this first (ordering preamble)

*(The numbered Phase 1–3 build-arc entries this preamble originally ordered now live in
`workplan-archive.md`; the ordering doctrine, tool rules, and per-unit loop below remain live.)*

**Scope of the numbered sequence, as originally written (through 2026-07-05) = Layers 1–4 (the
MVP).** `grid_axes.md` §"The Two Boundaries" is authoritative: Layers 1–4 are the InteractiveServer
MVP (data → service → logic → structure → style); Layers 5–8 are *additive and batchable* — they
swap method bodies / add DDL / add standalone workers behind contracts frozen in 1–4, and never
force a 1–4 change. That architectural property is still true. The *scheduling* claim that
followed from it — "Layers 5–8 post-MVP" — is **superseded**: `middle_plan_v2.md`'s platform-first
inversion (2026-07-05) moved most L5–L8 work *ahead of* several still-pending MVP-surface rows
(WU-L5Pilot shipped WASM 2026-07-04; WU-SignalBuffering dissolved the old Redis/L7 plan into L2/L6/L8
2026-07-06; WU-Marts shipped L8 2026-07-07). So **the numbered work-units (now in
`workplan-archive.md`) through 2026-07-05 built
L2/L3-Logic/L3.5-Structure/L4-Style** (L1 was done first — see WU0, archive); **named work-units from WU-CI onward
follow `middle_plan_v2.md`'s ordering instead**, and several of those are L5–L8. **The "Post-MVP"
section below is correspondingly partial** — some of what it once listed has already shipped out of
sequence (see each bullet's own status). If this scoping is unclear, see `middle_plan_v2.md` "Why v2
exists" and its v1→v2 phase-mapping table before reading further.

**Topological, bottom-up, three-phase (spec §9.2).** A cell's dependencies appear *earlier in this file*,
so they're at Stage 5 when reached. Phases:
- **Phase 1 — Atoms.** Leaves + foundational services consumed by many, depending on nothing
  feature-specific. Building these *mints contracts*: once a leaf's parameter/event contract is locked,
  its consumers flip from Stage 2 to Stage 3.
- **Phase 2 — Integration points.** Composites that consume atoms and produce surfaces pages embed
  (`StoryCard`/`StoryDeck`, `UserStoryInteractionPanel`, `ChapterNavigation`, `CommentSection`, …).
- **Phase 3 — Consumers / pages.** Dispatchers and feature pages aggregating Phase-2 output. Internal
  order is loose; deps still hold.

**Stage-4 / Stage-3 semantics (historical — the build arc completed).** During the arc, Stage-4
cells were treated as stale-code traps (build to spec, discard-not-reuse — audit-summary §0/§3)
and Stage-3 cells were *minted* as atom contracts landed, not found. Both descriptions are now
history: as of 2026-07-27 the grid holds **zero Stage-4 cells** and **five Stage-3 cells** (L4
rows 46/47/48/55/62, functional UI awaiting the standing Phase-3 visual pass). `status.md` is the
live count — nothing in this preamble describes current cells.

**Tool per work-unit.** opusplan for Stage-2 builds and atom-contract minting; **Sonnet in Claude
Code** for Stage-3 cells (today: the L4 visual-pass rows, which ride the Phase-3 sweep rather than
standalone units). L4-Style is never sequenced alone — it rides inside the same work-unit as that feature's
L3/L3.5 build (per Phase D rule; tokens are locked, `layer4-style.md` is the validated spec). "Build +
verify" for any unit touching L4 means render-and-look, not just `dotnet build` (the Phase-E rule,
carried forward from the retired forward_plan; mechanics in `run-server/SKILL.md`).

**Per-unit loop (Phase E).** pick next → read its audit pointer → feed audit "settled" notes to the tool as
"do not revisit" → build → `dotnet build` + `dotnet test` (should be green; add asserted tests for any new
testable surface per `canalave-conventions/testing.md`'s tier rules) + run the slice (+ visual check if L4)
→ update `status.md` (cell → 5) and this file (unit ✓). Record the covering test tier (Unit / Integration /
RazorComponents) — or why none applies — in the audit Stage note. Conventions skill auto-loads as guardrail.

---

## Blocked / deferred — genuine Stage-1 intent gaps (no sequence number)

These have an undesigned UI; resolve the design (chat with skill files) before they can be sequenced.
Their non-UI layers (L1/L2) may already be Stage 5/2 but the *UI cells* are blocked.

- **Community Spotlight** (55, all layers) — §5.26 donation infra TBD; entity is a placeholder.
  (Feature built as WU-Spotlight 2026-07-12; the donation-infra remainder got its Phase-4 verdict
  2026-07-11: deferred past beta.)

Formerly listed here, since resolved: Story Arcs UI (8) → WU45 (2026-07-12); Polls UI (37) →
WU-Polls (2026-07-12); Custom Lists (51) → design settled 2026-07-13
(`audit/CustomLists.md` §"Settled design") → WU-CustomLists.

When a gap resolves: it becomes Stage 2 (or 3 if the conversation yields a build-ready spec); insert a
work-unit into Phase 3 and update `status.md` + the audit file.

---

## Planned / not-yet-built named WUs (2026-07-15)

Named and sequenced into `roadmap.md`'s phases (Doc-Touch moment 1 formalization of the
2026-07-07 `middle-addendum.md` §3 findings), but **no code has been written yet** — distinct from
the DONE ✓ units (recent ones in the run later in this file; older in `workplan-archive.md`) and
from the "Post-MVP — Layers 5–8" section below (historical framing). Each entry names its
cell(s)/feature, phase, audit pointer, and deps; move it to the DONE ✓ run below (with
cells/verification) when built.

- **WU-A11y-Keyboard** — **Cells:** Feature 65, L4.5 Stage 1 (L4-Style already flipped to 5 by
  WU-A11y (Structure), 2026-07-31 — see DONE run below). **Phase:** 3, paired with the L4 freeze
  sweep. **Scope:** focus trap/restore (`modal.js`), the `dismiss.js` modal-first Escape branch,
  `aria-modal="true"` on `Modal`, `CanalaveTypeahead` combobox ARIA, the manual keyboard script —
  plus the axe-DevTools browser pass WU-A11y (Structure) didn't reach (contrast is unverified, not
  just ungated — fold it into this WU's own browser session rather than running two). **Pointer:**
  `audit/Accessibility.md`; tracker item **F6b**. **Deps:** Phase 3's L4 freeze sweep (same pass) —
  both need Brian's own browser-driven verification.
- **WU-EditorSprite** — **Cells:** Feature 6 (extends, no new cell). **Phase:** 4. **Scope:**
  inline Pokémon-sprite Quill blot (spec §5.30.2), deferred at WU6. **Pointer:**
  `audit/Chapters.md` Feature 6. **Deps:** WU6 (`EditorView`, Stage 5).
- **WU-EditorMobile** — **Cells:** Feature 6 (extends, no new cell). **Phase:** 4. **Scope:**
  mobile `EditorView` toolbar / desktop-mobile device composition, deferred at WU6. **Pointer:**
  `audit/Chapters.md` Feature 6. **Deps:** WU6.
*(WU-NotifEmail moved to the DONE run below, 2026-07-31.)*
---

## Post-MVP — Layers 5–8 (historical framing — every item below has since closed or been removed)

Per `grid_axes.md` §"The Two Boundaries": these swap method bodies / add DDL / add standalone workers
behind the contracts frozen in Layers 1–4. The section's premise ("batch later, when stable") was
overtaken by `middle_plan_v2.md`'s platform-first inversion — kept for its pointers; nothing here
is pending except where a bullet says so.

- **Messaging realtime push (SignalR) — REMOVED (2026-07-07).** Was tracked here as a Post-MVP
  additive layer on top of the stateless WU35 write service; permanently ruled out instead — Discord
  already covers real-time chat, and this site's messaging is deliberately async/long-form. Nothing
  in this project builds it now or later. See `cross-cutting.md` "Private Messaging Architecture" and
  `canalave-conventions/horizontal-scaling.md` §2 (no app-defined Hub means no SignalR backplane is
  needed at N≥2 either). Feature 49 L5 stays N/A.
- **L5 — WASM enablement — CLOSED (WU-L5Sweep + WU-GlobalFlip, 2026-07-13, archive).** Every
  `ServerXXXService` got its endpoint + client impl and the site flipped to global
  `InteractiveAuto`; the two once-flagged mechanical Stage-4 cells (Story L5 endpoint wiring,
  Sprites L5) closed along the way — the grid's built-surface L5 rows all read 5. Governed by
  `layer5-wasm.md`.
- **L6 — SQL indexes — batch CLOSED (WU-L6, 2026-07-07, archive):** USI filtered indexes restored
  (they had silently collapsed to one in the database), comment golden index landed, StoryTag
  reverse index REJECTED on measurement. **Still genuinely open:** the L6 Stage-2 cells (rows 6/7,
  33, 35, 38) awaiting the measure-first pass — evidence in `design/L6-reconciliation-matrix.md`
  (PENDING). Governed by `layer6-indexes.md`.
- **L7 — Redis integration.** **SUPERSEDED — see WU-SignalBuffering (2026-07-06) in
  `workplan-archive.md`.** Layer 7 dissolved: signal buffering (44/45) built as L2 in-process
  buffers, 16/17 stays durable-direct, 61's cache is the L8 mart itself. `layer7-redis.md` deleted.
- **L8 — Data marts — CLOSED (WU-Marts, 2026-07-07, archive** — the "requires real user data"
  horizontal boundary was crossed deliberately with SeedTool clustered synthetic data**):** rows
  59/60/61 marts + service layers built; **62 SiteDailyStat Worker — DONE, see WU-SiteDailyStat
  (2026-07-11) in `workplan-archive.md` — is the one documented exception with an EF model.**
  Pointers: `audit/Discovery.md` L8 notes, `audit/Moderation.md` Feature 62. Governed by
  `layer8-data-marts.md`.
- **Deferred workers — CLOSED (2026-07-15, archive):** 57 Notification Cleanup
  (WU-NotificationCleanup) and 58 UserStat Recalculation (WU-UserStatRecalc) both built once
  there was data to operate on.
- **Image storage cloud backend — DONE, see WU-S3Garage (2026-07-05, archive).** Was tracked here
  as a Post-MVP item (`S3ImageStorageService` behind the frozen `IImageStorageService`, MinIO
  endpoint in dev); built out of order and closed — F4/F20 L2 cloud-backend open item resolved,
  dev endpoint is Garage (MinIO OSS archived, superseded 2026-07-05), Cloudflare R2 in prod.
  Pointer: `audit/ImageStorage.md`.

---

## WU-StoryLifecycle — story status transition table, first-submission approval gate + trust waiver, nullable publish anchors (worksheet D1 + D2; extends `Stories/`, `Chapters/`, `Moderation/`, `Identity/`, `Export/`) — DONE ✓ (2026-09-30)

- **Cells:** none flipped. F4, F5, F6, F7, F47, F48, F62 stay at their `status.md` stages (F48 L4
  stays 3) — the gaps were beneath already-Stage-5 cells.
- **Trigger:** first WU of the worksheet-decisions build campaign. Owner rulings **D1** (queue
  mandatory for an author's first submission only; build the server-side transition table) and
  **D2** (nullable publish dates, NULL = never published, never re-stamped; a chapter-level anchor)
  were answered 2026-08-04 and unbuilt: authors set `StoryStatusId` freely through the property
  mapper, `CanSubmitForApproval` had no server caller, and both publish dates were stamped at
  creation. Sources: service audit §2.1.1/§2.9/§3.1, schema audit §3.4.
- **What landed:**
  1. **Status off the property path.** `StoryStatusId` left `IEditableStoryProperties`/
     `CreateStoryDTO` and the mapper; create always stamps Draft; `UpdateStoryAsync` never moves
     status (an in-queue edit stays Pending — D1 sub-edge, owner recommendation). `CanSave` rejects
     undefined `Rating`/`PostApprovalStatus`; `StoryUpdateDTO` echoes status + rejection reason.
  2. **Transition table** `Core/Stories/StoryLifecycle` (pure) applied by the new
     `IStoryWriteService.TransitionStatusAsync` / `POST /api/stories/{id}/status` / client impl:
     submit (trusted authors routed straight to `PostApprovalStatus`), withdraw, revise, published
     moves, unpublish — one conditional `ExecuteUpdate` per move, `PublishedDate ?? now`,
     `LastUpdatedDate`/`IsTakenDown` untouched.
  3. **Moderator hardening:** approve/reject 404/400 instead of `SingleAsync`/`InvalidOperation` →
     401; approve re-validates the entry status and requires a live author (reject never does);
     approve's status flip + `ApprovedStorySubmissions + 1` commit in one transaction.
     `SetCanAutoApproveAsync` (revoke **and** restore) + endpoint + `/mod/users/{id}` control; the
     queue orders by the new `Story.SubmittedDate`. Client moderation 400 →
     `ModerationValidationException` pulled forward from §2.7.5 (WU-ModerationIntegrity: verify).
  4. **Publish anchors:** `Story.PublishedDate`/`ChapterContent.PublishDate` nullable; new
     `Chapter.FirstPublishedDate` stamped on first publish and read by the chapter list (closes the
     promote-an-alternate bump vector); L8 `new_chapters`/`new_words` re-sourced; story page and
     every export writer render "Not yet published".
  5. **Migration `WU_StoryLifecycle`** with ordered data backfill (queue date preserved before
     `published_date` is nulled; chapter anchors from the earliest version; entry-set remap; trust
     backfill), run Up and Down against a populated clone of the dev DB. DataSeeder (TestUser left
     untrusted on purpose), SeedTool and DevDiagnostics updated; SeedTool run once against a scratch
     clone and the invariants checked in `psql`.
  6. **UI:** `StoryLifecyclePanel` (new, presentational) on `StoryEditorPage` — status updated in
     place, never a same-route `NavigateTo` (Quill hazard); "Status when published" select replaces
     the all-enum Status select; `ModSubmissionsPage` gained a queue-level error slot and reloads on a
     handled-elsewhere refusal.
- **Left alone, with the reason:** minimum-content floor for submission (**D20**, pending), the
  import-rider alternate reading (contradicts a settled WU38d note — needs the owner), notifying
  authors of auto-approve changes (owner silent), an expected-version token (**D30**, pending) —
  all four filed as tracker **F9**. Routed to later campaign WUs: the two CHECK constraints
  (published ⇒ `published_date`, `is_published` ⇒ `first_published_date`) → WU-SchemaHardening;
  throttles on the two new write surfaces → WU-ThrottleCoverage (the auto-approve one is a mod action,
  unthrottled by the existing `security.md` rule); the D21/D22 doctrine rewording →
  WU-CounterSymmetry (this WU wrote the trust-counter statement it must reuse); the rejection reason
  living in the takedown columns → D8 / WU-ModerationIntegrity (tracker **D6**); `StoryRejected`
  carrying its reason → D4/D5 / WU-InertFeatures. Observed and filed, not fixed: chapter publish
  never bumps `Story.LastUpdatedDate` (tracker **D7**). Known interim gap: author lifecycle errors
  read "Story validation failed." on WASM until WU-ParityAndRemaining P1.
- **Verification.** `dotnet build` green, no new warnings in touched files. `dotnet test` green:
  **Unit 1,010** (+217 — the exhaustive transition theory alone is 180), **RazorComponents 686**
  (+21), **Integration 1,117** (+53). New: Unit `StoryLifecycleTests`,
  `ClientStoryLifecycleServiceTests`, `StoryValidationsTests`/`StoryMappersTests`/
  `ExportWritersTests` additions; Integration `StoryLifecycleTests` plus `ModerationServiceTests`,
  `ChapterWriteServiceTests`, `SiteDailyStatAggregatorTests`, `StoryEndpointsTests`,
  `ModerationEndpointsTests` additions; RazorComponents `StoryLifecyclePanelTests`,
  `ModSubmissionsPageStoriesTests`, `ModUsersPageTests`/`StoryPropertiesFormTests` additions. The
  chapter-anchor read test was mutation-checked by hand. HTTP smoke on a freshly seeded scratch DB
  (unpublish → resubmit kept the date; self-approve 400; approve 204 then "already handled" 400;
  auto-approve revoke wrote its audit row). All four PowerShell gates passed. **No browser was
  available** — tracker **H12** carries the owner's browser pass.
- **Pointers:** `layer2-services.md` §"Story Lifecycle", §"Records of a decision are not counters",
  §"Account actions" rule 1; `layer8-data-marts.md` §`site_daily_stats`; `layer1-data-model.md`
  §"Column Conventions" (true-default bools); `audit/Stories.md` F4/F5, `audit/Chapters.md` F6/F7,
  `audit/Moderation.md` F47/F48/F62 Stage notes; `roadmap.md` §Resolved (D1, D2); tracker D6, D7,
  F9, H12; worksheet D1/D2 "Built:" lines.

## WU-QuickFixes — four no-deliberation-needed closures found by reading the process docs (tracker D4 + H6; cross-cutting, extends `Tags/`, `Stories/`, `Following/`, `Profiles/`, `RichText/`, composition root) — DONE ✓ (2026-09-20)

- **Cells:** none flipped. Every touched cell (F8/F9/F10 L2, F15 L2, F19 L3-Logic/L3.5, the
  `ContentSurface` atom) was already Stage 5 and stays 5 — the already-sound-cell shape this
  tracker exists for (B0/B4/B12/A6).
- **Trigger:** a read of the process docs looking for gaps that are *fixes*, not decisions. Four
  qualified; the entries that read like small fixes but are actually design questions were
  deliberately left alone, with the reason recorded (see "Left alone" below).
- **What landed:**
  1. **MA-107 — DI double-registration (7 clusters).** SavedTagSelection, CustomList, Series,
     StoryLineage, StoryAcknowledgment, StoryArc and Notification each registered the *write* class
     against both interfaces, so a scope injecting both got two instances of the same
     `ApplicationDbContext`-holding service. All seven now forward the read interface to the write
     registration — the shape already used by Moderation, Badges and Fanon, and the one the audit
     named the safer default. **Found in passing:** `ISavedTagSelectionWriteService` is the one
     write interface in the codebase that does *not* inherit its read interface (the forwarding
     delegate wouldn't compile), so that cluster registers the concrete class and forwards both
     interfaces — the `ServerTagHierarchyCache` shape. Rule now recorded in `layer2-services.md`
     §"Registering an inherited pair", which had no guidance for the inherited case at all.
  2. **MA-408 — `SavedTagSelection` N+1.** The profile Tag Selections tab looped `HydrateDetailAsync`
     (two queries per selection); it now runs two queries for the whole tab. New Integration test
     guards the failure mode a batch rewrite introduces and a single-row fixture can't see: chips
     landing on the wrong selection.
  3. **VouchButton follow-gate staleness (tracker H6).** `FollowButton` is a self-contained write and
     told nobody when it flipped, so `ProfileBanner` kept feeding `VouchButton` the page-load
     `RelationshipState.IsFollowing` — follow and vouch in one visit and the vouch button stayed
     hidden until a reload (recorded in the 2026-07-01 browser pass, never fixed). `FollowButton` now
     raises `OnFollowChanged`; the banner mirrors it. New `ProfileBannerTests` (3).
  4. **MA-007 + MA-211 — two dead-idiom cleanups.** `ContentSurface.FrameStyle` (the magic-int
     switcher for a gate review that ended 2026-07-10; the component's own header said the parameter
     goes when a treatment is ratified) and its only caller, the dev gallery's three-way switcher,
     are gone — the ratified side rails are now a `const`. `ServerStoryArcWriteService` stopped
     copying primary-ctor params into fields; the CS9107 alias it carried was unnecessary, since
     `writeDb` is never passed to the base constructor and `ActiveUser` is already exposed.
- **Left alone, deliberately:** **MA-006** (`ContentSurface`'s raw-hex reading-background palettes)
  is a choice between tokenizing into a *locked* `@theme` manifest and recording a sanctioned
  exception — a call, not a fix. **B15** (`CollapseCommentThreads`) is a design question the tracker
  already reserves for Brian. **B16** (`ResultsFilterPanel`'s `PageSize` hardcode) needs a
  consumer-contract audit, which its own entry says is a WU, not a point fix. **C3/C5** (index
  shapes) would add DDL without measurement, against the doctrine C1 set.
- **Also verified stale, no work needed:** **MA-509** (triplicated audience-badge statics) was
  already extracted to `SharedUI/Groups/GroupDisplayFormat.cs` and its Desktop/Mobile copies died
  with WU-ResponsiveMerge. `deferred-work.md` §3 and its pickup order are annotated.
- **Verification.** `dotnet build` green (0 errors; warning count unchanged from baseline).
  `dotnet test`: **Unit 793** (unchanged), **RazorComponents 665** (+3, new `ProfileBannerTests`),
  **Integration 1,058 passed / 1,064 total** (+1, the new batched-hydration test). Both new tests
  were mutation-checked by hand — each fails on the pre-fix code and passes after.
  **Environment caveat (the 2026-09-20 run happened in a Linux cloud container, not the Windows
  workbench):**
  the 6 non-passing Integration tests are the whole `S3ImageStorageServiceTests` class, which needs
  the Garage container image; every container registry's blob CDN is blocked by the container's
  egress proxy, so those 6 could not start (`GarageFixture.InitializeAsync` — Docker image pull),
  and the Postgres tier ran against a locally installed Postgres 16 instead of the pinned
  `postgres:18-alpine` container. No S3/image-storage code was touched. The PowerShell gates
  (`check-design-tokens.ps1`, `check-doc-hygiene.ps1`, `check-a11y.ps1`, `check-render-modes.ps1`)
  could not run either — no `pwsh` in the image.
  **Pre-merge re-verification (2026-09-29, Windows workbench) — closes the caveat:** `dotnet build`
  green, no warnings in any file the WU touched. Full `dotnet test` against the real containers
  (`postgres:18-alpine` + Garage): **Unit 793/793, RazorComponents 665/665, Integration
  1,064/1,064** — the 6 `S3ImageStorageServiceTests` and the new batched-hydration test included.
  All four PowerShell gates passed.
  **CI's Doc-hygiene step fixed in the same merge (2026-09-29).** The PR's CI failed at
  `check-doc-hygiene.ps1` with 411 violations while the same script was clean on Windows. This was
  not caused by this WU: every PR back to at least 2026-08-23, Dependabot's included, failed the same
  step. Check 4's repo-file index ran `Get-ChildItem -Recurse` without `-Force`, and on Linux every
  dot-prefixed directory is hidden, so `.claude/` and `.github/` were never indexed and every doc
  cross-reference read as MISSING. Its exclusion regex also only matched `\`, and `GetFileName`
  kept a `Client\Routes.razor`-style token whole. Fixed with `-Force`, an either-separator
  exclusion, and `\`→`/` normalization. Verified in a `mcr.microsoft.com/powershell` Linux container
  (411 violations reproduced; clean after the fix) and on Windows (still clean). A planted bogus
  reference is flagged identically on both, so the check still catches real misses.
- **Pointers:** `audit/Tags.md` §"WU-QuickFixes Stage note" (F15); `audit/Following.md`
  §"WU-QuickFixes slice" (F19); `audit/Stories.md` §"WU-QuickFixes slice" (F8/F9/F10 + MA-211);
  `layer2-services.md` §"Registering an inherited pair"; `hidden-deferrals-tracker.md` D4/H6;
  `modernization-audit/deferred-work.md` §3/§6.
