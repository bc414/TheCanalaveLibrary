# Workplan — Ordered Work-Units (atoms-first)

Produced by Phase D (`forward_plan.md`). This is the build sequence for Phase E. Each work-unit names
its **cell(s)** (Feature # + layer, per `status.md`), its **tool** (per CLAUDE.md Per-Stage Guidance),
its **audit pointer** (`.claude/audit/<Folder>.md`, section), and its **deps** (work-units that must be
at Stage 5 first). CLAUDE.md is the source of truth for stage semantics and file paths — this file
references it, does not restate it.

---

## Read this first (ordering preamble)

**Scope of the numbered sequence = Layers 1–4 (the MVP).** `grid_axes.md` §"The Two Boundaries" is
authoritative: Layers 1–4 are the InteractiveServer MVP (data → service → logic → structure → style);
Layers 5–8 are *additive and batchable* — they swap method bodies / add DDL / add standalone workers
behind contracts frozen in 1–4, and never force a 1–4 change. The resolved "Aspire orchestration during
MVP dev" decision (`forward_plan.md`) confirms this: MVP is InteractiveServer-only, no Redis/WASM,
Layers 5–8 post-MVP. So **every numbered work-unit below builds L2/L3-Logic/L3.5-Structure/L4-Style**
(L1 is done — see WU0). **L5–L8 are gathered into the "Post-MVP" section** at the end, unsequenced and
batched by pattern, not dropped. If this scoping is wrong, stop here — it reshapes everything below.

**Topological, bottom-up, three-phase (spec §9.2).** A cell's dependencies appear *earlier in this file*,
so they're at Stage 5 when reached. Phases:
- **Phase 1 — Atoms.** Leaves + foundational services consumed by many, depending on nothing
  feature-specific. Building these *mints contracts*: once a leaf's parameter/event contract is locked,
  its consumers flip from Stage 2 to Stage 3.
- **Phase 2 — Integration points.** Composites that consume atoms and produce surfaces pages embed
  (`StoryCard`/`StoryDeck`, `UserStoryInteractionPanel`, `ChapterNavigation`, `CommentSection`, …).
- **Phase 3 — Consumers / pages.** Dispatchers and feature pages aggregating Phase-2 output. Internal
  order is loose; deps still hold.

**Stage-4 cells use the resolved direction (audit-summary §0/§3).** Almost all are *stale-code traps*:
build to spec, treat existing code as discard-not-reuse. The flag means "don't copy this code," not "open
question." The two *genuine mechanical* reconciliations (Identity post-move refs; Story L5 endpoint wiring)
are called out where they sit (Identity = WU1; Story L5 = Post-MVP).

**Stage-3 is minted here, not found.** Expect ~0 Stage-3 cells now. opusplan passes on atoms create them;
mark the consumer flip (2→3) when an atom's contract lands, and switch that consumer's tool to Sonnet.

**Tool per work-unit.** opusplan for Stage-2 builds and atom-contract minting; **Opus (reconcile)** for the
residual genuine Stage-4 (WU1); **Sonnet in Claude Code** for cells already Stage-3 (none yet — they appear
as atoms land). L4-Style is never sequenced alone — it rides inside the same work-unit as that feature's
L3/L3.5 build (per Phase D rule; tokens are locked, `layer4-style.md` is the validated spec). "Build +
verify" for any unit touching L4 means render-and-look, not just `dotnet build` — see `forward_plan.md`
Phase E.

**Per-unit loop (Phase E).** pick next → read its audit pointer → feed audit "settled" notes to the tool as
"do not revisit" → build → `dotnet build` + `dotnet test` (should be green; add asserted tests for any new
testable surface per `canalave-conventions/testing.md`'s tier rules) + run the slice (+ visual check if L4)
→ update `status.md` (cell → 5) and this file (unit ✓). Record the covering test tier (Unit / Integration /
RazorComponents) — or why none applies — in the audit Stage note. Conventions skill auto-loads as guardrail.

---

## WU0 — Foundation (Phase A) — DONE ✓

- **Cells:** all L1 (re-modeled per audit-summary §3b) + `InitialSchema` migration + green build.
- **State:** code/schema-complete — `InitialSchema` generated, `dotnet build` green, template debris
  cleared, Identity namespaces partially normalized, three first-run runtime bugs fixed: Stories L2
  DI-registration (detail: `audit/Stories.md` row 4/5), render-mode/interactive-routing (detail:
  `cross-cutting.md` "Render Mode"), `ReadOnlyApplicationDbContext` ctor mismatch. Also pivoted dev
  workflow off Aspire for MVP (detail: `forward_plan.md` "Aspire orchestration during MVP dev").
- **VERIFIED (2026-06-20):** migration applied to a live local Postgres (`ConnectionStrings:canalavedb`,
  direct `TheCanalaveLibrary.Server` run — not AppHost), `DataSeeder` runs, app boots, Identity pages
  load end-to-end (`/`, `/Account/Login`, `/Account/Register` all `200`, no exceptions). The real
  "L1 → Stage 5" gate is closed. Start/stop procedure documented in `.claude/skills/run-server/SKILL.md`.
- **Tool:** Opus/Sonnet, Claude Code. **Pointer:** `forward_plan.md` Phase A addendum + `status.md`.

---

## Phase 1 — Atoms (mint the contracts)

### WU1 — Identity reconciliation *(genuine Stage-4, mechanical)* — DONE ✓ (2026-06-20)
- **Cells:** 1 L2/L3/L3.5, 52 L2/L3/L3.5 — all now Stage 5.
- **Done:** namespace/asset-path reconciliation (already complete pre-session); completed
  `UserDeletionService`'s four `Restrict`-edge handlers + DI registration + fixed a
  retrying-execution-strategy/manual-transaction bug found while verifying it; rewired
  `DeletePersonalData.razor` to use it; added minimal §3.19 login/logout triggers
  (`LoginDisplay.razor`) to `DesktopLayout`/`MobileLayout`/Identity `MainLayout`; added
  `DevDiagnosticsEndpoints.cs` as the standing home for Development-only verification endpoints.
  Full nav/bell/avatar deferred to WU22/WU30/WU33.
- **Verified:** `dotnet build` green; live app boot + `/`, `/Account/Login`, `/Account/Register` 200;
  login link renders anonymously; account deletion exercised twice end-to-end against fixture users.
  Detail in `audit/Identity.md` Features 1, 52 Stage-5 notes.
- **Tool:** Opus (reconcile). **Pointer:** `audit/Identity.md` Features 1, 52 (+ audit-summary §3a).
- **Deps:** WU0 runtime gate.

### WU2 — Sprites L2 service *(Stage-4, code works — light rename + cluster move)* — DONE ✓ (2026-06-20)
- **Cells:** 3 L2 — now Stage 5.
- **Done:** renamed `ISpriteService`→`ISpriteReadService`, impl→`ServerSpriteReadService` (now
  primary-constructor DI); moved all three files (interface, server impl, client impl) out of the legacy
  `ServiceInterfaces/`/`Services/` folders into their `Sprites/` cluster folder (Core/Server/Client — see
  `canalave-conventions/SKILL.md` "Code Organization"); updated both `Program.cs` DI registrations.
  **`GetInteractionIcon()` was dropped from scope** (settled WU2 — see `audit/Sprites.md` Feature 3 L2
  and `audit/UserStoryInteractions.md` Feature 16: theme-swappable interaction icons are a
  UserStoryInteraction-domain concept). At WU2 time the intent was to resolve them via the generic
  `GetSpriteUrl` instead; **WU7 superseded that** — interaction icons are inline SVG
  (`IconPath`/`AccentColor`), not a sprite URL at all, so `UserStoryInteractionButton` has no
  relationship to this service, direct or indirect. Contract feeds TagChip and StoryCard. Also
  hardened `canalave-conventions/SKILL.md` "Code Organization" (vertical clusters
  are now a stated rule, legacy folders named, Endpoints colocation settled) and `layer2-services.md`
  Naming, as Doc-Touch moment 1, before the code change.
- **Verified:** `dotnet build` green across all four projects; zero remaining `ISpriteService`/
  `FileSystemSpriteService` references repo-wide; live server run booted clean, DI resolved,
  `/`, `/Account/Login`, `/Account/Register` all `200`. Detail in `audit/Sprites.md` Feature 3 L2
  Stage-5 note.
- **Tool:** Opus (reconcile, mechanical). **Pointer:** `audit/Sprites.md` Feature 3 (+ audit-summary §3b).
- **Deps:** WU0.

### WU3 — Tags L2 read service *(Stage-4 trap → build to spec)* — DONE ✓ (2026-06-20)
- **Cells:** 13 L2, 14 L2 — now Stage 5.
- **Done:** renamed `ITagRetrievalService`→`ITagReadService` (`Core/Tags/`); added
  `ServerTagReadService` (`Server/Tags/`, primary-constructor DI over `ReadOnlyApplicationDbContext`,
  `.Select()` projection); registered `AddScoped<ITagReadService, ServerTagReadService>()` in
  `Server/Program.cs`; updated both existing injectors (`TagSelector.razor`,
  `StoryPropertiesForm.razor`). Server-only — no Client/L5 impl (MVP is InteractiveServer-only;
  deferred to post-MVP L5 batch alongside Sprites/Stories L5).
- **Verified:** `dotnet build` green across all four projects; zero remaining
  `ITagRetrievalService` references repo-wide; live server run booted clean, DI resolved,
  `/`, `/Account/Login`, `/Account/Register` all `200`. Detail in `audit/Tags.md` Features 13, 14
  Stage-5 notes.
- **Tool:** opusplan. **Pointer:** `audit/Tags.md` Features 13, 14 + cluster reconciliation note.
- **Deps:** WU0.

### WU4 — `TagChip` leaf — DONE ✓ (2026-06-21)
- **Cells:** 13 L3/L3.5/L4 — now Stage 5.
- **Done:** minted `TagChipDto` (Core/Tags/ — `TagId`, `TagName`, `TagTypeId`, `Description`,
  `SpriteUrl`); built `TagChip` (`SharedUI/Tags/`) as a pure leaf taking `Tag` (the DTO) +
  `EventCallback OnRemove`, no service injection. Settled (doc-touched into `layer2-services.md`
  before the build, per Doc-Touch moment 1): sprite URLs are resolved **server-side, in the
  producing read service's projection** via `ISpriteReadService.GetSpriteUrl`, mirroring
  `StoryListingDto.CoverArtRelativeUrl` — `TagChipDto.SpriteUrl` is a resolved relative path, not the
  raw `SpriteIdentifier` key, and is request-scoped (never cached cross-user/theme). Also fixed stale
  examples found along the way: `layer4-style.md`'s "Sprite Resolution" (wrong `GetSpriteUrl` arg
  order/path, referenced the dropped `GetInteractionIcon`) and `layer3.5-structure.md`'s canonical
  `TagChip` snippet (now takes `TagChipDto Tag`); added the tag-type color table to `layer4-style.md`
  Pattern Accumulation. No producing read service/consumer exists yet — verified via a throwaway demo
  harness on `HomeDesktop.razor` (removed once WU11/WU13 land).
- **Verified:** `dotnet build` green (4 projects); user-confirmed visual check against the live server
  (all six tag-type colors, sprite render, tooltip, conditional X button, no doubled spacing). Detail
  in `audit/Tags.md` Feature 13 Stage-5 note.
- **Tool:** opusplan. **Pointer:** `audit/Tags.md` Feature 13 + `layer2-services.md`
  §"Sprite URLs Are Resolved Server-Side, At Projection Time". **Deps:** WU2, WU3.

### WU5 — `RichTextView` leaf — DONE ✓ (2026-06-21)
- **Cells:** 7 L3.5/L4 (RichTextView slice only — `ChapterPage`/`ChapterNavigation` remain, WU18/WU26;
  cell numbers in `status.md` unchanged).
- **Done:** built `RichTextView` (`SharedUI/RichText/`, a new cross-cutting cluster like `Lookups/` —
  not filed under Chapters, since it's universal across Chapters/Comments/Recommendations/BlogPosts/
  Profiles/Messaging). Pure leaf, no service injection, no sanitization (trusts stored HTML;
  sanitize-on-save is WU6/L2's job, §3.21). Reader display settings arrive via a cascaded slim
  property bag, `ReaderDisplaySettings` (`SharedUI/RichText/`, deliberately not a `*Dto` — never
  crosses the service boundary), with built-in defaults when no cascade provider is present.
  `ReaderSettings` (Core) is unchanged — settled as a deliberate non-split (separation happens at the
  consumption layer, not storage). No border/background on the leaf (Container Composite/`Card`
  concern, owned by the composing context). Doc-Touch moment 1 (before the build): `SKILL.md` Code
  Organization (new `RichText/` cluster rule), `layer3.5-structure.md` ("Ambient Viewer Settings via
  Cascading Slim Bags" pattern), `layer4-style.md` ("Reader Settings as CSS" rewritten for the
  cascaded bag + Pattern Accumulation entry), `layer2-services.md` (sanitize-once-on-save trust
  boundary). The layout-level cascade *provider* (reading the real viewer's `User.ReaderSettings`) is
  deferred to its first real consumer (WU26/WU30), not wired here.
- **Verified:** `dotnet build` green (4 projects); live server run, homepage `200`; throwaway harness
  on `HomeDesktop.razor` confirmed both a non-default cascaded `ReaderDisplaySettings` and the
  no-cascade default path render correct inline styles + unescaped HTML; harness removed after
  confirmation. Detail in `audit/Chapters.md` Feature 7 WU5 Stage note.
- **Tool:** opusplan. **Pointer:** `audit/Chapters.md` Feature 7. **Deps:** WU0.

### WU6 — `EditorView` composite *(third-party Quill wrapper)* — DONE ✓ (2026-06-21)
- **Cells:** 6 L3-Logic, L3.5-Structure, L4-Style — now Stage 5 (6 L2 stays Stage 2 — sanitizer minted,
  no call site yet).
- **Done:** `EditorView` (`SharedUI/RichText/EditorView.razor`) wrapping Blazored TextEditor 1.1.3
  (Quill.js); minted `IHtmlSanitizationService`/`ServerHtmlSanitizationService` (`HtmlSanitizer`
  9.0.892, allow-list = exactly the toolbar's output set, `AddSingleton`). Two settled deviations from
  the original sketch: **no `Compact` runtime toggle** — Quill binds toolbar listeners once at
  construction, so a later `ToolbarContent` change doesn't rewire them; the device axis is deferred to
  a future separate desktop/mobile composition instead (MVP ships desktop toolbar only, not
  MVP-blocking), matching how the rest of the codebase handles desktop/mobile. **Preview is a popup
  overlay, not an in-place swap** — swapping reflowed the page every toggle; Quill now stays mounted
  continuously and `RichTextView` renders on top of a dimmed backdrop. Inline Pokémon-sprite Quill
  blot (spec §5.30.2) stays out of scope, its own future work-unit. Doc-Touch (moment 1, before the
  build): `layer2-services.md` "The allow-list is the inverse of the toolbar"; `layer3.5-structure.md`
  "Third-Party Wrapper Composite" (corrected the snippet's nonexistent `@bind-Value` to the real
  Blazored API and the popup pattern); `layer4-style.md` (fixed a stale `RichTextEditor`→`EditorView`
  naming mismatch); `cross-cutting.md` (flagged mobile toolbar deferred).
- **Verified:** `dotnet build` green (4 projects, 0 warnings); live server run, homepage `200`; NuGet
  restore + build confirmed both `Blazored.TextEditor` and `HtmlSanitizer` work on net10.0; throwaway
  harness + a throwaway dev-diagnostics endpoint confirmed the sanitizer strips
  `<script>`/event-handlers/`javascript:` hrefs while preserving allowed formatting; user-confirmed
  visual check against the live server (toolbar functional, preview popup opens/closes without page
  reflow, content captured correctly). Harness and diagnostic endpoint removed after confirmation.
  Detail in `audit/Chapters.md` Feature 6 WU6 Stage note.
- **Tool:** opusplan. **Pointer:** `audit/Chapters.md` Feature 6. **Deps:** WU0.

### WU7 — `UserStoryInteractionButton` leaf — DONE ✓ (2026-06-21)
- **Cells:** 16 L3/L3.5/L4 — now Stage 5 (button slice only; panel slice + debounce remain Stage 2,
  owned by WU16).
- **Done:** EventCallback-driven (absence of `OnToggle` ⇒ read-only, rendered only when `IsActive`);
  built as a square 3-state button (gray inactive → accent-fill-on-hover → inverted accent-bg/white-
  shape when active). Icon comes in as **inline SVG**, not a resolved sprite URL — `IconPath` (SVG
  `d` string) + `AccentColor` `[Parameter]`s, plus `Label` for `aria-label`/`title` (a11y gap the
  spec's 3-param contract didn't cover). The button itself injects no service and has no
  `InteractionTypeEnum` knowledge. Settled (WU7, Doc-Touch moment 1 before the build, supersedes the
  WU2-era sprite-key plan): interaction icons are inline SVG, permanently — carved out of the "never
  inline SVG" rule, which still governs tags/covers/avatars. The owning `UserStoryInteractionPanel`
  (WU16) maps `InteractionTypeEnum` → `(IconPath, AccentColor)` — that mapping table is the one
  remaining open item, left for WU16. `Sprites.ISpriteReadService.GetSpriteUrl` is not involved.
  Updated `layer3-logic.md`, `layer3.5-structure.md`, `layer4-style.md` (new "Interaction Icons Are
  Inline SVG" section + Pattern Accumulation entry), `audit/UserStoryInteractions.md`, `audit/Sprites.md`.
- **Verified:** `dotnet build` green (4 projects); user-confirmed visual check against the live
  server (all 3 states, hover fill, read-only visibility-when-active-only) via a throwaway harness
  on `HomeDesktop.razor` (removed after confirmation). Detail in `audit/UserStoryInteractions.md`
  Feature 16 Stage notes.
- **Tool:** opusplan. **Pointer:** `audit/UserStoryInteractions.md` Feature 16. **Deps:** none (the
  original WU2 dependency assumed sprite-based icons; inline SVG removed that coupling).

### WU8 — `PaginationControls` — DONE ✓ (2026-06-21; markup-level regression test added 2026-06-22). Note: visual box for active page (CSS custom property rendering) not verified in bUnit — requires human sign-off against live app for Stage 6.
- **Cells:** 31 L3.5/L4 (pagination slice) — built; cell numbers in `status.md` unchanged (slice
  only, rest of Feature 31 remains Stage 2/1 — see audit note).
- **Done:** built as a leaf (`SharedUI/Pagination/PaginationControls.razor`) per spec §3.11.1 —
  the `audit-summary.md` "Composite" tag is stale, superseded. Greenfield contract:
  `CurrentPage`/`PageSize`/`TotalCount` (primitives only) + `EventCallback<int> OnPageChanged`;
  stateless offset pagination (§5.3.4) — raises the requested page, never queries. Fixed 7-slot
  sliding window (first/last always shown, ellipsis fills gaps at `TotalPages > 7`; all pages shown,
  centered in the same reserved width, at `TotalPages <= 7`) so the control's total width never
  shifts between listings. Not used in random-discovery mode ("give me more" + interaction buttons
  remain the mechanism there). Two rounds of user-driven visual refinement after the initial build:
  summary text moved below the button row, buttons made into bordered solid blocks with hover
  shading (the active-page "doesn't look active" note traced to the demo not updating on click, not
  a styling gap), and the fixed-width windowing added to stop the footprint shifting page-to-page.
- **Verified:** `dotnet build` green (4 projects); user-confirmed visual check against the live
  server via a throwaway harness on `HomeDesktop.razor` (12-page sliding window, 3-page no-ellipsis/
  centered, single-page renders nothing, active highlight follows clicks) — harness removed after
  confirmation. Detail in `audit/Discovery.md` Feature 31 WU8 Stage note and `layer4-style.md`
  Pattern Accumulation.
- **Tool:** opusplan. **Pointer:** `audit/Discovery.md`. **Deps:** WU0.

### WU9 — `ConfirmDialog` *(universal container, §5.30.9)* — DONE ✓ (2026-06-21)
- **Cells:** 26 L3.5 — now Stage 5 (26 L4 stays Stage 1 — spoiler blur/cover styling, owned by WU20).
- **Done:** built `ConfirmDialog` (new `SharedUI/Dialogs/` cross-cutting cluster — no owning feature,
  mirrors `RichText/`/`Lookups/`). Contract settled (confirmed with user before build): `@bind-IsOpen`
  (two-way `IsOpen`/`IsOpenChanged`) rather than an imperative `@ref`-driven `ShowAsync()`, matching the
  `_showConfirmDialog` bool in the spec's spoiler example (`layer3-logic.md` "Spoiler Comment State").
  `Title`/`Message` for simple bodies, `ChildContent` for rich bodies (wins over `Message`),
  `ConfirmText`/`CancelText`, `IsDestructive` (red `bg-danger` confirm button vs. green `bg-primary`),
  `OnConfirm`/`OnCancel` EventCallbacks. Renders nothing when `!IsOpen`; backdrop click cancels, panel
  uses `@onclick:stopPropagation`. Overlay shell reuses the convention `EditorView`'s preview popup
  already established (backdrop `bg-black/50` + `rounded-xl bg-surface shadow-lg` panel) — not
  refactored into a further shared `Modal` primitive (only two consumers, two different flows; deferred
  until a third clarifies the shared part). Doc-Touch moment 1 (before the build):
  `canalave-conventions/SKILL.md` Code Organization (new `Dialogs/` cluster rule);
  `layer3.5-structure.md` (second Container Composite worked example + updated `EditorView` cross-ref);
  `layer4-style.md` Pattern Accumulation (modal shell convention recorded once).
- **Verified:** `dotnet build` green (4 projects, 0 new warnings); `npm run css:build` picked up
  `bg-danger` (theme token pre-existed, just unused until now); live server run, homepage `200`;
  user-confirmed visual check via a throwaway harness on `HomeDesktop.razor` (message-only dialog,
  `ChildContent` dialog, `IsDestructive` variant, backdrop-click + Confirm/Cancel all round-tripping
  `@bind-IsOpen`) — harness removed immediately after confirmation (self-contained, unlike WU4's
  TagChip harness which stood in for an unbuilt producer). Detail in `audit/Comments.md` Feature 26
  WU9 Stage-5 note.
- **Tool:** opusplan. **Pointer:** `audit/Comments.md` Feature 26. **Deps:** WU0.

### WU10 — `UserCard` leaf — DONE ✓ (2026-06-21)
- **Cells:** 18 L3.5/L4 — now Stage 5.
- **Done:** minted `UserCardDto`/`UserCardBadgeDto` (`Core/Users/`) and built `UserCard`
  (`SharedUI/Users/`) as a pure leaf, in a new cross-cutting `Users/` cluster (same `RichText/`-shaped
  exception as `Dialogs/` — no single feature owns the atom; doc-touched into `SKILL.md` "Code
  Organization" before the build). Settled and built per spec §5.30.7: View Profile is a plain
  always-on link; the remaining caret actions (Discover from this User, Copy link, Report, Send PM)
  are optional `EventCallback`s gated by `HasDelegate` — Report (WU34)/Send PM (WU35) stay dark until
  those features land. Badge collection minted on the DTO now, rendered conditionally (empty until
  WU36). Avatar is `User.ProfilePictureRelativeUrl` copied verbatim by the producing read service, not
  `ISpriteReadService.GetSpriteUrl` — corrected a stale overgeneralization in `layer4-style.md`
  (Doc-Touch before the build); added a static `wwwroot/img/default-avatar.svg` fallback. Contract
  feeds vouch display (WU21), profiles (WU30), and other listed consumers.
- **Verified:** `dotnet build` green (4 projects, 0 warnings); live server run, homepage `200`;
  user-confirmed visual check against the live server (avatar fallback, linked username, conditional
  tagline/badges, caret open/close, HasDelegate-gated menu items, no doubled spacing) via a throwaway
  harness on `HomeDesktop.razor` (removed after confirmation). Detail in `audit/Following.md`
  Feature 18 Stage-5 note. Consumers (WU21/WU30/…) remain Stage 2 — the DTO contract alone doesn't
  flip them, same as WU4's `TagChip`.
- **Tool:** opusplan. **Pointer:** `audit/Following.md` Feature 18 + `audit/Profiles.md`.
  **Deps:** WU2 (sprite/avatar).

### WU11 — `TagSelector` composite rebuild *(Stage-4 trap → spec §5.30.4)* — DONE ✓ (2026-06-22)
- **Cells:** 14 L3/L3.5/L4 — now Stage 5 (14 L2 was already Stage 5 from WU3, extended additively).
- **Done:** discarded the datalist/list-mutation/inline-badge component entirely; rebuilt around
  single-select **Blazored.Typeahead** 4.7.0 (300ms debounce, `MinimumLength=2`) sourced by a new,
  additive `ITagReadService.SearchTagChipsAsync(type, term)` (capped per-keystroke server search via
  `EF.Functions.ILike`, sprites resolved post-materialization through `ISpriteReadService` — Npgsql
  doesn't translate the `string.Contains(string, StringComparison)` overload, caught at build time).
  Selected chips render as `TagChip` leaves above the input; dropdown rows are lightweight (color dot +
  sprite + name). Settled (Doc-Touch moment 1, before the build): the typeahead sources per-keystroke
  from the server rather than loading a type's full tag set upfront; `OnSelectionChanged` emits
  `IReadOnlyList<TagChipDto>`, not the spec's literal `IReadOnlyList<Tag>` (DTO Firewall forbids the EF
  entity crossing into UI). Fixed the `mb-4` outer-margin violation. **Real bug found and fixed during
  verification, not anticipated in the plan:** `BlazoredTypeahead` requires a `SelectedTemplate`
  parameter — omitting it throws in `OnInitialized()`, which kills the Blazor Server circuit
  immediately (symptom: field permanently unresponsive, page frozen on prerendered markup — looked
  exactly like "can't type into the box," not like a missing-parameter exception). A secondary
  `Dispose()` `NullReferenceException` was a downstream symptom of that same half-init state, not a
  separate prerendering incompatibility — an earlier mid-build doc note misdiagnosed it as one and
  added an unnecessary `RendererInfo.IsInteractive` guard, since removed once the real cause was found.
  Updated `layer2-services.md`, `layer3-logic.md`, `layer3.5-structure.md` (canonical snippet +
  the SelectedTemplate pitfall), `layer4-style.md` (dot-color table + package CSS-skeleton note),
  `audit/Tags.md` Feature 14.
- **Verified:** `dotnet build` green (4 projects, 0 errors); live server run, homepage `200`, clean
  boot/request cycles with no exceptions. User-confirmed visual + interactive check on the live server
  via a throwaway `HomeDesktop.razor` harness (two `TagSelector` instances, Character + Genre, backed
  by 7 throwaway fixture `Tag` rows inserted via `psql`): debounced dropdown rows, chip add/remove,
  already-selected exclusion, no doubled spacing. Harness and fixture rows removed after confirmation.
- **Tool:** opusplan. **Pointer:** `audit/Tags.md` Feature 14 + cluster note. **Deps:** WU3, WU4.

---

## Phase 2 — Integration Points

### WU12 — Stories L2 (listing + write completion) — DONE ✓ (2026-06-22)
- **Cells:** 5 L2 — now Stage 5 (was Stage 2 — minted `StoryListingDto` + listing/browse projection +
  content-rating master filter "mature off ⇒ no trace"). 4 L2 — stays Stage 5 (slug generation built;
  create-path NRE fixed; cover-art *storage* infra built, upload UI still open → WU24).
- **Done:** content-rating filter landed as a global EF named query filter on `Story`
  (`ApplicationDbContext.OnModelCreating`, named `"ContentRating"`), sourced from a new scoped
  `IActiveUserContext` (`Core/Identity/` + `Server/Identity/ServerActiveUserContext.cs` — claims-only,
  `IHttpContextAccessor` primary / `AuthenticationStateProvider` fallback, lazy-resolved property to
  dodge `SecurityStampValidator`'s early-middleware DbContext resolution — see the class's own XML doc
  for the full reasoning). Listing scope landed exactly as settled: `StoryListingDto` +
  `GetListingsByIdsAsync(int[])` (the §6.6 building block) + `GetRecentListingsAsync(page, pageSize)`
  (one unfiltered-by-criteria browse projection); `GetListingsAsync(StoryFilterDto)` stays deferred to
  WU23. **Cover-art storage infra built, not stubbed:** minted `IImageStorageService` +
  `LocalImageStorageService` (wwwroot-backed, host-relative URLs) — see `audit/ImageStorage.md` — so
  `StoryCard` (WU13) has a real cover URL. The write path still treats `CoverArtRelativeUrl` as a
  pass-through string (no upload UI yet — that's WU24); the cloud backend (`S3ImageStorageService`,
  R2/MinIO) is the Post-MVP item below, an additive swap behind the same interface. **Two real bugs
  found and fixed in the write path, not anticipated in the plan:** a NRE in `StoryMappers.ToStory()` (a
  fresh `new Story()` had null `StoryListing`/`StoryDetail` navs, dereferenced before the partitions
  were attached — fixed by initializing both navs on create) and a compounding bug in
  `CreateStoryAsync`'s use of `writeDb.Attach(...)` on those same navs (`Attach` marks the graph
  Unchanged — would have silently skipped inserting the listing/detail rows even after the NRE was
  fixed; removed, `Stories.Add(newStoryDB)` alone correctly cascades as Added). Also removed the Aspire
  Npgsql EF Core *client* package from `TheCanalaveLibrary.Server` — pooled DbContexts are incompatible
  with `IActiveUserContext`'s Scoped lifetime; plain `AddDbContext` is now the standing registration for
  both DbContexts (see `status.md` Global Conditions, `forward_plan.md`).
- **Verified:** `dotnet build` green (4 projects, 0 warnings/errors); live server boot clean. Via
  `/dev/wu12/*` diagnostics (`DevDiagnosticsEndpoints.cs`) against fixture data (`TestUser`, fixture
  tags 10/11, test stories 5/6/7) — content filter confirmed both directions (anonymous + non-mature
  user saw only the Teen-rated fixture story; `TestUser` with `ShowMatureContent=true` saw all 4
  including Mature); `GetListingsByIdsAsync` confirmed reorder-to-input-order + silent-drop of
  filtered ids; `CreateStoryAsync` called twice with the same title produced disambiguated slugs
  (`wu12-mature-story`, `wu12-mature-story-2`), no NRE; `LocalImageStorageService.SaveAsync` round-
  tripped a 1x1 PNG to `/uploads/stories/999/cover-{uuid}.png`, served back `200 image/png`
  byte-identical. **Deviation from plan, deliberate, user-instructed:** the plan's step 4 ("remove the
  diagnostic endpoint + fixtures after confirmation") was *not* done — the user explicitly said to keep
  the `/dev/wu12/*` endpoints standing and keep all fixture data (test stories, fixture tags, the
  uploaded test image) for later analysis. These are not real seeded content; future sessions should
  not mistake story ids 5/6/7/999 or tag ids 10/11 for production seed data.
- **Tool:** opusplan. **Pointer:** `audit/Stories.md` Features 4, 5 + `audit/ImageStorage.md`.
  **Deps:** WU1.

### WU12.5 — Test Foundation (unit + integration test projects) — DONE ✓ (2026-06-22)
- **Cells:** none directly — cross-cutting tooling, not a feature cell. Follows directly from a
  post-WU12 post-mortem: WU12's two create-path bugs were caught only by a human reading
  `/dev/wu12/*` probe output, which asserts nothing.
- **Done:** minted `TheCanalaveLibrary.Tests.Unit` (xUnit, references Core only — no DB/host) and
  `TheCanalaveLibrary.Tests.Integration` (xUnit + `Testcontainers.PostgreSql` +
  `Microsoft.AspNetCore.Mvc.Testing`, references Server), both added to the `.sln`. See
  `canalave-conventions/testing.md` for the convention (two test tiers, why integration tests use a
  real Postgres container rather than EF InMemory/SQLite, the fake-`IActiveUserContext` pattern).
  Extracted the pure `Slugify` transform out of `ServerStoryWriteService` into
  `Core/Stories/StorySlug.cs` (unit-testable, no DbContext) — `GenerateUniqueSlugAsync` still owns the
  DB uniqueness scan. Added `public partial class Program;` to `Server/Program.cs` so
  `WebApplicationFactory<Program>` can reference it from the test assembly. Integration infra:
  `PostgresFixture` (one Testcontainers Postgres per collection, migrated via `Database.MigrateAsync()`
  — not `EnsureCreated()`), `TestAppFactory` (boots the real `Program.cs` host, swaps the real
  `ServerActiveUserContext` registration for a settable `FakeActiveUserContext`, redirects
  `IWebHostEnvironment.WebRootPath` to a per-factory temp folder so image tests never touch the real
  `wwwroot/uploads/`). Migrated the WU12 dev-diagnostics probes into asserted tests: content-rating
  filter both directions + `GetListingsByIdsAsync` reorder/drop (`ContentRatingFilterTests`), the
  create-path NRE/Attach-vs-Add/slug-disambiguation regressions (`StoryWriteServiceTests`),
  `GetRecentListingsAsync` ordering (`RecentListingsTests`), the image round-trip + delete +
  path-traversal guard (`ImageStorageServiceTests`), and a DI-resolution sanity check
  (`HostBootTests`). The `/dev/wu12/*` endpoints and their fixtures stay in place (per the WU12 user
  instruction) but are no longer the source of truth for these behaviors — see testing.md "Dev-
  diagnostics endpoints are probes, not the regression net."
- **Real bug found and fixed while building this, not anticipated in the plan:** the first version of
  `RecentListingsTests` dated its fixture rows "now + 10 years" expecting them to sort to the top of
  `GetRecentListingsAsync`'s unfiltered listing regardless of other accumulated rows in the shared
  Postgres container. That isn't isolation-proof: a leftover row from an *earlier, separate*
  `dotnet test` process invocation against the same container computes its own "+10 years" from an
  earlier wall-clock instant, and the test failed intermittently when re-run because two
  relatively-dated fixtures from different runs can land in either order relative to each other.
  Fixed by never asserting absolute top-N position against shared/accumulating state — instead fetch
  enough rows to be sure this test's own two known ids are present, then assert only their order
  *relative to each other*. Confirmed stable across three consecutive `dotnet test` invocations after
  the fix.
- **Verified:** `dotnet build` on the full `.sln` green (8 projects, 0 warnings/errors). `dotnet test`
  on the full `.sln` green: 25 unit tests (no DB), 15 integration tests (real Testcontainers Postgres,
  ~4s). Sanity check that the tests actually guard the invariant, not vacuously green: temporarily
  changed the `"ContentRating"` query filter in `ApplicationDbContext.OnModelCreating` to ignore
  `ShowMatureContent` (`s => s.Rating <= Rating.M` unconditionally) — 3 of 5 `ContentRatingFilterTests`
  failed as expected; reverted, full suite green again. No Docker containers left running after any
  run (Testcontainers' Ryuk reaper cleans up correctly).
- **2026-06-22 backfill addendum — methodology tightening + test taxonomy overhaul + service + component
  backfill.** Post-evaluation of WU12.5 found two gaps: (1) testing was never woven into the governing
  methodology (CLAUDE.md loops, Doc-Touch moment 3, audit Stage-5 rows all silent about tests); (2) the
  Unit/Integration tier boundary was mis-modeled as a reference-graph proxy ("Unit = Core refs only")
  that barred host-free Server services. Both fixed in one pass:
  - **Methodology docs updated (advisory — no Stage gate):** CLAUDE.md Doc-Touch moment 3 now runs
    `dotnet test` and records the covering tier (Unit/Integration/RazorComponents) or states why none
    applies; Per-Stage "After completing any work-unit" now names `dotnet test`; audit Stage-5 row
    strengthened to require tier name or rationale. `workplan.md` per-unit loop and `forward_plan.md`
    Phase E loop now include `dotnet test` and the tier-recording step (both identical so they don't
    drift). `testing.md` rewritten: three tiers by *kind* (Unit = directly-constructed, no host/DB,
    refs Core + Server; Integration = WebApplicationFactory/Testcontainers Postgres; RazorComponents =
    bUnit render tests — no host, no DB). `Tests.Unit.csproj` now references Server (enabling
    host-free Server-service unit tests; "no DbContext in Unit" is a convention guardrail, not a
    reference-graph constraint). `SKILL.md` description updated to surface testing.
  - **New test project:** `TheCanalaveLibrary.Tests.RazorComponents` (bUnit 1.33.3 + xUnit + FluentAssertions,
    references SharedUI; added to `.sln`). JSInterop.Loose for Blazored.Typeahead.
  - **Test backfill — Unit tier (`Tests.Unit`):** `HtmlSanitizationServiceTests` (WU5/WU6 —
    `ServerHtmlSanitizationService` directly constructed; covers all 11 allowed tags, `<script>`
    stripping, anchor normalization, scheme filtering, CSS/class stripping, whitespace guard); one
    production fix found: guard was `IsNullOrEmpty` — changed to `IsNullOrWhiteSpace`. `SpriteReadServiceTests`
    (WU2 — `ServerSpriteReadService` with `FakeWebHostEnvironment`; covers animated/static/unknown
    fallback, theme path).
  - **Test backfill — Integration tier (`Tests.Integration`):** `TagReadServiceTests` (WU3/WU11 —
    `ITagReadService` via `TestAppFactory` scope; covers ILike, alphabetical order, cap, SpriteUrl null,
    type filter, relative assertions). `UserDeletionServiceTests` (WU1 Feature 52 — highest-value
    FK-invariant test: all four Restrict edges verified against real Postgres via `UserManager<User>`;
    covers null SourceUserId, cascade-deleted UserStat, FollowedUser/Vouch/UserProfileComment cleanup,
    FK-violation-free execution).
  - **Test backfill — RazorComponents tier (`Tests.RazorComponents`):** `PaginationControlsTests` (WU8 —
    page-window math, active-page markup `aria-current`/CSS token, Prev/Next disabled, range summary,
    callback; CSS custom property rendering remains manual-only for Stage 6); `TagChipTests` (WU4);
    `TagSelectorTests` (WU11 — pre-selected chip render/remove, `OnSelectionChanged`; add-via-typeahead
    is manual-only per bUnit JS limitation); `UserCardTests` (WU10).
  - **Mutation sanity confirmed for all three new suites:** (1) `<script>` added to allow-list →
    `Sanitize_ScriptTag_IsStrippedCompletely` fails; (2) Notification SetNull commented out →
    `DeleteUserAsync_NullsOutSourceUserId_OnNotificationsSentByThisUser` fails; (3) `aria-current`
    condition inverted → 3 `PaginationControlsTests` fail. All reverted; suite green.
  - **Final counts:** 62 Unit + 33 Integration + 41 RazorComponents = **136 tests total**, all passing.
- **Tool:** opusplan. **Pointer:** `canalave-conventions/testing.md`, `forward_plan.md` "Test
  strategy" Resolved entry. **Deps:** WU12.

### WU15 — UserStoryInteractions L2 (writes + per-viewer state reads) *(reordered before WU13 — 2026-06-22)* — DONE ✓ (2026-06-22)
- **Cells:** 16 L2 → Stage 5; 16 L6 → Stage 5 (indexes already correct, verified during WU15);
  17 L6 → Stage 5 (same index file). **17 L2 stays Stage 2 — deferred to WU27**.
- **Done:** `Core/UserStoryInteractions/` cluster (enum, constants, DTOs, interfaces);
  `Server/UserStoryInteractions/` (read + write impls, CQRS-lite inheritance); DI in `Program.cs`.
  15 integration tests (`UserStoryInteractionServiceTests`, Testcontainers Postgres): upsert, date
  partition stamping/clearing, HasStarted preservation, sparse cleanup + cascade, `GetStatesByStoryIdsAsync`
  user-scoping, all-false default, anonymous context empty-read + write guard.
- **Verified:** `dotnet build` green (8 projects, 0 errors). `dotnet test` green: 236 total
  (93 Integration / 79 Unit / 64 RazorComponents). Detail in `audit/UserStoryInteractions.md` Feature 16 L2 Stage-5 note.
- **Tool:** opusplan. **Pointer:** `audit/UserStoryInteractions.md` Feature 16. **Deps:** WU12.

### WU16 — `UserStoryInteractionPanel` composite *(reordered before WU13 — 2026-06-22)* — DONE ✓ (2026-06-22)
- **Cells:** 16 L3/L3.5/L4 → Stage 5. 17 L3.5 stays Stage 2.
- **Done:** `Core/UserStoryInteractions/InteractionDisplayContext.cs` (enum: `Listing|Detail`);
  `SharedUI/UserStoryInteractions/InteractionVisuals.cs` (static class, inner `Info` record, verbatim
  audit table transcription); `SharedUI/UserStoryInteractions/UserStoryInteractionPanel.razor` (iterates
  `Enum.GetValues<InteractionTypeEnum>()` for locked button order; debounce via CTS + Task.Delay;
  optimistic local state; `IDisposable`; listing visibility rule: blank-slate OR already active for
  ReadLater/Ignore). `Tests.RazorComponents/FakeUserStoryInteractionWriteService.cs` +
  `UserStoryInteractionPanelTests.cs` (13 tests). `Tests.Unit/InteractionVisualsTests.cs` (26 tests).
  **Discovery**: Blazor renders bool `aria-pressed="@bool"` as a boolean HTML attribute (absent when
  false, empty string when true) — tests use `HasAttribute("aria-pressed")` not string comparison.
- **Verified:** `dotnet build` green (8 projects, 0 errors). `dotnet test` green: 275 total
  (105 Unit / 77 RazorComponents / 93 Integration). Detail in `audit/UserStoryInteractions.md`
  Feature 16 L3/L3.5/L4 Stage-5 notes.
- **Tool:** opusplan. **Pointer:** `audit/UserStoryInteractions.md` Feature 16. **Deps:** WU7, WU15.

### WU13 — `StoryCard` leaf *(moved after WU15+WU16 — 2026-06-22)* — DONE ✓ (2026-06-23)
- **Cells:** 5 L3/L3.5/L4 (card slice only — cells stay 4/4/1; StoryPage dispatcher, StoryDesktop/Mobile,
  and StoryDeck still hold those cells).
- **Done:** Step 0 — renamed `StoryInteractionPanel` → `UserStoryInteractionPanel` (component file,
  test file, `FakeUserStoryInteractionWriteService` xref, all doc/skill/audit references). Step 1 —
  additive `StoryListingDto.ShortDescription (string?)` extension (warm-partition projection, no migration;
  `StoryListingRow` + `ProjectListingRows` + `ToDto` updated in `ServerStoryReadService`). Step 2 —
  built `SharedUI/Stories/StoryCard.razor` as a pure leaf: `[EditorRequired] StoryListingDto Story` +
  `UserStoryInteractionStateDto? InteractionState` + `bool IsOwnStory` + 4 gated `EventCallback`s
  (Discover, CopyLink, Report, Download). Composes `TagChip` (read-only) + `UserStoryInteractionPanel`
  in Listing context. Author byline is a plain hyperlink — NOT `UserCard` (spec §5.30.7). Cover art uses
  stored URL verbatim with `_coverArtFailed` `@onerror` fallback. Status/rating/word-count computed
  display properties; caret with always-present "View Story" link + `HasDelegate`-gated optional items.
  Step 3 — `StoryCardTests.cs` (30 bUnit tests, JSInterop.Loose, registers
  `FakeUserStoryInteractionWriteService`): title link, author link/plain-text, tags, ShortDescription
  tooltip/null, word-count theory (8 cases), cover-art fallback, status/rating badge theories, panel
  composition (blank-slate + IsOwnStory), caret gating.
- **Verified:** `dotnet build` green (8 projects, 0 errors). `dotnet test` green: 105 Unit + 107
  RazorComponents + 109 Integration = 321 total. RazorComponents tier covers the StoryCard surface
  (30 new tests); L4-Style visual sign-off pending (requires live server check per the plan — see
  audit/Stories.md Feature 5 WU13 slice note). Mints the StoryCard contract → WU14 (`StoryDeck`) now
  has its key dep satisfied. Detail in `audit/Stories.md` Feature 5 WU13 slice note.
- **Tool:** opusplan. **Pointer:** `audit/Stories.md` Feature 5 + audit-summary §5.
  **Deps:** WU4, WU7, WU12, WU16.

### WU14 — `StoryDeck` composite *(pass-through)* — DONE ✓ (2026-06-23)
- **Cells:** 5 L3.5/L4 (deck slice only — cells remain Stage 4/1 because `StoryPage`
  dispatcher + `StoryDesktop`/`StoryMobile` still hold them → WU25; narrative in
  `audit/Stories.md` Feature 5 WU14 note).
- **Done:** built `SharedUI/Stories/StoryDeck.razor` as a pass-through layout composite (no service
  injection). Three-state internally: `null` → loading text, empty list → customisable `EmptyMessage`,
  populated → `grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6` of `StoryCard` + unconditional
  `PaginationControls` (self-hides at `TotalPages ≤ 1`). Contract: `[EditorRequired]
  IReadOnlyList<StoryListingDto>? Stories`, `IReadOnlyDictionary<int, UserStoryInteractionStateDto>?
  InteractionStates` (keyed by StoryId, batch-loaded by parent), `int? CurrentUserId` (deck computes
  `IsOwnStory` per card), `string EmptyMessage` (default "No stories found."), pagination forwards.
  Caret callbacks deferred — additive when first consumer (WU28/34/38) needs them.
  Pre-implementation doc-touch (moment 1/2): fixed duplicate StoryDeck paragraph and contradictory
  Loading States example in `layer3.5-structure.md`; added StoryDeck Pattern Accumulation entry to
  `layer4-style.md`. **Real insight during verification:** in Listing context, non-ReadLater/Ignore
  active buttons render as `<span>` (read-only, no `OnToggle` delegate) rather than `<button>` with
  `aria-pressed` — so the forwarding test asserts `span[aria-label="Favorite"]` appears when
  `IsFavorite=true`, not `aria-pressed`.
- **Verified:** `dotnet build` green (8 projects, 0 warnings/errors). `dotnet test` green: 381 total
  (112 Unit + 136 RazorComponents + 133 Integration). Mutation sanity: inverted populated-branch
  condition → 6 `StoryDeckTests` fail; reverted. L4 visual sign-off pending live-server check
  (Stage-6 gate). Test tier: RazorComponents (`StoryDeckTests.cs`, 14 tests).
- **Tool:** opusplan. **Pointer:** `audit/Stories.md` Feature 5. **Deps:** WU8, WU13.

### WU17 — Chapters L2 (writing/versioning + reading) — DONE ✓ (2026-06-22)
- **Cells:** 6 L2, 7 L2 — both now Stage 5.
- **Done:** Minted `Core/Chapters/` cluster (moved `Chapter`/`ChapterContent` from `Core/Models/`) and
  `Server/Chapters/`. New files: `ChapterText.cs` (word-count helper, strips HTML+decodes entities
  before whitespace-split), 5 DTOs, `ChapterValidations/ChapterValidationException`, `IChapterReadService`,
  `IChapterWriteService : IChapterReadService`, `ServerChapterReadService` (primary-ctor, `ReadOnlyApplicationDbContext`),
  `ServerChapterWriteService : ServerChapterReadService` (two-SaveChanges circular-FK break for create).
  Migration `20260623005108_MakeChapterPrimaryContentIdNullable` (made `PrimaryContentId` a `long?`).
  Two bugs found during implementation: (1) async-scope bug in test helpers (`using IServiceScope` +
  `return Task<>` without `await` → "reader is closed"; fixed to `async`/`await`); (2) `SelectMany`
  + outer `OrderBy` on projected DTO can't be translated by EF Core — fixed by moving `OrderBy` inside
  the inner query on the entity field.
- **Verified:** `dotnet build` green; `dotnet test` 50/50 green (Unit: `ChapterTextTests` 14 tests;
  Integration: `ChapterWriteServiceTests` 8 tests + `ChapterReadServiceTests` 8 tests). Mutation-sanity:
  `"script"` added to allow-list → `SanitizesScriptTag` fails; reverted. Server boot: `200` on homepage
  and `/Account/Login`. Detail in `audit/Chapters.md` Features 6, 7 Stage-5 notes.
- **Tool:** opusplan. **Pointer:** `audit/Chapters.md` Features 6, 7. **Deps:** WU12.

### WU18 — `ChapterNavigation` composite — DONE ✓ (2026-06-23)
- **Cells:** 7 L3.5/L4 (nav slice) — now Stage 5.
- **Done:** `SharedUI/Chapters/ChapterNavigation.razor` — injection-free coordination composite
  (spec §5.30.3). Four concerns inline (no sub-components): prev/next `<a>`/`<span aria-disabled>`
  links; chapter-select `<details>` disclosure with per-entry alt-version indicator
  (`HasAlternateVersions`); version picker `<details>` (rendered only when `Versions.Count > 1`);
  current chapter/version highlighted via `aria-current="page"`. Navigation is anchor hrefs (Blazor
  Router intercepts — no full page reload; no NavigationManager injection). Minted parameter contract
  for WU26 dispatcher. Doc-Touch: `layer3.5-structure.md` Pass-Through snippet updated to real shape;
  `layer4-style.md` Pattern Accumulation entry added.
- **Verified:** `dotnet build` green; `dotnet test` 336 total (105 Unit / 122 RazorComponents /
  109 Integration). Covering tier: **RazorComponents** (13 tests in `ChapterNavigationTests.cs`).
  Visual/L4 sign-off pending (Stage 6), human check against live server via throwaway harness before
  or during WU26. Detail in `audit/Chapters.md` Feature 7 WU18 Stage note.
- **Tool:** opusplan. **Pointer:** `audit/Chapters.md` Feature 7. **Deps:** WU17.

### WU19 — Comments L2 (posting / display / likes / spoiler) — DONE ✓ (2026-06-23)
- **Cells:** 23 L2, 24 L2, 25 L2, 26 L2 → Stage 5. Also closed: 25 L1 → Stage 5 (stale-code trap;
  explicit `CommentLike` entity built) and 26 L1 gap (added `IsSpoiler` property + migration).
- **Done:** `Core/Comments/` cluster (entities moved from `Core/Models/`, DTOs, interfaces, validations);
  `Server/Comments/ServerCommentReadService` (golden-index pagination, TPT via `ChapterComments` DbSet,
  per-viewer EXISTS subquery for `IsLikedByCurrentUser`) + `ServerCommentWriteService` (Post/Edit/Delete/
  ToggleLike, author-only, sanitize-once-on-save, hard delete + FK cascade). DI registered in `Program.cs`.
  Migration `20260623222518_AddIsSpoilerToChapterComment` applied. `layer2-services.md` stale doc line
  corrected (Moment 1). Tests: 7 Unit (`CommentValidationsTests`), 18 Integration (`CommentWriteServiceTests`),
  6 Integration (`CommentReadServiceTests`) — 367 total green. Server booted clean.
- **Tool:** opusplan. **Pointer:** `audit/Comments.md`. **Deps:** WU17.

### WU20 — `CommentItem` leaf + `CommentSection` composite — DONE ✓ (2026-06-23)
- **Cells:** 23/24/25/26 L3-Logic/L3.5-Structure/L4-Style → Stage 5.
- **Done:** `CommentEditor` leaf (shared editing surface, `SaveLabel`/`OnCancel.HasDelegate`/`Busy`),
  `CommentItem` leaf (author block, RichTextView↔CommentEditor edit swap, spoiler blur+confirm,
  like/reply/edit/delete affordances gated by `.HasDelegate`+`IsOwnComment`), `CommentSection`
  coordination composite (coordinated-paginated-region injection, paginated load, two-level tree,
  optimistic like reconciliation, delete ConfirmDialog, reply/edit/post composers). 45 new
  RazorComponents tests (11 CommentEditor, 21 CommentItem, 13 CommentSection) + `FakeCommentWriteService`.
  Key lesson: BlazoredTextEditor toolbar renders same-subtree buttons — all CommentEditor button
  selectors use `aria-label` not text-content scanning. L4 sign-off via throwaway harness on
  `HomeDesktop.razor` (server → `200`, harness removed). All 426 tests pass.
- **Tool:** opusplan. **Pointer:** `audit/Comments.md` Feature 24 Stage-5 note. **Deps:** WU6, WU9, WU19.

### WU21 — Following + Vouches — DONE ✓ (2026-06-22)
- **Cells:** 18 L2/L3/L3.5/L4 → Stage 5; 19 L1/L2/L3/L3.5/L4 → Stage 5 (L1 re-verified: `MakeVouchTextUnlimited` migration).
- **Done:** `Core/Following/` + `Server/Following/` CQRS-lite cluster; `IFollowingReadService` /
  `IFollowingWriteService`; `ServerFollowingReadService` (read-replica) + `ServerFollowingWriteService`
  (inherits read, adds write + sanitizer). DTOs: `UserRelationshipStateDto`, `VouchDisplayDto`.
  `FollowingConstants.MaxVouchesPerUser = 5`, `VouchLimitException`. `SharedUI/Following/`:
  `FollowButton.razor`, `VouchButton.razor`, `VouchList.razor` (owner-conditional `IsEditable`).
  Notification seams `// TODO(WU22)`. DI registered in `Program.cs`.
- **Verified:** `dotnet test` green — 79 Unit / 64 RazorComponents / 78 Integration (221 total).
  Integration: `FollowingWriteServiceTests` + `FollowingReadServiceTests`. RazorComponents:
  `FollowButtonTests`, `VouchButtonTests`, `VouchListTests`. Visual/L4 human sign-off pending (WU30).
- **Tool:** opusplan. **Pointer:** `audit/Following.md` Features 18, 19. **Deps:** WU10.

### WU22 — Notifications L2 (generation + service) — DONE ✓ (2026-06-23)
- **Cells:** 41 L2, 42 L2, 43 L2 — all now Stage 5.
- **Done:** Deliberated and settled the notification generation mechanism (direct injected call +
  semantic per-event methods + best-effort post-commit + in-app always-on). Minted
  `Core/Notifications/` (`NotificationDto`, `NotificationSettingDto`, `INotificationReadService`,
  `INotificationWriteService`) and `Server/Notifications/` (`ServerNotificationReadService`,
  `ServerNotificationWriteService`). Private `CreateCoreAsync` owns drop-self + dedup invariants.
  Settings are sparse (upsert when differing from defaults; delete row when returning to defaults).
  Wired `// TODO(WU22)` seams in `ServerFollowingWriteService` (`FollowAsync`/`VouchAsync`).
  Corrected two spec deviations: `SourceUserId` is SET NULL (not RESTRICT); §5.18 in-app toggle
  dropped (in-app always-on). Detail in `audit/Notifications.md`, `cross-cutting.md`,
  `layer2-services.md`, `forward_plan.md` Resolved.
- **Verified:** `dotnet build` green (0 errors). `dotnet test` green: 105 Unit + 77 RazorComponents +
  109 Integration = 291 tests total. Integration tier (`Tests.Integration/NotificationServiceTests.cs`,
  Testcontainers Postgres, 16 tests): generation correctness; drop-self; dedup; read/mark/settings
  coverage; end-to-end `FollowAsync` → notification row. Mutation sanity confirmed (drop-self line
  disabled → test fails; reverted).
- **Deferred semantic methods:** `NotifyNewChapterAsync`, `NotifyNewCommentAsync`, etc. — each lands
  co-delivered with its triggering work-unit. The create-core + DAG pattern are in place.
- **Tool:** opusplan. **Pointer:** `audit/Notifications.md`. **Deps:** WU1.

---

## Phase 3 — Consumers / Pages

### WU23 — `ResultsFilterPanel` composite
- **Cells:** 31 L3/L3.5/L4 (filter-panel slice).
- **Do:** coordination composite (tag filter via TagSelector, interaction filters, source/sort). Consumed
  by Search, Profiles, Bookshelves — build before them. **Tool:** opusplan.
  **Pointer:** `audit/Discovery.md` Feature 31. **Deps:** WU8, WU11.

### WU24 — Story create/edit (`StoryPropertiesForm` + `AdminControls`)
- **Cells:** 4 L3/L3.5/L4 *(Stage-4 → finish to spec)*.
- **Do:** complete the EditForm+ViewModel: TagSelector wiring, cover-art upload, slug (server-only),
  `AdminControls` (wraps `AuthorizeView`, author-only — universal, minted here, reused by WU25);
  shared create/edit routes. `StoryPropertiesForm.razor`/`StoryPropertiesViewModel.cs` already live at
  `SharedUI/Stories/` (moved out of the legacy `Components/StoryProperties/` folder ahead of this build
  — see `audit/Stories.md` Shared Context); that move is folder-only and is **not** a head start on the
  finish-to-spec work — the content is still the old-convention scaffolding the Stage-4 note describes
  (Bootstrap classes, no `TagSelector`/cover-art/slug/`AdminControls` wiring).
  **Tool:** opusplan (Stage-4 build-to-spec; Sonnet for parts now Stage-3).
  **Pointer:** `audit/Stories.md` Feature 4. **Deps:** WU11, WU14.

### WU25 — Story detail page (`StoryPage` + desktop/mobile) *(Stage-4 → build spec §5.28)*
- **Cells:** 5 L3/L3.5/L4.
- **Do:** discard the `RandomNumberGenerator` stubs; add `[PersistentState]` (no flicker), hybrid catch-all
  route `{*StorySlug}`; layout title→cover→long desc→chapter selection→recommendations; AdminControls for
  author UI. Delete `RandomNumberGenerator` when done. **Tool:** opusplan/Sonnet.
  **Pointer:** `audit/Stories.md` Feature 5. **Deps:** WU13, WU14, WU24, WU29 (recommendations section),
  WU26 (chapter selection list).

### WU26 — Chapter reading + writing pages
- **Cells:** 6 L3/L3.5/L4, 7 L3/L3.5/L4, 44 L2/L3/L3.5 (reading-progress MVP: client JS scroll %, direct
  DB write; `HasStarted` at 90% of Ch.1).
- **Do:** reading page (ChapterNavigation top+bottom, RichTextView, reader settings, rating warning +
  "skip to next"); writing page (EditorView + versioning); CommentSection on chapter.
  **Tool:** opusplan. **Pointer:** `audit/Chapters.md`. **Deps:** WU5, WU6, WU18, WU20.

### WU27 — Bookshelves page
- **Cells:** 17 L3/L3.5/L4.
- **Do:** `/bookshelves/{Tab}` system tabs (Favorites, Private, Read It Later, Actively Reading, Completed,
  Ignored, Abandoned, Following, My Stories); each composes StoryDeck + ResultsFilterPanel (narrowing, not
  discovery). **Tool:** opusplan/Sonnet. **Pointer:** `audit/UserStoryInteractions.md` Feature 17.
  **Deps:** WU14, WU16, WU23.

### WU28 — Discovery pages + Tag Directory + Tag Admin
- **Cells:** 31 L3.5 (page), 32 L2/L3/L3.5 (FTS as filter axis + Rank relevance sort), 33 L2/L3/L3.5
  (Manual Tree Search, stateless pivots), 34 L3/L3.5 (Tag Directory `/tags`), 11 L2/L3/L3.5 (Tag admin
  CRUD behind AuthorizeView on the directory).
- **Do:** SearchPage (`/discover`, random-preloaded, "give me more" = interaction-as-pagination); Tag
  Directory one-page/two-experiences; manual tree pages. **Tool:** opusplan.
  **Pointer:** `audit/Discovery.md`, `audit/Tags.md` Feature 11. **Deps:** WU14, WU23, WU4.

### WU29 — Recommendations
- **Cells:** 27/28/29/30 L2/L3/L3.5/L4. Hidden-Gem at-limit behavior resolved Phase B (reject +
  remove-first); 5-per-user in service.
- **Do:** submission (EditorView, min char count, one-per-user), display (Author Spotlight ≤5,
  RecommendationLike), attribution popup + RecommendationSuccess. **Tool:** opusplan.
  **Pointer:** `audit/Recommendations.md`. **Deps:** WU6, WU13.

### WU30 — Profiles + Theme selection
- **Cells:** 20/21/22 L2/L3/L3.5/L4, 3 L3/L3.5 (theme-selection UI in profile settings).
- **Do:** profile editing (JSON settings groups, `IUserSettingsService` self-edit exception, picture
  upload), display page (`/user/{UserId}/{*Tab}` — identity half + tabbed StoryDecks via
  ResultsFilterPanel; UserCard vouches; badges), UserStats real-time counters. **Tool:** opusplan.
  **Pointer:** `audit/Profiles.md`, `audit/Sprites.md` Feature 3. **Deps:** WU10, WU14, WU21, WU23.

### WU31 — Blog posts + Feature contributions
- **Cells:** 35/36 L2/L3/L3.5/L4, 56 L2/L3/L3.5.
- **Do:** TPT blog write (EditorView) + display in profile/story/group contexts + comments; admin
  feature-contribution attribution. **Tool:** opusplan. **Pointer:** `audit/BlogPosts.md`.
  **Deps:** WU6, WU20.

### WU32 — Groups
- **Cells:** 38/39/40 L2/L3/L3.5/L4.
- **Do:** group CRUD (3 audience types, GroupMember roles), GroupStory + GroupFolder nesting (rating
  enforcement at write), group page (`/group/{GroupId}/{*GroupSlug}`) with StoryDeck + comments.
  **Tool:** opusplan. **Pointer:** `audit/Groups.md`. **Deps:** WU14, WU6, WU20.

### WU33 — Notifications UI
- **Cells:** 42 L3/L3.5/L4, 43 L3/L3.5/L4.
- **Do:** `/notifications` grouped page + bell flyout (cross-cutting layout element) + settings page
  driven by DB data. **Tool:** opusplan. **Pointer:** `audit/Notifications.md`. **Deps:** WU22.

### WU34 — Moderation + Story import/approval
- **Cells:** 46/47/48 L2/L3/L3.5/L4, 53 L2/L3/L3.5.
- **Do:** reporting (polymorphic), `/mod/reports` queue (desktop-only, ActiveReportCount auto-flag),
  `/mod/submissions` approval, import verification. **Tool:** opusplan. **Pointer:** `audit/Moderation.md`.
  **Deps:** WU9, WU12.

### WU35 — Messaging
- **Cells:** 49 L2/L3/L3.5/L4.
- **Do:** `/messages/{ConversationId?}`, three-table model, SignalR realtime (InteractiveServer), EditorView
  composition, `LastReadTimestamp`, `AllowPrivateMessages` gate. **Tool:** opusplan.
  **Pointer:** `audit/Messaging.md`. **Deps:** WU6.

### WU36 — Badges
- **Cells:** 50 L2/L3/L3.5/L4.
- **Do:** synchronous inline badge checks; UserBadge display order on profiles. **Tool:** opusplan.
  **Pointer:** `audit/Badges.md`. **Deps:** WU30.

### WU37 — Remaining Stories/Tags cluster
- **Cells:** 9 L2/L3/L3.5/L4 (Series), 10 L2/L3/L3.5/L4 (Relationships), 12 L2/L3/L3.5 (Story Tagging
  write), 15 L2/L3/L3.5/L4 (Saved Tag Selections, copy-on-write share).
- **Tool:** opusplan. **Pointer:** `audit/Stories.md`, `audit/Tags.md` Features 12, 15. **Deps:** WU11, WU24.

### WU38 — Account deletion UI + View Count + Export
- **Cells:** 52 L3/L3.5 (deletion UI; service in WU1), 45 L2/L3 (view-count MVP direct increment, first
  client ping), 54 L2 (epub/pdf export, app-layer only).
- **Tool:** opusplan. **Pointer:** `audit/Identity.md`, `audit/Stories.md` Feature 45, `audit/Export.md`.
  **Deps:** WU25.

---

## Blocked / deferred — genuine Stage-1 intent gaps (no sequence number)

These have an undesigned UI; resolve the design (chat with skill files) before they can be sequenced.
Their non-UI layers (L1/L2) may already be Stage 5/2 but the *UI cells* are blocked.

- **Story Arcs UI** (8 L3/L3.5) — §8.2 arc-management UI never designed. (L1 Stage 5, L2 Stage 2.)
- **Polls UI** (37 L3/L3.5) — §8.6 detailed poll UI unspecified.
- **Custom Lists** (51 L3/L3.5) — §8.7 creation flow + filter composition TBD.
- **Community Spotlight** (55, all layers) — §5.26 donation infra TBD; entity is a placeholder.

When a gap resolves: it becomes Stage 2 (or 3 if the conversation yields a build-ready spec); insert a
work-unit into Phase 3 and update `status.md` + the audit file.

---

## Post-MVP — Layers 5–8 (additive, batchable; not sequenced here)

Per `grid_axes.md` §"The Two Boundaries": these swap method bodies / add DDL / add standalone workers
behind the contracts frozen in Layers 1–4. Batch by pattern when the MVP slice they sit behind is stable.

- **L5 — WASM enablement (all features).** Map endpoints from the stable `IXService` contracts + `Client`
  HTTP impls. Includes the two genuine mechanical Stage-4 cells — **Story L5 endpoint wiring** (4/5 L5:
  `HttpStory*Service` call `/{id}/edit` + write routes `StoryEndpoints` never maps) and **Sprites L5**
  (`WasmSpriteReadService` optimistic URLs, 3 L5 Stage 4). Pointers: `audit/Stories.md` §3a,
  `audit/Sprites.md`. Governed by `layer5-wasm.md`.
- **L6 — SQL indexes (batch DDL).** Regenerate UserStoryInteraction filtered indexes off the re-modeled
  `has_started` columns (16/17 L6 Stage 4), comment golden index `(chapter_id, date_posted DESC)`, StoryTag
  reverse index, etc. FTS GIN on `StoryListing.SearchVector` already in `InitialSchema`. Pointers:
  per-folder audit L6 notes. Governed by `layer6-indexes.md`.
- **L7 — Redis integration.** Write-behind (16/17 interactions, 45 view counts via `INCR`), ephemeral
  store (44 LastReadDate hash), read-side cache (61 top-100). MVP direct-write bodies get swapped behind
  unchanged signatures. Governed by `layer7-redis.md`.
- **L8 — Data marts (+ horizontal boundary: requires real user data).** 59 Automatic Tree Search, 60 Tree
  Search Data Mart Worker, 61 Also Favorited / Also Recommended, 62 SiteDailyStat Worker. No EF model —
  raw-SQL tables, zero-downtime swap (resolved: `SiteDailyStat`/marts have no EF model). Pointers:
  `audit/Discovery.md` L8 notes, `audit/Moderation.md` Feature 62. Governed by `layer8-data-marts.md`.
- **Deferred workers (nothing to operate on yet — `grid_axes.md` horizontal note).** 57 Notification
  Cleanup Worker (nothing 60 days old), 58 UserStat Recalculation Worker (real-time counters carry MVP).
- **Image storage cloud backend.** `IImageStorageService` (minted WU12, MVP impl
  `LocalImageStorageService` writing under `wwwroot/uploads/`) gets a second implementation,
  `S3ImageStorageService` (`AWSSDK.S3`), swapped in behind the same frozen interface — MinIO endpoint
  via Aspire in dev, Cloudflare R2 endpoint in prod (same SDK code, different endpoint config — spec
  §3.17 / `cross-cutting.md`). Additive, no Layer 1–4 change. Pointer: `audit/ImageStorage.md`.
