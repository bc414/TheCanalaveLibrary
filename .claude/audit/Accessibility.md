# Audit — Accessibility (Feature 65)

**Cross-cutting quality attribute, minted 2026-07-15 as Feature 65 in `status.md`'s grid** (an
exception to the "cross-cutting cluster = no grid row" precedent `Seo/`/`Errors/`/`Toasts/` follow —
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

## Feature 65 — settled vs. open (WU-A11y planning, 2026-07-15)

**Settled (do not revisit at build time):**
- The floor criterion added to `layer4-style.md` "Interaction States" applies going forward to new
  L4 work, independent of WU-A11y's eventual scope.
- The existing global focus-visible rule and 4.5:1 contrast policy are the foundation WU-A11y
  builds on, not something it needs to re-derive.
- WU-A11y is **not** expected to be a full WCAG AA compliance audit — `middle-addendum.md` #22's own
  framing is "not a full WCAG AA audit pre-launch," a targeted pass is the realistic ceiling for a
  solo dev.

**Open (blocks the build) — `middle_plan_v2.md` decision row 12:**
- **Scope/depth.** Full WCAG AA audit vs. a targeted axe-DevTools pass over the highest-traffic
  pages (search, story page, chapter reading, signup/login — the addendum's suggested set).
- **Which pages**, if the targeted-pass option is chosen — the addendum's four-page list is a
  starting suggestion, not a settled scope.
- **Whether to add an automated a11y test tier** (axe-core/Lighthouse-CI) to the existing
  Unit/Integration/RazorComponents three-tier suite, or keep this a manual/browser-band-only
  concern (per `canalave-conventions/testing.md`'s tier taxonomy — a fourth tier is a testing.md
  change, not just an L4.5 checklist item).

## Stage note

No Stage note yet — WU-A11y is unbuilt (`workplan.md` "Planned / not-yet-built named WUs"). When
decision row 12 resolves and the pass lands, record here: which pages/components were swept, what
was fixed (contrast, focus order, label association, ARIA), which tier (if any) now covers
regression, and update `status.md` Feature 65's L4/L4.5 cells from Stage 1.
