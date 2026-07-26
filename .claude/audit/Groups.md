# Audit — Groups/

**Features:** 38 (management), 39 (content & folders), 40 (display). Routes `/group/{GroupId:int}/{*Slug}`,
`/groups` (§5.29).

## Shared Context

> **2026-07-18 — Desktop/Mobile fork removed (WU-ResponsiveMerge).** `GroupDesktop`/`GroupMobile`
> merged into `GroupPage` (page renders its own markup; mobile variant deleted as an unvalidated
> placeholder); MA-509 audience-badge statics centralized. Narrow rendering is provisional pending
> the future mobile phase. Desktop/mobile assertions elsewhere in this file are historical. Rules:
> `canalave-conventions/render-and-layout.md` §"Responsive Layout Architecture"; spec §3.9/§3.10
> superseded on this axis.
> Verified 2026-07-18: full suite green post-merge (Unit 702 / Integration 727 / RazorComponents
> 510); browser smoke at desktop width clean (loads, no error banner, zero console errors);
> narrow rendering deliberately unpolished, no visual pass yet.

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
   **Amended 2026-07-25 (WU-B2): group blog posts are not story-linkable.** `GroupBlogPost.StoryId`
   removed (entity + column + DTO + editor story picker). Group posts are for topics about what the
   group is about; only *profile* posts speak about a specific story. Restores the original TPT
   design (the shipped `StoryId` was a deviation). Detail: `audit/BlogPosts.md` WU-B2 note.

5. **Group comments follow per-context method pattern (WU31 precedent).** `GetGroupCommentsAsync` /
   `PostGroupCommentAsync` mirror the blog-post methods. No `IsSpoiler` on `PostGroupCommentDto`.
   See `layer2-services.md` "Group Comments — Per-Context Method Pattern."
   **Amended 2026-07-25 (WU-B2): group-comment notifications are replies-only.** A group wall has no
   single comment-owner and no `NotifyForNewComment` membership flag; top-level group comments
   generate no notification — only `CommentReply` (34) to the parent-comment author fires.

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
- **L5 grid-mark reconciliation (WU-GroupsL5, 2026-07-24).** `status.md` rows 38–40 had stayed at
  Stage 2 despite this Stage-5 note above — WU-GlobalFlip's blanket "L5 flipped to 5 for all 40
  built-surface rows" claim missed the Groups cluster (it did update the sibling Recommendations
  rows 27–30 corrected in the same 2026-07-12 pass). Grid corrected to 5; no code change was
  needed here — F38's endpoints/client impl/browser verification were already sound. Also fixed a
  DI slip found during the reconciliation's build check: `Server/Program.cs` mapped
  `IGroupReadService` → `ServerGroupWriteService` (the heavier write impl) instead of
  `ServerGroupReadService`, unlike every other feature's read/write DI split (`Series` at the
  next registration block shares the same quirk — left as-is, out of scope for this WU). New
  Integration tier: `GroupEndpointsTests` (11 tests — `PagedResult<T>` envelope on both paged
  reads, the `RequireAuthorization()` 401 floor, the admin-only 403 gate, and full folder CRUD
  over HTTP incl. 404 on an unknown folder) — real Testcontainers-Postgres, all green. New Unit
  tier: `ClientGroupServiceTests` (16 tests — request URL/verb shapes incl. the folder routes,
  `PagedResult<T>` deconstruction, and the status-code → contract-exception mapping, pinning the
  project's one non-standard case: Groups' 403 disambiguation between the plain admin gate and
  the content-rating waterfall via `ProblemDetails.Detail` presence). `dotnet test` full suite
  1998/1998 (718 Unit + 521 RazorComponents + 759 Integration). Detail: `workplan.md` WU-GroupsL5;
  F39's Stage note below for the one genuine gap this WU also closed (the folder-management page).
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
- **D3.1 fix (WU-GroupsL5b, 2026-07-25).** `AssignStoryToFolderInternalAsync` never checked
  `folder.GroupId == groupStory.GroupId` — an admin of group A could file A's story into a
  group-B folder id via direct API use (`modernization-audit/deferred-work.md` §7, split into
  D3.1/D3.2 in `hidden-deferrals-tracker.md`, 2026-07-25). Now threads `expectedGroupId` through
  and rejects a mismatch with `KeyNotFoundException` — identical to a genuinely nonexistent
  folder, so the response never discloses that the id exists in another group. New Integration
  coverage at both the service (`GroupServiceTests`) and HTTP (`GroupEndpointsTests`) layers —
  previously zero tests existed for `AssignStoryToFolderAsync`/`UnassignStoryFromFolderAsync` at
  all.
- **L3-Logic — Stage 5 (2026-06-24, WU32).** `CreateFolderDto` with `MaxRating ≤ group cap` constraint.
  `AddGroupStoryDto` carries optional `GroupFolderId`. `ContentRatingExceededException` is a Core domain
  exception. Test tier: Unit — no pure-logic unit test (validation is simple enough; Integration covers).
- **L3.5-Structure — Stage 5 (2026-06-24, WU32; folder-management page built WU-GroupsL5,
  2026-07-24).** Folder tree rendered inline in `GroupPage` via `RenderFolders` recursive
  `RenderFragment` helper (read-only display, deliberate M-badge zero-trace suppression for
  non-consenting viewers — WU-AccessGate). Inline add-story form (StoryId text field) with
  `ContentRatingExceededException` surfaced as error toast. The admin "Manage" link to
  `/group/{GroupId}/folders` — a dangling affordance since WU32, folder management deferred
  post-MVP — now resolves: `GroupFolderManagementPage.razor` (admin-gated, mirrors
  `GroupCreateEditPage`'s pattern) owns its own interactive recursive tree (separate from
  `GroupPage.RenderFolders` — every row here is stateful/admin-only, no public suppression logic
  applies) with create (supports nesting via a depth-indented parent select), inline rename,
  two-step confirm-gated delete (`ConfirmDialog`), and sibling reorder (up/down buttons driving a
  `ReorderFolderAsync` SortOrder value-swap — robust to non-contiguous SortOrder, no unique
  constraint on the column). Every write does a full `GetByIdAsync` reload — no local tree
  mutation — so the server stays the single source of truth for uniqueness/cap/ordering.
  Story→folder assignment (`AssignStoryToFolderAsync`/`UnassignStoryFromFolderAsync`) is
  deliberately **not** on this page — it landed on `GroupPage` instead (WU-GroupsL5b, 2026-07-25;
  see Stage note below) since that's where stories actually render with titles/cards and the
  viewer's role already loads; this page owns folder CRUD/reorder only.
  Test tier: RazorComponents (`GroupFolderManagementPageTests`, 11 tests — admin gate (member and
  non-member both denied), create dispatch incl. nested `ParentFolderId`, rename dispatch, the
  two-step delete guard, the reorder value-swap + boundary-disabled buttons, validation-error
  surfacing via `InlineAlert`); Integration/Unit tiers for the transport layer are noted on F38's
  new Stage note above.

### WU-GroupsL5b Stage note (2026-07-25) — closes hidden-deferrals B6 + D3.1

**Trigger:** a hidden-deferrals audit (2026-07-24) flagged B6 — `AssignStoryToFolderAsync`/
`UnassignStoryFromFolderAsync` built and tested but no UI anywhere calls them. Investigating
surfaced the real blocker: no DTO exposed `GroupStoryId` (every write method needs it; only bare
`StoryId` existed on `GroupDetailDto.StoryIds`/`GroupFolderDto.StoryIds`). A first-draft fix
(a new admin-only `GetGroupStoriesAsync` endpoint) was rejected on review — it would have shipped
admin-only *read* access to data that was never actually gated (write-gating to admins is the
settled WU32 decision; read-gating was an unexamined default), and it would have left the real gap
open: `GroupPage.RenderFolders` had never rendered folder **contents** for *any* viewer, admin or
not, since WU32.

**Fix — retype at the source, not a parallel endpoint.** `GroupDetailDto.StoryIds`/
`GroupFolderDto.StoryIds` (`IReadOnlyList<int>`) retyped to `IReadOnlyList<GroupStoryDto>`
(new record, `GroupStoryId` + `StoryId`) in `Core/Groups/`. `ServerGroupReadService.GetByIdAsync`/
`BuildFolderTreeAsync` updated to project the richer shape. This means `GetByIdAsync` — already
fetched by `GroupPage` for every viewer, not just admins — carries everything needed in the one
round trip that already happens; no new endpoint. Blast radius was fully mapped before coding
(Explore agent + `dotnet build`): 6 consumption-site spots in `GroupPage.razor`, 2 test-fixture
named-arg renames in `GroupFolderManagementPageTests.cs` — nothing else in the solution touched
`.StoryIds` on either DTO.

**`GroupPage.razor` — the actual feature:**
- `RenderFolders` now shows each folder's story titles (linked to `/story/{id}`) for **every
  viewer**, unconditionally — the display gap this investigation found. Rewritten from imperative
  `RenderTreeBuilder` calls to a Razor-template recursive fragment (same idiom as
  `GroupFolderManagementPage.RenderFolderTree`) since it gained real interactive children.
- Per-folder unassign (×), admin-only, next to each listed story.
- Per-story assign/reassign + remove-from-group, admin-only, via `StoryDeck`'s existing
  `CardOverlay` slot (`RenderFragment<StoryListingDto>?`, `StoryDeck.razor:99`) — **no changes to
  `StoryDeck` itself**. Precedent: `CustomListPage.OwnerRemoveOverlay` (`pointer-events-auto`
  punched through the slot's `pointer-events-none` wrapper). The folder `<select>` treats
  story→folder as effectively single-primary (matching `AddGroupStoryDto.GroupFolderId`'s
  singular add-time intent) but doesn't guess if a story is ever in more than one folder
  (`GroupStory.GroupFolders` is a genuine many-to-many at the data layer) — it shows that plainly
  and points at the per-folder × controls instead.
- **`HandleStoryRemovedAsync` finally has a UI trigger.** It was fully implemented (error
  handling, reload, the works) but had never been wired to any button — found as a second dead
  handler during the same investigation, on the identical admin-action surface this WU was
  already building. Two-step confirmed via `ConfirmDialog`, same pattern as
  `GroupFolderManagementPage`'s delete-folder flow.

**D3.1 folded in** (see F39 L2 note above) — same method (`AssignStoryToFolderInternalAsync`)
this WU had to touch anyway.

**Tests:** `GroupServiceTests` +5 (assign/unassign happy paths, the D3.1 cross-group-rejection
pin, non-admin rejection, `GetByIdAsync`'s `Stories` carrying correct `GroupStoryId`).
`GroupEndpointsTests` +2 (cross-group → 404 over HTTP, admin assign → 204). New
`GroupPageTests.cs` (12 tests, RazorComponents — no file existed for this page before): folder
contents visible to every role incl. anonymous; non-admin sees zero admin controls; assign/
reassign/unfile dispatch correct id pairs; per-folder unassign dispatches correctly; remove is
two-step (trigger alone must not call the service). `ClientGroupServiceTests` +1 (deserializing a
populated `GetByIdAsync` body with the new nested shape — no prior test in that file exercised a
non-empty response at all).

**Verified:** `dotnet build` clean (confirms the retype's blast radius was fully caught — any
missed consumer fails to compile, by design). `dotnet test` full suite green: 753 Unit + 556
RazorComponents + 807 Integration = 2116/2116. `check-design-tokens.ps1` clean for the touched
file (two pre-existing, unrelated findings elsewhere untouched). Browser-verified live against
the dev DB: as admin, created a folder, added a story, assigned/reassigned/unassigned it via both
the per-story overlay and the per-folder ×, removed a story from the group via the confirm
dialog — `psql`-confirmed `group_stories`↔`group_folder_group_story` ground truth after each
step. Switched to a non-member seed user (`ReaderGamma`) on the same group: folder contents
(the story title) rendered correctly, zero admin controls anywhere on the page. Verification data
cleaned up afterward. Detail: `workplan.md` WU-GroupsL5b; `hidden-deferrals-tracker.md` B6/D3.1.
- **L4-Style — Stage 5 (2026-06-24, WU32).** Tailwind classes. Visual sign-off is human (Stage 6).
- **L5 — Stage 2 (corrected 2026-07-12 — was mismarked Stage 5; see F38's L5 note for the general
  correction).** Prior text, retained as the L2/L3 test record: waterfall rejection (both
  content-rating tiers), admin-only folder ops, story add — all covered by `GroupServiceTests`.
  See F38 L5 note for the root cause of the prior failures; the `AddStoryAsync`
  `IgnoreQueryFilters` fix is the direct fix for this feature's test assertions.
- **L5 — Stage 5 (WU-GlobalFlip, 2026-07-13; supersedes the correction above).** Endpoints +
  client impl live (WU-L5Sweep); stories/folders rendered on the group page under WASM in the
  flip's browser wave (folder-op writes not driven).
- **Folder-op writes now driven (WU-GroupsL5, 2026-07-24) — closes the "not driven" caveat
  above.** Grid corrected 2→5 (see F38's L5 grid-mark note for why it had drifted back to Stage
  2). `GroupFolderManagementPage` browser-verified end-to-end on a live dev-DB group as admin: a
  fresh page load confirmed genuine WASM execution (`_framework/*.wasm` bundle downloaded, zero
  `_blazor` WebSocket — the same signature WU-GlobalFlip's own verification used), then create
  (root + nested), rename, both-direction reorder, and confirm-gated delete were each driven and
  `psql`-confirmed against `group_folders` (parent id, swapped `sort_order`, row removal). The
  `GroupPage` display (`RenderFolders`) was confirmed to reflect the live tree afterward.
  Verification data cleaned up post-check (no fixture value, unlike WU-ChapterArcBrowserPass's
  deliberately-kept arcs). Detail: `workplan.md` WU-GroupsL5.

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
  `GroupPage.razor` additionally gained admin story→folder management (assign/reassign/unassign/
  remove) and folder-contents display for every viewer — WU-GroupsL5b, 2026-07-25, Stage note
  under F39 above (the story→folder feature itself is F39's territory; this is a structure-layer
  pointer to where it lives).
  Test tier: RazorComponents (`GroupCardTests`) — name, link, audience badge, member count (singular/plural),
  description present/absent. `GroupPageTests` (new, WU-GroupsL5b) covers the story-management
  additions specifically — see F39's Stage note.
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
- **Grid-mark reconciliation (WU-GroupsL5, 2026-07-24).** `status.md` row 40 had stayed at Stage 2
  despite this note — no code gap, purely the same missed-edit WU-GlobalFlip left on F38/F39 (see
  F38's L5 grid-mark note). Corrected 2→5; no browser re-verification needed beyond F38/F39's
  pass (this feature's display composition was already exercised in that same session).

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

---

**WU-ParentVisibility slice (2026-07-26) — F38/F39/F40.** New `GroupVisibilityGuard`. The
`GroupAudience` filter's own note reasons that "child entities are unreachable once their parent group
is filtered" — true only for queries that traverse the `Group` navigation, which the bare-`GroupId`
child queries never did. `GetMembersAsync` therefore handed an M-audience group's full roster
(usernames, avatars, roles, join dates) to anonymous callers, even though the sibling `GetByIdAsync` is
fully reveal-gated. Writes: `JoinAsync` and `AddStoryAsync`. **`JoinAsync` carried a false comment**
claiming "the audience filter is active on writeDb too" — it is not, and this same file says so
correctly twice elsewhere. That comment described a control that did not exist, and it was
load-bearing: joining unlocked the membership-gated writes (`CreateGroupBlogPostAsync`,
`AddStoryAsync`) and enrolled the user in `NewGroupStory`/`NewGroupBlogPost` fan-out for mature content
they had explicitly opted out of. `AddStoryAsync` gates on the **confidentiality axis only** — the
rating axis belongs to the tier-2 `MaxContentRating` waterfall, which reports its own
`ContentRatingExceededException` that a viewer-ceiling guard would pre-empt and lose.

Invariant, guards, and the two root causes: `identity-and-authorization.md` §"Parent-visibility guards" (conditionality kind (g)). Enforcement: `Tests.Integration/ParentVisibilityContractTests.cs`. Full narrative: `workplan.md` WU-ParentVisibility. **No Stage number changed — every affected cell was already Stage 5 and remains 5.**
