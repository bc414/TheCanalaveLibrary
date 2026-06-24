using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IBlogPostWriteService"/> and <see cref="IBlogPostReadService"/>
/// (WU31). Covers: create stamps AuthorId (client value ignored); sanitize-on-save (script stripped);
/// author-only update and delete gates (non-owner → UnauthorizedAccessException); anonymous guard
/// (all mutations → InvalidOperationException); toggle-like round-trip (LikeCount, BlogPostLike row,
/// per-viewer IsLiked); BlogPostsWritten increment via UserStats; content-rating filter hides mature
/// posts from non-mature viewer; draft visibility (GetByIdAsync returns null to non-authors).
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class BlogPostWriteServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _otherUserId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId     = await SeedUserAsync();
        _otherUserId  = await SeedUserAsync();
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: false));
    }

    // ── CreateProfileBlogPostAsync ────────────────────────────────────────────────

    [Fact]
    public async Task Create_StampsAuthorIdFromActiveUser_NotFromDto()
    {
        // The DTO has no AuthorId field — it is always server-stamped.
        int id = await CreatePostAsync();

        ProfileBlogPost? post = await LoadPostAsync(id);
        post.Should().NotBeNull();
        post!.AuthorId.Should().Be(_authorId);
    }

    [Fact]
    public async Task Create_SanitizesScriptTagOnSave()
    {
        int id = await CreatePostAsync(content: "<p>Hello</p><script>alert('xss')</script>");

        ProfileBlogPost? post = await LoadPostAsync(id);
        post!.Content.Should().NotContain("<script>");
        post.Content.Should().Contain("Hello");
    }

    [Fact]
    public async Task Create_IsPublishedFalseByDefault()
    {
        int id = await CreatePostAsync();
        ProfileBlogPost? post = await LoadPostAsync(id);
        post!.IsPublished.Should().BeFalse();
    }

    [Fact]
    public async Task Create_AnonymousViewer_ThrowsInvalidOperation()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        Func<Task> act = () => CreatePostAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── UserStats.BlogPostsWritten ─────────────────────────────────────────────────

    [Fact]
    public async Task Create_IncrementsUserStatsBlogPostsWritten()
    {
        // Ensure a UserStat row exists (created by seeding path or SeedUserAsync extension).
        await EnsureUserStatRowAsync(_authorId);

        int before = await GetBlogPostsWrittenAsync(_authorId);

        await CreatePostAsync();

        int after = await GetBlogPostsWrittenAsync(_authorId);
        after.Should().Be(before + 1);
    }

    // ── UpdateBlogPostAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ByAuthor_AppliesChanges()
    {
        int id = await CreatePostAsync(title: "Original Title");

        await CallUpdateAsync(id, title: "Updated Title");

        ProfileBlogPost? post = await LoadPostAsync(id);
        post!.Title.Should().Be("Updated Title");
    }

    [Fact]
    public async Task Update_ByNonOwner_ThrowsUnauthorized()
    {
        int id = await CreatePostAsync();

        SetActiveUser(_otherUserId);
        Func<Task> act = () => CallUpdateAsync(id);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Update_AnonymousViewer_ThrowsInvalidOperation()
    {
        int id = await CreatePostAsync();

        SetActiveUser(FakeActiveUserContext.Anonymous());
        Func<Task> act = () => CallUpdateAsync(id);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── DeleteBlogPostAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_ByAuthor_RemovesRow()
    {
        int id = await CreatePostAsync();

        await CallDeleteAsync(id);

        (await LoadPostAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task Delete_ByNonOwner_ThrowsUnauthorized()
    {
        int id = await CreatePostAsync();

        SetActiveUser(_otherUserId);
        Func<Task> act = () => CallDeleteAsync(id);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Delete_AnonymousViewer_ThrowsInvalidOperation()
    {
        int id = await CreatePostAsync();

        SetActiveUser(FakeActiveUserContext.Anonymous());
        Func<Task> act = () => CallDeleteAsync(id);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── ToggleLikeAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ToggleLike_FirstLike_AddsRowAndIncrementsCount()
    {
        int id = await CreatePostAsync();
        // Switch to the other user to like the author's post.
        SetActiveUser(_otherUserId);

        BlogPostLikeResultDto result = await CallToggleLikeAsync(id);

        result.IsLiked.Should().BeTrue();
        result.LikeCount.Should().Be(1);

        ProfileBlogPost? post = await LoadPostAsync(id);
        post!.LikeCount.Should().Be(1);
    }

    [Fact]
    public async Task ToggleLike_SecondLike_RemovesRowAndDecrementsCount()
    {
        int id = await CreatePostAsync();
        SetActiveUser(_otherUserId);

        await CallToggleLikeAsync(id); // like
        BlogPostLikeResultDto result = await CallToggleLikeAsync(id); // unlike

        result.IsLiked.Should().BeFalse();
        result.LikeCount.Should().Be(0);
    }

    [Fact]
    public async Task ToggleLike_Anonymous_ThrowsInvalidOperation()
    {
        int id = await CreatePostAsync();
        SetActiveUser(FakeActiveUserContext.Anonymous());
        Func<Task> act = () => CallToggleLikeAsync(id);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Content-rating filter (GetByIdAsync) ────────────────────────────────────

    [Fact]
    public async Task GetById_MaturePost_HiddenFromNonMatureViewer()
    {
        int id = await CreatePostAsync(rating: Rating.M, published: true);

        // Non-mature viewer (different user, ShowMatureContent = false).
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: false));

        BlogPostDto? result = await CallGetByIdAsync(id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetById_MaturePost_VisibleToMatureViewer()
    {
        int id = await CreatePostAsync(rating: Rating.M, published: true);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: true));

        BlogPostDto? result = await CallGetByIdAsync(id);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetById_MaturePost_VisibleToAuthorRegardlessOfMatureSetting()
    {
        int id = await CreatePostAsync(rating: Rating.M, published: true);

        // Author with mature off — still can see own mature post.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: false));

        BlogPostDto? result = await CallGetByIdAsync(id);
        result.Should().NotBeNull();
    }

    // ── Draft visibility ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_Draft_ReturnsNullToNonAuthor()
    {
        int id = await CreatePostAsync(published: false);

        SetActiveUser(_otherUserId);

        BlogPostDto? result = await CallGetByIdAsync(id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetById_Draft_VisibleToAuthor()
    {
        int id = await CreatePostAsync(published: false);

        // Author (same user as _authorId via SetActiveUser).
        BlogPostDto? result = await CallGetByIdAsync(id);
        result.Should().NotBeNull();
    }

    // ── IsLikedByCurrentUser (GetByIdAsync per-viewer) ───────────────────────────

    [Fact]
    public async Task GetById_IsLikedByCurrentUser_TrueAfterLike()
    {
        int id = await CreatePostAsync(published: true);
        SetActiveUser(_otherUserId);

        await CallToggleLikeAsync(id);

        BlogPostDto? post = await CallGetByIdAsync(id);
        post!.IsLikedByCurrentUser.Should().BeTrue();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<int> CreatePostAsync(
        string title   = "Test Blog Post",
        string content = "<p>Content</p>",
        Rating rating  = Rating.E,
        bool published = false)
    {
        // Publish by creating then updating (create always makes a draft).
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostWriteService svc = scope.ServiceProvider.GetRequiredService<IBlogPostWriteService>();

        int id = await svc.CreateProfileBlogPostAsync(new CreateProfileBlogPostDto
        {
            Title       = title,
            Content     = content,
            Rating      = rating,
            HasSpoilers = false
        });

        if (published)
        {
            await svc.UpdateBlogPostAsync(new UpdateBlogPostDto
            {
                BlogPostId  = id,
                Title       = title,
                Content     = content,
                Rating      = rating,
                HasSpoilers = false,
                IsPublished = true
            });
        }

        return id;
    }

    // IMPORTANT: these helpers must be `async Task` with `await` inside the `using IServiceScope`
    // block. Returning a Task from a non-async method disposes the scope synchronously (after the
    // return statement) but before the async continuation completes — the service's DbContext is
    // then disposed while a DB round-trip is still in flight, causing ObjectDisposedException.

    private async Task CallUpdateAsync(int blogPostId, string title = "Updated Title", string content = "<p>Updated</p>")
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostWriteService svc = scope.ServiceProvider.GetRequiredService<IBlogPostWriteService>();
        await svc.UpdateBlogPostAsync(new UpdateBlogPostDto
        {
            BlogPostId  = blogPostId,
            Title       = title,
            Content     = content,
            Rating      = Rating.E,
            HasSpoilers = false,
            IsPublished = false
        });
    }

    private async Task CallDeleteAsync(int blogPostId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostWriteService svc = scope.ServiceProvider.GetRequiredService<IBlogPostWriteService>();
        await svc.DeleteBlogPostAsync(blogPostId);
    }

    private async Task<BlogPostLikeResultDto> CallToggleLikeAsync(int blogPostId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostWriteService svc = scope.ServiceProvider.GetRequiredService<IBlogPostWriteService>();
        return await svc.ToggleLikeAsync(blogPostId);
    }

    private async Task<BlogPostDto?> CallGetByIdAsync(int blogPostId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostReadService svc = scope.ServiceProvider.GetRequiredService<IBlogPostReadService>();
        return await svc.GetByIdAsync(blogPostId);
    }

    private async Task<ProfileBlogPost?> LoadPostAsync(int blogPostId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.ProfileBlogPosts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.BlogPostId == blogPostId);
    }

    private async Task EnsureUserStatRowAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!await db.UserStats.AnyAsync(s => s.UserId == userId))
        {
            db.UserStats.Add(new UserStat { UserId = userId });
            await db.SaveChangesAsync();
        }
    }

    private async Task<int> GetBlogPostsWrittenAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserStats
            .Where(s => s.UserId == userId)
            .Select(s => s.BlogPostsWritten)
            .FirstOrDefaultAsync();
    }
}
