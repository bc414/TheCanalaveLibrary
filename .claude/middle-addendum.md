# Addendum — What's Missing for a Live Website

**Date:** 2026-07-07. **Scope:** not another internal-consistency pass (see `middle-audit.md` for
that) — this asks a different question: setting the docs' *own* claims aside, what does a real,
public, UGC-driven website actually need that never got surfaced anywhere across the spec,
`status.md`, `workplan.md`, `middle_plan_v2.md`, the skills files, or the audit files? Verified
against the live codebase where possible (grep + direct file reads), and against current external
practice/law via web research where the question is legal/industry-standard rather than
code-visible. **Legal sections are research, not legal advice** — treat them as "here's what to ask
a lawyer about," not a compliance sign-off.

Three categories, per your request: **§1 ought to be done and IS done**, **§2 documented as
planned** (cite the pointer), **§3 never surfaced anywhere**.

---

## §1 — Ought to be done, and is done

This project's engineering-facing readiness is genuinely strong for a solo pre-launch effort —
worth naming so the gaps in §3 read in proportion, not as an indictment:

- **Structured logging + OpenTelemetry** — Npgsql per-query spans, Blazor circuit sources/meters,
  custom `ActivitySource`/`Meter`, no-silent-catches sweep. `canalave-conventions/logging.md`.
- **CI + dependency automation** — GitHub Actions three-tier test suite on PRs, Dependabot
  (grouped Aspire/EF Core, weekly), report-only vuln scan. `.github/workflows/ci.yml`.
- **Security hardening** — upload magic-byte sniff + ImageSharp re-encode + decompression-bomb
  guard + EXIF strip, per-user L2 write-rate-limiting, HTTP edge rate limits on `/Account/*` and
  tag writes, security headers/CSP, Identity lockout + cookie flags. `security.md`.
- **Data Protection keyring persisted to Postgres** — survives app restarts/redeploys via
  `PersistKeysToDbContext`. (Also incidentally solves keyring-sharing across future web nodes —
  see §3's scaling item, where this fact is never connected to that need.)
- **Three-tier automated testing** — Unit / Integration (Testcontainers-Postgres) / RazorComponents
  (bUnit), Respawn-reset isolation. `testing.md`.
- **Content moderation** — report queue, moderator actions, account suspension/ban,
  `ActiveReportCount` auto-flagging, soft-delete default. `audit/Moderation.md`.
- **Real transactional email** — SMTP seam, MailKit sender, confirmation/reset/change flows
  verified end-to-end against Mailpit. `cross-cutting.md` "Identity & Auth."
- **Layered error handling** — page/chrome/card/comment error boundaries, toast + inline alert
  channels, localStorage draft autosave, restored `#blazor-error-ui` + `ReconnectModal`.
  `cross-cutting.md` "Error Handling Strategy."
- **Account deletion exists** (Feature 52) — a real mechanism, though see §3 for why "exists" and
  "satisfies GDPR erasure" are two different claims.

---

## §2 — Documented as planned (not yet done, but tracked with a pointer)

Everything here has an owner, a phase, and (usually) a decision row — the gap is execution, not
awareness:

| Item | Pointer |
|---|---|
| Deploy mechanism (`aspire publish` → docker-compose candidate) | `middle_plan_v2.md` Phase 7 |
| Config/secrets promotion contract | Phase 7 |
| Migration-in-production convention (gated step, not migrate-on-startup) | Phase 7 |
| DB backup policy + one performed restore drill; R2 blob story | Phase 7 |
| Uptime & alerting (external pinger + alert channel) | Phase 7 |
| Telemetry destination deployment (Grafana LGTM container) | Phase 7, decision row 7 resolved |
| CI hardening for a live master (push-trigger, branch protection, hard vuln-gate) | Phase 7, deliberately deferred with documented trigger conditions |
| TLS/domain | Phase 7 |
| Legal/policy track (ToS, privacy policy, DMCA agent, moderation obligations) | Decision row 10 — **see §3, this is far less resolved than one line implies** |
| Email provider + sending domain choice | Decision row 8, Phase 7 |
| L6 index batch (comment golden index, StoryTag reverse index) + performance baseline | Phase 1 item 3 |
| SignalR messaging push | Phase 1 item 8 |
| L5 WASM global flip | Phase 5 |
| Story Arcs / Polls / Custom Lists / Spotlight donation infra / Feature Contributions | Phase 4, decision row 3 |
| Beta logistics | Decision row 6 |
| Log retention / PII-in-logs policy | `logging.md`: "revisit at Phase 7, pairs with decision row 10" — flagged but not detailed |

---

## §3 — Never surfaced anywhere

Grouped by theme. Each item: what it is, why it matters for *this* project specifically, and a
realistic priority call (small solo fan site, not enterprise SaaS — I'm not recommending
compliance theater).

### Legal, safety & policy

*(Research-grounded, not legal advice — see sources at bottom.)*

1. **COPPA / under-13 exposure.** No minimum-age assertion exists anywhere (signup, ToS, or code).
   A Pokémon-branded site is a materially different fact pattern than a fandom-neutral archive
   like AO3 — the branding itself invites "this attracts children" scrutiny, and COPPA's "actual
   knowledge" trigger can fire from something as small as a user's bio or a parent email, not just
   deliberate targeting. **Cheap fix, worth doing before public launch:** an age
   checkbox/assertion at signup + a ToS clause. Low urgency for a small invite beta.
2. **Mature-content age verification.** `ShowMatureContent` is a bare self-toggle with zero
   verification — no jurisdiction awareness, no interstitial. 25+ US states now have
   age-verification statutes for content "harmful to minors" (SCOTUS upheld the model in 2025),
   though they target sites where such content is a *substantial portion* of the whole, which is
   a genuinely gray area for a mixed-rating UGC archive (AO3 has operated 15+ years on a bare
   click-through, not verification). **Realistic minimum:** an explicit per-work interstitial
   disclaimer for Explicit content (matching AO3's actual practice), plus a documented decision
   (not silence) on whether Explicit-tier volume could ever cross a state's threshold.
3. **DMCA safe harbor mechanics.** The project has a moderation queue for *user-conduct* reports,
   but DMCA §512 safe harbor is a separate, specific, and cheap thing: registering a designated
   agent with the US Copyright Office (~$6, public form), publishing that contact, and having a
   repeat-infringer termination policy. None of this exists or is named anywhere — decision row 10
   just says "DMCA agent/process" as a sub-clause with no actual task behind it. **This is the
   single highest-value/lowest-cost item on this whole list** — a copyright holder (or an
   overzealous fan) sending a takedown demand against a site hosting derivative fiction of a
   heavily-IP-protected franchise is a live, foreseeable scenario, and registering the agent is a
   ten-minute form, not a project.
4. **Section 230 framing.** Not referenced anywhere. Low practical risk given the moderation model
   (reactive, reports-driven review of third-party content is exactly what 230 protects), but
   worth one boilerplate sentence in the eventual ToS rather than silence.
5. **GDPR Article 15/17 specifics.** Account deletion (F52) exists, but "the user can delete their
   account" and "erasure that also purges backups/logs/moderation records and can be confirmed to
   the requester" are different claims — nothing in the docs addresses which one F52 actually is,
   nor is there a data-export/access-request feature. GDPR has no size floor; a US site with open
   signup will accumulate *some* EU visitors' data in the ordinary course. **Realistic minimum:** a
   privacy policy + confirming F52 actually purges data end-to-end, not full DSAR tooling.
6. **Trademark/IP disclaimer for the site's own branding.** Distinct from user-copyright concerns:
   the site itself is named after a Pokémon location and uses official-style sprites as UI chrome.
   The Pokémon Company's media-usage guidelines permit personal non-commercial fan use but don't
   license a hosting platform's branding. **Cheapest item on the entire list** — a one-paragraph
   "unofficial, non-commercial fan project, no ownership claimed" disclaimer, currently absent
   from spec, code, and every doc.
7. **Cookie consent.** Not currently needed (auth-cookie-only, no analytics exist yet per the SEO
   findings below) — but nothing tracks "revisit this the moment analytics ship," so it's a
   silent trip-wire rather than a tracked decision.

### Operational resilience

8. **Incident response / runbook.** Phase 7 gets you to "an alert fires and reaches Brian" — no
   doc anywhere says what happens next (who/what to check, rollback steps, how to communicate an
   outage). Currently zero-cost to skip pre-launch; worth a short runbook before real users depend
   on uptime.
9. **Session/circuit affinity & SignalR backplane for horizontal scaling.** Every N≥2 reference in
   this project's docs (`grid_axes.md`, `layer2-services.md`, `middle_plan_v2.md`) is about the
   Valkey signal-buffer swap — **nothing addresses sticky sessions / load-balancer affinity for
   Blazor Server's SignalR circuits**, which is a hard requirement before a second web node is
   viable at all (a naive round-robin LB breaks live circuits mid-session), nor a SignalR
   backplane distinct from the buffer-store concern. Not urgent at N=1, but the docs currently
   imply "N≥2 is just a Valkey config swap," which understates what scaling out actually needs.
10. **Health-check endpoint production exposure.** Confirmed in code: `MapHealthChecks` is
    wrapped in `IsDevelopment()` only. `middle_plan_v2.md` names "safely-exposed... currently
    dev-only-mapped" as an open item but never specifies what "safely exposed" means (auth token?
    IP allowlist to the external pinger? separate internal port?) — the *mechanism* is undecided,
    not just the deployment step.
11. **Backup/DR beyond the database.** A DB backup+restore drill is planned, but nothing addresses
    recovering the Grafana LGTM dashboards/config or documents that the Data Protection keyring is
    incidentally covered by the Postgres backup (it is, since keys live in `ApplicationDbContext`
    — but no doc states this connection, so a reader would reasonably believe it's still an open
    gap). A droplet-loss scenario today has no documented path to reconstructing observability
    config from scratch.
12. **Cost/capacity monitoring.** Nothing anywhere monitors droplet CPU/RAM/disk, Postgres storage
    growth, or defines a trigger for "time to upsize." Low urgency pre-launch, real gap once real
    traffic exists.
13. **Email deliverability (SPF/DKIM/DMARC).** WU-Email built a real SMTP sender; DNS-level
    deliverability setup is unmentioned anywhere, including in the Phase 7 checklist itself. Without
    it, transactional email (confirmation, password reset) risks landing in spam for real users —
    this is not cosmetic, it directly affects whether new users can activate accounts.
14. **Staging/pre-prod environment.** The path is local dev → CI (PR-gated) → straight to
    production droplet. No intermediate environment is discussed anywhere. Reasonable to accept
    for a solo small-beta launch; worth naming as a conscious trade-off rather than an unnoticed gap.

### SEO, discoverability & growth

15. **SEO fundamentals — robots.txt, sitemap.xml, per-page titles/meta descriptions.** Confirmed
    absent from `App.razor`/`_Layout.cshtml` and the whole repo. Without a sitemap, a UGC site
    with many story pages is slow/incomplete to crawl; without per-page titles, every story likely
    shares one generic search-result title. **Cheap, launch-worthy fix** (a few hours): a
    minimal-API sitemap endpoint over published stories + a static `robots.txt` + `PageTitle`/meta
    per page.
16. **Canonical slug redirect — spec'd but never built.** The spec (§ on story routing) explicitly
    says "redirect to canonical if slug doesn't match," but `StoryPage.razor` states plainly that
    the slug is cosmetic and does no redirect — `/story/42/wrong-slug` and `/story/42/right-slug`
    both resolve with no 301. This is a documented-but-unbuilt gap, not a never-considered one, and
    it's exactly the duplicate-content pattern that hurts SEO.
17. **Open Graph tags for social sharing.** Confirmed absent. Arguably the single highest-leverage
    item on this whole list for *this* audience — fandom communities live in Discord, and a shared
    story link currently unfurls as a bare gray URL with no title/image/blurb. OG tags work fine
    with this app's SSR/`HeadOutlet` architecture (Discord doesn't execute JS to unfurl, so
    JS-injected tags wouldn't work anyway — SSR is the right fit here). **Launch-worthy, cheap.**
18. **Mature-content search-engine noindex.** No mapping exists from the `Rating` field to a
    `noindex` directive. Independent of the age-verification legal question above, general practice
    is to keep Mature/Explicit story pages out of general search results (`noindex, follow`, not a
    `robots.txt` blanket block, which would also hide the noindex tag itself). Cheap — same code
    path as items 15/17, driven off a field the `Story` entity already has.
19. **RSS/Atom feeds.** Absent. Real precedent in this exact space (AO3, FFN both offer it) but
    both implementations are widely considered mediocre, and the real retention mechanism for
    "new chapter from a followed author" is the *already-deferred* email-digest feature
    (`EmailEnabled`), not RSS. **Low priority, safe to skip at launch.**
20. **Analytics.** No traffic-counting tool of any kind exists. Given the project already runs a
    self-hosted Grafana LGTM stack for app observability, a lightweight self-hosted option (Umami)
    or piping pageview counts into the existing OTel/Prometheus pipeline avoids standing up a
    fourth service. **Nice-to-have shortly after launch, not blocking.**
21. **PWA manifest.json.** Absent. The full offline/service-worker PWA story targets Blazor WASM
    standalone apps, not this server-rendered app, so it doesn't map cleanly here anyway. A bare
    manifest (icons, name, theme-color, no service worker) is cheap cosmetic polish. **Low priority.**

### Accessibility

22. **No accessibility convention or verification step exists at all.** Verified directly: 237
    incidental `aria-`/`role=`/`<label>`/`tabindex` occurrences across 66 component files exist —
    but these come from ordinary semantic HTML and Blazor's `EditForm` scaffolding, not a
    deliberate accessibility program. There is no WCAG reference, no keyboard-navigation or
    screen-reader check anywhere in `layer4-style.md`'s tier rules, no accessibility-specific
    testing tier (no axe-core/Lighthouse-CI style check in the three-tier test suite), and the
    L4.5-Browser verification band's own definition ("driven in a real browser and behaves as its
    audit file intends") never mentions keyboard-only or screen-reader navigation as part of what
    "behaves as intended" means. For a public UGC site this is a genuine gap, not just polish —
    color-contrast (relevant given the green Pokémon palette), focus-visible states, and form-label
    association are all currently unverified as a category, not just unimplemented in specific
    spots. **Realistic scope for a solo dev:** not a full WCAG AA audit pre-launch, but worth one
    pass with a browser extension (axe DevTools) over the handful of highest-traffic pages
    (search, story page, chapter reading, signup/login) before public launch, plus a one-line
    addition to `layer4-style.md`'s Stage-5 criteria ("keyboard-navigable, visible focus states")
    so it stops being invisible to the process entirely.

### Not flagged as gaps (deliberately out of scope, reasonably so)

- **Internationalization/localization** — nothing anywhere suggests non-English support was ever
  a goal; reasonable to treat as permanently out of scope for a small English-language fandom
  site, not a gap.
- **Payment/donation compliance** (PCI-DSS, nonprofit vs. for-profit revenue handling) — moot for
  now since Spotlight's donation infrastructure is explicitly deferred (decision row 3); revisit
  *if and when* that feature is greenlit, not before.

---

## Sources consulted (legal/compliance section, §3 items 1–7)

- [Complying with COPPA: FAQs — FTC](https://www.ftc.gov/business-guidance/resources/complying-coppa-frequently-asked-questions)
- [Musical.ly COPPA settlement — FTC](https://www.ftc.gov/business-guidance/blog/2019/02/largest-ftc-coppa-settlement-requires-musically-change-its-tune)
- [COPPA mixed-audience apps: actual knowledge & age gates](https://blog.promise.legal/coppa-mixed-age-audience-actual-knowledge/)
- [Age verification laws by state — Super Lawyers](https://www.superlawyers.com/resources/internet/age-verification-laws-accessing-adult-content/)
- [DMCA Designated Agent Directory — US Copyright Office](https://www.copyright.gov/dmca-directory/)
- [AO3 Terms of Service FAQ](https://archiveofourown.org/tos_faq)
- [Section 230 / moderator's dilemma — George Mason Law Review](https://lawreview.gmu.edu/print__issues/anderson-algorithms-and-section-230-after-netchoice-the-risk-of-a-new-moderators-dilemma/)
- [Does GDPR apply to my US company? — TechGDPR](https://techgdpr.com/blog/does-the-gdpr-apply-to-my-us-company/)
- [Pokémon Media Usage Guidelines — The Pokémon Company International](https://pokemon.gamespress.com/Media-Usage-Guidelines)
- [Cookie consent requirements in the US — CookieYes](https://www.cookieyes.com/blog/us-cookie-consent-requirements/)

## Overall take

Nothing here is a "the plan is broken" finding — the process docs are unusually disciplined about
the engineering surface (§1/§2 above are genuinely solid). The pattern across §3 is that
**everything the docs are disciplined about is code-adjacent and testable**, and everything
missing is exactly the stuff that isn't: legal exposure, a second web node, a DNS record, a search
engine's crawler, a screen reader. None of it demands enterprise-scale investment — the two
cheapest, highest-value items to close first are the **DMCA agent registration** (a ten-minute
form closing real copyright exposure) and the **SEO/OG/noindex bundle** (a few hours of work, all
sharing one code path, with the Discord-sharing payoff being the single highest-leverage growth
item on the list for this audience).
