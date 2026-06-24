using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for the bookshelf-tab ID-narrowing surface (WU27):
/// <list type="bullet">
///   <item><see cref="IUserStoryInteractionReadService.GetBookshelfStoryIdsAsync"/> — each tab predicate,
///   user-scoping, anonymous-empty, and invalid-tab guard.</item>
///   <item><see cref="IStoryReadService.GetStoryIdsByAuthorAsync"/> — includes own mature stories
///   (content-rating bypass), excludes other authors.</item>
///   <item><see cref="IStoryReadService.GetListingsAsync"/> with <c>restrictToStoryIds</c> — narrows
///   result set while preserving content-rating filter and existing predicates.</item>
///   <item><see cref="IRecommendationReadService.GetRecommendedStoryIdsAsync"/> and
///   <see cref="IRecommendationReadService.GetHiddenGemStoryIdsAsync"/> — own approved recs,
///   approved-only, hidden-gem narrowing, anonymous-empty.</item>
/// </list>
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class BookshelfStoryIdsTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private FakeActiveUserContext _fake = null!;
    private int _userId;
    private int _otherUserId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _fake = Factory.Services.GetRequiredService<FakeActiveUserContext>();
        _userId = await SeedUserAsync();
        _otherUserId = await SeedUserAsync();
        SetActiveUser(_userId);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // GetBookshelfStoryIdsAsync — USI-backed tabs
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetBookshelfStoryIds_Favorites_ReturnsOnlyFavoritedStories()
    {
        int favStory = await SeedStoryAsync();
        int otherStory = await SeedStoryAsync();
        await SeedUsiAsync(_userId, favStory, isFavorite: true);

        IReadOnlyList<int> ids = await CallGetBookshelfStoryIdsAsync(BookshelfTab.Favorites);

        ids.Should().Contain(favStory).And.NotContain(otherStory);
    }

    [Fact]
    public async Task GetBookshelfStoryIds_ActivelyReading_ReturnsHasStartedNotCompletedNotIgnored()
    {
        int reading = await SeedStoryAsync();
        int completed = await SeedStoryAsync();
        int ignored = await SeedStoryAsync();
        int notStarted = await SeedStoryAsync();

        await SeedUsiAsync(_userId, reading, hasStarted: true);
        await SeedUsiAsync(_userId, completed, hasStarted: true, isCompleted: true);
        await SeedUsiAsync(_userId, ignored, hasStarted: true, isIgnored: true);

        IReadOnlyList<int> ids = await CallGetBookshelfStoryIdsAsync(BookshelfTab.ActivelyReading);

        ids.Should().Contain(reading, "HasStarted && !IsCompleted && !IsIgnored");
        ids.Should().NotContain(completed, "IsCompleted excludes from ActivelyReading");
        ids.Should().NotContain(ignored, "IsIgnored excludes from ActivelyReading");
        ids.Should().NotContain(notStarted, "!HasStarted excludes from ActivelyReading");
    }

    [Fact]
    public async Task GetBookshelfStoryIds_Abandoned_ReturnsIgnoredAndStarted()
    {
        int abandoned = await SeedStoryAsync();
        int justIgnored = await SeedStoryAsync();
        int justStarted = await SeedStoryAsync();

        await SeedUsiAsync(_userId, abandoned, hasStarted: true, isIgnored: true);
        await SeedUsiAsync(_userId, justIgnored, isIgnored: true);
        await SeedUsiAsync(_userId, justStarted, hasStarted: true);

        IReadOnlyList<int> ids = await CallGetBookshelfStoryIdsAsync(BookshelfTab.Abandoned);

        ids.Should().Contain(abandoned, "IsIgnored && HasStarted = Abandoned");
        ids.Should().NotContain(justIgnored, "IsIgnored without HasStarted is not Abandoned");
        ids.Should().NotContain(justStarted, "HasStarted without IsIgnored is not Abandoned");
    }

    [Fact]
    public async Task GetBookshelfStoryIds_ScopedToActiveUser_OtherUserRowsNotReturned()
    {
        int story = await SeedStoryAsync();
        await SeedUsiAsync(_otherUserId, story, isFavorite: true);

        IReadOnlyList<int> ids = await CallGetBookshelfStoryIdsAsync(BookshelfTab.Favorites);

        ids.Should().NotContain(story, "another user's favorite is not in the active user's bookshelf");
    }

    [Fact]
    public async Task GetBookshelfStoryIds_Anonymous_ReturnsEmpty()
    {
        int story = await SeedStoryAsync();
        await SeedUsiAsync(_userId, story, isFavorite: true);
        SetAnonymous();

        IReadOnlyList<int> ids = await CallGetBookshelfStoryIdsAsync(BookshelfTab.Favorites);

        ids.Should().BeEmpty("anonymous users have no bookshelf");
    }

    [Fact]
    public async Task GetBookshelfStoryIds_MyStoriesTab_Throws()
    {
        Func<Task> act = () => CallGetBookshelfStoryIdsAsync(BookshelfTab.MyStories);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>(
            "MyStories is not backed by UserStoryInteraction — caller must route to IStoryReadService");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // GetStoryIdsByAuthorAsync
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetStoryIdsByAuthor_ReturnsOwnStoriesOnly()
    {
        int own = await SeedStoryAsync(authorId: _userId);
        int other = await SeedStoryAsync(authorId: _otherUserId);

        IReadOnlyList<int> ids = await CallGetStoryIdsByAuthorAsync(_userId);

        ids.Should().Contain(own, "own story returned");
        ids.Should().NotContain(other, "another author's story not returned");
    }

    [Fact]
    public async Task GetStoryIdsByAuthor_IncludesMatureStories_IgnoresContentRatingFilter()
    {
        // Viewer cannot see mature content — but must still see their own mature story.
        _fake.ShowMatureContent = false;

        int matureOwn = await SeedStoryAsync(authorId: _userId, rating: Rating.M);

        IReadOnlyList<int> ids = await CallGetStoryIdsByAuthorAsync(_userId);

        ids.Should().Contain(matureOwn,
            "GetStoryIdsByAuthorAsync bypasses the content-rating filter for the author");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // GetListingsAsync with restrictToStoryIds
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetListingsAsync_WithRestrictToStoryIds_IncludesOnlyRestrictedStories()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        int a = await SeedStoryAsync($"Restrict-A-{suffix}");
        int b = await SeedStoryAsync($"Restrict-B-{suffix}");
        int c = await SeedStoryAsync($"Restrict-C-{suffix}");

        (StoryListingDto[] items, _) = await CallGetListingsAsync(new StoryFilterDto(), restrictTo: [a, b]);

        int[] ids = items.Select(i => i.StoryId).ToArray();
        ids.Should().Contain(a).And.Contain(b, "a and b are in the restrict set");
        ids.Should().NotContain(c, "c is outside the restrict set");
    }

    [Fact]
    public async Task GetListingsAsync_WithRestrictToStoryIds_ContentRatingFilterStillApplies()
    {
        _fake.ShowMatureContent = false;

        int mature = await SeedStoryAsync(rating: Rating.M);

        (StoryListingDto[] items, _) = await CallGetListingsAsync(new StoryFilterDto(), restrictTo: [mature]);

        items.Should().NotContain(i => i.StoryId == mature,
            "content-rating filter still excludes mature stories even when in the restrict set");
    }

    [Fact]
    public async Task GetListingsAsync_NullRestrict_BehavesLikeUnrestricted()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        int story = await SeedStoryAsync($"NullRestrict-{suffix}");

        (StoryListingDto[] items, int total) = await CallGetListingsAsync(new StoryFilterDto(), restrictTo: null);

        items.Should().Contain(i => i.StoryId == story, "null restrict returns all stories");
        total.Should().BeGreaterThanOrEqualTo(1);
    }

    // ════════════════════════════════════════════════════════════════════════════
    // GetRecommendedStoryIdsAsync + GetHiddenGemStoryIdsAsync
    // ════════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetRecommendedStoryIds_ReturnsStoriesWithOwnApprovedRecommendation()
    {
        int story = await SeedStoryAsync();
        await SeedRecAsync(recommenderId: _userId, storyId: story, statusId: RecommendationStatusEnum.Approved, isHiddenGem: false);

        IReadOnlyList<int> ids = await CallGetRecommendedStoryIdsAsync();

        ids.Should().Contain(story, "user wrote an approved recommendation for this story");
    }

    [Fact]
    public async Task GetRecommendedStoryIds_ExcludesPendingRecommendations()
    {
        int story = await SeedStoryAsync();
        await SeedRecAsync(recommenderId: _userId, storyId: story, statusId: RecommendationStatusEnum.PendingApproval);

        IReadOnlyList<int> ids = await CallGetRecommendedStoryIdsAsync();

        ids.Should().NotContain(story, "pending recommendations are not shown in bookshelf");
    }

    [Fact]
    public async Task GetRecommendedStoryIds_ExcludesOtherUsersRecommendations()
    {
        int story = await SeedStoryAsync();
        await SeedRecAsync(recommenderId: _otherUserId, storyId: story, statusId: RecommendationStatusEnum.Approved);

        IReadOnlyList<int> ids = await CallGetRecommendedStoryIdsAsync();

        ids.Should().NotContain(story, "recommendations by another user are not in the active user's bookshelf");
    }

    [Fact]
    public async Task GetRecommendedStoryIds_Anonymous_ReturnsEmpty()
    {
        int story = await SeedStoryAsync();
        await SeedRecAsync(recommenderId: _userId, storyId: story, statusId: RecommendationStatusEnum.Approved);
        SetAnonymous();

        IReadOnlyList<int> ids = await CallGetRecommendedStoryIdsAsync();

        ids.Should().BeEmpty("anonymous users have no bookshelf");
    }

    [Fact]
    public async Task GetHiddenGemStoryIds_ReturnsOnlyHiddenGemRecommendations()
    {
        int gemStory = await SeedStoryAsync();
        int plainStory = await SeedStoryAsync();
        await SeedRecAsync(recommenderId: _userId, storyId: gemStory, statusId: RecommendationStatusEnum.Approved, isHiddenGem: true);
        await SeedRecAsync(recommenderId: _userId, storyId: plainStory, statusId: RecommendationStatusEnum.Approved, isHiddenGem: false);

        IReadOnlyList<int> ids = await CallGetHiddenGemStoryIdsAsync();

        ids.Should().Contain(gemStory, "IsHiddenGem = true appears in Hidden Gems bookshelf");
        ids.Should().NotContain(plainStory, "IsHiddenGem = false does not appear");
    }

    // ════════════════════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════════════════════

    private new void SetActiveUser(int userId)
    {
        // Override to also set ShowMatureContent = true — bookshelf tests need to see all ratings.
        _fake.UserId = userId;
        _fake.IsAuthenticated = true;
        _fake.ShowMatureContent = true;
    }

    private void SetAnonymous()
    {
        _fake.UserId = null;
        _fake.IsAuthenticated = false;
    }

    private async Task<int> SeedStoryAsync(
        string? title = null, int? authorId = null, Rating rating = Rating.E)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        Story story = new()
        {
            AuthorId = authorId,
            Rating = rating,
            StoryStatusId = StoryStatusEnum.InProgress,
            PublishedDate = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
            StoryListing = new StoryListing
                { StoryTitle = title ?? $"BookshelfTest-{suffix}", ShortDescription = "test" },
            StoryDetail = new StoryDetail
                { LongDescription = "test", PostApprovalStatus = StoryStatusEnum.InProgress }
        };
        db.Stories.Add(story);
        await db.SaveChangesAsync();
        return story.StoryId;
    }

    private async Task SeedUsiAsync(
        int userId, int storyId,
        bool isFavorite = false, bool hasStarted = false,
        bool isCompleted = false, bool isIgnored = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserStoryInteraction? existing = await db.UserStoryInteractions
            .FirstOrDefaultAsync(i => i.UserId == userId && i.StoryId == storyId);
        if (existing is not null)
        {
            existing.IsFavorite = isFavorite;
            existing.HasStarted = hasStarted;
            existing.IsCompleted = isCompleted;
            existing.IsIgnored = isIgnored;
        }
        else
        {
            db.UserStoryInteractions.Add(new UserStoryInteraction
            {
                UserId = userId, StoryId = storyId,
                IsFavorite = isFavorite, HasStarted = hasStarted,
                IsCompleted = isCompleted, IsIgnored = isIgnored
            });
        }
        await db.SaveChangesAsync();
    }

    private async Task<int> SeedRecAsync(
        int? recommenderId, int storyId, RecommendationStatusEnum statusId, bool isHiddenGem = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Recommendation rec = new()
        {
            StoryId = storyId,
            RecommenderId = recommenderId,
            StatusId = (short)statusId,
            IsHiddenGem = isHiddenGem,
            DatePosted = DateTime.UtcNow,
            RecommendationDetail = new RecommendationDetail { Text = new string('x', 500) }
        };
        db.Recommendations.Add(rec);
        await db.SaveChangesAsync();
        return rec.RecommendationId;
    }

    private async Task<IReadOnlyList<int>> CallGetBookshelfStoryIdsAsync(BookshelfTab tab)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<IUserStoryInteractionReadService>()
            .GetBookshelfStoryIdsAsync(tab);
    }

    private async Task<IReadOnlyList<int>> CallGetStoryIdsByAuthorAsync(int authorId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<IStoryReadService>()
            .GetStoryIdsByAuthorAsync(authorId);
    }

    private async Task<(StoryListingDto[] Items, int TotalCount)> CallGetListingsAsync(
        StoryFilterDto filter, IReadOnlyCollection<int>? restrictTo)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<IStoryReadService>()
            .GetListingsAsync(filter, restrictTo);
    }

    private async Task<IReadOnlyList<int>> CallGetRecommendedStoryIdsAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<IRecommendationReadService>()
            .GetRecommendedStoryIdsAsync();
    }

    private async Task<IReadOnlyList<int>> CallGetHiddenGemStoryIdsAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider
            .GetRequiredService<IRecommendationReadService>()
            .GetHiddenGemStoryIdsAsync();
    }
}
