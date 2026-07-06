# Security Conventions

Authoritative security-hardening rules for The Canalave Library. Established 2026-07-06
(WU-Security + WU-DataProtection); refined from battle-tested reality like `testing.md` /
`logging.md`. The threat model is a public UGC site: **every byte a client sends — headers,
MIME types, file contents, form values, event payloads — is attacker-controllable.** Nothing
the browser claims about itself is trusted; validation happens server-side, at or below the
service layer.

## Upload Content Pipeline (sniff + re-encode)

All user image uploads pass through `ImageUploadProcessor` (`Server/Images/`), the single
shared step both storage implementations call before writing bytes anywhere. The browser's
claimed MIME string is a **fast-fail pre-check only** — the stored format is decided by the
actual bytes. Pipeline, in order:

1. **Claimed-MIME fast-fail** — reject content types outside the allow-list
   (`image/jpeg`, `image/png`, `image/webp` per `ImageUploadRules.AllowedContentTypes`)
   before reading any bytes. Cheap early exit; not a security boundary.
2. **Buffer with hard cap** — copy the (often non-seekable) browser stream into memory,
   aborting past `ImageUploadRules.MaxBytes` (10 MB). This is the *only* size check; it works
   for seekable and non-seekable streams alike. (Closed a WU-Security-era gap:
   `LocalImageStorageService` previously skipped the cap when `Stream.CanSeek` was false —
   which browser streams usually are.)
3. **Magic-byte sniff** — `Image.Identify` on the buffer with only the JPEG/PNG/WebP decoders
   configured. The **sniffed format is authoritative**: a PNG uploaded as `image/jpeg` is
   stored as `.png` with `image/png`. Unidentifiable bytes → rejected.
4. **Header-level bomb guard** — from `Identify` metadata (no pixel decode yet): reject either
   dimension > `MaxSourceDimension` (16 384 px) or total pixels > `MaxSourcePixels` (64 MP).
   A kilobyte-sized file can otherwise decompress to gigabytes of pixel memory.
5. **Decode** — full decode, first frame only (animated WebP flattens to its first frame —
   deliberate; animated avatars/covers are not a supported feature).
6. **Strip metadata** — EXIF, XMP, and IPTC profiles removed (EXIF can carry GPS coordinates —
   privacy issue, not just attack surface). ICC color profile is **kept** (visual fidelity).
7. **Downscale** — longest side > `MaxStoredDimension` (2 048 px) is resized down,
   aspect-preserved. Display never needs more; bounds storage and bandwidth.
8. **Re-encode** in the sniffed format. Re-encoding is the real security payload of the
   pipeline: it proves the file is a genuine image and discards polyglot payloads, trailing
   data, and crafted metadata that survive a signature check alone.

Library: **SixLabors.ImageSharp, pinned to the 3.1.x line** (Split License — free below the
small-business revenue threshold; a hobby fanfiction site qualifies. Re-check terms if the
project ever takes money beyond donations). **Do not bump to 4.x**: v4 switched to a
build-time license-key requirement (`$(SixLaborsLicenseKey)` — the build fails without one);
Dependabot ignores the major bump (`.github/dependabot.yml`). Patch/minor 3.1.x security fixes
still flow. Revisit only if 3.1.x stops receiving security patches. Constants live on
`ImageUploadProcessor`.

**Rules:**
- No storage implementation ever writes caller-supplied bytes; both consume
  `ImageUploadProcessor.ProcessAsync(...)` output (bytes + sniffed content type + extension).
- Memory bound: worst-case transient decode is ~256 MB (64 MP × 4 B); the per-user
  `ImageUpload` throttle (below) bounds concurrency. Revisit `MaxSourcePixels` before any
  bulk-import feature.
- SVG stays off the allow-list permanently — SVG is XML that can carry scripts (stored XSS);
  raster only.

## Write Throttling (service layer — the transport-agnostic boundary)

**Why the service layer:** user writes reach L2 services over the SignalR circuit today
(`InteractiveServer`) and over per-feature HTTP endpoints after the L5 WASM flip — and
`InteractiveAuto` keeps the circuit path alive permanently even post-flip (first visit renders
over the circuit while WASM downloads). HTTP rate-limiting middleware never sees circuit
traffic, so the only single enforcement point covering every transport, present and future, is
the service method itself. This parallels the authorization rule in `cross-cutting.md`
§"Authorization Has Two Enforcement Surfaces": UI affordances are UX, the service is the
boundary.

**Contract (Core/Security/):** `IWriteRateLimitService.EnsureAllowed(WriteActionKind kind,
int userId)` — throws `WriteRateLimitExceededException` (carries a user-ready message with
retry-after seconds) when the per-user token bucket for that action kind is exhausted. No
`System.Threading.RateLimiting` types leak into Core.

**Impl (Server/Security/):** `ServerWriteRateLimitService`, singleton, one
`PartitionedRateLimiter<(int UserId, WriteActionKind Kind)>` of token buckets. Limits:

| Kind | Burst | Sustained | Applied in |
|---|---|---|---|
| `Comment` | 10 | 10/min | `ServerCommentWriteService.Post*CommentAsync` (all four contexts) |
| `Message` | 10 | 10/min | `ServerMessagingWriteService.StartConversationAsync` / `SendMessageAsync` |
| `Report` | 5 | 1 per 3 min | `ServerModerationWriteService.SubmitReportAsync` (reports only — mod actions never throttled) |
| `ContentCreate` | 5 | 1 per 2 min | story / chapter / alternate-version / blog-post creates, recommendation submit |
| `ImageUpload` | 10 | 1 per 30 s | `ImageUploadProcessor` (covers covers + avatars through the one seam) |

**Rules:**
- The `EnsureAllowed` call goes **immediately after the method's auth guard** (userId is
  non-null there) and before any DB work.
- Every *new* abuse-prone write method (creates content another user sees, or is unbounded)
  adds a call under an existing kind, or adds a kind + limit row here. Edits/deletes and
  bounded toggles are **deliberately unthrottled**: interaction/follow/like toggles
  (UX-hostile to limit; L7 write-behind absorbs the frequency), vouches / Hidden Gems (hard
  count limits already exist), tag writes (mod-only; HTTP policy covers).
- L5 endpoint contract (applies at the WASM flip): endpoints translate
  `WriteRateLimitExceededException` → **429** with `Retry-After`, joining `layer5-wasm.md`'s
  error-translation table.
- Tests: `TestAppFactory` registers a pass-through `FakeWriteRateLimitService` by default
  (existing integration tests legitimately hammer write services in loops); throttle-behavior
  tests re-register the real service with a tightened limits table.

## HTTP Edge Rate Limiting

`AddRateLimiter`/`UseRateLimiter` covers the surfaces that are plain HTTP today and stay
that way:

- **Auth form posts** (`POST /Account/*` — login, register, forgot/reset password, resend
  confirmation): global partitioned limiter, **per-IP fixed window 10/min**, `QueueLimit = 0`.
  Brute-force / credential-stuffing damping. Complements (does not replace) Identity
  **lockout**, which is per-*account* — the two defend different axes.
- **Tag write API** (`POST/PUT/DELETE /api/tags*`): named policy `"TagWrites"`, 30/min
  partitioned by authenticated user name (IP fallback).

**Rules:**
- Rejections are **429 with a `Retry-After` header and a small plain-text body**. The body is
  load-bearing: a body-less error response gets re-executed into `/not-found` by
  `UseStatusCodePagesWithReExecute` (same trap documented in `TagEndpoints.cs`).
- `app.UseRateLimiter()` sits after `UseStaticFiles()` (static assets exempt) and before
  `UseAntiforgery()`.
- **Per-IP partitioning is meaningless in production until the Phase 7 ForwardedHeaders work
  lands** — behind Cloudflare, `RemoteIpAddress` is a Cloudflare IP for everyone. The limiter
  is correct as written for dev/tests and becomes correct in prod only together with the
  `CF-Connecting-IP` / `KnownNetworks` configuration (see Phase-7 register below). Do not
  tighten per-IP limits to compensate before then.

## Response Headers & CSP

`SecurityHeadersMiddleware` (`Server/Security/`, wired immediately after
`UseHttpsRedirection()`) adds to every response:

| Header | Value | Defends against |
|---|---|---|
| `X-Content-Type-Options` | `nosniff` | Browsers MIME-guessing uploads into HTML/JS |
| `X-Frame-Options` | `DENY` | Clickjacking (legacy browsers) |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | URL leakage to third parties |
| `Permissions-Policy` | `camera=(), microphone=(), geolocation=()` | Unused browser APIs |
| `Cross-Origin-Opener-Policy` | `same-origin` | Cross-window scripting |
| `Content-Security-Policy` | see below | XSS execution even after sanitizer bypass |

CSP directives (built by the pure static `CspPolicy` so unit tests cover the string):
`default-src 'self'; script-src 'self' 'wasm-unsafe-eval' 'nonce-{per-request}'
https://cdn.jsdelivr.net; style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net;
img-src 'self' data:; font-src 'self'; connect-src 'self' ws: wss:; object-src 'none';
base-uri 'self'; form-action 'self'; frame-ancestors 'none'`.

Blazor-specific notes: `wasm-unsafe-eval` is required by the Blazor runtime (both render
modes); `ws:/wss:` in `connect-src` is the SignalR circuit; the nonce authorizes the inline
`<script type="importmap">` that `<ImportMap/>` renders (nonce generated per request by the
middleware, exposed via `HttpContext.Items`, consumed in `App.razor`);
`AddInteractiveServerRenderMode(o => o.ContentSecurityFrameAncestorsPolicy = "'none'")` keeps
the WebSocket-compression frame-ancestors setting consistent with the header.

**Environment gating:** the full policy is enforced `Content-Security-Policy` outside
Development and `Content-Security-Policy-Report-Only` in Development (dev tooling injects
scripts; violations surface in the browser console without breaking anything, and integration
tests — which run Development — assert the Report-Only header). One enforced CSP header exists
even in Development: the framework itself emits `Content-Security-Policy: frame-ancestors
'none'` from the `ContentSecurityFrameAncestorsPolicy` option — expected, and it must never
grow script directives. All non-CSP headers are enforced in every environment.

**Standing rules:**
- **No new raw inline `onerror=`/`onclick=`/`on*=` attributes in markup** — they are inline
  script under CSP. Image fallbacks use the delegated pattern: `data-fallback-src` (swap src
  once), `data-hide-on-error` (hide element), `data-sprite-fallback` (sprite chain via
  `spriteFallback`), handled by `SharedUI/wwwroot/js/img-fallback.js`'s capture-phase error
  listener. Blazor `@on*` handlers are server-dispatched and unaffected.
- Any CDN `<script>`/`<link>` carries **SRI** (`integrity` + `crossorigin`) and its origin is
  listed in the relevant CSP directive. Current CDN surface: Quill via `cdn.jsdelivr.net`.
- CSP changes are verified by browsing the report-only console before enforcement changes ship
  (see `run-server/SKILL.md` for the drive-the-UI mechanics).

## Identity Hardening

- **Lockout on:** `Lockout.MaxFailedAccessAttempts = 5`, `DefaultLockoutTimeSpan = 15 min`,
  `AllowedForNewUsers = true`; `Login.razor` calls `PasswordSignInAsync(...,
  lockoutOnFailure: true)` (the `IsLockedOut` branch already routes to `/Account/Lockout`).
- **Cookie flags explicit** (`ConfigureApplicationCookie`): `HttpOnly = true`,
  `SecurePolicy = Always`, `SameSite = Lax`. These match framework defaults today — set
  explicitly so the posture is self-documenting and survives framework-default drift.
- **Open redirects:** `IdentityRedirectManager` only redirects to relative/local URIs — any
  `ReturnUrl` flowing into a redirect goes through it (or `LocalRedirect`), never raw
  `NavigationManager.NavigateTo(userSuppliedUrl)`.
- `RequireConfirmedAccount = true` stands; real activation email is WU-Email's deliverable
  (`IdentityNoOpEmailSender` is the current placeholder).

## Data Protection Keyring

Auth cookies and antiforgery tokens are encrypted with the Data Protection key ring. Unpersisted
keys die with the process → every deploy logs out every user and breaks every open form.

**Rule:** `AddDataProtection().PersistKeysToDbContext<ApplicationDbContext>()
.SetApplicationName("TheCanalaveLibrary")`. Keys live in the `data_protection_keys` table
(`ApplicationDbContext : IDataProtectionKeyContext`; migrations target `ApplicationDbContext`
as always). `SetApplicationName` is mandatory — without it key isolation derives from the
content-root path and a moved deploy directory silently invalidates every cookie.

**Deliberate trade-off — no `ProtectKeysWith*`:** keys sit unencrypted in the app's own
Postgres. On Linux there is no DPAPI; the realistic alternative (self-managed certificate)
adds key-management burden against a threat — DB-read access — that already implies full
compromise of everything the keys protect. Accepted for a single-app deployment whose DB
backups live with the DB. Revisit only if backups ever land somewhere less trusted than the
database itself.

**Operational notes:** default 90-day key lifetime stands; rotation is automatic and old keys
are retained — **never delete rows from `data_protection_keys`** (deleting = permanent loss of
everything they protect). Respawn ignores the table in integration tests (`PostgresFixture`).
Shipping this change produced exactly one global sign-out (new key ring + app name), expected
and pre-beta-harmless.

## Dependency Vulnerability Scan Cadence

`dotnet list package --vulnerable --include-transitive` runs on every CI run
(`.github/workflows/ci.yml`), **report-only** (`continue-on-error`) — rationale in
`middle_plan_v2.md`'s CI-hardening Resolved entry. Dependabot supplies the bump PRs; CI vets
them. Promotion to a hard gate is a Phase 7 launch-readiness checklist item (a known-vuln
dependency becomes launch-blocking once real users exist).

## Phase-7 Deferred Register (launch-readiness picks these up)

Security items that only make sense at deployment — deliberately **not** built in WU-Security:

- **Cloudflare SSL/TLS mode Full (Strict)** — edge-to-origin hop encrypted and validated.
- **Origin firewall restricted to Cloudflare IP ranges** — otherwise attackers bypass every
  edge protection by hitting the droplet directly.
- **ForwardedHeaders** — honor `CF-Connecting-IP` with `KnownNetworks` = Cloudflare ranges
  only. Prerequisite for per-IP rate limits and IP logging to mean anything in prod (see HTTP
  Edge section above).
- **Serve user uploads from a separate origin** (R2 public domain / CDN host) — defangs any
  stored-XSS-via-upload residue by moving user bytes off the cookie-bearing origin. Then
  tighten CSP `img-src` from `'self' data:` to the specific host.
- **Turnstile (or equivalent) on registration** — spam-bot damping; pairs with WU-Email.
- **HSTS tuning** — raise `max-age` toward 1 year + `includeSubDomains` once the domain is
  stable; `preload` only when certain.
- **CSP enforce verification against the real domain** — one browse pass with enforcement on
  in the production topology before launch.
