# Audit — ContentGate/ (Feature 66 — Viewer Access Gating)

**Minted 2026-07-19** from the resolution of `middle_plan_v2.md` decision row 11. Built by
**WU-AccessGate** (Phase 2 item 8; absorbs WU-SeoSite / the Feature 64 build slice — that
feature's own ledger stays `audit/Seo.md`).

## Shared context

The model is fully specified outside this file — do not re-derive it here:

- **`.claude/design/access-gating-first-principles.md`** — the authoritative model: three planes
  (Discovery zero-trace / Direct-navigation consent gate / Personal never-filtered), Class A vs
  Class B enforcement, the Intentionality Doctrine, reveal semantics, all per-surface rulings.
- **`.claude/design/access-gating-audit.md`** — the five-sweep surface inventory (facts,
  file:line). Its Tier-0 list is **reclassified** by the first-principles doc §5 — where they
  disagree, first-principles wins.
- **Conventions:** `canalave-conventions/content-safety.md` §"The Three-Plane Access Model";
  `identity-and-authorization.md` §"Viewer Consent State".
- **Plan of record:** `C:\Users\Brian\.claude\plans\happy-booping-lark.md` (phases 0–5 + browser
  verification matrix).

## Settled vs. open (Feature 66)

**Settled (Brian, 2026-07-19 — do not revisit at build time):**
- Three planes; no "strict no-M" second setting; interstitial = title/author/rating only;
  "View this story"/"View this group" copy (never "work"); durable per-item reveals
  (DB `user_content_reveals` for accounts / 180-day ~50-cap Lax cookie for anon; discard on
  login); reveal management section in `/settings`; `RefreshSignInAsync` for responsive
  `ShowMatureContent`; group reveal covers all group-owned content, member stories gate
  individually; tree-search root honors reveals (results stay ceiling-filtered);
  count-line disclosure on all person/collection-scoped listings, two-step (gated mini-cards →
  story-page interstitial); notifications never rating-filtered; writes stay rating-blind;
  spotlight dedicated M/non-M slot pools with redemption-time slot-class validation;
  profile privacy honest states ("This profile is private" / "Sign in to view this profile";
  private lists stay 404-conflated); sign-in-required page = one component + route→copy map,
  register flows to a welcome page (no returnUrl); index-all / adult labels / robots AI-trainer
  blocking / sitemap includes M; verified-bot serving config-gated off until Phase 7 trust
  boundary; no cookie-consent banner (privacy-policy mention only — row 10 artifact).

**Open (deferred, tracked elsewhere):**
- Interstitial *wording* (willingness assertion text) — interim AO3-style copy ships now; final
  wording + any age-assertion element is a **row 10 counsel item** (with the cookie
  privacy-policy mention and the "substantial portion" documentation).
- `Seo:TrustVerifiedBots` activation — a Phase 7 launch-readiness checklist line (Cloudflare
  ForwardedHeaders/origin-lockdown prerequisite).
- Optional future: hard-mode preference ("M URLs 404 instead of interstitial") — cheap to add at
  the gated-existence read's single decision point if ever demanded; not built.

## Stage notes

### Stage 5 — built + verified (2026-07-23, WU-AccessGate)

All six plan phases landed in one pass; `dotnet test` green: **1955 tests (702 Unit / 510
RazorComponents / 743 Integration — 14 new in `ContentGateTests`), 0 failures.**

**What was built (representative files):**
- Class-A fixes: `ProfileVisibilityGuard` + guards in 7 profile-scoped read services; honest
  profile states (`ProfileAccessState` + `/access-state` read; "This profile is private" /
  "Sign in to view this profile"); anon-tolerant profile-tab endpoints (favorites/by-author/
  tag-selections — the MA-302 split-brain class); `SignInRequired` + `SignInRequiredCopy`
  route→copy map + `Routes.razor` NotAuthorized template + status-code re-execute
  (`/status-code/{0}`, GET/HEAD only via an `IStatusCodePagesFeature` opt-out — POST re-execution
  tripped component antiforgery; and the page re-asserts the original status code because
  component rendering resets to 200); explicit `UseAuthentication`/`UseAuthorization` placement
  (the blank-401 fix); five dead `<NotAuthorized>` blocks deleted; `/welcome` page + ConfirmEmail
  flow; soft-404s → `Nav.NotFound()`; author self-access on story/chapter edit paths (B5);
  group-blog-post permalinks (B7 — `GetByIdAsync` second TPT branch); notification group-name
  lookup normalized to ground truth (Personal plane).
- Consent infra: `user_content_reveals` (polymorphic Story/Group/BlogPost, migration
  `20260724003915_UserContentReveals`); `AnonPrefs` cookie (`canalave.prefs`, Lax, 180d, 50-LRU);
  `IActiveUserContext` DIM additions (`MaxRating`/`IsVerifiedBot`/`HasAnonRevealed`) +
  `ServerActiveUserContext` lazy cookie read; `RevealCheck`; `/content-gate/reveal|mature|
  refresh-claims` endpoints (form POST → 303; `RefreshSignInAsync` closes MA-605 — the settings
  toggle routes through refresh-claims via forceLoad); ceiling derivation centralized (5 chapter
  sites + 2 raw-SQL discovery params → `MaxRating`).
- Gates: gated-existence reads (`GetStoryGateAsync`/`GetGroupGateAsync`/`GetBlogPostGateAsync` →
  `GatedMetadataDto`, IsTakenDown kept active) + reveal-aware detail/chapter/TOC/versions/list/
  export reads; `ContentGateInterstitial` + third render branch on Story/Chapter/Group/BlogPost
  pages (B6 heuristic retired); `AdultContentMetaTags` (rating=adult + RTA) on interstitial AND
  revealed branches; `/gate` endpoints backing the WASM pass; tree-search root honors reveals.
- Personal plane + disclosure: `personalScope` hydration (bookshelves; owner custom-list reads
  elevated server-side); `MatureDisclosureLine` + gated mini-cards on profile story tabs, public
  lists, group story sections, series pages; `GetGatedCardsAsync`/`GetGatedStoriesByAuthorAsync`/
  `GetListHiddenMatureAsync`; spotlight dedicated M/non-M slot pools (`SpotlightSlot.
  MaxStoryRating`, migration `SpotlightSlotRatingClass`, redemption validation — the one
  sanctioned write-path rating check; mod grant checkbox; redemption-page label);
  `IContentRevealService` + `/settings` "Mature content you've revealed" revoke section.
- Feature 64: `/robots.txt` endpoint (AI trainers blocked; absolute Sitemap via
  `IPublicUrlProvider`); `/sitemap.xml` (published incl. M); canonical-slug **301 middleware** +
  `<link rel="canonical">` on StoryPage; `VerifiedBotMiddleware` (`Seo:TrustVerifiedBots`,
  default OFF — activation is a Phase-7 checklist line behind the Cloudflare trust boundary).

**How verified:** Integration tier (`ContentGateTests` — gate reads incl. taken-down-stays-404,
DB/anon/bot reveals, per-story scoping, gated cards, personalScope vs Discovery, Private-profile
guard + access state, blog gate, robots/sitemap/301/gate-endpoint over HTTP; plus the whole
pre-existing suite). Manual L4.5 band (server-only path, seeded DB): curl matrix — anon
interstitial (200, adult labels ×2, body absent), gate JSON (title/author/rating only), detail
endpoint null, stale slug → 301 → canonical, chapter-URL interstitial, encoded prefs-cookie
reveal/mature variants (incl. export 200-with-reveal / 404-without), styled 401 for
`/bookshelves` + `/mod/reports` as anon (real status codes + copy). Browser band (Chrome tools):
full anonymous consent loop (interstitial → "View this story" form POST → 303 → content);
logged-in "Always show mature content" (ReaderGamma DB flag flipped + claim refreshed —
content immediately, NO re-login: MA-605 demonstrably closed); logged-in per-story reveal
(AuthorAlpha → `user_content_reveals` row) + `/settings` revoke (row deleted, psql-confirmed);
authored-tab disclosure line ("2 mature stories aren't shown · show them" → minimal [M] cards;
banner count now consistent). Browser-tool click no-ops observed twice were the documented CDP
transient (JS-dispatched clicks confirmed app correctness).

**Known consistent quirk (recorded, not fixed):** `GetGatedStoriesByAuthorAsync` applies no
story-status filter, matching the visible-id read `GetStoryIdsByAuthorAsync` (pre-existing) — a
pending-approval M story appears in the authored-tab disclosure count exactly as a
pending-approval T story appears in the visible deck. Revisit both together if status-filtering
of authored tabs is ever tightened.

**Tests retired/amended:** none removed — `ContentRatingFilterTests.MatureRatedStory_IsInvisible…`
still passes (it pins raw filter mechanics; service-level policy elevates per-path) and
`BookshelfStoryIdsTests` pins id-collection (hydration, not ids, is where personalScope acts).
