# Audit — Sprites/

**Feature:** 3 (Sprite & Theme System). Read-only service cluster — `ISpriteReadService` only, **no write
half** (correct per §7.4). Git-managed assets in `wwwroot`. Dual server/WASM implementation.

## Shared Context

**Entities:** `Theme` (Core/Models/, seeded with the default "Pokémon" theme); `Tag.SpriteIdentifier`
stores a **key, not a URL** — the client builds `wwwroot/images/themes/{theme}/static|animated/...` at
render time. `User.PrefersAnimatedSprites` selects the path; fallback is `unknown_sprite.png`.

**Contracts/impls:** `ISpriteReadService` (Core/Sprites/),
`ServerSpriteReadService` (Server/Sprites/ — `IWebHostEnvironment` + disk check w/ fallback),
`OptimisticSpriteService` (Client/Sprites/ — constructs URLs optimistically, no disk/HTTP; its own
rename to `WasmSpriteReadService` is Post-MVP L5 work). Registered in each project's `Program.cs` as
`ISpriteReadService → ServerSpriteReadService` (Server) / `ISpriteReadService → OptimisticSpriteService`
(Client).

---

## Feature 3 — Sprite & Theme System
- **L1 — Stage 5.** `Theme` entity + seed; `Tag.SpriteIdentifier` key pattern; `User` animation/theme
  prefs. Adding a theme = a new folder, zero DB change (§3.17). Sound.
- **L2 — Stage 5 (WU2, 2026-06-20).** Dual-impl architecture was already correct (server
  disk-check-with-fallback vs. WASM optimistic), matching `layer5-wasm.md`'s resource-gap guidance.
  Reconciled the two naming/location divergences: renamed `ISpriteService`→`ISpriteReadService` and
  `FileSystemSpriteService`→`ServerSpriteReadService` (now primary-constructor DI, matching
  `Server{Feature}ReadService` style); moved all three files (interface, server impl, client impl —
  `OptimisticSpriteService` keeps its name, see L5 note below) out of the legacy `ServiceInterfaces/`/
  `Services/` folders into their `Sprites/` cluster folder in Core/Server/Client respectively; updated
  both `Program.cs` DI registrations. The single method
  `GetSpriteUrl(theme, spriteIdentifier, animated)` is otherwise unchanged.
  **Settled (WU2) — `GetInteractionIcon` is explicitly OUT of scope for this service** (previously this
  note and spec §5.30.5 implied the sprite service should grow a
  `GetInteractionIcon(InteractionTypeEnum, theme)` method). Theme-swappable interaction icons are a
  **UserStoryInteraction-domain concept** — see `audit/UserStoryInteractions.md` Feature 16. The sprite
  service stays a single generic `GetSpriteUrl` resolver; it never learns about `InteractionTypeEnum`.
  Spec §5.30.5's `ISpriteService.GetInteractionIcon(...)` line is superseded by this note (spec is a
  read-only historical snapshot; this audit file is the current authority).
  **Further settled (WU7) — interaction icons don't route through this service at all, even via the
  generic `GetSpriteUrl`.** The WU2-era plan was "map `InteractionTypeEnum` → sprite key → resolve via
  `GetSpriteUrl`"; WU7 replaced that with inline SVG (`IconPath`/`AccentColor` parameters on
  `UserStoryInteractionButton`, mapped by the owning composite — no sprite asset, no theme folder, no
  `GetSpriteUrl` call). `GetSpriteUrl` is otherwise unaffected — still the resolver for tags, covers,
  avatars, and any future theme-swappable art. See `audit/UserStoryInteractions.md` Feature 16 L4 note
  and `layer4-style.md` "Interaction Icons Are Inline SVG."
  **How verified:** `dotnet build` green across all four projects (zero warnings/errors introduced);
  grepped the repo for `ISpriteService`/`FileSystemSpriteService` — zero remaining code references;
  live server run (`TheCanalaveLibrary.Server`, direct, not AppHost) booted clean with DI resolving
  `ISpriteReadService → ServerSpriteReadService`, `/`, `/Account/Login`, `/Account/Register` all `200`.
  No sprite-rendering consumer exists yet, so this was a contract/DI-correctness verification, not a
  visual one (no L4 work in this unit).
  **2026-06-22 (WU12.5 backfill):** verification migrated into asserted tests — `SpriteReadServiceTests`
  in `TheCanalaveLibrary.Tests.Unit` (tier: **Unit**). `ServerSpriteReadService` is constructed
  directly (`new ServerSpriteReadService(fakeEnv)` with a `FakeWebHostEnvironment` pointing at a
  per-test temp dir — no host, no DB). Covers: animated `.webp` path when the file exists; static
  `.png` fallback when animated is missing or `prefersAnimated=false`; `unknown.png` when neither
  exists; correct theme sub-path. Mutation-sanity confirmed (removing `unknown.png` fallback → test
  fails). `dotnet test` green.
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
  lambda (inner-double-quote limitation in Razor attributes — see `cross-cutting.md` §"Razor attribute
  quoting"). Visual sign-off pending human run at `/settings`. Stage-6 gate = human visual approval.
- **L4-Style — Stage 1.** Theme-selection UI visual; blocked on tokens.
- **L5 — Stage 4.** `OptimisticSpriteService` (Client) is the WASM half and is architecturally sound, but
  carries the same interface-naming divergence as L2 (its own rename to `WasmSpriteReadService` is
  deferred to this Post-MVP L5 work, per `workplan.md`). No endpoint needed (pure URL construction) —
  correct.
- **L6/L7/L8 — N/A.**
