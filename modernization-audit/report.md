# The Canalave Library — Pre-Lock-In Modernization Audit
**Executive report · 2026-07-17 · the one document to read**

Supporting detail: `verification.md` (Tier-1 proofs), `cross-slice.md` (synthesis), `bucket-b.md`
(convention-vs-ecosystem, web-verified), `hypotheses.md` (100% coverage matrix), `slices/*-findings.md`
(all 98 findings with `file:line` evidence). Every finding here is spot-verifiable from its citation.

---

## The two questions you asked, answered

**1. "Is there anything that should be cleaned up before serious human testing and lock-in?"**
Yes — but it is bounded, well-understood, and does not indicate a troubled codebase. **Close 5 must-fix
items (4 security + 1 UX) and you are lock-in-ready.** The codebase is fundamentally disciplined: it
follows its own ratified conventions closely, and those conventions are themselves sound (14/17
verified current against live .NET 10 docs). The defects are *localized misses of an otherwise-followed
rule* — the signature of feature-by-feature generation — not systemic rot. There is no hidden
architectural problem and no security posture that needs rethinking.

**2. "Is the vastness warranted, or is refactoring for reduction warranted?"**
**It mostly earns its keep.** Across all seven slices, every backend compression idea was examined and
*rejected as a false economy* — the explicit DI registrations are load-bearing documentation, the
per-format import readers are genuinely distinct, the settled per-context service methods shouldn't
merge. The honest, *actionable-now* reduction is **small**: dead code + one exception-family unification (the
only trade-off-free wins), plus a product call on ~1,300 lines of untouched ASP.NET scaffold for features
you don't have (2FA, passkeys, external login). The apparent "Desktop/Mobile duplication" is **not** a
reduction opportunity — it is an unvalidated-placeholder state deferred to your later desktop→mobile
layout work (see the corrected note in Code Economy). **The per-feature density (~880 LOC across 9 layers)
is already lean.**

---

## MUST-FIX before lock-in (5 items — all Tier-1, all independently verified)

| ID | What | Where | Impact | Fix effort |
|---|---|---|---|---|
| **MA-602** | `UserProfileEndpoints` has **no auth at all** + trusts client `includePrivate` | `Server/Profiles/UserProfileEndpoints.cs:27` | **Anonymous can read any user's private profile/stats/last-seen.** Worst privacy finding. | S — derive `includePrivate` server-side from `IActiveUserContext` |
| **MA-301** | 5 of 7 chapter-write methods + draft-read lack the author gate | `Server/Chapters/ServerChapterWriteService.cs` (:148,183,210,…) | **Any logged-in user edits/publishes/reads-drafts on anyone's story** over live HTTP. | S–M — add owner-check to 6 methods; test seeds a 2nd user |
| **MA-601** | `BadgeEndpoints` trusts client `userId` (self-documented) | `Server/Badges/BadgeEndpoints.cs:56` | IDOR — self-award any badge (Patron/Architect), tamper with others' curation. | S — enforce caller==target in the service |
| **MA-201** | Story `LongDescription` saved unsanitized, rendered raw | `Server/Stories/ServerStoryWriteService.cs` (no sanitizer) | Stored XSS on the most-viewed page. Sibling Series service does it right. | S — inject `IHtmlSanitizationService`, sanitize on create+update |
| **MA-101** | ReconnectModal JS module referenced at a stale path → 404 | `Server/Components/ReconnectModal.razor:2` | Circuit-drop "Rejoining…" dialog never opens; testers hit "it froze". One-line fix. | S — correct `@Assets["Components/Layout/…"]` → `Components/…` |

**The #1 structural finding — the L5 endpoint authorization-deferral class** (MA-301/601/602 share it):
the mechanical `WU-L5Sweep` generated the HTTP endpoint layer with a blanket `RequireAuthorization()`
"floor" but **explicitly deferred per-resource authorization** ("flagged for the browser wave") — a pass
that never ran — and then the Global Flip made every endpoint live. **The service layer underneath is
disciplined; the mechanically-generated endpoint layer is where the holes cluster.** Good news: S7 proved
the class does **not** reach the high-privilege endpoints — every moderation takedown/ban and spotlight
grant *is* gated server-side. It slid through on 3 lower-privilege clusters only.
→ **Recommendation: a systematic authorization sweep of all ~40 endpoint files** ("does the backing
service enforce ownership, or does the endpoint trust a client id?"), framed as *closure of a
known-inconsistent pass*. We verified authz at depth in ~4 of ~15 endpoint clusters; "we found 3" ≠
"there are exactly 3." The fix per instance is small and `identity-and-authorization.md` §"security vs
affordance" already specifies it.

---

## SHOULD-FIX (Tier 2 — correctness & consistency; recommended before beta, not blocking)

Each is a *single fix-pattern* recurring across slices, not N unrelated bugs:

- **Non-atomic counters / lost-update races** (data integrity): `RecordSuccessAsync` (MA-502) and BlogPost
  `LikeCount` (MA-705) use tracked `++`/absolute-write in multi-reader paths. The codebase's own
  atomic-`ExecuteUpdateAsync` rule fixes both. *Highest-value Tier 2.*
- **Dropped interaction write** (MA-401): `UserStoryInteractionPanel.Dispose()` cancels the 2s debounce
  without flushing — toggle-then-navigate loses the write silently. Flush on dispose.
- **Not-found returns 200 not 404** (MA-202/304/404/606/708, ~13 sites): manual `NavigateTo("/not-found")`
  instead of `NavigationManager.NotFound()`. Web-verified: `NotFound()` is the correct .NET 10 API and
  yields a real 404 for crawlers (SEO/F64 relevant). One sweep. *(Caveat from bucket-b.md: the `<NotFound>`
  fragment is removed in .NET 10 — the app already uses the re-execution route it needs.)*
- **Error-channel drift** (MA-205/405/501/504/603/703/704, ~8 components): hand-rolled `<div role=alert>`
  + raw `ex.Message` instead of `InlineAlert`+`ExceptionPresenter`. The WU-ErrorHandling normalization
  didn't reach everything. Raw `ex.Message` in UI is a defect per your own error-handling.md.
- **HTTP status wrong side** (MA-123/701): Moderation's `RequireModerator` throws → 401 for a signed-in
  non-mod; **403 is correct** (web-verified against RFC 9110; the Spotlight/SiteSettings side already does
  403). Align Moderation.
- **Unregistered silent catches** (MA-001/002/206/303/503/704, ~6): `logging.md`'s registry claims one
  sanctioned site; reality is ~6 unregistered bare catches. Log-or-annotate + update the registry.
- **Two reopen-worthy L1 items**: `User.Roles` phantom navigation minting a shadow-FK column on
  `asp_net_roles` (MA-102, confirmed); an unnamed unique index silently flipping Identity's `EmailIndex`
  to unique while `RequireUniqueEmail` is off → duplicate-email registration throws a raw 500 (MA-103).

---

## Tier 3 (cosmetic / idiom — batch at leisure, not before lock-in)
~55 items: dead files (MA-105/111/113/204/207/208), aria-label gaps on EditorView-adjacent buttons
(MA-212/307/607/707), `RequireUserId` duplication collapsible to one Core extension (MA-210/308…),
stale doc pointers, comment debris, test-comment drift. All catalogued in `slices/*-findings.md`.

---

## Code economy — the direct answer to "does it earn its keep?"

**Pure wins (less code AND less fragility — do these):**
- Delete dead code: Razor Pages host + `AddRazorPages` (MA-105), `RedirectToLogin` (MA-111), unused Redis
  package (MA-113), `User.Roles` phantom nav+column (MA-102), 3 dead DTO/tombstone files (MA-204/207/208).
- **Unify the 13-member validation-exception family** (MA-008) — inconsistent property names + ctor shapes
  are the root cause of ~25 hand-rolled client `ThrowIfWriteFailedAsync` copies + a server name-suffix hack.
  Single biggest trade-off-free compression: less code *and* less fragility.

**Trades (less code, more machinery — your call, per your explicit-over-magic preference):**
- Export-writer DOM-walk visitor (~100 LOC, S3); triplicated StoryCard display statics (MA-209).

> **SUPERSEDED 2026-07-18 (Brian, WU-ResponsiveMerge):** the paragraph below assumed the
> Desktop/Mobile fork paradigm would continue and mobile would diverge as separate components.
> The single-responsive-site resolution (`middle_plan_v2.md` §Resolved;
> `canalave-conventions/render-and-layout.md` §"Responsive Layout Architecture") removed the
> paradigm itself: pairs merged into their pages, mobile variants deleted as unvalidated
> placeholders, MA-406 dissolved with the drawers, MA-209/MA-509 resolved during the merge.
> Future narrow-UX divergence, if any, rides the adaptivity ladder — not component pairs.

**Desktop/Mobile pairs — OUT OF SCOPE for the fix pass (audit verdict corrected 2026-07-17, Brian).**
The audit read the byte-identical `@code` in `StoryDesktop`/`StoryMobile` (and the other pairs) as a
merge candidate. That reading is **wrong for the actual workflow** and must NOT be acted on: desktop has
not been human-validated yet, layout changes are expected, and mobile will be scrutinized and diverged
*separately* after desktop is settled. The pairs are identical because **mobile is an unvalidated
placeholder copy, not wasteful duplication** — merging them now would collapse a seam that is meant to
diverge deliberately. Leave all Desktop/Mobile pairs (and the MA-406 filter-drawer shell) untouched.
Revisit code economy here only *after* the human desktop→mobile layout pass, if duplication genuinely
survives that. Corollary: all end-to-end verification to date was **desktop-only**, so mobile-specific
components are provisionally audited at best — treat mobile layout findings as pending that pass.

**False economies — examined and rejected (the answer to "should the backend shrink": no):**
assembly-scan DI (the explicit lists are documentation), merge test fixtures (a project for 50 LOC), merge
import readers (per-format), merge the TPT poll split / per-context methods / 6 settings forms (all settled,
field-set-distinct).

**Biggest single dead-weight block:** ~1,325 LOC of untouched Identity scaffold for features *not* in your
65 (2FA, passkeys, external login) — MA-610. A product decision: keep-for-later vs delete now.

---

## Are the conventions themselves current? (Bucket B — web-verified)
**Yes — 14 of 17 checked conventions verified current against live .NET 10 / EF Core 10 / RFC docs; only 3
drift, all under-specification, none dangerous.** This is load-bearing: it means "build to your own
standard" is the right instruction. The 3 drifts are doc-touch items for you:
- **BB-01** `[ValidatableType]` (layer3-logic.md): omits the required `AddValidation()` call + the "model
  must be `.cs` not `.razor`" source-gen constraint.
- **BB-02** (= MA-309) write-throttle rule is framed write-vs-read; current guidance selects by *cost* and
  ships a Concurrency Limiter for expensive authenticated parses (your import endpoints).
- **BB-03** (= MA-004/604) "IActiveUserContext won't exist in WASM" is factually dead post-Global-Flip.
- **Watch-item (not a defect):** `SerializeAllClaims` may omit RoleClaims in some setups (dotnet/aspnetcore
  #62923) — browser-verify WASM `<AuthorizeView Roles>` gating during any flip re-test.

---

## Coverage & confidence (honest statement)
- **Exhaustively read:** all product code in every feature cluster (Core/Server/SharedUI/Client), all three
  test tiers, all 19 convention files. Migrations excluded (generated); `Fimfiction`/`GeminiDiscussions`/
  screenshots excluded (not code).
- **Depth of the authz check:** write-paths audited at authorization depth in ~4 of ~15 endpoint clusters —
  hence the "sweep all endpoints" recommendation rather than "the 3 found are all of them."
- **Verification:** all 5 Tier-1s independently re-read by the main session (not taken on agents' word);
  proofs in `verification.md`. Coverage matrix 100% (20 hypotheses × 8 slices, no gaps).
- **Not automated-verified:** no builds/tests were run (read-only audit). Fixes should each get the
  regression test the finding names (several security fixes currently have *no* test because the code never
  did the thing being tested).
- **Deliberately NOT flagged** (looked wrong, verified settled — full list in `cross-slice.md` §E):
  bare-name Tailwind tokens (sanctioned dual style), `DateTime.UtcNow` everywhere (accepted testability
  trade), claims-baked-at-signin staleness (by design), the dev-tools raw-color exemptions (checker-sanctioned).

---

## Suggested sequence
1. **Before lock-in:** the 5 must-fixes, then the endpoint-authorization sweep (verification of the whole
   layer). Add the missing regression tests as you go.
2. **Before beta:** the Tier-2 fix-patterns (counters, not-found sweep, error-channel sweep, silent-catch
   sweep, the 2 L1 reopens).
3. **At leisure:** Tier-3 batches; the exception-family unification (pure win); decide the
   Identity-scaffold question (product call). **Desktop/Mobile is deferred to your post-fix desktop
   validation + separate mobile pass — do not merge the pairs now.**
4. **Doc-touch (yours):** BB-01/02/03 convention updates + the ~15 post-Global-Flip doc-staleness items.

Nothing here reopens a Settled Architectural Axiom. Findings against Stage-5 cells are flagged
"proposes reopen" in the slice files for your per-item sign-off; none was acted on.
