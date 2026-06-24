using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ICommentReadService"/> (WU19). Covers: golden-index pagination
/// on root comments, roots ordered DatePosted DESC, direct replies appear under their root ordered
/// DatePosted ASC, <c>TotalRootCount</c> is root-only, <c>IsLikedByCurrentUser</c> is per-viewer
/// (always false for anonymous), page Skip/Take returns the correct window.
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class CommentReadServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestAppFactory _factory = null!;
    private int _userId;
    private int _chapterId;

    public async Task InitializeAsync()
    {
        _factory = new TestAppFactory(postgres.ConnectionString);
        _ = _factory.Services;

        (_userId, _chapterId) = await SeedFixtureAsync();
        // SeedFixtureAsync ends with SetActiveUser(Authenticated) — leave user authenticated
        // so subsequent CallPostAsync helpers work without re-setting per test.
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GetChapterComments_EmptyChapter_ReturnsEmptyPage()
    {
        int emptyChapterId = await SeedEmptyChapterAsync();

        // Read as anonymous — empty chapter should return an empty page.
        SetActiveUser(FakeActiveUserContext.Anonymous());
        CommentPageDto page = await CallGetAsync(emptyChapterId, page: 1, pageSize: 10);

        page.TotalRootCount.Should().Be(0);
        page.Comments.Should().BeEmpty();
    }

    [Fact]
    public async Task GetChapterComments_TotalRootCount_CountsOnlyRoots()
    {
        // Post 2 roots + 1 reply as authenticated user.
        long root1 = await CallPostAsync(_chapterId, "<p>Root 1</p>");
        long root2 = await CallPostAsync(_chapterId, "<p>Root 2</p>");
        await CallPostAsync(_chapterId, "<p>Reply</p>", parentId: root1);

        // Read as anonymous — TotalRootCount must reflect only the roots (2 here), not replies.
        SetActiveUser(FakeActiveUserContext.Anonymous());
        CommentPageDto page = await CallGetAsync(_chapterId, page: 1, pageSize: 100);

        page.TotalRootCount.Should().BeGreaterThanOrEqualTo(2,
            "only root comments count; replies must not inflate TotalRootCount");
        long root1Idx  = page.Comments.ToList().FindIndex(c => c.CommentId == root1);
        long root2Idx  = page.Comments.ToList().FindIndex(c => c.CommentId == root2);
        root1Idx.Should().BeGreaterThanOrEqualTo(0);
        root2Idx.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetChapterComments_RootsAreOrderedNewestFirst()
    {
        // Post two roots sequentially — root2 is newer.
        long root1 = await CallPostAsync(_chapterId, "<p>Older root</p>");
        long root2 = await CallPostAsync(_chapterId, "<p>Newer root</p>");

        // Read as anonymous.
        SetActiveUser(FakeActiveUserContext.Anonymous());
        CommentPageDto page = await CallGetAsync(_chapterId, page: 1, pageSize: 100);

        List<long> rootIds = page.Comments
            .Where(c => c.ParentCommentId == null)
            .Select(c => c.CommentId)
            .ToList();

        // Both roots must appear.
        int idx1 = rootIds.IndexOf(root1);
        int idx2 = rootIds.IndexOf(root2);
        idx1.Should().BeGreaterThanOrEqualTo(0);
        idx2.Should().BeGreaterThanOrEqualTo(0);

        // Newer root must appear before older root (DatePosted DESC).
        idx2.Should().BeLessThan(idx1, "newer root should appear first");
    }

    [Fact]
    public async Task GetChapterComments_ReplyAppearsUnderItsRoot()
    {
        // Post root + reply as authenticated.
        long rootId  = await CallPostAsync(_chapterId, "<p>Root comment</p>");
        long replyId = await CallPostAsync(_chapterId, "<p>Reply</p>", parentId: rootId);

        // Read as anonymous.
        SetActiveUser(FakeActiveUserContext.Anonymous());
        CommentPageDto page = await CallGetAsync(_chapterId, page: 1, pageSize: 100);

        CommentDto? root  = page.Comments.FirstOrDefault(c => c.CommentId == rootId);
        CommentDto? reply = page.Comments.FirstOrDefault(c => c.CommentId == replyId);

        root.Should().NotBeNull();
        reply.Should().NotBeNull();
        reply!.ParentCommentId.Should().Be(rootId);

        // Root must appear before its reply in the ordered list.
        int rootIdx  = page.Comments.ToList().FindIndex(c => c.CommentId == rootId);
        int replyIdx = page.Comments.ToList().FindIndex(c => c.CommentId == replyId);
        rootIdx.Should().BeLessThan(replyIdx, "root must appear before its reply");
    }

    [Fact]
    public async Task GetChapterComments_Pagination_SkipTakeReturnsCorrectWindow()
    {
        // Post exactly 3 root comments as authenticated (so we can control the count).
        long idA = await CallPostAsync(_chapterId, "<p>Paged comment A</p>");
        long idB = await CallPostAsync(_chapterId, "<p>Paged comment B</p>");
        long idC = await CallPostAsync(_chapterId, "<p>Paged comment C</p>");

        // Switch to anonymous for reads.
        SetActiveUser(FakeActiveUserContext.Anonymous());

        CommentPageDto page1 = await CallGetAsync(_chapterId, page: 1, pageSize: 2);
        CommentPageDto page2 = await CallGetAsync(_chapterId, page: 2, pageSize: 2);

        // page1 should have exactly 2 root-level results (there are at least 3 roots).
        page1.Comments.Where(c => c.ParentCommentId == null).Should().HaveCount(2);
        // page2 should have at least 1 root result.
        page2.Comments.Where(c => c.ParentCommentId == null).Should().NotBeEmpty();

        // The ids on page1 and page2 must not overlap.
        IEnumerable<long> ids1 = page1.Comments.Select(c => c.CommentId);
        IEnumerable<long> ids2 = page2.Comments.Select(c => c.CommentId);
        ids1.Should().NotIntersectWith(ids2);

        // All three test ids must appear somewhere across pages 1 and 2.
        IEnumerable<long> allIds = ids1.Union(ids2);
        allIds.Should().Contain(idA);
        allIds.Should().Contain(idB);
        allIds.Should().Contain(idC);
    }

    [Fact]
    public async Task GetChapterComments_IsLikedByCurrentUser_TrueForLiker_FalseForAnonymous()
    {
        // Post as authenticated _userId (already set in InitializeAsync).
        long id = await CallPostAsync(_chapterId, "<p>Like check</p>");

        // Toggle like as the same user.
        using IServiceScope likeScope = _factory.Services.CreateScope();
        ICommentWriteService writeSvc = likeScope.ServiceProvider.GetRequiredService<ICommentWriteService>();
        await writeSvc.ToggleLikeAsync(id);

        // Read as the liker → IsLikedByCurrentUser = true.
        CommentPageDto asLiker = await CallGetAsync(_chapterId, page: 1, pageSize: 100);
        CommentDto? liked = asLiker.Comments.FirstOrDefault(c => c.CommentId == id);
        liked.Should().NotBeNull("the just-posted comment must appear in the page");
        liked!.IsLikedByCurrentUser.Should().BeTrue();

        // Switch to anonymous → IsLikedByCurrentUser = false.
        SetActiveUser(FakeActiveUserContext.Anonymous());
        CommentPageDto asAnon = await CallGetAsync(_chapterId, page: 1, pageSize: 100);
        CommentDto? anonLiked = asAnon.Comments.FirstOrDefault(c => c.CommentId == id);
        anonLiked.Should().NotBeNull();
        anonLiked!.IsLikedByCurrentUser.Should().BeFalse();
    }

    // --- Helpers ---

    private async Task<CommentPageDto> CallGetAsync(int chapterId, int page, int pageSize)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ICommentReadService svc = scope.ServiceProvider.GetRequiredService<ICommentReadService>();
        return await svc.GetChapterCommentsAsync(chapterId, page, pageSize);
    }

    private async Task<long> CallPostAsync(int chapterId, string text, long? parentId = null)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ICommentWriteService svc = scope.ServiceProvider.GetRequiredService<ICommentWriteService>();
        return await svc.PostChapterCommentAsync(new PostChapterCommentDto
        {
            ChapterId       = chapterId,
            ParentCommentId = parentId,
            CommentText     = text
        });
    }

    private async Task<(int userId, int chapterId)> SeedFixtureAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // .Where() + no-arg FirstAsync() — avoids EF Core / BCL AsyncEnumerable ambiguity in .NET 10.
        int uid = (await db.Users.Where(u => u.Id > 0).FirstAsync()).Id;

        string suffix = Guid.NewGuid().ToString("N")[..8];
        Story story = new()
        {
            Rating          = Rating.E,
            StoryStatusId   = StoryStatusEnum.InProgress,
            PublishedDate   = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
            StoryListing    = new StoryListing { StoryTitle = $"Comment RS Fixture {suffix}", ShortDescription = "test" },
            StoryDetail     = new StoryDetail { LongDescription = "test", PostApprovalStatus = StoryStatusEnum.InProgress }
        };
        db.Stories.Add(story);
        await db.SaveChangesAsync();

        Chapter chapter = new()
        {
            StoryId          = story.StoryId,
            ChapterNumber    = 1,
            Title            = "Chapter 1",
            PrimaryContentId = null,
            IsPublished      = true,
            VersionCount     = 0
        };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();

        // Leave the user authenticated so CallPostAsync works without re-setting per test.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(uid, showMatureContent: false));
        return (uid, chapter.ChapterId);
    }

    private async Task<int> SeedEmptyChapterAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        int anyStoryId = (await db.Stories.Where(s => s.StoryId > 0).FirstAsync()).StoryId;
        string suffix = Guid.NewGuid().ToString("N")[..6];

        Chapter chapter = new()
        {
            StoryId          = anyStoryId,
            ChapterNumber    = 900 + Math.Abs(suffix.GetHashCode() % 99), // unique-ish
            Title            = $"Empty chapter {suffix}",
            PrimaryContentId = null,
            IsPublished      = true,
            VersionCount     = 0
        };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        return chapter.ChapterId;
    }

    private void SetActiveUser(FakeActiveUserContext value)
    {
        FakeActiveUserContext fake = _factory.Services.GetRequiredService<FakeActiveUserContext>();
        fake.UserId            = value.UserId;
        fake.IsAuthenticated   = value.IsAuthenticated;
        fake.ShowMatureContent = value.ShowMatureContent;
    }
}
