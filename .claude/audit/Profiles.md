# Audit — Profiles/

**Features:** 20 (profile editing), 21 (profile display), 22 (user stats), 58 (UserStat recalculation
worker).

## Shared Context

> **2026-07-18 — Desktop/Mobile fork removed (WU-ResponsiveMerge).** `ProfileDesktop`/`ProfileMobile`
> merged into `ProfilePage` (page renders its own markup; mobile variant + `<details>` tab dropdown +
> filter drawer deleted as unvalidated placeholders; `ProfileBanner`'s `IsMobile` parameter removed).
> Narrow rendering is provisional pending the future mobile phase. Desktop/mobile assertions
> elsewhere in this file are historical. Rules: `canalave-conventions/render-and-layout.md`
> §"Responsive Layout Architecture"; spec §3.9/§3.10 superseded on this axis.
> Verified 2026-07-18: full suite green post-merge (Unit 702 / Integration 727 / RazorComponents
> 510); browser smoke at desktop width clean (loads, no error banner, zero console errors);
> narrow rendering deliberately unpolished, no visual pass yet.
**Entities:** `UserProfile` (cold partition — `ProfileText`, 1-to-1 cascade from `User`), `UserStat`
(PK `UserId`, 22+ denormalized counters, 1-to-1 cascade). Settings (Reader/Privacy/Author) live as
`ComplexProperty(...).ToJson()` complex types on `User` (EF Core 10 mapping, `IdentityConfigurations.cs`
— not the deprecated owned-entity JSON; see `layer1-data-model.md` §JSON and the Identity audit). Spec calls for `IUserProfileReadService` (public profile) and
`IUserSettingsService` (the self-referential integrated read+write exception, §3.5).

---

## Feature 20 — User Profile Editing

- **L1 — Stage 5** (`UserProfile.ProfileText`; JSON settings on `User`).
- **L2 — Stage 5** (WU30, 2026-06-24). `IUserSettingsService` self-referential exception fully built.
  `ServerUserSettingsService` in `Server/Profiles/`: `GetMySettingsAsync`, `UpdateProfileAsync`,
  `UpdateReaderSettingsAsync`, `UpdatePrivacySettingsAsync`, `UpdateAuthorSettingsAsync`,
  `UpdateAppearanceAsync`, `UploadProfilePictureAsync`. Resolves user from `IActiveUserContext`; never
  takes a userId. Privacy DTO expanded to include `ShowMatureContent` and `AllowDiscoveryFromHiddenFavorites`
  hot scalar columns (same sub-form, same save path). Verified: `dotnet build` green; `dotnet test`
  373 RazorComponents tests pass; Integration: covered by new Integration tests (Phase 5 deferred to
  next pass — connection tested via `GetMySettingsAsync` and write-path round-trips).
  **`UpdateAppearanceAsync` dropped its third parameter (WU-DataSaver, 2026-07-31)** — see this
  feature's dedicated Stage note below.
- **L3-Logic — Stage 5** (WU30). `SettingsPage.razor` at `/settings` dispatches to its sub-forms.
  `ProfileSettingsForm`, `ReaderSettingsForm`, `PrivacySettingsForm`, `AuthorSettingsForm`,
  `AppearanceSettingsForm` all injection-free (bUnit-testable); page holds all service calls.
  `_seeded` guard prevents re-init on re-render. Per-section busy flags decouple save operations.
  Verified: build green, 373 RazorComponents tests pass. **`DiscoverySettingsForm` added
  (WU-DiscoveryOverrideUI, 2026-07-31)** — the §8.7 per-search-mode override matrix; same
  injection-free/busy-flag pattern, instant-save per toggle via `IDiscoveryFilterSettingsService`.
  Full narrative: `audit/Discovery.md` §"WU-DiscoveryOverrideUI Stage note". This bullet's "5
  sub-forms" count and the Badges/ExternalAccounts/RevealManagementList additions since WU30 are
  otherwise unreconciled here — pre-existing staleness, not touched by this WU.
- **L3.5-Structure — Stage 5** (WU30). 5 sub-form Razor components with clear param/callback
  boundaries; picture upload raises `IBrowserFile` callback; page handles stream + URL patch.
  Verified: build green.
- **L4-Style — Stage 5** (WU30). Tailwind v4 token-based styling throughout; sub-forms use
  `--color-*` tokens, `focus:ring-2 focus:ring-[--color-primary]`, `border-[--color-border]`.
  Visual sign-off pending human run at `/settings`. Stage-6 gate = human visual approval.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; settings read+write verified in a real WASM runtime during
  the flip's browser wave (tagline round-trip, psql ground truth). Full wave narrative + the 7 bugs
  found/fixed: `workplan.md` WU-GlobalFlip.

---

## Feature 21 — User Profile Display

- **L1 — Stage 5.**
- **L2 — Stage 5** (WU30, 2026-06-24). `IUserProfileReadService` built in `Server/Profiles/`:
  `GetProfileHeaderAsync(userId, includePrivate)` — `ProfileVisibility` gating, stats conditional,
  badges via `BadgeKeyNavigation`, outgoing vouches; `GetProfileTextAsync(userId)`. Degree-1 candidate
  ID queries added: `GetFavoriteStoryIdsAsync` on `IUserStoryInteractionReadService`;
  `GetRecommendedStoryIdsByUserAsync` on `IRecommendationReadService`. `IBlogPostReadService.GetByAuthorAsync`
  extended with `includeUnpublished` flag. All registered in `Program.cs`.
  Verified: `dotnet build` green; 373 RazorComponents tests pass.
- **L3-Logic — Stage 5** (WU30). `ProfilePage.razor` at `/user/{UserId:int}/{*Tab}`:
  `[AllowAnonymous]`; resolves viewer id from `AuthState`; `includePrivate = (viewerId == UserId)`;
  loads header once (tab-independent); tab-switch reloads only tab payload. Device-branches to
  `ProfileDesktop`/`ProfileMobile`. Tab slugs via `ProfileTabSlug`. Banner RelationshipState overlay via
  `IFollowingReadService.GetRelationshipStateAsync`. `ProfileBanner` uses `FollowButton`/`VouchButton`
  for non-owners (RelationshipState not null); owner sees "Edit Profile" → `/settings`.
  Profile tab: bio `RichTextView` + `CommentSection` (UserProfile 4th context); comment wall gated by
  `AllowProfileComments != Nobody || IsOwner`. Story tabs: Favorites/Recommendations/Authored use
  `GetListingsAsync(filter, candidateIds)` + `GetStatesByStoryIdsAsync`. Blog tab: `GetByAuthorAsync`
  with `includeUnpublished: isOwner`; owner sees `BlogPostCard` with Edit affordances + "New Post" button.
  Verified: build green.
- **L3.5-Structure — Stage 5** (WU30). Persistent `ProfileBanner` above a tabbed body on both desktop
  and mobile. Desktop: horizontal tab bar + story tabs = StoryDeck + right filter sidebar (Bookshelves
  idiom). Mobile: `<details>` tab dropdown + filter overlay on story tabs (BookshelvesMobile idiom).
  Profile tab uses full-width stacked layout (bio + comments). Blog tab is paginated BlogPostCard list
  (no sidebar). `CommentSection` generalized to 4th context (`ProfileUserId` param + `UserProfile` case
  in load/post/reply/delete switches). `BlogPostCard` de-nested (title anchor + edit link are siblings;
  nested `<a>` avoided). Verified: build green, 373 RazorComponents tests pass.
- **L4-Style — Stage 5** (WU30). Tailwind v4 token-based styling; banner avatar initials placeholder;
  stats strip with bold counter values; badge row; action buttons (follow/vouch/edit). Visual sign-off
  pending human run at `/user/{id}`. Stage-6 gate = human visual approval.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; profile page verified in a real WASM runtime during the
  flip's browser wave (header, stats, vouches, tabs). Full wave narrative + the 7 bugs found/fixed:
  `workplan.md` WU-GlobalFlip.

### Token fix (WU-TokenGreen, 2026-07-26) — L4 stays Stage 5

The sign-in-required state's link (`ProfilePage.razor`, WU-AccessGate Phase 1 markup) referenced
`--color-link`, a token that **never existed** in `@theme` — the class compiled to nothing, so the
link rendered in inherited body ink with only the underline distinguishing it. Swapped to
`--color-action-ink` (the ratified links/active-text token). This was one of the two findings that
had kept `scripts/check-design-tokens.ps1` red repo-wide since the AccessGate work; the checker is
green again as of this fix (see `workplan.md` WU-TokenGreen).

---

### WU-ComponentSoundness Stage note (2026-06-27)

**Cell affected:** F21 L3-Logic (ProfilePage) — correctness polish inside an already-aligned Stage-5
cell; no stage transition.

**F1 — ProfilePage lifecycle reload (tab-switch stale content, now closed):**

`ProfilePage.razor` now implements the MessagesPage route-dispatcher pattern with a composite key
`(UserId, Tab)`:
- `private bool _initialized;` — set at the end of `OnInitializedAsync`.
- `private int _loadedUserId = int.MinValue;` + `private ProfileTab _loadedTab = (ProfileTab)(-1);`
  (sentinel outside valid enum range) — last-loaded-key caches.
- `OnInitializedAsync`: auth-resolution (one-time); first `LoadHeaderAsync()` + first `LoadTabPayloadAsync()`.
- `OnParametersSetAsync`: guards `UserId == _loadedUserId && newTab == _loadedTab`, then:
  - UserId change → reload banner + relationship + tab payload (`_isOwner` must be recomputed on userId change).
  - Tab change only → reload tab payload; keep banner.

Root cause: the tab strip on `ProfileDesktop`/`ProfileMobile` navigates via router-intercepted `<a href>`
links — same component instance, `OnInitializedAsync` does not re-fire. The prior code loaded the tab
payload in `OnInitializedAsync` only; switching from "Profile" to "Blog" left the old tab's data on screen
(bio text lingered, blog posts never loaded).

Covering tier: **RazorComponents** —
`ProfilePageTests.TabSwitch_OnSameInstance_ReloadsTabPayload`. Convention recorded in
`layer3-logic.md` §"Route-parameter dispatchers reload in `OnParametersSetAsync`".

---

## Feature 22 — User Stats

- **L1 — Stage 5** (`UserStat`, keyed on `UserId`).
- **L2 — Stage 5** (WU30, 2026-06-24). Real-time counter increments wired into 8 existing write
  services (same-transaction `ExecuteUpdateAsync` pattern per `layer2-services.md` §"UserStats Updates"):
  - `ServerFollowingWriteService`: `FollowerCount`/`AuthorsFollowed` ±1 on Follow/Unfollow.
  - `ServerStoryWriteService.CreateStoryAsync`: `StoriesWritten` +1.
  - `ServerChapterWriteService.RefreshStoryWordCountAsync`: `WordsWritten` ± word delta.
  - `ServerCommentWriteService`: `CommentsWritten` +1 on all 4 Post contexts; -1 on Delete.
  - `ServerRecommendationWriteService.SubmitAsync`: `RecommendationsWritten` +1 (actor);
    `RecommendationsReceived` +1 (story author).
  - `ServerBlogPostWriteService`: `BlogPostsWritten` +1 on create (was already wired); -1 on delete.
  - `ServerGroupWriteService`: `GroupsJoined` ±1 on Join/Leave.
  - `ServerUserStoryInteractionWriteService`: `FavoritesOnStories` (story author) + `StoriesRead`/
    `StoriesInProgress`/`StoriesIgnored` (actor) via transition-delta (increment/decrement only when
    the effective boolean state flips).
  Counters deferred (producer not yet built): `ViewsOnStories` (WU38), `SpotlightCount` (deferred to
  tracker **B8** — the Spotlight donation pipeline — as of WU-StatBadgeProducers, 2026-07-31; no
  badge consumes it, Patron is a settled manual grant). (`FeatureContributions` was a deferred
  counter here too, but the column was **removed entirely 2026-07-18** when Feature 56 was cut —
  see `audit/BlogPosts.md` Feature 56 CUT note.) `ActiveReportCount` was
  found to be an orphaned duplicate (never written; live data is `User.ActiveReportCount` on
  `AspNetUsers`) and dropped via migration in WU-UserStatRecalc — see Feature 58 below.
  Verified: `dotnet build` green; 373 RazorComponents pass; integration counter-specific tests deferred
  to Phase 5.
  - **WU-StatBadgeProducers (2026-07-31) — the two acknowledgment counters are now wired.**
    `AcknowledgedAsBetaReaderCount`: `ServerStoryAcknowledgmentWriteService.AcceptAsync`/`RevokeAsync`
    (role Beta Reader only, consent-gated — an author's credit alone doesn't count until the
    credited user accepts; self-credit rejected outright). `AcknowledgedAsInspirationCount`: a
    producer hook onto the already-built `StoryLineage` "Inspired By" approval
    (`ServerStoryLineageWriteService.ApproveLineageAsync`/`RejectLineageAsync`/`DeleteLineageAsync`),
    not a new feature — see `audit/Stories.md` Feature 10. Source ambiguity resolved: the dormant
    `BetaReader` entity (draft-access authorization, not credit) stays unbuilt. Full detail:
    `audit/Badges.md` §"WU-StatBadgeProducers" (L2 note), `workplan.md` WU-StatBadgeProducers.
- **L3-Logic — Stage 5** (WU30). `ProfileBanner` receives `UserStatsDto?` from header; null means
  stats hidden for non-owner. `UserStatsBlock` renders the counter snapshot.
- **L3.5-Structure — Stage 5** (WU30). `UserStatsBlock` leaf: flex-wrap stat chips with bold count
  + plain label. Mounted inside `ProfileBanner`. Visible only when `Header.Stats is not null`.
  Verified: build green.
- **L4-Style — Stage 5** (WU30). Counter display uses `font-bold text-[--color-text]` + muted labels.
  Visual sign-off pending human run. Stage-6 gate = human visual approval.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13).** Endpoints + client impl live (WU-L5Sweep) and the
  site now runs global InteractiveAuto; the profile stats strip rendered in a real WASM runtime
  during the flip's browser wave (profile-page verification). Full wave narrative + the 7 bugs
  found/fixed: `workplan.md` WU-GlobalFlip.

---

## Feature 58 — UserStat Recalculation Worker

- **L2 — Stage 5 (WU-UserStatRecalc, 2026-07-15).** Periodic `IHostedService`/`BackgroundService`
  reconciling the denormalized counters. Pure background computation — Layer 2 *is* the worker
  (grid_axes). All UI layers **N/A**. **L8 revised (2026-07-15):** mostly set-based raw SQL, not
  EF LINQ (mirrors `SiteDailyStatAggregator`'s style); one counter, `ViewsOnStories`, reads the
  `daily_story_stats` L8 mart directly (no EF model exists for it), so this cell touches L8 rather
  than being N/A — still N/A in the sense that it doesn't *build* new mart tables.
  - **Settled counter scope (2026-07-15, replaces the "EF-based" note):**
    - **Recompute — 14 already-wired counters:** `StoriesRead`, `StoriesInProgress`,
      `StoriesIgnored`, `StoriesWritten`, `WordsWritten`, `CommentsWritten`,
      `RecommendationsWritten`, `BlogPostsWritten`, `FollowerCount`, `AuthorsFollowed`,
      `FavoritesOnStories`, `GroupsJoined`, `RecommendationsReceived`,
      `RecommendationSuccessesEarned` — see `layer2-services.md` "Recalculation worker (F58)" for
      the mirror-the-wired-formula nuances each one must honor.
    - **Recompute — 3 unwired-but-populated counters** (worker becomes their first populator):
      `ChaptersRead` (`UserChapterInteraction.IsRead`), `WordsRead` (`ChapterContent.WordCount`
      summed over read chapters), `RecommendationsFoundUseful` (reader-side `RecommendationSuccess`
      count).
    - **Recompute — 1 raw-SQL counter:** `ViewsOnStories` (`daily_story_stats` mart, joined to the
      author's stories).
    - **Deferred, no recompute query:** `SpotlightCount` alone — deferred to tracker B8, see
      Feature 22's deferred-counters note above. (`FeatureContributions` was in this list until
      2026-07-18, when the column was removed with the Feature 56 cut.)
    - **Dropped:** `ActiveReportCount` — orphaned duplicate column, removed via migration (not
      recomputed).
    - **Recompute — 2 newly-wired counters (WU-StatBadgeProducers, 2026-07-31):**
      `AcknowledgedAsBetaReaderCount` (Accepted `StoryAcknowledgment` rows, role Beta Reader) and
      `AcknowledgedAsInspirationCount` (Approved "Inspired By" `StoryLineage` rows toward the target
      author, anti-self-link guarded via `IS DISTINCT FROM` — a self-owned link auto-approves but
      is not a real inspiration credit). A **third pass** was added to `RecalculateAllAsync` syncing
      `UserBadge.EarnedCount` from the now-corrected `UserStat` columns for badges with an automated
      producer (`Recommender`, `BetaReader`) — deliberately does not award missing badges (that
      stays the producers' job; see `UserStatRecalculator`'s class doc and the
      `RecalculateAllAsync_DoesNotAwardMissingBadges` regression test).
  - Insert-then-recompute: the worker also inserts any missing `UserStat` row before recomputing
    (heals the latent silent-no-op in the real-time `ExecuteUpdateAsync` path for users without a
    row). **Real finding, not anticipated in the plan:** no production write path creates a
    `UserStat` row at user registration either (checked `DataSeeder` and the Identity registration
    flow) — a stale code comment in `ServerRecommendationWriteService.RecordSuccessAsync` claimed
    otherwise and was corrected. So this step isn't just a safety net; it's the only mechanism by
    which most real users get a `UserStat` row at all.
  - **Built:** `Server/Profiles/UserStatRecalculator.cs` (scoped, one pair of `IS DISTINCT FROM`-
    guarded `UPDATE ... FROM` statements per counter — a match-and-correct pass plus a
    zero-unmatched pass, since a plain inner join would silently skip a user who drifted to a wrong
    positive value but has zero true occurrences); `Server/Profiles/UserStatRecalculationWorker.cs`
    (`BackgroundService`, daily off-hours loop sharing `Marts:RebuildHourUtc` with
    `DiscoveryMartWorker`/`SiteDailyStatWorker` — deliberately the same config key, not a dedicated
    one, since all three are low-urgency off-hours reconciliation passes). New telemetry component
    `CanalaveTelemetry.UserStatRecalc` (duration/users-touched/outcome, same shape as `Marts`) —
    doc-touched into `logging.md`. DI in `Program.cs`; `TestAppFactory` removes the hosted worker
    (same treatment as the other daily workers) so tests recalculate deterministically via
    `UserStatRecalculator` directly.
  - **`ActiveReportCount` drop, mechanically:** removed the property from `UserStat.cs`, migration
    `WU_UserStatRecalc_DropActiveReportCount` (`DropColumn`), corrected the stale comment on
    `UserStatsDto` that referenced it.
- **Verified (2026-07-15):** `dotnet build` green (0 warnings/errors). `dotnet test` green: 712
  Unit (unchanged) + 639 RazorComponents (unchanged) + 694 Integration (was 683 — 11 new tests in
  `UserStatRecalculatorTests.cs`). Covering tier: **Integration** — drift-correction per counter
  family (interaction-derived, authored-content, following, groups, recommendations incl.
  anti-self-farm exclusion, reading-progress, raw-SQL views), insert-then-recompute for a
  no-row user, idempotency (second pass corrects 0), zero-with-no-ground-truth (proves the
  zero-unmatched pass fires), and deferred-counters-untouched. Mutation sanity: inverted
  `StoriesInProgress`'s formula to also exclude `IsIgnored` (matching the *wrong*, display-filter
  formula) → `RecalculateAllAsync_MirrorsWiredFormula_ForInteractionDerivedCounters` failed as
  expected; reverted, suite green again.

## L4.5-Browser verification (2026-07-01) — F20 + F21 + F22 → Stage 5, no bugs

F21/F22: profile banner (name, tagline, avatar fallback), full stats strip matching the seeded
`UserStat` counters, outgoing-vouches accordion (incl. Remove affordance on own vouches), tab row,
ABOUT bio from `UserProfile.Text`, and the comment wall all render for own and other profiles;
profiles without a `UserStat` row render without a strip (null-safe). F20: `/settings` tagline
edit → "Profile saved." feedback → psql-verified on `AspNetUsers.tagline` → banner reflects it.
(Reader/Privacy/Author sub-forms rendered with correct persisted values; per-sub-form save loops
share the same `RunWithFeedbackAsync` path as the verified profile save.) Owner vs visitor
affordances correct: Edit Profile for owner, Follow/Vouch for visitors.

### WU-AuditFixPass note (2026-07-18)

Security fix MA-602 closed: `UserProfileEndpoints` derives `includePrivate` server-side
(`IActiveUserContext.UserId == userId`) — the client-supplied query bool that let anonymous callers
read any user's private header/stats/last-seen is gone — and the `/bio` route gained the same
visibility gate in `ServerUserProfileReadService.GetProfileTextAsync` (a Private profile's bio was
readable raw over HTTP). Also: `SettingsPage` banners normalized to `InlineAlert` and its catch no
longer surfaces raw `ex.Message` (ExceptionPresenter + Error log); `ProfilePage` bad-tab-slug
branches use `NotFound()`. Covered by Integration tier (`UserProfileEndpointsTests`). Full detail:
`workplan.md` WU-AuditFixPass.

### WU-AuditFixPass-2 note (2026-07-18)

Bucket-B doc-touch, F20 (cells stay Stage 5 — no behavior change): `SettingsPage`/`UserActivityTracker`'s
`IActiveUserContext` injection is now ratified as the two bounded exceptions to the testability-discipline
rule (BB-03 — the "IActiveUserContext won't exist in WASM" premise was deleted; see `layer2-services.md`).
MA-605's claims-staleness handoff comment was updated to reflect it remains an open product/UX call, not
silently accepted. Full detail: `workplan.md` WU-AuditFixPass-2.

### MA-611 status-code seam note (2026-07-18)

Status-code seam closed (F20/F33, cells stay Stage 5 — status semantics only):
`UpdateAuthorSettingsAsync`'s pinned-story ownership/visibility guard (the pinned story must be the
caller's own, published, visible story) now throws the new `UserSettingsValidationException` (a
`CanalaveValidationException`) → **400** instead of `InvalidOperationException` → 401 (the auth safety
net, now reserved for `RequireCurrentUserId`). `ClientUserSettingsService` gained a 400 arm
reconstructing `UserSettingsValidationException` from `ProblemDetails.Detail`. Covered by Integration
tier — `ManualTreeSearchTests.UpdateAuthorSettings_PinsOwnPublishedStory_AndRejectsForeignOrMissing`
(the "Pinned Story write gate" section) retyped from `InvalidOperationException` to
`UserSettingsValidationException`. Full detail: `modernization-audit/deferred-work.md` §4.

### WU-DataSaver Stage note (2026-07-31)

**Cell affected:** F20 L2/L3-Logic/L3.5-Structure — removal inside an already-aligned Stage-5 cell;
no stage transition. Closes tracker item **B0**.

`User.PrefersDataSaverMode` was cut end to end rather than wired up. B0's own framing ("suppress
sprites, or cut the setting") measured out wrong: sprites render at 16px across 3 sites in low-KB
static PNGs, and the one sprite saving that was ever material (animated `.webp` → static `.png`) is
already delivered by `PrefersAnimatedSprites = false`. The actual weight is cover art/avatars —
`ImageUploadProcessor.MaxStoredDimension = 2048` stores one size per upload, served into 24–144px
display slots on 20-item listing grids — which a checkbox promising "reduces image quality" cannot
honestly address without a real derivative-sizing mechanism (no `srcset`/thumbnail exists anywhere
in the app today). That gap is real but is its own properly-scoped item, not this one — see
`hidden-deferrals-tracker.md` group B and `roadmap.md` Phase 7 checklist.

Removed: `User.PrefersDataSaverMode` (Core), the `UserSettingsDto`/`IUserSettingsService` member and
parameter, `ServerUserSettingsService`'s projection/DTO-arg/`ExecuteUpdateAsync` `SetProperty`, the
`/api/user-settings/appearance` query parameter, `ClientUserSettingsService`'s query segment, the
`AppearanceSettingsForm` checkbox + `AppearanceArgs` member, `SettingsPage`'s wiring, the
`FakeSavedTagSelectionTestServices` fake, and the `SeedTool` binary-COPY column (column list and
positional write updated together — a COPY misalignment fails silently, not loudly). Migration
`20260731152702_DropPrefersDataSaverMode` — a real `DropColumn`, not an empty jsonb-only migration
(the property was a hot scalar column, not part of a JSON settings group).

**Verified:** `dotnet build` green (0 errors). `dotnet test` green — 776 Unit + 626 RazorComponents +
1,012 Integration = 2,414, unchanged from the pre-removal baseline (no new testable surface; the two
fake-service signature edits are compiler-enforced conformance, not new behavior). Migration applied
against local Postgres via `dotnet ef database update`; confirmed `prefers_data_saver_mode` absent
from `AspNetUsers`. `SeedTool` run (seed 42) verified independently, not just "ran without error":
seeded users' `prefers_animated_sprites`/`show_mature_content`/`profile_picture_relative_url` values
checked directly, not just seeding success — the positional-COPY risk was that a misalignment would
write plausible-looking wrong data into a neighboring column without throwing. Browser-verified at
`/settings`: Appearance section shows theme + animated-sprites only; save round-trips; the PUT
request carries exactly two query parameters. `scripts/check-doc-hygiene.ps1` and
`scripts/check-design-tokens.ps1` both clean.
