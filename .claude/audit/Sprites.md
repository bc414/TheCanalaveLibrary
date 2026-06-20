# Audit — Sprites/

**Feature:** 3 (Sprite & Theme System). Read-only service cluster — `ISpriteReadService` only, **no write
half** (correct per §7.4). Git-managed assets in `wwwroot`. Dual server/WASM implementation.

## Shared Context

**Entities:** `Theme` (Core/Models/, seeded with the default "Pokémon" theme); `Tag.SpriteIdentifier`
stores a **key, not a URL** — the client builds `wwwroot/images/themes/{theme}/static|animated/...` at
render time. `User.PrefersAnimatedSprites` selects the path; fallback is `unknown_sprite.png`.

**Contracts/impls:** `ISpriteService` (Core/ServiceInterfaces/),
`FileSystemSpriteService` (Server — `IWebHostEnvironment` + disk check w/ fallback),
`OptimisticSpriteService` (Client — constructs URLs optimistically, no disk/HTTP). Registered in
`Program.cs` as `ISpriteService → FileSystemSpriteService`.

---

## Feature 3 — Sprite & Theme System
- **L1 — Stage 5.** `Theme` entity + seed; `Tag.SpriteIdentifier` key pattern; `User` animation/theme
  prefs. Adding a theme = a new folder, zero DB change (§3.17). Sound.
- **L2 — Stage 4.** Dual-impl architecture is correct (server disk-check-with-fallback vs. WASM
  optimistic), matching `layer5-wasm.md`'s resource-gap guidance. **Divergences:**
  - Interface is **`ISpriteService`** with a single `GetSpriteUrl(theme, spriteIdentifier, animated)`.
    Spec/conventions want **`ISpriteReadService`** (read-suffix) and a richer surface including
    `GetInteractionIcon()` (used by `StoryInteractionPanel` for theme-swappable icons, §5.30.5).
  - Server impl is named `FileSystemSpriteService` (fine) but the convention prefix is
    `Server{Feature}ReadService`.
  Resolution → Stage 2/3: rename to `ISpriteReadService`, add `GetInteractionIcon()`, align impl names.
- **L3-Logic — Stage 2.** Theme-selection UI logic (`User.ThemeId` write, live re-render) unbuilt.
- **L3.5-Structure — Stage 2.** Theme-selection component unbuilt; sprite consumption is via injection
  into other folders' components (no component *in* this folder).
- **L4-Style — Stage 1.** Theme-selection UI visual; blocked on tokens.
- **L5 — Stage 4.** `OptimisticSpriteService` (Client) is the WASM half and is architecturally sound, but
  carries the same interface-naming divergence as L2. No endpoint needed (pure URL construction) — correct.
- **L6/L7/L8 — N/A.**
