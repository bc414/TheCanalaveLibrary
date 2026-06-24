using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// <see cref="ServerStoryReadService.GetRecentListingsAsync"/> orders by <c>LastUpdatedDate DESC</c>
/// over whatever happens to be in the shared "Postgres" collection's database — fixtures from other
/// test classes in this collection, the DataSeeder's own seeded story, and (this matters) leftover
/// rows from an *earlier, separate* `dotnet test` process invocation against the same container, since
/// Testcontainers tears the container down only when the test *process* exits, not between runs within
/// a debugging session. An earlier attempt at this test dated fixture rows "now + 10 years" expecting
/// them to sort to the very top — that still isn't isolation-proof: a stale leftover row from a run a
/// few minutes earlier computes its own "+10 years" from an earlier wall-clock instant, and two
/// relatively-dated fixtures from different runs can land in either order relative to each other.
/// Don't assert on absolute position (top-N) against shared, accumulating state. Instead, fetch enough
/// of the table to be sure both of *this test's own* known ids are present, then assert only their
/// order *relative to each other* — correct regardless of what else, past or present, surrounds them.
/// </summary>
[Collection("Postgres")]
public class RecentListingsTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _viewerUserId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _viewerUserId = await SeedUserAsync();
    }

    [Fact]
    public async Task GetRecentListingsAsync_OrdersByLastUpdatedDateDescending()
    {
        // After reset, DB has only lookup rows — page size == total, so top-N = absolute now.
        SetActiveUser(_viewerUserId);

        DateTime now = DateTime.UtcNow;
        int olderId = await SeedDatedStoryAsync($"Older Story {Guid.NewGuid():N}", now);
        int newerId = await SeedDatedStoryAsync($"Newer Story {Guid.NewGuid():N}", now.AddMinutes(1));

        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryReadService readService = scope.ServiceProvider.GetRequiredService<IStoryReadService>();

        (StoryListingDto[] items, _) = await readService.GetRecentListingsAsync(page: 1, pageSize: 100);

        int newerIndex = Array.FindIndex(items, i => i.StoryId == newerId);
        int olderIndex = Array.FindIndex(items, i => i.StoryId == olderId);

        newerIndex.Should().BeGreaterThanOrEqualTo(0, "the just-seeded newer story must appear in the listing");
        olderIndex.Should().BeGreaterThanOrEqualTo(0, "the just-seeded older story must appear in the listing");
        newerIndex.Should().BeLessThan(olderIndex, "a more recently updated story must sort before an older one");
    }

    /// <summary>
    /// Custom story seed with explicit <paramref name="lastUpdatedDate"/> — needed here because
    /// <see cref="IntegrationTestBase.SeedStoryAsync"/> always uses <c>DateTime.UtcNow</c>.
    /// </summary>
    private async Task<int> SeedDatedStoryAsync(string title, DateTime lastUpdatedDate)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Story story = new()
        {
            Rating = Rating.T,
            StoryStatusId = StoryStatusEnum.InProgress,
            PublishedDate = lastUpdatedDate,
            LastUpdatedDate = lastUpdatedDate,
            StoryListing = new StoryListing { StoryTitle = title, ShortDescription = "fixture" },
            StoryDetail = new StoryDetail { LongDescription = "fixture", PostApprovalStatus = StoryStatusEnum.InProgress }
        };

        db.Stories.Add(story);
        await db.SaveChangesAsync();
        return story.StoryId;
    }
}
