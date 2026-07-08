# Audit — Sprites/

**Feature:** 3 (Sprite & Theme System). Read service + asset probe. Git/Rclone-managed assets in
`wwwroot/sprites/themes/` (dev) / R2 (prod). Single shared Core impl.

## Shared Context

**Entities:** `Theme` (Core/Sprites/Theme.cs — `ThemeId`, `Name` (display), `Slug` (URL-safe,
unique, path segment), `Description`; seeded with `{ ThemeId=1, Name="Pokémon", Slug="pokemon" }`).
`Tag.SpriteIdentifier` (`[MaxLength(50)]`) stores a **semantic key** (e.g. `"bulbasaur"`) — **not a
URL**. The URL is computed at render time from `(SpriteBaseUrl, slug, id, prefersAnimated)`.

**Key architectural decisions (settled 2026-06-27, do not revisit):**
- Sprite assets are provisioned **out-of-band** (Rclone → R2 + DB seed for tags/themes). The Blazor
  app never writes sprite assets. No web upload UI, no `/mod/sprites` page, no runtime Theme CRUD.
- URL construction is **optimistic** — the browser handles misses via `onerror` (`webp → png → unknown.png`).
- **`ISpriteReadService`** (Core/Sprites/) — single shared impl `OptimisticSpriteReadService`
  (pure string builder, no host/disk dependency). Registered as singleton on both Server and Client.
  Method: `GetSpriteUrl(string slug, string id, bool prefersAnimated)`.
- **`ISpriteAssetProbe`** (Core/Sprites/) — server-only `ExistsAsync(slug, id)` for write-time
  validation in `ServerTagWriteService`. Never called at render time. Local impl: `LocalSpriteAssetProbe`
  (`File.Exists`); R2 impl deferred.
- **`ThemeContext` cascading value** (`record ThemeContext(string Slug, bool PrefersAnimated)`) —
  cascaded from a root `ThemeContextProvider` in `Routes.razor`. SharedUI sprite components inject
  `ISpriteReadService` and take `[CascadingParameter] ThemeContext`. See `render-and-layout.md`
  "ThemeContext Cascading Provider."

---

## Feature 3 — Sprite & Theme System
- **L1 — Stage 5 (updated 2026-06-27).** `Theme` entity + seed; `Tag.SpriteIdentifier` key pattern;
  `User` animation/theme prefs. **`Theme.Slug` added** (`[Required][MaxLength(64)]`, unique index;
  seeded `"pokemon"`). Claim + path carry the slug; `Theme.Name` stays display-only. Adding a theme
  = add a DB row (slug+name) + provision a sprite folder. Zero code change.
- **L2 — Stage 5 (WU2 through 2026-06-27).** History: WU2 renamed/relocated the interface and impls;
  WU12.5 added unit tests; pre-integration cleanup (2026-06-26) rewrote `ServerSpriteReadService` as a
  singleton startup-scan cache and added `SpriteReadServiceExtensions.cs` for the `IActiveUserContext`
  extension. **2026-06-27 (sprite redesign — this WU):** the startup-scan cache is structurally unable
  to work against remote object storage (cannot `File.Exists` an R2 bucket per render). It is replaced
  by a single **optimistic URL builder** `OptimisticSpriteReadService` in Core — pure string building,
  no host/disk dependency, registered as singleton on both Server and Client. The `IActiveUserContext`
  extension (`SpriteReadServiceExtensions.cs`) and `ServerSpriteReadService.cs` /
  `Client/OptimisticSpriteService.cs` are **deleted**. URL resolution moves from the read service into
  the rendering components (see L3.5/L4 note). The read-service call sites in
  `ServerTagReadService` (3 sites) and `ServerStoryReadService` (`ToTagChip` + character chips) revert
  to projecting the raw `SpriteIdentifier`; those services drop their `ISpriteReadService` constructor
  dependency. **Settled (WU2) — `GetInteractionIcon` stays OUT of scope.** See `audit/UserStoryInteractions.md`
  Feature 16 and `layer4-style.md` "Interaction Icons Are Inline SVG" (supersedes spec §5.30.5).
  **How verified (2026-06-27):** Unit tests (`SpriteReadServiceTests`, rewritten) — optimistic builder:
  `(slug, id, animated)` → `{base}/{slug}/animated/{id}.webp`; `prefersAnimated=false` →
  `.../static/{id}.png`; configured base prepended. New unit test class `LocalSpriteAssetProbeTests` —
  `ExistsAsync` true/false against a temp dir. Integration tests — read-service projections put
  `SpriteIdentifier` (not a URL) on `TagChipDto`. `dotnet test` green (all tiers). Tier: **Unit**
  (`SpriteReadServiceTests` rewritten + `LocalSpriteAssetProbeTests` new).

- **L3-Logic / L3.5-Structure (sprite rendering) — Stage 5 (2026-06-27).** `ThemeContextProvider`
  component added at the root of `Routes.razor` (inside `CascadingAuthenticationState`). Components
  `TagChip`, `TagSelector`, `CharacterEntry` now inject `ISpriteReadService` and take
  `[CascadingParameter] ThemeContext`; they resolve sprite URLs at render time with a plain-HTML
  `onerror` fallback chain (`webp → png → unknown.png`). DTOs carry `SpriteIdentifier` (not a URL).
  `ISpriteAssetProbe` wired into `ServerTagWriteService` as a non-blocking warning on mod-write.
  **Tier: RazorComponents** (`TagChip`/`TagSelector`/`CharacterEntry` tests updated — assert `<img src>`
  built from cascaded slug/animation, `onerror` present, null identifier renders no `<img>`).
  See `render-and-layout.md` "ThemeContext Cascading Provider" and `layer2-services.md` "Sprite URLs
  Are Resolved At Render Time."
- **L3-Logic — Stage 5 (WU30, 2026-06-24).** Theme-selection control built inside
  `SharedUI/Profiles/AppearanceSettingsForm.razor` (the Feature-3 theme-selection UI lives in the
  Profiles settings page Appearance section — this was the settled design). `AppearanceSettingsForm`
  receives the current `ThemeId`, `PrefersAnimatedSprites`, `PrefersDataSaverMode` from the page and
  raises them back via callbacks. The save calls `IUserSettingsService.UpdateAppearanceAsync(themeId,
  prefersAnimated, prefersDataSaver)`. `IThemeReadService.GetThemesAsync()` drives the theme `<select>`.
  How verified: `dotnet build` green; `dotnet test` 373 RazorComponents tests pass.
- **L3.5-Structure — Stage 5 (WU30, 2026-06-24).** `AppearanceSettingsForm` is an injection-free
  leaf-composite with `ThemeId`, `Themes`, `PrefersAnimated`, `PrefersDataSaver` params and
  `OnSave EventCallback<AppearanceModel>`. Theme dropdown uses `@onchange` with `int.TryParse` block
  lambda (inner-double-quote limitation in Razor attributes — see `layer3-logic.md` §"Razor attribute
  quoting"). Visual sign-off pending human run at `/settings`. Stage-6 gate = human visual approval.
- **L4-Style — Stage 1.** Theme-selection UI visual; blocked on tokens.
- **L5 — Stage 5 (resolved 2026-06-27).** The prior Stage-4 divergence (two separate impls with
  different behaviors) is resolved: the two impls collapsed into one `OptimisticSpriteReadService` in
  Core (registered on both Server and Client). No endpoint needed (pure URL construction). No WASM
  proxy required; SharedUI components inject the Core service directly on both sides.
- **L6/L7/L8 — N/A.**

- **L4.5-Browser verification (2026-07-02) — Feature 3 → L4.5=5.** The optimistic-URL design was
  driven end to end in a real browser:
  - Seeded a `Bulbasaur` character tag with `SpriteIdentifier="bulbasaur"` (added to `DataSeeder`
    too — the checked-in dev asset `wwwroot/sprites/themes/pokemon/static/bulbasaur.png` now has a
    matching tag on every fresh DB, so this path stays exercisable).
  - With `PrefersAnimatedSprites=false`: `/tags` rendered
    `<img src="/sprites/themes/pokemon/static/bulbasaur.png">` (slug from the cookie claim via
    `ThemeContextProvider`); asset served 200 and decoded (112×112).
  - Appearance settings (`/settings` → Save Appearance) persisted
    `prefers_animated_sprites=t` (psql-verified). The claim stays stale until re-sign-in — this is
    the **documented** behavior flagged in `ApplicationUserClaimsPrincipalFactory` (callers would
    need `RefreshSignInAsync`, which an interactive circuit cannot issue); after re-login the claim
    read `True` (`/dev/wu12/whoami`).
  - **Full fallback chain observed live:** initial request honored the animated pref →
    `animated/bulbasaur.webp` returned 404 (asset deliberately absent) → `spriteFallback` swapped
    `src` to `data-static` → static PNG loaded. Exactly the settled optimistic design.
  - Theme select renders the single seeded theme; multi-theme switching is untestable until a second
    Theme row + asset folder exist (adding one is a DB row + folder per the settled zero-code rule).
  - Tooling caveat: MCP-driven background tabs never intersection-trigger `loading="lazy"` images,
    so in-page `img.complete` stays false there — asset validity was proven by direct fetch/decode
    and the network log, not by paint.
