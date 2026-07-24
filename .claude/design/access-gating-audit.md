# Access-Gating & Anonymous-Viewer Surface Audit

> **Read `access-gating-first-principles.md` after this.** It answers Brian's first-principles
> challenges to this audit's framing and **reclassifies the Tier-0 list** (most L-items are
> downgraded to non-issues under the Intentionality Doctrine; the Class-A items stand). Where
> the two documents disagree, the first-principles document wins.

**Written 2026-07-19.** Five parallel code sweeps (identity/filter infrastructure; story-family
surfaces; interaction/library/profile surfaces; group/blog/comment/discovery surfaces; full
route-and-auth taxonomy) synthesized into one analysis. Purpose: define **one work unit**
(proposed: **WU-AccessGate**, new Feature 66) covering every code path that must change for the
mature-content interstitial + anonymous-preference + verified-crawler model to land coherently —
plus the defects the sweeps found that must be fixed regardless of any open decision.

**Inputs already settled (2026-07-19, Brian, in chat — formal doc reconciliation rides this WU):**
- **No `noindex` on M content — index-all.** The entire content class (AO3, Fimfiction, FFN in
  practice, Steam, Literotica) indexes mature/explicit content and gates *access*. Verdict closes
  `middle_plan_v2.md` decision row 11.
- **M = explicit.** `Rating { E, T, M }`; M includes explicit sexual content; E/T functionally
  identical. Recorded in `content-safety.md` §"Mature-Content Design Philosophy".
- **Zero-trace is a browsing rule, not an existence rule.** Internal listings/feeds/search keep
  zero-trace for mature-off viewers; a *deliberate direct navigation* to an M URL gets a gated
  interstitial (existence acknowledged, content withheld); external indexing is SafeSearch-scoped
  via adult labels (`<meta name="rating" content="adult">` + RTA).
- **Interim gate wording:** AO3-style willingness assertion; exact wording / age-assertion posture
  confirmed with counsel pre-launch (decision row 10, `middle-addendum.md` §3 #2).
- **Pattern B** (verified crawlers served full content) is the target; Pattern A (crawler sees the
  interstitial, which carries metadata + adult labels) is the interim behavior until the
  Cloudflare trust boundary exists (see §6).

---

## 1. Tier-0 findings — defects that stand regardless of any model decision

The sweeps found the settled zero-trace rule and the privacy model are **already violated today**.
These fixes have no dependency on any open decision and form the WU's first slice.

### 1a. Content-rating leaks (anonymous-reachable today)

| # | Leak | Where | Fix shape |
|---|------|-------|-----------|
| L1 | **Full comment text under M-story chapters** — widest leak; comment text routinely quotes the fiction; chapter ids are guessable ints | `GET /api/comments/chapter/{id}` → `ServerCommentReadService.cs:25-89` (never joins Story) | Join parent chain through filtered `Stories` |
| L2 | **Hidden M-audience group's full comment text** | `GET /api/comments/group/{id}` → `ServerCommentReadService.cs:91-147` | Parent-group visibility check |
| L3 | **Hidden M-audience group's member list** (usernames, avatars, roles, join dates, count) | `GET /api/groups/{groupId}/members` → `ServerGroupReadService.cs:119-146` | Parent-group visibility check |
| L4 | **Hidden M-audience group's blog posts** (titles + snippets) | `GET /api/blog-posts/by-group/{groupId}` → `ServerBlogPostReadService.cs:151-182` | Parent-group visibility check |
| L5 | **Arc titles + chapter ranges of hidden M stories** | `GET /api/story-arcs/by-story/{id}` → `ServerStoryArcReadService.cs:16-25` (no filter at all) | Ceiling join through `Stories` |
| L6 | **M titles + links in notifications to mature-off recipients** — reachable now via `StoryLineageRequested`/`RecommendationSpotlighted`; becomes Fimfiction-#365 at scale once follow-driven types land | `ServerNotificationReadService.cs:292-312` (enriches from unfiltered `StoryListings`/`Chapters`), `:336-348` (unceilinged `GroupBlogPosts`; inconsistent with its filtered Group lookup at `:325-334`) | Join enrichment through filtered `Stories`; presenter already handles null titles gracefully (`NotificationPresenter.cs:117-118`) |
| L7 | **View-count existence oracle for hidden M ids** | `GET /api/stories/{id}/total-views` → `ServerStoryReadService.cs:218-230` (raw SQL, no filter) | Ceiling check before query |
| L8 | **Raw M story ids in payloads**: `SeriesDetailDto.OrderedStoryIds` (`ServerSeriesReadService.cs:44-49`), `GroupDetailDto.StoryIds` + folder `StoryIds` (`ServerGroupReadService.cs:81-83,173`) | as cited | Filter id projections; ids become *meaningful* interstitial URLs under the new model |

### 1b. Privacy-model gap

**`ProfileVisibility` is enforced only on header + bio** (`ServerUserProfileReadService.cs:50-61,
132-141` — the MA-602 pattern, correctly server-derived). Every profile-tab API is independently
reachable and ignores it: `/api/following/{userId}`, `/api/following/vouches/outgoing/{userId}`,
`/api/series/by-author/{id}`, `/api/blog-posts/by-author/{id}`,
`/api/recommendations/by-user/{id}/story-ids`, `/api/custom-lists/public/{userId}`,
`/api/comments/profile/{userId}`. A "Private — only you" profile's following list, vouches,
series, published blogs, public lists, recommendations, and comment wall are all readable by
direct HTTP. Fix: a shared profile-visibility guard consulted by all seven read services (or at
their endpoints).

### 1c. Bugs and broken UX (fix in the same slice)

| # | Defect | Where |
|---|--------|-------|
| B1 | **Blank bare-401 page** is the anon experience for ~30 `[Authorize]` routes on full-document load — auto-inserted auth middleware short-circuits *before* `UseStatusCodePagesWithReExecute` (`Program.cs:118-127` vs `:504`); observed live (`audit/Identity.md:168-169`) | Fix globally: explicit `UseAuthentication`/`UseAuthorization` placement or `IAuthorizationMiddlewareResultHandler`; styled sign-in page with return URL |
| B2 | **No `NotAuthorized` template** on `AuthorizeRouteView` (`Client\Routes.razor:23`) → bare framework text on SPA nav; plus five pages carry *unreachable* friendly `<NotAuthorized>` markup (`BlogPostEditorPage.razor:101`, `GroupCreateEditPage.razor:105`, `GroupBlogPostEditorPage.razor:68`, `SeriesCreateEditPage.razor:163`, `MyStoryLineagesPage.razor:169`) | Add template; delete dead markup |
| B3 | **Profile tabs 401-crash for anon on the WASM pass** (InteractiveAuto split-brain): Favorites / Stories / Tag Selections tabs call auth-gated endpoints (`UserStoryInteractionEndpoints.cs:39`, `StoryEndpoints.cs:99-103`, `SavedTagSelectionEndpoints.cs:24-40`) from the `[AllowAnonymous]` profile page — server circuit succeeds, WASM revisit throws into the error boundary. Recurring MA-302 class | Anon-tolerant endpoints or public variants |
| B4 | **Soft-404s**: GroupPage (`:28-32`), BlogPostPage (`:24-27`), CustomListPage (`:29-35`), ProfilePage (`:45-49`) render inline "not found" with HTTP 200 — never hit the styled `/not-found`, and are indexable as soft-404s. Story/Chapter/Series/TreeSearch correctly `Nav.NotFound()` | Normalize per taxonomy (§3) |
| B5 | **Author lockout**: a mature-off author's own M story 404s on read (`ServerStoryReadService.cs:113-115`) and edit (`ServerChapterReadService.cs:271-275` documents the gap) while `by-author` elevates (`:238-240`) — their own profile lists a story whose link 404s for them; pinned as *intended* by `ContentRatingFilterTests.cs:175-188` | Owner-elevated read/edit; retire the pinning test |
| B6 | **Versioned-chapter gate heuristic misfires** (`ChapterReadingPage.razor:291`): any nonexistent `/story/x/y/z` shows "requires mature content" — false existence signal on arbitrary URLs; also its "read the default version" link 404s for M stories | Replaced by the real gated-existence read (§4) |
| B7 | **Group blog post permalinks 404 for everyone** — `BlogPostCard.razor:17` links `/blog/{id}` but `GetByIdAsync` reads `ProfileBlogPosts` only (`ServerBlogPostReadService.cs:30-32`) | Build the group-post detail read (needs the group-audience gate) |
| B8 | Minor: `/discover/me` anon prompt has no login link (`TreeSearchPage.razor:39-44`); `/Account/AccessDenied` orphaned (`Program.cs:123-127`); `GroupPage.razor:403-409` hides M-folder rating badge uncommented |

**Positive patterns to preserve:** MA-602 server-derived `includePrivate`; export's
"permission = readability → 404" model (`ExportEndpoints.cs:9-11`); deliberate
missing-vs-hidden indistinguishability for lists/profiles; MA-702 edge+service moderation
defense-in-depth; dev endpoints unmapped in production.

---

## 2. How the plumbing actually works (constraints for the design)

From the infrastructure sweep — these five facts shape every design choice below:

1. **`ShowMatureContent` is a claim minted at sign-in** (`ApplicationUserClaimsPrincipalFactory.cs:52`)
   and read via a per-scope, lazily-cached principal (`ServerActiveUserContext.cs:43-58`). The
   **MA-605 staleness problem** (settings change → no `RefreshSignInAsync` → takes effect at next
   login) is a live, deliberately-open item that this WU **forces**: the interstitial's "always
   show mature" action would otherwise write the DB and immediately re-show the interstitial.
2. **EF filters re-evaluate per query execution** (`ReadOnlyApplicationDbContext.cs:31-43`), and
   read services create short-lived contexts per method — so anything that changes what
   `IActiveUserContext` reports flows into queries automatically. The bottleneck is the claims
   source and per-scope cache, not EF. (Proven by the mutable `FakeActiveUserContext` singleton in
   integration tests.)
3. **Enforcement always runs server-side with an HttpContext available** — SSR/prerender pass,
   minimal-API request, or the circuit-connect request (`ServerActiveUserContext.cs:76-91`);
   crawlers only ever see the prerender pass (`Client\Program.cs:32-33`). A **cookie is the only
   preference/reveal transport that reaches the enforcement point in all three render modes** —
   circuit-scoped state misses export GETs (plain anchors, `StoryDownloadLinks.razor:19`) and
   dies on the WASM flip; there is no `ProtectedBrowserStorage` precedent in the solution.
4. **`[PersistentState]` embeds loaded DTOs in the prerendered HTML** (`StoryPage.razor:216-217`,
   `ChapterReadingPage.razor:163-168`) — the withhold must gate the **fetch**, never the render
   branch. CSS/JS-hiding shipped content is structurally impossible to do safely here.
5. **"Ignore the Story filter" ≠ reveal.** Five manual ceiling computations in
   `ServerChapterReadService` (`:20,85,113,140,239`) plus raw-SQL `maxRating` params in
   co-occurrence and tree search (`ServerCoOccurrenceReadService.cs:87`,
   `ServerTreeSearchReadService.cs:128`) sit outside the named filter. A reveal must raise the
   **effective ceiling in the viewer context** so all readers agree, not sprinkle
   `IgnoreQueryFilters` calls.
   Also: the `GroupAudience` filter keys off the same setting — an anon mature cookie widens
   group visibility too (intended, but must be stated).

---

## 3. The viewer-permission taxonomy (proposed)

One rule set for every "viewer is not permitted" outcome, replacing today's four inconsistent
renderings (blank 401 / bare text / soft-404 / silent real-404):

| Class | Condition | Outcome | Status |
|-------|-----------|---------|--------|
| **T1 Absent** | Row doesn't exist, or is taken down (takedown stays indistinguishable from absent) | Styled NotFound page | real 404 |
| **T2 Rating-gated** | Content exists; viewer's effective ceiling < content rating; deliberate direct navigation | **Mature interstitial**: metadata + willingness assertion + reveal actions; body absent from HTML | 200 (it *is* the indexable page) + adult labels |
| **T3 Privacy-gated** | Private profile / private list, non-owner | Privacy-preserving not-found — indistinguishable from absent (existing deliberate design, kept) | real 404 (fixes soft-404 indexability) |
| **T4 Auth-required** | `[Authorize]` personal surface, anonymous | Styled "sign in to continue" page with return-URL login link | 401 semantics, styled |
| **T5 Role-gated** | Mod surface, authenticated non-mod | Styled "not authorized" | 403, styled |
| **T6 Verified crawler** | Verified bot on a T2 page | Full content + adult labels (Pattern B; §6) | 200 |

`UsersOnly` profiles: recommend a T4-style "log in to view this profile" page rather than T3 —
logging in already distinguishes UsersOnly from absent, so the prompt leaks nothing new and is
actionable (Decision D10).

Zero-trace surfaces (listings, search, feeds, discovery, lineage boxes, tree-search hops) are
**not** in this taxonomy — they keep silent thinning. Discovery was audited clean: marts store
ids only and every read joins the live filtered story tables at presentation time.

---

## 4. The gate mechanism (story/chapter first, the template for the rest)

**Gated-existence read.** New read returning a discriminated result —
`Visible(dto) | GatedMature(metadata) | NotFound` — implemented with
`IgnoreQueryFilters(["ContentRating"])` while keeping `"IsTakenDown"` active (taken-down stays
T1). Metadata projection: title, author, rating, cover, slug, chapter count (+ short description
per D3). Precedent: the `// elevated read:` convention (`ServerStoryReadService.cs:238-240`,
`content-safety.md` §Elevated reads). This retires the null-conflation (`GetStoryByIdAsync` →
null for both absent and gated) and the B6 heuristic.

**Interstitial component** (SharedUI, new `ContentGate/` cluster): rating statement + interim
willingness assertion + actions:
1. **View this work** — grants a reveal (semantics per D1), then full-document navigation back to
   the same URL so the server re-renders with content (required anyway: circuit-frozen viewer
   state, §2.1–2.3).
2. **Always show mature content** — logged-in: DB write + **claim refresh** (resolves MA-605 via a
   server endpoint doing `RefreshSignInAsync` + redirect); anonymous: sets the mature cookie.
3. Leave (back/home).

**Chapter pages** share the story's reveal (one grant covers the work: story page, all chapters,
TOC, versions). The reveal raises the effective ceiling for that story id in the viewer context,
so all five manual chapter-ceiling sites and the TOC/version reads honor it uniformly.

**Adult labels.** New head emission (extend `<SocialMetaTags>` with an adult flag or a sibling
component — same append-only `<HeadContent>` constraint, `audit/Seo.md`): `<meta name="rating"
content="adult">` + RTA tag, emitted **unconditionally on M-work URLs** (interstitial and revealed
branches alike). Today there is no emission point at all — M pages 404 and emit nothing.

**View-ping:** interstitial impression does not count as a view; only a revealed load pings
(D13; `StoryPage.razor:371-391`).

---

## 5. Anonymous-preference cookie (the Model-B design)

New mechanism class for this codebase (no anon-preference precedent exists; nearest patterns:
the flash-message cookie `IdentityRedirectManager.cs:9-17` for writing, `DraftStore.cs` for JS
storage — the latter can't reach server enforcement and is rejected for this).

- **One JSON cookie** (e.g. `canalave.prefs`): `{ mature: bool, revealed: [storyIds…] }` —
  single mechanism serves both the global anon toggle and anon reveal grants.
- **Not HttpOnly** (contents are non-sensitive preferences; WASM parity may read it or receive it
  via `PersistentComponentState` — the auth-state channel can't carry anon data, and
  `WasmActiveUserContext` resolution is synchronous, so the prerender-handoff route fits best).
- **SameSite=Lax** — Strict would drop the cookie on cross-site top-level navigations, i.e.
  exactly the inbound Discord/search link the model is designed for, re-showing the interstitial
  to an already-consented anon reader.
- **Persistent** (e.g. 180 days) if D1 lands on durable consent; session-scoped if transient.
  Revealed-id list capped (~50, LRU) to bound cookie size. Essential/functional (no consent
  banner implications).
- **Server read**: `ServerActiveUserContext` consults the cookie when `!IsAuthenticated`, with the
  same lazy-once caching as `_principal` (the documented `SecurityStampValidator` hazard means no
  constructor reads). Logged-in users never read it (DB/claims are truth; D14: anon reveals do
  not migrate on login).
- **Extensibility note:** the cookie is deliberately a general anon-preferences envelope — future
  anon reader settings/theme could ride it — but this WU ships only `mature` + `revealed`.

For **logged-in users**, reveal storage per D1: durable → a small `user_revealed_works` table (or
jsonb set on User) consulted by the viewer context; transient → server session state keyed by
circuit + a short-lived cookie for the export path.

---

## 6. Verified-crawler serving (Pattern B) — build now, activate at the trust boundary

- `VerifiedBotMiddleware` stamping `HttpContext.Items` (pattern: `SecurityHeadersMiddleware.cs:22-25`),
  surfaced as `IsVerifiedBot` on the viewer context; T2 pages serve full content + labels when set.
- **Config-gated off by default** (`Seo:TrustVerifiedBots`): no ForwardedHeaders/Cloudflare trust
  boundary exists yet (`Program.cs:189-192`, deferred to Phase 7) — until origin lockdown, any
  direct-to-origin request could spoof the header. **Interim behavior is Pattern A**: crawlers get
  the interstitial page, which carries the indexable metadata + adult labels. Flip the flag when
  the Phase 7 trust boundary lands. This resolves the spoofability risk by sequencing, not by
  extra machinery.
- Scope: story/chapter (and other T2) **pages only** — never export files, never JSON APIs.
- Cross-engine: Cloudflare Verified Bots covers Google + Bing (Bing feeds DDG/Yahoo/Ecosia);
  aligned with the emerging Web Bot Auth standard. Serving verified crawlers what a consenting
  human sees, plus labels, is sanctioned by Google's explicit-content guidance and is not
  cloaking (bot view ≡ permitted-human view).

## 7. SEO slice (rides in the same WU)

`robots.txt` (static; `Sitemap:` line; AI-bot stance per D16) · `sitemap.xml` minimal-API endpoint
over published stories **including M URLs** (index-all), `<loc>` via the existing
`IPublicUrlProvider` seam · canonical-slug **301 redirect** on StoryPage (spec'd, never built —
slug is cosmetic today, `StoryDetailsDTO.cs:31-35`) **plus `<link rel="canonical">`** (absent
everywhere) · T3/T4 status-code fixes above double as soft-404 SEO hygiene.

---

## 8. Ripple surfaces — what the reveal model touches beyond the gate

The interaction sweep's central finding: **write paths are uniformly rating-blind by documented
design** ("a user adds from a story page they could open" — `ServerCustomListWriteService.cs:82-86`,
`ServerRecommendationWriteService.cs:45-48`), which is already false via direct API and becomes
*officially* false — or officially *true again* — depending on D1. Today this yields **ghost
rows**: a mature-off user's M favorites/list-entries/reading-progress are invisible and
un-deletable (the story page — the only clearing UI — 404s) while rating-blind `UserStat`
counters keep counting them (banner-vs-tab mismatches live now, `UserStatsBlock.razor:9,14,18-19`).

| Surface | Today | Under durable-consent (D1 rec) |
|---------|-------|-------------------------------|
| Bookshelves/library | Silent thinning (pinned by `BookshelfStoryIdsTests.cs:178-185`) | Owned-interaction reads include consented works (mirrors the existing MyStories owner elevation) |
| Reading history / continue-reading | Revealed-then-read M story vanishes from ActivelyReading — the most user-hostile trap | Consented works persist in history surfaces |
| Custom lists | Owner can't see/manage own M entries; public counts silently deflate; **CloneListAsync permanently destroys rating-hidden entries** (`ServerCustomListWriteService.cs:133-144`, settled 2026-07-13) | Owner-elevated management; optional "K mature items hidden" disclosure (D7); clone semantics revisited (D8) |
| Profile authored tab | M-only author = "No stories written yet." to mature-off/anon while banner says N — **severs the discovery bridge** | Redacted M-work cards with reveal affordance (D6) — literally "gate the content, not the discovery" |
| Recommendations | Mature-off user can recommend an M work (public statement); hidden-gem M slots un-manageable | Submit posture + gem management per D7/D9 discussion |
| Notifications | L6 fix (Tier 0) + policy for future follow-driven fan-out (D9) | Redaction is nearly free (presenter null-title paths) |
| Spotlight | M story can hold a slot (`ServerSpotlightWriteService.cs:83-95`, no rating check) then silently vanishes from the anon-majority homepage — paid slot devalued, no recorded decision | Gated spotlight card / block-at-redemption / status quo (D5) |
| Series | Unrated container; renders always; empty-shell copy misleads; raw counts accepted by WU41 | Copy fix; L8 id filtering; card counts revisit optional |
| M-audience groups | Silent 404 + porous child APIs (L2-L4) | Group interstitial (audience gate, one reveal per group) per D4 |
| Blog posts (M) | Silent soft-404 | Interstitial or defer (D4); B7 forces the group-post read either way |
| Export | Cleanly gated 404; **bypasses circuit** | Reveal must ride the cookie (D11) or stay T-capped |

**Two inconsistent filtering idioms** (join-at-id-collection vs filter-at-hydration) should be
named in `content-safety.md` when this lands — the hydration idiom leaves raw M ids in payloads
(L8) and is the latent-leak generator.

---

## 9. Decisions

Settled 2026-07-19 (Brian): scope (docs + SEO + gate design→build), zero-trace = browsing-only,
interim willingness wording. Still open — recommendations marked:

| # | Decision | Options | Recommendation |
|---|----------|---------|----------------|
| D1 | **Reveal semantics** — the keystone; most §8 rows collapse into it | (a) transient session view-grant; (b) durable for direct-nav only; (c) durable per-work consent consulted by owned-interaction surfaces too | **(c)** — resolves ghost rows, vanishing history, clone traps; granular-consent is the three-tier philosophy's own mechanism; (a) keeps zero-trace purest but guarantees the traps |
| D2 | Anon reveal persistence | session vs persistent cookie | **Persistent, Lax, ~50-id LRU cap** (consistent with D1c) |
| D3 | Interstitial metadata scope | title/author/rating only vs + cover + short description | **Include cover + short description** (it's the indexable artifact; AO3 shows summaries on its warning page). Caveat: descriptions of M works are author-written and may themselves be explicit — content-policy question for row 10/ToS |
| D4 | Gate surfaces beyond story/chapter | M-audience groups; M blog posts | **Groups: yes** (audience gate, one reveal per group; child APIs need parent checks regardless — L2-L4). **Blogs: simple interstitial reusing the component**, or defer to silent-404 — B7 forces the detail-read work either way |
| D5 | Spotlight M policy | block at redemption / gated card / silent drop (today) | **Gated card** — treats M as first-class on the front door, honest to slot-holders; blocking M from promotion contradicts the thesis |
| D6 | Author profile works list | silent thin (today) vs redacted M cards | **Redacted cards** — the discovery bridge is the middle tier's origin mechanism; also resolves the banner-vs-tab mismatch honestly |
| D7 | Library/list disclosure | silent thin / "K hidden" note / owner-elevated | **Owner-elevated for owned surfaces (under D1c) + "K mature items hidden" on public lists** |
| D8 | Clone semantics | keep destructive snapshot (settled 2026-07-13) vs ground-truth copy | **Keep, document the interaction with reveals**; revisit on complaint |
| D9 | Future follow-driven notification fan-out | suppress / redact / full | **Redact** (presenter null-title path is free) |
| D10 | Privacy taxonomy statuses | Private → real 404 (from soft-404); UsersOnly → login prompt vs indistinguishable | **Private: real 404. UsersOnly: login-prompt page** (login already distinguishes; prompt is actionable, leaks nothing new) |
| D11 | Export for revealed viewers | cookie-visible reveal (works) vs stay T-capped | **Cookie transport** — reveal rides the same cookie for anon; logged-in durable consent is DB-backed so export reads it server-side |
| D12 | Pattern B activation | now (spoofable) vs config-gated until Phase 7 trust boundary | **Config-gated; Pattern A interim** (§6) |
| D13 | View-ping on interstitial | count / don't | **Don't** — count revealed loads only |
| D14 | Anon reveals on login | migrate to account / discard | **Discard** (simplicity; re-consent is one click) |
| D15 | Naming | Feature 66 "Viewer access gating"; single **WU-AccessGate** with internal slices | as stated |
| D16 | robots.txt AI-crawler stance | permissive vs class-standard AI-trainer blocking (AO3/FFN/Fimfiction all block) | **Block AI trainers** (matches class norms and author expectations) |

---

## 10. WU-AccessGate — slice structure and code-touch inventory

One WU, five ordered slices; each slice is independently green (`dotnet test`) and shippable.

**Slice 0 — Tier-0 fixes (no open-decision dependency; do first).**
§1 tables L1-L8, 1b, B1-B8. Files: `ServerCommentReadService.cs`, `ServerGroupReadService.cs`,
`ServerBlogPostReadService.cs`, `ServerStoryArcReadService.cs`, `ServerNotificationReadService.cs`,
`ServerStoryReadService.cs` (total-views), `ServerSeriesReadService.cs`, the seven
profile-visibility services/endpoints, `Program.cs` (middleware ordering/status pages),
`Client\Routes.razor` (NotAuthorized template), soft-404 pages (`GroupPage`, `BlogPostPage`,
`CustomListPage`, `ProfilePage` → `Nav.NotFound()`), five dead-markup removals, `TreeSearchPage`
login link. New regression tests per leak (Integration tier).

**Slice A — Viewer-context mechanism (D1, D2).**
`IActiveUserContext` contract: effective-ceiling-for-work (`CanViewMature(storyId)` shape) +
`IsVerifiedBot`; `ServerActiveUserContext` (anon cookie read, reveal set, lazy-cached);
`WasmActiveUserContext` parity via `PersistentComponentState`; anon-prefs cookie
writer endpoint (full-document POST/redirect); MA-605 resolution (claim-refresh endpoint used by
both the interstitial's "always show" action and the settings form —
`ServerUserSettingsService.cs:160-183` + `PrivacySettingsForm`); durable-consent store (D1c:
migration for `user_revealed_works` or jsonb); `FakeActiveUserContext` + `IntegrationTestBase`
updates.

**Slice B — Gated-existence reads + interstitial + labels (D3, D13; template for D4).**
`ServerStoryReadService` (gated-metadata read; reveal-aware `GetStoryByIdAsync`; author
self-elevation B5); `ServerChapterReadService` (five ceiling sites reveal-aware; TOC/versions;
edit-path B5; B6 heuristic retired); `SharedUI\ContentGate\` interstitial component +
`StoryPage`/`ChapterReadingPage` third branch; adult-labels head component; `StoryEndpoints.cs`
gated envelope for `/api/stories/{id}`; export reveal transport (D11); RazorComponents tests
(interstitial renders, no body in gated HTML, temp-reveal doesn't mutate the setting) +
Integration (discriminated read, reveal-aware chapter reads) + manual L4.5 curl band
(cookieless → interstitial + labels; cookie → content; taken-down → 404).

**Slice C — SEO (§7).** `wwwroot\robots.txt`; sitemap minimal-API endpoint; canonical 301 +
`<link rel="canonical">` in `StoryPage`. Integration tests: sitemap includes an M story with
absolute `<loc>`; wrong slug → 301.

**Slice D — Verified-bot serving (D12).** `VerifiedBotMiddleware` + `Seo:TrustVerifiedBots`
config (default false); T2 pages serve-full for bots; simulated-bot integration/manual tests.
Activation itself is a Phase 7 checklist line (trust boundary).

**Slice E — Ripple-surface policies (D4-D9).** Group/blog interstitials; spotlight gated card;
profile redacted cards; owned-surface elevated reads + list disclosure; notification fan-out
policy note for the future follow-driven WU. Retires/updates pinned tests:
`BookshelfStoryIdsTests.cs:178-185` (silent thinning), `ContentRatingFilterTests.cs:175-188`
(author-filtered), plus a new pin for hidden-story TOC emptiness (currently inferred, untested).

**Doc reconciliation (rides Slice 0/moment 1):** `middle_plan_v2.md` row 11 → Resolved (+ new
Phase 2 item WU-AccessGate; WU-SeoSite absorbed); `grid_axes.md` Feature 64 re-scope + Feature 66;
`status.md` rows; `audit/Seo.md` close-out; new `audit/AccessGate.md` settled-vs-open note;
`middle-addendum.md` §3 #18 answered; `content-safety.md` taxonomy §3 + filtering-idiom rule +
row-11 pointer update; `identity-and-authorization.md` anon-cookie + bot-context sections.

---

## 11. Verification approach (WU-wide)

Per slice: `dotnet test` green with the new Integration/RazorComponents coverage named above.
End-to-end (run-server, seeded DB): curl matrix over an M story — anonymous no-cookie
(interstitial, adult labels, no body, 200), anonymous with mature cookie (content), anonymous
with reveal cookie (content for that work only; second M story still gated), logged-in mature-off
(interstitial), logged-in after "always show" (content, no re-login), simulated verified bot with
flag on (content) and off (interstitial), taken-down M story (404), nonexistent id (404), wrong
slug (301), `/sitemap.xml` (M URLs present), leak endpoints (all now empty/404 for un-permitted),
private profile tabs via direct API (empty), `[Authorize]` page as anon (styled sign-in, not
blank 401).
