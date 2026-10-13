# Security Conventions

Authoritative security-hardening rules for The Canalave Library. Established 2026-07-06
(WU-Security + WU-DataProtection); refined from battle-tested reality like `testing.md` /
`logging.md`. The threat model is a public UGC site: **every byte a client sends — headers,
MIME types, file contents, form values, event payloads — is attacker-controllable.** Nothing
the browser claims about itself is trusted; validation happens server-side, at or below the
service layer.

Viewer access gating (the Class-A security-boundary vs Class-B mature-consent split, interstitials,
gated-existence reads) is governed by `.claude/design/access-gating-first-principles.md`
(authoritative model; surface inventory in `access-gating-audit.md`) — this file owns the
transport/hardening half, that model owns which reads a viewer may see at all.

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

Library: **SixLabors.ImageSharp, held at 3.1.12** (Split License — free below the
small-business revenue threshold; a hobby fanfiction site qualifies. Re-check terms if the
project ever takes money beyond donations). **Do not bump to 4.x or 3.2.x without the owner's
call (tracker E9):** both lines run Six Labors' build-time license check
(`$(SixLaborsLicenseKey)` — a Release build fails without one, a Debug build only warns). 3.2.0
(2026-10-09) shipped the 2026-10 security fixes *together with* that check, so the 3.1.x line
gets no more security patches. That fired the old rule's revisit trigger. Until the owner picks
a key, a library swap or standing risk, the five 2026-10 advisories are accepted risk; see
§"Dependency Vulnerability Scan Cadence". Dependabot ignores the major bump
(`.github/dependabot.yml`). Its 3.2.x minor-bump PR fails CI's Release build for want of a key,
which is expected, not a regression. Constants live on `ImageUploadProcessor`.

**Rules:**
- No storage implementation ever writes caller-supplied bytes; both consume
  `ImageUploadProcessor.ProcessAsync(...)` output (bytes + sniffed content type + extension).
- Memory bound: worst-case transient decode is ~256 MB (64 MP × 4 B); the per-user
  `ImageUpload` throttle (below) bounds concurrency. Revisit `MaxSourcePixels` before any
  bulk-import feature.
- SVG stays off the allow-list permanently — SVG is XML that can carry scripts (stored XSS);
  raster only.
- The pipeline decodes and encodes **only** the three allow-listed formats (`SniffConfiguration`
  serves both `Identify` and `Load`), runs no image processor beyond `AutoOrient`/`Resize`, and
  **never reads `IccProfile.Entries`** (the ICC profile passes through as raw bytes). These three
  keep the 2026-10 ImageSharp advisories unreachable on 3.1.12.
  `ImageUploadProcessorAdvisoryTests` guards the TIFF and ICC halves with the advisories' own
  payloads.

## Write Throttling (service layer — the transport-agnostic boundary)

**Why the service layer:** user writes reach L2 services over BOTH transports — the SignalR
circuit (first visit under `InteractiveAuto`, which keeps the circuit path alive permanently)
and per-feature HTTP endpoints (WASM revisits, live since the Global Flip 2026-07-13). HTTP
rate-limiting middleware never sees circuit traffic, so the only single enforcement point
covering every transport is the service method itself. This parallels the authorization rule in `identity-and-authorization.md`
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
| `ContentCreate` | 5 | 1 per 2 min | story / chapter / alternate-version / blog-post / group creates, recommendation submit |
| `ImageUpload` | 10 | 1 per 30 s | `ImageUploadProcessor` (covers covers + avatars through the one seam) |
| `VerificationRequest` | 5 | 1 per 2 min | `ServerExternalVerificationWriteService.SubmitAccountForVerificationAsync` / `RequestLinkVerificationAsync` (Feature 53, WU39 — mod approve/reject never throttled) |

**Rules:**
- The `EnsureAllowed` call goes **immediately after the method's auth guard** (userId is
  non-null there) and before any DB work.
- **Select by cost, not by write-vs-read** (reframed 2026-07-18, BB-02/MA-309 — matches current
  MS rate-limiting guidance, which drives limiter choice from "the resources used: time, data
  access, CPU, I/O"). Two classes need protection:
  - *Abuse-prone writes* (creates content another user sees, or is unbounded): add an
    `EnsureAllowed` call under an existing kind, or add a kind + limit row above.
  - *Expensive authenticated operations that never commit* (file parsing, heavy decode): the
    token bucket never sees them — bound them with an edge **Concurrency Limiter** instead. Live
    instance: the `"ImportParse"` policy (Program.cs) capping the three import parse routes
    (Mammoth DOCX / EPUB decompression / AngleSharp DOM walks, up to 20 MB/file).
- Edits/deletes and bounded toggles are **deliberately unthrottled**: interaction/follow/like
  toggles (UX-hostile to limit; the 2s client debounce absorbs the frequency), vouches / Hidden
  Gems (hard count limits already exist), tag writes (mod-only; HTTP policy covers).
- L5 endpoint contract (live since the Global Flip, 2026-07-13): endpoints translate
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
img-src 'self' data:; font-src 'self' data:; connect-src 'self' ws: wss:; object-src 'none';
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

## Account-Status Enforcement (WU38a + WU-AccountEnforcement)

WU34 modeled `User.AccountStatus` (`Active`/`Warned`/`Suspended`/`Banned`) + `SuspendedUntilUtc` and
wired moderator actions + notifications, but deliberately deferred login-blocking (see
`audit/Moderation.md` Feature 47). WU38a closed that gap. WU-AccountEnforcement (2026-07-30) closed
the remaining mid-session-responsiveness gap described below.

- **Single choke point:** `CanalaveSignInManager : SignInManager<User>` overrides
  `CanSignInAsync(User)` — `Banned` → always `false`; `Suspended` with `SuspendedUntilUtc` still in
  the future → `false`; otherwise defers to `base.CanSignInAsync` (preserves
  `RequireConfirmedAccount`, etc.). Registered via `.AddSignInManager<CanalaveSignInManager>()`
  (last registration for a service type wins, same pattern as
  `ApplicationUserClaimsPrincipalFactory`). Every sign-in path — password, passkey, 2FA,
  external-login — funnels through `SignInManager.PreSignInCheck → CanSignInAsync`, so overriding
  it here covers all of them without touching each page. A blocked attempt surfaces as
  `SignInResult.NotAllowed`; `Login.razor` re-checks the password and, if it was correct, shows the
  specific suspended/banned reason instead of "Invalid login attempt."
- **Expired suspensions are allowed through as-is** — the predicate is read-only (no lazy status
  restore at the choke point); `AccountStatus` stays `Suspended` in the row until a moderator
  changes it, but `CanSignInAsync` stops blocking once `SuspendedUntilUtc` has passed. The moderator
  lever that writes `Active` back is **Reinstate** (`IModerationWriteService.ReinstateUserAsync`,
  WU-ModerationIntegrity 2026-09-30) — the only path out of `Banned`, and the way to clear an expired
  suspension's status.
- **A suspension always carries a future end date (WU-ModerationIntegrity, 2026-09-30; service
  §2.1.3(a)).** Both account-action entry points refuse a Suspend whose `suspendedUntilUtc` is NULL or
  not after now (`ModerationValidationException`). Before this, such a suspension was written verbatim:
  the stamp bump ejected the user, and `CanSignInAsync` (which blocks only a *future, non-null* date)
  let them straight back in. Every non-Suspend action clears `SuspendedUntilUtc` to NULL. The full
  transition table (Banned leavable only via Reinstate; no Warn over a ban or a live suspension) is in
  `layer2-services.md` §"Moderation Services".
- **Active sessions are killed on Suspend/Ban, not on Warn.** `ApplyAccountActionAsync`
  (`ServerModerationWriteService`) calls `UserManager.UpdateSecurityStampAsync` after setting
  `Suspended`/`Banned` — the existing 30-minute stamp revalidation
  (`IdentityRevalidatingAuthenticationStateProvider`) then invalidates the live cookie on its next
  check, and the following request re-authenticates through the now-blocking `CanSignInAsync`. A
  `Warned` user is not logged out — the warning surfaces as a banner, not an ejection. **This
  30-minute ejection window is a deliberate, accepted latency** (settled 2026-07-30, not revisited
  by WU-AccountEnforcement) — server-side enforcement holds throughout it regardless (every
  read/write still goes through `RequireModerator()`/owner checks and the blocked cookie), and the
  banner below now discloses the window rather than leaving it silent.
- **`canalave:account_status`** is baked into auth-cookie claims at sign-in alongside
  `ShowMatureContent`/`Theme`/`PrefersAnimatedSprites` (`ApplicationUserClaimsPrincipalFactory`,
  `SerializeAllClaims = true` already covers WASM). This baked claim is only ever the **first-paint**
  value now — see the live-read mechanism below for how staleness is actually closed.
- **`AccountStatusBanner` is responsive via a live per-navigation read, not the claim.**
  `IAccountStatusReadService.GetMyAccountStatusAsync()` (Identity cluster; server impl queries
  `User.AccountStatus`/`SuspendedUntilUtc` directly, client impl calls `GET /api/account-status`) is
  re-queried on `NavigationManager.LocationChanged`, the same pattern `MessagesNavLink` already used
  for its unread-count badge. **`RefreshSignInAsync` was considered and rejected as the tool here**:
  every existing call site (`ContentGateEndpoints.cs`, the stock Identity `Manage/*` pages) reissues
  the *caller's own* cookie from their own `HttpContext` — a warn/suspend/ban is applied by a
  *moderator*, in a different DI scope and a different circuit, so nothing in the app can reach the
  target's session to reissue its cookie. A live per-navigation read sidesteps the problem entirely:
  `AccountStatus` is display-only (not part of `IActiveUserContext`, not used for query-shaping or
  authorization), so there is no cookie-reissue requirement to satisfy in the first place. The
  banner renders all three non-Active states (Warned/Suspended/Banned) — see
  `content-safety.md` §"Account Actions" for why Suspended/Banned display was brought into scope
  alongside Warned.

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

**Pin rules.** Every explicit pin of a package that would otherwise float transitively carries a
comment in its csproj saying why it exists. A pin in a test project says it is test-only. Two
pins carry extra rules:
- **AngleSharp moves with HtmlSanitizer.** The Server's direct `AngleSharp` reference (used by the
  export/import DOM walks) is also the parser the sanitizer, our trust boundary, runs on. It stays
  inside the range the `HtmlSanitizer` release in use declares (at or above its floor, same
  major), on the newest patched 1.x stable, and never below an advisory's fix. Treat every
  AngleSharp bump as a sanitizer change: it lands only with `HtmlSanitizationServiceTests`
  (including the mXSS canaries) and the Export/Import tests green. A new AngleSharp major waits for
  an HtmlSanitizer release that declares it; the 0.17 → 1.x jump is why. On every bump, also read
  AngleSharp's own repository advisories. They can land before the GitHub-reviewed database that
  the scan reads; GHSA-cgp3 did. Tests.RazorComponents (bUnit) pins the same AngleSharp version.
- **ImageSharp stays at 3.1.12** until the owner rules on tracker E9 (§"Upload Content Pipeline").

**Resolved 2026-10-12 (WU-DependencyCVEs) — AngleSharp 0.17.1 (CVE-2026-54570 /
GHSA-pgww-w46g-26qg, tracker E2).** This was an accepted risk from 2026-07 to 2026-10. AngleSharp
did not treat `<annotation-xml encoding="text/html">` as an HTML integration point, so its DOM
disagreed with a browser's (an mXSS); fixed in 1.5.0. HtmlSanitizer 9.0.892 hard-pinned
`AngleSharp [0.17.1]`. HtmlSanitizer 9.2.1039 accepts `≥ 1.7.2`, so the fix was the zero-code path
this section had pre-authorized: HtmlSanitizer 9.0.892 → 9.2.1039, and the direct AngleSharp
0.17.1 → **1.8.4**. 1.8.4 rather than the floor 1.7.2, for one reason: AngleSharp's own repository
advisory **GHSA-cgp3-27rh-pcp2** (2026-09-19, moderate; SVG `<style>` text serialized raw, another
mXSS) covers 1.3.1–1.8.1 and is fixed in 1.8.2. That advisory is not in the GitHub-reviewed
database yet, so `dotnet list --vulnerable` cannot see it. There was no compile break and no
serializer diff in any tier. The planned "custom AngleSharp-1.5.2 sanitizer" is retired as
unneeded. Through 1.8.4, AngleSharp still writes `<`/`>` raw inside serialized attribute values;
the advisory text names this as an ingredient, but the 1.5.0 fix did not change it. So the
allow-list remains the defence in depth (`layer2-services.md` §"User HTML Is Sanitized Once, On
Save"): it admits no attribute except a scheme-checked `href`, and it stripped the vector on
0.17.1 too. `HtmlSanitizationServiceTests` carries both advisories' payloads, plus one parser
canary per advisory.

**Sweep 2026-10-12 (WU-DependencyCVEs).** The 2026-10-12 build listed these advisories (GitHub
publication dates run from 2026-07-17 to 2026-10-07). They were cleared as follows:

| Package | Scope | Advisories | Resolution |
|---|---|---|---|
| `System.Security.Cryptography.Xml` 10.0.9 | Core, and everything that references it. Legacy `Microsoft.AspNetCore.Identity` 2.3.x pulls it in through `DataProtection` 2.3.0. | GHSA-23rf-6693-g89p, -8q5v-6pqq-x66h, -cvvh-rhrc-wg4q, -g8r8-53c2-pm3f, -mmjf-rqrv-855v (high) | Core's pin raised to 10.0.12 (patched from 10.0.10). The redundant direct pins in SharedUI, Client and AppHost were removed: the first two inherit Core's, and nothing in AppHost's graph uses the package. |
| `SSH.NET` 2025.1.0 | **Test-only**: `Testcontainers` 4.13 in Tests.Integration. Only `ScpClient` is affected, and Testcontainers never uses SCP. | GHSA-mggc-4xg6-vcxf, GHSA-q939-rpr3-3284 (high) | Pinned to 2026.0.0 (no breaking changes listed). |
| `AngleSharp` 0.17.1 / 1.4.0 | Server, plus **test-only** bUnit 2.7.2 in Tests.RazorComponents | GHSA-pgww-w46g-26qg (moderate); the target also had to clear GHSA-cgp3-27rh-pcp2 | Both moved to 1.8.4. See the resolution above. |
| `SixLabors.ImageSharp` 3.1.12 | Server upload pipeline | GHSA-j3p4-wp97-rph4, -j9gm-c75j-xc9q, -jjfr-hcj7-qf5w (high); -gwg2-r3hj-4w44, -wmxv-xphr-5c9g (moderate) | **Accepted risk, pending owner (tracker E9).** See below. |

**Accepted risk — ImageSharp 3.1.12, pending tracker E9.** The fixes ship only in 4.1.2+ and
3.2.0+, and both run the license check (§"Upload Content Pipeline"). On 3.1.12 every vector is
unreachable through `ImageUploadProcessor`, the only ImageSharp consumer:
- **j3p4** (float TIFF + `HistogramEqualization`): no TIFF decoder is configured, and no histogram
  processor is ever called.
- **j9gm, jjfr** (TIFF CCITT T4/T6 *encoders*): the pipeline only encodes JPEG, PNG and WebP.
- **wmxv** (BigTIFF IFD loop): only the TIFF decoder reaches `ReadValues64`. EXIF inside
  JPEG/PNG/WebP takes the classic-TIFF path. Guarded by a Unit test that feeds the advisory's
  24-byte PoC.
- **gwg2** (ICC CLUT allocation): on v1–v3 this is reachable only through the lazy
  `IccProfile.Entries`, which the pipeline never reads. Guarded by a Unit test that feeds the
  advisory's PoC profile and bounds the allocation.

The report-only scan keeps listing ImageSharp until E9 is decided. That is expected, not a
regression.

**The scan cannot see the host runtime.** GHSA-cvvh-rhrc-wg4q also affects the .NET 10 shared
runtime ≤ 10.0.9. A framework-dependent deploy takes whatever runtime its host has, so the host
must run a patched .NET servicing release. This is a Phase-7 register item (below).

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
- **Patched .NET runtime on the host.** The deploy host or base image runs the current .NET 10
  servicing release (≥ 10.0.10 for the 2026-07 runtime advisories) and moves with each Patch
  Tuesday. The package scan cannot see the shared runtime (§"Dependency Vulnerability Scan
  Cadence").
