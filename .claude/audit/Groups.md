# Audit — Groups/

**Features:** 38 (management), 39 (content & folders), 40 (display). Routes `/group/{GroupId:int}/{*Slug}`,
`/groups` (§5.29).

## Shared Context

**Entities (Core/Models/):** `Group` (unique `GroupName`, `AudienceRating`/`MaxContentRating`→short,
`GroupAudienceType` presets derived at boundary), `GroupMember` (composite `(UserId,GroupId)`,
`GroupRole` enum, `DateJoined`), `GroupStory` (first-class, `AddedByUserId` SetNull),
`GroupFolder` (nesting via `ParentFolderId`, unique `(GroupId,ParentFolderId,Name)`, `MaxRating`→short),
`GroupComment` (TPT subtype of `BaseComment`, has `DatePosted` + `GroupId` — build-ready after WU31.5),
`GroupBlogPost` (TPT subtype of `BaseBlogPost`). **No services or components built.** Composes
`StoryDeck` for story listings and WU31 `BlogPost*` components/services for group blog posts.

**Note on `Group.Rating → AudienceRating` rename:** The EF entity property is renamed in WU32 Phase 1
for semantic clarity (spec's "Rating" was ambiguous with `MaxContentRating`). Column rename migration
is data-preserving; `HasConversion<short>()` is preserved. `GroupConfigurations.cs` updated to
reference `e.AudienceRating`.

### WU32 Settled Decisions (2026-06-24)

All four design questions were resolved with the user and are recorded in the skill files as
conventions. **Do not revisit these.** Pointers:

1. **Rating model — two properties, three-tier waterfall.**  
   `AudienceRating` (group visibility) vs `MaxContentRating` (content ceiling) are distinct.
   Three `GroupAudienceType` presets are a UI/write convention, not stored. Waterfall enforcement
   at write time: tier 1 = `ContentRating` named filter (model-level); tiers 2/3 = `ServerGroupWriteService`
   explicit checks. Violations throw `ContentRatingExceededException`.  
   See `content-safety.md` "Group Audience-Visibility Filter" and `layer2-services.md`
   "Group Rating Waterfall"/"Group Membership and Role Model."

2. **Membership — open join, permanent.** No approval, no kicking. Any authenticated user may join
   any visible group. See `layer2-services.md` "Group Membership and Role Model."

3. **Roles — Member / Admin only.** Creator auto-added as Admin on create. No `GroupRole.Moderator`
   category — permanent decision, not a deferral. Admin-gated mutations enforced server-side in
   `ServerGroupWriteService`. See `layer2-services.md` "Group Membership and Role Model."

4. **Group blog posts — in scope for WU32.** Honors `forward_plan.md` decision (2026-06-24). Reuses
   WU31 `BaseBlogPost` / `IBlogPostWriteService` / `BlogPostCard` / `BlogPostPropertiesForm`
   infrastructure; `IBlogPostWriteService` gains `CreateGroupBlogPostAsync` +
   `IBlogPostReadService` gains `GetByGroupAsync`.

5. **Group comments follow per-context method pattern (WU31 precedent).** `GetGroupCommentsAsync` /
   `PostGroupCommentAsync` mirror the blog-post methods. No `IsSpoiler` on `PostGroupCommentDto`.
   See `layer2-services.md` "Group Comments — Per-Context Method Pattern."

## Feature 38 — Group Management

- **L1 — Stage 5.** `Group` + `GroupMember` with role/audience model.
- **L2 — Stage 5 (2026-06-24, WU32).** `IGroupReadService` / `IGroupWriteService` in `Core/Groups/`;
  `ServerGroupReadService` / `ServerGroupWriteService` in `Server/Groups/`; CQRS-lite inheritance.
  `CreateGroupAsync` stamps creator as Admin in a second `SaveChangesAsync`. `JoinAsync` / `LeaveAsync`
  idempotent. DI registered in `Program.cs`. Migration `WU32_Groups` applied (data-preserving column
  rename `rating → audience_rating`). Covers: `GroupAudience` named filter, `GroupAudienceTypeMapper`,
  domain exceptions (`GroupValidationException`, `ContentRatingExceededException`).
  Test tier: Integration (`GroupServiceTests`) — group CRUD, join/leave idempotency, creator-as-admin,
  GroupAudience visibility filter, admin-only guards, waterfall rejection.
- **L3-Logic — Stage 5 (2026-06-24, WU32).** DTOs and validations in `Core/Groups/`.
  Test tier: Unit (`GroupValidationsTests`) — all validation paths exercised.
- **L3.5-Structure — Stage 5 (2026-06-24, WU32).** `GroupCreateEditPage.razor` (`/group/new` +
  `/group/{GroupId:int}/edit`): audience-type radio (preset → mapper), admin pre-check, `[Authorize]`.
  Test tier: none applicable (pure layout with Authorize guard; server gate is L2's test responsibility).
- **L4-Style — Stage 5 (2026-06-24, WU32).** Tailwind design-token classes throughout all group
  components. Visual sign-off is human (Stage 6).
- **L5 — Stage 2 (corrected 2026-07-12 — was mismarked Stage 5).** The Stage-5 mark below described
  `GroupServiceTests` (Integration tier, service-layer soundness only) — no endpoint/client impl
  ever existed. Per `layer5-wasm.md` §"L5 Stage Semantics", L5 Stage 5 means the HTTP body-swap
  (endpoints + client impl) exists and compiles; service-only soundness is Stage 2, same as every
  other not-yet-built L5 cell. Prior text, retained as the L2/L3 test record: `GroupServiceTests`
  (27 tests) — all pass. Blocked until 2026-06-25 by two bugs unmasked once the integration-test DB
  wiring was corrected (see Global Conditions note in `status.md`):
  (1) `ServerGroupWriteService.AddStoryAsync` fetched the story without
  `IgnoreQueryFilters(["ContentRating"])`, so M-rated stories appeared not-found when the active
  user had `ShowMatureContent=false`, causing `AddStory_Tier2_StoryRatingExceedsGroupMax_Throws`
  to throw `KeyNotFoundException` instead of `ContentRatingExceededException`.
  (2) `GroupServiceTests.CreateGroup_Mature_PersistsCorrectRatingPair` used `db.Groups.FindAsync`
  which applies the `GroupAudience` query filter — Mature groups were invisible to the non-mature
  test user, returning null and crashing. Fixed by using
  `IgnoreQueryFilters().FirstOrDefaultAsync(...)`.
  Verified: `dotnet test --filter "FullyQualifiedName~Group"` → 27/27 green; full
  `dotnet test` → 298 integration / 414 unit / 397 RazorComponents = 1,109 total, all green.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13; supersedes the 2026-07-12 correction above — the gap
  it named is now filled).** Endpoints + client impl live (WU-L5Sweep) and the site now runs global
  InteractiveAuto; groups listing (`PagedResult<T>` boundary) and group page (member role via the
  nullable `GroupRole?` read — one of the 18 empty-body-fix sites) verified in a real WASM runtime
  during the flip's browser wave. Full wave narrative + the 7 bugs found/fixed: `workplan.md`
  WU-GlobalFlip.
- L6 — Stage 2.

## Feature 39 — Group Content & Folders

- **L1 — Stage 5.** `GroupFolder` self-nesting + `GroupStory` first-class entity established.
- **L2 — Stage 5 (2026-06-24, WU32).** `IGroupWriteService.AddStoryAsync` enforces three-tier
  content-rating waterfall: tier 1 = `ContentRating` named filter (model); tier 2 =
  `story.Rating > group.MaxContentRating` (service); tier 3 = `story.Rating > folder.MaxRating`
  (service, when `GroupFolderId` set). Admin-only folder CRUD: `CreateFolderAsync`, `RenameFolderAsync`,
  `DeleteFolderAsync`, `ReorderFolderAsync`. `AssignStoryToFolderAsync` / `UnassignStoryFromFolderAsync`.
  `RemoveStoryAsync` (admin, cascades folder assignments). All guarded via `RequireAdminAsync` helper.
  Test tier: Integration (`GroupServiceTests`) — waterfall rejection both tiers, admin-only folder ops
  reject non-admins, story add for members.
- **L3-Logic — Stage 5 (2026-06-24, WU32).** `CreateFolderDto` with `MaxRating ≤ group cap` constraint.
  `AddGroupStoryDto` carries optional `GroupFolderId`. `ContentRatingExceededException` is a Core domain
  exception. Test tier: Unit — no pure-logic unit test (validation is simple enough; Integration covers).
- **L3.5-Structure — Stage 5 (2026-06-24, WU32).** Folder tree rendered inline in `GroupDesktop` /
  `GroupMobile` via `RenderFolders` recursive `RenderFragment` helper. Inline add-story form (StoryId
  text field) with `ContentRatingExceededException` surfaced as error toast. Admin manage-folders link
  to `/group/{GroupId}/folders` (folder management page deferred post-MVP; link is present as
  affordance). Test tier: none applicable (inline rendering in composite; Integration covers the data).
- **L4-Style — Stage 5 (2026-06-24, WU32).** Tailwind classes. Visual sign-off is human (Stage 6).
- **L5 — Stage 2 (corrected 2026-07-12 — was mismarked Stage 5; see F38's L5 note for the general
  correction).** Prior text, retained as the L2/L3 test record: waterfall rejection (both
  content-rating tiers), admin-only folder ops, story add — all covered by `GroupServiceTests`.
  See F38 L5 note for the root cause of the prior failures; the `AddStoryAsync`
  `IgnoreQueryFilters` fix is the direct fix for this feature's test assertions.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13; supersedes the correction above).** Endpoints +
  client impl live (WU-L5Sweep); stories/folders rendered on the group page under WASM in the
  flip's browser wave (folder-op writes not driven). Detail: `workplan.md` WU-GlobalFlip.

## Feature 40 — Group Display

- **L1 — Stage 5.**
- **L2 — Stage 5 (2026-06-24, WU32).** `IGroupReadService.GetListingsAsync` (audience-filtered, paged),
  `GetByIdAsync` (builds folder tree in-memory from flat DB load), `GetCurrentUserRoleAsync`,
  `GetMembersAsync`. `ICommentReadService.GetGroupCommentsAsync` / `ICommentWriteService.PostGroupCommentAsync`
  — per-context method pattern mirroring blog-post. `IBlogPostReadService.GetByGroupAsync` (reads
  `GroupBlogPost` TPT subtype; explicit `p.Rating <= maxRating` check — no named filter on TPT child).
  `IBlogPostWriteService.CreateGroupBlogPostAsync` — member gate + group-exists check + notification fan-out
  (`NotifyNewGroupBlogPostAsync`). `INotificationWriteService.NotifyNewGroupStoryAsync` (fan-out to
  members with `NotifyForNewStory=true` + `YourStoryAddedToGroup` to story author, drop-self handled);
  `NotifyNewGroupBlogPostAsync` (fan-out to members with `NotifyForNewBlogPost=true`). Notification
  fan-out is best-effort post-commit (try/catch).
  Test tier: Integration (`GroupServiceTests`) — group comments post/read, blog-post create + read,
  notification fan-out (NewGroupStory, YourStoryAddedToGroup, NewGroupBlogPost, drop-self rule).
- **L3-Logic — Stage 5 (2026-06-24, WU32).** `GroupAudienceTypeMapper` (preset round-trip; inverse).
  `CommentSection.GroupId` param + `CommentTarget.Group` branch (exactly-one-set guard, Load/Post/Reply).
  Test tier: Unit (`GroupAudienceTypeMapperTests`) — all three presets, round-trip, unknown value throws.
  RazorComponents (`CommentSectionGroupTests`) — initial load, post dispatch, no-spoiler-toggle, guard.
- **L3.5-Structure — Stage 5 (2026-06-24, WU32).** `GroupCard.razor` (leaf, no inject, audience badge).
  `GroupsPage.razor` (`/groups` listing, `IGroupWriteService`, `PaginationControls`, auth-gated Create link).
  `GroupPage.razor` (dispatcher: resolves `CurrentUserId` from auth cascade, batch-loads group detail +
  stories + blog posts + interaction states in parallel, surfaces `ContentRatingExceededException`).
  `GroupDesktop.razor` / `GroupMobile.razor` (composites: header, join/leave, story deck, folders,
  blog posts, comment section). `GroupBlogPostEditorPage.razor` (`/group/{GroupId}/blog/new`, member gate,
  reuses `BlogPostPropertiesForm`, `CreateGroupBlogPostAsync`).
  Test tier: RazorComponents (`GroupCardTests`) — name, link, audience badge, member count (singular/plural),
  description present/absent.
- **L4-Style — Stage 5 (2026-06-24, WU32).** All group components use design-token CSS variables
  (`--color-primary`, `--color-surface`, etc.) and Tailwind v4 utilities throughout. Visual sign-off
  is human (Stage 6).
- **L5 — Stage 2 (corrected 2026-07-12 — was mismarked Stage 5; see F38's L5 note for the general
  correction).** Prior text, retained as the L2/L3 test record: group comments, blog-post create +
  read, and notification fan-out (NewGroupStory, YourStoryAddedToGroup, NewGroupBlogPost,
  drop-self rule) covered by `GroupServiceTests`. See F38 L5 note for root cause of the prior
  failures.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13; supersedes the correction above).** Endpoints +
  client impl live (WU-L5Sweep); the group page's display composition (detail + stories + blog
  posts) rendered under WASM in the flip's browser wave. Detail: `workplan.md` WU-GlobalFlip.

### WU-ComponentSoundness Stage note (2026-06-27)

**Cell affected:** F40 L3-Logic (GroupPage) — correctness polish inside an already-aligned Stage-5
cell; no stage transition.

**F1 — GroupPage lifecycle reload (in-place GroupId change stale content, now closed):**

`GroupPage.razor` now implements the MessagesPage route-dispatcher pattern with key `GroupId`:
- `private bool _initialized;` + `private int _loadedGroupId = int.MinValue;` sentinel.
- `OnInitializedAsync`: auth-resolution (one-time); `_initialized = true` (first load handled inline).
- `OnParametersSetAsync`: guards `GroupId == _loadedGroupId`; calls `LoadGroupAsync()` for a changed GroupId.
- `LoadGroupAsync()`: sets `_loadedGroupId = GroupId` at the start, then loads group detail.

Root cause: `GroupCard` links (`/group/{id}/{*slug}`) can navigate in-place if routed via
`NavigationManager`; `OnInitializedAsync` does not re-fire on same-template navigation.

Covering tier: **manual boot gate** (no bUnit test — GroupPage injects too many services for a
minimal bUnit render; behavior listed in E2E checklist). Convention in
`layer3-logic.md` §"Route-parameter dispatchers reload in `OnParametersSetAsync`".

## L4.5-Browser verification (2026-07-01/02) — F38 + F39 + F40 → Stage 5

F38: created a group via `/group/new` (name + SFW Only audience preset radio) → landed on the
slugged group page as Admin with Edit Group + Manage-folders affordances; earlier joined the
seeded standard group as a member (member count bumped, Member/Leave state, + Add Story /
+ New Post affordances appeared). F39/F40: group detail renders the folder tree (parent + nested
child with rating cap), group stories as cards, the group blog post, and the group comment wall
(same CommentSection contract as chapters); audience filtering verified — the Mature group is
invisible to a mature-off viewer in `/groups`, and audience badges (Standard/SFW Only/Mature)
derive correctly from the rating pairs. Deeper folder management + add-story-to-group writes
remain Integration-covered (GroupServiceTests) rather than browser-driven.

### WU-AuditFixPass note (2026-07-18)

`GroupCreateEditPage` validation block normalized to `InlineAlert` (MA-504 class) and its
missing-group branch uses `NavigationManager.NotFound()` (same class as the MA-202 sweep; site
unnamed by the audit but identical shape). Full detail: `workplan.md` WU-AuditFixPass.

### WU-AuditFixPass-2 note (2026-07-18)

MA-508 closed, F38 (cell stays Stage 5): `CreateGroupAsync` is now throttled under the `ContentCreate`
limiter (group creation was previously unthrottled). Full detail: `workplan.md` WU-AuditFixPass-2.
