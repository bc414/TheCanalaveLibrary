using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ServerStoryReadService.GetRandomBatchAsync"/> and the
/// <see cref="TagIncludeMode.Or"/> branch of <see cref="ServerStoryReadService.GetListingsAsync"/>
/// (WU28, spec §5.28 random batch + §5.27 tag include-mode). Exercises real Postgres
/// (Testcontainers).
///
/// <b>Per-test seeding plan:</b>
/// <list type="bullet">
///   <item>All tests that need a viewer set it via <c>_fake</c> in <see cref="InitializeAsync"/>;
///   no user is shared across test methods except <c>_testUserId</c> (seeded once in
///   <see cref="InitializeAsync"/> for interaction tests).</item>
///   <item>Tag ids <c>_tagA</c> / <c>_tagB</c> are seeded once in
///   <see cref="InitializeAsync"/> and reused.</item>
///   <item>Story ids are seeded per-test with Guid-suffixed titles.</item>
/// </list>
/// Tier: Integration (Testcontainers Postgres, real EF, real Respawn).
/// </summary>
[Collection("Postgres")]
public class RandomBatchTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private FakeActiveUserContext _fake = null!;
    private int _testUserId;
    private int _tagA;
    private int _tagB;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _fake = Factory.Services.GetRequiredService<FakeActiveUserContext>();
        _testUserId = await SeedUserAsync();
        (_tagA, _tagB) = await SeedTagsAsync();
    }

    // ── GetRandomBatchAsync — basic behaviour ─────────────────────────────────────────

    [Fact]
    public async Task GetRandomBatchAsync_ReturnsBatchSizedSubset()
    {
        SetViewer(authenticated: false);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        for (int i = 0; i < 5; i++)
            await SeedStoryAsync($"Batch-{suffix}-{i}", Rating.T, []);

        StoryListingDto[] result = await InvokeAsync(svc =>
            svc.GetRandomBatchAsync(new StoryFilterDto(), batchSize: 3));

        result.Should().HaveCountLessThanOrEqualTo(3,
            "GetRandomBatchAsync must not return more than batchSize stories");
    }

    [Fact]
    public async Task GetRandomBatchAsync_ReturnsOnlyValidSetStories_AfterTagFilter()
    {
        SetViewer(authenticated: false);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int withTag    = await SeedStoryAsync($"WithTag-{suffix}", Rating.T, [_tagA]);
        int withoutTag = await SeedStoryAsync($"WithoutTag-{suffix}", Rating.T, []);

        // Include filter: only stories with _tagA should be returned.
        StoryListingDto[] result = await InvokeAsync(svc =>
            svc.GetRandomBatchAsync(
                new StoryFilterDto { IncludedTagIds = [_tagA] },
                batchSize: 100));

        int[] ids = result.Select(r => r.StoryId).ToArray();
        ids.Should().Contain(withTag,
            "story with the required tag is in the post-filter valid set");
        ids.Should().NotContain(withoutTag,
            "story without the required tag is excluded by the tag include filter");
    }

    [Fact]
    public async Task GetRandomBatchAsync_RespectsInteractionExclusion_AuthenticatedViewer()
    {
        SetViewer(authenticated: true);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int ignored    = await SeedStoryAsync($"Ignored-{suffix}", Rating.T, []);
        int notIgnored = await SeedStoryAsync($"NotIgnored-{suffix}", Rating.T, []);

        await SeedInteractionAsync(_testUserId, ignored, isIgnored: true);

        StoryListingDto[] result = await InvokeAsync(svc =>
            svc.GetRandomBatchAsync(
                new StoryFilterDto { ExcludedInteractions = [UserStoryInteractionTypeEnum.Ignore] },
                batchSize: 100));

        int[] ids = result.Select(r => r.StoryId).ToArray();
        ids.Should().NotContain(ignored,
            "viewer's ignored story must be excluded by the interaction filter");
        ids.Should().Contain(notIgnored,
            "story not ignored by the viewer must remain in the batch");
    }

    [Fact]
    public async Task GetRandomBatchAsync_ContentRatingFilter_HidesMatureFromRestrictedViewer()
    {
        // Viewer who cannot see Mature content.
        _fake.IsAuthenticated = true;
        _fake.UserId = _testUserId;
        _fake.ShowMatureContent = false;

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int matureId = await SeedStoryAsync($"Mature-{suffix}", Rating.M, []);
        int teenId   = await SeedStoryAsync($"Teen-{suffix}", Rating.T, []);

        StoryListingDto[] result = await InvokeAsync(svc =>
            svc.GetRandomBatchAsync(new StoryFilterDto(), batchSize: 100));

        int[] ids = result.Select(r => r.StoryId).ToArray();
        ids.Should().NotContain(matureId,
            "content-rating global filter drops Mature from a viewer who cannot see M content");
        ids.Should().Contain(teenId, "Teen-rated story is visible to the restricted viewer");
    }

    // ── GetListingsAsync — OR-include mode ───────────────────────────────────────────

    [Fact]
    public async Task GetListingsAsync_OrInclude_ReturnsStoriesMatchingAnyIncludedTag()
    {
        SetViewer(authenticated: false);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int onlyA    = await SeedStoryAsync($"OnlyA-{suffix}", Rating.T, [_tagA]);
        int onlyB    = await SeedStoryAsync($"OnlyB-{suffix}", Rating.T, [_tagB]);
        int both     = await SeedStoryAsync($"Both-{suffix}",  Rating.T, [_tagA, _tagB]);
        int neither  = await SeedStoryAsync($"Neither-{suffix}", Rating.T, []);

        StoryListingDto[] result = await GetAllAsync(new StoryFilterDto
        {
            IncludedTagIds = [_tagA, _tagB],
            IncludeMode = TagIncludeMode.Or
        });

        int[] ids = result.Select(r => r.StoryId).ToArray();
        ids.Should().Contain(onlyA,   "OR: story with only tagA satisfies the OR condition");
        ids.Should().Contain(onlyB,   "OR: story with only tagB satisfies the OR condition");
        ids.Should().Contain(both,    "OR: story with both tags satisfies the OR condition");
        ids.Should().NotContain(neither, "OR: story with neither tag does not satisfy");
    }

    [Fact]
    public async Task GetListingsAsync_AndInclude_StillRequiresAllTags()
    {
        SetViewer(authenticated: false);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int onlyA   = await SeedStoryAsync($"AndOnlyA-{suffix}", Rating.T, [_tagA]);
        int onlyB   = await SeedStoryAsync($"AndOnlyB-{suffix}", Rating.T, [_tagB]);
        int both    = await SeedStoryAsync($"AndBoth-{suffix}",  Rating.T, [_tagA, _tagB]);

        StoryListingDto[] result = await GetAllAsync(new StoryFilterDto
        {
            IncludedTagIds = [_tagA, _tagB],
            IncludeMode = TagIncludeMode.And   // explicit AND (same as default)
        });

        int[] ids = result.Select(r => r.StoryId).ToArray();
        ids.Should().Contain(both,   "AND: story with both tags satisfies");
        ids.Should().NotContain(onlyA, "AND: story with only tagA does not satisfy (must have ALL)");
        ids.Should().NotContain(onlyB, "AND: story with only tagB does not satisfy (must have ALL)");
    }

    // ── Mutation sanity ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SanityCheck_OrInclude_WouldIncludeStoryAbsentFromAndResult()
    {
        // Confirms the OR and AND modes produce different results for the same include ids,
        // so the OR test cannot pass by accident if the OR branch is not reached.
        SetViewer(authenticated: false);

        string suffix = Guid.NewGuid().ToString("N")[..8];
        int onlyA = await SeedStoryAsync($"SanityA-{suffix}", Rating.T, [_tagA]);

        // AND must exclude onlyA (missing tagB).
        StoryListingDto[] andResult = await GetAllAsync(new StoryFilterDto
        {
            IncludedTagIds = [_tagA, _tagB],
            IncludeMode = TagIncludeMode.And
        });
        andResult.Select(r => r.StoryId).Should().NotContain(onlyA,
            "AND baseline: story with only tagA is absent; if present, OR test cannot be meaningful");

        // OR must include onlyA.
        StoryListingDto[] orResult = await GetAllAsync(new StoryFilterDto
        {
            IncludedTagIds = [_tagA, _tagB],
            IncludeMode = TagIncludeMode.Or
        });
        orResult.Select(r => r.StoryId).Should().Contain(onlyA,
            "OR mode must include the story that AND excludes; confirms the branch is reached");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    private void SetViewer(bool authenticated, bool showMature = true)
    {
        _fake.IsAuthenticated = authenticated;
        _fake.UserId = authenticated ? _testUserId : null;
        _fake.ShowMatureContent = showMature;
    }

    private async Task<StoryListingDto[]> GetAllAsync(StoryFilterDto filter) =>
        (await InvokeListingsAsync(svc => svc.GetListingsAsync(filter with { Page = 1, PageSize = 10_000 }))).Items;

    private async Task<T> InvokeAsync<T>(Func<IStoryReadService, Task<T>> call)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryReadService svc = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        return await call(svc);
    }

    private async Task<T> InvokeListingsAsync<T>(Func<IStoryReadService, Task<T>> call) =>
        await InvokeAsync(call);

    private async Task<(int TagA, int TagB)> SeedTagsAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string suffix = Guid.NewGuid().ToString("N")[..8];
        Tag tagA = new() { TagName = $"RandTagA-{suffix}", TagTypeId = TagTypeEnum.Genre };
        Tag tagB = new() { TagName = $"RandTagB-{suffix}", TagTypeId = TagTypeEnum.Genre };
        db.Tags.AddRange(tagA, tagB);
        await db.SaveChangesAsync();
        return (tagA.TagId, tagB.TagId);
    }

    private async Task<int> SeedStoryAsync(string title, Rating rating, IReadOnlyList<int> tagIds)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
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

    private async Task SeedInteractionAsync(int userId, int storyId,
        bool isIgnored = false, bool isFavorite = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.UserStoryInteractions.Add(new UserStoryInteraction
        {
            UserId = userId,
            StoryId = storyId,
            HasStarted = false,
            IsCompleted = false,
            IsFavorite = isFavorite,
            IsHiddenFavorite = false,
            IsFollowed = false,
            IsReadItLater = false,
            IsIgnored = isIgnored
        });
        await db.SaveChangesAsync();
    }
}
