# Audit — Images/

**No own grid Feature.** Cross-cutting cluster (same shape as `RichText/`, `Dialogs/`, `Users/`) — its
"feature" is a shared service, not a row in `status.md`'s grid. The cells it actually serves are
**Stories Feature 4 L2** (cover-art upload — already Stage 5, this resolves that cell's remaining open
item) and **Profiles Features 20–22 L2** (profile-picture upload — still Stage 2, consumed by WU30, not
built here). This file is the settled-vs-open reference both cite.

## Shared Context

**Contract (Core/Images/):** `IImageStorageService` — `Task<string> SaveAsync(Stream content, string
contentType, ImageKind kind, int ownerId)` returning the **stored relative path**; `Task
DeleteAsync(string relativePath)`. `ImageKind` enum (`Cover`/`ProfilePicture`) drives the key
convention. This is a **write-side blob service** (distinct from sprites, which have no runtime write
path at all — sprite assets are provisioned out-of-band via Rclone → R2). Any read of a user image is
just serving the static file directly, no service call.

**Relationship to sprites (settled 2026-06-27):** user uploads and sprites converge on **one thing
only** — the public-asset-base config (same CDN / R2 bucket in prod, same `SpriteBaseUrl` /
upload-base seam). They do **not** share a storage service. The separation is principled:
uploads are owner-written at runtime (uuid key, entity stores path), sprites are out-of-band
provisioned (semantic key on `Tag`, URL computed per-viewer). `IImageStorageService` stays
uploads-only; sprites use `ISpriteAssetProbe` for write-time existence checking only.

**Orphan bug (fixed 2026-06-27):** `IImageStorageService.DeleteAsync` previously had zero callers
— replacing a cover or avatar orphaned the old blob forever. Now: `ServerStoryWriteService.UpdateStoryAsync`
calls `DeleteAsync(oldPath)` after a successful save when the cover changes;
`ServerUserSettingsService.UploadProfilePictureAsync` reads the old path and calls `DeleteAsync` after
persisting the new one. Both are best-effort (failure does not fail the user's save).

**Impls:**
- `LocalImageStorageService` (Server/Images/, **MVP, built WU12**) — writes under `wwwroot/uploads/`,
  served by `UseStaticFiles()`. Returns a **host-relative URL** (e.g.
  `/uploads/stories/5/cover-{uuid}.jpg`) that resolves against whatever origin the app is running on —
  `localhost:7xxx` in dev, the real domain once deployed. No full URL is ever stored (see "URL
  Conventions" below).
- `S3ImageStorageService` (`AWSSDK.S3`, **Post-MVP**, `workplan.md` Post-MVP section) — one
  implementation, two endpoints: MinIO via Aspire in dev, Cloudflare R2 in prod ("same AWS SDK code,
  different endpoint config" — spec §3.17 / `cross-cutting.md`). Swaps in behind the same interface;
  no Layer 1–4 change.

**Key convention (spec §3.17):** `stories/{StoryID}/cover-{uuid}.{ext}`,
`users/{UserID}/profile-{uuid}.{ext}`. Both impls honor it so a stored path is interchangeable across
implementations (no data migration on swap).

**URL conventions (settled WU12):** entity columns (`StoryListing.CoverArtRelativeUrl`,
`User.ProfilePictureRelativeUrl`) are `MaxLength(512)` **relative paths**, never full URLs — spec §3.17
explicitly rejected storing full URLs ("CDN domain change would require updating millions of rows").
`LocalImageStorageService` stores the host-relative path directly (`/uploads/...`); the value is
"appended to the CDN base" in the trivial sense that the MVP's CDN base *is* the site's own origin. If a
separate CDN subdomain is adopted later, the swap is at display time (prepend a configured base), not a
column rewrite. A default placeholder (`wwwroot/img/default-cover.svg`, mirroring WU10's
`default-avatar.svg`) substitutes when the column is null — consuming components never branch on null
themselves (same discipline as `UserCard`'s avatar fallback).

**Consumers:** `StoryListingDto.CoverArtRelativeUrl` (WU12, copied verbatim by the read projection, like
avatars — never re-resolved at display time); the upload **UI** (an `<InputFile>` calling `SaveAsync`)
is WU24 (story cover) and WU30 (profile picture) — not built in WU12, which only mints the service and
keeps the write path's `CoverArtRelativeUrl` a pass-through string.

---

## L2 — Stage 5 (WU12, 2026-06-22)

`IImageStorageService` + `LocalImageStorageService` minted and registered
(`AddScoped<IImageStorageService, LocalImageStorageService>()`). No write-path call site yet (Stories'
`CreateStoryAsync`/`UpdateStoryAsync` still take `CoverArtRelativeUrl` as a plain string parameter) —
that's deliberate; an upload UI is the first real caller (WU24/WU30), and minting ahead of that caller
gave `StoryCard` (WU13) a real, renderable cover URL to display instead of shipping covers as vapor.

**Open:** the cloud backend (`S3ImageStorageService`) — Post-MVP, `workplan.md` Post-MVP section. The
upload UI itself (file picker → `SaveAsync` → persist the returned path) — WU24, WU30.

**How verified:** via `/dev/wu12/upload-test-image` (kept as a standing dev-diagnostics endpoint, not
removed, per explicit user instruction to keep testing artifacts for later analysis) — POSTed a minimal
1x1 PNG to `LocalImageStorageService.SaveAsync(stream, "image/png", ImageKind.Cover, 999)`, got back
`/uploads/stories/999/cover-{uuid}.png`; `GET` of that path returned `200 OK`, `Content-Type:
image/png`, body byte-for-byte identical to the uploaded PNG (confirms `UseStaticFiles()` serves
`wwwroot/uploads/` correctly and the key convention round-trips). The fixture file under
`wwwroot/uploads/stories/999/` was left in place alongside the other WU12 fixtures — not real seeded
content, story id 999 does not correspond to an actual `Story` row.

**WU12.5 (2026-06-22)** migrated this verification into asserted, CI-runnable tests —
`ImageStorageServiceTests` in `TheCanalaveLibrary.Tests.Integration` covers the same save/round-trip
behavior plus delete and path-traversal-guard cases, writing to a per-test-run temp `WebRootPath`
rather than the real `wwwroot/uploads/`. The dev-diagnostics endpoint is no longer the source of truth
for this behavior (see `canalave-conventions/testing.md`).

**WU38 Stage note — orphan bug fix (2026-06-27):**

`IImageStorageService.DeleteAsync` had zero callers before this fix. Two call sites added:
- `ServerStoryWriteService.UpdateStoryAsync`: reads `storyToUpdate.StoryListing.CoverArtRelativeUrl`
  before updating, then calls `DeleteAsync(oldPath)` best-effort after `SaveChangesAsync` when the
  cover changed.
- `ServerUserSettingsService.UploadProfilePictureAsync`: reads `ProfilePictureRelativeUrl` before
  `SaveAsync`, calls `DeleteAsync(oldPath)` best-effort after persisting the new path.
Both guard on null old path and skip when old == new. Failures are silently swallowed — the user's
save already succeeded, and blob cleanup is best-effort.
**How verified (2026-06-27):** `dotnet build` green; `dotnet test` green (1228 total). The orphan
behavior has no automated test (would require a fake `IImageStorageService` tracking `DeleteAsync`
calls in the integration layer — not yet implemented; manual verification is the gate for Stage 6).
Cells: F4 L2 (Stories — story cover orphan) and F20 L2 (Profiles — avatar orphan) both remain Stage 5.

## L3/L3.5/L4 — N/A for this cluster

No component lives in `Images/` — it's a pure service cluster (like Sprites' read half). Upload UI
components belong to their owning feature (`StoryPropertiesForm` for WU24, profile edit form for WU30).

## L5/L6/L7/L8 — N/A

No endpoint needed in MVP (server-side `InteractiveServer` calls the service directly, no HTTP hop).
Post-MVP L5 WASM enablement would need an upload endpoint if the Client ever needs to upload directly —
not designed yet, out of scope here.
