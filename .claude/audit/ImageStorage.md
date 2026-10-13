# Audit — Images/

**No own grid Feature.** Cross-cutting cluster (same shape as `RichText/`, `Dialogs/`, `Users/`) — its
"feature" is a shared service, not a row in `status.md`'s grid. The cells it actually serves are
**Stories Feature 4 L2** (cover-art upload) and **Profiles Features 20–22 L2** (profile-picture
upload) — all Stage 5: the WU12-era "still Stage 2, consumed by WU30" framing below is historical
(WU30 built the avatar upload; live-verified 2026-07-05/06, see the Stage notes at the end of this
file). This file is the settled-vs-open reference both cite.

## Shared Context

**Contract (Core/Images/):** `IImageStorageService` — `Task<string> SaveAsync(Stream content, string
contentType, ImageKind kind, int ownerId)` returning the **stored relative path**; `Task
DeleteAsync(string relativePath)`; and, since WU-BlobCleanup (2026-10-13),
`IAsyncEnumerable<StoredImage> EnumerateAsync(CancellationToken)`, which streams every stored upload
under the upload key space only (`stories/`, `users/`) with the store-reported last-modified time.
`ImageKind` enum (`Cover`/`ProfilePicture`) drives the key convention. This is a **write-side blob
service** (distinct from sprites, which have no runtime write path at all — sprite assets are
provisioned out-of-band via Rclone → R2). Any read of a user image is just serving the static file
directly, no service call.

**`EnumerateAsync` is an amendment to the frozen interface, not a violation (owner ruling D14,
2026-08-07).** D14 named the member `IAsyncEnumerable<string>`; it yields a `StoredImage(RelativePath,
LastModifiedUtc)` record instead, because the sweep's grace window needs the store's last-modified
time and `ListObjectsV2` returns it in the same page (the alternative is one HEAD request per blob).
The interface is still never client-implemented.

**Relationship to sprites (settled 2026-06-27):** user uploads and sprites converge on **one thing
only** — the public-asset-base config (same CDN / R2 bucket in prod, same `SpriteBaseUrl` /
upload-base seam). They do **not** share a storage service. The separation is principled:
uploads are owner-written at runtime (uuid key, entity stores path), sprites are out-of-band
provisioned (semantic key on `Tag`, URL computed per-viewer). `IImageStorageService` stays
uploads-only; sprites use `ISpriteAssetProbe` for write-time existence checking only.

**Orphans — the replacement case (fixed 2026-06-27) was one case of four; D14 (2026-08-07) owns the
rest.** Until 2026-06-27 `IImageStorageService.DeleteAsync` had zero callers, so replacing a cover or
avatar orphaned the old blob forever; that fix added the replacement deletes. It did **not** make
orphans a solved problem, and reading it that way is part of why the other cases went unowned for
six weeks:
- **Terminal deletion** (an account deleted, a story hard-deleted by moderation) left the blobs its
  rows named.
- **A failed save after an upload** leaked a blob on a routine path: under the old two-step protocol
  `UploadCoverArtAsync` wrote the blob, then `UpdateStoryAsync` could refuse the save. The inverse was
  worse: an `UpdateStoryAsync` whose DTO omitted the cover nulled the column and deleted the live blob.
- **Fault-class residue**: a blob write that succeeds while its column write fails has no delete
  event to hook.

D14 settled two owners, deliberately: an inline delete at every terminal deletion path, plus a daily
sweep as the backstop for fault residue. "Orphans accepted" is rejected for every class. Its
sub-ruling moved cover persistence into the upload method (the avatar shape). Rule of record:
`layer2-services.md` §"Image Storage — blob ownership and cleanup"; built WU-BlobCleanup (2026-10-13).

**Settled (owner ruling D14, answered 2026-08-07; settled for WU-BlobCleanup 2026-10-13). Do not
revisit:**
- A blob is referenced by exactly one row; the method that writes it persists its reference (write →
  persist → best-effort delete old). No write DTO carries a blob path.
- Every terminal deletion path captures the paths before the row delete and deletes the blobs after the
  commit, outside the retry, best-effort with a Warning. D15's author story delete inherits this.
- The sweep is the backstop, never the erasure mechanism: unfiltered reference read, 24 h grace window,
  log-only first cycle, per-cycle cap, store → DB direction only, upload prefixes and generated key
  shape only, rows naming a missing blob logged and never acted on, a kill switch. One bucket per
  database.
- Cover upload is an immediate commit, so form-cancel no longer reverts a cover (accepted; a revert
  action is not built).

**Open, the owner's:** whether account deletion strips the covers of the user's surviving
(anonymized) stories — D14's text says delete them, but its premise ("the cascade removes the rows
that name them") is false since D13 keeps stories. Built: covers kept. `roadmap.md` decision row 25,
tracker **F22**. **Phase 7:** the CDN purge on hard delete (D14 flags it, not designed here) — a
`roadmap.md` Phase-7 bullet.

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

**Upload content pipeline (settled 2026-07-06, Brian, WU-Security planning):** the browser's
claimed MIME string is no longer trusted — `ImageUploadProcessor` (magic-byte sniff via
`Image.Identify`, sniffed format authoritative; ImageSharp decode/re-encode; header-level
decompression-bomb guard; EXIF/XMP/IPTC strip; downscale to ≤ 2048 px) is the single shared
step both storage impls consume before writing bytes. This also closes two pre-existing gaps:
`LocalImageStorageService`'s size cap silently skipped for non-seekable streams (`CanSeek`
guard), and stored extension/content-type following the claimed MIME rather than the actual
bytes. The frozen `IImageStorageService` signature is unchanged — hardening is entirely behind
`SaveAsync`, so it carries over as-is when an upload endpoint appears at the L5 flip. Full
pipeline + rationale: `canalave-conventions/security.md` §"Upload Content Pipeline".

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
was WU24's (story cover) and WU30's (profile picture) — WU12 only minted the service and kept the
write path's `CoverArtRelativeUrl` a pass-through string; both UIs have since shipped.

---

## L2 — Stage 5 (WU12, 2026-06-22; ImageSharp held at 3.1.12 as accepted risk with advisory guards, WU-DependencyCVEs, 2026-10-12; refusals made user-facing on both render phases by its browser pass the same day; blob cleanup owned since WU-BlobCleanup, 2026-10-13 — `EnumerateAsync`, the daily orphan sweep, the inline deletes at account deletion and moderation hard delete, cover upload persisting its own reference; Unit + Integration-tested, browser pass owed (tracker H31) — Stage notes below, newest last)

`IImageStorageService` + `LocalImageStorageService` minted and registered
(`AddScoped<IImageStorageService, LocalImageStorageService>()`). No write-path call site yet (Stories'
`CreateStoryAsync`/`UpdateStoryAsync` still take `CoverArtRelativeUrl` as a plain string parameter) —
that's deliberate; an upload UI is the first real caller (WU24/WU30), and minting ahead of that caller
gave `StoryCard` (WU13) a real, renderable cover URL to display instead of shipping covers as vapor.

**Open:** ~~the cloud backend (`S3ImageStorageService`)~~ — built WU-S3Garage (2026-07-05, Stage
note below). The upload UI itself (file picker → `SaveAsync` → persist the returned path) — WU24,
WU30 (since built). Remaining open: the production R2 configuration values (account id, API token,
bucket) — a deployment concern (`roadmap.md` decision row 4, launch-readiness mechanics), not a code gap.

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

**WU-Security Stage note (2026-07-06) — upload content pipeline built; L2 remains Stage 5:**

`ImageUploadProcessor` (Server/Images/) is now the single hardening step both impls call before
writing any byte: per-user throttle (`WriteActionKind.ImageUpload`, skipped for the anonymous
dev-diagnostics probe) → claimed-MIME fast-fail → buffered 10 MB cap (closes the Local impl's
`CanSeek`-gated bypass; S3's private `CopyWithLimitAsync` deleted as subsumed) → magic-byte
sniff via `Image.Identify` with only jpeg/png/webp decoders (sniffed format authoritative —
stored extension + content type follow the bytes, never the browser claim) → header-level
decompression-bomb guard (16 384 px side / 64 MP, pre-decode) → first-frame decode →
`AutoOrient` then EXIF/XMP/IPTC strip (ICC kept) → ≤ 2 048 px downscale → re-encode. Frozen
`IImageStorageService` unchanged. SixLabors.ImageSharp **pinned to 3.1.x** (4.x demands a
build-time license key; Dependabot major-ignore in `.github/dependabot.yml`).

**How verified:** Unit — `ImageUploadProcessorTests` (11: format round trips, spoofed-MIME
returns sniffed format, garbage/GIF rejection, oversized non-seekable stream rejection,
hand-crafted 50 000×50 000 IHDR rejected without decode, EXIF strip, aspect-preserving
downscale, throttle called for authenticated / skipped for anonymous);
`ImageStorageTelemetryTests` updated (re-encode changes stored size — assertions now
presence-based). Integration — `ImageStorageServiceTests` + `S3ImageStorageServiceTests`
refitted to real generated fixtures (`TestImages`), each with a spoofed-MIME end-to-end case
(stored key + served/object content type = png against real filesystem and real Garage).
Browser band (server-only path, 2026-07-06): `/settings` avatar upload of PNG bytes named
`.jpg` → DB row `profile-{uuid}.png`, served `image/png` with `nosniff`; 3000×100 upload
stored 2048×68; prior avatar orphan-deleted on replace.

**WU-DependencyCVEs Stage note (2026-10-12) — ImageSharp 3.1.12 kept as accepted risk; L2 remains
Stage 5.** Five ImageSharp advisories were published 2026-10-07: GHSA-j3p4-wp97-rph4,
GHSA-j9gm-c75j-xc9q and GHSA-jjfr-hcj7-qf5w (high); GHSA-gwg2-r3hj-4w44 and GHSA-wmxv-xphr-5c9g
(moderate). They are fixed only in 4.1.2+ and in 3.2.0 (2026-10-09), and both lines now run Six
Labors' build-time license check, which fails a Release build without a key. The WU-Security pin
rule ("stay on 3.1.x; revisit only if 3.1.x stops receiving security patches") has therefore fired.
The choice of a key, a library swap or standing risk is the owner's: `roadmap.md` decision row 21
(tracker **E9**). **No package or pipeline code changed.**
- **Why it is acceptable meanwhile.** Every vector is unreachable through `ImageUploadProcessor`:
  - no TIFF decoder is configured (`SniffConfiguration` serves both `Identify` and `Load`);
  - only JPEG, PNG and WebP are encoded;
  - no histogram processor runs;
  - `IccProfile.Entries` is never read (ICC passes through as raw bytes).

  Per-advisory analysis: `security.md` §"Dependency Vulnerability Scan Cadence". The pipeline rule
  is now written in §"Upload Content Pipeline" Rules.
- **Verified (Unit tier):** new `ImageUploadProcessorAdvisoryTests` (2), in a non-parallel
  collection, each feeding an advisory's own PoC:
  - the 24-byte BigTIFF with a 5×10⁹-entry IFD is rejected at the sniff, under a 10 s deadline;
  - a PNG carrying the 200-byte truncated-CLUT ICC profile is processed with < 48 MB allocated
    (measured ≈ 130 KB), and the output keeps the identical profile bytes.

  Mutation-checked: adding `TiffConfigurationModule` to the sniff reproduces the advisory's hang
  (the test fails on the deadline). Reading `IccProfile.Entries` after decode allocates 114.9 MB
  (fails). The existing `ImageUploadProcessorTests` and the Integration storage tests are unchanged
  and green.
- No browser pass is owed: ImageSharp did not change version and no UI changed.

**WU-DependencyCVEs browser verification (2026-10-12; server-only path, Local provider) — one bug
fixed; L2 remains Stage 5.** The pass fed the advisory payloads through the real upload UIs on both
render phases (circuit proven by `_blazor/negotiate`; WASM by the `/api/…` request), with `psql` as
ground truth.
- **Found:** the pipeline refused the 24-byte BigTIFF PoC promptly (about 1 s, no hang), but the
  uploader never got a readable refusal. Every refusal threw a BCL `ArgumentException`:
  - on the circuit, `/settings`' avatar upload showed the generic "Something went wrong" with an
    Error ID, and the server logged `fail: SettingsPage` ("Settings save failed unexpectedly");
  - on WASM the 400's `Detail` reached the page as "Uploaded file is not a recognized image.
    (Parameter 'buffer')".
- **Fixed:** new `ImageUploadValidationException` (Core `Images/`, a `CanalaveValidationException`,
  carrying the decoder's exception as its inner exception; the base gained that constructor).
  `ImageUploadProcessor` and `ImageUploadRules.ExtensionFor` throw it with user-written text for
  every refusal the uploader caused: claimed type, the 10 MB cap, unrecognized bytes, undecodable
  pixels and the dimension guard. The unreachable encoder fallback stays an `ArgumentException`.
  The rule is now `security.md` §"Upload Content Pipeline" Rules ("Every refusal is user-facing").
- **Re-verified after the fix:**
  - the BigTIFF avatar (AuthorBeta) and a non-image cover (AuthorAlpha, story 10) both read "This
    file isn't a JPEG, PNG or WebP image." on both phases (WASM: `POST …/profile-picture` and
    `…/cover` answered 400);
  - `psql`: no avatar or cover written, and the server log has no `fail:`;
  - a valid 64×64 PNG avatar upload still succeeded on WASM: stored `/uploads/users/5/profile-{uuid}.png`,
    served 200 `image/png`, re-encoded (328 → 218 bytes).
- **Tests:**
  - Unit `ImageUploadProcessorTests` (+1, 12) and `ImageUploadProcessorAdvisoryTests`: refusals are
    the typed exception, `ExceptionPresenter` treats them as user-facing, and the text is exact (the
    circuit half). `ImageStorageTelemetryTests`' oversize case asserts the new type.
  - Integration `ImageUploadEndpointsTests` (new, 3): the BigTIFF avatar, an SVG-claimed cover and
    a non-image cover each answer 400 with the readable `Detail` and write nothing (the WASM half).
  - Mutation-checked: putting the `ArgumentException` back at the sniff and the claimed-type check
    fails 5 Unit tests and all 3 Integration tests.

**WU-BlobCleanup Stage note (2026-10-13; owner ruling D14) — L2 remains Stage 5.** The Settled note in
Shared Context, built. Rule of record: `layer2-services.md` §"Image Storage — blob ownership and
cleanup". No migration (only two columns name blobs, and no seed sets either).
- **`EnumerateAsync` (frozen-interface amendment).** New Core `StoredImage(RelativePath,
  LastModifiedUtc)`. `ImageUploadRules.KeyPrefixes` (`stories/`, `users/`) and `IsGeneratedKey`, an
  anchored regex built from the allow-list's extensions so the two cannot drift: the exact `BuildKey`
  shape, the kind matching its folder, a lowercase "D" guid, an `int` owner, `\z` (a trailing newline
  does not match). Local walks the two prefix folders lazily and skips a file that vanished mid-walk;
  S3 pages `ListObjectsV2` per prefix (`S3ImageStorageOptions.ListPageSize`, default 1,000) and guards
  the SDK v4 nulls (`S3Objects`, `IsTruncated`; a null `LastModified` reads as "now", so the grace window
  skips it).
- **`ImageOrphanSweeper` + `ImageOrphanSweepWorker`** (Server/Images/; Program.cs registers both;
  `TestAppFactory` removes the worker). The sweeper reads the references first from the write context
  through `StoryListings`/`Users` with `IgnoreQueryFilters()`, then streams the store: referenced →
  kept; not a generated key → foreign; under 24 h → in grace; otherwise a candidate. A real cycle
  attempts at most 100 deletes (the cap counts attempts, so a failing store is bounded too), logs each
  delete, and counts failures without stopping. Rows naming a missing blob are one Warning (count plus
  ten samples), never acted on. One summary log, a Warning when capped, the root span
  `ImageStorage.OrphanSweep` (`canalave.image.sweep.*` tags) and the counter
  `canalave.image.orphans.deleted`. The worker's first cycle per process is a dry run, set only once a
  dry run has actually run; `ImageStorage:OrphanSweep:Enabled` is read every cycle. A dev server rarely
  lives 24 h, so in dev the sweep effectively only ever dry-runs. **Dev-data note:** the WU12 fixture
  `wwwroot/uploads/stories/999/cover-*.png` (and anything `/dev/wu12/upload-test-image` writes) is
  unreferenced by design, so the first real Local-mode cycle deletes it.
- **Inline sites.** `UserDeletionService` captures the avatar path inside the transaction and deletes it
  after the strategy returns (reset per attempt, so only the committed attempt counts).
  `ResolveWithRemovalAsync(hardDelete: true)` captures a Story's cover before the row delete
  (`HardDeleteOutcome`) and deletes it after the strategy returns, in its own `try` ahead of the
  notifications. Both log a Warning naming the path on failure. Covers of a deleted user's surviving
  stories stay (decision row 25, tracker F22).
- **Cover persistence.** `UploadCoverArtAsync` now loads the story with its listing, gates (an authorless
  story is 403, not 404: service audit §2.12's `(int?)` item, closed), writes the blob, persists the column
  and `LastUpdatedDate`, then deletes the old blob. `RemoveCoverArtAsync` is new
  (`DELETE /api/stories/{id}/cover` → 204, client impl). `CoverArtRelativeUrl` is off the write DTOs,
  the mapper and `UpdateStoryAsync` (whose cover-change delete branch is gone). Detail: `audit/Stories.md`
  F4's Stage note.
- **Doc corrections (D14 required them):** the "Orphan bug (fixed 2026-06-27)" header above is
  rewritten; `layer2-services.md`'s two stale "when S3 lands" sentences, `render-and-layout.md`'s twin,
  `layer5-wasm.md`'s "fix is adding UploadCoverArtAsync" (done since WU-L5Sweep, and it persists now),
  `folder_clusters.md`'s "Groups" consumer (none exists), and `ServerUserSettingsService`'s dead
  §"Image Storage" pointer. `SelectedCoverFile` is a registered retired term.
- **How verified:** build 0 errors, no new warnings in touched files; all three tiers green.
  - **Unit:** `ImageUploadRulesTests` (28: every kind × extension `BuildKey` emits is accepted, 20 near
    misses rejected, the prefixes cover every `ImageKind`) and `LocalImageStorageEnumerationTests` (5:
    `/`-separated paths with last-write times, only the two prefixes, nothing without `uploads/`,
    one-prefix, cancellation). These write files directly and never call `SaveAsync`, because
    `ImageStorageTelemetryTests`' process-wide listener counts `ImageStorage.Save` spans (`testing.md`).
  - **Integration:** `ImageOrphanSweeperTests` (8, each over its own temp store): the query-filter trap
    (taken-down, draft and M-rated covers plus an avatar survive while an orphan in the same run goes),
    the same over a mis-injected read context, grace window, foreign files, a dangling row
    (Warning, column untouched), dry run, the cap (102 orphans → 100 deleted, `CapReached`), failed
    deletes counted. `S3ImageStorageServiceTests` +3 against Garage: saved objects enumerate with a
    last-modified within 5 min, a `sprites/…` object is never listed, `ListPageSize = 1` pages through
    three or more objects. `BlobCleanupTests`, `BlobCleanupGarageTests`, `StoryCoverTests` and the
    endpoint tests: see the F4/F47/F52 Stage notes. `HostBootTests` +1 (the sweeper resolves; the shared
    host runs no sweep worker).
  - **Mutation-checked** (each fails a test, restored after): the reference read through `Stories`
    without `IgnoreQueryFilters`; no grace check; no key-shape guard; no cap; dry run ignored; avatars
    left out of the reference set; no S3 continuation; listing the whole bucket.
  - The worker loop has no automated test, like every other worker, since `TestAppFactory` removes it.
  - **No browser was available to the build:** tracker **H31** holds the pass (the editor's cover flow
    on both render phases; a hard delete's cover URL 404ing on the Aspire/Garage path).

## L3/L3.5/L4 — N/A for this cluster

No component lives in `Images/` — it's a pure service cluster (like Sprites' read half). Upload UI
components belong to their owning feature (`StoryPropertiesForm` for WU24, profile edit form for WU30).

## L5/L6/L8 — N/A (L7 dissolved 2026-07-06)

L5 stays N/A **by design, post-flip**: `IImageStorageService` is one of the deliberate structural
exclusions never client-implemented — upload flows (`InputFile` → `SaveAsync`) run server-side via
the stream/multipart endpoint pattern, not a client service body-swap. See `layer5-wasm.md`
§"Scope Inventory"/"Avoid" and the exclusion comment in `TheCanalaveLibrary.Client/Program.cs`.
(The original WU12-era rationale — "InteractiveServer calls the service directly, endpoint not
designed yet" — predates the Global Flip and is superseded by that recorded exclusion.)
