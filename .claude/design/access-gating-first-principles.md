# Access Gating from First Principles — Doctrine, Answers, and Reclassification

**Written 2026-07-19.** Companion to `access-gating-audit.md` (the five-sweep surface audit).
Brian challenged the audit's framing with a set of first-principles questions; this document
answers each one, derives the doctrine that resolves them coherently, and **reclassifies the
audit's Tier-0 findings** under that doctrine. Where this document and the audit disagree, this
document wins — the audit found the facts; this document decides what they mean.

---

## 1. What is the mature gate actually *for*?

Everything downstream depends on getting this one distinction right. The codebase currently
enforces two very different things with the same machinery, and the audit inherited that
conflation:

**Class A — Access control (real security).** Protects data some viewers are *not entitled to
have*: private profiles, hidden favorites, drafts, unpublished chapters, DMs, mod queues,
taken-down content. The adversary is real (someone wants data they shouldn't get), so enforcement
must be server-side at every reachable endpoint, because the adversary chooses the access path.
Auth checks, `ProfileVisibility`, `IsTakenDown`, ownership checks — all Class A.

**Class B — Consent UX (content curation).** M-rated content is *public content with a consent
checkpoint*. Anyone may read it: create an account, flip the toggle, done — or, in the target
model, click "View this story" on the interstitial. There is no confidentiality requirement and
no adversary. The gate exists to (a) prevent *unwanted or accidental* exposure through the UI,
and (b) capture a deliberate consent action for the legal/comfort record. The rating ceiling,
the interstitial, `GroupAudience` — all Class B.

The audit applied Class-A logic ("reachable = leak") to Class-B data. That produced the L1–L8
"leak" list. Under the correct framing, most of those aren't leaks at all — see §5.

**The one Class-B enforcement that IS load-bearing server-side:** the *page-serving* path. The
interstitial's value depends on the M story body being absent from HTML served to an un-consented
browser — because page HTML is what reaches people *accidentally* (links, searches, prefetch,
shoulder-surfing, minors clicking around). That stays strict. What changes is the JSON API layer.

## 2. The Intentionality Doctrine

Brian's proposed rule: **"If someone is using the API directly, they are doing it on purpose."**

Analysis: constructing `GET /api/comments/chapter/17342` by hand is a *more* deliberate act than
clicking "View this story" on an interstitial. The interstitial exists to capture deliberate
consent; the hand-built API call *is* deliberate consent, expressed more emphatically. Gating
Class-B data at the JSON layer therefore defends nothing the gate was built to defend — it only
adds joins, code, and audit noise.

**What the content class actually does.** This is not a novel stance — it is the established
practice of every comparable site, verified 2026-07-19:

- **AO3**: the adult-content gate is literally a query parameter — `?view_adult=true` — plus a
  cookie after one click. Every fanwork's full text is served to any client that appends the
  param. Their gate is a UI consent checkpoint, nothing more, and it has survived 15+ years of
  legal scrutiny in exactly that form.
- **Fimfiction**: the mature gate is a `view_mature` cookie. Set it with curl and read anything.
- **FFN**: no gate at all beyond a default browse filter; direct URLs serve everything to
  everyone, including the explicit content it nominally bans.

So the doctrine is not "weaker than industry standard" — it *is* the industry standard, stated
honestly instead of implemented accidentally.

**Consequences of adopting it (the complete honest list):**

1. **Legal: none material.** Age-verification statutes and consent-screen practice concern the
   *presentation of a website to users*, not machine-readable JSON. No statute requires DRM on an
   API. The consent record that matters is "the human-facing page required a deliberate opt-in,"
   which remains fully true. (Counsel confirms under row 10, like everything else in that track.)
2. **Scrapers/AI trainers get M content trivially.** They already do on every site in the class,
   and they'd get it here anyway via one cookie. robots.txt AI-bot blocking (D16) is the actual
   tool for that concern, and it's orthogonal.
3. **Third-party clients could present M content without a gate.** If someone builds an app on
   these APIs, the consent screen becomes their responsibility. At this site's scale this is
   hypothetical; if an API ecosystem ever matters, terms-of-use for API consumers is the fix, not
   JSON-layer rating checks.
4. **The real cost — your own future UI code.** This is the only consequence with teeth. If
   `/api/comments/chapter/{id}` is ungated, the invariant "comments render only inside a gated
   page" lives in *caller discipline*. A future "recent comments" widget, search-result preview,
   or dashboard that composes child data directly can leak M content to mature-off users
   *through the UI* — the exact thing the gate exists to prevent. The named-filter architecture
   was built on the opposite philosophy ("structurally safe instead of discipline-safe").

**Resolution of consequence 4 without abandoning the doctrine:** record the invariant as a named
convention (content-safety.md): *M-safety is enforced at page level; JSON APIs serving Class-B
child data are deliberately ungated; any NEW UI surface that composes child data (comments,
arcs, TOCs, group children) outside its gated parent page MUST itself apply the viewer's
effective ceiling.* Plus one structural backstop that costs nothing: child-data *page components*
(CommentSection etc.) only ever render inside a parent that already passed the gate — which is
today's composition and remains true under the interstitial. This converts a silent assumption
into a written rule the conventions skill enforces at review time.

**Verdict: coherent, adopted-pending-your-ratification, and it matches both the content class and
your stated philosophy.** It is not an anti-pattern; the anti-pattern was enforcing Class-A rigor
on Class-B data in some places while (accidentally) not doing so in others, then calling the
difference "leaks."

## 3. The mechanics question: does post-interstitial hydration actually work?

**Yes — and specifically *because* consent lives in a cookie (anon) or DB row (logged-in), not in
circuit state.** Walking the exact sequence for an anonymous user with M disabled hitting
`/story/42/3` (chapter 3 of an M story):

1. Request arrives with no consent cookie → `ServerActiveUserContext` reports effective ceiling T
   → gated-existence read returns `GatedMature(metadata)` → SSR renders the interstitial. No
   chapter body, no comments fetched. HTTP 200 + adult labels.
2. User clicks **"View this story"** → full-document POST to a small consent endpoint → response
   sets the cookie (`revealed += 42`) → 303 redirect back to `/story/42/3`.
3. The redirected request **carries the cookie**. The viewer context now reports effective
   ceiling M *for story 42*. The chapter read passes. During the same page render, the
   `CommentSection` fetch — whether it happens as a direct service call on the server circuit or
   as an HTTP call from WASM — **also carries the cookie** (same-origin requests attach cookies
   automatically). Chapter text and comments hydrate together, page-wide.
4. Every subsequent request — next chapter, TOC, versions, export anchor click, WASM revisit
   tomorrow — carries the same cookie and sees the same effective ceiling.

The same flow works for the logged-in mature-off user, with the DB reveal row (or the refreshed
claim, for "always show") replacing the cookie. The one architectural requirement, already
identified in the audit: the consent action must complete with a **full-document navigation**,
because the current circuit's viewer context is frozen (`ServerActiveUserContext` caches its
principal per scope). The interstitial's redirect satisfies this naturally.

**Group pages: identical.** Accept the group interstitial → cookie/DB records the group reveal →
reload → group detail, folders, blog list, comments all hydrate, because every child fetch
carries the consent. So yes: *the API doesn't need its own rating check for the flow to be
correct* — the check would be redundant with the consent the requests already carry. Whether to
keep a check anyway is purely the consequence-4 insurance question in §2, not a UX question.

## 4. The real model: three planes, not one boolean — and why your version is harder than theirs

Your "two levels of M governance" observation is the correct diagnosis, and it goes one step
further. The audit's contradictions all dissolve when surfaces are assigned to **three planes**:

| Plane | Definition | M-content rule | Examples |
|---|---|---|---|
| **Discovery** | The site offers you content you didn't ask for | **Zero-trace.** `ShowMatureContent=false` (or anon, no cookie) ⇒ M content simply does not exist here. This is the settled rule, unchanged. | Browse, search, tag listings, recs, random batch, tree search, homepage sections, group listings, co-occurrence |
| **Direct navigation** | You asked for a specific thing by URL | **Gate.** Existence acknowledged, content withheld until consent (interstitial); consent is per-story / per-group and remembered per D1. | Story page, chapter page, group page, M blog post page |
| **Personal** | Your own interaction graph | **Never rating-filtered.** Protected by *auth* (Class A), not by rating (Class B). What you favorited, followed, read, listed, and were notified about is your data, produced by your own deliberate acts. | Bookshelves, reading history, notifications, own lists, hidden-gem slots, own-authored stories |

`ShowMatureContent` = the **Discovery-plane setting only**. The reveal set = the
**Direct-navigation-plane consent record**. The Personal plane consults neither.

**Do you need a second, "strict no-M" setting?** No — that would be overfitting. Examine what a
strict-tier user actually experiences under the three-plane model with one boolean:

- Discovery: zero M anywhere. ✓ (unchanged)
- Direct navigation: they see an interstitial *only if they deliberately follow an M link*, and
  it shows title/author/rating only (per your D3 reversal — no cover, no description). They
  don't click "View this story." Nothing mature is ever displayed to them. The interstitial *is*
  the strict mode — it requires two deliberate acts (navigate + consent) before anything shows.
- Personal: they have no M interactions (they never consented to any) — nothing to see. The
  previously-M-on user who switched off (your case 2) sees their *own old favorites*, can now
  finally delete them (fixing the ghost-row trap), and their shelves reflect reality.

A hard-mode toggle ("don't even show me interstitials — pretend M URLs are 404s") is cheap to
add later *because the taxonomy centralizes the decision at one point* (the gated-existence
read), but nothing today demands it. Ship one boolean + reveals; note hard-mode as a possible
future preference.

**Why does this feel so much harder than what existing sites do? Because it is — but not for the
reason it seemed.** The hard part was never the gate (AO3's is a query param). The hard part is
the **rigorous Discovery-plane zero-trace mode**, and the empirical record shows nobody has
actually built it well:

- AO3 has **no account-level M filter at all** — you exclude ratings per-search, manually, every
  time. Their tier-1 story is "curate your own searches."
- FFN filters *default browse* to T but every direct path serves everything.
- Fimfiction is the only real attempt at your tier-1 mode, and its public bug tracker documents
  it failing at exactly the surfaces your audit flagged — M titles in libraries (#365), rating
  gaps on chapter pages (#169).

So: the sites you're comparing against mostly *present M content anyway* and filter only the
front window. You're building the version where tier-1 is real. The audit's length was the cost
of discovering that; the three-plane model is what makes it tractable — because the Discovery
plane is a **finite, enumerable list of surfaces** (the audit already enumerated them, and found
them *already clean* thanks to the named-filter architecture: marts, search, listings all passed).
Your EF named-filter infrastructure is genuinely *better* suited to rigorous zero-trace than
anything the incumbents have — the audit's scary findings were mostly (a) Class-A bugs unrelated
to mature content and (b) Class-B strictness applied to planes where it never belonged.

## 5. Reclassifying the audit's Tier-0 list under the doctrine

| Audit item | Old verdict | **New verdict** | Reasoning |
|---|---|---|---|
| L1 chapter comments API | Leak, fix | **Non-issue** (doctrine §2); optional insurance join per consequence-4 | Class-B child data; consent rides every request anyway |
| L2 group comments API | Leak, fix | **Non-issue**, same | Same |
| L3 group members API | Leak, fix | **Non-issue — ratified** (Brian: "nothing to obscure about a member list") | Social metadata, not M content; group existence is acknowledged under the interstitial model anyway |
| L4 group blog posts API | Leak, fix | **Non-issue**, same as L1 | Page-level group gate governs |
| L5 story arcs API | Leak, fix | **Non-issue**, same | Arc titles/ranges are Class-B child metadata of a page that now acknowledges existence |
| L6 notification enrichment | Leak, fix | **Correct behavior — Personal plane.** One change: *normalize toward ground truth* — the filtered Group-name lookup (`ServerNotificationReadService.cs:325-334`) should stop filtering so it matches the (correctly) unfiltered post-title path; delete the inconsistency in the unfiltered direction, not the filtered one | Notifications reference things the user interacted with; both of Brian's cases (per-story opt-in; previously-M-on) make filtering user-hostile |
| L7 view-count oracle | Leak, fix | **Non-issue** | Existence isn't secret under the interstitial model |
| L8 raw M ids in payloads | Leak, fix | **Non-issue** | Ids are Class-B; they resolve to gated pages |
| 1b ProfileVisibility API gap | Privacy gap, fix | **STANDS — Class A.** Private/UsersOnly profile tab data, comment wall, follow/vouch lists must respect ProfileVisibility server-side | Real access control; adversary model applies |
| Bookshelf/interaction endpoints | (via B3) | **STANDS, refined:** own shelves (all tabs, private favorites) = owner-auth only — *already true* (`RequireAuthorization` + MA-602-derived `includePrivate`, `UserStoryInteractionEndpoints.cs:58-64`). The fix is: public-profile favorites become anon-tolerant (B3 crash) **and** ProfileVisibility-gated (1b) | Brian's "server side gating if not authenticated as the user" is satisfied for private data; public favorites are deliberately public data with a privacy setting |
| B1 blank 401 pages | Bug, fix | **STANDS** (§7 design) | Class-A UX |
| B2 NotAuthorized template + dead markup | Bug, fix | **STANDS** (§7) | Same |
| B3 profile-tab WASM 401 crash | Bug, fix | **STANDS** | Split-brain bug regardless of doctrine |
| B4 soft-404s | Bug, fix | **STANDS** (status codes per taxonomy; §8 for profile wording) | SEO + consistency |
| B5 author lockout | Bug, fix | **STANDS — ratified** (Brian: authors always see their own story) | Personal plane; also retire the test pinning the lockout (`ContentRatingFilterTests.cs:175-188`) |
| B6 version-gate heuristic misfire | Bug, fix | **STANDS** — replaced by the real gated-existence read | |
| B7 group blog permalink 404 | Bug, fix | **STANDS — ratified** ("fixed properly first") | Build the group-post detail read with the group gate |
| B8 minor UX items | Fix | **STANDS** | |
| MA-605 claim staleness | Forced dependency | **STANDS — ratified** ("needs to be responsive") — §9 | |

**Net effect: Slice 0 shrinks by roughly half and becomes philosophically clean** — everything
left in it is Class-A (auth, privacy, real bugs). Every L-item that dissolved must still be
*recorded* — the Intentionality Doctrine goes into `content-safety.md` as a named convention so
no future audit re-reports ungated child APIs as leaks, and no future session "fixes" them.

## 6. The remaining per-question answers

### 6.1 Notifications (beyond the L6 reclassification)
No joins, no rating filtering, ever — Personal plane. The presenter's null-title fallback stays
as graceful degradation for *deleted* content, not rating. When follow-driven fan-out is built
later, deliver full titles: the user followed the story; that was the consent. (This supersedes
the audit's D9 "redact" recommendation — redaction was a compromise for a conflated model.)

### 6.2 Spotlight — **your call is recorded: dedicated M and non-M slot pools.**
Design implications for the WU: `SpotlightSlot` gains a rating-class dimension; redemption
validates story rating against slot class (the only rating check that *belongs* on a write path,
because it's slot-inventory integrity, not consent); the homepage section composes per-viewer —
mature-on viewers see both pools, mature-off/anon see the non-M pool at full width with no gaps
(the section renders a pool, not a thinned list, so nothing "vanishes"). Slot buyers of M slots
know exactly what audience they're buying: M-enabled viewers. This resolves the devaluation
problem more honestly than gated cards would have.

### 6.3 Export
Nothing special to build, in either direction. Export inherits the read service, so it honors the
effective ceiling automatically: post-interstitial UI export works (the anchor GET carries the
consent cookie / the DB reveal is server-readable); direct API export of an unconsented M story
returns 404 today — and per the doctrine we *don't care* to strengthen or weaken that; it's free
inheritance, not a policy surface. Verified crawlers are never served export files.

### 6.4 Writes are rating-blind — now *officially correct*
Your reading is right: the direct-API stance retroactively validates the write paths. Favoriting,
listing, recommending an M story requires no ceiling check — the actor either consented via the
gate, has M on, or called the API on purpose (which is consent). The "a user adds from a story
page they could open" comments become literally true under the interstitial. No write-path
validation is added anywhere. The ghost-row problem is solved on the *read* side by the Personal
plane rule, not by policing writes.

## 7. The bare-401 pages: what they are and how the fix should look

**The affected routes** (every `[Authorize]` page, on full-document load as anon): `/bookshelves`,
`/my-lists`, `/series` (mine), `/story-lineages`, `/notifications` + `/notifications/settings`,
`/messages`, `/settings`, `/spotlight`, all editors (`/story/new`, `/story/{id}/edit`, chapter
editors, `/blog/new`, `/blog/{id}/edit`, `/group/new`, `/group/{id}/edit`, `/group/{id}/blog/new`,
`/series/new`, `/series/{id}/edit`), the five `/mod/*` pages, and all fourteen `/Account/Manage/*`
pages. You're right that these are fundamentally "requires being a user" surfaces.

**The design space:**
- (a) Silent redirect to `/Account/Login?returnUrl=…` — classic, but you correctly flag it as
  disorienting (user asked for a page, got a login form with no explanation).
- (b) **Explanatory in-place page** — "This is your bookshelf — the stories you're reading,
  following, and have favorited. Sign in to see yours." with two actions: **Sign in** (with
  returnUrl, so they land back here) and **Create account** (no returnUrl — flows to email
  confirmation and then a welcome page, per your instinct). This is your proposal, and it's the
  right one: it converts a dead end into an advertisement for the feature.
- (c) Generic styled "sign in required" page — one message for all routes; cheaper, blander.
- (d) Modal over a skeleton — poor fit with SSR/prerender and adds nothing here.

**Recommended: (b), implemented as one component + a copy table.** And yes — **B2 is exactly the
relevant area.** Mechanics: page-level `[Authorize]` means the *router* renders the denial, not
the page, so per-page copy can't live in the page (that's why those five existing inline
`<NotAuthorized>` blocks are dead code). The clean shape: a `NotAuthorized` template on
`AuthorizeRouteView` in `Routes.razor` rendering a `SignInRequired` component that looks up
friendly copy from a small route-prefix → description map (one dictionary, one place, ~15
entries). The full-document path (the actual blank 401) additionally needs the middleware-order
fix so the 401 re-executes into the same experience. Mod pages get the T5 variant ("this area is
for moderators") from the same component. Delete the five dead blocks.

## 8. T3 privacy display: not-found or "not authorized"?

First principles: indistinguishability (404) buys privacy only when **existence isn't otherwise
observable**. Split by that test:

- **Profiles**: existence is already public — the person's username appears on their comments,
  stories, group memberships. A 404 on a user you can see commenting *right there* is confusing
  and protects nothing. Recommend: honest state — "**This profile is private**" (Private) and
  "**Sign in to view this profile**" (UsersOnly, which is just T4 with context — logging in
  already reveals it, so the prompt leaks nothing new). Real status codes (403/401 semantics)
  rather than soft-200s.
- **Private lists**: existence is *not* otherwise observable (opaque int URL, never linked
  publicly). Keep the current privacy-preserving conflation — "doesn't exist or isn't visible" —
  but as a real 404.

## 9. Making `ShowMatureContent` responsive (MA-605) — the options

The setting is a cookie claim minted at sign-in; today a change waits for the next login. Options:

| Option | Mechanics | Verdict |
|---|---|---|
| 1. **`RefreshSignInAsync` on change** | The change action (settings save, interstitial "always show") runs through a server endpoint that writes the DB, reissues the auth cookie, and 303-redirects | **Recommended.** Both change surfaces are naturally full-document interactions; the redirect also solves the frozen-circuit problem in the same motion |
| 2. Read DB per request instead of claim | Kills the claim; adds a query to every scope | Rejected — the claim exists precisely because this is the hottest filter input |
| 3. Preference-override cookie beside the claim | Cookie set on change; context prefers cookie over claim | Workable but creates two truths (DB vs cookie) that can diverge; only attractive if you wanted to avoid touching Identity — you don't |
| 4. Security-stamp invalidation | Stamp change forces principal refresh within the validation interval | Interval is 30 min by default; shortening it costs a DB hit per request — worse than option 2 |

Option 1's overlap with cookies is minimal and clean: **anonymous** viewers' toggle lives in the
prefs cookie (no Identity involvement); **logged-in** viewers' toggle lives in DB + claim,
refreshed via option 1. One endpoint serves both ("set mature preference": authenticated → DB +
refresh + redirect; anonymous → Set-Cookie + redirect).

## 10. Cookie consent banners: do you need one? (Not legal advice; add to the row-10 counsel list)

Almost certainly **no**. The banner regimes (ePrivacy/GDPR consent walls, and their cultural
imitations) target *tracking*: third-party cookies, advertising identifiers, analytics,
cross-site profiling. Your cookies are: (a) the auth session cookie — universally exempt as
strictly necessary; (b) a first-party functional preference cookie **set only as the direct
result of an explicit user action** ("Show mature content" / "View this story"). The user's
click *is* the consent, contemporaneous and specific — a banner would be asking permission to
remember the answer to a question the user just answered. No ads, no analytics cookies, no third
parties, no cross-site anything. Practice across the content class (AO3's `view_adult` cookie,
Fimfiction's `view_mature`) matches: none of them banner it. Do: mention both cookies in the
privacy policy (row 10 artifact). Don't: build a banner.

## 11. Is `ProtectedBrowserStorage` a good thing here? No — and here's the general principle

What it is: ASP.NET's wrapper over browser local/session storage where the payload is encrypted
and MAC'd with the *server's* DataProtection keys. Its purpose is narrow: let interactive-server
components stash state in the browser **that the client cannot read or forge**.

Choose client-side storage by three questions:
1. **Who must read it to enforce?** If the server must see it on ordinary HTTP requests (page
   loads, export anchor GETs, WASM API calls) → **cookie**. Browser storage never rides requests.
2. **Is it secret?** If yes → it shouldn't be on the client at all → server/DB.
3. **Is client tampering meaningful?** Protection matters only when the client forging the value
   gains something the server wouldn't grant. A mature-content preference fails this test
   spectacularly: the user is the *authority* on their own preference — "forging"
   `mature=true` is identical to setting it. Encrypting it protects nothing from no one.

`ProtectedBrowserStorage` additionally fails this codebase mechanically: it's unavailable during
SSR/prerender (JS interop — exactly when the gate decision is made), unusable from WASM (server
API), and async (clashes with the synchronous context resolution). Verdict: wrong tool here, and
mostly a niche tool generally — legitimate when circuit-servers need tamper-proof client-parked
state (rare). The codebase's zero usage of it is correct, not an omission.

## 12. The "five manual ceiling computations" — bad code or warranted?

**Warranted in structure, with one real smell.** The reason they exist is a genuine domain
feature: **chapter versions carry their own rating** — `(cc.Rating ?? cc.Chapter.Story.Rating) <=
ceiling` (`ServerChapterReadService.cs:34,47`). A T-rated story can host an M-rated *alternate
version* of a chapter (and the primary-version invariant guarantees effective(primary) ==
story.Rating). The named `ContentRating` filter lives on `Story`; a per-*version* rating decision
cannot be expressed there — it must be a predicate where `ChapterContent` is queried. So
per-site predicates are the correct shape, not negligence. (A model-level filter on
`ChapterContent` closing over `_activeUser` *and* navigating to `Story` would replicate the TPT
hazard class and complicate the deliberately-unceilinged author-edit path.)

The smell is the **five-fold duplication of the derivation** `ShowMatureContent ? M : T`. That
expression is about to become non-trivial (claims *or* anon cookie *or* per-story reveal *or*
verified bot), and five copies means five chances to miss one. The fix rides Slice A naturally:
centralize as one method on the viewer context (e.g. `ActiveUser.EffectiveCeilingFor(storyId)`),
and the five sites become one-line consumers. Same treatment for the raw-SQL `maxRating`
parameters in the discovery services (which stay Discovery-plane: ceiling only, no reveals).

## 13. How login actually works here — and what T4's "return-url" really means

Your questions ("does the login button send an HTTP request? is in-place refresh possible? is
this old-web?") deserve the full mechanical answer:

**Cookie authentication requires a full HTTP response.** The auth cookie is HttpOnly (so scripts
can't steal it), which means it can only be set by an HTTP response header — and a Blazor
circuit is a WebSocket, which has no place to attach Set-Cookie. This is why the Identity pages
are static SSR (`[ExcludeFromInteractiveRouting]`): `/Account/Login` renders a real `<form>`,
the browser POSTs it, `SignInManager` validates and attaches the cookie to the response, and the
response redirects. This is not "old web" as a compromise — it is the *only* correct way to
establish an HttpOnly cookie session, and every modern cookie-auth Blazor app does exactly this.
(JS-fetch-based login endpoints exist in SPA-land, but they still end with Set-Cookie + a reload
to rebuild the authenticated context — same shape, more parts.)

**So the flow for your T4 page is:** anon user hits `/bookshelves` → explanatory page (§7) →
clicks **Sign in** → full-document navigation to `/Account/Login?returnUrl=/bookshelves` → POST →
cookie set → redirect to `/bookshelves` → fresh document, fresh circuit, authenticated claims →
the page renders their shelves. That *is* "stays on the same page but refreshed with the
logged-in user" — implemented as navigate-away-and-return, because a mid-circuit identity change
is impossible (the circuit's principal is frozen; the codebase documents this assumption). The
returnUrl is exactly the mechanism that makes the round-trip feel in-place.

**Create account** deliberately breaks the round-trip, per your instinct: Register → (email
confirmation — `RequireConfirmedAccount=true` means they aren't signed in until confirmed) →
first sign-in → **welcome/onboarding page**, not the bookshelf they bounced off three days
earlier. returnUrl on register would be stale and weird; on sign-in it's correct.

**The persistent sign-in button in MainLayout** already exists (`UserMenu`/`LoginDisplay` render
"Log in" for anon) and needs only the returnUrl added so header sign-ins also round-trip.

## 14. Interstitial content — ratified decisions

- **Wording: "View this story" / "View this group."** Never "work." (Recorded as a UI-copy
  convention; AO3's terminology is explicitly not adopted.)
- **Metadata scope (D3 reversed from the audit's recommendation): title, author, rating only.**
  No cover, no description — covers and author-written descriptions of explicit stories can
  themselves be explicit, and the viewer on this page *has not consented yet*. Discovery
  surfaces never show M items to non-consenting viewers at all, so the interstitial is the only
  place this metadata could leak — and now it doesn't.
- **SEO consequence, accepted:** the Pattern-A indexable artifact is thinner (title/author/rating
  + adult labels; no description snippet). Search still finds M stories by title/author — the
  discovery bridge that matters. Pattern B (verified bots served full content) restores deep
  indexing when the trust boundary lands. Discord/OG unfurls of M links likewise show
  title/author only.

## 15. Reveal storage, check order, and D1 re-presented

**Check order (your statement, confirmed as the design):**
```
effective_can_view(story) =
    IsVerifiedBot
    || (authenticated ? claim.ShowMatureContent : cookie.mature)   // global: everything goes
    || reveals.Contains(story.Id)                                  // per-story consent
    || story.Rating <= T                                           // non-mature always
```
The mature boolean short-circuits first; the reveal set only matters when it's false. Exactly as
you said.

**Where reveals live:**
- **Anonymous → the prefs cookie** (`{ mature: bool, revealed: [ids…] }`, SameSite=Lax so inbound
  external links carry it, not HttpOnly for WASM parity, capped LRU list). No other option
  reaches server enforcement in all render modes.
- **Logged-in → database** (small `user_story_reveals` table), not a cookie. Why: durable across
  devices and browsers (the middle-tier reader's phone knows what their desktop consented to);
  no size cap; server-readable on every path (export, prerender) with zero transport concerns;
  consistent with "user data lives in the DB"; and it gives you a future settings surface
  ("stories you've revealed — revoke") for free. A cookie for logged-in users would silently
  diverge from their account (cleared cookies, second device) — worst of both worlds.
- **On login: discard anon reveals** (D14 stands) — re-consent is one click, and merging
  cookie-to-DB is machinery without a constituency.
- **No consent banner** for any of this (§10).

**D1 re-presented against your sensibilities.** The original D1 asked "transient view-grant vs
durable consent?" and hung many consequences on it (ghost rows, vanishing history, clone traps).
**The three-plane model has already absorbed most of those consequences**: the Personal plane is
never rating-filtered, so your shelves/history/notifications show your M interactions *regardless
of what D1 decides*. What's left of D1 is genuinely small — **re-prompt cadence**:

| Option | What the middle-tier reader experiences | Alignment |
|---|---|---|
| (a) Transient (session) | Re-click the interstitial for the same story every browser session | Friction exactly where you promised seamlessness; protects shared-device privacy slightly |
| (b) Durable per-story | Consent once per story, remembered (cookie ~180d anon / DB forever logged-in) | **Matches your sensibilities**: consent is per-story (granular, not global), sticky (seamless), and revocable (delete the cookie / future settings list) |
| (c) Durable escalating ("after N reveals, offer to flip the global toggle") | (b) plus a nudge | Nice-to-have later; don't build now |

**Recommend (b).** Your own framing decides it: tier-3 readers are *quiet consumers* — making
them re-perform consent per session converts quiet consumption into repeated ceremony, which is
the flattening you're avoiding. The shared-device concern is real but marginal (the reveal list
shows titles only via the shelf — which is Personal-plane and auth-protected anyway for
logged-in users; for anon shared devices, the cookie is clearable and expiring).

## 16. D6 — the author profile works list, under the three-plane model

The profile's authored tab sits at the **junction of planes**, which is why it was hard: it's
reached by *direct navigation* (you chose this author — bridge behavior) but it's *formatted* as
a listing (discovery ergonomics). Map the options against the planes and tiers:

| Option | Discovery-plane purity (tier 1) | Bridge (tier 3) | Honesty |
|---|---|---|---|
| (i) Silent thinning + ground-truth banner stats (today) | Trace-free list, but the banner *contradicts the tab* — "12 stories" vs 10 shown, which is itself a trace, and a confusing one | Severed — M-only author looks empty | Worst of all three |
| (ii) Full redacted cards per M story ("A Mature story — view") | Weak — N labeled mature slots is a lot of trace | Strong | High |
| (iii) **One count-line disclosure** — "2 mature stories aren't shown · show them" (line absent when count is 0; "show them" = per-story reveal flow or inline reveal) | Near-pure — one line of text, zero titles/covers/descriptions; arguably *less* trace than today's contradictory banner math | Intact — the tier-3 visitor knows the author has more and can act | High |
| (iv) Viewer-relative banner stats + silent thinning | Pure | Severed | Consistent but bridge-hostile |

**Recommend (iii).** It is the profile-scale expression of the same principle as the
interstitial — *acknowledge existence, withhold content, offer consent* — and it resolves the
banner-vs-tab contradiction by making the banner's ground-truth numbers *true again* (the tab
now accounts for all N, shown + disclosed). For the strict tier-1 viewer, one neutral line
("2 mature stories aren't shown") contains zero mature content and reads as the site *working
for them*; if that's still too much trace for your taste, (iv) is the fallback and the
future hard-mode toggle could switch (iii)→(iv) per user. The same pattern then serves public
custom lists ("K mature stories aren't shown") — one component, two surfaces.

## 17. What this changes in WU-AccessGate (delta to the audit's §10)

- **Slice 0 shrinks to Class-A only:** ProfileVisibility API enforcement, profile-tab
  anon-tolerance (B3), 401/403 experience (B1/B2 per §7), soft-404 → honest statuses (B4 per §8),
  author self-elevation (B5), group-blog detail read (B7), minor B8 items. L1–L5, L7, L8
  **removed**; L6 becomes a one-line normalization *toward* ground truth.
- **Doctrine recording rides Slice 0** (moment-1 doc work): Intentionality Doctrine + three-plane
  model + Class A/Class B distinction → `content-safety.md`; the "child APIs deliberately
  ungated" invariant + the new-surface rule; "story/group not work" copy convention.
- **Slice A** unchanged in shape, refined in spec: `EffectiveCeilingFor(storyId)` centralization
  (§12), reveal check order (§15), DB reveals for logged-in + cookie for anon, MA-605 via
  `RefreshSignInAsync` endpoint (§9), no consent banner.
- **Slice B** — interstitial metadata reduced to title/author/rating (§14); "View this story";
  group + M-blog interstitials confirmed same-rigor (D4 ratified yes for both).
- **Slice E** — notifications: no filtering (supersedes D9-redact); spotlight: dedicated slot
  pools (D5 resolved, new slot-class design task); profile disclosure line (D6 → option iii
  pending your pick); bookshelves/lists: Personal-plane unfiltered reads replace the audit's
  "owner-elevated" framing (same code shape, cleaner rationale).

**Decisions now effectively settled by this exchange (2026-07-19):** Intentionality Doctrine
(pending your final word, but every position in this doc assumes it); L3 member lists public;
notifications unfiltered; author self-view; group-blog fix-first; MA-605 responsive via refresh;
D3 minimal interstitial metadata; D5 dedicated spotlight pools; D11 export inherits; terminology.
**Still yours to call:** D1 cadence (recommend b), D6 profile disclosure (recommend iii), T3
profile wording (recommend "This profile is private"), B1 copy approach (recommend route-map
component), and the final ratification of the doctrine itself.
