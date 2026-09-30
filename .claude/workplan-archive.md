# Workplan Archive — DONE work-unit entries

> Moved out of `workplan.md` on 2026-07-27 (WU-DocHygiene2) to keep the live ledger navigable.
> **Append-only:** when `workplan.md`'s recent-DONE window grows long, move its oldest DONE
> entries here wholesale — never edit an archived entry. Citations of the form
> "`workplan.md` WU-X" resolve HERE for archived entries.
>
> Contents: the Phase A–E build arc (WU0, Phases 1–3: atoms → integration points → consumers /
> pages), then dated DONE entries 2026-07-06 → 2026-07-18 (first sweep, 2026-07-27), then
> 2026-07-23 → 2026-08-01, oldest first (second sweep, 2026-09-30 — `workplan.md` had reached
> 2,516 lines against its ~1,500 trigger; entries moved verbatim, only their order reversed to
> keep this file chronological). Entries from 2026-09-20 onward remain in `workplan.md` until a
> future archival sweep.

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
  8. **`/mod/submissions` tabbed shell; rec-approval wiring deferred.** *(Superseded by
     WU-RecLifecycle, 2026-07-25: no rec tab, ever — recs are author-controlled; see that entry.)*
     Recs currently write as
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

> **Report-target rating routing (decision row 1) — resolved 2026-07-18, supersedes the WU34-era
> "reach = ShowMatureContent" framing.** Was: open decision on extending per-moderator `ContentRating`
> scoping from Story reports (the only arm that had it) out to Recommendation/BlogPost/Comment. Now:
> scoping removed entirely — the report queue and pending-submissions queue are moderator work
> surfaces, shown regardless of rating. `ServerModerationReadService.GetReportQueueAsync`/
> `BatchLoadTargetsAsync`/`GetPendingSubmissionsAsync` bypass `ContentRating` alongside `IsTakenDown`;
> `dotnet test` 1941/1941 green (2 new Integration tests:
> `GetReportQueueAsync_ShowsMRatedStoryReport_ToModWithMatureOff`,
> `GetPendingSubmissionsAsync_ShowsMRatedSubmission_ToModWithMatureOff`). No Stage-number change
> (Features 46/47/48 L3-Logic stay Stage 5). See `middle_plan_v2.md` Resolved and
> `content-safety.md` §"Moderator review surfaces are work surfaces" for the settled mechanism and
> `audit/Moderation.md` Feature 47/48 Stage notes for the full record.

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

*(The WU39 entry — DONE ✓ 2026-07-25, inside the recent window — was moved back to
`workplan.md`'s recent-DONE run 2026-07-27; it had ridden the Phase-3 block into this archive.)*

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

## WU-RelatedStories — Also Favorited / Also Recommended UI (Feature 61) — DONE ✓ (2026-07-13)

- **Cells:** F61 L3-Logic/L3.5-Struct/L4.5 (2·2·1 → 5·5·5); L2/L5 stay 5 (extended additively);
  L4 stays 1 (visual sign-off pending, standard precedent). The L8 marts + L2 read service were
  already Stage 5 (WU-Marts, 2026-07-07) — this WU builds the missing embedded-section UI spec
  §5.28/§5.30 always called for, closing the last unbuilt slice of the feature.
- **Settled in chat 2026-07-13** (full record: `audit/Discovery.md` F61 "Settled for
  WU-RelatedStories"): reuse `StoryDeck` (not a bespoke strip), `take=6` per subsection; score
  never displayed; one shared `UserStoryInteractionFilter` (not `ResultsFilterPanel` — no tag/FTS
  axes apply to a co-occurrence read scoped to one story), collapsed `<details>` disclosure,
  authenticated-only; placement after `RecommendationSection` on both StoryDesktop/StoryMobile.
  This resolves spec §5.28's floated "simplified ResultsFilterPanel" open item.
- **Architectural consequence (additive, not a contradiction of the WU-Marts Stage-5 note):**
  making the filter live required `ICoOccurrenceReadService`'s two methods to accept an optional
  trailing `excludedInteractions` parameter (`null` preserves the old internal-defaults-resolution
  behavior). The two endpoints converted `MapGet`→`MapPost` to carry the new
  `CoOccurrenceRequest` record (array parameter isn't GET-bindable per `layer5-wasm.md`). Detail:
  `layer2-services.md` §"Optional caller-supplied exclusions".
- **Did:** `Core/Discovery/CoOccurrenceRequest.cs` (new); `ICoOccurrenceReadService` +
  `ServerCoOccurrenceReadService` (optional exclusions param); `CoOccurrenceEndpoints` (POST) +
  `ClientCoOccurrenceReadService` (`PostAsJsonAsync`); `DevDiagnosticsEndpoints`'s two F61 probes
  fixed to pass `ct` by name (positional binding would've silently mis-bound to the new param);
  `SharedUI/Discovery/RelatedStoriesSection.razor` (new, self-loading composite, pattern:
  `RecommendationSection`) wired into `StoryDesktop.razor`/`StoryMobile.razor`.
- **Verified:** `dotnet build` clean (0 errors). `dotnet test` green — Unit 712 (unchanged),
  Integration 681 (incl. new `AlsoFavorited_ExplicitExclusions_OverrideTheDefaultEntirely` —
  explicit `[]` un-hides a default-excluded story, explicit `[Favorite]` hides a non-default one,
  `null` still resolves §8.7 defaults), RazorComponents 639 (incl. new `RelatedStoriesSectionTests`
  ×7 + `FakeRelatedStoriesTestServices.cs`; fixed `StoryDesktopTests`/`StoryMobileTests`/
  `StoryExternalLinksRowTests` for the new nested injections, same treatment as WU28's
  `IModerationWriteService` fix). Token check: clean except the pre-existing `ImportReviewPanel`
  finding (WU40, untouched Import-cluster file).
- **Browser band (real circuit, SeedTool-volume dev DB):** scanned `/dev/discovery/also-favorited/
  {id}` + `/also-recommended/{id}` across several IDs for a story with both marts populated —
  story 500 (6+ scored rows each). Live at `/story/500`: authenticated (ReaderGamma) — both decks
  rendered (6 cards each), no score in markup, filter disclosure collapsed with all six USI
  checkboxes unchecked; opening it and toggling "ignored" fired exactly one `POST .../
  also-favorited` + `.../also-recommended` round-trip per the server log, while the *initial* page
  load took zero HTTP hits (`[PersistentState]` restored the SSR-prerendered listings — the
  no-double-fetch contract holds); rendered set was unchanged post-toggle because none of story
  500's related stories happened to be Ignored by this viewer (correct, not a bug). Anonymous
  (logged out): both decks rendered identically, no filter disclosure, no Recommend CTA. Zero
  console errors traced to any F61 code path; two unrelated pre-existing 401s from
  `NotificationBell` (stale WASM auth state immediately post-logout) observed and left as
  out-of-scope, not filed against this feature.
- **Tool:** Claude Code (Sonnet) driving Chrome via MCP browser tools. **Pointer:**
  `audit/Discovery.md` F61 Stage notes (settled design + verification); `layer2-services.md`
  §"Optional caller-supplied exclusions"; `layer5-wasm.md` "Reads with non-scalar parameters".

## NotificationBell anonymous-viewer 401 fix (Feature 42) — DONE ✓ (2026-07-13)

- **Cells:** F42 L3-Logic/L5 stay Stage 5 — this is a same-cell bug fix (`debugging.md`
  "Runtime bug surfaces during verification"), not a stage transition. Closes the "unrelated
  pre-existing 401s from `NotificationBell`" observed and deferred as out-of-scope in
  WU-RelatedStories above (same day).
- **Reported as:** unhandled 401 immediately after logout, hypothesized as stale WASM auth
  state. **Actual root cause (browser-verified, not the reported hypothesis):** reproduces for
  ANY anonymous viewer under the WASM runtime, not only post-logout — confirmed cold in a
  browser tab that had never authenticated. `NotificationBell`'s own `<AuthorizeView>` gated its
  markup but not its `OnInitializedAsync`, which called `INotificationWriteService`
  unconditionally; `@inject` and lifecycle methods resolve/run at component construction
  regardless of conditional markup inside the same component. Exactly the gap
  `layer3-logic.md` "Deferring DI Behind AuthorizeView (WU43)" already named `NotificationBell`
  as exposed to, predating a test that would have caught it — the WU-L5Sweep/WU-GlobalFlip WASM
  client impl (2026-07-12/13) turned the latent gap into a live crash by hitting a real
  `RequireAuthorization()` endpoint instead of the server impl's anonymous-safe zero/empty return.
- **Did:** split `NotificationBell.razor` into a thin `<AuthorizeView>` wrapper (no `@inject`)
  and `NotificationBellInner.razor` (all markup/services/`[PersistentState] UnreadCount`) per
  the established wrapper/inner pattern — not a defensive auth re-check inside the one component.
  `layer3-logic.md`'s WU43 section updated to record the fix and drop the stale "predates this
  convention" caveat.
- **Verified:** `dotnet build` clean, `dotnet test` RazorComponents 639/639 green (no test
  exercised this path before or after — `FakeNotificationWriteService` still isn't in the fakes
  catalog, same gap `layer3-logic.md` flagged; adding it + an anonymous-viewer test is follow-up,
  not done here). Browser: cold anonymous tab loads clean ("Log in" shown, zero console errors);
  full dev-bar TestUser login → logout cycle shows the chrome island correctly flip to "Log in"
  with zero console errors; flyout preview/mark-all-read still work for an authenticated viewer
  post-split.
- **Tool:** Claude Code (Sonnet) driving Chrome via MCP browser tools. **Pointer:**
  `audit/Notifications.md` F42 "Anonymous-viewer crash fix" Stage note; `layer3-logic.md`
  "Deferring DI Behind AuthorizeView (WU43)".

## WU-NotificationCleanup — Notification Cleanup Worker (Feature 57) — DONE ✓ (2026-07-15)

- **Cells:** F57 L2 (2 → 5). The feature's only applicable layer — the worker IS the feature
  (all other layers N/A per `grid_axes.md` #57).
- **Did:** `Server/Notifications/NotificationCleanupSweeper.cs` (new; body — one set-based
  `ExecuteDeleteAsync` on `IsRead && DateCreated < now − RetentionPeriod`, 60 days, `public
  static readonly` so tests age rows against the real constant, the `QuietPeriod` precedent) +
  `Server/Notifications/NotificationCleanupWorker.cs` (new; `BackgroundService` timer driver,
  24 h `PeriodicTimer`, first sweep ~5 s after startup — the `SpotlightGoLiveWorker`/`Sweeper`
  worker/body split verbatim). Registered in `Program.cs`; worker added to `TestAppFactory`'s
  removal list. Unread notifications are kept indefinitely regardless of age. **No new index**
  backs the `is_read + date_created` predicate (existing `ix_notifications_recipient_read_date`
  leads with recipient, doesn't serve the global scan): a once-daily sweep over a table pruned
  to ≤60 days of read rows makes a scan negligible; a partial index would tax every insert —
  rationale recorded in the audit Stage note.
- **Verified:** `dotnet build` clean; `dotnet test` green — Unit 712 / RazorComponents 639 /
  Integration 683 (incl. new `NotificationCleanupTests` ×2: four-quadrant read×age matrix
  deletes exactly the read+aged row; nothing-eligible sweep deletes zero). End-to-end
  (server-only path, SeedTool-volume dev DB): three psql marker rows for TestUser
  (read+61d / unread+61d / read+now); boot sweep logged "deleted 13520 read notification(s)
  older than 60 days" (13,519 aged bulk-seed rows + the read+61d marker, seed-user ownership
  psql-confirmed before start), survivors psql-verified, and `/notifications` in Chrome as
  TestUser rendered exactly the two survivors (unread 2mo-old with unread dot; read 1m-old
  without).
- **Tool:** Claude Code. **Pointer:** `audit/Notifications.md` F57 Stage note.

## WU-UserStatRecalc — UserStat Recalculation Worker (Feature 58) — DONE ✓ (2026-07-15)

- **Cells:** F58 L2 (2 → 5). The feature's only applicable layer — the worker IS the feature
  (all other layers N/A per `grid_axes.md` #58).
- **Pre-implementation doc-touch:** a per-counter ground-truth audit across all 23 `UserStat`
  counters found the docs' "acknowledgment/contribution counters deferred to WU37" pointer was
  **wrong** — WU37 is Story Tagging (Feature 12), unrelated; the acknowledgment/beta-reader
  producer has no assigned WU at all, and `FeatureContributions`' producer is Feature 56 (Stage 2).
  Fixed in `layer2-services.md`, `audit/Profiles.md`, `audit/Badges.md`. Also found
  `UserStat.ActiveReportCount` was an orphaned duplicate — nothing ever wrote it; the live
  moderation path writes `User.ActiveReportCount` on `AspNetUsers` instead — dropped via migration
  rather than wired.
- **Settled scope (user decisions):** recompute the 14 already-wired counters (mirroring each
  one's exact wired formula — `StoriesInProgress` deliberately doesn't exclude `IsIgnored`,
  `FavoritesOnStories` never counts `IsHiddenFavorite`, `CommentsWritten` doesn't exclude
  takedowns, `RecommendationSuccessesEarned` keeps the anti-self-farm join) plus 3 unwired-but-
  populated counters (`ChaptersRead`, `WordsRead`, `RecommendationsFoundUseful` — this worker is
  their first populator) plus 1 raw-SQL counter (`ViewsOnStories`, reading the `daily_story_stats`
  L8 mart directly — no EF model exists for it). Deferred, not recomputed: `SpotlightCount`
  (definition unsettled), the two acknowledgment counters and `FeatureContributions` (producers
  unbuilt — recomputing to 0 would mask that, not correct drift). Insert-then-recompute: the
  worker also inserts any missing `UserStat` row first, since no production write path creates one
  at registration either (a stale comment in `ServerRecommendationWriteService` claimed otherwise;
  corrected).
- **Did:** `Server/Profiles/UserStatRecalculator.cs` (scoped; one pair of `IS DISTINCT FROM`-
  guarded `UPDATE ... FROM` statements per counter — a match-and-correct pass plus a
  zero-unmatched pass, since a plain inner join would silently skip a user who drifted to a wrong
  positive value but has zero true occurrences today) + `Server/Profiles/UserStatRecalculationWorker.cs`
  (`BackgroundService`, daily off-hours loop deliberately sharing `Marts:RebuildHourUtc` with
  `DiscoveryMartWorker`/`SiteDailyStatWorker` rather than a dedicated config key — same shape
  as `SiteDailyStatWorker`/`SiteDailyStatAggregator`). New telemetry component
  `CanalaveTelemetry.UserStatRecalc` (duration/users-touched/outcome, same shape as `Marts`),
  doc-touched into `logging.md`. DI in `Program.cs`; `TestAppFactory` removes the hosted worker.
  Migration `WU_UserStatRecalc_DropActiveReportCount` drops `UserStat.ActiveReportCount`; removed
  the property from `UserStat.cs` and corrected the stale `UserStatsDto` comment referencing it.
- **Verified:** `dotnet build` green (0 warnings/errors). `dotnet test` green — Unit 712
  (unchanged) / RazorComponents 639 (unchanged) / Integration 694 (was 683 — 11 new tests in
  `UserStatRecalculatorTests.cs`): drift-correction per counter family (interaction-derived,
  authored-content, following, groups, recommendations incl. anti-self-farm exclusion,
  reading-progress, raw-SQL views), insert-then-recompute for a no-row user, idempotency (second
  pass corrects 0), zero-with-no-ground-truth (proves the zero-unmatched pass fires), and
  deferred-counters-untouched. Mutation sanity: inverted `StoriesInProgress`'s aggregate to also
  exclude `IsIgnored` (the wrong, display-filter formula) → the mirror-wired-formula test failed as
  expected; reverted, suite green again.
- **Tool:** Claude Code (Opus). **Pointer:** `audit/Profiles.md` Feature 58 Stage-5 note;
  `layer2-services.md` "Recalculation worker (F58)".

## WU-AuditFixPass — Modernization-audit Tier-1 (5 must-fix) + Tier-2 (should-fix) closure — DONE ✓ (2026-07-18)

Source: `modernization-audit/report.md` (2026-07-17 pre-lock-in audit). All 5 Tier-1s, all Tier-2
fix-patterns, plus 4 hardening items a post-fix adversarial re-audit surfaced. Finding IDs (MA-*)
resolve in `modernization-audit/slices/*-findings.md`; Tier-1 proofs in its `verification.md`.

**Tier-1 security/UX fixes:**
- **MA-101** `ReconnectModal.razor` `@Assets` path corrected `Components/Layout/…` →
  `Components/…` (matches the physical `.razor.js`); repo-grepped — no other stale reference.
- **MA-602** `UserProfileEndpoints` header route now derives `includePrivate` server-side from
  `IActiveUserContext.UserId == userId`; the client-supplied query bool is gone
  (`ClientUserProfileReadService` no longer sends it). **Hardening:** the sibling `/bio` route
  leaked Private/UsersOnly profiles' bios — `ServerUserProfileReadService.GetProfileTextAsync` now
  applies the same visibility gate as the header (owner always passes); interface doc updated.
- **MA-601** `BadgeEndpoints` no longer accepts a client `userId` on any route — curation read and
  display-order derive it from `IActiveUserContext` (UserSettingsEndpoints pattern). **Hardening:**
  the `/award` route is REMOVED entirely, not self-scoped — awards are earned, the only production
  caller is `ServerRecommendationWriteService` (in-process), and a mapped route would let any WASM
  caller self-mint Patron/Architect (same decision + rationale as NotificationEndpoints' unmapped
  generation; `ClientBadgeWriteService.AwardAsync` throws `NotSupportedException`). Note this
  deliberately deviates from verification.md's "enforce caller==target in the service" fix-shape:
  a service-level check would break the legitimate in-process award-to-other-user path.
- **MA-301** `ServerChapterWriteService`: all five previously-ungated write methods
  (Create/AddAlternateVersion/UpdateContent/SetPrimaryVersion/SetPublished) now enforce
  `Story.AuthorId == ActiveUser.UserId` (the same authority Move/Delete already used);
  `GetChapterForEditAsync` (draft read) gates too. **Hardening:** the read gate requires an
  authenticated viewer explicitly (an anonymous viewer vs. an authorless story would otherwise
  pass on null == null); `ClientChapterReadService.GetChapterForEditAsync` translates 401/403 →
  `UnauthorizedAccessException` so `ChapterEditorPage`'s new catch renders the forbidden state
  identically under both render modes. Moderation/import/seeder call paths verified unaffected
  (moderation never calls this service; import commits as the story's author; DataSeeder writes
  entities directly). `ChapterEndpoints` class doc updated to describe the closed gate.
- **MA-201** `ServerStoryWriteService` injects `IHtmlSanitizationService` and sanitizes
  `LongDescription` on create AND update (the rule `ServerSeriesWriteService` already followed).

**Tier-2 fix-patterns:**
- **Counters (MA-502/MA-705):** `RecordSuccessAsync`'s tracked `SuccessfulRecCount++` → atomic
  `ExecuteUpdateAsync` delta *after* the success-row insert commits (a PK-race insert failure can
  no longer increment); BlogPost `ToggleLikeAsync`'s C#-computed absolute write → atomic ±1 delta
  (0-clamped) + re-read for the returned count.
- **MA-401:** `UserStoryInteractionPanel` is now `IAsyncDisposable` with a `_flushPending` flag —
  a toggle inside the 2s debounce window flushes on dispose instead of being dropped; flush
  failure during teardown logs Warning (never throws).
- **Not-found sweep (MA-202/304/404/606/708 + GroupCreateEditPage, unnamed by the audit but same
  class):** all 12 `NavigateTo("/not-found")` sites → `NavigationManager.NotFound()` (StoryPage ×2,
  StoryEditorPage, SeriesCreateEditPage, ChapterReadingPage, ChapterEditorPage ×3, BlogPostEditorPage,
  GroupCreateEditPage, ProfilePage ×2), plus the two inline-render sites (SeriesPage missing-series,
  TreeSearchPage missing-root) now also call `NotFound()`. Deliberately NOT swept: BlogPostPage /
  CustomListPage / GroupPage inline branches (documented missing-vs-private ambiguity).
- **Error-channel sweep (MA-205/405/501/504/603/703/704):** 11 components normalized to
  `InlineAlert` + `ExceptionPresenter` (raw `ex.Message` eliminated from UI): SeriesCreateEditPage,
  MyStoryLineagesPage, TagEditorForm, MessageThread, ComposeConversationModal, GroupCreateEditPage,
  SettingsPage (success banner → `InlineAlertVariant.Success`), ModReportsPage, ModUsersPage,
  ModSubmissionsPage, BlogPostPage, ReportDialog (now logs unexpected at Error), and
  RecommendationSection fully adopted CommentSection's `Translate` pattern.
- **MA-123/MA-701:** Moderation's `RequireModerator()` role branch now throws
  `UnauthorizedAccessException` (→ 403) matching Spotlight/SiteSettings/Poll; the unauthenticated
  branch stays `InvalidOperationException` (→ 401). `ModerationServiceTests` updated to pin 403.
  (MA-702's Tier-3 edge `RequireAuthorization(ModeratorOnly)` NOT done — out of Tier-2 scope.)
- **Silent catches (MA-001/002/206/303/503):** ReaderDisplayProvider + MessagesNavLink now
  LogWarning (MessagesNavLink also skips the query for anonymous viewers via the AuthState cascade
  instead of relying on the service throwing); DraftAutosave's fire-and-forget loop gained
  catch-log-continue per tick (one unexpected throw no longer kills draft safety for the session);
  StoryPage/ChapterReadingPage dispose JS calls narrowed from bare `catch` to typed teardown
  filters; DraftAutosave capture-tick + RecommendationEditor sample-tick annotated
  `sanctioned-silent` and REGISTERED in `logging.md` (registry updated same work-unit).
- **MA-102:** `User.Roles` phantom nav deleted; migration `MA102_DropPhantomUserRolesShadowFk`
  drops `asp_net_roles.user_id` + `ix_asp_net_roles_user_id` (verified drop-only).
- **MA-103:** `options.User.RequireUniqueEmail = true` in Program.cs — ratifies the already-UNIQUE
  `EmailIndex` as site policy (one account per email) and converts the duplicate-email raw-500
  into a friendly validation error; intent comment added at the `HasIndex` site.

**Test infra:** `TestAuthenticationHandler` added (test host's default auth scheme) — authenticates
requests as whatever `FakeActiveUserContext` holds, so `.RequireAuthorization()`-gated endpoints are
now testable over `Factory.CreateClient()` (no test previously performed a real cookie sign-in;
audited: no existing test depended on the old always-401 behavior).

**Verified:** `dotnet build` green. New/updated regression tests: `BadgeEndpointsTests` (award route
unmapped + curation/display-order caller-scoping), `UserProfileEndpointsTests` (private-profile
header + hidden-last-seen + bio gate, attacker-shaped query strings), `ChapterWriteServiceTests`
(+7 authorship tests incl. author-positive GetChapterForEdit), `StoryWriteServiceTests` (+2 XSS
sanitize tests), `ModerationServiceTests` (403 pin). Fixture updates for the MA-301 gate
(chapter seeding now runs as the story's author): ChapterWriteServiceTests, StoryDetailTests,
ExportServiceTests, ChapterReadServiceTests. Full suite green (Unit 712 / RazorComponents 639 /
Integration — all three tiers; the affected-class subset re-verified 102/102 after fixture fixes).
Browser E2E pass: see status note / this entry's date.

- **Cells:** no Stage numbers change — every touched cell was Stage 5 and remains Stage 5 with
  corrected behavior; the audit's per-cell "proposes reopen" flags are resolved by this pass.
- **Deliberately deferred (out of Tier-1/2 scope):** the systematic ~40-endpoint authz sweep
  (report.md's #1 recommendation — verification/closure of WU-L5Sweep's deferred pass), Tier-3
  batch, code-economy items, BB-01/02/03 doc-touches.
- **Tool:** Claude Code (Fable). **Pointer:** `modernization-audit/report.md`; per-cluster audit
  files carry dated pointers to this entry.

## WU-AuditFixPass-2 — Endpoint-authz sweep closure + remaining Tier-2/3 + code-economy + Bucket-B docs — DONE ✓ (2026-07-18)

Source: `modernization-audit/fix-status.md` (the done/not-done map handed to this pass). Closes
everything the first pass (WU-AuditFixPass) deferred except the explicit ⛔ / 🧑 items (Desktop/Mobile
merges, Identity-scaffold prune, export-writer visitor — all product/human calls). Finding IDs (MA-*)
resolve in `modernization-audit/slices/*-findings.md`; BB-* in its `bucket-b.md`.

## WU-ResponsiveMerge — Single responsive site; Desktop/Mobile fork paradigm exorcised — DONE ✓ (2026-07-18)

**Decision (resolved same day, `middle_plan_v2.md` §Resolved):** one component tree, one DOM, every
viewport; CSS-first adaptivity (two tiers, 768px, no tablet tier); device detection and the
`{X}Desktop`/`{X}Mobile` fork retired (the Oct-2025 "flicker-fix ratchet" — chronicle pointer in
`render-and-layout.md` §"Responsive Layout Architecture"); page-level coordination composites folded
into their pages (single-consumer pass-through tier dissolved; dumb *reusable* children unchanged);
mobile variants deleted as unvalidated placeholders. Supersedes `modernization-audit` §1's
2026-07-17 "do not merge" verdict (Brian-confirmed). Native-app direction: PWA post-L5, not MAUI.

**Done (21 commits, 6c6a002 → this entry's doc commit):**
- Phase 0 (6c6a002): doc moment 1 — conventions rewritten (`render-and-layout.md` §"Responsive
  Layout Architecture", `layer4-style.md` §"Responsive Adaptivity Ladder", `layer3.5-structure.md`
  §"Responsive Structure", `layer3-logic.md` page-tier), `grid_axes.md` L3/L3.5/L4 tier definitions
  de-dispatchered, `middle_plan_v2.md` Resolved row + WU-EditorMobile re-scope, audit supersession
  notes, `modernization-audit` verdict superseded.
- Phase 1 (d47c1e6): `DesktopLayout`→`MainLayout` (single layout; top bar `flex-wrap`s at narrow;
  skip-to-content link added); `DeviceLayout` + `MobileLayout` (placeholder bottom bar) deleted.
- Phase 2 (58a604f…78b8e54, two commits per non-trivial cluster: merge then fold): all nine pairs
  merged into their pages — Home, Tags (mod CRUD now all viewports), Groups (**MA-509** resolved:
  `GroupDisplayFormat`), Bookshelves, Search, Profiles (`ProfileBanner.IsMobile` removed), Stories
  (**MA-209** resolved: `StoryDisplayFormat`; 999,999→"1000K" quirk deliberately preserved),
  Messages (panes stack at narrow, list `max-h-64`), TreeSearch (Tree⇄Results toggle deleted;
  `ExploreTab`/`DeepDiveTab` device params removed). **MA-406 dissolved** (drawer shells deleted).
  Final commit 78b8e54: `IDeviceDetectionService` + both impls + `device.js` + registrations +
  script tag deleted — zero device-detection code remains (repo-grep verified).
- Tests: 43 mobile-side bUnit tests deleted; 4 desktop suites retargeted to page suites (incl.
  `StoryExternalLinksRowTests`, a hidden `StoryDesktop` consumer); `AlwaysDesktopDeviceService`
  deleted; new configurable fakes (`FakeTagWriteService` directory knob, `FakeStoryPageTestServices`,
  `FakeTreeSearchReadService`).

**Verified:** full suite green post-merge — Unit 702 / Integration 727 / RazorComponents 510.
Browser smoke (Chrome, InteractiveAuto, TestUser session): 13 routes at desktop width — all load,
no `blazor-error-ui`, zero console errors. A11y-lite floor (machine pass, decision-row-12
evidence): `/`, `/story/1`, `/messages` zero findings; `/discover` + `/bookshelves` share one
pre-existing pattern (placeholder-only typeahead inputs, no page `<h1>`); `/user/1` flags Quill
toolbar buttons (known third-party gap). None merge-introduced. **Narrow-viewport visual pass NOT
done** (browser window pinned maximized; and narrow is deliberately unpolished pending the mobile
phase) — L4.5 claims cover desktop width, per the `status.md` Global Conditions note.
`check-design-tokens.ps1`: one finding, pre-existing in `Import/` (verified at base commit),
untouched.

- **Cells:** no Stage numbers change (touched cells were Stage 5, remain 5). Narratives: audit
  Shared-Context notes in Stories/Profiles/Discovery/Messaging/Groups/Tags/UserStoryInteractions
  (+ Chapters re-scope note).
- **Next (not this WU):** desktop UX evaluation on the merged single tree; PWA Tier 0 whenever
  desired (independent); mobile phase later decides real narrow UX (and only it may trigger the
  `ViewportState` cascade).
- **Tool:** Claude Code (Fable) + one implementation subagent for clusters 3–9.

**#1 recommendation — the systematic endpoint-authorization sweep (DONE).** All 38 `*Endpoints.cs`
files audited at authorization depth (fan-out: 7 parallel readers, one per cluster; each traced every
route into its backing service). Verdict: the WU-L5Sweep deferral class was **wider than the 3 found**
— 7 additional holes closed, plus MA-702's edge-gate gap. This is the closure the audit called for.
- **Story `/edit` (GetStoryForEditAsync):** trusted the auth floor only — any authenticated user could
  read any story's edit DTO (incl. the moderation-only `PostApprovalStatus`). Now loads the owner and
  throws `UnauthorizedAccessException` (→403); endpoint wrapped in `ExecuteWriteAsync`;
  `ClientStoryReadService` maps 401/403→`UnauthorizedAccessException`; `StoryEditorPage` renders the
  forbidden state (mirrors the MA-301 chapter shape).
- **Story `/by-author/{authorId}` (GetStoryIdsByAuthorAsync):** the `IgnoreQueryFilters(["ContentRating"])`
  bypass was keyed to a **client-supplied** authorId — any viewer could enumerate another author's
  rating-hidden story ids. Bypass now applies only when `authorId == ActiveUser.UserId`. Browser-verified:
  owner (mature-on) sees 5 of their stories, a mature-off non-owner sees 3.
- **BlogPost `GetByAuthorAsync` + `GetForEditAsync`:** forged `includeUnpublished=true` leaked drafts to
  anyone; `/edit` had no ownership check (draft content readable by any authed user). Both gated
  server-side (owner-derived flag / ownership throw); client + editor page mirror the chapter pattern.
  Browser-verified cross-author blog `/edit` → 403.
- **UserStoryInteractions `favorites/{userId}?includePrivate`:** the private-favorites switch was
  client-supplied — hidden favorites leaked. Now server-derived (`activeUser.UserId == userId`), same as
  MA-602's header fix; client stops sending it.
- **Chapter toc/list/versions:** draft (unpublished) chapter *metadata* (titles, word counts) enumerated
  to anyone. Now `IsPublished || Story.AuthorId == viewer` in all three reads. Browser-verified: StoryPage
  as a non-author shows only published chapters.
- **ManualTreeSearch favoriters:** the story-pivot favoriters section didn't constrain the anchor to the
  viewer-visible set — leaked who favorited a rating-hidden/taken-down story. Now anchored to `visible`,
  matching the author/recommendation sections.
- **Tag write routes:** added the `.RequireAuthorization()` floor they lacked (service `RequireMod`
  already covered it; defense-in-depth + stale-comment fix). `HttpRateLimitTests.TagWrites_*` now
  authenticates as a moderator (the real caller) to reach the limiter past the new floor.
- **MA-702:** registered the named `AuthorizationPolicies.RequireModerator` policy (Program.cs;
  `Server/Identity/AuthorizationPolicies.cs`) and applied it as the edge role gate on all mod-only groups
  (Moderation queue reads + all 6 writes, SiteDailyStat, SpotlightSlotAllocator, SiteSettings write) —
  replaced 4 duplicated inline `AuthorizeAttribute` copies. Browser-verified: non-mod → 403 on every
  mod route; AdminUser → 200/204.
- **Confirmed already-secure (no change):** every other cluster — the 7 sweep agents' full route×verdict
  tables are the closure record. The service layer was disciplined; holes clustered in the mechanically-
  generated read/endpoint layer exactly as the audit predicted.

**Remaining Tier-2 (report's curated list was partial — cross-slice §B):** MA-203 (StoryPage's 6 serial
loads → `Task.WhenAll` in a shared `LoadSupplementaryAsync`, both lifecycle methods), MA-204 (dead
`ChapterNames` projection + DTO field removed — a correlated subquery on the hottest read, plus a latent
draft-title leak), MA-402 (`ResultsFilterPanel` + `UserStoryInteractionFilter` now resync-until-interaction
like `TreeSearchControls` — the async default-exclusion seed reflects in the checkboxes; browser-verified
"Ignored" renders checked), MA-403 (`AddToCustomListMenu` split into wrapper/inner, deferring DI behind
`AuthorizeView`), MA-302 (reading-progress ping dropped its `RequireAuthorization()` — anonymous scroll now
202-no-ops instead of 401; browser-verified), MA-706 (`IBlogPostReadService` rebound to the read impl, not
the derived write class), MA-003 (logout `ReturnUrl` computed at render time / on `LocationChanged`, not
cached once per circuit), MA-005 (`CanalaveTypeahead` guards the caller `SearchMethod` + clears stale
results so Enter can't select from them), MA-110 (`Error.razor` rewritten from template debris to the
NotFound.razor role treatment + trace id).

**MA-008 (biggest pure-win compression):** new `CanalaveValidationException` base; 15 `*ValidationException`
types converted to one `Errors` shape (fixes a latent bug — StoryArc/SavedTagSelection/Spotlight previously
fell through `ExceptionPresenter` to the generic message). `ExceptionPresenter` (11 arms → 1),
`EndpointHelpers` (name-suffix hack → `is CanalaveValidationException`), and `TagEndpoints` (MA-407 —
private `ExecuteWriteAsync` deleted) all collapse onto it. Client: shared
`ClientHttpHelpers.ThrowIfWriteFailedAsync(response, validationFactory, kind?)` replaces ~14 hand-rolled
copies (3 deviant ones left: Group/Messaging 403-disambiguation, USI ArgumentOutOfRange). **Behavior note:**
converted client services now map a bare 401→`InvalidOperationException` (was `UnauthorizedAccessException`);
no catch site reads it, but an expired-cookie 401 on those services routes to the generic-error path, not a
`_forbidden` state. Two Unit tests split accordingly.

**Tier-3 batch (mechanical):** dead code (MA-105 Razor Pages host + `Pages/`, MA-106 `AddApiEndpoints`,
MA-111 `RedirectToLogin`, MA-113 Redis pkg + `layer7-redis.md` pointer, MA-207 `StoryListingPageDto`,
MA-208 `StoryCharacterRelationship` tombstone); aria-labels on all 4 EditorView-wrapping submit buttons +
ChapterList toggle (MA-212/307/607/707); `IActiveUserContext.RequireUserId()` Core extension collapsing
5 duplicated guards (MA-210/308); `(int?)`→anonymous-type FK projections (MA-409/507); MA-305
(`ImportReviewPanel` `bg-danger`→token), MA-306 (attribution fire-and-forget now logged), MA-011
(`ToastHost` auto-dismiss tied to a disposal CTS), MA-511 (vestigial `CardShadowClass` inlined); comment/
test debris (MA-109/115/116/117/119/120/213); new `ConfirmDialogTests` (MA-121).

**Bucket-B + doc-staleness (all doc-touch):** BB-01 (`[ValidatableType]` — added `AddValidation()` +
.cs-not-.razor + root-only constraints), BB-02/MA-309 (write-throttle rule reframed cost-not-write-vs-read;
`"ImportParse"` concurrency limiter added to the 3 parse routes), BB-03/MA-004/604 (the "IActiveUserContext
won't exist in WASM" premise deleted; rule restated as testability discipline + 2 ratified exceptions),
MA-104 (default-allow recorded as operative posture), MA-108/114/118/122/123-note, content-safety/BaseBlogPost
TPT-filter contradiction corrected against code, MA-506/605/709 (stale-WU TODOs retargeted). Also MA-508
(group-create now throttled `ContentCreate`).

**Verified:** full three-tier suite green — **Unit 712 / RazorComponents 646 / Integration 734, 0 failures**
(the sweep + MA-402 added 22 regression tests: StoryEndpointsTests, BlogPostEndpointsTests,
UserStoryInteractionEndpointsTests, ChapterDraftVisibilityTests, ModerationEndpointsTests, +2 ResultsFilterPanel
resync tests; 2 chapter tests + 1 rate-limit test updated for the new draft-visibility/auth-floor behavior).
Browser E2E (server-only, live DB): all 7 sweep holes + MA-702 (both directions) + MA-302 + MA-402 confirmed
over the wire; StoryPage/discover render clean, no console errors.

- **Cells:** no Stage numbers change — audit findings targeted Stage-5 cells and corrected behavior in place;
  the "proposes reopen" flags across the slice files are resolved by this pass.
- **NOT done (deliberate — ⛔/🧑 in fix-status.md):** Desktop/Mobile pair merges (unvalidated placeholder
  seam), MA-610 Identity-scaffold prune (product call), export-writer DOM-walk visitor + MA-209/406/510
  extract-or-not seams (Brian's explicit-over-magic call), the residual client 401-mapping deviants.
- **Tool:** Claude Code (Fable), fan-out to parallel subagents for the sweep + mechanical batches.
  **Pointer:** `modernization-audit/fix-status.md` (rewritten to reflect this pass); per-cluster audit files.

---

## WU-StatusCodeSeams — MA-505 / MA-611 (DONE ✓ 2026-07-18)

Closed the deferred status-code seam (`modernization-audit/deferred-work.md` §4): authenticated
business-rule rejections that threw `InvalidOperationException` — mapped to **401** by the auth-safety-net
arm of `EndpointHelpers.ExecuteWriteAsync` — now throw typed `{Feature}ValidationException`s → **400**.
No shared-helper change (every new type derives from `CanalaveValidationException`, which the 400 arm and
`ExceptionPresenter` already match — MA-008).

- **New Core types:** `FollowingValidationException`, `BadgeValidationException`,
  `UserSettingsValidationException` (mirror the existing family).
- **Throw sites retyped:** self-follow / self-vouch / not-following (`ServerFollowingWriteService`);
  Hidden-Gem-limit / spotlight-limit (`ServerRecommendationWriteService`); unowned display key
  (`ServerBadgeWriteService`); unpinnable story (`ServerUserSettingsService`).
- **Client translators:** `ClientFollowingWriteService` 400 → `FollowingValidationException` (was
  `VouchLimitException`); `ClientBadgeWriteService` + `ClientUserSettingsService` gained a 400 arm;
  `ClientRecommendationWriteService` unchanged (already reconstructs its validation type).
- **Docs:** the 4 endpoint doc comments ("known mismatch" → "resolved"); `IFollowingWriteService` /
  `IBadgeWriteService` / `IUserSettingsService` exception docs; `layer5-wasm.md` "The Error-Translation
  Contract" (validation-family + `InvalidOperationException` rows, plus the stale name-suffix note
  corrected to the `CanalaveValidationException` base); per-cluster audit Stage notes.
- **Cells:** no Stage numbers change — corrected status semantics in place within already-Stage-5 cells.
- **Tests retyped:** the reject-at-limit / self-guard / unowned-key assertions in
  `FollowingWriteServiceTests`, `RecommendationWriteServiceTests`, `BadgeServiceTests`, and
  `ManualTreeSearchTests` (pinned-story gate) moved off `InvalidOperationException` onto the new typed
  exceptions (pinned-story coverage already existed there — no new test class added).
- **Verified:** Unit 712 / RazorComponents 646 / Integration 734 green; browser E2E — self-follow /
  not-following / unowned badge key / unpinnable story all return 400, unauthenticated still 401.
- **Tool:** Claude Code (Opus). **Pointer:** `modernization-audit/deferred-work.md` §4; audit
  Following/Recommendations/Badges/Profiles.

---

## WU-MigrationCollapse — Squash 34 migrations → 1 InitialSchema (DONE ✓ 2026-07-18)

Pre-launch "nuke and rebuild" (`canalave-conventions/layer1-data-model.md` "Migrations"): the dev DB
is rebuilt from scratch on every consumer (`DataSeeder.MigrateAsync`; Testcontainers per integration
collection) and the site exists nowhere else, so the 34 accumulated migrations (2026-06-20 → -07-18,
mostly make-nullable / drop-phantom-FK / rename churn) were collapsed to a single regenerated
`InitialSchema`.

- **Non-model DDL preserved by hand re-append** to the regenerated `InitialSchema.Up()`/`Down()` (the
  only DDL `ef migrations add` does not emit from the model; audit of all 34 confirmed exactly these
  two): the `daily_story_stats` raw table + `COMMENT` (ex-`R2_ViewCountToDailyStoryStats`) and the
  MVCC storage tuning `fillfactor`/`autovacuum_vacuum_scale_factor` on
  `user_chapter_interactions`/`daily_story_stats`/`user_story_interactions` (ex-`R4_MvccStorageTuning`).
  Everything else regenerates from the model (all tables/columns/FKs, the 7 USI partial-covering
  indexes + GIN + `stored` `search_vector` computed column, 17 `HasData` seed sets) or was a
  rename/data-transform moot on a from-scratch build.
- **Runtime bug surfaced during verification + fixed same session** (CLAUDE.md rule):
  `UserStatRecalculator.InsertMissingRowsSql` omitted `recommendation_successes_earned`, silently
  leaning on that column's `DEFAULT 0` — a leftover `AddColumn` artifact the EF model never declared
  (hence `has-pending-model-changes` clean). The collapse correctly shed the default and exposed the
  gap; fix adds the column to the explicit 0-seed list, honoring the method's own stated invariant
  ("list every column explicitly … not rely on the database"). No other raw-SQL INSERT path affected
  (`ViewCountFlusher`/`ReadingProgressFlusher`/`SiteDailyStatAggregator` targets are byte-identical
  before/after).
- **Verified:** `pg_dump --schema-only` before vs after diff is clean — identical object counts
  (97 tables / 138 indexes / 97 PK / 139 FK / 41 identity seq / 1 COMMENT / 3 storage-param settings),
  empty table+index name symmetric-diff; residual textual diffs are benign squash normalizations only
  (column ordinal order, named-vs-anonymous NOT NULL, stale-vs-fresh constraint/sequence names from
  pre-rename tables, and shed non-model backfill defaults). `has-pending-model-changes` clean.
  `dotnet test` green: Unit 712 / Integration 734 (incl. `ViewCountFlushTests` exercising the
  re-appended `daily_story_stats`, and all `UserStatRecalculatorTests`) / RazorComponents 646 (one
  pre-existing flaky `CanalaveTypeaheadTests.Escape_ClosesDropdown_WithoutSelecting` raced once in the
  full parallel run; passes 7/7 in isolation — unrelated to this change).
- **Cells:** no Stage numbers change (schema-maintenance op; no feature/layer behavior change).
- **Docs:** `layer1-data-model.md` "Migrations" — added the re-append rule, the two concrete
  non-model items, and the pg_dump schema-diff verification step.
- **Tool:** Claude Code (Opus). **Pointer:** `.claude/plans/please-put-together-a-tidy-hollerith.md`;
  `canalave-conventions/layer1-data-model.md` "Migrations".

## WU-IntTestPerf — Integration tier speedup: shared host + cheap seeding (DONE ✓ 2026-07-18)

Integration tier was **12m30s / 727 tests** — cost was repeated construction + wasted crypto paid
per `[Fact]`, serially, not the Postgres/Respawn realism. Two changes (plan:
`.claude/plans/put-together-a-plan-linked-hoare.md`; rigor archaeology in that plan's Context):

- **Tier A — no wasted password crypto** (`TestAppFactory`): `PasswordHasherOptions.IterationCount = 1`
  (auth is faked; no test verifies a password) speeds every `SeedUserAsync`; `DevSeed=None` (was
  `Minimal`) skips the seeder entirely — safe because role rows come from `HasData` (Respawn-ignored)
  and no test may depend on seeded users. Measured alone: 12m30s → **10m27s**, 727 green.
- **Tier B — one shared host, not one per test** (`PostgresFixture` now owns a collection-wide
  `TestAppFactory`, built once; `IntegrationTestBase.Factory` returns it). Respawn still resets the
  DB per test; a new `ResetSharedHostState()` resets the *only* stateful singletons — the mutable
  `FakeActiveUserContext` → `Anonymous()` and the three signal buffers' `Clear()`. Net: **10m27s →
  ~1m25s**, 727 green (two consecutive clean runs).
- **Shared-host conflicts surfaced by Tier B, both fixed** (exactly the "test that mutates shared
  host state" class the plan flagged): `DataProtectionPersistenceTests` called `Factory.Dispose()`
  to simulate process replacement — now uses two of its own throwaway factories; `HttpRateLimitTests`
  relied on a fresh rate-limiter window per test (stateful middleware) — now owns a per-test factory,
  same pattern as `WriteThrottleTests`. Both leave the shared host untouched.
- **Cells:** none — test infrastructure; no feature/layer behavior change. **Not in scope:** Tier C
  (parallelism via DB-per-worker); the pre-existing flaky `CanalaveTypeaheadTests` (RazorComponents).
- **Docs:** `testing.md` — new "Integration test host is shared collection-wide" section (Respawn owns
  DB isolation; the reset-hook rule for new stateful singletons; the own-your-own-factory rule),
  rewrote the `DevSeed` note, refreshed the stale ~2m20s figure. Stale doc-comments corrected on
  `TestAppFactory` / `IntegrationTestBase` / `PostgresFixture`.
- **Tool:** Claude Code (Opus). **Pointer:** `.claude/plans/put-together-a-plan-linked-hoare.md`;
  `canalave-conventions/testing.md` "Integration test host is shared collection-wide".

## WU-IntTestPerf follow-ups: durability tuning (no gain) + CanalaveTypeaheadTests flake fixed (2026-07-18)

Two loose ends from WU-IntTestPerf, closed same day:

- **Integration container durability tuning (Tier D) — landed but measured NO gain.**
  `PostgresFixture`'s ephemeral container now runs with `fsync=off` /
  `synchronous_commit=off` / `full_page_writes=off` (safe: throwaway container, nothing needs
  crash-durability). Real bug hit and fixed on the way: `.WithCommand("postgres", "-c", ...)`
  duplicated the program name — `PostgreSqlBuilder`'s own default command already supplies
  `postgres`, so the container exited 1 with `postgres: invalid argument: "postgres"` (repro'd
  directly via `docker run` before landing the fix: pass flags only, no leading `"postgres"`).
  Measured before/after (2 runs each): ~1m25s/1m35s pre-tuning → ~1m26s/1m27s post-tuning — no
  measurable change; kept anyway since it's zero-maintenance and doesn't regress anything.
  Tier C (DB-per-worker parallelism) remains the only lever left for further Integration
  speedup and is still out of scope.
- **`CanalaveTypeaheadTests.Escape_ClosesDropdown_WithoutSelecting` flake — root-caused and
  fixed**, closing the "pre-existing flaky" note carried since WU-MigrationCollapse. Forced a
  repro via a 40-iteration full-suite stress loop (RazorComponents runs test classes in
  parallel, unlike Integration): **6/40 failures (15%)** — 5× `Escape_ClosesDropdown_WithoutSelecting`,
  1× `EnterKey_SelectsHighlighted_ArrowsMoveHighlight` (different test, same mechanism — ruled
  out an Escape-specific logic bug). Root cause: the test helper's `DebounceMilliseconds = 1`
  ("effectively no debounce") still routes through a real `Task.Delay(1, token)` — a genuine
  timer-thread hop that, under the CPU contention of 40 parallel `dotnet test` processes, opened
  a real race between the debounced-search render and the test's immediately-following key
  event. `CanalaveTypeahead.razor`'s `HandleInputAsync` now skips `Task.Delay` entirely when
  `DebounceMilliseconds <= 0` (a legitimate improvement independent of tests — no reason to
  schedule a nil-duration timer); test helper changed to `DebounceMilliseconds = 0`, so
  `SearchMethod`'s already-completed `Task.FromResult(...)` awaits synchronously with no gap.
  Re-ran the identical 40-iteration stress loop post-fix: **0/40 failures** (p < 0.002 under the
  null hypothesis that the true rate is still 15%). Full `dotnet test` green (Unit 702,
  RazorComponents 510 ×2 clean, Integration 727 ×2 clean) confirms no regression.
- **Cells:** none — test-infra tuning + a test-only-triggered timing bug in shared UI plumbing;
  no feature behavior changed (production callers all use the real `DebounceMilliseconds`
  default, 300ms, never `<=0`).
- **Tool:** Claude Code (Opus). **Pointer:** `TheCanalaveLibrary.Tests.Integration/PostgresFixture.cs`;
  `TheCanalaveLibrary.SharedUI/Controls/CanalaveTypeahead.razor` `HandleInputAsync`.

## WU-CutFeature56 — Feature Contributions cut from the roadmap (DONE ✓ 2026-07-18)

- **Decision:** Feature 56 (Feature Contributions — admin attribution of accepted site-feature
  suggestions) rendered decision row 3's final verdict: **cut entirely**, not deferred. Rationale +
  full record: `middle_plan_v2.md` §Resolved "Feature Contributions (56) cut"; `audit/BlogPosts.md`
  Feature 56 CUT note. The prosocial-badge intent that motivated it is preserved by **keeping the
  Architect badge** as a manual grant (direct `user_badges` insert; `AwardAsync` stays unmapped).
- **Removed (code + schema):** `FeatureContribution` entity (`Core/Models/`), its `DbSet`, the three
  `SetNull` FK configs (`BaseBlogPostConfiguration`, `BaseCommentConfiguration`, User config) + the
  `FeatureContributions` navigations on `User`/`BaseBlogPost`/`BaseComment`, the
  `UserStat.FeatureContributions` counter (Core entity + `UserStatRecalculator` doc-list +
  insert-column list), the delete-path notes in `ServerCommentWriteService`/`ServerBlogPostWriteService`,
  the `UserStatRecalculatorTests` seed/assertion, the `SeedBulkWriter` `user_stats` COPY column+value
  (would have failed at runtime against the dropped column), and the `ReferenceSQL` table/column/FKs.
- **Migration:** sole pre-launch `InitialSchema` regenerated (nuke-and-rebuild per
  `layer1-data-model.md`); non-model DDL (`daily_story_stats` + MVCC tuning) re-appended by hand.
  New id `20260719023703_InitialSchema`. Architect badge seed retained (grep-confirmed present).
- **Cells:** Feature 56 row **removed** from `status.md` (number kept, not renumbered — marked CUT in
  `grid_axes.md`). No other cell Stage changed.
- **Also fixed (pre-existing, surfaced here):** `SeedBulkWriter`'s `user_stats` COPY listed
  `active_report_count`, a column `WU-UserStatRecalc` dropped — a latent runtime break in the
  (test-uncovered) SeedTool that predates this cut. Removed the column + its value write alongside
  the `feature_contributions` removal; `user_stats` COPY is now 22 aligned columns matching the
  live schema.
- **Verified:** `dotnet build` clean; `dotnet test` green all three tiers (Unit 702, RazorComponents
  510, Integration 727 — the Integration tier applied the regenerated migration + re-appended DDL to
  fresh Testcontainers-Postgres). Dev DB reset (`reset-dev-db.ps1`) + real server boot confirmed
  migrate + seed succeed end-to-end: psql ground truth — `feature_contributions` table absent,
  `user_stats` = 22 columns (no `feature_contributions`, no `active_report_count`),
  `daily_story_stats` present (re-appended DDL), Architect badge seeded, 7 users.
- **Tool:** Claude Code (Opus). **Pointer:** `audit/BlogPosts.md` Feature 56 CUT note.

---

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

---

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

---

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

---

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

---

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

---

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

---

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

---

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

---

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

---

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

---

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

## WU-StatBadgeProducers — Story Acknowledgments + Inspiration producer + no-tiers badge model (Features 10/22/50/58, extends `Messaging/`, `Spotlight/`) — DONE ✓ (2026-07-31)

- **Cells:** none flip — F10, F22, F50, F58 (and F49/F55 for the picker retrofits) all stay Stage 5;
  this is producer/plumbing work under already-sound cells, same shape as WU-MsgArchive/
  WU-GroupsL5b/WU-ApplyFiltersPurity above. Closes tracker item **B4** in full and **B3**'s two
  acknowledgment-counter rows (`SpotlightCount` re-filed under **B8** as its owner — no badge
  consumes it, and `audit/Spotlight.md` already filed it as riding with the donation pipeline).
- **Scope reasoning (why the one-line B3/B4 framing understated it, same lesson as A5/WU-TagFanon):**
  `AcknowledgedAsInspirationCount` turned out to be a producer hook onto the already-built
  `StoryLineage` "Inspired By" approval, not a new feature; `AcknowledgedAsBetaReaderCount` needed a
  whole new consent-gated feature (`StoryAcknowledgment` had schema and nothing else); scoping B4
  then surfaced that the Bronze/Silver tier paradigm itself had no design provenance (see
  `audit/Badges.md` "Tier paradigm — RETIRED site-wide" for the full Entry-#1577 trace) and was
  retired site-wide as part of this WU, not left as a parallel model beside the new badge.
- **New `Core/Collaboration/` + `Server/Collaboration/` cluster** (relocated from `Core/Models/`,
  closing MA-108/MA-112 for these four files): `StoryAcknowledgment` gained `StatusId`
  (`StoryAcknowledgmentStatus`: Pending/Accepted/Declined) + `DateResponded`;
  `IStoryAcknowledgmentReadService`/`WriteService` mirror `IStoryLineageReadService`/`WriteService`'s
  shape (request/accept/decline/revoke; consent always required — no self-owned-auto-approve case,
  since self-crediting is rejected outright; composite-PK row reuse on re-request-after-decline).
  `AcknowledgmentRole` id 5 "Inspiration" retired (redundant with the lineage-sourced counter — see
  `audit/Stories.md` Feature 10).
- **Producers:** `ServerStoryAcknowledgmentWriteService.AcceptAsync`/`RevokeAsync` drive
  `AcknowledgedAsBetaReaderCount` (role Beta Reader only, transition-delta decrement on revoke-while-
  Accepted). `ServerStoryLineageWriteService.ApproveLineageAsync`/`RejectLineageAsync`/
  `DeleteLineageAsync` drive `AcknowledgedAsInspirationCount` (type-1 "Inspired By" only, cross-author
  guarded via `IS DISTINCT FROM`, same transition-delta shape — both reject/delete can act on an
  already-Approved row with no status precondition, so prior status is captured before mutating).
- **No-tiers badge model (settled, retires WU36's Bronze/Silver — see `audit/Badges.md`):** a badge
  is earned at ≥1 and displays `UserBadge.EarnedCount` (new column). `IBadgeWriteService.AwardAsync`
  gained an `earnedCount` parameter, updating the count on both first award and every repeat
  qualifying event. `RecommenderSilver` removed outright (const/seed row/threshold literal/tests) —
  pre-production, so a clean generated migration with no hand-written data-preserving SQL.
  `Recommender` moved from threshold 10 to ≥1. `UserStatRecalculator` gained a third pass syncing
  `EarnedCount` from the corrected `UserStat` columns for badges with an automated producer
  (deliberately does not award — that stays the producers' job).
- **`UserPicker` retrofit (owed work, not new scope — see `audit/Badges.md`):** new
  `IUserProfileReadService.SearchUsersByNameAsync` (`ILike`, cap 10, deliberately ignores
  `ProfileVisibility` — addressing a user isn't disclosing their profile) backs a new
  `SharedUI/Users/UserPicker.razor` (mirrors `StoryTitlePicker`). Retrofitted into
  `ComposeConversationModal` (Messaging) and `ModSpotlightPage` (Spotlight, which also **drops its
  cross-cluster `IMessagingReadService` injection**) — both previously borrowed
  `IMessagingReadService.FindUserByUsernameAsync` (exact-match) as a documented stopgap.
  `FindUserByUsernameAsync` stays live for the `ComposeForUsername` deep-link path.
- **New UI:** `MyAcknowledgmentsPage` (`/acknowledgments`, mirrors `MyStoryLineagesPage` — incoming
  Accept/Decline inbox + outgoing Revoke list + create form), `StoryAcknowledgmentsBox` (public
  display leaf, Accepted-only, mirrors `StoryLineageBox`), wired into `StoryPage`, `StoryEditorPage`
  ("Manage acknowledgments →" link), `UserMenu`/`CreateMenu`. Badge count renders as `×N` in
  `UserCard.razor` and `BadgeSettingsForm.razor`.
- **Verified:** `dotnet build` clean. `dotnet test` full suite green — 776 Unit + 626 RazorComponents
  + 1,012 Integration = **2,414 total** (up from the 2,330 baseline). New: `StoryAcknowledgmentServiceTests`
  (full lifecycle, self-credit rejection, counter inc/dec, badge award + `EarnedCount`), 6 new tests
  in `StoryLineageServiceTests` (Inspiration producer), `UserStatRecalculatorTests` (both new
  aggregates, the `EarnedCount` sync pass, a does-not-award-missing-badges guard), `RecommendationWriteServiceTests`
  (≥1 award replacing the retired 10/50 boundary pair), `UserProfileEndpointsTests`
  (`SearchUsersByNameAsync`). Two real bugs found by the test run itself, not anticipated in the
  plan: the new counter producers are silent no-ops without a seeded `UserStat` row (same documented
  caveat as `RecommendationSuccessesEarned`) — seven new tests initially failed until
  `SeedUserStatAsync` calls were added; `ModSpotlightPageTests`/`StoryPageTests`/
  `StoryExternalLinksRowTests` needed new fake-service registrations for the added `@inject`s.
  Browser-verified end to end against a freshly reseeded dev DB: full credit→notify→accept→badge
  round trip (`BetaReader×1`, no tier) with `psql` ground truth at each step; public
  `StoryAcknowledgmentsBox` renders on the story page; `Recommender×12` (seeded) renders correctly;
  `RecommenderSilver` absent from the live `badges` table and the role dropdown shows only 4 roles;
  both `UserPicker` retrofits driven live (Spotlight grant end to end, Messaging compose modal).
- **`SeedTool` follow-up (2026-07-31, same day):** added `SeedAcknowledgmentRow`/`SeedLineageRow`
  generators (~15% of stories get 1-3 acknowledgment credits, roles 1-4, Accepted/Pending/Declined
  mix; ~8% get a cross-author "Inspired By" link, Approved/Pending/Rejected mix — self-credit and
  same-author links are excluded by construction, matching the write services' own guards).
  `CopyUserStatsAsync`'s two counter columns — previously hardcoded to 0 in every COPY row — now
  compute real values from the generated graph, mirroring the producers' exact formula (Accepted +
  role 1 for beta-reader; Approved for inspiration). Notifications (types 50/51/52) generated too,
  matching the tool's existing one-notification-per-social-action convention. **Verified independently,
  not just "ran without error":** a 500-user/800-story run (seed 42) produced 208 acknowledgments /
  58 lineage links with zero FK violations, and a direct `psql` cross-check running
  `UserStatRecalculator`'s own aggregate SQL against the loaded data returned **zero mismatches**
  against what `SeedBulkWriter` wrote — the seeded counters are provably correct by the same formula
  the production recalculator uses, not merely plausible. Verification run wiped afterward
  (`reset-dev-db.ps1`) — the persistent dev DB is back to a clean `DataSeeder`-only state.
- **Tool:** opusplan. **Pointers:** `layer2-services.md` §"Synchronous Inline Badge Awards" and
  §"UserStats Updates"; `audit/Badges.md` §"WU-StatBadgeProducers" (primary narrative + provenance
  trace); `audit/Profiles.md` Feature 22/58; `audit/Stories.md` Feature 10; `audit/Messaging.md` and
  `audit/Spotlight.md` (picker retrofits); `design/access-gating-first-principles.md` (search
  visibility exclusion); `.claude/hidden-deferrals-tracker.md` B3/B4/B8. **Deps:** none (built on
  already-Stage-5 `StoryLineage`, `UserStat`, `Badge` cells).

---

## WU-DataSaver — `PrefersDataSaverMode` cut; image derivative sizing opened as B14 (Features 3/20/21/22, extends `Profiles/`, `Sprites/`) — DONE ✓ (2026-07-31)

- **Cells:** none flip — F3 (Sprites), F20/F21/F22 (Profiles) all stay Stage 5; removing an inert
  setting doesn't change any layer's soundness. Closes tracker item **B0**; opens **B14**.
- **Decision (measured before building, not just decided — see `roadmap.md` §Resolved for the full
  reasoning):** B0's own framing ("suppress sprites, or cut the setting") was wrong on the numbers.
  Sprites render at 16px across 3 sites (`TagChip`/`TagSelector`/`CharacterEntry`) in low-KB static
  PNGs, and the one sprite saving that was ever material — animated `.webp` → static `.png` — is
  already delivered by `PrefersAnimatedSprites = false`. The real weight is cover art/avatars:
  `ImageUploadProcessor.MaxStoredDimension = 2048` stores one size per upload, served into 24–144px
  display slots on 20-item listing grids, and no `srcset`/thumbnail mechanism exists anywhere in
  the app. **Settled: cut the setting; open the real gap as its own item (B14), sequenced with
  Phase 7's R2/Cloudflare work — not a ride-along here** (upload pipeline + both storage impls +
  ~16 markup consumer sites is properly its own WU).
- **Removed end to end:** `User.PrefersDataSaverMode` (Core); `UserSettingsDto`/`IUserSettingsService`
  member and `UpdateAppearanceAsync` parameter (3→2); `ServerUserSettingsService`'s projection/DTO-
  arg/`ExecuteUpdateAsync` `SetProperty`; the `/api/user-settings/appearance` query parameter;
  `ClientUserSettingsService`'s query segment; `AppearanceSettingsForm`'s checkbox + `AppearanceArgs`
  member (also fixed a pre-existing doc/code name drift: audit called it `AppearanceModel`, code has
  always been `AppearanceArgs`); `SettingsPage`'s wiring; the `FakeSavedTagSelectionTestServices`
  fake. **`SeedTool`'s `SeedBulkWriter.CopyUsersAsync`** uses a positional binary COPY — column list
  and write sequence edited together (a misalignment there fails silently, not loudly, writing
  plausible-looking wrong data into a neighboring column). `scripts/verify-tagfanon-migration.ps1`
  left untouched deliberately: it inserts at the pre-migration (`RecLifecycle`) schema state where
  the column still existed and isn't CI-invoked, so it remains a correct historical snapshot.
- **Migration:** `20260731152702_DropPrefersDataSaverMode` — a real `DropColumn` (hot scalar column,
  not part of a JSON settings group, so unlike `RemoveAutoLoadNextChapter` this isn't an empty
  jsonb-only migration).
- **Verified:** `dotnet build` green (0 errors). `dotnet test` green — 776 Unit + 626 RazorComponents
  + 1,012 Integration = **2,414 total**, unchanged from the pre-removal baseline (no new testable
  surface; the fake-service signature edit is compiler-enforced conformance). Migration applied
  against local Postgres via server startup (`Database.MigrateAsync`); `prefers_data_saver_mode`
  confirmed absent from `AspNetUsers`, `prefers_animated_sprites` confirmed present. `SeedTool` run
  (seed 42, 2,000 users) verified **by value, not just success**: `prefers_animated_sprites`,
  `show_mature_content`, `profile_picture_relative_url`, `theme_id`, `two_factor_enabled` all
  checked directly against the hardcoded seed values — the positional-COPY risk was a silent
  misalignment, not a thrown error. No browser tool available in this environment; substituted a
  live HTTP round-trip against the running dev server instead (`dev-login` → `PUT
  /api/user-settings/appearance?themeId=1&prefersAnimated=false` → 204 → `GET
  /api/user-settings` reflects the change with no `prefersDataSaverMode` field anywhere in the
  response). Verification run wiped afterward (`reset-dev-db.ps1`) — the persistent dev DB is back
  to a clean pre-seed state.
- **Tool:** opusplan (plan-mode decision + build). **Pointers:** `hidden-deferrals-tracker.md`
  B0/B14; `roadmap.md` §Resolved; `audit/Profiles.md` Feature 20 Stage note (primary narrative);
  `audit/Sprites.md` Feature 3 L3-Logic/L3.5-Structure notes; `audit/Identity.md` Shared Context;
  `layer2-services.md` §"Self-Referential Editing Exception." **Deps:** none.

---

## WU-DiscoveryOverrideUI — §8.7 filter-override editing UI; `UserCustomFilter` cut (Features 31/21/22, extends `Discovery/`, `Profiles/`) — DONE ✓ (2026-07-31)

- **Cells:** none flip — F31 (Search Page), F21/F22 (Profiles) all stay Stage 5; the read/merge half
  was already Stage 5 and this closes the invisible gap under it (same shape as B0/B4/B12). Closes
  tracker item **B7**; opens **B15** (`CollapseCommentThreads` inert) and **B16**
  (`ResultsFilterPanel`'s separate `PageSize=20` hardcode).
- **Decisions settled (2026-07-31, Brian-ratified — do not revisit; see `roadmap.md` §Resolved for
  full reasoning):**
  - **Surface: `/settings`, not `ResultsFilterPanel`.** `audit/Discovery.md`'s own earlier "likely
    composes into ResultsFilterPanel" guess is superseded — the panel is deliberately injection-free
    with dispatcher-resolved seed state, and an inline affordance would only ever reach whichever
    search mode the user happens to be on. `NotificationSettingsPage` is the precedent.
  - **`UserCustomFilter` cut entirely, not trimmed.** It's bidirectional (group whitelist as well as
    blacklist), so the 2026-07-13 Custom Lists ethics ruling only ever cleared the
    `PersonalList`/`PublicList` half; the surviving group/folder half is simply unbuilt, unrequested,
    and undesigned. Six columns, trivially re-addable.
  - **`CollapseCommentThreads` deliberately excluded.** Comments has no collapse behavior to hook
    into at all — building it means designing new thread UI, a call for Brian to make from using
    the site, not one this WU should pre-empt.
- **Built:**
  - `Core/Discovery/`: `IDiscoveryFilterSettingsService`, `DiscoveryFilterModeDto`,
    `DiscoveryFilterRowDto` — new self-referential CQRS-lite exception, separate from the
    anonymous-callable `IDiscoveryDefaultsReadService`.
  - `Server/Discovery/ServerDiscoveryFilterSettingsService.cs`: `GetMyMatrixAsync` (four confirmed-
    consumer modes × six mappable keys); `SetOverrideAsync` sparse upsert/delete, mirrors
    `ServerNotificationWriteService.SetSettingAsync` exactly. `ServerDiscoveryDefaultsReadService
    .KeyToEnum` promoted `internal` so both services share one source of truth for mappable keys.
  - `Client/Discovery/ClientDiscoveryFilterSettingsService.cs` + `DiscoveryDefaultsEndpoints.cs`'s
    new `[Authorize]`d `/my-matrix` sub-group (GET + PUT), leaving the anonymous `GET /` untouched.
  - `SharedUI/Profiles/DiscoverySettingsForm.razor` (parameter-driven, no `@inject`) wired into
    `SettingsPage.razor` alongside its eight existing sub-forms; instant-save per toggle with
    optimistic local update (mirrors `NotificationSettingsPage.HandleToggleAsync`). Reuses
    `UserStoryInteractionFilter.LabelFor` (promoted `internal`) for identical wording with the live
    filter panel.
  - **`UserCustomFilter` removed:** `Core/Models/UserCustomFilter.cs`, `FilterEntityType` enum,
    the DbSet, `DiscoveryConfigurations`' `UserCustomFilterConfiguration`, and the `SearchMode`/
    `User` nav-collection wiring in `IdentityConfigurations`/`SearchMode.cs`. Migration
    `DropUserCustomFilter` (clean `DropTable`). Both retired names added to
    `scripts/check-doc-hygiene.ps1`'s registry.
  - **`SearchPage.razor`:** `RandomBatchSize` constant → `_randomBatchSize` field, seeded from
    `ReaderSettingsDto.DefaultPaginationSize` for authenticated viewers (anonymous keeps the
    hardcoded 20). `DefaultSearchSort` seeds the initial `Sort` only when it equals `DatePublished`
    — `Relevance` needs a live text query that doesn't exist pre-load, and `Score`/`RecentlyRead`
    aren't in this surface's `AvailableSorts` at all, so either falls back to Random rather than
    being misapplied (`ReaderSettingsForm`'s dropdown offers every `DefaultSortOrder` value with no
    per-surface restriction — a latent gap worked around here, not fixed). The initial-load branch
    now dispatches on `IsRandomMode` instead of unconditionally loading a random batch — required
    once Sort could be non-Random on first render.
  - **Found, not fixed:** `ResultsFilterPanel.ApplyAsync`'s separate `PageSize = 20` hardcode
    (sorted-mode pagination, shared across `/discover`/Bookshelves/Profile tabs) — a genuine
    cross-cutting fix across several consumers, filed as **B16** rather than folded in here.
- **Verified:** `dotnet build` green (0 errors). `dotnet test` green — 776 Unit + 635 RazorComponents
  + 1,021 Integration = **2,432 total**. New coverage: `DiscoveryFilterSettingsServiceTests.cs`
  (Integration — matrix read merges defaults with overrides; `SetOverrideAsync` inserts on
  divergence/deletes on return-to-default/idempotent/unauthenticated throws/unmappable key no-ops;
  a round-trip test confirms `IDiscoveryDefaultsReadService` observes rows the new write path
  persists); `DiscoverySettingsFormTests.cs` (RazorComponents — section/checkbox rendering, label
  wording, `OnToggle` payload, `Busy` disables); `SearchPageTests.cs` additions (anonymous keeps
  batch size 20; authenticated reads `DefaultPaginationSize`; `DatePublished` default loads sorted
  mode on first render; `Score` default falls back to random). Migration applies cleanly to a fresh
  database. `check-doc-hygiene.ps1` / `check-design-tokens.ps1` both clean. Browser-verified end to
  end (toggle persists/round-trips as a DB row, deletes on return to default, takes effect on
  `/discover` and Also-Favorited after a fresh navigation, survives cross-browser, anonymous still
  gets system defaults; Reader Settings page size/sort honored on `/discover` with the
  device-local restore still winning over the account default).
- **Tool:** Sonnet in Claude Code (Stage 2/3 mixed — surface/cut decisions settled via
  `AskUserQuestion`, then built from spec + audit). **Pointers:** `hidden-deferrals-tracker.md`
  B7/B15/B16; `roadmap.md` §Resolved; `audit/Discovery.md` §"WU-DiscoveryOverrideUI Stage note";
  `audit/Profiles.md` Feature 20; `layer2-services.md` §8.7. **Deps:** none.

---

## WU-SweepRiders — closes tracker H1, E4, H8 (cross-cutting, extends `Errors/`, `Seo/`, Identity) — DONE ✓ (2026-07-31)

- **Cells:** none flip. H1/E4 are off-grid (no cell); H8 leaves F1 L4 at Stage 1, unchanged —
  this WU settled the *decision* the Phase 3 sweep needed, not the sweep's own styling pass.
- **Why pulled ahead of Phase 3** (`roadmap.md` Tier 4 originally read "fold into the same sweep —
  don't build standalone"): H8 governs whether ~1,325 LOC of Identity 2FA/passkey/external-login
  pages get styled at all during that sweep, so it needed settling first, not during.
- **H8 — MA-610 prune-vs-keep: keep, across 2FA, passkeys, and external login (Brian, 2026-07-31).**
  Corrected the tracker/audit entry's own over-generalization along the way: only external login is
  provider-dependent (OAuth/OIDC client registration) — its `ManageNavMenu.razor` entry is already
  conditional on `hasExternalLogins`. TOTP 2FA (`RequiresTwoFactor` → `LoginWith2fa.razor`) and
  passkeys (`SignInManager.PasskeySignInAsync`/`MakePasskeyCreationOptionsAsync`, wired via
  `PasskeySubmit.razor` and two endpoint maps in `IdentityComponentsEndpointRouteBuilderExtensions.cs`)
  are functional today with no external config — not inert scaffold. No code changed; kept as-is:
  `ManageNavMenu.razor`, `CanalaveSignInManager`'s overrides, `Login.razor`'s passkey/2FA branches,
  the four `/Account/*` endpoint maps. **Not closed by this decision:** the MA-112/608/012
  just-in-time org moves bundled into the same tracker entry (`UserDeletionService`,
  `MainLayout.razor` under `Server/Components/Layout/`, `Core/Models` scaffold, `NotFound.razor`) —
  a distinct organizational question this WU's clusters (`Errors/`/`Seo/`) don't touch. **Opened
  H9** — none of these three flows has ever been driven end-to-end; "keep" rests on the code
  looking intact, not on verified behavior.
- **H1 — `Error.razor` mismatch: closed with no code change; the tracker context was stale.**
  MA-110 (2026-07-18) had already replaced the "Development Mode boilerplate" this entry
  described with the plaque/vessel treatment, before this WU started. The one open question —
  whether `/Error` gets any layout, given it carries no `@layout` and the Server assembly is
  excluded from `Routes.razor`'s Router `AppAssembly`/`AdditionalAssemblies` — turned out to
  already be resolved: `AuthorizeRouteView`'s `DefaultLayout="typeof(MainLayout)"` ambient default
  wraps it in the real site chrome regardless (that Router exclusion governs client-side
  SPA-navigation matching only, not which layout a statically-routed SSR endpoint receives).
  Verified live: started the server under a non-Development (`Staging`) environment via an
  explicit `ConnectionStrings__canalavedb` env var (bypassing `appsettings.Development.json`,
  which only loads under `Development`), hit a temporary forced-throw endpoint, and confirmed via
  both `curl` and a real browser tab: single "Canalave Library" header (no duplication), full nav
  chrome, correct plaque/request-id content, and — separately — that the wire status code is
  already **500**, not 200, so no `OnInitialized` status re-assert (the pattern
  `StatusCodePage.razor` uses for a *different* middleware, the status-code re-execute path) was
  needed. The temporary endpoint and the Staging-only env vars were removed before finishing;
  no production code path changed.
- **E4 — default OG/social image was an SVG, not a raster: built.** New shared constant
  `TheCanalaveLibrary.Core.SeoDefaults.OgFallbackImagePath` = `/img/og-default.png`, replacing the
  `/img/default-cover.svg`/`default-avatar.svg` literal previously repeated across seven files, at
  all eight `og:image`/`twitter:image` call sites: `HomePage`, `StoryPage`, `ChapterReadingPage`,
  `SeriesPage`, `GroupPage`, `BlogPostPage`, `ProfilePage`, `ContentGateInterstitial`. The in-page
  `<img>` placeholders (`StoryCard.DefaultCoverArtFallback` and every avatar `<img>` fallback)
  are untouched — this only changes the crawler-facing fallback, since crawlers don't reliably
  rasterize SVG for card images but do for `<img>` (a decorative element the crawler doesn't
  render). Asset: `wwwroot/img/og-default.png`, 1200×630, generated with ImageSharp from a
  throwaway scratchpad console app (not committed) rendering the site's own design tokens
  (`--color-surface`/`--color-border`/`--color-action-ink`, Fraunces via the site's own
  `fraunces-var.woff2`) — no new package dependency added to the Server project, since the
  rendering need is build-time-only. At Brian's request the asset carries a visible
  "AI-generated placeholder — replace before launch" caption baked into the image, so its
  provisional status is legible on the card itself, not only in commit history.
- **Verified:** `dotnet build` clean; `dotnet test` green, unchanged count (no new automated tier
  applies to any of the three — H1/E4 are markup/asset/constant changes with no new branching,
  H8 is a decision; `PublicUrlProviderTests`' existing fallback-resolution coverage needed no
  change, since its literals are arbitrary test inputs, not the production constant). Browser
  band: `/Error` per above; OG tags via prerendered-HTML `curl` (crawler-equivalent fetch, no JS)
  across a coverless story, a series, a group, a blog post, a no-avatar profile, and the homepage
  — `og:image`/`twitter:image` all resolve absolute and end in `og-default.png`; direct fetch of
  `/img/og-default.png` confirmed 1200×630 `image/png`.
- **Tool:** Sonnet in Claude Code (H1/E4 execution) + chat (H8 decision, Brian). **Pointers:**
  `hidden-deferrals-tracker.md` H1/E4/H8/H9; `modernization-audit/deferred-work.md` §2 MA-610
  (updated in place); `error-handling.md` §"The `/Error` HTML path"; `design/surface-registry.md`
  §"Sweep completion" (corrected); `audit/Seo.md` (Open item moved to Resolved); `roadmap.md`
  §Resolved. **Deps:** none.

---

## WU-A11y (Structure) — static/naming accessibility sweep + Modal primitive (Feature 65, extends `Dialogs/`, `Controls/`, most SharedUI clusters) — DONE ✓ (2026-07-31)

- **Cells:** Feature 65 L4-Style 1 → 5. L4.5-Browser stays 1 — deliberately (see below).
- **Decision row 12, resolved same day:** sweep by defect class, not by page; no fourth test tier
  (extend bUnit + add `scripts/check-a11y.ps1`); extract the shared `Modal` primitive
  (`layer3.5-structure.md`'s "third consumer" deferral trigger had fired — 9 sites);
  `Server/Identity/` in scope. Full rationale: `roadmap.md` §Resolved.
- **Scope split, same day, along the line the decision itself drew:** ARIA that *names* existing
  structure (this WU) vs. ARIA that *promises an interaction model* not yet built
  (`aria-modal`, focus trap, combobox roles — WU-A11y-Keyboard, paired with the Phase-3 L4 sweep).
  Rationale: axe-DevTools cannot test keyboard behavior at all, and shipping the naming half
  under full mechanical-gate coverage is strictly better than shipping a floor that immediately
  decays unverified.
- **`Modal` primitive** — `SharedUI/Dialogs/Modal.razor`: `role="dialog"` + `aria-label="@Title"`
  (not `aria-labelledby`, deliberately — a generated heading id would drift across the
  InteractiveAuto prerender→WASM handoff; `aria-label` needs no id machinery and can't drift from
  the visible heading since `Title` is the source of both). No `aria-modal`, no focus trap — an
  honest omission until WU-A11y-Keyboard, not silently promised. All 9 prior hand-rolled overlay
  sites migrated onto it with contracts preserved: `ConfirmDialog` (rebuilt on top, 14 consumers
  untouched — its own `IsOpenChanged` is wired explicitly rather than via `@bind-IsOpen` sugar on
  `<Modal>`, because that sugar only assigns a local field and would silently break upward
  propagation to `ConfirmDialog`'s own consumers), `ReportDialog`, `ComposeConversationModal`,
  `EditorView`'s preview popup, both `SavedTagSelection*Inner` dialogs, `TagDirectoryPage`,
  `DesignGalleryPage` (×2, including the gallery's own living-reference sample).
- **Labelling swept by defect class across 17 files** — 43 orphan `<label>`s + 2 dangling `for=`
  targets, found by hand-writing and running the exact classifier gate E now runs (not estimated):
  `StoryPropertiesForm` (8), `ChapterPropertiesForm` (6, plus one real bug — see below),
  `BlogPostPropertiesForm` (4), `SiteAnnouncementPropertiesForm` (2), `SeriesCreateEditPage` (3),
  `MyStoryLineagesPage` (3), `CharacterEntry` (3), `MyAcknowledgmentsPage` (3),
  `GroupCreateEditPage` (3), `StoryChapterImport` (1 real + 1 false positive — a label wrapping a
  hidden `<InputFile>` across Razor `@if` branches, which an earlier draft of gate E's regex
  mis-classified as orphan; fixed by matching `Input[A-Za-z]+` instead of `Input[A-Z]\b`, which
  never matches multi-letter component names since `\b` needs a word boundary immediately after
  the single capital), `TagSelector` (1), `PairingBuilder` (1), `ChapterFileImport` (0 real, same
  false-positive class), `GroupPage` (1), `ShipFilter` (1), `FlatTagOverlayEntry` (1),
  `ProfileSettingsForm` (1), `ComposeConversationModal` (2, incl. the dangling `for=`),
  `LoginWith2fa` (1 dangling `for=`, resolved by removing it — the label already wraps its
  checkbox, so `for=` was redundant debris, not a missing association).
  Three mechanical shapes: `for=`/`id=` pairs (kebab-case, matching the house pattern already in
  `stsd-nickname`/`Input.Email`; 4 loop-rendered components — `CharacterEntry`,
  `FlatTagOverlayEntry`, `PairingBuilder`, `TagSelector` — get a `Guid`-based per-instance id,
  since a static id would collide across simultaneously-rendered instances);
  `role="group"`/`aria-labelledby` for composites with no single native control (mostly
  `ContentSurface`/`EditorView` wrappers — a `<span id>` demotes the old `<label>`, never a
  `<label for>` pointing at a non-labelable element like a `<p>`); and a new
  `CanalaveTypeahead.AriaLabel` parameter (nullable, renders no attribute when unset) threaded
  through `TagSelector`, `ShipFilter`, `UserPicker`, `StoryTitlePicker`, `FanonAxisPage` — the
  picker-owns-its-name case, which also incidentally names every one of those inputs for the
  first time (none had any accessible name before, not just a mismatched one).
- **One real bug found via the new bUnit helper (below), not the static gates:**
  `ChapterPropertiesForm`'s "Version Rating" `role="group"`/`aria-labelledby` wrapper named the
  *group*, not the `<select>` inside it — `aria-labelledby` on an ancestor doesn't propagate to a
  descendant control's own accessible name (WCAG 4.1.2 needs the control itself named). Fixed
  with a direct `aria-label="Version Rating"` on the `InputSelect`. Audited every other
  `role="group"` site introduced this WU for the same shape; all others wrap either a
  `ContentSurface`/`EditorView` composite (no native control the selector matches) or a component
  that already self-names via the new `AriaLabel` parameter (`UserPicker`/`StoryTitlePicker`
  inside `MyAcknowledgmentsPage`/`MyStoryLineagesPage`/`ComposeConversationModal`/`GroupPage`) —
  confirmed safe, not just assumed.
- **All 43 `ValidationMessage` usages** (18 files — the 3 SharedUI forms above + 15 Identity
  scaffold pages) carry an `id="{Prop}-error"`; their inputs carry a matching
  `aria-describedby`. The 15 Identity pages were transformed by a small idempotent script (not
  hand-edited file by file) exploiting the scaffold's exactly-uniform
  `id="Input.X"`/`For="() => Input.X"` pattern — diffed by hand afterward to confirm nothing
  beyond the intended two insertions changed per field, and that the file's own em-dash/unicode
  prose survived the UTF-8-explicit read/write round trip.
- **Avatar `alt=` convention standardized on `alt=""`** — 5 sites moved off `alt="@Username"`
  (`ComposeConversationModal`'s preset-recipient branch, `ConversationListItem`, `MessageItem`,
  `MessageThread`, `ProfileBanner`): every one sits beside visible username text, so a matching
  `alt` double-announces the name to screen readers. `alt=""` was already the majority pattern
  (`UserCard`, `UserPicker`, `CommentItem`, `ModSpotlightPage`) — this makes it universal, not a
  new decision.
- **`BadgeSettingsForm`'s `alt="" title="@b.DisplayName"` fixed** — `title=` removed (redundant
  with the adjacent visible `DisplayName` span; hover-only and not reliably exposed to AT anyway —
  `layer4-style.md`'s "no title-only essential info" rule).
- **`prefers-reduced-motion` CSS block added** to `Server/Styles/app.css`, confirmed present in
  the Tailwind-compiled `wwwroot/app.css` output (built and grepped, not assumed). **Documented,
  not silently narrow:** covers `transition-`/`animate-` utilities only — does nothing for
  animated `.webp` sprites (animated images ignore CSS animation properties entirely); that axis
  is `User.PrefersAnimated` (defaults `true`), unreachable from a media query without a
  `matchMedia` service, which `render-and-layout.md` bans absolutely. Logged as tracker **A9**,
  not left implicit.
- **`scripts/check-a11y.ps1`** — 7 gates, mirroring `check-design-tokens.ps1`'s structure as a
  separate script (that script's name/header narrate a token-specific purpose; a11y rules churn
  independently): **B** modal recipe (`z-(--z-modal)`) confined to `Modal.razor` — the
  highest-value rule, since it's what stops the shell being re-hand-rolled; **A** every
  `<label for="X">` resolves to an `id="X">` in the same file; **C** every `ValidationMessage` has
  an id referenced by a matching `aria-describedby`; **D** icon-only `<button>`s (glyph set built
  from `\u`-codepoint escapes at runtime, not literal characters, to keep the script's own
  executable lines pure ASCII — Windows PowerShell 5.1 misreads non-BOM UTF-8 in executable code,
  which is exactly the bug that broke an earlier draft of gate B's own message string) need a
  name; **E** orphan-label detection (the classifier described above); **F** `<img>` alt
  presence/misuse; **G** the reduced-motion rule stays present. A shared `Get-RazorMarkup` helper
  strips `@* ... *@` Razor comments before every gate scans a file — without it, gate F flagged
  `Sprites/ThemeContextProvider.razor`'s doc-comment example `<img src>` as a real violation.
  **Every gate mutation-tested by hand** (deliberately broke a known-good file — removed an
  `aria-describedby`, stripped an `alt=` — confirmed the gate caught it, restored the file) rather
  than trusted on the strength of passing against an already-clean tree. Wired into
  `.github/workflows/ci.yml` alongside the design-token check.
- **New bUnit `AccessibleNameAssertions` helper** (`TheCanalaveLibrary.Tests.RazorComponents`) —
  renders a component, asserts every `<input>`/`<select>`/`<textarea>` has an accessible name
  (`aria-label`, a `for=`/`id=` match, or a wrapping `<label>` ancestor), skipping `type="hidden"`
  fields and Quill's own toolbar chrome (`EditorView`'s `<ToolbarContent>` — third-party markup
  Quill.js enhances client-side; bUnit never runs real JS so it renders unlabelled, an accepted
  exception mirroring `check-design-tokens.ps1`'s own named exemption list). Complements, doesn't
  duplicate, the static gates: it inspects the rendered DOM, so it catches names supplied by a
  *child* component and — as the Version-Rating bug above shows — catches a class of gap the
  source-level regexes structurally cannot. Applied to the four densest label files:
  `StoryPropertiesFormTests`, `BlogPostPropertiesFormTests`, `SeriesCreateEditPageTests` (existing
  files, one test added each), `ChapterPropertiesFormTests` (new file — none existed before).
- **Addendum, same day — the axe-DevTools browser pass ran after all.** Chrome browser-automation
  tooling became available mid-session, closing the gap above. Real axe-core 4.10.2 (`<script>`
  tag injection — the extension's isolated-JS-world `eval` path silently failed to attach
  `window.axe`, a page-main-world `<script src>` worked) ran against all six planned pages.
  **Fixed:** missing `<h1>`/`<PageTitle>` on `/` and `/discover` (sr-only additions,
  `HomePage.razor`/`SearchPage.razor` — no visual change, since adding a *visible* header row is
  Phase-3 L4 sweep territory, not a structural fix); two sidebar `<aside>`s nested inside `<main>`
  on `/discover` and `/messages` (axe's `landmark-complementary-is-top-level` — changed to `<div>`
  in `SearchPage.razor`/`MessagesPage.razor`, arguably the better semantic fit anyway since both
  are integral to the page's primary task, not tangential content); `ChapterNavigation.razor`'s
  duplicate `<nav aria-label="Chapter navigation">` (landmark-unique — added a `NavLabel`
  parameter, `ChapterReadingPage.razor` passes `"...top"`/`"...bottom"`) and its disabled
  prev/next `<span>`s' `aria-prohibited-attr` (added `role="link"` — the ARIA APG pattern for a
  disabled link; a bare `<span>`'s implicit role doesn't support `aria-label`). Doc synced in the
  same edit: `layer3.5-structure.md`'s `ChapterNavigation` exemplar code sample.
  **Confirmed as accepted, not fixed, exactly as designed:** Quill's own toolbar chrome
  (`button-name`/`aria-command-name`, matches `AccessibleNameAssertions`' documented exemption);
  the not-yet-rendered `ValidationMessage`/`aria-describedby` id on `#story-title` etc.
  (`aria-valid-attr-value` incomplete — matches the plan's own prediction verbatim: axe reports
  "incomplete," not a violation). **New real finding, measured not guessed, NOT fixed —
  Brian's design-token decision:** `--color-tagtype-character` white-on-`#6890f0` = 3.07:1,
  `--color-tagtype-genre` white-on-`#f85888` = 3.11:1, the Indicator success-tint recipe = 2.88:1,
  one other tint pairing = 1.7:1 — all fail the ratified 4.5:1 policy. These are locked,
  gate-reviewed, live-tuned-with-Brian tokens (some "transcribed verbatim from *Visuals.cs*" —
  Pokémon-canon colors), so WU-A11y (Structure) did not change the hex values unilaterally.
  **New, unrelated, severe, NOT fixed — flagged for immediate attention:**
  `/Account/Login` and `/Account/Register` both return a raw 500
  (`System.InvalidOperationException: The registered callback PersistProperty must be associated
  with a component or define an explicit render mode type during registration.`) for every
  visitor, authenticated or not — reproduced twice, independent of auth state, confirmed unrelated
  to this WU's own attribute-only edits to those files. **The entire Identity/auth funnel was
  broken.** Deliberately not diagnosed further here (real risk of an unrelated Blazor-routing
  rabbit hole) — logged as tracker **H10**, high priority. **Resolved same day by WU-H10Fix**
  (see Last landed). The hypothesis recorded at this point — "three `[PersistentState]`-bearing
  descendants (`NotificationBellInner`, `MessagesNavLink`, `ReaderDisplayProvider`)" — was wrong
  on both counts: two offenders, and `ReaderDisplayProvider` was the *correct* precedent, not one
  of them. `audit/Identity.md`'s Stage note carries the confirmed root cause.
- **Verified:** `dotnet build` clean across the solution. `dotnet test` green:
  RazorComponents 650 (was 642; +8 new tests, 0 regressions). No Unit/Integration tier impact (no
  service-layer changes). `check-a11y.ps1`/`check-design-tokens.ps1`/`check-doc-hygiene.ps1` all
  green. Tailwind build confirmed the reduced-motion rule compiles into the real
  minified CSS output, not just the source.
- **Tool:** Sonnet in Claude Code. **Pointers:** `audit/Accessibility.md` (Stage note, settled/open,
  claims statement); `layer3.5-structure.md` "Container Composite" (`Modal`'s full rationale);
  `layer4-style.md` "Accessibility as a Stage-5 criterion" + "Overlay recipe"; `testing.md`
  (class-presence carve-out for semantic attributes); `hidden-deferrals-tracker.md` **F6**
  (closed, split into the done half above) / **F6b** (WU-A11y-Keyboard, open) / **A9** (new,
  reduced-motion/sprite gap); `roadmap.md` §Resolved (decision row 12). **Deps:** none for this
  half; WU-A11y-Keyboard depends on nothing this WU didn't already provide (the `Modal` primitive
  it extends with a trap).

---

## WU-NotifEmail — notification email fan-out; new `Email/` cluster (Features 41/42/43, extends `Notifications/`, `Identity/`) — DONE ✓ (2026-07-31)

- **Cells:** none flipped. F41/F42/F43 L2 were already Stage 5 and stay 5 — this closed the inert
  plumbing beneath them (tracker **B1**), the same invisible-gap shape as B0/B7/B11.
- **Trigger:** a question about whether B1 could be built before an email provider was chosen. It
  could. The Phase-6 placement rested on the provider being a blocker; `Email:Provider` is a config
  switch over plain SMTP, every candidate provider exposes SMTP, addresses already existed on
  `User : IdentityUser<int>`, and Mailpit made the path locally verifiable. Pulled off the gate.
- **Doc-Touch moment 1 (before code):** the settled `audit/Notifications.md` note said "build the
  inline version first and measure" — **superseded**, Brian-ratified, because ~22 seeded types
  default `EmailEnabled = true` and several fan out to every follower, so inline sending is a
  known-shape problem rather than an open measurement. Unsubscribe mechanism settled as RFC 8058
  one-click. Both recorded in `roadmap.md` §Resolved and `layer2-services.md` §"Email fan-out". Also
  fixed a stale "folds in H5" claim carried by three docs (H5 closed at WU-TagFanon, 2026-07-26).
- **Done:**
  - New `Server/Email/` cross-cutting cluster — `IMailTransport`/`OutgoingMail`, `SmtpMailTransport`
    (MailKit body + `Email.Send` span + counters, extracted from `SmtpEmailSender`, plus a
    one-connection `SendBatchAsync`), `NoOpMailTransport`, `EmailOptions` (moved out of
    `Identity/`). `SmtpEmailSender` reduced to an adapter; Identity's behaviour unchanged.
  - `NotificationEnricher` extracted from `ServerNotificationReadService` — recipient-agnostic, so
    the ~40-arm `RelatedEntityId` switch is shared by the panel and email instead of forked.
  - `NotificationEmailBuffer`/`Flusher`/`Worker` (30s), enqueued from `CreateCoreAsync` — which
    inherits drop-self and dedup for free. Eligibility resolved at drain time.
  - `UnsubscribeTokenService` + `NotificationEmailEndpoints` (GET confirms, POST acts) +
    an "Unsubscribe" rate-limit policy. No schema, no migration.
  - `NotificationSettingUpsert` — sparse upsert/delete rule shared by `SetSettingAsync` and the
    token-authenticated unsubscribe.
  - `NotificationEmailBodies` composing over `NotificationPresenter.Compose`.
- **Two corrections the build made to the plan:** `IPublicUrlProvider`/`Site:PublicBaseUrl` already
  solved absolute URLs, so the planned `Email:SiteBaseUrl` key was never added (AppHost did need
  `Site__PublicBaseUrl` pinned to the http profile's port); and the plan's claim that this "goes
  against an interface" was half-right — `IEmailSender<User>` is Identity's fixed three-method
  contract and could not carry notification mail, which is why a general seam was extracted.
- **Verified:** `dotnet test` green — Unit 793 (+17), Integration 1039 (+18), RazorComponents 650.
  All three gate scripts clean. **Live SMTP pass** (server-only run with `Email__Provider=Smtp` at
  the Mailpit container): follow → notification row → mail in 12s, correct recipient/subject, both
  `List-Unsubscribe` headers, three absolute body links; unsubscribe GET left the settings table
  untouched, POST wrote the sparse override, and a fresh unread notification of the unsubscribed
  type sat through two drain cycles with no mail while the in-app notification remained.
  `psql`-confirmed at each step; verification rows removed afterwards.
- **Tool:** Claude Code (Opus; plan approved 2026-07-31). **Pointer:** `audit/Notifications.md`
  §"WU-NotifEmail Stage note"; `layer2-services.md` §"Email fan-out"; tracker **B1** ticked.
  **Left open:** tracker **F4** / decision row 8 — provider, sending domain, SPF/DKIM/DMARC.

---

## WU-ExploreFilterAxes — candidate-pane tag/ship/interaction axes; shared `StoryFilterPredicates` (Feature 33, extends `Discovery/`, `Stories/`, `Tags/`) — DONE ✓ (2026-07-31)

- **Cells:** none flipped. F33 L2/L3.5/L4.5 were already Stage 5 and stay 5 — this widened what
  they cover (tracker **A6**), the same already-sound-cell shape as A5/B0/B7/B12.
- **Trigger:** a question about what tracker A6 actually was. Answering it surfaced that the entry
  was not a scoped build task: `layer3.5-structure.md` carried a **settled** 2026-07-12 note
  stating flatly that neither manual tab is filtered by `TagFilter`/`UserStoryInteractionFilter`,
  which makes taking A6 up a *reopen* under CLAUDE.md's Stage-2 stop-and-flag rule. Brian
  adjudicated it a WU40 **scope cut, not a design objection**. Same lesson as A5 and the
  WU-DataSaver/B0 case: a one-line tracker entry is a pointer to an investigation.
- **Doc-Touch moment 1 (before any code):** rewrote the §"Filter-Axis Component Pattern"
  paragraph, added the disclosure to §"Explore tab", corrected that section's overstated
  "both paths use an Apply button" into the real per-control-kind split, and annotated WU40's
  L4.5 "Deferred" line as superseded.
- **Settled, three ways** (all now stated in `layer3.5-structure.md`, not just here):
  1. **Explore, user anchors only.** A story anchor has no story-valued section — `Author` and
     `Favoriters` are `UserCardDto`, and every recommendation-family row's story *is* the anchor.
     Filters there are inert at best and blank the pane whenever the anchor itself fails them. So
     `StoryNeighborsRequest` gained nothing and the disclosure swaps with the anchor exactly as
     the edge-toggle row does.
  2. **Deep Dive permanently unfiltered — a standing design rule, not a deferral.** Every
     walkable pair is bounded to ≤5 or ≤1; silently dropping links out of a chain the viewer was
     told is complete breaks the guarantee the whitelist exists to provide. Recorded in the
     tracker as a non-goal so a later pass doesn't "finish" it.
  3. **Filter state session-only.** Not written to `localStorage`, so the persisted-tree contract
     (IDs + edges only) is untouched; a durable version rides WU40's already-deferred "saved
     trees" decision.
- **Built — L2:** `Server/Stories/StoryFilterPredicates.cs` (new) — `ApplyFilters`,
  `ApplyShipTerm`, `ValidateShipShape`, `NamesTagIds` moved verbatim out of
  `ServerStoryReadService` (they were `private static`), preserving the WU-ApplyFiltersPurity
  invariant (pure, synchronous, no DbContext — tracker B12). Chosen over transcribing the
  predicate locally: tag roll-up and interaction-exclusion semantics are precisely what must not
  drift between two copies. `UserNeighborsRequest.Filter` (nullable `StoryFilterDto`) rides the
  existing POST endpoint — no client change. `ServerManualTreeSearchReadService` composes the
  predicate onto the **one shared `visible` queryable**, which every story-valued section already
  flows through (family via `visible.Any(...)`, Favorites/Authored/Vouched via joins) — one
  substitution covers all four, and count and page keep sharing a predicate so `TotalCount` stays
  honest under a filter. `TextQuery`/`Sort` ignored (`hasFts: false`). The user route is now
  wrapped in `EndpointHelpers.ExecuteAsync`: `ValidateShipShape` throws the user-facing
  `StoryValidationException`, which unwrapped is a 500 for what should be a 400 — the identical
  defect WU-ErrorHandling2 fixed on the three story-listing reads.
- **Built — L3.5:** `ExploreTab` composes `TagFilter` (`AllowSavedSelections=false`, the
  documented narrow-context opt-out) + `ShipFilter` + `UserStoryInteractionFilter` as individual
  axes in a collapsed `<details>`, **not** `ResultsFilterPanel` (which also assembles FTS and sort).
  Buffered per axis, applied as one DTO, resetting per-section paging. §8.7 seed via
  `IDiscoveryDefaultsReadService` under `SiteSearchModes.TreeSearch`. The collapsed summary shows
  an active-axis count — the seeded default hides rows out of the box, and a filter that silently
  removes results must be legible without opening the panel.
- **Found in passing:** `SiteSearchModes.TreeSearch` has been seeded in the §8.7 matrix (with
  `Ignored` enabled) since the matrix was created, with **no UI surface consuming it** — the
  Automatic tab uses `AutoTreeSearch`. This WU is its first consumer.
- **Verified:** `dotnet build` green. `dotnet test` green — **2,502** (Unit 793, RazorComponents
  655 (+5), Integration 1,054 (+5)). Browser pass (server-only, DataSeeder DB) against psql ground
  truth — AuthorAlpha's 4 published stories, TestUser's ignored story 7, story 1 as the only
  Cynthia-tagged one: **Authored (3)** with "(1 active)" on load (§8.7 default hiding story 7,
  honest count, checkbox matching the query behind it); uncheck + Apply → **(4)**; one Cynthia chip
  + Apply → **Authored (1)** *and* Recommendations 1→0, Vouched 1→0 in the same pivot (the single
  substitution point, confirmed on real data); Clear → all four restored; story anchor → disclosure
  gone, toggle row swapped; full reload re-seeds the default (filters are session-only). No console
  errors. `check-design-tokens.ps1` + `check-doc-hygiene.ps1` clean.
- **Pointer:** `audit/Discovery.md` F33 "WU-ExploreFilterAxes Stage note".

---

## WU-UserModeration — the user-moderation seam: reporting a user, acting on a content author, per-user history (Features 46/47, extends `Moderation/`, `Profiles/`, `Users/`, `Discovery/`) — DONE ✓ (2026-08-01)

- **Cells:** none flipped. F46/F47 were already L1–L3.5=5, L4=3, L5=5, L6=5 and stay there — this
  closed gaps *beneath* sound cells, the same shape as B0/B4/B12. What changed is that the feature
  became reachable at all.
- **Trigger:** a question about tracker item **B13** (`ModUsersPage`'s `{UserId:int?}` route
  parameter declared and never read, filed `polish · low`). Investigating it found the parameter was
  the least of it — the fourth repetition of this tracker's own lesson (A5, B0, B3/B4): *a one-line
  entry is a pointer to an investigation, not a scoped work item.*
- **What the investigation actually found, in order of severity:**
  1. **Nothing in the app could report a User.** Every `ReportDialog.OpenAsync` call site passed
     `Story` (5 sites) or `Comment` (2) — no others existed in the repo. `ModUsersPage` filters the
     queue to `EntityType == User`, so it rendered "No reported users." permanently, for every
     moderator, forever. `UserCard` had carried a `HasDelegate`-gated `OnReport` since WU34, wired
     by no consumer; its own comment said Report "stays dark until those features land."
  2. **`/mod/reports`' "Warn user" threw every time.** It passed a content report's id into
     `ApplyAccountActionAsync`, which WU34 settled as User-target-only. Since Story and Comment were
     the only reports the app could produce, the button always failed — and as
     `InvalidOperationException` isn't in `ExceptionPresenter.IsUserFacing`, the moderator got
     "Something went wrong on our end" while the server logged at Error.
  3. **So the whole account-action capability was unreachable** — built WU34, sign-in-blocked WU38a,
     banner-surfaced WU-AccountEnforcement, integration-tested throughout, and usable from no in-app
     path. **Nobody could be warned, suspended, or banned.**
  4. `ApplyAccountActionAsync` never decremented `ActiveReportCount` (its two sibling resolve paths
     do), leaking the counter the mod-triage sort orders on; and `UserMenu` linked only
     `/mod/reports`, leaving the other four `/mod/*` pages URL-typed-only.
- **Decisions (Brian, 2026-07-31):** cover the full seam in one WU; moderators **may** act on users
  who were never reported, as a **mod-filed report** rather than a new audit table; `/mod/reports`
  carries the full Warn/Suspend/Ban set acting on the reported content's **author**, which makes
  `/mod/users` a lookup-and-history view. That last one is what closes B13 — the route parameter
  goes **live**, it isn't deleted.
- **Doc-Touch moment 1 (before any code):** new `layer2-services.md` §"Account actions — target
  resolution and the report-as-audit-record rule"; new WU-UserModeration settled-constraints block
  in `audit/Moderation.md` Feature 47, superseding two WU34 rules.
- **Built — L2:** `ResolveActionTargetUserIdAsync` (User → that user; Story/Comment/BlogPost/
  Recommendation → the content's `AuthorUserId` via the existing `LoadModeratableAsync`; Message →
  its sender), throwing the new user-facing `ModerationValidationException` when no account resolves;
  the missing `AdjustActiveReportCountAsync(..., -1)`; the status/stamp/notification tail extracted
  to `ApplyStatusAndNotifyAsync` and shared with the new `ApplyAccountActionToUserAsync`, which opens
  *and* resolves its own `Report` in one unit of work (`ReporterUserId == ModeratorUserId` is the
  moderator-initiated marker; no `ActiveReportCount` churn since the +1/−1 would cancel). New read
  `GetUserModerationHistoryAsync` + `UserModerationHistoryDto`; two endpoints; two client impls.
  **No migration and no new seeded `ReportReason`** — the moderator picks from the six existing
  `HasData` rows, which is more useful in an audit trail than a synthetic "Moderator-initiated".
- **Built — L3.5:** `AccountActionPanel.razor` extracted from `ModUsersPage`. The extraction is
  load-bearing, not tidiness: the panel owns the 2026-07-30 `DateTime.SpecifyKind` Npgsql fix, so
  giving `/mod/reports` a Suspend control would otherwise have meant re-deriving that fix in a second
  place. `ModUsersPage` rewritten around the now-live route parameter (lookup mode = `UserPicker`
  over the triage list; detail mode = standing + actions + history), following `MessagesPage`'s
  `_initialized`/`_loadedUserId` guard pattern and its persisted-list/ephemeral-detail split.
  `/mod/reports` gained Warn/Suspend/Ban. Report-a-user lit up: `ProfilePage`'s single `ReportDialog`
  moved out of the Stories tab to page level (a `@ref` inside a tab branch is null on the others) and
  a banner control opens it with `ReportedEntityType.User`; `UserCard.OnReport` wired at `VouchList`
  and both tree-search tabs via dispatcher-pattern pass-throughs. `UserMenu` now lists all five
  `/mod/*` pages. `DataSeeder` gained a User-targeted report.
- **Verified:** `dotnet test` green — Unit 793, RazorComponents 662 (+12), Integration 1063 (+8),
  **2,518 total**. New Integration coverage in `ModerationServiceTests` (author resolution on Story
  and on `UserProfileComment` reports; unresolvable author → `ModerationValidationException`; counter
  decrement; mod-filed report shape; non-mod and self-action rejection; history scope and null).
  New `AccountActionPanelTests` carries the retargeted UTC-Kind regression guard plus three
  validation cases; `ModUsersPageTests` rewritten for both route modes (the `UserPicker` *pick*
  stays uncovered at this tier — the documented `StoryTitlePickerTests`/`ModSpotlightPageTests`
  limitation). Gates green: design-tokens, doc-hygiene, a11y.
- **Browser-verified end to end** (server-only, AdminUser, `psql` after every step): reported
  ReaderGamma from their profile → row `type=User`, count 1 → visible on `/mod/reports` *and*
  `/mod/users` → `/mod/users/6` rendered standing + history; claimed the **Story** report and
  Suspended → **AuthorBeta, the story's author, went Suspended with the entered clock value
  persisted unshifted** (the path that threw before), story counter 1 → 0; then `/mod/users` with no
  id → typeahead-found LurkerDelta (zero reports) → Ban → `account_status=3`, mod-filed report with
  `reporter_user_id == moderator_user_id`, counter untouched. Both notifications fired. Finally
  `/dev/wu12/login-as/AuthorBeta` left the session signed out — `CanalaveSignInManager` blocking a
  suspended account, i.e. the WU38a mechanism exercised from the UI for the first time. Zero
  `fail:`/`crit:` lines in the server log.
- **Tracker:** closes **B13**; opens **B17** (BlogPost/Recommendation/PrivateMessage reports still
  have no entry point — the same gap, three more surfaces), **B18** (a user's history omits reports
  against content they authored; the page states the caveat on screen), **B19** (no role
  grant/revoke capability exists anywhere — moderators can only be minted by editing the seeder).
- **Dev-DB note:** the browser pass mutated the persistent workbench — AuthorBeta is Suspended,
  LurkerDelta Banned, two new reports exist. `scripts\reset-dev-db.ps1` restores it (and the seeder
  now creates a User-targeted report, so `/mod/users` is non-empty on a fresh seed).
