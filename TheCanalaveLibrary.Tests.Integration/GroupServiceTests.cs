using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IGroupWriteService"/> (WU32) — Groups feature cluster
/// (Features 38/39/40). Covers: group CRUD, join/leave idempotency, creator-as-admin,
/// content-rating waterfall rejection (tiers 2 and 3), admin-only folder ops, group comments,
/// GroupAudience visibility filter, group blog posts, notification fan-out.
///
/// <b>Per-test seeding:</b> every test seeds users and stories via <c>SeedUserAsync</c> /
/// <c>SeedStoryAsync</c>. Group rows are seeded via <see cref="SeedGroupAsync"/> (inline helper)
/// where needed so FK parents always exist before the service is called. Respawn resets the DB
/// between every test — see testing.md.
///
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class GroupServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    // ── Shared ids set in InitializeAsync ────────────────────────────────────────
    private int _userId;
    private int _otherUserId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userId      = await SeedUserAsync("creator");
        _otherUserId = await SeedUserAsync("other");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Group CRUD
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateGroup_Standard_InsertsGroupAndAddsCreatorAsAdmin()
    {
        SetActiveUser(_userId);

        int groupId = await CreateGroupAsync(new CreateGroupDto
        {
            GroupName    = "Test Group",
            AudienceType = GroupAudienceType.Standard
        });

        GroupDetailDto? detail = await GetGroupAsync(groupId);
        detail.Should().NotBeNull();
        detail!.GroupName.Should().Be("Test Group");
        detail.AudienceType.Should().Be(GroupAudienceType.Standard);
        detail.CreatorId.Should().Be(_userId);
        detail.MemberCount.Should().Be(1, "creator is added as Admin member");

        detail.CurrentUserRole.Should().Be(GroupRole.Admin, "creator is always Admin");
    }

    [Fact]
    public async Task CreateGroup_SfwOnly_PersistsCorrectRatingPair()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto
        {
            GroupName    = "SFW Group",
            AudienceType = GroupAudienceType.SfwOnly
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Group? g = await db.Groups.FindAsync(groupId);
        g.Should().NotBeNull();
        g!.AudienceRating.Should().Be(Rating.E);
        g.MaxContentRating.Should().Be(Rating.T);
    }

    [Fact]
    public async Task CreateGroup_Mature_PersistsCorrectRatingPair()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto
        {
            GroupName    = "Mature Group",
            AudienceType = GroupAudienceType.Mature
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // IgnoreQueryFilters: the GroupAudience filter hides M-rated groups when ShowMatureContent=false.
        // We're verifying internal persistence here, not public visibility.
        Group? g = await db.Groups.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.GroupId == groupId);
        g.Should().NotBeNull();
        g!.AudienceRating.Should().Be(Rating.M);
        g.MaxContentRating.Should().Be(Rating.M);
    }

    [Fact]
    public async Task CreateGroup_Anonymous_ThrowsInvalidOperation()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        Func<Task> act = () => CreateGroupAsync(new CreateGroupDto { GroupName = "Anon Group" });
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateGroup_EmptyName_ThrowsGroupValidationException()
    {
        SetActiveUser(_userId);
        Func<Task> act = () => CreateGroupAsync(new CreateGroupDto { GroupName = "" });
        await act.Should().ThrowAsync<GroupValidationException>();
    }

    [Fact]
    public async Task UpdateGroup_Admin_ChangesNameAndAudienceType()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto
        {
            GroupName    = "Original Name",
            AudienceType = GroupAudienceType.Standard
        });

        await UpdateGroupAsync(new UpdateGroupDto
        {
            GroupId      = groupId,
            GroupName    = "Updated Name",
            AudienceType = GroupAudienceType.SfwOnly
        });

        GroupDetailDto? detail = await GetGroupAsync(groupId);
        detail!.GroupName.Should().Be("Updated Name");
        detail.AudienceType.Should().Be(GroupAudienceType.SfwOnly);
    }

    [Fact]
    public async Task UpdateGroup_NonAdmin_ThrowsUnauthorizedAccess()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Locked Group" });

        // _otherUserId joins as Member (not Admin).
        SetActiveUser(_otherUserId);
        await JoinGroupAsync(groupId);

        Func<Task> act = () => UpdateGroupAsync(new UpdateGroupDto
        {
            GroupId   = groupId,
            GroupName = "Hacked Name"
        });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetListings_ReturnsOnlyVisibleGroups_WhenMatureDisabled()
    {
        // Two groups: one Standard, one Mature.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_userId, showMatureContent: false));

        int stdGroupId  = await CreateGroupAsync(new CreateGroupDto
            { GroupName = "Standard", AudienceType = GroupAudienceType.Standard });
        int matureGroupId = await CreateGroupAsync(new CreateGroupDto
            { GroupName = "Mature",   AudienceType = GroupAudienceType.Mature });

        // Switch to a mature-disabled viewer.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: false));
        (GroupCardDto[] items, int total) = await GetListingsAsync(page: 1, pageSize: 20);

        items.Should().Contain(c => c.GroupId == stdGroupId, "Standard group is visible");
        items.Should().NotContain(c => c.GroupId == matureGroupId, "Mature group hidden from mature-disabled user");
    }

    [Fact]
    public async Task GetListings_MatureGroupVisible_WhenShowMatureContentTrue()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_userId, showMatureContent: true));
        int matureGroupId = await CreateGroupAsync(new CreateGroupDto
            { GroupName = "Mature", AudienceType = GroupAudienceType.Mature });

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: true));
        (GroupCardDto[] items, _) = await GetListingsAsync(page: 1, pageSize: 20);
        items.Should().Contain(c => c.GroupId == matureGroupId);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Membership — join / leave
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task JoinGroup_NewMember_AddsMemberRow()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Open Group" });

        SetActiveUser(_otherUserId);
        await JoinGroupAsync(groupId);

        GroupDetailDto? detail = await GetGroupAsync(groupId);
        detail!.MemberCount.Should().Be(2, "creator + new member");
    }

    [Fact]
    public async Task JoinGroup_Idempotent_DoesNotDuplicateMember()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Group" });

        SetActiveUser(_otherUserId);
        await JoinGroupAsync(groupId);
        await JoinGroupAsync(groupId); // second call — idempotent

        GroupDetailDto? detail = await GetGroupAsync(groupId);
        detail!.MemberCount.Should().Be(2, "still only creator + one member");
    }

    [Fact]
    public async Task LeaveGroup_Member_RemovesRow()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Group" });

        SetActiveUser(_otherUserId);
        await JoinGroupAsync(groupId);
        await LeaveGroupAsync(groupId);

        GroupDetailDto? detail = await GetGroupAsync(groupId);
        detail!.MemberCount.Should().Be(1, "only creator remains");
    }

    [Fact]
    public async Task LeaveGroup_Idempotent_NonMember_NoThrow()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Group" });

        SetActiveUser(_otherUserId);
        // _otherUserId never joined — Leave should be a no-op.
        Func<Task> act = () => LeaveGroupAsync(groupId);
        await act.Should().NotThrowAsync();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Content-rating waterfall — tier 2 (story > group max) and tier 3 (story > folder max)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddStory_Tier2_StoryRatingExceedsGroupMax_Throws()
    {
        // SFW group (MaxContentRating = T) + M-rated story → tier-2 violation.
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto
            { GroupName = "SFW", AudienceType = GroupAudienceType.SfwOnly });
        int mStoryId = await SeedStoryAsync(authorId: _userId, rating: Rating.M);

        Func<Task> act = () => AddStoryAsync(new AddGroupStoryDto
            { GroupId = groupId, StoryId = mStoryId });
        await act.Should().ThrowAsync<ContentRatingExceededException>(
            "M-rated story exceeds SFW group MaxContentRating of T");
    }

    [Fact]
    public async Task AddStory_EStoryToSfwGroup_Succeeds()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto
            { GroupName = "SFW", AudienceType = GroupAudienceType.SfwOnly });
        int eStoryId = await SeedStoryAsync(authorId: _userId, rating: Rating.E);

        Func<Task> act = () => AddStoryAsync(new AddGroupStoryDto
            { GroupId = groupId, StoryId = eStoryId });
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task AddStory_Tier3_StoryRatingExceedsFolderMax_Throws()
    {
        // Standard group (MaxContentRating = M) + folder with MaxRating = E + T-rated story.
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto
            { GroupName = "Std", AudienceType = GroupAudienceType.Standard });
        int folderId = await CreateFolderAsync(new CreateFolderDto
            { GroupId = groupId, Name = "E-only folder", MaxRating = Rating.E });
        int tStoryId = await SeedStoryAsync(authorId: _userId, rating: Rating.T);

        Func<Task> act = () => AddStoryAsync(new AddGroupStoryDto
            { GroupId = groupId, StoryId = tStoryId, GroupFolderId = folderId });
        await act.Should().ThrowAsync<ContentRatingExceededException>(
            "T story exceeds folder MaxRating of E");
    }

    [Fact]
    public async Task AddStory_NonMember_ThrowsUnauthorizedAccess()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Group" });
        int storyId = await SeedStoryAsync(authorId: _otherUserId);

        SetActiveUser(_otherUserId);
        // _otherUserId never joined.
        Func<Task> act = () => AddStoryAsync(new AddGroupStoryDto
            { GroupId = groupId, StoryId = storyId });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Folder management — admin-only
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateFolder_Admin_InsertsFolder()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Group" });

        int folderId = await CreateFolderAsync(new CreateFolderDto
            { GroupId = groupId, Name = "Chapter Fics", MaxRating = Rating.M });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        GroupFolder? folder = await db.GroupFolders.FindAsync(folderId);
        folder.Should().NotBeNull();
        folder!.Name.Should().Be("Chapter Fics");
        folder.MaxRating.Should().Be(Rating.M);
    }

    [Fact]
    public async Task CreateFolder_NonAdmin_ThrowsUnauthorizedAccess()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Group" });

        SetActiveUser(_otherUserId);
        await JoinGroupAsync(groupId); // member, not admin

        Func<Task> act = () => CreateFolderAsync(new CreateFolderDto
            { GroupId = groupId, Name = "Hacker Folder" });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DeleteFolder_Admin_RemovesFolder()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Group" });
        int folderId = await CreateFolderAsync(new CreateFolderDto
            { GroupId = groupId, Name = "TempFolder" });

        await DeleteFolderAsync(folderId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        GroupFolder? folder = await db.GroupFolders.FindAsync(folderId);
        folder.Should().BeNull("folder must be deleted");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Group comments
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PostGroupComment_Member_InsertsCommentRow()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Commenting Group" });

        long commentId = await PostGroupCommentAsync(new PostGroupCommentDto
        {
            GroupId     = groupId,
            CommentText = "<p>Hello group!</p>"
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        GroupComment? comment = await db.GroupComments.FindAsync(commentId);
        comment.Should().NotBeNull();
        comment!.GroupId.Should().Be(groupId);
        comment.UserId.Should().Be(_userId);
    }

    [Fact]
    public async Task GetGroupComments_ReturnsPaginatedComments()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Group" });

        await PostGroupCommentAsync(new PostGroupCommentDto { GroupId = groupId, CommentText = "<p>A</p>" });
        await PostGroupCommentAsync(new PostGroupCommentDto { GroupId = groupId, CommentText = "<p>B</p>" });

        SetActiveUser(_otherUserId);
        await JoinGroupAsync(groupId);
        await PostGroupCommentAsync(new PostGroupCommentDto { GroupId = groupId, CommentText = "<p>C</p>" });

        CommentPageDto page = await GetGroupCommentsAsync(groupId, page: 1, pageSize: 20);
        page.TotalRootCount.Should().Be(3);
        page.Comments.Should().HaveCount(3);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Group blog posts
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateGroupBlogPost_Member_InsertsPost()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Blog Group" });

        int blogPostId = await CreateGroupBlogPostAsync(new CreateGroupBlogPostDto
        {
            GroupId = groupId,
            Title   = "Group Post",
            Content = "<p>Hello group blogosphere!</p>",
            Rating  = Rating.E
        });

        (BlogPostListingDto[] items, int total) = await GetGroupBlogPostsAsync(groupId, page: 1, pageSize: 10);
        total.Should().Be(1);
        items.Should().ContainSingle(p => p.BlogPostId == blogPostId && p.Title == "Group Post");
    }

    [Fact]
    public async Task CreateGroupBlogPost_NonMember_ThrowsUnauthorizedAccess()
    {
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Blog Group" });

        SetActiveUser(_otherUserId);
        // _otherUserId never joined.
        Func<Task> act = () => CreateGroupBlogPostAsync(new CreateGroupBlogPostDto
        {
            GroupId = groupId,
            Title   = "Hacked Post",
            Content = "<p>bad actor</p>"
        });
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Notification fan-out
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddStory_NotifiesMembers_WhenNotifyForNewStoryEnabled()
    {
        // Seed: creator (_userId) creates group; _otherUserId joins with NotifyForNewStory = true (default).
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Notif Group" });
        int storyId = await SeedStoryAsync(authorId: _userId, rating: Rating.E);

        SetActiveUser(_otherUserId);
        await JoinGroupAsync(groupId); // joins with NotifyForNewStory = true (GroupMember default)

        SetActiveUser(_userId);
        await AddStoryAsync(new AddGroupStoryDto { GroupId = groupId, StoryId = storyId });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // _otherUserId should receive NewGroupStory (type 60).
        bool hasNewGroupStoryNotif = await db.Notifications
            .AnyAsync(n => n.RecipientUserId == _otherUserId
                        && n.NotificationTypeId == NotificationTypeEnum.NewGroupStory);
        hasNewGroupStoryNotif.Should().BeTrue(
            "member with NotifyForNewStory=true should receive NewGroupStory notification");

        // Story author (_userId) should receive YourStoryAddedToGroup (type 25).
        // Drop-self: _userId is sourceUserId — so they should NOT receive it.
        bool selfNotified = await db.Notifications
            .AnyAsync(n => n.RecipientUserId == _userId
                        && n.NotificationTypeId == NotificationTypeEnum.YourStoryAddedToGroup);
        selfNotified.Should().BeFalse(
            "drop-self rule: adding your own story should not generate a self-notification");
    }

    [Fact]
    public async Task AddStory_YourStoryAddedToGroup_NotifiesStoryAuthor_WhenDifferentFromAdder()
    {
        // Creator (_userId) adds a story authored by _otherUserId.
        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Notif Group" });

        // Seed the story with a different author.
        int storyId = await SeedStoryAsync(authorId: _otherUserId, rating: Rating.E);

        await AddStoryAsync(new AddGroupStoryDto { GroupId = groupId, StoryId = storyId });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        bool authorNotified = await db.Notifications
            .AnyAsync(n => n.RecipientUserId == _otherUserId
                        && n.NotificationTypeId == NotificationTypeEnum.YourStoryAddedToGroup);
        authorNotified.Should().BeTrue(
            "story author should receive YourStoryAddedToGroup when someone else adds their story");
    }

    [Fact]
    public async Task CreateGroupBlogPost_NotifiesMembers_WhenNotifyForBlogPostEnabled()
    {
        // Seed member with NotifyForNewBlogPost = true (requires direct DB seed since default is false).
        int thirdUserId = await SeedUserAsync("notif-member");

        SetActiveUser(_userId);
        int groupId = await CreateGroupAsync(new CreateGroupDto { GroupName = "Blog Notif Group" });

        // Seed thirdUser as member with NotifyForNewBlogPost = true.
        await SeedGroupMemberAsync(groupId, thirdUserId, GroupRole.Member, notifyBlogPost: true);

        await CreateGroupBlogPostAsync(new CreateGroupBlogPostDto
        {
            GroupId = groupId,
            Title   = "Notification Test Post",
            Content = "<p>Members should be notified.</p>",
            Rating  = Rating.E
        });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        bool hasNotif = await db.Notifications
            .AnyAsync(n => n.RecipientUserId == thirdUserId
                        && n.NotificationTypeId == NotificationTypeEnum.NewGroupBlogPost);
        hasNotif.Should().BeTrue(
            "member with NotifyForNewBlogPost=true should receive NewGroupBlogPost notification");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Private helpers — service calls
    // ─────────────────────────────────────────────────────────────────────────────

    private async Task<int> CreateGroupAsync(CreateGroupDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupWriteService svc = scope.ServiceProvider.GetRequiredService<IGroupWriteService>();
        return await svc.CreateGroupAsync(dto);
    }

    private async Task UpdateGroupAsync(UpdateGroupDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupWriteService svc = scope.ServiceProvider.GetRequiredService<IGroupWriteService>();
        await svc.UpdateGroupAsync(dto);
    }

    private async Task<GroupDetailDto?> GetGroupAsync(int groupId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupReadService svc = scope.ServiceProvider.GetRequiredService<IGroupReadService>();
        return await svc.GetByIdAsync(groupId);
    }

    private async Task<(GroupCardDto[] Items, int TotalCount)> GetListingsAsync(int page, int pageSize)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupReadService svc = scope.ServiceProvider.GetRequiredService<IGroupReadService>();
        return await svc.GetListingsAsync(page, pageSize);
    }

    private async Task JoinGroupAsync(int groupId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupWriteService svc = scope.ServiceProvider.GetRequiredService<IGroupWriteService>();
        await svc.JoinAsync(groupId);
    }

    private async Task LeaveGroupAsync(int groupId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupWriteService svc = scope.ServiceProvider.GetRequiredService<IGroupWriteService>();
        await svc.LeaveAsync(groupId);
    }

    private async Task AddStoryAsync(AddGroupStoryDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupWriteService svc = scope.ServiceProvider.GetRequiredService<IGroupWriteService>();
        await svc.AddStoryAsync(dto);
    }

    private async Task<int> CreateFolderAsync(CreateFolderDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupWriteService svc = scope.ServiceProvider.GetRequiredService<IGroupWriteService>();
        return await svc.CreateFolderAsync(dto);
    }

    private async Task DeleteFolderAsync(int folderId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IGroupWriteService svc = scope.ServiceProvider.GetRequiredService<IGroupWriteService>();
        await svc.DeleteFolderAsync(folderId);
    }

    private async Task<long> PostGroupCommentAsync(PostGroupCommentDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICommentWriteService svc = scope.ServiceProvider.GetRequiredService<ICommentWriteService>();
        return await svc.PostGroupCommentAsync(dto);
    }

    private async Task<CommentPageDto> GetGroupCommentsAsync(int groupId, int page, int pageSize)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICommentReadService svc = scope.ServiceProvider.GetRequiredService<ICommentReadService>();
        return await svc.GetGroupCommentsAsync(groupId, page, pageSize);
    }

    private async Task<int> CreateGroupBlogPostAsync(CreateGroupBlogPostDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostWriteService svc = scope.ServiceProvider.GetRequiredService<IBlogPostWriteService>();
        return await svc.CreateGroupBlogPostAsync(dto);
    }

    private async Task<(BlogPostListingDto[] Items, int TotalCount)> GetGroupBlogPostsAsync(
        int groupId, int page, int pageSize)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostReadService svc = scope.ServiceProvider.GetRequiredService<IBlogPostReadService>();
        return await svc.GetByGroupAsync(groupId, page, pageSize);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Inline seed helpers — group-specific FK parents
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a <see cref="GroupMember"/> row directly via <see cref="ApplicationDbContext"/> so
    /// tests can control <c>NotifyForNewBlogPost</c> (which defaults to <c>false</c> on the entity
    /// and is not exposed via <see cref="IGroupWriteService.JoinAsync"/>). The group must exist.
    /// </summary>
    private async Task SeedGroupMemberAsync(
        int groupId, int userId, GroupRole role, bool notifyBlogPost = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        bool alreadyMember = await db.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (!alreadyMember)
        {
            db.GroupMembers.Add(new GroupMember
            {
                GroupId              = groupId,
                UserId               = userId,
                Role                 = role,
                NotifyForNewBlogPost = notifyBlogPost,
                NotifyForNewStory    = false,
                DateJoined           = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }
}
