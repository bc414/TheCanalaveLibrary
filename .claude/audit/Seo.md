# Audit — Seo/

**Two-part ownership (revised 2026-07-15).** The already-shipped per-page slice (`IPublicUrlProvider`,
`<SocialMetaTags>` — OG/Twitter/`<meta description>`) is a cross-cutting cluster with no own grid
Feature (same shape as `Images/`, `Errors/`, `Toasts/`) — consumed by every shareable content page
(Stories Feature 4, Chapters Feature 6, Profiles Feature 20, Series, BlogPosts Feature 35, Groups
Feature 38) but owned by none of them. The remaining site-level work below (robots/sitemap/canonical
redirect/`noindex`) is tracked as **Feature 64 — Site SEO** (`grid_axes.md`, `status.md`,
`folder_clusters.md`'s new `Seo/` row) and sequenced as **WU-SeoSite** (`middle_plan_v2.md` Phase 2
item 8, `workplan.md` "Planned / not-yet-built named WUs"). Addendum-sourced:
`.claude/middle-addendum.md` §3 items **#15** (`<meta name="description">`, description shipped;
sitemap/robots half now Feature 64), **#16** (canonical-slug redirect, now Feature 64), **#17** (Open
Graph, shipped), and **#18** (mature-content `noindex`, now Feature 64, gated on decision row 11).

## Shared Context

**Contract (Core/Seo/):** `IPublicUrlProvider` — `string AbsolutePageUrl(string relativePath)` and
`string AbsoluteImageUrl(string? relativePath, string fallbackRelative)`. Both return absolute URLs;
`AbsoluteImageUrl` substitutes `fallbackRelative` (the site-default OG image) when `relativePath` is
null/empty, mirroring the null-cover-fallback discipline `Images/` already established (consuming
components never branch on null themselves).

**Why configured base URLs, not `NavigationManager.BaseUri` (settled 2026-07-11, Brian):** the
deployment target is Cloudflare in front of DigitalOcean droplets, heading toward N≥2. Cloudflare is a
reverse proxy — request-derived URLs depend on `ForwardedHeaders` middleware reading
`X-Forwarded-Proto`/`X-Forwarded-Host` correctly, or an internal `http://`/droplet-host leaks into
`og:image` and crawlers silently reject the card (they do not retry or fall back). `og:url`/`og:image`
are meant to be the one canonical public address of a resource, not whatever host happened to serve a
given request (droplet IP, health-check host, staging). Under N≥2 the public base is the same canonical
domain on every droplet by construction, so a single config value is inherently correct under
horizontal scaling in a way request-derivation is not. See `render-and-layout.md`
§"Social Meta Tags (Open Graph)" for the crawler/prerender framing this pairs with.

**Two settings, both wired now:**
- `Site:PublicBaseUrl` — the canonical site origin, used for `og:url`.
- `ImageStorage:PublicBaseUrl` — the image origin, used for `og:image`; **falls back to the site base
  when unset.** Read eagerly in `Program.cs` like `Sprites:BaseUrl`/`ImageStorage:Provider`.

**Why the image base is a separate setting from day one, even though it equals the site base today
(settled 2026-07-11, Brian):** today every image (`StoryListing.CoverArtRelativeUrl`,
`User.ProfilePictureRelativeUrl`) is served **same-origin through the app** — `<img src="/uploads/…">`
— even in S3/R2 mode, where `Server/Images/ImageEndpoints.cs`'s `/uploads/{**key}` endpoint streams the
bytes from the bucket server-side; the browser never talks to R2/Garage directly (confirmed in code,
2026-07-11). The intent is to eventually move to **direct browser-to-R2** image serving (a public R2
bucket behind a Cloudflare custom domain, e.g. `cdn.thecanalavelibrary.com`), which offloads image
bytes from the app entirely. Wiring `ImageStorage:PublicBaseUrl` as its own setting now — rather than
hardcoding `AbsoluteImageUrl` to reuse the site base — means that future switch is a **config change
only** for `og:image`; no call site changes, because callers already go through
`IPublicUrlProvider.AbsoluteImageUrl`.

**The migration this does NOT do (recorded, not resolved):** `og:image` is not the only place a
relative `/uploads/…` path renders — every `<img src>` across the app (story covers, profile pictures;
sprites are a separate, already-solved system, see `audit/Sprites.md`) is same-origin today, same as
`Images/`'s own "URL conventions" note anticipates ("if a separate CDN subdomain is adopted later, the
swap is at display time — prepend a configured base — not a column rewrite"). To actually offload bytes
via direct-R2, that same display-time base must be prepended to every `<img>` render, not just
`og:image`. This unit does not touch `<img>` rendering. `IPublicUrlProvider.AbsoluteImageUrl` /
`ImageStorage:PublicBaseUrl` is the designated shared seam both OG and that future `<img>`-display work
should read from — do not mint a second config key or resolver when that follow-up unit lands. Also
undecided at that point: CORS/cache headers on the public bucket, and whether `/uploads/{**key}` is
retired or kept as a fallback.

**Consumers:** `<SocialMetaTags>` (SharedUI/Seo/) — one component emitting the full
`<HeadContent>` tag set (description, `og:*` including `og:site_name`, `twitter:card`), parameterized
by `Title`/`Description`/`ImageUrl`/`Url`/`OgType`. Dropped into each shareable page's loaded branch
beside the existing `<PageTitle>` (`StoryPage`, `ChapterReadingPage`, `ProfilePage`, `SeriesPage`,
`BlogPostPage`, `GroupPage`). A pure description-cleaning helper (Core/Seo/) strips HTML and
truncates rich-text fields (falls back through e.g. `ShortDescription` → cleaned `LongDescription`
for stories) to a plain-text blurb.

**Deliberately NOT added: static OG fallback tags in `App.razor`'s `<head>` for non-content pages
(settled 2026-07-11, discovered mid-build).** `<HeadOutlet>`/`<HeadContent>` do not override or
deduplicate against static `<head>` markup by tag name — they only ever *append* additional
elements into the same `<head>`. Static `og:site_name`/`og:type` markup in `App.razor` would
therefore render **alongside**, not instead of, `<SocialMetaTags>`'s own tags on every page that
uses it — two `<meta property="og:site_name">` elements, undefined which one a crawler picks. The
correct fix if a non-content page (home, `/discover`) ever wants OG tags is to give it its own
`<SocialMetaTags>` instance (as every other page does), not a global static default. Non-content
pages simply have no OG tags today — unchanged from before this unit, not a regression.

## Stage note — built (2026-07-11)

`IPublicUrlProvider`/`PublicUrlProvider` (Core/Seo/), `SocialDescriptionHelper` (Core/Seo/), and
`<SocialMetaTags>` (SharedUI/Seo/) landed as designed above. Registered singleton on Server
(`PublicUrlProvider(siteBaseUrl, imageBaseUrl)` from `Site:PublicBaseUrl`/`ImageStorage:PublicBaseUrl`,
same pattern as `OptimisticSpriteReadService`) and scoped on Client (constructed per-circuit from
`NavigationManager.BaseUri`). Wired into all six planned pages
(`StoryPage`/`ChapterReadingPage`/`ProfilePage`/`SeriesPage`/`BlogPostPage`/`GroupPage`); `BlogPostPage`
previously had no `<PageTitle>` at all — added alongside `<SocialMetaTags>` in the same edit rather
than left half-done. `ChapterReadingPage`'s OG cover/description fall back to the parent story via
`IStoryReadService.GetListingsByIdsAsync([StoryId])` (the lightweight listing projection, not the
heavier `GetStoryByIdAsync`), loaded in parallel with the existing TOC/versions calls — no added
round-trip vs. what the page already awaited. `StoryDetailsDTO.Slug` added (projection in
`ServerStoryReadService.GetStoryByIdAsync`) so `og:url` can be canonical.

**Mid-build correction to the plan:** attribute values like
`ImageUrl="@PublicUrlProvider.AbsoluteImageUrl(x, "/img/default-cover.svg")"` don't parse — a C#
string literal inside a double-quoted Razor attribute collides with the attribute's own quotes.
Every OG value is instead computed as a private `@code` property (`OgTitle`/`OgDescription`/
`OgImageUrl`/`OgUrl`) referenced by plain `@PropertyName` in markup — also reads better than
inlining multi-step expressions in the tag itself.

**How verified:**
- **Unit** — `PublicUrlProviderTests` (site/image base split, trailing-slash handling, null/empty
  image fallback, image-base-defaults-to-site-base) and `SocialDescriptionHelperTests` (HTML strip,
  entity decode, whitespace collapse, word-boundary truncation, tags-only → null). `dotnet test`
  TheCanalaveLibrary.Tests.Unit: 609/609 green (24 new).
- **RazorComponents** — `ProfilePageTests` (the only dispatcher among the six with a direct bUnit
  render test) updated to register `IPublicUrlProvider`; full suite 564/564 green, no regressions.
  The other five dispatchers have no direct bUnit render test (too many injected services — same
  reasoning `ProfilePageTests`' own doc comment gives for `ChapterReadingPage`), consistent with
  before this unit.
- **Integration** — full `dotnet test` run green (Testcontainers-Postgres): 516/516, confirming the
  `StoryDetailsDTO.Slug` projection change didn't break existing Story read-service coverage.
- **Manual/L4.5 browser band** — server-only dev path, seeded DB: `curl`'d the **prerendered** HTML
  (no JS, matching what a social crawler actually fetches) for one page of each of the six types.
  All six correctly emit `description`/`og:site_name`/`og:type`/`og:title`/`og:description`/`og:url`/
  `og:image`/`twitter:*`, exactly once each (confirming the App.razor-duplication risk above was
  correctly avoided) — Story (article, real cover, slug-correct `og:url`), Chapter (article, falls
  back to story cover/blurb), Profile (profile, avatar + tagline), Series/Group (website, default
  image, real title/description), BlogPost (article, real cover-less description). One profile-blog
  variant (`blog_post_id=1`, a `group_blog_posts` row) correctly 404'd as "Post not found" on the
  plain `/blog/{id}` route — pre-existing routing behavior, not caused by this change; verified
  against a `profile_blog_posts` id instead.

Cell mapping: this cluster has no grid row (see header). The consuming features' own grid cells
(Stories F4, Chapters F6, Profiles F20, BlogPosts F35, Groups F38) are unaffected — OG tags are
additive `<head>` output, not a change to any of those features' existing Stage-5 behavior.

## Open

- **Default OG image is an SVG re-use, not a proper raster asset.** No-cover/no-avatar/no-image
  content types (Series, BlogPosts, Groups; Chapter falls back to its story's cover which falls
  back to this) fall back to the existing `wwwroot/img/default-cover.svg` — the same placeholder
  `<img>` tags already use. Wired correctly, but social crawlers (Twitter/Facebook in particular)
  often don't rasterize SVG for `og:image`/`twitter:image` reliably — the OG spec and Twitter's
  card docs both assume PNG/JPG/WEBP/GIF. A proper branded raster asset (1200×630, the OG-recommended
  aspect ratio) should replace this before the OG rollout is genuinely launch-ready; not fabricated
  here since it's a design asset, not a code decision.
- Direct-R2/CDN `<img>`-display migration — see "The migration this does NOT do" above.
- Production values for `Site:PublicBaseUrl` (and `ImageStorage:PublicBaseUrl` if/when it diverges)
  are a Phase-7 config/secrets-promotion concern (`middle_plan_v2.md`), not a code gap.

## Feature 64 — Site SEO: settled vs. open (revised 2026-07-19; built by WU-AccessGate)

**Settled (do not revisit at build time):**
- **`noindex` is resolved: never added.** Decision row 11 resolved 2026-07-19 as "index all; gate
  access" — M pages serve a consent interstitial (title/author/rating + `meta rating=adult` + RTA
  labels) which is itself the indexable artifact. The interstitial/labels/reveals work is
  **Feature 66 (Viewer Access Gating)**, ledger `audit/AccessGate.md`; Feature 64 keeps only the
  site-crawlability slice below. Full model: `.claude/design/access-gating-first-principles.md`.
- Scope is exactly `robots.txt` (static; allows search crawlers, disallows named AI-training bots
  per the AO3/Fimfiction/FFN class norm — settled 2026-07-19) + `sitemap.xml` (minimal-API
  endpoint over published-story listings, **including M stories** — index-all) + canonical-slug
  301 redirect (`StoryPage`'s routing check) + `<link rel="canonical">`.
- Adult labels extend `<SocialMetaTags>` or add a sibling head component — do not duplicate the
  head-content mechanism (same append-only `<HeadContent>` constraint documented above for OG).
- `IPublicUrlProvider`/`Site:PublicBaseUrl` is the existing seam for any absolute-URL need
  (sitemap `<loc>` entries) — do not mint a second base-URL resolver.

**Open:** none — WU-AccessGate (`middle_plan_v2.md` Phase 2 item 8) is unblocked end-to-end.

### Feature 64 Stage 5 — built + verified (2026-07-23, WU-AccessGate)

`Server/Seo/SeoEndpoints.cs`: `/robots.txt` endpoint (absolute `Sitemap:` line via
`IPublicUrlProvider` — the reason it's an endpoint, not a wwwroot file; AI-training crawlers
disallowed per class norm; `/api`, `/Account`, `/content-gate`, `/status-code` excluded for all),
`/sitemap.xml` (published stories INCLUDING M — index-all per resolved row 11; elevated read with
IsTakenDown kept; single file, 50k cap noted), and `UseCanonicalStorySlugRedirect` middleware
(true 301 for stale `/story/{id}/{slug}` before rendering; numeric third segments excluded —
chapter routes). `<link rel="canonical">` added to StoryPage's loaded branch. Adult labels
(`SharedUI/Seo/AdultContentMetaTags.razor`, rating=adult + RTA) emitted on M URLs in both
interstitial and revealed branches. `VerifiedBotMiddleware` registered, config-gated OFF
(`Seo:TrustVerifiedBots`) — activation is a Phase-7 launch-readiness line behind the Cloudflare
ForwardedHeaders/origin-lockdown work.

**How verified:** Integration (`ContentGateTests`: robots content incl. AI-bot blocks + sitemap
pointer; sitemap contains an M story; stale slug → 301 with exact canonical Location; gate
endpoint JSON). Manual band: curl of all three surfaces against the seeded server (robots body,
sitemap `<loc>` entries, 301 redirect_url). Feature 64 grid row → 5.
