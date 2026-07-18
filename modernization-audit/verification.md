# Tier-1 Adversarial Verification

Most Tier-1 findings are verified in one batch after all slices land (plan step 2d). High-stakes
security Tier-1s are spot-verified by the main session as they surface (cheaper to catch a false
positive early than to let it sit in the ledger). Each verdict: CONFIRMED / REFUTED / DEMOTED,
with what was checked.

---

### MA-201 (Tier 1, stored XSS — Story LongDescription unsanitized) — **CONFIRMED**
Checked 2026-07-17 by main session (direct re-read of cited spans, not the reporting agent's word).
- `ServerStoryWriteService.cs:6-12` ctor deps = (readDbFactory, writeDb, activeUser, imageStorage, rateLimit, logger) — **no `IHtmlSanitizationService`**. Confirmed.
- Full-file grep `[Ss]anitiz` on `ServerStoryWriteService.cs` → **zero matches**. Neither `CreateStoryAsync` (:15-67) nor `UpdateStoryAsync` (:69+) sanitizes. Confirmed.
- `LongDescription` is EditorView output (`StoryPropertiesForm.razor:78-79` + pull-on-submit) rendered via `RichTextView`→`MarkupString` (raw). Confirmed by S2 evidence + S0 seam record.
- Sibling `ServerSeriesWriteService` DOES inject + call the sanitizer — proves omission is an inconsistency, not a design decision.
**Verdict: CONFIRMED, stays Tier 1.** Mitigating note (does not change tier): production CSP (`script-src 'self' 'wasm-unsafe-eval' 'nonce-…'`, no `unsafe-inline`) blocks a bare stored `<script>` and inline `on*=` handlers in prod — but CSP is **Report-Only in Development** (dev/test fully exposed), and non-script injection (link/img/content-defacement/phishing) survives CSP. The sanitize-on-save architecture exists precisely so CSP is never the only barrier. Fix: inject `IHtmlSanitizationService`, sanitize `LongDescription` on create+update, add the regression test that currently can't exist.

### MA-301 (Tier 1, broken access control — chapter writes lack authorship checks) — **CONFIRMED**
Checked 2026-07-17 by main session (direct re-read of `ServerChapterWriteService.cs`).
- Grep of all `public async` methods + ownership tokens: `MoveChapterAsync:229` and `DeleteChapterAsync:281` DO gate (`if (story.AuthorId != userId) throw UnauthorizedAccessException`). The other five do NOT.
- Direct read of :148-220 confirms: `UpdateChapterContentAsync` (`.Include(c.Story)` at :152 — `AuthorId` is loaded, used only for rating at :158, never checked), `SetPrimaryVersionAsync` (:183, no check), `SetPublishedAsync` (:210, no check) all mutate + save with zero `AuthorId`/`ActiveUser.UserId` comparison. `CreateChapterAsync`/`AddAlternateVersionAsync` stamp `AuthorId = ActiveUser.UserId` but never verify caller owns the parent story.
- The L5 endpoints are `RequireAuthorization()` only (per S3 evidence), and the Global Flip made them live over HTTP → directly exploitable: any authenticated user edits/publishes/re-versions anyone's chapters, and `GetChapterForEditAsync` leaks any author's unpublished drafts.
- `identity-and-authorization.md` (kind d/f) + `audit/Chapters.md` WU26 + `ChapterManagerPanel` header all assert the service IS the authority — code contradicts documented intent (Bucket A, doc is right).
**Verdict: CONFIRMED, Tier 1.** Severity note: arguably higher-impact than MA-201 — no special setup, tampers with OTHER users' content, and the UI-affordance gating (edit button hidden for non-authors) is not a control per the codebase's own "security vs affordance" rule. Fix: add the author gate (load story, compare AuthorId to ActiveUser.UserId, throw) to all five ungated write methods + GetChapterForEditAsync; seed a second user in tests to pin it.

### MA-601 (Tier 1, IDOR — BadgeEndpoints) — **CONFIRMED**
Checked 2026-07-17 by main session (direct read of `Server/Badges/BadgeEndpoints.cs`).
- `/award` (:56-59): `(IBadgeWriteService badges, int userId, string badgeKey) => AwardAsync(userId, badgeKey)` — client supplies `userId`, no caller-vs-target check. `.RequireAuthorization()` only. `/` curation read (:50) + `/display-order` reorder (:65) same shape.
- The endpoint's OWN class doc (:19-24) states the vuln verbatim: "a caller could pass an arbitrary `userId` to read another user's hidden-badge curation view, self-award any catalogue badge via `/award`, or overwrite another user's `DisplayOrder`… none of that is caught today because the service is the single enforcement point and the service doesn't enforce it. Flagged for the eventual browser debug wave rather than resolved here."
- Client impls registered (`Client/Program.cs`) + Global Flip done (status.md 2026-07-13) → **live over HTTP.**
**Verdict: CONFIRMED, Tier 1.** Impact: privilege/integrity — any user self-awards any badge (incl. Patron/Architect) and tampers with others' curation. Fix: enforce caller==target (or RequireMod for award) in the SERVICE (`IBadgeWriteService`), per the codebase's own "service is the enforcement point" rule.

### MA-602 (Tier 1, broken access control — UserProfileEndpoints, anonymous private-data read) — **CONFIRMED**
Checked 2026-07-17 by main session (direct read of `Server/Profiles/UserProfileEndpoints.cs`).
- `GET /api/user-profiles/{userId:int}` (:27-28): `(profiles, int userId, bool includePrivate) => GetProfileHeaderAsync(userId, includePrivate)` — **no `.RequireAuthorization()` on the group or route** (doc :9 "neither route carries RequireAuthorization()"), and the client-supplied `includePrivate` query bool is passed straight to the service.
- The service contract intends `includePrivate = (viewerId == profileUserId)` computed by the dispatcher; the endpoint never re-derives it server-side (doc :12-16 "the caller … is trusted to pass viewerId == profileUserId"). So `?includePrivate=true` for any id, from anyone incl. anonymous, returns that user's private header/stats/last-seen — bypassing the viewer's own `PrivacySettings.ShowActivityStatus` / profile-visibility.
**Verdict: CONFIRMED, Tier 1.** Impact: privacy — anonymous enumeration of any user's private profile data. Fix: derive `includePrivate` server-side from `IActiveUserContext.UserId == userId` (never trust the client bool); the public-vs-private split is a server decision, not a client parameter.

---

### MA-101 (Tier 1→ see note, ReconnectModal stale asset path) — **CONFIRMED (severity nuance)**
Checked 2026-07-17 by main session (direct read of both files).
- File is physically at `Server/Components/ReconnectModal.razor.js`; the reference is `ReconnectModal.razor:2` `@Assets["Components/Layout/ReconnectModal.razor.js"]` — the `Layout/` segment does not exist on disk, so `@Assets` misses the fingerprinted manifest entry → 404, module never loads.
- The module is **load-bearing, not enhancement**: `ReconnectModal.razor` renders a `<dialog id="components-reconnect-modal">` (the .NET 9/10 custom-reconnect-UI pattern), and the `.razor.js` is what attaches the `components-reconnect-state-changed` listener + calls `reconnectModal.showModal()/.close()` (lines 2-15) and wires the Retry/Resume buttons (5-9, 23-57). Without it: on a circuit drop the dialog never opens and Retry/Resume are dead.
- **Adversarial mitigation found:** `blazor.web.js` attempts silent auto-reconnection independently of this modal, so a brief blip often recovers with no visible modal needed. The dead-modal failure is user-visible only when auto-reconnect is ALSO slow/failing → frozen-looking page with no retry affordance.
**Verdict: CONFIRMED.** Tier: defensible as **Tier 1 by the project's own "testing-friction" criterion** — human testers WILL hit network blips (laptop sleep, wifi handoff) and file "the site froze" reports during exactly the lock-in test phase; the fix is one string. Could be argued Tier 2 (degraded-not-broken given the silent-auto-reconnect safety net). Flag for Brian's tier call; either way it's a trivial must-do-before-testing. Fix: correct the two path strings to the physical folder (`Components/ReconnectModal.razor.js`), browser-verify on a forced circuit drop.

---

## CROSS-CUTTING THEME (synthesis input for report.md) — the L5 endpoint authorization-deferral class
MA-301, MA-601, MA-602 (three slices: Chapters, Badges, Profiles) share ONE root cause, and it is the single most important structural finding of the audit:
- The **service layer is disciplined** — audited clean on authorization in S4, S5, and the correct references in S6 (IUserSettingsService self-scoped, NotificationEndpoints deliberately unmapped-generation, Tags RequireMod).
- The **L5 endpoint layer** (added mechanically in WU-L5Sweep, "add-without-verify") applied `RequireAuthorization()` as a blanket floor and **explicitly deferred per-resource authorization** ("flagged for the eventual browser debug wave") — trusting client-supplied `userId`/`includePrivate`/ownership. Some services back this correctly (self-scoped) and some do NOT (Badges, UserProfile, Chapters write).
- **The Global Flip (2026-07-13) made every one of these endpoints live over HTTP** — so the deferral became an open door, not a dormant TODO.
This is not three bugs; it is one deferred-verification pass that ran INCONSISTENTLY — completed for some clusters, skipped for others.

**S7 REFINEMENT (2026-07-17, important — narrows the blast radius):** S7 audited the HIGHEST-privilege endpoints in the app — Moderation (takedown/ban/suspend/report-resolve), Spotlight (grant/redeem), SiteSettings — and the class **DOES NOT recur there.** Every mod/spotlight/site-settings mutation gates server-side in its backing service (`RequireModerator()`, owner-checks, target-id derived from `report.ReportedEntityId` never a client id). So the deferred authz pass WAS completed for the dangerous clusters; it slid through only on three specific lower-privilege ones (Chapters write, Badges, UserProfile-read).

**Net severity picture:** the worst case (a non-mod reaching a takedown or ban) does NOT exist. The three confirmed holes are real and must-fix — MA-602 (anon reads any private profile) is a genuine privacy breach, MA-301 (any user edits any chapter) is content-integrity, MA-601 (self-award any badge) is integrity/trust — but none is catastrophic-privilege. The class is **bounded to 3 known instances**, not pervasive.

**Report recommendation (revised):** still a systematic authz sweep of ALL ~40 endpoint files (the completion was demonstrably inconsistent, so "we found 3" ≠ "there are exactly 3" — the audit read write-paths at authz depth in ~4 endpoint clusters of ~15), but framed as **verification/closure of a known-inconsistent pass**, not damage control on a broken layer. The fix per instance is small and the codebase's own conventions specify it exactly (`identity-and-authorization.md` "security vs affordance": the service loads the entity and compares owner-id to `IActiveUserContext.UserId`).
