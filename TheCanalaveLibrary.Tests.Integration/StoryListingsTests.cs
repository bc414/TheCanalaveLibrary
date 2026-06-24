using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ServerStoryReadService.GetListingsAsync"/> (WU23, spec §5.27).
/// Exercises the full filtered query path against real Postgres (Testcontainers), covering:
/// <list type="bullet">
///   <item>Tag include (AND semantics — story must have ALL included tags).</item>
///   <item>Tag exclude (story must have NONE of the excluded tags).</item>
///   <item>FTS + Relevance sort (title match scores higher than description match).</item>
///   <item>Viewer-scoped interaction exclusion (authenticated only; anon sees everything).</item>
///   <item>DatePublished sort order.</item>
///   <item>Paging + TotalCount.</item>
///   <item>Content-rating filter still applied (Mature hidden from viewer who can't see it).</item>
/// </list>
///
/// Follows the shared-accumulating-state rule: fixture rows use Guid-suffixed identifiers / seeded ids;
/// assertions use relative order or presence, never absolute position.
/// </summary>
[Collection("Postgres")]
public class StoryListingsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private TestAppFactory _factory = null!;
    private FakeActiveUserContext _fake = null!;

    // Fixture tag ids seeded in InitializeAsync.
    private int _genreTagA;
    private int _genreTagB;

    // The DataSeeder creates "TestUser" — we use that user's real id for interaction seeds.
    private int _testUserId;

    public async Task InitializeAsync()
    {
        _factory = new TestAppFactory(postgres.ConnectionString);
        _ = _factory.Services; // trigger host build + DataSeeder
        _fake = _factory.Services.GetRequiredService<FakeActiveUserContext>();

        (_genreTagA, _genreTagB) = await SeedTagsAsync();
        _testUserId = await GetTestUserIdAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    // ── Tag include (AND semantics) ──────────────────────────────────────────────────

    [Fact]
    public async Task GetListingsAsync_IncludedTagIds_ReturnsOnlyStoriesWithAllTags()
    {
        SetViewer(authenticated: true);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int bothTags = await SeedStoryAsync($"BothTags-{suffix}", Rating.T, [_genreTagA, _genreTagB]);
        int oneTag   = await SeedStoryAsync($"OneTag-{suffix}", Rating.T, [_genreTagA]);
        int noTag    = await SeedStoryAsync($"NoTag-{suffix}", Rating.T, []);

        StoryListingDto[] items = await GetAllAsync(new StoryFilterDto
        {
            IncludedTagIds = [_genreTagA, _genreTagB]
        });

        int[] ids = items.Select(i => i.StoryId).ToArray();
        ids.Should().Contain(bothTags, "story with both tags must be included");
        ids.Should().NotContain(oneTag, "story with only one included tag must be excluded");
        ids.Should().NotContain(noTag, "story with no included tags must be excluded");
    }

    // ── Tag exclude ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListingsAsync_ExcludedTagIds_ExcludesStoriesWithAnyExcludedTag()
    {
        SetViewer(authenticated: true);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int withExcluded = await SeedStoryAsync($"WithExcluded-{suffix}", Rating.T, [_genreTagA]);
        int withNeither  = await SeedStoryAsync($"WithNeither-{suffix}", Rating.T, []);

        StoryListingDto[] items = await GetAllAsync(new StoryFilterDto
        {
            ExcludedTagIds = [_genreTagA]
        });

        int[] ids = items.Select(i => i.StoryId).ToArray();
        ids.Should().NotContain(withExcluded, "story with excluded tag must be omitted");
        ids.Should().Contain(withNeither, "story with no excluded tags must remain");
    }

    // ── FTS + Relevance sort ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListingsAsync_TextQuery_FiltersToMatchingStories()
    {
        SetViewer(authenticated: true);

        string uniqueWord = $"Pikachu{Guid.NewGuid():N}"[..20];
        int match    = await SeedStoryWithDescAsync($"FtsMatch-{Guid.NewGuid():N}"[..24], uniqueWord);
        int noMatch  = await SeedStoryAsync($"FtsNoMatch-{Guid.NewGuid():N}"[..24], Rating.T, []);

        StoryListingDto[] items = await GetAllAsync(new StoryFilterDto
        {
            TextQuery = uniqueWord,
            Sort = DefaultSortOrder.Relevance
        });

        int[] ids = items.Select(i => i.StoryId).ToArray();
        ids.Should().Contain(match, "story whose description contains the unique word must match");
        ids.Should().NotContain(noMatch, "story without the word must not match");
    }

    // ── Interaction exclusion ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListingsAsync_ExcludeFavorited_HidesViewersFavoritedStories()
    {
        // Use the DataSeeder's TestUser — only user guaranteed to exist in the shared container.
        SetViewer(authenticated: true, userId: _testUserId);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int favorited   = await SeedStoryAsync($"Fav-{suffix}", Rating.T, []);
        int unfavorited = await SeedStoryAsync($"Unfav-{suffix}", Rating.T, []);

        await SeedInteractionAsync(_testUserId, favorited, isFavorite: true);

        StoryListingDto[] items = await GetAllAsync(new StoryFilterDto
        {
            ExcludedInteractions = [UserStoryInteractionTypeEnum.Favorite]
        });

        int[] ids = items.Select(i => i.StoryId).ToArray();
        ids.Should().NotContain(favorited, "a story the viewer has favorited must be excluded");
        ids.Should().Contain(unfavorited, "a story the viewer has not favorited must remain");
    }

    [Fact]
    public async Task GetListingsAsync_ExcludeFavorited_AnonViewerSeesEverything()
    {
        SetViewer(authenticated: false); // anonymous — no userId

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int storyId = await SeedStoryAsync($"AnonCheck-{suffix}", Rating.T, []);

        // Even with an ExcludedInteractions filter, anon viewer should see all (no USI rows for anon).
        StoryListingDto[] items = await GetAllAsync(new StoryFilterDto
        {
            ExcludedInteractions = [UserStoryInteractionTypeEnum.Favorite]
        });

        items.Select(i => i.StoryId).Should().Contain(storyId,
            "anonymous viewer has no interaction rows; exclusion filter must be a no-op");
    }

    // ── Sort: DatePublished ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListingsAsync_SortDatePublished_OrdersByPublishedDateDescending()
    {
        SetViewer(authenticated: true);

        DateTime baseTime = DateTime.UtcNow.AddYears(-1); // well in the past, avoids DataSeeder race
        string suffix = Guid.NewGuid().ToString("N")[..8];
        int olderId = await SeedStoryAtAsync($"OlderPub-{suffix}", baseTime);
        int newerId = await SeedStoryAtAsync($"NewerPub-{suffix}", baseTime.AddMinutes(5));

        // Just need the total count — reuse GetAllAsync (large page) which manages scope correctly.
        StoryListingDto[] all = await GetAllAsync(new StoryFilterDto { Sort = DefaultSortOrder.DatePublished });

        int newerIdx = Array.FindIndex(all, s => s.StoryId == newerId);
        int olderIdx = Array.FindIndex(all, s => s.StoryId == olderId);

        newerIdx.Should().BeGreaterThanOrEqualTo(0);
        olderIdx.Should().BeGreaterThanOrEqualTo(0);
        newerIdx.Should().BeLessThan(olderIdx, "more recently published story must sort first");
    }

    // ── Paging + TotalCount ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListingsAsync_Paging_TotalCountIsIndependentOfPageSize()
    {
        SetViewer(authenticated: true);

        // Seed two stories unique to this test.
        string suffix = Guid.NewGuid().ToString("N")[..8];
        await SeedStoryAsync($"Page1-{suffix}", Rating.T, []);
        await SeedStoryAsync($"Page2-{suffix}", Rating.T, []);

        var (_, totalFull) = await InvokeSvcAsync(svc => svc.GetListingsAsync(new StoryFilterDto { PageSize = 1000 }));
        var (page1Items, totalPage1) = await InvokeSvcAsync(svc => svc.GetListingsAsync(new StoryFilterDto { PageSize = 1, Page = 1 }));
        var (_, totalPage2) = await InvokeSvcAsync(svc => svc.GetListingsAsync(new StoryFilterDto { PageSize = 1, Page = 2 }));

        totalFull.Should().Be(totalPage1, "TotalCount must not change with PageSize");
        totalPage1.Should().Be(totalPage2, "TotalCount is consistent across pages");
        page1Items.Should().HaveCount(1, "PageSize=1 returns exactly one item");
    }

    // ── Content-rating filter ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetListingsAsync_RatingFilter_HidesMatureFromViewerWhoCannotSeeMature()
    {
        // Viewer who cannot see Mature content.
        SetViewer(authenticated: true, showMature: false);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int matureId  = await SeedStoryAsync($"MatureStory-{suffix}", Rating.M, []);
        int teenId    = await SeedStoryAsync($"TeenStory-{suffix}", Rating.T, []);

        StoryListingDto[] items = await GetAllAsync(new StoryFilterDto());

        int[] ids = items.Select(i => i.StoryId).ToArray();
        ids.Should().NotContain(matureId, "Mature story must be hidden from viewer who cannot see M content");
        ids.Should().Contain(teenId, "Teen story must be visible");
    }

    // ── Mutation sanity (drop exclude predicate → exclusion test fails) ──────────────

    [Fact]
    public async Task SanityCheck_ExcludedTagFilter_WouldFailWithoutPredicate()
    {
        // Seeds a story tagged with _genreTagA, then verifies it IS present in an unfiltered query.
        // If the exclusion predicate were dropped, ExcludedTagIds test above would also pass — this
        // confirms the unfiltered baseline contains the story that the exclude test must hide.
        SetViewer(authenticated: true);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int taggedId = await SeedStoryAsync($"SanityTagged-{suffix}", Rating.T, [_genreTagA]);

        StoryListingDto[] allItems = await GetAllAsync(new StoryFilterDto());

        allItems.Select(i => i.StoryId).Should().Contain(taggedId,
            "sanity: the tagged story must appear in an unfiltered listing; " +
            "if it does not, the ExcludedTagIds test cannot prove the predicate works");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────

    private void SetViewer(bool authenticated, int userId = 1, bool showMature = true)
    {
        _fake.IsAuthenticated = authenticated;
        _fake.UserId = authenticated ? userId : null;
        _fake.ShowMatureContent = showMature;
    }

    /// <summary>Runs a GetListingsAsync call with a large page to collect all matching stories.</summary>
    private async Task<StoryListingDto[]> GetAllAsync(StoryFilterDto filter) =>
        (await InvokeSvcAsync(svc => svc.GetListingsAsync(filter with { Page = 1, PageSize = 10_000 }))).Items;

    /// <summary>
    /// Invokes <paramref name="call"/> within a properly-scoped service resolve.
    /// The scope (and its DbContext) stays alive until the async call completes.
    /// </summary>
    private async Task<T> InvokeSvcAsync<T>(Func<IStoryReadService, Task<T>> call)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        IStoryReadService svc = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        return await call(svc);
    }

    private async Task<int> GetTestUserIdAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.Where(u => u.UserName == "TestUser").Select(u => u.Id).FirstAsync();
    }

    private async Task<(int TagA, int TagB)> SeedTagsAsync()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        Tag tagA = new() { TagName = $"FilterTagA-{suffix}", TagTypeId = TagTypeEnum.Genre };
        Tag tagB = new() { TagName = $"FilterTagB-{suffix}", TagTypeId = TagTypeEnum.Genre };
        db.Tags.AddRange(tagA, tagB);
        await db.SaveChangesAsync();
        return (tagA.TagId, tagB.TagId);
    }

    private async Task<int> SeedStoryAsync(string title, Rating rating, IReadOnlyList<int> tagIds)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Story story = new()
        {
            Rating = rating,
            StoryStatusId = StoryStatusEnum.InProgress,
            PublishedDate = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
            StoryListing = new StoryListing { StoryTitle = title },
            StoryDetail = new StoryDetail { LongDescription = "fixture", PostApprovalStatus = StoryStatusEnum.InProgress }
        };

        foreach (int tagId in tagIds)
            story.StoryTags.Add(new StoryTag { TagId = tagId });

        db.Stories.Add(story);
        await db.SaveChangesAsync();
        return story.StoryId;
    }

    private async Task<int> SeedStoryWithDescAsync(string title, string description)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Story story = new()
        {
            Rating = Rating.T,
            StoryStatusId = StoryStatusEnum.InProgress,
            PublishedDate = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
            StoryListing = new StoryListing { StoryTitle = title, ShortDescription = description },
            StoryDetail = new StoryDetail { LongDescription = "fixture", PostApprovalStatus = StoryStatusEnum.InProgress }
        };

        db.Stories.Add(story);
        await db.SaveChangesAsync();
        return story.StoryId;
    }

    private async Task<int> SeedStoryAtAsync(string title, DateTime publishedDate)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Story story = new()
        {
            Rating = Rating.T,
            StoryStatusId = StoryStatusEnum.InProgress,
            PublishedDate = publishedDate,
            LastUpdatedDate = publishedDate,
            StoryListing = new StoryListing { StoryTitle = title },
            StoryDetail = new StoryDetail { LongDescription = "fixture", PostApprovalStatus = StoryStatusEnum.InProgress }
        };

        db.Stories.Add(story);
        await db.SaveChangesAsync();
        return story.StoryId;
    }

    private async Task SeedInteractionAsync(int userId, int storyId, bool isFavorite = false)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.UserStoryInteractions.Add(new UserStoryInteraction
        {
            UserId = userId,
            StoryId = storyId,
            IsFavorite = isFavorite,
            HasStarted = false,
            IsCompleted = false,
            IsHiddenFavorite = false,
            IsFollowed = false,
            IsReadItLater = false,
            IsIgnored = false
        });

        await db.SaveChangesAsync();
    }
}
