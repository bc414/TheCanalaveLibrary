# Audit — Accessibility (Feature 65)

**Cross-cutting quality attribute, minted 2026-07-15 as Feature 65 in `status.md`'s grid** (an
exception to the "cross-cutting cluster = no grid row" precedent `Errors/`/`Toasts/` follow;
`Seo/` since gained its own row, Feature 64 —
Brian's explicit choice, since accessibility needs its own Stage tracking rather than folding
silently into every consuming feature's L4 cell). No owning folder — see `folder_clusters.md`'s
`Accessibility` row (folder `—`). Addendum-sourced: `.claude/middle-addendum.md` §3 item **#22**
("No accessibility convention or verification step exists at all") — "never surfaced anywhere"
before this file.

## Shared Context

**Current state (verified 2026-07-07 addendum pass, re-confirmed 2026-07-15):** 237 incidental
`aria-`/`role=`/`<label>`/`tabindex` occurrences exist across 66 component files — these come from
ordinary semantic HTML and Blazor's `EditForm` scaffolding, not a deliberate accessibility program.
Two conventions already exist and predate this feature row: the global `focus-visible` ring
(`app.css`, documented in `layer4-style.md` "Interaction States" table) and the WCAG AA 4.5:1
contrast policy (Brian-ratified 2026-07-10, `layer4-style.md` "Prerequisite: Design Tokens"). What's
missing is everything downstream of "the rule exists": no WCAG reference document, no
keyboard-navigation or screen-reader check in any tier rule, no accessibility-specific test tier
(no axe-core/Lighthouse-CI style check in the three-tier suite), and the L4.5-Browser verification
band's own definition (`status.md` header) never mentions keyboard-only or screen-reader navigation
as part of "behaves as intended."

**A minimal Stage-5 bar was added 2026-07-15** (`layer4-style.md` "Interaction States", after the
recipe table) so accessibility isn't entirely invisible to the L4 gate in the meantime: keyboard
reachability/operability, the global focus-visible ring not suppressed, and label association on
form inputs. This is explicitly **not** a WCAG AA claim — it's a floor, pending WU-A11y's real scope.

## Feature 65 — settled vs. open (WU-A11y planning, 2026-07-15; decision row 12 resolved 2026-07-31)

**Settled (do not revisit at build time):**
- The floor criterion added to `layer4-style.md` "Interaction States" applies going forward to new
  L4 work, independent of WU-A11y's eventual scope.
- The existing global focus-visible rule and 4.5:1 contrast policy are the foundation WU-A11y
  builds on, not something it needs to re-derive.
- WU-A11y is **not** expected to be a full WCAG AA compliance audit — `middle-addendum.md` #22's own
  framing is "not a full WCAG AA audit pre-launch," a targeted pass is the realistic ceiling for a
  solo dev.
- **Decision row 12, resolved 2026-07-31** (full rationale: `roadmap.md` §Resolved):
  1. **Sweep by defect class, not by page** — the addendum's four-page framing would have missed
     most of the actual defect concentration (the 43 orphan `<label>`s sit in authoring forms none
     of the four pages exercise).
  2. **No fourth test tier** — extend the bUnit `RazorComponents` tier + `scripts/check-a11y.ps1`
     mechanical gates instead of axe-core/Lighthouse-CI in CI.
  3. **Extract the shared `Modal` primitive** — `layer3.5-structure.md`'s deferral trigger ("until
     a third consumer's shape clarifies what the shared part actually is") had fired: 9 sites.
  4. **`Server/Identity/` in scope.**
- **`aria-invalid` is a deliberate non-goal**, not an oversight — WCAG AA's 3.3.1 is satisfied by
  the error text being programmatically associated (`aria-describedby`), and axe-core has no rule
  for `aria-invalid`. It is also the only labelling concern that would need a live cascading
  `EditContext`, i.e. the sole remaining case for a `FormField` wrapper component — with it out of
  scope, WU-A11y fixes labelling in place rather than introducing a wrapper.
- **The L4.5-Browser axis definition (`grid_axes.md`) is deliberately not amended**, even though
  this file originally named its silence on keyboard/screen-reader as part of the gap. Amending the
  axis would retroactively reopen 40+ already-Stage-5 L4.5 cells across the grid. Accessibility
  stays scoped to Feature 65's own row; the axis's silence is accepted, not fixed.
- **Do not add `aria-modal="true"` to a `Modal` consumer ahead of WU-A11y-Keyboard.** It asserts the
  background is inert, which isn't true until the focus trap exists — a false claim is worse than
  the omission it replaces.

**Split into two work units, same day the decision resolved** (the dividing line: ARIA that names
existing structure vs. ARIA that promises an interaction model not yet built — see
`workplan.md`'s WU-A11y (Structure) DONE entry for the full rationale):

- **WU-A11y (Structure)** — labelling, validation association, the `Modal` primitive (shell + ARIA
  role + accessible name, no trap), image `alt`, `prefers-reduced-motion`, mechanical gates. Fully
  gatable, ships with regression protection. **This is the WU this file's Stage note below covers.**
- **WU-A11y-Keyboard** (tracker **F6b**, not yet started) — focus trap/restore, the `dismiss.js`
  modal-first Escape branch, `aria-modal="true"`, `CanalaveTypeahead` combobox ARIA, the manual
  keyboard script. Pairs with the Phase-3 L4 freeze sweep, since axe-DevTools cannot test keyboard
  behavior at all and Feature 65's L4.5-Browser cell needs Brian's own browser-driven verification
  before it can move off Stage 1.

## What Feature 65 claims after WU-A11y (Structure), stated so the Stage note below can't be
misread as a compliance claim

- **L4-Style → 5.** Static/naming accessibility (labelling, validation association, modal naming,
  image `alt`, reduced motion) is implemented and mechanically gated against regression.
- **L4.5-Browser stays 1.** Keyboard and focus behavior are unverified *by design* — WU-A11y-Keyboard's
  scope, not this WU's. The axe-DevTools browser pass **did eventually run** (2026-07-31, same
  day, once Chrome browser-automation tooling became available mid-session — see Stage note's
  "Addendum" for the full evidence table), but axe structurally cannot test keyboard
  behavior/focus order regardless, so this cell still cannot move without WU-A11y-Keyboard's own
  human-driven pass.
- **Not claimed:** WCAG AA conformance; keyboard operability; screen-reader navigation; focus
  management; reduced motion for animated sprites (the `PrefersAnimated` setting is untouched —
  seeding it from `prefers-reduced-motion` needs a `matchMedia` service, banned by
  `render-and-layout.md`).
- **Verified via axe-DevTools, real browser, 2026-07-31:** contrast, document-title,
  page-has-heading-one, landmark structure, aria-prohibited-attr, button/aria naming — see the
  Stage note Addendum for the full before/after evidence. **One category remains an accepted,
  unfixed finding, not silently dropped:** the tag-type badge palette (`--color-tagtype-*`,
  ratified 2026-07-10, "transcribed verbatim from *Visuals.cs*") and at least one Indicator tint
  recipe fail 4.5:1 against white/tint text in real measurements. These are Brian's locked,
  live-tuned design tokens (`layer4-style.md` "Prerequisite: Design Tokens" — gate-reviewed
  against `/dev/design-gallery`), so WU-A11y (Structure) did not unilaterally change their hex values;
  see the Addendum for the exact ratios and affected components.

## Stage note (2026-07-31)

**Done, mechanically verified:**
- **Modal primitive** (`SharedUI/Dialogs/Modal.razor`) extracted; all 9 prior hand-rolled overlay
  sites (`ConfirmDialog`, `ReportDialog`, `ComposeConversationModal`, `EditorView` preview,
  `SavedTagSelectionSaveDialogInner`, `SavedTagSelectionLoadFlyoutInner`, `TagDirectoryPage`,
  `DesignGalleryPage` ×2) migrated onto it. Carries ARIA `role="dialog"` + `aria-label`;
  deliberately no `aria-modal`/trap (WU-A11y-Keyboard's scope) — `ModalTests.cs` asserts the
  absence directly.
- **Labelling swept by defect class** across 17 files (43 orphan `<label>`s + 2 dangling `for=`
  targets found by an exact hand-classification pass, not estimated): `StoryPropertiesForm`,
  `ChapterPropertiesForm`, `BlogPostPropertiesForm`, `SiteAnnouncementPropertiesForm`,
  `SeriesCreateEditPage`, `MyStoryLineagesPage`, `CharacterEntry`, `MyAcknowledgmentsPage`,
  `GroupCreateEditPage`, `StoryChapterImport`, `TagSelector`, `PairingBuilder`,
  `ChapterFileImport`, `GroupPage`, `ShipFilter`, `FlatTagOverlayEntry`, `ProfileSettingsForm`,
  `ComposeConversationModal`, `LoginWith2fa` (dangling `for=`). Three mechanical shapes used:
  `for=`/`id=` pairs (with `Guid`-based per-instance ids on the 4 loop-rendered components),
  `role="group"`/`aria-labelledby` for composites with no single native control (mostly
  `ContentSurface`/`EditorView`), and a new `AriaLabel` parameter on `CanalaveTypeahead` (threaded
  through `TagSelector`, `UserPicker`, `StoryTitlePicker`, `ShipFilter`, `FanonAxisPage`) for the
  picker-owns-its-name case. One real bug found and fixed along the way: `ChapterPropertiesForm`'s
  "Version Rating" `role="group"` wrapper named the *group*, not the `<select>` inside it — WCAG
  4.1.2 needs the control itself named; fixed with a direct `aria-label` on the select. Caught by
  the new bUnit helper, not by the static gates — see below.
- **All 43 `ValidationMessage` usages** (18 files: 15 Identity scaffold pages + the 3 SharedUI
  forms) carry an `id`; their inputs carry a matching `aria-describedby`.
- **Avatar `alt=` convention standardized on `alt=""`** (5 sites moved off `alt="@Username"`:
  `ComposeConversationModal`, `ConversationListItem`, `MessageItem`, `MessageThread`,
  `ProfileBanner`) — every avatar site has adjacent visible username text, so a matching `alt`
  double-announces to screen readers; `alt=""` was already the majority convention
  (`UserCard`/`UserPicker`/`CommentItem`/`ModSpotlightPage`).
- **`BadgeSettingsForm`'s `alt="" title=` combo fixed** — `title=` removed (hover-only, redundant
  with the adjacent visible `DisplayName`; `layer4-style.md`'s "no title-only essential info" rule).
- **`prefers-reduced-motion` CSS block added** to `Server/Styles/app.css`; confirmed present in the
  compiled/minified `wwwroot/app.css` output. Documented gap (not silently narrowed): does not
  reach animated sprite `.webp` images — `PrefersAnimated` axis untouched, logged as tracker item
  **A9**.
- **`scripts/check-a11y.ps1`** (7 gates: modal-recipe confinement, label `for=` resolution,
  `ValidationMessage`/`aria-describedby` pairing, orphan-label detection, icon-only-button naming,
  `<img>` alt presence/misuse, reduced-motion-rule presence) wired into `.github/workflows/ci.yml`
  alongside `check-design-tokens.ps1`. Every gate mutation-tested by hand (deliberately broke a
  known-good file, confirmed the gate caught it, restored) before being trusted — not just written
  and assumed correct.
- **New bUnit `AccessibleNameAssertions` helper** (`TheCanalaveLibrary.Tests.RazorComponents`) —
  renders a component and asserts every `<input>`/`<select>`/`<textarea>` has an accessible name;
  catches names supplied by a child component and the Version-Rating class of bug the static gates
  structurally can't see. Applied to the four densest label files:
  `StoryPropertiesFormTests`, `ChapterPropertiesFormTests` (new), `BlogPostPropertiesFormTests`,
  `SeriesCreateEditPageTests`.
- **Test tier:** RazorComponents (bUnit) — 650 tests green (was 642 before this WU; +8 new). No
  Unit/Integration tier impact (no service-layer changes). `dotnet build` green across the
  solution.
- **Structural fixes from the real axe-DevTools pass** (see the Addendum below for full
  before/after evidence): `HomePage`/`SearchPage` gained a `<PageTitle>`/sr-only `<h1>`;
  `SearchPage`/`MessagesPage`'s sidebar `<aside>` became a plain `<div>` (not a top-level landmark
  — nested inside `<main>`, which axe's `landmark-complementary-is-top-level` rule flags, and
  arguably not "complementary" content anyway); `ChapterNavigation.razor` gained a `NavLabel`
  parameter (distinguishing its top/bottom instances for landmark navigation) and `role="link"` on
  its disabled prev/next spans (a bare `<span>`'s implicit role doesn't support `aria-label`).

**Addendum (same day, 2026-07-31) — the axe-DevTools browser pass.** Chrome browser-automation
tooling became available partway through the session, closing the gap flagged above. Real
axe-core 4.10.2 (loaded via a `<script>` tag into the running dev server — the extension's
isolated-JS-world `eval` path silently failed to attach `window.axe`; a real `<script src>` tag
executes in the page's main world and worked) was run against the six planned pages via
`mcp__claude-in-chrome__*`, ruleset `wcag2a`/`wcag2aa`/`wcag21aa`/`best-practice`.

| Page | Before (violations / incomplete) | Findings | After fix |
|---|---|---|---|
| `/` | 1 / 0 | `page-has-heading-one` (no `<h1>` — the page is deliberately headerless by design) | **Fixed**: sr-only `<h1>The Canalave Library</h1>` added (`HomePage.razor`) — no visual change. 0 / 0. |
| `/discover` | 4 / 1 | `color-contrast` (20 nodes — tag-type badges + Indicator tint, see below), `document-title` (no `<PageTitle>`), `landmark-complementary-is-top-level` (`<aside>` nested in `<main>`), `page-has-heading-one` | **Fixed** (structure): `<PageTitle>Discover</PageTitle>` + sr-only `<h1>` added; `<aside>` → `<div>` (`SearchPage.razor` — a filter panel integral to the page's task isn't truly "complementary" content anyway). **Not fixed** (design token): color-contrast — see below. Re-run: 0 violations / 1 incomplete (contrast; random story batch draws different tag chips each load). |
| `/story/{id}/{ch}` (chapter reading) | 1 / 2 | `landmark-unique` (two `<nav aria-label="Chapter navigation">`, indistinguishable by landmark), `aria-prohibited-attr` incomplete ×4 (`<span aria-disabled aria-label>` — bare `<span>`'s implicit role `generic` doesn't support `aria-label` in strict ARIA) | **Fixed**: `ChapterNavigation.razor` gained a `NavLabel` parameter (`"Chapter navigation, top"`/`"...bottom"`, wired from `ChapterReadingPage.razor`) and `role="link"` on the disabled spans (the ARIA Authoring Practices pattern for a disabled link). Re-run confirms both classes resolved. Re-run also surfaced `button-name`/`aria-command-name` (Quill toolbar, accepted exception — see below) and one `heading-order` finding (an `<h3>` with no preceding `<h2>`, in the comments/recommendations area) — **new discovery, not fixed by WU-A11y (Structure)**, out of its defect-class scope; logged for the L4 sweep. |
| `/story/{id}/edit` | 3 / 1 | `aria-command-name` + `button-name` ×9 (Quill's own toolbar chrome — `.ql-header`/`.ql-bold`/etc.), `aria-valid-attr-value` incomplete ×2 (`#story-title`/`#story-short-description`'s `aria-describedby` pointing at a `ValidationMessage` id that doesn't exist in the DOM until there's an actual error), `color-contrast` ×3 | **Accepted, not fixed** — all three are exactly the exceptions this WU's design already named: Quill toolbar (matches `AccessibleNameAssertions`' documented exemption), the not-yet-rendered `ValidationMessage` id (matches the plan's explicit prediction — "axe 'incomplete', not a violation" — confirmed verbatim), and the token-level contrast issue (see below). |
| `/Account/Login`, `/Account/Register` | **N/A — both return a raw 500** | See "Severe finding" below. | **Not fixed** — out of WU-A11y's scope; flagged separately. |
| `/messages` (+ compose `Modal`) | 1 / 0 (list); 3 / 0 (modal open) | `landmark-complementary-is-top-level` (conversation-list `<aside>`, same class as `/discover`); with the compose `Modal` open: `aria-command-name`/`button-name` ×9 (Quill again, same accepted exception) | **Fixed**: `<aside>` → `<div>` (`MessagesPage.razor`). The `Modal` primitive itself (the WU's riskiest new code — Quill inside a `role="dialog"`) opened correctly with `role="dialog" aria-label="New Message"` and produced **zero new violations of its own** — confirms the primitive's naming contract holds under a real browser, not just bUnit. |

**Contrast — measured, not fixed, Brian's call.** Real `getComputedStyle`-backed ratios from axe:
`#ffffff` on `--color-tagtype-character` (`#6890f0`) = **3.07:1**; `#ffffff` on
`--color-tagtype-genre` (`#f85888`) = **3.11:1**; the Indicator "success" tint recipe
(`text-(--color-success)` on `bg-(--color-success)/15`, composited over a card ground) =
**2.88:1**; one other tint pairing = **1.7:1**. All fail the ratified 4.5:1 policy. These are
locked, gate-reviewed, live-tuned-with-Brian tokens (`layer4-style.md` "Prerequisite: Design
Tokens") and Pokémon-canon colors (`--color-tagtype-*` "transcribed verbatim from *Visuals.cs*")
— changing the hex values is a design decision, not a markup fix, so WU-A11y (Structure) did not
unilaterally alter them. Logged as a new open item — see tracker.

**Severe finding, out of scope, NOT fixed — flagged prominently:** `/Account/Login` and
`/Account/Register` both return a raw 500 (`System.InvalidOperationException: The registered
callback PersistProperty must be associated with a component or define an explicit render mode
type during registration.`), reproducing for both an anonymous visitor and a signed-in one —
**the entire Identity/auth funnel is currently broken**, not an edge case. Confirmed unrelated to
this WU's own edits (Register.razor's diff was pure `id=`/`aria-describedby=` attribute
additions; the exception's stack trace is in Blazor's render-mode-inference/prerendered-state
infrastructure, nowhere near ValidationMessage). Likely cause, not confirmed: `MainLayout`
contains three `[PersistentState]`-bearing descendants (`NotificationBellInner`,
`MessagesNavLink`, `ReaderDisplayProvider`) that normally get an explicit render mode via
`App.razor`'s `<Routes @rendermode="PageRenderMode"/>` → `AuthorizeRouteView`'s ambient
`DefaultLayout="typeof(MainLayout)"`; Identity pages are statically routed outside `Routes.razor`
entirely (`Server/Identity` is excluded from its `AppAssembly`/`AdditionalAssemblies`, per
WU-SweepRiders' own H1 finding the same day), so if `MainLayout` is now reaching them through
that same ambient-layout mechanism, it arrives with no render mode, and `[PersistentState]`
inference throws. **Not diagnosed further or fixed** — this is unrelated-to-accessibility Blazor
routing infrastructure, a real rabbit hole, and a severity that warrants Brian's own
prioritization call, not a rushed patch buried inside an a11y WU. See tracker for the new item.

**Cells flipped:** `status.md` Feature 65 L4-Style: 1 → 5. L4.5-Browser: stays 1 (keyboard
verification is still WU-A11y-Keyboard's scope; axe cannot substitute for it regardless of when
it ran).
