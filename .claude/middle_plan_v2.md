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

- **WU38b (View Count)** moves *into* the platform phase — originally as the "L7 Redis battle
  test"; landed 2026-07-06 as one of WU-SignalBuffering's two in-process signal buffers after
  the L7 dissolution (see Resolved "Layer 7 dissolved").
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
      → 1. PLATFORM BUILD-OUT (observability, signal buffers, indexes, error handling,
           email, security, data-protection, marts)
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
2. **WU-SignalBuffering — DONE ✓ (2026-07-06). Supersedes WU-Redis** (the 2026-07-05 "L7
   end-to-end" sequencing — see Resolved "Layer 7 dissolved"). A first-principles audit of the
   deferred L7 assumptions found the write-behind's protect-reads-from-locks rationale was a
   SQL-Server artifact (void under Postgres MVCC) and Redis an Aspire-template overfit; the
   layer dissolved into its correct homes and the genuine work was built directly:
   - **Reading-progress signal buffer (F44, the flagship)** — in-process coalescing store
     (max progress + latest ts per (user, chapter)) + 5 s `BackgroundService` flush via
     `unnest … ON CONFLICT` + shutdown drain + buffer-depth/batch-size/duration telemetry
     (the observability pilot as planned). Bookshelves "Actively Reading" now sorts by derived
     `MAX(uci.last_interaction_date)` (`DefaultSortOrder.RecentlyRead`) — no stored LastReadDate.
   - **View-count signal buffer (F45)** — per-story sum → `daily_story_stats` (per-story/day,
     migration-managed raw DDL, no EF model; ground truth, not a mart). `ViewCount` columns
     dropped from `Story`/`ChapterContent`/`BaseBlogPost`; lifetime total = SUM, revealed
     on-demand from the StoryCard dropdown; **never a sort key** (view-count-not-a-sort settled
     2026-07-06 — anti-popularity-snowball).
   - **F16 interactions stay durable-direct permanently** (no lossy buffer for durable intent;
     the 2s client debounce already absorbs churn); MVCC churn managed by `R4_MvccStorageTuning`
     (fillfactor + autovacuum; USI index audit: all 7 partial indexes justified, none dropped).
   - Pattern doc: `layer2-services.md` §"Signal Buffering" (layer7-redis.md deleted). Forward
     constraint: N≥2 body-swap detail (Valkey, session affinity, no SignalR backplane needed) now
     lives in `canalave-conventions/horizontal-scaling.md`. `dotnet test` 1335/1335.
3. **WU-L6 index batch + performance baseline — DONE ✓ (2026-07-07).** Delivered as
   `L6_IndexBatch` + the `TheCanalaveLibrary.PerfBaseline` fixture (custom dependency-free
   harness — NBomber v5 licensing and k6's external binary disqualified them for a
   forever-rerunnable `dotnet run` fixture), run against the SeedTool extended dataset per the
   2026-07-07 amendment, including the F59 rCTE at depth 2 vs 5. Headline finding: the seven USI
   filtered indexes had silently collapsed to one in the database (unnamed HasIndex calls on the
   same columns — see `layer6-indexes.md` §"Multiple indexes on the same columns"); six restored.
   Headline number: comment roots page −98.8% (24.3→0.29 ms). The StoryTag reverse index of the
   original wording was REJECTED under R4 (the PK already serves the probes — measured neutral).
   Detail: `workplan.md` WU-L6; `layer6-indexes.md` (rewritten against reality).
4. **WU-ErrorHandling — DONE ✓ (2026-07-06).** Decision row 9 resolved same day (four forks —
   see Resolved "Error-handling UX + strategy"); the standing `cross-cutting.md` gap replaced
   with the settled strategy: layered `CanalaveErrorBoundary` islands (page/chrome/card/comments),
   `ExceptionPresenter` message discipline + `InlineAlert`, minimal toast channel, localStorage
   `DraftAutosave` on all four long-form editors, last-resort surfaces (`#blazor-error-ui` —
   restored to `App.razor`; interactive pages previously had none — + `ReconnectModal`) restyled,
   `DetailedErrors` dev-only, logging.md's two stubs filled. Verified by deliberately-thrown
   faults in a real browser (`/dev/error-playground` is the standing test bed) + the chapter-editor
   draft loop end-to-end; `dotnet test` 1374/1374. The `ProblemDetails` envelope + client HTTP
   translation half is deferred to a Phase-5-adjacent follow-up (no testable HTTP error surface
   until the WASM client makes those calls). Detail: `workplan.md` WU-ErrorHandling.
5. **WU-Email** — the sharpest beta blocker: Identity runs `RequireConfirmedAccount = true`
   against `IdentityNoOpEmailSender`, so real users cannot activate accounts. Mechanism settled
   2026-07-06 (see Resolved): a provider-agnostic **SMTP** seam (`Email:Provider` = `Smtp`/`NoOp`,
   mirroring `ImageStorage:Provider`), a real `IEmailSender<User>` over MailKit (confirmation,
   password reset, email-change), and a **Mailpit** dev inbox via Aspire. **Scope: transactional
   only** — notification email fan-out (the `EmailEnabled` per-user setting already exists,
   unconsumed) is deferred to a follow-up WU (see `audit/Notifications.md`). The prod provider +
   sending domain remain open (decision row 8 residual), deferred to Phase 7. Test bed:
   registration + reset + email-change flows verified end-to-end against Mailpit (Aspire path).
6. **WU-Security** — DONE ✓ (2026-07-06) — hardening pass + new
   `canalave-conventions/security.md`. As-built differs from the original wording in one
   settled way: comment/upload writes are NOT HTTP endpoints (SignalR circuit; and
   `InteractiveAuto` keeps the circuit alive post-flip), so write throttling lives at the L2
   service layer (`IWriteRateLimitService`) with HTTP middleware limits only on the genuinely
   HTTP surfaces (`POST /Account/*` per-IP, `/api/tags` writes). Upload validation expanded
   from sniff-only to sniff + ImageSharp re-encode (see Resolved). Headers/CSP + nonce +
   SRI + inline-handler sweep, Identity lockout + cookie flags, vuln-scan cadence documented.
   Detail: `workplan.md` WU-Security entry; `security.md`.
7. **WU-DataProtection** — DONE ✓ (2026-07-06) — keyring persisted via
   `PersistKeysToDbContext<ApplicationDbContext>` + `SetApplicationName`; restart drill
   passed (filesystem store moved aside, process replaced, cookie + antiforgery survived).
   One-time global sign-out expected when this first deploys. Detail: `workplan.md` entry;
   `security.md` §"Data Protection Keyring".
8. **WU-SignalR — REMOVED (2026-07-07).** Was tracked here as messaging push (first app-level Hub,
   `MessagesHub`); permanently ruled out instead, not deferred — see Resolved "SignalR permanently
   ruled out for private messaging" and `cross-cutting.md` "Private Messaging Architecture". The app
   has zero app-defined SignalR Hubs, and that's now a permanent property, not a pending build.
9. **WU-Marts — DONE ✓ (2026-07-07). Scope expanded same day (supersedes the one-mart-pattern +
   wait-for-beta sequencing).** The horizontal line is crossed deliberately with synthetic *clustered* data
   instead of waiting for beta (see Resolved "Horizontal line crossed / discovery mart family").
   Scope: (a) **`TheCanalaveLibrary.SeedTool`** — standalone bulk-load console (seeded PRNG,
   taste-communities + power-law + supernodes + hidden-gem chains + spotlight + vouch + consent
   structure, Npgsql binary `COPY`; never wired into startup or `TestAppFactory`); (b) the full
   discovery mart family + workers (F60 narrow edge-list mart, F61 co-occurrence marts —
   raw SQL, `_a`/`_b` swap, `CanalaveTelemetry.Marts`); (c) the F61 ranked-read and F59 rCTE
   **service layers** (+ diagnostics probes; UI stays deferred); (d) headless verification
   (Integration graph fixture + Unit + diagnostics JSON). Workers 57/58/62 are NOT in scope
   (nothing to clean/aggregate yet — they keep the old sequencing). Conventions:
   `layer8-data-marts.md`; decisions: `audit/Discovery.md` F59/F60/F61 + F33.

## Phase 2 — MVP-surface completeness (v1 Phase 1, minus WU38b)

Ordered as v1, all deps Stage 5, settled directions in the audit files named in `workplan.md`:

1. **WU-Home** — the front door. The Community Spotlight slice split out and landed as
   **WU-Spotlight (2026-07-11)** — the full feature (slot grants, block booking, homepage display
   section, mod surfaces), not just a display slice; see `workplan.md` WU-Spotlight and
   `audit/Spotlight.md`. WU-Home now covers the *remaining* homepage sections, still gated by
   decision row 2's remaining half (homepage design). Features built from here on inherit
   Phase 1's conventions (logging, error UX, rate limits) from day one.
2. **WU41 Series — DONE ✓ (2026-07-11), WU42 Story Lineage (renamed from "Story↔Story
   Relationships," 2026-07-12 — see `audit/Stories.md` Feature 10) — DONE ✓ (2026-07-12), WU43 Saved
   Tag Selections.**
3. **WU40 Manual Tree Search** (stateless pivot; does not wait on marts — settled 2026-07-03).
   **WU44 Automatic Tree Search UI — DONE ✓ (2026-07-11)** (Feature 59; shipped the unified-page
   shell + Automatic tab; Manual tab is a placeholder for WU40 to fill in).
4. **WU38a Account Deletion UI** (surface the existing service from `/settings`).
5. **WU-AccountEnforcement** (Suspended/Banned at login; Warned banner).
6. **WU39 External Link Verification (mod workflow)** — re-minted 2026-07-11 (was "Story Import &
   Verification"): fills the `/mod/submissions` tab with review of `Unverified` `StoryExternalLink`
   rows + the two-way-link authorship mechanism. Its author-facing half (links + story-page
   display) moved into WU38d; file ingestion became Feature 63/WU38d. See `audit/Moderation.md`
   Feature 53.
7. **WU38c Export (six formats) + WU38d Chapter Import & external links — DONE ✓ (2026-07-11).**
   Scope expanded same day from "epub/pdf export, lowest value": planning surfaced that
   file-format import (Word/Google-Docs/EPUB → chapters) existed nowhere, and it's
   launch-adoption-critical for a community migrating in from other sites. Shipped: export six
   formats (EPUB/PDF/HTML/TXT/MD/DOCX, "export = what you can read"), import five modes over one
   sanitizer-bounded backend (new Feature 63), the Feature-53 reframe ("Also posted on" links +
   `StoryExternalLink`/`ExternalPlatform` remodel). 1600 tests green; export/import round-trips +
   browser-verified. WU39 (item 6) now owns only the mod link-verification tab. See
   `audit/Export.md`, `audit/Import.md`, `audit/Moderation.md` F53, `workplan.md` WU38c/WU38d.
8. **WU-SeoSite** *(added 2026-07-15, Feature 64, minted from `middle-addendum.md` §3 #15/#16/#18).*
   `robots.txt`, a `sitemap.xml` endpoint over published stories, and the spec'd-but-never-built
   canonical-slug 301 redirect. The mature-content `noindex` half of this feature is gated on
   decision row 11 (ramifications not yet assessed) — build the redirect/robots/sitemap slice
   first, add `noindex` once row 11 resolves. See `audit/Seo.md`.

## Phase 3 — Full L4 sweep + Stage-6 freezes (v1 Phase 2, unchanged)

After Phase 2, so freezes happen once on final surface. L4-Style freeze sweep (Brian-driven,
per-cluster render → fix → Pattern-Accumulate → 5→6 on sign-off); surface decision row 1
(non-story report-target rating routing) during the moderation-queue review. **WU-A11y**
*(added 2026-07-15, Feature 65, minted from `middle-addendum.md` §3 #22)* pairs naturally with
this sweep — both are a final whole-site pass over already-built surfaces — but its scope/depth is
gated on decision row 12; see `audit/Accessibility.md`.

## Phase 4 — Beta-scope decisions (v1 Phase 3, unchanged)

Decision row 3 is now fully resolved (all per-feature verdicts rendered — see Resolved).
Feature Contributions (56) got its verdict 2026-07-18: **cut from the roadmap entirely** (machinery
removed; the Architect badge is retained as a manual grant — see Resolved "Feature Contributions
(56) cut"). (Spotlight donation
infra — 55 remainder — got its verdict 2026-07-11: explicitly deferred past beta; the spotlight
feature itself was built in Phase 2 as WU-Spotlight. Polls — 37 — designed + built 2026-07-12.
Story Arcs — 8 — designed + built 2026-07-12 as WU45, scope widened to include the chapter-list
presentation upgrade and chapter reorder/delete. Custom Lists — 51 — designed 2026-07-13, built as
WU-CustomLists. See Resolved.) Two Chapters-cluster (Feature 6) items follow the same
design-now-or-defer pattern, carried from WU6's original scope notes rather than decision row 3:
**WU-EditorSprite** (inline Pokémon-sprite Quill blot, spec §5.30.2, deferred at WU6 "its own
future work-unit") and **WU-EditorMobile** (compact `EditorView` toolbar — re-scoped 2026-07-18:
a future *mobile-phase* UX decision riding the adaptivity ladder's rung-3 trigger; the
device-specific-composition framing is obsolete, the desktop/mobile axis having been dissolved by
the single-responsive-site resolution — see "Resolved"). See `audit/Chapters.md`.

## Phase 5 — L5 WASM enablement (v1 Phase 4 item 6, deliberately after features)

Kept last-before-beta on purpose — see "the inversion" above. Per-feature endpoint + client
pairs built headlessly for the *final* Phase-2 surface, then the single global `InteractiveAuto`
flip + one whole-site browser wave, per `layer5-wasm.md` §"Rollout Strategy" / §"The Global
Flip". Battle-tested pattern (WU-L5Pilot); this batch is application, not discovery.

**WU-ErrorHandling2** *(Phase-5-adjacent follow-up, named 2026-07-15)* — the `ProblemDetails`
envelope + client HTTP error translation half WU-ErrorHandling deliberately deferred (no HTTP
error surface existed until the WASM client made calls). The global flip (WU-GlobalFlip,
2026-07-13) already landed, so this follow-up is now unblocked; sequenced here rather than
earlier because it completes the WASM-era error story right after the flip that created its need.
See `error-handling.md`.

## Phase 6 — Beta

Small audience from the existing community (logistics: decision row 6). Entry gate: Phases 0–3
and 5 done; every Phase 4 item resolved or explicitly deferred; email (Phase 1 item 5) live.
L2/L3 changes from feedback remain normal and planned for — each also touches its L5
endpoint/client impl (accepted 2026-07-03).

**WU-NotifEmail** *(named 2026-07-15)* — notification email fan-out over the inert
`UserNotificationSetting.EmailEnabled` setting, explicitly deferred out of WU-Email's
transactional-only scope. A live beta audience is the natural trigger for this (digest/fan-out
email only matters once there's a real audience to notify), so it's sequenced at the Beta gate
rather than earlier. Hook point: `audit/Notifications.md`.

## Phase 7 — Launch readiness + Launch (DigitalOcean)

Topology settled 2026-07-03, amended 2026-07-06 (droplet: **server only** — the Redis component
was superseded by in-process signal buffers, see Resolved "Layer 7 dissolved"; a Valkey container
joins the droplet only at the N≥2 / measured-need trigger; managed PostgreSQL; Cloudflare R2 + CDN).
Decision row 4 is hereby expanded from "deployment mechanics" into the full launch-readiness
checklist — each bullet becomes a checkable item, most are small:

- **Deploy mechanism** (manual vs CI; `aspire publish` docker-compose output is the candidate
  path — one `AddDockerComposeEnvironment` line + generated compose/.env maps onto the settled
  topology; managed PG and R2 replace the postgres/garage resources by connection string).
- **Config/secrets promotion contract** — the single documented list of every env var the
  droplet provides (connection strings, `ImageStorage__S3__*` R2 values, `Email__Provider` +
  `Email__Smtp__*` (Host/Port/User/Password/UseStartTls) + `Email__FromAddress`/`Email__FromName`
  for the chosen provider (decision row 8), OTLP endpoint, Data Protection). The local pattern
  (user secrets → env injection) already mirrors it.
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
expanded in scope by Phase 7 above. **Row 5 resolved 2026-07-05; rows 7 and 9 resolved
2026-07-06; row 3 fully resolved 2026-07-18 (last item — Feature 56 — cut) — moved to Resolved
below** — row numbers are otherwise left as gaps rather than
renumbered, since other docs cite them by number.

| # | Decision | Default (per spec/§0) | Why it's yours |
|---|----------|----------------------|----------------|
| 1 | **Non-story report-target rating routing** — unchanged from v1 (see `middle_plan.md` row 1 for the full technical framing). | Deferred from pre-integration cleanup (2026-06-26). | Own work-unit; surface during the Phase 3 moderation-queue review. |
| 2 | **Homepage design — remaining sections.** The spotlight-curation half was resolved 2026-07-11 (see Resolved "Community Spotlight model"); the spotlight section of `/` is built by WU-Spotlight and no longer gated. What remains open: what else the front door shows (recently updated, featured tags, active SitePolls — open intent recorded 2026-07-12, see `audit/BlogPosts.md` F37 — etc.) and its layout. | Spec §5.28: `/` = Community Spotlight stories; other sections undecided. | Front-door product design. Gates the rest of Phase 2 item 1 (WU-Home). |
| 4 | **Launch-readiness mechanics** — now the full Phase 7 checklist: deploy mechanism, config contract, migration-in-prod, backup+restore drill, uptime/alerting, TLS/domain, R2 values. | Topology settled (droplet + managed PG + R2); `aspire publish` compose output is the default deploy candidate. | Operational cost/effort trade-offs. Phase 7. |
| 6 | **Beta logistics** — who, how many, invite mechanism, feedback channel. | None. | Community relationships are yours. Phase 6 gate. |
| 8 | **Email provider + sending domain** (residual — mechanism resolved 2026-07-06, see Resolved) — which SMTP provider to point the seam at, and the sending domain. | Postmark or Amazon SES (cheap at this scale) or Resend; needs a sending domain, which ties into row 4's domain work. | Cost, deliverability reputation, and the domain is yours. Config-only swap once decided (no code change) — gates Phase 7, not Phase 1 anymore. |
| 10 | **Legal/policy track ownership + timing** — ToS, privacy policy, DMCA agent/process, moderation obligations for a fanfiction UGC site. | None. | Legal exposure and community policy are yours; engineering only hosts the documents. Gates Phase 7 (lighter obligation defensible for the trusted-audience beta — your call). |
| 11 | **Mature-content `noindex` ramifications** — whether `noindex, follow` on Mature/Explicit story pages risks de-listing legitimate content, how it interacts with the still-open age-verification legal question (decision row 10 / `middle-addendum.md` §3 #2), and any crawl-budget effect. Raised, not answered, 2026-07-15 (Brian: "need to understand the ramifications of this suggestion" — `middle-addendum.md` §3 #18). | None — genuine open question. | Product/legal judgment call. Gates WU-SeoSite's `noindex` half (Phase 2 item 8); the robots/sitemap/redirect half of that WU is unaffected and can proceed. |
| 12 | **Accessibility scope/depth** — a full WCAG AA audit vs. a targeted axe-DevTools pass over the highest-traffic pages (search, story, chapter reading, signup/login); which pages if targeted; whether to add an automated a11y test tier (axe-core/Lighthouse-CI) to the three-tier test suite. | None — genuine Stage-1 intent gap (`middle-addendum.md` §3 #22 names the gap but not a scope). | Product/effort trade-off; solo-dev realistic scope is yours to set. Gates WU-A11y (Phase 3). |

---

## Resolved

- **Feature Contributions (56) cut (row 3's last Feature-56 verdict)** — **resolved 2026-07-18**
  (Brian, in chat; ground-truthed against the Gemini origin record and code reality). **Cut from the
  roadmap entirely**, not deferred. Rationale: the prosocial-recognition space is already covered by
  shipped features (Beta Reader acknowledgments, Muse/Inspiration, Recommendations, Community
  Spotlight, Custom Lists); a formal on-site attribution loop has near-zero value at trusted-beta
  scale (suggestions arrive via Discord); and the feature's `FeatureContribution` table was the
  single most complex delete-path in the schema (the three-way `SetNull` "diamond" User→FC /
  User→BlogPost→FC / User→Comment→FC). The prosocial-badge *intent* that motivated it is preserved
  by **keeping the Architect badge**, granted manually (direct `user_badges` insert — `AwardAsync`
  stays deliberately unmapped, no admin UI). Clean pre-launch removal (sole squashed `InitialSchema`
  migration, no shipped data, no `.razor` UI). Full record: `audit/BlogPosts.md` Feature 56 CUT note;
  feature marked CUT (number kept) in `grid_axes.md` / `status.md`.

- **Desktop/Mobile axis → single responsive site** — **resolved 2026-07-18** (Brian, deliberated in
  chat against first principles, the Oct-2025 Gemini origin record, and current external guidance).
  One component tree, one DOM, every viewport; CSS-first adaptivity (two tiers, 768px, no tablet
  tier); device detection and the `{X}Desktop`/`{X}Mobile` fork paradigm removed
  (WU-ResponsiveMerge); page-level coordination composites folded into their pages; mobile
  variants deleted as unvalidated placeholders — deliberate narrow UX is a future mobile-phase
  activity gated on desktop evaluation finishing first. Native-app direction is **PWA post-L5**,
  not MAUI. Supersedes the 2026-07-17 "do not merge" verdict in `modernization-audit/report.md` /
  `deferred-work.md` §1 (that verdict assumed the fork paradigm would continue). Rules now live in:
  `canalave-conventions/render-and-layout.md` §"Responsive Layout Architecture" (architecture +
  rung-3 trigger), `layer4-style.md` §"Responsive Adaptivity Ladder" (the ladder),
  `layer3.5-structure.md` §"Responsive Structure" (composition consequences).

- **Custom Lists requirements + build scope (row 3's Feature-51 verdict)** — **resolved 2026-07-13**
  (Brian, in chat; ground-truthed against the Gemini design log and reconciled with code reality).
  Designed and built now, not deferred, as WU-CustomLists. Headline decisions: positioning is
  *named shareable shelves* (privacy demoted — Private Favorites already own the zero-effect save);
  the public-list-as-search-filter integration is **dropped** (shared blocklists cut against the
  site ethos; whitelists are redundant with view+clone), dissolving §8.7's filter-composition open
  question with zero schema change; sharing = view + optional clone (clone copies only
  cloner-visible entries, starts private); separate `/my-lists` surface, not Bookshelves tabs;
  user-selectable sort, no manual ordering. Full settled record: `audit/CustomLists.md`
  §"Settled design".

- **Story Arcs + chapter presentation + chapter reorder/delete (row 3's Feature-8 verdict)** —
  **resolved 2026-07-12** (Brian, in chat over many rounds; two real Fimfiction story pages
  inspected DOM/CSS/JS as behavioral reference, deliberately not ported — Blazor-first-principles
  re-derivation). Designed and built now, not deferred, with scope deliberately widened twice:
  (1) the `ChapterList` presentation upgrade (per-viewer read/progress state from Feature 44, one
  pure shared segmenter for frontier-window collapse + arc headers, strict-chain "New" badge,
  progress fill-bar, manual read-marks as a new durable-direct seam) is a prerequisite that ships
  with arcs, and (2) chapter drag-reorder + deletion (Feature 6) is a co-requisite because arc
  ranges are `ChapterNumber`-keyed and reordering didn't exist (append-only writes). `StoryArc.
  SortOrder` eliminated. Reorder/delete warnings explicitly waived. Settled-vs-open detail:
  `audit/Stories.md` Feature 8 + `audit/Chapters.md` "WU45 settled design"; build: `workplan.md`
  WU45.

- **Polls requirements + build scope (row 3's Feature-37 verdict)** — **resolved 2026-07-12**
  (Brian, in chat). Designed and built now, not deferred. Full settled requirements (per-poll
  config: vote mode / results visibility / anonymity; config-locks-after-first-vote; nullable
  `DateClosed` lifecycle; 30-min quiet-period edit notifications; `/polls` surface; mods create
  Site, authors create Blog): `audit/BlogPosts.md` Feature 37 "Requirements settled 2026-07-12".
  Home-page SitePoll placement is an open intent folded into decision row 2 (homepage sections).

- **Community Spotlight model + Feature-55 build scope (row 2's spotlight half; row 3's
  55-remainder verdict)** — **resolved 2026-07-11** (Brian, in chat over several rounds). The
  Gemini pledge-drive design (GeminiDiscussions Sept–Nov 2025 §VIII) is requirements-spirit only;
  implementation is first-principles — spec §5.26's "simplified to direct donations" stands, now
  with mechanics. Settled: donation-funded slot *grants* with donations DEFERRED
  (`ISpotlightSlotAllocator` is the seam — mods grant now, the payment pipeline becomes a second
  grant source later); the donor/awardee picks **someone else's** story (self-recommendation ok,
  self-story never); the display is an additive composition of existing things (Story + optional
  Recommendation with its recommender attribution); **discrete calendar blocks × N concurrent
  homepage positions** (N mod-set now, activity/cost-scaled later), schedulable future start,
  per-story cooldown; knobs are DB-backed mod-editable `site_settings`; three notifications
  (grant inline; story-author + recommender at go-live via a worker). No algorithmic homepage —
  site-mission constraint. Homepage scope: the spotlight section only; row 2's homepage-design
  remainder stays open. Detail: `audit/Spotlight.md` (settled-vs-open); conventions:
  `layer2-services.md` §"Community Spotlight" + §"Site Settings"; build: `workplan.md`
  WU-Spotlight.
- **Open Graph / social-sharing meta tags (addendum §3 #15/#17, WU-Seo)** — **resolved 2026-07-11**
  (Brian). Scope: OG + Twitter card + `<meta name="description">` on all shareable content pages
  (Stories, Chapters, Profiles, Series, BlogPosts, Groups); mature-content `noindex` (addendum #18)
  deliberately deferred to a follow-up unit. Absolute `og:url`/`og:image` resolve via a **configured**
  `Site:PublicBaseUrl` (not `NavigationManager.BaseUri`) — request-derived URLs are unsafe behind the
  Cloudflare→DigitalOcean topology and don't generalize to N≥2 droplets. `og:image` reads a separate
  `ImageStorage:PublicBaseUrl` (defaults to the site base) — wired now as the seam for a future
  direct-R2/CDN image-serving migration, even though images are same-origin-through-the-app today.
  Full reasoning: `audit/Seo.md`, `render-and-layout.md` §"Social Meta Tags (Open Graph)".

- **Saved Tag Selections scope (Feature 15, WU43)** — **resolved 2026-07-11** (Brian). Persists only the
  tag include/exclude axis (not text/sort/interactions — those are owned elsewhere or transient); one
  unified selection spans all tag types, not per-type; no per-user cap; `Description` is bounded plain
  text (280 chars), not rich HTML; Load (searchable/sortable flyout) and Save (compact dialog) are
  separate surfaces mounted once in `TagFilter`'s header, reaching all four `ResultsFilterPanel`
  consumers for free; sharing is copy-on-write onto a dedicated profile tab, no public gallery. Full
  reasoning: `audit/Tags.md` Feature 15, `layer2-services.md` §"Saved Tag Selections Persist Only the
  Tag Axis".

- **Automatic Tree Search UI scope + spec §5.26 filter-composition gap (Feature 59, WU44)** —
  **resolved 2026-07-11** (Brian). Scope: ship the Unified Tree Search Page shell
  (`/discover/me`, `/discover/user/{userId}`, `/discover/story/{storyId}`) + the working Automatic
  tab now; the Manual tab (Feature 33 / WU40) is a placeholder in the same shell until its
  from-scratch graph component lands. Results reuse `StoryDeck` + a degree badge, not a bespoke
  tree-results list. Filter composition: spec §5.26 says tags/FTS/interaction "compose with the
  data mart query," but the Stage-5 `TreeSearchRequest` accepts only root/degrees/edge-types/sort/
  cap. Resolved via the three-axis model — tree search is a **Source** (the rCTE), `StoryFilterDto`
  is the **Filter**, degree/random are the **Sort** — composed by handing the rCTE's raw reached
  set to the existing `ApplyFilters` predicate (never duplicating tag/FTS SQL into the rCTE). Full
  design + rejected alternatives: `audit/Discovery.md` Feature 59, `layer2-services.md` "Tree
  Search — Automatic Tab Composition (WU44)".

Newest first. Every entry points at the doc that now states the rule. Entries up to 2026-07-05
are carried forward from `middle_plan.md` (which carried 2026-07-01-and-earlier entries from
`forward_plan.md`) — a few long entries lightly condensed with their full technical framing
intact at the named pointer; `middle_plan.md` remains the unabridged historical record.

- **`SiteDailyStat`/`DailyStoryStat` requirements + `active_users`/"last seen" privacy stance
  (Feature 62)** — **resolved 2026-07-10** (Brian, WU-SiteDailyStat planning session), superseding
  the earlier "raw-SQL mart, no EF model, matching the other three Layer-8 marts" framing.
  `SiteDailyStat` now gets an EF entity + `DbSet` + migration — the one documented Layer-8
  exception, because it's append-only ground truth with rich time-series reads (a user-facing
  dashboard), not a rebuildable mart; the daily worker still writes it via raw SQL only. Full
  counter set (incl. the `new_`/`total_` stock-vs-flow rule) settled by counter-by-counter source
  audit against the live schema. **Privacy stance for `active_users`:** `User.LastActiveUtc` is
  stamped for authenticated requests only, riding the existing strictly-necessary auth-session
  cookie — first-party functional data, not tracking-cookie/ad-tech, so no consent banner is
  needed; consistent with the ad-free community ethos. `DailyStoryStat` (a different, separate
  table from the same Gemini discussion) stays dropped entirely — not to be confused with this
  one. Detail: `layer8-data-marts.md` §`site_daily_stats`, `layer2-services.md` §"Signal
  Buffering", `layer1-data-model.md` §"Column Conventions", `audit/Moderation.md` Feature 62.

- **Error-handling UX + strategy (decision row 9)** — resolved (2026-07-06, Brian, design
  conversation per the Stage-1 venue): four forks settled. (1) **Scope split** — the circuit-side
  UX half builds now (testable on today's InteractiveServer surface); the `ProblemDetails`
  envelope + client HTTP translation defer to a Phase-5-adjacent follow-up (no HTTP error surface
  exists until the WASM client makes those calls). (2) **Layered island error boundaries** — page
  + chrome + card + comment-section, not global-only; a fault degrades its island, the circuit
  and every other island survive. (3) **Hybrid feedback channels** — inline for form/validation,
  a minimal toast channel only for transient non-blocking system events. (4) **Editor draft
  safety = device-local localStorage autosave + restore** (survives teardown/reload/crash,
  identical under Server and WASM; `[PersistentState]` rejected — prerender-handoff-only), on top
  of editor-scoped containment. Secondary rules: only typed user-facing exceptions surface their
  message (`ExceptionPresenter`, generic + on-screen error id = trace id otherwise); retry of
  *user operations* is manual only ("Try again" affordances — never automatic re-submit; the
  pre-existing Npgsql `EnableRetryOnFailure` connection-level retry is unchanged); the
  last-resort surfaces
  (`#blazor-error-ui` — restored to `App.razor`, it was stranded in the Identity-only
  `MainLayout` leaving interactive pages with no teardown surface — and `ReconnectModal`) adopt
  the design language. Rule: `error-handling.md` §"Error Handling Strategy" (UX + containment),
  `logging.md` §"Unhandled exceptions" (server-side contract).

- **Email mechanism = pluggable SMTP seam, provider decision deferred to Phase 7 (decision row 8
  mechanism half)** — resolved (2026-07-06, Brian, WU-Email planning): rather than picking a
  transactional provider now, the `IEmailSender<User>` seam is provider-agnostic SMTP
  (`Email:Provider` = `Smtp`/`NoOp`, mirroring the `ImageStorage:Provider` switch) — every
  candidate provider (Postmark/SES/Resend/SendGrid/Mailgun) exposes SMTP, so the prod choice
  becomes host+credentials in config with no code change. **Scope is transactional-only**
  (confirmation, password reset, email-change) — notification email fan-out (the inert
  `EmailEnabled` per-user setting) is explicitly deferred to a follow-up WU, not bundled in.
  **Dev inbox: Mailpit via Aspire** (same `AddContainer` shape as Garage), so the whole flow is
  browser-verifiable with zero external accounts; the server-only path keeps the existing
  `IdentityNoOpEmailSender` fallback (its on-page confirmation link in `RegisterConfirmation.razor`
  is already gated on `is IdentityNoOpEmailSender`, so it self-corrects once a real sender is
  configured — no change needed there). The provider + sending domain choice (decision row 8
  residual) stays open, moved to Phase 7 since it's now config-only. Rule: `identity-and-authorization.md`
  "Identity & Auth"; `audit/Identity.md` WU-Email Stage note.

- **Upload validation = magic-byte sniff + ImageSharp decode/re-encode (WU-Security scope)** —
  resolved (2026-07-06, Brian, WU-Security planning): the Phase 1 item 6 wording ("magic-byte
  sniffing") is expanded to the full pipeline — sniff (sniffed format authoritative over the
  browser's claimed MIME) + decode/re-encode via SixLabors.ImageSharp + header-level
  decompression-bomb guard + EXIF strip + downscale to a stored ceiling. Sniff-only was weighed
  and rejected: signature checks are beatable by prepending valid magic bytes to a polyglot
  file, and re-encode also closes `LocalImageStorageService`'s CanSeek-gated size-cap bypass
  through the same shared step. SVG stays off the allow-list permanently. Rule:
  `canalave-conventions/security.md` §"Upload Content Pipeline".

- **Write throttling lives at the L2 service layer, not (only) HTTP middleware (WU-Security
  scope)** — resolved (2026-07-06, Brian, WU-Security planning): Phase 1 item 6's "rate
  limiting on auth/upload/comment endpoints" assumed those were HTTP endpoints; uploads and
  comments actually travel over the SignalR circuit, which HTTP rate-limiting middleware never
  sees — and the settled `InteractiveAuto` end state keeps the circuit path alive permanently
  even after the Phase 5 WASM flip. Therefore: per-user token-bucket throttling
  (`IWriteRateLimitService`) enforced inside the L2 write services (one transport-agnostic
  point covering circuit now + HTTP endpoints later), with HTTP middleware limiting reserved
  for the surfaces that are genuinely HTTP today (`/Account/*` auth form posts per-IP,
  `/api/tags` writes). Endpoint-only limiting (deferring to Phase 5) was weighed and rejected —
  it leaves writes unthrottled until the flip and the residual circuit path uncovered forever.
  Rule: `canalave-conventions/security.md` §"Write Throttling" / §"HTTP Edge Rate Limiting".

- **Data Protection keys persist to Postgres unencrypted (WU-DataProtection scope)** — resolved
  (2026-07-06, Brian, WU-Security planning): `PersistKeysToDbContext<ApplicationDbContext>` +
  `SetApplicationName`, deliberately with **no** `ProtectKeysWith*` at-rest key encryption. On
  Linux there is no DPAPI; a self-managed certificate adds key-management burden against a
  threat (DB-read access) that already implies full compromise of what the keys protect.
  Revisit only if DB backups ever land somewhere less trusted than the database. Rule:
  `canalave-conventions/security.md` §"Data Protection Keyring".

- **Production telemetry destination = self-hosted Grafana LGTM container (decision row 7)** —
  resolved (2026-07-06, Brian, during WU-Observability planning): one `grafana/otel-lgtm` container
  on the droplet, deployed at Phase 7. Deciding criterion: the consumer is *Claude queried
  on demand* when Brian points it at an issue — not Brian reading dashboards — so what matters is
  agent-queryable HTTP APIs, which LGTM provides natively (Loki logs / Tempo traces / Prometheus
  metrics, each curl-able over SSH to the droplet with no exposed endpoints; official Grafana MCP
  server available if richer access is ever wanted). Full metrics support retained deliberately —
  the signal-buffer flush workers (WU-SignalBuffering) need buffer-depth/flush-latency/batch-size
  metrics to be trustworthy.
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

- **Layer 7 dissolved; Redis exorcised (supersedes "L7 pilot = View Count")** — resolved
  (2026-07-06, Brian, WU-SignalBuffering): a first-principles audit of the deferred L7 assumptions
  found (a) the UserStoryInteraction write-behind's stated rationale ("batch writes to protect the
  read-hot table from locks") was designed under SQL Server hours before the Postgres switch and is
  **void under MVCC** (writers never block readers; the surviving costs — index amplification,
  dead-tuple bloat — scale with write rate, and interactions are low-write durable intent behind a
  2s client debounce); (b) Redis itself entered via the Aspire template, not a measured need. The
  layer redistributed: lossy coalescable signals (F44 reading progress — the true flagship, every
  active reader; F45 view counts) = **in-process L2 signal buffers** (built 2026-07-06, no external
  store); F16 = durable direct write permanently; LastReadDate = never stored (recency derived via
  `MAX(uci.last_interaction_date)`, `DefaultSortOrder.RecentlyRead`); Also-Favorited read cache =
  the L8 mart itself, no app-tier cache ever. **View count is never a sort key** (would recreate
  the popularity snowball — same philosophy as removed likes/no-sort-by-favorites): non-sortable,
  on-demand reveal from the StoryCard dropdown, accumulated in `daily_story_stats` (per-story/day,
  migration-managed raw DDL, no EF model, partition-ready by stat_date; `Story.ViewCount` +
  chapter/blog copies dropped). Forward constraints: at **N≥2 web nodes** buffer bodies swap to a
  shared **Valkey** store (open-licensed, DO-managed; Redis relicensed off open source 2024) behind
  unchanged interfaces — the Aspire `cache` container stays provisioned for that day, nothing
  consumes it at N=1; when a read replica lands, route `StoryEditorPage`'s post-save reload off the
  read context (`ServerStoryReadService.GetStoryForEditAsync` — the one read-your-writes-exposed
  path; all other edit surfaces are optimistic local state). Rules: `layer2-services.md` §"Signal
  Buffering", `layer6-indexes.md` §"MVCC Storage Tuning", `grid_axes.md` "Layer 7 — dissolved",
  conventions SKILL.md axiom 7, `canalave-conventions/horizontal-scaling.md` (full N≥2 story: the
  Valkey swap, load-balancer session affinity, why no SignalR backplane is needed). Prior
  2026-07-05 sequencing (Redis battle test, `redis-cli` ground truth, `AddRedisDistributedCache`)
  is historical.

- **SignalR permanently ruled out for private messaging (supersedes WU-SignalR)** — resolved
  (2026-07-07, Brian): messaging will never use SignalR — Discord already serves the real-time-chat
  need, and this site's private messaging is deliberately for substantive, async, long-form
  conversation, the same reasoning that already kept group conversations off-site. This hardens
  WU35's original "SignalR deferred post-MVP" framing into a permanent decision: `MessagesHub` (the
  only app-level SignalR Hub ever proposed anywhere in this project) is cancelled, not shelved.
  Consequence for horizontal scaling: since no app-defined Hub exists or ever will, there is no
  SignalR backplane need at N≥2 — only load-balancer session affinity for Blazor Server's own
  circuits remains relevant, which is a different concern entirely (a backplane routes cross-node
  Hub broadcasts; affinity keeps a circuit's requests routed to the node that holds it — the two
  were being conflated). Rule: `cross-cutting.md` §"Private Messaging Architecture",
  `canalave-conventions/horizontal-scaling.md` (new file — also consolidates the previously
  scattered Valkey N≥2 body-swap notes from `SKILL.md`, `layer2-services.md`, `cross-cutting.md`,
  and `grid_axes.md` into one place). `middle_plan_v2.md` Phase 1 item 8 marked REMOVED above;
  `workplan.md`'s Post-MVP "Messaging realtime push (SignalR)" entry marked REMOVED; `audit/Messaging.md`
  WU35 note #2 updated; `grid_axes.md`/`folder_clusters.md` Feature 49 descriptions updated.

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
  *Amended 2026-07-06:* the Redis component is superseded ("Layer 7 dissolved" above) — the
  droplet runs the server alone; a Valkey container joins only at the N≥2 / measured-need trigger.
- **S3 image storage before launch** — resolved (2026-07-03): `S3ImageStorageService`, dev S3
  endpoint via Aspire (Garage per the 2026-07-05 entry above), R2 in prod, behind the frozen
  `IImageStorageService`. Built 2026-07-05 (WU-S3Garage). See `audit/ImageStorage.md`.
- **Aspire returns for orchestration** — resolved (2026-07-03): AppHost with Postgres + Redis +
  blob store, pre-beta. The MVP-era pivot off Aspire (2026-06-20) governed the MVP only; WU12's
  anti-pooling ruling (plain `AddDbContext`, no Aspire EF client package) survives inside the
  orchestrated setup. Built 2026-07-05 (WU-Aspire).
- **Marts not required for Manual Tree Search** — resolved (2026-07-03, reaffirming WU28
  Phase 0; reaffirmed 2026-07-07 with the marts now actually being built): WU40 pivots statelessly
  over live tables; marts feed only F59/F61 + workers. Strengthened rationale: manual is degree-1
  interactive (must be fresh — the mart is a daily rebuild) and needs edge detail (recommendation
  blurb, spotlight context) the IDs-only mart cannot carry. See `audit/Discovery.md` Feature 33.

- **Horizontal line crossed / discovery mart family design** — resolved (2026-07-07, WU-Marts
  Phase 0, Brian): F59/F61 are built pre-beta against **synthetic clustered seed data**
  (`TheCanalaveLibrary.SeedTool`) rather than waiting for real users — uniform-random volume
  stays degenerate; the clustered distribution (taste-communities, power-law, supernodes,
  hidden-gem chains) is the actual requirement. Design decision set (full detail:
  `layer8-data-marts.md` + `audit/Discovery.md` F59/F60/F61/F33): rCTE affirmed over the
  **narrow** `(user_id, story_id, edge_type)` edge-list mart (supersedes the wide-boolean
  schema); six-edge taxonomy, **every edge worth 1, no weights** (diverges from spec §5.4's
  "scoring weights" — two sort orders random/by-degree instead); **vouch = projection onto the
  vouchee's published stories** in both tree searches (supersedes spec §5.8 "strengthen edge
  weights"; mid-tier — ≤5 vouchees but unbounded stories each); **author-spotlight first-class**
  beside hidden-gem (both ≤5-capped = the chain-of-trust deep edges); path materialization
  service-required, capped-edges-only; hidden favorites → plain Favorite edge iff edge-owner
  consent (no "boosted" flag); rating + searcher exclusion at the presentation join; Apache
  AGE / pgvector recorded as deferred future options only; UI + Manual Tree Search build (WU40)
  stay deferred; verification fully headless.
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
  Convention: `content-safety.md` "Content Rating Filtering."
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
  fix. See `render-and-layout.md` "ThemeContext Cascading Provider", `layer2-services.md` "Sprite
  URLs Are Resolved At Render Time", `audit/ImageStorage.md`.

- **WU37 Story Tagging — architecture, scope split, naming** — resolved (2026-06-25):
  F9/10/15 carved to WU41/WU42/WU43; Character→`StoryCharacter` (not `StoryTag`);
  pairing→`StoryCharacterPairing`; `TagTypeEnum.Relationship` removed; service-layer enforcement
  only; `ApplyFilters` character branch. See `layer2-services.md` "Structured Tag Authoring &
  Legality Enforcement" / "Structured Tag Authoring — Per-Type Filter Branch."

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
  split (F53 → WU39). See `content-safety.md` "Moderation Model", `layer2-services.md`
  "Notification Generation", `audit/Moderation.md` Feature 53.

- **Moderator role assignment in dev seed** — resolved (2026-06-24, WU27.5): role *rows* are
  already seeded via `ApplicationRoleConfiguration.HasData`. WU27.5 assigns `AdminUser` to both
  `"Moderator"` and `"Admin"` in `DataSeeder.cs` — role gate is now exercisable end-to-end.
  Admin-inheritance expressed by listing both roles (IsInRole is literal). See
  `identity-and-authorization.md` "Role-Based (Moderator) Gating."

- **WU32 Groups — five decisions** — resolved (2026-06-24): `AudienceRating`/`MaxContentRating`
  split; open join, permanent; Member+Admin only (no Moderator — permanent); group blog posts in
  WU32; per-context comment methods. See `content-safety.md` "Group Audience-Visibility
  Filter", `layer2-services.md` "Group Membership and Role Model"/"Group Rating
  Waterfall"/"Group Comments", `audit/Groups.md` WU32.

- **Active-user-conditional handling + two content-editing patterns** — resolved (2026-06-23):
  `IActiveUserContext` server-only; ownership = identity equality, inline `@if`; view/edit-page
  split for Story/Chapter; in-place inline for comments/recs/vouch. See `identity-and-authorization.md`
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
  dedup. See `layer2-services.md` "Notification Generation" (filtering semantics + generation
  mechanism).

- **Notification in-app toggle dropped (§5.18 deviation)** — resolved (2026-06-23, WU22): the
  spec §5.18 "in-app toggle" is not implemented. `UserNotificationSetting` stores only
  `EmailEnabled` and `Collapsed`; in-app delivery is always-on (after drop-self, dedup). No
  `InAppEnabled` column will be added. Deviation recorded in `audit/Notifications.md`.

- **`Story.ChapterCount`** — resolved (2026-06-22, WU17): **not a denormalized column.** A
  count of published chapters is computable via `c.Chapters.Count(ch => ch.IsPublished)` in any
  EF projection. If the subquery becomes a hotspot, the remedy is an L6 partial index on
  `(story_id) WHERE is_published`, not a counter column. See `audit/Chapters.md` Feature 6 L2
  Stage note.

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
  progressive disclosure UX; `ChapterContent.Rating?` nullable. See `layer3.5-structure.md` "Chapter
  Versioning — Progressive Disclosure."

- **WU33 Notification UI** — resolved (2026-06-24): rich flat DTO + normalized target pair;
  two-pass batch enrichment; grouped + flat feeds; bell flyout (UserCard caret pattern); per-row
  settings save. See `layer2-services.md` "Polymorphic RelatedEntityId",
  `layer3.5-structure.md` "Notification Presentation Model", `audit/Notifications.md` Feature 42.

- **WU30 Profiles + theme-selection — seven decisions** — resolved (2026-06-24):
  `IUserSettingsService` self-referential exception; UserStats counter wiring; profile comment
  wall as 4th `CommentSection` context; tabbed page shape; blog-tab owner/viewer distinction;
  `IThemeReadService.GetThemesAsync`; `Profiles/` cluster added. See `layer2-services.md`
  "Self-Referential Editing Exception"/"UserStats Updates",
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
  Features 35/36/56, `identity-and-authorization.md` "Two content-editing patterns."

- **Test strategy** — resolved (2026-06-22, updated post-WU12.5): three tiers by kind — Unit
  (directly-constructed, no host/DB), Integration (Testcontainers Postgres +
  `WebApplicationFactory` + `IActiveUserContext` fake), RazorComponents (bUnit); never EF
  InMemory/SQLite. See [canalave-conventions/testing.md](skills/canalave-conventions/testing.md).
