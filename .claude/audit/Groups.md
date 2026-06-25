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
   See `cross-cutting.md` "Group Audience-Visibility Filter" and `layer2-services.md`
   "Group Rating Waterfall."

2. **Membership — open join, permanent.** No approval, no kicking. Any authenticated user may join
   any visible group. See `cross-cutting.md` "Group Membership and Role Model."

3. **Roles — Member / Admin only.** Creator auto-added as Admin on create. No `GroupRole.Moderator`
   category — permanent decision, not a deferral. Admin-gated mutations enforced server-side in
   `ServerGroupWriteService`. See `cross-cutting.md` "Group Membership and Role Model."

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
- L5 — Stage 2. L6 — Stage 2.

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
- L5 — Stage 2.

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
- L5 — Stage 2.
