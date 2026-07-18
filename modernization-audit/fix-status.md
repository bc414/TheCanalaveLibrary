# Modernization-Audit Fix Status — done / not-done map
**Companion to `report.md` · last updated 2026-07-18 (WU-AuditFixPass-2 closed the remainder)**

This maps every item in `report.md` to its status. Fixes landed across two work-units:
**WU-AuditFixPass** (Tier-1 + Tier-2 headline patterns) and **WU-AuditFixPass-2** (the endpoint-authz
sweep + everything else the first pass deferred). `.claude/workplan.md` has the full narrative for
both; per-cluster `.claude/audit/*.md` files carry dated Stage notes. Finding IDs (MA-*) resolve in
`slices/*-findings.md`; BB-* in `bucket-b.md`. Where a fix deviated from the report's suggested shape
it's flagged **[deviation]** / **[added]**.

Status legend: ✅ done + regression-tested + browser-verified · 🧑 needs a human/product decision
(not actioned) · ⛔ explicitly out of scope (do not act).

---

## The one big before-lock-in item — NOW DONE ✅

**✅ The systematic endpoint-authorization sweep** (report.md "#1 structural finding", lines 43–54).
All 38 `*Endpoints.cs` files were audited at authorization depth (7 parallel readers, one per cluster;
each traced every route into its backing service). The audit's caveat held — "we found 3" ≠ "there are
exactly 3": **7 additional holes** surfaced beyond MA-301/601/602 and are fixed, plus MA-702's edge-gate
gap. The service layer was disciplined throughout; holes clustered in the mechanically-generated
read/endpoint layer, exactly as `report.md` predicted. Full route×verdict tables for all seven clusters
are the closure record (in the WU-AuditFixPass-2 agent outputs; the fixes are enumerated below and in
`workplan.md`).

New holes closed by the sweep (all with regression tests + browser verification):
| Where | Hole | Fix |
|---|---|---|
| `ServerStoryReadService.GetStoryForEditAsync` | any authed user could read any story's edit DTO (incl. moderation-only `PostApprovalStatus`) | owner-throw (→403); endpoint wrapped in `ExecuteWriteAsync`; client + `StoryEditorPage` mirror the MA-301 chapter shape |
| `ServerStoryReadService.GetStoryIdsByAuthorAsync` | `IgnoreQueryFilters(["ContentRating"])` keyed to a **client** authorId — enumerate another author's rating-hidden story ids | bypass only when `authorId == ActiveUser.UserId` (browser: owner 5, non-owner mature-off 3) |
| `ServerBlogPostReadService.GetByAuthorAsync` | forged `includeUnpublished=true` leaked drafts to anyone | owner-derived flag (degrades to public view) |
| `ServerBlogPostReadService.GetForEditAsync` | any authed user could read any blog draft's full content | owner-throw (→403); client + editor mirror the pattern (browser: cross-author `/edit` → 403) |
| `ServerUserStoryInteractionReadService.GetFavoriteStoryIdsAsync` | client-supplied `includePrivate` leaked hidden favorites | server-derived (`activeUser.UserId == userId`), MA-602 pattern; client stops sending it |
| `ServerChapterReadService` toc/list/versions | draft chapter **metadata** (titles, word counts) enumerated to anyone | `IsPublished \|\| Story.AuthorId == viewer` in all three (browser: non-author sees only published) |
| `ServerManualTreeSearchReadService` favoriters | pivot leaked who favorited a rating-hidden/taken-down story | anchored to the `visible` story set, matching sibling sections |
| Tag write routes | no `.RequireAuthorization()` floor (service `RequireMod` covered it) | floor added (defense-in-depth); rate-limit test now authenticates as a mod |

---

## MUST-FIX — all 5 Tier-1 done ✅ (WU-AuditFixPass)

| ID | Status | Notes |
|---|---|---|
| MA-602 UserProfileEndpoints anon private read | ✅ | `includePrivate` server-derived; **[added]** `/bio` route gated too. Browser-verified: Private-profile (LurkerDelta) returns empty to a non-owner despite `?includePrivate=true`; owner sees it. |
| MA-301 chapter write/draft-read author gates | ✅ | all 5 ungated write methods + `GetChapterForEditAsync` enforce `Story.AuthorId == ActiveUser.UserId`; the `/edit` 500→403 wire gap was caught in the browser pass and locked with `ChapterEndpointsTests`. |
| MA-601 BadgeEndpoints IDOR | ✅ | curation/display-order derive `userId` server-side; **[deviation]** `/award` removed entirely (server-internal only). |
| MA-201 story LongDescription stored XSS | ✅ | `ServerStoryWriteService` sanitizes on create + update. |
| MA-101 ReconnectModal stale asset path | ✅ | `@Assets` path corrected; module resolves 200. |

---

## SHOULD-FIX (Tier 2) — all done ✅

Report's 7 headline bullets (WU-AuditFixPass): non-atomic counters (MA-502/705), dropped interaction
write (MA-401), not-found 200→404 sweep (MA-202/304/404/606/708), error-channel drift
(MA-205/405/501/504/603/703/704), HTTP status wrong side (MA-123/701), unregistered silent catches
(MA-001/002/206/303/503), L1 reopens (MA-102/103) — **all ✅**.

The slice-file Tier-2 findings the report didn't headline (cross-slice §B) — **all ✅ (WU-AuditFixPass-2):**
| ID | Status | Notes |
|---|---|---|
| MA-402 ResultsFilterPanel snapshot-vs-async-seed | ✅ | `ResultsFilterPanel` + `UserStoryInteractionFilter` resync-until-interaction (TreeSearchControls pattern). Browser-verified: "Hide stories I've ignored" renders **checked** on `/discover` from the async seed. +2 RazorComponents tests. |
| MA-403 AddToCustomListMenu DI-in-leaf | ✅ | split into `AddToCustomListMenu` wrapper + `AddToCustomListMenuInner` (SavedTagSelection pattern). |
| MA-203 StoryPage sequential loads | ✅ | 6 independent loads → `Task.WhenAll` in a shared `LoadSupplementaryAsync` (both lifecycle methods). Browser-verified full render. |
| MA-204 dead ChapterNames projection | ✅ | projection + DTO field + record member removed (hottest-read correlated subquery gone; also closed a latent draft-title leak). |
| MA-302 anon reading-progress ping 401 | ✅ | dropped `RequireAuthorization()` — anonymous scroll 202-no-ops. Browser-verified 202. |
| MA-706 BlogPost read→write DI bind | ✅ | `IBlogPostReadService` rebound to `ServerBlogPostReadService`. |
| MA-003 stale logout ReturnUrl | ✅ | computed at render time / on `LocationChanged`, not cached once per circuit. |
| MA-005 CanalaveTypeahead unguarded search | ✅ | caller `SearchMethod` wrapped (logs Warning, degrades to empty); stale results cleared before a new search. |
| MA-702 mod-write edge role gate | ✅ | named `AuthorizationPolicies.RequireModerator` registered + applied to every mod-only group. Browser-verified both directions (non-mod 403, AdminUser 200/204). |
| MA-508 group-create unthrottled | ✅ | `ContentCreate` throttle added. |

---

## Tier 3 (~55 items) — done ✅ (WU-AuditFixPass-2)

- **Dead code:** MA-105 (Razor Pages host + `AddRazorPages` + `Server/Pages/`), MA-106 (`AddApiEndpoints`),
  MA-111 (`RedirectToLogin`), MA-113 (Redis pkg + `layer7-redis.md` pointer), MA-207 (`StoryListingPageDto`),
  MA-208 (`StoryCharacterRelationship` tombstone), MA-204 (dead projection).
- **aria-labels:** MA-212/307/607/707 (all 4 EditorView-wrapping submit buttons + ChapterList toggle).
- **RequireUserId:** MA-210/308 collapsed to one `IActiveUserContext.RequireUserId()` Core extension.
- **Projection idiom:** MA-409/507 `(int?)`→anonymous-type FK projections.
- **Misc:** MA-305 (`ImportReviewPanel` token), MA-306 (attribution fire-and-forget logged), MA-011
  (`ToastHost` disposal CTS), MA-511 (`CardShadowClass` inlined), MA-109/115/116/117/119/120/213 (comment/
  test debris), MA-121 (new `ConfirmDialogTests`).

---

## Code economy

- **Pure wins ✅:** dead-code deletions (above); **MA-008** the 15-member validation-exception unification
  under `CanalaveValidationException` (fixed a latent bug — 3 types fell through to the generic message) +
  MA-407 (TagEndpoints local `ExecuteWriteAsync` deleted) + the shared client `ThrowIfWriteFailedAsync`
  replacing ~14 hand-rolled copies. MA-102 ✅ (L1 reopen, WU-AuditFixPass).
- **Trades 🧑 (Brian's explicit-over-magic call — NOT actioned):** export-writer DOM-walk visitor (MA-209
  statics), MA-406 filter-drawer shell, MA-510 comment-read generic. Left as-is.
- **Desktop/Mobile pairs ⛔ DO NOT ACT** (report lines 104–113): untouched.
- **MA-610 Identity scaffold (~1,325 LOC) 🧑 product decision:** untouched.

---

## Bucket B — conventions current? done ✅ (doc-touch, WU-AuditFixPass-2)

- **BB-01** `[ValidatableType]` (layer3-logic.md): added the `AddValidation()` call, the ".cs-not-.razor"
  source-gen constraint, and the root-only annotation correction.
- **BB-02** (= MA-309): write-throttle rule reframed cost-not-write-vs-read; an `"ImportParse"` concurrency
  limiter now caps the three import parse routes (Program.cs).
- **BB-03** (= MA-004/604): the "IActiveUserContext won't exist in WASM" premise deleted; the rule restated
  as testability discipline with 2 ratified exceptions (UserActivityTracker, SettingsPage).
- **Post-Global-Flip doc-staleness (cross-slice §D):** MA-104 (default-allow recorded as operative posture),
  MA-108/114/118/122/123-note, content-safety/BaseBlogPost TPT-filter contradiction corrected against code,
  MA-506/605/709 (stale-WU TODOs retargeted). All ✅.
- **Watch-item (not a defect):** `SerializeAllClaims`/RoleClaims (dotnet/aspnetcore #62923) — browser
  re-verified during this pass: WASM `<AuthorizeView Roles>` gating works (AdminUser reaches `/mod/*`; the
  edge `RequireModerator` policy 403s a non-mod). No issue observed at N=1.

---

## What was verified, and how

- **Automated:** full three-tier suite green after the pass — **Unit 712 / RazorComponents 646 /
  Integration 734, 0 failures.** New regression tests (22): `StoryEndpointsTests`, `BlogPostEndpointsTests`,
  `UserStoryInteractionEndpointsTests`, `ChapterDraftVisibilityTests`, `ModerationEndpointsTests`, +2
  `ResultsFilterPanelTests` resync tests, `ConfirmDialogTests`. Updated for new behavior: 2 chapter tests
  (draft visibility), `HttpRateLimitTests.TagWrites_*` (auth floor), 2 Unit client tests (401-mapping split).
- **Browser E2E** (server-only path, live DB): all 7 sweep holes + MA-702 (non-mod 403 / mod 200-204) +
  MA-302 (anon 202) + MA-402 (Ignored checkbox checked) confirmed over the wire; StoryPage + `/discover`
  render clean, zero console errors.
- **Not done here (deliberate):** everything marked 🧑/⛔ above — Desktop/Mobile merges, Identity-scaffold
  prune, the extract-or-not seams, and the 3 residual client 401-mapping deviants (Group/Messaging/USI).
