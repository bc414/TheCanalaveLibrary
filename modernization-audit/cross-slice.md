# Cross-Slice Comparison (main session synthesis)

98 findings across S0–S7 (13/23/13/9/9/11/11/9). Coverage matrix 100% (20 H × 8 slices).
The recurring classes below are where the SAME problem appears in multiple slices — the signature
failure mode of session-by-session generation. Each is one fix-pattern, not N unrelated bugs.

## A. Confirmed security must-fixes (5 Tier-1, all verified in verification.md)
| ID | Class | Location | Verdict |
|---|---|---|---|
| MA-201 | Stored XSS | Story LongDescription (Stories only) | CONFIRMED |
| MA-301 | Broken access control | 5/7 chapter write methods + GetChapterForEditAsync | CONFIRMED |
| MA-601 | IDOR | BadgeEndpoints /award, /, /display-order | CONFIRMED |
| MA-602 | Anon private-data read | UserProfileEndpoints (no RequireAuthorization) | CONFIRMED |
| MA-101 | Dead reconnect UI | ReconnectModal stale @Assets path | CONFIRMED (tier nuance) |

**THE #1 THEME — L5 endpoint authorization-deferral (MA-301/601/602):** one root cause (WU-L5Sweep
added endpoints with blanket RequireAuthorization but deferred per-resource authz; Global Flip made
them live). NOT systemic — S7 proved the high-privilege endpoints (Moderation/Spotlight/SiteSettings)
DO gate correctly; it slid through on 3 lower-privilege clusters. Bounded but the completion was
inconsistent → recommend a systematic authz sweep of ALL ~40 endpoint files as verification/closure.

## B. Recurring consistency classes (Tier 2/3 — one fix-pattern each)
1. **Not-found mechanism** (~13 sites): manual `NavigateTo("/not-found")` instead of `Nav.NotFound()` → deleted-entity URLs return 200 not 404 (F64/SEO). MA-202(S2 ×5), MA-304(S3 ×4), MA-404(S4), MA-606(S6 ×2), MA-708(S7). Right pattern EXISTS (BookshelvesPage, MessagesPage clean). One sweep.
2. **Feedback-channel drift** (~8 components): hand-rolled `<div role=alert>`/raw `ex.Message` instead of `InlineAlert`+`ExceptionPresenter` — the WU-ErrorHandling normalization didn't reach everything. MA-205(S2), MA-405(S4), MA-501/504(S5), MA-603(S6), MA-703/704(S7). One sweep.
3. **Bare unregistered silent catches** (~6): MA-001/002(S0), MA-206(S2), MA-303(S3), MA-503(S5), MA-704(S7). logging.md registry claims 1 sanctioned site; reality ~6 unregistered. One sweep + registry update.
4. **Non-atomic counter / lost-update race** (2 confirmed): MA-502(S5 RecordSuccess), MA-705(S7 BlogPost LikeCount). Tracked `++`/absolute-write in multi-reader paths; the codebase's own atomic-ExecuteUpdate rule fixes both. Data-integrity — worth Tier-2 priority.
5. **RequireUserId duplication** (~8 copies): MA-210(S2 ×4), MA-308(S3), + inline elsewhere. One `IActiveUserContext.RequireUserId()` Core extension collapses all.
6. **aria-label on EditorView-adjacent submit** (~5): MA-212(S2), MA-307(S3), MA-607(S6), MA-707(S7). testing.md collision rule + a11y. One sweep.
7. **Validation-exception family drift** (MA-008, S0): 13 types, inconsistent property names + ctor shapes → root cause of the ~25 hand-rolled client ThrowIfWriteFailedAsync copies + the server name-suffix-matching hack. One unification = the biggest pure-win compression lever.

## C. Code-economy roll-up (answers "does it earn its keep / reduce?")
**Pure wins (do these — less code AND less fragility, no indirection cost):**
- Dead files/refs: Razor Pages host (MA-105), RedirectToLogin (MA-111), Redis pkg (MA-113), User.Roles phantom nav+column (MA-102/confirmed), StoryListingPageDto (MA-207), StoryCharacterRelationship tombstone (MA-208), ChapterNames dead projection (MA-204). ~small each.
- Validation-exception unification (MA-008) → collapses ~25 client + server sites.
- Audience-badge statics ×3 (MA-509), mod-page error boilerplate (rides MA-703).

**Trades (less code, MORE machinery/indirection — Brian decides per the explicit-over-magic preference):**
- **Desktop/Mobile pairs — the headline UI lever:** StoryDesktop/Mobile (S2, byte-identical @code, ~450+670 LOC — STRONGEST merge candidate) and HomeDesktop/Mobile (S1) genuinely fail the "separate only when structurally different" test today. The Discovery mobile filter-drawer shell is verbatim-triplicated (MA-406 — the codebase's own "extract at 3rd consumer" threshold is MET). Messages/Group pairs (S5) are JUSTIFIED splits (false economy to merge). BlogPost (S7) below norm.
- Export-writer DOM-walk visitor (~100 LOC, S3). Triplicated StoryCard/Desktop/Mobile display statics (MA-209).

**False economies — considered and REJECTED (the answer to "should the backend be reduced": no):**
- Assembly-scan DI to replace ~260 explicit registration lines (S1) — the lists are load-bearing documentation.
- Merge near-dup test fixtures / TestImages (S1) — a shared project for 50 LOC.
- Merge import readers (S3) — genuinely per-format.
- Merge SitePoll/BlogPostPoll TPT split (S7), per-context comment/blog/poll write methods (S5/S7) — settled per-context justification.
- Merge the 6 settings forms (S6) — correctly separate by field-set.

**Biggest single dead-weight block:** ~1325 LOC of untouched ASP.NET Identity scaffold for features NOT in the 65-feature set (2FA, passkeys, external login) — MA-610 (S6). Product decision: keep-for-later vs delete.

**Verdict on the question:** the backend earns its keep (every compression idea rejected as false economy); the honest reduction is bounded and concentrated in UI Desktop/Mobile pairs + the Identity scaffold. Realistic ceiling ~10–15%, of which the trade-off-free portion is small (dead code + the exception unification).

## D. Doc-vs-code staleness (Bucket A doc-touch, ~15 items — for Brian, not the ecosystem agent)
MA-004/010/013 (S0), MA-104/108/114/118 (S1), MA-506/605/610/604 (TODOs + scaffold + IActiveUserContext-in-SharedUI), content-safety/BaseBlogPost contradiction, MA-123 (throw-type). Mostly post-Global-Flip drift. Bucket-B agent rules on the ones needing an ecosystem judgment (MA-004/604, MA-123, MA-309, Nav.NotFound, MA-101).

## E. Deliberately NOT flagged (looked wrong, verified settled — with reference)
- Bare-name semantic Tailwind tokens (bg-surface etc, 108 uses) — sanctioned dual style, CI-green.
- DateTime.UtcNow everywhere (no TimeProvider) — codebase-wide accepted testability trade.
- Series.Description unbounded — SeriesConstants documents deliberate.
- Import parse endpoints unthrottled — reasoned in each endpoint (commit is the cost gate) → but MA-309 B-flags whether the rule SHOULD cover them (ecosystem agent rules).
- Claims-baked-at-signin staleness — by design (render-and-layout.md).
- Manual DevTools/DesignGallery/DevLoginBar raw-color — check-design-tokens.ps1 explicitly exempts.
