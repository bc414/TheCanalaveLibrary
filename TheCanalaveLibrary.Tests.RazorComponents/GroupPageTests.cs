using System.Security.Claims;
using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.SharedUI;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Render tests for the story↔folder membership behavior added to <see cref="GroupPage"/>
/// (WU-GroupsL5b, 2026-07-24 — closes hidden-deferrals B6). Scoped to this new behavior, not a
/// full GroupPage regression suite: folder contents are visible to every viewer (never gated —
/// only the admin write actions are, per the settled WU32 <c>RequireAdminAsync</c> decision); the
/// per-story assign/remove overlay and the per-folder unassign control render admin-only and
/// dispatch the correct <c>GroupStoryId</c>/<c>GroupFolderId</c> pairs; removing a story from the
/// group is two-step-confirmed. Tier: RazorComponents (bUnit, no host or DB).
/// </summary>
public class GroupPageTests : BunitContext
{
    private const int TestGroupId = 10;
    private const int UnfiledGroupStoryId = 51;
    private const int UnfiledStoryId = 1;
    private const int FiledGroupStoryId = 50;
    private const int FiledStoryId = 2;
    private const int FolderXId = 100;
    private const int FolderYId = 101;

    private readonly FakeGroupWriteService _groupService = new();
    private readonly FakeRelatedStoriesStoryReadService _storyService = new();
    private readonly FakeModerationWriteService _moderationService = new();
    private readonly FakeBlogPostReadService _blogService = new();

    public GroupPageTests()
    {
        _storyService.StoriesById = new Dictionary<int, StoryListingDto>
        {
            [UnfiledStoryId] = NewStory(UnfiledStoryId, "Story A"),
            [FiledStoryId]   = NewStory(FiledStoryId, "Story B")
        };

        Services.AddScoped<IGroupWriteService>(_ => _groupService);
        // The admin-less rescue panel (D13 edge (b)) reads members and writes through the moderation service.
        Services.AddScoped<IGroupReadService>(_ => _groupService);
        Services.AddScoped<IModerationWriteService>(_ => _moderationService);
        Services.AddScoped<IStoryReadService>(_ => _storyService);
        Services.AddScoped<IUserStoryInteractionReadService>(_ => new FakeInteractionReadService());
        Services.AddScoped<IBlogPostReadService>(_ => _blogService);
        Services.AddScoped<ICommentWriteService>(_ => new FakeCommentWriteService());
        Services.AddScoped<IReportSubmissionService>(_ => new FakeReportSubmissionService());
        Services.AddScoped<IToastService>(_ => new FakeToastService());
        // SocialMetaTags (inside the page) injects IPublicUrlProvider — pure Core class, no host dependency.
        Services.AddScoped<IPublicUrlProvider>(_ => new PublicUrlProvider("https://test.local"));
        // StoryDeck nests StoryCard, which nests UserStoryInteractionPanel (write service),
        // TagChip (sprite lookup), and AddToCustomListMenu (write service + its own AuthorizeView) —
        // same registration set StoryCardTests uses.
        Services.AddScoped<IUserStoryInteractionWriteService>(_ => new FakeUserStoryInteractionWriteService());
        Services.AddSingleton<ISpriteReadService>(new OptimisticSpriteReadService("/sprites/themes"));
        Services.AddScoped<ICustomListWriteService>(_ => new FakeCustomListWriteService());

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private static StoryListingDto NewStory(int id, string title) => new(
        id, title, "A description.", null, AuthorId: 1, AuthorName: "Author",
        WordCount: 1000, StoryStatusId: StoryStatusEnum.InProgress, Rating: Rating.T,
        LastUpdatedDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), Tags: []);

    /// <summary>Root folder X containing the filed story; the unfiled story sits in neither folder.</summary>
    private static GroupFolderDto NewFolderX() => new(
        FolderXId, TestGroupId, ParentFolderId: null, "Folder X", Rating.M, SortOrder: 0,
        Stories: [new GroupStoryDto(FiledGroupStoryId, FiledStoryId)], Children: []);

    private static GroupFolderDto NewFolderY() => new(
        FolderYId, TestGroupId, ParentFolderId: null, "Folder Y", Rating.M, SortOrder: 1,
        Stories: [], Children: []);

    private GroupDetailDto NewGroupDetail(IReadOnlyList<GroupFolderDto> folderTree, int adminCount = 1) => new(
        TestGroupId, "Test Group", "A description.", GroupAudienceType.Standard, Rating.M,
        CreatorId: 1, CreatorDisplayName: "Creator", MemberCount: 3,
        DateCreated: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CurrentUserRole: _groupService.Role, FolderTree: folderTree,
        Stories: [new GroupStoryDto(UnfiledGroupStoryId, UnfiledStoryId), new GroupStoryDto(FiledGroupStoryId, FiledStoryId)],
        AdminCount: adminCount);

    private IRenderedComponent<GroupPage> RenderGroup(GroupRole? role, int adminCount = 1, bool asModerator = false)
    {
        _groupService.Role = role;
        _groupService.GroupDetail = NewGroupDetail([NewFolderX(), NewFolderY()], adminCount);

        // GroupPage resolves _currentUserId from a plain [CascadingParameter] Task<AuthenticationState>
        // (not <AuthorizeView>), but StoryCard's nested AddToCustomListMenu DOES use AuthorizeView —
        // bUnit's authorization double satisfies both simultaneously (same pattern as StoryCardTests).
        var auth = this.AddAuthorization();
        if (role is not null || asModerator)
            auth.SetAuthorized("test-user").SetClaims(new Claim(ClaimTypes.NameIdentifier, "1"));
        if (asModerator)
            auth.SetRoles("Moderator");

        IRenderedComponent<GroupPage> cut = Render<GroupPage>(p => p.Add(c => c.GroupId, TestGroupId));
        cut.WaitForState(() => cut.Markup.Contains("Test Group"), TimeSpan.FromSeconds(2));
        return cut;
    }

    private static IElement FindButton(IRenderedComponent<GroupPage> cut, string text) =>
        cut.FindAll("button").First(b => b.TextContent.Trim() == text);

    // ── Folder contents — visible to every viewer, never gated ─────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData(GroupRole.Member)]
    [InlineData(GroupRole.Admin)]
    public void FolderTree_ShowsStoryTitlesAsLinks_ForEveryViewer(GroupRole? role)
    {
        IRenderedComponent<GroupPage> cut = RenderGroup(role);

        cut.Markup.Should().Contain("Folder X");
        IElement link = cut.Find("a[href='/story/2']");
        link.TextContent.Trim().Should().Be("Story B", "the folder's Stories collection resolves to the loaded story's title");
    }

    [Fact]
    public void CreatorName_LinksToTheCreatorsProfileRoute()
    {
        // The creator link pointed at /profile/{id}, a route that does not exist (404) — found in the
        // WU-AccessGateSweep2 browser pass. Profiles live at /user/{id}.
        IRenderedComponent<GroupPage> cut = RenderGroup(GroupRole.Member);

        cut.FindAll("a").Single(a => a.TextContent.Trim() == "Creator")
            .GetAttribute("href").Should().Be("/user/1");
    }

    // ── Admin-only overlay gating ────────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData(GroupRole.Member)]
    public void NonAdmin_SeesNoAssignOverlayOrUnassignControls(GroupRole? role)
    {
        IRenderedComponent<GroupPage> cut = RenderGroup(role);

        // Markup-text checks (not compound CSS selectors — testing.md "AngleSharp compound-selector
        // fragility") for both admin-only surfaces, gated by the same _currentUserRole == Admin check.
        cut.Markup.Should().NotContain("Move Story A to folder", "the assign overlay is admin-only");
        cut.Markup.Should().NotContain("Remove Story B from Folder X", "the per-folder unassign control is admin-only");
        cut.Markup.Should().NotContain("Remove from group");
    }

    [Fact]
    public void Admin_SeesAssignOverlayAndPerFolderUnassign()
    {
        IRenderedComponent<GroupPage> cut = RenderGroup(GroupRole.Admin);

        cut.Find("select[aria-label='Move Story A to folder']").Should().NotBeNull();
        cut.Find("[aria-label='Remove Story B from Folder X']").Should().NotBeNull();
        cut.Markup.Should().Contain("Remove from group");
    }

    // ── Assign / reassign dispatch ───────────────────────────────────────────────

    [Fact]
    public async Task Admin_AssignsUnfiledStoryToFolder_DispatchesAssignOnly()
    {
        IRenderedComponent<GroupPage> cut = RenderGroup(GroupRole.Admin);

        cut.Find("select[aria-label='Move Story A to folder']").Change(FolderXId.ToString());

        cut.WaitForState(() => _groupService.AssignStoryToFolderCalls.Count > 0, TimeSpan.FromSeconds(2));

        _groupService.AssignStoryToFolderCalls.Should().ContainSingle().Which
            .Should().Be((UnfiledGroupStoryId, FolderXId));
        _groupService.UnassignStoryFromFolderCalls.Should().BeEmpty(
            "an unfiled story has nothing to unassign from first");
    }

    [Fact]
    public async Task Admin_ReassignsFiledStory_UnassignsOldThenAssignsNew()
    {
        IRenderedComponent<GroupPage> cut = RenderGroup(GroupRole.Admin);

        cut.Find("select[aria-label='Move Story B to folder']").Change(FolderYId.ToString());

        cut.WaitForState(() => _groupService.AssignStoryToFolderCalls.Count > 0, TimeSpan.FromSeconds(2));

        _groupService.UnassignStoryFromFolderCalls.Should().ContainSingle().Which
            .Should().Be((FiledGroupStoryId, FolderXId));
        _groupService.AssignStoryToFolderCalls.Should().ContainSingle().Which
            .Should().Be((FiledGroupStoryId, FolderYId));
    }

    [Fact]
    public async Task Admin_UnfilesStory_ChoosingUnfiledOption_DispatchesUnassignOnly()
    {
        IRenderedComponent<GroupPage> cut = RenderGroup(GroupRole.Admin);

        cut.Find("select[aria-label='Move Story B to folder']").Change("");

        cut.WaitForState(() => _groupService.UnassignStoryFromFolderCalls.Count > 0, TimeSpan.FromSeconds(2));

        _groupService.UnassignStoryFromFolderCalls.Should().ContainSingle().Which
            .Should().Be((FiledGroupStoryId, FolderXId));
        _groupService.AssignStoryToFolderCalls.Should().BeEmpty();
    }

    [Fact]
    public async Task Admin_ClicksPerFolderUnassignButton_DispatchesUnassignForThatFolder()
    {
        IRenderedComponent<GroupPage> cut = RenderGroup(GroupRole.Admin);

        cut.Find("[aria-label='Remove Story B from Folder X']").Click();

        cut.WaitForState(() => _groupService.UnassignStoryFromFolderCalls.Count > 0, TimeSpan.FromSeconds(2));

        _groupService.UnassignStoryFromFolderCalls.Should().ContainSingle().Which
            .Should().Be((FiledGroupStoryId, FolderXId));
    }

    // ── Remove from group — two-step confirm ────────────────────────────────────

    [Fact]
    public void RemoveTrigger_OpensConfirmDialog_WithoutCallingService()
    {
        IRenderedComponent<GroupPage> cut = RenderGroup(GroupRole.Admin);

        FindButton(cut, "Remove from group").Click();

        _groupService.RemoveStoryCalls.Should().BeEmpty(
            "the trigger must only open the confirm dialog, never remove directly");
        cut.Markup.Should().Contain("Remove story?");
    }

    [Fact]
    public async Task RemoveConfirmed_CallsRemoveStoryAsync()
    {
        IRenderedComponent<GroupPage> cut = RenderGroup(GroupRole.Admin);

        // Two "Story A" overlays exist (unfiled) plus "Story B" (filed) — trigger the first
        // "Remove from group" button found; StoryDeck orders by the Stories array, Story A first.
        FindButton(cut, "Remove from group").Click();
        FindButton(cut, "Remove").Click();

        cut.WaitForState(() => _groupService.RemoveStoryCalls.Count > 0, TimeSpan.FromSeconds(2));

        _groupService.RemoveStoryCalls.Should().ContainSingle().Which.Should().Be(UnfiledGroupStoryId);
    }

    // ── The admin-less group (owner ruling D13 edge (b), WU-UserDeletion) ─────────

    private const string RescueHeading = "This group has no admin";

    [Fact]
    public void RescuePanel_RendersForAModerator_WhenTheGroupHasNoAdmin()
    {
        IRenderedComponent<GroupPage> cut = RenderGroup(GroupRole.Member, adminCount: 0, asModerator: true);

        cut.Markup.Should().Contain(RescueHeading);
        _groupService.GetMembersCalls.Should().Be(1, "the panel loads the members to choose from");
    }

    [Theory]
    [InlineData(GroupRole.Member, 0, false)]  // an ordinary member of an admin-less group
    [InlineData(null, 0, false)]              // anonymous
    [InlineData(GroupRole.Member, 1, true)]   // a moderator, but the group has an admin
    [InlineData(null, 2, true)]
    public void RescuePanel_IsAbsent_UnlessAModeratorViewsAnAdminlessGroup(GroupRole? role, int adminCount, bool asModerator)
    {
        IRenderedComponent<GroupPage> cut = RenderGroup(role, adminCount, asModerator);

        cut.Markup.Should().NotContain(RescueHeading);
        _groupService.GetMembersCalls.Should().Be(0);
    }

    [Fact]
    public void RescuePanel_RefusedBecauseAnotherModeratorAssignedFirst_ReloadsTheGroup_HidesThePanel_KeepsTheMessage()
    {
        // error-handling.md's child-panel case (WU-UserDeletion review fixes): the panel's own member reload
        // left Group.AdminCount at 0, so it kept offering "Assign as admin" and every retry was refused.
        const string refusal = "This group still has an admin. A moderator can only assign an admin to a group that has none.";
        _groupService.Members =
        [
            new GroupMemberDto(21, "Ash", null, GroupRole.Member, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)),
        ];
        _moderationService.AssignGroupAdminCalls = [];
        _moderationService.AssignGroupAdminException = new ModerationValidationException([refusal]);
        IRenderedComponent<GroupPage> cut = RenderGroup(GroupRole.Member, adminCount: 0, asModerator: true);
        int loadsBefore = _groupService.GetByIdCalls;

        // Another moderator's assignment lands server-side while this page still shows the panel.
        _groupService.GroupDetail = NewGroupDetail([NewFolderX(), NewFolderY()], adminCount: 1);
        cut.Find("select").Change("21");
        cut.Find("textarea").Input("Requested by the members");
        FindButton(cut, "Assign as admin").Click();

        cut.WaitForState(() => !cut.Markup.Contains(RescueHeading), TimeSpan.FromSeconds(2));
        _groupService.GetByIdCalls.Should().BeGreaterThan(loadsBefore, "the refusal reloads the group itself");
        cut.Markup.Should().Contain("This group still has an admin", "the refusal survives the panel it came from");
        cut.FindAll("button").Should().NotContain(b => b.TextContent.Trim() == "Assign as admin");
    }

    [Fact]
    public void SoleAdmin_Leave_OpensTheWarning_AndLeavesOnlyOnConfirm()
    {
        IRenderedComponent<GroupPage> cut = RenderGroup(GroupRole.Admin, adminCount: 1);

        FindButton(cut, "Leave").Click();

        cut.Markup.Should().Contain("Leave as the only admin?");
        cut.Markup.Should().Contain("until a site moderator");
        _groupService.LeaveCalls.Should().Be(0, "the sole admin is warned before LeaveAsync runs");

        FindButton(cut, "Leave group").Click();

        cut.WaitForState(() => _groupService.LeaveCalls > 0, TimeSpan.FromSeconds(2));
        _groupService.LeaveCalls.Should().Be(1);
    }

    [Fact]
    public void SoleAdmin_Leave_Cancelled_DoesNotLeave()
    {
        IRenderedComponent<GroupPage> cut = RenderGroup(GroupRole.Admin, adminCount: 1);

        FindButton(cut, "Leave").Click();
        FindButton(cut, "Stay").Click();

        _groupService.LeaveCalls.Should().Be(0);
        cut.Markup.Should().NotContain("Leave as the only admin?");
    }

    [Theory]
    [InlineData(GroupRole.Admin, 2)]   // another admin remains
    [InlineData(GroupRole.Member, 1)]  // a member never sees the warning
    public void Leave_WithAnotherAdminLeft_LeavesDirectly(GroupRole role, int adminCount)
    {
        IRenderedComponent<GroupPage> cut = RenderGroup(role, adminCount);

        FindButton(cut, "Leave").Click();

        cut.WaitForState(() => _groupService.LeaveCalls > 0, TimeSpan.FromSeconds(2));
        cut.Markup.Should().NotContain("Leave as the only admin?");
    }

    // ── Blog-post Report (tracker B17) ─────────────────────────────────────────────

    [Fact]
    public void BlogPosts_ReportShowsOnOthersPosts_NotOnTheViewersOwn()
    {
        // The viewer is user 1 (RenderGroup's signed-in claim).
        _blogService.GroupResult = (
        [
            new BlogPostListingDto(11, "My own post", "Snippet", DateTime.UtcNow, Rating.T, false, AuthorId: 1),
            new BlogPostListingDto(12, "Someone else's post", "Snippet", DateTime.UtcNow, Rating.T, false, AuthorId: 2),
        ], 2);

        IRenderedComponent<GroupPage> cut = RenderGroup(GroupRole.Member);

        // BlogPostCard: root div > title row > title link, so the card is the link's grandparent.
        string[] ReportsOnCard(string title) =>
        [
            .. cut.FindAll("a[href^='/blog/']").Single(a => a.TextContent.Trim() == title)
                .ParentElement!.ParentElement!.QuerySelectorAll("button")
                .Select(b => b.TextContent.Trim()).Where(t => t == "Report"),
        ];

        ReportsOnCard("Someone else's post").Should().ContainSingle();
        ReportsOnCard("My own post").Should().BeEmpty("the listing's AuthorId marks the viewer's own post");
    }

    [Fact]
    public void BlogPosts_Anonymous_NoReport()
    {
        _blogService.GroupResult = (
            [new BlogPostListingDto(12, "Someone else's post", "Snippet", DateTime.UtcNow, Rating.T, false, AuthorId: 2)], 1);

        IRenderedComponent<GroupPage> cut = RenderGroup(role: null);

        cut.Markup.Should().Contain("Someone else's post");
        cut.FindAll("button").Where(b => b.TextContent.Trim() == "Report").Should().BeEmpty(
            "the report endpoint requires auth");
    }
}
