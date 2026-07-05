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
- `S3ImageStorageService` (`AWSSDK.S3` 4.x, **built WU-S3Garage 2026-07-05**) — one implementation,
  two endpoints: Garage via Aspire in dev, Cloudflare R2 in prod (spec §3.17's "same AWS SDK code,
  different endpoint config", made true by the three wire-format constraints below). Selected by
  `ImageStorage:Provider = "S3"` (Program.cs switch; default `Local` — the server-only path keeps
  the filesystem impl). Buffers uploads (enforces the 10 MB cap even on non-seekable browser
  streams — a check `LocalImageStorageService`'s CanSeek guard can't make). In S3 mode the stored
  `/uploads/{**key}` URLs are served by `ImageEndpoints.MapImageServingEndpoints` (streams from the
  bucket, immutable cache header; mapped only when the provider is S3 — in Local mode static files
  serve the same URLs). Shared validation/key logic lives in `ImageUploadRules` so the two impls
  cannot drift.

**Dev S3 endpoint is Garage, not the spec's MinIO (settled 2026-07-05, Brian; built WU-S3Garage
same day):** spec §1/§3.17/decision #8 named MinIO for dev, but MinIO OSS was archived 2026-02 and
its Aspire toolkit package is deprecated — Garage v2.3.0 (`dxflrs/garage`, actively maintained)
took the role. The AppHost briefly ran a pinned MinIO container (WU-Aspire, same week, never
consumed); it was replaced before anything wrote to it. Current resource: `canalave-garage`,
persistent lifetime, S3 API pinned to host port 3900, `--single-node --default-bucket` bootstrap
(auto-creates layout + access key + `canalave-images` bucket from `GARAGE_DEFAULT_*` env vars —
**restart-idempotent, verified 2026-07-05** by container restart against existing metadata),
config bind-mounted from `AppHost/garage.toml` (its `s3_region = "garage"` must match the injected
`ImageStorage:S3:Region`), secrets in AppHost user secrets (`Parameters:garage-s3-secret`,
`Parameters:garage-rpc-secret`). No web console — inspect via
`docker exec canalave-garage /garage bucket info canalave-images`.

**R2 interchangeability (researched + encoded 2026-07-05 — the spec's "same AWS SDK code,
different endpoint config" needed three wire-format constraints to be actually true):**
1. **`UseChunkEncoding = false` on every PutObject** — Cloudflare R2 does not implement SigV4
   streaming (aws-chunked) payloads, which AWSSDK.S3 v4 emits by default; unchunked signed
   payloads work on both R2 (since 2022) and Garage, over http and https.
2. **`RequestChecksumCalculation`/`ResponseChecksumValidation = WHEN_REQUIRED`** — opts out of
   the SDK's 2025 "default integrity protections" trailers. Garage ≥ 2.0 tolerates the defaults;
   R2 accepts checksum *headers* (since 2025-02) but not the streaming trailers — `WHEN_REQUIRED`
   keeps one deterministic wire format on both.
3. **`ForcePathStyle = true`** — required for Garage on localhost, harmless on R2.
All three live in exactly one place: `S3ImageStorageService.CreateClient` (used by both the
production DI path and `GarageFixture` tests). Prod config: `ServiceUrl =
https://<account-id>.r2.cloudflarestorage.com`, `Region = "auto"`, credentials from an R2 API
token; `CreateBucket` is supported on R2's S3 API but the prod bucket will pre-exist. R2 limits
irrelevant at this payload size (10 MB cap vs 5 GiB single-PUT); if multipart is ever added, R2
requires all parts except the last be equal-sized.

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

**Open:** ~~the cloud backend (`S3ImageStorageService`)~~ — built WU-S3Garage (2026-07-05, Stage
note below). The upload UI itself (file picker → `SaveAsync` → persist the returned path) — WU24,
WU30 (since built). Remaining open: the production R2 configuration values (account id, API token,
bucket) — a Phase-6 deployment concern (middle_plan decision row 4), not a code gap.

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

**WU-S3Garage Stage note (2026-07-05) — S3 backend built, L2 remains Stage 5:**

`S3ImageStorageService` + `S3ImageStorageOptions` + `ImageEndpoints` + `ImageUploadRules`
(extraction refactor of the validation/key logic both impls share) landed; `LocalImageStorageService`
refactored onto the shared rules with behavior unchanged. AppHost provisions Garage and injects
`ImageStorage__*` env vars into the web app (S3 provider active under Aspire; server-only path
unchanged on Local).

**How verified:** Integration tier — new `S3ImageStorageServiceTests` (7 tests) against a real
single-node Garage Testcontainer (`GarageFixture`, same image + bootstrap as the AppHost resource),
constructing the client via the same `CreateClient` production uses: save round-trip (bytes +
content type + stored-path shape identical to Local's), profile-kind keys, delete, delete-nonexistent
no-op, traversal/foreign-path no-op, content-type rejection, over-cap rejection (11 MB). Existing
`ImageStorageServiceTests` (Local impl) still green post-refactor. Full `dotnet test` green
(1,266 total). The `/uploads/{**key}` serving route is **browser-verified under the Aspire
AppHost** rather than automated — Program.cs reads `ImageStorage:Provider` eagerly, before
`WebApplicationFactory` config overrides apply (the same documented quirk as the connection
string, see `TestAppFactory`) — full flow 2026-07-05: real `/settings` avatar upload as TestUser →
DB row `/uploads/users/1/profile-{uuid}.png` (psql on 5433), blob in `canalave-images` (Garage CLI:
1 object, exact byte size), page renders it, direct GET 200 `image/png` with
`public, max-age=31536000, immutable`; second upload replaced the first — bucket still exactly
1 object, old URL 404, new URL 200 (orphan cleanup exercised end-to-end for the first time with a
real blob backend); Garage container restart → bootstrap idempotent, blob survived, URL still 200.

## L3/L3.5/L4 — N/A for this cluster

No component lives in `Images/` — it's a pure service cluster (like Sprites' read half). Upload UI
components belong to their owning feature (`StoryPropertiesForm` for WU24, profile edit form for WU30).

## L5/L6/L7/L8 — N/A

No endpoint needed in MVP (server-side `InteractiveServer` calls the service directly, no HTTP hop).
Post-MVP L5 WASM enablement would need an upload endpoint if the Client ever needs to upload directly —
not designed yet, out of scope here.
