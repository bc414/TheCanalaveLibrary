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
`GroupFolder` (nesting via `ParentFolderId` — a self-FK `ON DELETE SET NULL` and `ck_group_folders_no_self`
since WU-SchemaHardening 2026-10-13, a bare integer before it; unique `(GroupId,ParentFolderId,Name)`
**NULLS NOT DISTINCT** since the same WU, so top-level names are unique too; `MaxRating`→short),
`group_stories` unique `(GroupId, StoryId)` (D23, same WU),
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
   *Correction 2026-10-13 (WU-DocCorrections, service audit §4 row 2):* tier 1 is not an add-time
   gate. Since WU38 every named filter lives on `ReadOnlyApplicationDbContext`, and `AddStoryAsync`
   loads the story from the unfiltered write context, so the adding member's own rating ceiling is
   never consulted at add time. Tier 1 is a **display-side** property: group listings read
   through the filtered read context and hide that story from that viewer. At add time the service
   applies the kind-(g) confidentiality guard (`StoryVisibilityGuard.IsStoryPublishedAsync`, rating
   axis lifted on purpose, WU-ParentVisibility) and tiers 2/3. The code is correct and deliberate;
   only this note and the convention text were wrong. Rule: `layer2-services.md` §"Group Rating
   Waterfall".

2. **Membership — open join, permanent.** No approval, no kicking. Any authenticated user may join
   any visible group. See `layer2-services.md` "Group Membership and Role Model."

3. **Roles — Member / Admin only.** Creator auto-added as Admin on create. No `GroupRole.Moderator`
   category — permanent decision, not a deferral. Admin-gated mutations enforced server-side in
   `ServerGroupWriteService`. See `layer2-services.md` "Group Membership and Role Model."
   **Amended 2026-10-13 (owner ruling D13 edge (b), built WU-UserDeletion): the admin-less group.**
   The "no transfer" line now has two halves. Member-to-member transfer of a *living* admin role stays
   absent, permanently. A group with **zero** admins (its last admin left or deleted their account) is
   a waiting state that a site moderator ends by promoting an existing member
   (`AssignGroupAdminAsync`). Auto-promotion and self-claim are rejected. Rule:
   `layer2-services.md` §"Group Membership and Role Model"; F38's Settled note below.

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

**Settled — the admin-less group (owner ruling D13 edge (b), answered 2026-08-06; settled for
WU-UserDeletion 2026-10-13). Do not revisit:**
- **A waiting state, both routes.** A group whose last admin leaves *or* deletes their account
  survives with no admin (`creator_id` NULL on the deletion route). A site moderator assigns a new
  admin on request. Auto-promotion and member self-claim are rejected; member-to-member transfer of a
  living admin role stays absent.
- **The action:** `IModerationWriteService.AssignGroupAdminAsync(groupId, targetUserId, notes)` with
  exactly three guards (caller is a moderator; the group has zero admins; the target is already a
  member). No fourth guard: a moderator may assign themselves if they are a member. It files an
  opened-and-resolved `Report` row (`Group` target, reason Other, the notes) and notifies the promoted
  user (type 62, null-sourced; whether it should name the moderator is roadmap decision row 23). It
  moves no counter (D24).
- **UI:** the sole admin's Leave confirms first, saying the group will have no admin until a site
  moderator assigns one (D13's recommended rider); `LeaveAsync` itself is unchanged. A moderator
  viewing an admin-less group gets the rescue panel on the group page (headed "This group has no
  admin", button "Assign as admin"). `GroupDetailDto` carries `AdminCount` to drive both.
- **Open, not decided here:** the request channel for "on request" (unspecified by the owner; out of
  band, by messaging a moderator, until D47), and what a moderator can do about a group that still has
  an admin (D47(b), pending).

Rule: `layer2-services.md` §"Group Membership and Role Model".

**WU-UserDeletion Stage note (2026-10-13) — no F38 flip.** The Settled note above is built, but none
of it lives in F38's own surfaces: `ServerGroupWriteService` is unchanged (`LeaveAsync` too, as D13
requires), and `GroupCreateEditPage` is untouched. The action is `IModerationWriteService`'s
(`audit/Moderation.md` F47's WU-UserDeletion note), the page changes are F40's (its Stage note), and
the deletion route is F52's (`audit/Identity.md`). Tier coverage of the role-model rule itself:
Integration `GroupAdminRescueTests` (the role flips to Admin on an existing row; `GroupsJoined` does not
move — D24) and `UserDeletionServiceTests` (a sole admin's account deletion leaves the group with no
admin). Tracker H6's "last-admin-leave unhandled" bullet is closed.

**Settled — owner rulings D24 and D23 (answered 2026-08-08; built WU-CounterSymmetry 2026-09-30).
Do not revisit:**
- **Creating a group counts as joining it.** `CreateGroupAsync` moves the creator's `GroupsJoined`
  +1, post-commit, after the creator's member row saves. `LeaveAsync`'s −1 and the recompute
  (`COUNT(*) FROM group_members`, which never reads `creator_id`) are correct as shipped and do not
  change. There is no floor-at-zero clamp: it would hide an oscillation, not fix it.
- **`JoinAsync` is an upsert.** One `INSERT … ON CONFLICT (user_id, group_id) DO NOTHING`, and the
  `+1` runs only when that insert landed a row, so a concurrent double join neither 500s nor
  double-counts. It ships with D24's increment because the two touch the same method pair.
- **A moderator's assign-admin action (WU-GroupAdminRescue, D13 edge (b); built as WU-UserDeletion's Part B, 2026-10-13) has no counter effect.** It
  changes `Role` on an existing row; it is not a membership change.
- **Conditional falling case.** If a group-delete path is ever built (D47(b), pending), every member's
  `GroupsJoined` falls through the `group_members` cascade. That is correct and needs no counter code.

Rule: `layer2-services.md` §"Counter ↔ event map" and §"Check-then-act posture".

**WU-ModerationIntegrity Stage note (2026-09-30) — no flip:** the shared `ActiveUser.RequireUserId()`
guard (`Core/Identity/ActiveUserContextExtensions.cs`) replaces this service's private
`RequireAuthenticatedUser` copy — owner ruling D9's "one shared guard", WU-ModerationIntegrity
2026-09-30. Same `InvalidOperationException` → 401, so no behavior change; the existing Integration
tests for the anonymous refusal stay green.

**WU-CounterSymmetry Stage note (2026-09-30) — no flip** (owner rulings D24 and D23; the Settled note
above):
- `CreateGroupAsync` moves the creator's `GroupsJoined` +1 after the creator's member row saves.
  Create-then-leave used to leave −1 on a public profile, and every recompute pass flipped it back to
  0, so the two oscillated.
- `JoinAsync` replaced its `AnyAsync`-then-`Add` with one parameterized `INSERT … ON CONFLICT
  (user_id, group_id) DO NOTHING`. The column values come from a C#-built `GroupMember`, so the
  `NotifyForNewStory = true` default stays single-sourced; the columns have no DB default. The `+1`
  runs only when the insert landed a row. A concurrent double join used to 500 on `pk_group_members`.
- `LeaveAsync` and `GroupsJoinedAgg` are unchanged, as D24 says. The `+1` stays post-commit
  (WU-ParityAndRemaining may wrap creation in a transaction; it must keep the counter after the commit).

The interface doc now states "creating counts as joining". **Test tier: Integration.** In
`GroupServiceTests` (+5): create → 1, create-then-leave → 0, create-leave-rejoin → 1, join twice → 1,
and a concurrent double join (no throw, one row, exactly 1). Every test ends with a
`UserStatRecalculator` pass that corrects 0 and inserts no row. `CounterSymmetryTests` adds
`Join_ACompetingJoinLandingBeforeTheInsert_*`, which interleaves the competing row just before the
insert (mutation-checked: without `ON CONFLICT` it 500s).

**Browser verification (2026-09-30, WU-CounterSymmetry e2e pass) — no flip; L4.5 stays 5.** Server-only
path, reset workbench, `psql` after every write, render phase read from the network log. AuthorAlpha
started at `groups_joined` 1, a member of the standard group only.
- **Create (circuit).** Creating "E2E Counter Group A" (group 4) at `/group/new` wrote an Admin member
  row and moved `groups_joined` 1→2.
- **Leave, then a double Join (circuit).** Leave took it to 1. A real double-click on Join Group
  landed one member row and moved the counter once, to 2. But the second click's handler ran while the
  first was still awaiting, on the same circuit-scoped `ApplicationDbContext`, and threw
  `InvalidOperationException` ("A second operation was started on this context instance"). The viewer
  got the generic "Something went wrong on our end" toast. This is not D23's cross-request race and
  not this WU's code: the old `AnyAsync`-then-`Add` shared the same context. It is the same-circuit
  write interleaving `layer2-services.md` sets aside, filed as tracker **D12**.
- **Leave, then a double Join (WASM).** Leave (`POST /api/groups/4/leave`) took it to 1. Two Join
  clicks in one JS tick sent two `POST /api/groups/4/join`. Both reached the `ON CONFLICT` insert
  (server log), one member row landed, the counter moved once (to 2), and no error showed: each request
  has its own scope.
- The WASM profile then read "2 groups".

- **L1 — Stage 5.** `Group` + `GroupMember` with role/audience model.
- **L2 — Stage 5 (2026-06-24, WU32; shared auth guard WU-ModerationIntegrity, 2026-09-30; create counts as joining + `JoinAsync` upsert WU-CounterSymmetry, 2026-09-30 — see its Stage note; create is one commit and a taken name is a 400 on create and rename, WU-ParityAndRemaining, 2026-10-13 — Integration-tested, both races included since its review fixes, browser-driven on both render phases by its browser pass the same day, stage unchanged, the note just below).**
  **WU-ParityAndRemaining note (2026-10-13) — no flip (service audit §2.12).** `CreateGroupAsync`
  committed the group and its creator's Admin row in two saves (a failure between them left an
  admin-less group), and a duplicate name reached `ix_groups_group_name` as a raw 500. Now: the creator's
  `GroupMember` is added through `group.GroupMembers` before the single save; create and a renaming
  `UpdateGroupAsync` pre-check the trimmed name with `==` (byte-wise, the index's comparison; D29 owns
  case-insensitivity) and refuse with `GroupValidationException` ("A group named “…” already exists.");
  a concurrent create that passes the pre-check is caught on the index's name only, answered the same
  way, and its rows detached (the folder backstop's shape). The index name is now pinned with
  `HasDatabaseName` (no migration: it was already that name). D24's `GroupsJoined +1` stays post-commit.
  Rule: `layer2-services.md` §"Group Rating Waterfall" → "Group names are unique, byte-wise". Tier:
  Integration `GroupCreateIntegrityTests` (7: duplicate create, case-differing name allowed, rename onto
  another name, keeping its own name, the interleaved race, both rows committed, and `POST /api/groups`
  answering 400); the race test is mutation-checked against a backstop matching the wrong index. The
  review fixes (same day) added an eighth, the rename's interleaved race (another group takes the name
  between `UpdateGroupAsync`'s pre-check and its `UPDATE`: the same refusal, the stored name unchanged,
  the tracker empty), mutation-checked by disabling that catch. **Browser pass (2026-10-13):** on
  `/group/new`, the taken name "Seed Group: Standard" showed "A group named “Seed Group: Standard”
  already exists." on WASM (`POST /api/groups` 400) and on the circuit; renaming group 7 onto "Seed
  Group: SFW Only" showed the same refusal on WASM (`PUT` 400) and left the name alone. A real create on
  the circuit ("E2E Parity: atomic create", group 7) wrote the group and TestUser's Admin row with one
  timestamp. `IGroupReadService` / `IGroupWriteService` in `Core/Groups/`;
  `ServerGroupReadService` / `ServerGroupWriteService` in `Server/Groups/`; CQRS-lite inheritance.
  `CreateGroupAsync` adds the creator's Admin membership through `group.GroupMembers` and commits it
  with the group in one `SaveChangesAsync` (WU-ParityAndRemaining; WU32 used a second save). `JoinAsync` / `LeaveAsync`
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
  wiring was corrected (WU31_5b — see `workplan-archive.md`; the old `status.md` Global Conditions
  note was removed in the 2026-07-27 standing-constraints rewrite):
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

**Open for the owner — group story-add throttling (WU-ThrottleCoverage, 2026-10-13; decision row 26
(b), tracker F23).** D20's throttle half requires every unthrottled write to be named with its reason.
`AddStoryAsync` could not honestly be called exempt: with the D16 anchor below, each add of a distinct
story notifies every opted-in member, and an admin's remove + re-add mints a new `GroupStory` row, so
dedup never merges two adds. It stays unthrottled (no code change) and is listed as *flagged* in
`security.md`'s enumeration until the owner rules. Folder CRUD is listed exempt (group-admin-only,
no notification). No cell moves.

**Settled — group-story notifications (owner ruling D16, answered 2026-08-07, consumed
WU-InertFeatures 2026-09-30; do not revisit).** `NewGroupStory` (60) and `YourStoryAddedToGroup` (25)
both carry the **`GroupStory` junction row's id** — not the group id (which could never name the story,
and made two different stories collapse under unread dedup) and not the story id. The enricher's
`GroupStory` kind yields the group name (link `/group/{id}`, unchanged) and the story title from that
one id; a removed `GroupStory` row leaves the notification title-less (accepted). Riders on
`AddStoryAsync`: the notify block runs only when the story was actually added (a re-add re-fired the
whole fan-out); an authorless story still notifies members; the story's author is excluded from the
60 fan-out and gets 25 only (one event, one notification). Rule: `layer2-services.md`
§"Polymorphic RelatedEntityId".

**Settled — folder integrity (schema §2.4, service §2.10, worksheet D23's `group_stories` row; settled
for WU-SchemaHardening 2026-10-13; do not revisit without the owner).** Sibling folder names are unique
byte-wise, top level included (NULLS NOT DISTINCT); create and rename validate the name (required,
≤ 100 after trimming) and pre-check siblings with the index's own comparison. A parent folder must be
in the same group, and a bad parent id is a not-found (D3.1's non-disclosure shape). Deleting a folder
moves its sub-folders to the top level, appended after the existing top-level folders; a sub-folder
whose name would duplicate a top-level name makes the delete refuse with nothing deleted. That
refusal is a **default, not an owner ruling** (roadmap decision row 22). `group_stories` is a set: the
unique index refuses a duplicate and a racing add counts as already added. **Still open, not part of
this:** case-insensitive names (D29), a composite same-group FK and the join's rigor (D33), the
`sort_order` index question (L6 always-measure). Rule: `layer2-services.md` §"Group Folder Integrity".

- **L1 — Stage 5 (self-FK `ON DELETE SET NULL`, NULLS NOT DISTINCT sibling names, `ck_group_folders_no_self`
  and a unique `group_stories (GroupId, StoryId)` since WU-SchemaHardening, 2026-10-13 — stays Stage 5,
  Stage note at the end of this feature).** `GroupFolder` self-nesting + `GroupStory` first-class entity established.
- **L2 — Stage 5 (2026-06-24, WU32; story-add notifications re-anchored and de-spammed,
  WU-InertFeatures 2026-09-30 — stays Stage 5; folder integrity — name rules, same-group parents,
  delete moves sub-folders to the top level — and the `AddStoryAsync` race backstop, WU-SchemaHardening
  2026-10-13 — stays Stage 5, Stage note at the end of this feature; the waterfall's tier-1 doctrine
  corrected, code unchanged, WU-DocCorrections 2026-10-13 — stays Stage 5, note below; the draft-title
  finding split into tracker D21 and decision row 31 by its review fixes, 2026-10-14).**
  **WU-DocCorrections Stage note (2026-10-13; service audit §4 row 2) — no flip, no code change.** The
  convention text (`layer2-services.md` §"Group Rating Waterfall") and the WU32 settled note #1 above
  said tier 1 (the viewer's `ContentRating` ceiling) is enforced when a story is added, because the
  write-side story load "is already filtered". False since WU38: the write context carries no filters.
  Tier 1 is now stated as a display-side property of the read context; the add path's confidentiality
  guard plus tiers 2/3 are recorded as the deliberate add-time checks (WU-ParentVisibility's rationale,
  which the `AddStoryAsync` comment already gave). The WU32 text below keeps its original wording as
  the record. Test tier: none owed (doc only); tiers 2/3 are covered by Integration `GroupServiceTests`.
  **Found while verifying, not fixed (tracker D21):** the confidentiality guard has no test, and it
  exempts the story's own author, so an author can file their own Draft into a group and the members'
  `NewGroupStory` notifications name it. A throwaway Integration probe confirmed it; the comment in
  `AddStoryAsync` now says so. Fixing it needs a product call (may an author park a draft in a group?).
  *(Split by the WU-DocCorrections review fixes, 2026-10-14: the title reaching members breaks the
  settled Class-A rule however that call goes, so it stays on tracker D21 as a defect for the next fix
  WU; the product call is decision row 31, tracker F28.)*
  **WU-InertFeatures Stage note (owner ruling D16 +
  service audit §2.8 bullets 1–3):** `AddStoryAsync` now notifies only inside `if (!alreadyAdded)` —
  re-adding an existing story was a repeatable fan-out any member could trigger; the
  `if (storyAuthorId.HasValue)` wrapper is gone (an authorless story silenced the member fan-out
  too); it passes `groupStory.GroupStoryId` and the nullable author to
  `NotifyNewGroupStoryAsync`, which anchors both 60 and 25 on that junction row and excludes the
  story's author from the 60 fan-out (one event, one notification). Verified by Integration
  `GroupServiceTests` (+5: both types carry the `GroupStoryId` and enrich to group name + `/group/{id}`
  + story title, title-less after `RemoveStoryAsync`; an author-member gets 25 only; a re-add by another
  member writes nothing even with the first rows read; an authorless story still notifies members; two
  different stories while unread are two 60s). The author-exclusion and re-add tests fail against the
  pre-fix code (mutation-checked). **Browser-driven 2026-09-30** by the WU-InertFeatures browser pass
  (tracker H14 closed). A member's add wrote one 60 and one 25, both anchored on the new `GroupStory`.
  The bell read "{story} was added to {group}" and "Your story {story} was added to {group}", and a
  re-add wrote nothing (`audit/Notifications.md` F41's browser note). Original WU32 text follows.
  `IGroupWriteService.AddStoryAsync` enforces three-tier
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
- **L3-Logic — Stage 5 (2026-06-24, WU32; folder-name validation in Core `GroupFolderValidations` and
  the folder page's refusal-and-reload handlers since WU-SchemaHardening, 2026-10-13 — stays Stage 5,
  Stage note at the end of this feature).** `CreateFolderDto` with `MaxRating ≤ group cap` constraint.
  `AddGroupStoryDto` carries optional `GroupFolderId`. `ContentRatingExceededException` is a Core domain
  exception. `GroupFolderValidations.NameErrors` (tier 2) trims a folder name, requires it and caps it
  for create and rename. `GroupFolderManagementPage`'s write handlers keep each "no longer exists"
  refusal and reload the tree; a refused rename also closes its editor. Test tiers: Unit
  `GroupFolderValidationsTests` (the name rules); RazorComponents `GroupFolderManagementPageTests` (the
  handlers); Integration `GroupServiceTests` and `GroupFolderIntegrityTests` (the server-side checks).
- **L3.5-Structure — Stage 5 (2026-06-24, WU32; folder-management page built WU-GroupsL5,
  2026-07-24; delete copy and the new refusals handled WU-SchemaHardening, 2026-10-13 — stays Stage 5,
  and L4.5 went 5→1 at the WU and back to 5 at its browser pass the same day, tracker H28 closed;
  Stage note and browser verification note at the end of this feature).** Folder tree rendered inline in `GroupPage` via `RenderFolders` recursive
  `RenderFragment` helper (read-only display, deliberate M-badge zero-trace suppression for
  non-consenting viewers — WU-AccessGate). Inline add-story form (StoryId text field) with
  `ContentRatingExceededException` surfaced as error toast *(an inline `ErrorAlert` above the stories
  since WU-TrackerFixes, 2026-10-13 — F40's Stage note)*. The admin "Manage" link to
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
  Test tier: RazorComponents (`GroupFolderManagementPageTests`, 17 tests since the WU-SchemaHardening
  review fixes, 2026-10-13 — admin gate (member and
  non-member both denied), create dispatch incl. nested `ParentFolderId`, rename dispatch, the
  two-step delete guard, the reorder value-swap + boundary-disabled buttons, validation-error
  surfacing via `InlineAlert`, and WU-SchemaHardening's delete copy and refusals); Integration/Unit
  tiers for the transport layer are noted on F38's new Stage note above.

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

### WU-SchemaHardening Stage note (2026-10-13) — Feature 39: folder integrity, `group_stories` as a set

**No L1/L2/L3-Logic/L3.5 flip; F39 L4.5 5→1, and back to 5 at the browser pass the same day (note
below).** Schema audit §2.4 and service audit §2.10 found the folder model
sound in shape but unguarded. These defects sat beneath Stage-5 cells:
- `parent_folder_id` had no FK: a deleted folder's sub-folders kept a dangling parent id and vanished
  from the tree, while `DeleteFolderAsync`'s comment claimed a SET NULL configured nowhere.
- `CreateFolderAsync` stored any `ParentFolderId`, including another group's folder or a nonexistent id.
- Names were unvalidated: a blank name persisted, a name over 100 characters was a raw 500, and a
  duplicate sibling was a raw 500. Two top-level folders could share a name (default NULLS DISTINCT).
- `group_stories` had no unique `(GroupId, StoryId)`, so the idempotent add was advisory (D23).

What changed:
- **L1** (migration `WU_SchemaHardening`): the self-FK `fk_group_folders_group_folders_parent_folder_id`
  `ON DELETE SET NULL` with its convention index; `ix_group_folders_group_id_parent_folder_id_name`
  rebuilt NULLS NOT DISTINCT under the same name and columns; `ck_group_folders_no_self`;
  `ix_group_stories_group_id_story_id` UNIQUE (it replaced the FK index `ix_group_stories_group_id`).
  Hand DML at the top of `Up()` moves dangling or cross-group parents to the top level, suffixes
  duplicate top-level names with the row id and deletes duplicate `group_stories`. It was rehearsed on
  a dev-DB clone dirtied with each case (all fixed, a duplicate's folder assignment cascaded), then
  Down and Up again; the migration is applied to the server-only dev DB.
- **L2** (`ServerGroupWriteService`; rule `layer2-services.md` §"Group Folder Integrity"): the new
  Core helper `GroupFolderValidations.NameErrors` (an L3-Logic tier-2 validator) on create and rename; byte-wise sibling pre-checks;
  renaming a folder to its own name is a no-op; a parent must be in the same group (`KeyNotFoundException`,
  the D3.1 non-disclosure shape). Delete refuses, naming each clash, when a sub-folder would duplicate a
  top-level name (a default: roadmap decision row 22). Otherwise it appends the sub-folders after the
  existing top-level folders and deletes set-based, so the database's SET NULL runs after the parent is
  gone; a sub-folder named like its deleted top-level parent takes that name. Race backstops catch
  23505/23503 by constraint name and answer as the checks do, detaching the rejected entity:
  create/rename (name index), create (parent FK), delete (name index, then a re-check for the message),
  and `AddStoryAsync` (the `group_stories` index; the add counts as already done, with no folder
  assignment and no notification). The phantom SET-NULL comment is gone (service §4's false-comment
  inventory: WU-DocCorrections can skip it), and `IGroupWriteService`'s docs now state the rules.
- **L3.5** (`GroupFolderManagementPage` markup): the delete confirmation says sub-folders move to the
  top level.
- **L3-Logic** (the same page's `@code` handlers): a delete refusal shows the service's message instead
  of the generic error; a gone parent on create shows "That parent folder no longer exists."; and every
  "no longer exists" refusal reloads the tree, keeping the message (`error-handling.md`'s
  moved-under-the-page rule; it was missing on rename, delete and reorder too). A refused rename also
  closes its editor.

Tests:
- **Integration** `GroupFolderIntegrityTests` (24). It covers name, parent, delete and set semantics,
  plus five interleaved races (`InterleavingCommandInterceptor`): create, rename, parent deleted, delete
  and add story.
- **Integration** `SchemaConstraintTests`, the folder and `group_stories` rows: the NULLS NOT DISTINCT
  index, the self-FK refusing a missing parent and setting children NULL, the duplicate
  `group_stories` row.
- **Integration** `GroupEndpointsTests` +2 (a duplicate top-level name and a colliding delete both
  answer 400).
- **RazorComponents** `GroupFolderManagementPageTests` +4.
- **Unit** `GroupFolderValidationsTests` (8).
- Mutation-checked, each failing its tests when reverted: a tracked EF delete instead of the set-based
  one (the parent-named sub-folder trips the index), the sort-order append, the same-group parent
  check, the name validation, the `AddStoryAsync` catch, and the page's create and delete catches and
  its copy. The rename and reorder catches had no test at the WU (see the review fixes below).

**Review fixes (2026-10-13) — no flip.** Three paths the WU changed had no test, so reverting them left
the suite green:
- **RazorComponents** `GroupFolderManagementPageTests` +2 (17 in all). The fake service gained
  `RenameFolderException` and `ReorderFolderException`. A rename whose folder is gone keeps the
  message, reloads the tree and closes the editor; a move whose folder is gone keeps the message and
  reloads. Mutation-checked: removing either catch's reload, or the rename catch's
  `_editingFolderId = null`, fails its test.
- **Integration** `GroupFolderIntegrityTests` +1 (25, six interleaved races):
  `DeleteFolder_DeletedByAnotherCallBeforeTheDelete_ThrowsKeyNotFound`. A competing delete lands just
  before this call's `DELETE`, which then affects no row and throws `KeyNotFoundException` (before the
  WU, a lost delete-delete race was a concurrency exception and a 500). Mutation-checked: without the
  0-row check the delete returns silently and the test fails.
- The L6 intent ledger's `GroupStory` row now names the UNIQUE `ix_group_stories_group_id_story_id`.

**Browser:** this WU ran with no browser available, so F39 L4.5 went 5→1 (the campaign's no-browser
precedent: the page's delete behaves differently and has new refusals). Tracker **H28** lists the
steps that return it to 5. (It ran the same day: the browser verification note below.)

### WU-SchemaHardening browser verification Stage note (2026-10-13) — F39 L4.5 1→5; tracker H28 closed

Tracker H28's four steps on the server-only path, as AuthorAlpha (the seed group's admin) on
`/group/1/folders`, on both render phases. The circuit pass shows `_blazor/negotiate` and no `/api`
call for the action; the WASM pass shows no negotiate and the action's `/api/groups/folders…` request.
`psql` on `group_folders` and `group_folder_group_story` after every write. The browser console showed
no errors. The server log's only `fail:` lines are the documented stale-antiforgery line on the first
request and EF Core's own lines for a handled race (last bullet). **No runtime bug was found in the
WU's code.**
- **Step 1: delete moves sub-folders up.** Circuit: "A" with sub-folder "B"; "Seed Story: Completed
  One-Shot (E)" filed into "B" from the group page. The confirmation reads "… Any sub-folders move to
  the top level. This can't be undone." After the delete, "B" is top-level (`parent_folder_id` NULL)
  with `sort_order` 2, after the existing top level, and its story stayed filed. WASM: the same with
  "G"/"H" and "Seed Story: Five Chapters + Alt Version (T)"; `DELETE` 204, "H" got `sort_order` 4, after
  the top level's highest 3.
- **Step 2: the clash refusal.** "C" with sub-folder "D", plus a top-level "D". Deleting "C" shows
  "Can't delete "C": its sub-folder "D" would move to the top level, where a folder named "D" already
  exists. Rename one of them first." on both phases (WASM `DELETE` 400), and `psql` shows nothing
  deleted. With two clashes the circuit shows one line per clash; WASM shows them joined in one
  paragraph, because the 400 carries one `Detail` (`ClientHttpHelpers`' documented contract: per-item
  errors do not round-trip). Both read.
- **Step 3: names.** A padded "  B " beside "B" answers "A folder named "B" already exists here.";
  renaming a top-level "D" to "C" answers the same shape and keeps the editor open; renaming "D" to
  "D" is a no-op (WASM `PUT` 204, no message); "D" under another parent is accepted. Create and Save
  are disabled for a spaces-only name on both phases, and the input's `maxlength` stops typing at 100,
  so the server refusals were driven by same-origin `fetch`: a blank create and a blank rename answer
  400 "A folder name is required.", 101 characters 400 "Folder names must be 100 characters or
  fewer.", and a nonexistent parent 404.
- **Step 4: a stale tab.** A second tab deleted folders out from under the first. Creating under the
  gone parent shows "That parent folder no longer exists.", resets the parent select to (root) and
  reloads the tree without it. Renaming a gone folder shows "That folder no longer exists.", closes the
  editor and reloads; moving one and deleting one (a delete-delete) show the same and reload. Both
  phases; on WASM each answered 404.
- **Other actors.** TestUser (a member) sees "You must be an admin of this group to manage its
  folders.", and the API answers 403 to its create and delete. Anonymous gets the sign-in page (401)
  and 401 from the API.
- **Beyond the steps: `AddStoryAsync`'s race backstop.** Four concurrent `POST /api/groups/stories` for
  one story all answered 204. There is one `group_stories` row (three losing inserts hit
  `ix_group_stories_group_id_story_id`), one folder assignment and one fan-out (two type-60 rows). EF
  Core logs each caught violation as `fail: Microsoft.EntityFrameworkCore.Update[10000]` and
  `…Database.Command[20102]` before the service handles it. That is a pre-existing logging class
  shared with the moderation report dedup and recommendation backstops, so it is tracker **E10**, not
  changed here.
- **Tests:** none added, because no runtime defect was found.
- **Cells:** F39 L4.5 goes 1→5. Tracker H28 is closed. The folders and the group story this pass made
  were removed through the UI afterwards; the dev-DB state it left is listed in the workplan entry.

## Feature 40 — Group Display

**WU-TrackerFixes Stage note (2026-10-13) — tracker B17's group half and the inline refusals (the
WU-ErrorHandling2 follow-up); L4.5 5→1, other cells unchanged.**
- **Blog-section Report (B17).** `BlogPostCard` gets `IsOwner` from the listing's new `AuthorId` and
  `OnReport` for signed-in viewers. The page's single `ReportDialog` moved from inside the stories
  `<section>` to page level so the blog section reaches it.
- **Refusals inline, never toasts.** The page's eight catch sites showed `Toasts.Show(...)`, which
  `error-handling.md` reserves for non-blocking system events. Join/leave refusals now show in an
  `ErrorAlert` under the membership controls, story add/remove/file refusals above the stories, and the
  folder tree's unassign refusal inside the tree; each carries Sign in for an expired session. A
  non-user-facing failure now logs at Error with the group id (it was silent). `IToastService` is no
  longer injected.
- Tests: **RazorComponents** `GroupPageTests` +4 (Report on another member's post and not the viewer's
  own; none for anonymous viewers; an inline join refusal with Sign in; an inline remove refusal that a
  fresh attempt clears). **L4.5 5→1:** no browser was available; tracker **H38** steps 2 and 6.
- **Review fixes (2026-10-13), no flip.** The viewer's own blog card rendered an empty bordered footer
  row (fixed in `BlogPostCard`, F36's note). Three surfaces lacked a test: `GroupPageTests` +3 — the blog
  card files a `BlogPost` report for its post, not a `Story` one (the page's one dialog serves both);
  a filing refusal shows above the stories; an unfile refusal shows inside the tree with Sign in. The
  stale "inline error strip" comment above `@code` is gone. H38 step 2 gains the footer check.
- **Browser pass (2026-10-13), L4.5 1→5.** `psql` after every write:
  - **Blog-section Report** (Feature 36's note): filed from a member's view on both phases; the
    author's own card shows no Report and no footer row; signed out, none.
  - **Inline refusals, on WASM with the session expired** (AuthorAlpha as admin of group 1; TestUser for
    Join on group 2): Join showed the session message with Sign in under the header; filing a story
    into a folder and Remove from group showed it above the stories; unfiling from the folder tree showed
    it inside the tree, and the stories' message stayed, each beside its own controls. No toast, no error
    boundary; `group_members`, `group_stories` and the folder assignment unchanged. Signed in again, a
    real file and unfile of story 3 went through, leaving no assignment.
  - **Fixed:** the story cards offered Report to anonymous viewers (Moderation F46); `GroupPageTests` +2.
  - **Seen, filed:** after a refused file the story's folder select still shows the folder it was not
    filed into (tracker **H40**).
  - Tracker **H38** closed.

- **Settled — group content FKs are RESTRICT (owner ruling D10, 2026-08-06; built WU-TptHardDelete
  2026-09-30).** `group_comments.group_id` and `group_blog_posts.group_id` refuse a group delete while
  the group has comments or posts: deleting the group could never reach their `base_comments` /
  `base_blog_posts` rows. No group-delete path exists (D47(b) pending). Whoever builds one adds a
  group-scope method to `TptDelete` first (`layer1-data-model.md` §"Hard-deleting a content parent").
- **WU-TptHardDelete Stage note (2026-09-30; owner ruling D10) — no flip.** L1: migration
  `WU_TptHardDelete_FkPosture` flips both FKs above from CASCADE to RESTRICT. L2: a group post can now be
  edited and deleted by its author (`UpdateGroupBlogPostAsync`/`DeleteGroupBlogPostAsync`, author-only,
  no membership recheck — `audit/BlogPosts.md` F35); before, the only delete path was the profile one,
  which 500'd on a group id. Deleting a post leaves its group untouched. Tier: Integration —
  `TptHardDeleteTests` (raw `DELETE FROM groups` with a group comment, then with a group post, each
  refused by its named constraint; the catalog guard; a group post's author deletes it with no base
  rows left) and `BlogPostWriteServiceTests` (group-post gates, the author who left the group). No
  group-delete path was added (D47(b) pending), so there is no group-scope `TptDelete` method.
- **WU-UserDeletion Stage note (2026-10-13; owner ruling D13 edge (b)) — F40 L4.5 5→1; L2/L3/L3.5 stay
  5.** The admin-less group on the group page (F38's Settled note "the admin-less group").
  - **L2.** `GroupDetailDto` gains trailing `int AdminCount`, projected in `GetByIdAsync` as
    `g.GroupMembers.Count(m => m.Role == Admin)`. Tier: Integration — `UserDeletionServiceTests` (0 after
    a sole admin's account deletion) and `GroupAdminRescueTests` (0 → 1 after the assignment).
  - **L3/L3.5.** `GroupPage` resolves `_viewerIsModerator` from the `AuthState` cascade (never
    persisted; `AdminCount` rides the persisted `Group` DTO). A Moderator/Admin viewer of a group with
    `AdminCount == 0` sees the new `GroupAdminRescuePanel` (`SharedUI/Groups/`, explicit `@namespace`):
    the first 100 members in a labelled `<select>` (a note when there are more), a required notes
    `<textarea maxlength="1024">`, and "Assign as admin" (Container + `action` Control + `ErrorAlert`).
    It calls `IModerationWriteService.AssignGroupAdminAsync` (the server service on the circuit, the
    client over WASM), toasts, and raises `OnAssigned` so the page reloads and the panel hides. A
    refusal keeps its message and reloads the member list (`error-handling.md`). The sole admin's Leave
    (`_currentUserRole == Admin && AdminCount == 1`) opens a `ConfirmDialog` saying the group will have
    no admin until a site moderator assigns one; every other Leave is unchanged, and `LeaveAsync` is
    untouched. Tier: RazorComponents — `GroupPageTests` +9 (the panel only for a moderator of an
    admin-less group; the sole admin's warning, confirm and cancel; a non-sole admin and a member leave
    directly) and the new `GroupAdminRescuePanelTests` (5: labelled controls, submit disabled until a
    member and non-blank notes, the call carries exactly the pick and notes and raises `OnAssigned`, a
    refusal keeps the message and reloads, the more-members note). Mutation-checked: dropping the
    `AdminCount` condition or the leave check fails its tests. `check-design-tokens.ps1` and
    `check-a11y.ps1` pass.
  - **L4.5 5→1.** No browser was available; tracker **H29** carries the moderator panel and the
    leave warning. It went back to 5 at the browser pass the same day (below).
- **WU-UserDeletion review fixes (2026-10-13) — no flip; L4.5 stays 1.** A refusal left the panel stale:
  it reloaded only its own member list, so after another moderator's assignment `Group.AdminCount` stayed
  0 and the panel kept offering "Assign as admin", refused on every retry. The panel now raises
  `OnStale(message)` instead when the host wires it; `GroupPage.HandleRescueStaleAsync` reloads the group
  (the panel hides once it has an admin) and shows the refusal in an `ErrorAlert` where the panel was,
  since the reload can remove or recreate the panel. Any later load clears the message; a failed reload
  is logged and appended. Standalone, the panel keeps its old behaviour. Rule: `error-handling.md`, the
  child-panel case (new). Tier: RazorComponents — `GroupPageTests` +1 (a "still has an admin" refusal
  reloads the group, hides the panel and keeps the message) and `GroupAdminRescuePanelTests` +1 (with a
  handler the message goes to the host and the panel shows none); unwiring `OnStale` on the page, or
  skipping the panel's handler branch, fails them. The class doc's pointer to the guards' tests now names
  `GroupAdminRescueTests`. Tracker **H29** step 7's stale-tab check is updated.
- **WU-UserDeletion browser verification (2026-10-13) — F40 L4.5 1→5.** Server-only path, `psql`
  after every write, both render phases. The method, accounts and logs are in `audit/Identity.md`
  F52's browser verification Stage note. No bug was found in the group page.
  - **Fixture groups.** Group 6, "E2E Rescue: leave route", was founded by AuthorBeta; TestUser,
    ReaderGamma and ModUser joined. Group 5, "E2E Rescue: deletion route", was founded by E2EDelY;
    ReaderGamma joined.
  - **Leave warning.** AuthorBeta, sole admin of group 6, clicked Leave on the circuit. "Leave as the
    only admin?" opened with its copy. Stay closed it with nothing written. Leave group left:
    `group_members` held no Admin row, and `creator_id` still names AuthorBeta. ReaderGamma, sole
    admin of group 5 by then, saw the same dialog on WASM and stayed; no `/api/groups/5/leave` call
    was made. A plain member left group 6 without a prompt on both phases: ModUser on the circuit,
    ReaderGamma on WASM.
  - **Rescue panel.** Group 5 went admin-less when its founder deleted their account. Group 6 went
    admin-less when its admin left. As ModUser, "This group has no admin" listed the members
    longest-standing first, with labelled controls and `maxlength` 1024. "Assign as admin" stayed
    disabled with only a member picked, with whitespace notes, and with only notes.
    - **WASM, group 5.** The assignment sent `POST /api/moderation/groups/5/assign-admin` (204). The
      toast read "ReaderGamma is now this group's admin.", the page reloaded and the panel was gone.
    - **Circuit, group 6.** The same flow made no `/api` call and gave TestUser the admin role.
    - **`psql`, both groups.** The member's role became Admin. One `reports` row was written per
      group: type 6, reason 1, reporter and moderator both ModUser, Resolved – Action Taken, the notes
      in both columns, reported and resolved at one instant. `reported_user_id` holds the founder
      snapshot: NULL for group 5, whose founder deleted their account, and AuthorBeta for group 6.
      `groups_joined` did not move.
  - **Stale second tab.** On each phase a second tab opened while the group was admin-less, then
    assigned after the first tab had. WASM answered 400 with no console error; the circuit made no
    `/api` call. Both reloaded the page, which dropped the panel and showed "This group still has an
    admin. A moderator can only assign an admin to a group that has none." where it had been. Nothing
    was written.
  - **Who sees the panel.** A plain member of an admin-less group (ReaderGamma on group 6) saw no
    panel on either phase. Her `POST` to the assign route got 403, and an anonymous one got 401. A
    moderator who is a member of a group with an admin saw no panel either.
  - **Not driven: a two-admin group's Leave.** No in-app path makes a second admin, because D13
    keeps member-to-member transfer absent. RazorComponents `GroupPageTests` covers it.
- **L1 — Stage 5** (group comment and group post FKs RESTRICT since WU-TptHardDelete, 2026-09-30 — Stage note above).
- **L2 — Stage 5 (2026-06-24, WU32; `GroupDetailDto.AdminCount` since WU-UserDeletion, 2026-10-13, Integration-tested; group-post update/delete since WU-TptHardDelete, 2026-09-30 — Stage notes above).** `IGroupReadService.GetListingsAsync` (audience-filtered, paged),
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
- **L3-Logic — Stage 5 (2026-06-24, WU32; `GroupPage`'s moderator flag, leave warning and the panel's submit/refusal handling since WU-UserDeletion, 2026-10-13, the refusal handed to the page to reload the group since its review fixes the same day, RazorComponents-tested — Stage notes above).** `GroupAudienceTypeMapper` (preset round-trip; inverse).
  `CommentSection.GroupId` param + `CommentTarget.Group` branch (exactly-one-set guard, Load/Post/Reply).
  Test tier: Unit (`GroupAudienceTypeMapperTests`) — all three presets, round-trip, unknown value throws.
  RazorComponents (`CommentSectionGroupTests`) — initial load, post dispatch, no-spoiler-toggle, guard.
- **L3.5-Structure — Stage 5 (2026-06-24, WU32; the admin-less rescue panel and the sole admin's leave warning since WU-UserDeletion, 2026-10-13, RazorComponents-tested — Stage note above).** `GroupCard.razor` (leaf, no inject, audience badge).
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
- **L4.5 — Stage 5 (1→5 at the WU-TrackerFixes browser pass, 2026-10-13: the blog section's Report and the inline refusals, driven on both render phases — tracker H38 closed; 5→1 at WU-TrackerFixes earlier the same day; Stage note at this feature's top, with its review fixes the same day, no flip. Before that Stage 5: 5→1 at WU-UserDeletion, 1→5 at its browser pass, both 2026-10-13).** The rescue
  panel, its stale-tab refusal and the sole admin's leave warning were driven on both render phases
  (browser verification Stage note above; tracker **H29** closed). It was 5 from the 2026-07-01/02 pass
  (the "L4.5-Browser verification" section below) through the later browser-verified WUs.
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
- **L3.5 fix — WU-AccessGateSweep2 browser pass (2026-09-30), no cell flips.** The group page's
  "Created by" link pointed at `/profile/{id}`, a route that does not exist (404). It was found beside
  the same defect on the blog post page and the poll voter list (`audit/BlogPosts.md`). It now goes to
  `/user/{id}` and was clicked through live. Tier: RazorComponents — `GroupPageTests` +1, which failed
  with the old href.

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
