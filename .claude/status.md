# Status Grid — Feature × Layer → Stage

Dashboard only. Stage values per CLAUDE.md (1–6 or N/A). Rows are the dependency-ordered
features from `grid_axes.md`, grouped by folder cluster. Columns:

`L1 | L2 | L3-Logic | L3.5-Structure | L4-Style | L4.5-Browser | L5 | L6 | L8`

(L7 — formerly "Redis Integration" — dissolved 2026-07-06, WU-SignalBuffering: redistributed into
L2 signal buffers / L6 MVCC tuning / L8 marts; L8 keeps its historical number. Detail:
`grid_axes.md` "Layer 7 — dissolved".)

L4.5-Browser is the end-to-end browser-verification band — defined in `grid_axes.md`
"Layer 4.5 — Browser Verification".

Global conditions affecting many cells — kept terse; detail lives at the pointer, not here:
- **Tag-model overlay reshape + fanonization pipeline (WU-TagFanon, 2026-07-26).** The per-story
  tag overlay is now one concept in two shapes: `CustomName` (gated by the single new
  `Tag.AllowCustomName`) + `Nuance` (never gated, every tag type), on `StoryTag` and
  `StoryCharacter`. `SettingDetail` deleted (folded onto the junction); `OcName`/`OcBio` renamed;
  pairing members became row indexes. Hierarchy roll-up now applies in `ApplyFilters` (a parent
  matches its children, symmetric) and a ship-filter axis exists. **Deliberate Stage-4 reopening of
  the WU37 "settled" routing table** — recorded in `audit/Tags.md`'s header note, not slipped
  through. No cell Stage changed: every affected cell was Stage 5 and remains 5 (F11/F12 L4 stay
  Stage 1 — the new `/fanon`, `/tag-adoptions` and overlay surfaces join the standing Phase-3
  visual pass unsigned-off). Detail: `layer2-services.md` §"Structured Tag Authoring" +
  §"Tag Hierarchy Roll-Up"; `audit/Tags.md`; `workplan.md` WU-TagFanon.
- **Parent-visibility invariant established and swept (WU-ParentVisibility, 2026-07-26).** Child
  content is never more visible, nor more writable, than the parent that hosts it — now conditionality
  kind (g). 38 surfaces across 12 clusters were violating it (13 reads, 25 writes), all fixed; three
  guards join `ProfileVisibilityGuard`. Two root causes: bare-FK queries never expand the parent so no
  named query filter can apply, and `writeDb` carries no visibility filters at all, making every
  existence check on the write path prove nothing. **No cell Stage changed** — every affected cell was
  Stage 5 and remains 5, which is exactly the hidden-deferral shape. Enforcement is
  `ParentVisibilityContractTests` (27 tests), not prose: the rule already existed in `layer2-services.md`
  and a sweep still missed it. Detail: `identity-and-authorization.md` §"Parent-visibility guards";
  `workplan.md` WU-ParentVisibility.
- **Viewer access-gating model live (WU-AccessGate + WU-AccessGate2, 2026-07-23/24).** Feature 66
  minted; the three-plane model (Discovery zero-trace / Direct-nav consent interstitial / Personal
  never rating-filtered) enforced cross-cluster: `ProfileVisibilityGuard` + guards in seven read
  services, `IActiveUserContext` surface additions consumed everywhere, gated-existence reads on
  Story/Chapter/Group/BlogPost, the `"StoryStatus"` named filter, `MatureDisclosureLine` on person/
  collection listings, spotlight M/non-M slot pools, durable per-item reveals. Detail:
  `design/access-gating-first-principles.md` (authoritative model), `audit/AccessGate.md`,
  `workplan.md` WU-AccessGate.
- **Desktop/Mobile fork paradigm removed — single responsive site (WU-ResponsiveMerge, 2026-07-18).**
  Device detection deleted; nine `{X}Desktop`/`{X}Mobile` pairs merged into their pages;
  coordination-composite tier folded into pages; `MainLayout` is the only layout. No cell Stage
  changed (touched cells were Stage 5 and remain 5). Narrow-viewport rendering is provisional
  (graceful degradation only) pending the future mobile phase — L4.5 claims cover desktop width.
  Detail: `middle_plan_v2.md` §Resolved; `render-and-layout.md` §"Responsive Layout Architecture";
  `workplan.md` WU-ResponsiveMerge.
- **Design solidification COMPLETE (WU-DesignSystem, 2026-07-10).** Phases B–F landed same-day
  after the gate: all 17 RichTextView/EditorView sites on `ContentSurface` (side-rails paper;
  MessageItem de-bubbled); `ReaderDisplayProvider` wired — the reader-settings cascade has a
  provider for the first time — plus the new `ReadingBackground` override (L1 JSON field +
  migration, settings select, ContentSurface consumption); `action`/`mission` families replaced
  `primary`/`accent` site-wide (bridge deleted); Interaction States grammar swept (one neutral
  hover, global focus-visible ring, z-ladder/backdrop/shadow tokens, uniform flyout dismissal
  via `dismiss.js`); Identity fully restyled (31 pages + Shared, Bootstrap debris deleted);
  `scripts/check-design-tokens.ps1` in CI. `dotnet test` 1406/1406 (479+514+413); token check
  green. L4 cell Stages unchanged — visual sign-off of swept pages is the standing human pass.
  Detail: `workplan.md` WU-DesignSystem; `layer4-style.md`; `surface-registry.md` §"Sweep
  completion".
- **Spec supersedes stale code.** No Stage-4 cells remain in the grid (last one cleared 2026-07-27, `audit/Lookups.md`); the adjudication doctrine is retained in `audit-summary.md` §0/§3 for any future reopening.
- **L4-Style blocker cleared (Tailwind v4 tokens locked).** Each rides along inside its feature's Phase-E work-unit. Detail: `layer4-style.md` §"Prerequisite: Design Tokens", `middle_plan_v2.md` §Resolved (carried forward from forward_plan Phase C).
- **L1 migration-verified.** Every L1 Stage-5 cell below has an applied migration (originally the clean `InitialSchema` generation; later WUs added named migrations). Detail: `layer1-data-model.md` §"Fluent API Organization."
- **Rows 19, 29 reclassified (Phase B).** Detail: `audit/Following.md`, `audit/Recommendations.md`.
- **Workplan exists.** `.claude/workplan.md` sequences the build; rows 8/37/51/55 blocked/deferred. Per `middle_plan_v2.md`'s inversion (2026-07-05), platform-layer work (L2 signal buffering, L6 tuning, L8 marts) landed in Phase 1 ahead of several MVP-surface-completeness rows still pending in Phase 2 — "post-MVP" no longer describes the actual sequencing; see `middle_plan_v2.md` "Why v2 exists". Planning artifact — no cell Stage changed by this.
- **First real app run (WU0) fixed 3 startup bugs; dev pivoted off Aspire for MVP.** Detail: `audit/Stories.md`, `render-and-layout.md` "Render Mode", `middle_plan_v2.md` §Resolved (Aspire-MVP, carried forward), `.claude/skills/run-server/SKILL.md`.
- **Legacy technical-layer folders being retired to vertical clusters just-in-time (WU2).** No new file added to deprecated folders; touched files migrate as part of each work-unit. Detail: `canalave-conventions/SKILL.md` "Code Organization".
- **Cross-cutting infra minted (WU12): `IActiveUserContext`, content-rating named query filter, `IImageStorageService`.** Aspire Npgsql EF pooling removed; plain `AddDbContext` is standing registration. Detail: `identity-and-authorization.md` "Active-User Context", `content-safety.md` "Content Rating Filtering", `layer2-services.md` "DbContext Registration".
- **TPT denormalization retrofitted (WU31.5, 2026-06-24).** Discovery columns moved from base to child tables; named filter removed from `BaseBlogPost`. Detail: `layer1-data-model.md` §"Denormalization with TPT", `audit/BlogPosts.md`, `audit/Comments.md`.
- **Three-tier automated test suite in place (WU12.5, 2026-06-22 backfill).** `dotnet test` runs Unit + Integration + RazorComponents; obligation advisory, no Stage gate. Detail: `canalave-conventions/testing.md`, `workplan.md` WU12.5.
- **Integration test isolation overhaul (2026-06-24).** Respawn reset + `IntegrationTestBase` + GUID seeding across all 19 classes. Detail: `canalave-conventions/testing.md` §"Integration tests reset between every test".
- **TestAppFactory DB-wiring + TPT phantom-nav fixed (2026-06-25, WU31_5b); 298 integration tests green.** Detail: `testing.md`, `audit/Comments.md`, `audit/Groups.md`.
- **Content-safety filter revamp done (2026-06-27).** All display filters moved to `ReadOnlyApplicationDbContext` only; write context sees ground truth. Detail: `audit/Stories.md`, `content-safety.md` "Content Rating Filtering".
- **Pre-integration cleanup done (WU37.5, 2026-06-26).** Renamed `IsTakenDown`/TakedownDate; sprite singleton cache; `CharacterRelationshipType` deleted. No grid changes. Detail: `workplan.md` WU37.5.
- **Component-soundness wave done (WU-ComponentSoundness, 2026-06-27).** Three compile-clean Stage-5 patterns corrected: F1 (route-param dispatchers now reload in `OnParametersSetAsync` — ProfilePage, BookshelvesPage, GroupPage, BlogPostPage, StoryPage, ChapterReadingPage), F2 (StoryDeck `@key` — fixes `UserStoryInteractionPanel._localState` data-corruption on list swap), F3 (CommentSection `@key` — fixes `_isRevealed` spoiler-state bleed on pagination). No stage-number changes (cells were already Stage 5 — the wave closes a latent correctness gap). `dotnet test` 1235/1235 pass (446 RazorComponents + 437 Unit + 352 Integration). Detail: `workplan.md` WU-ComponentSoundness; audit notes in `audit/Stories.md`, `audit/Comments.md`, `audit/Recommendations.md`, `audit/Profiles.md`, `audit/Groups.md`, `audit/BlogPosts.md`, `audit/UserStoryInteractions.md`.
- **First browser-debugging wave done (WU-BrowserPass, 2026-07-01).** Read context now factory-per-method (`AddDbContextFactory`, scoped — supersedes spec §6.6, plain `AddDbContext` remains for the write context); all 987 Tailwind token classes converted to v4 `-(--token)` syntax; chapter editor, comment composer, dev-login fixes. All fixed same-session — no grid changes. Login, navigation, authoring→reading, social, and mod flows verified in a real browser. `dotnet test` 1238/1238 (355 Integration incl. 3 new concurrency regressions). Detail: `workplan.md` WU-BrowserPass; `layer2-services.md` §"Read-Context Concurrency"; `layer4-style.md` §"Consuming tokens in classes"; `canalave-conventions/debugging.md`.
- **`forward_plan.md` retired (2026-07-03); `middle_plan.md` retired (2026-07-05); `.claude/middle_plan_v2.md` is the live master plan** (platform-first → features → beta → launch — infrastructure lands before new feature work now that the functional site makes it testable; adds observability/logging, email, error-handling, CI, security, data-protection, launch-readiness work-units). Planning artifact — no cell Stage changed. Detail: `middle_plan_v2.md` "Why v2 exists".
- **L5 WASM pilot landed (WU-L5Pilot, 2026-07-04); rollout strategy settled same day.** `layer5-wasm.md` is battle-tested (was provisional design intent): Tags-cluster endpoints + client HTTP services + serialized auth verified end-to-end in a real WASM runtime. Rollout is per-feature headless builds → **one global `InteractiveAuto` flip** (no long-lived mixed mode; the pilot's island directives on `/tags` were removed after verification — the page rides global `InteractiveServer` until the flip). Rule: `layer5-wasm.md`. Detail: `workplan.md` WU-L5Pilot; `audit/Tags.md` F11/F13; `audit/Discovery.md` F34; `middle_plan_v2.md` §Resolved.
- **Aspire orchestration path live (WU-Aspire, 2026-07-05).** AppHost (Aspire 13.4.6, SDK+packages aligned) runs containerized Postgres 5433 + Redis + Garage S3 + web on 5028; server-only path unchanged and still the default. No cell changes. Detail: `run-server/SKILL.md` "Aspire path", `cross-cutting.md` "Aspire 13 Configuration", `workplan.md` WU-Aspire.
- **S3 image storage live (WU-S3Garage, 2026-07-05).** `S3ImageStorageService` (AWSSDK.S3) behind the frozen interface — Garage in dev (Aspire path; supersedes spec's MinIO — OSS archived), Cloudflare R2 in prod; provider switch `ImageStorage:Provider`, server-only path stays `Local`. Detail: `audit/ImageStorage.md`, `workplan.md` WU-S3Garage.
- **Sticky top-bar nav chrome built (2026-07-01; lives in `MainLayout` since the 2026-07-18 responsive merge).** Brand, Home/Discover/Tags/Groups, `CreateMenu`, `UserMenu`; no dedicated grid row (persistent-layout chrome, not a tracked feature). Detail: `layer4-style.md` Pattern Accumulation.
- **Logging & telemetry conventions live (WU-Observability, 2026-07-06).** OpenTelemetry additive pass: Npgsql per-query spans + Blazor circuit sources/meters subscribed; `CanalaveTelemetry` per-component custom sources (Core/Diagnostics, pilot: ImageStorage); `TelemetryCircuitHandler` scope enrichment; silent-catch sweep complete (best-effort swallows = Warning with IDs; one sanctioned-silent site). Decision row 7 resolved: Grafana LGTM, deploy Phase 7. No cell Stage changed. Detail: `canalave-conventions/logging.md`, `workplan.md` WU-Observability, `middle_plan_v2.md` Resolved.
- **Security hardening + Data Protection keyring live (WU-Security + WU-DataProtection, 2026-07-06).** Upload sniff+re-encode (`ImageUploadProcessor`, ImageSharp pinned 3.1.x), per-user write throttle at L2 (`IWriteRateLimitService`), HTTP edge limits on `/Account/*` + tag writes, security headers/CSP (enforced prod, Report-Only dev) with the no-inline-`on*` rule, Identity lockout + cookie flags, keyring persisted to Postgres (`data_protection_keys`; one-time global sign-out on first deploy of this change is expected). No cell Stage changed. Detail: `canalave-conventions/security.md` (new), `workplan.md` WU-Security entry, `audit/ImageStorage.md`, `audit/Identity.md`, `middle_plan_v2.md` Resolved ×3.
- **CI + dependency automation live (Phase 0 WU-CI, 2026-07-05).** `.github/workflows/ci.yml` runs the full `dotnet test` suite on PRs + manual dispatch (not on master pushes — deliberate, see `middle_plan_v2.md` Resolved); `.github/dependabot.yml` groups the Aspire train + EF Core, weekly. `phase-a-foundation` merged into `master`; branch convention settled (commit to master directly). No cell Stage changed. Detail: `middle_plan_v2.md` Phase 0 + Resolved "CI hardening deliberately deferred to launch", `workplan.md` WU-CI.
- **Layer 7 dissolved; signal buffering live (WU-SignalBuffering, 2026-07-06).** First-principles audit of the deferred "L7 Redis" assumptions (SQL-Server-era lock rationale void under Postgres MVCC): L7 column removed from this grid — signal buffering → L2 (F44 reading-progress + F45 view-count in-process buffers built + tested; `layer2-services.md` §"Signal Buffering"), MVCC storage tuning + index audit → L6 (`R4_MvccStorageTuning`), Also-Favorited cache → L8's mart. F16 interactions stay durable-direct permanently; `Story/ChapterContent/BaseBlogPost.ViewCount` dropped for `daily_story_stats` (`R2_ViewCountToDailyStoryStats`); views are non-sortable/on-demand-only; Bookshelves Actively Reading sorts by derived recency (`RecentlyRead`). N≥2 body-swap detail (Valkey, session affinity, no SignalR backplane needed): `canalave-conventions/horizontal-scaling.md`. `dotnet test` 1335/1335. Detail: audit notes in `audit/Chapters.md` F44, `audit/Stories.md` F45, `audit/UserStoryInteractions.md` F16, `audit/Discovery.md` F61; `workplan.md` WU-SignalBuffering; `middle_plan_v2.md` Resolved.
- **Error-handling strategy live (WU-ErrorHandling, 2026-07-06).** Decision row 9 resolved (four forks: scope split circuit-UX-now/HTTP-at-Phase-5, layered island boundaries, hybrid inline+toast, localStorage editor autosave). Layered `CanalaveErrorBoundary` (page/chrome/card/comments islands), `ExceptionPresenter` message discipline (raw `ex.Message` in UI is now a defect), `InlineAlert`, minimal `ToastHost`, `DraftAutosave` on all four long-form editors, `#blazor-error-ui` restored to `App.razor` (was stranded in Identity-only MainLayout — interactive pages had NO teardown surface) + restyled with `ReconnectModal`; `DetailedErrors` dev-only; `/dev/error-playground` is the standing fault test bed. No cell Stage changed. Detail: `error-handling.md` §"Error Handling Strategy", `logging.md` §"Unhandled exceptions", `workplan.md` WU-ErrorHandling, `middle_plan_v2.md` Resolved.
- **L6 index batch + perf baseline live (WU-L6, 2026-07-07).** `L6_IndexBatch` migration (headline: the seven `user_story_interactions` filtered indexes had silently collapsed to ONE in the database — unnamed `HasIndex` calls on the same columns overwrite each other; six restored) + comment/notification/story/message indexes, measured before/after at SeedTool volume via the new rerunnable `TheCanalaveLibrary.PerfBaseline` fixture (comment paging −98.8%). Detail: `layer6-indexes.md` (rewritten against reality), `workplan.md` WU-L6, per-cluster audit L6 notes.
- **Horizontal line crossed; discovery marts + services live (WU-Marts, 2026-07-07).** The "needs real user data" deferral is superseded: `TheCanalaveLibrary.SeedTool` (standalone bulk-load console, never on startup/test paths) generates clustered synthetic data; the three discovery marts, daily worker, and F59/F61 service layers are built and headlessly verified. UI stays deferred. Detail: `layer8-data-marts.md`, `audit/Discovery.md` F59/F60/F61, `workplan.md` WU-Marts, `middle_plan_v2.md` Resolved.
- **Design tokens LOCKED (Phase A gate, 2026-07-10).** Role-based `@theme` manifest live (canvas
  vibrant grass; action family light-fill+dark-ink; mission surf blue held at 0.56 by the
  Brian-ratified AA-4.5-everywhere contrast policy; HP-trio indicators; feature accents +
  Pokémon-type tag tokens; Fraunces/Mulish self-hosted woff2; z-ladder + backdrop tokens; the
  `primary`/`accent` alias bridge was deleted at Phase B end — see the COMPLETE note above). New:
  `ContentSurface.razor` (Reading/Inline/Input) and `/dev/design-gallery` (Development-only living
  style reference; three canvas variants retained for beta testers). Gate reviewed live by Brian on
  the gallery. The element-role constitution (Canvas / Wayfinding / Container / Content Surface /
  Control / Indicator / Overlay) and per-component audit live at `.claude/design/surface-registry.md`.
  Detail: `layer4-style.md` §"Prerequisite: Design Tokens", `surface-registry.md`.
- **Bare-name dead-class sweep (2026-07-10).** 49 usages of `text-muted` (not a real utility — the token `--color-text-muted` generates `text-text-muted`, so the bare short name silently emitted no CSS) converted to the paren form across 17 files (16 SharedUI components + `UserCardTests`); the bare-name trap is now documented as the second silent-failure mode beside the bracket-form trap. RazorComponents tier 471/471 green (Unit/Integration blocked at run time by a VS-Insiders file lock on Server's output — no logic surface touched by the sweep). The once-dead `bg-surface-hover` family is resolved — `--color-surface-hover` is declared in the locked `@theme` manifest and consumers use the paren form. Detail: `layer4-style.md` §"Consuming tokens in classes" ("The bare-name trap").
- **Real transactional email live (WU-Email, 2026-07-06).** The beta-blocking `IdentityNoOpEmailSender`-only setup is closed: a pluggable SMTP seam (`Email:Provider` = `Smtp`/`NoOp`, mirrors `ImageStorage:Provider`) plus `SmtpEmailSender` (MailKit) sends real confirmation/password-reset/email-change mail; a Mailpit dev inbox joins the Aspire AppHost (server-only path keeps the `NoOp` fallback, whose on-page confirmation link auto-hides once a real sender is active). Scope is transactional-only — notification email fan-out (`EmailEnabled`) stays deferred. A real double-HTML-encoding bug (the confirmation/reset link's already-encoded `&` was encoded a second time, corrupting the `code` query parameter) was found and fixed during live Mailpit verification the same session. F1 Identity L4.5 stays Stage 5 but now genuinely sends mail instead of surfacing a NoOp dev link. `dotnet test` 1344/1344. Detail: `identity-and-authorization.md` "Identity & Auth", `audit/Identity.md` WU-Email Stage note, `workplan.md` WU-Email, `middle_plan_v2.md` Resolved "Email mechanism".
- **SiteDailyStat worker built — the Layer-8 mart family is complete (WU-SiteDailyStat, 2026-07-11).** Also minted `User.CreatedUtc`/`LastActiveUtc` (a third Signal-Buffering instance, authenticated-only — privacy reasoning in `layer8-data-marts.md`). Row-62 verification narrative: `audit/Moderation.md` Feature 62. Detail: `layer8-data-marts.md` §"site_daily_stats", `workplan.md` WU-SiteDailyStat.
- **Cross-cutting SiteSettings cluster minted (WU-Spotlight, 2026-07-12): `ISiteSettingsRead/WriteService`** — DB-backed mod-editable runtime knobs (string-key `site_settings` rows, seeded defaults paired with keys in Core). First consumer: Feature 55's five spotlight knobs. Detail: `layer2-services.md` §"Site Settings", `SKILL.md` "SiteSettings/".
- **L5 grid-mark correction (2026-07-12).** Rows 27–29 (Recommendations) and 38–40 (Groups) were mismarked L5 Stage 5 off service-layer test citations with no endpoint/client ever built; corrected to Stage 2. (All later re-reached 5 — 27–29 via WU-GlobalFlip; 38–40's Stage-2 premise itself turned out stale, reconciled by WU-GroupsL5 below.) Detail: `layer5-wasm.md` §"L5 Stage Semantics", per-row audit Stage notes.
- **Mechanical WASM API sweep (WU-L5Sweep, 2026-07-13): every `ServerXXXService` gets an HTTP endpoint + client impl** (add-only pass — no new per-feature tests, no global render-mode flip). Scope, structural exclusions, and the doc hardening this required: `layer5-wasm.md` (naming rule, extended exception table, POST-for-complex-reads incl. the `[FromQuery]`-sibling-array gotcha, `PagedResult<T>`, stream/multipart pattern). `dotnet build` clean on Core/Client(WASM)/Server; `dotnet test` full solution green (Unit 685/685, RazorComponents 619/619, Integration 650/650) — confirms the sweep compiles and the pre-existing suite still passes, not that the new endpoints/client impls are behaviorally verified. Touched L5 cells stay at their current number — this pass makes the code flip-ready, it does not itself earn Stage 5 verification. Detail: `workplan.md` WU-L5Sweep.
- **GLOBAL FLIP DONE (WU-GlobalFlip, 2026-07-13): the site runs `InteractiveAuto`** — circuit on first visit, WebAssembly on revisits (verified: `_framework/*.wasm` + zero `_blazor` WebSocket on the cached pass). Full `[PersistentState]` adoption across all data-loading pages/components (hydration confirmed by network log: primary data never refetched). The WASM browser wave found + fixed 7 bugs same-session (empty-body nullable reads, POST array binding, IStoryTag polymorphism, Blazored.Typeahead replaced by in-house `CanalaveTypeahead`, Quill same-component-redirect forceLoads, TreeSearchPage stale root, ReaderDisplayProvider static-SSR persistence 500ing `/Account/*`). L5 column flipped to 5 for all 40 built-surface rows (incl. 49/63 N/A→5 — both gained real client surfaces); rows 51/53/56 keep their unbuilt numbers. Known tooling false-alarm: the browser extension's network reader shows body-less 2xx as "503" — server log/DB are ground truth. Detail: `workplan.md` WU-GlobalFlip; `layer5-wasm.md` (new rules); per-feature audit L5 notes.
- **Modernization-audit Tier-1 + Tier-2 fix pass done (WU-AuditFixPass, 2026-07-18).** All 5 must-fix items (4 security: chapter-write/draft-read author gates, badge endpoint caller-scoping + `/award` unmapped, profile `includePrivate` derived server-side + bio visibility gate, story LongDescription sanitize-on-save; 1 UX: ReconnectModal asset path) and every Tier-2 fix-pattern (atomic counters, flush-on-dispose, `NotFound()` sweep, InlineAlert/ExceptionPresenter sweep, Moderation 403, silent-catch sweep + registry, `User.Roles` phantom-nav migration, `RequireUniqueEmail`) closed with regression tests; no Stage numbers change (touched cells were and remain Stage 5 — the audit's "proposes reopen" flags are resolved). Still open from the report: the systematic ~40-endpoint authz sweep, Tier-3 batch, code economy, BB-01/02/03 doc-touches. Detail: `workplan.md` WU-AuditFixPass; `modernization-audit/report.md`.
- **Modernization-audit closure pass done (WU-AuditFixPass-2, 2026-07-18).** The audit's #1 recommendation — the systematic endpoint-authorization sweep — is CLOSED: all 38 `*Endpoints.cs` files audited at authz depth (7 additional holes beyond the original 3 found + fixed: Story `/edit` + `/by-author` bypass, BlogPost `GetByAuthor`/`GetForEdit`, USI hidden-favorites, chapter draft-metadata, ManualTree favoriters), MA-702's named `RequireModerator` edge policy applied to every mod-only group, plus the remaining Tier-2 (MA-203/204/302/402/403/706/003/005/110), the MA-008 validation-exception unification (15 types + shared client translator), the Tier-3 mechanical batch (dead code, aria-labels, RequireUserId extension, projection idioms, comment debris, ConfirmDialogTests), and all Bucket-B/doc-staleness items (BB-01/02/03, MA-104/108/114/118/122/123-note, content-safety TPT correction, TODO retargets, ImportParse concurrency limiter + group-create throttle). No cell Stage changes. Full suite green (712 / 646 / 734, 0 failures; +22 regression tests). Browser E2E confirmed all 7 holes + MA-702 (both directions) + MA-302 + MA-402 over the wire. Deliberately NOT done (⛔/🧑): Desktop/Mobile merges, Identity-scaffold prune, extract-or-not seams. Detail: `workplan.md` WU-AuditFixPass-2; `modernization-audit/fix-status.md`.
- **Integration tier sped up ~9× (WU-IntTestPerf, 2026-07-18).** One collection-shared host (was one per `[Fact]`) + `DevSeed=None` + 1-iteration PBKDF2: ~12m30s → ~1m25s, 727/727 green, same Postgres/Respawn rigor. No cell Stage changed. Detail: `testing.md` §"Integration test host is shared collection-wide", `workplan.md` WU-IntTestPerf.
- **Feature 56 (Feature Contributions) CUT (2026-07-18).** Decision row 3's final verdict; machinery removed (`FeatureContribution` entity/FKs, `UserStat.FeatureContributions` counter), sole `InitialSchema` migration regenerated. Its grid row is gone (number kept, not renumbered). The Architect badge is retained as a manual grant. Detail: `audit/BlogPosts.md` Feature 56 CUT note; `middle_plan_v2.md` §Resolved.
- **Groups L5 grid-mark reconciliation (WU-GroupsL5, 2026-07-24).** Rows 38–40 L5 corrected 2 → 5 (WU-GlobalFlip's flip claim had skipped this cluster; the sibling Recommendations correction was rows 27–29). A second Groups gap (folder assignment had no caller and `GroupPage` never rendered folder contents — tracker B6) closed by WU-GroupsL5b (2026-07-25). Narrative: `audit/Groups.md` Stage notes. Detail: `workplan.md` WU-GroupsL5, WU-GroupsL5b.

| # | Feature | Folder | L1 | L2 | L3-Logic | L3.5-Struct | L4-Style | L4.5-Browser | L5 | L6 | L8 |
|---|---------|--------|----|----|----------|-------------|----------|--------------|----|----|----|
| 1 | Identity & Auth | Identity | 5 | 5 | 5 | 5 | 1 | 5 | N/A | N/A | N/A |
| 2 | Lookup Tables & Seed Data | Lookups | 5 | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 3 | Sprite & Theme System | Sprites | 5 | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 4 | Story Creation & Editing | Stories | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 5 | Story Browsing & Display | Stories | 5 | 5 | 5 | 5 | 1 | 5 | 5 | 5 | N/A |
| 6 | Chapter Writing & Versioning | Chapters | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 2 | N/A |
| 7 | Chapter Reading | Chapters | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 2 | N/A |
| 8 | Story Arcs | Stories | 5 | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 9 | Series & Ordering | Stories | 5 | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 10 | Story Lineage | Stories | 5 | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 11 | Tag Administration | Tags | 5 | 5 | 5 | 5 | 1 | 5 | 5 | 5 | N/A |
| 12 | Story Tagging | Tags | 5 | 5 | 5 | 5 | 1 | 5 | 5 | 5 | N/A |
| 13 | Tag Display & Sprites | Tags | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 14 | Tag Filtering & Selection UI | Tags | N/A | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 15 | Saved Tag Selections | Tags | 5 | 5 | 5 | 5 | 1 | 1 | 5 | 5 | N/A |
| 16 | Story Interaction State Writes | UserStoryInteractions | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 17 | Story Interaction Lists & Bookshelves | UserStoryInteractions | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 18 | User Following | Following | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 19 | Vouches | Following | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 20 | User Profile Editing | Profiles | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 21 | User Profile Display | Profiles | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 22 | User Stats | Profiles | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 23 | Comment Posting | Comments | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 24 | Comment Display & Pagination | Comments | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 25 | Comment Likes | Comments | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 26 | Spoiler Comments | Comments | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 27 | Recommendation Submission | Recommendations | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 28 | Recommendation Display | Recommendations | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 29 | Hidden Gem Management | Recommendations | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 30 | Recommendation Attribution | Recommendations | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 31 | Search Page | Discovery | N/A | 5 | 5 | 5 | 1 | 5 | 5 | 5 | N/A |
| 32 | Full-Text Search | Discovery | 5 | 5 | 5 | 5 | 1 | 5 | 5 | 5 | N/A |
| 33 | Manual Tree Search | Discovery | N/A | 5 | 5 | 5 | 1 | 5 | 5 | 2 | N/A |
| 34 | Tag Directory | Discovery | N/A | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 35 | Blog Post Writing | BlogPosts | 5 | 5 | 5 | 5 | 1 | 5 | 5 | 2 | N/A |
| 36 | Blog Post Display | BlogPosts | 5 | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 37 | Polls | BlogPosts | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 38 | Group Management | Groups | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 2 | N/A |
| 39 | Group Content & Folders | Groups | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 40 | Group Display | Groups | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 41 | Notification Generation | Notifications | 5 | 5 | N/A | N/A | N/A | 5 | N/A | 5 | N/A |
| 42 | Notification Display | Notifications | 5 | 5 | 5 | 5 | 1 | 5 | 5 | 5 | N/A |
| 43 | Notification Settings | Notifications | 5 | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 44 | Reading Progress Tracking | Chapters | 5 | 5 | 5 | 5 | N/A | 5 | 5 | N/A | N/A |
| 45 | View Count Tracking | Stories | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 46 | Content Reporting | Moderation | 5 | 5 | 5 | 5 | 3 | 5 | 5 | 5 | N/A |
| 47 | Moderation Queue & Actions | Moderation | 5 | 5 | 5 | 5 | 3 | 5 | 5 | 5 | N/A |
| 48 | Story Approval Workflow | Moderation | 5 | 5 | 5 | 5 | 3 | 5 | 5 | N/A | N/A |
| 49 | Private Messaging | Messaging | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A |
| 50 | Badge System | Badges | 5 | 5 | 5 | 5 | 1 | 5 | 5 | N/A | N/A |
| 51 | Custom Lists | CustomLists | 5 | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 52 | User Account Deletion | Identity | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A | N/A |
| 53 | External Story Links & Verification | Stories | 5 | 5 | 5 | 5 | 1 | 5 | N/A | N/A | N/A |
| 54 | Content Download/Export | Export | N/A | 5 | N/A | N/A | N/A | 5 | N/A | N/A | N/A |
| 55 | Community Spotlight | Spotlight | 5 | 5 | 5 | 5 | 3 | 5 | 5 | N/A | N/A |
| 57 | Notification Cleanup Worker | Notifications | N/A | 5 | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 58 | UserStat Recalculation Worker | Profiles | N/A | 5 | N/A | N/A | N/A | N/A | N/A | N/A | N/A |
| 59 | Automatic Tree Search | Discovery | N/A | 5 | 5 | 5 | 1 | 5 | 5 | N/A | 5 |
| 60 | Tree Search Data Mart Worker | Discovery | N/A | N/A | N/A | N/A | N/A | N/A | N/A | N/A | 5 |
| 61 | Also Favorited / Also Recommended | Discovery | N/A | 5 | 5 | 5 | 1 | 5 | 5 | N/A | 5 |
| 62 | SiteDailyStat Worker | Moderation | 5 | 5 | 5 | 5 | 3 | 5 | N/A | N/A | 5 |
| 63 | Chapter Import (file ingestion) | Import | N/A | 5 | 5 | 5 | 5 | 5 | 5 | N/A | N/A |
| 64 | Site SEO | Seo | N/A | 5 | 5 | 5 | N/A | 5 | N/A | N/A | N/A |
| 65 | Accessibility | — | N/A | N/A | N/A | N/A | 1 | 1 | N/A | N/A | N/A |
| 66 | Viewer Access Gating | ContentGate | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 |
