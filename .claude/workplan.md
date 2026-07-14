# Workplan — Ordered Work-Units (atoms-first)

> New work-units are sequenced by `.claude/middle_plan_v2.md` (the live master plan, since
> 2026-07-05 — it superseded `.claude/middle_plan.md`, which had itself superseded
> `forward_plan.md`); this file remains the work-unit ledger — every unit is still recorded here.

Produced by Phase D (`forward_plan.md`, now retired). This is the build sequence for Phase E. Each work-unit names
its **cell(s)** (Feature # + layer, per `status.md`), its **tool** (per CLAUDE.md Per-Stage Guidance),
its **audit pointer** (`.claude/audit/<Folder>.md`, section), and its **deps** (work-units that must be
at Stage 5 first). CLAUDE.md is the source of truth for stage semantics and file paths — this file
references it, does not restate it.

---

## Read this first (ordering preamble)

**Scope of the numbered sequence, as originally written (through 2026-07-05) = Layers 1–4 (the
MVP).** `grid_axes.md` §"The Two Boundaries" is authoritative: Layers 1–4 are the InteractiveServer
MVP (data → service → logic → structure → style); Layers 5–8 are *additive and batchable* — they
swap method bodies / add DDL / add standalone workers behind contracts frozen in 1–4, and never
force a 1–4 change. That architectural property is still true. The *scheduling* claim that
followed from it — "Layers 5–8 post-MVP" — is **superseded**: `middle_plan_v2.md`'s platform-first
inversion (2026-07-05) moved most L5–L8 work *ahead of* several still-pending MVP-surface rows
(WU-L5Pilot shipped WASM 2026-07-04; WU-SignalBuffering dissolved the old Redis/L7 plan into L2/L6/L8
2026-07-06; WU-Marts shipped L8 2026-07-07). So **numbered work-units below through 2026-07-05 build
L2/L3-Logic/L3.5-Structure/L4-Style** (L1 is done — see WU0); **named work-units from WU-CI onward
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
  `render-and-layout.md` "Render Mode"), `ReadOnlyApplicationDbContext` ctor mismatch. Also pivoted dev
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
  dropped (in-app always-on). Detail in `audit/Notifications.md`,
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

### WU23 — `ResultsFilterPanel` composite — DONE ✓ (2026-06-23)
- **Cells:** 31 L3/L3.5 → Stage 5. 31 L4 stays Stage 1 (visual sign-off pending, consistent with
  WU8/WU13 precedent).
- **Done:** Six-phase delivery. Phase 0: `UserStoryInteraction` nomenclature sweep — renamed all
  `Interaction…` identifiers to `UserStoryInteraction…` across Core, SharedUI, Server, and all three
  test projects; §8.7 entity renames (`UserInteractionFilter→UserStoryInteractionFilterType` etc.)
  carried by a data-preserving rename migration (`RenameTable`/`RenameColumn`/raw-SQL
  `RENAME CONSTRAINT` — no drop/recreate). Phase 1: `Core/Discovery/StoryFilterDto.cs` (sealed record)
  + `Core/Tags/TagFilterSelection.cs` (axis emit contract). Phase 2: `SharedUI/Tags/TagFilter.razor`
  (include/exclude axis, cross-dedup, injection-free) +
  `SharedUI/UserStoryInteractions/UserStoryInteractionFilter.razor` (checkbox axis, injection-free).
  Phase 3: `SharedUI/Discovery/ResultsFilterPanel.razor` (assembler, @code-buffered, Apply emits
  `StoryFilterDto`; Relevance hidden without text; default sorts `[DatePublished, Random]`). Phase 4:
  `GetListingsAsync(StoryFilterDto)` added to `IStoryReadService` + implemented in
  `ServerStoryReadService` (two-step: filtered IQueryable → scalar ID page → `GetListingsByIdsAsync`;
  tag AND-include; tag exclude; FTS via `PlainToTsQuery`/`SearchVector.Matches`; viewer-scoped
  interaction exclusion with pre-computed bool constants; sort switch). Phase 5: tests —
  9 Integration (`StoryListingsTests`), 8 RazorComponents (`ResultsFilterPanelTests`),
  7 RazorComponents (`UserStoryInteractionFilterTests`).
- **Verified (2026-06-23):** `dotnet build` green (8 projects, 0 errors). `dotnet test` green:
  112 Unit + 198 RazorComponents + 142 Integration = 452 total. Detail in `audit/Discovery.md`
  Feature 31 WU23 Stage note.
- **Tool:** opusplan. **Pointer:** `audit/Discovery.md` Feature 31. **Deps:** WU8, WU11.

### WU24 — Story create/edit pages (`StoryEditorPage` + `StoryPropertiesForm` rebuild) — DONE ✓ (2026-06-23)
- **Cells:** 4 L3/L3.5/L4 — all now Stage 5.
- **Architecture settled (WU24 planning, 2026-06-23):** no `AdminControls` component — ownership-conditional
  affordances are inline `@if` on a page-computed bool (settled in `identity-and-authorization.md`
  "Security vs affordance"). Editing is author-only (identity-equality, server-enforced),
  never author-or-mod (moderation is a separate WU34 path). Content-editing pattern for Story/Chapter:
  **view-page / edit-page split** (see `identity-and-authorization.md` "Two content-editing patterns").
- **Do:**
  - **Doc-touch (moment 1, before code):** update `identity-and-authorization.md` + `layer3.5-structure.md` with the
    active-user-conditional handling analysis and two-pattern content-editing rule (done pre-WU24 build).
  - **New `StoryEditorPage.razor`** (Server, both `@page "/story/new"` and `@page "/story/{StoryId:int}/edit"`,
    single responsive page). Thin dispatcher: resolves auth, data-loads for edit, maps ViewModel ↔ DTOs,
    hosts `<StoryPropertiesForm>`. On edit: owner redirect/forbidden if `story.AuthorId != currentUserId`
    (UX pre-check; server gate is the real authority).
  - **Rebuild `StoryPropertiesForm.razor`** (presentational — no `@inject`; stays bUnit-testable):
    Bootstrap → Tailwind; `InputTextArea` long-desc → **`EditorView`** (pull-on-submit via `@ref`);
    add Status field; one `<TagSelector>` per `TagTypeEnum` category; `<InputFile>` for cover art;
    delete dead `TagDropDownDTO`/`GetAllCharacterTagsAsync` code.
  - **TagSelector → DTO mapping:** `TagChipDto` ↔ `IStoryTag` with default Priority (no priority UI in MVP).
  - **Cover-art upload ordering:** on create = save story first (get id), then upload to `IImageStorageService`
    + patch `CoverArtRelativeUrl`; on edit = id already exists.
  - **Server-side author gate:** `UpdateStoryAsync` — load story, throw `UnauthorizedAccessException` if
    `story.AuthorId != activeUser.UserId`. `CreateStoryAsync` — stamp `AuthorId` from
    `IActiveUserContext.UserId`, not DTO (drop `AuthorId` from the client-settable DTO).
- **VERIFIED (2026-06-23):** `dotnet build` green (8 projects, 2 pre-existing warnings, 0 new errors).
  `dotnet test` green: 112 Unit + 208 RazorComponents + 145 Integration = 465 total (13 net-new tests).
  Covering tiers: **Integration** — `StoryWriteServiceTests` (3 new tests: `CreateStoryAsync_StampsAuthorId`,
  `UpdateStoryAsync_Owner_CanUpdateTitle`, `UpdateStoryAsync_NonOwner_ThrowsUnauthorizedAccessException`);
  **Integration** — `CommentReadServiceTests` + `CommentWriteServiceTests` backfill (wired separately; in
  comment cluster); **RazorComponents** — `StoryPropertiesFormTests.cs` (10 tests: title input, textarea,
  select, file input, default/custom submit label, valid submit callback, invalid empty-title, server
  validation errors rendered, IsLoading disables button); **RazorComponents** — `TagSelectorTests`/
  `ResultsFilterPanelTests` updated (`GetTagChipsByIdsAsync` stub added to `FakeTagReadService`).
  `Routes.razor` now uses `AuthorizeRouteView` (not `RouteView`) — `[Authorize]` attributes now enforced.
  L4 visual sign-off pending human review.
- **Tool:** opusplan. **Pointer:** `audit/Stories.md` Feature 4. **Deps:** WU11, WU14.

### WU25 — Story view page (`StoryPage` + desktop/mobile) — DONE ✓ (2026-06-24)
- **Cells:** 5 L3/L3.5/L4 → Stage 5 (L4 stays Stage 1 pending visual sign-off, per WU13/WU14/WU24 precedent).
- **Architecture (settled WU25, 2026-06-24):** read-only view page (content-editing Pattern 1 — view
  side). Full detail layout per §5.28: title → cover art → long description →
  chapter selection → recommendations. Full metadata row (rating, status, word count, dates, tag chips).
  Author-only "Edit Story" link is an inline `@if` (Owner-Conditional Edit Affordances convention).
  - **`ChapterNavigation` is NOT used here.** That component (WU18) is reading-context-only
    (`CurrentChapterNumber` is `[EditorRequired]`; renders prev/next + a "Chapter N" dropdown).
    The story landing page uses a dedicated **`ChapterList` leaf** (new, `SharedUI/Chapters/`).
  - **`StoryDetailsDTO` extended:** added `int? AuthorId`, `string? CoverArtRelativeUrl`, `Rating Rating`,
    `StoryStatusEnum Status`, `IReadOnlyList<TagChipDto> Tags`. `GetStoryByIdAsync` projection uses
    two-step intermediate row (mirrors listing service) to resolve sprites in memory via `ToTagChip`.
  - **`GetChapterListAsync(int storyId)`** added to `IChapterReadService` + `ServerChapterReadService`.
    Returns `IReadOnlyList<ChapterListEntryDto>` — all chapters with non-primary alternate versions
    as indented sub-rows. Two-step query (chapters + non-primary versions via SelectMany, grouped
    in memory — mirrors `GetChapterVersionsAsync` pattern).
  - **`ChapterList` leaf** (`SharedUI/Chapters/`): `Chapters`, `StoryId`, `ShowDrafts`. Primary
    chapter row → `/story/{id}/{ch}`; non-primary alternates indented beneath as `Title - VersionName`
    → `/story/{id}/{ch}/{versionOrder}`. Same vertical structure on desktop and mobile.
  - **`[PersistentState]`** on `Story` and `Chapters` in `StoryPage` (kills prerender flicker).
  - **Route:** `/story/{StoryId:int}/{*StorySlug}` (catch-all cosmetic slug).
- **Tests (2026-06-24):** Integration tier — `StoryDetailTests` (15 tests) covering new `StoryDetailsDTO`
  fields and `GetChapterListAsync` (ordering, alternates, content-rating ceiling, unpublished). RazorComponents
  tier — `ChapterListTests` (14), `StoryDesktopTests` (22), `StoryMobileTests` (21) = 57 tests. All green.
- **Tool:** Sonnet in Claude Code. **Pointer:** `audit/Stories.md` Feature 5.
  **Deps:** WU13, WU14, WU24, WU29, WU26.

### WU26 — Chapter reading + writing pages — DONE ✓ (2026-06-24)
*(Pattern 1 split; rating model reconciliation; reading-progress)*
- **Cells:** 6 L3/L3.5/L4 → Stage 5, 7 L3/L3.5/L4 → Stage 5, 44 L2/L3/L3.5 → Stage 5;
  Feature 6/7 L1+L2 rating reconciliation (ChapterContent.Rating → nullable, floor + primary invariants).
- **Architecture (settled WU26):** content-editing Pattern 1 — two separate routes:
  - **Reading page** `/story/{StoryId:int}/{ChapterNumber:int}[/{VersionOrder:int}]` — `RichTextView` +
    `ChapterNavigation` top+bottom + `CommentSection` + JS scroll-% tracking + reader settings cascade
    + `RecommendationHelpfulPrompt` gate. Public (content-rating filter applies).
    Author-only inline `@if` link to chapter edit page.
  - **Edit/write page** `/story/{id}/chapter/new` + `/story/{id}/chapter/{ch}/edit[/{versionOrder}/]`
    — `EditorView` + progressive-disclosure versioning UI. `[Authorize]` + ownership gate.
  - The two renderers (`RichTextView` / `EditorView`) never co-exist (different routes).
  - See `layer3.5-structure.md` "Chapter Versioning — Progressive Disclosure" for the full route table and
    rating-floor/primary invariants.
- **Phases:**
  - Phase 0: doc-touch (route docs, versioning rule, WU29 reconcile).
  - Phase 0.5: rating model reconciliation (nullable `ChapterContent.Rating`, migration, DTOs, read/write
    service floor + primary invariants).
  - Phase 1: Feature 44 `IReadingProgressWriteService` + `MarkStartedAsync` + rec prompt-gate read.
  - Phase 2: `ChapterReadingPage` dispatcher.
  - Phase 3: `ChapterEditorPage` + `ChapterPropertiesForm`.
- **Tool:** Sonnet in Claude Code (plan exists). **Pointer:** `audit/Chapters.md`.
  **Deps:** WU5, WU6, WU18, WU20, WU29.

### WU27 — Bookshelves page — DONE ✓ (2026-06-24)
- **Cells:** 17 L2/L3/L3.5/L4 → Stage 5.
- **Done:** Full `/bookshelves/{Tab?}` page (11 tabs). New service methods: `GetBookshelfStoryIdsAsync`,
  `GetStoryIdsByAuthorAsync` (content-rating bypass), `GetListingsAsync(filter, restrictToStoryIds?)`,
  `GetRecommendedStoryIdsAsync` + `GetHiddenGemStoryIdsAsync` (additive; own approved recs by active user).
  `BookshelfTabVisuals`, `BookshelvesPage` dispatcher, `BookshelvesDesktop`, `BookshelvesMobile`.
  Following reskinned teal site-wide (`#2DBBA0`).
  Tests: Unit (BookshelfTabVisualsTests, 14), Integration (BookshelfStoryIdsTests, 16),
  RazorComponents (BookshelvesDesktopTests 7 + BookshelvesMobileTests 10). All green.
  Human visual sign-off pending → Stage 6.
  **Pointer:** `audit/UserStoryInteractions.md` Feature 17. **Deps:** WU14, WU16, WU23.

### WU27.5 — Tag Directory + Tag Administration — DONE ✓ (2026-06-25)
- **Cells:** 34 L2/L3/L3.5 → Stage 5, 11 L2/L3/L3.5 → Stage 5. L4 both features stays Stage 1.
- **Done:** Phase 0: workplan split, audit repoints, cross-cutting.md role correction, forward_plan.md
  resolved. Phase 1: `DataSeeder.cs` assigns AdminUser to Moderator+Admin; `Tag.ChildTags` nav rename;
  composite `(TagName, TagTypeId)` index migration (`20260625032833_WU27_5_TagCompositeUniqueIndex`).
  Phase 2: `GetTagDirectoryAsync` + `TagDirectoryGroupDto`/`TagDirectoryNodeDto`; `TagChipDto` extended
  with `IsFanon`/`AllowOCDetails`/`ParentTagId`. Phase 3: `ITagWriteService`, `ServerTagWriteService`,
  `TagValidations`, `TagValidationException`, `CreateTagDto`/`UpdateTagDto`, `TagTypeLayout`,
  `TagEditorFormResult`; DI registered. Phase 4: `TagDirectoryPage` + `TagDirectoryDesktop` +
  `TagDirectoryMobile` + `TagDirectorySection` + `TagEditorForm`. Phase 5: 75 tests green (23 Unit,
  24 RazorComponents, 28 Integration). Detail in `audit/Tags.md` Feature 11 WU27.5 Stage note +
  `audit/Discovery.md` Feature 34 WU27.5 Stage note.
  **Tool:** Sonnet in Claude Code (plan approved 2026-06-24). **Pointer:** `audit/Tags.md` Feature 11,
  `audit/Discovery.md` Feature 34. **Deps:** WU4, WU9, WU11 (all Stage 5).

### WU28 — Discovery: Search Page + FTS consumption + §8.7 default-settings matrix DONE ✓ (2026-06-25)
- **Cells:** 31 L2 (random batch method); 31 L3.5 (page dispatcher + Desktop/Mobile composites);
  32 L2/L3-Logic/L3.5 (FTS consumed + verified via the page — already built in `GetListingsAsync`,
  WU28 exposes + confirms end-to-end). Also additive: `TagIncludeMode { And, Or }` enum
  (`Core/Discovery/`), `StoryFilterDto.IncludeMode`, `TagFilterSelection.IncludeMode`,
  `TagFilter.AllowIncludeModeToggle`, `ResultsFilterPanel.ShowTagIncludeModeToggle`/seed-chip params
  (extend already-Stage-5 components; no separate cell).
  **Feature 33 (Manual Tree Search) carved into WU40.**
- **Do:**
  - **Phase 0** (doc-touch first): workplan WU28 rewrite + WU40 add; `forward_plan.md` resolved items;
    `audit/Discovery.md` settled-vs-open; skill files `layer2-services.md` / `layer3.5-structure.md`.
  - **Phase 1a:** `GetRandomBatchAsync(StoryFilterDto filter, int batchSize)` — extract `ApplyFilters`
    private helper from `GetListingsAsync`; plain random draw from the post-filter set
    (`OrderBy(Random()).Take(batchSize)`), no shown-id tracking, no TotalCount; interface + impl +
    integration tests.
  - **Phase 1b:** `IDiscoveryDefaultsReadService.GetDefaultExcludedInteractionsAsync(string searchModeKey)`
    — system matrix overlaid with sparse per-user overrides; `HasStarted` key dropped from mapping;
    `ServerDiscoveryDefaultsReadService`; register in `Program.cs`; integration tests.
  - **Phase 1c:** `TagIncludeMode { And, Or }` enum; `StoryFilterDto.IncludeMode = And` (default preserves
    all existing behaviour); `ApplyFilters` Or branch (`Any(st => ids.Contains(st.TagId))`); integration tests.
  - **Phase 2a:** `TagFilterSelection.IncludeMode`; `TagFilter [Parameter] AllowIncludeModeToggle = false`
    + AND/OR control above include selectors only (exclude side unchanged); bUnit tests.
  - **Phase 2b:** `ResultsFilterPanel [Parameter] ShowTagIncludeModeToggle = false`; `[Parameter]
    IReadOnlyList<TagChipDto> InitialIncludedTags`/`InitialExcludedTags` (closes WU23-deferred seed-chip
    enrichment); bUnit tests.
  - **Phase 3a:** `SearchPage.razor` dispatcher (`SharedUI/Discovery/`, `@page "/discover"`,
    `[AllowAnonymous]`; §8.7 defaults seed on init; two modes: random-append + "Give me more" vs.
    sorted-offset pagination).
  - **Phase 3b:** `SearchDesktop.razor` / `SearchMobile.razor` composites (`ShowTagIncludeModeToggle=true`
    on panel; random mode suppresses pagination + shows Give-me-more button; sorted mode shows pagination).
  - **Phase 4:** tests — Integration: `DiscoveryDefaultsReadServiceTests`, `RandomBatchTests`,
    OR-include tag mode. RazorComponents: `SearchDesktopTests` / `SearchMobileTests`,
    `ResultsFilterPanelTests` (seed-chip + toggle), `TagFilterTests` (AND/OR control).
- **Cells flipped:** 31 L2 `2→5`; 32 L2/L3/L3.5 `2→5`. Feature 33 unchanged → WU40.
  Feature 31 L3.5 stays Stage 5 (additive page on already-Stage-5 cell, precedent WU13).
  L4 stays Stage 1 (visual sign-off pending, consistent with WU8/WU13/WU23 precedent).
- **Did:** All five phases complete. `SiteSearchModes`/`UserStoryInteractionFilters` moved to Core
  (SharedUI accessibility fix). `HttpStoryReadService` stub added for `GetRandomBatchAsync`.
  Pre-existing `IModerationWriteService` DI failures (37 RazorComponents tests across 4 classes)
  fixed by adding `FakeModerationWriteService` + registration. Final counts: 429 Unit (all pass);
  428 RazorComponents (all pass, 0 failures); 329 Integration pass (7 pre-existing
  `ModerationServiceTests` DI failures unrelated to WU28). New tests: `DiscoveryDefaultsReadServiceTests`
  (5 Integration), `RandomBatchTests` (7 Integration), `SearchDesktopTests` (8 RazorComponents),
  `SearchMobileTests` (9 RazorComponents). See `audit/Discovery.md` F31 WU28 Stage note.
- **Tool:** opusplan (plan approved 2026-06-25). **Pointer:** `audit/Discovery.md` Features 31, 32.
  **Deps:** WU14, WU23, WU4.

### WU29 — Recommendations ✓ DONE (2026-06-23/24)
- **Cells:** 27/28/29/30 L2/L3/L3.5/L4/L5 (L5 includes integration test isolation overhaul);
  27 L6 (unique index). F30 L5 stays at 2 (attribution trigger deferred to WU26).
- **Done:** submission, display (Author Spotlight ≤5, RecommendationLike), attribution surface minted
  (trigger deferred), RecommendationStatusEnum added to Core. Integration test isolation overhaul
  (Respawn + IntegrationTestBase + SeedUserAsync GUID-suffix fix) unlocked reliable L5 verification
  for F7/16/17/18/19/23/24/25/26/27/28/29/42/43. See `audit/Recommendations.md` + `testing.md`.
- **L4 visual sign-off:** completed 2026-06-23 (manual).
- **Pointer:** `audit/Recommendations.md`.

### WU30 — Profiles + Theme selection — DONE ✓ (2026-06-24)
- **Cells:** 20/21/22 L2/L3/L3.5/L4 → Stage 5; 3 L3/L3.5 → Stage 5. L4 visual sign-off pending (human
  run at `/settings` and `/user/{id}`) — Stage 6 gate = human visual approval.
- **Completed:** `IUserSettingsService` self-edit exception (`GetMySettingsAsync`, `UpdateProfileAsync`,
  `UpdateReaderSettingsAsync`, `UpdatePrivacySettingsAsync`, `UpdateAuthorSettingsAsync`,
  `UpdateAppearanceAsync`, `UploadProfilePictureAsync`). `IUserProfileReadService`
  (`GetProfileHeaderAsync(userId, includePrivate)`, `GetProfileTextAsync`). `IThemeReadService.GetThemesAsync`.
  Candidate-id queries: `GetFavoriteStoryIdsAsync` (on `IUserStoryInteractionReadService`),
  `GetRecommendedStoryIdsByUserAsync` (on `IRecommendationReadService`). `IBlogPostReadService.GetByAuthorAsync`
  extended with `includeUnpublished` flag; `IsPublished` added to `BlogPostListingDto`.
  `SettingsPage.razor` at `/settings` + 5 injection-free sub-form components (`ProfileSettingsForm`,
  `ReaderSettingsForm`, `PrivacySettingsForm`, `AuthorSettingsForm`, `AppearanceSettingsForm`).
  `ProfilePage.razor` at `/user/{UserId:int}/{*Tab}` (dispatcher — banner-once, tab-payload-on-switch;
  own-vs-other `includePrivate`; device-branch to `ProfileDesktop`/`ProfileMobile`).
  `ProfileBanner` (avatar, tagline, stats, vouches, relationship actions, Edit Profile link).
  `ProfileDesktop`/`ProfileMobile` (tab body — Profile tab = bio + `CommentSection` UserProfile context;
  story tabs = `StoryDeck` + `ResultsFilterPanel`; Blog tab = paginated `BlogPostCard` list).
  `UserStatsBlock` leaf. `BlogPostCard` de-nested (Edit link sibling of title anchor; `IsOwner`/`EditHref`
  params; draft badge). `CommentSection` generalized to 4th context (UserProfile) —
  `ProfileUserId` param, `UserProfile` case in all switches. UserStats real-time counter wiring across
  8 write services (`FollowerCount`/`AuthorsFollowed`, `StoriesWritten`, `WordsWritten` ± delta,
  `CommentsWritten` ±1, `RecommendationsWritten`/`RecommendationsReceived`, `BlogPostsWritten` ±1,
  `GroupsJoined` ±1, `FavoritesOnStories`/`StoriesRead`/`StoriesInProgress`/`StoriesIgnored` via
  transition-delta).
- **Verified:** `dotnet build` green (1 pre-existing warning). `dotnet test`: 236 non-Group integration
  tests pass; 373 RazorComponents tests pass; 44 GroupServiceTests fail (all pre-existing WU32 issue —
  root cause at `CreateGroupAsync` line 47, unrelated to WU30 counter additions). Integration tests for
  WU30-specific paths (UserSettings round-trips, counter assertion, UserProfileComments) deferred to
  Phase 5. L4 visual sign-off pending (human).
- **Pointer:** `audit/Profiles.md`, `audit/Sprites.md` Feature 3 L3/L3.5. **Deps:** WU10, WU14, WU21, WU23.

### WU31 — Blog posts (profile only; Feature 56 deferred post-MVP) — DONE ✓ (2026-06-24)
- **Cells:** 35/36 L2/L3/L3.5 → Stage 5. L4 → Stage 1 (visual sign-off pending, same as WU13/WU24).
  Feature 56 stays Stage 2 (deferred post-MVP).
- **Completed:** Profile blog-post write + read (`ServerBlogPostReadService` / `ServerBlogPostWriteService`
  with two-query scalar split to work around EF Core 10 TPT + `IgnoreQueryFilters()` entity-materialization
  bug), likes, `CommentSection` generalized for blog-post context, `BlogPostPropertiesForm` +
  `BlogPostEditorPage` + `BlogPostPage` + `BlogPostCard`, content-rating filter on `BaseBlogPost`.
  `ExecuteDeleteAsync` on TPT base type replaced with raw SQL + CASCADE FK.
- **Verified:** `dotnet test` 691/691 green — Unit (205), Integration (215 incl. 20 BlogPostWriteServiceTests),
  RazorComponents (271 incl. 10 BlogPostPropertiesFormTests). L4 visual + server smoke still required.
- **Pointer:** `audit/BlogPosts.md`. **Deps:** WU6, WU20.

### WU31.5 — TPT denormalization retrofit (BlogPosts + Comments) — DONE ✓ (2026-06-24)
- **Cells:** F35/F36 L1/L2 + F23–F26 L1/L2 — momentarily reopened, returned to Stage 5 on green tests.
- **Completed:** (1) Discovery columns (`DateCreated`, `LastUpdatedDate`, `Rating`, `IsPublished`) moved
  from `BaseBlogPost` → `ProfileBlogPost`/`GroupBlogPost`. `DatePosted` moved from `BaseComment` →
  `ChapterComment`/`BlogPostComment`/`GroupComment`/`UserProfileComment`. (2) Named query filter
  removed from `BaseBlogPost`; content-rating ceiling checked via explicit `.Where(p => p.Rating <= max)`
  projection checks in `ServerBlogPostReadService`. (3) Two-query scalar split + raw-SQL delete
  removed; `GetByIdAsync`/`GetForEditAsync` now single projection on `ProfileBlogPosts`. Delete now
  uses change-tracker stub (`writeDb.Remove(new ProfileBlogPost { BlogPostId = id })`). (4) Migration
  `WU31_5_DenormalizeTptDiscoveryColumns` with manual data-copy SQL (base→child before drop).
  (5) Spec §4.3 denormalization technique corrected in `layer1-data-model.md`.
- **Verified:** `dotnet test` 691/691 green — Unit (205), Integration (215), RazorComponents (271).
  Content-rating projection path covered by existing `GetById_MaturePost_HiddenFromNonMatureViewer`,
  `GetById_MaturePost_VisibleToMatureViewer`, `GetById_MaturePost_VisibleToAuthorRegardlessOfMatureSetting`.
- **Pointer:** `audit/BlogPosts.md`, `audit/Comments.md`. **Deps:** WU31.

### WU31_5b — TPT phantom BaseComment FKs + integration-test DB wiring — DONE ✓ (2026-06-25)
- **Cells:** F23–F26 L1 (momentarily reopened, returned to Stage 5 on green tests);
  F38/39/40 L5 (2 → 5, unblocked by this fix).
- **Completed:**
  (1) Removed four phantom down-navigation properties from `BaseComment`
  (`BlogPostComment`, `ChapterComment`, `GroupComment`, `UserProfileComment`). These caused EF to
  produce backwards FK columns on `base_comments` (`{type}_comment_comment_id`), forming FK cycles
  that broke Respawn's topological sort and left `groups` rows alive between tests.
  Migration `WU31_5b_DropPhantomBaseCommentFKs` drops the 4 columns / 4 indexes / 4 FK constraints.
  Convention added to `canalave-conventions/layer1-data-model.md`.
  (2) Fixed `TestAppFactory` DB wiring: `ConfigureAppConfiguration` fires too late with
  `WebApplicationBuilder` — the connection string is read before the override lands. Rewrote to
  re-register both `DbContextOptions` in `ConfigureServices`. Documented in `testing.md`.
  (3) Fixed `ServerGroupWriteService.AddStoryAsync`: story lookup was missing
  `IgnoreQueryFilters(["ContentRating"])` — M-rated stories appeared not-found when `ShowMatureContent`
  was false, causing `AddStory_Tier2_…_Throws` to throw `KeyNotFoundException` instead of the
  expected `ContentRatingExceededException`.
  (4) Fixed `ServerRecommendationWriteService.SubmitAsync`: (a) `Select((int?)s.AuthorId)
  .FirstOrDefault()` confuses "story not found" with "story has null AuthorId" — fixed with
  anonymous-type projection; (b) unconditional `.Value` on nullable `storyAuthorId` crashes on
  authorless stories — fixed with `if (storyAuthorId.HasValue)`.
  (5) Fixed `GroupServiceTests.CreateGroup_Mature_PersistsCorrectRatingPair`: used `FindAsync`
  which applies the `GroupAudience` filter — Mature group returned null. Fixed with
  `IgnoreQueryFilters().FirstOrDefaultAsync(...)`.
- **Verified:** `dotnet test` → 298 integration / 414 unit / 397 RazorComponents = 1,109 total,
  all green.
- **Pointer:** `audit/Comments.md`, `audit/Groups.md`, `canalave-conventions/testing.md`. **Deps:** WU31.5.

### WU32 — Groups — DONE ✓ (2026-06-24)
- **Cells:** 38/39/40 L2/L3/L3.5/L4 → Stage 5.
- **Done:** Phase 0 doc-touch: settled rating model (AudienceRating vs MaxContentRating), GroupAudience
  named filter, membership/role model, group blog posts in scope, per-context comment pattern.
  Phase 1: `GroupRole`/`GroupAudienceType` enums, `GroupAudienceTypeMapper`, `Group.Rating → AudienceRating`
  rename, `DbSet<GroupComment>`, `GroupAudience` named filter, migration `WU32_Groups`.
  Phase 2: Full L2 services (Core contracts + Server impls + DI) — group CRUD, join/leave, rating
  waterfall, folder CRUD, group comments, group blog posts, notification fan-out.
  Phase 3: `GroupCard`, `GroupsPage`, `GroupPage` (dispatcher), `GroupDesktop`, `GroupMobile`,
  `GroupCreateEditPage`, `GroupBlogPostEditorPage`. `CommentSection.GroupId` + `CommentTarget.Group`.
  Phase 4: Unit (`GroupAudienceTypeMapperTests`, `GroupValidationsTests` — 22 tests),
  Integration (`GroupServiceTests` — 22 tests, DB-gated), RazorComponents (`GroupCardTests`,
  `CommentSectionGroupTests` — 15 tests).
  `dotnet build` green (0 errors); 513 non-integration tests pass (227 unit + 286 RazorComponents).
  **Pointer:** `audit/Groups.md`. **Deps:** WU14, WU6, WU20.

### WU33 — Notifications UI — DONE ✓ (2026-06-24)
- **Cells:** 42 L3/L3.5 → Stage 5, 43 L3/L3.5 → Stage 5. L4 stays Stage 1 (pending visual sign-off
  per WU8/WU13/WU23 precedent — Tailwind classes are present but not locked into Pattern Accumulation).
  F42 L2 additive enrichment (SourceUserName, TargetTitle, TargetUrl, GetTotalCountAsync) also verified.
- **Done:** Phase 0 doc-touch (layer2/layer3.5/layer4/cross-cutting skills + audit + forward_plan);
  Phase 1 L2 enrichment (NotificationFeedOrder enum, NotificationDto extended, INotificationReadService
  updated, ServerNotificationReadService two-pass batch enrichment); Phase 2 presentation atoms
  (NotificationCategoryVisuals.cs, NotificationPresenter.cs, NotificationItem.razor); Phase 3
  NotificationsPage.razor (by-date + by-category views, view toggle, sort toggle, mark-all, pagination);
  Phase 4 NotificationBell.razor + layout insertion (DesktopLayout + MobileLayout); Phase 5
  NotificationSettingsPage.razor (per-row immediate save, grouped by category); Phase 6 tests
  (Integration: 6 new WU33 tests — enrichment, total-count, ordering; Unit: 22+13 new tests in
  NotificationCategoryVisualsTests + NotificationPresenterTests; RazorComponents: pre-existing 308
  unchanged — notification RazorComponents tests deferred per plan's note on FakeNotificationWriteService).
- **Verified:** `dotnet build` 0 errors; `dotnet test` — Unit 391 ✓, RazorComponents 308 ✓, Integration
  notification tests 22/22 ✓ (GroupServiceTests failures are pre-existing, unrelated). Routes auto-
  discovered from SharedUI assembly — `/notifications` and `/notifications/settings` wired. Visual
  sign-off pending (bell flyout, page view toggle, settings toggles).
- **Pointer:** `audit/Notifications.md`. **Deps:** WU22.

### WU34 — Moderation (reporting, queue, actions, approval workflow, related notifications) DONE ✓ (2026-06-25)
- **Cells:** 46 L2/L3-Logic/L3.5/L4; 47 L2/L3-Logic/L3.5/L4; 48 L2/L3/L3.5/L4.
  Momentary L1 reopen (→ Stage 5 on green, precedent WU31.5): `Report.ReportedEntityId int→long`;
  `ReportedEntityType` +`Message`; soft-delete columns on Story/BaseComment/BaseBlogPost/Recommendation
  (`IsHidden`, `DateModeratedRemoved`, `ModerationRemovalReason`); `User.AccountStatus` +
  `SuspendedUntilUtc` + `ActiveReportCount`; `NotificationType` seed for `StoryApproved` (type 75).
  Notification semantic methods are additive to Features 41/42 (no stage change on those cells).
- **Settled decisions (do not revisit in opusplan):**
  1. **Soft-delete default, narrow hard-delete escape hatch.** Normal mod action = `IsHidden = true`
     (reversible, author notified with reason). Separate explicit "illegal content" path hard-deletes
     (CSAM/piracy only). Rationale: archive mission — mistakes must be reversible; authors are owed the
     reason. This is the opposite default from attention platforms. See `content-safety.md` "Moderation Model."
  2. **No auto-hide.** `ActiveReportCount` drives mod-only queue ordering (most-reported first) and
     an inline badge — never an automatic action. Deliberations' "3 distinct reporters in 24h" threshold
     is dropped. Report counts are mod-only (no public display — that gamifies reporting and enables
     brigading). See `content-safety.md` "Moderation Model."
  3. **Account actions: model state + notify now; login enforcement staged.** `AccountStatus` enum
     (Active/Warned/Suspended/Banned — no Shadowbanned) + `SuspendedUntilUtc` on `User`. Actions set
     status, record on `Report`, and notify. Login-blocking enforcement is a follow-up slice (see
     deferred-follow-up note below). **Shadowban rejected permanently** — deception-as-moderation,
     contradicts §13 transparency philosophy.
  4. **`User.ActiveReportCount` added** (symmetric with other authored-content targets; uniform
     `AdjustActiveReportCount(type, id, delta)` switch; skips `PrivateMessage`).
  5. **Reportable targets widen to `long`.** Set = Story, User, Comment, BlogPost, Recommendation,
     PrivateMessage. `ReportedEntityType` +`Message = 5`.
  6. **Notification dedup-key fix.** `CreateCoreAsync` currently dedups on `(type, sourceUserId, !IsRead)`;
     widen key to include `RelatedEntityId`. Regression-test follow/vouch/group notification suites.
  7. **`StoryApproved` notification type added** (`NotificationTypeEnum.StoryApproved = 75`,
     category `YourStories=2`, `KindFor → Story`). Seeded `NotificationType` row + migration.
  8. **`/mod/submissions` tabbed shell; rec-approval wiring deferred.** Recs currently write as
     `Approved` directly. Build the tab shell; import-verification tab drops in with WU39. Do not change
     the rec write-path in WU34.
- **Superseded (from `Moderation_And_Reporting_Deliberations.md` — do not resurrect):**
  `RequestedStatusId` (use `StoryDetail.PostApprovalStatus`); `Author` role (SiteRoles =
  User/Moderator/Admin); single `IModerationService` (CQRS-lite split); Shadowban; 3-reporter
  auto-hide; SQL trigger on `FavoriteCount`; `DefaultCommentModeration`/`AllowGuestComments` in
  `AuthorSettings`; single "Moderation & Safety" notification category (live: Warnings=7, YourReports=8).
- **Build phases (for opusplan):**
  - Phase 0 — Doc-touch: `forward_plan.md` + `content-safety.md` + `layer2-services.md` + audit files.
  - Phase 1 — Cluster relocation + schema: move `Report`/`ReportReason`/`ReportStatus` → `Core/Moderation/`
    (StoryImport stays in `Core/Models/` for WU39). One migration: widen `ReportedEntityId`; add soft-delete
    columns; add `User` columns; add `Report(ReportStatusId)` + `Report(ReportedEntityType, ReportedEntityId)`
    indexes; seed `StoryApproved` `NotificationType`. Add `"ModeratedVisibility"` named query filters on
    four content entities; mod/author reads use `IgnoreQueryFilters`.
  - Phase 2 — Notifications: dedup-key fix in `CreateCoreAsync`; add semantic methods
    (`NotifyReportReceivedAsync`, `NotifyReportResolvedAsync`, `NotifyReportResolvedNoActionAsync`,
    `NotifyContentRemovedAsync`, `NotifyStoryRejectedAsync`, `NotifyStoryApprovedAsync`,
    `NotifyAccountWarningAsync`/`Suspended`/`Banned`); `KindFor` branch for `StoryApproved → Story`.
  - Phase 3 — Moderation services: `Core/Moderation/` DTOs (`SubmitReportRequest`, `ReportReasonDto`,
    `ReportQueueItemDto`, `ModeratedTargetDto`) + `IModerationReadService`/`IModerationWriteService`;
    `Server/Moderation/ServerModerationReadService` + `ServerModerationWriteService`. DI in `Program.cs`.
  - Phase 4 — `ReportDialog` + entry points: reusable `ReportDialog.razor` (reuses `ConfirmDialog` pattern,
    WU9); add report affordances on StoryCard, UserCard, CommentItem, BlogPostCard, recommendation cards,
    message thread. 46 L5 stays Stage 2 (public, can be WASM later).
  - Phase 5 — `/mod/reports` queue: `@page "/mod/reports"`, `[Authorize(Policy="RequireModerator")]`,
    two-pass `BatchLoadEntitiesAsync` pattern for polymorphic target label + deep-link, ordered by
    `ActiveReportCount` desc.
  - Phase 6 — Moderator actions: resolve-no-action / resolve-action-taken / claim logic; content
    removal `ApplyRemoval(type, id, reason)` switch (soft-hide default; explicit hard-delete variant);
    account actions set `AccountStatus`/`SuspendedUntilUtc` + notify (no login enforcement yet).
  - Phase 7 — `/mod/submissions` + `/mod/users`: submissions tabbed shell; approve → `StoryStatusId =
    PostApprovalStatus` + `NotifyStoryApprovedAsync`; reject → `Rejected` + reason + `NotifyStoryRejectedAsync`.
    Users: lookup + report history + warn/suspend/ban controls. Both server-rendered, mod-gated.
  - Phase 8 — Tests: Unit (target-type allow-set, `AdjustActiveReportCount` switch, dedup-key fix);
    Integration (submit increments + ReportReceived; resolve decrements + notifies; dedup-key regression;
    approve/reject; soft-hide visibility filter; non-mod → 403); bUnit (ReportDialog, ModReportsPage,
    submissions tab shell).
- **Ordering:** 0 → 1 → 2 → 3 → 4 → 6 → 5 → 7 → 8 (woven).
- **Tool:** opusplan. **Pointer:** `audit/Moderation.md` Features 46/47/48; `content-safety.md` "Moderation
  Model." **Deps:** WU9, WU12, WU20, WU22, WU24, WU29, WU31, WU35.

### WU35 — Messaging — DONE ✓ (2026-06-24)
- **Cells:** 49 L2/L3/L3.5/L4 → Stage 5.
- **Do:** `/messages/{ConversationId?}`, three-table model, stateless request/response (no SignalR —
  the spec's "real-time" framing was reversed for MVP, see `cross-cutting.md` "Private Messaging
  Architecture"; hardened to a permanent decision 2026-07-07), EditorView composition,
  `LastReadTimestamp`, `AllowPrivateMessages` gate. **Tool:** opusplan.
  **Pointer:** `audit/Messaging.md`. **Deps:** WU6.

### WU36 — Badges — DONE ✓ (2026-06-25)
- **Cells:** 50 L2→5 / L3→5 / L3.5→5 (L4 stays 1 — visual sign-off pending; L1 was already 5).
- **Did:** `IBadgeReadService` / `IBadgeWriteService` + `EarnedBadgeDto`; `ServerBadgeReadService` /
  `ServerBadgeWriteService`; DI registration; `UserStat.RecommendationSuccessesEarned` column +
  `SiteBadges.RecommenderSilver` seed (migration `20260625234308_WU36_Badges`); award trigger wired
  in `ServerRecommendationWriteService.RecordSuccessAsync` (anti-self-farm, best-effort, idempotent);
  display-projection fix at all 6 card-producer sites; `BadgeSettingsForm.razor` + `SettingsPage.razor`
  wiring. Tests: `BadgeServiceTests` (11 Integration) + 6 Tastemaker award-chain tests in
  `RecommendationWriteServiceTests` + `BadgeSettingsFormTests` (14 RazorComponents). All WU36
  tests green; pre-existing `ModerationServiceTests` DI failures (7) unrelated to this WU.
  **Pointer:** `audit/Badges.md`. **Deps:** WU30.

### WU37 — Story Tagging — structured authoring (Feature 12) — DONE ✓ (2026-06-25)
- **Cells:** 12 L2/L3/L3.5 → Stage 5 (L4 → Stage 1, visual sign-off pending). L1 additions
  (`AllowSettingDetails`, `StoryCharacterPairing` rename, `UNIQUE(SettingDetail)`, new
  `StoryCharacterPairingMember`) noted against the existing Stage-5 L1 cell.
- **Scope note (2026-06-25):** Features 9 (Series), 10 (story↔story Relationships), 15 (Saved Tag
  Selections) were originally bundled here; carved to WU41/WU42/WU43 — each is independently
  L1-settled, greenfield from L2, with no design coupling to Feature 12.
- **Architecture:** shared catalog / differentiated per-story association.
  - Genre/ContentWarning/CrossoverFandom → `StoryTag` (flat)
  - Setting → `StoryTag` + optional `SettingDetail` side-row
  - Character → `StoryCharacter` (replaces `StoryTag`; OC payload + pairing anchor)
  - Pairing (ship) → `StoryCharacterPairing` + `StoryCharacterPairingMember` join (renamed from
    `StoryCharacterRelationship`; promotes the only implicit shadow join to first-class entity)
  - `TagTypeEnum.Relationship` removed; a pairing is not a catalog tag.
  - `ApplyFilters` partitions included/excluded ids by `TagTypeId`: Character ids →
    `s.StoryCharacters.Any(...)`, all others → `s.StoryTags.Any(...)`.
    (See `audit/Discovery.md` Feature 31.)
- **DONE ✓ (2026-06-25):** Phase 0 doc-touch → Phase 1 L1 migration (`WU37_StructuredStoryTagging`)
  → Phase 2 L2 write/read + `ApplyFilters` character branch → Phase 3 L3/L3.5 `StoryPropertiesForm`
  rebuild (`CharacterEntry`, `SettingEntry`, `PairingBuilder`) + `StoryEditorPage` mapping
  → Phase 5 integration tests (`StoryTaggingTests.cs`, 12 tests) + RazorComponents tests
  (`CharacterEntryTests.cs` 8 tests, `PairingBuilderTests.cs` 5 tests) → Phase 6 view-page display
  (`StoryDetailsDTO` extended, `GetStoryByIdAsync` projection, `StoryDesktop`/`StoryMobile` OC names
  + ship pills). L4 stays Stage 1 pending human visual sign-off.
  Final: 348 Integration + 440 RazorComponents + 434 Unit = **1222 tests green**.
- **Enforcement:** service-layer only (`CanSave()` / `StoryValidationException`); no DB trigger.
- **Tool:** opusplan. **Pointer:** `audit/Tags.md` Feature 12; `audit/Discovery.md` Feature 31
  (ApplyFilters branch). **Deps:** WU11, WU24, WU27.5.

### WU37.5 — Pre-Integration Cleanup — DONE ✓ (2026-06-26)
- **Cells:** All touched cells were already Stage 5; this is a code-quality / naming cleanup, not a
  stage change. Cells that were re-verified clean: F3 L2 (Sprites), F12 L1/L2 (Tags/Lookups enum),
  F46/F47/F48 L2/L3-Logic/L3.5 (Moderation). No stage number changes to `status.md`.
- **Done (5 phases):**
  - **Phase 0 doc prep:** `forward_plan.md` "Decisions that need you" row added for deferred
    non-story rating-route scoping (blog posts, recs, rated comments).
  - **Phase 1 enum cleanup:** Deleted vestigial `CharacterRelationshipType { Romantic, Platonic }`
    enum (zero references; live type is `CharacterPairingType`). Changed `CharacterPairingType : byte`
    → `: short` (convention alignment; no migration — Npgsql maps both to `smallint`). Removed dead
    placeholder comment in `ModelEnums.cs`.
  - **Phase 2 moderation:** Renamed soft-delete columns on Story/BaseComment/BaseBlogPost/Recommendation:
    `IsHidden → IsTakenDown`, `DateModeratedRemoved → TakedownDate`, `ModerationRemovalReason →
    TakedownReason`. Added `IModeratableContent` interface (`Core/Moderation/`) implemented by all four
    roots. Collapsed three-switch dispatch in `ServerModerationWriteService` → single `LoadModeratableAsync`
    loader + interface mutation. Renamed EF named filter key `"ModeratedVisibility" → "IsTakenDown"`.
    Replaced all parameterless `IgnoreQueryFilters()` in moderation services with by-name
    `IgnoreQueryFilters(["IsTakenDown"])` so `ContentRating`/`GroupAudience` stay live — a moderator's
    rating reach equals their `ShowMatureContent`. Report-queue stitch drops rows filtered by `ContentRating`
    instead of emitting `[Type #Id]` placeholder. Removed no-op `IgnoreQueryFilters()` on `ReadDb.Reports`.
    Updated all call sites in `ModerationServiceTests`, `GroupServiceTests`, `BlogPostWriteServiceTests`.
    EF migrations: `PreIntegrationCleanup_TakedownColumns` (ApplicationDbContext + ReadOnlyApplicationDbContext).
  - **Phase 3 sprites:** `ServerSpriteReadService` rewritten as singleton with startup existence cache
    (enumerates `wwwroot/sprites/themes/*/{animated,static}/` into HashSets at construction; O(1) lookups,
    no `File.Exists` per call). DI changed `AddScoped → AddSingleton`. `SpriteReadServiceExtensions.cs`
    added (`GetSpriteUrl(ISpriteReadService, IActiveUserContext, string)` extension). Five call sites
    updated in `ServerStoryReadService` and `ServerTagReadService`. Unit tests restructured so
    `BuildSut()` is called after `CreateSpriteFile()` (startup cache requires files to exist at construction).
  - **Phase 4 home placeholder:** Replaced `HomeDesktop.razor` WU13 harness (hardcoded StoryCard sample
    data) and `HomeMobile.razor` stub with honest minimal placeholders. `<DevLoginBar />` retained.
  - **Phase 5 docs:** `layer2-services.md`, `layer1-data-model.md` enum table,
    `audit/Moderation.md`, `audit/Sprites.md`, `audit/Tags.md` all updated. `forward_plan.md`
    "Decisions that need you" row added.
- **Migrations:** `PreIntegrationCleanup_TakedownColumns` (column renames on 4 tables);
  `PreIntegrationCleanup_PairingTypeShort` (empty — snapshot sync for `CharacterPairingType` type change).
  Both ApplicationDbContext + ReadOnlyApplicationDbContext.
- **Verified (2026-06-26):** `dotnet build` 0 errors; `dotnet test` 434 Unit + 440 RazorComponents +
  348 Integration = **1222 tests green**.
- **Tool:** Opus (holistic pre-integration audit + implementation). **Pointer:** `audit/Moderation.md`
  Features 46/47/48; `audit/Sprites.md` Feature 3 L2; `audit/Tags.md` Shared Context.

### WU38 — Sprite System Redesign + Existence Validation — DONE ✓ (2026-06-27)
- **Cells:** 3 L5 → Stage 5 (resolved Stage-4 divergence; prior: Server startup-scan cache vs. Client
  optimistic build; now: single `OptimisticSpriteReadService` in Core, registered on both). All other
  touched cells (3 L1/L2/L3.5, 11 L2/L3, 4 L2, 20 L2) were already Stage 5 — corrections within
  Stage 5, no regression.
- **Done (9 phases):**
  - **Phase 0 (doc prep, moment 1):** Skill files updated — `layer2-services.md` (sprite resolution
    moves to render time; `ISpriteReadService` allowed in SharedUI; `ISpriteAssetProbe` server-only),
    `render-and-layout.md` (ThemeContext cascading provider + SpriteBaseUrl seam),
    `layer1-data-model.md` (`Theme.Slug` convention). Audit files (`Sprites.md`, `Tags.md`,
    `Stories.md`, `ImageStorage.md`) updated with settled decisions. `forward_plan.md` moved sprite
    redesign to Resolved.
  - **Phase 1:** `Theme.Slug` column added (`[Required][MaxLength(64)]`, unique index). Migration
    `WU38_ThemeSlug` on both DbContexts; seed updated `{ Name="Pokémon", Slug="pokemon" }`.
  - **Phase 2:** Claims carry slug — `ApplicationUserClaimsPrincipalFactory` bakes `Theme.Slug`
    (not `.Name`) into the `canalave:theme` claim; default changed `"Pokémon"` → `"pokemon"` in
    `ServerActiveUserContext`, `ApplicationDbContextFactory`, and `IActiveUserContext` XML doc.
  - **Phase 3:** `OptimisticSpriteReadService` (Core/Sprites/) — pure string builder, singleton on
    both Server and Client. `SpriteBaseUrl` config seam (`Sprites:BaseUrl`, default
    `/sprites/themes`). Deleted `ServerSpriteReadService`, `SpriteReadServiceExtensions`, and
    `Client/OptimisticSpriteService`.
  - **Phase 4:** `ThemeContext(string Slug, bool PrefersAnimated)` record (Core/Sprites/).
    `ThemeContextProvider.razor` (Server) reads claims from cascaded `AuthenticationState`;
    nested inside `CascadingAuthenticationState` in `Routes.razor`.
  - **Phase 5:** `TagChipDto.SpriteUrl` renamed → `SpriteIdentifier`. `ServerTagReadService` and
    `ServerStoryReadService` drop `ISpriteReadService` dep; project raw `SpriteIdentifier`.
    `TagChip`, `TagSelector`, `CharacterEntry` inject `ISpriteReadService` + take `[CascadingParameter]
    ThemeContext`; resolve URL at render time with `onerror` fallback chain. `sprite-fallback.js`
    helper (SharedUI wwwroot); script tag added to `App.razor`.
  - **Phase 6:** `ISpriteAssetProbe` (Core) + `LocalSpriteAssetProbe` (Server, `File.Exists`
    against static PNG). `TagSaveResult(int TagId, string? SpriteWarning)` record. `ITagWriteService`
    signatures updated. `ServerTagWriteService` probes default theme; surfaces non-blocking warning.
    `TagDirectoryPage` shows amber advisory.
  - **Phase 7:** `IImageStorageService.DeleteAsync` callers added: `ServerStoryWriteService.UpdateStoryAsync`
    (best-effort cover cleanup) + `ServerUserSettingsService.UploadProfilePictureAsync` (best-effort
    avatar cleanup).
  - **Phase 8:** `wwwroot/sprites/themes/pokemon/unknown.png` (1×1 transparent PNG fallback).
    `.gitignore` updated — `static/` and `animated/` subdirs gitignored; `unknown.png` at theme root
    committed so the fallback renders correctly without provisioning a full pack.
- **Test backfill (all tiers):**
  - Unit — `SpriteReadServiceTests` rewritten for `OptimisticSpriteReadService` (5 tests);
    `LocalSpriteAssetProbeTests` new (4 tests).
  - RazorComponents — `TagChipTests` rewritten for new architecture (11 tests, `AddCascadingValue`
    + `ISpriteReadService` registration). All RazorComponents test contexts that render
    `TagChip`/`TagSelector`/`CharacterEntry` updated to register `ISpriteReadService`.
  - Integration — `TagWriteServiceTests` unwraps `TagSaveResult.TagId` at all call sites;
    `TagReadServiceTests` uses `SpriteIdentifier` (not `SpriteUrl`).
- **Verified (2026-06-27):** `dotnet build` 0 errors; `dotnet test` 437 Unit + 443 RazorComponents +
  348 Integration = **1228 tests green**.
- **Tool:** Opus (multi-phase holistic redesign). **Pointer:** `audit/Sprites.md` Feature 3,
  `audit/Tags.md` Feature 11 WU38 note, `audit/ImageStorage.md` WU38 note.
- **Deps:** WU27.5.

### WU38a — Account Deletion UI + Account-Status Login Enforcement — DONE ✓ (2026-07-11)
- **Cells:** 52 L3/L3.5 stay Stage 5 (deletion UI + service already landed in WU1 — see
  `audit/Identity.md` Feature 52; that reconciliation is not reopened). **52 L4-Style: 1 → 5**
  (the one genuinely open Feature-52 cell, closed by this unit). 1 L2/L3-Logic/L3.5 (login
  enforcement + Warned banner, additive to Identity & Auth) and 47 L2 (stamp-bump, additive to
  Moderation Queue & Actions) stay Stage 5, re-verified.
- **Direction settled (2026-07-11, do not revisit):** the workplan's original "52 L3/L3.5" framing
  was stale doc-drift — those cells were already Stage 5 from WU1, browser-verified 2026-07-01, and
  tokenized by the 2026-07-10 design sweep. WU38a's real scope, decided with the user: (A) the
  deletion page keeps its existing password-confirm form and gets a delete-vs-anonymize
  consequence disclosure only — **no** `ConfirmDialog`/interactive-island rework (Identity pages
  are static SSR) — plus a goodbye page fixing the known post-deletion 401 flash, then the standing
  L4-Style visual sign-off; (B) the `workplan.md` deferred "Account-status login enforcement"
  follow-up (below) is **folded into this unit**, built on WU34's `AccountStatusEnum`/
  `SuspendedUntilUtc` state — a single `CanalaveSignInManager.CanSignInAsync` choke point blocking
  every sign-in path, **plus** a security-stamp bump on Suspend/Ban (not Warn) so already-open
  sessions die via the existing 30-min revalidation, plus a claims-baked Warned banner in layout
  chrome. No grace period / soft-delete / type-to-confirm — the spec (`§Delete Policy Summary`,
  `§5.30.9`) doesn't call for any. See `canalave-conventions/security.md` "Account-Status
  Enforcement" for the mechanism and `audit/Identity.md`/`audit/Moderation.md` Feature 47 for the
  settled-vs-open notes.
- **Done:** (A) `DeletePersonalData.razor` disclosure + new anonymous `AccountDeleted.razor`
  goodbye page (fixes the 401 flash). (B) `CanalaveSignInManager` (`CanSignInAsync` override,
  registered via `.AddSignInManager<>()`) blocks Banned/currently-Suspended sign-ins;
  `Login.razor` surfaces the specific reason; `ApplyAccountActionAsync` bumps the security stamp
  on Suspend/Ban (not Warn); `ApplicationUserClaimsPrincipalFactory` bakes
  `canalave:account_status`; new `AccountStatusBanner` (SharedUI/Layout) renders the Warned banner
  in `DesktopLayout`/`MobileLayout`.
- **Verified:** `dotnet build` 0 warnings (8 projects); `dotnet test` 1483/1483 green (530 Unit /
  517 RazorComponents / 436 Integration) — new `AccountStatusEnforcementTests` (Integration) +
  `AccountStatusBannerTests` (RazorComponents); mutation-sanity confirmed (inverted Banned branch →
  4 tests failed, reverted). Manual/browser band: real password login + delete of a throwaway
  registered user → goodbye page, no 401 flash, `psql`-confirmed row gone; `ReaderGamma` fixture
  driven through real login POSTs at Suspended-future/Banned/Warned (state restored to Active
  after) — blocked with the specific reason message in the first two cases, banner rendered live
  in the third. Detail in `audit/Identity.md` WU38a Stage note, `audit/Moderation.md` Feature 47
  WU38a Stage note.
- **Tool:** opusplan. **Pointer:** `audit/Identity.md`, `audit/Moderation.md` Feature 47. **Deps:** WU25.

### WU38b — View Count — DONE ✓ (superseded by WU-SignalBuffering, 2026-07-06)
- **Cells:** 45 L2/L3 — originally scoped as view-count MVP direct increment + first client ping.
  Shipped instead as part of WU-SignalBuffering's signal-buffer pattern (see that block below):
  `ViewCountBuffer`/`ViewCountFlusher`/`ViewCountFlushWorker` batching into `daily_story_stats`,
  not a direct increment. **Pointer:** `audit/Stories.md` Feature 45.
- **L5** (WASM view-ping endpoint) remains Stage 2, deferred to the global WASM interactivity
  flip — not WU38b-specific, not pulled forward here.

### WU38c — Export (six formats) — DONE ✓ (2026-07-11)
- **Cells:** 54 L2 → Stage 5; 54 L4.5 → Stage 5. (L3/L3.5 stay N/A — the trigger is anchor links
  in Stories surfaces, a light Feature-5 touch recorded in `audit/Stories.md`.)
- **Done (scope expanded same day from "epub/pdf"):** EPUB (zero-dep ZipArchive, OCF-correct) /
  PDF (QuestPDF, Community license in `PdfWriter`'s static ctor) / HTML / TXT / Markdown / DOCX
  (Open XML SDK, real heading styles + hyperlink relationships + numbering part) behind
  `ExportFormat` + per-format writers over one shared AngleSharp DOM walk;
  `GET /api/stories/{id}/export/{format}` plain-anchor download (bypasses the circuit —
  `layer2-services.md` §"File Downloads Bypass the Circuit"); "export = what you can read"
  (read services' rating ceiling is the only gate); additive `GetChaptersForExportAsync` on
  Chapters; `StoryDownloadLinks` leaf on story page + StoryCard Download submenu (dead
  `OnDownload` EventCallback removed). New packages: QuestPDF 2026.7.1, DocumentFormat.OpenXml
  3.5.1, AngleSharp pinned 0.17.1 (HtmlSanitizer hard constraint — csproj note).
- **Verified:** `dotnet test` green — Unit `ExportWritersTests` (9), Integration
  `ExportServiceTests` (16, incl. mature-gate both directions + attachment headers + 404s),
  RazorComponents `StoryCardTests` update. Live curl: all six formats for seed story 1, correct
  types/signatures/slug filename. Detail: `audit/Export.md` Stage-5 note.
- **Tool:** opusplan. **Pointer:** `audit/Export.md`. **Deps:** WU25.

### WU38d — Chapter Import (file ingestion) + "Also posted on" external links — DONE ✓ (2026-07-11)
- **Cells:** 63 L2/L3/L3.5/L4/L4.5 → Stage 5 (new feature row — `audit/Import.md`); 53 L1 → Stage
  5 (remodel migrated); the author-facing slice of 53 L2/L3/L3.5 shipped (cells stay 2 — the mod
  verification half is WU39's; split documented in `audit/Moderation.md` F53).
- **Done:** five explicit import modes (into-editor / as-version / file-per-chapter /
  one-doc-auto-detect / EPUB) over one backend — readers (Mammoth DOCX incl. working
  `br[type='page'] => hr` page-break map, VersOne.Epub, AngleSharp HTML, TXT, Markdig MD; PDF
  deferred) → `ImportHtmlNormalizer` (maps toward the allowlist so the sanitizer's
  drop-with-children default can't silently delete text; counts images/tables lost) → sanitizer
  per draft (trust boundary) → suggest-then-refine `ChapterSplitter` (in-memory re-split, no
  re-upload) → `ImportReviewPanel` (rename/merge/drop/reorder/preview) → existing
  `IChapterWriteService` (unpublished drafts). Feature-53 reframe shipped: `StoryImport` →
  `StoryExternalLink` (many per story) + seeded `ExternalPlatform` lookup (deliberately not an
  enum), migration `WU38d_StoryExternalLinks`, write-service sync (URL edit resets verification),
  paste-a-URL platform auto-detect, story-page row (after chapters, before recs; checkmark only
  when Verified). New packages: Mammoth 1.11.0, VersOne.Epub 3.3.6, Markdig 1.3.2.
- **Verified:** `dotnet test` 1600 green (568 Unit + 546 RazorComponents + 486 Integration; +179
  this combined WU). Unit `ContentImportTests` (18 — **export→import round-trips across all five
  formats**, splitter, normalizer, guards); Integration `ImportCommitTests` +
  `StoryExternalLinkTests` (8); RazorComponents `ImportReviewPanelTests` (8) +
  `StoryExternalLinksRowTests` (4, incl. the settled placement assertion). Browser (real circuit):
  mode 4 with a WU38c-exported DOCX end-to-end (split → delimiter switch → commit →
  psql-confirmed drafts), mode 1 into Quill, links flow with live AO3 auto-detect + verified-flip
  checkmark. `external_platforms` added to Respawn's TablesToIgnore (seeded lookup). Outstanding
  manual item: paste-from-Word fidelity (needs real Word; recorded in `audit/Import.md`).
- **Tool:** opusplan. **Pointer:** `audit/Import.md` + `audit/Moderation.md` Feature 53.
  **Deps:** WU38c (round-trip test fixtures), WU25.

### WU40 — Manual Tree Search (Feature 33) — DONE ✓ (2026-07-12)
- **Cells:** 33 L2/L3-Logic/L3.5 `2→5`; 33 L4.5 `1→5` (behavioral browser verification).
  L4-Style stays 1 (visual sign-off pending, standing precedent); L5 stays 2 (rides the
  `InteractiveAuto` flip); L6 stays 2 (pivots ride existing indexes; R4 measurement deferred).
  Also touched Feature 59's frozen L3.5 cell (additive de-anonymization fix to
  `TreeSearchResultBadge` — hydrated PathHops with usernames/titles — cell number unaffected).
- **Did:** all phases complete — Phase 0 doc sweep (privacy-model correction across 6 docs;
  `allow_discovery_consent` deleted as never-implemented), Phase 1 HTML mock (4 iterations with
  Brian; final: 2D top-down tidy-tree, per-(edge,direction) toggles everywhere, Deep Dive
  click-auto-adds + floating panel, compound rec rows), Pinned Story (`User.PinnedStoryId`,
  migration `WU40_PinnedStory`, AuthorSettings picker + server gate),
  `IManualTreeSearchReadService`/`ServerManualTreeSearchReadService` (one call per pivot,
  paged sections, family flag-composition), shared tree canvas + Explore/DeepDive tabs +
  `manual-tree-search.js` (gestures + localStorage IDs-only persistence w/ rehydration prune),
  three-tab integration. Suite 1,829 green (647 U / 574 I / 608 RC); E2E browser pass incl. the
  anti-bounce guard on real data. Detail: `audit/Discovery.md` F33 WU40 Stage note.
- **Deferred follow-up (not yet sequenced):** Pinned Story mart/Automatic-tab integration — a
  7th UNION arm in the frozen `DiscoveryMartSchema` + auto-tab chain-of-trust membership
  (reopens F59/F60); candidate-pane tag/interaction filter axes.
- **Direction settled 2026-07-12 (supersedes the WU28-Phase-0-era note in its entirety — the "four
  clean edges" / `allow_discovery_consent` claims below were stale and are corrected, not
  preserved):** manual tree search is **two distinct interactive paradigms** — **Explore**
  (two-pane: persistent client-curated tree + stateless candidate-results pane, all edges, section
  model grouped by underlying table) and **Deep Dive** (full-screen pannable tree + node flyout,
  restricted to the four edge×direction pairs bounded to ≤1/≤5) — plus **Automatic**, three
  top-level tabs total (diverges from spec §5.26's literal "two tabs," deliberate). Stateless pivot
  over live tables, unchanged (not the mart). Distinct graph/node visualization — **NOT
  `StoryDeck`**. Privacy model corrected: manual excludes hidden favorites, so every edge it
  exposes is genuinely public — nodes render real, clickable identity; the old "never reveals
  identity" / `allow_discovery_consent` claims were wrong (the latter never existed in code) and
  are removed. New edge, **Pinned Story** (`User.PinnedStoryId`, cap 1) — the missing 1:1 connector
  that lets the Author Spotlight chain self-sustain in Deep Dive, mirroring how `AuthoredBy`
  already does this for Hidden Gem. Corroborated by original deliberations' §2 stateless-fresh-
  search and §3 hidden-gem chain-of-trust. Full design, edge×direction boundedness table, section
  model, and service-layer gap analysis: `audit/Discovery.md` Feature 33.
- **Sequencing note — Pinned Story is manual-only in WU40.** The mart/Automatic-tab integration (a
  7th UNION arm in the frozen, Stage-5 `DiscoveryMartSchema`, reopening Feature 59/60) is
  deliberately deferred to a future work-unit, not yet numbered — flagged here so it isn't lost.
- **Process note:** WU40 opens with a Phase 0 doc-touch sweep (this entry, `audit/Discovery.md`,
  `layer3.5-structure.md`) and a throwaway HTML/CSS/JS mock (Phase 1) with an explicit
  user-review checkpoint before any real Razor/service/schema code — see the plan file for the
  full phase breakdown if resuming this work-unit.
- **Tool:** opusplan. **Pointer:** `audit/Discovery.md` Feature 33. **Deps:** WU14, WU23, WU4, WU25,
  WU44 (shares the `TreeSearchPage` shell and the `TreeSearchResultBadge` fix).

### WU39 — External Link Verification (mod workflow) *(re-minted 2026-07-11; was "Story Import & Verification")*
- **Cells:** the mod-workflow remainder of 53 L2/L3/L3.5/L4 (the author-facing half — link
  editing, story-page display, `StoryExternalLink`/`ExternalPlatform` remodel — moved into WU38d;
  file-format content ingestion became Feature 63, also WU38d).
- **Do:** extend the WU34 `/mod/submissions` tabbed shell with a **link-verification** tab:
  moderator reviews `Unverified` `StoryExternalLink` rows and flips `VerificationStatus`
  (`Verified` → the story page's "Also posted on" checkmark appears automatically). Open question
  owned here: the two-way-link mechanism (site publishes a verifiable token the author puts on
  the source page, vs. purely manual review). The old "route the story into `PendingApproval`"
  step is dropped — links don't gate story approval (Feature 48 untouched); verification is
  per-link, display-only. Per-platform verification properties go on the `ExternalPlatform`
  lookup as columns, not code branches.
- **Tool:** opusplan. **Pointer:** `audit/Moderation.md` Feature 53. **Deps:** WU34, WU38d.

> **Account-status login enforcement — folded into WU38a (2026-07-11), no longer deferred.** Was:
> "block Suspended (until `SuspendedUntilUtc`) / Banned users at login and surface the Warned banner
> in layout chrome; WU34 ships the `AccountStatus` state + notifications it builds on; enforcement
> is a security-surface slice to append as its own WU when scheduled (candidate: alongside WU38
> account-deletion UI)." See WU38a above for the settled mechanism and
> `canalave-conventions/security.md` "Account-Status Enforcement".

### WU41 — Series (Feature 9) — DONE ✓ (2026-07-11)
- **Cells:** 9 L2/L3-Logic/L3.5-Structure/L4.5-Browser → Stage 5. L4-Style stays Stage 1 (pending
  visual/token sign-off, per WU8/WU13/WU23/WU28/WU37 precedent — L4.5 verified it's *usable*, not
  polished). L5 stays Stage 2 (rides the future site-wide WASM flip). L1 was already Stage 5
  (`Series`/`SeriesEntry`); relocated `Core/Models/` → `Core/Series/` cluster (namespace unchanged).
- **Settled decisions (2026-07-11, Doc-Touch moment 1, before the build — full detail in
  `audit/Stories.md` Feature 9):** a series holds only the owner's own stories; membership is
  managed on a dedicated `/series/{id}/edit` page (not the story editor); browse surfaces are a
  public per-series page + a profile Series tab + an owner "My Series" list (no global directory);
  the story page shows a "Part of series X — Part N of M" box with Prev/Next in-series nav; a story
  may belong to more than one series (the existing `SeriesEntry` PK already permits it, no L1
  change); `StorySeriesMembershipDto`'s Position/Count/Prev/Next are computed over viewer-visible
  members only (an explicit join through `Story`, not a raw `SeriesEntry` count) so they never
  expose or link to a story the viewer can't see.
- **Done:** `Core/Series/` (entities + DTOs + `ISeriesReadService`/`ISeriesWriteService`);
  `Server/Series/` (`ServerSeriesReadService`/`ServerSeriesWriteService`, CQRS-lite inheritance
  mirroring Groups; owner-gate; `Description` sanitized once on save; pre-insert duplicate-name
  check, per-author not global, surfaces as `SeriesValidationException`). `SharedUI/Series/` —
  `SeriesCard`/`SeriesMembershipBox` (leaves), `SeriesPage` (`/series/{id}/{*Slug}`, public,
  `StoryDeck` in `OrderIndex` order), `SeriesCreateEditPage` (`/series/new` +
  `/series/{id}/edit`, owner-gated create/edit/add/remove/reorder/delete), `MySeriesPage`
  (`/series`, owner listing). Integrated into `StoryPage`/`StoryDesktop`/`StoryMobile` (membership
  box list), `ProfilePage`/`ProfileDesktop`/`ProfileMobile` (new `ProfileTab.Series` tab),
  `CreateMenu` ("New Series"), `UserMenu` ("My Series"). `ExceptionPresenter` extended with
  `SeriesValidationException`. Fixed a test-fixture gap found during full-suite verification:
  `ProfilePageTests`/`FakeProfileTestServices` didn't register `ISeriesReadService` — added
  `FakeSeriesReadService`.
- **Two real runtime bugs found + fixed via the L4.5 browser pass (dispatcher-reload class, same as
  WU-ComponentSoundness's F1 StoryPage fix):** (1) `SeriesCreateEditPage`'s two `@page` routes
  ("/series/new" + "/series/{id}/edit") share one component type; the post-create redirect reuses
  the instance, so `OnInitializedAsync` never re-fired and the edit page rendered blank/create-mode
  — added `OnParametersSetAsync` with route-changed guards; regression test
  `SeriesCreateEditPageTests.PostCreateRedirect_OnSameInstance_ReloadsEditModeData`. (2) `StoryPage`'s
  existing `OnParametersSetAsync` (in-place story nav) reloaded Story/Chapters/UsiState but not the
  new `_seriesMemberships` — clicking a membership box's "Next" link left the *previous* story's
  series box on screen; added the missing reload. No dedicated bUnit test (StoryPage has none even
  for its own original F1 fix); covered by this L4.5 pass. Both confirmed live via
  `mcp__claude-in-chrome__*` against the dev server + `psql` ground truth (cascade delete correct,
  member stories survived, reorder persisted) — detail: `audit/Stories.md` Feature 9.
- **Verified (2026-07-11):** `dotnet build` 0 errors/warnings (8 projects). `dotnet test` full
  solution green: 541 Unit + 533 RazorComponents + 462 Integration = **1536 tests**, including 26
  new Integration tests (`SeriesServiceTests` — CRUD owner-gating, cross-author add rejection,
  append/reorder/remove `OrderIndex`, duplicate-name rejection, cascade delete, multi-series
  membership, and the content-rating-filter-drop case for Position/Count/Next) + 11 new Unit
  (`SeriesValidationsTests`) + 16 new RazorComponents (`SeriesCardTests`/`SeriesMembershipBoxTests`/
  `SeriesCreateEditPageTests`). Mutation-sanity: temporarily stripped the `Story` join out of
  `GetMembershipsForStoryAsync` (bypassing the ContentRating/IsTakenDown filters) → the mature-drop
  test failed as expected; reverted, suite green again. `check-design-tokens.ps1` green (the only
  2 findings it reports are pre-existing in `TreeSearchResultBadge.razor`, unrelated to this unit).
  **L4.5-Browser Stage 5 (2026-07-11)** — full create/add/reorder/delete/navigate flow driven live;
  see `audit/Stories.md` Feature 9 for the step-by-step. **Scope note:** beyond the one regression
  test above, no *general* `SeriesCreateEditPageTests` CRUD-UI coverage was added — matches the
  existing precedent that `GroupCreateEditPage` has none either; owner-gate/CRUD logic is exercised
  at the Integration tier (the real authority).
- **Tool:** opusplan. **Pointer:** `audit/Stories.md` Feature 9. **Deps:** WU14, WU24.

### WU42 — Story Lineage (Feature 10, formerly "Story↔Story Relationships") — DONE ✓ (2026-07-12)
- **Cells:** 10 L2/L3-Logic/L3.5-Structure → Stage 5; L4.5-Browser → Stage 5. L4-Style stays Stage 1
  (pending human visual sign-off, WU8/WU13/WU23/WU28/WU44 precedent).
- **Renamed 2026-07-12 (Doc-Touch moment 1, before the build):** `StoryRelationship`/
  `StoryRelationshipType`/`StoryRelationshipStatus` → `StoryLineage`/`StoryLineageType`/
  `StoryLineageStatus` (feature-wide — entity, table, enum, the two pre-existing notification enum
  members, config classes, `Story` nav collections). The old name collided with both
  `StoryCharacterPairing` (WU37 renamed *that* away from `StoryCharacterRelationship` for the same
  reason) and `UserStoryInteraction`. Migration `AddStoryLineageRename` renames tables/constraints/
  indexes in place (verified no data loss against the live dev DB). Full settled-decisions note:
  `audit/Stories.md` Feature 10.
- **Done:** `IStoryLineageReadService`/`IStoryLineageWriteService`; cross-author request/approve/
  reject flow on `StoryLineage` (`SourceStoryId`, `TargetStoryId`, `RelationshipTypeId`, `StatusId`)
  — self-owned links auto-approve, no self-notification; a new reusable `SearchStoriesByTitleAsync`
  + `StoryTitlePicker` typeahead for target selection (also retrofits Groups' add-story numeric-id
  input); public display on `StoryPage` (`StoryLineageBox`); management on a new user-wide
  `/story-lineages` owner page (`MyStoryLineagesPage`, mirrors `MySeriesPage`) + a "Manage story
  lineage →" link from the story edit page. L1 already Stage 5 (`StoryLineage`, `StoryLineageType`
  present — unrelated to the `StoryCharacterPairing` introduced in WU37). Real bug found + fixed
  during verification: `PostgresFixture`'s Respawn `TablesToIgnore` list still had the pre-rename
  table name (a literal SQL string, invisible to the Phase-0 C#-identifier rename grep) — wiping the
  seeded type rows between every integration test until fixed.
- **Verified (2026-07-12):** `dotnet build` 0 errors/warnings (8 projects). `dotnet test` full
  solution green: 615 Unit + 570 RazorComponents + 544 Integration = **1729 tests**, including 28 new
  Integration (`StoryLineageServiceTests`), 6 new Unit (`StoryLineageValidationsTests`), 8 new
  RazorComponents (`StoryLineageBoxTests`/`StoryTitlePickerTests`). Mutation-sanity: bypassed the
  `ContentRating` filter on the target-story join → the mature-drop test failed as expected;
  reverted, suite green. **L4.5-Browser Stage 5 (2026-07-12)** — full cross-author request → notify →
  approve → notify → public-display flow driven live (`AuthorAlpha`/`AuthorBeta` fixtures, `psql`
  ground truth at each step), plus self-owned auto-approve (zero extra notifications) and the Groups
  retrofit (cross-author add via typeahead, correct content-rating filtering); see `audit/Stories.md`
  Feature 10 for the full step-by-step. **Scope note:** no dedicated `MyStoryLineagesPageTests`
  (single-route page, no dispatcher-reuse trap to test) — matches the `SeriesCreateEditPageTests`
  scope-note precedent; CRUD-UI authority is the Integration tier.
- **Tool:** opusplan. **Pointer:** `audit/Stories.md` Feature 10. **Deps:** WU24, WU25.

### WU43 — Saved Tag Selections (Feature 15) — DONE ✓ (2026-07-11)
- **Cells:** 15 L2/L3-Logic/L3.5-Structure `2→5`; L6 `N/A→5` (new indexes). L4-Style/L4.5-Browser stay
  Stage 1 (pending visual/live-browser sign-off, WU8/WU13/WU23 precedent). L5 stays Stage 2 (deferred,
  MVP is InteractiveServer-only). L1 already Stage 5, extended additively (see Done).
- **Scope settled before build (2026-07-11, do not revisit — see `audit/Tags.md` Feature 15 for full
  reasoning):** persists only the tag include/exclude axis (not text/sort/interactions); ONE unified
  selection spans all tag types (not per-type); no per-user cap; `Description` is bounded plain text
  (280 chars); Load (searchable/sortable flyout) and Save (compact dialog) are separate UI surfaces
  mounted once in `TagFilter`'s header, reaching all four `ResultsFilterPanel` consumers
  (`/discover`, Tree Search, Bookshelves, Profile) for free; sharing is copy-on-write onto a dedicated
  `ProfileTab.TagSelections`, no public gallery.
- **Done:** Additive migration `WU43_SavedTagSelectionExcludeAndDescription` (`SavedTagSelectionEntry
  .IsExcluded`, `SavedTagSelection.Description` + two new indexes); moved both entities
  `Core/Models/` → `Core/Tags/`. New L2: `SavedTagSelectionSummaryDto`/`DetailDto`/`Input`,
  `SavedTagSelectionSortEnum`, `ISavedTagSelectionReadService`/`WriteService`,
  `ServerSavedTagSelection{Read,Write}Service`, `SavedTagSelectionValidations` (pure rules +
  copy-nickname disambiguation helper). New L3/L3.5: `SavedTagSelectionLoadFlyout`/
  `SavedTagSelectionSaveDialog` — each a thin `<AuthorizeView>` wrapper plus an `…Inner` component
  holding the real markup/`@inject` (see `layer3-logic.md` "Deferring DI Behind AuthorizeView" — a
  same-component `@inject` resolves at construction time regardless of `AuthorizeView`'s decision,
  which broke nine pre-existing bUnit suites until split); both mounted in `TagFilter`'s header.
  `TagFilter.ApplySavedSelectionAsync` rewrites its per-type buckets and forces every `TagSelector` to
  remount via a `@key` generation counter (see `layer3-logic.md` "Forcing a Child to Re-Seed via
  @key" — `TagSelector` only seeds on `OnInitialized`, so mutating state alone doesn't refresh it).
  New `ReaderSettings.SavedTagSelectionSort` preference (JSON blob, no migration) + `ReaderSettingsForm`
  dropdown. New `ProfileTab.TagSelections` tab (profile dispatcher + Desktop/Mobile bodies + "Add to
  my filters" copy-on-write button, toast feedback). New L6 indexes
  (`ix_saved_tag_selections_user_id_date_created`, `…_user_id_is_public`).
- **Verified (2026-07-11):** `dotnet build` full solution green, 0 warnings/errors. `dotnet test` full
  suite green: **585 Unit + 564 RazorComponents + 516 Integration = 1665 total** (+17 Unit / +30
  Integration / +18 RazorComponents net-new for this WU). Covering tiers: **Unit**
  (`SavedTagSelectionValidationsTests` — CanSave rules, nickname-disambiguation incl. case-
  insensitivity and truncation); **Integration** (`SavedTagSelectionServiceTests` — CRUD + owner
  gating, per-user duplicate-nickname rejection, `IsExcluded` persisted both ways, wholesale entry
  replacement on update, sort orders, public/private visibility gate, copy-on-write independence
  [editing/deleting one side never affects the other], nickname-collision disambiguation, and the
  `SavedTagSelection.UserId` Cascade on user delete via `UserDeletionService`); **RazorComponents**
  (`SavedTagSelectionLoadFlyoutTests`, `SavedTagSelectionSaveDialogTests`, `TagFilterTests` — hidden
  for anonymous, list/filter/sort, Apply re-emits + visually remounts `TagSelector`, Delete via nested
  `ConfirmDialog`, Save-disabled-on-empty-set, validation-error `InlineAlert` surfacing). Also fixed
  nine pre-existing `ResultsFilterPanel`/`TagFilter`-rendering bUnit files (`SearchDesktop/MobileTests`,
  `BookshelvesDesktop/MobileTests`, `TreeSearchDesktop/MobileTests`, `ResultsFilterPanelTests`,
  `ProfilePageTests`) that broke from the new `<AuthorizeView>`-gated children — needed only
  `this.AddAuthorization()` (defaulting anonymous) post-split, no saved-selection service fakes,
  confirming the wrapper/inner DI-deferral fix. `scripts/check-design-tokens.ps1` clean (same 3
  pre-existing, unrelated findings as before this WU). Following Series (WU41)/`DefaultSearchSort`
  precedent, no dedicated `ProfileDesktop/MobileTests`/`ReaderSettingsFormTests` files were added for
  the presentational-only tab body / dropdown addition. L4-Style and L4.5-Browser visual/live-browser
  sign-off remain open — not exercised this session.
- **Tool:** opusplan. **Pointer:** `audit/Tags.md` Feature 15. **Deps:** WU23, WU27.5.

### WU44 — Automatic Tree Search UI (Feature 59) — DONE ✓ (2026-07-11)
- **Cells:** 59 L3-Logic/L3.5 `2→5`; L4.5-Browser `1→5` (real-circuit verification, see Stage
  note). L4-Style stays Stage 1 (pending visual sign-off, WU8/WU13/WU23/WU28 precedent). L5 stays
  Stage 2 (rides the future site-wide `InteractiveAuto` flip, per `/tags`).
- **Direction settled (2026-07-11, do not revisit):** ship the Unified Tree Search Page shell
  (`TreeSearchPage` dispatcher, routes `/discover/me` / `/discover/user/{userId}` /
  `/discover/story/{storyId}`, root-entity header, two-tab strip) + the working **Automatic** tab
  now. The **Manual** tab (Feature 33 / WU40) is a placeholder ("Graph view coming soon") in the
  same shell — WU40 fills it in later without reworking the shell. Results reuse `StoryDeck` + a
  degree badge (not a bespoke tree-results list).
  **Corrected by WU40 (2026-07-12):** the "Manual" placeholder split into two tabs (Explore, Deep
  Dive) — the strip is three tabs total, not two; and the path-chip badge's "collapse user hops,
  never render a username" behavior was over-anonymized and is fixed as part of WU40. See
  `audit/Discovery.md` Feature 33/59 and `layer3.5-structure.md`'s corrected Automatic-tab note.
  These are additive corrections to an already-shipped, tested cell — no `status.md` regression.
  **Filter composition (spec §5.26 vs the Stage-5 `TreeSearchRequest` contract gap):** tree search
  is the **Source** (the rCTE over the mart), `StoryFilterDto`/`ResultsFilterPanel` is the
  **Filter**, Random/ByDegree is the **Sort**. Composed via a new
  `ITreeSearchReadService.SearchAsync(TreeSearchRequest, StoryFilterDto, ct)`: the rCTE returns a
  raw reached set (no rating/interaction filter, no cap — additive, defaulted; existing
  `TraverseAsync` unchanged), and a new `IStoryReadService.FilterCandidateIdsAsync` reuses the
  existing `ApplyFilters` predicate verbatim to own every relevance filter (rating, interaction,
  tags, FTS) **and** the cap, before hydration via `GetListingsByIdsAsync`. Full analysis + rejected
  alternatives: `audit/Discovery.md` Feature 59, `layer2-services.md` "Tree Search — Automatic Tab
  Composition (WU44)", `middle_plan_v2.md` Resolved.
- **Done:** built exactly as settled above, plus a `StoryDeck.CardOverlay` additive slot (degree
  badge / path chip) and a real runtime bug found + fixed via L4.5 browser verification
  (`TreeSearchControls`' `OnInitialized()`-snapshot race — see the audit Stage note). `dotnet test`:
  Unit 530, Integration 424, RazorComponents 513, all green.
- **Tool:** opusplan. **Pointer:** `audit/Discovery.md` Feature 59. **Deps:** WU-Marts (F59 L2/L8,
  done), WU23 (`ResultsFilterPanel`/`StoryDeck`), WU28 (`ApplyFilters`/`IDiscoveryDefaultsReadService`).

---

### WU45 — Story Arcs + chapter-presentation upgrade + chapter reorder/delete (Features 8, 6, 7-surface, 44-surface) — BUILT ✓ (2026-07-12; L4.5 browser pass deferred)
- **Cells:** 8 L1 `5→2→5-target` (SortOrder-drop migration), 8 L2/L3/L3.5/L4 → build (design
  settled 2026-07-12 — resolves the long-standing §8.2 Stage-1 gap); 6 L2 reopened (reorder +
  delete are new capability); 7 L3/L3.5/L4 reopened (`ChapterList` rewrite); 44 L2 additive
  (manual read-mark durable-direct seam).
- **Direction settled 2026-07-12 (Brian, extensive chat deliberation; do not revisit):**
  one flat pure segmenter (arc + frontier-window boundaries over one ordered list; constants
  `CollapseMinimum≈10` / `HeadWindow=3` / `TailWindow=3`, named + tunable); arcs sticky/toggleable,
  supersede windowing inside themselves; strict-chain "New" badge; progress fill-bar; manual marks
  set both `IsRead`+`ReadProgress` and call `MarkStartedAsync`, discard pending buffer pings;
  reorder = drag-only, silent (link/arc warnings explicitly waived), append-only creation stays;
  delete shifts −1 with Restrict-FK two-step; `StoryArc.SortOrder` eliminated; arc manager =
  separate panel, rows + live preview; reading page shows `Arc X — [name]` under the title.
  Fimfiction inspected as behavioral reference only (DOM/CSS/JS of two real pages) — Blazor
  first-principles implementation, not a port.
- **Settled-vs-open:** `audit/Stories.md` Feature 8; `audit/Chapters.md` "WU45 settled design".
- **Did (2026-07-12):** all of the above, in one pass. L1: `WU45_StoryArcDropSortOrder` migration
  + `StoryArc` move to `Core/Stories/`. L2: `IStoryArc{Read,Write}Service`,
  `IChapterReadMarkWriteService` (+ `ReadingProgressBuffer.Discard` seam), viewer-aware
  `GetChapterListAsync` + `GetViewerLastInteractionUtcAsync`, `MoveChapterAsync`/
  `DeleteChapterAsync` (negative-pass renumbering, arc shift composition, TPT-safe comment
  delete), `ExportChapterAsync` + per-chapter endpoint. Shared: `ChapterListSegmenter` (pure, one
  function for SSR + client re-segment). UI: `ChapterList` rebuilt (fill-bar, toggles, expanders,
  sticky arc headers, download menu), `ChapterManagerPanel` (drag reorder + delete),
  `StoryArcManagerPanel` (rows + live preview), `StoryPage` wiring, reading-page `Arc X — [name]`
  label. `dotnet test` green: Unit 685 / Integration 650 / RazorComponents 619 (70 new tests
  across the tiers). `check-design-tokens.ps1`: only the pre-existing `ImportReviewPanel` finding.
  **L4.5-Browser verification deferred at close (Brian's direction)** — F6/F7 L4.5 `5→2`, F8
  L4.5 `2` in `status.md`; details + the not-covered list in `audit/Chapters.md` WU45 Stage note.
- **Tool:** Claude Code (requirements deliberated in chat, same session). **Deps:** WU25
  (`StoryPage`/`ChapterList`), WU26 (reading page, F44 pipeline), WU38c (export writers, for
  per-chapter download).
- **Cells changed:** F4/F5 L5 `4 → 2`. All other affected cells (Moderation/Groups/Recommendations/
  BlogPosts L2) were already Stage 5 and remain so — this work corrected the code underlying them.
- **Done:**
  - **Phase A:** All four named EF display/visibility filters (`ContentRating`, `GroupAudience`,
    `IsTakenDown` ×4 roots) moved from `ApplicationDbContext.OnModelCreating` to
    `ReadOnlyApplicationDbContext.OnModelCreating`. `_activeUser` changed from `private` to
    `protected`. Write context sees ground truth with no filters. ~15 write-side `IgnoreQueryFilters`
    calls deleted; ~7 read-side elevated reads kept and annotated `// elevated read:`. Latent edit
    bug at `ServerStoryWriteService:51` fixed by construction.
  - **B1:** `Migrations/ReadOnlyApplicationDb/` deleted (9 files). Read context owns no migration
    history; `ApplicationDbContext` is the sole migration source.
  - **B2:** `HttpStoryReadService.cs` + `HttpStoryWriteService.cs` deleted; DI registrations at
    `Client/Program.cs:16-17` removed; stale doc comment in `StoryDetailsDTO.cs:42` updated.
  - **Tests:** `ContentRatingFilterTests` extended (+5 integration tests incl. line-51 regression);
    `ModerationServiceTests` fixture corrected. All 1232 tests pass.
  - **Docs:** `content-safety.md`, `layer1-data-model.md`, `layer2-services.md` skill files updated;
    `audit/Stories.md` and `audit/Moderation.md` Stage notes written; `status.md` updated.
- **Pointer:** `audit/Stories.md` §"Feature 4 / Feature 5 — Filter revamp Stage note."

---

### WU-CounterAtomicity — Denormalized-counter lost-update fix + CS9107 tidy — DONE ✓ (2026-06-27)
- **Cells changed:** none — Comments L2/L3 and Recommendations L2/L3 stay Stage 5; Stories L2/L3
  stay Stage 5. These were correctness polishes inside already-aligned cells; no stage transition.
- **Done:**
  - `ServerRecommendationWriteService.ToggleLikeAsync` and `ServerCommentWriteService.ToggleLikeAsync`:
    replaced tracked read-modify-write (`rec.LikeCount++`) with atomic
    `ExecuteUpdateAsync(SetProperty(x => x.LikeCount, x => x.LikeCount + delta))` after the join-row
    `SaveChangesAsync`. Returned DTO value unchanged (optimistic `loaded + delta`). Eliminates the
    lost-update race when two users like the same target concurrently.
  - `ServerStoryReadService`: promoted `activeUser` primary-ctor parameter to
    `protected IActiveUserContext ActiveUser { get; } = activeUser;` (same pattern as
    `ServerBlogPostReadService`); routed internal uses through `ActiveUser`.
  - `ServerStoryWriteService`: changed two `activeUser.UserId` references to `ActiveUser.UserId`
    (via the inherited property). The ctor parameter now only appears in the base-ctor argument, not
    as a captured field — CS9107 eliminated.
  - `layer2-services.md` §"UserStats Updates": added "Counter mutation rule" subsection documenting
    the atomic `ExecuteUpdateAsync` requirement for all denormalized counters.
- **Verified:** `dotnet build` green, zero errors, zero CS9107 warnings. `dotnet test` 1232/1232 pass
  (437 Unit + 443 RazorComponents + 352 Integration). Concurrency fix is not automatable
  (no parallel-request seam in the test harness); covered by code review + sequential toggle tests
  confirming correct counter behavior.
- **Pointer:** `audit/Recommendations.md` §Feature 29, `audit/Comments.md` §Feature 25,
  `audit/Stories.md` §"WU-CounterAtomicity Stage note."

### WU-ComponentSoundness — Lifecycle reload + list keying correctness wave — DONE ✓ (2026-06-27)
- **Cells:** none — all affected cells (F5 L3/L3.5 StoryPage/StoryDeck, F7 L3 ChapterReadingPage,
  F17 L3 BookshelvesPage, F21 L3 ProfilePage, F26/F28 L3.5 CommentSection/RecommendationSection,
  F36 L3 BlogPostPage, F40 L3 GroupPage) were already Stage 5. This wave closes three latent
  correctness gaps inside aligned cells — no stage transition.
- **Done:**
  - **Phase 0 (conventions):** `layer3-logic.md` §"Route-parameter dispatchers reload in
    `OnParametersSetAsync`" added (MessagesPage pattern: `_initialized` + `_loadedXxx` sentinel,
    one-time auth in `OnInitializedAsync`, reload in `OnParametersSetAsync`; `[PersistentState]`
    `??=`-vs-plain-assignment gotcha documented). `layer3.5-structure.md` §"`@key` on `@foreach`
    over stateful children" added (when required vs. not; self-healing and pure-display exceptions;
    `if (_field is null)` cache guard as the aggravating pattern).
  - **Phase 1 (F1 lifecycle fixes):** `ProfilePage`, `BookshelvesPage`, `GroupPage`, `BlogPostPage`,
    `StoryPage`, `ChapterReadingPage` — all converted to MessagesPage pattern. ChapterReadingPage also
    adds `DisposeJsRegistrationAsync()` called on chapter change (dispose + reset `_jsRegistered`) and
    drops `firstRender` guard from `OnAfterRenderAsync` in favor of `_jsRegistered` flag alone.
    `[PersistentState]` plain-assignment fix in `StoryPage.OnParametersSetAsync`.
  - **Phase 2 (F2/F3 list keying):** `@key="story.StoryId"` on `<StoryCard>` in `StoryDeck.razor`;
    `@key="root.CommentId"` + `@key="reply.CommentId"` on `<CommentItem>` in `CommentSection.razor`;
    `@key="rec.RecommendationId"` on `<RecommendationCard>` in `RecommendationSection.razor`.
  - **Phase 3 (tests):** `StoryDeckTests.KeyedList_WhenStorySwapped_*` (F2 mutation-sanity);
    `CommentSectionTests.KeyedList_WhenSpoilerPaginates_*` (F3 mutation-sanity);
    `ProfilePageTests.TabSwitch_OnSameInstance_ReloadsTabPayload` (F1 lifecycle, with 6 new fake
    service classes in `FakeProfileTestServices.cs`).
  - **Phase 4 (docs):** audit Stage notes in all 7 affected audit files; this workplan entry;
    `status.md` Global conditions bullet.
- **Verified:** `dotnet build` green (1 pre-existing CS8618 warning in `TagDropDownDTO.cs`, unrelated).
  `dotnet test` 1235/1235 pass (446 RazorComponents + 437 Unit + 352 Integration). Remaining F1 pages
  (ChapterReadingPage, GroupPage, BlogPostPage, StoryPage) covered by manual E2E checklist (JS-interop
  or service-heavy; see `audit/Stories.md`, `audit/Groups.md`, `audit/BlogPosts.md`).
- **Tool:** Opus in Claude Code. **Pointer:** `.claude/plans/l3-component-soundness-md-is-a-plan-quiet-swan.md`;
  audit Stage notes in `audit/Stories.md`, `audit/Comments.md`, `audit/Recommendations.md`,
  `audit/Profiles.md`, `audit/Groups.md`, `audit/BlogPosts.md`, `audit/UserStoryInteractions.md`.

### WU-BrowserPass — First browser-based debugging wave (real-circuit bugs) — DONE ✓ (2026-07-01)
- **Cells:** none flipped — every bug was fixed same-session (`debugging.md` "Fix same-session"), so
  Stage numbers keep describing sound code. Cross-cutting corrections + five feature-local fixes.
- **Done:** first end-to-end browser pass over the integrated MVP (dev-bar login → navigation →
  authoring → reading → social → moderation). Five bug classes found and fixed, none reproducible
  by the automated tiers:
  1. **Circuit-scoped read-DbContext concurrency crash** (login gate — every authenticated page
     500'd): all read services moved to per-method contexts from a scoped
     `IDbContextFactory<ReadOnlyApplicationDbContext>`; supersedes spec §6.6. Detail:
     `layer2-services.md` §"Read-Context Concurrency: Factory Per Method", `forward_plan.md`
     Resolved entry, `audit/Notifications.md` + `audit/Messaging.md` notes; regression net
     `Tests.Integration/ConcurrentReadAccessTests.cs` (3 tests).
  2. **Tailwind v3 CSS-variable classes silently no-oping under v4** (`-[--token]` → invalid CSS;
     transparent flyouts, invisible badges): 987 usages converted to `-(--token)` + CSS rebuilt.
     Detail: `layer4-style.md` §"Consuming tokens in classes".
  3. **ChapterPropertiesForm passed phantom `InitialHtml`/`Compact` params to EditorView**
     (chapter editor 500'd) + **ChapterEditorPage navigated by PK in the ChapterNumber route slot**
     + missing `OnParametersSetAsync` reload. Detail: `audit/Chapters.md` note.
  4. **CommentSection persistent composer never cleared after posting** (double-post hazard):
     `EditorView.SetHtmlAsync` → `CommentEditor.ClearAsync` → clear on successful post. Detail:
     `audit/Comments.md` note.
  5. **DevLoginBar's fetch-POST silently dropped on an established circuit** (couldn't switch
     users): endpoint is now GET + redirect, bar renders plain anchors. Detail:
     `run-server/SKILL.md` "Skipping login".
- **Verified:** browser — login as TestUser and AdminUser, mark-all-read, chapter
  create→publish→read, comment post + composer clear, group join, mod queue as AdminUser, all
  major routes render (`/discover`, `/tags`, `/bookshelves`, `/notifications{,/settings}`,
  `/messages`, `/settings`, `/story/*`, `/user/*`, `/groups`, `/group/*`, `/blog/new`,
  `/story/new`, `/mod/*`). `dotnet test` 1238/1238 (437 Unit + 446 RazorComponents +
  355 Integration — includes the 3 new concurrency regressions).
- **Tool:** Sonnet in Claude Code (browser tools per `run-server/SKILL.md`). **Pointer:** audit
  notes listed above; methodology minted this session in `canalave-conventions/debugging.md`.
- **Known non-blockers (deliberately not fixed):** dev DB carries pre-Testcontainers fixture junk
  (GUID-suffixed tags/stories — data hygiene, not code); empty author's-note panels render as blank
  boxes on the reading page (cosmetic); anonymous mod-page hit returns a bare 403 status (deliberate
  Blazor-cookie choice in `Program.cs`).

### WU-DesktopNav — Desktop top navigation bar — DONE ✓ (2026-07-01)
- **Cells:** none tracked — persistent-layout chrome has no dedicated grid row (`status.md` Global
  Conditions note).
- **Done:** replaced `DesktopLayout.razor`'s placeholder (`w-64` empty sidebar + hardcoded MS
  "About" link) with a single full-width sticky top bar: brand wordmark, `NavLink`s to
  Home/Discover/Tags/Groups, and a right-side chrome group. Added two new components:
  `CreateMenu` (auth-gated "Write" dropdown → New Story/Blog Post/Group) and `UserMenu` (profile
  dropdown replacing desktop's `LoginDisplay` — My Profile/Bookshelves/Settings/role-gated Mod
  tools/Log out). Both follow the existing `NotificationBell` caret dropdown pattern. Mobile
  (`MobileLayout`, still on plain `LoginDisplay`) intentionally untouched — desktop/mobile chrome
  are structurally separate compositions. Detail: `layer4-style.md` Pattern Accumulation
  "`DesktopLayout` top bar / `UserMenu` / `CreateMenu`".
- **Verified:** `dotnet build` clean (0 warnings/errors); `npm run css:build` picked up the new
  paren-form token classes. No Chrome MCP tool available this session, so verification was via
  headless server + curl/cookie-jar HTTP checks rather than a real browser: anonymous homepage
  shows wordmark/Discover/Groups/"Log in" only (no Write button); dev-login as TestUser shows
  username + Write, no Mod tools; dev-login as AdminUser shows Mod tools; `/mod/reports` returns
  403 for TestUser and 200 for AdminUser; all nav-linked routes (`/discover`, `/tags`, `/groups`,
  `/bookshelves` [redirects to its default tab], `/settings`, `/notifications`, `/messages`)
  return 200/expected-redirect with no errors in the server log. This is L4-Style chrome (manual
  visual band, no automated tier); the interactive dropdown open/close click behavior itself was
  not click-tested this session (no browser tool) — follow-up visual pass recommended once one is
  available.
- **Tool:** Opus in Claude Code (plan mode + direct implementation, no browser tools this session).

### WU-DevSeed — Dev-DB reset workflow + representative seed data — DONE ✓ (2026-07-01)
- **Cells:** none — dev tooling. Purged the pre-isolation fixture junk (6,295 GUID tags, WU12-era
  stories/groups) by instituting the wipe workflow rather than surgical deletes.
- **Done:**
  - **`scripts/`** (new, repo root): `start-dev-server.ps1` (foreground or `-Background` with
    log-wait), `stop-dev-server.ps1` (port-based kill + verify), `reset-dev-db.ps1` (stop +
    `DROP DATABASE … WITH (FORCE)` + existence re-check; `-Restart` chains a background start —
    the next Development boot's `MigrateAsync` recreates the DB and `DataSeeder` repopulates).
    Scripts are ASCII-only on purpose: PowerShell 5.1 reads BOM-less `.ps1` as ANSI, and UTF-8
    em-dashes decode into smart-quote bytes that terminate strings mid-line (bit us on first run,
    as did PS→native quoting: an unescaped `"TheCanalaveLibraryDB"` identifier reached psql
    unquoted, was lowercased, and "dropped" a nonexistent database — the script now verifies the
    DB is actually gone).
  - **`DataSeeder` rewritten** (mode via config `DevSeed`: `Full` default / `Minimal` users+roles /
    `None`): deterministic medium-showcase inventory (7 users incl. `TestUser`=1/`AdminUser`=2,
    44-tag real taxonomy, 12 stories across ratings/statuses, multi-chapter + alternate version +
    draft chapter, full bookshelf coverage, comments/likes/spoiler, 3 recommendations incl. Hidden
    Gem + author-highlighted, 3 groups incl. Mature-audience + SFW-only `(E,T)` per
    `GroupAudienceTypeMapper`, blog posts, unread message, 3 notifications, 2 open reports).
    Raw-DbContext graph inserts with invariants maintained by construction (see file header —
    the single source of truth for the inventory); deliberately artificial naming (no faux
    community content).
  - **`TestAppFactory`** pins `DevSeed=Minimal` (the seeder runs before every integration test
    under the Development env — Full would balloon the suite). `appsettings.Development.json`
    sets `DevSeed=Full`.
  - **`run-server/SKILL.md`**: Start/Stop now lead with the scripts; new **"Dev DB lifecycle —
    keep or wipe (agent's choice)"** section (default keep; wipe deliberately via the script for
    confounding state / schema+seed changes / junk-caused errors / user request; ask before wiping
    ambiguous state); prerequisites corrected (DB auto-created by `MigrateAsync`, no manual setup).
  - `testing.md` §"Driving the content-rating filter" documents the `DevSeed=Minimal` pin.
- **Verified:** `reset-dev-db.ps1 -Restart` run twice end-to-end (drop → recreate → migrate →
  seed); browser walk of the seeded showcase — discover cards with real tags, clean tag directory
  (no GUID junk), bookshelves populated per tab, flagship story TOC with nested alternate version +
  author-highlighted recommendation, messages badge 1, bell badge 3, ReaderGamma sees no Mature
  group/story, AdminUser mod queues show 2 submissions + 2 reports. `dotnet test` green with
  Integration duration flat (Minimal pin effective).
- **Tool:** Sonnet in Claude Code. **Pointer:** `DataSeeder.cs` header;
  `run-server/SKILL.md` "Dev DB lifecycle"; `testing.md` DevSeed note.

### WU-L45Pass — L4.5-Browser verification wave (feature-by-feature) — DONE ✓ (2026-07-02)
- **Cells:** new `L4.5-Browser` column added to the `status.md` grid (legend there defines the
  band); every feature with L1–L3.5 at Stage 5 was driven end to end in a real browser against the
  seeded dev DB and flipped to L4.5=5: **F1, 3, 4–7, 11–14, 16–32, 34–36, 38–44, 46–50, 52**
  (36 features across 16 clusters). Remaining L4.5=1 rows are features whose earlier layers are
  unbuilt/blocked; N/A rows have no browser surface (workers, pure seed).
- **Method:** per cluster — drive the audit file's intended flows in Chrome (claude-in-chrome MCP),
  verify every mutation against psql ground truth, fix bugs same-session (per CLAUDE.md rule), then
  flip the grid number and write the narrative into the cluster's audit Stage note. Verification
  narratives live in the per-cluster audit files (Identity, Stories, Chapters, Tags,
  UserStoryInteractions, Following, Profiles, Comments, Recommendations, Discovery, BlogPosts,
  Groups, Notifications, Moderation, Messaging, Sprites, Badges — all dated 2026-07-01/02).
- **Bugs found & fixed (browser-only classes, invisible to the three automated tiers):**
  1. Email login broken for every account (`Login.razor` passed email as username).
  2. Registration 500 (`ThemeId` never set → FK violation) + emails leaked as public usernames
     (dedicated Username field added).
  3. Scoped-CSS bundle href 404 (`App.razor` missing `.Server` in the bundle name) — permanent
     error banner on Identity pages.
  4. Story `PublishedDate`/`LastUpdatedDate` never stamped (showed "Jan 1, 0001").
  5. Literal string-parameter bindings (missing `@`): `TagDirectoryDesktop.ServerError`, then the
     same class ×6 in Messaging (`ReplyError`/`ComposeError` chains) — phantom error text always
     visible. Project-wide sweep found no further instances.
  6. `TagEditorForm` enum `<option>` values serialized numerically — Tag Type select rendered blank.
  7. USI Detail-context panel was built + bUnit-tested but mounted nowhere — Favorite/Follow/
     Complete unreachable in the entire UI; mounted on StoryPage→StoryDesktop/Mobile with
     dispatcher-loaded state (N+1 rule).
  8. `DataSeeder` stamped `PostApprovalStatus = status` — approval of the seeded pending stories
     would have been a silent no-op (production submit path was already validation-guarded).
  9. Compose-conversation modal never closed after a successful send (same-route navigation reuses
     the page instance).
  10. Badge icon imgs rendered broken-image glyphs (assets are out-of-band and absent in dev) —
      `onerror` hide added to `UserCard`/`BadgeSettingsForm`.
- **Seed additions:** `Bulbasaur` character tag with `SpriteIdentifier="bulbasaur"` (matches the
  checked-in dev asset; the sprite render+fallback path is exercisable on a fresh DB) and one
  earned `Recommender` badge for TestUser (curation UI + card badge row render populated).
- **Verified:** `dotnet test` 1238/1238 green (437 Unit + 446 RazorComponents + 355 Integration).
- **Tool:** Sonnet in Claude Code. **Pointer:** `status.md` L4.5 legend; per-cluster audit Stage
  notes; `canalave-conventions/debugging.md` for the methodology.
- **Docs follow-up (2026-07-02):** methodology learnings institutionalized into the skills tree —
  `run-server` ("Driving the UI reliably" browser mechanics; seed state-machine-invariant rule),
  `layer3-logic.md` (literal string params, enum-select binding patterns), `render-and-layout.md`
  (claim staleness),
  `layer3-logic.md` (transient UI state on same-route nav), `layer4-style.md` (out-of-band asset
  `onerror` rule), `testing.md` (unmounted-component reachability hole + first L4.5 cross-ref),
  `debugging.md` (recorded-intent-before-fixing principle). Prior "tool limitation" claims from
  this WU's browser pass were researched + empirically re-tested first: the background-tab freeze
  was Chrome Memory Saver (setup, not tooling), `form_input` works on both SSR POSTs and
  interactive `@bind` (earlier failures misattributed), and the coordinate space is the documented
  CSS×DPR contract — the skill documents setup + intended usage, dated 2026-07-02, not permanent
  limitations.

### WU-L5Pilot — First WASM feature end-to-end (Tag Directory island) — DONE ✓ (2026-07-04)
- **Cells:** F11 Tag Administration L5 `2 → 5`, F13 Tag Display & Sprites L5 `2 → 5`,
  F34 Tag Directory L5 `2 → 5`. Purpose: battle-test `layer5-wasm.md` (previously Stage-2 design
  intent, unbuilt) on one representative feature before the Phase-4 L5 batch applies it broadly
  (`middle_plan.md` Phase 4 item 6).
- **Done:**
  - `Server/Tags/TagEndpoints.cs` — full `ITagRead/WriteService` HTTP surface under `/api/tags`
    (cluster-colocated, thin pass-throughs, exception→status translation; bodied `Results.Problem`
    for ALL error statuses — bare `Results.NotFound()` gets re-executed by
    `UseStatusCodePagesWithReExecute` with the original HTTP method and surfaces as 405).
  - `Client/Tags/ClientTagReadService` + `ClientTagWriteService` — first minted client HTTP pair
    (write inherits read; status→exception translation restores the typed-exception contract),
    registered in `Client/Program.cs`.
  - `TagDirectoryPage` converted to the island pattern: `[ExcludeFromInteractiveRouting]` +
    `@rendermode RenderMode.InteractiveWebAssembly` (both load-bearing — without the attribute,
    in-circuit nav to the page crashes the InteractiveServer circuit) + `[PersistentState]`
    directory (zero refetch on hydration) + page-level `ThemeContextProvider` wrap.
  - `ThemeContextProvider` moved `Server/Components/` → `SharedUI/Sprites/` (islands need it;
    zero server-only deps). `Program.cs`: `AddAuthenticationStateSerialization(SerializeAllClaims
    = true)` so theme claims + roles reach the WASM runtime. `App.razor` unchanged in the end
    (`AcceptsInteractiveRouting` covers island pages too).
- **Verified:** real-browser end to end (2026-07-04): WASM runtime boots on `/tags` (dotnet.wasm
  + assemblies fetched; `AuthorizeView` evaluates in-browser); anonymous browse + sprite fallback
  chain identical to server rendering; AdminUser sees mod controls via serialized role claims;
  create → `POST /api/tags` 200 + psql row + sprite-warning advisory rendered; duplicate name →
  400 → inline `TagValidationException` message; delete → row gone in psql; cross-navigation
  both directions (island→home enhanced nav; home→island full-page reload — the pre-fix circuit
  crash is the documented hazard). `dotnet test` green: 448 Unit (+11 `ClientTagServiceTests`) +
  446 RazorComponents + 365 Integration (+10 `TagEndpointsTests`).
- **Tool:** Claude Code (browser-driven verification per `run-server/SKILL.md`). **Pointer:**
  `layer5-wasm.md` (rewritten from battle-tested reality — the deliverable), `render-and-layout.md`
  §"Render Mode" + §"ThemeContext Cascading Provider", `testing.md` project-setup reference,
  audit notes in `audit/Tags.md` (F11/F13) and `audit/Discovery.md` (F34).
- **Post-verification decision (2026-07-04):** rollout strategy settled — per-feature L5 builds
  stay headless; the render-mode conversion happens in ONE global `InteractiveAuto` flip + one
  browser wave (`middle_plan.md` Resolved "L5 rollout strategy"). The pilot's island directives
  (`[ExcludeFromInteractiveRouting]` + `@rendermode`) and page-level `ThemeContextProvider` wrap
  were removed from `TagDirectoryPage` — `/tags` rides global `InteractiveServer` again;
  `[PersistentState]` kept (benefits circuit prerender too). F11/F13/F34 L5 stay Stage 5: the
  cells' substance (endpoints, client impls, serialized-auth config, tests) is live and green;
  the WASM-runtime browser verification stands as recorded above. The island recipe survives in
  `layer5-wasm.md` §"The Island Recipe" as a flip-wave debugging technique.

### WU-Aspire — Orchestration returns: AppHost + Postgres/Redis/MinIO (Phase 4 item 1) — DONE ✓ (2026-07-05)
*(Snapshot of what this WU stood up that day — MinIO was replaced by Garage a few entries later,
see WU-S3Garage; treat "Postgres/Redis/MinIO" above as historical, not current.)*
- **Cells:** none (dev-infrastructure work-unit — no feature cell changes stage; recorded as a
  `status.md` Global Condition). Executes `middle_plan.md` Phase 4 item 1 under its two standing
  constraints: plain `AddDbContext` stays (WU12 anti-pooling ruling — zero Server code changed),
  and the server-only dev path remains fully supported.
- **Done:**
  - `AppHost.csproj` realigned: `Aspire.AppHost.Sdk` was 9.5.2 against 13.4.5 hosting packages —
    an unsupported mismatch (the SDK pins DCP + dashboard binaries). Now top-level SDK
    `Aspire.AppHost.Sdk/13.4.6` + all hosting packages 13.4.6; the explicit
    `Aspire.Hosting.AppHost` PackageReference is gone (encapsulated by the 13.x SDK).
  - `AppHost.cs` resource graph: Postgres 18 (`WithImageTag("18")`, host port 5433, database
    `canalavedb`), Redis as `cache` (6379, `WithPersistence`), MinIO as pinned plain
    `AddContainer` (9000/9001; the CommunityToolkit MinIO package is deprecated — MinIO OSS
    archived 2026-02, see `audit/ImageStorage.md`). All three: persistent lifetime, named
    containers/volumes (`canalave-*`), secret parameters from AppHost user secrets. Web =
    Server `http` launch profile → same 5028 as the server-only path; `WaitFor(canalaveDb)`.
  - Scripts: `start-aspire.ps1` / `stop-aspire.ps1` / `reset-aspire-db.ps1` (mirror the
    server-only trio's contracts: refuse double-start, background readiness wait on the web app,
    kill-the-worker-not-the-launcher, wipe = remove container+volume). `start-dev-server.ps1`
    header updated to name the two paths. `ASPIRE_ALLOW_UNSECURED_TRANSPORT=true` added to the
    AppHost `http` launch profile + script (http-only apphost URL hard-fails without it).
  - `aspire` CLI 13.4.6 installed globally (`dotnet tool install --global Aspire.Cli`).
  - Docs: `run-server/SKILL.md` "Two run paths" + "Aspire path" sections; `cross-cutting.md`
    "Aspire 13 Configuration" rewritten from the live implementation (the old sketch's
    `AddNpgsqlDbContext` consumption line contradicted the settled plain-`AddDbContext` rule —
    removed); `layer7-redis.md` Aspire section now names the real `cache` resource;
    `audit/ImageStorage.md` MinIO provisioning note.
- **Verified (2026-07-05):** full end-to-end run under the AppHost — three containers up
  (pinned images, proxied pinned host ports), fresh-volume boot ran migrate + full `DataSeeder`
  (12 stories / 7 users via psql on 5433), dev-login + `/discover` deck browser-verified against
  the containerized DB, dashboard authenticated via tokenized login URL with all 5 resources
  Running and zero error-level structured logs for `web`; stop/start cycle proved persistent
  containers + data survival (no reseed) with the second start taking seconds. `dotnet test`
  green — no automated tier covers orchestration itself (Integration uses Testcontainers, not
  the AppHost); the manual end-to-end run above is the verification band, per the L4.5 precedent.
- **Tool:** Claude Code (research-driven; Aspire 13.4.6 facts current as of 2026-07-05).
  **Pointer:** `run-server/SKILL.md` "Aspire path", `cross-cutting.md` "Aspire 13 Configuration".

### WU-S3Garage — S3 image storage: Garage (dev) / Cloudflare R2 (prod) (Phase 4 item 3) — DONE ✓ (2026-07-05)
- **Cells:** F4 L2 and F20 L2 stay Stage 5 (cloud backend was those cells' recorded open item —
  now closed; the frozen `IImageStorageService` contract and every call site are untouched).
  Decision input: middle_plan Resolved "Garage replaces MinIO as the dev S3 endpoint"
  (2026-07-05, Brian) — MinIO OSS archived 2026-02, spec §1/§3.17 superseded on the dev-endpoint
  choice only; everything else in the settled S3 design holds.
- **Done:**
  - `ImageUploadRules` (new): shared allow-list, 10 MB cap, spec §3.17 key convention, and
    stored-path parsing used by BOTH impls — interchangeability enforced by construction.
    `LocalImageStorageService` refactored onto it, behavior unchanged.
  - `S3ImageStorageService` (`AWSSDK.S3` 4.0.100.2): buffers uploads (cap enforcement even on
    non-seekable browser streams), returns the same `/uploads/{key}` stored shape as Local.
    `CreateClient` centralizes the three researched wire-format constraints that make "same SDK,
    different endpoint" actually true against both Garage and R2: `UseChunkEncoding = false`
    (R2 has no SigV4 streaming), checksum calculation/validation `WHEN_REQUIRED` (R2 lacks the
    SDK-v4 default trailers), `ForcePathStyle = true`. Full R2 dossier:
    `audit/ImageStorage.md` "R2 interchangeability".
  - `ImageEndpoints.MapImageServingEndpoints` (new): S3-mode-only `GET /uploads/{**key}` streams
    from the bucket (key validation via the same rules, immutable cache header); Local mode keeps
    serving the identical URLs from wwwroot via static files.
  - Program.cs provider switch: `ImageStorage:Provider` = `Local` (default; server-only path
    unchanged) | `S3` (singleton `IAmazonS3` + scoped service + serving route).
  - AppHost: `canalave-minio` replaced by `canalave-garage` (`dxflrs/garage:v2.3.0`,
    `--single-node --default-bucket` self-bootstrap, S3 API pinned 3900, `AppHost/garage.toml`
    bind mount, `canalave-garage-meta`/`-data` volumes, secrets `Parameters:garage-s3-secret`/
    `garage-rpc-secret`); injects `ImageStorage__*` env vars + `WaitFor(garage)` into web. Old
    minio container/volume/user-secret removed from the machine.
- **Verified (2026-07-05):** Integration — `S3ImageStorageServiceTests` (7 tests) against a real
  Garage Testcontainer via the production `CreateClient`; full `dotnet test` green (1,266:
  448 Unit + 446 RazorComponents + 372 Integration). Browser, full stack under the AppHost —
  `/settings` avatar upload as TestUser: DB row correct shape (psql 5433), exactly 1 object in
  `canalave-images` at exact byte size (Garage CLI), page renders from the bucket, direct GET 200
  + immutable cache header; replacement upload: bucket still 1 object, old URL 404 / new 200
  (first end-to-end exercise of the WU38 orphan cleanup against a real blob backend); Garage
  container restart: bootstrap idempotent, blob survived. The serving route is browser-band (not
  automated) because Program.cs reads the provider eagerly, pre-`WebApplicationFactory`-override —
  the documented TestAppFactory quirk; rationale in the audit Stage note.
- **Tool:** Claude Code (research-driven: Garage v2.3.0 + AWSSDK-v4/R2 compat verified against
  current sources 2026-07-05). **Pointer:** `audit/ImageStorage.md` (Shared Context +
  WU-S3Garage Stage note), `cross-cutting.md` "Aspire 13 Configuration",
  `run-server/SKILL.md` "Aspire path".

### WU-CI — Git/CI hygiene: CI + Dependabot (Phase 0) — DONE ✓ (2026-07-05)
- **Cells:** none (process/tooling work-unit — no feature cell changes stage; recorded as a
  `status.md` Global Condition). Executes `middle_plan_v2.md` Phase 0.
- **Done:**
  - `.github/workflows/ci.yml`: single job on `ubuntu-latest` — `actions/setup-dotnet` (10.0.x) +
    `actions/setup-node` (20, npm-cached on `TheCanalaveLibrary.Server/package-lock.json`, needed
    because the Server build's `NpmInstall`/`TailwindBuild` MSBuild targets shell out to npm) →
    `dotnet restore`/`build -c Release` (runs the Tailwind step) → `dotnet test --no-build -c
    Release` (all three tiers; `ubuntu-latest` ships the Docker daemon Integration's Testcontainers
    Postgres `postgres:18-alpine` + Garage `dxflrs/garage:v2.3.0` fixtures need; nothing else to
    configure — the suite is fully self-contained, no secrets/services block) → `dotnet list
    package --vulnerable --include-transitive`, `continue-on-error: true` (report-only by design).
    **Triggers: `pull_request` + `workflow_dispatch` only — no `push: master`**, a deliberate
    choice (see `middle_plan_v2.md` Resolved "CI hardening deliberately deferred to launch"):
    Brian tests locally before his own pushes, so CI's job is vetting Dependabot's PRs on GitHub's
    infra, not re-checking his own already-tested work.
  - `.github/dependabot.yml`: `nuget` ecosystem (directory `/`) with grouped rules — `aspire`
    (pattern `Aspire*`, enforcing the version-lockstep correctness constraint from
    `cross-cutting.md` "Aspire 13 Configuration") and `efcore` (`Microsoft.EntityFrameworkCore*`,
    `Npgsql.EntityFrameworkCore.*`); `npm` ecosystem (directory `/TheCanalaveLibrary.Server`, where
    `package.json`/Tailwind live). Weekly, capped at 5 open PRs per ecosystem.
  - `global.json` added at repo root (`"version": "10.0.100"`, `rollForward: "latestFeature"`) —
    previously absent; local dev, CI, and future prod builds now resolve the same SDK feature band
    instead of "whatever's installed." Verified it resolves against the installed 10.0.301 SDK.
  - `phase-a-foundation` merged into `master` (fast-forward, 0 conflicts — master was 0 ahead/38
    behind) and pushed. Branch convention settled: commit to master directly going forward (decision
    row 5, resolved — see `middle_plan_v2.md` Resolved).
  - GitHub web-UI steps (outside Claude Code's reach, Brian-performed): Dependabot security
    alerts/updates toggle (Settings → Code security); confirmed Actions enabled (public repo
    default). Branch protection deliberately not enabled yet — see the Resolved entry.
- **Verified (2026-07-05):** local pre-flight — `dotnet build TheCanalaveLibrary.sln -c Release`
  green (Tailwind step ran, 0 errors); `dotnet test TheCanalaveLibrary.sln --no-build -c Release`
  green (Docker running locally, Testcontainers Postgres + Garage came up). No automated test
  applies to the workflow/Dependabot YAML themselves (process config, not app code) — verification
  is the local pre-flight matching the workflow's exact commands, plus a post-merge manual
  `workflow_dispatch` run on GitHub confirming the cloud run is green end-to-end.
- **Tool:** Claude Code. **Pointer:** `middle_plan_v2.md` Phase 0 + Resolved (branch convention,
  CI-hardening deliberation), `status.md` Global Conditions.

### WU-DepBump1 — First Dependabot batch: all 7 PRs applied locally (2026-07-05) — DONE ✓
- **Cells:** none (dependency maintenance — no feature cell changes stage). Applied the whole
  first Dependabot wave directly on master rather than merging 7 PRs individually; Dependabot
  auto-closes its PRs when it sees the versions bumped on master.
- **Done:**
  - Test projects (×3): `coverlet.collector` 6.0.4→10.0.1, `FluentAssertions` 7.0.0→8.10.0,
    `Microsoft.NET.Test.Sdk` 17.14.1→18.7.0. RazorComponents additionally: **`bunit`
    1.33.3→2.7.2 (major, real API migration)** — all 40 test classes: `TestContext` →
    `BunitContext`, `RenderComponent<T>()` → `Render<T>()` (373 sites),
    `SetParametersAndRender` → `Render` (4 sites), `TestAuthorizationContext`/
    `AddTestAuthorization()` → `BunitAuthorizationContext`/`AddAuthorization()`
    (TagDirectoryTests), removed-abstraction `IRefreshableElementCollection<IElement>` → `var`
    (PaginationControlsTests). FluentAssertions 8 rename: `HaveCountLessOrEqualTo` →
    `HaveCountLessThanOrEqualTo` (2 Integration sites). `IRenderedComponent<T>`, `JSInterop.Mode`,
    `WaitForState`, `Services.Add*` all survived v2 unchanged.
  - `ServiceDefaults`: `OpenTelemetry.Instrumentation.AspNetCore` 1.15.2→1.16.0.
  - npm (`TheCanalaveLibrary.Server`): `tailwindcss` + `@tailwindcss/cli` 4.3.1→4.3.2
    (lockfile bump via `npm update`).
  - **FluentAssertions 8 licensing note:** v8 moved to a paid license for commercial use
    (free for non-commercial/OSS). Fine for this project as-is; revisit only if the project's
    commercial status ever changes (alternatives: stay on v7, or the Apache-licensed
    AwesomeAssertions fork).
  - `testing.md` tier table updated to the bunit v2 API names (Doc-Touch moment 2).
- **Verified (2026-07-05):** `dotnet build` -c Release green; full `dotnet test` green —
  1,266/1,266 (448 Unit + 446 RazorComponents + 372 Integration) on the new versions. The
  446 RazorComponents tests passing is the regression net for the bunit 2 migration itself.
- **Tool:** Claude Code (bunit 1→2 migration guide via live docs). **Pointer:** this entry;
  `testing.md` tier table.

### WU-Observability — Logging & telemetry conventions + additive OTel (middle_plan_v2 Phase 1 item 1) — DONE ✓ (2026-07-06)
- **Cells:** none (cross-cutting platform work-unit — recorded as a `status.md` Global
  Condition). Decision row 7 resolved as Doc-Touch moment 1 (Grafana LGTM on the droplet,
  chosen for the Claude-queried-on-demand consumption model; deploy stays Phase 7 — see
  `middle_plan_v2.md` Resolved).
- **Scope philosophy:** conventions + seams, not instrument-everything. Auto-instrumentation
  closes the visibility holes now (Npgsql per-query spans, .NET 10 Blazor circuit/component
  sources — the app's real execution path is the circuit, which the stock template never saw);
  custom spans only where auto-instrumentation is blind. The signal-buffering work consumes the seams as the
  named observability pilot for worker metrics.
- **Done:**
  - `Core/Diagnostics/CanalaveTelemetry.cs` (new cross-cutting cluster): per-component
    `ActivitySource`+`Meter` registry (`TheCanalaveLibrary.{Component}`; first component
    `ImageStorage`, reserved `ViewCount`/`Email`/`Marts`), wildcard-subscribed
    (`"TheCanalaveLibrary.*"`) in ServiceDefaults with no project reference (string literal,
    cross-commented).
  - ServiceDefaults: `Npgsql.OpenTelemetry` 10.0.3 (`AddNpgsql()` tracing; version tracks
    transitive Npgsql — dependabot efcore group widened to `Npgsql*`), `AddMeter("Npgsql")`,
    Blazor built-in sources/meters (`Microsoft.AspNetCore.Components` +`.Lifecycle`
    +`.Server.Circuits`), `EnrichWithHttpResponse` → `canalave.user.id` on request spans
    (response hook — auth runs after span start).
  - `Server/Telemetry/TelemetryCircuitHandler.cs` (new): scoped `CircuitHandler` wrapping every
    inbound circuit dispatch — `BeginScope` `CircuitId`/`UserId` (lazy from circuit-scoped
    `IActiveUserContext`) + `canalave.user.id` on `Activity.Current`; the dispatch-boundary
    counterpart to HTTP middleware, which circuit work never traverses.
  - Image-storage pilot: both impls (S3 + Local) emit `ImageStorage.Save`/`.Delete` spans
    (provider/kind/size tags, exceptions recorded + status Error on failure, no double-log),
    `canalave.image.uploads` + `.upload.size` metrics via shared `RecordUpload`, `Information`
    save logs, `Warning` on foreign-path delete no-ops (previously a silent return).
  - Silent-catch sweep (exhaustive; grep re-verified): the two
    `/* best-effort; log in a future structured-logging pass */` blob-delete sites
    (`ServerUserSettingsService`, `ServerStoryWriteService`) → `LogWarning` with
    `{ImagePath}`/`{UserId}`/`{StoryId}`; the two unlogged notification fan-out swallows
    (`ServerBlogPostWriteService`, `ServerGroupWriteService`) → `LogWarning` with entity IDs;
    consistency pass normalized Following's two `LogError`-without-IDs sites to the settled
    Warning-with-IDs shape; `ServerActiveUserContext` anonymous fallback annotated
    `sanctioned-silent` (the registry's first entry).
  - `canalave-conventions/logging.md` (new, linked from SKILL.md hub + cluster list): templates,
    level semantics (best-effort swallows = Warning, settled 2026-07-06), no-silent-catches +
    sanctioned registry, dispatch-boundary scopes, per-surface recipes (external call = worked
    example; worker stub for the signal-buffering work — the hub-stub half never shipped, since
    SignalR was permanently ruled out for messaging 2026-07-07), telemetry testing patterns,
    dashboard-reading guide.
- **Verified (2026-07-06):** Unit — `ImageStorageTelemetryTests` (4 tests: span tags/error
  status, metric values+tags via `MetricCollector`, `FakeLogger` level+structured-state;
  `Microsoft.Extensions.Diagnostics.Testing` added to Tests.Unit). Integration —
  `NpgsqlTracingSmokeTests` pins the `"Npgsql"` source name against silent upgrade breakage.
  The four swept catch-log sites are review-carried (DbContext-bound services — throwing-fake
  machinery disproportionate to one-line catches; rationale in `logging.md` §Testing). Full
  `dotnet test` green (1,271: 452 Unit + 446 RazorComponents + 373 Integration). Browser band:
  Aspire-path dashboard pass — circuit-parented Npgsql spans with SQL text, `ImageStorage.Save`
  span + S3 HTTP child on avatar upload, `canalave.*` metrics, `CircuitId`/`UserId` log scopes.
- **Tool:** Claude Code (Fable; plan approved 2026-07-06). **Pointer:**
  `canalave-conventions/logging.md`; `middle_plan_v2.md` Phase 1 item 1 + Resolved (row 7).

### WU-Security + WU-DataProtection — hardening pass + keyring persistence (middle_plan_v2 Phase 1 items 6–7) — DONE ✓ (2026-07-06)
- **Cells:** none flipped (cross-cutting platform work-units — `status.md` Global Condition;
  Stage notes in `audit/ImageStorage.md` + `audit/Identity.md`). Three design decisions
  resolved as Doc-Touch moment 1 (upload sniff+re-encode over sniff-only; write throttling at
  the L2 service layer, not HTTP-only — the middle_plan wording assumed comment/upload
  endpoints were HTTP, but they ride the SignalR circuit and `InteractiveAuto` keeps the
  circuit alive post-flip; keyring persisted unencrypted, no `ProtectKeysWith*` — see
  `middle_plan_v2.md` Resolved ×3).
- **Done (WU-DataProtection):** `AddDataProtection().PersistKeysToDbContext<ApplicationDbContext>()
  .SetApplicationName("TheCanalaveLibrary")`; `ApplicationDbContext : IDataProtectionKeyContext`
  + migration #20 `AddDataProtectionKeys` (`data_protection_keys`, Respawn-ignored);
  `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` 10.0.9. Fresh-scope
  `ApplicationDbContext` resolution at key-manager time verified safe (`ServerActiveUserContext`
  stores deps lazily; no query filters on the write context).
- **Done (WU-Security):**
  - Upload pipeline: new `Server/Images/ImageUploadProcessor` (throttle → claimed-MIME
    fast-fail → buffered 10 MB cap that no longer depends on `CanSeek` → `Image.Identify`
    sniff with only jpeg/png/webp decoders, sniffed format authoritative → header-level bomb
    guard (16384px / 64 MP) → decode first-frame-only → AutoOrient + EXIF/XMP/IPTC strip →
    ≤2048px downscale → re-encode); both storage impls consume it (S3's `CopyWithLimitAsync`
    and Local's `CanSeek` cap deleted as subsumed). SixLabors.ImageSharp **pinned 3.1.x**
    (4.x requires a build-time license key — Dependabot major-ignore in `dependabot.yml`).
  - Service-layer write throttle: `Core/Security/` (`WriteActionKind`, `IWriteRateLimitService`,
    `WriteRateLimitExceededException` with user-ready message + RetryAfter) +
    `Server/Security/ServerWriteRateLimitService` (singleton `PartitionedRateLimiter` of token
    buckets per (userId, kind)); `EnsureAllowed` calls in comment ×4 / messaging ×2 / report /
    story / chapter ×2 / blog post ×2 / recommendation creates + uploads via the processor.
    `TestAppFactory` defaults to pass-through `FakeWriteRateLimitService`.
  - HTTP edge: `AddRateLimiter` (global per-IP 10/min window on `POST /Account/*`, bodied 429
    + `Retry-After`; named `"TagWrites"` 30/min on the three tag write endpoints) +
    `UseRateLimiter` after `UseStaticFiles`.
  - Headers/CSP: `Server/Security/SecurityHeadersMiddleware` + pure `CspPolicy` builder —
    nosniff, `X-Frame-Options DENY`, Referrer-Policy, Permissions-Policy, COOP on every
    response; full CSP enforced outside Development / Report-Only in Development; per-request
    nonce → `<ImportMap nonce>`; `ContentSecurityFrameAncestorsPolicy = "'none'"`. SRI pinned
    on both Quill jsdelivr assets. **Inline-handler sweep:** all 12 raw `onerror=` attributes
    replaced with `data-fallback-src`/`data-hide-on-error`/`data-sprite-fallback` + new
    delegated `SharedUI/wwwroot/js/img-fallback.js` (capture-phase listener + attach sweep).
  - Identity hardening: lockout on (5 attempts / 15 min; `Login.razor`
    `lockoutOnFailure: true`), explicit cookie flags (`HttpOnly`/`SecurePolicy=Always`/
    `SameSite=Lax`).
- **Verified (2026-07-06):** full `dotnet test` green (1,306: 472 Unit + 446 RazorComponents +
  388 Integration; new — `ImageUploadProcessorTests` 11 incl. hand-crafted IHDR bomb,
  `ServerWriteRateLimitServiceTests` 4, `CspPolicyTests` 3, `DataProtectionPersistenceTests` 2
  incl. cross-factory survive-redeploy, `WriteThrottleTests` 3 with real limiter re-registered,
  `SecurityHeadersTests` 4, `HttpRateLimitTests` 4; `TagChipTests` updated to the `data-*`
  contract). Browser band (server-only path): **cookie-survives-restart drill** — filesystem
  key store moved aside, server process replaced, TestUser session survived and logout form
  POST 200'd (antiforgery valid; keyring provably from Postgres, no filesystem store
  recreated); **CSP Report-Only console watch** — zero violation reports across home / tags /
  discover / story / chapter / messages / settings / login incl. Quill CDN (SRI 200s, no digest
  errors); **upload pipeline live** — PNG-bytes-claimed-JPEG avatar stored as `.png` (served
  `image/png` + nosniff), 3000×100 PNG stored 2048×68, old avatar orphan-deleted; **delegated
  fallbacks** — `data-fallback-src` swap + `data-hide-on-error` proven via injected broken
  imgs, sprite chain advanced webp→static; **lockout** — 5 wrong ReaderGamma passwords →
  `/Account/Lockout` (counter verified in psql at 3/5 mid-drill; state reset after). Enforced
  CSP against production topology deliberately remains a Phase 7 checklist item.
- **Tool:** Claude Code (Fable; plan approved 2026-07-06). **Pointer:**
  `canalave-conventions/security.md` (new); `middle_plan_v2.md` Phase 1 items 6–7 + Resolved ×3;
  `audit/ImageStorage.md`, `audit/Identity.md`.

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

## Post-MVP — Layers 5–8 (additive, batchable; not sequenced here)

Per `grid_axes.md` §"The Two Boundaries": these swap method bodies / add DDL / add standalone workers
behind the contracts frozen in Layers 1–4. Batch by pattern when the MVP slice they sit behind is stable.

- **Messaging realtime push (SignalR) — REMOVED (2026-07-07).** Was tracked here as a Post-MVP
  additive layer on top of the stateless WU35 write service; permanently ruled out instead — Discord
  already covers real-time chat, and this site's messaging is deliberately async/long-form. Nothing
  in this project builds it now or later. See `cross-cutting.md` "Private Messaging Architecture" and
  `canalave-conventions/horizontal-scaling.md` §2 (no app-defined Hub means no SignalR backplane is
  needed at N≥2 either). Feature 49 L5 stays N/A.
- **L5 — WASM enablement (all features).** Map endpoints from the stable `IXService` contracts + `Client`
  HTTP impls. Includes the two genuine mechanical Stage-4 cells — **Story L5 endpoint wiring** (4/5 L5:
  `HttpStory*Service` call `/{id}/edit` + write routes `StoryEndpoints` never maps) and **Sprites L5**
  (`WasmSpriteReadService` optimistic URLs, 3 L5 Stage 4). Pointers: `audit/Stories.md` §3a,
  `audit/Sprites.md`. Governed by `layer5-wasm.md`.
- **L6 — SQL indexes (batch DDL).** Regenerate UserStoryInteraction filtered indexes off the re-modeled
  `has_started` columns (16/17 L6 Stage 4), comment golden index `(chapter_id, date_posted DESC)`, StoryTag
  reverse index, etc. FTS GIN on `StoryListing.SearchVector` already in `InitialSchema`. Pointers:
  per-folder audit L6 notes. Governed by `layer6-indexes.md`.
- **L7 — Redis integration.** **SUPERSEDED — see WU-SignalBuffering (2026-07-06) at the end of this
  file.** Layer 7 dissolved: signal buffering (44/45) built as L2 in-process buffers, 16/17 stays
  durable-direct, 61's cache is the L8 mart itself. `layer7-redis.md` deleted.
- **L8 — Data marts (+ horizontal boundary: requires real user data).** 59 Automatic Tree Search, 60 Tree
  Search Data Mart Worker, 61 Also Favorited / Also Recommended — no EF model, raw-SQL tables,
  zero-downtime swap. **62 SiteDailyStat Worker — DONE, see WU-SiteDailyStat (2026-07-11) below —
  is the one documented exception with an EF model** (superseding the "no EF model" note this bullet
  used to carry for all four rows). Pointers: `audit/Discovery.md` L8 notes, `audit/Moderation.md`
  Feature 62. Governed by `layer8-data-marts.md`.
- **Deferred workers (nothing to operate on yet — `grid_axes.md` horizontal note).** 57 Notification
  Cleanup Worker (nothing 60 days old), 58 UserStat Recalculation Worker (real-time counters carry MVP).
- **Image storage cloud backend — DONE, see WU-S3Garage (2026-07-05).** Was tracked here as a
  Post-MVP item (`S3ImageStorageService` behind the frozen `IImageStorageService`, MinIO endpoint in
  dev); built out of order and closed — F4/F20 L2 cloud-backend open item resolved, dev endpoint is
  Garage (MinIO OSS archived, superseded 2026-07-05), Cloudflare R2 in prod. Pointer:
  `audit/ImageStorage.md`.

---

## WU-SignalBuffering — DONE ✓ (2026-07-06)

Supersedes the "L7 — Redis integration" item above (and middle_plan_v2's WU-Redis). First-principles
audit of the deferred L7 assumptions: the write-behind's protect-reads-from-locks rationale was a
SQL-Server artifact (void under Postgres MVCC); Redis entered via the Aspire template, not a measured
need. Layer 7 dissolved — grid column removed; L8 keeps its number.

- **Built:** F44 reading-progress signal buffer (`ReadingProgressBuffer/Flusher/FlushWorker`,
  Server/Chapters/) + F45 view-count signal buffer (`ViewCountBuffer/Flusher/FlushWorker`,
  Server/Stories/) — in-process coalescing stores, 5 s `BackgroundService` flush via
  `unnest … ON CONFLICT`, shutdown drain, restore-on-failure, `CanalaveTelemetry` depth/batch/duration
  instruments. `DefaultSortOrder.RecentlyRead` (derived `MAX(uci.last_interaction_date)`) defaults the
  Bookshelves Actively Reading tab. `StoryViewStats` on-demand reveal in StoryCard's caret menu;
  `view-ping.js` (first scroll / 5 s dwell, never page load).
- **Migrations:** `R2_ViewCountToDailyStoryStats` (drops `view_count` from
  stories/chapter_contents/base_blog_posts; creates `daily_story_stats` — migration-managed raw DDL,
  no EF model, ground truth not a mart) + `R4_MvccStorageTuning` (fillfactor 90 on the two
  HOT-eligible flush targets; autovacuum_vacuum_scale_factor 0.05 on the three churn tables).
- **Settled:** F16 interactions durable-direct permanently (no lossy buffer for durable intent); view
  count never a sort key (non-sortable on-demand metric); no stored LastReadDate; the L8 mart IS the
  Also-Favorited cache; no CHECK constraints for flag pairs (spec §4 forbids nothing — see
  `audit/UserStoryInteractions.md` R3 divergence note); N≥2 body-swap detail (Valkey, session
  affinity, no SignalR backplane needed): `canalave-conventions/horizontal-scaling.md`.
- **Verified:** `dotnet test` 1335/1335 (483 Unit + 450 RazorComponents + 402 Integration; 22 new
  tests across all three tiers). Browser E2E 2026-07-06 with psql ground truth: chapter scroll →
  buffered flush landed the row; Actively Reading ordered most-recently-read-first; story-page ping →
  `daily_story_stats` row; "View stats" reveal = "1 view"; `/discover` sort dropdown view-free;
  favorite toggle durable across hard reload. R5 NULLS-ordering audit: all SQL-translated sort keys
  non-nullable or explicitly handled; RecentlyRead built NULLS-safe.
- **Docs:** `layer2-services.md` §"Signal Buffering" (new pattern home); `layer6-indexes.md` §"MVCC
  Storage Tuning" (+ 7-index audit: all justified); `layer7-redis.md` deleted; conventions SKILL.md
  axiom 7 + platform line; `grid_axes.md` "Layer 7 — dissolved" + F16/F44/F45 rows; CLAUDE.md grid
  columns; `status.md` L7 column removed + WU note; `middle_plan_v2.md` Phase 1 item 2 + Resolved
  "Layer 7 dissolved" (+ topology amendment: droplet runs server only). Spec NOT edited (read-only);
  divergence notes in `audit/Chapters.md` F44, `audit/Stories.md` F45,
  `audit/UserStoryInteractions.md` F16, `audit/Discovery.md` F61.

---

## WU-Email — Real transactional email (middle_plan_v2 Phase 1 item 5) — DONE ✓ (2026-07-06)

- **Cells:** none flipped (cross-cutting platform work-unit, same shape as WU-Observability/
  WU-Security — `status.md` Global Condition; Stage note in `audit/Identity.md`). Closes the
  sharpest beta blocker: `RequireConfirmedAccount = true` against `IdentityNoOpEmailSender`-only
  meant no real user could confirm an account. Mechanism decision (pluggable SMTP seam, Mailpit
  dev inbox, transactional-only scope) resolved as Doc-Touch moment 1, before the build — see
  `middle_plan_v2.md` Resolved "Email mechanism" and the narrowed decision row 8.
- **Done:**
  - `Server/Identity/EmailOptions.cs` (`EmailOptions`/`EmailSmtpOptions`, bound from `Email`,
    same shape as `S3ImageStorageOptions`) + `Server/Identity/EmailBodies.cs` (pure subject/body
    composition, unit-testable without SMTP) + `Server/Identity/SmtpEmailSender.cs`
    (`IEmailSender<User>` over MailKit; instrumented via a new `CanalaveTelemetry.Email`
    component — `Email.Send` span + `sent`/`failed` counters, per logging.md's reserved slot).
  - Provider switch in `Program.cs` (`Email:Provider` = `Smtp`/`NoOp`, default `NoOp`) — identical
    shape to `ImageStorage:Provider`; `NoOp` keeps `IdentityNoOpEmailSender` registered and its
    `RegisterConfirmation.razor` on-page link auto-hides once `Smtp` is active (no code change
    needed there).
  - **Mailpit dev inbox** added to `AppHost.cs` (`axllent/mailpit:v1.28.1`, same `AddContainer`
    shape as Garage; SMTP 1025, web UI 8025) + `Email__*` env wiring on the `web` project
    (endpoint-property callback form for host/port — the form that correctly resolves
    cross-resource hostnames). `MailKit` 4.17.0 package added to `TheCanalaveLibrary.Server`.
  - `run-server/SKILL.md` updated: Aspire-path resource count/description, comparison table
    "Email" row, Mailpit ground-truth verification technique (web UI + JSON API), a
    differs-per-path gotcha parallel to image storage's.
  - **Scope: transactional only** (confirmation, password reset, email-change). Notification
    email fan-out (`UserNotificationSetting.EmailEnabled`, still inert) explicitly deferred to a
    follow-up WU — hook point documented in `audit/Notifications.md` so it isn't re-discovered.
- **Real bug found and fixed during live verification, not anticipated in the plan:**
  `EmailBodies`' link-body methods re-encoded `confirmationLink`/`resetLink`, which every Identity
  page caller already HTML-encodes before calling `IEmailSender<User>` (the scaffold contract
  `IdentityNoOpEmailSender` relies on by interpolating verbatim). Double-encoding turned the
  link's `&amp;` query separator into `&amp;amp;`, which one round of browser HTML-decoding
  resolves to literal text instead of a real `&` — `code` then fails to bind on
  `ConfirmEmail`/`ResetPassword`. Found by comparing a live Mailpit message's raw HTML source
  against its browser-resolved form, confirmed with `psql` ground truth (pre-fix user:
  `email_confirmed` stayed `false` after clicking its link; post-fix user: flipped `true`). Fixed
  by removing the re-encode from the two link methods (`resetCode`, the one un-pre-encoded value,
  keeps its `HtmlEncode` call). Regression test added same-session
  (`EmailBodiesTests.ConfirmationBody_DoesNotReEncodeAnAlreadyEncodedLink`).
- **Verified:** `dotnet build` green (0 warnings). `dotnet test` 1344/1344 (491 Unit + 450
  RazorComponents + 403 Integration; 9 new — `EmailOptionsTests` 3, `EmailBodiesTests` 5,
  `EmailProviderSelectionTests` 1). The `Smtp` provider branch is deliberately **not**
  Integration-tested (same `WebApplicationBuilder`-reads-config-before-`WithWebHostBuilder`-
  override timing quirk `TestAppFactory`'s own class doc records for the connection string — see
  `EmailProviderSelectionTests`' class doc); it's proven live instead, matching the existing
  `ImageStorage:Provider` `S3`-branch precedent. Manual/browser band (Aspire path + Mailpit, real
  SMTP over MailKit, no mocks): registered a throwaway user → confirmation email landed in Mailpit
  with the configured From name/address → decoded link clicked → `email_confirmed` true in
  Postgres; ForgotPassword → reset email in Mailpit → link clicked → new password set → logged in
  with it (fresh auth cookie issued). Email-change reuses the identical
  `SendConfirmationLinkAsync`/`ConfirmationBody` path already proven twice, so it was not
  separately re-driven.
- **Tool:** Claude Code (Opus; plan approved 2026-07-06). **Pointer:** `identity-and-authorization.md`
  "Identity & Auth"; `audit/Identity.md` WU-Email Stage note; `middle_plan_v2.md` Phase 1 item 5 +
  Resolved "Email mechanism"; `audit/Notifications.md` (deferred notification-email hook point).

## WU-ErrorHandling — Error-handling strategy (middle_plan_v2 Phase 1 item 4) — DONE ✓ (2026-07-06)

- **Cells:** none flipped (cross-cutting platform work-unit, same shape as WU-Observability/
  WU-Email — `status.md` Global Condition). Resolved decision row 9 as Doc-Touch moment 1 (design
  conversation, four forks settled: scope split / layered islands / hybrid channels / localStorage
  autosave — see `middle_plan_v2.md` Resolved "Error-handling UX + strategy") and replaced
  `cross-cutting.md`'s "Gap — Not Yet Designed" section with the settled strategy; filled
  logging.md's two reserved WU-ErrorHandling stubs (level-table `Error` row + "Unhandled
  exceptions" three-tier contract). The `ProblemDetails` envelope + client HTTP translation half
  is **deferred to a Phase-5-adjacent follow-up** (no HTTP error surface exists until the WASM
  client makes those calls).
- **Done:**
  - **Containment** — `SharedUI/Errors/CanalaveErrorBoundary.razor` (ErrorBoundary subclass:
    Error-level log with `{Boundary}` island label + `{ErrorId}` trace id also shown in the
    fallback; user-gesture `Recover()`; auto-Recover on navigation). Layered placement: `page` +
    `chrome` islands in DesktopLayout/MobileLayout, per-card in StoryDeck, `comments` around all
    six CommentSection consumer sites.
  - **Message discipline** — `Core/Errors/ExceptionPresenter.cs` (typed user-facing exceptions
    surface their messages; `UnauthorizedAccessException`/`KeyNotFoundException` → fixed friendly
    text; everything else → generic + trace-id suffix). `SharedUI/Errors/InlineAlert.razor`
    replaces the hand-rolled per-form danger divs (Story/Chapter/BlogPost properties forms,
    CommentSection). CommentSection's raw `ex.Message` sites swept through the presenter — which
    also closed a real gap: its filtered catches let `WriteRateLimitExceededException` (comment
    posting IS throttled, security.md) escape to circuit teardown; the single-catch translate
    pattern now shows the rate-limit message inline. Editor pages' generic catches now log
    Error with entity IDs (logging.md tier-2 contract).
  - **Toast channel** — `SharedUI/Toasts/` (`IToastService`/`ToastService`/`ToastHost`,
    aria-live, auto-dismiss, registered in both hosts); host rendered by both layouts. Narrow by
    contract: transient non-blocking system events only; first consumer is "Draft restored."
  - **Draft safety** — `SharedUI/Drafts/` (`DraftStore` over new `js/draft-autosave.js`
    localStorage seam; `DraftAutosave` component: 10s change-only capture ticks, restore banner
    with relative age, no-edit sessions never write, identical-to-loaded backups silently
    cleared, `ClearAsync` on successful submit) wired into all four long-form editors
    (StoryEditorPage, ChapterEditorPage, BlogPostEditorPage, GroupBlogPostEditorPage; prose
    fields only — structured tag/character picker state deliberately excluded). Properties forms
    gained `Set*Async` push methods (Quill ignores later `Html` parameter changes).
  - **Last-resort surfaces** — `#blazor-error-ui` moved from MainLayout (Identity-Manage-only!)
    to `App.razor` so every page has a teardown surface — interactive pages previously had NONE;
    restyled to design tokens, mojibaked `??` dismiss glyph fixed; `MainLayout.razor.css`
    deleted. `ReconnectModal.razor.css` palette swapped to design tokens (structure/hook classes
    untouched). `CircuitOptions.DetailedErrors` = Development only.
  - **Test bed** — `SharedUI/Errors/DevErrorPlaygroundPage.razor` (`/dev/error-playground`,
    DevLoginBar-style Development gate) + `DevBreakableTile`: page fault / island fault / toast /
    true circuit teardown buttons — the standing browser-band vehicle for this WU's surfaces.
- **Discovery (containment stronger than expected):** an `InvokeAsync(() => throw)` fault from an
  event-handler context IS routed to the enclosing boundary (verified live — the log shows the
  `page` boundary catching it), so the playground's teardown button uses an `async void`
  continuation, which genuinely bypasses component dispatch and kills the circuit. Recorded in
  the playground's comments.
- **Verified:** `dotnet build` green (0 warnings). `dotnet test` 1374/1374 (500 Unit + 471
  RazorComponents + 403 Integration; 22 new — `ExceptionPresenterTests` 9 Unit;
  `CanalaveErrorBoundaryTests` 6, `InlineAlertTests` 5 (as counted by xunit cases),
  `ToastHostTests` 5, `DraftAutosaveTests` 5 RazorComponents). Browser band (server-only path,
  real circuit, TestUser): island fault degraded one tile while page+chrome+circuit survived;
  page fault showed the full-panel fallback whose on-screen Error ID **exactly matched** the
  server log record (`bcff6f63…`); Try again recovered in-place on the same circuit; toast
  rendered bottom-right in the aria-live region and auto-dismissed; `async void` fault tore the
  circuit down and the restyled `#blazor-error-ui` bar appeared; chapter-editor draft flow driven
  end-to-end (typed sentinel → 10s autosave to `draft:chapter:14` → reload → banner → Restore
  returned the sentinel to Quill + "Draft restored." toast → Save cleared the key), with `psql`
  ground truth at both ends (sentinel present in `chapter_contents` after save; seed row restored
  to original text afterwards).
- **Tool:** Claude Code (Opus; design forks resolved with Brian in chat, 2026-07-06). **Pointer:**
  `error-handling.md` §"Error Handling Strategy" (the settled strategy); `logging.md` §"Unhandled
  exceptions" (server contract); `middle_plan_v2.md` Phase 1 item 4 + Resolved; audit notes in
  `audit/Comments.md`, `audit/Chapters.md`, `audit/Stories.md`, `audit/BlogPosts.md`.

## WU-Marts — Extended seed track + discovery mart family (middle_plan_v2 Phase 1 item 9, scope expanded) — DONE ✓ (2026-07-07)

- **Cells flipped:** F59 L2/L8 `2→5`; F60 L8 `2→5`; F61 L2/L8 `2→5`, L6 `2→N/A` (reclassified —
  mart indexes are raw-SQL in the worker, matching F59's treatment). F59/F61 L3/L3.5 stay Stage 2
  (UI deferred by design — service layer only). Stage notes: `audit/Discovery.md` F59/F60/F61.
- **The decision that reshaped the WU (Doc-Touch moment 1, resolved with Brian in chat over
  several forks):** the horizontal line ("needs real user data") was crossed deliberately with
  synthetic *clustered* data instead of waiting for beta — uniform-random volume stays degenerate;
  the clustered distribution is the actual requirement. Full decision set (rCTE affirmed vs. a
  precomputed story→story matrix after auditing the original GeminiDiscussions deliberations;
  narrow `(user_id, story_id, edge_type)` mart superseding the wide-boolean design; six-edge
  taxonomy, every edge worth 1, no weights — two sort orders instead of spec §5.4's "scoring
  weights"; vouch = projection onto the vouchee's published stories in both tree searches,
  superseding spec §5.8's "strengthen edge weights"; author-spotlight first-class; hidden-favorite
  edge-owner consent → plain Favorite edge, "boosted" flag removed; path materialization
  service-required on chain-of-trust edge sets only; rating + exclusions at the presentation
  join): `middle_plan_v2.md` Resolved "Horizontal line crossed / discovery mart family",
  `layer8-data-marts.md`, `audit/Discovery.md`.
- **Done:**
  - **`TheCanalaveLibrary.SeedTool`** (new console project, references Core + Npgsql only; never
    on the startup or test paths): deterministic seeded-PRNG generator of taste-communities,
    power-law popularity, supernode recommenders, wired hidden-gem chains (curator→curator, ≤5
    cap respected), author spotlights, vouches biased toward low-volume authors, consent-split
    hidden favorites, and negative-test rows (drafts, pending, anonymized recs); loads via Npgsql
    binary COPY with one shared PBKDF2 hash; composes around the existing dev seed (MAX+1 id
    bases, refuses to run twice); re-syncs identity sequences.
  - **Marts + workers** (`Server/Discovery/`): `DiscoveryMartSchema` (raw SQL: narrow tree edge
    list + two covering indexes; `also_*_scores` + ranked covering indexes; fresh-staging swap
    with the load-bearing PK/index RENAMEs), `DiscoveryMartRebuilder` (scoped, per-mart rebuild,
    `CanalaveTelemetry.Marts` root spans + metrics), `DiscoveryMartWorker` (hosted: bootstrap +
    rebuild-when-empty + daily 03:00 UTC; failures keep the previous live table serving).
  - **F59 service** (`ITreeSearchReadService` / `ServerTreeSearchReadService`): static-SQL
    recursive CTE (CYCLE clause for pruning + native paths; LATERAL per-node fan-out LIMIT;
    `edge_type = ANY(@edges)` — no dynamic SQL), min-degree per story, random/by-degree sorts,
    chain-of-trust-only path materialization, presentation-join filters, `CanalaveTelemetry
    .Discovery` instrumentation incl. the cap-truncation flooding counter.
  - **F61 service** (`ICoOccurrenceReadService` / `ServerCoOccurrenceReadService`): ranked mart
    reads + visibility/rating/§8.7 exclusions; missing-mart degrades to empty-with-Warning.
  - Wire-up: Program.cs registrations; TestAppFactory removes `DiscoveryMartWorker` (rebuilds are
    test-deterministic via the rebuilder); `DevDiagnosticsEndpoints` migrated
    `Server/Endpoints/` → `Server/Diagnostics/` (legacy-folder rule) + four probes
    (`POST /dev/marts/rebuild`, `GET /dev/discovery/tree-search`, `/also-favorited/{id}`,
    `/also-recommended/{id}`).
- **Verified:** `dotnet build` green; `dotnet test` **1398/1398** (514 Unit + 471 RazorComponents
  + 413 Integration; 24 new — `DiscoveryMartTests` 10 Integration over Testcontainers Postgres:
  six-edge projection matrix w/ consent + visibility + anonymized rules, rebuild-twice rename
  dance, ranked co-occurrence both directions, Ignored exclusion, consent split, Also-Recommended
  mirror, wide degree-2 traversal, deep gem chain at degrees 2/4/6 with paths + depth cutoff,
  mature-silent-bridge, vouch projection; `TreeSearchRequestValidationTests` 12 +
  `MartsTelemetryTests` 2 Unit). Headless live band (server-only path + SeedTool data, 2000
  users / 3000 stories / 38k interactions loaded in 1.8s): `/dev/marts/rebuild` → 46,571 edges +
  463k/527k score pairs; also-favorited top-5 on a hub story rankable (17/16/16/16/15 — the
  "rankable, not just non-empty" bar); deep gem-chain traversal surfaced niche stories at degrees
  2/4/6 with legible paths; wide hub traversal fired `resultCapTruncated: true` (flooding
  indicator working). No browser band — headless-only by design; UI is deferred.
- **Deliberately NOT in scope:** F59/F61 UI (embedded sections, graph viz, sort toggles); Manual
  Tree Search build (WU40 — but its settled design, incl. the vouch live projection and full edge
  set, is recorded in `audit/Discovery.md` F33); the NBomber/k6 perf baseline (stays WU-L6,
  amended to run against the SeedTool dataset — now unblocked); workers 57/58/62.
- **Tool:** Claude Code (plan iterated with Brian through five revisions, 2026-07-06→07).
  **Pointer:** `layer8-data-marts.md` (authoritative conventions, now battle-tested);
  `audit/Discovery.md` F59/F60/F61 Stage notes + implementation notes + F33; `logging.md`
  (Marts/Discovery components); `middle_plan_v2.md` Phase 1 items 3/9 + Resolved.

## WU-L6 — Index batch + performance baseline (middle_plan_v2 Phase 1 item 3) — DONE ✓ (2026-07-07)

- **Cells flipped (L6):** F4/F5/F11/F12/F18/F23/F24/F31/F41/F42/F49 `2→5` (built + measured, or
  resolved as already-covered/rejected under R4 with the reason recorded); F61 L6 was
  reclassified `→N/A` under WU-Marts. F6/F7 (chapters) L6 stay Stage 2 — chapter-read queries
  were not assessed this pass. Stage notes in each cluster's audit file.
- **Headline reality finding — the USI index collapse:** the seven `user_story_interactions`
  filtered covering indexes, declared since WU0, were declared with UNNAMED
  `HasIndex(e => e.UserId)` calls — EF collapses unnamed HasIndex calls on the same property set
  into ONE index (each call overwrites the previous filter/name), so the database contained only
  `ix_user_story_interactions_has_started`. Six bookshelf tabs ran unindexed for the project's
  whole life, invisible at dev-seed volume; both the WU15 (2026-06-22) L6 verification and the
  R4 (2026-07-06) index audit had audited the *config file*, not the database. Corrected in
  `audit/UserStoryInteractions.md`; the two rules (name argument is load-bearing; verify index
  claims against `pg_indexes`/the snapshot, never the config) are now in `layer6-indexes.md`.
- **Done:**
  - **SeedTool extended** with 323,817 threaded chapter comments (popularity-weighted, 42k
    replies) + 20,073 notifications (derived from real favorite/rec/vouch actions) — comment and
    notification indexes are unmeasurable at toy volume.
  - **`TheCanalaveLibrary.PerfBaseline`** (new console project, Npgsql only, dependency-free by
    design — NBomber v5 licensing / k6 external binary disqualify them for a forever-rerunnable
    fixture): 12 SQL scenarios lifted verbatim from the hot service methods (provenance comments
    = the R4 trail), deterministic hottest-id parameter pools, p50/p95 over 40 iterations,
    `EXPLAIN (ANALYZE, BUFFERS)` capture per scenario, `--label`/`--compare` workflow; results
    committed under the project's `results/`.
  - **`L6_IndexBatch` migration:** the six restored USI filtered indexes (named HasIndex); four
    comment golden composites (chapter/blog/group/profile × `(owner_id, date_posted)`, superseding
    their FK indexes); `ix_notifications_recipient_read_date`; `ix_stories_published_date` +
    `ix_stories_last_updated_date`; `ix_private_messages_conversation_id_date_sent`.
  - **Rejected under R4, recorded with reasons** (`layer6-indexes.md` §"Rejected"): story-centric
    USI mirrors (no story-centric query exists — counts denormalized), `user_story_interaction_dates`
    date indexes (table never read), USI composite-boolean partials (≤0.13 ms measured),
    `story_tags` reverse composite (PK already optimal — measured neutral), followed_users sort
    index, tag trigram, rating-prefixed sort spines.
- **Measured (SeedTool volume, local PG18, p50 of 40 iterations on hottest ids):** comment roots
  page **24.32→0.29 ms (−98.8%, p95 136.82→0.38 ms** — the before-plan burned ~20 ms on
  parallel-worker launch + sort; after = backward index scan into the LIMIT, 0.05 ms execution);
  roots count −97%; discover DatePublished page −76%; §8.7 exclusion probe −68%; unread count
  −47%; favorites tab −33%. Honest neutrals: notifications newest-first +6.7% (per-user residual
  sort, by design), tag filter +7% (PK was already optimal — confirms the rejection); tree-search
  / co-occurrence deltas are cache noise (no mart index changed).
- **Verified:** `dotnet build` 0 warnings; `dotnet test` **1398/1398** (514 Unit + 471
  RazorComponents + 413 Integration — the Integration tier migrates through `L6_IndexBatch` on
  Testcontainers Postgres every test run); `pg_indexes` confirms all seven USI indexes + the new
  set; before/after EXPLAIN plans committed.
- **Tool:** Claude Code. **Pointer:** `layer6-indexes.md` (rewritten against reality — the
  authoritative L6 doc); `TheCanalaveLibrary.PerfBaseline/results/`; audit L6 notes in
  `UserStoryInteractions.md` (the correction), `Comments.md`, `Notifications.md`, `Messaging.md`,
  `Stories.md`, `Tags.md`, `Following.md`, `Discovery.md` F31; `middle_plan_v2.md` Phase 1 item 3.

## WU-DesignSystem — Design solidification: role system, token manifest, re-role sweep (plan transient-tinkering-narwhal) — DONE ✓ (2026-07-10)

- **What:** The codebase's first semantics pass over the visual layer (git-verified etiology:
  Tailwind v4 toolchain from first commit, v3-idiom blind authorship — every role-level choice
  predates visible rendering). Ratified constitution + seven element roles (Canvave/Wayfinding/
  Container/Content Surface/Control/Indicator/Overlay); locked role-based `@theme` manifest at a
  live gate review on `/dev/design-gallery` (canvas vibrant grass, action light-fill+dark-ink,
  mission surf blue held at 0.56 by the AA-4.5-everywhere contrast policy, HP-trio indicators,
  Pokémon-type tag tokens, feature accents tokenized, Fraunces/Mulish shipped); built
  `ContentSurface` (Reading/Inline/Input, side-rails frame) and wrapped all 17 RTV/EV sites
  (MessageItem de-bubbled per ratification); `ReaderDisplayProvider` wired (cascade finally has
  a provider) + Phase E `ReadingBackground` reader override (L1 JSON field + migration
  `ReaderBackgroundOverride`, L2 service mapping, settings select, ContentSurface consumption);
  action/mission families replaced primary/accent (alias bridge deleted); Interaction States
  grammar (one neutral hover, global `:focus-visible` ring, z-ladder/backdrop/shadow tokens,
  uniform dismissal via `dismiss.js`, tint-recipe badges/buttons); Identity fully restyled
  (31 pages + Shared — Bootstrap debris deleted, carve-out revoked); vessels/plaques on all
  bare pages; `scripts/check-design-tokens.ps1` wired into CI as the permanent silent-failure
  feedback loop.
- **Verified:** `dotnet test` all tiers green (479 RazorComponents incl. new ContentSurfaceTests
  + 514 Unit + 413 Integration); token check green; browser walk (chapter reading on paper,
  Discover/Bookshelves/Tags on the new system). Visual sign-off of every swept page remains the
  standing L4 human pass — L4 cell Stages unchanged by this WU.
- **Tool:** Claude Code (+ parallel subagents for wraps/vessels/Identity). **Pointer:**
  `layer4-style.md` §"Element Roles"/"Interaction States"/"Prerequisite: Design Tokens";
  `.claude/design/surface-registry.md` (audit + ratifications + sweep completion);
  plan `~/.claude/plans/transient-tinkering-narwhal.md`; palette artifact (rev 3.1).

## WU-SiteDailyStat — SiteDailyStat worker + user activity tracking + /mod/stats dashboard (Feature 62) — DONE ✓ (2026-07-11)

- **Cells flipped:** F62 L1/L2/L3-Logic/L3.5/L4.5/L8 `→5`; L4 `→3` (functional, not design-reviewed);
  L5/L6 stay N/A. Row 62 was the last unbuilt Layer-8 mart cell — the mart family is now complete.
  Stage note: `audit/Moderation.md` Feature 62.
- **The decision that reshaped the WU (Doc-Touch moment 1, resolved with Brian in chat over several
  rounds):** reconciled the Gemini design source (2025-10-29 deliberation) against the live schema
  via a full counter-by-counter source audit. Key calls: **`SiteDailyStat` gets an EF model** — the
  one documented Layer-8 exception, superseding "no EF model" — because it's append-only ground
  truth with rich time-series reads (a dashboard), not a rebuildable mart; the worker still writes
  only via raw SQL. **`new_`/`total_` rule**: a stock (`total_`) column exists only where the
  cumulative level is a headline platform-size curve AND the population can shrink (deletions/
  takedowns) — exactly three: users, stories, words; everything else is flow-only. **Active-users
  privacy stance**: `User.LastActiveUtc` stamped for authenticated requests only, riding the
  existing auth-session cookie — first-party functional data, no tracking cookie, no consent
  banner, consistent with the ad-free community ethos; aggregate DAU counts everyone, the public
  "last seen" *display* alone is gated by the pre-existing `PrivacySettings.ShowActivityStatus`.
  **User-facing dashboard is in scope** (beyond MVP — "flourishes," per Brian) — activates
  L2–L4.5 for this row. `stories_approved` dropped at build time (no dated column exists on the
  approval path); `favorites_added` confirmed sourceable from `UserStoryInteractionDate`.
  Full detail: `layer8-data-marts.md` §"site_daily_stats", `middle_plan_v2.md` Resolved.
- **Done:**
  - **L1** (`AddSiteDailyStatAndUserActivityColumns` migration): `User.CreatedUtc`/`LastActiveUtc`;
    `SiteDailyStat` EF entity (PK `stat_date`) + Fluent config — applied clean to the standing dev
    DB (3012 stories, 2007 users, all backfilled to the migration's deploy instant, confirming the
    documented one-time `new_users` deploy-day-spike limitation).
  - **L2 signal buffer** (`Server/Identity/`): `UserActivityBuffer`/`UserActivityFlusher`/
    `UserActivityFlushWorker` + `ServerUserActivityWriteService` — a third Signal-Buffering
    instance (latest-timestamp coalescing, `GREATEST` null-tolerant merge); `UserActivityTracker`
    (non-visual, mounted once in `Routes.razor`, stamps on circuit start + every navigation for
    authenticated users only — an approximate, go-forward-only signal, documented as such).
  - **L8 worker** (`Server/Moderation/`): `SiteDailyStatAggregator` (one raw
    `INSERT … ON CONFLICT (stat_date) DO UPDATE`, all ~19 counters via scalar subqueries; day
    boundaries as explicit UTC range parameters, never a `::date` cast — session-timezone-safe) +
    `SiteDailyStatWorker` (hosted: bounded 30-day startup gap-fill + daily 03:00 UTC, reusing
    `DiscoveryMartWorker.DelayUntilNext`); `CanalaveTelemetry.UserActivity` instrumentation.
  - **L2/L3/L4 dashboard**: `ISiteDailyStatReadService`/`ServerSiteDailyStatReadService` (plain
    LINQ — the one L8 table with an EF model); `/mod/stats` (`ModStatsPage.razor` — stat tiles,
    3 small-multiple growth charts, DAU chart, 2-series reports-filed-vs-resolved chart, and a
    data table for the 12 flow counters in place of an over-wide bar chart, per the dataviz
    skill); `DailyStatLineChart`/`StatTile`/`ActivityRow` (self-contained inline SVG, no external
    chart CDN, `sr-only` data-table fallback per component).
  - **Profile "last seen"**: `ProfileHeaderDto.LastSeenUtc` + `ServerUserProfileReadService` +
    `ProfileBanner` — same gating shape as the existing `ShowUserStats`/`Stats` pattern.
  - Wire-up: Program.cs registrations; `TestAppFactory` removes `UserActivityFlushWorker` +
    `SiteDailyStatWorker`; `POST /dev/marts/site-daily-stat` diagnostic probe.
- **Verified:** `dotnet build` green; `dotnet test` **1421/1421** (524 Unit + 479 RazorComponents +
  418 Integration; new: `UserActivityBufferTests`, `SiteDailyStatWorkerTests` (Unit — made the two
  day-window helpers `public` test seams per the repo's no-`InternalsVisibleTo` convention),
  `SiteDailyStatAggregatorTests` (every counter + day-boundary exclusion + recompute-not-accumulate
  idempotency), `UserActivityFlushTests` (Integration)). No RazorComponents test for `ModStatsPage`
  — its `@code` is a thin init-load with no non-trivial computed state, which the repo's own
  testing convention says to skip; sibling mod pages carry no RazorComponents test either. Live
  browser verification (server-only path, standing dev DB, not wiped): the startup gap-fill
  backfilled 30 real days unprompted (`new_comments` varying 121–283/day); `/mod/stats` rendered
  live as AdminUser; the activity-buffer→flush→"Last seen Jul 11, 2026" loop confirmed end-to-end
  for both owner and non-owner profile viewers.
- **Tool:** Claude Code. **Pointer:** `layer8-data-marts.md` §"site_daily_stats";
  `audit/Moderation.md` Feature 62 Stage note; `layer2-services.md` §"Signal Buffering";
  `layer1-data-model.md` §"Column Conventions"; `middle_plan_v2.md` Resolved.

## WU-Seo — Open Graph / social-sharing meta tags (addendum §3 #15/#17) — DONE ✓ (2026-07-11)

- **Cells flipped:** none — `Seo/` is a new cross-cutting cluster with no grid Feature (same shape
  as `Images/`), so no `status.md` row changes. The consuming features' own cells (Stories F4,
  Chapters F6, Profiles F20, BlogPosts F35, Groups F38, plus Series/Groups' L3/L3.5) are unaffected:
  OG tags are additive `<head>` output, not a change to any existing Stage-5 behavior.
- **Origin:** `.claude/middle-addendum.md` §3 items #15/#17 — flagged "never surfaced anywhere" for
  a live public UGC site; #17 called Discord-unfurl the single highest-leverage growth item for this
  audience. Scope confirmed with Brian: OG + Twitter card + `<meta name="description">` on all six
  shareable content types; mature-content `noindex` (#18) explicitly deferred to a follow-up unit.
- **Done:**
  - **Core/Seo/**: `IPublicUrlProvider` + `PublicUrlProvider` (pure string builder, same shared-impl
    shape as `OptimisticSpriteReadService` — Server constructs it from `Site:PublicBaseUrl`/
    `ImageStorage:PublicBaseUrl` config, Client from `NavigationManager.BaseUri`); descriptions via a
    standalone `SocialDescriptionHelper` (HTML strip, entity decode, word-boundary truncation).
  - **SharedUI/Seo/**: `<SocialMetaTags>` — one `<HeadContent>` component emitting the full tag set,
    parameterized by `Title`/`Description`/`ImageUrl`/`Url`/`OgType`.
  - Wired into `StoryPage` (article, real cover, canonical slug), `ChapterReadingPage` (article,
    falls back to the parent story's cover/blurb via the lightweight
    `GetListingsByIdsAsync` projection — not the heavier `GetStoryByIdAsync` — loaded in parallel
    with the page's existing TOC/versions calls), `ProfilePage` (profile, avatar + tagline),
    `SeriesPage`/`GroupPage` (website, no cover field → site-default image), `BlogPostPage` (article
    — also gained its first-ever `<PageTitle>`, which the page was missing entirely before this unit).
  - `StoryDetailsDTO.Slug` added (projection in `ServerStoryReadService.GetStoryByIdAsync`) so
    `og:url` can be canonical without waiting on the separate, still-unbuilt slug-redirect item
    (addendum #16).
  - **Settled, not just built** (Doc-Touch moment 1, before code): base URLs are always configured,
    never `NavigationManager.BaseUri` server-side — the Cloudflare→DigitalOcean topology and planned
    N≥2 droplets make request-derived URLs unsafe. A separate `ImageStorage:PublicBaseUrl` (defaults
    to the site base) was wired in *now*, ahead of need, as the seam for a planned future direct-R2/
    CDN image-serving migration. Static OG fallback tags in `App.razor` for non-content pages were
    deliberately **not** added — discovered mid-build that `<HeadOutlet>`/`<HeadContent>` only ever
    *append* into `<head>`, never override by tag name, so a static default would duplicate
    `<SocialMetaTags>`'s own tags on every page using it. Full reasoning: `audit/Seo.md`,
    `render-and-layout.md` §"Social Meta Tags (Open Graph)", `middle_plan_v2.md` Resolved.
- **Verified:** `dotnet build` green. `dotnet test` green across all three tiers — Unit 609/609
  (24 new: `PublicUrlProviderTests`, `SocialDescriptionHelperTests`), RazorComponents 564/564
  (`ProfilePageTests` updated to register `IPublicUrlProvider` — the only one of the six dispatchers
  with a direct bUnit render test), Integration 516/516 (confirms the `StoryDetailsDTO.Slug`
  projection change didn't break existing Story read-service coverage). Browser band (server-only
  path, standing dev DB, not wiped): `curl`'d the **prerendered** HTML (no JS — what a crawler
  actually sees) for one seeded page of each of the six types; all correctly emit
  `description`/`og:site_name`/`og:type`/`og:title`/`og:description`/`og:url`/`og:image`/`twitter:*`
  exactly once each (confirming the App.razor-duplication risk above was correctly avoided).
- **Tool:** Claude Code. **Pointer:** `audit/Seo.md`; `render-and-layout.md` §"Social Meta Tags
  (Open Graph)"; `canalave-conventions/SKILL.md` "Seo/" cluster entry; `middle_plan_v2.md` Resolved;
  `middle-addendum.md` §3 items #15/#17.

## WU-Spotlight — Community Spotlight, full feature minus donations (Feature 55) — DONE ✓ (2026-07-12)

- **Cells flipped:** F55 L1/L2/L3-Logic/L3.5/L4.5 `1→5`; L4 `1→3` (functional, not design-reviewed
  — the row-62 precedent); L5 `N/A→2` (a real browser surface now exists; endpoint+client pair due
  in the Phase-5 batch). L6/L8 stay N/A (the `(start_date, end_date)` composite index shipped as
  part of L1 — low-volume table, no measured L6 pass warranted; the go-live worker is an L2-style
  hosted service, not a mart). Stage notes: `audit/Spotlight.md`.
- **The decision that shaped the WU (Doc-Touch moment 1, resolved with Brian in chat over three
  rounds, 2026-07-11):** the Gemini pledge-drive design is requirements-spirit only; implementation
  is first-principles. Settled model: donation-funded slot grants with donations DEFERRED past beta
  (`ISpotlightSlotAllocator` is the seam — mods grant now, the payment pipeline grants later);
  donor picks someone else's story (self-rec fine, self-story never); display = additive
  composition (StoryCard + optional RecommendationCard); discrete calendar blocks × N mod-set
  homepage positions; schedulable future start; per-story cooldown; DB-backed mod-editable knobs
  (the new cross-cutting SiteSettings cluster); three notifications (grant inline, story-author +
  recommender at go-live via worker). Full record: `audit/Spotlight.md`, `middle_plan_v2.md`
  Resolved "Community Spotlight model".
- **Done:**
  - **L1** (`WU_Spotlight_SlotsAndSiteSettings` migration): two-table split — new `SpotlightSlot`
    (entitlement: granted-to/by, `Source` ModAward|Donation, `Status`, reserved `PaymentId`) +
    reshaped `CommunitySpotlight` (placement: unique `SlotId` FK Restrict, `RecommendationId`
    SetNull, `GoLiveNotifiedUtc` stamp; dropped `SponsorComment`, moved `PaymentId` to the slot);
    `SiteSetting` string-key table seeded from `SiteSettingKeys` (5 spotlight knobs);
    `CommunitySpotlight.cs` migrated out of the legacy `Core/Models/` folder to `Core/Spotlight/`;
    3 seeded `NotificationType` rows (90–92).
  - **L2:** `ISpotlightReadService`/`ISpotlightWriteService`/`ISpotlightSlotAllocator` +
    `ISiteSettingsRead/WriteService` (new cross-cutting cluster). Redemption validates inside one
    `pg_advisory_xact_lock`-serialized transaction under `CreateExecutionStrategy()` (the
    `UserDeletionService` precedent) — no self-story, public-status story, rec-belongs-to-story,
    on-grid/horizon block, per-story cooldown (both directions), capacity count-then-insert.
    `SpotlightBlocks` (Core, pure) owns the epoch-anchored computed grid — never stored, so knob
    changes rewrite no data. Displays read by joining the filtered DbSets (viewer's
    ContentRating/IsTakenDown do the work) and compose `GetListingsByIdsAsync` +
    `IRecommendationReadService` for presentation.
  - **Worker:** `SpotlightGoLiveWorker` (1-min sweep) / `SpotlightGoLiveSweeper` (testable body,
    the SiteDailyStat split): fires `StorySpotlighted`/`RecommendationSpotlighted` when a window
    opens, stamps `GoLiveNotifiedUtc` (fires-once); fully-elapsed windows age out unnotified.
    `TestAppFactory` removes the worker.
  - **UI:** `CommunitySpotlightDisplay` slotted into `HomeDesktop`/`HomeMobile` (placeholder home
    page now carries a real section); `SpotlightRedemptionPage` (`/spotlight`, UserMenu link) —
    own-recs/hidden-gems primary pick path + `StoryTitlePicker` secondary, any-of-the-story's-recs
    attach (own rec preselected), block calendar with per-block occupancy; `ModSpotlightPage`
    (`/mod/spotlight`) — grant-by-exact-username (reuses
    `IMessagingReadService.FindUserByUsernameAsync`), monthly-cap display, revoke, knob editor.
- **Verified:** `dotnet test` green across all three tiers — 1782/1782 (Unit 636 incl. 12 new
  `SpotlightBlocksTests`; RazorComponents 582 incl. 12 new across display/redemption/mod-page;
  Integration 564 incl. 20 new `SpotlightServiceTests` — grant cap/roles/donation-seam, all
  redemption rejections, an advisory-lock two-racers-one-opening test, sweep fires-once, FK
  cascade/SetNull, settings round-trip). Browser band (server-only path, standing dev DB kept, not
  wiped): migration applied cleanly on startup; as AdminUser granted a slot to TestUser from
  `/mod/spotlight` (capacity 12→11, grant listed); as TestUser redeemed via the primary pick path
  into the current block; the placement rendered live on `/` (StoryCard + RecommendationCard);
  psql ground truth confirmed slot Redeemed, placement row, worker stamp landing within its 1-min
  cadence unprompted, notification 90→awardee + 91→story author, and NO 92 (sponsor attached their
  own rec — drop-self correctly suppressed). One runtime bug found + fixed same-session: unbreakable
  rec text forced the homepage grid wider than the viewport (grid items' `min-width:auto`) —
  `min-w-0` + inherited `break-words` wrappers in `CommunitySpotlightDisplay`. Token check: no new
  findings (3 pre-existing in Discovery/Import files belong to their in-flight WUs).
- **Deferred (Phase-4 verdict rendered):** donation/payment pipeline (second allocator source +
  `PaymentId`), activity/cost-scaled formula for N, Patron badge (`SpotlightCount`), slot expiry.
- **Tool:** Claude Code (Fable). **Pointer:** `audit/Spotlight.md`; `layer2-services.md`
  §"Community Spotlight" + §"Site Settings"; `SKILL.md` "SiteSettings/" cluster entry;
  `folder_clusters.md` Spotlight/SiteSettings rows; `middle_plan_v2.md` Resolved + Phase 2 item 1.

## WU-Polls — Polls, full feature (Feature 37; Phase-4 verdict rendered) — DONE ✓ (2026-07-12)

- **Cells:** F37 L1 (Stage 4 reconcile → 5), L2/L3/L3.5/L4/L4.5 (→ 5). L5 stays 2 (codebase-wide
  InteractiveServer posture, same as F35/F36). Closes spec Open Question #6 and renders row 3's
  Feature-37 beta-scope verdict (designed + built — `middle_plan_v2.md` Resolved "Polls
  requirements").
- **Requirements settled in chat 2026-07-12** (full record: `audit/BlogPosts.md` F37
  "Requirements settled"): per-poll owner config (AllowMultiple / ResultsVisibility
  AfterVote·Always·AfterClose / AnonymityMode Anonymous·Public·VoterChoice), config locks after
  first vote, nullable `DateClosed` lifecycle (scheduled open, indefinite, manual close; archive
  orthogonal), min-2 options no cap, fully editable while open with a 30-min quiet-period
  `PollUpdated=100` voter notification batch, retract-hides-AfterVote-results, optimistic-local
  tallies (no SignalR), mods create Site inline on `/polls`, authors create Blog polls in the
  editor, blocks after post content.
- **L1 reconcile:** `WU_Polls_ConfigLifecycleAndShadowFkFix` migration — config columns +
  `poll_votes.is_anonymous` + nullable `date_closed` + `last_edited_at`/`edit_notified_at`
  (partial index) + drops the spurious `base_polls.base_blog_post_blog_post_id` shadow FK
  (`BaseBlogPost.Polls` was `ICollection<BasePoll>`; retyped to child + explicit pairing). Poll
  entities moved `Core/Models/` → `Core/BlogPosts/`.
- **L2:** `IPollReadService`/`IPollWriteService` + `ServerPoll{Read,Write}Service`
  (`Server/BlogPosts/`), `PollDto` family, `PollRules` (Core, dependency-free), staged option
  reconcile inside an execution-strategy transaction (non-deferred unique indexes), server-side
  results-visibility zeroing. `PollEditNotificationSweeper`/`Worker` (SpotlightGoLive split;
  TestAppFactory removes the worker).
- **UI:** `PollView` (self-contained vote composite), `PollEditorForm` (presentational),
  `PollsPage` `/polls` (active + archived, inline mod management), `BlogPostPage` poll blocks,
  `BlogPostEditorPage` Polls section. `PollValidationException` registered in
  `ExceptionPresenter`.
- **Verified:** `dotnet test` green all tiers (~38 new: `PollRulesTests`, `PollEditDtoTests`,
  `PollServiceTests` incl. sweep). Browser band (server-only, standing dev DB kept): full detail
  in the F37 L4.5 Stage note — create/vote/anonymity/AfterVote-gate/retract/config-lock/
  multi-vote flows all psql-ground-truthed; the REAL 1-min worker delivered the quiet-period
  notification unprompted. **Two runtime bugs found via browser and fixed same-session** (TPT
  cross-child projection coercion on `OfType` sources; bool `<select @bind>` case-mismatch) —
  conventions recorded in `layer1-data-model.md` §TPT and `layer3-logic.md`, regression tests
  added. Token check: no new findings (Import's pre-existing in-flight finding only).
- **Deferred:** home-page SitePoll surfacing (folded into homepage-sections decision row 2).
- **Tool:** Claude Code (Fable). **Pointer:** `audit/BlogPosts.md` F37; `layer4-style.md`
  Pattern Accumulation "PollView / PollEditorForm"; `middle_plan_v2.md` Resolved + row 3.

## WU-L5Sweep — Mechanical Layer-5 add: every ServerXXXService gets an HTTP endpoint + client impl — DONE ✓ (2026-07-13)

- **Goal:** get the whole codebase flip-ready for `InteractiveAuto` — add the minimal-API
  endpoint + `HttpClient` client-impl pair for every `ServerXXXService` not already built
  (Tags/Tag Directory were the only pre-existing Layer-5 surface, WU-L5Pilot). Explicitly
  **add-without-verify**: no per-feature Integration/Unit tests, no browser pass, no
  `App.razor` render-mode flip — those remain future work. Compile-clean is the only bar.
- **Doc-Touch moment 1 (before any code):** `layer5-wasm.md` hardened — canonical
  `/api/{kebab-plural-entity}` naming table; exception→status table extended from Tags' original
  3 cases to the full ~10-case set actually thrown across the service layer; POST-for-complex-reads
  rule (non-scalar params can't GET-bind); `PagedResult<T>` ruling for the 6 tuple-returning listing
  methods; stream/multipart pattern (upload via `MultipartFormDataContent`/`IFormFile`, download via
  direct anchor-link, never a client service round-trip); self-referential/read-only single-class
  client shapes. Grid correction: L5 Stage 5 had drifted to mean two things — Groups (F38–40) and
  Recommendations (F27–29) were marked Stage 5 off service-layer test citations with **no
  endpoint/client ever built**; corrected to Stage 2 (`status.md`, `audit/Groups.md`,
  `audit/Recommendations.md`). Also fixed mid-implementation (Doc-Touch moment 2): `StoryEditorPage`
  injected `IImageStorageService` directly (a stray bypass of the service-owns-the-upload pattern);
  added `IStoryWriteService.UploadCoverArtAsync` so cover upload flows through the same pattern as
  `IUserSettingsService.UploadProfilePictureAsync`.
- **Shared infra (once, used by all 20 clusters below):** `Server/Http/EndpointHelpers.cs`
  (`ExecuteWriteAsync` — the one copy of the exception→status map, validation-exception matching by
  type-name suffix since the 13 `{Feature}ValidationException` types share no common base);
  `Core/Http/PagedResult.cs`; `Client/Http/ClientHttpHelpers.cs` (shared `ProblemDetails.Detail`/
  `retryAfterSeconds` extraction; exception *construction* stays per-feature). Deleted the stale
  `Server/Endpoints/StoryEndpoints.cs` (flat deprecated folder, already claiming `/api/stories`).
- **Swept (20 cluster tasks, one endpoints class + client impl per interface, mostly built via
  parallel subagents against the hardened doc + the Tags reference implementation):** Stories
  (Story/StoryArc/StoryLineage/ViewCount-ping), Series, Chapters (Chapter/ChapterReadMark/
  ReadingProgress-ping), Comments, UserStoryInteractions, SavedTagSelection, Following, Profiles
  (UserProfile/UserSettings incl. multipart upload) + Sprites/Theme, Recommendations, BlogPosts
  (BlogPost/Poll), Notifications, Discovery (ManualTreeSearch/TreeSearch/DiscoveryDefaults/
  CoOccurrence — all POST-reads; minted a small `ResplitRequest`/`TreeSearchListingRequest`
  transport envelope apiece for the two-complex-param methods), Groups, Moderation
  (Moderation/SiteDailyStat), Messaging, Spotlight, SiteSettings, Badges, Import (three multipart
  parse endpoints + `Resplit` — confirmed server-only via its `IHtmlSanitizationService` dependency,
  so it could NOT skip the network hop despite being synchronous/pure-looking), UserActivity-ping.
  `IExportService` needed no work — already fully built (`Server/Export/ExportEndpoints.cs`,
  anchor-link download, never `@inject`ed). Structural exclusions unchanged from the plan:
  `IImageStorageService`, `IHtmlSanitizationService`, `IWriteRateLimitService` (server-only infra),
  `IDeviceDetectionService`/`ISpriteReadService` (already WASM-native via shared impls), all of
  Identity's static-SSR surface.
  - **Known, documented, NOT fixed (out of scope for a mechanical add-only pass):**
    `EndpointHelpers`' blanket `InvalidOperationException → 401` is imprecise for several clusters
    (Following's self-follow/self-vouch guards, Badges'/Moderation's/Recommendations' business-rule
    limit checks) that also throw `InvalidOperationException` for non-auth reasons — the message
    still survives verbatim via `ProblemDetails.Detail`, only the status code is generic. Each
    affected `*Endpoints.cs` documents this locally. `ServerBadgeReadService`/`WriteService` have no
    ownership/role check at all (any caller can act on any userId) — pre-existing gap, surfaced in
    `BadgeEndpoints.cs`'s doc comment, not fixed here.
- **Program.cs wiring:** all 33 `app.Map{X}Endpoints();` calls added to `Server/Program.cs`; all 51
  `AddScoped<I,Client>()` registrations added to `Client/Program.cs` (both under one `WU-L5Sweep`
  comment block) — consolidated by the orchestrating session after the parallel cluster work landed,
  specifically to avoid concurrent edits to these two shared files.
- **Verified:** `dotnet build` clean (0 warnings/0 errors) on `TheCanalaveLibrary.Core`,
  `TheCanalaveLibrary.Client` (the WASM compile — confirms every client impl fully satisfies its
  interface with no server-only type leakage), and `TheCanalaveLibrary.Server`, each built to an
  isolated output path to route around a live dev-server file lock. One cross-cluster defect caught
  at this stage and fixed: `ClientGroupReadService.GetMembersAsync` read a nonexistent `.Members`
  field off `PagedResult<T>` (the record only has `.Items`) — a concurrent-agent-authoring
  side-effect, not a doc gap.
  **A real, more serious bug surfaced only by `dotnet test`, not `dotnet build`:**
  `StoryEndpoints.cs`'s `/query` and `/filter-candidates` handlers each combine the body-inferred
  `StoryFilterDto filter` with an unattributed sibling array (`restrictToStoryIds`/`candidateIds`).
  `RequestDelegateFactory` can't disambiguate an array parameter's binding source once another
  parameter in the same handler is already inferred as `[FromBody]` — it resolved to an un-bindable
  "UNKNOWN" source and **threw at app startup** (`AuthorizationPolicyCache` builds every endpoint's
  metadata eagerly, so one bad handler crashed `WebApplicationFactory` for the whole app). This
  looked like a mass regression (642 of 650 Integration tests failed with an identical
  `IntegrationTestBase.InitializeAsync` stack trace) but was one root cause. Fixed with explicit
  `Microsoft.AspNetCore.Mvc.FromQueryAttribute` on both array parameters (a first attempt using
  `Microsoft.AspNetCore.Http`'s namespace was the wrong one — `[FromQuery]` lives in `Mvc`); the
  gotcha and rule ("array param sharing a handler with a body-inferred DTO always needs explicit
  `[FromQuery]`") are now recorded in `layer5-wasm.md` §"Reads with non-scalar parameters" so future
  POST-read handlers don't repeat it. `dotnet test`, full solution, after both fixes: **Unit
  685/685, RazorComponents 619/619, Integration 650/650 — all green.**
- **Explicitly not done (by design — see `layer5-wasm.md` "Rollout Strategy" WU-L5Sweep bullet):**
  the `App.razor` → `InteractiveAuto` flip, `[PersistentState]` adoption, per-feature
  `{Feature}EndpointsTests`/`Client{Feature}ServiceTests`, any browser verification. No L5 grid cell
  moves to Stage 5 from this work-unit — cells stay at their current number (mostly Stage 2) until
  the future verification wave lands per-feature.
- **Tool:** Claude Code (Opus), orchestrating 20 parallel `general-purpose` subagents for the
  mechanical per-cluster authoring. **Pointer:** `layer5-wasm.md` (hardened this WU);
  `status.md` Global Conditions "Mechanical WASM API sweep."

## WU-GlobalFlip — InteractiveAuto flip + full [PersistentState] adoption + WASM browser wave — DONE ✓ (2026-07-13)

The Layer-5 endgame (layer5-wasm.md §"The Global Flip"), executed same-day on top of WU-L5Sweep:
the whole site now runs `InteractiveAuto` (server circuit on first visit, WebAssembly on revisits),
with declarative prerender-state persistence adopted across every data-loading page, verified by a
WASM-focused whole-site browser wave that found and fixed seven real bugs.

- **Doc-Touch moment 1:** `layer5-wasm.md` §"[PersistentState]" hardened against the official
  .NET 10 doc (learn.microsoft.com "Blazor prerendered state persistence") before implementation:
  public-property requirement, ValueTuple-doesn't-survive-STJ, browser-exposure rule
  (persist-only-what-renders), no-prerender-on-internal-SPA-navs (dispatcher param-change reloads
  stay plain fetches), `@key` instance association, `AllowUpdates`/`RestoreBehavior` options.
- **The flip (checklist steps 1–3):** `Routes.razor` moved Server→Client with retargeted
  assemblies (AppAssembly = SharedUI, Additional = Client; the Server assembly deliberately absent —
  URLs the interactive router can't match fall back to full-document navs, which IS the Identity
  static-SSR escape hatch). `UserActivityTracker` moved Server→SharedUI. `App.razor`
  `PageRenderMode` → `InteractiveAuto` (the `AcceptsInteractiveRouting()` guard stays). The
  client-registration sweep surfaced four DI gaps, all fixed: `WasmActiveUserContext`
  (claims-only twin of `ServerActiveUserContext` over the deserialized auth state — 8 components
  inject `IActiveUserContext`), `WasmHostEnvironmentAdapter` (`IHostEnvironment` →
  `IWebAssemblyHostEnvironment`, unblocks DevLoginBar), `ManualTreeStore` client registration, and
  `ISpotlightSlotAllocator` (which needed its own full L5 surface:
  `SpotlightSlotAllocatorEndpoints` + `ClientSpotlightSlotAllocator`, mod-role-gated).
- **[PersistentState] adoption (checklist step 4):** all ~30 data-loading pages + 9 self-loading
  components converted by 8 parallel agents (StoryPage/TagDirectoryPage were the pre-existing
  references). Primary fetched content persists; per-viewer supplementary state stays ephemeral
  (StoryPage's `_usiState` judgment); ValueTuple pairs split into separate persisted properties;
  dispatcher pages keep plain-assign param-change reloads. Two exposure fixes landed en route
  (ChapterEditorPage's forbidden-branch no longer persists unrendered chapter source;
  ModUsersPage filters before persisting). Home fetch confirmed to live entirely in
  `CommunitySpotlightDisplay`.
- **The wave (checklist step 5) — WASM-focused per the runtime check (network shows
  `_framework/*.wasm` + zero `_blazor` WebSocket on the cached-runtime pass). Seven bugs found
  live, all fixed same-session:**
  1. **Empty-body 200s crashed every nullable-returning read** (`GetViewerLastInteractionUtcAsync`
     broke StoryPage for viewers with no read history): ASP.NET writes an EMPTY body for a null
     result value under BOTH `Results.Ok(null)` and `Results.Json(null)`, and
     `GetFromJsonAsync<T?>` throws `ExpectedJsonTokens`. Fixed client-side:
     `ClientHttpHelpers.GetNullableFromJsonAsync`/`ReadNullableFromJsonAsync` (empty→null), swapped
     into all 18 nullable reads + `ClientTagWriteService.UpdateTagAsync` (a latent pilot bug whose
     null branch had never been hit). Rule: layer5-wasm.md §"Error-Translation Contract".
  2. **Poll voting 400'd every vote**: on POST, a bare `int[]` infers as `[FromBody]` (query
     inference is GET-only) — `PollEndpoints`' vote handler demanded a body the client never sends.
     Fixed with `[FromQuery]`; rule extended in layer5-wasm.md (both inference failure modes now
     documented).
  3. **`IStoryTag` polymorphism** (flagged during [PersistentState] work, fixed pre-wave):
     `CreateStoryDTO`/`StoryUpdateDTO` carry `List<IStoryTag>` across HTTP; interface-typed members
     can't round-trip STJ without `[JsonPolymorphic]`/`[JsonDerivedType(typeof(StoryTagDTO))]`.
     Verified live: story create with Setting+Genre tags → 201-equivalent → tags persisted.
  4. **Blazored.Typeahead crashed the WASM renderer** (archived lib, Blazored/Typeahead#221 —
     programmatic Value-clear after a pick, the TagSelector pattern; first-ever WASM exposure).
     REPLACED with in-house `SharedUI/Controls/CanalaveTypeahead.razor` (100% Blazor-managed DOM,
     one delegated `typeahead.js` Enter-suppression listener, token-styled Overlay dropdown,
     debounced, keyboard nav) — TagSelector + StoryTitlePicker rebuilt on it, package/CSS/JS refs
     removed, `CanalaveTypeaheadTests` (7 tests) covers the search→select path bUnit never could.
     `layer4-style.md`'s old leave-as-is stylesheet carve-out closed.
  5. **Same-component route redirects on Quill-hosting pages crashed the WASM renderer**
     (`removeChild` of null — Blazored.TextEditor#71 geometry: in-place fine-grained diffs walk
     sibling lists Quill altered; cross-component teardown is root-first and safe). Fixed with
     `forceLoad: true` on Story/Chapter/BlogPost editor create→edit + version-switch redirects —
     [PersistentState] makes the full-load hydration invisible. Verified: story AND chapter
     create→edit both land clean.
  6. **TreeSearchPage stale root**: no `OnParametersSetAsync` reload path, so an in-app nav
     `/discover/me` → `/discover/user/2` reused the instance and rendered the OLD root's tree
     under the new URL. Fixed with the standard WU-ComponentSoundness dispatcher pattern
     (route-identity tracking + plain-assign reload); verified live both directions.
  7. **Every `/Account/*` page 500'd**: `ReaderDisplayProvider` (tree-wrapping provider, renders on
     static-SSR Identity pages too) used `[PersistentState]`, whose persistence callback has no
     inferable render mode on a fully static render → framework throw at persist time. Converted
     to the manual `PersistentComponentState` API with the explicit
     `RegisterOnPersisting(cb, RenderMode.InteractiveAuto)` overload — the one sanctioned
     exception to the "don't hand-roll" rule (now documented in layer5-wasm.md).
- **Verified working under real WASM in the wave** (each with zero console errors + psql ground
  truth for writes): home/spotlight, discover (random batch + filtered `POST /query` + sort),
  story page (persisted-state hydration confirmed by network log: primary data never refetched,
  only ephemeral per-viewer calls fire), favorite toggle (DB row), chapter reading, comment post
  (sanitized row 323825), tags directory, tag typeahead search/select, bookshelves, profile
  (LastActive loop live), settings read+write (tagline round-trip), groups list/page (PagedResult +
  nullable GroupRole), polls vote→results→retract, notifications page/bell, messages read+send,
  story create/edit incl. tags, chapter create, story lineage page + StoryTitlePicker, tree search
  auto tab + root switching, all five mod pages as AdminUser (incl. `/mod/stats` charts + slot
  grants via `ClientSpotlightSlotAllocator`), spotlight redemption page, Identity `/Account/Manage`
  (static SSR via full-doc nav), EPUB export download (200 + attachment headers), DevLoginBar user
  switching, DraftAutosave restore banner.
- **Known false-alarm recorded:** the claude-in-chrome network reader reports body-less success
  responses (202/204/200-empty) as "503" — server log + DB are the ground truth (all such
  requests executed correctly). Also noted as pre-existing (not flip regressions, not fixed):
  MessageComposer doesn't clear after send (never did); ModSpotlightPage's unpersisted settings
  knobs still gate its loading flash.
- **Verified:** `dotnet build` clean; RazorComponents tier 626/626 mid-wave after the typeahead
  swap; full `dotnet test` re-run at wave end (Unit + RazorComponents + Integration) — result
  recorded in the final session summary. Test stories/chapters created by the wave were removed
  from the standing dev DB (psql); the wave's test comment and favorite row on story 2385 remain
  as ordinary TestUser data.
- **Tool:** Claude Code (Opus) driving Chrome via MCP browser tools; 8 parallel agents for the
  [PersistentState] adoption. **Pointer:** `layer5-wasm.md` (all new rules recorded);
  `status.md` grid + Global Conditions "Global Flip".

## WU-CustomLists — Custom Lists, full feature (Feature 51; Phase-4 verdict rendered) — DONE ✓ (2026-07-13)

- **Cells:** F51 L2/L3/L3.5/L4/L4.5/L5 (2·1·1·1·1·2 → all 5); L1 stays 5 (zero schema change).
  Renders row 3's Feature-51 beta-scope verdict (designed + built — `middle_plan_v2.md` Resolved
  "Custom Lists requirements").
- **Requirements settled in chat 2026-07-13** (full record: `audit/CustomLists.md` §"Settled
  design"): positioning = *named shareable shelves* (privacy demoted — Private Favorites own the
  zero-effect save, verified against the code); filter-template integration DROPPED (ethos:
  shared blocklists; whitelists redundant with view+clone — dissolves spec §8 row 7);
  sharing = view + optional clone (visible-entries-only, private-start, "(copy N)"
  disambiguation, self-clone OK); add via StoryCard caret expander (NOT prime real estate) +
  in-list `StoryTitlePicker`; separate `/my-lists` section (closed `BookshelfTab` enum stays
  closed; UserMenu item + Bookshelves cross-link); `ProfileTab.Lists`; user-selectable sort
  (DateAdded↑↓/Title↑↓ — no `SortOrder` column, no content-rating sort); 100-list cap, entries
  uncapped; 256-char names (schema kept; the design log's 100 superseded); no per-list email
  alerts, no collaboration, **no author notification on add** (deliberately unlike GroupStory);
  no rate limiting (matches SavedTagSelections).
- **Structural template:** SavedTagSelections (Feature 15) end-to-end — service/DI/endpoint/
  client/validation/exception shapes mirrored with story entries. Entities moved
  `Core/Models/` → `Core/CustomLists/` (legacy-folder retirement).
- **Did:** Core (`CustomListDtos`, `CustomListSortEnum`, `CustomListValidations`,
  `CustomListValidationException` + `ExceptionPresenter` registration, `ICustomListRead/
  WriteService`); Server (`ServerCustomList{Read,Write}Service` — viewer-visible counts/ids via
  filtered-`Stories` joins so counts never phantom; clone reads source entries through the READ
  context = visible-only by construction; `CustomListEndpoints`); Client
  (`ClientCustomList{Read,Write}Service` + registrations); SharedUI (`MyListsPage`,
  `CustomListPage`, `AddToCustomListMenu` caret composite, profile Lists tab across
  ProfilePage/Desktop/Mobile, UserMenu + Bookshelves nav seams); `[PersistentState]` on both new
  pages per the post-GlobalFlip rules.
- **Verified:** `dotnet test` green all tiers — Unit 712 (incl. `CustomListValidationsTests`,
  `ClientCustomListServiceTests`), RazorComponents 632 (incl. `AddToCustomListMenuTests`;
  Bookshelves tab-count tests updated for the cross-link; `StoryCardTests`/`ProfilePageTests`
  gained the new fake/auth registrations), Integration 680 (incl. `CustomListServiceTests`
  ~28). Token check: no new findings. **Browser band: full loop driven under InteractiveAuto
  with the later flows confirmed on the real WASM runtime** — detail in `audit/CustomLists.md`
  F51 L4.5 Stage note (create/toggle/clone/delete through the client impls, psql ground truth,
  rating-filter + anonymous checks, zero console errors).
- **Tool:** Claude Code (Fable) driving Chrome via MCP browser tools. **Pointer:**
  `audit/CustomLists.md` (settled design + Stage notes); `audit/Discovery.md` §"Note on
  search-result narrowing" (lists-as-filter-source dropped — `UserCustomFilter` rationale to
  re-derive); `folder_clusters.md` CustomLists row; `middle_plan_v2.md` Resolved + Phase 4.
