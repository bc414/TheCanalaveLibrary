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

- **Last landed:** WU-ApplyFiltersPurity (2026-07-30) — `ServerStoryReadService.ApplyFilters`
  reverts to pure/synchronous (the tag-hierarchy expansion map is now an explicit argument, resolved
  via a new cached `ITagHierarchyReadService`/`ServerTagHierarchyCache`, invalidated on any `Tag`
  write plus a 60 s TTL). Closes tracker item **B12** — the per-filtered-read DB round-trip
  WU-TagFanon's roll-up introduced is gone from the steady state; no cell flips (F31 L2, F11/F12 L2
  all stay Stage 5). (Before that, same day: WU-ErrorHandling2 — the `ProblemDetails` API error
  envelope + full client HTTP error translation, completing what WU-ErrorHandling (2026-07-06)
  deferred, closing tracker item **E1** and D5's behavior-change half; found and fixed a live gap
  along the way (`StoryEndpoints`' filter/random-batch/filter-candidates reads still 500ing on
  malformed ship input after WU-TagFanon typed the exception but never wrapped the endpoint).
  Before that, same day: WU-AccountEnforcement — mid-session account-status responsiveness
  (`AccountStatusBanner` live-reads status per navigation instead of relying on the sign-in claim;
  now covers Warned/Suspended/Banned; `NotificationBellInner`'s identical mid-session-staleness bug
  folded in and fixed the same way), closing tracker item **G1**'s residual and Phase 2's last open
  item. Before that, 2026-07-28: WU-DiscoveryFilterRestore + WU-SelectionPermalink — decision row
  13 resolved, closing tracker item **B11**; earlier the same day, WU-Home + WU-SiteNews — decision
  row 2 resolved, closing tracker item F1.)
- **Phase (`roadmap.md`):** **Phase 2 is DONE ✓ (2026-07-30).** Phases 0, 1, 2, and 5 are all DONE.
  **Phase 3 is next** — Brian-driven L4 freeze sweep + WU-A11y (the latter gated on decision
  row 12) — nothing further blocks starting it. WU-ApplyFiltersPurity and WU-ErrorHandling2 were
  both Tier-1/Tier-2 between-phase work (below), not phase gates.
- **Between-phase work:** `hidden-deferrals-tracker.md` closures land as ad-hoc WUs — open items
  exist in **every group A–H** (~19 unchecked boxes, one fewer now that B12 is closed), including
  two **high-priority security items: E2 and E3**. WU-ErrorHandling2 also left a named follow-up:
  the 8 SOLO editor pages' error surfaces still want `ErrorAlert` adoption (see its DONE entry).
- **Blocked on Brian:** decision rows 4, 6, 8, 10, and 12 (`roadmap.md` §"Decisions
  that need you"; rows 2 and 13 resolved 2026-07-28).

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

- **WU-A11y** — **Cells:** Feature 65 (new), L4/L4.5 currently Stage 1. **Phase:** 3, paired with
  the L4 freeze sweep. **Scope:** blocked on decision row 12 (scope/depth). **Pointer:**
  `audit/Accessibility.md`. **Deps:** Phase 3's L4 freeze sweep (same pass).
- **WU-EditorSprite** — **Cells:** Feature 6 (extends, no new cell). **Phase:** 4. **Scope:**
  inline Pokémon-sprite Quill blot (spec §5.30.2), deferred at WU6. **Pointer:**
  `audit/Chapters.md` Feature 6. **Deps:** WU6 (`EditorView`, Stage 5).
- **WU-EditorMobile** — **Cells:** Feature 6 (extends, no new cell). **Phase:** 4. **Scope:**
  mobile `EditorView` toolbar / desktop-mobile device composition, deferred at WU6. **Pointer:**
  `audit/Chapters.md` Feature 6. **Deps:** WU6.
- **WU-NotifEmail** — **Cells:** Features 41–43 (extends, no new cell). **Phase:** 6, Beta gate.
  **Scope:** notification email fan-out over `UserNotificationSetting.EmailEnabled`, deferred at
  WU-Email; also folds in the untested anonymous-`NotificationBell` RazorComponents gap noted in
  `audit/Notifications.md` Feature 42. **Pointer:** `audit/Notifications.md`. **Deps:** WU-Email
  (DONE ✓ 2026-07-06).
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

## WU-ApplyFiltersPurity — `ApplyFilters` reverts to pure/sync; cached tag-hierarchy service (Feature 31, extends `Stories/`, `Tags/`) — DONE ✓ (2026-07-30)

- **Cells:** none flip — F31 L2, F11/F12 L2, F59/F60 L8 all stay Stage 5; additive to already-sound
  cells, same shape as WU-TagFanon's own note. Closes tracker item **B12**.
- **Scope:** WU-TagFanon (2026-07-26) had made `ServerStoryReadService.ApplyFilters` async and
  dependent on a live `ReadOnlyApplicationDbContext` for tag-hierarchy roll-up — impure, and
  unreproducible from its `StoryFilterDto` alone (B12 complaint 1); the expansion rule was also
  unshared with any future consumer (complaint 2), and the 0.02 ms measurement that justified the
  per-read round-trip captured localhost DB execution, not a production network hop (complaint 3).
- **Decisions settled before building (per B12's open questions):** cache the map (not keep the
  per-request lookup) — process-local snapshot, invalidated on any `Tag` write, plus a 60 s absolute
  TTL; broad invalidation trigger (any `Tag` write, not just `ParentTagId` changes); expansion gets
  its own `ITagHierarchyReadService` interface rather than joining `ITagReadService` (5 existing
  implementers, server-only concern); the "re-measure on a network-separated database" precondition
  is **obviated, not deferred** — that measurement existed to justify keeping the round-trip, and
  this WU removes it instead, so the round-trip's cost being ≥0 and network-topology-dependent means
  eliminating it is non-worse under any possible measurement outcome.
- **New Core types:** `TagExpansionMap` (`Core/Tags/`) — immutable `{self} ∪ children` snapshot,
  `Expand(id)` returns `[id]` on a miss (never throws — the highest-likelihood refactor bug, since
  the retired per-request dictionary was keyed on the caller's own ids and could not miss).
  `ITagHierarchyReadService.GetExpansionMapAsync()`.
- **New Server type:** `ServerTagHierarchyCache` (`Server/Tags/`) — singleton; `volatile` snapshot +
  `SemaphoreSlim(1,1)` double-checked reload; opens a fresh `IServiceScopeFactory` scope per load
  (the read-context factory is scoped, same discipline as `ViewCountFlusher`); no try/catch (a
  failed load leaves the snapshot null, exception propagates — `logging.md` "No Silent Catches").
  Registered singleton concrete + forwarded interface, same shape as `IFanonReadService`/Write.
- **Invalidation:** `ServerTagWriteService.CreateTagAsync`/`UpdateTagAsync`/`DeleteTagAsync` each
  call `Invalidate()` immediately after their `SaveChangesAsync()` (after commit, never before — a
  pre-commit call would let a concurrent reader re-cache the stale rows).
- **`ApplyFilters` refactor:** signature is now `ApplyFilters(query, filter, TagExpansionMap
  expansion, int? viewerId, bool hasFts)` — `static`, synchronous, no `DbContext`, no ambient
  `ActiveUser` read. New `ResolveExpansionAsync(filter)` runs `ValidateShipShape` first (malformed
  ship input still 400s before any cache/DB work, unchanged), then resolves the map only if the
  filter names any tag id (`TagExpansionMap.Empty` otherwise — unfiltered browse still touches the
  hierarchy not at all, the property B12 itself credited). `ExpandWithChildrenAsync` deleted; its
  query moved into the cache's loader. All three call sites (`GetListingsAsync`,
  `FilterCandidateIdsAsync`, `GetRandomBatchAsync`) updated. No interface, DTO, endpoint, or
  component change anywhere.
- **Integration-harness fix (the largest implementation risk):** the suite shares one
  `TestAppFactory` for its whole run, and most tests seed `Tag` rows directly via
  `ApplicationDbContext` rather than through `ITagWriteService` — write-invalidation never fires for
  them. `ServerTagHierarchyCache.Invalidate()` added to `IntegrationTestBase.ResetSharedHostState`
  (its own doc claims to enumerate every stateful singleton in the host); a new
  `InvalidateTagHierarchy()` helper covers the rarer mid-test case. Confirmed by inspection that no
  existing test seeds a `Tag` row *after* a filtered story read within the same method.
- **Verified:** `dotnet build` clean; `dotnet test` green, run twice (order-dependence risk) —
  2,374 total (776 Unit + 625 RazorComponents + 973 Integration; new:
  `TagExpansionMapTests` — grouping, self-first ordering, miss-returns-self; `TagHierarchyCacheTests`
  — cold load, cross-scope `ReferenceEquals` reuse, write-invalidation through the real
  `ITagWriteService` for create/re-parent/delete). `DiscoveryRollUpAndShipTests`,
  `StoryListingsTests`, `RandomBatchTests`, `TreeSearchComposeTests`, `ApiErrorEnvelopeTests` all
  green unmodified.
- **Tool:** opusplan. **Pointers:** `layer2-services.md` §"Reference-Data Caching" and §"Tag
  Hierarchy Roll-Up"; `horizontal-scaling.md` §5 (process-local caches need no shared store at
  N≥2); `testing.md` §"Integration test host is shared collection-wide"; `audit/Discovery.md`
  §"WU-ApplyFiltersPurity note"; `audit/Tags.md` §"WU-ApplyFiltersPurity Stage note". **Deps:**
  WU-TagFanon (DONE ✓ 2026-07-26).

---

## WU-ErrorHandling2 — `ProblemDetails` envelope + client HTTP error translation (cross-cutting, extends `Errors/`) — DONE ✓ (2026-07-30)

- **Cells:** none — cross-cutting; the L5 column stays Stage 5 everywhere, unchanged. Closes
  tracker item **E1** and D5's "shipped behavior change" half.
- **Scope:** completes what WU-ErrorHandling (2026-07-06) deferred — the API error-envelope +
  full client-service HTTP error translation, unblocked by WU-GlobalFlip (2026-07-13).
- **Server:** `AddProblemDetails()` + a `/api`-scoped `ApiExceptionHandler` (`Server/Http/`) —
  an unhandled `/api/*` exception now answers JSON (with a `traceId` extension), not the HTML
  `/Error` page. `EndpointHelpers.ExecuteWriteAsync` renamed to `ExecuteAsync` (mechanical, 204
  call sites) — the mapping was never write-specific. Applied to every read endpoint whose
  service can throw a typed exception (`TreeSearchEndpoints`, `MessagingEndpoints`'
  `GetConversationsAsync`/`GetConversationThreadAsync`), closing 500s that were previously
  unhandled. **Found live during the audit, not previously known:** `StoryEndpoints`' `/query`,
  `/random-batch`, `/filter-candidates` were still unwrapped after WU-TagFanon upgraded
  `ApplyFiltersAsync`'s ship-shape validation to a proper `StoryValidationException` — malformed
  ship input was still 500ing. Fixed in the same pass. Full endpoint audit swept all 42
  `*Endpoints.cs`; the only bare `Results.NotFound()` found (`FanonEndpoints`' adoption-page
  read) is now a bodied `Results.Problem`. Exemption list (binary/dev-only surfaces) recorded in
  `layer5-wasm.md`.
- **New Core types:** `SessionExpiredException` (401 — a session signal, distinct from
  `UnauthorizedAccessException`'s 403 authorization denial) and `ServerFaultException(traceId)`
  (unhandled 5xx, carries the server's own trace id so the id a user reports is the id of the
  request that actually failed, correct under both InteractiveServer and the WASM hop —
  `Activity.Current` is null in WASM). `ExceptionPresenter` extended for both.
- **Client:** `ClientHttpHelpers` gained `ThrowIfReadFailedAsync` (the read-side twin of
  `ThrowIfWriteFailedAsync`) and both now reconstruct 401→`SessionExpiredException`/
  5xx→`ServerFaultException`. Ten private per-service write translators collapsed onto the
  shared helper (closing D5's "shipped behavior change" — several previously conflated 401 with
  403 into one exception type, predating the session/permission distinction); Messaging/Groups
  keep their documented 403-disambiguation deviation but delegate every other arm. Ten gated
  client read services gained translation (`ClientStoryReadService`, `ClientBlogPostReadService`,
  `ClientChapterReadService`, `ClientTreeSearchReadService`, `ClientMessagingReadService`, +
  `ClientUserStoryInteractionReadService`'s shared bookshelf/write translator); genuinely-public
  reads (`ClientManualTreeSearchReadService`, `ClientCoOccurrenceReadService`,
  `ClientNotificationReadService`) were audited and confirmed to need no change — their server
  counterparts never throw.
- **UI:** new `SharedUI/Errors/ErrorAlert.razor` — drop-in `InlineAlert` replacement with a
  `ShowSignIn` affordance (inline "Sign in" link, current path as `ReturnUrl`; the user stays on
  the page so `DraftAutosave` keeps unsaved work, no hard redirect). Adopted across all 19
  `ExceptionPresenter`+`InlineAlert` PAIR components identified in planning (`CommentSection`,
  `RecommendationSection`, the three `Mod*Page`s, `SettingsPage`, `StoryArcManagerPanel`, etc.) —
  each catch site that sets the component's error field now also sets a sibling
  `_...IsSessionExpired` bool from `ex is SessionExpiredException`. `ProfilePage.razor` was
  audited and correctly excluded (its one catch site reports via `Toasts.Show`, not an inline
  field). **Not done — explicit follow-up, not a silent gap:** the 8 SOLO editor pages
  (`StoryEditorPage`, `ChapterEditorPage`, `BlogPostEditorPage`, `PollsPage`,
  `SiteAnnouncementEditorPage`, `GroupBlogPostEditorPage`, `GroupPage`, `ReportDialog`) render
  errors a different way each and were out of this WU's scope per the plan's own fallback —
  needs its own pass to establish (and possibly redesign) each one's error surface before
  `ErrorAlert` can drop in. **Minor known gap:** a few `Mod*Page`/`SeriesCreateEditPage`/
  `StoryArcManagerPanel` handlers set the error field via a hardcoded early-return validation
  string (not `ExceptionPresenter`) and don't reset `_...IsSessionExpired`; harmless in practice
  (a stale `true` only shows a spurious Sign-in link if a session-expired error preceded a
  same-handler validation error) but not swept in this pass.
- **Related, pre-existing, out of scope:** `StoryValidationException.Message` is always the
  fixed "Story validation failed." text — `EndpointHelpers.ExecuteAsync`'s 400 arm reads
  `ex.Message`, not `ex.Errors`, so the specific ship-filter constraint text never crosses the
  wire as `Detail` (present for every `StoryValidationException` site, including the pre-existing
  write path, not something this WU introduced or was scoped to fix).
- **Verified:** `dotnet build` clean; `dotnet test` green — 2362 total (769 Unit + 625
  RazorComponents + 968 Integration; new: `ApiExceptionHandlerTests`, `ExceptionPresenterTests`
  additions, `ErrorAlertTests`, `ApiErrorEnvelopeTests`). Four pre-existing Unit tests pinning the
  old 401 mapping (`ClientGroupServiceTests`, `ClientExternalVerificationServiceTests`,
  `ClientTagServiceTests`, `ClientCustomListServiceTests`) updated to assert
  `SessionExpiredException`. Both hygiene gates clean.
- **Tool:** opusplan. **Pointers:** `error-handling.md` §"The API error envelope";
  `layer5-wasm.md` §"The Error-Translation Contract"; `identity-and-authorization.md`'s 401
  pointer. **Deps:** WU-GlobalFlip (DONE ✓ 2026-07-13).

---

## WU-AccountEnforcement — mid-session account-status responsiveness (Feature 1, extends) — DONE ✓ (2026-07-30)

- **Cells:** F1 L2/L3-Logic stay Stage 5, re-verified — additive, no Stage change. Closes the last
  open Phase 2 item and tracker item G1's genuine residual.
- **Scope:** the only slice WU38a (2026-07-11) left open — a freshly-Warned/Suspended/Banned user
  saw nothing until their next sign-in, since `canalave:account_status` is a claim baked once at
  sign-in.
- **`RefreshSignInAsync`, the tool `roadmap.md`/the tracker named, turned out not to apply and was
  dropped before any code was written (Doc-Touch moment 1):** every existing call site
  (`ContentGateEndpoints.cs`, the stock Identity `Manage/*` pages) reissues the *caller's own*
  cookie — a moderator's Warn/Suspend/Ban runs in a different DI scope and a different circuit
  than the target, so nothing can reach the target's session to reissue its cookie.
- **Shipped instead — a live read, not a claim/cookie refresh.** `AccountStatus` turned out to
  have exactly one consumer (`AccountStatusBanner`) and is never used for query-shaping or
  authorization, so there was nothing to reissue in the first place — new
  `IAccountStatusReadService`/`ServerAccountStatusReadService`/`ClientAccountStatusReadService` +
  `GET /api/account-status` (modeled on the existing `IUserActivityWriteService` quartet).
  `AccountStatusBanner` keeps the baked claim as its first-paint value only and re-reads live on
  `NavigationManager.LocationChanged` — the `MessagesNavLink` unread-badge pattern, not a new one.
- **Widened while building it (settled with the user):** the banner now renders all three
  non-Active states, not just Warned — Suspended/Banned are reachable *only* via the live read (a
  claim can never carry them, `CanalaveSignInManager` blocks that user at sign-in) — and the
  30-minute stamp-bump ejection window stays unshortened by deliberate choice; the banner is the
  disclosure that window now requires. Suspended/Banned copy reuses `Login.razor`'s wording
  verbatim and adds a sign-out affordance; Warned is unchanged.
- **Folded in, same bug class:** `NotificationBellInner` claimed to refresh "on mount / navigation"
  but never subscribed to `LocationChanged` — fixed identically, found while building the above,
  not independently reported.
- **Real bug found and fixed live during browser verification (Feature 47, out of this WU's own
  scope but fixed same-session per `debugging.md`):** driving a real Suspend action through
  `ModUsersPage.razor`'s form for the first time ever (every prior Suspend verification, including
  WU38a's, set the date via `psql`/fixture, never through this UI) crashed with
  `ArgumentException: Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp
  with time zone'` — the `datetime-local` input's `@bind` produces `Kind=Unspecified`, never tagged
  before reaching `ApplyAccountActionAsync`. Fixed with `DateTime.SpecifyKind(..., Utc)` at the
  call site; new `ModUsersPageTests.SuspendUser_SubmitsUtcKindDateTime` (RazorComponents,
  mutation-sanity confirmed) pins it. Detail: `audit/Moderation.md` Feature 47.
- **Verified:** `dotnet build` clean; `dotnet test` green — 2344 total (763 Unit + 620
  RazorComponents + 961 Integration). Both hygiene gates clean. **Real Chrome browser pass**
  (server-only path, two tabs, `psql` ground truth): a moderator Warn/Suspend through the real
  `/mod/users` UI was visible to the target within one in-app navigation with no reload — banner
  text, bell count, and (for Suspend) the exact date and sign-out affordance all correct; the
  claim-only first-paint value self-corrected to the live value on the next navigation, exactly as
  designed; anonymous → 401 on the endpoint; `Log out` verified working end-to-end. One early
  two-tab run hit an `AntiforgeryValidationException` — traced to the test methodology, not the
  app: logging in as a second user in one browser tab silently overwrites the shared session cookie
  for every other tab of the same profile (cookies are per-origin, not per-tab); a clean single-session
  repro (moderator action completed first, target logs in after and is never touched again)
  reproduced cleanly with no error. Not a defect, but worth remembering for any future two-identity
  browser verification in this app.
- **Doc corrections in the same WU (Doc-Touch moment 1):** `security.md`'s claim literal
  (`canalave:accountstatus` → the actual `canalave:account_status`); `roadmap.md`'s Phase 2 →
  DONE and the "Group G is fully closed" line, which contradicted G1's own still-open residual;
  `hidden-deferrals-tracker.md` G1 residual ticked; stale in-code comments on `User.AccountStatus`,
  `ActiveUserClaimTypes.AccountStatus`, and `ApplicationUserClaimsPrincipalFactory`'s `<remarks>`.
  New rule minted: `identity-and-authorization.md` §"Account Status Is Display-Only, Read Live" —
  the general "claim-shapes-a-query → needs cookie reissue; claim-is-display-only → prefer a live
  read" distinction, for the next baked claim that goes stale.
- **Tool:** opusplan. **Pointers:** `audit/Identity.md` WU-AccountEnforcement Stage note;
  `audit/Moderation.md` Feature 47 WU-AccountEnforcement Stage note; `audit/Notifications.md`
  Feature 42 mid-session-refresh note; `canalave-conventions/security.md` "Account-Status
  Enforcement"; `identity-and-authorization.md` §"Account Status Is Display-Only, Read Live".
  **Deps:** WU38a (DONE ✓).

---

## WU-DiscoveryFilterRestore + WU-SelectionPermalink — decision row 13 resolved and built (Features 31 + 15) — DONE ✓ (2026-07-28)

- **Cells:** F31 L2/L3-Logic/L3.5 and F15 L2/L3-Logic/L3.5/L5 — all already Stage 5, extended
  additively; **no grid numbers changed**. F15 L1 unchanged (the permalink needs no column).
  **Closes tracker B11.**
- **Doc-Touch moment 1 (first, before code):** resolved **decision row 13** — `/discover` never
  carries filter state in its URL. The row's own framing was superseded: it argued "follow
  `TreeSearchPage`'s pattern", but that page carries *control* state (`?degrees=2&sort=…`), which is
  not precedent for serialising arbitrary id lists — nothing in this codebase has ever done that.
  Moved to `roadmap.md` §Resolved with the three-call resolution; rewrote the Tier-2 row (the
  planned "WU-DiscoveryURLState" was a misnomer once the decision went *against* URL state) and
  **re-homed B12**, whose only stated blocker was row 13, as its own WU-ApplyFiltersPurity.
- **Shipped — sharing (WU-SelectionPermalink):** `/discover/selection/{SelectionId:int}/{*Slug}` on
  `SearchPage`, story-slug contract (id is truth, slug decorative and never parsed → no slug column,
  no migration, renames don't break links). New anonymous-callable
  `ISavedTagSelectionReadService.GetPublicSelectionByIdAsync` + server/client impls + endpoint,
  enforcing `IsPublic` **and** the owner's `ProfileVisibility` (Class A) with every failure mode
  collapsing to one indistinguishable null. `SelectionPermalinkBanner` (injection-free) +
  `SelectionAdoptButton` behind `<AuthorizeView>` per the WU43 DI-split rule; profile Tag Selections
  cards link out. Sort/text/interaction exclusions stay the *viewer's* §8.7 defaults, so the
  artifact is still a tag combination, not a saved query.
- **Shipped — return integrity (WU-DiscoveryFilterRestore):** `DiscoveryFilterStore` +
  `js/discovery-filter.js` (third instance of the ratified thin-JS seam) persisting
  `DiscoveryFilterSnapshot` — **ids only**, per-viewer key, chips/ship labels rehydrated via
  `GetTagChipsByIdsAsync` and unresolvable ids pruned. `[PersistentState]` deliberately not used
  (`error-handling.md`: prerender-handoff only — B11's own sketch was wrong here).
- **Shipped — ship seeding parity (the B11 gap):** `InitialIncludedShipNames`/`InitialExcludedShipNames`
  on `ResultsFilterPanel`, seed params on `ShipFilter` under the MA-402 re-seed guard, labels
  resolved by the dispatcher, and `ShipFilterDto.JoinMemberNames` as the single label
  implementation so pick-time and seed-time can't diverge.
- **Runtime bug found in the browser pass and fixed same-session:** `TagFilter` seeded only in
  `OnInitialized`, so a late-arriving restored seed left the sidebar visibly empty while the query
  behind it *was* filtered. Fixed with an `OnParametersSet` re-seed + the existing WU43
  `_selectionGeneration` `@key` remount, guarded by a seed signature; two regression tests added.
- **Doc drift corrected:** `ShipFilter` claimed it "owns its injection, like TagFilter" —
  `TagFilter` injects nothing. `layer3.5-structure.md` gained §"Seed state vs. live fetch in filter
  components"; `layer2-services.md` gained the permalink-≠-saved-query and artifact-vs-device-local
  rules; `audit/Tags.md`'s "sole discovery path for a shared selection" and `audit/Discovery.md`'s
  "NOT settled, and never discussed" both amended.
- **Verified:** `dotnet build` green; `dotnet test` green — 761 Unit + 612 RazorComponents + 957
  Integration = **2,330**. Both gates clean (`check-design-tokens.ps1`, `check-doc-hygiene.ps1`).
  Browser pass: filter+ship restored across a navigation, payload confirmed ids-only, permalink
  followed from the profile tab, stale slug resolved, missing id → neutral notice, anonymous view
  with log-in affordance, and owner's profile flipped to `Private` in Postgres → same URL, identical
  notice, no leak (DB state restored afterward).
- **Tool:** opusplan. **Pointers:** `audit/Discovery.md` §"WU-DiscoveryFilterRestore +
  WU-SelectionPermalink note"; `audit/Tags.md` §"WU-SelectionPermalink Stage note".

---

## WU-AccessGate + WU-AccessGate2 — viewer access gating end-to-end (Features 64 + 66) — DONE ✓ (2026-07-23/24)

*(Moved out of the "Planned / not-yet-built" section 2026-07-27 — the two entries below were
written there as planned items and updated in place to DONE without being re-filed.)*

- **WU-AccessGate — DONE ✓ (2026-07-23)** *(re-minted 2026-07-19 from WU-SeoSite, which it
  absorbed — decision row 11 resolved "index all; gate access")* — **Cells:** Features 64 + 66,
  all applicable layers → **Stage 5**. **Phase:** 2, item 8. **Shipped:** the three-plane access
  model end-to-end — Class-A fixes (ProfileVisibility enforced on all seven profile-scoped read
  paths + honest profile states; styled sign-in-required experience replacing the blank-401 class
  via explicit auth-middleware placement + `/status-code/{0}` re-execute; soft-404s → real 404s;
  author self-access on own-M read/edit; group-blog permalinks; five dead `<NotAuthorized>`
  blocks deleted; `/welcome` flow), consent infrastructure (`user_content_reveals` polymorphic
  table, `canalave.prefs` anon cookie, consent endpoints with `RefreshSignInAsync` — MA-605
  closed, ceiling derivation centralized as `MaxRating`), the gates (gated-existence reads →
  `ContentGateInterstitial` on story/chapter/group/blog-post pages, adult labels rating=adult +
  RTA on both branches, `/gate` endpoints for the WASM pass, reveal-aware
  chapter/TOC/versions/export subtree, tree-search root reveals), Personal plane + disclosure
  (`personalScope` bookshelf/owner-list hydration, `MatureDisclosureLine` gated mini-cards on
  profile tabs/public lists/group sections/series, spotlight dedicated M/non-M slot pools with
  redemption validation, `/settings` reveal revoke section), and the Feature-64 slice
  (robots.txt with AI-trainer blocks, sitemap.xml incl. M, canonical-slug 301 middleware +
  `<link rel="canonical">`, `VerifiedBotMiddleware` config-gated OFF until Phase 7's trust
  boundary). **Verified:** `dotnet test` green — 1955 total (14 new Integration
  `ContentGateTests`); curl matrix + Chrome browser band (anonymous consent loop, logged-in
  always-show with immediate claim refresh, DB reveal + revoke, disclosure line) — full
  narrative in `audit/AccessGate.md` Stage-5 note. **Pointers:** `audit/AccessGate.md`,
  `.claude/design/access-gating-first-principles.md` (model), `audit/Seo.md` (Feature 64 slice).

- **WU-AccessGate2 — DONE ✓ (2026-07-24)** — the post-completion-review follow-ups. **Shipped:**
  the `"StoryStatus"` named filter (Class-A: Draft/PendingApproval/Rejected confidential to all
  but their own author — closes the pre-existing gap where such stories were served by direct
  link and listed in search/browse; author-aware clause, mod work surfaces bypass by name);
  `GetChapterGateAsync` (consent path for an M alternate version of a non-M story — fixes the
  WU-AccessGate silent-404 regression; one story reveal unlocks its versions); the
  `IActiveUserContext` consent members made abstract with explicit implementations in all five
  implementors (default-interface-member shortcut removed); sitemap expanded to Public profiles
  + groups + published blog posts per the "original content homes" paradigm; interstitial
  minimal OG + group-post `SubjectNoun` copy; Phase-7 checklist lines. **Verified:** `dotnet
  test` green — 1961 total (6 new Integration `StoryVisibilityTests`); seeded curl matrix +
  browser band (author sees own draft, mod queue lists pending, sitemap in/out counts) — full
  narrative in `audit/AccessGate.md` "WU-AccessGate2" Stage note.

## WU-ChapterArcBrowserPass — Real-circuit L4.5 pass: Story Arcs + chapter reorder/delete/reading state (Features 6, 7, 8) — DONE ✓ (2026-07-24)

- **Scope:** closes the WU45 L4.5-Browser deferral (Brian's direction, 2026-07-12) for Features 6,
  7, 8 only — the not-covered checklist from that Stage note. Rows 6/7's L6 (chapter read-query
  indexes) and row 8's L4-Style (human visual sign-off) are explicitly out of scope and unchanged.
- **Vehicle:** the seeded flagship story ("Seed Story: Five Chapters + Alt Version (T)", story 1,
  `DataSeeder.cs`) — no seeder change needed. `AuthorAlpha` drove author surfaces, `TestUser` drove
  reader surfaces.
- **Verified live in Chrome, `psql`-confirmed after every mutation:** arc creation + live preview
  interactivity + overlap validation (`StoryArcManagerPanel`); arc headers/collapse-expand +
  reading-page `Arc X — [name]` label (`ChapterList`/`ChapterReadingPage`); chapter drag-drop
  reorder (swapped ch 4/5, restored); chapter delete via `ConfirmDialog` (created + deleted a
  throwaway Chapter 6 rather than a seeded one, to avoid cascading away seeded comments); mark-read
  → live fill-bar/count repaint with no reload; the "New" badge strict-chain rule (temporarily
  future-dated a chapter's `PublishDate` via `psql` to trigger it, confirmed the chain-break
  behavior on the next chapter, then restored the date); per-chapter download endpoint's
  `Content-Disposition: attachment; filename=...` header.
- **No runtime bugs found; no code changes.** `dotnet build` clean (pre-existing `AngleSharp`
  NU1902 advisories only). Full `dotnet test` not re-run — no code changed that could regress it.
- **Fixture note:** the two demonstration arcs created during this pass were left on the flagship
  story rather than wiped — it had zero arcs before, so nothing exercised the Story Arcs UI for
  future manual verification. Reasoning: `audit/Stories.md` Feature 8 Stage note.
- **Cells:** `status.md` F6/F7/F8 `L4.5-Browser`: `2 → 5`.
- **Tool:** Claude Code (Opus). **Pointer:** `audit/Chapters.md` WU-ChapterArcBrowserPass Stage
  note; `audit/Stories.md` Feature 8 WU-ChapterArcBrowserPass Stage note.

## WU-GroupsL5 — Groups L5 grid-mark reconciliation + folder-management page (Features 38/39/40) — DONE ✓ (2026-07-24)

- **Trigger:** the user couldn't recall why Groups L5 (rows 38–40) was still Stage 2 while
  nearly every other feature's L5 had flipped to 5. Investigation found the premise false: the
  endpoints/client impl were already built, registered, and browser-verified in WU-GlobalFlip
  (2026-07-13) — `audit/Groups.md` already carried Stage-5 L5 notes for all three features.
  WU-GlobalFlip's "L5 flipped to 5 for all 40 built-surface rows" claim simply missed the Groups
  cluster when it updated the sibling Recommendations rows (27–30, corrected in the same
  2026-07-12 pass) — a stale grid mark, not a deferred decision.
- **Scope (settled with the user, given the finding):** (1) reconcile the stale `status.md`
  marks; (2) a DI cleanup at `Server/Program.cs` (`IGroupReadService` was mapped to the write
  impl); (3) build the one truly-missing consumer — the deferred group **folder-management page**
  (`/group/{GroupId:int}/folders`), since `IGroupWriteService`'s four folder-write methods had NO
  UI at all, and the page requires "browser-verify folder writes" to be answerable; (4) add the
  deferred L5 test tiers (`GroupEndpointsTests`, `ClientGroupServiceTests`); (5) browser-verify
  end-to-end. L6 row-38's two missing composite indexes are a separate, real, explicitly
  out-of-scope gap (`design/L6-reconciliation-matrix.md`).
- **Built:** `TheCanalaveLibrary.SharedUI/Groups/GroupFolderManagementPage.razor` — admin-gated
  (mirrors `GroupCreateEditPage`'s pattern: `[Authorize]` + UX admin pre-check +
  `[PersistentState]` + `InlineAlert` + exception-to-message mapping), own recursive interactive
  tree (deliberately not sharing `GroupPage.RenderFolders`, a read-only display fragment with
  public M-badge suppression that doesn't apply here), create with optional nesting (a
  depth-indented parent `<select>`), inline rename, two-step `ConfirmDialog`-gated delete, and
  sibling reorder via a `ReorderFolderAsync` SortOrder value-swap (robust to non-contiguous
  SortOrder — no unique constraint on the column). Every write reloads the whole tree from
  `GetByIdAsync` — no local mutation. One-line addition: `GroupConstants.MaxFolderNameLength`.
  Story→folder assignment (`AssignStoryToFolderAsync`/`UnassignStoryFromFolderAsync`) still has
  no UI — deliberately out of scope, flagged as a follow-up.
- **Fixed:** `Server/Program.cs` — `IGroupReadService` now maps to `ServerGroupReadService`
  (was `ServerGroupWriteService`, the heavier write impl with sanitizer/notifications/rate-limit
  deps), matching every other feature's read/write DI split. `Series`' registration block has the
  same quirk — left as-is, out of scope for this WU.
- **New tests:** `GroupFolderManagementPageTests` (RazorComponents, 11) — admin gate, create
  dispatch incl. nested `ParentFolderId`, rename dispatch, the two-step delete guard (trash click
  must not call the service), reorder value-swap + boundary-disabled buttons, validation-error
  surfacing. `GroupEndpointsTests` (Integration, 10) — `PagedResult<T>` envelope on both paged
  reads, the `RequireAuthorization()` 401 floor, the admin-only 403 gate, full folder CRUD over
  HTTP incl. 404 on an unknown folder. `ClientGroupServiceTests` (Unit, 16) — request URL/verb
  shapes incl. every folder route, `PagedResult<T>` deconstruction, and the status-code →
  contract-exception mapping, pinning Groups' one non-standard case (403 disambiguated via
  `ProblemDetails.Detail` presence into `UnauthorizedAccessException` vs.
  `ContentRatingExceededException`).
- **Verified:** `dotnet build` clean. `dotnet test` full suite green: 718 Unit + 521
  RazorComponents + 759 Integration = 1998/1998 (Integration ran for real against
  Testcontainers-Postgres). `scripts/check-design-tokens.ps1` clean for the new page (two
  pre-existing unrelated findings elsewhere — `ImportReviewPanel.razor` UGC-outside-ContentSurface,
  `ProfilePage.razor` undeclared `--color-link` — untouched by this WU). Browser-verified live
  against the dev DB as `TestUser` (admin of a throwaway test group): confirmed genuine WASM
  execution on a fresh load (`_framework/*.wasm` bundle, zero `_blazor` WebSocket — the same
  signature WU-GlobalFlip's own verification used), then drove create (root + nested folder),
  rename, both-direction reorder, and confirm-gated delete, `psql`-confirming `group_folders`
  ground truth after each (parent id, swapped `sort_order`, row removal); confirmed `GroupPage`'s
  read-only tree reflects the live state afterward. Verification group/folders deleted afterward
  (no fixture value) — unlike WU-ChapterArcBrowserPass's deliberately-kept arcs above, this data
  had no standing purpose.
- **Cells:** `status.md` F38/F39/F40 `L5`: `2 → 5` (grid-mark correction, not new capability for
  F38/F40; F39 additionally gained the folder-management page itself). No other cells changed —
  F38's L6 stays 2 (the separate index gap).
- **Tool:** Claude Code (Opus). **Pointer:** `audit/Groups.md` F38/F39/F40 Stage notes;
  `layer5-wasm.md` §"L5 Stage Semantics".

## A3 — Story-completion auto-producer, wired to the spoiler gate (Features 7, 26, 44) — DONE ✓ (2026-07-24)

- **Scope:** closes `hidden-deferrals-tracker.md` item A3 — the F26 spoiler completion-gate was fed
  a hardcoded `UserHasCompletedStory=false` from `ChapterReadingPage` since WU26 ("full completion
  tracking is post-MVP"), making its single-click-reveal branch unreachable in production. Owner
  decided (2026-07-24, mid-session) to build the deferred spec §5.12 producer now rather than leave
  it deferred, given a live plan-mode reassessment of "post-MVP" scope.
- **Design:** `IUserStoryInteractionWriteService.MarkCompletedAsync(int storyId)` — a durable direct
  write mirroring the existing `MarkStartedAsync`, deliberately never routed through the
  `ReadingProgressBuffer`/`ReadingProgressFlusher` signal buffer (that buffer's contract is
  loss-tolerant scroll pings only; completion is a durable, aggregate-driving transition). Fires only
  for author-Completed stories, on reaching the final published chapter (not a chapter-count
  comparison), with no auto-clear (holds the V3 reading-status design's rejection of a stored
  `CaughtUp` state + publish-time worker). Two trigger sites: `ChapterReadingPage.OnScrollProgress`
  (mirrors the `MarkStartedAsync` guard shape) and `ServerChapterReadMarkWriteService`'s manual
  mark-read path. Wiring: `ChapterReadingDto` gained `ViewerHasCompletedStory`/`StoryIsComplete`,
  populated by `GetChapterForReadingAsync` via a correlated subquery — no extra round-trip.
- **Bug found and fixed same session:** `CompletionProducerTests` caught a latent `StoriesInProgress`
  counter underflow — `MarkCompletedAsync`'s decrement assumed `MarkStartedAsync` had already
  incremented it, but `MarkStartedAsync` never touched that counter (only the panel did). Fixed by
  giving `MarkStartedAsync` the missing transition-delta.
- **Verified:** `dotnet build` clean. `dotnet test` full suite green: 752 Unit + 544 RazorComponents +
  800 Integration = 2096/2096 (new `CompletionProducerTests.cs`; `ChapterReadServiceTests.cs`
  extended for the projection fields). Browser-verified live against the server-only dev DB: as
  AuthorBeta, posted a spoiler comment on the seeded Completed one-published-chapter story (story 12);
  as TestUser (seeded `IsCompleted=false` for that story), confirmed the "haven't finished" dialog
  still gated the reveal; scrolled the chapter to the bottom, `psql`-confirmed
  `user_story_interactions` flipped to `is_completed=t` with the pre-existing `is_ignored=t` bit
  untouched (zero-coupling), `CompletedDate` stamped, `UserStat` counters moved; reloaded and
  confirmed the same spoiler comment now revealed on a single click, no dialog.
- **Cells:** `status.md` F7 `L3-Logic` and F26 (all built cells) stay Stage 5 — no grid number
  change, the cells were already Stage 5; this closes the gap the grid couldn't show. F44 `L2`
  likewise stays 5.
- **Tool:** Claude Code (Opus). **Pointer:** `audit/Chapters.md` A3 Stage note;
  `audit/UserStoryInteractions.md` A3 settled note; `audit/Comments.md` Feature 26 A3 update;
  `layer2-services.md` §"`IsCompleted` auto-producer"; `.claude/hidden-deferrals-tracker.md` A3.

## WU-GroupsL5b — Story↔folder membership: closes B6 + D3.1 + dead RemoveStoryAsync (Features 39/40) — DONE ✓ (2026-07-25)

- **Trigger:** the hidden-deferrals audit (2026-07-24) flagged **B6** —
  `AssignStoryToFolderAsync`/`UnassignStoryFromFolderAsync` built and tested, but no UI anywhere
  called them (WU-GroupsL5 had pointedly excluded story-assignment from the folder-management
  page it built the day before).
- **First-draft design mistake, caught in review, then corrected:** the initial fix patched the
  missing `GroupStoryId` read path with a brand-new admin-only `GetGroupStoriesAsync` endpoint.
  The user rejected this on two grounds: (1) no decision anywhere ever gated *read* access to
  story→folder membership to admins — only the write actions are settled-admin-only (WU32) — and
  shipping an admin-only fetch would have left a real display gap open for every other viewer
  (`GroupPage.RenderFolders` had never rendered folder *contents*, for anyone, since WU32); (2) a
  parallel endpoint next to `GroupDetailDto.StoryIds`/`GroupFolderDto.StoryIds` — which already
  carried almost what was needed, missing only `GroupStoryId` — would be a workaround, not a fix:
  "don't make shortcuts or tech debt due to existing code... if a refactor is warranted, do it."
- **Actual fix — retype at the source.** `GroupDetailDto.StoryIds`/`GroupFolderDto.StoryIds`
  (`IReadOnlyList<int>`) retyped to `IReadOnlyList<GroupStoryDto>` (new record: `GroupStoryId` +
  `StoryId`) in `Core/Groups/`; `ServerGroupReadService.GetByIdAsync`/`BuildFolderTreeAsync`
  updated to project the richer shape. `GetByIdAsync` — already fetched by `GroupPage` for every
  viewer — now carries everything needed in the one round trip that already happens. No new
  endpoint. Blast radius mapped exhaustively before coding (Explore agent, confirmed by
  `dotnet build`): 6 consumption-site spots in `GroupPage.razor`, 2 test-fixture named-arg
  renames — nothing else in the solution touched `.StoryIds` on either DTO.
- **`GroupPage.razor` built:** `RenderFolders` now shows each folder's story titles (linked) for
  **every viewer**, unconditionally — closing the display gap the first-draft mistake would have
  left open; rewritten from imperative `RenderTreeBuilder` to a Razor-template recursive fragment
  (matching `GroupFolderManagementPage.RenderFolderTree`'s idiom) since it gained real interactive
  children. Per-folder unassign (×), admin-only. Per-story assign/reassign + remove-from-group,
  admin-only, via `StoryDeck`'s existing `CardOverlay` slot (no changes to `StoryDeck` itself —
  same `pointer-events-auto`-through-the-wrapper pattern as `CustomListPage.OwnerRemoveOverlay`).
  The folder `<select>` treats story→folder as single-primary (matching `AddGroupStoryDto`'s
  add-time intent) but doesn't guess when a story is genuinely in more than one folder
  (`GroupStory.GroupFolders` is a real many-to-many) — shows that plainly, points at the
  per-folder × controls instead.
- **Second dead handler found and wired in the same pass:** `HandleStoryRemovedAsync` was fully
  implemented (error handling, reload) but had no UI trigger anywhere — found while building the
  admin story-action surface this WU needed regardless. Two-step confirmed via `ConfirmDialog`.
- **D3.1 folded in** (same method, `AssignStoryToFolderInternalAsync`, this WU had to touch
  anyway): it never checked `folder.GroupId == groupStory.GroupId` — an admin of group A could
  file A's story into group B's folder id via direct API use. Now threads `expectedGroupId`
  through and rejects a mismatch with `KeyNotFoundException` (identical to a genuinely nonexistent
  folder — no disclosure that the id exists elsewhere). **D3.2** (the Recommendations half of the
  original combined D3 item — `RecordAttributionSourceAsync`'s missing ownership check) was split
  off at the user's direction and deliberately deferred to a future Recommendations-refinement
  session; this WU only touched the tracker doc to record the split, no Recommendations code.
- **New tests:** `GroupServiceTests` +5 (assign/unassign happy paths, the D3.1
  cross-group-rejection pin, non-admin rejection, `GetByIdAsync.Stories` carrying correct
  `GroupStoryId`). `GroupEndpointsTests` +2 (cross-group → 404 over HTTP, admin assign → 204).
  New `GroupPageTests.cs` (12 tests, RazorComponents — no file existed for this page before):
  folder contents visible to every role incl. anonymous; non-admin sees zero admin controls;
  assign/reassign/unfile dispatch correct id pairs; per-folder unassign dispatches correctly;
  remove is two-step (trigger alone must not call the service). `ClientGroupServiceTests` +1
  (deserializing a populated `GetByIdAsync` body with the new nested shape — no prior test in
  that file exercised a non-empty response at all).
- **Verified:** `dotnet build` clean (confirms the retype's blast radius was fully caught — a
  missed consumer fails to compile, by design). `dotnet test` full suite green: 753 Unit + 556
  RazorComponents + 807 Integration = 2116/2116. `scripts/check-design-tokens.ps1` clean for the
  touched file (two pre-existing, unrelated findings elsewhere — `ImportReviewPanel.razor`,
  `ProfilePage.razor` — untouched). Browser-verified live against the dev DB: as admin, created a
  folder, added a story, assigned/reassigned/unassigned it via both the per-story overlay and the
  per-folder ×, removed a story from the group via the confirm dialog — `psql`-confirmed
  `group_stories`↔`group_folder_group_story` ground truth after each step. Switched to a
  non-member seed user (`ReaderGamma`) on the same group: folder contents rendered correctly,
  zero admin controls anywhere on the page. Verification data cleaned up afterward.
- **Cells:** `status.md` — no Stage-number change; F39/F40 were already Stage 5 across the board.
  This fills in inert plumbing + a display gap under already-Stage-5 cells, exactly what the
  hidden-deferrals tracker exists to catch.
- **Tool:** Claude Code (Opus). **Pointer:** `audit/Groups.md` F39/F40 Stage notes;
  `.claude/hidden-deferrals-tracker.md` B6, D3.1, D3.2.

## WU-B2 — Comment & blog-follower notifications + blog spoiler interstitial + story-link integrity (Features 23/24/35/36/41) — DONE ✓ (2026-07-25)

- **Trigger:** hidden-deferrals tracker **B2** — five built-but-inert notification seams (four
  `// TODO(post-MVP comment-notifications)` in `ServerCommentWriteService`, one
  `// TODO(post-MVP follower-notifications)` in `ServerBlogPostWriteService`). Scope grew during
  plan review (owner decisions recorded in the audit files): blog spoiler content interstitial,
  card-snippet suppression, StoryId ownership validation, group StoryId removal, PollUpdated
  enrichment fix.
- **Notifications wired:** five new semantic methods on `INotificationWriteService`
  (`NotifyNewStoryCommentAsync` 24 / `NotifyNewBlogCommentAsync` 33 / `NotifyNewProfileCommentAsync`
  31 / `NotifyCommentReplyAsync` 34 / `NotifyNewProfileBlogPostAsync` 13–16), all funneling through
  the existing `CreateCoreAsync`. Comment seams: best-effort post-commit, reply/container-suppress,
  null-skip for SET-NULL''d authors, replies carry the *context* id (`CommentId` is `long`,
  `RelatedEntityId` is `int`); group comments = replies-only (owner decision — no single
  comment-owner). Blog fan-out fires on the `IsPublished` false→true transition in
  `UpdateBlogPostAsync` (drafts silent; republish re-notifies deliberately), recipient sets made
  disjoint by precedence 13>14>15>16; `ReceiveAlerts` gates the author-follow set only.
- **Read side:** new `RelatedEntityKind.BlogPostDirect` (TPT-root `BlogPosts` → `/blog/{id}`,
  `IsTakenDown` filter deliberately active, no rating bypass — none exists on blog posts); types
  13–16, 33, and `PollUpdated` mapped to it (PollUpdated''s group-only lookup had left profile-post
  poll notifications title-less); `NewStoryComment`→Chapter deep-link; `NewCommentOnYourProfile`→User;
  `CommentReply` stays None (non-navigating, known minor gap). Presenter: new `NewCommentOnBlog` arm;
  14/15/16 reworded for a blog-title `{target}` with the story-relationship cue kept.
- **Blog spoiler interstitial (owner pulled into scope):** `HasSpoilers` now gates post *content*,
  not just a badge. `BlogPostPage` blur curtain + "⚠ Reveal spoiler" Control (CommentItem §5.9.1
  pattern, NOT the mature content-gate), completion-gated: immediate reveal when non-story-linked or
  `BlogPostDto.ViewerHasCompletedStory` (new per-viewer projection in `GetByIdAsync` off
  `UserStoryInteraction.IsCompleted`); `ConfirmDialog` otherwise; author auto-reveals; ephemeral
  state (reset in `LoadPostAsync`). `BlogPostCard` suppresses the body-derived `ContentSnippet`
  under `HasSpoilers` ("Content hidden — contains spoilers").
- **Story-link integrity:** write-time ownership gate (`EnsureLinkedStoryOwnedAsync`) on profile
  create + update — closes the fan-out spam vector (forged `StoryId` → spam a story''s audience;
  the editor dropdown was affordance only). `GroupBlogPost.StoryId` **removed** (entity + DTO +
  editor picker + read-service projection; migration `DropGroupBlogPostStoryId` — the column had no
  FK constraint) — group posts are group topics; restores the original TPT design (Gemini #930).
- **New tests:** `CommentAndBlogNotificationTests.cs` (Integration, 22 tests — all four seams incl.
  drop-self / suppress / null-skip pins, fan-out precedence-dedup, draft-silent / no-transition /
  republish behaviors, ownership gate ×2, enrichment URL pins for `/blog/{id}` + chapter deep-link,
  `ViewerHasCompletedStory` ×4). `NotificationPresenterTests` +5 (new 33 arm + reworded 14/15/16).
  New `BlogPostPageTests.cs` (bUnit, 9 — curtain visibility ×4, reveal flow ×5 incl. dialog
  confirm/cancel) + `BlogPostCardTests.cs` (bUnit, 2 — snippet suppression).
- **Verified:** `dotnet build` clean; `dotnet test` full suite green — 758 Unit + 567
  RazorComponents + 832 Integration = **2157/2157**. `scripts/check-design-tokens.ps1`: no new
  findings (the two pre-existing, unrelated findings — `ImportReviewPanel.razor`,
  `ProfilePage.razor` — confirmed present on clean HEAD via stash round-trip). **Browser pass
  (L4.5) done 2026-07-25** vs. the real circuit + dev DB (`psql`-confirmed, verification rows
  cleaned up): publish-transition fan-out with live 13>15 precedence-dedup (each follower-favoriter
  got exactly one type-13 row); bell text + `/blog/{id}` navigation; the full completion-gated
  curtain flow (blur → confirm-dialog when not-completed → reveal; immediate reveal when completed;
  re-hide on reload; author no curtain); chapter-comment bell + `/story/{id}/{ch}` deep-link; group
  editor has no story picker; group post has no "About:" row. Detail: `audit/BlogPosts.md` WU-B2
  L4.5 note.
- **Cells:** `status.md` — no Stage-number changes; F23/F24/F35 L2 were "5-but-inert," now live;
  the interstitial is additive under F35/F36''s existing Stage 5s; the group `story_id` column drop
  is L1-neutral (no feature contract changed). Exactly the hidden-deferral shape the tracker exists
  to catch.
- **Tool:** Claude Code (Opus/Fable). **Pointer:** `audit/Notifications.md` WU-B2 slice;
  `audit/BlogPosts.md` WU-B2 notes; `audit/Groups.md` amendments; `audit/Comments.md` F23 note;
  `layer2-services.md` §"Comment & blog-post semantic methods"; `hidden-deferrals-tracker.md` B2;
  `L6-reconciliation-matrix.md` story-centric USI addendum.

## WU39 — External Link Verification (mod workflow) — DONE ✓ (2026-07-25) *(re-minted 2026-07-11; was "Story Import & Verification")*
- **Cells:** 53 L1/L2/L3-Logic/L3.5-Structure/L4.5-Browser → Stage 5. L4-Style stays Stage 1
  (pending visual/token sign-off, per the WU8/WU13/WU23/WU28/WU37/WU41 precedent).
- **Shipped:** the two-way-link mechanism question is resolved as a **two-tier model** — an
  account tier (`UserExternalIdentity`: one public site-wide code per user, placed on the
  external profile, moderator-confirmed once per user×platform) plus the existing per-link
  `StoryExternalLink.VerificationStatus` tier (an authorship check that only opens up once the
  account tier is Verified for that platform — platform work URLs don't name their author, so
  account-verified alone doesn't prove any specific linked story is theirs). The `/mod/submissions`
  Imports tab now hosts two live queues (pending accounts, pending links), reusing the Stories-tab
  Approve/Reject idiom. Reader display is settled as **no checkmark** — a muted "reviewed ·
  author's account: `<handle>`" sub-line only, inviting comparison rather than asserting
  permanent trust; non-reviewed states (never-requested/pending/rejected) are deliberately
  identical to the reader. The old "route into `PendingApproval`" step stays dropped — links
  don't gate story approval (Feature 48 untouched); verification is per-link, display-only.
  Per-platform verification properties (placement instructions, `SupportsVerification`) live as
  columns on `ExternalPlatform`, not code branches.
- **Tool:** opusplan. **Pointer:** `audit/Moderation.md` Feature 53 (WU39 Stage note). **Deps:** WU34, WU38d.

> **Account-status login enforcement — folded into WU38a (2026-07-11), no longer deferred.** Was:
> "block Suspended (until `SuspendedUntilUtc`) / Banned users at login and surface the Warned banner
> in layout chrome; WU34 ships the `AccountStatus` state + notifications it builds on; enforcement
> is a security-surface slice to append as its own WU when scheduled (candidate: alongside WU38
> account-deletion UI)." See WU38a above for the settled mechanism and
> `canalave-conventions/security.md` "Account-Status Enforcement".

## WU-RecLifecycle — Recommendation lifecycle (A4) + D1 leak fix + author content control (Features 23/27/28/30) — DONE ✓ (2026-07-25)

- **Trigger:** hidden-deferrals tracker **A4** + **D1** (coupled by design: the missing status
  filter becomes a live leak the moment non-Approved rows exist). Scope grew during planning at the
  owner's direction: author-deletes-comments-on-their-story (the FFN "can't remove reviews"
  grievance), **D3.2** (rec↔story attribution validation), self-rec block.
- **Two spec corrections settled before any code (Doc-Touch moment 1):** spec §5.6's "moderator
  review" was a mis-rewording of the source deliberation (author-approval + time auto-approve — no
  mod gate ever existed in the design); and on first-principles review the owner **rejected the
  pre-publication gate outright** (recs are discovery, not feedback; a gate delays discovery,
  dead-weights inactive authors, and merges the two distinct author intents — "fix an earnest
  flaw" vs "remove a troll" — into one harsh mechanism). The full deliberation (rejected
  alternatives: pre-pub gate + 7-day timer; pure post-mod binary reject) is recorded in
  `audit/Recommendations.md` §"WU-RecLifecycle settled design". There is **no `/mod/submissions`
  rec tab, ever** — `audit/Moderation.md` F48 carries the supersession.
- **Model shipped — Publish + Request-Revision + Remove:** live on submit (self-rec blocked; story
  author notified — type 22's first production sender); author `RequestRevisionAsync(note)` →
  `NeedsRevision` (hidden, note on hot `revision_request_note`, recommender notified; the
  recommender's edit auto-relives it, note cleared, author notified via new `RecommendationRevised`
  27); author `RemoveAsync` → `Rejected` (silent, sticky — edit/delete/resubmit all refused; the
  Rejected row + unique index ARE the block record); author `UnblockAsync` → straight to Approved
  (`RecommendationApproved` 40's only trigger). Flag invariant: leaving Live clears
  IsHiddenGem/IsHighlightedByAuthor (slots freed, not auto-restored); both setters refuse on
  non-Approved. Statuses now NeedsRevision(1)/Approved(2)/Rejected(3) — PendingApproval/UnderReview
  deleted (nothing ever wrote them). Migration `RecLifecycle`.
- **D1 closed:** `GetRecommendedStoryIdsByUserAsync` gains the Approved filter its siblings and its
  own interface doc always promised — regression-tested for the first time. Applies to the owner
  viewing their own profile too (owner visibility lives in the new surfaces below). **D3.2 closed:**
  `RecordAttributionSourceAsync` verifies the rec exists AND belongs to the claimed story.
- **Per-viewer reads:** `GetForStoryAsync` — public sees Approved only; the story author also sees
  NeedsRevision/Rejected (to act); a recommender also sees their own hidden rec (with note).
  Status/note projected only on elevated rows — public DTOs never carry them. New
  `GetMyRecommendationsNeedingAttentionAsync` feeds the Bookshelves Recommendations tab's
  "Needs attention" section (rec-level rows: status, author's note, story link via
  `GetListingsByIdsAsync`). `RecommendationSection`: author actions (inline revision-note panel on
  the ModSubmissionsPage reject-panel pattern; Remove behind `ConfirmDialog`; Unblock direct),
  recommender status strip + note on own hidden card.
- **Author-deletes-comments:** `DeleteCommentAsync` widened to comment-author OR the chapter
  comment's `Chapter.Story.AuthorId` (other three comment types unchanged; hard-delete semantics
  kept — deliberately weaker stickiness than rec-Remove since comments have no uniqueness).
  `CommentItem.ViewerIsStoryAuthor` threaded from `ChapterReadingPage._isAuthor` via
  `CommentSection`. Actor-class framing minted: `content-safety.md` §"Author-Controlled Content
  Actions".
- **Also:** the three `ApprovedStatusId = 2` magic-number consts now derive from the enum
  (Spotlight's idiom); SeedTool `AddRecommendation` skips self-recs (+ `MarkGem` null-guard);
  Spotlight needed **no changes** (`GetByIdAsync` null = its documented blank-rec display state).
  Co-authors deliberately excluded (dormant scaffolding — zero service/razor references); tracked
  follow-ups: co-author extension, profile-owner comment deletion.
- **New tests:** `RecommendationWriteServiceTests` +15 (self-rec block; submit notification;
  request-revision hide/note/notify/flag-clear + empty-note + non-author; edit auto-relive +
  author notification; remove silent/sticky ×3 (edit/delete/resubmit refused); unblock restore +
  notify + wrong-state guard; gem-on-hidden refused; D3.2). `RecommendationReadServiceTests` +6
  (author/recommender/public visibility split; note never leaks publicly; **D1 regression**;
  needing-attention incl. anonymous-empty). `CommentWriteServiceTests` +2 (story-author deletes
  other's comment; author-of-different-story 403). `CommentItemTests` +2, 
  `RecommendationSectionTests` +5 (author actions dispatch; public sees none; note renders).
  Fakes extended (`FakeRecommendationWriteService`, three read fakes, presenter category map).
- **Browser-verified end-to-end (L4.5, 2026-07-25)** against the dev DB, every step `psql`-confirmed:
  request-revision (hide + note + flag-clear + notify 43) → recommender's note display on both the
  story card and the Bookshelves "Needs attention" section → edit auto-relive (notify 27, flags NOT
  restored) → remove (silent, `status→3`) → **server-side stickiness proven by direct API calls: edit
  403 / delete 403 / resubmit 401** → unblock (notify 40) → self-rec **400** → fresh rec publishes
  immediately + notify 22 (that type's first production send) → **D1 confirmed**: a third party's view
  of the recommender's profile Recommendations tab showed "No recommendations given yet." while the
  rec was hidden. Comments: story author saw 3 Delete / 1 Edit, a non-author saw 0 Delete; drove a
  real post→delete→confirm round trip. Workbench restored to seed state afterward.
- **Two runtime defects found in that pass and fixed in-session** (CLAUDE.md fix-same-session rule):
  1. **`GetListingsAsync` empty-restrict bug (pre-existing since WU23, high impact).**
     `restrictToStoryIds is { Count: > 0 }` treated an EMPTY candidate set as "no narrowing," so
     **every bookshelf/profile story tab with zero candidates listed the entire library** (seen live
     on an always-empty Hidden Gems tab). It also silently undid this WU's own D1 fix. Now
     `is not null` — null = no narrowing, empty = narrow to nothing. +1 Integration regression test.
     Detail: `audit/Stories.md` Feature 5 WU-RecLifecycle note.
  2. **Self-rec CTA affordance.** "Recommend this story" was offered to the story's own author — an
     action the server can only reject. Now gated on `CurrentUserId != StoryAuthorId`; +1 bUnit test.
- **Verified:** `dotnet build` clean; `dotnet test` full suite green: **2193/2193** before the two
  browser-caught fixes, **2195/2195** after — 764 Unit (unchanged) + 575 RazorComponents (+1, the
  self-rec CTA pin) + 856 Integration (+1, the empty-restrict pin).
  `scripts/check-design-tokens.ps1`: touched files clean (the two pre-existing unrelated findings —
  `ImportReviewPanel.razor`, `ProfilePage.razor` — untouched).
- **Cells:** `status.md` — no Stage-number changes; F27/F28/F30 and F23 were already Stage 5; this
  replaces the auto-approve shortcut and inert seams under them. Exactly the hidden-deferral shape
  the tracker exists to catch.
- **Tool:** Claude Code (Opus/Fable). **Pointer:** `audit/Recommendations.md` §"WU-RecLifecycle
  settled design" + Stage note; `audit/Comments.md` F23 WU-RecLifecycle note; `audit/Moderation.md`
  F48 supersession; `layer2-services.md` §"Publish-immediately + the Recommendation Lifecycle";
  `content-safety.md` §"Author-Controlled Content Actions"; `hidden-deferrals-tracker.md` A4/D1/D3.2.

---

## WU-MsgArchive — Private-message archive/unarchive UI (closes B5) (Feature 49) — DONE ✓ (2026-07-26)

- **Trigger:** hidden-deferrals tracker item **B5** — `SetArchivedAsync`, the `includeArchived` read
  filter, the unread-badge exclusion, the HTTP endpoint, the client impl and Integration tests all
  existed, but no UI control surfaced any of it. A complete vertical slice with no button.
- **Provenance traced first (2026-07-26).** `IsArchived` has **no design deliberation anywhere in
  the record**: it first appears in the Gemini log at Entry #1539 (2025-10-25) already present in a
  SQL script the owner pasted in for Identity conversion, and the sole first-principles PM design
  turn (Entry #1409) never mentions archiving. Spec §5.19 describes the column, not a user story.
  The capability was therefore **ratified deliberately in this WU** rather than inherited by default
  — the same treatment A2 (AutoLoadNextChapter) got when its unprompted origin was traced, but with
  the opposite outcome: build it, because with no delete and no block for an established thread,
  archive is the only disposal gesture a user has.
- **Settled semantic — sticky, not filing.** A new inbound message never clears `IsArchived`;
  Gmail-style raise-on-reply was considered and **rejected** (it would let a persistent unwanted
  correspondent drag a thread back indefinitely, leaving archive with no relief value). The global
  nav badge excludes archived conversations; the per-conversation `UnreadCount` deliberately stays
  populated so the Archived tab surfaces a reply rather than swallowing it. **Zero service change** —
  this is what `GetConversationsAsync` already did. Recorded in `layer2-services.md`
  §"Conversation Archiving Is Sticky" as a Doc-Touch moment-1 item, before any code.
- **Built:** Archive/Unarchive button in the `MessageThread` header (the sole affordance —
  `ConversationListItem` stays a single `<a>`, which cannot legally contain a `<button>`);
  Inbox|Archived segmented toggle in `MessagesPage` (recipe from `NotificationsPage`); per-tab fetch
  with `[PersistentState]` on the Inbox list only and the archived list ephemeral/on-demand (the
  archived set is the one that grows without bound, so it must not ride along on every page load);
  archiving navigates to `/messages` and resets to Inbox; `ConversationThreadDto.IsArchived` added
  (free — the header query already read the viewer's participant row; sourced there rather than off
  the sidebar list so direct-URL navigation resolves correctly); `ConversationListItem`'s "Archived"
  chip **removed** as redundant under the tab split, its ratified `surface-registry.md` row struck.
- **L2 — inbox sort pushed from C# into SQL.** **The two-key idiom is load-bearing:** Postgres
  defaults to `NULLS FIRST` for `ORDER BY … DESC`, so a naive single-key translation would silently
  promote message-less conversations to the top of every inbox. `.OrderByDescending(x =>
  x.LastMessage!.DateSent != null).ThenByDescending(…)` preserves the message-less-sorts-LAST
  contract. No paging added — conversation counts are bounded by human effort, unlike notifications.
- **Verified:** `dotnet build` clean; `dotnet test` full suite green — **2213/2213** (764 Unit +
  590 RazorComponents + 859 Integration). New: 3 Integration (ordering-with-message-less-last — the
  guard on the sort move; `includeArchived` both directions; the sticky invariant driven through the
  real service), 13 RazorComponents (`MessagesPageTests` ×8 — first page-level messaging coverage,
  via the new `FakeMessagingWriteService`; `MessageThreadTests` ×5), and `ConversationListItemTests`'
  archived-chip test replaced by its inverse plus an unread-survives-archiving pin.
  **L4.5 browser pass (2026-07-26)** on the server-only path, every step `psql`-confirmed: archive →
  navigates away, pane clears, row leaves Inbox, nav badge goes quiet, `is_archived` flips for the
  viewer's row only; Archived tab shows it with no chip and the header reads "Unarchive". Sticky
  proven end-to-end by signing in as the other participant and sending a **real reply** while
  archived — no return to Inbox, no nav badge, but the Archived tab showed the unread count and new
  preview. Unarchive returned it. Ordering verified live with seeded fixtures: newest → older →
  **message-less last**. Zero console messages, zero server-log errors; fixtures removed and the dev
  workbench restored to seeded state.
  `scripts/check-design-tokens.ps1`: no findings in any Messaging file (the two pre-existing
  unrelated findings — `ImportReviewPanel.razor`, `ProfilePage.razor` — untouched, same as
  WU-RecLifecycle recorded).
- **Cells:** `status.md` — **no Stage-number changes.** F49 L2/L3-Logic/L3.5/L4/L4.5 were all already
  Stage 5 and remain so; this fills in inert plumbing underneath them. Exactly the hidden-deferral
  shape the tracker exists to catch.
- **Deliberately out of scope:** no index work, and no doc note claiming index work was cut — tracker
  item **C4** is left exactly as written, per the owner's instruction that all index work happens
  later as its own pass.
- **Tool:** Claude Code (Opus). **Pointer:** `audit/Messaging.md` §"WU-MsgArchive"; `layer2-services.md`
  §"Conversation Archiving Is Sticky"; `design/surface-registry.md` (struck Archived-chip row);
  `hidden-deferrals-tracker.md` B5.

- **Same-session review addendum (2026-07-26).** A post-completion review found: (1) an
  **Archived-tab sidebar staleness defect** — `LoadThreadAsync`/`HandleSendReplyAsync` refreshed only
  the Inbox list while the sidebar renders `_archivedConversations` on the Archived tab, so a
  just-read archived thread kept its badge until a tab toggle; fixed (both handlers refresh the
  archived list when that tab is active) + regression-pinned
  (`OpeningArchivedThread_FromArchivedTab_ClearsItsSidebarUnreadBadge`; the fake's mark-read now
  zeroes the store's unread count so the flow is observable). (2) The plan's **generated-SQL
  inspection step had been skipped** — discharged via `ToQueryString()`: projection uses ROW_NUMBER
  window joins (good); ORDER BY keys re-emit correlated subqueries rather than reusing the join;
  first key switched to `x.LastMessage != null` (translates to a cheaper `EXISTS` probe); both keys
  are single seeks on `ix_private_messages_conversation_id_date_sent`, negligible at human-bounded
  counts — full key/join reuse belongs to the deferred ID-first read-path rework. (3) Polish:
  tab-scoped Inbox empty copy ("Your inbox is empty."), inline-error catch on the archived tab
  fetch. Post-addendum: full suite green **2214/2214** (764/591/859). Detail: `audit/Messaging.md`
  WU-MsgArchive addendum.

---

## WU-TokenGreen — restore the design-token gate to green (Features 63 L4 / 21 L4) — DONE ✓ (2026-07-26)

- **Trigger:** `scripts/check-design-tokens.ps1` — nominally a CI gate — had been exiting 1 on the
  same two findings across multiple work-units (recorded as "pre-existing, untouched" in at least
  WU38d-era entries, WU-RecLifecycle, and WU-MsgArchive). A permanently-red enforcement gate trains
  everyone to ignore it; owner directed the fix.
- **Fixed:**
  1. **`Import/ImportReviewPanel.razor` — UGC outside ContentSurface.** The expanded draft preview
     rendered `RichTextView` in a bare bordered div. Drafts are user prose; the UGC-on-ContentSurface
     rule applies to previews. Now `<ContentSurface Variant="Inline">` inside a scroll-only wrapper
     (`max-h-64 overflow-y-auto` — ground/frame/padding moved to the surface, per the role system).
  2. **`Profiles/ProfilePage.razor` — undeclared `--color-link`.** The sign-in-required state's link
     referenced a token that never existed in `@theme` (class compiled to nothing; the link rendered
     in inherited ink). Swapped to the ratified link token `--color-action-ink`.
- **Verified:** `scripts/check-design-tokens.ps1` **green** (first clean run since the findings were
  introduced); `ImportReviewPanelTests` + `ProfilePage` RazorComponents tests pass unchanged (9/9 in
  the filtered run — no test pinned the old markup); full suite green 2214/2214 immediately prior
  (WU-MsgArchive addendum) with only these two markup-local diffs since.
- **Cells:** no Stage changes — F63 and F21 L4 already Stage 5; both were latent visual defects under
  Stage-5 cells (the `--color-link` one user-visible: an unstyled link).
- **Tool:** Claude Code (Fable). **Pointer:** `audit/Import.md` token-fix note; `audit/Profiles.md`
  §"Token fix (WU-TokenGreen)".

---

## WU-ParentVisibility — the parent-visibility invariant: 38 surfaces across 12 clusters (D2 and its whole class) — DONE ✓ (2026-07-26)

- **Trigger:** `hidden-deferrals-tracker.md` **D2** ("Poll `by-blog-post` leaks draft metadata").
  Investigation showed D2 was not a defect but a symptom: a sweep of all 29 server read services and
  all 26 server write services found **38 surfaces** where child content was more visible, or more
  writable, than its parent. Owner chose one sweep over staged WUs, and required the plan be written
  as intent/requirements rather than implementation.
- **Root causes (two, neither a coding mistake):** (1) the bare-FK shape
  `readDb.Children.Where(c => c.ParentId == id)` never expands the parent entity, so **no** named query
  filter (`ContentRating`, `GroupAudience`, `StoryStatus`, `IsTakenDown`) and no reveal check can reach
  it; (2) those filters live only on `ReadOnlyApplicationDbContext` — `writeDb` is unfiltered, so every
  `writeDb.X.AnyAsync(id == …)` existence check proved existence and nothing else. The rule *did* exist
  (the "join-not-bare-projection rule") but only inside the StoryLineage and Spotlight narratives in
  `layer2-services.md`, so nobody writing a poll or comment service would meet it.
- **Convention first (Doc-Touch moment 1):** `identity-and-authorization.md`'s "Six kinds of
  active-user conditionality" is now **seven** — new kind **(g) parent-visibility inheritance** — plus a
  §"Parent-visibility guards" section carrying the guard set, the contract shape, and four
  easy-to-get-wrong rules (non-disclosure; authors keep their drafts; takedown outranks authorship;
  narrow exemptions). `layer2-services.md`'s two incidental mentions now point at it.
- **Guards shipped:** `BlogPostVisibilityGuard`, `StoryVisibilityGuard` (story + chapter), and
  `GroupVisibilityGuard`, joining the existing `ProfileVisibilityGuard` — one per parent kind, not one
  universal guard (parents differ in columns and reveal target). Each exposes a pure decision over
  already-projected facts plus an id-loading overload, so `ServerBlogPostReadService.GetByIdAsync`
  delegates its gate with **zero** extra queries while child services pay one lookup. `GetByIdAsync`
  now owns none of the rule.
- **Two axes, made explicit:** confidentiality (story status, takedown) is absolute; consent (rating)
  is reveal-bypassable and deliberately not applied to a few writes. `IsStoryPublishedAsync` serves
  those — recommendation submit, custom-list add, group story-add — preserving three *existing* tests
  that assert the permissive behavior. The suite caught every one of these; none was guessed.
- **Reads fixed (13):** polls ×2 (D2 itself, incl. the wider by-id hole), comments ×3 (blog/chapter/
  group), group members, blog-posts-by-group, recommendations ×2, story arcs, story total views,
  manual-tree-search ×2.
- **Writes fixed (25):** poll vote, comments ×5, recommendations ×4, blog-post like, chapter
  read-marks ×2, user-story interactions ×3, group join + story-add, custom-list add, report submit,
  follow + vouch, lineage request, and the two buffered writes.
- **Notable specifics:** `RecordSuccessAsync` awards real site badges off an unverified parent — a loop
  over guessed ids could farm another user's `SuccessfulRecCount` and badges. `JoinAsync` let a
  mature-off account join an M-audience group, unlocking the membership-gated writes and M-content
  notification fan-out. `SubmitReportAsync` had **no existence check at all**. `ServerStoryArcReadService`
  was the only service injecting no `IActiveUserContext` and so could not gate at all — constructor changed.
- **Settled decisions (2026-07-26, recorded before implementation):** buffered writes validate at
  **drain time** (the flushers' existing `EXISTS` guard now carries `DiscoveryMartSchema.VisibleStory`,
  reused rather than restated — buffer entry keeps zero added latency, and only the confidentiality axis
  is meaningful in a viewerless background scope); reports require existence **always** and visibility
  **except when the parent is hidden solely by takedown** (a good-faith report filed just after a removal
  must still land); custom-list add verifies its previously-unverified premise.
- **Two false comments corrected.** `ServerGroupWriteService` claimed "the audience filter is active on
  writeDb too" — it is not, and the same file says so correctly twice elsewhere; that false comment was
  load-bearing for the join hole. `ServerCustomListWriteService` asserted a premise the code never checked.
- **New tests:** `Tests.Integration/ParentVisibilityContractTests.cs` — **36** tests; the enrolment list
  *is* the enforcement mechanism (adding a parent-scoped read/write means adding a row). Covers each
  hidden-parent kind × read-empty/write-refused, plus the positive directions: author still sees and
  manages their own draft's poll, and the two deliberately rating-permissive writes still succeed.
  Docs alone had already failed once — the rule was written down and the WU-AccessGate sweep still
  shipped `GetUserNeighborsAsync` handing a Private profile's contents to anonymous callers.
  **Self-audit correction (same session):** the suite shipped at 27 tests while its own doc comment
  claimed every governed surface was enrolled — nine were not, including `RecordSuccessAsync` (the
  badge-award path this WU called its sharpest find) and both buffered writes, whose drain-time
  validation had nothing proving it drops hidden rows. The suite was green the whole time. The nine
  were added and the doc comment now carries the correction, because "the guard is called from that
  method" is not coverage. Exactly the failure mode this WU exists to prevent, found in its own
  deliverable.
- **Four pre-existing tests corrected, not weakened:** two `BlogPostWriteServiceTests.ToggleLike_*`
  were liking an *unpublished draft* as a non-author (asserting the leak — `CreatePostAsync` defaults to
  a draft); `CustomListServiceTests.AddStoryAsync_MRatedStory_MatureOffOwner_StillAdds` and
  `GroupServiceTests.AddStory_Tier2_StoryRatingExceedsGroupMax_Throws` documented real settled decisions
  and drove the confidentiality-only split above.
- **Verified:** `dotnet build` clean. `dotnet test` full suite green — **2241/2241**
  (764 Unit + 591 RazorComponents + 886 Integration) at the WU's own commit `1308f13`, and
  **2271/2271** (753 + 591 + 927) after the nine added contract tests and the WU-TagFanon /
  messaging commits that landed on top; the tier counts moved for reasons unrelated to this WU, so
  the earlier figure is not reproducible on a later tree. `scripts/check-design-tokens.ps1` passed.
  **HTTP pass (anonymous + per-user cookies):** every fixed read probed against seeded fixtures — draft
  post's poll `[]` vs published control returning the poll; group-3 (M audience) members/comments/
  blog-posts empty for anonymous and for a mature-**off** user, real data for a mature-**on** user;
  standard group unaffected; draft story's arcs/views/recs empty with published controls intact.
  **Non-disclosure confirmed byte-identical:** hidden poll and nonexistent poll both return empty body,
  status 200. **Write refusals confirmed at the DB:** a stranger voting on the draft's poll got 404 with
  `psql` showing 0 vote rows and `ConfigLocked` still false, while the published control took the vote;
  mature-off join → 404, mature-on join → 204. **Browser pass (L4.5):** as the draft's author, `/blog/3`
  renders with its poll fully manageable; as a stranger the same URL is a real 404; the published post
  still renders its poll including the stranger's own vote state. All verification rows cleaned up
  (`psql`-confirmed zero remaining).
- **Cells:** `status.md` — **no Stage-number changes.** Every affected cell was already Stage 5 and
  remains 5; the invariant is cross-cutting and attaches to no single cell, so it is recorded as a
  Global Conditions note pointing at the convention section. Exactly the hidden-deferral shape the
  tracker exists to catch.
- **Tool:** Claude Code (Fable). **Pointer:** `identity-and-authorization.md` §"Parent-visibility
  guards" + kind (g); `layer2-services.md` (two cross-references); `audit/BlogPosts.md`,
  `audit/Comments.md`, `audit/Chapters.md`, `audit/Groups.md`, `audit/Recommendations.md`,
  `audit/Discovery.md`, `audit/Following.md`, `audit/Moderation.md`, `audit/Stories.md`;
  `hidden-deferrals-tracker.md` D2.

---

## WU-TagFanon — Tag-model overlay reshape + fanonization pipeline — DONE ✓ (2026-07-26)

**Why it grew.** Started as tracker item **A5** ("fanonize notify/migrate flow"), framed as: flip
`IsFanon`, match `OcName` to `TagName`, notify, offer a one-click update. Planning against the
Gemini-era record (Entry #1316 + the §IV.7 architecture summary) established the real intent — a
three-tier character model (generic archetype → specific-canon child → fanon child) where
fanonization is a *moderator review process starting from the story data* — and auditing what
existed against it found the subsystem beneath A5 substantially non-functional. A5's one-line
framing was wrong three times over: no entry point existed, it breaks on the owner's own
`"Saura (Silver Resistance)"` example, and it never establishes `ParentTagId`.

**Nine requirement groups, delivered in dependency order** (plan:
`~/.claude/plans/i-want-to-plan-resilient-sonnet.md`):

1. **Doc-touch first** — the WU37 routing-table reopening recorded in `audit/Tags.md` as a
   deliberate Stage-4 reopening; `layer1-data-model.md` + `layer2-services.md` rewritten;
   `grid_axes.md` drift fixed (stale `Relationship` tag type + the never-implemented
   `TR_StoryCharacters_EnforceOCLogic` trigger).
2. **Model** — `CustomName`/`Nuance` on `StoryTag` AND `StoryCharacter`; single
   `Tag.AllowCustomName`; `SettingDetail` deleted (folded onto the junction — cardinality rule);
   `UNIQUE (StoryId, CharacterTagId, CustomName)` NULLS NOT DISTINCT; pairing members became row
   indexes; L1 length drift fixed. Migration hand-edited to be **data-preserving**: flag OR-merge
   before the drop, side-row fold before the table drop, truncation guard on the 512→500 shrink.
3. **Seed** — SeedTool gained the whole tag world (vocabulary with parent/child trees, fanon
   population, 14 OC-name clusters spanning both sides of the reach threshold, overlays, pairings,
   saved selections, notification settings, one pre-linked cluster with type-26 rows). DataSeeder
   gained the three-tier showcase + a two-author "Saura" cluster.
4. **Display/authoring** — chip fanon ✦ + parent ring + tooltip + sr-only cue; parent-inherited
   sprites; the `*` overlay reveal on story pages AND cards; loud/quiet nuance affordance;
   `FlatTagOverlayEntry` generalizing the deleted Setting-only entry; repeat character selection.
5. **Discovery** — hierarchy roll-up + the ship-filter axis.
6–8. **Fanon pipeline** — `/fanon` hub + axis pages (public, mod controls inline), the
   link-and-notify act, `/tag-adoptions` index + per-tag adoption page, editor nudge.
9. **Docs** — this entry, the audit notes, status.md global condition, tracker rewrite.

**Verified:** `dotnet test` **2258 green** (753 Unit / 593 RazorComponents / 912 Integration);
design-token check green; browser pass against the extended seed with psql ground truth at every
mutation; live `pg_indexes` sweep + EXPLAIN ANALYZE (no new indexes warranted; **tracker C1
resolves to REJECT**, measured at 0.079 ms over 136 tags).

**Two bugs found in the browser pass and fixed same-session** (per `debugging.md`): the mod link
panel pre-filled the create-new tag name, so typing in the typeahead without selecting a result
silently minted a duplicate tag; and `TagEditorForm` never hydrated `SpriteIdentifier`, so editing
any sprite-bearing tag cleared its sprite key.

**Tracker impact:** supersedes **A5**; resolves **C1** (measured → reject); half-closes **C4**
(F15 seeding lands; F49 Messaging stays open); closes **H5** in full; corrects **H6**'s tag-length
drift. Adds newly-found seams: `PrefersDataSaverMode` inert, and the six defects listed in
`audit/Tags.md`'s Stage note.

- **Tool:** Claude Code (Fable). **Pointer:** `audit/Tags.md` §"WU-TagFanon Stage note";
  `audit/Discovery.md`, `audit/Notifications.md`; `layer1-data-model.md`, `layer2-services.md`;
  `status.md` Global conditions; `hidden-deferrals-tracker.md`.

### WU-TagFanon post-review pass (2026-07-26, same session)

A deliberate re-read of the WU-TagFanon diff after the suite was green and the browser pass was
clean. It found four defects and two architectural gaps. **None was caught by the 2258 tests or the
browser verification** — recording that because it argues for diff-review as a distinct step, not a
formality after green tests.

**Fixed:**
1. **Malformed ship input returned 500, not 400.** `ApplyShipTerm` threw `ArgumentException` from
   inside predicate assembly when a ship named more than `ShipFilterDto.MaxMembers` characters.
   Replaced with a `ValidateShipShape` guard at the service entry throwing `StoryValidationException`
   (a `CanalaveValidationException`, which the endpoint layer maps to 400); it also now rejects a
   ship naming the same character twice.
2. **Adoption crashed on case-variant duplicates.** The fanon group key is case-INSENSITIVE but the
   `story_characters` unique index is case-SENSITIVE, so one story could legally hold "Saura" and
   "saura" on one base tag — and adopting mapped both to `(story, target, NULL)`, violating the
   index as a raw `DbUpdateException`. Root fix: `ValidateStructuredTagGatesAsync` now compares
   custom names case-insensitively, so writes can no longer create the pair. Existing rows are
   handled by treating such a story as a collision — skipped with the same explanation, never merged.
3. **Two N+1 loops.** `GetGroupsAsync` ran two queries per linked group inside a `foreach`;
   `GetMyAdoptionIndexAsync` ran one count per fanon link SITE-WIDE, unbounded by paging. Both
   rewritten to batch (`GroupAuthorsBatchedAsync` + a single notified-pairs query; two batched
   queries for the adoption index). This was a violation of `layer2-services.md`'s own
   "Two-Pass Batch Enrichment" rule, in the same file family as tracked defect MA-408.
4. **The "data-preserving" migration had never run against data.** Both prior applications were to a
   freshly-dropped dev database, so the flag OR-merge, the SettingDetail fold and the description
   truncation all executed against zero rows. Now proven by
   **`scripts/verify-tagfanon-migration.ps1`** — stands up a scratch DB at the pre-overlay schema,
   seeds representative old-shape rows (both gate flags independently, an over-length description,
   an OC overlay, a SettingDetail side-row), applies the migration and asserts 12 preservation
   claims. All pass. Re-run it whenever those migrations are edited.
   *(Two Windows-PowerShell traps encoded in that script: native-command stderr under
   `$ErrorActionPreference='Stop'` turns psql NOTICEs into terminating errors, and PS 5.1's
   native-argument quoting strips the double quotes around `"AspNetUsers"` — hence `psql -f file`
   rather than `-c`.)*

Also removed a dead `_linkingGroup` field (and the now-unused `OpenLinkPanel` parameter) — refactor
residue that created a second source of truth for the open panel.

**Recorded, not built:** `hidden-deferrals-tracker.md` **B11** (ship filter has no restore path —
carries the URL-round-trip decision that must be settled first) and **B12** (roll-up made
`ApplyFilters` impure; expansion is uncached and unshared with Layer 8, and the 0.02 ms figure
measured localhost DB execution rather than a production round-trip). Both entries are written to be
planned from, with options and trade-offs.

**Doc correction:** `audit/Discovery.md` had let the settled F15 decision ("ships are never persisted
in `SavedTagSelection`") read as though it also settled ship URL/seed round-tripping, which was never
discussed. The note now separates settled from open explicitly. This is the same failure mode
WU-TagFanon existed to clean up — a non-decision wearing a decision's clothes — reproduced in the
same session, which is why it is called out here rather than quietly amended.

**Verified:** `dotnet test` green; three new regression tests (case-variant adoption skip, ship
arity 400, repeated-member 400); migration script green.

---

## WU-MsgReadPath — ID-first conversation listing + scoped reads (Feature 49 L2/L5) — DONE ✓ (2026-07-26)

- **Trigger:** WU-MsgArchive's review had deferred the messaging read-path rework as coupled work
  ("don't do the small half alone"); owner chose to take it now as foundation rather than leave it.
- **Did:** `ConversationScope` enum (`Active`/`Archived`, disjoint, deliberately no "all") replaces
  `includeArchived` across `IMessagingReadService`/server/client/endpoint
  (`?scope=Archived`); `ConversationSummaryDto.IsArchived` **removed** (scope implies it — keeping
  it would be the tracker's own inert-plumbing shape; the per-thread flag stays on
  `ConversationThreadDto`); `GetConversationsAsync` restructured to the two-step ID-first shape
  (metadata: ids + `MAX(date_sent)`, NULLS-last two-key order, the future Skip/Take site →
  hydration: participant/unread/`SUBSTRING(message_text,1,2048)` — never the whole body), rows
  reassembled in step-1 order; `MakePreview` drops SQL-bisected trailing tag fragments;
  `MessagesPage.LoadArchivedAsync` collapsed to a direct scoped call (client-side filter gone).
- **SQL shape inspected** (`ToQueryString`, scratch deleted): step 1 = two correlated `MAX()`
  ordering seeks on `ix_private_messages_conversation_id_date_sent`, id-only projection; step 2 =
  ROW_NUMBER window joins, substring inside the join, no ORDER BY. Convention:
  `layer2-services.md` §"Conversation listing is scoped, ID-first, and unpaged" (supersedes the
  WU-MsgArchive paragraph).
- **Verified:** full suite green — 753 Unit / 591 RazorComponents / 916 Integration (counts absorb
  the concurrent WU-TagFanon session's tests). New Integration pins: scope disjointness (Archived
  returns archived rows only) + bounded preview on a ~9 KB body (≤101 chars; also proves the
  `Substring` translation against real Postgres). bUnit: fake store reworked (archived flag beside
  the DTO); the two chip tests retired — the pin is now structural (uncompilable). Browser smoke on
  the server-only path: Inbox / empty Archived / archive → scoped Archived list (preview from the
  bounded prefix) / unarchive round trip, `psql`-confirmed clean workbench, zero console errors.
- **Cells:** no Stage changes — F49 L2/L5 already Stage 5; this replaces the shape underneath.
  L6 untouched: C4's messaging half stays open, all index work deferred per standing instruction.
- **Tool:** Claude Code (Fable). **Pointer:** `audit/Messaging.md` §"WU-MsgReadPath";
  `layer2-services.md` §"Conversation listing is scoped, ID-first, and unpaged".

- **Post-WU review addendum (2026-07-26) — WU-MsgReadPath.** A self-review after the WU closed found
  seven items; all fixed in-session. The consequential one:
  1. **The claimed payload improvement had never been measured** (violating the standing "always
     measure" rule). Measuring reversed the conclusion **twice**: the shape as shipped was an
     **88 % regression** (11.31 ms vs the pre-rework 5.72 ms), because writing the preview
     `Substring` inside the `FirstOrDefault` projection pushed it into EF's `ROW_NUMBER()` window
     over the whole `private_messages` table — detoasting all 8 460 rows before eliminating them to
     401. Moving it to the outer projection makes EF emit a correlated `ORDER BY … LIMIT 1` index
     seek instead (no `Seq Scan` on messages at all), giving **3.14 ms — a 45 % improvement** over
     baseline. Now a do-not-simplify rule in `layer2-services.md`; numbers + EXPLAIN plans in
     `PerfBaseline/results/msgreadpath*`; volume reproducible via the new
     `PerfBaseline/seed-messaging-volume.sql` (three permanent `messaging_inbox_*` scenarios added).
     **Neither the green suite nor the clean browser pass detected this** — only measurement did.
  2. **Repo hygiene (pre-existing, not this WU):** ~30 MB of `.trx`/coverage artifacts were tracked
     in git (two entered history in `f2d7527`). `.gitignore` now covers `TestResults/`/`*.trx`/
     coverage output and the four files are `git rm --cached`'d. History still carries the blobs.
  3. **Two untested branches closed:** the `MakePreview` bisected-tag guard (the long-message test
     cut mid-word, never mid-tag) and the accepted "markup-dense bodies yield a shorter preview"
     behavior — both now Integration-pinned.
  4. **Corrected an overclaim in `audit/Messaging.md`:** it said the browser pass verified the
     preview "rendered from the bounded prefix". It did not and could not — the seed conversation's
     messages are far under the prefix, so the SUBSTRING is a no-op there. Bounded-prefix behavior
     is Integration-covered only; the note now says so explicitly.
  5. **Process deviation, recorded rather than hidden:** WU-MsgReadPath opened by stating the
     `layer2-services.md` rewrite was a Doc-Touch *moment-1* item to be done first, then actually
     wrote it last, after all code. CLAUDE.md requires moment-1 touches to complete **before** any
     code change. No harm resulted here (the convention text landed accurate), but the sequence was
     wrong and stating the rule while breaking it is worth the record.
  6. **Deploy note (`?includeArchived=` → `?scope=`):** a breaking query-param rename with no API
     versioning story. A stale cached WASM client would send the old param, bind nothing, and
     silently render the **inbox** under the Archived tab. Harmless pre-launch (no external
     consumers, no cached clients in the wild); flagged for the Phase-7 launch checklist because
     the failure mode is silent rather than an error.

## WU-DocHygiene — process-doc contradiction & staleness cleanup (no code cells) — DONE ✓ (2026-07-27)

- **Trigger:** a four-agent cross-check sweep of the full process-doc corpus (~28k lines: CLAUDE.md,
  status.md, grid_axes, folder_clusters, middle_plan_v2, middle-addendum, all audit files, all
  canalave-conventions skills, design/, tracker, workplan) found ~60 confirmed contradictions and
  stale claims. Three root causes: paradigm shifts never swept through the corpus (the Global Flip
  2026-07-13 and the Desktop/Mobile fork removal 2026-07-18), status.md Global Conditions drifting
  into a changelog, and audit-file headline stage lines never revisited after later Stage notes
  superseded them.
- **Grid corrections (the only cell changes):** F44 L5 `N/A → 5` (ReadingProgressEndpoints +
  ClientReadingProgressWriteService exist — the grid_axes buffered-signal L5 exception);
  F47/F48 L5 `N/A → 5` (ClientModerationRead/WriteService + endpoints exist; `/mod/*` pages
  WASM-verified in WU-GlobalFlip's wave — Brian-approved 2026-07-27). Row 66's Folder cell
  `AccessGate → ContentGate` (no AccessGate folder exists in code); row 53's Folder → `Stories`
  (WU39 settlement); row 17 renamed to grid_axes' "Story Interaction Lists & Bookshelves".
- **status.md:** seven single-cell narratives collapsed to pointers (their full text already lived
  in audit files); dead "Stage-4 cells" doctrine note, both contradictory F4/F5 L5 clauses, the
  superseded alias-bridge/"Phases B–F executing"/"design underway" clauses, and the three retired
  forward_plan/middle_plan pointers all fixed; the missing WU-AccessGate Global condition added;
  the WU-GroupsL5 note's "(27–30)"/"one genuine gap" claims corrected.
- **grid_axes.md:** Layer 4.5 section added (definition moved from status.md, pointer left);
  Features 65/66 reordered; "64–65" cross-cutting note extended to 64–66; typeahead ref updated.
  **CLAUDE.md:** feature count 65→66.
- **Audit headline reconciliation:** Lookups.md L1 Stage-4 divergence list rewritten as the
  Stage-5 record (all five items verified resolved in code — SiteSearchModes catalog,
  DefaultSortOrder axis, ReadStatus/FavoriteStatus removal); Identity.md F52 L4; Discovery.md
  F31 L2 / F34 L2-L3.5 / F59 L3-L3.5 headlines (own later Stage notes contradicted them) +
  IDeviceDetectionService ref; Tags.md WU-TagFanon title overclaim + F14 L5 supersession;
  Moderation.md F46 L5 note re-scoped to F47/48 with both stage notes corrected; Sprites/Lookups
  L7 enumerations; Accessibility.md's Seo/ precedent claim; Stories.md + layer4-style.md
  session-relative language replaced with dated references.
- **folder_clusters.md:** F56 removed (CUT); Vouch question marked settled; dispatcher wording
  struck; `Images/`/`Errors/`/`Toasts/` cross-cutting rows added; SiteSettings ledger note;
  Core/Series relocation note; UserStoryInteractionPanel name.
- **middle_plan_v2.md:** eight shipped items retensed with DONE dates (WU-Observability, WU-Email,
  WU40, WU43, WU38a, WU-AccountEnforcement-in-WU38a + residual, WU-AccessGate/+2, workers
  57/58/62); Phase 5 retitled DONE (WU-L5Sweep + WU-GlobalFlip) and the Phase-6 gate updated;
  three intra-doc supersessions fixed (WU35 SignalR pointer, OG noindex deferral, the 07-05
  snapshot banner); Phase-7 checklist gains SPF/DKIM/DMARC DNS (row 8 + addendum #13) and the
  addendum #8–#14 operational-resilience group. middle-addendum §2's table annotated with later
  DONE/superseded outcomes.
- **Conventions skills:** Global Flip retensed everywhere (SKILL.md axiom 8, render-and-layout
  code sample `InteractiveServer → InteractiveAuto` + dev-shortcut note, security.md ×2,
  layer5-wasm ×4 incl. the L5-Stage-Semantics 2026-07-24 correction note); device-fork teaching
  purged (SKILL.md scope rows + taxonomy table, cross-cutting MessagesNavLink + EditorView
  toolbar reframe, layer3.5 TreeSearch/GroupPage refs, layer4 StoryPage ref); the layer3.5
  TagSelector recipe rewritten against the real `CanalaveTypeahead` contract (pick-fires-a-
  callback, no SelectedTemplate) and layer3-logic's debounce ref updated; identity file: "six"→
  "seven" kinds, posture heading renamed to match its own MA-104 correction, IsModerator
  comment + intro reworded to match §"Two Enforcement Surfaces" (it IS the server-side
  enforcement input), access-gating design-doc pointers added here + security.md;
  render-and-layout's dead "won't exist post-WASM-split" claim fixed; SKILL.md hub: ContentGate/
  + Controls/ cluster bullets, layer2 topic list expanded, content-safety summary + author-
  controlled actions, retired-plan pointer swaps (layer4-style, layer2-services ×2); two soft
  anchors promoted to real headings (layer2-services §"Publish-immediately + the Recommendation
  Lifecycle", run-server §"Extended seed").
- **Lifecycle (all Brian-decided 2026-07-27):** `hidden-deferrals-tracker.md` + `middle-addendum.md`
  + `modernization-audit/` + a `.claude/design/` genre row added to CLAUDE.md's table;
  audit-summary row rewritten (superseded caveat); L6-intent-ledger + L6-reconciliation-matrix
  moved `audit/ → design/` (wrong genre for audit/; all references repointed incl.
  PerfBaseline/Scenarios.cs + seed-messaging-volume.sql comments); test-hygiene-manifest folded
  into tracker H7 (the deferred `*Mobile` deletions were already discharged by WU-ResponsiveMerge)
  and retired with a banner; modernization-audit README got a completion banner + its plan of
  record copied into the repo (`plan-of-record.md` — the `~/.claude/plans/` original is
  unreachable to future sessions); middle-audit.md marked DISCHARGED (all 2026-07-07 findings
  verified actioned); forward_plan banner now points through to v2; workplan preamble's
  forward_plan rule pointer + the stale WU-AccountEnforcement "planned" entry fixed. New CLAUDE.md
  process rule (Brian-approved): when a WU retires a pattern/term/component, grep all process docs
  for the retired name in the same WU.
- **Verified:** grep gates clean — zero non-historical hits for BlazoredTypeahead, `{X}Desktop`/
  `{X}Mobile` components, `IDeviceDetectionService`, "this session" (outside dated workplan
  blocks), live `forward_plan`/`middle_plan.md` rule pointers, or Stage-4 claims. `dotnet build`
  0 errors. `dotnet test` full suite green **2271/2271** (753 Unit + 591 RazorComponents +
  927 Integration). Docs-only change (two .cs/.sql comment
  lines repointed); no behavior surface touched — Unit/Integration/RazorComponents cover nothing
  new because nothing testable changed.
- **Tool:** Fable 5 in Claude Code (four parallel Explore agents for the cross-check sweep, then
  direct implementation).

## WU-DocHygiene2 — process-doc best-practices hardening (no code cells) — DONE ✓ (2026-07-27)

- **Trigger:** the post-WU-DocHygiene analysis of residual structural weaknesses; all seven
  recommendations Brian-approved 2026-07-27.
- **Built:**
  1. **`scripts/check-doc-hygiene.ps1` + CI step** — the doc analog of the token check: fails on
     retired terms mentioned as live in the live docs (7-term seed registry; retirement WUs
     append), session-relative language outside the dated workplan ledgers, and live pointers
     into retired plan files. First run immediately caught a real stale recipe:
     `layer2-services.md`'s WU37 routing block still built the deleted `SettingDetail` and named
     the replaced `AllowOCDetails`/`AllowSettingDetails` gates — rewritten against the current
     `StoryMappers.cs` (CustomName/Nuance on-row, pairing members by index).
  2. **status.md Global Conditions re-genred to standing constraints only** (13 bullets, down
     from ~25 event-log entries; each states a currently-binding fact, deleted when it stops
     binding). Genre rule recorded in the section header + CLAUDE.md's status.md row. The one
     line-number reference into the old layout (tracker H8 "status.md line 85") repointed.
  3. **CLAUDE.md moment-3 additions:** update the audit file's headline stage line in the same
     edit as the Stage note (never append-only), and the standing-constraint genre for Global
     Conditions notes.
  4. **folder_clusters.md columns re-scoped to structural facts** — the 16 stale
     "Missing"/"Blocked on design tokens" cells (premise died when tokens locked 2026-07-10)
     replaced with owned-surface/recipe statements; vocabulary retirement noted in its header +
     CLAUDE.md row.
  5. **workplan.md split:** Phase A–E build arc + dated DONE entries 2026-07-06→07-18 moved
     wholesale to the new `workplan-archive.md` (append-only; "workplan.md WU-X" citations
     resolve there); live file dropped 4,337 → ~1,050 lines and keeps the preamble,
     blocked/planned/post-MVP sections, and entries from 2026-07-24 on. CLAUDE.md table row added.
  6. **Position block** at the top of workplan.md (last landed / phase / between-phase work /
     blocked-on-Brian), maintained at moment 3.
  7. **CLAUDE.md "Retiring or closing" rule extended** to closures: closing a deferral or
     resolving a decision sweeps all three open-work ledgers (middle_plan_v2 decision
     table/phases, hidden-deferrals-tracker, workplan blocked/planned) in the same WU.
- **Verified:** `scripts/check-doc-hygiene.ps1` clean (25 live docs, 7 retired terms, 64 process
  docs); `scripts/check-design-tokens.ps1` unaffected; `dotnet test` full suite green (see
  commit). Docs/tooling only — no behavior surface; no cell Stage changed.
- **Tool:** Fable 5 in Claude Code, direct implementation.

## WU-DocHygiene3 — fresh-eyes fixes on the doc corpus (no code cells) — DONE ✓ (2026-07-27)

- **Trigger:** a three-agent fresh-eyes analysis after WU-DocHygiene/-2 landed (cold-session
  orientation walk, restructure integrity check, untouched-files probe). It confirmed the core
  sound (grid/constraints/Position triangle unbreakable under attack; the workplan split lost
  zero content by byte-level diff) and found three defect pools: damage the surgery itself
  introduced, pre-existing dirt in the hygiene gate's blind spots, and routing/doctrine gaps.
- **Gate widened:** `.claude/design/*.md` added to `check-doc-hygiene.ps1` `$liveDocs`
  (**surface-registry.md exempted** — Brian ruled it a paused-session artifact pending a
  ground-up rewrite once the foundation/tracker work completes; banner added to the file, caveat
  added to its CLAUDE.md row, exemption documented in the script); device-fork regex generalized
  from a six-name alternation to `(?<!WU-)\b[A-Z]\w+(Desktop|Mobile)\b` + `(Desktop|Mobile)Layout`
  (immediately caught three more live-doc hits: error-handling boundary table, layer3.5
  NotificationBell consumers, layer4 top-bar note); audit/-exemption rationale documented.
- **Surgery repairs:** Position block rewritten with verified claims (TWO unbuilt Phase-2 items —
  WU-Home + the WU-AccountEnforcement residual; tracker open items span groups A–H incl.
  high-priority E2/E3); WU39 (DONE 07-25) moved back from the archive it had ridden into;
  four cross-boundary "above/below/end of this file" pointers repointed at `workplan-archive.md`;
  the WU-AccessGate/-2 DONE entries moved out of the "Planned / not-yet-built" section;
  folder_clusters' 8 misplaced "Owned surfaces" clauses moved Structure→Style, 7 boilerplate
  cells' wrong noun fixed, `LookupConfigurations.cs`/`SiteConstants` location/`ImportModePicker`
  false claims corrected, Notifications/Messaging/BlogPosts structure cells given real component
  facts; tracker's three stale `workplan.md:~NNNN` citations rewritten.
- **Preamble/Post-MVP retense:** the Stage-4/Stage-3 paragraphs are now explicitly historical
  (grid: zero 4s, five 3s — the L4 visual-pass rows); the Post-MVP section retitled historical
  with every bullet carrying its closure (L5 flip, L6 batch + the genuinely-open Stage-2 rows
  6/7/33/35/38, L8 marts, workers 57/58).
- **New:** `middle_plan_v2.md` **decision row 13** (`/discover` URL state round-tripping — B11's
  blocking question promoted into the decision ledger; B11 backlinks it); a real **WU-Home**
  Planned entry (settled inputs from `audit/Spotlight.md` + `audit/BlogPosts.md`, row-2 gate);
  CLAUDE.md cold-session read order + corrected root-artifacts sentence (four `*_Deliberations.md`
  + `modernization-audit/`).
- **Tracker closures (all three ledgers swept per the closure rule):** G1 (content-safety login
  enforcement retensed to shipped-WU38a + residual), G2 (Lookups Stage-4 — closed by
  WU-DocHygiene's rewrite), G3 (deferred-workers bullet).
- **Point fixes:** ImageStorage header/consumers/L5-rationale (F20–22 L2 Stage 5; post-flip
  structural-exclusion reasoning), Export trigger-surface + Import Shared-Context as-built names,
  Groups.md dead Global-Conditions pointer, USI locked-mapping column header
  (`UserStoryInteractionTypeEnum`), Profiles ComplexProperty/ToJson wording, layer3-logic ×4
  (`Rating` enum, `ChapterReadingPage`, `UserStoryInteractionConstants` ×2, first-party typeahead
  sentence), logging.md telemetry roster (+`UserActivity`, `Email` built — code is the roster of
  record), layer1 JSON hedge answered from `IdentityConfigurations.cs`, horizontal-scaling tense,
  error-handling deferral rationale retensed + MA-008 partial coverage noted, SharedUI.csproj
  dead `UpToDateCheckInput` block for deleted fork pages removed.
- **Verified:** `check-doc-hygiene.ps1` clean (29 live docs incl. design/), `check-design-tokens.ps1`
  clean, `dotnet build` 0 errors, `dotnet test` full suite green (see commit).
- **Tool:** Fable 5 in Claude Code (three parallel Explore agents for the analysis, direct
  implementation for the fixes).

## WU-DocAuditSkill — doc-audit skill + filename-existence gate (no code cells) — DONE ✓ (2026-07-27)

- **Trigger:** the post-DocHygiene3 analysis's remaining "keep it this way" items, Brian-approved:
  institutionalize the fresh-eyes audit, mechanize the dead-filename class, and two rule one-liners.
- **Built:**
  1. **`.claude/skills/doc-audit/SKILL.md`** — the fresh-eyes audit method as an invocable skill:
     three probe shapes (cold-session orientation walk, restructure integrity check,
     untouched-tail staleness probe), ground rules (fresh subagent eyes, confirmed-only findings
     with both sides cited, derived-state blocks verified claim-by-claim), standing exemptions
     (surface-registry until its rewrite; dated ledger entries), and the after-audit fold-in
     steps. CLAUDE.md table row added. Rationale: all ~45 non-term defects found today required
     reading, not lint — this makes that capability reusable instead of conversation-local.
  2. **Gate check #4 (`check-doc-hygiene.ps1`):** every backticked `Name.ext` file reference in a
     live doc must exist in the repo (basename match against a recursive index; case-sensitive
     extensions so `System.Text.Json`-style namespaces skip; placeholder/framework allowlist —
     `Foo*`, `Component.razor.*`, `dotnet.runtime.js`; same historical-marker escape). **First
     run caught five real defects:** `InteractionVisuals.cs` → `UserStoryInteractionVisuals.cs`
     (the WU23 rename family again), `RecommendationVisuals.cs` → `RecommendationIcons.cs`,
     `SpriteEndpoints.cs` example → `ThemeEndpoints.cs`, the L6 matrix citing the migration file
     WU-MigrationCollapse squashed away, and grid_axes' Layer-4 section still saying "Blocked on
     design tokens (`tailwind.config.js`)" — tokens locked 2026-07-10, v4 CSS-first. All fixed.
  3. **Rule one-liners:** archive sweep trigger (workplan.md > ~1,500 lines → move DONE entries
     older than ~2 weeks; header note + CLAUDE.md row) and the Position block's
     verify-at-write-never-carry rule (its only defects ever came from carried claims).
  4. **Cosmetic:** the two unescaped in-cell pipes in `folder_clusters.md` (`static\|animated`
     path, `Visible \| GatedMature \| NotFound` union) escaped so the rows render as 6 columns.
- **Verified:** `check-doc-hygiene.ps1` clean (30 live docs, 4 checks); `check-design-tokens.ps1`
  clean; `dotnet test` full suite green (see commit). Docs/tooling only; no cell Stage changed.
- **Tool:** Fable 5 in Claude Code, direct implementation.

## WU-DocRoadmap — retire the `forward_plan → middle_plan → middle_plan_v2` chain in favor of `.claude/roadmap.md` (no code cells) — DONE ✓ (2026-07-27)

- **Trigger:** Brian, in chat — the plan-doc chain's naming (a stage-of-project metaphor combined
  with a version suffix, `middle_plan_v2.md`) had no coherent next name, and he asked for a
  separate, stably-named `roadmap.md` carrying forward anything still outstanding, with the old
  chain retired.
- **Design (settled before writing anything, given the ~150-reference blast radius a naive
  find-replace would touch):** don't duplicate the ~150-entry historical Resolved log — mirror the
  `workplan.md`/`workplan-archive.md` split already proven in this repo. `roadmap.md` carries only
  still-live content (Phase status, condensed to a one-line DONE summary for Phases 0/1/4/5;
  full detail for the still-open Phase 2 tail/3/6/7; the "Decisions that need you" table; a fresh,
  currently-empty Resolved section starting today). `middle_plan_v2.md` keeps its full Phase
  history and Resolved index verbatim and gets a retirement banner (same treatment it gave
  `middle_plan.md`/`forward_plan.md`) — every existing `§Resolved "…"` / historical `Phase N item M`
  citation across the corpus (audit files, `workplan-archive.md`, skill files) stays valid
  unedited, since that content never moved. Only citations describing *current* gating state
  (an unresolved decision row, an unbuilt Phase item) were repointed to `roadmap.md` — confirmed
  file-by-file via a full-repo grep before editing, not guessed.
- **Repointed** (live-gating citations only, ~30 files): `CLAUDE.md` (Project Files table,
  cold-session read order, Doc-Touch Timing moment 1, "Retiring or closing"), `status.md`
  (Orientation bullet), `workplan.md` (intro blockquote, Position block, Planned-section pointers
  for WU-Home/WU-AccountEnforcement), `hidden-deferrals-tracker.md` (A1/B1/B11/E1/E2/E3/F1–F6),
  `grid_axes.md`, `audit/Accessibility.md`, `audit/BlogPosts.md` ×2, `audit/Seo.md`,
  `audit/ImageStorage.md`, `security.md`, `content-safety.md`, `layer4-style.md`,
  `skills/doc-audit/SKILL.md` ×2, `middle-addendum.md` ×3 (routing-table cells only, per its
  annotate-only update rule), `audit-summary.md` (banner amendment), `.github/workflows/ci.yml` ×2,
  `TheCanalaveLibrary.Server.csproj`, `EmailOptions.cs`, `SmtpEmailSender.cs`.
- **Deliberately left untouched:** every `§Resolved "…"` citation (the bulk of the ~150 hits, in
  `workplan-archive.md` and the audit/skill files) — that content still lives in `middle_plan_v2.md`.
  `middle_plan.md`/`forward_plan.md`/`next_steps.md` themselves — already correctly self-describe
  their own retirement one hop forward; not rewritten to point past their immediate successor,
  matching the chain's existing convention. `modernization-audit/*.md` — frozen per its own
  CLAUDE.md row. Code-comment citations of `forward_plan.md` (`Program.cs`,
  `ServerChapterWriteService.cs`, `app.css`, `ModUsersPage.razor`) — historical Resolved-decision
  pointers, still valid.
- **check-doc-hygiene.ps1 updated:** `$liveDocs` swaps `middle_plan_v2.md` → `roadmap.md`; Check 3's
  `$retiredPlanPointer` regex gained `middle_plan_v2\.md` (so a future stray "middle_plan_v2.md is
  live" claim gets caught, the same mechanism already covering `forward_plan.md`/`middle_plan.md`);
  the doc-comment and the self-exemption comment updated to name `roadmap.md`.
- **Verified:** `scripts/check-doc-hygiene.ps1` clean. Docs/tooling only — no behavior surface, no
  cell Stage changed.
- **Tool:** Sonnet 5 in Claude Code, direct implementation.
- **Same-day addendum (2026-07-27): "Recommended next work units" section.** Brian asked for a
  git-log-informed sequencing recommendation written into `roadmap.md` itself, not left in chat.
  Trajectory read from `git log --format=%ad %h %s -100`: build-in-bursts-then-harden-in-a-burst
  (Phase 1's nine items in three days, most of Phases 2/4/5 in a week, WU-ResponsiveMerge's
  20-commit day, then four straight days — 07-24→07-27 — of zero new features, entirely
  hidden-deferral closures + doc hygiene). `roadmap.md`'s "Where things stand" now carries that
  breakdown; a new "Recommended next work units" section replaces the old "Also still open" stub
  with a 6-step sequence (decisions first → already-unblocked wins → clustered debt-paydown
  WUs — WU-L6MeasurePass/WU-DiscoveryURLState/WU-StatBadgeProducers — → WU-Home → Phase 3 → beta
  work), explicitly framed as a recommendation, not a mandate. Three stale
  `§"Also still open"` cross-references (`hidden-deferrals-tracker.md` E1, `error-handling.md`)
  updated to the new section title. `check-doc-hygiene.ps1` re-verified clean.
- **Second same-day addendum (2026-07-27): full-backlog reanalysis.** Brian asked for a reanalysis
  of the tiered table from the original chat-only ordering (not just the condensed 6-step version
  above) to be written into `roadmap.md`. Re-verified against the tracker's current state (no
  checkbox changes since 2026-07-24) and re-derived the tiering from scratch rather than
  copy-pasting; the re-derivation matched the original 1:1 (all 42 open tracker items accounted
  for exactly once, confirmed by tally). The condensed prose steps were replaced with a full
  Tier 0–6 table covering every open tracker item (not just the phase-gated ones), plus an
  explicit "deliberately not reordered" list and a standalone flag on E2 (AngleSharp CVE — `high`
  priority but risk-accepted, worth an explicit re-confirmation rather than a silent default).
  `check-doc-hygiene.ps1` re-verified clean.

## WU-Home + WU-SiteNews — the front door + staff site announcements (closes Phase 2) — DONE ✓ (2026-07-28)

- **Cells:** neither carries a dedicated grid row — WU-Home is persistent chrome composing
  existing features (Spotlight is Feature 55, shipped separately); WU-SiteNews extends Features
  35/36 (BlogPosts cluster), same "extends, no new cell" treatment as WU-EditorSprite/
  WU-EditorMobile.
- **Decision row 2 resolved in chat first** (Doc-Touch moment 1, before code): the home page is
  the community page — a focused surface, not a broad discovery one. Full settled design:
  `roadmap.md` §Resolved. Closed tracker item F1.
- **WU-Home shipped:** `SharedUI/Home/HomePage.razor` rebuilt — Welcome/mission-blurb `<details>`
  expander (open while Spotlight is empty, collapsed once live — driven by
  `CommunitySpotlightDisplay`'s new `OnLoaded(bool)` callback, no new `site_settings` key) →
  `<CommunitySpotlightDisplay>` unchanged → the active SitePoll inline via
  `IPollReadService.GetSitePollsAsync(includeArchived:false)` + client-side
  `FirstOrDefault(Status == Open)` (no new service method), rendered via the existing `<PollView
  CanManage="false">`, nothing when none is open → a community-discourse link cluster (Polls,
  Fanon, Spotlight-explained, Site News — surfaces deliberately off the persistent top nav; Groups
  excluded, already a nav link) → root `<SocialMetaTags>` (Feature 64 — every other shareable page
  had one, Home didn't).
- **No story discovery on the front door, in any form** — Recently Updated and a random draw were
  both considered and rejected in the design conversation (spec §5.3.3's "no sort by last updated"
  reasoning; "focused purpose, not broad" framing). Consequently removed as dead code:
  `IStoryReadService.GetRecentListingsAsync` (interface + `ServerStoryReadService`/
  `ClientStoryReadService` impls) and `GET /api/stories/recent` (`StoryEndpoints.cs`) — its sole
  stated purpose ("kept for home-page hot-path") no longer exists. `Tests.Integration/
  RecentListingsTests.cs` deleted (its only subject); `DevDiagnosticsEndpoints.cs`'s
  `/wu12/listings/recent` probe and two host-concurrency/tracing smoke tests
  (`ConcurrentReadAccessTests`, `NpgsqlTracingSmokeTests`) repointed to `GetListingsAsync` rather
  than deleted (they only needed *some* story query, not this one). Three now-orphaned fake
  `GetRecentListingsAsync` overrides removed from `Tests.RazorComponents`. Registered in
  `check-doc-hygiene.ps1`'s retired-term registry; `layer2-services.md`/`layer6-indexes.md`
  (live) and `roadmap.md`/`L6-reconciliation-matrix.md` (dated) updated with historical markers —
  `ix_stories_last_updated_date` itself is **kept**, still driving the Relevance sort tie-break.
- **No personalized/signed-in strip** — Continue Reading/follows/bookshelves are already one click
  away via `NotificationBell`/`UserMenu`; `HomePage` stays render-mode uniform for every viewer
  (only the blurb's *initial* expand state varies, and that's driven by data, not by viewer).
- **WU-SiteNews shipped (the gap the resolution surfaced — no site-announcement channel existed,
  and `NotificationTypeEnum.SiteAnnouncement` had sat seeded/tested but unproduced since the
  notification system was built):** `SiteBlogPost : BaseBlogPost` (`Core/BlogPosts/`), the exact
  structural mirror of the already-shipped `SitePoll : BasePoll` split — site-owned, no `StoryId`,
  no `HasSpoilers`, `Rating` always `E` (not exposed to the editor), plus
  `NotifyAllUsers`/`NotifiedAtUtc` for a per-post, fire-once notification fan-out. Migration
  `WU_SiteNews_SiteBlogPost` (new `site_blog_posts` table only — applied + `psql`-verified against
  local dev Postgres). `IBlogPostReadService` gained `GetSiteAnnouncementsAsync`/
  `GetSiteAnnouncementForEditAsync` + a third `GetByIdAsync` branch (needed for `/blog/{id}/{*Slug}`
  — `BlogPostPage` — to serve the new type unmodified); `IBlogPostWriteService` gained
  `Create/Update/DeleteSiteBlogPostAsync`, gated `IsModerator || IsAdmin` throughout (any
  moderator/admin manages any site post, not just its creator — the `SitePoll` precedent, verified
  against `ServerPollWriteService.LoadAuthorizedPollWithOptionsAsync`'s exact rule). New pages:
  `/news` (public list), `/news/new` + `/news/{id}/edit` (`[Authorize(Roles="Moderator,Admin")]`,
  Pattern-1 shape mirroring `BlogPostEditorPage`) via a sibling `SiteAnnouncementPropertiesForm`
  (no Rating/HasSpoilers/story-picker; adds the `NotifyAllUsers` checkbox). New endpoints under
  `/api/blog-posts/site*`.
- **Parent-visibility invariant enrolment (identity-and-authorization.md):**
  `BlogPostVisibilityGuard.LoadFactsAsync` — the mechanism comments/likes on any blog post go
  through — got a third `SiteBlogPost` branch. Missing this would have silently made comments on
  a site announcement permanently invisible rather than erroring, since `IsBlogPostVisibleAsync`
  returns `false` on an absent facts row.
- **NotifyAllUsers fan-out:** new `INotificationWriteService.NotifyNewSiteAnnouncementAsync`
  (`ServerNotificationWriteService`; `ClientNotificationWriteService` throws `NotSupportedException`
  same as every other server-internal `Notify*` method) selects all `Users` and reuses the private
  `CreateCoreAsync` drop-self/dedup path — the `NotifyNewGroupBlogPostAsync` shape with
  `GroupMembers` swapped for the full `Users` table. Fires once, on the false→true publish
  transition (or immediately on create if already published) and only when `NotifyAllUsers` is
  true; `SiteBlogPost.NotifiedAtUtc` is stamped after a successful fan-out so a later edit never
  re-fires it — verified by `Update_AfterAlreadyNotified_DoesNotReNotify`.
- **Discovered, deliberately not fixed (out of scope):** `UpdateBlogPostAsync`/
  `DeleteBlogPostAsync`/`GetForEditAsync` are hardcoded to the `ProfileBlogPosts` table despite
  their generic-sounding names — editing/deleting a `GroupBlogPost` through them silently no-ops
  the child-table update (a pre-existing latent gap, found while confirming these methods were
  safe to extend, not introduced by this WU). WU-SiteNews avoided the landmine entirely by adding
  dedicated `*SiteBlogPostAsync` methods rather than branching the existing ones. Worth its own
  tracker item if `GroupBlogPost` editing is ever exercised for real.
- **BlogPostPage (`/blog/{id}/{*Slug}`) view compatibility — no changes needed** beyond the
  `GetByIdAsync` branch above: it already renders any `BaseBlogPost` subtype generically. The
  Edit-link affordance was deliberately *not* extended to moderators there (it's `isAuthor`-gated,
  linking to `/blog/{id}/edit` which is profile-post-only) — editing a site announcement happens
  from `/news`'s per-card Edit links instead, avoiding a second edit-route-selection branch on an
  already-complex page.
- **Verified:** `dotnet build` (whole solution) green. `dotnet test` green across all three tiers:
  **Unit** 753/753 (no new cases — no host-free pure logic introduced beyond DTO mapping).
  **Integration** 948/948, including the new `SiteAnnouncementServiceTests.cs` (24 tests:
  moderator/plain-user/anonymous authorization on create/update/delete; any-moderator-manages-
  any-post; sanitize-on-save; create-already-published; the fan-out's recipient set and
  fire-once guard on both create-published and draft→publish paths;
  `GetSiteAnnouncementsAsync`'s published-only default, `includeUnpublished`, and newest-first
  ordering; the `GetByIdAsync` third branch's anonymous-visible/draft-hidden behavior; the
  `BlogPostVisibilityGuard` third branch directly). **RazorComponents** 601/601, including
  `CommunitySpotlightDisplay`'s new `OnLoaded` callback (3 tests) and
  `SiteAnnouncementPropertiesFormTests.cs` (7 tests, mirroring `BlogPostPropertiesFormTests.cs`
  minus the removed fields). `scripts/check-doc-hygiene.ps1` clean throughout.
- **Post-implementation review (2026-07-28, diff re-read):** two authz gaps found + fixed
  same-session — a forged `includeUnpublished=true` on the public `/api/blog-posts/site` route
  leaked draft titles/snippets to anyone (now service-demoted unless moderator/admin, the
  `GetByAuthorAsync` shape), and `/api/blog-posts/site/{id}/edit` leaked draft *full content* to
  any signed-in user (read service now gates `IsModerator || IsAdmin`, the `GetForEditAsync`
  shape). +4 Integration regression tests → 952/952. Plus a blurb copy fix ("above"→"below") and
  a false `NewsPage` comment rewritten. Both gaps violated the very 2026-07-18 endpoint-authz
  precedents this WU cited — caught only by re-reading the diff (the WU-TagFanon review lesson,
  again). Full detail + known-behavior notes: `audit/BlogPosts.md` §"Post-implementation review".
- **L4.5-Browser pass (2026-07-28, server-only path, standing dev DB kept):** `/` confirmed —
  blurb expander open (empty Spotlight), no story-discovery section anywhere, Community link row
  (Polls/Fanon/Community Spotlight/Site News) all present and correctly styled;
  `curl`'d the prerendered HTML and confirmed the full OG/Twitter tag set (`og:title`="The
  Canalave Library", correctly truncated `og:description`, `og:image` falling back to
  `/img/default-cover.svg`). `/polls`/`/fanon` reachable and rendering (previously `/polls` had
  no inbound link anywhere in the app). Full WU-SiteNews browser narrative (create → publish →
  fan-out at the dev DB's actual 2,007-user scale → cleanup, plus a transient reconnect-banner
  non-issue investigated and ruled out): `audit/BlogPosts.md`'s L4.5-Browser note.
- **Tool:** Opus 5 in Claude Code (plan mode → build), 2026-07-28.
