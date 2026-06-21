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
  (`StoryCard`/`StoryDeck`, `StoryInteractionPanel`, `ChapterNavigation`, `CommentSection`, …).
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
"do not revisit" → build → `dotnet build` + run the slice (+ visual check if L4) → update `status.md`
(cell → 5) and this file (unit ✓). Conventions skill auto-loads as guardrail.

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
  UserStoryInteraction-domain concept, resolved there via the generic `GetSpriteUrl`, not minted on the
  sprite service). Contract feeds TagChip, StoryCard — and UserStoryInteractionButton **indirectly**, via
  the panel resolving a URL through `GetSpriteUrl` and passing it down as `IconIdentifier`, not via
  direct injection. Also hardened `canalave-conventions/SKILL.md` "Code Organization" (vertical clusters
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

### WU4 — `TagChip` leaf
- **Cells:** 13 L3/L3.5/L4.
- **Do:** extract leaf (Tag DTO param, no service injection); sprite via Sprites L2; type-based coloring;
  tooltip from `Description`. Mints the chip contract consumed by StoryCard, story detail, TagSelector
  dropdown, Tag Directory.
- **Tool:** opusplan. **Pointer:** `audit/Tags.md` Feature 13. **Deps:** WU2, WU3.

### WU5 — `RichTextView` leaf
- **Cells:** 7 L3.5/L4 (RichTextView slice).
- **Do:** pure display of sanitized chapter HTML; **user `ReaderSettings` font**, not the Tailwind chrome
  font (`layer4-style.md` §"Reader Settings as CSS"). Consumed by chapter reading + previews.
- **Tool:** opusplan. **Pointer:** `audit/Chapters.md`. **Deps:** WU0.

### WU6 — `EditorView` composite *(third-party Quill wrapper)*
- **Cells:** 6 L3.5 (EditorView slice).
- **Do:** Blazored TextEditor / Quill adapter + server-side `HtmlSanitizer` allow-list integration on save.
  Universal — consumed by Chapters, BlogPosts, Recommendations, Messaging, Comments.
- **Tool:** opusplan. **Pointer:** `audit/Chapters.md`. **Deps:** WU0 (+ add Blazored.TextEditor NuGet).

### WU7 — `UserStoryInteractionButton` leaf
- **Cells:** 16 L3/L3.5/L4 (button slice).
- **Do:** EventCallback-driven (absence of `OnToggle` ⇒ read-only, rendered only when active); icon via
  Sprites `GetInteractionIcon()`; no service injection (debounce lives in the panel, WU16). Two contexts
  (listing vs detail).
- **Tool:** opusplan. **Pointer:** `audit/UserStoryInteractions.md` Feature 16. **Deps:** WU2.

### WU8 — `PaginationControls`
- **Cells:** 31 L3.5/L4 (pagination slice).
- **Do:** generic paged-listing controls (offset mode). Consumed by any StoryDeck-backed listing.
- **Tool:** opusplan. **Pointer:** `audit/Discovery.md`. **Deps:** WU0.

### WU9 — `ConfirmDialog` *(universal container, §5.30.9)*
- **Cells:** 26 L3.5 (dialog slice).
- **Do:** `@ChildContent` confirm/cancel container for spoiler reveal + destructive actions site-wide.
- **Tool:** opusplan. **Pointer:** `audit/Comments.md`. **Deps:** WU0.

### WU10 — `UserCard` leaf
- **Cells:** 18 L3.5/L4 (UserCard slice).
- **Do:** display-only user summary (avatar/name/tagline) from a user-summary DTO; mint that DTO here.
  Consumed by vouch display + member lists.
- **Tool:** opusplan. **Pointer:** `audit/Following.md` + `audit/Profiles.md`. **Deps:** WU2 (sprite/avatar).

### WU11 — `TagSelector` composite rebuild *(Stage-4 trap → spec §5.30.4)*
- **Cells:** 14 L3/L3.5/L4.
- **Do:** discard the datalist/list-mutation/inline-badge component; rebuild around **Blazored.Typeahead**
  (300ms debounce) + `TagChip` selected chips + lightweight dropdown rows (dot+sprite+name); raise
  `EventCallback<IReadOnlyList<Tag>> OnSelectionChanged` (no list mutation); fix the `mb-4` outer-margin
  violation.
- **Tool:** opusplan. **Pointer:** `audit/Tags.md` Feature 14 + cluster note. **Deps:** WU3, WU4
  (+ add Blazored.Typeahead NuGet).

---

## Phase 2 — Integration Points

### WU12 — Stories L2 (listing + write completion)
- **Cells:** 5 L2 (Stage 2 — add `StoryListingDto` + listing/browse projection + content-rating master
  filter "mature off ⇒ no trace"), 4 L2 (slug generation in write path; cover-art upload to R2/MinIO —
  *flag as open infra; may stub for MVP*).
- **Tool:** opusplan. **Pointer:** `audit/Stories.md` Features 4, 5. **Deps:** WU1.

### WU13 — `StoryCard` leaf
- **Cells:** 5 L3/L3.5/L4 (card slice).
- **Do:** warm-partition projection display; composes TagChip + UserStoryInteractionButton (listing
  context). Mints the StoryCard contract → flips StoryDeck + every listing consumer toward Stage 3.
- **Tool:** opusplan. **Pointer:** `audit/Stories.md` Feature 5 + audit-summary §5. **Deps:** WU4, WU7, WU12.

### WU14 — `StoryDeck` composite *(pass-through)*
- **Cells:** 5 L3.5/L4 (deck slice).
- **Do:** arrange StoryCards + PaginationControls + three-state (loading/empty/populated). Consumed by
  Search, Bookshelves, Profiles, Groups, Also-Favorited.
- **Tool:** opusplan. **Pointer:** `audit/Stories.md` Feature 5. **Deps:** WU8, WU13.

### WU15 — UserStoryInteractions L2 (writes + bookshelf reads)
- **Cells:** 16 L2 (direct-EF write path; reject impossible combos; zero auto-cascade between bits),
  17 L2 (derived-tab list reads: Actively Reading / Abandoned, etc.).
- **Tool:** opusplan. **Pointer:** `audit/UserStoryInteractions.md` Features 16, 17. **Deps:** WU12.

### WU16 — `StoryInteractionPanel` composite
- **Cells:** 16 L3/L3.5/L4, 17 L3.5 (panel slice).
- **Do:** coordination composite owning the 2-second debounce across UserStoryInteractionButtons; detail
  context (all clickable). Consumed by story detail + listings.
- **Tool:** opusplan. **Pointer:** `audit/UserStoryInteractions.md`. **Deps:** WU7, WU15.

### WU17 — Chapters L2 (writing/versioning + reading)
- **Cells:** 6 L2 (versioning, `PrimaryContentId`, sanitize, word count on stripped text), 7 L2 (read
  + next/prev + TOC).
- **Tool:** opusplan. **Pointer:** `audit/Chapters.md` Features 6, 7. **Deps:** WU12.

### WU18 — `ChapterNavigation` composite
- **Cells:** 7 L3.5/L4 (nav slice).
- **Do:** top+bottom coordination composite (prev/next, TOC). **Tool:** opusplan.
  **Pointer:** `audit/Chapters.md` Feature 7. **Deps:** WU17.

### WU19 — Comments L2 (posting / display / likes / spoiler)
- **Cells:** 23 L2, 24 L2, 25 L2, 26 L2 (chapter context first; other TPT contexts follow in their
  feature units). Comment Likes L1 (`CommentLike` junction) already done in Phase A.
- **Tool:** opusplan. **Pointer:** `audit/Comments.md`. **Deps:** WU17.

### WU20 — `CommentItem` leaf + `CommentSection` composite
- **Cells:** 23/24/25/26 L3/L3.5/L4.
- **Do:** threaded display (golden-index pagination), like toggle, spoiler flow via ConfirmDialog
  (completion-gated reveal — `ChapterPage` passes `UserHasCompletedStory`), compose EditorView.
- **Tool:** opusplan. **Pointer:** `audit/Comments.md`. **Deps:** WU6, WU9, WU19.

### WU21 — Following + Vouches
- **Cells:** 18 L2/L3/L3.5/L4, 19 L2/L3/L3.5/L4 (Vouch L1 done; 5-per-user limit in service).
- **Do:** follow/unfollow (+ReceiveAlerts bell), vouch create with optional `VouchText`; display via
  UserCard (outgoing public / incoming private).
- **Tool:** opusplan. **Pointer:** `audit/Following.md` Features 18, 19. **Deps:** WU10.

### WU22 — Notifications L2 (generation + service)
- **Cells:** 41 L2 (generation hooks), 42 L2, 43 L2.
- **Do:** notification creation on high-effort events (NOT likes), sparse-override settings read/write.
  Generation hooks are *called from* other features' write paths — implement the service now; wire calls as
  those features land. **Tool:** opusplan. **Pointer:** `audit/Notifications.md`. **Deps:** WU1.

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
  shared create/edit routes. **Tool:** opusplan (Stage-4 build-to-spec; Sonnet for parts now Stage-3).
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
