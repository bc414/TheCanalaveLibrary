# Roadmap — The Canalave Library (Platform-First → Features → Beta → Launch)

> **Live master plan.** Phase status, the "Decisions that need you" table, and newly-resolved
> decisions going forward. Supersedes `middle_plan_v2.md` (retired 2026-07-27), which itself
> superseded `middle_plan.md` (retired 2026-07-05) and `forward_plan.md` (retired 2026-07-03).
> That chain's full historical Resolved log (every design decision from the original 2025 sessions
> through 2026-07-27) is **not duplicated here** — it stays in `middle_plan_v2.md` §Resolved,
> which itself carries forward `middle_plan.md`'s and `forward_plan.md`'s entries. This file starts
> a fresh Resolved section (see bottom) rather than re-copying 150+ historical entries. `CLAUDE.md`
> remains the source of truth for file paths, artifact names, and Stage semantics; `workplan.md`
> remains the work-unit ledger — new work-units are *sequenced* here and *recorded* there.
>
> **Why the rename (2026-07-27):** the `forward_plan → middle_plan → middle_plan_v2` chain named
> itself after its position in a sequence, with no stable next name and a version suffix baked into
> the filename — the only doc in this corpus named that way. `roadmap.md` is named for its role,
> like `status.md`/`workplan.md`, and doesn't need a `_v2` next time content is superseded — content
> gets superseded, the filename stays put.

## Where things stand (2026-07-27)

MVP build-out + platform build-out (Phases 0, 1, 5) are complete and browser-verified. Recent
trajectory (`git log`): after WU-AccessGate/WU-AccessGate2 closed viewer access gating end-to-end
(Features 64 + 66, 2026-07-23/24), the last few weeks' work shifted to closing hidden deferrals
surfaced by a 2026-07-24 audit (`hidden-deferrals-tracker.md`) — WU-TagFanon, WU-RecLifecycle,
WU-GroupsL5b, WU-MsgArchive, WU-TokenGreen, WU-ParentVisibility — plus three doc-hygiene passes
(WU-DocHygiene/2/3) and this file's own consolidation. Only two Phase-2 items and the whole of
Phases 3, 6, 7 remain before launch. Live count/Position detail: `workplan.md`'s Position block.

## Phase status

- **Phase 0 — Hygiene + CI — DONE ✓ (2026-07-05).** Full detail: `middle_plan_v2.md` Phase 0.
- **Phase 0.5 — Convention-settling visual mini-pass — DONE**, folded into ongoing Pattern
  Accumulation (`layer4-style.md`).
- **Phase 1 — Platform build-out — DONE ✓ (2026-07-07, WU-Marts closing item 9).** All 9 items
  shipped: Observability, SignalBuffering (supersedes the dissolved Redis/L7 plan), L6 index
  batch + perf baseline, ErrorHandling, Email, Security, DataProtection, SignalR (removed —
  permanently ruled out for messaging), Marts. Full detail: `middle_plan_v2.md` Phase 1.
- **Phase 2 — MVP-surface completeness — tail end, in progress.** Items 2–8 are DONE (Series/
  Story Lineage/Saved Tag Selections, Manual+Automatic Tree Search, Account Deletion UI, External
  Link Verification, Export/Import, AccessGate) — full detail: `middle_plan_v2.md` Phase 2. Two
  items remain open:
  1. **WU-Home** — the front door's remaining sections (recently-updated / featured-tags /
     active-SitePolls placement / layout). The spotlight slice already shipped as WU-Spotlight.
     **Gated on decision row 2** below. Pointer: `workplan.md` "Planned" WU-Home entry.
  2. **WU-AccountEnforcement (residual)** — core login-blocking + banner shipped inside WU38a
     (2026-07-11); the only open slice is mid-session responsiveness (a freshly-Warned/Suspended
     user only sees the effect at next sign-in). `RefreshSignInAsync` (from WU-AccessGate's
     `/content-gate` work) is the ready-made tool. **Unblocked, unsequenced.** Pointer:
     `workplan.md` "Planned" WU-AccountEnforcement entry; `security.md` "Account-Status
     Enforcement."
- **Phase 3 — Full L4 sweep + Stage-6 freezes — not started, blocked behind Phase 2's tail.**
  Brian-driven, per-cluster render → fix → Pattern-Accumulate → 5→6 on sign-off. Surface decision
  row 1 is resolved (see Resolved below in `middle_plan_v2.md`). **WU-A11y** (Feature 65) pairs
  with this sweep — both are a final whole-site pass over already-built surfaces — gated on
  **decision row 12** below.
- **Phase 4 — Beta-scope decisions — DONE ✓ (2026-07-18, last verdict: Feature 56 cut).** Every
  per-feature verdict rendered. Full detail: `middle_plan_v2.md` Phase 4.
- **Phase 5 — L5 WASM enablement — DONE ✓ (2026-07-13, WU-L5Sweep + WU-GlobalFlip).** One
  Phase-5-adjacent follow-up remains — see "Also still open" below.
- **Phase 6 — Beta — not started.** Small audience from the existing community. Entry gate:
  Phases 0–3 done (0/1 already are), every Phase 4 item resolved (is), email live (is).
  **Blocked on decision row 6** below. **WU-NotifEmail** (notification email fan-out over the
  inert `EmailEnabled` setting) is sequenced at this gate — a live audience is the natural trigger
  for fan-out email. Hook point: `audit/Notifications.md`.
- **Phase 7 — Launch readiness + Launch (DigitalOcean) — not started.** Topology settled: droplet
  (server only — Redis superseded by in-process signal buffers; Valkey joins only at the N≥2
  trigger), managed PostgreSQL, Cloudflare R2 + CDN. The checklist, each item small:
  - **Deploy mechanism** — `aspire publish` docker-compose output is the candidate path.
  - **Config/secrets promotion contract** — one documented list of every env var the droplet needs
    (connection strings, `ImageStorage__S3__*` R2 values, `Email__*` per the chosen provider,
    OTLP endpoint, Data Protection).
  - **Migration-in-production convention** — gated deploy step (backup first), not dev's
    migrate-on-startup.
  - **Backups you have restored** — managed-PG backup policy + one performed restore drill; R2
    story for blobs.
  - **Uptime & alerting** — safely-exposed health endpoint (exposure mechanism itself undecided)
    + external pinger + an alert channel that reaches Brian.
  - **Sending-domain DNS (SPF/DKIM/DMARC)** — part of decision row 8 below; must verify before
    beta invitations go out.
  - **Operational resilience group** — minimal ops runbook, DR beyond the DB, cost/billing
    alerts, the explicit no-staging-environment decision.
  - **Telemetry destination live** — self-hosted Grafana LGTM container (resolved mechanism, see
    `middle_plan_v2.md` §Resolved); deploy it, set `OTEL_EXPORTER_OTLP_ENDPOINT`.
  - **CI hardening for a live master** — add `push: master` trigger + branch protection + promote
    the vuln scan from report-only to a hard gate (all deliberately deferred from Phase 0 until
    master means "what's deployed" — see `middle_plan_v2.md` §Resolved).
  - **Clear the AngleSharp 0.17.1 mXSS at the root (CVE-2026-54570)** — the vuln-scan hard gate
    above will block on this unless cleared first. Fix: replace `Ganss.Xss` with a custom
    AngleSharp-1.5.2 sanitizer behind the existing seam. Currently risk-accepted (mitigated by the
    13-tag/`href`-only allow-list) — see `security.md` "Accepted-risk register."
  - **TLS/domain** (Cloudflare Registrar).
  - **Activate verified-crawler serving** — flip `Seo:TrustVerifiedBots` once the origin-lockdown
    item above lands, and verify the real Cloudflare verified-bot header contract at that time.
  - **Legal/policy track** — decision row 10 below; non-engineering, runs parallel, gates launch.

## Decisions that need you

Row numbers preserved from the retired chain (other docs cite them by number — see
`middle_plan_v2.md` §"Decisions that need you" header for which rows resolved when, up to
2026-07-27). Row 13 was added 2026-07-27 (promoted out of `hidden-deferrals-tracker.md` B11's
blocking question, same treatment now applied going forward: a tracker item that turns out to be a
genuine open decision gets promoted here, not left buried in the tracker).

| # | Decision | Default (per spec/§0) | Why it's yours |
|---|----------|----------------------|----------------|
| 2 | **Homepage design — remaining sections.** Spotlight is resolved and shipped. What remains: what else the front door shows (recently updated, featured tags, active SitePolls, etc.) and its layout. | Spec §5.28: `/` = Community Spotlight stories; other sections undecided. | Front-door product design. Gates Phase 2's WU-Home. |
| 4 | **Launch-readiness mechanics** — the full Phase 7 checklist above. | Topology settled (droplet + managed PG + R2); `aspire publish` compose output is the default deploy candidate. | Operational cost/effort trade-offs. Gates Phase 7. |
| 6 | **Beta logistics** — who, how many, invite mechanism, feedback channel. | None. | Community relationships are yours. Gates Phase 6. |
| 8 | **Email provider + sending domain** — mechanism is resolved (config-only SMTP swap); which provider, the sending domain, and its SPF/DKIM/DMARC DNS records remain open. | Postmark, SES, or Resend (cheap at this scale); needs a sending domain, tying into row 4's domain work. | Cost, deliverability reputation, and the domain is yours. Gates Phase 7. |
| 10 | **Legal/policy track ownership + timing** — ToS, privacy policy, DMCA agent/process, moderation obligations for a fanfiction UGC site. | None. | Legal exposure and community policy are yours; engineering only hosts the documents. Gates Phase 7. |
| 12 | **Accessibility scope/depth** — full WCAG AA audit vs. a targeted axe-DevTools pass over the highest-traffic pages; whether to add an automated a11y test tier. | None — genuine Stage-1 intent gap. | Product/effort trade-off; solo-dev realistic scope is yours to set. Gates WU-A11y (Phase 3). |
| 13 | **`/discover` URL state round-tripping** — should `/discover` round-trip filter state through the URL at all? (1) yes, all axes, `TreeSearchPage`-style; (2) no URL state but add ship seeding anyway, closing the ships-die-on-navigation asymmetry cheaply; (3) leave as-is — only defensible if URL round-tripping is rejected permanently. Full framing: `hidden-deferrals-tracker.md` B11. | None. | Shareable-URL product behavior + a privacy-perception call (tag ids visible in URLs). Gates only tracker item B11; no phase gate. |

## Also still open (unblocked, not phase-gated)

- **WU-ErrorHandling2** — the `ProblemDetails` envelope + client HTTP error translation half
  WU-ErrorHandling deferred. Unblocked since WU-GlobalFlip (2026-07-13) made the WASM client's
  HTTP calls exist to translate; simply never picked up since. Pointer: `error-handling.md`
  §"Deferred (Phase-5-adjacent)."

The broader off-grid/deferred backlog (built-but-inert plumbing, unmeasured indexes, polish,
test-hygiene) lives in `hidden-deferrals-tracker.md` — a snapshot checklist with its own
priority/window labels, not phase-gated the way the items above are. This roadmap tracks only
phase-gated and decision-gated work; consult the tracker for everything else.

## Resolved

*(Empty as of 2026-07-27 — this file was just created. New entries go here going forward, newest
first, each pointing at the doc that now states the rule, same convention as the retired chain.
The full historical Resolved index — every decision from 2025's design sessions through
2026-07-27 — lives in `middle_plan_v2.md` §Resolved.)*
