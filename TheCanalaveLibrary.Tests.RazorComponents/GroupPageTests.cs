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

    public GroupPageTests()
    {
        _storyService.StoriesById = new Dictionary<int, StoryListingDto>
        {
            [UnfiledStoryId] = NewStory(UnfiledStoryId, "Story A"),
            [FiledStoryId]   = NewStory(FiledStoryId, "Story B")
        };

        Services.AddScoped<IGroupWriteService>(_ => _groupService);
        Services.AddScoped<IStoryReadService>(_ => _storyService);
        Services.AddScoped<IUserStoryInteractionReadService>(_ => new FakeInteractionReadService());
        Services.AddScoped<IBlogPostReadService>(_ => new FakeBlogPostReadService());
        Services.AddScoped<ICommentWriteService>(_ => new FakeCommentWriteService());
        Services.AddScoped<IModerationWriteService>(_ => new FakeModerationWriteService());
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

    private GroupDetailDto NewGroupDetail(IReadOnlyList<GroupFolderDto> folderTree) => new(
        TestGroupId, "Test Group", "A description.", GroupAudienceType.Standard, Rating.M,
        CreatorId: 1, CreatorDisplayName: "Creator", MemberCount: 3,
        DateCreated: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        CurrentUserRole: _groupService.Role, FolderTree: folderTree,
        Stories: [new GroupStoryDto(UnfiledGroupStoryId, UnfiledStoryId), new GroupStoryDto(FiledGroupStoryId, FiledStoryId)]);

    private IRenderedComponent<GroupPage> RenderGroup(GroupRole? role)
    {
        _groupService.Role = role;
        _groupService.GroupDetail = NewGroupDetail([NewFolderX(), NewFolderY()]);

        // GroupPage resolves _currentUserId from a plain [CascadingParameter] Task<AuthenticationState>
        // (not <AuthorizeView>), but StoryCard's nested AddToCustomListMenu DOES use AuthorizeView —
        // bUnit's authorization double satisfies both simultaneously (same pattern as StoryCardTests).
        var auth = this.AddAuthorization();
        if (role is not null)
            auth.SetAuthorized("test-user").SetClaims(new Claim(ClaimTypes.NameIdentifier, "1"));

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
}
