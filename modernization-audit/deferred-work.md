# Modernization-Audit — Deliberately Deferred Work
**Handoff doc · 2026-07-18 · everything WU-AuditFixPass / -2 left untouched, and why**

The two fix work-units (see `fix-status.md`, `.claude/workplan.md`) closed every **actionable** audit
finding: all 5 Tier-1, all Tier-2, the full endpoint-authz sweep, the Tier-3 mechanical batch, MA-008,
and the Bucket-B doc-touches. This file is the residue — the items skipped **on purpose**. Nothing here
is a known live security hole (the sweep confirmed that). Finding IDs (MA-*) resolve in
`slices/*-findings.md`; line numbers are as-of the audit (2026-07-17) and may have drifted a little.

Each item says: **what it is · why it was left · what acting on it involves.** Grouped by the kind of
decision it needs, hardest-to-reverse first.

---

## 1. ⛔ DO NOT ACT — the Desktop/Mobile seam

**Status: the audit verdict was explicitly corrected to "do not merge" (report.md lines 104–113, Brian,
2026-07-17).** These read as duplication but are an intentional seam: mobile is an **unvalidated
placeholder copy**, and mobile layout will be scrutinized and diverged *separately* after the human
desktop→mobile pass. Merging now collapses a seam meant to diverge.

- `StoryDesktop`/`StoryMobile` (byte-identical `@code`, ~450+670 LOC — the "strongest merge candidate").
- `HomeDesktop`/`HomeMobile`.
- **MA-406** — the mobile filter-drawer overlay shell, verbatim-triplicated across `SearchMobile`,
  `BookshelvesMobile`, `TreeSearchMobile` (`layer3.5-structure.md`'s "extract at 3rd consumer" threshold
  is technically met, but it's still a Desktop/Mobile-side concern).

**Do not touch until after the desktop→mobile layout pass, and only if duplication genuinely survives it.**
Corollary the audit flagged: all E2E to date is desktop-only, so mobile components are provisionally
audited at best.

---

## 2. 🧑 Product decisions (need a human call, not an engineering one)

### MA-610 — the Identity scaffold (~1,325 LOC, the single biggest dead-weight block)
`Server/Identity/Pages/**` carries untouched ASP.NET-Identity scaffold for features **not** in the
65-feature set: two-factor auth (`LoginWith2fa`, `LoginWithRecoveryCode`, `Manage/EnableAuthenticator`/
`Disable2fa`/`ResetAuthenticator`/`GenerateRecoveryCodes`/`TwoFactorAuthentication`), passkeys
(`Manage/Passkeys`/`RenamePasskey`), external-login (`ExternalLogin`, `Manage/ExternalLogins`). No provider
is configured. **NOT a namespace/asset defect** — the historical `Components.Account → Identity` drift and
the `PasskeySubmit.razor.js` asset path are both already resolved.
- **The call:** keep-as-framework-flow (leave it — it's the stock Identity UI, harmless and inert) vs.
  prune the unbuilt-feature pages + their `Manage/ManageNavMenu.razor` entries. Removal is ~L effort but
  purely subtractive.

### Beta-scope / launch / legal / a11y rows — `middle_plan_v2.md` "Decisions that need you"
Untouched by this pass because they are genuinely yours (rows 1, 3-Feature-51/56, 4, 6, 8, 10, 12): beta
logistics, launch mechanics, legal/policy track, accessibility depth. Listed here only so a fresh session
knows they exist and are **not** engineering-blocked — see `middle_plan_v2.md` for each.

---

## 3. 🧑 Code-economy "trades" — less code, more machinery (your explicit-over-magic call)

The audit classed these as reductions that add indirection, so it left the direction to you. Each is a
pure refactor (no behavior change); none is blocking.

- **MA-510 — `ServerCommentReadService` 4 near-dup read methods.** `GetChapterCommentsAsync`/
  `GetGroupCommentsAsync`/`GetUserProfileCommentsAsync`/`GetBlogPostCommentsAsync` are ~55-line clones
  (~165 of the file's 264 LOC); the two-step root-paging + `rootIds.Contains` projection + in-memory
  ordering tail is byte-identical ×4. A private generic `PageCommentsAsync<TComment>(IQueryable<TComment>
  roots, …)` over `BaseComment` collapses the tail; the public per-context methods stay. (The settled
  "per-context method" justification is about the *write*-side verification difference — the read side has
  no verification step, so it doesn't cover this.)
- **MA-209 — triplicated StoryCard display statics.** `WordCountDisplay` + `StatusLabel` + `RatingLabel`
  are verbatim across `StoryCard`/`StoryDesktop`/`StoryMobile` (~`:163-194`/`:196-227`/`:189-220`).
  `StatusBadges.ForStatus/ForRating` already centralizes the *class* half; a parallel `StoryDisplayFormat`
  static would centralize the *label* half and fix the shared `999,999→"1000K words"` edge quirk once.
  ⚠️ Touches `StoryDesktop`/`StoryMobile` — coordinate with the §1 Desktop/Mobile decision.
- **MA-509 — triplicated audience-badge statics.** `AudienceBadgeLabel` + `AudienceBadgeClasses` byte-
  identical across `GroupCard`/`GroupDesktop`/`GroupMobile` (`:40-52`/`:251-263`/`:193-205`). Pure
  viewer-independent switches. Cross-slice §C actually calls this a **pure win** — I left it only because
  it touches the Group Desktop/Mobile files (same §1 caution). Lowest-risk of this group.
- **Export-writer DOM-walk visitor (~100 LOC, S3).** An `ExportDom`-walking visitor would de-duplicate
  across the six export writers; the audit judged the per-format writers "genuinely distinct" and the
  visitor a machinery trade. Report explicitly leaves this to you.
- **MA-107 — DI double-registration shape.** 7 clusters still register the concrete write class twice
  (`AddScoped<IXRead, ServerXWrite>()` + `AddScoped<IXWrite, ServerXWrite>()` → two instances per scope):
  Group, Series, StoryLineage, StoryArc, SavedTagSelection, CustomList, Notification (BlogPost was fixed
  as MA-706). Moderation/Badges use a forwarding delegate (`IXRead` resolved from the `IXWrite` instance)
  whose comment declares instance-unity the point. Unify on one shape — the **forwarding delegate is the
  safer default**. Mechanical, but a cross-cutting one-shot in `Program.cs`.
- **MA-408 — SavedTagSelection N+1.** `ServerSavedTagSelectionReadService.GetPublicSelectionsByUserAsync`
  (`:54-72`) loops `HydrateDetailAsync` (2 queries per selection). Low volume (a user's public saved
  selections, surfaced on the profile TagSelections tab). A single batched entry-join over all ids removes
  the loop.

---

## 4. ✅ RESOLVED (2026-07-18) — Status-code seams — 401 where 400 is semantically right

**Fixed.** `EndpointHelpers.ExecuteWriteAsync` mapped `InvalidOperationException` → **401** (auth safety
net), and several **business-rule** rejections threw that same type, surfacing as 401 instead of the
accurate 400. The chosen fix shape was the typed `*ValidationException` route (no shared-helper change
needed — every new type derives from `CanalaveValidationException`, which the 400 arm and
`ExceptionPresenter` already match, MA-008):

- **MA-505** (Following/Recommendations): self-follow / self-vouch / not-following now throw
  `FollowingValidationException`; hidden-gem-limit / spotlight-limit now throw
  `RecommendationValidationException`. All → 400.
- **MA-611** (Badges/Profiles): badge `/display-order` not-yet-earned key now throws
  `BadgeValidationException`; `/author` non-owned/unpublishable pinned story now throws
  `UserSettingsValidationException`. All → 400.

Client translators updated (`ClientFollowingWriteService` 400 → `FollowingValidationException`;
`ClientBadgeWriteService` / `ClientUserSettingsService` gained a 400 arm; `ClientRecommendationWriteService`
already reconstructed its validation type). Only the genuine unauthenticated-caller guards still map to
401. Endpoint doc comments, interface docs, and `layer5-wasm.md` "The Error-Translation Contract" updated;
integration tests retyped/renamed. Verified: `dotnet test` green + browser end-to-end (self-follow returns
400, not 401).

---

## 5. 🧑 Organization — just-in-time moves (the convention already defers these)

`SKILL.md` "Code Organization" says legacy technical-layer folders empty out **as touching WUs pass
through**, not in a big-bang move. These are the known stragglers so none is invisible:

- **MA-112 / MA-608** — `Server/Services/{UserDeletionService → Identity/, ServerDeviceDetectionService}`,
  `Client/Services/{WasmDeviceDetectionService}`, `Core/ServiceInterfaces/{IDeviceDetectionService}`,
  `Core/Models/` (6 `partial` scaffold files for unbuilt features), `SharedUI/Pages/{NotFound.razor →
  Errors/ or Home/}`. `UserDeletionService` also still uses the pre-primary-constructor idiom.
- **MA-012** — `MainLayout.razor` still under `Server/Components/Layout/` (the folder family the Identity
  move was meant to empty); vertical convention places it in `Server/Identity/`.

Move each when its cluster is next touched (grep the old dotted-path namespace per SKILL.md's rename rule).

---

## 6. 🧑 Small cleanups the audit surfaced but no pass claimed

- **MA-006** — `ContentSurface` hardcodes the 3 `ReadingBackground` palettes as raw hex in a `style=`
  attribute (`:35-37`), which `layer4-style.md` calls a defect. Either tokenize the three palettes or
  record a sanctioned exception (like the DevLoginBar/DesignGallery raw-color exemptions already in
  `check-design-tokens.ps1`).
- **MA-007** — `ContentSurface` retains the pre-ratification `FrameStyle` int param (magic 2/3/default,
  `:47`) that its own header says is removed once the gate ratifies a treatment (ratified 2026-07-10). It
  survives only for the dev gallery's comparison switcher — convert the gallery to a private variant, then
  delete the param.
- **MA-211** — `ServerStoryArcWriteService` copies primary-ctor params into `_writeDb`/`_activeUser`
  fields (`:20-22`) where every sibling write service uses the params directly. Cosmetic idiom divergence.

---

## 7. Informational — non-hole observations from the authz sweep

The 7 sweep readers flagged these as **not** access-control holes but worth a human eye. None was acted on.

- **`GetRecommendedStoryIdsByUserAsync`** (`ServerRecommendationReadService:152-153`) omits the
  `StatusId == Approved` filter the sibling reads apply. Harmless under auto-approve, but **once
  moderation can reject a recommendation** it would surface pending/rejected rec *story ids* on the public
  profile tab. ⚠️ Worth checking whether WU34 moderation already makes this live — if so it's a real
  (minor) leak, not just latent.
- **`RecordAttributionSourceAsync`** (`ServerRecommendationWriteService:330-343`) never validates that the
  `recommendationId` exists or belongs to `storyId` — a user can seed a bogus self-attribution that later
  feeds `RecordSuccessAsync` credit to an arbitrary recommendation. Self-scoped integrity, not privilege
  escalation.
- **`AssignStoryToFolderAsync`** (`ServerGroupWriteService:347-369`) doesn't verify `folder.GroupId ==
  groupStory.GroupId`, so an admin of group A could file A's story into a group-B folder id. Caller must be
  admin of the *story's* group, so it's a data-integrity nuance, not privesc.
- **DevDiagnostics** (`/dev/*`) is environment-gated (`IsDevelopment()`), never mapped in production — but
  a dev server exposed beyond localhost hands out anonymous `login-as`/`delete-user`. Environmental risk,
  not code.
- **Public avatar URLs** (`ImageEndpoints` `/uploads/{**key}`) are anonymously fetchable even for a
  Private-profile user who knows the URL. URLs are unguessable UUIDs and gated reads never disclose them —
  standard CDN model, accepted.
- **`GET /api/polls/by-blog-post/{id}`** exposes poll name/description of polls attached to an unpublished
  draft post (tallies/voter names still blanked server-side). Minor.

---

## 8. ⚠️ One behavior change already shipped — know it exists

MA-008's shared client translator (`ClientHttpHelpers.ThrowIfWriteFailedAsync`) maps a bare **401 →
`InvalidOperationException`** for the ~14 converted client write services (previously
`UnauthorizedAccessException`). No catch site reads the 401 message, **but**: an expired-cookie 401 on
those services now routes to the generic-error path instead of a `catch (UnauthorizedAccessException) →
_forbidden` state. If a future session sees "editor pages stopped showing the forbidden banner on session
expiry," this is why — it's intended (403 still maps to `UnauthorizedAccessException`; only the bare-401
race changed). Three client services were **left on their old deviant mappings** and not converted:
`ClientGroupWriteService` (403 content-rating disambiguation), `ClientMessagingWriteService`
(403→`MessagingPermissionException`), `ClientUserStoryInteractionReadService`/`WriteService`
(`ArgumentOutOfRangeException` mapping). Converting those three is optional cleanup, not a bug.

---

## Suggested pickup order for a fresh session

1. **Cheapest wins first:** MA-509 (pure static extract), MA-006/007/211/012 (small cleanups),
   MA-408 (one N+1).
2. **One coordinated change each:** MA-107 (DI shape). ~~MA-505/611 (status-code seams)~~ — DONE
   2026-07-18, see §4.
3. **Verify then decide:** the item-7 observations — especially the `GetRecommendedStoryIdsByUserAsync`
   moderation-filter question (confirm live vs latent before deciding urgency).
4. **Only after the desktop→mobile human pass:** MA-209 and the §1 Desktop/Mobile items.
5. **Your call, no rush:** MA-610 Identity scaffold, MA-510 + export-visitor trades, the org just-in-time
   moves (fold into whatever WU next touches those clusters).
