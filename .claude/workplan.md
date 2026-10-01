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

- **Last landed:** WU-ModerationIntegrity (2026-09-30) — owner rulings **D7, D8, D9** plus service
  audit §2.1.2/§2.1.3/§2.4.4. Resolve paths lock the report row and refuse a resolved one; a removal
  closes every sibling report on the same `(type, id)` target and notifies each reporter; one open
  report per reporter per target (partial unique index); `Report.ReportedUserId` (every target type,
  snapshot) feeds a per-user history that now includes content reports (tracker **B18** closed); the
  account-status transition table and a new **Reinstate**; account deletion closes the reports on what
  it destroys; every moderator-only read gates in the service via one shared `RequireModerator()`, and
  `IReportSubmissionService` is split from the mod interfaces. Migration `WU_ModerationIntegrity`.
  F47 L4.5 5→1 (no browser — tracker **H19**). `dotnet test`: Unit 1,070, RazorComponents 760,
  Integration 1,297. Decision row **20**; trackers **F13/F14/F15/D10/H19** opened. **Pointers:** its DONE
  entry; `layer2-services.md` §"Moderation Services".
  Before that, 2026-09-30: WU-InertFeatures — worksheet D3/D4/D5/D16/D17 (attribution on the RIL bit, the de-identified notification core, the new-chapter fan-out); see its DONE entry.
  Before that, 2026-09-30: WU-AccessGateSweep2 — worksheet D6 (raises guarded, clears free) plus service audit §2.6's access fixes; see its DONE entry.
  Before that, 2026-09-30: WU-StoryLifecycle — worksheet D1/D2: the story transition table, first-submission approval gate, nullable publish anchors; see its DONE entry.
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
  **In flight (between-phase): the worksheet-decisions build campaign** — the answered rows of
  `.claude/design/audit-decision-worksheet.md` built as a sequence of WUs; WU-StoryLifecycle (D1/D2),
  WU-AccessGateSweep2 (D6), WU-InertFeatures (D3/D4/D5/D16/D17) and WU-ModerationIntegrity
  (D7/D8/D9) have landed, and **WU-TptHardDelete** is next in the campaign's build order.
  **Phase 3 is next** after it — Brian-driven L4 freeze sweep + WU-A11y-Keyboard (paired; decision row 12,
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
  drive returning 500; **D6/D7/E7/F9/H12** opened by WU-StoryLifecycle, 2026-09-30 — E7 is routed to
  WU-ThrottleCoverage; D6 was routed to WU-ModerationIntegrity, which left it open (a rejection files
  no `Report` row, so D8(b) does not cover it); **F10/H13** opened by WU-AccessGateSweep2,
  2026-09-30, which also annotated E6 without closing it; **B23/B24/H14** opened by WU-InertFeatures,
  2026-09-30, which closed B20–B22 (its browser pass closed H14 the same day); **D9/H15** opened by the WU-StoryLifecycle browser pass,
  2026-09-30, which narrowed H12; **F12/H16/H17/H18** opened by the WU-AccessGateSweep2 browser
  pass, 2026-09-30, which closed H13; **F13/F14/F15/D10/H19** opened by WU-ModerationIntegrity,
  2026-09-30, which closed B18), including two **high-priority security items:
  E2 and E3**. **A7** is the
  remaining half of `roadmap.md`'s Tier-6 discovery pair now that A6 is closed; it is a heavier
  lift (reopens the frozen `DiscoveryMartSchema` for a 7th UNION arm) and unchanged by this work. WU-ErrorHandling2 also
  left a named follow-up: the 8 SOLO editor pages' error surfaces still want `ErrorAlert` adoption
  (see its DONE entry). WU-StatBadgeProducers' `SeedTool` follow-up landed the same day — see its
  DONE entry.
- **Blocked on Brian:** decision rows 4, 6, 8, 10 and **14–20** (`roadmap.md` §"Decisions
  that need you"; rows 2 and 13 resolved 2026-07-28, row 12 resolved 2026-07-31; rows 14–16 — the
  story-lifecycle questions WU-StoryLifecycle left open — 17–18 — the two defaults
  WU-InertFeatures had to take — 19 — type 90's moderator attribution, filed by its review
  fixes — and 20 — WU-ModerationIntegrity's two derived account-status refusals — added
  2026-09-30). Tracker **H19**'s browser pass (WU-ModerationIntegrity's moderator UI, both render
  phases) restores F47 L4.5. Separately, not a
  numbered decision row: WU-A11y-Keyboard's browser pass (focus/Escape/keyboard-only) and the
  now-also-outstanding axe-DevTools pass WU-A11y (Structure) didn't reach need Brian's own browser
  session — see those WUs' DONE entries and `audit/Accessibility.md`. (Tracker **H12**'s story-lifecycle pass
  ran 2026-09-30 and returned F4/F47/F48 L4.5 to 5; only a WASM re-check after
  WU-ParityAndRemaining P1 remains — see WU-StoryLifecycle's DONE entry. Tracker **H13**'s
  access-gate pass also ran 2026-09-30 and closed — see WU-AccessGateSweep2's DONE entry. Tracker
  **H14**'s attribution-and-notifications pass ran 2026-09-30 too, returned F16/F30/F33/F41/F55 L4.5
  to 5 and closed — see WU-InertFeatures' DONE entry.)

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

## WU-ModerationIntegrity — report lifecycle integrity: lock-and-guard resolves, sibling closing, dedup, `ReportedUserId`, account-status table + Reinstate, zombie closure at the source, service-side mod read gates (worksheet D7/D8/D9; extends `Moderation/`, `Stories/` (ExternalVerification), `Identity/`, `Spotlight/`, `SiteSettings/`, `Tags/`, `BlogPosts/`, and the auth-guard sweep in `CustomLists/`, `Following/`, `Groups/`, `Notifications/`, `Recommendations/`, `Messaging/`) — DONE ✓ (2026-09-30)

- **Cells:** **F47 L4.5 5→1** (the `/mod/users` history Target column, Reinstate and the hidden
  "Hide content" on `/mod/reports` changed; the WU ran with no browser available — tracker **H19**).
  Everything else beneath Stage-5 cells: F46/F47 L1/L2/L3/L3.5/L5, F53 L2, F62 L2, F55 L2, F11/F15 L2,
  F35/F37 L2, F52 L2, and the L2 guard sweep in F18/F27/F38/F42/F49/F51. L1 stays 5 with the migration
  applied.
- **Trigger:** three answered, unbuilt owner rulings (D7 sibling closing, D8 `ReportedUserId` +
  takedown-reversal ratification, D9 read gates + interface split) that the worksheet assigned to this WU
  jointly with service §2.1.2 (no status guard), §2.1.3 (account-status model) and §2.4.4
  (`ActiveReportCount` corruption); schema §3.7; tracker B18. Two sub-edges the owner delegated were
  picked and recorded: zombie reports from account deletion are closed **at the source**, and the
  401-instead-of-404/400 class is fixed **at the throw sites** (the `EndpointHelpers` table is unchanged).
- **What landed:**
  1. **Migration `WU_ModerationIntegrity`** — `reports.reported_user_id` (FK SET NULL) +
     `ix_reports_open_target` (partial), `ix_reports_open_reporter_target` (partial UNIQUE),
     `ix_reports_reported_user_id`; the full `ix_reports_reporter_user_id` is now declared explicitly so
     EF stops dropping it behind the partial unique index. Hand SQL between the FK and the indexes:
     backfill per type, close zombies, close same-reporter duplicates (keep the oldest), recompute every
     `active_report_count`. Run against a dirtied clone of the dev workbench DB (duplicate, zombie, wrong
     counters, anonymous rows): all handled; Down and re-Up clean. DataSeeder sets `ReportedUserId` on
     its three reports; SeedTool/SeedGraph write none.
  2. **D9** — shared `ActiveUserContextExtensions.RequireModerator()`; every private moderator-guard copy
     (Moderation, ExternalVerification, SiteSettings, the Spotlight allocator, Fanon, Tags, site posts,
     site polls) and the six private `RequireAuthenticatedUser` copies (+ Messaging's, now a wrapper)
     replaced. Gated reads: the three moderation reads, both EV queues, both SiteDailyStat reads, the
     allocator's capacity read; the SiteSettings `GetIntAsync` read is the recorded non-gate. The read
     handlers wrap in `ExecuteAsync`. `IReportSubmissionService` split out (one concrete class, three
     forwarded registrations; `ClientReportSubmissionService` with `rateLimitedAction: Report`).
  3. **Exception translation** — unknown report/EV ids → 404; the allow-set and EV business rules →
     400 (`ModerationValidationException`, new `ExternalVerificationValidationException`); the EV client
     reconstructs the new type. §2.7.5's moderation client mapping was already built by WU-StoryLifecycle
     (verified).
  4. **Submit path** — resolve `ReportedUserId` (nullable resolver whose default arm throws); dedup
     check + the named-index race catch (first in the codebase); row first, counter second (D22).
  5. **Resolve paths** — one execution-strategy transaction each, `FOR UPDATE` lock-and-guard; D7 sibling
     closing on removal (target-keyed, −(1 + N), every sibling reporter notified with their own report
     id); User reports refuse removal; each notification in its own try. The report-driven account
     action now sends 81 to the member reporter, and decrements after the stamp bump (`UserManager`
     rewrites every user column, which silently undid a `User`-target decrement).
  6. **§2.1.3** — the transition table in both entry points, `SuspendedUntilUtc` cleared off-Suspend,
     `ReinstateUserAsync` (+ endpoint, client, `ModeratorActionType.ReinstateUser`). Two refusals derived
     → decision row 20. `SetCanAutoApproveAsync`'s audit row sets `ReportedUserId` (amendment U5).
  7. **Zombie closure** — `ReportLedger.CloseForDestroyedTargetsAsync`, called by `UserDeletionService`
     (moved from the legacy `Server/Services/` to `Server/Identity/`) for the user and their profile
     comments.
  8. **B18 + UI** — the history reads `ReportedUserId` and keeps deleted targets; `/mod/users` loses the
     caveat, gains a Target column and Reinstate; `AccountActionPanel` gains the Reinstate verb;
     `/mod/reports` hides "Hide content" for User reports; `ReportDialog` injects the submission
     service.
  9. **Docs, moment 1:** `layer2-services.md` §"Moderation Services" rewritten (split, read gates, the
     count's meaning, submit order + dedup, `ReportedUserId`, lock-and-guard, D7, zombie closure,
     transition table + Reinstate, exception translation; two stale claims corrected);
     `identity-and-authorization.md` (shared guards, kind (c)'s service half, the D9 sweep table and
     non-gate); `security.md` (suspend dates, Reinstate); `content-safety.md` (reversible-by-design,
     count rules, transitions); `layer6-indexes.md` + the L6 matrix (the three unmeasured pre-data
     indexes); audit Settled notes (Moderation cluster/F46/F47; the WU-UserModeration "still open"
     history bullet closed); roadmap Resolved D7/D8/D9 + row 20; worksheet Built lines; service/schema
     audit banners; tracker F13/F14/F15/D10/H19 filed, D6/D9 annotated. Moment 2: `layer5-wasm.md`
     enforcement-point wording, `testing.md` (`interceptReaders`).
- **Left alone, with the reason:** D47 (Group-typed report actions — pending; no `Group` member added,
  WU-UserDeletion adds it with its resolver arm); takedown reversal (D8(b) is forward-only); the
  `ActiveReportCount` reconciler (D21 → WU-CounterSymmetry); zombie closure at author self-delete sites
  (owner-open → **F13**); a reinstatement notification (no type, a catalogue decision → **F14**); the EV
  author/mod interface split and EV status guards (unruled → **F15**, **D10**); report queue paging and
  reason caps (D20 bounds pending); expired suspensions auto-normalizing (unruled; Reinstate is the
  lever); tracker **D6** (a rejection files no `Report` row, so D8(b)'s premise does not hold — open) and
  **D9** (moderator reasons in the query string — not in this WU's sources; `/reinstate` follows the
  existing shape). `ApproveStoryAsync`'s stricter null-date live-author predicate is kept (it fails
  closed; null-dated suspensions are no longer writable).
- **Verification:** `dotnet build` 0 errors, no new warnings. `dotnet test`: Unit 1,070 (+12),
  RazorComponents 760 (+6), Integration 1,297 (+36), all green. New: Integration
  `ModerationIntegrityTests` (30), plus `ModerationEndpointsTests` (+4), `ExternalVerificationTests` (+2)
  and a flipped history test; Unit `ModerationIntegrityClientTests` (+11) and one EV client test; bUnit
  `ModReportsPageTests` (2), `ModUsersPageTests` (+3), `AccountActionPanelTests` (+1). Mutation-checked
  (each reverted fix fails its tests): sibling closing, the status guard, the race catch, a read gate,
  the ledger call, the transition table, the reporter's 81, the history predicate, the Hide-content
  guard. Migration verified on a dirtied DB clone (above). `check-doc-hygiene`, `check-design-tokens`,
  `check-render-modes` and `check-a11y` pass. **No browser was available** — tracker **H19** carries the
  pass for F47 L4.5.
- **Pointers:** `audit/Moderation.md` (cluster Settled note "report lifecycle integrity"; F46, F47, F53,
  F62 Stage notes); `audit/Identity.md` F52; `audit/Spotlight.md` F55; `audit/Tags.md` F11/F15;
  `audit/BlogPosts.md` F35/F37; short guard-sweep notes in CustomLists, Following, Groups, Notifications,
  Recommendations, Messaging; `layer2-services.md` §"Moderation Services"; `roadmap.md` Resolved D7/D8/D9
  and row 20.

## WU-InertFeatures — recommendation attribution rebuilt on the RIL bit, notification core (nullable source, de-identified moderation band, one-anchor rule, hidden favoriters), new-chapter fan-out (worksheet D3/D4/D5/D16/D17; extends `Recommendations/`, `UserStoryInteractions/`, `Notifications/`, `Moderation/`, `Groups/`, `Chapters/`, `Tags/`, `Stories/`, `Discovery/`, `Spotlight/`) — DONE ✓ (2026-09-30)

- **Cells:** **F16 / F30 / F41 L4.5 5→1** (UI and bell behavior changed; WU-InertFeatures ran with
  no browser available — tracker **H14**); the review fixes added **F33 / F55 L4.5
  5→1** (same reason); all five **back to 5** after the browser pass (2026-09-30, below); everything
  else beneath Stage-5 cells: F30
  L1/L2/L3/L3.5/L5, F16 L2/L3, F41 L1/L2, F42 L2, F39 L2, F46/F47/F48/F53 L2, F6 L2.
- **Trigger:** five answered, unbuilt owner rulings (D3, D4, D5, D16, D17) and three inert features
  the service audit found (§2.3.1 chapter publish notified nobody; §2.3.2 `ReportReceived` deleted by
  drop-self; §2.3.3/§2.4.1 attribution FK-failed and `RecordSuccessAsync` was a credit faucet) — filed
  and closed as tracker **B20–B22**. Sources: worksheet D3/D4/D5/D16/D17 (+ D1's anti-bump rider, D2's
  anchor, D6's sequencing); service audit §2.3, §2.4.1, §2.8 (bullets 1–3), §3.2–§3.4, §3.6, §3.20;
  schema audit §2.1, §2.3.
- **What landed:**
  1. **Migration `WU_InertFeatures`** — `notifications.related_entity_id` int→bigint; the phantom
     `user_story_interactions.recommendation_id` (unpaired `Recommendation.UserStoryInteractions`
     nav, a 2025-DDL fossil) dropped; the attribution's rec FK made explicit with no DDL churn. Up and
     Down both run against a populated clone of the dev DB. SeedTool: COPY column list and the
     bigint write; `SeedGraph`'s type-20 seed now skips hidden favorites (D17 mirror).
  2. **D4/D5 — notification core.** `CreateCoreAsync(int? source, (int, long)[])`; conditional
     drop-self; NULL-matching dedup; a dedup-exempt set (72/73/74/76/77/90). Every moderator-id
     parameter is gone from the interface (70–82 and 26 null-sourced); 70/80/81/82 carry the report
     id; `ReportReceived` restored; the D4 guardrail on the mod-initiated account action.
  3. **D16 — one anchor per event.** 60/25 carry the `GroupStory` row id; new enricher kind; the
     author is excluded from 60; `AddStoryAsync` notifies only on a real add and for authorless
     stories too. Enricher takes `long` ids (narrowed per kind) and returns a `NotificationTarget`
     with a new `TargetContextTitle`; presenter two-nulls rule, arms for 75–79, the 80 receipt, and
     story+group / story+chapter phrasing.
  4. **D17** — type 15 goes to `IsFavorite || IsHiddenFavorite`.
  5. **D3 — attribution.** `SetReadItLaterFromRecommendationAsync` (card RIL, one save) and
     `MarkStartedAsync(storyId, recId)` (direct link at Ch.1 90%) behind a shared
     `RecommendationAttribution` helper; `RecordAttributionSourceAsync` retired; prompt read returns
     the DTO with four gates; `DismissHelpfulPromptAsync`; `RecordSuccessAsync` gate + consume;
     triggers 1 and 5 (USI clear; author remove + moderator takedown). UI: Yes/X reminder prompt,
     card RIL + Read now, `ReadItLaterRecommendationCard` for Explore/Deep Dive/Spotlight, the
     section wired on the story page; the panel adopts a changed `State` when idle and every host
     re-reads after a card save (the spotlight now batch-loads states — it passed none before).
  6. **New-chapter fan-out** — `NotifyNewChapterAsync`, fired by `SetPublishedAsync` on the
     `FirstPublishedDate` stamp only, while the story is publicly published (default → row 17).
  7. **Docs, moment 1:** `layer2-services.md` §Notification Generation (null source, de-identification,
     dedup enumeration, 0 sentinel, eligibility correction, new-chapter rule, read-service wording),
     §Comment & blog-post (reply rationale, D17 plane rule), §Polymorphic RelatedEntityId (one-anchor
     rule, conformance list, `GroupStory`, context title), new §"Attribution (Feature 30)";
     `content-safety.md` §Notification Loop + the takedown sentence; `layer1-data-model.md` (unpaired
     collection navs); `testing.md` FK-parent example; audit Settled notes (Recommendations F30,
     Notifications F41, Groups F39, UserStoryInteractions F16, Moderation cluster); roadmap Resolved
     D3 / D4+D5 / D16+D17 and new rows 17–18; worksheet "Built:" lines; service/schema audit banners;
     `check-doc-hygiene.ps1` registers both retired method names.
- **Left alone, with the reason:** D16's re-point backlog (owner: "none urgent" — tracker **B23**);
  unproduced types 11/12/20/21/41/42 (tracker **B24**); story-lineage surrogate PK (a schema
  implication, not decided work); 90's source (outside D5's band, unruled — decision row 19 since the
  review fixes); self-recommender credit
  (unruled — row 18); the report-driven account action sending 81 to a member reporter
  (WU-ModerationIntegrity); presenter arms for 90–92 and §2.8's remainder (WU-NotificationCorrectness);
  throttles on the new write endpoints (WU-ThrottleCoverage, D20 pending); a story-centric USI index
  (D32 pending); splitting server-only `Notify*` off the WASM interface (D36/D37 pending).
- **Verification.** `dotnet build` green, no new warnings in touched files (the CS9107 in
  `ServerCommentWriteService` predates this WU). `dotnet test` green: **Unit 1,047** (+25,
  `NotificationPresenterTests`), **RazorComponents 726** (+23 — prompt rewrite, card, section, panel,
  story page, spotlight, Explore, Deep Dive, new `ChapterReadingPageAttributionTests`), **Integration
  1,250** (+70 — new `RecommendationAttributionTests` and `NewChapterNotificationTests`; moderation,
  notification, group, comment/blog, rec read/write, parent-visibility, endpoint and verification/fanon
  additions). Mutation-checked: dedup exemption, author exclusion, D17 predicate, credit gate, both
  trigger-5 sweeps, trigger 1, the panel's adoption clause and both fan-out guards each fail their
  tests when removed. All four PowerShell gates pass. **No browser was available** — tracker **H14**
  (run and closed 2026-09-30 — "Browser verification" below).
- **Review fixes (second commit, 2026-09-30).** Three reviews; 19 findings (14 distinct), each
  checked against the code and the worksheet. All were real; one needed no change once checked:
  1. **The acting moderator is never notified of their own act.** D5's null source drops nobody, so
     the band lost drop-self's protection. A moderator resolving a report they filed got 81/82, and
     one removing their own content got 70; the same held for 75/71 (own story), 76–79 (own account
     or link), 72–74 (a report-driven action on themselves) and 26 (a fanon name they used). Every call
     site now skips them. This is D4's guardrail generalized in `layer2-services.md`; 80 stays, since
     filing is the member's own act.
  2. **The dismissed prompt stays dismissed.** X deleted the row, but `?rec=` stayed in the address,
     so a reload minted a new one. The page now drops the carrier once used (replaced history entry,
     scroll kept), and `IsAttributableAsync` refuses an already-credited rec. That second rule is
     derived, not owner text: such a row could never be consumed.
  3. **The card's saved state is the host's.** Both card hosts latched "Saved for later". A Read It
     Later cleared in the panel left the button disabled, which blocks D3's re-RIL, and Deep Dive
     carried the latch to the next node. The latches are gone; the panel raises `OnStateSaved`,
     `StoryCard` forwards it, and the four hosts update their copy. Deep Dive and Explore key the
     cards.
  4. **Tests the WU owed:** new `ReadItLaterRecommendationCardTests` (nudge, error paths), the
     section's error path, Deep Dive's C11 case (a gem node), and `RecordSuccessAsync`'s
     lingering-row delete.
  5. **Docs:**
     - Moment 2: `layer3-logic.md` (idle adoption and reporting, and why not an `@key` remount) and
       `layer3.5-structure.md` (the panel example).
     - The audit notes the WU skipped: F5, F7/F44, F33 and F55. F33 and F55 L4.5 go to 1, and H14
       is extended.
     - Roadmap row 19 (type 90's moderator attribution); the enricher bound now reads "max 8";
       session-relative wording fixed.
     - WU-StoryLifecycle's "`StoryRejected` carrying its reason" hand-off is closed with no change. No
       notification carries free text (`content-safety.md` §"Content Removal"); the author reads the
       reason in the editor (`StoryUpdateDTO.RejectionReason`); the column issue is tracker **D6**.
  6. **Checked, nothing to fix:** rows written before the migration. 60/25 rows anchored on a group
     id and moderator-sourced 70–82/26 rows would display wrongly, but a `psql` count found 0 such
     rows in the dev DB (reset since), the seed writes neither shape, and no production DB exists.
  `dotnet test`: **Unit 1,058, RazorComponents 754 (+16), Integration 1,261 (+8)**. Every
  fix-specific test failed against a targeted revert of its fix. Build clean in touched files; all
  four gates pass. **No browser was available** — H14 gained the un-save, Deep Dive, carrier and
  self-resolve steps.
- **Browser verification (2026-09-30, after the review fixes; one commit, "browser verification" — no
  bug found, so no fixes commit).**
  - **Setup:** Aspire path, so the type-10 and type-70 emails could be read in Mailpit. The Aspire DB
    was wiped first: its schema was eight weeks old. The phase was read from the network log
    (`_blazor/negotiate` against `/api` calls). `psql` after every write.
  - **What was driven:** all seven H14 steps. Steps 1, 3 and 4 and the bells ran on both phases.
    - Card Read It Later in Explore, the story page, the homepage spotlight (granted and redeemed
      through the UI) and Deep Dive. The panel kept the bit; a panel clear deleted the attribution and
      re-enabled the card.
    - The anonymous nudge, and back after signing in.
    - Yes and X.
    - The `?rec=` link. Story 1's Chapter 1 was lengthened as a scroll fixture. At 90% the address
      dropped `?rec=` with the scroll held, and Back from Chapter 2 was clean.
    - The type-10 fan-out: both followers got the bell and the email; a republish wrote nothing.
    - The report receipt, the null-sourced 81/82/70, and a moderator's self-resolve sending no 82.
    - 60/25 on the `GroupStory`; a re-add wrote nothing.
    - A refused card save on a stale page reads "That content couldn't be found — it may have been
      removed." on both phases.
    - Trigger 5 by the author's remove.
    - GIF: `e2e-WU-InertFeatures.gif`.
  - **Not driven** (Integration covers both): the moderator-takedown sweep and the story-author gate.
  - **Tooling:** `stop-aspire.ps1 -StopContainers` missed the Mailpit container, which was added after
    the script was written. It now stops all four. `run-server/SKILL.md` gained notes on the Aspire
    log location, hidden-tab scrolling and the first antiforgery failure after a wipe.
  - **Docs:** browser notes in `audit/Recommendations.md` F30 and `audit/Notifications.md` F41. Short
    notes in F16, F33, F55, F5, F7, F6, F39 and F46. F16/F30/F33/F41/F55 L4.5 are back to 5 in
    `status.md`. H14 closed and B22's line updated.
  - **Tests** (no code change): Unit 1,058, RazorComponents 754, Integration 1,261. All four gates pass.
  - **Dev-DB state left behind** (Aspire DB, `canalavedb` on 5433; `reset-aspire-db.ps1` drops it):
    - Story 1's Chapter 1 text has 60 filler paragraphs appended (`chapter_content_id` 1).
    - Rec 3's text was rewritten to a 500+ character filler. Two revision requests made the edit
      necessary to restore Approved.
    - Rec 2 was removed and unblocked, so it is Approved again.
    - Chapter 8 (story 2, Chapter 3) is published, with notifications 6–7.
    - Reports 2, 4 and 5 are resolved and comment 3 is hidden.
    - `GroupStory` 3 (story 7 in group 1).
    - A spotlight for story 5 with rec 3 in the Sep 28 – Oct 5 block, from AuthorAlpha's granted slot.
    - Successes: TestUser→rec 2, ReaderGamma→rec 3, AuthorBeta→rec 1.
    - Live attributions: TestUser (story 5, rec 3) and ModUser (story 5, rec 3).
    - Read It Later: TestUser on stories 3 and 5, ReaderGamma on story 5, LurkerDelta on story 3 and
      ModUser on story 5. ReaderGamma also follows story 2.
    - LurkerDelta and AuthorBeta have started story 1, and AuthorBeta has completed story 3.
- **Pointers:** `layer2-services.md` §"Notification Generation", §"Polymorphic RelatedEntityId",
  §"Attribution (Feature 30)"; Stage notes in `audit/Recommendations.md` F30,
  `audit/UserStoryInteractions.md` F16, `audit/Notifications.md` F41/F42, `audit/Groups.md` F39,
  `audit/Moderation.md` F46/F47/F48/F53, `audit/Chapters.md` F6 and (review fixes) F7/F44,
  `audit/Tags.md` (type 26), and (review fixes) `audit/Stories.md` F5, `audit/Discovery.md` F33,
  `audit/Spotlight.md` F55.

## WU-AccessGateSweep2 — raises gated, clears free; service audit §2.6 access fixes (worksheet D6; extends `UserStoryInteractions/`, `Chapters/`, `BlogPosts/`, `Comments/`, `Recommendations/`, `Following/`, `Collaboration/`, `Stories/`, `Profiles/`, `Seo/`) — DONE ✓ (2026-09-30)

- **Cells:** none flipped — every change sits beneath an already-Stage-5 cell (F10, F16, F18, F20,
  F23, F25, F28, F35–37, F44, F49, F50, F64, F66); the WU-ParentVisibility shape. Markup changed
  only in `PrivacySettingsForm`'s option values, so no L4.5 cell moved (browser pass: tracker **H13**,
  run and closed 2026-09-30 — see "Browser verification" below).
- **Trigger:** owner ruling **D6** (answered 2026-08-04, unbuilt): the kind-(g) guard ran on clears
  too — a mature-off reader could not un-favorite an M story; mark-unread was refused on taken-down
  stories. Plus service audit §2.6's items not blocked on D25–D27. Sources: worksheet D6; service
  audit §2.6/§3.5; `access-gating-first-principles.md` §5 row 1b; tracker E6.
- **What landed:**
  1. **D6.** `SetUserStoryInteractionStateAsync` loads, diffs, then guards raises only (no row +
     all-false returns before any guard; a mixed payload is refused whole); the two read-mark
     `(…, false)` paths skip both guard and existence check (no oracle); blog/comment/rec
     `ToggleLikeAsync` skip the guard when the caller holds the like row; `SetReceiveAlertsAsync(true)`
     on an alerts-off row is now a guarded raise. The six already-unguarded sibling clears (reveal
     remove, unfollow, remove-vouch, group leave, list/series remove-story) plus both curation-flag
     clears are enrolled as conformance tests. The enumeration table is in `audit/AccessGate.md` F66.
  2. **Profile blog posts are profile-tab data** (derived from first-principles §5 row 1b + the F15
     permalink precedent, recorded as settled in `audit/AccessGate.md`): `BlogPostVisibilityFacts`
     gained `AuthorProfileVisibility`; the profile check sits above the verified-bot short-circuit;
     `ProfileVisibilityGuard` gained its pure `IsVisible` overload; the gate read returns null for a
     hidden author; the sitemap lists only Public authors' profile posts. All guard consumers inherit it.
  3. **Bare-FK reads:** story-acknowledgment and lineage by-story reads gained `IsStoryVisibleAsync`.
     `RevokeAsync` checks ownership before the credit lookup.
  4. **`AllowProfileComments`** is enforced in `PostUserProfileCommentAsync` (four tiers, mirrors the
     messaging gate, `CommentValidationException`, after the `ProfileVisibility` guard).
     **`PrivacySettingsForm`**'s "Off" options wrote `2` (`Following`), not `Nobody` — found while
     scoping, now bound to the enum.
  5. **Docs, moment 1:** `identity-and-authorization.md` kind (g) + §"Raises vs clears" + guard table;
     `layer2-services.md` Content-Rating case 2 cross-ref + new §"`AllowProfileComments` Gate";
     `content-safety.md` "write paths" corrected; `layer3.5-structure.md` UserProfile gating rewritten;
     `status.md` kind-(g) bullet; `audit/AccessGate.md` Settled; first-principles §6.4 annotated;
     roadmap Resolved D6; worksheet D6 "Built:" line; tracker F10, E6 annotation. Interface docs on
     eleven Core interfaces. False comments fixed in the touched methods: the USI "reject impossible
     combinations" comment and the `cross-cutting.md` cites at USI (×2), Comment and Rec
     `ToggleLikeAsync` — the remaining `cross-cutting.md` cites (Following, other Comment, BlogPost)
     are WU-DocCorrections'.
- **Left alone, with the reason:** saved-selection copy path (**D25** pending); series and custom-list
  by-id reads and `CloneListAsync`'s messages (**D26** family) — ledger home since the review fixes:
  tracker **F11**; edit-read 403-vs-null oracles, own-content curation raises,
  `AllowProfileComments=Nobody` read semantics — owner-open, tracker **F10** items 2–4; E6's four
  clauses (annotated; clause 1 → WU-ThrottleCoverage). *(The build also left poll-vote retraction as
  F10 item 1, recorded a blog fan-out "accepted risk", and returned a hidden parent's `LikeCount` on
  unlike as a "flagged derivation" — all three were corrected by the review fixes below.)*
- **Verification.** `dotnet build` green, no new warnings in touched files. `dotnet test` green:
  **Unit 1,022** (+12, `VisibilityGuardRuleTests`), **RazorComponents 701** (+5,
  `PrivacySettingsFormTests`), **Integration 1,171** (+40 — `ParentVisibilityContractTests` clear /
  conformance / Private-author / acknowledgment-lineage sections, `CommentWriteServiceTests`
  AllowProfileComments incl. an HTTP 400 check, `StoryAcknowledgmentServiceTests` revoke, sitemap).
  Mutation check: with the production changes stashed, every fix-specific Integration test (26) and all
  five bUnit tests failed; the Unit bot-ordering test failed when the bot check was moved back up. All four
  PowerShell gates pass. **No browser was available** — tracker **H13**.
- **Review fixes (second commit, 2026-09-30).** Three reviews; 11 findings (8 distinct) checked
  against the code and the worksheet, all real, none rejected (one overstated its scope):
  1. **A clear's response is a read and stays gated.** The three unlikes returned the hidden
     parent's post-toggle `LikeCount` — a WU derivation that `identity-and-authorization.md` stated
     under the D6 heading as if owner text, and that contradicts D6's "deleting your own row teaches
     them nothing". On a hidden parent they now return `(0, false)`; the counter still moves.
  2. **Poll-vote withdrawal moved** (was F10 item 1 — D6 leaves no sub-edge open, and the return
     shape is engineering). `VoteAsync` loads, diffs, then guards: a pure withdrawal skips the
     guard; an added option or an anonymity flip guards the whole call; with no vote a hidden poll
     answers like a missing one. Contract `Task<PollDto?>` (null = withdrawn from a poll the caller
     can no longer see); client, endpoint and `PollView` (raises `OnPollChanged(null)`) follow.
  3. **Two more paths follow the profile-post rule:** the reveal-management list's post titles
     (a title oracle for drafts and Private authors' posts — the consent endpoint mints rows for
     any id), and the publish fan-out — a Private author's post now notifies nobody (`UsersOnly`
     needs no check; the review's `UsersOnly` claim was the overstatement). Residual (notifications
     minted before the author went Private) → tracker **D8**, WU-NotificationCorrectness.
  4. **Ledgers and docs:** tracker **F11** is the home for the D25/D26 slice, and the worksheet's
     Block G / D6 "Built:" lines point at it; the read-mark clear theory gained the rating axis;
     `content-safety.md`'s history line corrected; service audit §2.6/§3.5/§4/§7 annotated;
     `layer2-services.md` §"Comment & blog-post semantic methods" gained the fan-out rule.
  `dotnet test`: **Unit 1,022, RazorComponents 703 (+2, `PollViewTests`), Integration 1,180 (+9)**;
  every fix-specific new test failed against a targeted mutation of its fix (the controls — visible-post unlike, `UsersOnly` fan-out, refreshed `PollView` result — pass both ways by design); build clean in touched files; all
  four gates pass. **No browser was available** — H13 gained step 5 (stale-page withdrawal/unlike).
- **Browser verification (2026-09-30, after WU-InertFeatures and the WU-StoryLifecycle browser pass
  had landed; two commits: "browser-pass fixes", then "browser verification").**
  - **What was driven:**
    - All five H13 steps, on the circuit and on WASM, with `psql` after each write. The phase was
      read from the network log (`_blazor/negotiate` against `/api` calls).
    - Privacy "Off" writes `3`/`3`. Its wall and messaging refusals read correctly.
    - A D6 clear (Read It Later) on a rating-hidden M story, from the bookshelf card. H13 said
      "un-favorite", but Favorite is read-only in listing context.
    - AuthorAlpha Private (the settings form): real 404s for a reader, a follower and anonymous;
      no API oracle; the sitemap drops the post; the reveal list shows "(deleted post)"; a Private
      publish notifies nobody (the Public publish was the control).
    - The `Following` wall's inline error and the followed user's successful post.
    - The stale-page flows (the GIF, `e2e-WU-AccessGateSweep2.gif`): vote switch refused with the
      not-found message, withdrawal drops the poll and the row, unlike settles at 0 while
      `like_count` drops by one (2 → 1 on WASM).
  - **Bugs fixed:**
    1. A raise refused on a parent hidden behind an open page escaped its handler. The page error
       boundary then replaced the whole page (interaction panel; `FollowButton`'s alerts-on, a raise
       this WU guarded), on both phases. The fix rolls back to the last accepted state and shows the
       refusal inline. `VouchButton` was fixed the same way. Rule: `layer3-logic.md` §"Optimistic
       Updates & Debounce" rule 4, with a line in `error-handling.md`.
    2. Dead `/profile/{id}` links on the blog post page, the poll voter list and the group page now
       go to `/user/{id}`.
  - **Tests:** RazorComponents +8 (panel ×2, `FollowButton` ×2, `VouchButton` ×1, three link hrefs).
    All 8 failed with the SharedUI changes stashed. `dotnet test`: **Unit 1,058, RazorComponents 738,
    Integration 1,253**. All four gates pass.
  - **Not driven:** the acknowledgment/lineage reads and `RevokeAsync` (no UI path / API-only),
    unfollow on a Private profile (seed counter, H18). Integration covers them.
  - **Tracker:** H13 closed. New: **F12** (the new-post Publish checkbox is ignored — draft-first),
    **H16** (bool-bound ARIA states render absent or empty), **H17** (other uncaught write handlers),
    **H18** (seed follower counters).
  - **Dev-DB state left behind** (the DB was reset first; reset again to drop it):
    - All seven users' privacy settings are back at seed values.
    - ReaderGamma (mature off) holds story 4 favorite + followed (the follow bit came from a
      stale-claim test), and a reveal of post 4. ReaderGamma likes post 2 (`like_count` 1).
    - TestUser's wall comment 6 is on AuthorAlpha's wall.
    - AuthorAlpha has an open poll 1 "E2E Poll: Which region?" (no votes) on post 2, M post 4
      (published) and post 5 (published).
    - Notifications 5–6 (type 13, post 4) for TestUser and ReaderGamma.
- **Pointers:** `identity-and-authorization.md` §"Parent-visibility guards" → "Raises vs clears";
  `layer2-services.md` §"`AllowProfileComments` Gate", §"Comment & blog-post semantic methods";
  `audit/AccessGate.md` F66 (enumeration record), `audit/UserStoryInteractions.md` F16,
  `audit/Chapters.md` F44, `audit/BlogPosts.md` F35–37, `audit/Comments.md` F23/F25,
  `audit/Recommendations.md` F28, `audit/Following.md` F18, `audit/Stories.md` F10,
  `audit/Badges.md` F50, `audit/Profiles.md` F20, `audit/Messaging.md` F49, `audit/Seo.md` F64,
  `audit/Notifications.md` F41; `roadmap.md` §Resolved (D6); tracker D8, E6, F10, F11, H13.
  Browser pass: `audit/AccessGate.md` F66 browser-verification note (the narrative), with notes in
  `audit/UserStoryInteractions.md` F16, `audit/Following.md` F18, `audit/BlogPosts.md` F35–37,
  `audit/Groups.md` F40, and the F20/F23/F41/F49/F64 notes; `layer3-logic.md` §"Optimistic Updates &
  Debounce" rule 4; tracker F12, H16, H17, H18.

## WU-StoryLifecycle — story status transition table, first-submission approval gate + trust waiver, nullable publish anchors (worksheet D1 + D2; extends `Stories/`, `Chapters/`, `Moderation/`, `Identity/`, `Export/`) — DONE ✓ (2026-09-30)

- **Cells:** the build flipped none (the gaps were beneath already-Stage-5 cells); its review fixes
  flipped **F4, F47, F48 L4.5 5→1** — the publishing, approval and auto-approve UI they vouched for
  changed and was never browser-driven — and the browser pass the same day returned **all three to 5**.
  F5, F6, F7, F54, F62 and F64 unchanged; F47/F48 L4 stay 3.
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
  carrying its reason → D4/D5 / WU-InertFeatures (closed there with no change at its review fixes —
  no notification carries free text; see its DONE entry). Observed and filed, not fixed: chapter publish
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
- **Review fixes (second commit, 2026-09-30).** Three independent reviews; 21 findings checked
  against the code and the worksheet, all real, all fixed or routed:
  1. **Takedown freeze (L2, flagged — derived from D1's rationale, not an owner sentence).** A
     taken-down story could be unpublished over the API, resubmitted, then approved (+1 trust) or
     rejected (overwriting its takedown reason). Now `TransitionStatusAsync` refuses every move on an
     `IsTakenDown` row, approve/reject refuse it, the queue keeps the `IsTakenDown` filter on, and all
     three conditional `WHERE`s carry `!IsTakenDown`.
  2. **Tests for the guards themselves:** new `InterleavingCommandInterceptor` (+ `testing.md`
     §"Testing a check-then-act guard") lands a competing write between read and update — approve,
     reject and transition each refuse and write nothing; a failed trust write rolls approve's flip
     back. Plus: auto-approve restore/unknown/over-long; explicit-`false` insert; new
     `StoryEditorPageTests` (the page applies the RESULTING status); StoryPage date cases. The
     guard, takedown, transaction, initializer and page tests were mutation-checked.
  3. **UI:** Unpublish now opens a destructive `ConfirmDialog` (the convention already named it);
     `ModSubmissionsPage`'s reload-after-refusal no longer escapes the handler or strands "Loading…";
     queue/panel copy no longer assumes only first-ever submissions are reviewed.
  4. **Docs:** `layer1-data-model.md` corrected (EF Core 10 infers `Sentinel = true`; the `= true`
     initializer is the load-bearing half); Export.md F54 Stage note added; roadmap D1 entry no
     longer attributes restore to the owner; folder_clusters Stories row, service audit §2.7.5
     banner, migration comment (pre-WU pulled-back drafts lost their date — dev data only).
  5. **Routed, not decided:** decision rows **14** (import rider), **15** (notify on auto-approve
     change), **16** (does a chapter "go live" while its story isn't? — its L8 effect recorded in
     `layer8-data-marts.md`); tracker **E7** (the `/status` endpoint's throttle classification →
     WU-ThrottleCoverage — the ledger copy of a prose routing).
  `dotnet test`: **Unit 1,010, RazorComponents 696 (+10), Integration 1,131 (+14)**; build clean in
  touched files; all four gates pass. **No browser was available** — H12 now covers the confirm
  dialog and the hidden taken-down rows.
- **Browser verification (2026-09-30, after WU-InertFeatures had landed; two commits: "browser-pass
  fixes", then "browser verification").**
  - **What was driven:**
    - Every H12 item, on the circuit and on WASM, with `psql` after each write. The phase was read
      from the network log: `_blazor/negotiate` against `/api` calls.
    - Untrusted TestUser: create (Draft, NULL date), the save-first submit hint, submit, withdraw,
      resubmit.
    - Trusted AuthorAlpha (the GIF): submit publishes directly, published moves, unpublish behind
      the destructive dialog (Cancel keeps it published), republish with the date kept.
    - Revoke (circuit) and restore (WASM) on `/mod/users/4`, with the queue/direct routing and the
      audit rows checked.
    - `/mod/submissions`: `SubmittedDate` order; already-handled from a stale tab on both phases; a
      taken-down pending row hidden; approve +1 trust; reject storing the reason.
    - Readable refusals: the circuit shows the server's sentence. WASM shows "Story validation
      failed." for author moves and saves (the recorded P1 gap) and the real text for moderator
      refusals.
  - **Three runtime bugs fixed:**
    1. A refused story save crashed the circuit (Quill `removeChild`). `EditorView` now freezes its
       rendered content at the first render. This is cross-cutting: the three blog editors share the
       write-back. Rule: `layer5-wasm.md` §"WASM renderer vs third-party DOM" rule 3.
    2. The canonical-slug middleware 301'd every full load of a published story's editor to the
       story page. New `StorySlug.IsReservedRouteSegment`; the slug generator now suffixes `edit` and
       numeric slugs (F64 note in `audit/Seo.md`).
    3. The rejection reason rendered as "_rejectionReason" (a missing `@`).
  - **Tests:** RazorComponents `EditorViewTests` (2) and `StoryEditorPageTests` +2; Unit
    `StorySlugTests` +11; Integration `ContentGateTests` +1 and `StoryWriteServiceTests` +2. Each
    fails with its fix reverted, checked by hand. `dotnet test`: **Unit 1,058, RazorComponents 730,
    Integration 1,253**. All four gates pass.
  - **Tracker:** H12 narrowed to the WASM re-check that waits on P1. New: **D9** (moderator reasons
    in the query string) and **H15** (raw enum name and a lingering message on the queue card).
  - **Dev-DB state left behind** (the DB was reset first; reset again to drop it):
    - TestUser has `approved_story_submissions` = 1.
    - Story 8 is In Progress (published 2026-10-01). Its stale rejection reason is still in
      `takedown_reason` (tracker D6).
    - Story 9 (AuthorBeta) is Rejected with an E2E reason.
    - Story 10 (AuthorAlpha) is In Progress, published 2026-10-01, with a stale rejection reason.
    - Stories 13 "E2E Lifecycle: TestUser Story" (Completed) and 14 "E2E Refused Save Check" (Draft)
      were added.
    - AuthorAlpha has two audit reports (revoke + restore) and is back to auto-approve on.
    - Notifications: 75 and 71 for TestUser, and a 71 each for AuthorAlpha (story 10) and AuthorBeta
      (story 9).
    - This Chrome profile caches the old 301 for `/story/10/edit`. Clear cached files, or use another
      story, to reach that editor by full load.
- **Pointers:** `layer2-services.md` §"Story Lifecycle", §"Records of a decision are not counters",
  §"Account actions" rule 1; `layer8-data-marts.md` §`site_daily_stats`; `layer1-data-model.md`
  §"Column Conventions" (true-default bools); `audit/Stories.md` F4/F5, `audit/Chapters.md` F6/F7,
  `audit/Moderation.md` F47/F48/F62 Stage notes, `audit/Export.md` F54; `roadmap.md` §Resolved (D1,
  D2) and decision rows 14–16; `testing.md` §"Testing a check-then-act guard"; tracker D6, D7, E7, F9,
  H12, D9, H15; worksheet D1/D2 "Built:" lines; browser pass: `layer5-wasm.md` §"WASM renderer vs third-party DOM" rule 3, `identity-and-authorization.md` route note, `audit/Seo.md` F64 and the browser-verification Stage notes in `audit/Stories.md` F4 and `audit/Moderation.md` F48.

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
