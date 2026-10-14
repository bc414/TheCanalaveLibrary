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

- **Last landed:** WU-D21Confidentiality (2026-10-14) — tracker **D21** closed, the campaign wrap-up's
  fix WU. A notification never names or links a story outside the public window (Draft, PendingApproval,
  Rejected) or taken down unless its recipient is the story's author: `NotificationEnricher`'s `Story`,
  `Chapter` and `GroupStory` kinds each split by recipient the D8 way (23 types enumerated; the `Own*`
  kinds resolve as stored, minus a link that would 404 for the author, the rest read the public window,
  and a followed chapter must be published). Members' 60 keeps the group and drops the story title; the
  lineage-request and credit-on-a-draft leaks close with it; type 52's title-free copy fixed. Rule in
  `layer2-services.md`. Decision row 31 / **F28** stays open. **F42 L4.5 5→1** (no browser; **H43**).
  Filed **D22** (the outgoing lineage list) and, by its review fixes, **F29** / new decision **row 32**
  (may a credit or lineage request name an unpublished story). `dotnet test`: Unit 1,377,
  RazorComponents 929, Integration 1,687. **Pointer:** its DONE entry.
  Before that, 2026-10-13: WU-DocCorrections — the campaign's doc backstop (service audit §4's doctrine re-synced, false comments corrected, the CORRECTION-note convention, /fanon reach fixed; D21/F28/H41/H42 filed; its fresh-eyes probe gate is still open, **H42**, and the campaign's wrap-up waits on it); see its DONE entry.
  Before that, 2026-10-13: WU-TrackerFixes — the tracker sweep (B16/B17/D8/H7 closed, U11 built; row 30/F27, E12, E13, H39, H40 filed, H38 closed by its browser pass); see its DONE entry.
  Before that, 2026-10-13: WU-ParityAndRemaining — service audit §2.7 and §2.12 (WASM parity, the remaining service defects; row 29/F26 filed, H37/H12 closed by its browser pass); see its DONE entry.
  Before that, 2026-10-13: WU-PrivateMessageEmail — worksheet D18 (Messaging-owned PM email, one per unread run, its own preference and unsubscribe, a shared `(kind, id)` pipeline; row 28/F25, D20 filed, H35 closed by its browser pass); see its DONE entry.
  Before that, 2026-10-13: WU-AuthorStoryDelete — worksheet D15 (an author's hard delete of their own story under any moderation state, an unstarted Spotlight slot returned, a typed-title dialog; row 27/F24, D19 filed, H34 closed by its browser pass); see its DONE entry.
  Before that, 2026-10-13: WU-ThrottleCoverage — worksheet D20's throttle half (three new kinds, the unthrottled-surface enumeration, the WASM 429 rebuilt, `AnonPrefs`' shared-default consent leak fixed; row 26, D16–D18/F23 filed, H33 closed by its browser pass); see its DONE entry.
  Before that, 2026-10-13: WU-BlobCleanup — worksheet D14 (inline post-commit blob deletes plus a daily orphan sweep, cover upload persists its own reference; F22/row 25 filed, H31/H3 closed by its browser pass); see its DONE entry.
  Before that, 2026-10-13: WU-PollVoteIntegrity — worksheet D23's poll paragraph and `ConfigLocked` rider (single-choice a database constraint, `AllowMultiple` frozen under a vote by an FK; F21/row 24 filed, H30 closed by its browser pass); see its DONE entry.
  Before that, 2026-10-13: WU-UserDeletion — worksheet D13 (poll votes survive anonymized, empty conversations deleted at the source, the deletion page as the compliance surface, the admin-less group rescue; F17–F20/E11/row 23 filed, H29 closed by its browser pass); see its DONE entry.
  Before that, 2026-10-13: WU-SchemaHardening — folder integrity, `group_stories` as a set, ten model-declared CHECKs (decision row 22, trackers D14/D15/E10/H27 filed; H28 closed by its browser pass); see its DONE entry.
  Before that, 2026-10-12: WU-NotificationCorrectness — worksheet D19 plus service audit §2.8's remainder (per-field settings, mail restores only the unsent, feed tiebreak, subject cap); see its DONE entry.
  Before that, 2026-10-12: WU-DependencyCVEs — the NuGet advisory sweep (Crypto.Xml, SSH.NET and AngleSharp patched, E2 closed, ImageSharp held as accepted risk pending decision row 21); see its DONE entry.
  Before that, 2026-09-30: WU-CounterSymmetry — owner rulings D21–D24 (every counter derived and reconciled, post-commit counters, check-then-act per family, creating a group counts as joining); see its DONE entry.
  Before that, 2026-09-30: WU-TptHardDelete — worksheet D10/D11/D12 (content-parent deletes through `TptDelete`, RESTRICT FKs, per-subtype blog-post lifecycle, polls survive their owner); see its DONE entry.
  Before that, 2026-09-30: WU-ModerationIntegrity — worksheet D7/D8/D9 (lock-and-guard resolves, sibling closing, `ReportedUserId`, Reinstate, service-side mod read gates); see its DONE entry.
  Before that, 2026-09-30: WU-InertFeatures — worksheet D3/D4/D5/D16/D17 (attribution on the RIL bit, the de-identified notification core, the new-chapter fan-out); see its DONE entry.
  Before that, 2026-09-30: WU-AccessGateSweep2 — worksheet D6 (raises guarded, clears free) plus service audit §2.6's access fixes; see its DONE entry.
  Before that, 2026-09-30: WU-StoryLifecycle — worksheet D1/D2: the story transition table, first-submission approval gate, nullable publish anchors; see its DONE entry.
  Before that, 2026-09-20: WU-QuickFixes — four no-deliberation closures (MA-107, MA-408, H6's VouchButton staleness, MA-007/MA-211); see its DONE entry in `workplan-archive.md`.
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
  **Built (between-phase): the worksheet-decisions build campaign** — the answered rows of
  `.claude/design/audit-decision-worksheet.md` built as a sequence of WUs; WU-StoryLifecycle (D1/D2),
  WU-AccessGateSweep2 (D6), WU-InertFeatures (D3/D4/D5/D16/D17), WU-ModerationIntegrity
  (D7/D8/D9), WU-TptHardDelete (D10/D11) and WU-CounterSymmetry (D21–D24) have landed, then (on the
  2026-10-12 resume) the unplanned WU-DependencyCVEs and WU-NotificationCorrectness (D19), then
  WU-SchemaHardening (2026-10-13), then WU-UserDeletion (D13, 2026-10-13), then WU-PollVoteIntegrity (D23's poll restructure, 2026-10-13), then WU-BlobCleanup (D14, 2026-10-13), then WU-ThrottleCoverage (D20's throttle half, 2026-10-13), then WU-AuthorStoryDelete (D15, 2026-10-13), then WU-PrivateMessageEmail (D18, 2026-10-13), then WU-ParityAndRemaining (service audit §2.7/§2.12, 2026-10-13), then WU-TrackerFixes (the tracker sweep, 2026-10-13), then WU-DocCorrections (the doc backstop, 2026-10-13), the last in its build order. The campaign's wrap-up (a final full verification and the owner summary of open decisions and tracker items) has not run yet; it waits on tracker **H42**, WU-DocCorrections' fresh-eyes doc-audit probes, which need a session with a subagent tool.
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
  B14/B15/B16/H9 newly opened (B16 since closed, WU-TrackerFixes 2026-10-13) — **H9 is now unblocked**, since H10 had the funnel it needs to
  drive returning 500; **D6/D7/E7/F9/H12** opened by WU-StoryLifecycle, 2026-09-30 — E7 was closed by
  WU-ThrottleCoverage, 2026-10-13; D6 was routed to WU-ModerationIntegrity, which left it open (a rejection files
  no `Report` row, so D8(b) does not cover it); **F10/H13** opened by WU-AccessGateSweep2,
  2026-09-30, which also annotated E6 without closing it; **B23/B24/H14** opened by WU-InertFeatures,
  2026-09-30, which closed B20–B22 (its browser pass closed H14 the same day); **D9/H15** opened by the WU-StoryLifecycle browser pass,
  2026-09-30, which narrowed H12; **F12/H16/H17/H18** opened by the WU-AccessGateSweep2 browser
  pass, 2026-09-30, which closed H13; **F13/F14/F15/D10/H19** opened by WU-ModerationIntegrity,
  2026-09-30, which closed B18 (its browser pass closed H19 the same day and opened **E8/H20**);
  **B25/F16/H21** opened by WU-TptHardDelete, 2026-09-30 (its browser pass closed H21 the same day and
  opened **H22**); **D11/H23** opened by WU-CounterSymmetry, 2026-09-30 (its browser pass narrowed
  H23 to an owner check the same day and opened **D12/H24**); WU-DependencyCVEs, 2026-10-12, closed
  **E2** and opened **E9**, the ImageSharp licensing call the owner has to make, now decision row 21;
  WU-NotificationCorrectness, 2026-10-12, opened **D13/H26** and re-scoped **D8** without building it (its review fixes, 2026-10-13, routed D8 to WU-TrackerFixes; its browser pass closed H26 the same day);
  WU-SchemaHardening, 2026-10-13, opened **D14/H27/H28**, and its review fixes **D15** (its browser
  pass closed H28 the same day and opened **E10**); WU-UserDeletion, 2026-10-13, opened **F17/F18/F19/H29** and closed H6's last-admin-leave bullet (its browser pass closed H29 the same day); WU-PollVoteIntegrity, 2026-10-13, opened **F21/H30** (its browser pass closed H30 the same day and added the poll sites to **E10**); WU-BlobCleanup, 2026-10-13, opened **F22/H31** (its browser pass closed H31 and H3 the same day and opened **H32**); WU-ThrottleCoverage, 2026-10-13, opened **D16/D17/F23/H33**, closed **E7** and annotated E6 (clause 1 resolved) and D14 (now the ledger home of §2.11 too) (its browser pass closed H33 the same day and opened **D18**); WU-AuthorStoryDelete, 2026-10-13, opened **F24/H34** and annotated F13 (site 5 built, still open), F3 (the ToS line) and B25 (the story half of its copy duty paid); its review fixes opened **D19** and annotated D18; its browser pass closed H34 the same day); WU-PrivateMessageEmail, 2026-10-13, opened **F25/H35/H36** (its review fixes folded H36 into **H17**, added F25 (e), extended H35 and annotated D15; its browser pass closed H35 the same day, opened **D20** and annotated F6b); WU-ParityAndRemaining, 2026-10-13, opened **F26/H37**, folded H12's last step into H37 and annotated **D11** (its browser pass closed H37 and H12 the same day and annotated H22 and F6b); WU-TrackerFixes, 2026-10-13, closed **B16/B17/D8/H7**, opened **F27/H38**, re-verified H8's residual and annotated F6b (its review fixes opened **E12** and extended H38; its browser pass closed H38 and opened **E13/H39/H40**); WU-DocCorrections, 2026-10-13, opened **D21/H41/H42** (its review fixes, 2026-10-14, split D21's product question out as **F28**, decision row 31, and raised D21 and H42); WU-D21Confidentiality, 2026-10-14, closed **D21** and opened **D22/H43** (its review fixes split D22's product question out as **F29**, decision row 32),
  including two **high-priority security items:
  E3 and E9**. **A7** is the
  remaining half of `roadmap.md`'s Tier-6 discovery pair now that A6 is closed; it is a heavier
  lift (reopens the frozen `DiscoveryMartSchema` for a 7th UNION arm) and unchanged by this work. WU-ErrorHandling2's named
  follow-up (the 8 SOLO editor pages' `ErrorAlert` adoption) was built by WU-TrackerFixes, 2026-10-13. WU-StatBadgeProducers' `SeedTool` follow-up landed the same day — see its
  DONE entry.
- **Blocked on Brian:** decision rows 4, 6, 8, 10 and **14–32** (`roadmap.md` §"Decisions
  that need you"; rows 2 and 13 resolved 2026-07-28, row 12 resolved 2026-07-31; rows 14–16 — the
  story-lifecycle questions WU-StoryLifecycle left open — 17–18 — the two defaults
  WU-InertFeatures had to take — 19 — type 90's moderator attribution, filed by its review
  fixes — and 20 — WU-ModerationIntegrity's two derived account-status refusals — added
  2026-09-30; 21 — ImageSharp licensing (tracker **E9**: a Six Labors key, a library swap or
  standing risk) — added 2026-10-12 by the WU-DependencyCVEs review fixes; 22 — the folder-delete
  name-clash default — added 2026-10-13 by WU-SchemaHardening; 23 — type 62's moderator attribution —
  added the same day by the WU-UserDeletion review fixes; 24 — whether a poll's anonymity mode is
  frozen by the database like its vote mode — added the same day by WU-PollVoteIntegrity; 25 — whether
  account deletion strips the covers of the user's surviving stories — added the same day by
  WU-BlobCleanup; 26 — three follow-ups of D20's throttle half (the provisional `Vote`/`Reveal` numbers,
  group story-add, first-request friction) — added the same day by WU-ThrottleCoverage; 27 — five
  edges of D15's author story delete (a live block, the moderator path, telling the sponsor, the
  download's scope, how an author finds a taken-down story's editor) — added the same day by WU-AuthorStoryDelete;
  28 — five defaults D18's private-message email left to the build (on by default, no sender text,
  archive mutes, the run reading of its formula, no account-status gate) — added the same day by
  WU-PrivateMessageEmail, (e) by its review fixes; 29 — whether an author may edit content a moderator
  took down (freeze or remediate) — added the same day by WU-ParityAndRemaining, tracker **F26**; 30 —
  whether site announcements are reportable — added the same day by WU-TrackerFixes, tracker **F27**; 31 —
  whether an author may file their own unpublished story into a group — added 2026-10-14 by the
  WU-DocCorrections review fixes, tracker **F28**; 32 — whether a credit or a lineage link may be
  requested on an unpublished story, and what its counterpart sees while it is pending — added the same
  day by the WU-D21Confidentiality review fixes, tracker **F29**).
  Tracker **H38**, WU-TrackerFixes' browser pass, ran 2026-10-13, returned its fifteen L4.5 cells to 5
  and closed — see WU-TrackerFixes' DONE entry. Tracker **H43**, WU-D21Confidentiality's browser pass
  (F42 L4.5 is 1 until it runs: the bell and inbox for hidden stories, and type 52's copy), is open — see
  its DONE entry.
  Separately, not a numbered decision row: WU-A11y-Keyboard's browser pass (focus/Escape/keyboard-only) and the
  now-also-outstanding axe-DevTools pass WU-A11y (Structure) didn't reach need Brian's own browser
  session — see those WUs' DONE entries and `audit/Accessibility.md`. (Tracker **H12**'s story-lifecycle pass
  ran 2026-09-30 and returned F4/F47/F48 L4.5 to 5; its WASM re-check waited on
  WU-ParityAndRemaining P1, which landed 2026-10-13, and passed as step 1 of tracker **H37**, whose pass
  ran the same day, returned F24/F26/F49 L4.5 to 5, closed H37 and H12 and fixed three runtime bugs —
  see WU-StoryLifecycle's and WU-ParityAndRemaining's DONE entries. Tracker **H13**'s
  access-gate pass also ran 2026-09-30 and closed — see WU-AccessGateSweep2's DONE entry. Tracker
  **H14**'s attribution-and-notifications pass ran 2026-09-30 too, returned F16/F30/F33/F41/F55 L4.5
  to 5 and closed — see WU-InertFeatures' DONE entry. Tracker **H21**'s pass, WU-TptHardDelete's
  blog-post page and delete paths, ran 2026-09-30 too, returned F36 L4.5 to 5 and closed — see
  WU-TptHardDelete's DONE entry. Tracker **H23**'s pass, WU-CounterSymmetry's badge display, ran
  2026-09-30 too and returned F50 L4.5 to 5; H23 stays open only for Brian's keep-or-revert call on
  the `UserCard` manual-grant rider — see WU-CounterSymmetry's DONE entry. Tracker **H33**'s pass, WU-ThrottleCoverage's
  throttle messages on both render phases, the reveal paths and the consent leak fix, ran 2026-10-13,
  returned F8/F9/F10/F37/F50/F51/F66 L4.5 to 5 and closed; it fixed one runtime bug and filed **D18**
  — see WU-ThrottleCoverage's DONE entry. Tracker **H34**'s pass, WU-AuthorStoryDelete's delete dialog, its
  two outs, the taken-down editor and the deletion page's Stories line, ran 2026-10-13, returned F4/F52
  L4.5 to 5 and closed with no runtime bug — see WU-AuthorStoryDelete's DONE entry. Tracker **H35**'s
  pass, WU-PrivateMessageEmail's settings section, its email in Mailpit, the unsubscribe pages and the
  privacy form's pointer, ran 2026-10-13, returned F20/F43/F49 L4.5 to 5 and closed; it fixed one
  runtime bug and filed **D20** — see WU-PrivateMessageEmail's DONE entry. Tracker **H37**'s pass,
  WU-ParityAndRemaining's WASM error lists, edited marker, edit spoiler toggle, deleted participant,
  client config, anonymous reader and imports, ran 2026-10-13, returned F24/F26/F49 L4.5 to 5 and
  closed with H12; it fixed three runtime bugs — see WU-ParityAndRemaining's DONE entry.)

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

## WU-D21Confidentiality — a notification never names or links a story outside the public window or taken down unless its recipient is the story's author: the story-family enricher kinds split by recipient, the D8 way (tracker D21; extends `Notifications/`, with a comment and a guard test in `Groups/`) — DONE ✓ (2026-10-14)

- **Cells:** **F42 L4.5 5→1** (no browser in this pass; tracker **H43**). Beneath Stage-5 cells, no flip:
  F42 L2 (the enricher), F39 L2 (a comment and a guard test). No migration.
- **Trigger:** tracker **D21** (decided by the settled Class-A rule; fix shape and the D8 precedent in
  the item) via the wrap-up amendment. No spec file: self-scoped in `wu-specs/WU-D21Confidentiality.md`.
  Not in scope: decision row 31 / tracker **F28** (owner-open); `AddStoryAsync`'s accept/refuse is unchanged.
- **What landed** (two commits: `1db5b5e` moment-1 docs, then code, tests and moment-3 docs):
  1. **Rule (moment 1).** `layer2-services.md` §"Polymorphic RelatedEntityId" → "Story-family kinds by
     recipient" (the rule, the type table, the chapter clause, rendering, how to classify a new type);
     `audit/Notifications.md` settled note beside D8's.
  2. **Enumeration.** Three kinds read a story title unfiltered (`StoryListings`): `Story` (19 types, 6
     B24 stubs), `Chapter` (10, 24), `GroupStory` (60, 25), 23 types. Each type's recipient traced to its producer.
  3. **`NotificationEnricher`.** `OwnStory`/`OwnChapter`/`OwnGroupStory` for the author-recipient types
     (9 `Story` types, 24, 25), resolved as stored; `Story`/`Chapter`/`GroupStory` for the rest, over
     `Stories` with `ContentRating` and `StoryStatus` lifted by name and the window restated without the
     viewer clause (recipient-agnostic, so the email composer shares it), `IsTakenDown` active; `Chapter`
     also requires a published chapter. One loader per pair over two story sources. Rendering follows D8:
     a miss, so the title-free arm and no link; 60 keeps the group's name and link. Closes two more
     mint-time leaks of the same root: a lineage request from a draft source (50), a credit on a draft (52).
  4. **Copy.** Type 52's title-free arm said "acknowledged your story" to the credited user; now
     "acknowledged your help on a story".
  5. **Comment.** `AddStoryAsync`'s D21 sentence re-tensed (row 31 still open).
- **Filed:** **D22** (the credit and lineage manage lists show a counterpart an unpublished story's
  title; split by the review fixes below), **H43** (browser pass). Ticked **D21**; F28 and roadmap row 31
  re-tensed, still open.
- **Left alone:** row 31 / F28, including *when* members hear of an author's own draft (the 60 still
  fires at add time, now title-free); D22's and F29's lists; the `Own*` kinds keep a taken-down story's
  title for its author (unlike `OwnBlogPost`, which has always kept the removal filter; the rule's
  "unless the recipient is the author" is the authority), but not its link (review fixes).
- **Verification:** build 0 errors, no new warnings in touched files; **Unit 1,377 (+1), RazorComponents
  929, Integration 1,687 (+21)**, all green; check-doc-hygiene, check-design-tokens, check-render-modes
  and check-a11y pass. Integration `StoryNotificationConfidentialityTests` (17) and
  `GroupServiceTests.AddStory_AnotherUsersUnpublishedOrTakenDownStory_IsRefusedAsNotFound` (4). With the
  enricher reverted, 13 of the 17 fail (the three visible-story controls and the classification check
  pass); mutation-checked separately: 25 and 24 moved to their filtered kinds, 75 moved to `Story`, the
  chapter clause removed, `IsTakenDown` lifted, an unlisted type mapped to `Story` — each fails. Unit
  `NotificationPresenterTests` (52 with and without a title). **No browser in this pass:** the bell and
  inbox rendering on both phases is tracker **H43** (F42 L4.5 is 1 until it runs).
- **Review fixes (2026-10-14; three reviewers, twelve findings: eleven real, eight distinct, all
  verified against the code):**
  - *Behavior:* the `Own*` kinds linked a page that 404s for the author too (the story and chapter
    pages keep `IsTakenDown` for everyone; the chapter page serves only a published chapter). `OwnStory`
    and `OwnChapter` keep the title and drop the link for a taken-down story, `OwnChapter` also for an
    unpublished chapter. Editor links stay decision row 27 (e)'s. Integration: the Story-kind theory's
    takedown case and the chapter theory's takedown and unpublished-chapter cases, which fail with the
    link restored (3 of 17).
  - *Ledgers:* **D22** split the way D21 was: its product question (may a credit or lineage request
    name an unpublished story; what the counterpart sees while it is pending) is new **F29**, promoted
    to `roadmap.md` decision **row 32**; D22 keeps the decided half (the outgoing lineage list's hidden
    target). F28 and row 31 name both tests that lean on the add-time 60 (probe, email; they move under
    "no" and under "yes, at publish time"); F29 names the lineage and credit draft tests. **H43** step 4
    gains the author's takedown case.
  - *Docs and comments:* the enumeration count (23 types, 6 stubs; was 31 and 4) here, in the Position
    block and in F42's Stage note; F41's Stages and L2 headline pointers; the D16 settled notes in
    `audit/Groups.md` and `audit/Notifications.md` annotated with the split; `layer2-services.md`'s
    Group Rating Waterfall clause re-tensed and its "Story-family kinds" section given the link rule;
    the 60/25 producer comments (`ServerNotificationWriteService`, `INotificationWriteService`); the four
    status-quo tests' comments.
  - *Rejected:* type 52's "acknowledged your help on a story" was said to be false for the
    "Inspiration" role, but `WU_StatBadgeProducers` deleted that role; the four live roles (Beta
    Reader, Planner, Cover Artist, Editor) are all help.
  - Tests unchanged in count: Unit 1,377, RazorComponents 929, Integration 1,687, all green; the four
    gates pass. No browser: H43.
- **Pointers:** Stage notes in `audit/Notifications.md` F42 and `audit/Groups.md` F39; tracker D21, D22,
  F28, F29, H43; `roadmap.md` decision rows 31 and 32.

## WU-DocCorrections — the campaign's doc backstop: service audit §4's stale doctrine and false comments corrected against the code, the worksheet registered, the CORRECTION-note convention stated, every pre-split `cross-cutting.md` citation re-pointed (worksheet D12's doc half verified; service audit §4 and §7; extends no cluster — docs and comments, plus two Integration tests; its review fixes added the /fanon hub's reach fix and a third) — DONE ✓ (2026-10-13; its fresh-eyes probe gate is open, H42)

- **Cells:** none moved. Docs and comments beneath Stage-5 cells: F39 L2, F33 L2, F11/F12 L2, F15 L5,
  F10 L2, F50 L2, F24 L2, F41 L2. No migration (`has-pending-model-changes`: none). Dated 2026-10-13, the
  session's `currentDate` when it started; its last commit landed after midnight.
- **Trigger:** spec `wu-specs/WU-DocCorrections.md` (worksheet D12, service audit §4/§7, the CLAUDE.md
  registration rider) and its amendment (C1/C4 may be done; re-derive E2's guard list; run the probes).
- **Closed by earlier WUs, verified and not re-edited:** all of §A (D12's convention homes, settled note,
  `grid_axes`/`folder_clusters`/Identity notes, the hygiene registry and its Check-1 fix, the evidence
  rows, roadmap Resolved, the code pointers, the copy) — WU-TptHardDelete, except A10's optional test
  summary, which this WU added to `DeleteComment_ReparentsReplies_ToTopLevel`; A12 holds (`ParentCommentId`
  → `SetNull` in the snapshot); A13 — WU-ParityAndRemaining (R1); B2's code twins — WU-InertFeatures;
  C1 — WU-ThrottleCoverage; C4 — WU-AccessGateSweep2; G1 — WU-CounterSymmetry; G2/G3 and G5's Fanon
  comment — WU-ParityAndRemaining; G4 — WU-NotificationCorrectness; G5's folder comment and G6 —
  WU-SchemaHardening; G5's blog-delete comments — WU-TptHardDelete; G5's moderation-client comment —
  WU-ModerationIntegrity. F4: `workplan.md` was 2,836 lines, over the sweep trigger, but every DONE entry
  dates 2026-09-30 or later (under two weeks), so nothing was eligible to move.
- **What landed** (three commits: `91d0407` moment-1 + doctrine docs, `3c164ce` comments + citation sweep,
  then the tests, the D21 comment and the moment-3 docs):
  1. **Registration (F1/F2).** `CLAUDE.md`'s design/ row names `audit-decision-worksheet.md`;
     `roadmap.md`'s decision table says the worksheet's unticked rows (D20's bounds half, D25–D47) are
     open owner decisions that live there.
  2. **Convention (F3).** `SKILL.md` §"Comments That Assert a Control": verify a control claim when
     writing it, correct a load-bearing false one with a `CORRECTION (WU-X):` note, describe current
     code only, cite docs by current file + section.
  3. **`layer2-services.md` (B1–B5, E1).** Tier 1 of the Group Rating Waterfall is display-side (false
     since WU38); the Notification Generation sample re-synced (it had the recipient and source swapped)
     and the fan-out list re-derived; Moderation's DAG paragraph matches its ctor; the write→write edges
     are a closed list of three kinds with the grep that re-checks it; both WU23 sketches bannered;
     badge curation recorded as the third self-referential variant. `testing.md` and
     `identity-and-authorization.md` corrected; `Groups.md`'s WU32 settled note gets a dated correction.
  4. **Comments (B6, C2, C3, C5–C11).** Two `CORRECTION` notes on the manual tree search's 2..7 status
     window (its class doc and `VisibleStories`; the `StoryStatus` filter exempts the author, so the window
     is load-bearing; C11's "filters skip navigations" checked against the EF Core docs and corrected);
     Fanon's class doc and `GroupQuery` rewritten plainly to say the same (their text made no false
     claim); the lineage reject doc,
     sprite-resolution claims, the write-context-filter claims, `StoryFilterDto`'s fallback, the username
     lookup, the saved-selection auth claims, nine stale-tense sites, theme order, the badge award callers.
  5. **Citation sweep (D).** Every `cross-cutting.md` citation in code, tests and `draft-autosave.js`
     that named a section moved out on 2026-07-07 now names its home; the six live-section citations stay.
  6. **Tests.** Integration `ManualTreeSearchTests.UserPivot_AuthorExploringThemselves_TheirUnpublishedStoriesStayOut`
     and `FanonPipelineTests.GetGroupsAsync_TheViewersOwnDraft_DoesNotFeedPublicReach` pin the two
     windows the CORRECTION notes call load-bearing; neither had a test (the existing Fanon draft test
     cannot see the viewer's own draft). Both fail with the window removed (mutation-checked).
  7. **Found while verifying, not fixed:** tracker **D21** — `AddStoryAsync`'s confidentiality guard
     exempts the story's author, so an author can file their own Draft into a group and members' type-60
     notifications name it (confirmed by a throwaway Integration probe). The `AddStoryAsync` comment, the
     waterfall section and `Groups.md` say so. (Split by the review fixes: the title leak is decided by
     the settled Class-A rule and stays on D21; the product call is **F28**, decision row 31.)
- **Filed:** **H41** (the pattern-uniformity residue, re-derived: inline auth checks, two private guard
  copies, three owner gates answering an anonymous caller 403 (two `GetMy*` reads wait on worksheet D27), mixed read-return types, `UserDeletionService`'s interface (D36),
  `IContentImportService`'s lifetime (D36/D37)); **D21**; **H42** (fresh-eyes probes). Tracker H6's
  drifted `layer2-services.md:1022` citation now names its section. Evidence annotations: service audit
  §4 rows 2/4/8/9/10, the false-comment and uniformity paragraphs, §7.
- **Left alone:** the code halves of §4's pattern-uniformity items (H41; not assigned to a WU);
  `IContentImportService` and any registration-shape change (D36/D37 pending); a status precondition on
  `RejectLineageAsync` (a lifecycle-guard choice; the doc only describes the code); log-level drift on
  best-effort swallows (behavior); the spec file. No Identity.md note: A7's copy was rewritten by
  WU-TptHardDelete and again by WU-UserDeletion, so F52 is untouched.
- **Doc-audit probe coverage — the gate is still open.** The amendments and service audit §7 require
  the doc-audit skill's fresh-eyes probes as part of this WU; neither pass had a subagent tool, so they
  are tracker **H42**, which the campaign's wrap-up waits on. Mechanical substitutes run 2026-10-13 by
  the authoring pass: every `file.md` §"Section" reference in the conventions, `CLAUDE.md`,
  `roadmap.md`, `status.md`, the tracker and `audit/*.md` resolved against its target; every feature's
  L2/L4/L4.5 audit headline matches `status.md`'s grid; line-number citations into the edited docs
  checked (one fixed, H6); `check-doc-hygiene.ps1` clean. Added 2026-10-14 by the review fixes: a scan of
  every `audit/*.md` feature section for a campaign WU's Stage note its headline does not name (it
  confirmed the reviewers' five and found `Moderation.md` F47, all fixed; H42 has the list).
- **Review fixes (2026-10-14; three reviewers, sixteen findings, all verified against the code):**
  - *Behavior:* `ServerFanonReadService.GetEstablishedFanonTagsAsync`, the public /fanon hub's
    "N stories", counted the bare `StoryTags`/`StoryCharacters` collections (no query filter), so
    drafts, pending, rejected and taken-down stories counted; it now uses the dashboard's rule (through
    the `Story` navigation, 2..7 window, `ContentRating` bypassed). Integration
    `FanonPipelineTests.GetEstablishedFanonTagsAsync_ReachCountsOnlyPublishedLiveStories_OfEveryRating`,
    failing on the old count. The rewritten Fanon class doc had claimed this for every query.
  - *Comments:* `StoryListingDto` (the Personal plane lifts the rating filter), `IActiveUserContext`
    (SharedUI's two ratified injectors), `StoryCard`/`StoryPage`/`StoryDetailTests` ("sprite-resolved"),
    `Program.cs`'s WU22 future tense, `AddStoryAsync`'s D21 pointer.
  - *Docs:* six headline lines (`Tags.md` F11/F12/F15, `Stories.md` F10, `Comments.md` F23,
    `Moderation.md` F47); `Discovery.md` F33's WU40 navigation claim marked corrected;
    `layer2-services.md`'s write→write grep now finds its own third edge (`usiWrite`);
    `identity-and-authorization.md`'s H41 and SharedUI sentences; service audit §4's Fanon wording and
    §7's "Built" (the probe gate is open); the Position block's session-relative phrase.
  - *Ledgers:* H41's two `GetMy*` adoption reads moved under worksheet **D27** (they had been filed as
    401 candidates, which pre-decided it); **D21** re-filed as a decided confidentiality defect
    (`high`; the enricher's `Story`/`Chapter` kinds share the root, by reading) and its product half
    promoted to **F28** / decision row 31; **H42** raised to `med · beta` as this WU's open gate.
  - *Rejected:* none. Not done here: the D21 fix itself (a cross-kind enricher change that the next fix
    WU scopes, enumerating every Story-family kind first) and the probes (H42, no subagent tool).
- **Verification:** build 0 errors, no new warnings in touched files; **Unit 1,376, RazorComponents 929,
  Integration 1,666** (+2 by the WU, +1 by its review fixes), all green; check-doc-hygiene,
  check-design-tokens, check-render-modes and check-a11y pass. The build pass had no browser, and none
  was owed: no markup changed, and the one behavior change is a server-computed count pinned at the
  Integration tier. **Browser pass (2026-10-14, server-only path, psql ground truth; no bug found, no
  code change):** with a `psql` fixture of two fanon tags across published (E/T/M), draft, pending,
  rejected and taken-down stories, the /fanon hub counted only the published, live ones (2 and 3,
  against 7 and 6 junction rows) for TestUser, AuthorAlpha (the draft's author), ReaderGamma (mature
  off; M still counts), anonymous and AdminUser, on the circuit and on WASM (`/api/fanon/established`);
  reinstating the taken-down story raised both by one. The two pinned windows held too: AuthorAlpha's own
  draft stays out of the "saura" dashboard group and out of their own Explore "Authored (4)", both
  render phases. No `fail:`/`crit:` lines, no console errors. Detail: `audit/Tags.md` and
  `audit/Discovery.md` F33 notes. **Dev DB:** the fixture (tag-47 and OC rows, a fanon Setting tag,
  two story status/takedown flips) was removed and restored afterwards; the dev DB is as found, apart
  from the `tags` and `story_characters` identity sequences having advanced.
- **Pointers:** Stage notes in `audit/Groups.md` F39, `audit/Discovery.md` F33, `audit/Tags.md`
  (F11/F12/F15), `audit/Stories.md` F10, `audit/Badges.md` F50, `audit/Comments.md` F23/F24,
  `audit/Notifications.md` F41; tracker D21, F28, H41, H42; `roadmap.md` decision row 31.

## WU-TrackerFixes — the tracker sweep: page size follows the reader setting (B16), a report entry point for every target and a participant-only message report (B17), blog-post notifications by recipient (D8), the test-hygiene backlog (H7), `ErrorAlert` on the SOLO surfaces (amendment U11), the last dead profile links (extends `Discovery/`, `Bookshelves/`, `Profiles/`, `Moderation/`, `BlogPosts/`, `Recommendations/`, `Messaging/`, `Groups/`, `Notifications/`, `Errors/`, `Stories/`, `Chapters/`, the Integration and RazorComponents suites) — DONE ✓ (2026-10-13)

- **Cells:** **F4, F6, F17, F21, F28, F31, F35, F36, F37, F40, F42, F46, F47, F48, F49 L4.5 5→1** (visible
  behavior changed with no browser available; tracker **H38**), **and 1→5 at the browser pass the same
  day** (below). Built beneath Stage-5 cells: F31/F17/F21
  L3-Logic, F46 L2 (the participant rule), F35/F36 L2 (`AuthorId`), F42 L2 (D8), F47 L2 (the link). No
  migration. Dated 2026-10-13, the session's `currentDate`, as the WUs before it.
- **Trigger:** spec `wu-specs/WU-TrackerFixes.md` and amendments U10/U11. §1 C / E2 was done by
  WU-DependencyCVEs; §1 E's three razor links by the WU-AccessGateSweep2 browser pass; B18 by
  WU-ModerationIntegrity. Tracker D8 was routed here by the WU-NotificationCorrectness review fixes.
- **What landed** (five commits: moment-1 docs `058e30b`, H7 `c4f3ef8`, B16/B17/D8/links `21eac93`,
  U11 `6e8d4a1`, moment-3 docs and the site-announcement boundary):
  1. **B16.** `ResultsFilterPanel` echoes `InitialFilter.PageSize`, copied ahead of its interaction
     guard; `SearchPage`, `BookshelvesPage` and `ProfilePage`'s story tabs seed it from the signed-in
     viewer's `DefaultPaginationSize` before the first load (anonymous viewers keep 20, no settings
     call). `TreeSearchPage` needed nothing. No new parameter, no server cap (D20 bounds pending).
  2. **B17.** Report on `BlogPostCard` (profile, group — `BlogPostListingDto.AuthorId` hides it on own
     posts), `BlogPostPage` ("Report post"), `RecommendationCard` (via the section's own dialog) and
     `MessageItem` (via `MessageThread` → `MessagesPage`). **Behavior change:** a `Message` report needs
     the reporter to be a participant (kind (g)); message ids are sequential, so before this any user
     could queue a stranger's message.
  3. **D8, the type split.** 33 → new `OwnBlogPost` kind (as stored); `BlogPostDirect` (13–16, 100) drops
     an unpublished post and a Private author's profile post; `PollUpdated` names its post again.
  4. **H7.** 24 bUnit tests folded (every assertion kept; the 25th no longer existed); the
     `SiteAnnouncementServiceTests` `Task.Delay` replaced by pinned stamps; `SeedUserStatAsync` hoisted to
     `IntegrationTestBase` (nine copies removed or wrapped).
  5. **U11.** `EditorFormViewModel` (`SharedUI/Errors/`) under the four form view models; the forms,
     `PollEditorForm` and `ReportDialog` render `ErrorAlert`; `GroupPage`'s eight toasts became three
     inline alerts that log unexpected failures. The non-reset flag fixed in `ModReportsPage`,
     `ModSubmissionsPage` (×3) and `AccountActionPanel`.
  6. **Links.** The mod queue's User target and `/mod/users/{id}`'s View profile used the username;
     the route is `/user/{UserId:int}`.
  7. **Docs.** Moment 1: `layer3.5-structure.md` (page-size seed), `layer2-services.md` (report entry
     points, the participant rule, three blog-post kinds), `error-handling.md` (the adoption rules; its
     false `<ErrorAlert Error=…>` line corrected), Settled notes in Discovery F31, Moderation F46,
     Notifications; tracker corrections (B19's stray bullets to B13, H7's stale phases and flake note,
     H6's spoiler bullet struck, H8 re-verified, also in `deferred-work.md`). Moment 2: `testing.md`
     (the shared `SeedUserStatAsync`; ordering tests pin stamps). Moment 3: the Stage notes and headlines
     below, `status.md`, tracker, roadmap.
- **Left alone:** U10 (optional): `aria-modal` ahead of the focus trap contradicts the settled
  `audit/Accessibility.md` rule, and the combobox roles belong with F6b's screen-reader run (annotated
  there). The Explore/DeepDive/Spotlight rec cards and site announcements stay unwired (scope boundary;
  the second is decision row 30 / **F27**, owner-open). C2–C6 (WU-L6MeasurePass), the needs-owner items
  (A1, A7, A9, B15, B19, D4, H6, H11) and the browser/manual items (F6b, H2, H4, H9) per the spec's §2.
- **Verification:** build 0 errors, no new warnings in touched files; **Unit 1,375** (the PollUpdated
  presenter test rewritten), **RazorComponents 898** (874 → 863 by the merges, +35), **Integration
  1,660** (+8). Every new test was mutation-checked against a targeted revert (listed in the commit
  bodies). check-doc-hygiene, check-design-tokens, check-render-modes and check-a11y pass. No browser
  was available: tracker **H38** holds eight steps. **Browser pass (2026-10-13):** all eight driven, the
  fifteen cells back to 5, six runtime bugs fixed (below); after its fixes Unit 1,376, RazorComponents
  929, Integration 1,663.
- **Review fixes (2026-10-13).** Three reviews raised 13 findings, 9 distinct after merging duplicates;
  all were real, and none needed an owner ruling.
  1. **`layer4-style.md` contradicted the WU's own rule.** It still sent action failures to a toast, with
     `GroupPage` as its example. It now names the inline `ErrorAlert`. Five refusal toasts outside U11's
     eight surfaces are listed in `error-handling.md` and filed as tracker **E12** (a build, not a ruling).
  2. **A stale cookie took down the public profile page.** On WASM, a viewer whose cookie expired after
     prerender still reads as signed in, and the WU's new settings read answered 401 →
     `SessionExpiredException` → the page's error boundary. The page-size reads (`ProfilePage`,
     `BookshelvesPage`, `SearchPage`'s pre-existing one) and the saved-selection flyout's sort read (the
     same exposure, found by the new tests) are best-effort: Warning, defaults. Rule in
     `layer3.5-structure.md`'s page-size seed.
  3. **The viewer's own group blog card had an empty bordered footer.** `BlogPostCard`'s row guard is now
     its children's guards.
  4. **Tests the WU lacked:** the Sign in binding on the chapter, blog-post and site-announcement forms;
     the poll flag on `PollsPage` and `BlogPostEditorPage`; the chapter editor's clear-before-try
     (Publish); the restore's page size; the account and link reject resets; `GroupPage`'s file and
     unfile refusals; the filed entity type on all five Report hosts (`FakeReportSubmissionService` now
     records requests). New test helpers `ScriptedProxy` and `ReportDialogDriver`.
  5. **Doc drift:** Groups F39's toast line, Moderation F47/F48's headline prefixes, H7's tense,
     Notifications' type-100 line.

  Each new test fails with its fix reverted (21 mutants in two batches). **RazorComponents 916** (+18);
  Unit 1,375 and Integration 1,660 unchanged; build 0 errors, no new warnings in touched files; the four
  gates pass. No stage moved. H38 steps 1 and 2 gained the expired-session and footer checks. Review-fix
  notes in each WU-TrackerFixes Stage note (Chapters F6, UserStoryInteractions F17, Profiles F21,
  Recommendations F28, Discovery F31, BlogPosts F35/F36/F37, Groups F40, Moderation F46/F48, Messaging
  F49) with its headline, a new Stage note at Tags F15; annotations at Groups F39, Moderation F47's
  headline and Notifications' type-100 line.
- **Browser pass (2026-10-13). F4, F6, F17, F21, F28, F31, F35, F36, F37, F40, F42, F46, F47, F48, F49
  L4.5 1→5; H38 closed; six runtime bugs fixed.**
  - **Setup.** Server-only path on the standing workbench (no migration in this WU, so not reset), `psql`
    after every write. Circuit proven by `_blazor/negotiate` and no `/api` call (Auto hash cleared before
    each full load), WASM by the action's `/api` request. Actors: TestUser, AuthorAlpha, ReaderGamma,
    AdminUser, anonymous. An expired session was made with a `fetch` logout under the running WASM app
    (it exists only on WASM). Page size used a `psql` fixture of 3, the seed being about ten stories.
  - **GIF:** `e2e-WU-TrackerFixes.gif` (WASM: ReaderGamma's "Report post" on `/blog/4`, filed).
  - **Bugs fixed** (commit "browser-pass fixes"): (1) `POST /api/stories/query` bound an absent
    restrict set as an empty array, which the service reads as "narrow to nothing", so every unrestricted
    WASM listing was empty (`/discover`'s Date-published sort showed no stories): the endpoint maps empty
    to null and the client answers an empty set itself; (2) a restored sorted `/discover` page lost its
    pager (`TotalCount` now persisted beside `Items`); (3) `CommentSection` and three `StoryDeck` hosts
    offered Report to anonymous viewers (a WASM crash, an anonymous circuit filing); (4) `StoryDeck`
    forwarded `EventCallback.Empty`, whose delegate made every unwired card offer a dead Report; (5)
    `ReportDialog`'s first open crashed on an expired WASM session (the reasons read); (6) a stale WASM
    session still took the profile's Stories tab down through the interaction-mark read (and would
    have `/discover`). Tests +17: Unit +1, RazorComponents +13, Integration +3; 12 mutants in four
    batches, each failing its test (the fix commit's body says 13: a miscount). Conventions:
    `layer5-wasm.md` (an absent array key binds empty), `layer3.5-structure.md`, `layer2-services.md`,
    `error-handling.md`, `layer3-logic.md`, `testing.md` (the HTTP-binding blind spot; restore mutants with
    a fresh timestamp), `run-server/SKILL.md`.
  - **What it saw:** the steps' results are in the audit Stage notes (F4, F5, F6, F17, F21, F24, F28,
    F31, F35–F37, F40, F42, F46–F49, and Tags F15). Also driven: the duplicate-report refusal on both
    phases, a non-participant's message report (404, like an absent id), a refused group file and
    unfile, the external-account reject on a fixture.
  - **Not covered:** the link tier of step 7 (no pending link verification; bUnit covers it); keyboard
    and screen-reader use (**F6b**). Filed: **E13** (the interaction-mark read on twelve other listing
    call sites), **H39** (Report on the viewer's own story and user card), **H40** (a refused file keeps
    its folder choice; the Sign in line sits flush against the editor forms).
  - **Logs.** No `fail:`/`crit:` in the server log; the browser console showed Warnings only (the
    best-effort reads in a stale session) once the fixes were in.
  - **Dev-DB state left (server-only).**
    - TestUser's `DefaultPaginationSize`: 50 and 3 during the pass, 20 again (as before).
    - AuthorAlpha's profile went Private through the form and back to Public through `psql`.
    - New: blog post 4 "E2E D8: followers see this title" (AuthorAlpha, published) and its type-13
      notifications 40052–40054; poll 2's description "E2E D8: edited by AuthorAlpha", its
      `last_edited_at` aged 31 minutes through `psql`, `edit_notified_at` stamped, notification 40055;
      site announcement 5 (AdminUser), published and then unpublished.
    - Reports 14–17 and 19–23 open (receipts 40048–40051, 40057–40061; `active_report_count` up on posts
      1, 2 and 4); report 18 (anonymous, filed by bug 3) closed No action by AdminUser.
    - TestUser's verification code and `user_external_identities` row 1 (AO3, Rejected by AdminUser;
      notification 40056).
    - Notifications 40047 and 40052 read by their clicks.
- **Pointers:** Stage notes at the top of Stories F4, Chapters F6, UserStoryInteractions F17,
  Profiles F21, Recommendations F28, Discovery F31, BlogPosts F35/F36/F37, Groups F40, Notifications F42,
  Moderation F46/F47/F48, Messaging F49, Tags F15 (review fixes); the browser-pass notes in those and in
  Stories F5 and Comments F24; tracker B16, B17, D8, H7, F27, H38, F6b, E12, and from the browser pass
  E13, H39, H40; roadmap row 30.

## WU-ParityAndRemaining — the InteractiveAuto/WASM parity breaks (validation error lists over the wire, the client's sprite root, the re-split's camelCase read, the anonymous reader's durable marks, the Explore pivot's message) and the remaining confirmed service defects (reply depth, atomic group and chapter creates, tag trust, the edited marker, archive and encoding imports, three filter/fabrication fixes) (service audit §2.7 and §2.12; worksheet D12's "Left open"; extends `Client/Http/`, `Comments/`, `Groups/`, `Chapters/`, `Stories/`, `Tags/`, `Import/`, `Recommendations/`, `Messaging/`, `Sprites/`, `Discovery/`, `Data/`) — DONE ✓ (2026-10-13)

- **Cells:** **F24, F26 and F49 L4.5 5→1** (the "(edited)" marker, the edit spoiler toggle, the
  deleted-participant rendering; no browser was available, tracker **H37**), and **1→5** by its browser
  pass the same day. Built beneath Stage-5
  cells: F23/F24/F26 L1–L3.5, F38 L2, F6 L2, F7 L3-Logic, F12 L2, F4 L2, F63 L2/L5, F28 L2, F49 L2/L3.5,
  F3 L5, F33 L5; the browser pass's fixes beneath F63 L3-Logic/L5, F3 (the sprite chain's script) and the required rich-text fields'
  L2 (F23, F35, F6, F49, F4). Dated 2026-10-13, the session's `currentDate`, as the WUs before it.
- **Trigger:** spec `wu-specs/WU-ParityAndRemaining.md` and its amendment (R12 builds on
  WU-UserDeletion's label; X6 is owner-deferred). D36/D37 pending, so no registration-shape change.
- **What landed:**
  1. **P1 — validation lists cross the hop.** `EndpointHelpers` adds an `errors` array for every
     `CanalaveValidationException`; `ClientHttpHelpers.ThrowIfWriteFailedAsync`'s factory is now
     `Func<IReadOnlyList<string>, Exception>` (tolerant read, `[detail]` fallback). The signature
     change swept every client factory through the compiler (41 lambdas in 33 files, plus P5's new one).
  2. **P2 — `GET /api/client-config`.** One server read of `Sprites:BaseUrl` into a `ClientConfigDto`
     singleton; the WASM host loads it into `ClientRuntimeConfig` before `RunAsync` (Warning +
     default on failure); the client `ISpriteReadService` is a lazy factory over it.
  3. **P3/P4/P5:** the re-split reads through new sync helpers (web options); `OnScrollProgress` calls
     `MarkStarted`/`MarkCompleted` only for a signed-in reader; the Explore user pivot rebuilds
     `StoryValidationException`.
  4. **R1/R5 — comments.** `RequireTopLevelParentAsync` in all four post methods; migration
     `WU_ParityAndRemaining_CommentDateEdited` (`base_comments.date_edited`), stamped only on a real
     change; `UpdateCommentDto.IsSpoiler` (`bool?`, chapter-only); `CommentDto.DateEdited`; the
     "(edited)" marker and the chapter-root edit toggle.
  5. **R2/R3 — atomic creates.** `CreateGroupAsync` adds the admin through the navigation (one save) and
     refuses a taken name on create and rename (pre-check + index-named backstop, rows detached);
     `CreateChapterAsync` runs in one execution-strategy transaction and clears the tracker on failure.
  6. **R4/R6/R7 — tags.** The tag gate loads `TagId → TagTypeId` first (missing, mistyped, Character in
     the flat list, non-Character as a character); a doubled pairing member is refused; fanon invites
     skip taken-down stories, and the four no-op `IgnoreQueryFilters` on the write context are gone.
  7. **R8/R9 — imports.** `ArchiveGuard` (entry cap 10 000, real decompressed-byte cap 100 MB) on the
     EPUB and DOCX paths; `ImportTextDecoder` (BOM → strict UTF-8 → Windows-1252 + `EncodingFallback`).
  8. **R11/R12:** the rec author probe lifts `ContentRating`; `OtherParticipant` is null for a deleted
     account (no fabricated id 0), rendered by the two leaves.
  9. **Docs:** moment 1 — `layer5-wasm.md` (400 row, factory shape, the sync-JSON and caller-gate rules,
     §"Server configuration the client needs"), `error-handling.md`, `layer2-services.md` (sprite seam,
     write context has no filters, comment rules, group names, legality rows, text decoding),
     `render-and-layout.md`, `security.md` §"Archive imports", audit settled notes (Comments F23,
     Import), roadmap **row 29** / tracker **F26**, the worksheet D12 Built line. Moment 3 — Stage notes
     and headlines (Comments F23/F24/F26, Groups F38, Chapters F6/F7, Tags F12, Stories F4, Import F63,
     Recommendations F28, Messaging F49, Sprites F3, Discovery F33), service audit §2.7/§2.12/§4
     annotations, `status.md` (three L4.5 cells, the API-envelope constraint), tracker **H37** filed,
     **H12** folded into it, **D11** annotated.
- **Deviations from the spec, each with its reason:** commits are one, not one per phase (each commit
  must pass the full suite; the phases share files). X10 is roadmap row 29 / tracker F26, not a new
  worksheet D48 (the campaign records owner-open items in the tracker and the decisions table, and
  the worksheet holds the owner's answers). R2 adds a race backstop the spec did not name (D23: a
  visible 500 is never a posture). R10 was already built by WU-TptHardDelete (verified, with its test).
  R12's spec test (delete a participant row by hand) is the WU-UserDeletion deletion test, updated: it
  drives the real cascade.
- **Left alone:** X1 (D36), X2 (D37), X4 (D42), X13 (D29), X16 (D20 bounds), X18 (D31), X19 (D27) —
  pending rows; X6 (site-poll `RelatedEntityId = 0`, D16's re-point backlog, tracker **B23**, owner
  "not urgent"); X10 (row 29 / **F26**); X21 (an inline pivot message is new UX). Found while building,
  recorded on **D11**: concurrent chapter creates still race on the chapter number (a 500).
- **Verification:** build 0 errors, no new warnings in touched files; **Unit 1,342** (+34:
  `ValidationErrorsWireContractTests` 11, `WasmParityClientTests` 9, `ImportArchiveAndEncodingTests`
  14), **RazorComponents 869** (+7: ChapterReadingPage +1 and one rewritten, CommentItem +1,
  CommentSection +3, ConversationListItem +1, MessageThread +1), **Integration 1,649** (+35:
  `CommentThreadingAndEditTests` 14, `GroupCreateIntegrityTests` 7, `ChapterCreateAtomicityTests` 1,
  `StoryTaggingTests` +6, `FanonPipelineTests` +1, `RecommendationReadServiceTests` +1,
  `ApiErrorEnvelopeTests` +5; `UserDeletionServiceTests` updated), all green. Mutation-checked in one
  batch of 16 mutants, every one failing its test: the `errors` extension, the re-split options, the
  reader gate, the pivot translation, the depth check, the no-op stamp, the group backstop, the chapter
  transaction, the fanon `!IsTakenDown` (corrected by the review fixes: only the Character branch's
  clause had a test, the flat-tag one gained its own there), the stored-type check, the doubled
  member, the rec lift, the EPUB guard, a reader's warning, the fabricated participant. Migration applied, rolled back and
  re-applied on the server-only dev DB. Gates: doc-hygiene, design-tokens, render-modes, a11y pass.
  **No browser was available** at the build: tracker **H37** (eight steps, H12's re-check first), driven
  by the browser pass below the same day, which closed H37 and H12.
- **Review fixes (2026-10-13).** Three reviews raised 12 findings, 11 distinct after merging duplicates;
  all were real and none needed an owner ruling.
  1. **The tag gate cached tag types on the circuit.** Its `ToDictionaryAsync` ran its selectors in
     memory, so it tracked whole `Tag` rows on the circuit-scoped write context; after an admin re-typed
     a tag, the same circuit judged the next save on the old type. It projects `{ TagId, TagTypeId }`
     first. Rule added to `layer2-services.md` §"Read-Context Concurrency: Factory Per Method" ("A gate
     that only reads another entity's values projects them").
  2. **The sprite cutover still missed `unknown.png`.** Three leaves hardcoded it on both phases.
     `ISpriteReadService.GetUnknownSpriteUrl` now builds it from the configured root; the seam
     paragraphs in `layer2-services.md`, `render-and-layout.md` and `layer4-style.md` say so; H37 step 5
     gains the fallback check.
  3. **`layer5-wasm.md` contradicted P5** (and the story listing reads since WU-ErrorHandling2): it said
     non-scalar reads skip `ThrowIfWriteFailedAsync`. §"Reads with non-scalar parameters" now says the
     translator follows the endpoint; the Error-Translation Contract states the exception.
  4. **Tests the WU lacked:** the fanon flat-tag `!IsTakenDown`, the edit-toggle seed on a spoiler root,
     the group rename's race backstop.
  5. **Doc drift:** stale present-tense sentences in Groups F38 and Comments F26; "Rows 7–10" named the
     wrong rows; Import F63's L5 headline; D11's grid line gains F6.

  Each new test fails with its fix reverted (six mutants in one batch). **Unit 1,344** (+2:
  `SpriteReadServiceTests`), **RazorComponents 872** (+3: `CommentSectionTests`, `TagChipTests`,
  `CharacterEntryTests`), **Integration 1,652** (+3: `StoryTaggingTests`, `FanonPipelineTests`,
  `GroupCreateIntegrityTests`), all green; build 0 errors, no new warnings in touched files; the four
  gates pass. No stage moved. Stage notes: `audit/Tags.md` F12, `audit/Sprites.md` F3,
  `audit/Comments.md` F26, `audit/Groups.md` F38, `audit/Discovery.md` F33; headlines in
  `audit/Stories.md` F4 and `audit/Import.md` F63.
- **Browser pass (2026-10-13). F24, F26 and F49 L4.5 1→5; H37 and H12 closed; three runtime bugs
  fixed.**
  - **Setup.** All eight H37 steps on the server-only path, standing workbench (not reset; the WU's
    migration was already applied), `psql` after every write. Circuit proven by `_blazor/negotiate` and
    no `/api` call (Auto hash cleared before each full load), WASM by the action's `/api` request.
    Actors: TestUser, ReaderGamma, AuthorAlpha, ModUser, anonymous.
  - **GIF:** `e2e-WU-ParityAndRemaining.gif` (WASM: the new-story save listing the server's sentences).
  - **Bugs fixed** (commit "browser-pass fixes"): (1) on WASM the re-split crashed the story editor (the
    synchronous client blocked on HTTP and the runtime refuses the wait); `IContentImportService.ResplitAsync`,
    and `StoryChapterImport` awaits it and shows a refusal; (2) a missing sprite's static `.png` was
    re-requested forever and `unknown.png` never reached (`sprite-fallback.js` compared an absolute
    `img.src` with a relative URL); (3) an untouched editor's `<p><br></p>` passed every required
    rich-text validation (an empty comment was posted); `RichTextContent.IsBlank` now backs all
    sixteen. After the fixes: Unit 1,375 (+31 `RichTextBlankTests`), RazorComponents 874 (+2
    `StoryChapterImportTests`), Integration 1,652; the four gates pass. Conventions: `layer5-wasm.md`
    ("A client method never blocks on a task"), `layer2-services.md` §"Three-Tiered Validation",
    `layer4-style.md` (the sprite chain), `testing.md` (what the tiers can't see).
  - **What it saw:** the steps' results are in tracker H37 and the audit notes. Also driven: the
    reply-to-reply and group-spoiler 400s, the taken group name on create and rename (both phases), an
    atomic group and chapter create, a re-typed tag refused and then accepted on the same circuit, and
    a signed-in reader's marks still written. Not browser-driven (no UI path): P5, R4, R7, R8, R11.
  - **Not covered:** a screen reader on "(edited)" and the toggle's keyboard order (**F6b**). H22
    annotated: the optional half of the blank-markup class is unbuilt.
  - **Logs.** No console error; no `fail:`/`crit:` beyond the known antiforgery key-ring line on the
    first request (this browser last ran the Aspire path).
  - **Tooling notes:** `run-server/SKILL.md` corrects the coordinate-frame formula and adds lazy images
    in a hidden tab, stale-page refusals through `psql`, and Quill input.
  - **Dev-DB state left (server-only).**
    - Comments 1 (TestUser), 3 (ReaderGamma), 4 (AuthorAlpha's profile-wall comment) and 5 (TestUser's
      group-wall comment) carry edited text and `date_edited`; comment 1's spoiler flag went on and off
      (false), comment 3's stayed true.
    - New: comment 11 (TestUser's edited reply to comment 1) and comment 12 (TestUser on blog post 2,
      posted empty by bug 3, then edited to text).
    - Story 13 (TestUser, Draft) was saved twice (`last_updated_date` moved) and gained unpublished
      chapter 27 "E2E Parity: atomic chapter" (version 30, notes stored as `<p><br></p>`, H22).
    - Group 7 "E2E Parity: atomic create" (TestUser admin).
    - ModUser's `user_story_interactions` row on story 12 (started and completed), plus reading-progress
      from the reads.
    - Fixtures restored: story 13's `post_approval_status` (2), tag 30's type (Genre), tag 1's
      `sprite_identifier` (NULL).
- **Pointers:** the audit Stage notes above (and their browser-verification bullets in
  `audit/Comments.md`, `Messaging.md`, `Stories.md`, `Import.md`, `Sprites.md`, `Chapters.md`,
  `Groups.md`, `Tags.md`, `BlogPosts.md`); `layer5-wasm.md` §"The Error-Translation Contract",
  §"Server configuration the client needs" and §"Client Service Implementations"; tracker H37 and H12
  (closed), F26, D11, H22, F6b; roadmap row 29.

---

## WU-PrivateMessageEmail — private-message email, Messaging-owned: one email per unread run, its own preference and unsubscribe, on a shared `(kind, id)` outbound pipeline (worksheet D18; service audit §3.18; extends `Messaging/`, `Email/`, `Notifications/`, `Identity/`, `Data/`) — DONE ✓ (2026-10-13)

- **Cells:** **F43 L4.5 5→1** (the settings page's new Private messages section) and **F49 L4.5 5→1**
  (the new email and its unsubscribe pages); no browser or Mailpit was available, tracker **H35** holds
  the pass. Built beneath Stage-5 cells: F49 L1/L2/L5, F41 L2, F43 L3/L3.5/L5. The review fixes moved
  **F20 L4.5 5→1** (the privacy form's pointer line, H35 step 9) and built beneath F47 L2 and F20 L3.5.
  The browser pass the same day returned F20, F43 and F49 L4.5 to 5 (H35 closed) and fixed one runtime
  bug beneath F49 L3-Logic. Dated 2026-10-13, the session's `currentDate`, as the WUs before it.
- **Trigger:** worksheet D18 (answered 2026-08-07) and its spec `wu-specs/WU-PrivateMessageEmail.md`
  (no amendment). WU-NotificationCorrectness's hand-off: keep the composed pairing, the restore rules
  and `DrainAllAsync`; WU-ThrottleCoverage's: enumerate the new surfaces.
- **What landed:**
  1. **Schema** (migration `WU_PrivateMessageEmail`, two columns, nothing else): `User.EmailOnNewMessage`
     (bool-true shape, backfills on) and `ConversationParticipant.LastNotifiedTimestamp` (nullable).
  2. **Shared pipeline in `Server/Email/`:** the notification buffer and worker renamed to
     `OutboundEmailBuffer`/`OutboundEmailWorker` (`git mv`) over `OutboundEmailWork(Kind, Id)`; the old
     flusher split into the domain-free `OutboundEmailFlusher` (per-kind routing, one send, every
     restore rule kept, unroutable kinds dropped with an Error, after-send hooks for taken mails only —
     the processed head of an aborted batch included, `CancellationToken.None`, never a restore) and
     `Notifications/NotificationEmailComposer` (the query, verbatim). `UnsubscribeLandingPage` extracted;
     its settings link fixed (`/settings` → `/notifications/settings`).
  3. **Messaging:** both sends enqueue after commit; `PrivateMessageEmailComposer` (write context; the
     run predicate, archive mutes, preference + confirmed address; stamps after the send);
     `PrivateMessageEmailBodies` (no sender-authored text); `MessagingUnsubscribeTokenService` +
     `MessagingEmailEndpoints` (`/unsubscribe/messages/{token}`); `Get`/`SetMyNewMessageEmailEnabledAsync`
     (set-based, tracked `User` detached), `GET`/`PUT /api/messaging/email-preference`, client calls.
  4. **UI:** the "Private messages" section of `/notifications/settings` (Email only, optimistic, one
     write at a time, inline rollback).
  5. **Ratified in code:** doc comments on `NotificationTypeEnum` and `INotificationWriteService`.
  6. **Docs:** moment 1 — `layer2-services.md` §"Outbound email pipeline" (retitled from "Email fan-out";
     the feature-owned-email rule, the standing rider) and §"Private-Message Email" (new);
     `cross-cutting.md` §"Two unread systems"; `security.md` §"One-click unsubscribe tokens" (new), the
     edge-policy bullet and two enumeration rows; `horizontal-scaling.md` §6 (new); `layer1-data-model.md`;
     `SKILL.md`, `folder_clusters.md`; audit settled notes (Messaging §"D18", Notifications Shared
     Context); roadmap Resolved D18 and **decision row 28**; tracker **F25**; the worksheet Built line;
     the retired `NotificationEmail(Buffer|Worker|Flusher)` names in `check-doc-hygiene.ps1`. Moment 2 —
     `testing.md` (flusher Unit-tier, the re-enqueue shape). Moment 3 — Stage notes + headlines
     (Messaging F49, Notifications F41/F43), **H35** and **H36** filed, `status.md` F43/F49 L4.5.
- **Deviations from the spec, each with its reason:** the after-send hook takes the **taken indices**,
  not a bare callback (WU-NotificationCorrectness's partial sends and deferrals postdate the spec, so
  "after a successful send" had to become "for what the transport took"); the bool default is the
  model's `HasDefaultValue(true).HasSentinel(true)` (layer-1 rule), not a hand-edited migration
  default, so SeedTool's COPY omits the column like `can_auto_approve`; a valid token for a deleted
  account answers the shared 400 page (the notification routes' rule since 2026-10-13), not a success
  page; the N≥2 note is a new `horizontal-scaling.md` §6, not a §3 paragraph; F43's stale "Stage 2"
  line the spec named was already corrected.
- **Left alone:** decision row 28 / tracker F25 (default on, no sender text, archive mutes, the run
  reading, and since the review fixes no account-status gate — built as defaults). Inherited, as D18
  directed the build to decide: the shared 30 s drain.
  Exactly-once at N≥2 (claim-before-send) not built (`horizontal-scaling.md` §6). The category
  toggles' missing catch (**H17**; filed as H36, folded by the review fixes). No throttle on the
  preference (self-scoped setting; D20 bounds pending).
- **Verification:** build 0 errors, no new warnings; **Unit 1,304** (+41: `OutboundEmailFlusherTests`
  9, `OutboundEmailBufferTests` 8, `MessagingUnsubscribeTokenServiceTests` 10,
  `PrivateMessageEmailBodiesTests` 8, `ClientMessagingEmailPreferenceTests` 6), **RazorComponents
  859** (+7:
  `NotificationSettingsPageTests`), **Integration 1,610** (+20: `PrivateMessageEmailTests`), all green;
  the existing `NotificationEmailFanOutTests` pass unchanged in substance. Mutation-checked, each
  failing a test: no run predicate, no archive gate, no after-send stamp, no detach, no aborted-batch
  hook, a cancellable hook token, no rollback, no busy-disable. Migration applied, rolled back and
  re-applied on the server-only dev DB. Gates: doc-hygiene, design-tokens, render-modes, a11y pass.
  **No browser was available** at the build (H35; its browser pass is below). A live smoke without one: the server on the `Smtp` branch against
  a local SMTP sink, driven by `curl` — one mail per run, the burst, the re-fire after a read, the
  stamp, the unsubscribe GET/POST and the preference endpoints all behaved as the rule says
  (`audit/Messaging.md` F49 note); the smoke conversation was deleted from the dev DB after.
- **Review fixes (2026-10-13).** Three reviews raised seven findings; all were real. None needed an
  owner ruling; one became a derived default for the owner to confirm.
  1. **An unsubscribe undone by a moderator.** The moderator-initiated account paths loaded their
     target with a tracking query on the circuit-scoped context, so a circuit that had tracked the
     member (an earlier Warn) got its stale copy back. On Suspend/Ban the stamp bump's full-row
     `UserManager` write put every column changed since back (`email_on_new_message`,
     `ActiveReportCount`, `ApprovedStorySubmissions`), and Reinstate/auto-approve decided on old values.
     `LoadTargetUserAsync` detaches any tracked copy first (Apply, Reinstate, SetCanAutoApprove).
     Swept: `UserManager` is the only full-row writer, and this the only circuit-scoped caller. Rule
     added to `layer2-services.md` "Set-based writes leave the circuit's tracked copies stale"; D15
     annotated; `User.cs`'s "written only by" corrected.
  2. **No account-status gate** was a derived choice cited as "mirroring" a notification reason that
     does not carry over: now row 28 **(e)** / F25 (e) with its own reason, pinned by an Integration test.
  3. **D18's "where a user looks for it"**: the privacy form's "Private messages" control points to the
     toggle (a pointer, not a second toggle); the audit settled note no longer files the scalar column
     under "as D18 directed".
  4. **H36 duplicated H17**: folded into H17, H36 closed as a retired id.
  5. **Tests:** the landing page's link (Unit `UnsubscribeLandingPageTests`, both Integration GETs);
     the flusher's per-group after-send indices for a later group (Unit, plus a deferral case).
  6. **A dead pointer** in `layer2-services.md` §"Outbound email pipeline" now names §"Private-Message Email".

  Mutation-checked, each failing its test: the detach removed (three Integration tests), batch
  positions for the hook indices (two Unit tests), the old `/settings` link (Unit). **Unit 1,308** (+4),
  **RazorComponents 860** (+1: `PrivacySettingsFormTests`), **Integration 1,614** (+4:
  `ModerationIntegrityTests` 3, `PrivateMessageEmailTests` 1), all green; build 0 errors, no new
  warnings in touched files; the four gates pass. No browser: **H35** gains F20 and step 9. Stage
  notes: `audit/Moderation.md` F47, `audit/Profiles.md` F20, `audit/Messaging.md` F49,
  `audit/Notifications.md` F41/F43.
- **Browser pass (2026-10-13).** Aspire path (Mailpit; `psql` on 5433), on the persistent Aspire
  workbench, not reset. Actors: AuthorBeta (sender), ReaderGamma (recipient), AdminUser (moderator).
  All nine H35 steps passed, on both render phases wherever UI changed. **F20, F43 and F49 L4.5 1→5;**
  H35 closed. GIF: `e2e-WU-PrivateMessageEmail.gif` (compose, the mail in Mailpit, its headers).
  - **Seen:** one mail per run, stamped with the message's `date_sent`; a burst stays silent; a read
    re-arms the run; archive mutes it. The unsubscribe GET is read-only, the POST turns the email off,
    and a re-POST shows the same page; the three wrong-token cases answer 400.
  - **Also seen:** a WASM 401 on the toggle rolls back with the session-expired line. The moderator
    stale-copy fix held on one real circuit (Warn, unsubscribe, Suspend, Reinstate: the email stayed
    off).
  - **The shutdown drain mailed a queued message** when the `web` resource was stopped from the
    dashboard. `stop-aspire.ps1` cannot show it: DCP's proxies die first, so the drain fails and the
    mail is lost. Recorded in `run-server/SKILL.md`.
  - **Runtime bug fixed** (its own "browser-pass fixes" commit). After a full load, archiving left the
    conversation in the Inbox until a reload, on both phases.
    - **Cause:** .NET 10 hands a `[PersistentState]` property's restored snapshot back on the next
      parameter set.
    - **Fix:** `MessagesPage.Conversations` is field-backed, and its setter takes the framework's value
      only during initialization.
    - **Test:** RazorComponents `MessagesPageTests` +2, through the new
      `PersistentStateTestSupport.RestoreDeclarativeStateAsync`; the archive test fails without the
      fix.
    - **Docs:** the convention is in `layer3-logic.md` §"State Persistence", `layer5-wasm.md` and
      `testing.md`. Tracker **D20** holds the unswept `[PersistentState]` components; **F6b** gains the
      screen-reader check of step 9's line.
  - **Counts:** Unit 1,308, RazorComponents 862 (+2), Integration 1,614, all green; the four gates
    pass.
  - **Dev-DB state left (Aspire workbench):**
    - **Conversation 3**, AuthorBeta ↔ ReaderGamma: 9 messages, unarchived, with ReaderGamma's
      `last_notified_timestamp` set.
    - **AdminUser's three mod-filed reports on ReaderGamma** (ids 9–11: the Warn, the Suspend and the
      Reinstate, all resolved). ReaderGamma is Active again and her message email is back on.
    - **Mailpit** holds the run's mails; they are ephemeral.
- **Pointers:** `audit/Messaging.md` §"D18" + F49 note (and its browser verification);
  `audit/Notifications.md` WU-PrivateMessageEmail Stage note; `audit/Moderation.md` F47 and
  `audit/Profiles.md` F20 review-fixes notes; `layer2-services.md` §"Private-Message Email";
  `layer3-logic.md` §"State Persistence"; decision row 28; tracker F25, H35, H17, D15, D20.

## WU-AuthorStoryDelete — an author's hard delete of their own story, under any moderation state, with an unstarted Spotlight booking's slot returned and a typed-title dialog that offers unpublish and a download first (worksheet D15; service audit §3.20; extends `Stories/`, `Spotlight/`, `Identity/`) — DONE ✓ (2026-10-13)

- **Cells:** **F4 L4.5 5→1** (the Delete story section and `StoryDeleteDialog` are new UI) and **F52
  L4.5 5→1** (the deletion page's copy changed); no browser was available, tracker **H34** holds the
  pass. Built beneath Stage-5 cells: F4 L2/L3-Logic/L3.5/L4/L5, F55 L2, F52 L4. The review fixes moved
  no cell (F6 L3-Logic re-verified beneath its 5). The browser pass the same day returned F4 and F52
  L4.5 to 5 (H34 closed). Dated 2026-10-13, the session's `currentDate`, as the WUs before it.
- **Trigger:** worksheet D15 (answered 2026-08-07) and its spec `wu-specs/WU-AuthorStoryDelete.md`, with
  the amendment (X3 reworded as moot). Routed here: WU-UserDeletion's deletion-copy line, WU-BlobCleanup's
  third inline blob site, WU-ThrottleCoverage's "Deletes and removals" row.
- **What landed:**
  1. **`DeleteStoryAsync`** (`IStoryWriteService`, server, `DELETE /api/stories/{id}`, client): projected
     owner gate (primary author only; a NULL author owns nothing; no moderation gate), then one strategy
     transaction — tracker cleared, cover captured, `TptDelete.StoryCommentsAsync`, the slot release,
     the story `ExecuteDeleteAsync` with the author in its `WHERE` (0 rows → 404, a lost race), commit —
     then the cover blob best-effort and a `LogInformation` trace. No counter touched.
  2. **`SpotlightPlacementRelease`** (server-only, scoped; not on the WASM-registered
     `ISpotlightWriteService`, D37 pending): under the booking lock (hoisted to
     `SpotlightLocks.BookingLockSql`, shared with `RedeemSlotAsync`), `StartDate > now` slots of the
     story go `Redeemed → Available`; a live block counts as started.
  3. **UI:** `StoryDeleteDialog` on `Modal` (copy naming what goes; Unpublish instead for published
     stories; `StoryDownloadLinks` with "only the chapters that are published"; typed **saved** title,
     exact and case-sensitive; Cancel, then filled-danger delete). `StoryEditorPage`: Delete story
     section last; success clears the `draft:story:{id}` backup and `forceLoad`s to
     `/user/{id}/authored`; a refusal stays in the dialog; the delete joins `CoverChangeBlocked`.
  4. **Deletion page** (D13's compliance surface): the Stories delete-first line with the taken-down
     caveat; "Stories and blog posts can't be deleted" narrowed to blog posts.
  5. **Comments corrected:** `StoryEndpoints` class doc (delete is ownership-gated),
     `SpotlightSlotStatus.Redeemed`, `SpotlightSlot.Placement`.
  6. **Docs:** moment 1 — `layer2-services.md` §"Author story deletion" (new), the hard-delete callers,
     the Image Storage rule-2 sites and §"Community Spotlight" (slot return); `layer1-data-model.md`
     (story-scope `TptDelete`; `daily_story_stats`' cascade marked load-bearing); `cross-cutting.md`
     (Delete Policy bullet, GDPR rule 1 met for stories); `content-safety.md` (unconditional under
     moderation); `layer3.5-structure.md` (an input-bearing confirm is a `Modal` consumer);
     `security.md` (the delete in "Deletes and removals"); audit settled notes (Stories F4, Spotlight);
     roadmap Resolved D15 and **decision row 27**; tracker **F24** filed, F13/F3/B25 annotated; the
     worksheet Built line. Moment 2 — `testing.md` (interleaving a service whose collaborator shares the
     write context; an advisory-lock probe; re-entry tests must not await the refused call). Moment 3 —
     Stage notes + headlines (Stories F4, Spotlight F55, Identity F52), ImageStorage and Export lines,
     **H34** filed, `status.md` F4/F52 L4.5.
- **Deviations from the spec, each with its reason:** the redirect target comes from the
  `AuthenticationState` cascade, not `@inject IActiveUserContext` (that would be a third exception to
  the two-identity-source rule; `identity-and-authorization.md`); Unpublish instead runs the lifecycle
  transition **in place** rather than a `forceLoad` reload (the page's established lifecycle behavior;
  unsaved form edits survive); the cover path is captured **inside** the transaction (a retry
  recaptures it, and a cover replaced after the gate is the one deleted); X2 went to the existing
  tracker **F13** (its site 5) instead of a new item; T9 lives in `BlobCleanupTests` (D14's inline-site
  class) with a temp blob store rather than an HTTP 404 on the shared host's upload folder.
- **Correction reported to Brian (not built):** D15 says `daily_story_stats` has no FK and must be
  deleted explicitly. Its raw DDL carries `REFERENCES stories ON DELETE CASCADE`; the aggregator test D15
  asked for passes on the cascade alone, and the raw `DELETE` would be the hand-enumeration D15's
  bullet 2 forbids.
- **Left alone (owner-open), decision row 27 / tracker F24:** (a) a live block counts as started; (b)
  the moderator hard delete returns no slot (X5); (c) no sponsor notification (X4); (d) the download is
  published chapters only and a taken-down story's export 404s (X6); (e) a taken-down story's editor
  doesn't load, so it has no Delete button (found by the build). Also: zombie open reports on the
  deleted story (**F13** site 5, X2); the ToS line (**F3**, X7); the CDN purge (Phase 7, X8). X3 is moot
  (WU-ModerationIntegrity kept the per-report throw; the queue drops such rows). No migration.
- **Verification:** build 0 errors, no new warnings (the pre-existing CS9107/CS8602 only); **Unit
  1,263** (+4: `ClientStoryDeleteServiceTests`), **RazorComponents 840** (+25: `StoryDeleteDialogTests`
  16, `StoryEditorPageTests` +9), **Integration 1,587** (+26: `StoryDeletionTests` 18 — D15's list,
  the authorless refusal, the dangling notification, the circuit tracker, a second delete, an
  interleaved lost race, the booking lock held at the delete; `BlobCleanupTests` +4;
  `StoryEndpointsTests` +4; `StaticSsrPageRenderTests` +4 assertions), all green. Mutation-checked, each
  failing a test: no story-scope `TptDelete`, no slot release, no tracker clear, no 0-row check, no
  blob delete, the release's `StartDate` test removed or widened to `EndDate`, its story filter or its
  lock removed; the live title as the confirmation string, the delete outside `CoverChangeBlocked`, no
  draft clear, the dialog left open on Unpublish instead, a case-insensitive match, no clear on reopen,
  a Cancel while busy. Gates: doc-hygiene, design-tokens, render-modes, a11y pass. **No browser was
  available** (H34); the browser pass below ran the same day and closed it.
- **Review fixes (2026-10-13).** Three reviews raised 11 findings, 9 distinct after merging duplicates.
  All 9 were real; none needed an owner ruling.
  1. **A taken-down story had no Delete button.** D15 rules it ("an active `IsTakenDown` does not block
     it"; the control is "on the story edit surface"), so it was not an open edge. `GetStoryForEditAsync`
     lifts the takedown for the author alone (anyone else gets the missing-id `null`, never a 403) and
     echoes `StoryEditDTO.IsTakenDown`; `StoryEditorPage` renders **delete-only** (notice + Delete story;
     no form, lifecycle or chapter panels; edit mode only, so a reused instance's create route never
     inherits it, tracker D18); the dialog replaces both outs with one line; `ChapterEditorPage` keeps
     such a story not found. The deletion page's caveat became "This works for a story a moderator has
     taken down too". Row 27 (e) / F24 (e) narrowed to the open part: nothing links the author there.
  2. **The typed title** now matches with whitespace runs collapsed (NBSP included), as the label shows it.
  3. **A comment committed mid-delete** failed the cascade with 23001: `TptDelete.StoryCommentsAsync`
     locks the story's chapters `FOR UPDATE` first (the moderation hard delete shares it).
  4. **A lost commit acknowledgement** replayed into a 404: `ExecuteInTransactionAsync` with
     `verifySucceeded`. Items 3–4 at the other scopes (chapter, blog post, profile wall): tracker **D19**.
  5. **The post-delete `forceLoad`** is asserted from `BunitNavigationManager.History`.
  6. **Docs:** `cross-cutting.md`'s survival test now quotes the owner ("once the actor is gone"); the
     advisory-lock registry names `SpotlightPlacementRelease`; `TptDelete`'s doc and tracker F13 lost
     their "once built"; `testing.md` gains the lock-timeout and lost-acknowledgement test shapes.

  Mutation-checked, each failing its test: the chapter lock, `verifySucceeded` answering false, the edit
  read's takedown lift, its non-author `null`, the chapter editor's check, the whitespace collapse,
  `forceLoad: false`, the edit-mode condition. **Unit 1,263**, **RazorComponents 852** (+12:
  `StoryDeleteDialogTests` +7, `StoryEditorPageTests` +3, `ChapterEditorPageTakedownTests` 2),
  **Integration 1,590** (+3: `StoryDeletionTests`; `StaticSsrPageRenderTests` assertions updated), all
  green; build 0 errors, no new warnings in touched files; the four gates pass. No browser: **H34** gains
  steps 9–10. Stage notes: `audit/Stories.md` F4, `audit/Chapters.md` F6, `audit/Identity.md` F52.
- **Browser pass (2026-10-13). F4 and F52 L4.5 1→5; H34 closed; no runtime bug.**
  - **Setup.** All ten steps of H34 ran. The server-only path used the standing workbench (not reset). The
    Aspire path (Garage) ran step 9 again. `psql`, the upload folder or `garage bucket info`, the cover URL
    and the log's `Story {id} deleted by its author` trace were read after every write.
  - **Phases.** The circuit was proven by `_blazor/negotiate` and no `/api` call (the Auto hash removed
    before each full load). WASM was proven by the action's `/api` request.
  - **Actors.** AuthorAlpha, AuthorBeta, TestUser (sponsor, replier, non-author), ReaderGamma, ModUser,
    anonymous.
  - **GIF:** `e2e-WU-AuthorStoryDelete.gif` (WASM: an unsaved title edit, the dialog asking for the saved
    title, the delete, the Authored tab).
  - **What it saw.** The typed-title gate held: case-sensitive, a trailing space trimmed, a double-space
    title matched as its label shows it. The saved title held under an unsaved edit. Unpublish instead
    ran in place, with unsaved edits kept, on both phases. The six downloads answered 200. A Draft or
    Pending story had no Unpublish button. Each delete showed "Deleting…" with every control
    disabled, landed on `/user/{id}/authored`, and cleared `draft:story:{id}`. Each deleted story left
    no rows (story, listing, chapters, contents, comments, readers' rows), and its cover file or object
    and URL were gone.
  - **Taken-down stories.** The delete-only editor appeared for the author on the circuit (Local) and on
    WASM (Garage). Another user and the chapter route got not found, and the takedown reports
    survived.
  - **Spotlight.** A future booking's slot came back to its sponsor's `/spotlight`, and a live one's
    stayed consumed.
  - **Refusals.** A lost race (404, both phases) and a stale-identity 403 (WASM) showed readable lines in
    the dialog. The endpoint answered 401 (anonymous), 403 (TestUser) and 404 (missing).
  - **Logs.** No `fail:`/`crit:` beyond the documented Aspire antiforgery line. No console errors.
  - **Not covered.** Keyboard-only use and focus stay with the `Modal` keyboard half (F6b).
  - **Narrative:** the browser Stage notes in `audit/Stories.md` F4, `Identity.md` F52, `Spotlight.md`,
    `Chapters.md` F6, `ImageStorage.md`, `Export.md`.
  - **Dev-DB state left (server-only).**
    - Stories 16–21 are gone: 16 was WU-BlobCleanup's taken-down fixture; 17–21 were created and deleted
      by this pass. That took with them chapters 22–26, comments 7–10, the readers' `started` rows and
      the covers of 16–18.
    - Spotlight slots 1 (TestUser, `Available`) and 2 (TestUser, `Redeemed`, no placement): both granted
      by ModUser. No placements.
    - Report 13 survives (resolved, naming deleted story 16).
    - Profile counters (`StoriesWritten` and the rest) are stale until the next recompute, by design
      (D15/D21).
    - Empty `wwwroot/uploads/stories/16/`, `17/` and `18/` folders remain.
  - **Dev-DB state left (Aspire).** Story 14 (WU-BlobCleanup's taken-down fixture) is deleted and its
    cover object gone. The bucket holds 1 object (the July `users/1/profile-*.png` orphan). Report 8
    survives. The containers were stopped afterwards.
- **Pointers:** `audit/Stories.md` F4 (settled note + Stage notes), `audit/Spotlight.md` F55,
  `audit/Identity.md` F52, `audit/Chapters.md` F6; `layer2-services.md` §"Author story deletion"; `roadmap.md` row 27; tracker
  F24/H34 (closed)/F13/D19.

## WU-ThrottleCoverage — the throttle-coverage enumeration: three new write kinds, `ContentCreate` wired to lists/series/arcs, an `ExportGenerate` limiter, every unthrottled surface named with its reason, and the WASM 429 rebuilt from the wire (worksheet D20, throttle half only; service audit §2.5's throttle rows, §2.8's re-request bullet, §4's lineage-reject comments; extends `Security/`, `ContentGate/`, `BlogPosts/`, `Collaboration/`, `Stories/`, `Series/`, `CustomLists/`, `Export/`, `Client/Http/`) — DONE ✓ (2026-10-13)

- **Cells:** the build flipped none. Built beneath Stage-5 cells: F8 L2, F9 L2/L3-Logic, F10 L2, F37 L2,
  F50 L2, F51 L2, F54 L2, F63 L2, F66 (all), plus the cross-cutting client 429 arm (every Client* write
  service's L5 leg). The review fixes flipped **F8, F9, F10, F37, F50, F51 and F66 L4.5 5→1** (each
  now meets a refusal or a page no browser has seen; tracker **H33**). F54 stays 5. The browser pass
  the same day returned all seven to 5 (H33 closed). Dated 2026-10-13, the session's `currentDate`, as
  the WUs before it.
- **Trigger:** worksheet D20's throttle-coverage half (answered 2026-08-07) and its spec
  `wu-specs/WU-ThrottleCoverage.md`, with the amendment (a third `rateLimitedAction` caller,
  `ClientReportSubmissionService`). Routed here by earlier WUs: tracker **E7** (story status moves),
  **E6** clause 1 (consent throttling), the InertFeatures endpoints, `RemoveCoverArtAsync`.
- **What landed:**
  1. **Kinds:** `Reveal`, `RepeatRequest` (5 burst, 1 per 3 min — owner-given), `Vote`, appended to
     `WriteActionKind` with `DefaultLimits` rows; `Vote`/`Reveal` at 10/10 per min are **provisional**
     (decision row 26 (a)).
  2. **Call sites:** `ContentCreate` in `CreateListAsync`, `CloneListAsync`, `CreateSeriesAsync`,
     `CreateArcAsync`; `Vote` in `VoteAsync` (every call); `RepeatRequest` in
     `RequestAcknowledgmentAsync` (every request) and `RequestLineageAsync` (only when it notifies),
     placed after validation and before the tracked mutation — `security.md`'s placement exception.
  3. **Reveal:** new server-only `ContentRevealRecorder` (enum check → bucket → target check →
     idempotent insert; no-ops still redirect — the build checked the target against ground truth,
     which its review fixes replaced, item 1 below); the endpoint answers a refusal with a plain-text
     429 + `Retry-After`; the anonymous branch skips an undefined type (was a 500).
  4. **`ExportGenerate`** concurrency limiter (mirrors `ImportParse`) on both export routes.
  5. **WASM leg (found by the spec, decision-free):** the 429 `ProblemDetails` carries
     `writeActionKind`; `ClientHttpHelpers`' 429 arm is unconditional and tolerant (a plain-text edge
     429 is an `HttpRequestException`, no longer a `JsonException`); `rateLimitedAction` and
     `ReadRetryAfterSecondsAsync` retired (registered in `check-doc-hygiene.ps1`). Before, only three
     clients showed "wait N seconds"; every other throttled write showed the generic error on WASM.
  6. **Found while building, fixed — a cross-viewer consent leak (F66):** `AnonPrefs.Read` returned one
     shared static instance for cookieless requests and the consent endpoints mutate it, so one visitor's
     "Always show mature content" or "View this story" applied to every later cookieless visitor until
     restart. `Read` now returns a fresh instance; a nulled cookie list parses to empty.
  7. **`SeriesCreateEditPage`'s catch-all** used a fixed "unexpected error" string; it now follows the
     `ExceptionPresenter` catch-site pattern, so the create throttle reads as a wait.
  8. **Comments:** the lineage-reject "spam guard" claim (service + interface), the list interface's
     "no rate limiting", the series/arc/lineage endpoint docs, `ContentImportEndpoints`' dead citation,
     the `Program.cs` policy-set header, interface `throws` docs.
  9. **Docs:** moment 1 — `security.md` §"Write Throttling" (limits rows, the placement exception, the
     cost classes with `ExportGenerate`, the **unthrottled-surface enumeration** incl. the conditional
     edit exemption and the view-ping ratification, the 429 contract corrected: no `Retry-After` header
     on service throttles) and §"HTTP Edge Rate Limiting" (the full policy set; an edge policy never
     doubles a bucket); `layer5-wasm.md` (its "write endpoints take `RequireRateLimiting`" rule was the
     opposite of practice; the 429 row; the client arm); audit Settled notes (CustomLists' "no rate
     limiting" superseded, AccessGate's deferral resolved, Stories F45 view ping); roadmap Resolved D20
     (throttle half), **decision row 26**; tracker D16/D17/F23, E6 and D14 annotated; the worksheet
     Built line. Moment 2 — `identity-and-authorization.md` (prefs parse is the caller's instance),
     `testing.md` (throttle tests prove placement; limiters pinned by metadata), `layer2-services.md`
     (the report client's 429). Moment 3 — Stage notes + headlines (Stories F8/F9/F10/F45, Badges F50,
     CustomLists F51, BlogPosts F37, AccessGate F66, Export F54, Import F63, a Moderation annotation),
     service audit §2.5/§2.8/§2.11/§4 annotated, tracker E7 closed, **H33** filed.
- **Left alone:** D20's bounds/caps half and the **§2.11** reading-progress clamp (D20's own "Still
  open"; the task text named §2.11 as a source, but the owner's text outranks it — ledger home tracker
  **D14**, annotated); the `Edit` kind (owed only if the caps half is descoped); the import commit past
  five chapters (tracker **D16**, a design choice); saved selections' missing count cap (**D17**);
  group story-add's fan-out (flagged, not exempted — decision row 26 (b), tracker **F23**); a
  `RepeatRequest` charge on first requests is the build's reading (row 26 (c)); D20's registration-side
  throttles (still open); E6's clauses 2–4. The recorder keeps the endpoint's check-then-insert, so a
  double-click race can still 500 (tracker D11's class).
- **Verification:** build 0 errors, no new warnings in touched files (the three pre-existing C#
  warnings only); **Unit 1,258** (+23: `WriteThrottleWireContractTests` 18, `AnonPrefsTests` 5; one
  `ModerationIntegrityClientTests` body updated to the wire's real shape), **RazorComponents 813** (+2:
  `SeriesCreateEditPageTests`), **Integration 1,557** (+17: `WriteThrottleTests` 11 — lists, clone,
  series, arcs, the revoke/re-request cycle, validation spends nothing, no dirty row after a refusal,
  lineage only-when-notifying, votes incl. retraction, reveals, the reveal type mapping;
  `ExportServiceTests` 6 — the `ExportGenerate`/`ImportParse` metadata and `/resplit`'s absence), all
  green. Mutation-checked, each failing its test: the client's pre-WU 429 fall-through (17 of 18 wire
  tests), a shared `AnonPrefs` default, the acknowledgment call moved after the mutation, self-owned
  lineage links charged, BlogPost reveals routed to `BlogPosts`. check-doc-hygiene (21 terms),
  check-design-tokens, check-render-modes and check-a11y pass. No browser was available: tracker
  **H33** holds the pass. No migration; the build changed no `status.md` cell.
- **Review fixes (2026-10-13).** Three reviews raised 11 findings, 7 distinct after merging duplicates.
  All 7 were real; none needed an owner ruling.
  1. **The reveal's target check was an existence oracle (F66 L2).** It checked ground truth, so a
     hidden id (another author's draft, pending or rejected story, anything taken down, an unpublished
     or Private-author post) got a row that `/settings` listed as "(deleted …)", while a missing id got
     none; before the WU both got rows. The check is now the viewer's confidentiality predicate,
     `ServerContentRevealService.ResolveVisibleTitlesAsync` (the lookup the list titles with), so a row
     is minted exactly when the list can title it. `RecordAsync` reads the viewer from
     `IActiveUserContext` (no `userId` parameter). **Integration**
     `ContentGateTests.RevealRecorder_AHiddenTarget_LeavesTheSameListAsAMissingOne`.
  2. **The reveal's 429 was unlabeled (F66).** `Results.Text(…, "text/plain")` adds no charset and the
     message carries an em dash; now `text/plain; charset=utf-8` through
     `ContentGateEndpoints.RevealThrottled`. **Unit** `RevealThrottled_IsLabelledUtf8PlainText_WithRetryAfter`.
  3. **The enumeration missed four writes.** `AcceptAsync`, `DeclineAsync` and `RejectLineageAsync`
     were named nowhere (now a "Respondent answers" row), though `audit/Badges.md` cited the
     enumeration for Accept and Decline; `UnfollowAsync` joins the deletes. Spotlight grant/revoke
     are named under mod-gated. The list was re-checked against every public method of every server
     write service.
  4. **`VouchAsync` was under the wrong bound.** It sat under "Hard-capped", but a vouch → remove →
     vouch loop re-notifies, so the dedup is its bound; it moved to the notifying toggles.
  5. **The lineage placement was unproven (F10 L2).** **Integration** `WriteThrottleTests` +3:
     validation failures spend nothing, a refused re-request of a Rejected link leaves no dirty row
     for the scope's next save, and an authorless target is free (the build's exemption beyond spec
     R4's "only when `!selfOwned`": nobody to notify; `security.md` now names it).
  6. **AccessGate's Settled bullet had the recorder's order wrong** (a missing target is dropped after
     the bucket); rewritten together with the new check.
  7. **L4.5 flips (the F46/F53 precedent).** F8, F9, F10, F37, F50, F51 and F66 5→1. F54 stays 5: its
     limiter refuses only under server-wide overload (the PollVoteIntegrity no-flip precedent). H33
     gains the hidden-target and 429-text steps.

  Mutation-checked, each failing its test: the ground-truth check, the dropped charset, the lineage
  call at method entry, the call after the mutation, and charging authorless targets. **Unit 1,259**
  (+1), **RazorComponents 813**, **Integration 1,561** (+4), all green; build 0 errors, no new
  warnings in touched files; the four gates pass. No browser: **H33**.
- **Browser pass (2026-10-13). F8, F9, F10, F37, F50, F51 and F66 L4.5 1→5; H33 closed; D18 opened.**
  Steps 1–8 of H33 ran on the server-only path, standing workbench (not reset), `psql` after every
  write. The throttle steps ran on the circuit (`_blazor/negotiate`, no `/api` call; the Auto hash
  removed before each full load), then on WASM after a server restart had emptied the in-memory
  buckets (the action's `/api` request and its 429, whose body carried `writeActionKind`). Actors:
  TestUser, AuthorAlpha, AuthorBeta, ReaderGamma, ModUser, anonymous. GIF:
  `e2e-WU-ThrottleCoverage (1).gif` (WASM series creates, the sixth refused; the first export of
  that name holds a single frame).
  - Every refusal read "You're doing that a little too fast — please wait N seconds and try again.":
    lists and a clone (120 s), series (120 s), arcs (120 s), lineage requests (180 s, self-owned links
    still free), credit requests (180 s, no Pending row left), poll votes (the eleventh, 6 s). The
    reveal flows, the 429's `text/plain; charset=utf-8` and `Retry-After`, and the consent leak fix
    (browser A against a cookieless client B) all held; exports answered 200 in all six formats,
    anonymous and signed in. Narrative: the Stage notes in `audit/Stories.md`, `BlogPosts.md` F37,
    `Badges.md`, `CustomLists.md`, `AccessGate.md`, `Export.md`.
  - **One runtime bug, fixed** (commit "browser-pass fixes"): `SeriesCreateEditPage` kept a create
    flag and an edit id that nothing cleared, so Write → New Series from an edit page showed the
    edited series' name in the create form. One nullable route key now (`layer3-logic.md`'s new
    create/edit rule). **RazorComponents** `SeriesCreateEditPageTests` +2, both failing before the
    fix; re-verified on both phases. After it: Unit 1,259, **RazorComponents 815**, Integration 1,561,
    all green; the four gates pass.
  - **Filed D18:** story, blog post, site announcement and group create/edit pages have no dispatcher
    at all and show the same symptom (three reproduced in the browser).
  - **Dev-DB state left (server-only).** Custom lists 1–5 and 7–11 (TestUser, private, "E2E Throttle
    Circuit/WASM List 1–5") and 6 (ModUser, public, "E2E Public List By ModUser"). Series 1–11
    (AuthorAlpha: "E2E Throttle Circuit Series 1–5", "E2E Throttle WASM Series 1–5", "E2E
    Browser-Pass Fix Series"). Arcs 1–9 on story 6 (AuthorBeta, chapters 1–9). Lineage rows from
    story 1 (→6 types 1–4 and →5 type 1 Pending; →3 types 1–2 and →7 type 1 Approved) and story 3 (→6
    types 1–4 and →12 type 1 Pending; →7 type 2 Approved). No acknowledgment rows (all revoked). Poll 3:
    ReaderGamma and TestUser both on Johto. `user_content_reveals` (6, Story, 4). Notifications
    40033–40036. The test browser's `canalave.prefs` cookie was cleared, then reset by the anonymous
    consent steps.
- **Pointers:** `security.md` §"Write Throttling"; decision row 26; tracker D14, D16, D17, D18, E6, E7,
  F23, H33; the Stage notes above.

## WU-BlobCleanup — blob cleanup owned twice (an inline post-commit delete at every terminal deletion path, a daily orphan sweep as the backstop) and cover upload persisting its own reference (worksheet D14, both halves; service audit §3.16 and §2.12; extends `Images/`, `Stories/`, `Moderation/`, `Identity/`) — DONE ✓ (2026-10-13)

- **Cells:** F4 L4.5 5→1 (the editor's cover flow changed, no browser; tracker **H31**). F4
  L2/L3-Logic/L3.5/L4/L5, F47 L2 and F52 L2 stay 5 (built beneath Stage-5 cells); the Images cluster
  has no grid row. The review fixes flipped **F52 L4.5 5→1** (the deletion page's copy changed again;
  H31). The browser pass the same day returned **F4 and F52 L4.5 1→5** (H31 and H3 closed). Dated
  2026-10-13, the session's `currentDate`, as the WUs before it.
- **Trigger:** worksheet D14 (answered 2026-08-07) and its spec `wu-specs/WU-BlobCleanup.md` (which
  merges D14's WU-CoverPersistence half), with the campaign amendment: X9 dropped
  (`daily_story_stats` cascades from `stories`), the hard-delete blob delete after the strategy returns.
- **What landed:**
  1. **Enumeration (frozen-interface amendment):** `IImageStorageService.EnumerateAsync` yields Core
     `StoredImage(RelativePath, LastModifiedUtc)` over `stories/` and `users/` only;
     `ImageUploadRules.KeyPrefixes` and `IsGeneratedKey` (the exact `BuildKey` shape, extensions from the
     allow-list); Local walks lazily, S3 pages `ListObjectsV2` (`ListPageSize`).
  2. **Sweep:** `ImageOrphanSweeper` + `ImageOrphanSweepWorker` (registered in Program.cs, removed by
     `TestAppFactory`): reference read first, unfiltered (write context + `IgnoreQueryFilters`); 24 h
     grace; foreign keys skipped; 100 attempts per cycle; dry-run first cycle per process; dangling rows
     logged, never acted on; kill switch `ImageStorage:OrphanSweep:Enabled`; span
     `ImageStorage.OrphanSweep` and counter `canalave.image.orphans.deleted`.
  3. **Inline sites:** `UserDeletionService` deletes the avatar after the strategy returns;
     `ResolveWithRemovalAsync(hardDelete: true)` captures a story's cover before the delete
     (`HardDeleteOutcome`) and deletes it after the strategy, in its own `try` ahead of the
     notifications. Both best-effort with a Warning; a takedown never touches the blob.
  4. **Cover persistence:** `CoverArtRelativeUrl` off `IEditableStoryProperties`/`StoryUpdateDTO`/
     `CreateStoryDTO` and the mapper; `UpdateStoryAsync`'s cover branch gone; `GetStoryForEditAsync`
     returns `StoryEditDTO`; `UploadCoverArtAsync` loads, gates (authorless → 403, closing §2.12's
     `(int?)` item), writes, persists, deletes the old blob; new `RemoveCoverArtAsync`
     (`DELETE /api/stories/{id}/cover`, client impl). `StoryPropertiesForm` raises
     `OnCoverSelected`/`OnRemoveCover` with `CoverUploadBusy`; `StoryEditorPage` uploads on pick (edit)
     or after the create (create, the second `UpdateStoryAsync` gone).
  5. **Docs:** moment 1 — `layer2-services.md` §"Image Storage — blob ownership and cleanup" (new) and its
     two stale S3 sentences; `testing.md` (the upload folder is not reset; `TempBlobStore`);
     `cross-cutting.md` (Blobs in the delete policy and the account-deletion lists); `layer5-wasm.md`,
     `render-and-layout.md`, `layer3.5-structure.md`, `folder_clusters.md`; Settled notes in
     ImageStorage (the "Orphan bug" header rewritten, the Contract amended), Stories F4, Moderation F47,
     Identity F52; roadmap D14 Resolved, Phase-7 CDN purge, the restore-drill kill switch, one bucket per
     database, **decision row 25**; tracker **F22**; service audit §3.16/§2.12 annotated;
     `SelectedCoverFile` registered as retired. Moment 2 — `testing.md` §Unit (span emitters);
     `logging.md` (the sweep's span and counter).
     Moment 3 — Stage notes and headlines (ImageStorage L2, Stories F4, Moderation F47, Identity F52),
     `status.md` F4 L4.5, the worksheet D14 Built line, tracker **H31**. The deletion page's copy (the
     compliance surface) names the profile picture as deleted and covers as kept.
- **Left alone:** stripping a deleted user's surviving-story covers (the spec's X1: D14's text asks for
  it on a premise D13 overturned — **decision row 25**, tracker **F22**); the CDN purge (X2, Phase 7);
  author story delete as the third inline site (X3, WU-AuthorStoryDelete inherits the rule); derivative
  sizing (B14); a revert-cover action (X5, D14's accepted consequence); a throttle on
  `RemoveCoverArtAsync` (X6, WU-ThrottleCoverage lists it as exempt); `UserDeletionService`'s folder and
  interface (X7); avatar removal (X10). The create flow's pre-existing gap (the story exists if the
  cover upload then fails, and a resubmit duplicates it) is noted in F4's Stage note, not fixed. No
  automated test drives the worker loop, as for every worker.
- **Verification:** build 0 errors, no new warnings in touched files (the three pre-existing C#
  warnings only); **Unit 1,225** (+35: `ImageUploadRulesTests` 28, `LocalImageStorageEnumerationTests`
  5, `StoryMappersTests` 2), **RazorComponents 805** (+10: `StoryEditorPageTests` 5,
  `StoryPropertiesFormTests` 5), **Integration 1,539** (+36: `ImageOrphanSweeperTests` 8,
  `BlobCleanupTests` 6, `BlobCleanupGarageTests` 1, `StoryCoverTests` 12, `StoryEndpointsTests` 5,
  `S3ImageStorageServiceTests` 3, `HostBootTests` 1; `StaticSsrPageRenderTests` asserts the new copy),
  all green. Mutation-checked, each failing a test: the reference read through filtered `Stories`; no
  grace, key-shape guard, cap or dry run; avatars left out of the references; no S3 continuation or
  prefix scope; no avatar delete; no cover delete or its own `try`; an unpersisted upload; no replaced
  or removed blob delete; the old authorless gate; an update after either editor upload; the upload
  deferred to save; no busy disable. A first full run caught a Unit cross-talk (a `SaveAsync` in a
  parallel class tripped `ImageStorageTelemetryTests`' span count); fixed and written into
  `testing.md`. A second run caught a pre-existing `GarageFixture` readiness race that a second
  Garage-using class made likelier: a just-opened container dropped the connection
  (`HttpIOException`), which escaped the retry's `AmazonS3Exception`-only catch; the retry now tolerates
  `IOException`/`HttpRequestException` until its deadline. check-doc-hygiene, check-design-tokens, check-render-modes and check-a11y pass. No
  browser was available: tracker **H31** holds the pass.
- **Review fixes (2026-10-13).** Three reviews raised 14 findings, 12 distinct after merging duplicates.
  All 12 were real; none needed an owner ruling.
  1. **The sweep was inert under daily restarts (L2).** The dry run ran at each start and the first
     real cycle a day later, so a process restarted at least daily never deleted, against D14's
     "orphans accepted is rejected for any class". Now the first real cycle follows a completed dry run
     by an hour (`ImageOrphanSweepWorker.DelayAfterCycle`); left: a process restarted more often than
     hourly only dry-runs. **Unit** `ImageOrphanSweepWorkerTests` (3).
  2. **A cover call could overlap the save (F4 L3-Logic).** Pick-then-Save started the save under a
     streaming upload; its reload aborted the upload, and on a circuit both shared the scoped write
     context. The editor's three writes (cover call, save, status change) are now mutually exclusive,
     disabled both ways and re-checked in each handler; the save-vs-status pair predated the WU and was
     closed in the same edit. `layer2-services.md`'s write-context bullet now says this is the page's
     job. **RazorComponents** +6 (`StoryEditorPageTests` +4 with the 403 cover case,
     `StoryPropertiesFormTests` +2).
  3. **The deletion page missed the cover delete-first path (F52 L4).** "Cover images: use Remove cover
     in your story's editor." and "The same goes for a cover image that shows you." (D13 rider (i));
     `cross-cutting.md`'s copy paragraph; **F52 L4.5 5→1** per the WU-UserDeletion precedent;
     `StaticSsrPageRenderTests` asserts both.
  4. **Untested client impls (F4 L5).** **Unit** `ClientStoryCoverServiceTests` (7): the DELETE's verb,
     route and refusals, the multipart upload, and the `StoryEditDTO` echo beside the polymorphic tags.
  5. **Docs.** ImageStorage's Settled note split into D14's text and the derived build choices, with
     `layer2-services.md` marking each sweep constraint *(D14)* or *(derived)*; the trap test's comments
     say what each test catches (re-mutation-checked); the hard-delete recipe gains its blob step and
     `content-safety.md`'s escape hatch names the cover erasure and CDN caveat; the DELETE's
     "no body, so no antiforgery concern" is now a fact, not a CSRF rationale (D38 pending); F4's L4
     headline and F52's L4/L4.5 headlines updated; tracker **H3** folded into **H31** (roadmap tier-6
     row repointed), H31 gains the overlap and real-sweep steps; the D14 Built line notes the timing.

  Mutation-checked: dropping the editor's handler guards or the form's cross-disables fails the overlap
  tests; a reference read routed through `Stories` fails only the mis-injected-context test, as its
  comment now says. **Unit 1,235** (+10), **RazorComponents 811** (+6), **Integration 1,539**, all
  green; build 0 errors, no new warnings in touched files; the four gates pass. No browser: **H31**.
- **Browser pass (2026-10-13). F4 and F52 L4.5 1→5; H31 and H3 closed; H32 opened.** All nine of
  H31's steps ran: the editor steps on both render phases (circuit proven by `_blazor/negotiate`, WASM by
  the `/api/stories/…` requests), the deletion page, the hard delete and takedown over HTTP as ModUser,
  two account deletions, and the sweep. Server-only (Local) and Aspire (Garage). `psql` and the upload
  folder or `garage bucket info` after every write. Actors: TestUser, AuthorAlpha, ModUser, two
  throwaway accounts, anonymous.
  - Every flow worked: create-with-cover (one `POST …/cover`, no `PUT`), upload on pick (old blob
    gone), save after upload (cover kept), Remove cover, the refused file, and the three writes kept
    apart. A Save force-enabled mid-upload was ignored. The hard delete and the account deletions erased
    their blobs (URL 404, bucket −1), and the takedown kept its cover. The dry run logged two would-be
    deletes, and the real cycle an hour later deleted both. Narrative: the four audit Stage notes.
  - **One runtime bug, fixed** (commit "browser-pass fixes"): an upload present in `wwwroot/uploads/`
    at build time was compiled into the static web assets manifest, so after the sweep deleted it its
    URL still answered 200 (empty). The Server csproj now removes the folder from `Content`;
    `layer2-services.md` states the rule. **Integration** `HostBootTests` +1 (fails before the fix,
    only on a machine with uploads at build time). After the fix: Unit 1,235, RazorComponents 811,
    **Integration 1,540**, all green; the four gates pass.
  - **Filed H32** (polish): a refused file is reported out of view, "Uploading…" shows during a
    removal, the picker keeps the last file name, and a status change leaves a stale refusal.
    `run-server/SKILL.md` gained the `InputFile` and transient-state techniques.
  - **Dev-DB state left (server-only).** Stories 13 "E2E Blob: WASM Race Title" (TestUser, Draft, no
    cover) and 14 "E2E Blob: WASM Created With Cover" (TestUser, Draft, no cover). Story 15 (AuthorAlpha)
    was created and hard-deleted (report 12). Story 16 "E2E Blob: Takedown Target" (AuthorAlpha,
    taken down, cover `stories/16/…` kept; report 13). Notifications 40029–40032. User 10 was created
    and deleted. The sweep deleted the WU12 fixture `uploads/stories/999/cover-*.png` and an
    unreferenced `uploads/users/1/profile-*.png`. The planted `uploads/stories/12/` fixtures were
    removed.
  - **Dev-DB state left (Aspire).** Story 13 (AuthorAlpha) was created and hard-deleted (report 7).
    Story 14 "E2E Blob S3: Takedown Target" (AuthorAlpha, taken down, its cover object kept; report 8).
    User 8 was created and deleted, with one Mailpit message. The bucket holds 2 objects: story 14's
    cover and the July `users/1/profile-*.png` orphan, which the next real Aspire sweep deletes. The
    containers were stopped afterwards.
- **Pointers:** `audit/ImageStorage.md` (Settled + Stage note), `audit/Stories.md` F4,
  `audit/Moderation.md` F47, `audit/Identity.md` F52; `layer2-services.md` §"Image Storage — blob
  ownership and cleanup"; roadmap decision row 25; tracker F22, H31 and H3 (closed), H32;
  `run-server/SKILL.md` ("Driving an `InputFile`").

## WU-PollVoteIntegrity — single-choice as a database constraint, and `AllowMultiple` frozen under a vote by an FK (worksheet D23's poll paragraph and its `ConfigLocked` rider; extends `BlogPosts/`, `Data/`) — DONE ✓ (2026-10-13)

- **Cells:** none flipped. F37 L1 and L2 stay 5 (defects beneath Stage-5 cells); F37 L4.5 stays 5 (no
  markup or normal-flow copy changed). Dated 2026-10-13, the session's `currentDate`, as the WUs before it.
- **Trigger:** the campaign amendment for this WU (no spec file; self-scoped from D23 against fe3d032):
  `PollId` on the vote row with a composite FK, a guard set to `PollId` for single-choice polls and
  unique with `user_id`, and the rider closed declaratively if possible.
- **What landed:**
  1. **Migration `WU_PollVoteIntegrity`:** `poll_votes.poll_id` (composite FK `(poll_id,
     poll_option_id)` → `poll_options`' new alternate key, replacing the single-column option FK),
     `poll_votes.allow_multiple` (composite FK `(poll_id, allow_multiple)` → `base_polls`' new alternate
     key, no ON UPDATE action: the rider) and the STORED generated guard `single_choice_poll_id`,
     unique with `user_id`, NULLS DISTINCT. Up backfills, keeps the newest of a live voter's duplicate
     single-choice rows, then sets NOT NULL. Rehearsed Up/Down/Up on a dirtied dev-DB clone; applied to
     the server-only dev DB.
  2. **`ServerPollWriteService`:** `VoteAsync` writes `PollId` and the read's mode, and answers a lost
     race (23505 on either vote index, 23503 on either composite FK, a 0-row delete) with
     `VoteRaceMessage` (a 400; touched entries detached), never a 500 or a second live row. (Since the
     review fixes, a lost race whose rows already stand as asked succeeds.)
     `UpdatePollAsync` writes a pre-vote mode change set-based, because EF refuses a mutable key
     property (probed), answers the mode FK's 23503 as the lock message, and detaches the poll after
     every call.
  3. **Docs:** moment 1 — `layer2-services.md` §"Check-then-act posture" (poll row built, the
     mechanism, why it holds under concurrency, the refusals, the set-based key write, what it does
     not cover) and the stale-tracking rule; `layer1-data-model.md` §"Column Conventions" (the
     `poll_votes` bullet, alternate-key naming, key columns read-only, STORED generated columns);
     F37's Settled bullets; roadmap D23 Resolved; service audit item 15; tracker **F21** and
     **decision row 24**. Moment 2 — `testing.md` (an inline vote seed names its poll and mode).
     Moment 3 — F37 Stage note and L1/L2 headlines; worksheet D23 Built line; tracker **H30**.
- **Left alone:** the other three locked fields (`AnonymityMode`, `ResultsVisibility`, `DateOpened`)
  keep the `hasVotes` check-then-act. D23's rider names `AllowMultiple` only, and whether
  `AnonymityMode` (whose race can name a voter who voted under `Anonymous`) gets the same FK is the
  owner's (**decision row 24**, tracker **F21**). No retry on a lost vote race: a retry would cast the
  vote under settings the voter never saw. `PollView` still shows the refusal inline without reloading
  the poll. The tracker F17 upper bound is unchanged: the guard does not group anonymized rows.
- **Verification:** build 0 errors, no new warnings in touched files; **Unit 1,190**, **RazorComponents
  795**, **Integration 1,498** (+13: `PollVoteIntegrityTests`), all green. Mutation-checked: a
  non-unique guard, no mode FK, no vote catch, no vote detach, no poll detach, no edit catch, and
  dropping the concurrency or option-FK branch each fail their tests. Four inline vote seeds in three
  classes were refused by the new FKs (the rule working) and now name the poll. check-doc-hygiene,
  check-design-tokens, check-render-modes and check-a11y pass. No browser was available to the build:
  tracker **H30** held a regression smoke of the vote and edit flows. The browser pass closed it the
  same day (below).
- **Review fixes (2026-10-13).** Three reviews raised 6 findings, 4 distinct after merging duplicates.
  All 4 were real. One needs an owner ruling and was routed, not built.
  1. **A double submit was refused (L2).** Two identical votes from one voter (two tabs, a client retry)
     made the loser answer `VoteRaceMessage` ("This poll changed…") although the vote stood as
     submitted. Now a lost race re-reads the voter's rows and succeeds when they already stand exactly
     as asked, per D23's create-create posture (`layer2-services.md` §"Check-then-act posture"). The
     message now reads "This poll or your vote on it changed…".
  2. **Untested branches.** No service test reached the (option, user) index branch, and the refused
     edit's detach was never exercised on the same context. **Integration** +5: identical vote counts as
     cast ×2, same option with the other visibility refused ×2, double retraction counts as done. The
     refused-edit test now checks an empty tracker and a further save through the same context.
  3. **Docs.** `layer2-services.md` narrowed `UpdatePollAsync`'s detach to "after the `AllowMultiple`
     write" and to "the poll and its options". It is a `finally` after every call, and it covers votes.
  4. **`AnonymityMode` (owner-open).** Decision row 24 and tracker **F21** now quote the lock's stated
     purpose ("prevents retroactive anonymity exposure") and the rider's "first-vote-vs-config-edit
     boundary". The owner can see that the status-quo default leaves that purpose open for one
     request's window. Not built.

  Mutation-checked: dropping the option-user branch, refusing every lost race, no vote detach, no
  edit detach, and ignoring `IsAnonymous` each fail a test. **Unit 1,190**, **RazorComponents 795**,
  **Integration 1,503** (+5), all green; build 0 errors, no new warnings in touched files; the four gates
  pass. No browser: the new success path and the reworded refusal show only on a race (tracker **H30**
  unchanged).
- **Browser pass (2026-10-13). No flip (F37 L4.5 stays 5); H30 closed; no runtime bug.** All five of
  H30's steps ran on the server-only path, on both render phases (circuit proven by
  `_blazor/negotiate`, WASM by the `/api/polls` calls), with `psql` after every write. Actors were
  ModUser (site poll), AuthorAlpha (blog-post polls), TestUser (voter) and anonymous.
  - **Flows.** Pre-vote mode edits went both ways, twice through one circuit and twice over `PUT`.
    Single-choice vote, change, retract and re-vote left one row at most. Multi-choice ticks tracked the
    rows. The lock showed disabled selects, and a forced select read "Vote mode is locked once votes
    exist." on both phases. A direct `PUT` with the other mode answered 400. Two polls were deleted
    through the confirm dialog, and no rows remained.
  - **Races, timed from the page.** A `fetch` fired beside the UI's own vote or edit. Both refusal texts
    read on both phases. An identical double submit answered 200 twice with one row. A `psql`
    `FOR NO KEY UPDATE` lock held an edit's `UPDATE` while a first vote committed, and the database
    refused the mode change. The same circuit's next vote and next edit both saved after a refusal,
    so the detaches work.
  - **Constraints.** Raw `psql` violations inside rolled-back transactions were all refused: the
    guard with 23505, and the option FK and the mode FK (both the UPDATE and the INSERT direction) with
    23503.
  - **Logs.** The handled races log EF Core's `fail:` lines, so the poll sites were added to tracker
    **E10**; the level is unchanged here. No console error. Narrative: `audit/BlogPosts.md` F37's
    WU-PollVoteIntegrity Stage note, browser bullet.
  - **Dev-DB state left (server-only).**
    - Site poll 3 "E2E integrity poll: favourite region" (ModUser; Single choice, Always, Shown;
      options 5–7). Its description edit with a vote present stamped `last_edited_at`, so the edit
      worker will notify TestUser on the server's next run.
    - TestUser's vote on poll 3 (Sinnoh, row 41).
    - TestUser's poll-2 vote, restored to Turtwig after a change (row 88, which replaced row 4).
    - Polls 4 and 5 (blog post 2) were created and deleted. The rolled-back probes and races used up
      identity values.
- **Pointers:** `audit/BlogPosts.md` F37 (Stage note, its browser bullet, and the Settled bullets);
  `layer2-services.md` §"Check-then-act posture"; tracker F21, H30 (closed) and E10; roadmap decision row 24.

## WU-UserDeletion — the account-deletion survival set ratified and built (poll votes survive anonymized, empty conversations deleted at the source, the deletion page as the compliance surface) and the admin-less group rescue (worksheet D13 and its edges, schema audit §3.8; extends `Identity/`, `BlogPosts/`, `Messaging/`, `Groups/`, `Moderation/`, `Notifications/`, `Data/`) — DONE ✓ (2026-10-13)

- **Cells:** L4.5 **5→1** on F37, F49 and F52 (Part A: the voter list's "[deleted user]" branch,
  messaging's label, the deletion copy) and F40, F42 and F47 (Part B: the group page's rescue panel and
  leave warning, the type-62 copy, Group rows in "Show resolved"); no browser was available, tracker
  **H29**. All six went **1→5** at the browser pass the same day, which closed H29. Every other touched cell stays 5: F37 L1/L2/L3/L3.5, F49 L2, F52 L1/L2/L4, F40 L2/L3/L3.5,
  F41 L1/L2, F42 L2, F46/F47 L1/L2/L5, F38 (no own surface changed). Two commits (the spec's Part A and
  Part B). Dated 2026-10-13, the session's `currentDate`, as the WUs before it.
- **Trigger:** the WU spec plus orchestrator amendments U3 (close open Message reports inside the
  orphan-conversation cleanup), the Group resolver arm, test 12's current exception, and "compose with
  D23's poll shape" (WU-PollVoteIntegrity follows).
- **What landed — Part A (survival set, edge (a), the copy):**
  1. **Migration `WU_UserDeletion_SurvivalSet`:** `poll_votes` surrogate `poll_vote_id` PK, nullable
     `user_id` `ON DELETE SET NULL`, unique `ix_poll_votes_poll_option_id_user_id` (NULLS DISTINCT).
     Scaffolded `defaultValue: 0` removed; `Down()` deletes anonymized votes first. Rehearsed Up/Down/Up
     on a dirtied dev-DB clone; applied to the server-only dev DB.
  2. **Poll reads:** `TotalVoterCount` = distinct live voters + one per anonymized row; the voter list
     keeps anonymized rows (live names first) as `PollVoterDto(null, null)`; `PollView` renders them as
     plain "[deleted user]"; the edit sweep notifies live voters only. "Has voted" sites verified
     NULL-safe, unchanged.
  3. **`UserDeletionService`:** primary ctor, rewritten class doc, the load-bearing hard-delete comment;
     after its save it deletes the conversations it left with no participant (scoped to the user's
     captured ids, one `ExecuteDeleteAsync`) and first closes their open message reports.
  4. **Messaging** reads "[deleted user]" (one `DeletedUserLabel` constant, eight sites).
  5. **`DeletePersonalData.razor`** enumerates the survival set, the destroyed set, the delete-it-first
     paths (and the two that do not exist yet: stories, blog posts), that sent messages cannot be
     deleted, that a removed name is not removed text, and (Part B) the sole-admin consequence.
- **What landed — Part B (edge (b)):**
  1. **`ReportedEntityType.Group = 6`**, ledger-only (not in the submission allow-set; D47's floor).
  2. **`AssignGroupAdminAsync`** on `IModerationWriteService`: exactly three guards (moderator,
     admin-less, target is a member) plus input checks; `Role = Admin` and an opened-and-resolved Group
     `Report` (reason Other, the notes, `ReportedUserId` from the resolver's new Group arm) in one save;
     type 62 to the promoted user after commit, null-sourced, skipped for a self-assignment. No counter.
  3. **Type 62 `GroupAdminAssigned`** (migration `WU_UserDeletion_GroupAdminAssigned`, rehearsed and
     applied): the notify method (no moderator parameter), the enricher's Group kind, an actor-free
     presenter arm, client twin and fake.
  4. **L5:** `POST /api/moderation/groups/{groupId}/assign-admin`, a **JSON body**
     (`AssignGroupAdminRequest`) rather than tracker D9's query-string shape; the client posts it.
  5. **Queue:** `BatchLoadTargetsAsync`'s Group arm ("Group: {name}", `/group/{id}`).
  6. **UI:** `GroupDetailDto.AdminCount`; `GroupAdminRescuePanel` on `GroupPage` for a moderator of an
     admin-less group; the sole admin's Leave confirms first. `LeaveAsync` unchanged.
  7. **Docs:** moment 1 (both parts, before Part A's code) — `cross-cutting.md` §"Account deletion"
     (new), `layer1-data-model.md` Keys, `layer2-services.md` (§"Group Membership and Role Model"
     rewritten, the Group row shape, the `ReportedUserId` Group arm, type 62), `content-safety.md`,
     Settled notes in Identity F52, BlogPosts F37, Messaging F49, Groups F38 (WU32 Decision 3 amended),
     Moderation F47, Notifications F41; `roadmap.md` Resolved; tracker F17/F18/F19. Moment 3 — Stage
     notes and headlines in Identity F52, BlogPosts F37, Messaging F49, Groups F38/F40, Moderation
     F46/F47, Notifications F41/F42; worksheet D13 (two) and D8 Built lines; tracker H6 bullet closed,
     H29 filed, B25/D9/D11 annotated; `grid_axes.md` F52's service name.
- **Left alone:** an exact `AllowMultiple` voter count after a deletion (owner-open, tracker **F17**;
  the upper bound is pinned by a test); a sender-side message delete (**F18**); the bare ids in
  `reports.reported_entity_id` / `notifications.related_entity_id` (**F19**; log retention moved to
  **E11** by the review fixes, below); the story
  self-delete link (WU-AuthorStoryDelete must add it to the copy) and the blog-post delete UI (**B25**);
  avatar blobs (WU-BlobCleanup — the post-commit seam is marked); D47(b) and user-filed Group reports;
  the "on request" channel (out of band until D47); a moderator self-assignment guard and
  member-to-member transfer (D13: exactly three guards; transfer stays absent); the assign-vs-leave race
  (a 500, recorded in **D11**); `ServerModerationReadService`'s `"[deleted]"` submissions label (not
  D13's scope); D23's `PollId`/guard columns (WU-PollVoteIntegrity, whose migration must come after
  both of this WU's).
- **Review fixes (2026-10-13).** Three reviews raised 15 findings, 12 distinct after merging duplicates.
  Each was checked against the code and the worksheet; all 12 were real. One needs an owner ruling and
  was routed, not decided.
  1. **The orphan-conversation race (L2).** Two participants deleting at overlapping times each saw the
     other's uncommitted participant row, so neither deleted the conversation. `DeleteUserAsync` now
     locks the user's conversations (`FOR NO KEY UPDATE`, id order) before the delete
     (`cross-cutting.md` §"Account deletion").
  2. **A stale rescue panel (L3).** A refusal reloaded only the panel's members, so after another
     moderator's assignment it kept offering "Assign as admin". It now hands the refusal to `GroupPage`
     (`OnStale`), which reloads the group and keeps the message where the panel was
     (`error-handling.md`, the child-panel case, new).
  3. **D13 fidelity.** `cross-cutting.md` rule 3 now has the owner's full list, "any auth/IP logs"
     included. The log half is ruled, so it moved out of F19 into new tracker **E11**, a Phase-7
     obligation (`security.md` register, `roadmap.md` Phase 7, `logging.md`). Rule 1 names rider (i)'s
     unmet halves: stories (WU-AuthorStoryDelete) and blog posts (**B25**, which no WU owns).
     `roadmap.md`'s D13 entry, `audit/Identity.md` F52 and the worksheet's Built line agree.
  4. **The deletion copy.** It no longer claims "nothing links what remains back to you" (server logs
     do). It says that Retract vote works only on an open poll and that a recommendation the author
     removed can't be deleted. Whether rider (i) reaches that recommendation is new tracker **F20**.
  5. **Type 62's attribution (owner-open).** The docs and comments had cited D5 "by its own wording",
     but D5 covers the sanctioned or reporting user in 70–82. The null source is now recorded as the
     spec's default under D5's threat model, routed as **roadmap decision row 23** (sibling of row 19).
     Behaviour unchanged.
  6. **Tests and records.** An Integration read-back of the type-62 notification (name, link, an
     M-audience group); Unit tests for the client's `AssignGroupAdminAsync` (route, JSON body, 400);
     `layer2-services.md`'s `ReportedUserId` producer list (the fourth producer records the founder);
     the panel's real heading quoted in `layer2-services.md` and `audit/Groups.md`; an "above"
     pointer in `audit/Notifications.md`; the panel test class's pointer to `GroupAdminRescueTests`.
- **Verification:** `dotnet build` 0 errors, no new warnings in touched files. `dotnet test` after
  Part A: Unit 1,182, RazorComponents 777 (+1), Integration 1,468 (+9); after Part B: Unit 1,188 (+6),
  RazorComponents 791 (+14), Integration 1,483 (+15). Mutation-checked: the old voter-count formula, an
  unfiltered sweep, the old messaging label, the conversation delete, the message-report closure, the
  admin-less guard, the self-notify skip, the queue's Group arm, the panel's `AdminCount` condition and
  the leave check each fail their tests. check-doc-hygiene, check-design-tokens, check-render-modes and
  check-a11y pass. No browser was available (tracker **H29**).
  - After the review fixes: build 0 errors, no warnings in touched files; **Unit 1,190** (+2),
    **RazorComponents 793** (+2), **Integration 1,485** (+2), all green; the four gates pass. Each fix
    was reverted in turn and its tests failed: the conversation lock (the conversation survived with no
    participant), the enricher's 62 arm and its `GroupAudience` bypass (no title), the page's `OnStale`
    wiring and the panel's handler branch, and the client's body route. H29's steps 1 and 7 are
    updated; no stage moved.
  - **Browser pass (2026-10-13). F37/F40/F42/F47/F49/F52 L4.5 1→5; H29 closed.** All nine of H29's
    steps ran. Steps 1–7 and 9 used the server-only path, step 8's email the Aspire path (Mailpit).
    Interactive pages were checked on both render phases, with `psql` after every write.
    - Two throwaway accounts were registered and deleted through the page. Their votes were
      anonymized, the conversation with a survivor stayed, the emptied one went, and its open message
      report closed.
    - The survivor read "[deleted user]" in the poll and in messaging. Both admin-less routes were
      rescued with stale-tab refusals on each phase. Type 62 arrived in the bell, the inbox and the
      email. Group rows appeared in "Show resolved" and in the founder's history.
    - Not driven: a two-admin Leave, since no in-app path makes a second admin (RazorComponents
      covers it). Narrative: `audit/Identity.md` F52's browser verification Stage note.
    - **One runtime bug, fixed** (commit "browser-pass fixes"): the message reply box kept the sent
      text, so a second click re-sent it. It was pre-existing, noted by WU-GlobalFlip in July and
      carried by no ledger. RazorComponents `MessagesPageTests` +2. After the fix: Unit 1,190,
      **RazorComponents 795**, Integration 1,485; the four gates pass.
    - **Dev-DB state left (server-only).** Poll 2 on blog post 2 (AuthorAlpha), with two anonymized
      votes and TestUser's. Conversation 2 (TestUser and a deleted user; messages 4, 6, 8–11).
      Groups 5 "E2E Rescue: deletion route" (ReaderGamma admin, founder NULL) and 6 "E2E Rescue: leave
      route" (TestUser sole admin; AuthorBeta, ReaderGamma and ModUser left). Reports 9 (closed by
      deletion), 10 and 11 (Group rows). Type-62 notifications 40024 and 40025. Users 8 and 9 were
      created and deleted.
    - **Dev-DB state left (Aspire).** Group 4 "E2E Rescue: email check" (TestUser admin), report 6,
      notification 22, and one Mailpit message. The containers were stopped afterwards.
- **Pointers:** `audit/Identity.md` F52 (incl. the browser verification), `audit/BlogPosts.md` F37,
  `audit/Messaging.md` F49 (incl. the reply-box fix), `audit/Groups.md` F38/F40, `audit/Moderation.md`
  F47, `audit/Notifications.md` (type 62) Stage notes;
  `cross-cutting.md` §"Account deletion"; tracker F17/F18/F19/F20/E11/H29; roadmap decision row 23.

## WU-SchemaHardening — group-folder integrity, `group_stories` as a set, ten model-declared CHECK constraints, the schema §2.7 consistency batch (schema audit §2.4/§2.6/§2.7, service audit §2.10, worksheet D23's `group_stories` row; extends `Groups/`, `Data/`, `Stories/`, `Chapters/`, `Following/`, `Tags/`, `BlogPosts/`, `Moderation/`, `Discovery/`) — DONE ✓ (2026-10-13)

- **Cells:** F39 L4.5 **5→1** (the folder page's delete and refusals changed; no browser was
  available; tracker **H28**), and **1→5** at the browser pass the same day, which closed H28. Every
  other touched cell stays 5: F4/F6/F8/F10/F11/F18/F19/F37/F39/F47/F62
  L1, F31/F37/F39 L2, F39 L3-Logic and L3.5, F62 L8 (F31 L2 by the review fixes). Dated 2026-10-13 (the
  session's `currentDate`, as the two WUs before it), not the template's 2026-09-30; the review fixes
  and the browser pass are dated the same way.
- **Trigger:** the WU spec plus orchestrator amendment (WU-StoryLifecycle's two CHECKs, and fixing every
  test that seeds a published chapter or story without its date).
- **What landed:**
  1. **Migration `WU_SchemaHardening`.** Group-folder self-FK `ON DELETE SET NULL`; the name index
     rebuilt NULLS NOT DISTINCT; unique `ix_group_stories_group_id_story_id` (replaces the FK index);
     the filter overrides' natural PK (surrogate dropped); `site_daily_stats.new_users` NOT NULL; ten
     CHECKs. Hand DML at the top of `Up()` normalizes violating rows. Rehearsed Up/Down/Up on a dev-DB
     clone dirtied with every case; SeedTool re-run on a migrated scratch clone; applied to the
     server-only dev DB.
  2. **CHECK mechanism (moment-1 rule):** `HasCheckConstraint` in the model, lowercase
     `ck_<table>_<rule>`, no re-append entry; a probe showed EF puts the TPT base's CHECK on
     `base_polls` only. The ten: `ck_story_arcs_bounds`, `ck_story_lineages_no_self`,
     `ck_stories_published_date`, `ck_chapters_first_published_date`, `ck_followed_users_no_self`,
     `ck_vouches_no_self`, `ck_fanon_links_no_self`, `ck_group_folders_no_self`, `ck_base_polls_dates`,
     `ck_users_suspension` (one direction).
  3. **Folder integrity** (`ServerGroupWriteService`, new Core `GroupFolderValidations`): validated
     names, byte-wise sibling uniqueness, same-group parents (`KeyNotFoundException`), rename-to-self
     no-op. Delete moves sub-folders to the top level after the existing ones (set-based, so the DB's
     SET NULL runs after the parent row is gone) and refuses a top-level name clash. Race backstops by
     constraint name on create, rename, delete and `AddStoryAsync` (counts as already added: no
     folder, no notification).
  4. **Poll dates:** the close date is checked against the effective open date (create and update) →
     `PollValidationException`, not the CHECK's 500.
  5. **`site_daily_stats`:** `active_users` only for a measurable day (`IsActiveUsersMeasurable`),
     `COALESCE`-preserved on re-upsert; stale backfill comments fixed.
  6. **UI:** the folder page's delete copy, its refusal message, "That parent folder no longer
     exists.", and a reload after every "no longer exists" refusal (`error-handling.md`).
  7. **Docs:** moment 1 — `layer1-data-model.md` (§"CHECK constraints", the §TPT PK-name ratified
     divergence, `CreatedUtc`), `layer2-services.md` (§"Group Folder Integrity", the check-then-act
     row, the Story Lifecycle invariants), `cross-cutting.md`, `layer6-indexes.md`,
     `layer8-data-marts.md`, `audit/Groups.md` Settled, `roadmap.md` Resolved and row 22, worksheet D23
     Built line, tracker D14/H27/H28. Moment 3 — Stage notes and headlines in Groups F39, Stories
     F4/F8/F10, Chapters F6, Following F18/F19, Tags F11, BlogPosts F37, Moderation F47/F62,
     Discovery F31.
- **Left alone:**
  - **Spec-author defaults, flagged:** the folder-delete refusal (not auto-rename) is decision
    **row 22**. Spec C8a (stale suspension dates) was already built by WU-ModerationIntegrity:
    verified, not rebuilt.
  - **D4 descoped:** the twelve TPT `PK_<table>` names cannot be renamed (one EF key per hierarchy;
    the probe renamed all five comment PKs to one name). Recorded as a ratified divergence.
  - **Pending owner rows:** `read_progress` CHECK (D20 bounds; tracker **D14**), case-insensitive
    names (D29), join rigor and a composite same-group FK (D33), the `sort_order` index (always
    measure), the suspension biconditional (§2.1.3(a)).
  - **Declined, with reasons:** the `user_notification_settings` all-NULL CHECK (its writers pass
    through an all-NULL row and no reader depends on it) and counter non-negativity (schema §2.6).
  - `/mod/stats` drawing a NULL day as 0 (tracker **H27**, L4).
  - Service §4's phantom SET-NULL comment is fixed here: WU-DocCorrections skips it.
- **Review fixes (2026-10-13).** Three reviews raised 11 findings, 9 distinct after merging duplicates.
  Each was checked against the code; all 9 were real and none needed an owner ruling.
  1. **A re-toggled discovery setting threw on a circuit (high).** With the natural key,
     `SetOverrideAsync`'s tracked `Add` after its set-based delete collided with the instance the
     first `Add` left tracked in the circuit-scoped write context ("already being tracked"). It is now
     one `INSERT … ON CONFLICT … DO UPDATE`. The general rule is new in `layer2-services.md`
     §"Read-Context Concurrency: Factory Per Method"; tracker **D15** records a pre-existing site of
     the same class (the blog-post like toggle) and the unswept rest.
  2. **Untested paths:** the folder page's rename and reorder "no longer exists" catches, the
     delete-delete race (0 rows → `KeyNotFoundException`) and the blog-post poll create's close-date
     check. Each now has a test that fails with its code reverted.
  3. **A midnight flake:** `SiteDailyStatAggregatorTests` chose its day and the aggregator its
     measurability from two clock readings. `UpsertDayAsync` gained an overload that takes the
     caller's reading; the tests and the worker pass one.
  4. **Records:** the F39 L3-Logic headline and Stage note (the validator and page handlers), this
     entry's Cells line (F11, F39 L3-Logic), `grid_axes.md`'s L1 CHECK clause, the L6 intent ledger's
     `GroupStory` row, and `UserStoryInteractionFilterSettingId` in the retired-term registry.
- **Verification:**
  - `dotnet build`: 0 errors, 33 warnings, none new (baseline diff).
  - `dotnet test`: **Unit 1,182** (+13), **RazorComponents 774** (+4), **Integration 1,456** (+48),
    all green. New Integration classes: `GroupFolderIntegrityTests` (24, five interleaved races) and
    `SchemaConstraintTests` (18, including the catalog inventory). The amendment's seed fixes: 94
    tests in 12 classes inserted published chapters without `FirstPublishedDate`.
  - Mutation-checked, each failing its tests: a tracked EF delete, the sort-order append, the
    same-group parent check, name validation, the `AddStoryAsync` catch, the poll check, the
    `active_users` `CASE` and `COALESCE`, the page's create and delete catches and its copy (rename
    and reorder: the review fixes).
  - The four gates pass. **No browser was available** — tracker **H28**.
  - After the review fixes: build 0 errors, 33 warnings (none in touched files); **Unit 1,182**,
    **RazorComponents 776** (+2), **Integration 1,459** (+3), all green; the four gates pass. Each fix
    was reverted in turn and its tests failed: the `ON CONFLICT` upsert (the old code threw the
    tracking exception), the rename catch's reload and its editor close, the reorder catch's reload,
    the 0-row delete check and the blog-post poll check. No UI behavior changed, so H28's steps stand.
  - **Browser pass (2026-10-13), server-only path, both render phases, `psql` after every write. No
    runtime bug in the WU's code; F39 L4.5 1→5; H28 closed.**
    - Folder page as AuthorAlpha: H28's four steps (delete moves sub-folders up after the top level
      with their stories still filed; the clash refusal deletes nothing; duplicate, padded and
      rename-to-sibling names refused, rename-to-self a no-op; from a stale tab, create under a gone
      parent, rename, move and delete each refuse and reload). Blank, 101-character and
      missing-parent refusals over HTTP (the page disables its buttons for a blank name). A member
      gets the admin gate and 403; anonymous gets 401.
    - Four concurrent story adds: all 204, one `group_stories` row, one fan-out. EF Core still logs
      each caught violation at `fail:`: tracker **E10** (pre-existing class, not changed here).
    - Polls (`/polls` as ModUser, `/blog/3/edit` as AuthorAlpha): a past close date with Opens blank
      on create, and an edit that blanks Opens with a close date before the stored open date, all
      read "The close date must be after the open date." (WASM 400s), with no row written.
    - `/settings` Discovery Defaults toggled on, off, on, off on one circuit and on WASM, `psql` after
      each; `/mod/stats` renders on both phases; LurkerDelta suspended, reinstated, suspended, banned
      and reinstated (the date clears each time); the self-follow, self-vouch, inverted-arc,
      self-lineage and self-fanon-link requests answer 400 from their service guards.
    - Tests after the pass (no code changed): build 0 errors, 31 warnings; **Unit 1,182**,
      **RazorComponents 776**, **Integration 1,459**, all green; the four gates pass.
    - **Dev-DB state it left** (server-only DB): the folders and the group story it made were
      removed through the UI, so group 1's folders and story list are as seeded. Left: site poll 1
      ("Verifier poll: past close date", ModUser, opens 2026-12-01); notifications 40018–40019 (type 60
      for the removed group story) and 40020–40022 (LurkerDelta's account actions); LurkerDelta's
      account-action history (Suspend, Reinstate, Suspend, Ban, Reinstate; Active at the end).
- **Pointers:** `layer2-services.md` §"Group Folder Integrity" and §"Read-Context Concurrency: Factory
  Per Method"; `layer1-data-model.md` §"CHECK constraints"; `audit/Groups.md` F39 Stage note and its
  browser verification note; `audit/Discovery.md` F31's WU-SchemaHardening note; `roadmap.md` §Resolved
  and row 22; tracker D15, E10.

---

## WU-NotificationCorrectness — `UserNotificationSetting` sparse per field (worksheet D19), mail restores only the unsent and drains to empty, feed tiebreak, one-save `StartConversationAsync`, subject cap (service audit §2.8; extends `Notifications/`, `Email/`, `Messaging/`, `Spotlight/`, `SeedTool/`) — DONE ✓ (2026-10-12)

- **Cells:** the build flipped none. F41/F42/F43 L1/L2, F49 L2 and F55 L2 stay 5: defects beneath
  sound cells. The review fixes (2026-10-13) flipped **F42, F43 and F49 L4.5 5→1** (UI behaviour
  changed, no browser, tracker H26); the build had kept F42 at 5 as a "copy-only change". The
  browser pass (2026-10-13) set all three back to 5 and closed H26.
  Migration `WU_NotificationCorrectness_SparseSettingFields` is applied to the dev DB, and to the
  Aspire DB on the browser pass's startup. Dated 2026-10-12 (the session's `currentDate`, per
  CLAUDE.md and WU-DependencyCVEs' precedent), not the template's 2026-09-30; the review fixes and
  the browser pass ran on 2026-10-13.
- **Trigger:** worksheet D19 (answered 2026-08-07); service audit §2.8's items not owned by
  WU-InertFeatures; amendment U8. It also took the 90–92 presenter arms WU-InertFeatures routed here.
- **What landed:**
  1. **D19.** `EmailEnabled`/`Collapsed` are `bool?`, sparse per field. Up canonicalizes existing
     rows; Down backfills the defaults. Both ran on a populated dev-DB clone. `NotificationSettingUpsert`
     owns the rule: NULL when a field equals its default, delete when both are NULL, and unsubscribe
     writes only `email_enabled`. Three per-field read sites.
     `NotificationSettingDto.IsDefault` → `EmailIsDefault`/`CollapsedIsDefault` (registered as a
     retired term). No UI, endpoint or client change. SeedTool emits only canonical rows (COPY
     writes NULLs) and keeps the RNG stream unchanged.
  2. **D23 applied (flagged: from the promulgated test, not a new owner answer).** Both settings
     writers are one `INSERT … ON CONFLICT DO UPDATE` of their own columns. Two concurrent unsubscribe
     POSTs used to 500. Notification dedup is recorded as an advisory policy (doc only).
  3. **Mail.** `SmtpMailTransport.IsPerMessageFailure`: per-message only for a 5xx while still
     connected, or a compose failure. Everything else throws the new
     `MailBatchInterruptedException(ProcessedCount)`. The flusher restores only the unsent tail;
     cancellation restores and rethrows the OCE. `DrainAllAsync` plus the worker's 15 s
     `ShutdownDrainBudget` drain to empty and log what was stranded. Identity's `SendAsync` unwraps.
  4. **Feed:** `OldestUnreadFirst` ends in `NotificationId`. No DDL; EXPLAIN shows an Incremental Sort.
  5. **Messaging:** `StartConversationAsync` is one save with one timestamp. The subject is capped
     at `MessagingConstants.MaxSubjectLength` (2048, the existing column cap) → 400.
  6. **Found during the build:** the Spotlight go-live sweep passed a stand-in source `0` for a
     deleted sponsor. The FK refused it, so the placement never stamped and nobody was notified. It
     now passes the null (`int? sponsorUserId`).
  7. **Presenter:** arms for 90–92 and 100, which used to read "You have a new notification".
  8. **Docs:**
     - moment 1: `layer2-services.md` §Filtering semantics, §Email fan-out (restore rules, shutdown,
       transport failure contract), §Signal Buffering worker, dedup, the check-then-act family table,
       the deleted-actor rule, the single-field discovery mirror; `layer6-indexes.md`;
       `audit/Notifications.md` Settled; `roadmap.md` Resolved (D19); service-audit §2.8/§3.17/§4
       row 7; worksheet D19 Built line; tracker D13 (the paged-ordering sweep).
     - moment 2: `testing.md` §"Unit of work, partial sends, drains and tiebreaks".
     - moment 3: Stage notes in `audit/Notifications.md` (incl. the F42/F43 L3 headlines that still
       read Stage 2), `Messaging.md` F49 and `Spotlight.md` F55; tracker H26 and D8.
- **Left alone:**
  - Tracker **D8** (blog titles after the author goes Private), routed here by WU-AccessGateSweep2:
    not built, re-scoped. Its one-line fix would blank the author's own type-33 notifications, and
    an unpublished post leaks the same way. The review fixes routed it to **WU-TrackerFixes** and
    hold type 100 title-free until it is built.
  - The other `Skip/Take` orderings (tracker **D13**, audit before the cross-cutting build).
  - The PM email / `(kind,id)` buffer (WU-PrivateMessageEmail builds on this flusher shape and must
    keep the partial-restore and drain-to-empty rules).
  - Lineage/acknowledgment re-request vectors (WU-ThrottleCoverage).
  - Service §4 rows 3/4 and the write-service DAG paragraph (WU-DocCorrections).
  - The CHECK `email_enabled IS NOT NULL OR collapsed IS NOT NULL` (WU-SchemaHardening's call).
  - The three-state default/on/off control (D19's accepted cost).
- **Review fixes (2026-10-13).** Three reviews raised 14 findings, 11 distinct after merging
  duplicates. Each was checked against the code and the owner's text. All 11 were real (one detail
  was not: the presenter's text reaches the email's lead line, not its subject). None needed an
  owner ruling.
  1. **One deferred recipient stalled all notification email (high).** A 4xx to `RCPT TO` while
     connected (452 over quota, 450/451 greylisting) aborted the batch. The restored mail led the
     retried tail and aborted every later batch, with no retry cap, and each shutdown drain stopped
     at it. It is now a third transport class, the **deferral** (`SmtpMailTransport.IsDeferral`): the
     batch goes on, and `SendBatchAsync` returns the deferred indices (`DeferredIndices` on an abort).
     The flusher requeues them through `NotificationEmailBuffer.Defer`, which drops a mail at its
     10th deferral (counted failed, logged at Error). `DrainAllAsync` stops at a batch that settled
     nothing. A 421 and a 4xx to `MAIL FROM`/`DATA` still abort. Identity's single path drops a
     deferral, as before the WU. Spec R5's "4xx aborts" is superseded in `layer2-services.md`.
  2. **Presenter.** 90 is actor-free: naming the moderator answered the unruled decision row 19 in
     the bell, the inbox and the default-on email. Row 19 is annotated. 100 is title-free until
     tracker D8 is built: the poll-edit sweep notifies the voters of a post that has since gone
     Private or been unpublished. D8 is routed to WU-TrackerFixes.
  3. **Unsubscribe for a deleted account** (365-day token, hard delete) tripped the FK into a 500.
     The insert is now `INSERT … SELECT … WHERE EXISTS`, and the endpoint answers the
     invalid-link page (400).
  4. **F42/F43/F49 L4.5 5→1.** The "copy-only" precedent the build cited had a browser pass behind
     it.
  5. **Tests and records.**
     - New tests: the feed's per-field `Collapsed` against a flipped default with a row present, and
       the SeedTool re-seed (scratch DB: 444 rows, none all-NULL, none equal to a default).
     - Stamps: two code stamps 2026-09-30 → 2026-10-12; Spotlight F55 now counts 21 tests.
     - Docs: an `audit/Identity.md` F1 L2 transport note; WU-QuickFixes archived (`workplan.md` was
       1,641 lines; every other DONE entry is under two weeks old).
- **Verification:**
  - `dotnet build`: 0 errors, 32 warnings (none in touched files).
  - `dotnet test`: **Unit 1,157** (+67), **RazorComponents 770** (unchanged), **Integration 1,401**
    (+18), all green. The four gates pass.
  - Every fix-specific test was mutation-checked: each fails with its fix reverted (two batches plus
    both directions of the email coalesce).
  - After the review fixes: build 0 errors, 32 warnings; **Unit 1,169** (+12), **RazorComponents
    770**, **Integration 1,408** (+7), all green; the four gates pass. Each fix was reverted in turn
    and its tests failed: the flusher's success-path and abort-path requeues, the drain's settle
    check, the deferral-count reset, `IsDeferral`, the conditional insert and the feed coalesce.
  - **Browser:** the build and the review fixes ran with no browser available (tracker **H26**).
    **The browser pass (2026-10-13) closed H26. F42/F43/F49 L4.5 went 1→5 and no runtime bug was
    found.**
    - Path and evidence: the Aspire path, with mail in Mailpit; the circuit and WASM phases (proved
      from the network log); `psql` after every write; the server log clean of `fail:`/`crit:`
      except the documented stale-antiforgery line; no console errors.
    - Users: AdminUser granted the slot. TestUser booked it, voted, ran the settings page and the
      compose modal, and unsubscribed from Poll Updated. AuthorAlpha (story author, poll owner) and
      ReaderGamma (recommender) checked their copy. AuthorAlpha used the Mailpit unsubscribe. A
      tampered token was posted anonymously.
    - Flows: the 90/91/92/100 copy in the bell, the inbox and the mail lead lines, with each subject
      the type's display name; per-field rows for Collapsed-only and Email-only toggles; the
      unsubscribe writing `email_enabled` alone; a default-off type's override deleted; the
      2,049-character subject refused (WASM 400, no row); a trimmed 2,048-character subject stored.
    - The one change was a misplaced XML doc comment in `NotificationEmailEndpoints.cs`. After it:
      build 0 errors, 32 warnings; Unit 1,169, RazorComponents 770, Integration 1,408, all green;
      the four gates pass.
    - **Aspire dev-DB state left (`canalavedb`, port 5433):**
      - slot 2 (TestUser, redeemed) and placement 2 (story 3, sponsor TestUser, ReaderGamma's
        recommendation 2, live Oct 12–19; 10 grants left this month);
      - unread notifications 18–21;
      - poll 1 "Seed poll: favourite region" on blog post 2 (options Sinnoh / "Johto (Gen II)", one
        TestUser vote), its `last_edited_at` moved back 31 minutes through `psql` to skip the quiet
        period;
      - AuthorAlpha's Story Spotlighted override `(email off, collapsed on)`;
      - conversation 2 (TestUser → AuthorAlpha, a 2,048-character subject, message 4).

      Mailpit's mail went with the container stop. The server-only dev DB was not touched.
- **Pointers:** `audit/Notifications.md` §"WU-NotificationCorrectness Stage note", its review-fixes
  note and its browser verification note; `audit/Identity.md` F1 L2 transport note;
  `audit/Messaging.md` F49; `audit/Spotlight.md` F55; `layer2-services.md` §"Notification
  Generation" / §"Email fan-out".

---

## WU-DependencyCVEs — the 2026-10-12 NuGet advisory sweep: Crypto.Xml, SSH.NET and AngleSharp patched, tracker E2 closed, ImageSharp held as accepted risk pending the owner (tracker E9) (cross-cutting; extends `RichText/`, `Export/`, `Import/`, `Images/`, the csproj set) — DONE ✓ (2026-10-12)

- **Cells:** none flipped. The work sits beneath Stage-5 cells (F6 L2 sanitizer, F54 L2, F63 L2,
  F4/F20 L2 via `Images/`) or has no cell (E2). No migration.
- **Trigger:** the 2026-10-12 resume build reported NuGet advisories; warnings had grown 93 → 125
  since the campaign started. Orchestrator amendment §"WU-DependencyCVEs". This WU took over E2
  from WU-TrackerFixes.
- **What landed:**
  1. **`System.Security.Cryptography.Xml` 10.0.9 → 10.0.12** (GHSA-23rf/-8q5v/-cvvh/-g8r8/-mmjf,
     high). Only Core's graph needs it: legacy `Microsoft.AspNetCore.Identity` 2.3.11 pulls it in
     through `DataProtection` 2.3.0. Core keeps the one pin, with a why-comment. The redundant direct
     pins in SharedUI, Client and AppHost were deleted: SharedUI and Client inherit Core's, and
     AppHost's graph has no consumer.
  2. **`SSH.NET` pinned to 2026.0.0** in Tests.Integration (test-only; Testcontainers 4.13's
     transitive 2025.1.0 had two high `ScpClient` advisories, a path Testcontainers never uses).
  3. **E2: `HtmlSanitizer` 9.0.892 → 9.2.1039, direct `AngleSharp` 0.17.1 → 1.8.4**, and the
     bUnit-transitive 1.4.0 in Tests.RazorComponents pinned to 1.8.4. This was the zero-code path:
     no compile break, no serializer diff.
     - **Why 1.8.4, not the 1.7.2 floor the earlier scoping proposed:** AngleSharp's repository
       advisory GHSA-cgp3-27rh-pcp2 (2026-09-19; SVG `<style>` serialization mXSS) covers
       1.3.1–1.8.1. It is not yet in the reviewed database the scan reads.
     - The pin rule changed from "exactly the floor" to "inside the sanitizer's declared range,
       newest patched 1.x, every bump gated by the sanitizer tests" (Doc-Touch moment 2,
       `security.md` pin rules).
  4. **ImageSharp 3.1.12 kept as accepted risk** (five 2026-10-07 advisories). Both patched lines
     (3.2.0 and 4.1.2+) now run Six Labors' build-time license check, and a Release build fails
     without a key. That fired the old "revisit if 3.1.x stops getting security patches" trigger.
     Every vector is unreachable through `ImageUploadProcessor`, and the analysis per advisory is in
     `security.md`. **Filed tracker E9** for the owner's choice: a key, a library swap or standing
     risk (promoted to decision row 21 by the review fixes).
  5. **Docs:**
     - `security.md`: pin rules, the E2 resolution, the sweep table, the ImageSharp accepted risk, a
       pipeline rule, and a patched-runtime item in the Phase-7 register;
     - `layer2-services.md`: the allow-list's mXSS paragraph;
     - the comment in each touched csproj;
     - the `dependabot.yml` comments;
     - tracker E2 ticked, E3 extended, E9 filed;
     - `roadmap.md`: the Phase-7 AngleSharp bullet struck and an E9 bullet added.
- **Left alone:**
  - the ImageSharp version (owner-open, E9);
  - removing the legacy `Microsoft.AspNetCore.Identity` 2.3.11 from Core, the root of the Crypto.Xml
    chain (out of scope);
  - bumping bUnit or Testcontainers instead of pinning (the amendment asked for pins);
  - the host runtime, which is a deploy-time item (Phase-7 register, tracker E3). The dev machine
    runs 10.0.11, already patched.
  - The planned AngleSharp attribute-escaping test was dropped: AngleSharp, through 1.8.4, still
    writes `<`/`>` raw inside serialized attribute values. The 1.5.0 fix never covered that half
    of the advisory text; the allow-list covers it.
- **Review fixes (2026-10-12).** Three reviews raised 10 findings, 7 distinct after merging
  duplicates. Each was checked against the code, the advisory databases and the roadmap rule; all 7
  were real, and none needed an owner ruling. Docs and csproj comments only; no code or test changed.
  1. **E9 promoted to `roadmap.md` decision row 21.** The roadmap's rule sends a tracker item that is
     a genuine open decision to the table; the WU had left it as "not a numbered decision row". The
     header sentence, the Phase-7 bullet, the Position "Blocked on Brian" line and E9's tag
     (`decision`) now say so; group F's intro points at it.
  2. **Only ImageSharp 4.1.2+ clears the scan.** The GitHub-reviewed ranges for all five advisories
     run to `<= 4.1.1` (first patched 4.1.2), so 3.2.x carries the fixes but stays listed and E3's
     hard gate stays blocked. Stated in E9 option (a), row 21, the Phase-7 bullet, E3 and
     `security.md` (library paragraph, accepted-risk entry).
  3. **The deployed Server never uses the Crypto.Xml package.** .NET 10 prunes it in favour of the
     ASP.NET Core shared framework's copy (the Server's `project.assets.json` has no such package), so
     in production all five Crypto.Xml advisories close only on a patched host runtime (≥ 10.0.10),
     not just GHSA-cvvh. Corrected in `security.md` (sweep row, runtime note, Phase-7 register item),
     tracker E3 and Core's csproj comment.
  4. **The SSH.NET pin's removal trigger is Testcontainers 4.14+**, not 4.16+: 4.14.0's nuspec
     already depends on SSH.NET 2026.0.0. Corrected in the Tests.Integration csproj comment and the
     `security.md` sweep row.
  5. **`testing.md` gained the Unit-tier rule** for process-wide measurements
     (`AllocationSensitiveCollection`) and hang regressions (`Task.Run` + `WaitAsync` deadline),
     Doc-Touch moment 2.
  6. **Counts and wording.** `audit/Chapters.md` F6 L2: `HtmlSanitizationServiceTests` is 37 (+6;
     31 before), not 36/30. The environment clause in `audit/ImageStorage.md` and this entry is
     gone.
- **Verification:**
  - `dotnet list ... --vulnerable --include-transitive` lists only ImageSharp (E9).
  - Build: 0 errors, 33 warnings (was 125): the three pre-existing C# warnings plus the ImageSharp
    NU190x lines.
  - `dotnet test`: Unit 1,089 (+8), RazorComponents 770, Integration 1,380, all green. The four
    gates pass.
  - After the review fixes (docs and csproj comments only): build 0 errors, 33 warnings; Unit 1,089,
    RazorComponents 770, Integration 1,380, all green; the four gates pass.
  - New Unit tests:
    - `HtmlSanitizationServiceTests` +6: both advisories' payloads in a 4-case theory, plus one
      parser canary per advisory;
    - `ImageUploadProcessorAdvisoryTests` (2, non-parallel collection): the BigTIFF IFD-loop PoC is
      rejected at the sniff under a deadline; the truncated-CLUT ICC PoC passes through as raw
      bytes, < 48 MB allocated (measured ≈ 130 KB).
  - Mutation-checked:
    - back on 0.17.1, the pgww canary fails;
    - on 1.7.2, the cgp3 canary fails;
    - a TIFF module in the sniff reproduces the advisory's hang, and the test fails on its deadline;
    - reading `IccProfile.Entries` allocates 114.9 MB and fails.
  - No browser pass was owed (no UI changed, and ImageSharp did not change version), but the
    orchestrator ran one the same day ("Browser verification" below). Its fix brought `dotnet test`
    to Unit 1,090, RazorComponents 770, Integration 1,383, all green; the four gates pass; build 0
    errors, the same three C# warnings.
- **Browser verification (2026-10-12).** Server-only path, Local provider, on the standing workbench
  (not reset), `psql` after every write. The phase was read from the network log
  (`_blazor/negotiate` against the action's `/api` call), with the circuit forced by removing the
  Auto-mode localStorage hash. Every `onerror` payload bumped a `window.__pwned` counter instead of
  opening a dialog; it stayed 0 throughout. GIF: `e2e-WU-DependencyCVEs.gif` (the primary flow:
  payload import, preview, commit, publish, read, comment).
  - **Sanitizer on AngleSharp 1.8.4:** one HTML document carrying both advisories' payloads went
    through AuthorAlpha's one-document import (circuit). The two drafts and the stored chapters 5 and 6
    of story 2 kept the allowed formatting and no vector. Chapter 5, published on WASM, read clean
    as ReaderGamma on both phases. ReaderGamma's WASM comment had its `<img>` stripped. A Markdown
    "into editor" import and save (circuit) stored the payloads as escaped text.
  - **Import readers:** an EPUB round trip on WASM (story 2's export into story 10, `POST
    /api/content-import/epub`) kept the body exactly, apart from the EPUB's own chapter heading. The
    one-document re-split picker worked on the circuit, and a broken `.epub` read "This file isn't an
    EPUB (not a valid archive)." AuthorBeta was refused on the edit
    page and with 403 over HTTP; an anonymous parse got 401.
  - **Export writers:** all six formats of story 2, downloaded anonymously, answered 200 with intact
    formatting and no vector. A Draft, a pending, an M-rated and an unpublished chapter answered 404.
  - **Client bundle:** with Core's Crypto.Xml pin inherited, the WASM runtime loaded
    `System.Security.Cryptography.Xml` and every WASM action above ran. Sign-in (dev login) worked
    for all three users the pass drove (AuthorAlpha, AuthorBeta, ReaderGamma).
  - **Bug fixed (commit "browser-pass fixes"):** an upload refusal never read on either phase. The
    BigTIFF PoC avatar showed the generic error and logged `fail:` on the circuit, and "… (Parameter
    'buffer')" on WASM. The pipeline now throws `ImageUploadValidationException` with user-written
    text (`security.md` §"Upload Content Pipeline" Rules). Re-verified: the BigTIFF avatar and a
    non-image cover read "This file isn't a JPEG, PNG or WebP image." on both phases, nothing was
    written, and no `fail:` was logged. A valid PNG avatar still stored and served. Tests:
    - Unit `ImageUploadProcessorTests` +1;
    - Integration `ImageUploadEndpointsTests` (3);
    - updated assertions in the advisory and telemetry tests.

    Mutation-checked: putting the `ArgumentException` back fails 5 Unit and 3 Integration tests.
  - **Filed, not this WU's code:** **H25**. "Replace editor content" mangles imported blockquotes
    and whitespace (Blazored's raw-`innerHTML` load), and saving a draft chapter lands on "Page not
    found".
  - **Not driven:** a moderator surface (this WU touches none); keyboard-only and screen-reader
    passes (owner's, as before).
  - **Dev-DB state left behind** (server-only workbench; `reset-dev-db.ps1` drops it):
    - story 2: chapter 5 "Probe Chapter One" (id 19, published; `Story.WordCount` 90 → 114) and
      chapter 6 "Probe Chapter Two" (id 20, draft), both sanitized payload probes;
    - ReaderGamma's comment on chapter 5 (comment 6);
    - story 10 (AuthorAlpha's Draft): chapter 1 (id 21, draft; content 24), created by the EPUB
      round-trip import and then overwritten in place by the Markdown "into editor" import and save;
    - AuthorBeta's avatar: `/uploads/users/5/profile-{uuid}.png` (a 64×64 green-and-white PNG).
  - Narrative: the browser notes in `audit/ImageStorage.md` L2, `audit/Chapters.md` F6 L2,
    `audit/Import.md` F63 and `audit/Export.md` F54.
- **Hand-offs:**
  - **WU-TrackerFixes:** skip its §1 C / E2 entirely, including the §5 sanitizer tests (landed here,
    minus the dropped href-escaping test).
  - `AllocationSensitiveCollection` (Tests.Unit) is reusable by any test that measures process-wide
    allocation (rule in `testing.md`, added by the review fixes).
- **Pointers:**
  - `security.md` §"Upload Content Pipeline" and §"Dependency Vulnerability Scan Cadence";
  - the Stage notes in `audit/Chapters.md` F6 L2, `audit/Export.md` F54, `audit/Import.md` F63 and
    `audit/ImageStorage.md` L2;
  - tracker E2/E3/E9, and H25 (from the browser pass).

## WU-CounterSymmetry — counter & concurrency doctrine: every counter derived and reconciled, post-commit counters, check-then-act hardened per family, creating a group counts as joining (worksheet D21–D24; extends `Profiles/`, `Groups/`, `Following/`, `UserStoryInteractions/`, `Chapters/`, `Comments/`, `Recommendations/`, `Moderation/`, `Badges/`, `Stories/`, `Users/`) — DONE ✓ (2026-09-30)

- **Cells:** **F50 L4.5 5→1** (the badge display filter and `UserCard`'s count guard, no browser
  available — tracker **H23**), back to 5 after the browser pass the same day ("Browser verification"
  below). Everything else lands beneath Stage-5 cells: F58 L2 (the reconciler),
  F22 L2, F38 L2, F6 L2, F16 L2, F19 L2, F25 L2, F28 L2, F10 L2, F47 L2, F62 L8, F50 L2/L3.5. No
  migration. Review fixes, all beneath Stage 5: F16 L2, F19 L2, F50 L2, F58 L2. Browser-pass fix,
  beneath Stage 5: F10 L3-Logic (`MyStoryLineagesPage`).
- **Trigger:** owner rulings D21 (every counter derived, recompute in code), D22 (post-commit,
  recompute-corrected), D23 (check-then-act posture per family) and D24 (creating counts as joining);
  service audit §2.4.2/§2.4.3/§2.4.6, schema §3.6. Orchestrator amendment U1 (lineage double approve).
- **What landed:**
  1. **D24 + D23 (`group_members`).** `CreateGroupAsync` adds the creator's `GroupsJoined` +1 after
     their member row saves. `JoinAsync` is one `INSERT … ON CONFLICT DO NOTHING`, and its `+1` runs
     only when that insert landed a row.
  2. **D23 (vouches, USI).** The vouch insert is an `ON CONFLICT` upsert, and only the vouch that
     landed notifies. The pre-check is kept, so a re-vouch at the limit is still a no-op. All four USI
     create sites (the fourth is `SetReadItLaterFromRecommendationAsync`) and the date partition go
     through `EnsureRowAsync` / `EnsureDatePartitionAsync`: an `ON CONFLICT` insert, then a tracked
     re-read, with the `was*` captures taken from that row. The ensure step runs after the D6 guard,
     and the tracker is never cleared.
  3. **D22.** `Chapter.VersionCount++` is now a post-commit `ExecuteUpdateAsync`.
  4. **§2.4.3.** `Story.WordCount` / `WordsWritten` count published chapters only, and
     `SetPublishedAsync` refreshes them.
  5. **§2.4.6.** The comment and recommendation like toggles return the re-read landed count; the
     recommendation one was the unnamed third sibling. `site_daily_stats.total_words` takes
     `total_stories`' visibility predicate.
  6. **D21.** The new `ContentCounterRecalculator` (`Server/Profiles/`) has 11 specs: `like_count` ×3,
     `successful_rec_count`, `version_count`, `stories.word_count` and `active_report_count` ×5. The
     worker runs it before `UserStatRecalculator`, each pass with its own try/log. It emits a
     `ContentCounterRecalc.Pass` span on the existing source.
  7. **D21's zero-count badges.** `SiteBadges.CounterColumnByBadge` is the single source.
     `BadgeCounterSpecs` derives from it, and all 11 display projections drop a 0-count counter-backed
     badge. Curation is unfiltered. `UserCard` hides a 0 count.
  8. **U1.** `ApproveLineageAsync` is Pending-only, as a conditional update; anything else is a 400.
  9. **Seeds.** `SeedGraph` sums published chapters only. `DataSeeder` gives TestUser's Recommender
     badge real ground truth (one success, count 1).
  10. **Comments.**
      - The counter citations now point at `layer2-services.md`.
      - "Same transaction" is now "post-commit" in the code and in four test comments.
      - `IChapterWriteService`, `IGroupWriteService` and `IStoryLineageWriteService` docs updated.
      - `RecordSuccessAsync`'s false "UserStat row on registration" corrected.
  11. **Docs, moment 1.** `layer2-services.md`:
      - §"UserStats Updates" rewritten (D22; spec §9.4 superseded);
      - new §"Counter recompute principle" and §"Check-then-act posture" (classification test, family
        table, stated-soft limits, advisory-lock bar, the ensure-row mechanism);
      - extended §"Counter mutation rule", the counter map, the worker section (direction of
        authority, order) and §"Word Count";
      - the `ApprovedStorySubmissions` statement reused, not rewritten.

      Elsewhere: `layer8-data-marts.md` `total_words`; `logging.md`; `testing.md` (the convergence
      rule, the upsert interleave); audit Settled notes in Groups F38 and Profiles F22; roadmap
      §Resolved; worksheet Built lines; `grid_axes.md` F58 scope.
- **Left alone (and why):**
  - **Routed elsewhere:** `group_stories` unique → WU-SchemaHardening; the poll single-choice
    restructure + `ConfigLocked` → WU-PollVoteIntegrity.
  - **Pending worksheet rows:** group deletion's `GroupsJoined` fall (D47(b)), recorded as the
    conditional sixth case only; the StoriesInProgress formula (D41) unchanged.
  - **Not ruled, not changed:** `BlogPostsWritten` stays unchanged (tracker F16, owner-open); the
    blog-like SQL clamp vs the unclamped siblings (an asymmetry with no ruling); non-negativity
    CHECKs (schema §2.6 excludes them).
  - **Already done:** the report-submit reorder (WU-ModerationIntegrity had already landed it);
    the USI "reject impossible combinations" comment (already corrected).
  - **Unrouted crash sites → new tracker D11.** These are the sibling create-create and delete-delete
    500s D23 did not route: `FollowAsync`, the like inserts and unlikes, `CustomList`/`Series` adds,
    `LeaveAsync`/`UnfollowAsync`/`RemoveVouchAsync`, the USI sparse cleanup, and the attribution row.
  - **Not owner-open:** the `ApprovedStorySubmissions` question. Per the amendment, the orchestrator
    flags the D1/D21 tension to the owner separately.
  - **Owner visibility (review fixes):**
    - The stated-soft gem, highlight and vouch limits rest on reading D21's silence as D23's
      conditional decline. That is an inference, not a ruling. It is flagged in `roadmap.md` §Resolved,
      so the owner can fund a corrective instead.
    - `UserCard`'s hidden "0" changes manual-grant display. It is a spec rider outside D21's scope, and
      the owner confirms it in **H23** step 2.
- **Found while building:**
  - `Reference(...).LoadAsync()` is a no-op on a navigation an `Include` already marked loaded, so the
    partition ensure needs a tracked query. The rule is in layer2 §"Check-then-act posture".
  - The like toggles' concurrency fix *is* automatable by interleaving another user's SQL, contrary
    to the WU-CounterAtomicity notes. Both are now tested.
- **Review fixes (2026-09-30).** Three reviews raised 14 findings. Each was checked against the code
  and the worksheet; all 14 were real, and none needed an owner ruling.
  1. **The USI ensure re-read could answer 401.** The same user's all-false panel write can
     sparse-delete the just-ensured row before its re-read. `FirstAsync`'s `InvalidOperationException`
     maps to 401 in `EndpointHelpers`. Now `FirstOrDefaultAsync` runs the insert and re-read once more,
     then throws `DbUpdateConcurrencyException`. The partition fallback was mis-commented: a vanished
     partition means its parent is gone, so it throws too. The update-vs-delete race joins **D11**.
     The "parent and attribution in one save" claims are corrected (the class doc, two comments, layer2
     §"Attribution").
  2. **Docs.**
     - WU15's sparse-semantics settled bullet carries a dated amendment. The "next write's sparse
       cleanup removes it" claim is corrected in layer2, F16 and `EnsureRowAsync`'s doc.
     - layer2 "Direction of authority" names `BlogPostsWritten` (**F16**) as the one open permanent
       disagreement, and tracker F16 notes that narrowing the recompute redefines the question.
     - The stated-soft write-up is an inference and an analogy, not a D21 ruling (owner-visible, see
       Left alone).
     - Built banners: service audit §2.1.4, §2.4 (2.4.2/3/6 built, 2.4.5 dissolved), the 2.4.4
       reconciler line, §3 items 7/14/15, §4 row 1, §7 item 8; schema audit §3.6. layer2's
       `ActiveReportCount` and the Moderation settled note no longer say the reconciler is pending.
     - Badges F50 L3.5 headline; the `UserCard` rider labelled spec-sourced, with an owner check in H23.
     - `testing.md`'s convergence rule is scoped to sequential tests; interleaved race tests are exempt.
  3. **One shared badge predicate.** The 11 sites re-typed one lambda. `SiteBadges.IsDisplayed` is now
     the single expression, applied with `.AsQueryable().Where(...)`.
  4. **Owed tests.**
     - Unit `UserStatRecalculationWorkerTests` (2): pass order, per-pass isolation, shutdown. The two
       passes run through the new public seam `RunPassesAsync`.
     - Integration `FollowingWriteServiceTests` +1: a vouch that lands notifies once, and a repeat does
       not.
     - `BadgeDisplayTests` +4: user search, following, outgoing vouches, the tree-search author card.
     - `CounterSymmetryTests` +2 (USI re-read retry, partition vanish). The interceptor gains
       `fireOnMatch`.
     - `ApproveLineage_Twice` and the draft word-count test gain convergence passes. The
       VersionCount test is renamed to `…Count3…`.
     - Mutation-checked: the single attempt, the inverted vouch gate, the swapped worker order, and the
       predicate dropped at two sites each fail their tests.
  5. **Not changed:** the `UserCard` rider stays (owner check in H23). Seeds have no test tier (stated
     in F58). No cell flips. No browser was available, so H23 gains the owner check only.
- **Verification:** `dotnet build` 0 errors, no new warnings in touched files; `has-pending-model-changes`
  clean. `dotnet test` all green: Unit 1,079 (+0), RazorComponents 766 (+2), Integration 1,373 (+28);
  after the review fixes Unit 1,081, RazorComponents 766, Integration 1,380. The four gates pass. New
  Integration tests:
  - `CounterSymmetryTests` (13: join, vouch, six USI, VersionCount, both like toggles, two lineage; 11
    interleaved per `testing.md`, plus two sequential guards, the partition creation and the double
    approve; +2 interleaved at the review fixes);
  - `ContentCounterRecalculatorTests` (3: all 11 specs corrected and zeroed, exactly 22 corrections, a
    second pass corrects 0);
  - `BadgeDisplayTests` (3; +4 at the review fixes);
  - `GroupServiceTests` +5 and `ChapterWriteServiceTests` +4, each ending in a convergence pass that
    corrects 0.

  Also: `SiteDailyStatAggregatorTests` tightened to exact totals; two broken word-count tests now
  publish first; bUnit `UserCardTests` +2. Mutation-checked (every reverted fix fails its tests): both
  `ON CONFLICT` USI inserts, the join and vouch `ON CONFLICT`, the create `+1`, the `IsPublished`
  filter, the `++`, both re-reads, the `total_words` predicate, the lineage `Pending` predicate, and
  the badge filter. WU-CounterSymmetry (2026-09-30) ran with no browser available (H23). The browser
  pass the same day drove every change on both render phases ("Browser verification" below); its fix
  added bUnit `MyStoryLineagesPageTests` (4), so `dotnet test` reads Unit 1,081, RazorComponents 770,
  Integration 1,380, and the four gates pass.
- **Dev DB:** no migration. The first nightly reconciler pass rewrites drifted values: seeded Draft
  stories' `word_count` drops to 0. The `DataSeeder` badge change needs `reset-dev-db.ps1`.
- **Browser verification (2026-09-30).** Server-only path on a reset workbench, `psql` after every
  write; the phase was read from the network log (`_blazor/negotiate` against the action's `/api`
  call), with the circuit forced by removing the Auto-mode localStorage hash.
  - **H23, both phases:** the counted badge ("1", "Recommender (1)"), a `psql` Patron grant (no count,
    "Patron"), and Recommender at `earned_count` 0 gone from every UI and API display surface while
    `/settings` still lists it; signed in and anonymous. F50 L4.5 is back to 5; H23 stays open only for
    the owner's rider call. GIF: `e2e-WU-CounterSymmetry.gif`.
  - **Every other change:** create counts as joining, leave and a double join (one row, +1 once);
    the circuit vouch and three concurrent HTTP vouches (one row, one notification); USI ensure-row
    on a favorite, a Read It Later from a card and the 90% start; publish/unpublish word counts, a
    draft adding nothing, `VersionCount` on alternates; both like toggles returning the landed count
    against a `psql`-landed concurrent like; the stale-tab and concurrent lineage approves;
    `/mod/stats` total words 627.
  - **The nightly worker, live:** started with `Marts__RebuildHourUtc=1` (configuration only), its
    01:00 UTC pass corrected exactly the nine content counters `psql` had drifted, then inserted 4
    missing `user_stats` rows and corrected 26 values. AuthorBeta's `words_written` came out at 180, not
    the 10,143 it would read had the UserStat pass run first, and TestUser's zeroed Recommender came
    back to 1 (`audit/Profiles.md` F58).
  - **Bug fixed (commit "browser-pass fixes"):** after a refused stale approve, `/story-lineages` kept
    the row, whose Reject would have revoked the other tab's approval. The handlers now reload after a
    "moved under the page" refusal (`error-handling.md`); bUnit `MyStoryLineagesPageTests` (4).
  - **Filed, not this WU's code:** **D12** (a circuit double-click on Join Group shares the scoped
    write context → the generic toast; the data stays right) and **H24** (a fresh workbench's
    hand-set seed counters are wrong until the first nightly pass).
  - **Dev-DB state left behind** (server-only workbench, reset at the start of the pass):
    - a `psql` Patron grant on TestUser (`earned_count` 0, `display_order` 2);
    - group 4 "E2E Counter Group A", whose creator AuthorAlpha left and rejoined, so it now has one
      Member and no Admin;
    - AuthorBeta follows and vouches for AuthorAlpha (two type-32 notifications);
    - on story 2: an alternate version of chapters 1 and 3, and a 13-word draft chapter 4 ("E2E Draft
      Chapter");
    - comment 1's likes from ModUser, AuthorBeta and (via `psql`) LurkerDelta, and LurkerDelta's
      `psql` like on recommendation 1;
    - TestUser's favorite on story 6; Read It Later rows on story 3 for AuthorBeta and ModUser,
      attributed to recommendation 2; started rows on story 1 for ModUser and AuthorBeta;
    - five approved Inspired-By links from AuthorBeta's stories into AuthorAlpha's;
    - every counter now derived by the nightly pass: the drift fixtures left no residue, and the
      seed's hand-set `user_stats` values are gone.
  - Narrative: the browser notes in `audit/Badges.md` F50, `audit/Groups.md` F38, `audit/Following.md`
    F19, `audit/UserStoryInteractions.md` F16, `audit/Chapters.md` F6, `audit/Comments.md` F25,
    `audit/Recommendations.md` F28, `audit/Stories.md` F10, `audit/Moderation.md` F62 and
    `audit/Profiles.md` F58.
- **Hand-offs:**
  - **WU-ParityAndRemaining:** if it wraps `CreateGroupAsync` in a transaction, the `GroupsJoined` +1
    stays after the commit.
  - **WU-GroupAdminRescue:** the moderator assign-admin action must not move `GroupsJoined`.
  - **WU-PollVoteIntegrity:** the D23 classification test is already promulgated.
  - **WU-TrackerFixes H7:** may hoist the private `SeedUserStatAsync` copies (now also in
    `ChapterWriteServiceTests`, `GroupServiceTests`, `CounterSymmetryTests`).
- **Pointers:**
  - `layer2-services.md` §"UserStats Updates", §"Counter recompute principle", §"Check-then-act
    posture", §"Recalculation worker (F58)";
  - audit Stage notes in Profiles F22/F58, Groups F38, Chapters F6, UserStoryInteractions F16,
    Following F19, Comments F25, Recommendations F28, Stories F10, Moderation F47/F62, Badges F50;
  - `roadmap.md` §Resolved (D21–D24).

## WU-TptHardDelete — content-parent deletes go through the TPT base rows (`TptDelete`), parent → TPT-child FKs RESTRICT, per-subtype blog-post lifecycle, polls survive their owner (worksheet D10/D11; extends `Data/`, `BlogPosts/`, `Chapters/`, `Moderation/`, `Identity/`, `Notifications/`) — DONE ✓ (2026-09-30)

- **Cells:** **F36 L4.5 5→1** (`BlogPostPage`'s Edit link is now profile-only; the WU ran with no
  browser available — tracker **H21**), back to 5 after the browser pass the same day (H21 closed —
  "Browser verification" below). Everything else lands beneath Stage-5 cells: F35 L1/L2/L5, F36
  L2/L3.5, F37 L1/L2, F6 L2, F47 L2, F40 L1/L2, F23 L1, F52 L1/L2; F58 L2 carries an open-item note
  only. L1 stays 5 with the migration applied. Review fixes, all beneath Stage 5: F6 L2, F35 L2/L5, F52
  L2/L4 (the D12 copy), F23/F24 docs (D12).
- **Trigger:** owner rulings D10 (fix every TPT hard-delete site through one set-based helper, RESTRICT
  as the guardrail, no orphan sweep, TPH closed) and D11 (poll owner SET NULL; NULL = nobody), with
  service §2.2's group-post lifecycle defect and its false "cascades handle it" comments; schema
  §2.2/§2.5. Orchestrator amendments: the spec's R10 (`BlogPostsWritten` for group/site posts) excluded
  → tracker **F16**; U9 (hide Edit on non-profile posts, with a bUnit test) added.
- **What landed:**
  1. **Migration `WU_TptHardDelete_FkPosture`** — the five content-parent → TPT-child FKs CASCADE →
     RESTRICT (`blog_post_comments.blog_post_id` now configured explicitly); `base_polls.owner_id`
     nullable SET NULL (explicit relationship in `IdentityConfigurations`). `Down()` deletes ownerless
     polls first. No data SQL: every existing row already conforms.
  2. **`TptDelete`** (`Server/Data/`) — set-based raw SQL keyed off the child table, run in the caller's
     transaction: chapter, story, profile wall, blog-post dependents, blog post. No group-scope method
     (nothing deletes a group; D47(b)).
  3. **Callers** — `DeleteChapterAsync` (no more materialize + `RemoveRange`); profile, site and the new
     group blog-post deletes in one strategy transaction (the three orphaning stub deletes are gone);
     the moderation hard delete (Story, BlogPost) before `Remove`, inside `InResolveTransactionAsync`;
     `UserDeletionService`'s profile wall (reads only the ids, for the D7 closure).
  4. **Per-subtype blog-post lifecycle** — the profile update/delete probe `ProfileBlogPosts` (a group or
     site id is 404 where delete was a 500 and update half-applied; an authorless post is 403); new
     `UpdateGroupBlogPostAsync`/`DeleteGroupBlogPostAsync` + `UpdateGroupBlogPostDto` +
     `PUT`/`DELETE /api/blog-posts/group/{id}` + client twins. False comments corrected in the service,
     interface, `BlogPostConfigurations` and `UserDeletionService`.
  5. **D11 readers** — the poll manage gate and the read's `isOwner` pattern-match (`null == null` would
     have made an anonymous caller the owner of an ownerless poll); `OwnerUserName` null-safe;
     `NotifyPollUpdatedAsync`'s source is `int?` (four implementers) and the sweeper passes null.
  6. **U9** — `BlogPostDto.Kind` (`BlogPostKind`); the Edit link renders for `Kind == Profile` only.
- **Left alone (and why):** `BlogPostsWritten` for group/site posts → **F16** (the comment that claimed
  group posts were tracked now states the live behavior). Delete UI for any subtype and a group-post
  editor → **B25** (UX unruled; D13's GDPR rider (i) makes it matter). Pending worksheet rows, not
  decided: D42 (ownerless-poll votability — still votable), D43 (group-post rating on update — mirrors
  create), D47(b) (group delete), D20/D39 (caps, `CancellationToken`). Reports on what an author deletes
  and on the TPT comments a hard delete destroys stay Open → existing **F13** (annotated). D13's full
  `DeletePersonalData.razor` rewrite and `poll_votes` survival → WU-UserDeletion (D12's minimal copy
  fix and doc sweep were wrongly routed away too; the review fixes did them). The orphan sweep is
  declined by D10(d), so no tracker item. No one-shot orphan purge in the migration: a dev DB holding
  pre-WU orphans is reset with `reset-dev-db.ps1`.
- **Found while building:** Postgres reports an `ON DELETE RESTRICT` refusal as **23001**
  (`restrict_violation`), not the 23503 D10's text names; the posture tests assert 23001. The spec's K1
  risk (the story cascade past the Restrict `primary_content_id` FK) did not materialize.
- **Review fixes (2026-09-30)** — three reviews, 14 findings (11 distinct), each checked against the
  code and the worksheet; all real, none needed an owner ruling.
  1. **A regression on the circuit** (high): `DeleteChapterAsync` threw when the circuit's scoped write
     context still tracked a comment on that chapter (an author replies, then deletes it). EF refuses to
     `Remove` a principal with a tracked RESTRICT dependent, and the raw `DELETE` never reaches the
     tracker. The gate is now a projection; the delegate clears the tracker and loads the chapter
     inside. `UserDeletionService` (the other EF `Remove` after a `TptDelete`) clears too.
  2. **A lost delete race double-decremented `BlogPostsWritten`**: `TptDelete.BlogPostAsync`'s row count
     was ignored. A 0-row delete now throws `KeyNotFoundException` inside the delegate (404, rollback).
  3. **D12 restored to its owner routing** (worksheet: "the doc sweep and the one-line copy fix ride
     WU-TptHardDelete"; the spec's X1 had moved both, unamended). "[Deleted Comment]" is a retired term
     in `check-doc-hygiene.ps1` (case-sensitive; Check 1 now strips the term before the marker test,
     since "deleted" exempted it; the worksheet is exempt from Check 1 only). Rule: `cross-cutting.md`
     §"Delete Policy Summary" and `content-safety.md` §"Author-Controlled Content Actions"; swept:
     `grid_axes.md` F24, `folder_clusters.md`, the Comments settled note and F23 headline, the
     Identity WU38a note, the service audit's §3/§6 rows, `roadmap.md` §Resolved, two code comments.
     `DeletePersonalData.razor`: comments stay as "[deleted user]"; profile-wall comments go.
  4. **Docs**: `IChapterWriteService`'s doc no longer claims a cascade removes comments; layer2's step 3
     names the moderation `ActiveReportCount` exception; the WU31.5 stub-delete settled bullet is
     annotated superseded; tracker F13's annotation is scoped to sites 3–5; D10's Built marker and the
     roadmap entry record 23001.
  5. **Owed tests**: Unit `ClientBlogPostWriteServiceTests` (5); Integration — the two same-scope
     deletes, the interleaved race, authorless update and group update/delete, `PUT /group` 403/204, `Kind
     == Site`, the deletion page's copy. Each of the four code fixes was mutation-checked.
  No browser was available: tracker **H21** gains two optional steps (circuit chapter delete; the
  deletion page's copy). No cell flips.
- **Verification:** `dotnet build` 0 errors, no new warnings. `dotnet test` all green: Unit 1,074,
  RazorComponents 764, Integration 1,338 (+4 / +4 / +33); after the review fixes Unit 1,079,
  RazorComponents 764, Integration 1,345. The four gates pass. New
  `TptHardDeleteTests` (12): zero surviving base comment/poll/option/vote rows on every delete path,
  including the moderation hard delete of a story (two chapters, a reply, a like) and of a blog post;
  five raw-SQL posture tests; a `pg_constraint` catalog guard (five `r`, plus `parent_comment_id` and the
  poll owner at `n`). Also `BlogPostWriteServiceTests` +10, `BlogPostEndpointsTests` +4,
  `PollServiceTests` +4, `UserDeletionServiceTests` +3 (and the profile-wall test strengthened with a
  raw-SQL count), `BlogPostValidationsTests` +4, `BlogPostPageTests` +4. Mutation-checked: the old poll
  gate, the old `isOwner` and the story hard delete without the helper each fail their test.
  WU-TptHardDelete (2026-09-30) ran with no browser available (H21). The workbench DB was not migrated;
  the next dev start applies the migration. The browser pass the same day applied it and drove every
  surface on both render phases ("Browser verification" below).
- **Browser verification (2026-09-30)** — server-only path; `psql` after every write; phase read from
  the network log (`_blazor/negotiate` against the action's `/api` call), with the circuit forced by
  removing the Auto-mode localStorage hash. **No bug in this WU's code**, so there is no fixes commit.
  1. **Migration in place:** `WU_TptHardDelete_FkPosture` applied on startup to the un-reset workbench.
     `pg_constraint` shows the five FKs at `r` and the poll owner at `n`. There were no orphans before
     or after the pass.
  2. **H21 steps 1–3, both phases:** the Edit link shows on profile posts only, for the author.
     Group-post and site-announcement authors, non-authors and anonymous prerenders see none.
  3. **Lifecycle over HTTP** (no UI, B25), for all three subtypes, with comments, replies, likes and
     voted polls:
     - updates and deletes answered 204;
     - cross-subtype ids answered 404 and never 500, and a profile-route update of a group id no
       longer half-applies;
     - non-authors got 403, and anonymous callers 401;
     - a second delete answered 404, and `BlogPostsWritten` moved once;
     - each delete left no base comment or poll row.
  4. **The circuit chapter delete** after the author's reply succeeds, and so does the WASM one. GIF:
     `e2e-WU-TptHardDelete.gif`.
  5. **Both moderation hard deletes** (a blog post and a story) over HTTP leave no dependents.
  6. **Two throwaway accounts deleted themselves.** The D12 copy was read; a poll survived its owner
     with `owner_id` NULL and hidden tallies for anonymous viewers; a profile wall went through its
     base rows.
  - **Filed:** **H22** (pre-existing: the stale arcs panel after a chapter delete; untouched note
    editors save `<p><br></p>`), and an F16 annotation (the hard delete moves no counter).
  - **Dev-DB state left behind:**
    - AuthorAlpha's group post 4 and profile post 5 and ModUser's site post 6 were created in their
      editors and deleted over the API. AuthorBeta's post 7 and AuthorAlpha's story 13 were hard-deleted.
    - The two chapters added to story 2 were deleted, so story 2 still has its three seed chapters.
    - Post 8, "E2E Ownerless Poll Post", remains: authorless, with ownerless poll 5 (TestUser's vote)
      and TestUser's comment.
    - Seed post 2 has a comment from a deleted user ("[deleted user]").
    - Throwaway users 8 and 9 were registered and deleted.
    - Resolved reports 23 and 24 point at deleted targets.
    - AuthorBeta's `BlogPostsWritten` reads 1 until the nightly recompute.
    - Notifications landed for several seed users: comments, new chapters, report receipts and
      resolutions.
    - `reset-dev-db.ps1` restores the seed.
- **Hand-offs:** WU-AuthorStoryDelete consumes `TptDelete.StoryCommentsAsync` (the story cascade needs
  no `primary_content_id` release — proven by the hard-delete test). WU-BlobCleanup edits
  `ApplyHardDeleteAsync` next. WU-UserDeletion edits `UserDeletionService` after this WU (keep the
  delegate's `ChangeTracker.Clear()`) and rewrites the D13 copy over the D12-minimal one.
  WU-DocCorrections' D12 items (its §A) are done — verify, then skip. WU-SchemaHardening must not
  re-flip these FKs.
- **Pointers:** `layer1-data-model.md` §"Hard-deleting a content parent" and §"Relationships & Queries";
  `layer2-services.md` §"Hard deletes of content parents"; `cross-cutting.md` §"Delete Policy Summary";
  audit Stage notes in BlogPosts F35/F36/F37, Chapters F6, Moderation F47, Groups F40, Comments F23,
  Identity F52, Profiles F58; `roadmap.md` §Resolved (D10, D11, D12).

## WU-ModerationIntegrity — report lifecycle integrity: lock-and-guard resolves, sibling closing, dedup, `ReportedUserId`, account-status table + Reinstate, zombie closure at the source, service-side mod read gates (worksheet D7/D8/D9; extends `Moderation/`, `Stories/` (ExternalVerification), `Identity/`, `Spotlight/`, `SiteSettings/`, `Tags/`, `BlogPosts/`, and the auth-guard sweep in `CustomLists/`, `Following/`, `Groups/`, `Notifications/`, `Recommendations/`, `Messaging/`) — DONE ✓ (2026-09-30)

- **Cells:** **F47 L4.5 5→1** (the `/mod/users` history Target column, Reinstate and the hidden
  "Hide content" on `/mod/reports` changed; the WU ran with no browser available — tracker **H19**);
  **F46 and F53 L4.5 5→1** at the review fixes (the duplicate-report refusal and the inline EV
  business-rule messages changed what users see, undriven — H19). All three went back to 5 after the
  browser pass the same day (H19 closed — "Browser verification" below). Everything else beneath Stage-5
  cells: F46/F47 L1/L2/L3/L3.5/L5, F48 L2, F53 L2, F62 L2, F55 L2, F11/F15 L2, F35/F37 L2, F52 L2, and
  the L2 guard sweep in F18/F27/F38/F42/F49/F51. L1 stays 5 with the migration applied.
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
  `check-render-modes` and `check-a11y` pass. **No browser was available** — tracker **H19** carried the
  pass for F47 L4.5 (run and closed 2026-09-30 — "Browser verification" below).
- **Review fixes (second commit, 2026-09-30).** Three reviews; 21 findings (17 distinct), each checked
  against the code and the worksheet. All were real; none needed an owner ruling:
  1. **A standing ban answers a report.** The derived second-Ban refusal also bound the report-driven
     path, so once an account was banned its other reports could close only as "no action" (82) — a
     false outcome, and `ResolvedNoAction` in D8's history. A report-driven Ban on a banned account now
     resolves the report against the ban (81; no status write, stamp bump or second 74); the
     moderator-initiated path keeps the refusal. Recorded as a refinement on decision row 20.
  2. **Tests the build owed:** the decrement surviving the stamp bump (the counter fix had none), the
     report-driven table refusal rolling back, both new moderator self-skips, the D7 hard-delete case in
     the owner's three-reporter shape, the `/reinstate` route's 204 and 400.
  3. **Wording:** the profile-comment closure note named the wrong account ("its owner's"); it now
     names the deleted profile.
  4. **Docs:** F46 and F53 L4.5 5→1 and H19 extended (steps 2, 4, 5); an F48 Stage note (the queue
     read's gate, `ReportedUserId` on the waiver row); the F47 WU34 settled bullets annotated
     (InteractiveAuto, reversal unbuilt); `identity-and-authorization.md` no longer claims every
     authentication check uses the shared guard (inline checks remain — service §4 residue,
     WU-DocCorrections); `layer2-services.md`'s forwarding rule names Moderation; `layer6-indexes.md`
     gains trap rule 3 (a partial index leading with an FK column drops the full FK index) and the
     explicit `ix_reports_reporter_user_id`, also on the L6 matrix; tracker H8's `UserDeletionService`
     move ticked and the worksheet's two links re-pointed; a garbled F46 heading and two pointers fixed.
  5. **Verification:** Unit 1,070, RazorComponents 760, Integration 1,305 (+8), all green; each new
     test failed against a targeted revert of what it covers. Build 0 errors; the four gates pass. No
     browser was available — H19 (run and closed the same day, next bullet).
- **Browser verification (2026-09-30, after the review fixes; one commit, "browser verification" — no
  bug found in this WU's code, so no fixes commit).**
  - **Setup:** server-only path.
    - The workbench DB first took `WU_ModerationIntegrity` in place: the backfill set
      `reported_user_id` on the three seed reports, and the five `reports` indexes were present.
    - It was then reset, so the edited `DataSeeder` ran fresh, with the same three values.
    - The phase was read from the network log (`_blazor/negotiate` against the action's `/api` call).
      `psql` after every write.
  - **What was driven:** all five H19 steps, each on both phases.
    - D7 sibling closing: three reporters on WASM, two on the circuit. The counter went to 0, with one
      81 per reporter carrying their own id and one 70 to the author.
    - The stale-panel "This report has already been resolved." The counter moved once.
    - No "Hide content" on a User report.
    - The transition-table refusals inline. Ban from Suspended cleared the date.
    - A report-driven Ban on a banned account resolved the report: one 81, no second 74, stamp
      unchanged.
    - Reinstate: off-Active only, no date field. It writes one reason-1 row and no notification.
    - The history's Target column and "[deleted Comment]".
    - The dialog's duplicate message, other users still reporting, and on WASM the throttle's 429 as
      the rate-limit message.
    - The EV "Verify your Archive of Our Own account first." inline (no session-expired redirect on
      WASM, no generic error on the circuit), plus the Settings malformed-URL message and a successful
      request.
    - The D9 read gates over HTTP: moderator 200, member 403, anonymous 401, unknown ids 404.
    - GIF: `e2e-WU-ModerationIntegrity.gif`.
  - **Not driven** (Integration covers it): account deletion's zombie closure.
  - **Filed:** **H20** (comment previews show raw markup; `AccountActionPanel` carries a reason across
    verbs) and **E8** (hand-built malformed or out-of-range bodies answer 500). Both predate this WU.
  - **Docs:**
    - The browser note in `audit/Moderation.md` F47, with short notes in F46, F48, F53, F62 and
      `audit/Spotlight.md` F55. Headlines updated.
    - F46/F47/F53 L4.5 are back to 5 in `status.md`.
    - H19 closed and B18's line updated.
  - **Tests** (no code change): Unit 1,070, RazorComponents 760, Integration 1,305. All four gates
    pass.
  - **Dev-DB state left behind** (server-only DB `TheCanalaveLibraryDB`; `reset-dev-db.ps1` drops it):
    - Comment 1 is deleted (TestUser), and its seed report 2 stays Open (tracker F13).
    - Story 5 and comment 2 are taken down by D7 removals. Reports 1, 4, 5, 7 and 13 are resolved;
      report 6 is resolved no-action.
    - Reports 3 and 15 were resolved by report-driven Bans on banned accounts.
    - Moderator-filed account rows 8–12 and 16–17 record ReaderGamma's ban/reinstate, AuthorAlpha's
      suspend/ban/reinstate and AuthorBeta's ban/reinstate. All seven users are Active, and the
      security stamps of ReaderGamma, AuthorAlpha and AuthorBeta changed.
    - LurkerDelta's five open throttle-run reports (18–22, on stories 12, 11, 7, 6 and 3; each counter
      is 1).
    - Report id 14 is a sequence gap from a refused insert.
    - AuthorAlpha holds a Verified AO3 identity (handle `e2e_alpha3`). Story 2 has an AO3 link (id 1)
      pending link review.
    - Notifications 4–29, all in the 70–82 band. They include AuthorAlpha's 76 and LurkerDelta's five
      80 receipts.
- **Pointers:** `audit/Moderation.md` (the F47 Settled note "report lifecycle integrity"; F46, F47, F48,
  F53, F62 Stage notes); `audit/Identity.md` F52; `audit/Spotlight.md` F55; `audit/Tags.md` F11/F15;
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
