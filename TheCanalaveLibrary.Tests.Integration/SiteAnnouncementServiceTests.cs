using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for the <see cref="SiteBlogPost"/> slice of <see cref="IBlogPostWriteService"/>/
/// <see cref="IBlogPostReadService"/> (WU-SiteNews). Covers: IsModerator||IsAdmin gates every
/// mutation (not author-only — the SitePoll precedent, including "any moderator manages any
/// site post"); anonymous guard; the NotifyAllUsers fan-out fires exactly once on the
/// false→true publish transition and stamps NotifiedAtUtc so a later edit never re-fires it;
/// GetSiteAnnouncementsAsync's published-only default and includeUnpublished/ordering; the
/// GetByIdAsync third branch (BlogPostPage view-page compatibility) and its draft-visibility
/// rule; BlogPostVisibilityGuard's third branch (comment/like parent-visibility enrolment).
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class SiteAnnouncementServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _modId;
    private int _otherModId;
    private int _plainUserId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _modId       = await SeedUserAsync();
        _otherModId  = await SeedUserAsync();
        _plainUserId = await SeedUserAsync();
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
    }

    // ── CreateSiteBlogPostAsync — authorization ────────────────────────────────────

    [Fact]
    public async Task Create_AsModerator_StampsAuthorIdAndDefaultRating()
    {
        int id = await CreatePostAsync();

        SiteBlogPost? post = await LoadPostAsync(id);
        post.Should().NotBeNull();
        post!.AuthorId.Should().Be(_modId);
        post.Rating.Should().Be(Rating.E);
    }

    [Fact]
    public async Task Create_AsPlainUser_ThrowsUnauthorized()
    {
        SetActiveUser(_plainUserId);
        Func<Task> act = () => CreatePostAsync();
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Create_AnonymousViewer_ThrowsInvalidOperation()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        Func<Task> act = () => CreatePostAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Create_SanitizesScriptTagOnSave()
    {
        int id = await CreatePostAsync(content: "<p>Hello</p><script>alert('xss')</script>");

        SiteBlogPost? post = await LoadPostAsync(id);
        post!.Content.Should().NotContain("<script>");
        post.Content.Should().Contain("Hello");
    }

    // Unlike ProfileBlogPost (always draft on create), a SiteBlogPost may be created already
    // published — the moderator chooses via the DTO, no forced draft-first.
    [Fact]
    public async Task Create_WithIsPublishedTrue_PersistsPublished()
    {
        int id = await CreatePostAsync(isPublished: true);
        SiteBlogPost? post = await LoadPostAsync(id);
        post!.IsPublished.Should().BeTrue();
    }

    // ── NotifyAllUsers fan-out — fire-once guard ───────────────────────────────────

    [Fact]
    public async Task Create_PublishedWithNotifyAllUsers_FansOutToEveryUserExceptAuthor()
    {
        int id = await CreatePostAsync(isPublished: true, notifyAllUsers: true);

        List<int> recipients = await GetSiteAnnouncementRecipientsAsync(id);
        // Drop-self excludes the posting moderator — _otherModId + _plainUserId only.
        recipients.Should().BeEquivalentTo([_otherModId, _plainUserId]);

        SiteBlogPost? post = await LoadPostAsync(id);
        post!.NotifiedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Create_PublishedWithoutNotifyAllUsers_DoesNotFanOut()
    {
        int id = await CreatePostAsync(isPublished: true, notifyAllUsers: false);

        (await GetSiteAnnouncementRecipientsAsync(id)).Should().BeEmpty();
        (await LoadPostAsync(id))!.NotifiedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Create_DraftWithNotifyAllUsers_DoesNotFanOutUntilPublished()
    {
        int id = await CreatePostAsync(isPublished: false, notifyAllUsers: true);

        (await GetSiteAnnouncementRecipientsAsync(id)).Should().BeEmpty();
        (await LoadPostAsync(id))!.NotifiedAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Update_DraftToPublishedWithNotify_FansOutOnce()
    {
        int id = await CreatePostAsync(isPublished: false, notifyAllUsers: true);

        await CallUpdateAsync(id, isPublished: true, notifyAllUsers: true);

        List<int> recipients = await GetSiteAnnouncementRecipientsAsync(id);
        recipients.Should().BeEquivalentTo([_otherModId, _plainUserId]);
        (await LoadPostAsync(id))!.NotifiedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_AfterAlreadyNotified_DoesNotReNotify()
    {
        int id = await CreatePostAsync(isPublished: true, notifyAllUsers: true);
        (await GetSiteAnnouncementRecipientsAsync(id)).Should().HaveCount(2);

        // A later edit, still published, still NotifyAllUsers=true — must not insert a second round.
        await CallUpdateAsync(id, title: "Edited Title", isPublished: true, notifyAllUsers: true);

        (await GetSiteAnnouncementRecipientsAsync(id)).Should().HaveCount(2,
            "the fire-once guard (NotifiedAtUtc) must stop a second fan-out on re-edit");
    }

    // ── UpdateSiteBlogPostAsync — authorization (any moderator, not creator-only) ──

    [Fact]
    public async Task Update_ByADifferentModerator_Succeeds()
    {
        int id = await CreatePostAsync(title: "Original");

        // SitePoll precedent: any moderator/admin manages any site post, not just its creator.
        SetActiveUser(FakeActiveUserContext.Moderator(_otherModId));
        await CallUpdateAsync(id, title: "Changed by a different moderator");

        (await LoadPostAsync(id))!.Title.Should().Be("Changed by a different moderator");
    }

    [Fact]
    public async Task Update_ByPlainUser_ThrowsUnauthorized()
    {
        int id = await CreatePostAsync();

        SetActiveUser(_plainUserId);
        Func<Task> act = () => CallUpdateAsync(id);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Update_NotFound_ThrowsKeyNotFound()
    {
        Func<Task> act = () => CallUpdateAsync(blogPostId: 999_999);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── DeleteSiteBlogPostAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ByADifferentModerator_RemovesRow()
    {
        int id = await CreatePostAsync();

        SetActiveUser(FakeActiveUserContext.Moderator(_otherModId));
        await CallDeleteAsync(id);

        (await LoadPostAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task Delete_ByPlainUser_ThrowsUnauthorized()
    {
        int id = await CreatePostAsync();

        SetActiveUser(_plainUserId);
        Func<Task> act = () => CallDeleteAsync(id);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── GetSiteAnnouncementsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetSiteAnnouncements_DefaultExcludesDrafts()
    {
        await CreatePostAsync(title: "Draft", isPublished: false);
        int publishedId = await CreatePostAsync(title: "Published", isPublished: true);

        (BlogPostListingDto[] items, int totalCount) = await CallGetSiteAnnouncementsAsync(includeUnpublished: false);

        totalCount.Should().Be(1);
        items.Should().ContainSingle(i => i.BlogPostId == publishedId);
    }

    [Fact]
    public async Task GetSiteAnnouncements_IncludeUnpublished_IncludesDrafts()
    {
        await CreatePostAsync(title: "Draft", isPublished: false);
        await CreatePostAsync(title: "Published", isPublished: true);

        (BlogPostListingDto[] items, int totalCount) = await CallGetSiteAnnouncementsAsync(includeUnpublished: true);

        totalCount.Should().Be(2);
        items.Should().Contain(i => !i.IsPublished);
    }

    // Regression net for the post-implementation review findings (2026-07-28): the unpublished
    // view must be moderator/admin-only ENFORCED IN THE SERVICE — the flag rides the public HTTP
    // route, so a forged includeUnpublished=true from a plain or anonymous caller must degrade
    // to the published view, never leak draft titles/snippets (GetByAuthorAsync precedent).

    [Fact]
    public async Task GetSiteAnnouncements_ForgedIncludeUnpublished_ByPlainUser_DegradesToPublishedOnly()
    {
        await CreatePostAsync(title: "Draft", isPublished: false);
        int publishedId = await CreatePostAsync(title: "Published", isPublished: true);

        SetActiveUser(_plainUserId);
        (BlogPostListingDto[] items, int totalCount) = await CallGetSiteAnnouncementsAsync(includeUnpublished: true);

        totalCount.Should().Be(1);
        items.Should().ContainSingle(i => i.BlogPostId == publishedId);
    }

    [Fact]
    public async Task GetSiteAnnouncements_ForgedIncludeUnpublished_Anonymous_DegradesToPublishedOnly()
    {
        await CreatePostAsync(title: "Draft", isPublished: false);
        int publishedId = await CreatePostAsync(title: "Published", isPublished: true);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        (BlogPostListingDto[] items, int totalCount) = await CallGetSiteAnnouncementsAsync(includeUnpublished: true);

        totalCount.Should().Be(1);
        items.Should().ContainSingle(i => i.BlogPostId == publishedId);
    }

    [Fact]
    public async Task GetSiteAnnouncements_OrdersNewestFirst()
    {
        int older = await CreatePostAsync(title: "Older", isPublished: true);
        await Task.Delay(10); // ensure distinct DateCreated ordering
        int newer = await CreatePostAsync(title: "Newer", isPublished: true);

        (BlogPostListingDto[] items, _) = await CallGetSiteAnnouncementsAsync();

        items.Select(i => i.BlogPostId).Should().ContainInOrder(newer, older);
    }

    // ── GetSiteAnnouncementForEditAsync — read-side gate (review finding, 2026-07-28) ──
    // The /site/{id}/edit route carries only RequireAuthorization(); without this service-side
    // gate any signed-in user could read a DRAFT announcement's full content.

    [Fact]
    public async Task GetSiteAnnouncementForEdit_ByPlainUser_ThrowsUnauthorized()
    {
        int id = await CreatePostAsync(isPublished: false);

        SetActiveUser(_plainUserId);
        Func<Task> act = () => CallGetForEditAsync(id);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetSiteAnnouncementForEdit_ByADifferentModerator_ReturnsDto()
    {
        int id = await CreatePostAsync(isPublished: false);

        // Role, not authorship — any moderator manages any site post.
        SetActiveUser(FakeActiveUserContext.Moderator(_otherModId));
        SiteAnnouncementEditDto? dto = await CallGetForEditAsync(id);

        dto.Should().NotBeNull();
        dto!.BlogPostId.Should().Be(id);
    }

    // ── GetByIdAsync third branch (BlogPostPage view-page compatibility) ────────────

    [Fact]
    public async Task GetById_PublishedSiteBlogPost_IsVisibleToAnonymousViewer()
    {
        int id = await CreatePostAsync(isPublished: true);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        BlogPostDto? result = await CallGetByIdAsync(id);

        result.Should().NotBeNull();
        result!.Rating.Should().Be(Rating.E);
        result.StoryId.Should().BeNull();
        result.HasSpoilers.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_Draft_ReturnsNullToNonAuthorModerator()
    {
        int id = await CreatePostAsync(isPublished: false);

        SetActiveUser(FakeActiveUserContext.Moderator(_otherModId));
        (await CallGetByIdAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task GetById_Draft_VisibleToItsAuthor()
    {
        int id = await CreatePostAsync(isPublished: false);
        (await CallGetByIdAsync(id)).Should().NotBeNull();
    }

    // ── BlogPostVisibilityGuard third branch (parent-visibility enrolment) ─────────

    [Fact]
    public async Task VisibilityGuard_PublishedSiteBlogPost_IsVisible()
    {
        int id = await CreatePostAsync(isPublished: true);

        using IServiceScope scope = Factory.Services.CreateScope();
        ReadOnlyApplicationDbContext readDb = scope.ServiceProvider.GetRequiredService<ReadOnlyApplicationDbContext>();
        bool visible = await BlogPostVisibilityGuard.IsBlogPostVisibleAsync(
            readDb, FakeActiveUserContext.Anonymous(), id);

        visible.Should().BeTrue(
            "comments/likes on a SiteBlogPost must be reachable — omitting the third branch would silently hide them");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────
    // IMPORTANT: these helpers must be `async Task` with `await` inside the `using IServiceScope`
    // block (same discipline as BlogPostWriteServiceTests) — returning a bare Task disposes the
    // scope before the async continuation completes.

    private async Task<int> CreatePostAsync(
        string title = "Test Announcement",
        string content = "<p>Content</p>",
        bool isPublished = false,
        bool notifyAllUsers = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostWriteService svc = scope.ServiceProvider.GetRequiredService<IBlogPostWriteService>();
        return await svc.CreateSiteBlogPostAsync(new CreateSiteBlogPostDto
        {
            Title          = title,
            Content        = content,
            IsPublished    = isPublished,
            NotifyAllUsers = notifyAllUsers
        });
    }

    private async Task CallUpdateAsync(
        int blogPostId,
        string title = "Updated Title",
        string content = "<p>Updated</p>",
        bool isPublished = false,
        bool notifyAllUsers = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostWriteService svc = scope.ServiceProvider.GetRequiredService<IBlogPostWriteService>();
        await svc.UpdateSiteBlogPostAsync(new UpdateSiteBlogPostDto
        {
            BlogPostId     = blogPostId,
            Title          = title,
            Content        = content,
            IsPublished    = isPublished,
            NotifyAllUsers = notifyAllUsers
        });
    }

    private async Task CallDeleteAsync(int blogPostId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostWriteService svc = scope.ServiceProvider.GetRequiredService<IBlogPostWriteService>();
        await svc.DeleteSiteBlogPostAsync(blogPostId);
    }

    private async Task<BlogPostDto?> CallGetByIdAsync(int blogPostId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostReadService svc = scope.ServiceProvider.GetRequiredService<IBlogPostReadService>();
        return await svc.GetByIdAsync(blogPostId);
    }

    private async Task<(BlogPostListingDto[] Items, int TotalCount)> CallGetSiteAnnouncementsAsync(
        bool includeUnpublished = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostReadService svc = scope.ServiceProvider.GetRequiredService<IBlogPostReadService>();
        return await svc.GetSiteAnnouncementsAsync(1, 20, includeUnpublished);
    }

    private async Task<SiteAnnouncementEditDto?> CallGetForEditAsync(int blogPostId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostReadService svc = scope.ServiceProvider.GetRequiredService<IBlogPostReadService>();
        return await svc.GetSiteAnnouncementForEditAsync(blogPostId);
    }

    private async Task<SiteBlogPost?> LoadPostAsync(int blogPostId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.SiteBlogPosts
            .IgnoreQueryFilters(["IsTakenDown"])
            .FirstOrDefaultAsync(p => p.BlogPostId == blogPostId);
    }

    private async Task<List<int>> GetSiteAnnouncementRecipientsAsync(int blogPostId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications
            .Where(n => n.NotificationTypeId == NotificationTypeEnum.SiteAnnouncement
                        && n.RelatedEntityId == blogPostId)
            .Select(n => n.RecipientUserId)
            .ToListAsync();
    }
}
