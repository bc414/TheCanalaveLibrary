# Audit — UserStoryInteractions/

**Features:** 16 (state writes), 17 (lists & bookshelves).

## Shared Context

**Entities (Core/Models/):** `UserStoryInteraction` (the bit-packed hot table, PK `(UserId,StoryId)`),
`UserStoryInteractionDate` (warm 1-to-1 partition), `UserStoryRecommendationSource` (sparse 1-to-1).
**Fluent config:** composite keys, partition 1-to-1 cascades, and **seven filtered/covering indexes** on
`UserId INCLUDE (StoryId)` filtered by each boolean (NAMED HasIndex calls since WU-L6 2026-07-07 —
unnamed, they had silently collapsed to one index in the database; see the F17 L6 note).

This is the cluster the audit flagged as the most significant case of **code staleness** (a stale-code
trap, not an intent contest — see audit-summary §0).

---

## The reading-status divergence (resolved in WU0 / InitialSchema — L1 Stage 5)

**Historical note (do not apply to current code — already resolved).** When the audit ran, the
pre-revision `UserStoryInteraction` had these booleans: `IsInProgress, IsCompleted, IsActivelyReading,
IsFavorite, IsHiddenFavorite, IsFollowed, IsReadItLater, IsIgnored`. The resolved model (per spec §4/§5.12
and `layer1-data-model.md`) instead has:
- **`HasStarted`** (`Has-` prefix, permanent past event, set at 90% scroll of Ch.1) — permanent,
  never cleared.
- `IsCompleted`, `IsIgnored`, `IsFavorite`, `IsHiddenFavorite`, `IsFollowed`, `IsReadItLater` (`Is-`
  mutable) — 6 mutable bits.
- "Actively Reading" / "In Progress" is **derived** (`HasStarted AND NOT IsCompleted AND NOT IsIgnored`),
  not a stored column.
- Zero-coupling: no bit drives another; the service rejects impossible combos but never auto-cascades.

**Resolution completed (WU0/InitialSchema, 2026-06-20):** `IsInProgress`/`IsActivelyReading` dropped;
`HasStarted` added; vestigial `ReadStatus`/`FavoriteStatus` enums retired. Seven filtered/covering indexes
in `UserStoryInteractionConfigurations.cs` target the revised columns. L1 is Stage 5. L6 still needs index
regeneration (see L6 below). This section is kept as a historical reference — do not use it as a
description of current code.

---

## WU23 Nomenclature Sweep (2026-06-23)

All identifiers meaning *user×story interaction* are renamed to spell out `UserStoryInteraction…`.
The rule and the deliberate-leave list are now in `canalave-conventions/SKILL.md` "UserStoryInteraction
prefix rule". Summary of changes applied in WU23 Phase 0:

**Tier 1 — Types/enums (C#-only; stored as `short` where applicable):**
- `InteractionTypeEnum` → `UserStoryInteractionTypeEnum` (file: `…/UserStoryInteractionTypeEnum.cs`)
- `InteractionDisplayContext` → `UserStoryInteractionDisplayContext`
- `InteractionStateUpdate` → `UserStoryInteractionStateUpdate`
- `InteractionVisuals` → `UserStoryInteractionVisuals` (SharedUI)
- `InteractionConstants` → `UserStoryInteractionConstants`

**Tier 2 — Member renames (Tier 3 in the plan; no column change except nav prop):**
- `InteractionConstants.InteractionDebounceMs` → `UserStoryInteractionConstants.UserStoryInteractionDebounceMs`
- `IUserStoryInteractionWriteService.SetInteractionStateAsync` → `SetUserStoryInteractionStateAsync`
- `UserStoryInteraction.InteractionDate` nav prop → `InteractionDatePartition` (no column; avoids shadowing the type `UserStoryInteractionDate`)
- `StoryCard.[Parameter] InteractionState` → `UserStoryInteractionState`
- `StoryDeck.[Parameter] InteractionStates` → `UserStoryInteractionStates`
- Test helpers: `SeedInteractionRowAsync` → `SeedUserStoryInteractionRowAsync`; `CallSetStateAsync` → `CallSetUserStoryInteractionStateAsync`

**Deliberately NOT renamed:**
- `UserChapterInteraction` / `LastInteractionDate` — chapter-reading domain, already fully qualified.
- Prose in comments / seed-data description strings.

The §8.7 entity/column renames (`UserInteractionFilter→UserStoryInteractionFilterType` etc.) and the
`AllowInteractions→SocialInteractionPermission` enum are recorded in `audit/Discovery.md` (they belong
to the Discovery cluster) and `audit/Identity.md` (for `AllowInteractions` on User) respectively.

## Feature 16 — Story Interaction State Writes
- **L1 — Stage 5 (re-model resolved in WU0 / InitialSchema, 2026-06-20).** See "The reading-status
  divergence" section above. `UserStoryInteractionDate` warm partition and sparse semantics ("no row =
  all false; date row only when relevant") survived intact.
- **L2 — Stage 5 (WU15, 2026-06-22).** Read/write service implemented and tested.

  **Settled for WU15 (2026-06-22, do not revisit):**
  - WU15 is **trimmed to the panel-critical slice** — Feature 16 L2 only (write path + per-viewer state
    reads). Feature 17 L2 (bookshelf tab reads) is deferred to WU27 (`status.md` 17 L2 stays Stage 2).
  - **`InteractionTypeEnum` uses imperative-verb identifiers**: `Favorite, PrivateFavorite, Follow,
    Complete, ReadLater, Ignore`. This resolves the code-identifier noun/verb mix introduced by the
    entity columns (`IsFavorite`/`IsHiddenFavorite`/…) and the server `UserStoryInteractionFilters`
    constants. **Scope: the new enum only** — the migrated `Is*` entity columns and the Server filter
    constants are **left as-is** (no migration here); their mix and the `Favorited`/`IsFavorite`
    mismatch are noted for a later L1 reconcile. Display labels (Favorite / Private Favorite / Following
    / Completed / Read It Later / Ignored) are user-facing strings and may stay mixed.
  - **Panel manages 6 bits via `InteractionStateUpdate`**: `IsFavorite, IsHiddenFavorite, IsFollowed,
    IsCompleted, IsReadItLater, IsIgnored`. `HasStarted` is intentionally absent (reading path owns it,
    set at 90% scroll Ch.1, WU26) — the write preserves it. `IsCompleted` is included so users can mark
    stories read on other sites, toggle it off, and see completion in story decks.
  - **Zero-coupling**: each bit is set/cleared independently; the service rejects impossible combos per
    the §4 table but never auto-cascades between bits.
  - **Sparse semantics**: no row = all false; create row on first true bit; delete row when all bits
    go false (date partition cascades). Date partition stamped when bit goes true, nulled when false.
  - **Write guard**: anonymous caller ⇒ throw (real gating is `AuthorizeView` at UI level).
  - `GetStatesByStoryIdsAsync(IReadOnlyList<int>)` is the N+1-safe batch method (one query, missing
    rows ⇒ absent key ⇒ caller treats as all-false). `IReadOnlyList<T>` per the WU12 id-batch rule.

  **How verified (WU15, 2026-06-22):** `dotnet build` green (8 projects, 0 errors, 2 pre-existing
  warnings). `dotnet test` green: 93 Integration / 79 Unit / 64 RazorComponents = 236 total.
  Integration tier (`UserStoryInteractionServiceTests`, 15 tests, Testcontainers Postgres):
  upsert creates row when absent; updates existing row; FavoriteDate stamped on true / cleared on false;
  CompletedDate stamped (panel-writable "read elsewhere" use case); HasStarted preserved across write;
  all-bits-false → row removed + date partition cascade-deleted; all-false with HasStarted=true → row
  survives; all-false + no row → no row created; `GetStatesByStoryIdsAsync` scoped to active user;
  absent key in result treated as all-false; anonymous context → empty reads; anonymous write throws.
  Files (post-WU23 rename): `Core/UserStoryInteractions/UserStoryInteractionTypeEnum.cs`,
  `Core/UserStoryInteractions/UserStoryInteractionConstants.cs`,
  `Core/UserStoryInteractions/UserStoryInteractionStateDto.cs`,
  `Core/UserStoryInteractions/UserStoryInteractionStateUpdate.cs`,
  `Core/UserStoryInteractions/IUserStoryInteractionReadService.cs`,
  `Core/UserStoryInteractions/IUserStoryInteractionWriteService.cs`,
  `Server/UserStoryInteractions/ServerUserStoryInteractionReadService.cs`,
  `Server/UserStoryInteractions/ServerUserStoryInteractionWriteService.cs`,
  DI in `Server/Program.cs`.

- **L3-Logic — Stage 5 (panel slice, WU16, 2026-06-22).** `UserStoryInteractionButton` leaf
  (WU7, Stage 5) + `UserStoryInteractionPanel` coordination composite (WU16). Panel owns the 2-second
  debounce via `CancellationTokenSource` + `Task.Delay`; applies optimistic local state update before
  the debounce fires; calls `SetUserStoryInteractionStateAsync` on flush.
  `UserStoryInteractionConstants.UserStoryInteractionDebounceMs = 2000` in
  `Core/UserStoryInteractions/UserStoryInteractionConstants.cs` (not Server's `SiteConstants` —
  SharedUI cannot reference Server). Panel injects only `IUserStoryInteractionWriteService` (no read
  service; N+1 rule). State flows in as a `[Parameter]` from the batch-loading parent.

  **How verified (WU16, 2026-06-22):** `dotnet test` green, 275 total. Unit:
  `InteractionVisualsTests` (26 tests — 6 non-empty IconPath + 6 AccentColor + 6 Label; locked 6
  AccentColors; PrivateFavorite reuses Favorite's IconPath; 6 distinct colors). RazorComponents:
  `UserStoryInteractionPanelTests` (13 tests — detail renders all 6 in locked enum order; listing
  blank-slate shows ReadLater+Ignore; listing IsFavorite=true hides ReadLater+Ignore; listing
  Favorite active renders as `<span>`; listing IsReadLater=true Ignore still shown (ReadLater
  doesn't break blank-slate); listing IsCompleted=true hides ReadLater+Ignore + shows Complete span;
  IsOwnStory renders Edit link + no buttons; optimistic toggle adds/removes aria-pressed before debounce).

- **L3.5-Structure — Stage 5 (panel slice, WU16, 2026-06-22).** `UserStoryInteractionPanel` iterates
  `Enum.GetValues<UserStoryInteractionTypeEnum>()` (declaration order = locked button order). Parameters:
  `StoryId` (EditorRequired int), `State` (`UserStoryInteractionStateDto?`; null = all-false),
  `Context` (`UserStoryInteractionDisplayContext`: `Listing|Detail`), `IsOwnStory` (bool).
  `UserStoryInteractionDisplayContext` is a Core enum in `Core/UserStoryInteractions/`. Blank-slate
  condition for listing ReadLater/Ignore visibility: NOT (IsFavorite OR IsHiddenFavorite OR IsFollowed
  OR IsCompleted OR ActivelyReading) — the IsReadItLater/IsIgnored bits intentionally do not break
  blank-slate.

- **L4-Style — Stage 5 (panel icon/color/label mapping, WU16, 2026-06-22).** `UserStoryInteractionVisuals`
  static class in `SharedUI/UserStoryInteractions/` (renamed from `InteractionVisuals` in WU23 Phase 0)
  transcribes the locked audit table verbatim. Inner `Info` record carries `(IconPath, AccentColor, Label)`.
  `PrivateFavorite` reuses `Favorite`'s `HeartPath` constant; color signals privacy. All 6 AccentColors
  match the palette exactly.
  **Button-leaf contract (locked WU7, do-not-revisit):** `UserStoryInteractionButton`
  takes `IsActive` / `OnToggle` / `IconPath` / `AccentColor` / `Label`. Read-only (no `OnToggle`)
  renders as `<span>` and only when `IsActive`.
  **How verified (WU7, 2026-06-21):** `dotnet build` green (4 projects, zero new warnings); live
  server run, homepage `200`; user-confirmed visual check of all three states (gray inactive, hover
  accent-fill, inverted accent-bg/white-shape active) plus the read-only-renders-only-when-active
  rule, via a throwaway harness on `HomeDesktop.razor` (heart + star sample shapes, removed after
  confirmation). No real consumer exists yet (`UserStoryInteractionPanel` is WU16).

  **`UserStoryInteractionTypeEnum → (IconPath, AccentColor, Label)` mapping — locked 2026-06-22,
  consumed by WU16.** Keys below use the **imperative-verb identifiers** (`PrivateFavorite` / `Follow` / `Complete`
  / `ReadLater` / `Ignore`) settled for the enum. All paths use the default SVG `nonzero` fill rule (no
  `fill-rule` attribute change needed). Compound paths use winding direction deliberately: CW subpaths
  fill positively; a CCW subpath inside a CW outer path cancels winding to 0 (transparent = cutout).
  Visually verified via `wwwroot/icon-preview.html` (throwaway — remove before Stage 6). Color palette
  is Gen 4/5 Pokémon-grounded: warm tones for personal attachment (Fairy Pink, Mismagius Magenta, Arceus
  Gold), cool for lifecycle/relationship actions (Azurite Blue, Manaphy Teal), rust for dismissal (Cinnabar
  Rust). Green is now reserved for curation-tab icons (My Stories / Recommendations / Hidden Gems) in
  `BookshelfTabVisuals` — Follow was reskinned to Manaphy Teal in WU27 to free the green family for that.

  | `InteractionTypeEnum` | Label | `AccentColor` | Icon concept | `IconPath` (`d=""`) |
  |---|---|---|---|---|
  | `Favorite` | Favorite | `#E8507A` Fairy Pink | Filled heart | `M12 21.35l-1.45-1.32C5.4 15.36 2 12.28 2 8.5 2 5.42 4.42 3 7.5 3c1.74 0 3.41.81 4.5 2.09C13.09 3.81 14.76 3 16.5 3 19.58 3 22 5.42 22 8.5c0 3.78-3.4 6.86-8.55 11.54L12 21.35z` |
  | `PrivateFavorite` | Private Favorite | `#C040A8` Mismagius Magenta | Filled heart — same shape, color alone signals privacy | same `d` as Favorite |
  | `Follow` | Following | `#2DBBA0` Manaphy Teal *(reskinned WU27 — was `#4A9B52` Eterna Green; green now reserved for curation tabs: My Stories / Recommendations / Hidden Gems)* | Award ribbon: circle badge + two-tailed fork; Gen 4 ribbon = earned commitment | `M6 8A6 6 0 0 1 18 8A6 6 0 0 1 6 8Z M9 14L6 22L9.5 20L12 21.5L14.5 20L18 22L15 14Z` |
  | `Complete` | Completed | `#E8B84B` Arceus Gold | Filled circle; CCW checkmark polygon inside cancels winding → transparent cutout | `M12 2A10 10 0 0 1 22 12A10 10 0 0 1 12 22A10 10 0 0 1 2 12A10 10 0 0 1 12 2Z M6 12.5L5 14L10 19L20 7L18.5 5.5L10 16Z` |
  | `ReadLater` | Read It Later | `#2E6FBF` Azurite Blue | Open Pokéball (bottom D + raised lid = 2 px gap, center clasp dot) with 3 page-lines entering from top — capturing a story from the discovery stream | `M5 16A7 7 0 0 1 19 16Z M5 14A7 7 0 0 0 19 14Z M10.5 15A1.5 1.5 0 0 1 13.5 15A1.5 1.5 0 0 1 10.5 15Z M9 1L15 1L15 2L9 2Z M9 3L15 3L15 4L9 4Z M9 5L15 5L15 6L9 6Z` |
  | `Ignore` | Ignored | `#C04030` Cinnabar Rust | Ban circle: CW outer disk + CCW inner circle (ring hole) + CW diagonal bar | `M12 2A10 10 0 0 1 22 12A10 10 0 0 1 12 22A10 10 0 0 1 2 12A10 10 0 0 1 12 2Z M5 12A7 7 0 0 0 19 12A7 7 0 0 0 5 12Z M5.5 7.5L7.5 5.5L18.5 16.5L16.5 18.5Z` |

- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; the favorite toggle verified in a real WASM runtime during
  the flip's browser wave (psql ground truth for the written row). Full wave narrative + the 7 bugs
  found/fixed: `workplan.md` WU-GlobalFlip.
- **L6 — Stage 5 (WU-L6, 2026-07-07 — CORRECTS the 2026-06-22 WU15 note and the 2026-07-06 R4
  audit outcome).** Those earlier verifications audited the *configuration file*, not the
  database: the seven `HasIndex(e => e.UserId)` calls were UNNAMED, and EF collapses unnamed
  HasIndex calls on the same property set into ONE index (each call overwrites the previous
  filter/name) — the database contained only `ix_user_story_interactions_has_started` (the last
  declared) from WU0/InitialSchema until WU-L6. Six bookshelf tabs ran unindexed the whole time
  (invisible at dev-seed volume). Fixed in `L6_IndexBatch` via the named `HasIndex(props, "name")`
  overload; verified against `pg_indexes` (all seven present) and the perf fixture (favorites tab
  p50 0.19→0.13 ms at 38k rows; discovery-exclusion probe −68% riding the restored `ignored`
  partial). Convention + gotcha: `layer6-indexes.md` §"Multiple indexes on the same columns".
  Tiers: Integration (the suite migrates through `L6_IndexBatch` on Testcontainers Postgres);
  measurement via `TheCanalaveLibrary.PerfBaseline` (committed `results/`).
- **L7 — removed with the layer (WU-SignalBuffering, 2026-07-06). F16 stays durable-direct
  permanently.** The planned write-behind queue's stated rationale ("batch writes to protect the
  read-hot table from locks") was designed under SQL Server hours before the Postgres switch and is
  void under MVCC (writers never block readers). Interactions are **durable user intent** — low-
  write, already churn-absorbed by the 2 s client debounce — so a lossy buffer is the wrong pattern
  by the signal-buffering criterion (`layer2-services.md` §"Signal Buffering": high-frequency AND
  loss-tolerant AND coalescable). MVCC churn cost (each flag toggle changes a partial-index
  predicate → never HOT) is managed by `R4_MvccStorageTuning` autovacuum tuning instead. Any future
  volume relief must be durability-preserving and measurement-gated — never a lossy queue.
  Browser-verified 2026-07-06: favorite toggle durably persisted (psql: `is_favorite=t` +
  `favorite_date` stamped), state survives a hard reload from a fresh DB read.

- **R3 divergence note (2026-07-06): no DB CHECK constraints for flag pairs — deliberately.**
  The requirements draft assumed mutually-exclusive pairs (e.g. `IsFavorite` vs
  `IsHiddenFavorite`) enforceable by CHECK. Ground truth: spec §4's zero-coupling model declares
  **all combinations valid** (all 8 reading-status states including `0,1,x` "read elsewhere";
  favorite+hidden both-true = "favorite hidden from visitors", handled by the profile queries'
  `IsFavorite && (includePrivate || !IsHiddenFavorite)` shape), and the write service's
  `ValidateCombination` is a deliberately empty extension point. There is nothing DB-constrainable.
  Resolved with Brian 2026-07-06: no CHECKs; the entity/interface comments that overstated
  ("service rejects impossible combinations") were corrected to match §4.

**Future Design Work, post MVP:** Pokeball needs to be side view
Hidden favorite should have a mystery texture to it
Perhaps need to go more texture/color detail instead of totally flat svg, or use dual colors

## Feature 17 — Story Interaction Lists & Bookshelves
- **L1 — Stage 5 (re-model resolved in WU0 / InitialSchema, 2026-06-20).** `HasStarted` is present;
  derived tabs "Actively Reading" (`HasStarted AND NOT IsCompleted AND NOT IsIgnored`) and "Abandoned"
  (`IsIgnored AND HasStarted`) are now computable from stored columns.
- **L2 — Stage 5 (WU27, 2026-06-24).** Tab-backing read queries:
  - `GetBookshelfStoryIdsAsync(BookshelfTab)` on `IUserStoryInteractionReadService` — all candidate IDs for
    active user, scoped via `IActiveUserContext`; anonymous → empty; `MyStories`/`Recommendations`/
    `HiddenGems` throw `ArgumentOutOfRangeException` (routed differently by the dispatcher).
  - `GetStoryIdsByAuthorAsync(int)` on `IStoryReadService` — `IgnoreQueryFilters("ContentRating")` so
    authors see own mature stories.
  - `GetListingsAsync(filter, restrictToStoryIds?)` — additive param on `IStoryReadService`; `restrictToStoryIds`
    is applied first, before all other predicates and before count; null = unrestricted; existing callers
    unaffected.
  - `GetRecommendedStoryIdsAsync()` + `GetHiddenGemStoryIdsAsync()` on `IRecommendationReadService` — added
    in WU27 (additive, not WU29); own approved recs, scoped by `RecommenderId == userId`.
  - Verified: Integration tier — `BookshelfStoryIdsTests.cs` (16 tests, Testcontainers Postgres):
    Favorites/ActivelyReading/Abandoned predicates, user-scoping, anonymous-empty, MyStories-throws,
    GetStoryIdsByAuthorAsync incl. mature bypass, GetListingsAsync narrowing + content-rating still applies,
    GetRecommendedStoryIds / GetHiddenGemStoryIds approved-only and hidden-gem narrowing.
- **L3-Logic / L3.5-Structure — Stage 5 (WU27, 2026-06-24).** `BookshelvesPage` dispatcher
  (`/bookshelves/{Tab?}`, `[Authorize]`, mirrors StoryPage fetch-then-dispatch); `BookshelvesDesktop`;
  `BookshelvesMobile`. Not a discovery surface — no SearchMode entries. Composes `StoryDeck` +
  `ResultsFilterPanel` (narrowing, not discovery); keeps them unbundled per §5.27 convention.
  `BookshelfTabVisuals` maps `BookshelfTab → (IconPath, AccentColor, Label, Slug)`.
  Verified: RazorComponents tier — `BookshelvesDesktopTests.cs` (7 tests): tab bar 11 tabs, active
  `aria-current`, inactive tabs, all hrefs, StoryDeck present, ResultsFilterPanel present, alternate-active
  assertion. `BookshelvesMobileTests.cs` (10 tests): dropdown 11 tabs, active aria-current, correct hrefs,
  overlay initially closed, filter button opens overlay, backdrop-click closes, close-button closes,
  StoryDeck present, open-then-close = no panel.
- **L4-Style — Stage 5 (WU27, 2026-06-24).** Desktop: horizontal tab bar with dynamic inline accent
  styles (`background-color:{AccentColor}22;color:{AccentColor};border-bottom:2px solid {AccentColor}` on
  active tab). Mobile: `<details>` tab dropdown + filter overlay (`fixed inset-0 z-50 bg-black/50`,
  `@onclick:stopPropagation`, renders nothing when closed). Human visual sign-off pending (browser walk-through
  of all 11 tabs + teal Following + mobile overlay) — no automated style check; visual → Stage 6.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; the bookshelves page verified in a real WASM runtime during
  the flip's browser wave. Full wave narrative + the 7 bugs found/fixed: `workplan.md` WU-GlobalFlip.
- **L6 — Stage 5** (same indexes; verified during WU15, 2026-06-22 — see Feature 16 L6 note above).

  **Settled design — 11 tabs, in display order (desktop bar + mobile dropdown both use this order):**

  | `BookshelfTab` | AccentColor | Color name | Label | Icon concept |
  |---|---|---|---|---|
  | `MyStories` | `#2F7D4F` | Leafeon Green | My Stories | Book body + spine (left ⅔) + diagonal quill/pen nib crossing |
  | `HiddenGems` | `#1FA37A` | Torterra Emerald | Hidden Gems | Kite diamond gem with CCW crown-facet cutout (Pokémon-gem motif) |
  | `Recommendations` | `#5BB85A` | Roserade Green | Recommendations | 5-pointed star at top-right + two diagonal streak trails from bottom-left (shooting star) |
  | `Favorites` | `#E8507A` | Fairy Pink | Favorites | Reuses `UserStoryInteractionVisuals.For(Favorite)` |
  | `PrivateFavorites` | `#C040A8` | Mismagius Magenta | Private Favorites | Reuses `UserStoryInteractionVisuals.For(PrivateFavorite)` |
  | `Completed` | `#E8B84B` | Arceus Gold | Completed | Reuses `UserStoryInteractionVisuals.For(Complete)` |
  | `Following` | `#2DBBA0` | Manaphy Teal | Following | Reuses `UserStoryInteractionVisuals.For(Follow)` (teal after WU27 reskin) |
  | `ActivelyReading` | `#2E96A8` | Lake Acuity Blue | Actively Reading | Two open-book page rectangles (left + right, spine gap) with page-line fills |
  | `ReadItLater` | `#2E6FBF` | Azurite Blue | Read It Later | Reuses `UserStoryInteractionVisuals.For(ReadLater)` |
  | `Abandoned` | `#9A8580` | Wayward Cave Gray | Abandoned | House silhouette (rect body + triangle roof) with CCW door opening cutout |
  | `Ignored` | `#C04030` | Cinnabar Rust | Ignored | Reuses `UserStoryInteractionVisuals.For(Ignore)` |

  Source of truth for the 5 new icons (My Stories, Hidden Gems, Recommendations, Actively Reading,
  Abandoned): `SharedUI/Bookshelves/BookshelfTabVisuals.cs`. The 6 interaction-tab icons are a
  pass-through to `UserStoryInteractionVisuals.For(...)` — single source of truth, picks up the teal
  Following automatically. The 2 recommendation icons (Hidden Gems, Recommendations) also live as
  `public const string` in `SharedUI/Recommendations/RecommendationIcons.cs` (owned by WU27, consumed by
  WU29's `RecommendationCard`).

  **Desktop layout:** horizontal tab bar (icon + label, `<a href>` links, `aria-current="page"` on active);
  two-column body: `StoryDeck` (main, ~⅔ width) + `ResultsFilterPanel` right sidebar (~⅓ width).

  **Mobile layout:** tab `<details>` dropdown (shows active icon + label; expands to all 11 with
  icon + label; reuses `ChapterNavigation` `<details>` pattern); filter button opens `ResultsFilterPanel`
  as an inline backdrop overlay (ConfirmDialog shell convention — `fixed inset-0 z-50 bg-black/50`,
  `@onclick:stopPropagation`, renders nothing when closed). The overlay IS the "third consumer" the WU9
  note flagged for deciding whether to extract a shared `Modal` primitive — decision: **do NOT extract**;
  a slide-in filter drawer is structurally different from the centered ConfirmDialog; build inline.

  **My Stories content-rating**: `IgnoreQueryFilters("ContentRating")` — an author always sees their own
  stories regardless of maturity. Service gate still enforces that only the author can see unpublished /
  private drafts (WU24).

  **Recommendations + Hidden Gems tabs** ("My Recommendations" / "My Hidden Gems"): stories the active
  user wrote an *approved* recommendation for. `GetRecommendedStoryIdsAsync` = any approved rec written by
  the user; `GetHiddenGemStoryIdsAsync` = same narrowed to `IsHiddenGem == true`. Both methods added to
  `IRecommendationReadService` and implemented in `ServerRecommendationReadService` as part of WU27
  (2026-06-24) — additive, server-only, no client stub required.

### WU-ComponentSoundness Stage note (2026-06-27)

**Cell affected:** F17 L3-Logic (BookshelvesPage) — correctness polish inside an already-aligned
Stage-5 cell; no stage transition.

**F1 — BookshelvesPage lifecycle reload (tab-switch stale content, now closed):**

`BookshelvesPage.razor` now implements the MessagesPage route-dispatcher pattern with key `Tab`:
- `private bool _initialized;` + `private string _loadedTab = "";` (empty string sentinel — no valid
  tab slug is empty).
- `OnInitializedAsync`: auth-resolution (one-time); calls `LoadTabAsync()` then sets `_initialized = true`.
- `OnParametersSetAsync`: guards `Tab == _loadedTab`; resets `_filter = new()`, then calls `LoadTabAsync()`.
- `LoadTabAsync()`: handles empty-tab redirect (→ default "favorites" tab), bad-slug NotFound, sets
  `_tab`, `_loadedTab = Tab`, then calls `LoadPageAsync()`.

Root cause: the 11-tab bar on `BookshelvesDesktop`/`BookshelvesMobile` navigates via router-intercepted
`<a href>` links — same component instance, `OnInitializedAsync` does not re-fire. The prior code
loaded the tab payload in `OnInitializedAsync` only; switching from "Favorites" to "Completed"
left the Favorites deck on screen.

Covering tier: **manual boot gate** (no bUnit test — BookshelvesPage injects many services; listed in
E2E checklist). Convention in `layer3-logic.md`
§"Route-parameter dispatchers reload in `OnParametersSetAsync`".

---

### Dependency callout
Everything in this folder past L1 is blocked on resolving the reading-status re-model **and** on the
`StoryCard`/`StoryDeck` atoms (owned by Stories/). Both resolved: `StoryDeck` is WU14 (Stage 5),
`StoryCard` is WU13 (Stage 5), `ResultsFilterPanel` is WU23 (Stage 5). WU27 proceeds.

## L4.5-Browser verification (2026-07-01) — F16 + F17 → Stage 5, one implementation gap fixed

**Gap found and fixed same-session:** `UserStoryInteractionPanel`'s `Detail` context (all six
buttons, built + bUnit-tested in WU16) was mounted NOWHERE — only `StoryCard` consumed the panel,
in `Listing` context, whose blank-slate deliberately shows just ReadLater/Ignore. Net effect:
Favorite / PrivateFavorite / Follow / Complete were unreachable anywhere in the UI. Fixed:
`StoryPage` now injects `IUserStoryInteractionReadService`, loads `GetStateAsync(StoryId)` for
authenticated viewers (both lifecycle paths), and passes `UsiState` to `StoryDesktop`/`StoryMobile`,
which mount the panel in `Detail` context under the badge row (auth-gated; own-story renders the
Edit link per the panel's `IsOwnStory` contract; N+1 rule preserved — panel still receives state
from the dispatcher).

**Verified in browser:** on another author's story, all six buttons render with seeded state
reflected (Ignore active); clicking Favorite applied the optimistic pink-active state and, after
the 2s debounce, persisted (`is_favorite=t` via psql); `/bookshelves/favorites` immediately lists
the story (F17). All bookshelf tabs render with the seeded per-tab coverage; listing-context card
buttons render per the blank-slate rules.

### WU-AuditFixPass note (2026-07-18)

MA-401 closed: `UserStoryInteractionPanel` is `IAsyncDisposable` with a pending-flush flag —
toggle-then-navigate-away within the 2s debounce window now flushes the write on dispose instead of
silently dropping durable user intent (axiom 7); a teardown flush failure logs Warning, never
throws. Full detail: `workplan.md` WU-AuditFixPass.

### WU-AuditFixPass-2 note (2026-07-18)

Endpoint-authz sweep + MA-402, F16/F17 (cells stay Stage 5 — behavior corrected in place):
`GetFavoriteStoryIdsAsync` now derives `includePrivate` server-side (`activeUser.UserId == userId`,
MA-602 pattern) — the client-supplied bool that leaked hidden favorites is gone (client stops sending it).
**MA-402:** `UserStoryInteractionFilter` resync-until-interaction (TreeSearchControls pattern) so the
async default-exclusion seed reflects in the checkbox. Covered: `UserStoryInteractionEndpointsTests`
(Integration) + browser. Full detail: `workplan.md` WU-AuditFixPass-2.
