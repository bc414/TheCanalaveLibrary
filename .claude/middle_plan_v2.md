# Middle Plan v2 — The Canalave Library (Platform-First → Features → Beta → Launch)

> Successor to `middle_plan.md` (now historical reference, the same treatment it gave
> `forward_plan.md`). This is the live master plan; it carries forward the "Decisions that
> need you" table (rows 1–6, plus new rows 7–10) and the Resolved index. `CLAUDE.md` remains
> the single source of truth for file paths, artifact names, and Stage semantics;
> `workplan.md` remains the work-unit ledger — new work-units are *sequenced* here and
> *recorded* there.

## Why v2 exists — the inversion

v1 ordered the work features-first (MVP-surface completeness → platform build-out). v2 inverts
that: **platform build-out now comes before new feature work** (settled 2026-07-05, Brian). The
reasoning: a functional, browser-verified website now exists (WU0–WU38 + cleanup waves, 1,266
tests green), which means every platform practice — caching, observability, error handling,
email, security — finally has a *real surface to battle-test against* instead of being built on
speculation. And the causality runs the other way too: every feature built *after* the platform
lands inherits its conventions (structured logging, error UX, rate limits, telemetry) from day
one, instead of being retrofitted in a sweep later. Infrastructure first, while it is cheap to
test and before the feature count grows.

Two deliberate exceptions where feature and platform work are intertwined:

- **WU38b (View Count)** moves *into* the platform phase — it is the chosen minimal
  battle-test feature for L7 Redis (direct-increment L2 body first, Redis write-behind swap
  behind the same signature in the same arc).
- **L5 WASM enablement stays *after* feature completeness** — the settled single-global-flip
  economics (Resolved 2026-07-04) are unchanged: once the flip lands, every subsequent L2
  contract change also costs an endpoint + client-impl touch, so the flip belongs after the
  feature surface stops moving, not before it starts.

### Phase mapping v1 → v2 (for older pointers in audit files / workplan)

| middle_plan.md (v1) | v2 |
|---|---|
| Phase 0 Hygiene | Phase 0 (expanded: + CI, dependency automation) |
| Phase 0.5 Visual mini-pass | Phase 0.5 (unchanged) |
| Phase 1 MVP-surface completeness | Phase 2 (minus WU38b, which moved to Phase 1 item 2) |
| Phase 2 L4 sweep + freezes | Phase 3 |
| Phase 3 Beta-scope decisions | Phase 4 |
| Phase 4 Platform build-out | Phase 1 (items 1/3 were DONE in v1; item 6 L5 → Phase 5) |
| Phase 5 Beta | Phase 6 |
| Phase 6 Launch | Phase 7 (expanded into a launch-readiness checklist) |

## Where you are (as of 2026-07-05)

The MVP build arc is complete and browser-verified feature-by-feature (see v1 "Where you are"
for the full history). Platform groundwork already landed:

- **Aspire 13.4.6 orchestration live** (WU-Aspire, 2026-07-05): AppHost with containerized
  Postgres (5433) + Redis (`cache`, 6379) + web on 5028; two documented run paths
  (`run-server/SKILL.md`); server-only path unchanged as the default.
- **S3 image storage live on Garage** (WU-S3Garage, 2026-07-05): `S3ImageStorageService`
  (AWSSDK.S3) behind the frozen interface; Garage in dev, Cloudflare R2 in prod needs only
  Phase-7 config values; the R2 wire-format dossier is encoded in code + `audit/ImageStorage.md`.
- **L5 WASM battle-tested** (WU-L5Pilot, 2026-07-04): Tags cluster end-to-end in a real WASM
  runtime; `layer5-wasm.md` is proven; rollout = headless per-feature builds → one global flip.
- `dotnet test` 1,266/1,266 (448 Unit + 446 RazorComponents + 372 Integration). All of
  WU-Aspire/WU-S3Garage committed (671e537).

## The shape of the remaining work

```
0. Hygiene + CI → 0.5 Convention mini-pass
      → 1. PLATFORM BUILD-OUT (observability, Redis+worker, indexes, error handling,
           email, security, data-protection, SignalR, marts)
      → 2. MVP-surface completeness → 3. Full L4 sweep + Stage-6 freezes
      → 4. Beta-scope decisions → 5. L5 WASM global flip
      → 6. Beta → 7. Launch readiness + Launch (DigitalOcean)
```

Phases 3 and 4 interleave with the tail of Phase 2 (unchanged from v1). Phase 1's items are
ordered among themselves but items 4–9 tolerate reordering if a decision row blocks one. The
per-unit build loop (pick → read audit pointer → build → `dotnet build` + `dotnet test` green →
update `status.md`/`workplan.md`/audit Stage note) is unchanged.

---

## Phase 0 — Hygiene + CI — DONE ✓ (2026-07-05)

- **Merge `phase-a-foundation` → `master`.** Fast-forward, zero conflicts (master was 0
  ahead/38 behind). Settled the go-forward branch convention (decision row 5 — see Resolved):
  commit to master directly; feature branches are optional/throwaway, no PR ceremony required.
- **WU-CI** — `.github/workflows/ci.yml` (remote: github.com/bc414/TheCanalaveLibrary, public):
  build + the three-tier `dotnet test` suite (Testcontainers Postgres `postgres:18-alpine` +
  Garage `dxflrs/garage:v2.3.0` on `ubuntu-latest`, which ships Docker), including the Tailwind
  npm step (`actions/setup-node` + the Server build's existing `NpmInstall`/`TailwindBuild`
  MSBuild targets). **Deliberately triggers on `pull_request` + manual `workflow_dispatch`
  only — no `push: master`.** Brian tests locally before pushing his own work; CI's job here is
  vetting Dependabot's version-bump PRs on GitHub's infra so nobody pulls each bump branch to
  test by hand. `push: master` + branch protection + a hard vuln-gate are deferred to the Phase 7
  launch-readiness checklist below (see also the Resolved entry recording this deliberation) —
  they matter once master means "what's deployed," not while working solo pre-launch.
- **Dependency automation** — `.github/dependabot.yml`: grouped `nuget` rule for the Aspire
  train (SDK + every `Aspire.*` package move together — version alignment is a correctness
  constraint, `cross-cutting.md` "Aspire 13 Configuration") and for EF Core packages; `npm`
  ecosystem for Tailwind (`TheCanalaveLibrary.Server/package.json`). Plus
  `dotnet list package --vulnerable --include-transitive` in CI, **report-only**
  (`continue-on-error`) — never fails the build; a CVE in a transitive dependency you can't
  immediately fix shouldn't block every push. Renovate/Snyk considered and declined for now
  (see Resolved entry).
- **`global.json`** added (`10.0.100`, `rollForward: latestFeature`) so local dev, CI, and future
  prod builds resolve the same SDK feature band instead of "whatever's installed."

## Phase 0.5 — Convention-settling visual mini-pass (unchanged from v1)

Brian + a live server over 3–4 representative screens; taste-level corrections locked into
`layer4-style.md` Pattern Accumulation before new features are built to those conventions. Not
a freeze sweep — that is Phase 3. Interleaves freely with Phase 0/1.

## Phase 1 — Platform build-out (the point of v2)

Each item names its test bed (the already-built surface that makes it meaningfully verifiable)
and its doc deliverable — every settled practice lands as a convention file or an extension of
one, same as `testing.md`/`debugging.md` accreted.

1. **WU-Observability** — logging & telemetry conventions. OpenTelemetry plumbing already
   exists (ServiceDefaults: logs/traces/metrics + OTLP → Aspire dashboard in dev); this WU adds
   the *practice*: a new `canalave-conventions/logging.md` (structured message templates, level
   semantics, scopes for userId/storyId context, **no silent catches** — log-and-continue, never
   swallow-without-trace); the sweep that fixes the existing `/* best-effort; log in a future
   structured-logging pass */` sites; Npgsql tracing instrumentation in ServiceDefaults (per-query
   spans); a custom `ActivitySource`/`Meter` primer for domain instrumentation. Test bed: the
   whole app under the Aspire dashboard. Decision row 7 (production telemetry destination)
   resolved 2026-07-06 — Grafana LGTM container, see Resolved; deployment stays Phase 7.
2. **WU-Redis — L7 end-to-end, two steps** (sequencing settled 2026-07-05, see Resolved):
   - **Step 1 — WU38b View Count as the isolated battle test** (moves here from v1 Phase 1).
     Arc: `AddRedisDistributedCache("cache")` client registration → WU38b direct-increment L2 +
     client ping → Redis `INCR` write-behind swap behind the same signature → the **first**
     `BackgroundService` drain worker (5 s cadence, `GETSET` read-and-reset, batch UPDATE —
     `layer7-redis.md` "View Count Tracking"). Deliberately the simplest L7 pattern: it stands
     up the worker infrastructure, drain cadence, health checks, and queue/drain metrics once,
     on a genuinely high-frequency, lossy-tolerant write. **This step is also the observability
     pilot**: queue depth / drain latency / batch size metrics are exactly what a write-behind
     buffer needs to be trustworthy, and they exercise item 1's conventions on the first
     component whose correctness is invisible without them.
   - **Step 2 — the flagship swap on proven scaffolding**: the UserStoryInteraction write-behind
     queue (F16/17 — `LPUSH` → drain → consolidate per (UserId, StoryId) → one batch MERGE,
     including the UserStats counter deltas per the transition-delta rule), the LastReadDate
     ephemeral hash (F44), and the read-side decorator caches — the full v1 Phase 4 item 4
     batch. Method-body swaps behind unchanged signatures; F16/17's existing integration +
     browser-verified coverage is the regression baseline.
   - Refine `layer7-redis.md` from battle-tested reality after each step (the WU-L5Pilot
     treatment). Test bed: story pages + bookshelves under the Aspire path; ground truth via
     `redis-cli` + psql. **Considered and rejected as pilots** (2026-07-05): comment likes
     (low-frequency — write-behind there would be permanent pedagogical complexity with no load
     justification; Comments L7 stays N/A per the grid) and interactions-first (the everything-
     at-once first bite: multi-type consolidation + upsert + counter deltas simultaneously with
     first-ever worker mechanics).
3. **WU-L6 index batch + performance baseline** — the v1 Phase 4 item 2 DDL (UserStoryInteraction
   filtered indexes, comment golden index, StoryTag reverse index; `layer6-indexes.md`) — but
   preceded by a **performance smoke baseline** (NBomber or k6 against Full seed data) so index
   work gets before/after numbers instead of vibes. The baseline script becomes a rerunnable
   fixture for every later L6/L7/L8 claim. Test bed: `/discover`, story pages, bookshelves under
   load.
4. **WU-ErrorHandling** — resolve the standing `cross-cutting.md` "Error Handling Strategy
   (Gap — Not Yet Designed)": what a user sees on a circuit crash / unhandled service exception /
   failed form post (error boundaries, toast vs page, retry affordances), plus the server-side
   contract (what gets logged — builds on item 1). Gated by decision row 9 (a design
   conversation, CLAUDE.md Stage-1 venue). Test bed: deliberately-thrown faults across the
   existing surface; the L4.5 browser band re-verifies the worst flows.
5. **WU-Email** — the sharpest beta blocker: Identity runs `RequireConfirmedAccount = true`
   against `IdentityNoOpEmailSender`, so real users cannot activate accounts. Provider per
   decision row 8; real `IEmailSender<User>` (confirmation, password reset), then notification
   email fan-out (the `EmailEnabled` per-user setting already exists, unconsumed). Test bed:
   registration + reset flows; a dev inbox (provider sandbox or local catcher via Aspire).
6. **WU-Security** — hardening pass + new `canalave-conventions/security.md`: ASP.NET rate
   limiting on auth/upload/comment endpoints; magic-byte sniffing on image uploads (both storage
   impls currently trust the browser's claimed MIME — close it in shared `ImageUploadRules`);
   response headers (CSP to the extent Blazor allows, X-Content-Type-Options, frame options);
   the CI vulnerability scan from Phase 0 documented as cadence. Test bed: existing auth/upload
   surfaces; integration tests for limiter + sniffer.
7. **WU-DataProtection** — persist the Data Protection keyring (default: EF
   `PersistKeysToDbContext`, i.e. Postgres) so auth cookies and antiforgery tokens survive
   process replacement — the classic droplet redeploy footgun. Test bed: locally provable now —
   wipe `bin/`, restart, cookie still valid. Small; do it while touching Program.cs for items 5–6.
8. **WU-SignalR** — messaging push (settled WU35 design; first app-level Hub, `MessagesHub`)
   plus the hub integration-test harness that does not exist yet. Test bed: the built messaging
   feature, two browser sessions. Pointer: `cross-cutting.md` "Private Messaging Architecture".
9. **WU-Marts** — establish the L8 pattern with ONE mart end-to-end (raw SQL, no EF model,
   table-swap rebuild, worker cadence — `layer8-data-marts.md`): recommend `AlsoFavoritedScore`
   (feeds F61 later). Sparse dev-seed results are expected and fine — the deliverable is the
   *pattern* (worker + swap + tests + conventions refined from reality), not meaningful data,
   which arrives with beta. The remaining marts + their consumers (F59 Automatic Tree Search,
   F61 Also Favorited/Recommended, workers 57/58/60/62) land as a batch between Phases 5 and 6 —
   they must exist before beta but benefit most from beta data, so they go last among pre-beta
   work (unchanged rationale from v1 Phase 4 item 7).

## Phase 2 — MVP-surface completeness (v1 Phase 1, minus WU38b)

Ordered as v1, all deps Stage 5, settled directions in the audit files named in `workplan.md`:

1. **WU-Home** — the front door + Community Spotlight display slice. Still gated by decision
   row 2 (curation model / homepage design). Features built from here on inherit Phase 1's
   conventions (logging, error UX, rate limits) from day one.
2. **WU41 Series, WU42 Story↔Story Relationships, WU43 Saved Tag Selections.**
3. **WU40 Manual Tree Search** (stateless pivot; does not wait on marts — settled 2026-07-03).
4. **WU38a Account Deletion UI** (surface the existing service from `/settings`).
5. **WU-AccountEnforcement** (Suspended/Banned at login; Warned banner).
6. **WU39 Story Import & Verification** (fills the `/mod/submissions` Imports tab).
7. **WU38c Export (epub/pdf)** — lowest value; may slip past beta without blocking.

## Phase 3 — Full L4 sweep + Stage-6 freezes (v1 Phase 2, unchanged)

After Phase 2, so freezes happen once on final surface. L4-Style freeze sweep (Brian-driven,
per-cluster render → fix → Pattern-Accumulate → 5→6 on sign-off); surface decision row 1
(non-story report-target rating routing) during the moderation-queue review.

## Phase 4 — Beta-scope decisions (v1 Phase 3, unchanged)

Story Arcs (8), Polls (37), Custom Lists (51), Spotlight donation infra (55 remainder),
Feature Contributions (56): design now or explicitly defer past beta — a deliberate verdict per
feature (decision row 3). Interleaves with Phases 2–3.

## Phase 5 — L5 WASM enablement (v1 Phase 4 item 6, deliberately after features)

Kept last-before-beta on purpose — see "the inversion" above. Per-feature endpoint + client
pairs built headlessly for the *final* Phase-2 surface, then the single global `InteractiveAuto`
flip + one whole-site browser wave, per `layer5-wasm.md` §"Rollout Strategy" / §"The Global
Flip". Battle-tested pattern (WU-L5Pilot); this batch is application, not discovery.

## Phase 6 — Beta

Small audience from the existing community (logistics: decision row 6). Entry gate: Phases 0–3
and 5 done; every Phase 4 item resolved or explicitly deferred; email (Phase 1 item 5) live.
L2/L3 changes from feedback remain normal and planned for — each also touches its L5
endpoint/client impl (accepted 2026-07-03).

## Phase 7 — Launch readiness + Launch (DigitalOcean)

Topology settled 2026-07-03 (droplet: server + Redis; managed PostgreSQL; Cloudflare R2 + CDN).
Decision row 4 is hereby expanded from "deployment mechanics" into the full launch-readiness
checklist — each bullet becomes a checkable item, most are small:

- **Deploy mechanism** (manual vs CI; `aspire publish` docker-compose output is the candidate
  path — one `AddDockerComposeEnvironment` line + generated compose/.env maps onto the settled
  topology; managed PG and R2 replace the postgres/garage resources by connection string).
- **Config/secrets promotion contract** — the single documented list of every env var the
  droplet provides (connection strings, `ImageStorage__S3__*` R2 values, email keys, OTLP
  endpoint, Data Protection). The local pattern (user secrets → env injection) already mirrors it.
- **Migration-in-production convention** — migrate as a gated deploy step (backup first), not
  dev's migrate-on-startup; write it into `layer1-data-model.md` or the deploy doc.
- **Backups you have restored** — managed-PG backup policy + one performed restore drill; R2
  story for blobs (versioning or periodic sync). A backup never restored is a hypothesis.
- **Uptime & alerting** — safely-exposed health endpoint (currently dev-only-mapped) + external
  pinger + an alert channel that reaches Brian.
- **Telemetry destination live** (decision row 7, resolved 2026-07-06: self-hosted Grafana LGTM
  single container on the droplet — see Resolved). Deploy the container; set
  `OTEL_EXPORTER_OTLP_ENDPOINT` on the web app (the exporter is already gated on it — config
  contract above); verify the Claude query path (SSH to droplet → curl Loki/Tempo/Prometheus
  HTTP APIs; optionally the Grafana MCP server).
- **CI hardening for a live master** (deferred from Phase 0 WU-CI, 2026-07-05 — see Resolved):
  - Add `push: master` to `ci.yml`'s triggers — master now means "what's deployed," so continuous
    proof it's green matters where it didn't pre-launch; this is also the signal an auto-deploy
    step would key off.
  - Turn on branch protection requiring the CI check before merge — converts CI from a report
    only Brian reads into a hard gate. Earns its keep once a broken master is an outage instead
    of a shrug (live site, or a collaborator appears).
  - Promote the vuln scan (`dotnet list package --vulnerable`) from report-only to a hard gate —
    shipping a known-vulnerable dependency to real users carries real risk that it didn't pre-launch.
- **TLS/domain** (Cloudflare Registrar per spec §1).
- **Legal/policy track** (decision row 10 — ToS, privacy policy, DMCA agent, moderation
  obligations): non-engineering, runs parallel, gates launch.

---

## Decisions that need you

Rows 1–6 carried from v1 (numbering preserved — existing docs cite these numbers). Row 4 is
expanded in scope by Phase 7 above. **Row 5 resolved 2026-07-05, row 7 resolved 2026-07-06 —
both moved to Resolved below** — row numbers are otherwise left as gaps rather than renumbered,
since other docs cite them by number.

| # | Decision | Default (per spec/§0) | Why it's yours |
|---|----------|----------------------|----------------|
| 1 | **Non-story report-target rating routing** — unchanged from v1 (see `middle_plan.md` row 1 for the full technical framing). | Deferred from pre-integration cleanup (2026-06-26). | Own work-unit; surface during the Phase 3 moderation-queue review. |
| 2 | **Homepage design, incl. the spotlight curation model.** | Spec §5.28: `/` = Community Spotlight stories; §5.26 has no curation mechanics. | Front-door product design. Gates Phase 2 item 1 (WU-Home). |
| 3 | **Beta scope for features 8 / 37 / 51 / 55-remainder / 56** — design or defer, per feature. | None — genuine Stage-1 intent gaps. | Product-scope judgment. Phase 4. |
| 4 | **Launch-readiness mechanics** — now the full Phase 7 checklist: deploy mechanism, config contract, migration-in-prod, backup+restore drill, uptime/alerting, TLS/domain, R2 values. | Topology settled (droplet + managed PG + R2); `aspire publish` compose output is the default deploy candidate. | Operational cost/effort trade-offs. Phase 7. |
| 6 | **Beta logistics** — who, how many, invite mechanism, feedback channel. | None. | Community relationships are yours. Phase 6 gate. |
| 8 | **Email provider + sending domain** — transactional provider for confirmation/reset/notification mail. | Postmark or Amazon SES (cheap at this scale); needs a sending domain, which ties into row 4's domain work. | Cost, deliverability reputation, and the domain is yours. Gates Phase 1 item 5. |
| 9 | **Error-handling UX** — what a user sees on circuit crash / service exception / failed form post (the three dimensions flagged in `cross-cutting.md`'s standing gap). | None designed — Blazor defaults today. | Product-feel decision (CLAUDE.md Stage-1 venue: design conversation in chat). Gates Phase 1 item 4. |
| 10 | **Legal/policy track ownership + timing** — ToS, privacy policy, DMCA agent/process, moderation obligations for a fanfiction UGC site. | None. | Legal exposure and community policy are yours; engineering only hosts the documents. Gates Phase 7 (lighter obligation defensible for the trusted-audience beta — your call). |

---

## Resolved

Newest first. Every entry points at the doc that now states the rule. Entries up to 2026-07-05
are carried forward from `middle_plan.md` (which carried 2026-07-01-and-earlier entries from
`forward_plan.md`) — a few long entries lightly condensed with their full technical framing
intact at the named pointer; `middle_plan.md` remains the unabridged historical record.

- **Production telemetry destination = self-hosted Grafana LGTM container (decision row 7)** —
  resolved (2026-07-06, Brian, during WU-Observability planning): one `grafana/otel-lgtm` container
  on the droplet, deployed at Phase 7. Deciding criterion: the consumer is *Claude queried
  on demand* when Brian points it at an issue — not Brian reading dashboards — so what matters is
  agent-queryable HTTP APIs, which LGTM provides natively (Loki logs / Tempo traces / Prometheus
  metrics, each curl-able over SSH to the droplet with no exposed endpoints; official Grafana MCP
  server available if richer access is ever wanted). Full metrics support retained deliberately —
  WU-Redis's write-behind worker needs queue-depth/drain-latency metrics to be trustworthy.
  Alternatives weighed: Seq (best-in-class log search but weak metrics — would need Prometheus
  bolted on anyway), SigNoz/ClickStack (SQL-queryable but heavier ops on a small droplet), SaaS
  free tiers (external account + egress for no ops savings that matter at this scale). The only
  code seam is `OTEL_EXPORTER_OTLP_ENDPOINT` (exporter already gated on it) — goes on the Phase 7
  config contract. Rule: this file, Phase 7 "Telemetry destination live" bullet;
  `canalave-conventions/logging.md` records the conventions the destination will receive.

- **CI hardening deliberately deferred to launch (report-only, PR-only, no branch protection)**
  — resolved (2026-07-05, Brian, Phase 0 WU-CI): considered turning CI into a hard gate now
  (`push: master` trigger, branch protection requiring green CI, a failing vuln scan) and declined
  for the pre-launch solo period — a broken master is currently a non-event (no one else depends
  on it, nothing is deployed from it), so a hard gate would add friction with no one to protect
  against. CI instead runs on `pull_request` + manual `workflow_dispatch` only, whose real job is
  vetting Dependabot's version-bump PRs on GitHub's infra so they don't have to be pulled and
  tested by hand; the vuln scan runs every time but is report-only
  (`continue-on-error`), since a CVE in a transitive dependency outside Brian's control shouldn't
  block every push. Each hardening step is re-triggered by a concrete future condition, not a
  fixed date, and is written into the Phase 7 launch-readiness checklist as a checkable item at
  that trigger:
  - `push: master` CI trigger + branch protection + hard vuln-gate → triggered by *either* the
    site going live (master becomes "what's deployed," so a broken master becomes an outage
    instead of a shrug) *or* a collaborator appearing (someone else to coordinate/gate against).
  - Auto-deploy on green master → triggered by Phase 7's deploy mechanism landing (decision row
    4); no deploy target exists yet to key off CI's signal.
  - Dependabot auto-merge for green patch/minor bumps → optional, post-launch only, once the
    suite's coverage of critical paths is trusted enough that a green check implies "safe" and
    manual review of every routine bump becomes tedious rather than valuable.
  - Renovate / Snyk in place of Dependabot — considered and declined; Dependabot's grouping
    (Aspire train, EF Core) and weekly cadence already cover the two ecosystems in play. Revisit
    only if Dependabot's grouping/scheduling genuinely becomes a ceiling — may never be needed.
  - Per-tier parallel CI jobs/matrix — considered and declined; a single job (build → all 3
    tiers) is simpler and currently fast enough. Revisit if Integration's Testcontainers startup
    makes total CI wall-clock painful.
  Rule: `.github/workflows/ci.yml` (PR-only trigger + report-only vuln step, both commented
  in-file with this rationale); Phase 7 launch-readiness checklist for the live-later items.

- **Branch convention going forward (decision row 5)** — resolved (2026-07-05, Brian, Phase 0):
  commit to `master` directly; feature branches are optional/throwaway (no push, no PR) rather
  than mandatory per-work-unit. Superseded the v1/v2-draft default of "feature branches off
  master, merged per work-unit" — that ceremony (PR review, branch protection) exists to
  coordinate multiple people or to protect a *deployed* branch, neither of which applies solo
  pre-launch; revisit once a collaborator appears or at launch (see the CI-hardening entry
  above, same trigger). PRs remain available any time Brian wants one (e.g. as a changelog view)
  but are never required to reach master. Rule: this file, Phase 0.

- **L7 pilot = View Count, then interactions** — resolved (2026-07-05, Brian): WU38b view count
  is the isolated Redis battle test (simplest pattern, genuinely planned + needed, high-frequency,
  lossy-tolerant), standing up worker infrastructure + observability once; the UserStoryInteraction
  queue + LastReadDate + read caches follow as step 2 on proven scaffolding. Comment likes
  rejected (low-frequency, would need a Comments L7 N/A→2 reclassification for purely pedagogical
  write-behind); interactions-first rejected (first worker + multi-type consolidation + counter
  deltas in one bite). Rule: this file, Phase 1 item 2.

- **Platform-before-features reordering (this file)** — resolved (2026-07-05, Brian):
  infrastructure lands before new feature work, now that the functional site makes platform
  practices meaningfully testable; WU38b rides the Redis WU; L5 flip stays post-features. Rule:
  this file, "Why v2 exists".

- **Garage replaces MinIO as the dev S3 endpoint** — resolved (2026-07-05, Brian): the spec's
  "MinIO via Aspire in dev" (§1/§3.17/decision #8) predates MinIO OSS's archival (2026-02);
  Garage (actively maintained, S3-compatible, single-node dev mode) takes the role. Everything
  else in the settled S3 design holds: `S3ImageStorageService` on `AWSSDK.S3`, one
  implementation, R2 in prod, endpoint-only difference. Rule: `audit/ImageStorage.md` Shared
  Context; `cross-cutting.md` "Aspire 13 Configuration".

- **L5 rollout strategy — single global flip, no long-lived mixed mode** — resolved
  (2026-07-04, WU-L5Pilot): per-feature endpoint/client-impl work lands incrementally and
  headlessly; the render-mode conversion to `InteractiveAuto` happens in one whole-site pass
  followed by one browser debug wave. `InteractiveAuto` requires dual implementations behind
  every reachable interface (no fallback for missing client DI), and mixed-mode pages cost UX
  degradations + a circuit-crash hazard for no early user value. The pilot's island directives
  on `/tags` were removed accordingly; the island recipe survives as a debugging/staged-rollout
  technique. Rule: `layer5-wasm.md` §"Rollout Strategy" / §"The Global Flip".

- **Revised MVP cutoff — L5–L8 land pre-beta** — resolved (2026-07-03): the L1–L4 scheduling
  boundary is retired. `grid_axes.md`'s architectural boundaries are unchanged.
- **DigitalOcean launch topology** — resolved (2026-07-03): one droplet (server + Redis),
  managed PostgreSQL, Cloudflare R2. Mechanics still open (decision row 4). See Phase 7.
- **S3 image storage before launch** — resolved (2026-07-03): `S3ImageStorageService`, dev S3
  endpoint via Aspire (Garage per the 2026-07-05 entry above), R2 in prod, behind the frozen
  `IImageStorageService`. Built 2026-07-05 (WU-S3Garage). See `audit/ImageStorage.md`.
- **Aspire returns for orchestration** — resolved (2026-07-03): AppHost with Postgres + Redis +
  blob store, pre-beta. The MVP-era pivot off Aspire (2026-06-20) governed the MVP only; WU12's
  anti-pooling ruling (plain `AddDbContext`, no Aspire EF client package) survives inside the
  orchestrated setup. Built 2026-07-05 (WU-Aspire).
- **Marts not required for Manual Tree Search** — resolved (2026-07-03, reaffirming WU28
  Phase 0): WU40 pivots statelessly over live tables; marts feed only F59/F61 + workers. See
  `audit/Discovery.md` Feature 33.
- **Community Spotlight display slice belongs to the homepage** — resolved (2026-07-03): spec
  §5.28 puts spotlight stories on `/`; F55 splits into selection + display (WU-Home) vs.
  donation infrastructure (deferred, Phase 4). See Phase 2 item 1.
- **Style-pass sequencing** — resolved (2026-07-03): hybrid — early convention-settling
  mini-pass over representative screens (Phase 0.5), exhaustive Stage-6 freeze sweep after
  feature completeness on final surface (Phase 3).

- **Read-context lifetime under Blazor Server circuits (supersedes spec §6.6)** — resolved
  (2026-07-01, browser-debugging wave): `ReadOnlyApplicationDbContext` is registered via
  `AddDbContextFactory<…>(…, ServiceLifetime.Scoped)` and every read-service method creates its
  own short-lived context (`await using`). Spec §6.6's direct-injection rationale ("scoped DI
  addresses the thread-safety concern") holds for per-request scopes but not per-circuit scopes —
  layout chrome + page dispatchers query concurrently on one circuit, crashing a shared scoped
  context on every authenticated load. Compile-time read/write separation and scoped
  `IActiveUserContext` filter resolution are preserved; the write context stays plain scoped
  `AddDbContext`; WU12's anti-pooling ruling is unaffected. Convention: `layer2-services.md`
  "Read-Context Concurrency: Factory Per Method"; regression:
  `Tests.Integration/ConcurrentReadAccessTests.cs`.

- **Content-visibility filter placement** — resolved (2026-06-27, WU-FilterRevamp):
  All named display/visibility EF Core query filters (`"ContentRating"`, `"GroupAudience"`,
  `"IsTakenDown"`) live on `ReadOnlyApplicationDbContext.OnModelCreating` only. The write context
  (`ApplicationDbContext`) carries no filters and sees ground truth. A `readDb` bypass
  (`IgnoreQueryFilters`) is always a deliberate elevated read, annotated `// elevated read:`.
  Convention: `cross-cutting.md` "Content Rating Filtering."
- **Read context migration tree** — resolved (2026-06-27, WU-FilterRevamp):
  `ReadOnlyApplicationDbContext` owns no schema and has no migration tree. Deleted
  `Migrations/ReadOnlyApplicationDb/`. Future migrations always target `ApplicationDbContext`.
  Convention: `layer1-data-model.md` §"Two DbContexts."
- **HttpStory{Read,Write}Service (Client) dead-code removal** — resolved (2026-06-27,
  WU-FilterRevamp): Deleted. MVP is `InteractiveServer`-only. F4/F5 L5 reclassified `4 → 2`.
  Convention: post-MVP L5 WASM enablement section in `workplan.md`.

- **Sprite system redesign — full decision set** — resolved (2026-06-27, 8 decisions):
  Theme.Slug column; optimistic URL + onerror; singleton `OptimisticSpriteReadService` in Core;
  component-level resolution via `ThemeContext` + `ISpriteReadService`; `SpriteBaseUrl` config
  seam; assets provisioned out-of-band; `ISpriteAssetProbe` write-time checker; image-orphan
  fix. See `cross-cutting.md` "ThemeContext Cascading Provider", `layer2-services.md` "Sprite
  URLs Are Resolved At Render Time", `audit/ImageStorage.md`.

- **WU37 Story Tagging — architecture, scope split, naming** — resolved (2026-06-25):
  F9/10/15 carved to WU41/WU42/WU43; Character→`StoryCharacter` (not `StoryTag`);
  pairing→`StoryCharacterPairing`; `TagTypeEnum.Relationship` removed; service-layer enforcement
  only; `ApplyFilters` character branch. See `cross-cutting.md` "Structured Tag Authoring &
  Legality Enforcement", `layer2-services.md` "Structured Tag Authoring — Per-Type Filter Branch."

- **WU28 Discovery defaults + random-preload** — resolved (2026-06-25):
  `IDiscoveryDefaultsReadService` merges system defaults + sparse per-user overrides; random
  batch = stateless re-draw from post-filter set; F33 tree search carved to WU40. See
  `layer2-services.md` "Discovery Defaults + Random Batch", `audit/Discovery.md` Features 31/33.

- **WU36 Badges** — resolved (2026-06-25): synchronous inline `AwardAsync`; Recommender +
  RecommenderSilver tiers; `RecommendationSuccessesEarned` column; anti-self-farm guard. See
  `layer2-services.md` "Synchronous Inline Badge Awards", `audit/Badges.md` WU36.

- **WU34 Moderation — eight design decisions** — resolved (2026-06-25): soft-delete default;
  no auto-hide; `AccountStatus`+`SuspendedUntilUtc`; `ActiveReportCount` on User;
  `ReportedEntityId int→long`; dedup-key fix; `StoryApproved` notification type; WU34/WU39 scope
  split (F53 → WU39). See `cross-cutting.md` "Moderation Model", `layer2-services.md`
  "Notification Generation", `audit/Moderation.md` Feature 53.

- **Moderator role assignment in dev seed** — resolved (2026-06-24, WU27.5): role *rows* are
  already seeded via `ApplicationRoleConfiguration.HasData`. WU27.5 assigns `AdminUser` to both
  `"Moderator"` and `"Admin"` in `DataSeeder.cs` — role gate is now exercisable end-to-end.
  Admin-inheritance expressed by listing both roles (IsInRole is literal). See
  `cross-cutting.md` "Role-Based (Moderator) Gating."

- **WU32 Groups — five decisions** — resolved (2026-06-24): `AudienceRating`/`MaxContentRating`
  split; open join, permanent; Member+Admin only (no Moderator — permanent); group blog posts in
  WU32; per-context comment methods. See `cross-cutting.md` "Group Audience-Visibility
  Filter"/"Group Membership and Role Model", `layer2-services.md` "Group Rating
  Waterfall"/"Group Comments", `audit/Groups.md` WU32.

- **Active-user-conditional handling + two content-editing patterns** — resolved (2026-06-23):
  `IActiveUserContext` server-only; ownership = identity equality, inline `@if`; view/edit-page
  split for Story/Chapter; in-place inline for comments/recs/vouch. See `cross-cutting.md`
  "Active-User-Conditional Handling", `layer3.5-structure.md` "Owner-Conditional Edit
  Affordances."

- **`UserStoryInteraction` nomenclature rule** — resolved (2026-06-23, WU23 Phase 0): every
  identifier meaning *user×story interaction* must be spelled `UserStoryInteraction…`, never
  bare `Interaction…`. Full codebase sweep ran in WU23 Phase 0. Deliberate leave-list:
  `UserChapterInteraction` / `LastInteractionDate` (chapter domain); prose in comments/seeds.
  See `canalave-conventions/SKILL.md` "UserStoryInteraction prefix rule."

- **`StoryFilterDto` shape + `GetListingsAsync` two-step** — resolved (2026-06-23, WU23): DTO
  in `Core/Discovery/`; fields: `TextQuery`, `IncludedTagIds`, `ExcludedTagIds`,
  `ExcludedInteractions (UserStoryInteractionTypeEnum list)`, `Sort`, `Page`, `PageSize`.
  Content rating and Source axis excluded by design. `GetListingsAsync(StoryFilterDto)` uses the
  two-step pattern (filter IQueryable → scalar IDs → `GetListingsByIdsAsync`). See
  `canalave-conventions/layer2-services.md` "StoryFilterDto + GetListingsAsync."

- **`ResultsFilterPanel` composition + axis extraction** — resolved (2026-06-23, WU23): filter
  axes (`TagFilter`, `UserStoryInteractionFilter`) are the unit of reuse — extracted as
  standalone components; `ResultsFilterPanel` is one assembler. Panel + StoryDeck kept separate
  at page level (spec §5.27 rejected a bundled composite). Both panel and tree search use a
  batched Apply button. See `canalave-conventions/layer3.5-structure.md` "Filter-Axis Component
  Pattern."

- **§8.7 entity renames** — resolved (2026-06-23, WU23 Phase 0): `UserInteractionFilter` →
  `UserStoryInteractionFilterType`, `DefaultSearchSetting` →
  `DefaultUserStoryInteractionFilterSetting`, `UserSearchSetting` →
  `UserStoryInteractionFilterSetting`. Real rename migration (no pinning). See
  `audit/Discovery.md` "WU23 Shared Context."

- **`AllowInteractions` → `SocialInteractionPermission`** — resolved (2026-06-23, WU23
  Phase 0): disambiguates from `UserStoryInteraction`. C#-only; column names unchanged. See
  `audit/Discovery.md`.

- **Notification generation mechanism** — resolved (2026-06-23): semantic per-event methods
  injected into write services; best-effort post-commit; private create-core owns drop-self +
  dedup. See `cross-cutting.md` "Notification Creation", `layer2-services.md` "Notification
  Generation."

- **Notification in-app toggle dropped (§5.18 deviation)** — resolved (2026-06-23, WU22): the
  spec §5.18 "in-app toggle" is not implemented. `UserNotificationSetting` stores only
  `EmailEnabled` and `Collapsed`; in-app delivery is always-on (after drop-self, dedup). No
  `InAppEnabled` column will be added. Deviation recorded in `audit/Notifications.md`.

- **`Story.ChapterCount`** — resolved (2026-06-22, WU17): **not a denormalized column.** A
  count of published chapters is computable via `c.Chapters.Count(ch => ch.IsPublished)` in any
  EF projection. If the subquery becomes a hotspot, the remedy is an L6 partial index on
  `(story_id) WHERE is_published`, not a counter column. See `audit/Chapters.md` Feature 6 L2
  Stage note.

- **`SiteDailyStat`/`DailyStoryStat`** — resolved: raw-SQL marts, no EF model, matching the
  other three Layer-8 marts. `DailyStoryStat` was dropped entirely. See
  [audit/Moderation.md](audit/Moderation.md) Feature 62 and
  [audit/Discovery.md](audit/Discovery.md)'s Layer-8 implementation notes.
- **JSON settings mapping** — resolved: `ComplexProperty(...).ToJson()`, migrated off the older
  `OwnsOne(...).ToJson()` approach. See [audit/Identity.md](audit/Identity.md) Feature 1 and
  [layer1-data-model.md](skills/canalave-conventions/layer1-data-model.md) §"JSON Complex Types."

- **`IEntityTypeConfiguration<T>` extraction** — resolved: extracted before the first
  migration. One `{Entity}Configuration` class per entity, all colocated in
  `TheCanalaveLibrary.Server/Data/Configurations/`. See
  [layer1-data-model.md](skills/canalave-conventions/layer1-data-model.md) §"Fluent API
  Organization" and [audit/Lookups.md](audit/Lookups.md) item 6.
- **Vouches L1 shape** (§8.13) — resolved Phase B (2026-06-20): dedicated `Vouch` table with
  optional `VouchText`, `MaxLength(1000)` (not the spec's proposed 280 — code is authoritative,
  spec not edited). See [audit/Following.md](audit/Following.md) Feature 19.
- **Hidden Gem at-limit behavior** (§8#4) — resolved Phase B (2026-06-20): reject +
  remove-first at the 5-item limit; no atomic swap, no auto-evict. See
  [audit/Recommendations.md](audit/Recommendations.md) Feature 29.
- **Recommendation minimum length** — resolved WU29 (2026-06-23): **500 characters**, measured
  on HTML-stripped, entity-decoded plain text. Standing constant in
  `RecommendationConstants.MinLength`. See [audit/Recommendations.md](audit/Recommendations.md)
  Feature 27 and [layer2-services.md](skills/canalave-conventions/layer2-services.md)
  §"Recommendation Write Conventions".
- **Recommendation approval lifecycle for MVP** — resolved WU29 (2026-06-23): new
  recommendations are written directly as **Approved**. Spec §5.6's Pending lifecycle deferred
  to WU34. See [audit/Recommendations.md](audit/Recommendations.md) Feature 27.
- **Tailwind version + build tooling** (Phase C) — resolved Phase C (2026-06-20): **Tailwind
  v4**, CSS-first config (`@theme` block), npm + MSBuild target. Color palette: green, rooted in
  Pokémon Gen 4/5 — explicitly not blue. Font-scope rule: Tailwind fonts cover site chrome only;
  user-generated content uses `ReaderSettings` font. See
  [layer4-style.md](skills/canalave-conventions/layer4-style.md) §"Prerequisite: Design Tokens"
  and §"Reader Settings as CSS."
- **Aspire orchestration during MVP dev** — resolved (2026-06-20, narrowed WU12): AppHost
  deferred for MVP; Aspire Npgsql EF client package removed (pooling incompatible with Scoped
  `IActiveUserContext`); plain `AddDbContext` is permanent (holds in production too). See
  `layer2-services.md` "DbContext Registration." (Superseded on the orchestration half by the
  2026-07-03 "Aspire returns" entry; the DbContext half is permanent.)
- **Interaction-icon design** (Feature 16 L4) — resolved WU7 (2026-06-21): inline SVG shapes —
  a permanent, deliberate carve-out from the "never inline SVG" rule. `UserStoryInteractionButton`
  takes `IconPath`/`AccentColor` `[Parameter]`s and stays dumb. See
  [layer4-style.md](skills/canalave-conventions/layer4-style.md) §"Interaction Icons Are Inline
  SVG" and [audit/UserStoryInteractions.md](audit/UserStoryInteractions.md) Feature 16.
- **WU26 chapter routes, versioning, rating** — resolved (2026-06-24):
  `/story/{id}/{ch}[/{versionOrder}]`; edit routes use `/chapter/`; version token = SortOrder;
  progressive disclosure UX; `ChapterContent.Rating?` nullable. See `cross-cutting.md` "Chapter
  Versioning — Progressive Disclosure."

- **WU33 Notification UI** — resolved (2026-06-24): rich flat DTO + normalized target pair;
  two-pass batch enrichment; grouped + flat feeds; bell flyout (UserCard caret pattern); per-row
  settings save. See `layer2-services.md` "Polymorphic RelatedEntityId",
  `layer3.5-structure.md` "Notification Presentation Model", `audit/Notifications.md` Feature 42.

- **WU30 Profiles + theme-selection — seven decisions** — resolved (2026-06-24):
  `IUserSettingsService` self-referential exception; UserStats counter wiring; profile comment
  wall as 4th `CommentSection` context; tabbed page shape; blog-tab owner/viewer distinction;
  `IThemeReadService.GetThemesAsync`; `Profiles/` cluster added. See `layer2-services.md`
  "Self-Referential Editing Exception", `cross-cutting.md` "UserStats Updates",
  `layer3.5-structure.md` "Profile Page Composition"/"CommentSection".

- **Integration test isolation foundation** — resolved (2026-06-24): Respawn reset +
  `IntegrationTestBase` + GUID-suffixed seeding across all 19 classes; serial execution
  deliberate. See [canalave-conventions/testing.md](skills/canalave-conventions/testing.md)
  §"Integration tests reset between every test."

- **WU31.5 TPT denormalization** — resolved (2026-06-24): discovery/date columns base→child;
  named filter removed from `BaseBlogPost`; change-tracker stub delete. See
  `layer1-data-model.md` §"Denormalization with TPT", `audit/BlogPosts.md` Feature 35,
  `audit/Comments.md`.

- **WU35 Messaging architecture** — resolved (2026-06-24): 1-on-1 only; stateless MVP, SignalR
  post-MVP (now Phase 1 item 8); global unread badge in chrome; no PM Notification rows
  (watermark only). See `cross-cutting.md` "Private Messaging Architecture",
  `audit/Messaging.md` WU35.

- **WU31 Blog Post** — resolved (2026-06-24): F56 deferred; edit-page pattern for blog posts;
  `GroupBlogPost` UI in WU32; optional story-link picker via `GetStoryIdsByAuthorAsync`;
  content-rating filter on `BaseBlogPost`; `{*slug}` cosmetic only. See `audit/BlogPosts.md`
  Features 35/36/56, `cross-cutting.md` "Two content-editing patterns."

- **Test strategy** — resolved (2026-06-22, updated post-WU12.5): three tiers by kind — Unit
  (directly-constructed, no host/DB), Integration (Testcontainers Postgres +
  `WebApplicationFactory` + `IActiveUserContext` fake), RazorComponents (bUnit); never EF
  InMemory/SQLite. See [canalave-conventions/testing.md](skills/canalave-conventions/testing.md).
