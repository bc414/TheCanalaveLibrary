using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ServerTreeSearchReadService.SearchAsync"/> (WU44) — the
/// Automatic Tree Search UI composition: Source (raw-reached rCTE) × Filter (<c>ApplyFilters</c>
/// via <see cref="IStoryReadService.FilterCandidateIdsAsync"/>) × Sort (Random/ByDegree). See
/// `audit/Discovery.md` Feature 59 and `layer2-services.md` "Tree Search — Automatic Tab
/// Composition (WU44)" for the design this proves.
///
/// <b>Per-test seeding plan:</b> each test builds its own small favorite-graph via the base
/// helpers + direct <c>ApplicationDbContext</c> rows (mirrors <c>DiscoveryMartTests</c>), then
/// triggers <see cref="DiscoveryMartRebuilder"/> directly (the hosted worker is removed by
/// TestAppFactory) before calling <see cref="ITreeSearchReadService.SearchAsync"/>.
/// Tier: Integration (Testcontainers Postgres, real EF, real Respawn).
/// </summary>
[Collection("Postgres")]
public sealed class TreeSearchComposeTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    // ── Seeding helpers (mirrors DiscoveryMartTests / RandomBatchTests) ───────────────────────

    private async Task FavoriteAsync(int userId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStoryInteractions.Add(new UserStoryInteraction { UserId = userId, StoryId = storyId, IsFavorite = true });
        await db.SaveChangesAsync();
    }

    private async Task MarkIgnoredAsync(int userId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStoryInteractions.Add(new UserStoryInteraction { UserId = userId, StoryId = storyId, IsIgnored = true });
        await db.SaveChangesAsync();
    }

    private async Task<int> SeedTagAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        Tag tag = new() { TagName = $"TreeSearchTag-{suffix}", TagTypeId = TagTypeEnum.Genre };
        db.Tags.Add(tag);
        await db.SaveChangesAsync();
        return tag.TagId;
    }

    private async Task TagStoryAsync(int storyId, int tagId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.StoryTags.Add(new StoryTag { StoryId = storyId, TagId = tagId });
        await db.SaveChangesAsync();
    }

    private async Task RebuildMartsAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        DiscoveryMartRebuilder rebuilder = scope.ServiceProvider.GetRequiredService<DiscoveryMartRebuilder>();
        await rebuilder.RebuildAllAsync();
    }

    private async Task<TreeSearchListingResultDto> SearchAsync(TreeSearchRequest request, StoryFilterDto filter)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ITreeSearchReadService svc = scope.ServiceProvider.GetRequiredService<ITreeSearchReadService>();
        return await svc.SearchAsync(request, filter);
    }

    // ── Tag composition ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_ComposesTagFilter_OnlyReturnsReachedStoriesMatchingTag()
    {
        int root = await SeedStoryAsync();
        int tagged = await SeedStoryAsync();
        int untagged = await SeedStoryAsync();
        int user1 = await SeedUserAsync();
        int user2 = await SeedUserAsync();
        await FavoriteAsync(user1, root); await FavoriteAsync(user1, tagged);
        await FavoriteAsync(user2, root); await FavoriteAsync(user2, untagged);
        int tagId = await SeedTagAsync();
        await TagStoryAsync(tagged, tagId);
        await RebuildMartsAsync();

        SetActiveUser(FakeActiveUserContext.Anonymous());

        TreeSearchListingResultDto result = await SearchAsync(
            new TreeSearchRequest { RootStoryId = root, MaxDegrees = 2, EdgeTypes = [TreeSearchEdgeType.Favorite] },
            new StoryFilterDto { IncludedTagIds = [tagId] });

        result.Items.Select(i => i.Story.StoryId).Should().BeEquivalentTo([tagged],
            "both stories are graph-reachable, but only the tagged one survives the composed filter");
    }

    [Fact]
    public async Task SearchAsync_UntaggedFilter_ReturnsBothReachedStories_MutationSanity()
    {
        // Confirms the tag filter above isn't accidentally excluding everything — without the
        // filter, both co-favorited stories come back.
        int root = await SeedStoryAsync();
        int storyA = await SeedStoryAsync();
        int storyB = await SeedStoryAsync();
        int user1 = await SeedUserAsync();
        int user2 = await SeedUserAsync();
        await FavoriteAsync(user1, root); await FavoriteAsync(user1, storyA);
        await FavoriteAsync(user2, root); await FavoriteAsync(user2, storyB);
        await RebuildMartsAsync();

        SetActiveUser(FakeActiveUserContext.Anonymous());

        TreeSearchListingResultDto result = await SearchAsync(
            new TreeSearchRequest { RootStoryId = root, MaxDegrees = 2, EdgeTypes = [TreeSearchEdgeType.Favorite] },
            new StoryFilterDto());

        result.Items.Select(i => i.Story.StoryId).Should().BeEquivalentTo([storyA, storyB]);
    }

    // ── Interaction-exclusion composition (user-editable, unlike the raw traversal) ──────────

    [Fact]
    public async Task SearchAsync_ComposesInteractionExclusion_ViaFilterNotTraversal()
    {
        int root = await SeedStoryAsync();
        int ignored = await SeedStoryAsync();
        int notIgnored = await SeedStoryAsync();
        int user1 = await SeedUserAsync();
        int user2 = await SeedUserAsync();
        await FavoriteAsync(user1, root); await FavoriteAsync(user1, ignored);
        await FavoriteAsync(user2, root); await FavoriteAsync(user2, notIgnored);
        await RebuildMartsAsync();

        int viewer = await SeedUserAsync("viewer");
        await MarkIgnoredAsync(viewer, ignored);
        SetActiveUser(viewer);

        TreeSearchListingResultDto result = await SearchAsync(
            new TreeSearchRequest { RootStoryId = root, MaxDegrees = 2, EdgeTypes = [TreeSearchEdgeType.Favorite] },
            new StoryFilterDto { ExcludedInteractions = [UserStoryInteractionTypeEnum.Ignore] });

        result.Items.Select(i => i.Story.StoryId).Should().BeEquivalentTo([notIgnored],
            "the viewer's Ignored story is excluded by the composed EF filter, not by the raw-reached traversal");
    }

    // ── Rating filter still applies (via the EF global query filter, not the rCTE) ───────────

    [Fact]
    public async Task SearchAsync_MatureStory_HiddenFromRestrictedViewer_ViaComposedFilter()
    {
        int root = await SeedStoryAsync(rating: Rating.E);
        int matureReached = await SeedStoryAsync(rating: Rating.M);
        int teenReached = await SeedStoryAsync(rating: Rating.T);
        int user1 = await SeedUserAsync();
        int user2 = await SeedUserAsync();
        await FavoriteAsync(user1, root); await FavoriteAsync(user1, matureReached);
        await FavoriteAsync(user2, root); await FavoriteAsync(user2, teenReached);
        await RebuildMartsAsync();

        int sfwViewer = await SeedUserAsync("sfw");
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(sfwViewer, showMatureContent: false));

        TreeSearchListingResultDto result = await SearchAsync(
            new TreeSearchRequest { RootStoryId = root, MaxDegrees = 2, EdgeTypes = [TreeSearchEdgeType.Favorite] },
            new StoryFilterDto());

        result.Items.Select(i => i.Story.StoryId).Should().BeEquivalentTo([teenReached],
            "the raw-reached traversal carries no rating filter — the composed EF read's global " +
            "content-rating query filter is what hides the mature story from this viewer");
    }

    // ── Sort ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SearchAsync_ByDegreeSort_OrdersAscendingByDegree()
    {
        // Hidden-gem chain: root -> gem2 @2 -> gem4 @4 (mirrors DiscoveryMartTests' deep-chain shape).
        int root = await SeedStoryAsync();
        int degree2 = await SeedStoryAsync();
        int degree4 = await SeedStoryAsync();
        int curator1 = await SeedUserAsync("curator1");
        int curator2 = await SeedUserAsync("curator2");
        await RecommendGemAsync(curator1, root); await RecommendGemAsync(curator1, degree2);
        await RecommendGemAsync(curator2, degree2); await RecommendGemAsync(curator2, degree4);
        await RebuildMartsAsync();

        SetActiveUser(FakeActiveUserContext.Anonymous());

        TreeSearchListingResultDto result = await SearchAsync(
            new TreeSearchRequest
            {
                RootStoryId = root, MaxDegrees = 4, EdgeTypes = [TreeSearchEdgeType.HiddenGem],
                Sort = TreeSearchSortOrder.ByDegree,
            },
            new StoryFilterDto());

        result.Items.Select(i => (i.Story.StoryId, i.Degree)).Should().Equal((degree2, 2), (degree4, 4));
        result.DegreesReached.Should().Be(4);
    }

    // ── Cap on the FILTERED set (Fork 3A — the reason raw-reached carries no cap) ────────────

    [Fact]
    public async Task SearchAsync_ResultCapTruncated_ComputedAgainstFilteredSet()
    {
        int root = await SeedStoryAsync();
        int tagId = await SeedTagAsync();

        int taggedA = await SeedStoryAsync();
        int taggedB = await SeedStoryAsync();
        int untagged = await SeedStoryAsync(); // reachable but filtered out — must not count toward the cap
        await TagStoryAsync(taggedA, tagId);
        await TagStoryAsync(taggedB, tagId);

        int user1 = await SeedUserAsync();
        int user2 = await SeedUserAsync();
        int user3 = await SeedUserAsync();
        await FavoriteAsync(user1, root); await FavoriteAsync(user1, taggedA);
        await FavoriteAsync(user2, root); await FavoriteAsync(user2, taggedB);
        await FavoriteAsync(user3, root); await FavoriteAsync(user3, untagged);
        await RebuildMartsAsync();

        SetActiveUser(FakeActiveUserContext.Anonymous());

        // Cap of 1 against a filtered set of exactly 2 (taggedA, taggedB) — truncated is true and
        // exactly one item comes back, proving the cap applied AFTER the tag filter, not before.
        TreeSearchListingResultDto result = await SearchAsync(
            new TreeSearchRequest
            {
                RootStoryId = root, MaxDegrees = 2, EdgeTypes = [TreeSearchEdgeType.Favorite], ResultCap = 1,
            },
            new StoryFilterDto { IncludedTagIds = [tagId] });

        result.Items.Should().HaveCount(1);
        result.ResultCapTruncated.Should().BeTrue();
        result.Items[0].Story.StoryId.Should().BeOneOf(taggedA, taggedB);
    }

    // ── Helper: hidden-gem recommendation (mirrors DiscoveryMartTests.RecommendAsync) ────────

    private async Task RecommendGemAsync(int recommenderId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Recommendations.Add(new Recommendation
        {
            StoryId = storyId,
            RecommenderId = recommenderId,
            StatusId = (short)RecommendationStatusEnum.Approved,
            IsHiddenGem = true,
            DatePosted = DateTime.UtcNow,
            RecommendationDetail = new RecommendationDetail { Text = "<p>test rec</p>" },
        });
        await db.SaveChangesAsync();
    }
}
