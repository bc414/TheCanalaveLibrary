# Audit — Profiles/

**Features:** 20 (profile editing), 21 (profile display), 22 (user stats), 58 (UserStat recalculation
worker).

## Shared Context
**Entities:** `UserProfile` (cold partition — `ProfileText`, 1-to-1 cascade from `User`), `UserStat`
(PK `UserId`, 22+ denormalized counters, 1-to-1 cascade). Settings (Reader/Privacy/Author) live as owned
JSON on `User` (see Identity audit). Spec calls for `IUserProfileReadService` (public profile) and
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
- **L3-Logic — Stage 5** (WU30). `SettingsPage.razor` at `/settings` dispatches to 5 sub-forms.
  `ProfileSettingsForm`, `ReaderSettingsForm`, `PrivacySettingsForm`, `AuthorSettingsForm`,
  `AppearanceSettingsForm` all injection-free (bUnit-testable); page holds all service calls.
  `_seeded` guard prevents re-init on re-render. Per-section busy flags decouple save operations.
  Verified: build green, 373 RazorComponents tests pass.
- **L3.5-Structure — Stage 5** (WU30). 5 sub-form Razor components with clear param/callback
  boundaries; picture upload raises `IBrowserFile` callback; page handles stream + URL patch.
  Verified: build green.
- **L4-Style — Stage 5** (WU30). Tailwind v4 token-based styling throughout; sub-forms use
  `--color-*` tokens, `focus:ring-2 focus:ring-[--color-primary]`, `border-[--color-border]`.
  Visual sign-off pending human run at `/settings`. Stage-6 gate = human visual approval.
- **L5 — Stage 2.** Client HTTP impl deferred (MVP = InteractiveServer, no API controllers yet).

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
- **L5 — Stage 2.** Client HTTP impl deferred.

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
  services (same-transaction `ExecuteUpdateAsync` pattern per `cross-cutting.md` §"UserStats Updates"):
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
  Counters deferred to their own WU (not yet built): `ViewsOnStories` (WU38),
  `SpotlightCount` (post-MVP), `ActiveReportCount` (WU34), acknowledgment counters (WU37).
  Verified: `dotnet build` green; 373 RazorComponents pass; integration counter-specific tests deferred
  to Phase 5.
- **L3-Logic — Stage 5** (WU30). `ProfileBanner` receives `UserStatsDto?` from header; null means
  stats hidden for non-owner. `UserStatsBlock` renders the counter snapshot.
- **L3.5-Structure — Stage 5** (WU30). `UserStatsBlock` leaf: flex-wrap stat chips with bold count
  + plain label. Mounted inside `ProfileBanner`. Visible only when `Header.Stats is not null`.
  Verified: build green.
- **L4-Style — Stage 5** (WU30). Counter display uses `font-bold text-[--color-text]` + muted labels.
  Visual sign-off pending human run. Stage-6 gate = human visual approval.
- **L5 — Stage 2.** Client HTTP impl deferred.

---

## Feature 58 — UserStat Recalculation Worker

- **L2 — Stage 2.** Periodic `IHostedService`/`BackgroundService` reconciling the denormalized counters.
  Pure background computation — Layer 2 *is* the worker (grid_axes). All UI layers **N/A**; **L8 — N/A**
  (EF-based recalculation, not a raw-SQL data mart).

## L4.5-Browser verification (2026-07-01) — F20 + F21 + F22 → Stage 5, no bugs

F21/F22: profile banner (name, tagline, avatar fallback), full stats strip matching the seeded
`UserStat` counters, outgoing-vouches accordion (incl. Remove affordance on own vouches), tab row,
ABOUT bio from `UserProfile.Text`, and the comment wall all render for own and other profiles;
profiles without a `UserStat` row render without a strip (null-safe). F20: `/settings` tagline
edit → "Profile saved." feedback → psql-verified on `AspNetUsers.tagline` → banner reflects it.
(Reader/Privacy/Author sub-forms rendered with correct persisted values; per-sub-form save loops
share the same `RunWithFeedbackAsync` path as the verified profile save.) Owner vs visitor
affordances correct: Edit Profile for owner, Follow/Vouch for visitors.
