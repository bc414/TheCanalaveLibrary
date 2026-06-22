using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The core invariant WU12 minted: the global <c>"ContentRating"</c> named query filter on
/// <see cref="Story"/> ("mature off ⇒ no trace anywhere", spec §5) must actually translate to SQL
/// against a real Postgres — see testing.md "Integration tests run against real Postgres." Also
/// exercises <c>GetListingsByIdsAsync</c>'s reorder-to-input-order and silent-drop behavior, since both
/// ride on the same filtered query.
/// </summary>
[Collection("Postgres")]
public class ContentRatingFilterTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestAppFactory _factory = null!;
    private int _teenStoryId;
    private int _matureStoryId;

    public async Task InitializeAsync()
    {
        _factory = new TestAppFactory(postgres.ConnectionString);
        // Force the host (and Program.cs's DataSeeder/migration check) to build now, so any DI
        // misconfiguration surfaces here rather than inside the first test assertion.
        _ = _factory.Services;

        (_teenStoryId, _matureStoryId) = await SeedFixtureStoriesAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task AnonymousViewer_SeesOnlyTeenRatedStories()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        StoryListingDto[] listings = await GetListingsAsync([_teenStoryId, _matureStoryId]);

        listings.Select(l => l.StoryId).Should().BeEquivalentTo([_teenStoryId]);
    }

    [Fact]
    public async Task NonMatureUser_SeesOnlyTeenRatedStories()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(userId: 1, showMatureContent: false));

        StoryListingDto[] listings = await GetListingsAsync([_teenStoryId, _matureStoryId]);

        listings.Select(l => l.StoryId).Should().BeEquivalentTo([_teenStoryId]);
    }

    [Fact]
    public async Task MatureEnabledUser_SeesBothRatings()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(userId: 1, showMatureContent: true));

        StoryListingDto[] listings = await GetListingsAsync([_teenStoryId, _matureStoryId]);

        listings.Select(l => l.StoryId).Should().BeEquivalentTo([_teenStoryId, _matureStoryId]);
    }

    [Fact]
    public async Task GetListingsByIdsAsync_ReordersToInputOrder_AndDropsFilteredOrMissingIds()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(userId: 1, showMatureContent: true));
        const int nonExistentId = -999;

        // Deliberately shuffled input order, plus a nonexistent id mixed in — the result must come
        // back reordered to match this exact input order, with the nonexistent id silently dropped
        // (not erred). The rating-based drop is covered separately below.
        StoryListingDto[] listings = await GetListingsAsync([_matureStoryId, nonExistentId, _teenStoryId]);

        listings.Select(l => l.StoryId).Should().Equal(_matureStoryId, _teenStoryId);
    }

    [Fact]
    public async Task GetListingsByIdsAsync_SilentlyDropsMatureStory_WhenCallerCannotSeeIt()
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(userId: 1, showMatureContent: false));

        // Mature id requested first, but the caller can't see it — must be dropped, not erred, and the
        // remaining teen id must still come back.
        StoryListingDto[] listings = await GetListingsAsync([_matureStoryId, _teenStoryId]);

        listings.Select(l => l.StoryId).Should().Equal(_teenStoryId);
    }

    private void SetActiveUser(FakeActiveUserContext value)
    {
        FakeActiveUserContext fake = _factory.Services.GetRequiredService<FakeActiveUserContext>();
        fake.UserId = value.UserId;
        fake.IsAuthenticated = value.IsAuthenticated;
        fake.ShowMatureContent = value.ShowMatureContent;
    }

    private async Task<StoryListingDto[]> GetListingsAsync(IReadOnlyList<int> ids)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IStoryReadService readService = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        return await readService.GetListingsByIdsAsync(ids);
    }

    private async Task<(int TeenStoryId, int MatureStoryId)> SeedFixtureStoriesAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext writeDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Direct EF inserts (Add + SaveChanges), not CreateStoryAsync — the content-rating query
        // filter only applies to SELECT-shaped queries, never to Add/SaveChanges, so this is a clean
        // way to seed both ratings regardless of the active user at insert time.
        string suffix = Guid.NewGuid().ToString("N")[..8];
        Story teenStory = NewStory($"Teen Fixture {suffix}", Rating.T);
        Story matureStory = NewStory($"Mature Fixture {suffix}", Rating.M);

        writeDb.Stories.AddRange(teenStory, matureStory);
        await writeDb.SaveChangesAsync();

        return (teenStory.StoryId, matureStory.StoryId);
    }

    private static Story NewStory(string title, Rating rating) => new()
    {
        Rating = rating,
        StoryStatusId = StoryStatusEnum.InProgress,
        PublishedDate = DateTime.UtcNow,
        LastUpdatedDate = DateTime.UtcNow,
        StoryListing = new StoryListing { StoryTitle = title, ShortDescription = "fixture" },
        StoryDetail = new StoryDetail { LongDescription = "fixture", PostApprovalStatus = StoryStatusEnum.InProgress }
    };
}
