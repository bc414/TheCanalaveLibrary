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

MVP build-out + platform build-out (Phases 0, 1, 5) are complete and browser-verified. `git log`
shows a bursty pattern, not a steady drip — relevant to how "Recommended next work units" below is
sequenced:

- **2026-07-05 → 07-07:** all nine Phase 1 platform items in three days (Observability,
  SignalBuffering, L6 index batch, ErrorHandling, Email, Security, DataProtection, Marts).
- **2026-07-10 → 07-13:** most of Phases 2/4/5 — Design System, seven MVP-surface features landing
  in a single day (07-11: Series, Story Lineage, Saved Tag Selections, Account Deletion UI,
  Import/Export, Open Graph, Site Daily Stat), Spotlight/Polls/Story Arcs (07-12), the L5 WASM
  global flip + Custom Lists (07-13).
- **2026-07-18:** one 20-commit day removing the desktop/mobile device-fork paradigm
  (WU-ResponsiveMerge) plus the Feature-56 cut.
- **2026-07-24 → 07-27 (most recent four days):** zero new features — entirely hidden-deferral
  closures surfaced by the 2026-07-24 audit (`hidden-deferrals-tracker.md`: WU-AccessGate2,
  WU-ChapterArcBrowserPass, WU-GroupsL5/L5b, A3, WU-RecLifecycle, WU-B2, WU39, WU-TagFanon,
  WU-MsgArchive, WU-TokenGreen, WU-ParentVisibility) plus three doc-hygiene passes and this file's
  own creation.

The pattern is build-in-bursts, then harden-in-a-burst. Only two Phase-2 items and the whole of
Phases 3, 6, 7 remain before launch. Live count/Position detail: `workplan.md`'s Position block.

## Phase status

- **Phase 0 — Hygiene + CI — DONE ✓ (2026-07-05).** Full detail: `middle_plan_v2.md` Phase 0.
- **Phase 0.5 — Convention-settling visual mini-pass — DONE**, folded into ongoing Pattern
  Accumulation (`layer4-style.md`).
- **Phase 1 — Platform build-out — DONE ✓ (2026-07-07, WU-Marts closing item 9).** All 9 items
  shipped: Observability, SignalBuffering (supersedes the dissolved Redis/L7 plan), L6 index
  batch + perf baseline, ErrorHandling, Email, Security, DataProtection, SignalR (removed —
  permanently ruled out for messaging), Marts. Full detail: `middle_plan_v2.md` Phase 1.
- **Phase 2 — MVP-surface completeness — DONE ✓ (2026-07-30, WU-AccountEnforcement closing the
  last item).** Items 2–8 are DONE (Series/Story Lineage/Saved Tag Selections, Manual+Automatic
  Tree Search, Account Deletion UI, External Link Verification, Export/Import, AccessGate) — full
  detail: `middle_plan_v2.md` Phase 2. Item 1, WU-Home, is DONE ✓ (2026-07-28) — decision row 2
  resolved and built; see §Resolved above and `workplan.md`'s DONE entry.
  **WU-AccountEnforcement (residual) — DONE ✓ (2026-07-30).** Core login-blocking + banner shipped
  inside WU38a (2026-07-11); the mid-session-responsiveness residual is closed. `RefreshSignInAsync`
  turned out not to be the tool — it reissues only the *caller's* cookie, and a moderator cannot
  reach a different user's session with it. Built instead: `AccountStatusBanner` re-reads status
  live via a new `IAccountStatusReadService`, re-queried on every in-app navigation (the
  `MessagesNavLink` unread-badge pattern), now covering Warned/Suspended/Banned rather than Warned
  alone. Pointer: `workplan.md` DONE entry; `security.md` "Account-Status Enforcement";
  `identity-and-authorization.md` §"Account Status Is Display-Only, Read Live".
- **Phase 3 — Full L4 sweep + Stage-6 freezes — not started; Phase 2 is done, so this is next.**
  Brian-driven, per-cluster render → fix → Pattern-Accumulate → 5→6 on sign-off. Surface decision
  row 1 is resolved (see Resolved below in `middle_plan_v2.md`). **WU-A11y** (Feature 65) pairs
  with this sweep — both are a final whole-site pass over already-built surfaces — gated on
  **decision row 12** below.
- **Phase 4 — Beta-scope decisions — DONE ✓ (2026-07-18, last verdict: Feature 56 cut).** Every
  per-feature verdict rendered. Full detail: `middle_plan_v2.md` Phase 4.
- **Phase 5 — L5 WASM enablement — DONE ✓ (2026-07-13, WU-L5Sweep + WU-GlobalFlip).** One
  Phase-5-adjacent follow-up remains — see "Recommended next work units" below.
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
genuine open decision gets promoted here, not left buried in the tracker) and resolved 2026-07-28.

| # | Decision | Default (per spec/§0) | Why it's yours |
|---|----------|----------------------|----------------|
| 4 | **Launch-readiness mechanics** — the full Phase 7 checklist above. | Topology settled (droplet + managed PG + R2); `aspire publish` compose output is the default deploy candidate. | Operational cost/effort trade-offs. Gates Phase 7. |
| 6 | **Beta logistics** — who, how many, invite mechanism, feedback channel. | None. | Community relationships are yours. Gates Phase 6. |
| 8 | **Email provider + sending domain** — mechanism is resolved (config-only SMTP swap); which provider, the sending domain, and its SPF/DKIM/DMARC DNS records remain open. | Postmark, SES, or Resend (cheap at this scale); needs a sending domain, tying into row 4's domain work. | Cost, deliverability reputation, and the domain is yours. Gates Phase 7. |
| 10 | **Legal/policy track ownership + timing** — ToS, privacy policy, DMCA agent/process, moderation obligations for a fanfiction UGC site. | None. | Legal exposure and community policy are yours; engineering only hosts the documents. Gates Phase 7. |
| 12 | **Accessibility scope/depth** — full WCAG AA audit vs. a targeted axe-DevTools pass over the highest-traffic pages; whether to add an automated a11y test tier. | None — genuine Stage-1 intent gap. | Product/effort trade-off; solo-dev realistic scope is yours to set. Gates WU-A11y (Phase 3). |

## Recommended next work units (2026-07-27)

*A full-backlog sequencing recommendation, not a mandate — priorities stay yours to reweigh.
Reanalyzed 2026-07-27 against the current tracker state (unchanged since the 2026-07-24 audit
except for closures already reflected in "Already-closed" there) and the `git log` trajectory
above. Work units cluster by shared surface — the pattern WU-TagFanon/WU-RecLifecycle/WU-GroupsL5b
already proved: one WU per shared file/subsystem beats one WU per tracker checkbox — not by
tracker letter. Full narrative/rationale for every item stays in `hidden-deferrals-tracker.md`;
this table adds sequencing only, it doesn't restate content.*

**Why this order:** the debt-paydown burst (07-24 → 07-27) hasn't run dry — tracker groups A–H
still hold roughly 20 open items, two flagged high-priority security (E2, E3 — both already
deliberately gated to Phase 7/launch, not moved up by this reanalysis). Tiers 1–2 continue that
burst a little further on the cheapest, most-unblocked items rather than context-switching back to
feature work mid-burst; Tier 3 onward is the pivot back onto the Phase 2/3 critical path once it's
done.

| Tier | Proposed work unit | Tracker items closed | Why here |
|---|---|---|---|
| **0** — decisions only, chat, no code | ~~Decision row 2 (homepage sections)~~ **DONE 2026-07-28** | — | Unblocks WU-Home, the last unstarted Phase-2 item |
| **0** | ~~Decision row 13 (`/discover` URL state)~~ **DONE 2026-07-28** | — | Resolved against URL state; see "Resolved" below |
| **1** — already unblocked, no decision needed | ~~WU-AccountEnforcement residual~~ **DONE 2026-07-30** | *(G1's residual)* | Closed Phase 2's last item — see Phase status above |
| **1** | ~~WU-ErrorHandling2~~ **DONE 2026-07-30** | E1 | Unblocked since WU-GlobalFlip (2026-07-13), never picked up since |
| **2** — continue the debt-paydown burst, clustered by shared surface | WU-L6MeasurePass | C2, C3, C4, C5, C6 | Same "always measure" origin; C4 needs one new Messaging SeedTool generator, reused by the other four |
| **2** | WU-DiscoveryFilterRestore | B11 | Device-local filter restore + ship seeding parity — same `SearchPage`/`ResultsFilterPanel`/`ShipFilter` surface. Replaces the misnamed "WU-DiscoveryURLState" (row 13 decided *against* filter URL state) |
| **2** | WU-SelectionPermalink | — | Artifact-addressed sharing for public saved selections (`/discover/selection/{id}/{*slug}`); row 13's other half |
| **2** | WU-ApplyFiltersPurity | B12 | No longer blocked by row 13 — `ApplyFiltersAsync` impurity/uncached expansion is independent of how filter state is addressed |
| **2** | WU-StatBadgeProducers | B3, B4 | B4's BetaReader badge literally depends on B3's counter existing |
| **2** | WU-DataSaver | B0 | Small standalone decision ("suppress sprites, or cut the setting") + build |
| **2** | WU-DiscoveryOverrideUI | B7 | Per-user filter-override editing surface (§8.7) |
| **3** | ~~WU-Home~~ **DONE 2026-07-28** | F1 | Closed Phase 2's last content item; Phase 2 fully closed 2026-07-30 (WU-AccountEnforcement, Tier 1) |
| **4** — Phase 3 (L4 freeze sweep) | WU-A11y | F6 | Resolve decision row 12 just before the sweep starts |
| **4** | *(fold into the same sweep — don't build standalone)* | H1, E4, H8 | Each cheap enough to ride the sweep rather than justify its own WU |
| **5** — beta window | WU-EditorSprite | A1 | Spec'd authoring capability; design the sanitizer allow-list addition alongside it |
| **5** | Decision row 6 (beta logistics) | F5 | Chat-only, shortly before beta opens |
| **5** | WU-NotifEmail | B1 | Stays at the Phase 6 gate itself — no live audience to fan out to before then |
| **6** — cheap filler, anytime, no phase dependency | WU-PolishSweep | D4, D5, H2, H6 | Code-economy items, 401-mapping cleanup, StoryDeck skeleton, the by-design-gap list |
| **6** | WU-TestHygieneSweep | H3, H4 (manual-verify half) | Cover-art browser-verify, paste-from-Word manual check |
| **6** | *(fold H7 into whichever WU next touches the test suite, or its own pass)* | H7 | The 25 C-consolidate merges are mechanical, no urgency, no dependency |
| **6** | *(standalone, own WU whenever convenient)* | A6, A7 | Discovery-adjacent but distinct from Tier 2's cluster (Explore filter axes; the frozen `DiscoveryMartSchema`'s 7th UNION arm) — heavier lift, `anytime` window, no forcing function |

**Deliberately not reordered** — each already has an explicit gate and a stated reason; this
reanalysis found no case for moving any of them up: A8 (post-mvp-mobile), B8 (post-beta), B9/B10
(launch), E3/E5 (launch/post-launch), E6 (pre-launch — no rate-limit abuse signal exists yet), F8
(post-launch). F2/F3/F4/F7 are already tracked above via the Decisions table and the Phase 7
checklist, not re-listed here. H4's PDF-import half stays a deliberate future-format candidate.

**E2 (AngleSharp CVE) flagged, not moved.** Tracker priority is `high`, but the risk is accepted
and mitigated (the 13-tag/`href`-only allow-list strips the attack vector) with a concrete Phase 7
root-cause fix already designed. Leaving it at Phase 7 is a decision worth your explicit
re-confirmation given the `high` label, not a silent default this reanalysis is making for you.

Group G is fully closed (2026-07-30) — G1's genuine residual (mid-session account-status
responsiveness, tracked separately as the WU-AccountEnforcement Tier-1 row above) shipped
2026-07-30; nothing left to sequence.

## Resolved

- **Decision row 13 — `/discover` URL state round-tripping (2026-07-28).** **`/discover` never
  carries filter state in its URL.** The row's original framing ("follow `TreeSearchPage`'s
  pattern") was itself wrong and is superseded: `TreeSearchPage` carries *control* state
  (`?degrees=2&sort=…` — small legible scalars), which is not precedent for serialising arbitrary
  id lists. No surface in this codebase has ever done that; every addressable surface uses clean
  paths (`/story/{id}/{slug}`, `/user/{id}/tag-selections`). Three separable calls settled:
  - **Sharing is the artifact's job.** A public `SavedTagSelection` gets a permalink,
    `/discover/selection/{SelectionId:int}/{*Slug}`, following the story-slug contract exactly —
    **the id is the source of truth, the slug is a decorative tail that is never parsed** (so no
    slug column, no migration, and renaming a selection never breaks a link). It lands the visitor
    in `SearchPage` pre-seeded and fully editable. Requires a new anonymous-callable read gated on
    **both** `IsPublic` **and** the owner's `ProfileVisibility` (Class A —
    `design/access-gating-first-principles.md`). → WU-SelectionPermalink.
  - **Return integrity is device-local, not a URL and not server state.** `/discover` restores the
    last-applied filter through the ratified localStorage seam (`layer3.5-structure.md` §"The
    shared tree canvas": ids only, display data rehydrated via the existing batch reads, entities
    the viewer can no longer see pruned silently). `[PersistentState]` is **not** usable for this —
    `error-handling.md` rejects it as prerender-handoff-only, and B11's own sketch was wrong to
    propose it. → WU-DiscoveryFilterRestore.
  - **Ships get seeding parity only.** `Initial*` params + re-seed + dispatcher-resolved display
    names. Ships stay non-shareable and non-persisted; F15's tag-axis-only scope is untouched.
    Making ships shareable would need new L1 child tables, because `SavedTagSelectionEntry` is a
    flat `(SelectionId, TagId, IsExcluded)` row unique on `(SelectionId, TagId)` while a ship is a
    *group* — flattening it degrades to co-presence, which WU-TagFanon ruled **is not a ship**, and
    the unique constraint forbids one character appearing in two ships. Recorded as the known cost
    of a future decision, not a silent deferral.

  Conventions now stating the rules: `layer2-services.md` §"Saved Tag Selections Persist Only the
  Tag Axis" (permalink ≠ saved query; artifact vs. device-local restoration) and
  `layer3.5-structure.md` §"Seed state vs. live fetch in filter components".

- **Decision row 2 — Homepage design (2026-07-28, WU-Home).** The home page is the community
  page: a focused surface, not a broad discovery one. Composition: a "Welcome" mission blurb
  (expanded when Spotlight is empty, collapsed once a spotlight is live) → Community Spotlight →
  the active SitePoll inline when one is open → a community-discourse link cluster (Polls, Fanon,
  Spotlight explained, Site News) surfacing what the persistent nav/UserMenu deliberately omit.
  Deliberately excluded: any story discovery (Recently Updated and a random draw were both
  considered and rejected — spec §5.3.3's "no sort by last updated" reasoning and the "focused
  purpose, not broad" framing) and any personalized/signed-in strip (Continue Reading/follows/
  bookshelves are already one click away via NotificationBell/UserMenu). This decision's only
  stated purpose for `GetRecentListingsAsync`/`GET /api/stories/recent` is gone, so both were
  removed as dead code rather than left in place. Surfaced a second gap along the way — no
  site-announcement channel — closed
  by the companion WU-SiteNews (`SiteBlogPost`, extends Features 35/36) on the same date. Detail:
  `audit/BlogPosts.md`, `audit/Seo.md`.

*(Empty as of 2026-07-27 — this file was just created; the entry above is the first addition.
New entries go here going forward, newest first, each pointing at the doc that now states the
rule, same convention as the retired chain. The full historical Resolved index — every decision
from 2025's design sessions through 2026-07-27 — lives in `middle_plan_v2.md` §Resolved.)*
