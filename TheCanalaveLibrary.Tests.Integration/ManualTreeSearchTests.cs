using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ServerManualTreeSearchReadService"/> (Feature 33 / WU40) —
/// the stateless degree-1 pivots over LIVE tables that power the Explore and Deep Dive tabs —
/// plus the Pinned Story write gate (<see cref="IUserSettingsService.UpdateAuthorSettingsAsync"/>)
/// and the WU40 path-hop hydration on <see cref="ITreeSearchReadService.SearchAsync"/>.
///
/// <b>Per-test seeding plan:</b> every test seeds its own users/stories via the base helpers and
/// its own recommendations/interactions/vouches via direct <c>ApplicationDbContext</c> rows
/// (mirrors <c>DiscoveryMartTests</c>). No mart rebuild is needed for the pivots — manual tree
/// search reads live tables by design. Tier: Integration (Testcontainers Postgres, real EF).
/// </summary>
[Collection("Postgres")]
public sealed class ManualTreeSearchTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    // ── Seeding helpers (mirrors DiscoveryMartTests) ────────────────────────────────────────────

    private async Task<int> RecommendAsync(
        int? recommenderId, int storyId, bool gem = false, bool spotlight = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Recommendation rec = new()
        {
            StoryId = storyId,
            RecommenderId = recommenderId,
            StatusId = (short)RecommendationStatusEnum.Approved,
            IsHiddenGem = gem,
            IsHighlightedByAuthor = spotlight,
            DatePosted = DateTime.UtcNow,
            RecommendationDetail = new RecommendationDetail { Text = "<p>test rec</p>" },
        };
        db.Recommendations.Add(rec);
        await db.SaveChangesAsync();
        return rec.RecommendationId;
    }

    private async Task FavoriteAsync(int userId, int storyId, bool hidden = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStoryInteractions.Add(new UserStoryInteraction
        {
            UserId = userId, StoryId = storyId,
            IsFavorite = !hidden, IsHiddenFavorite = hidden,
        });
        await db.SaveChangesAsync();
    }

    private async Task VouchAsync(int voucherId, int voucheeId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Vouches.Add(new Vouch { VouchingUserId = voucherId, VouchedUserId = voucheeId, DateVouched = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    private async Task PinAsync(int userId, int? storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await db.Users.FindAsync(userId) ?? throw new InvalidOperationException("seed user missing");
        user.PinnedStoryId = storyId;
        await db.SaveChangesAsync();
    }

    private async Task<ManualTreeNeighborsDto> StoryPivotAsync(StoryNeighborsRequest request)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IManualTreeSearchReadService>()
            .GetStoryNeighborsAsync(request);
    }

    private async Task<ManualTreeNeighborsDto> UserPivotAsync(UserNeighborsRequest request)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IManualTreeSearchReadService>()
            .GetUserNeighborsAsync(request);
    }

    // ── Story anchor ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StoryPivot_ReturnsAuthor_AndPublicFavoritersOnly()
    {
        int author = await SeedUserAsync("Author");
        int story = await SeedStoryAsync(authorId: author);
        int publicFav = await SeedUserAsync("PublicFav");
        int hiddenFav = await SeedUserAsync("HiddenFav");
        await FavoriteAsync(publicFav, story);
        await FavoriteAsync(hiddenFav, story, hidden: true);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        ManualTreeNeighborsDto dto = await StoryPivotAsync(new StoryNeighborsRequest { StoryId = story });

        dto.Author.Should().NotBeNull();
        dto.Author!.UserId.Should().Be(author);
        dto.Favoriters.Should().NotBeNull();
        dto.Favoriters!.Items.Select(u => u.UserId).Should().BeEquivalentTo([publicFav],
            "hidden favorites NEVER appear in manual tree search, regardless of owner consent");
        dto.Favoriters.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task StoryPivot_FamilyFlags_WidenAndNarrowOneQuery_BadgesStackOnOneRow()
    {
        int author = await SeedUserAsync("Author");
        int story = await SeedStoryAsync(authorId: author);
        int plainRec = await SeedUserAsync("Plain");
        int gemAndSpot = await SeedUserAsync("GemSpot");
        await RecommendAsync(plainRec, story);
        await RecommendAsync(gemAndSpot, story, gem: true, spotlight: true);

        SetActiveUser(FakeActiveUserContext.Anonymous());

        // All three flags on: both rows, the double-flagged one ONCE with both badges.
        ManualTreeNeighborsDto all = await StoryPivotAsync(new StoryNeighborsRequest { StoryId = story });
        all.RecommendationFamily!.Items.Should().HaveCount(2);
        ManualTreeRecItemDto stacked = all.RecommendationFamily.Items
            .Single(i => i.Recommendation.Recommender!.UserId == gemAndSpot);
        stacked.Recommendation.IsHiddenGem.Should().BeTrue();
        stacked.Recommendation.IsHighlightedByAuthor.Should().BeTrue();

        // Plain off, gem on: only the flagged row survives (the checkbox narrows the WHERE clause).
        ManualTreeNeighborsDto gemsOnly = await StoryPivotAsync(new StoryNeighborsRequest
        {
            StoryId = story, IncludeRecommendations = false, IncludeSpotlights = false,
        });
        gemsOnly.RecommendationFamily!.Items.Should().ContainSingle()
            .Which.Recommendation.Recommender!.UserId.Should().Be(gemAndSpot);
        gemsOnly.RecommendationFamily.TotalCount.Should().Be(1, "TotalCount respects the same flags");
    }

    [Fact]
    public async Task StoryPivot_TogglesOff_SuppressSections()
    {
        int author = await SeedUserAsync("Author");
        int story = await SeedStoryAsync(authorId: author);
        await FavoriteAsync(await SeedUserAsync(), story);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        ManualTreeNeighborsDto dto = await StoryPivotAsync(new StoryNeighborsRequest
        {
            StoryId = story,
            IncludeAuthor = false,   // toggleable — never hardcoded on (the Author×Pinned bounce fix)
            IncludeFavoriters = false,
            IncludeRecommendations = false, IncludeHiddenGems = false, IncludeSpotlights = false,
        });

        dto.Author.Should().BeNull();
        dto.Favoriters.Should().BeNull();
        dto.RecommendationFamily.Should().BeNull();
    }

    // ── User anchor ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UserPivot_AuthoredSortsPinnedFirst_AndReportsPinnedStoryId()
    {
        int author = await SeedUserAsync("Author");
        int older = await SeedStoryAsync(authorId: author);
        int newer = await SeedStoryAsync(authorId: author);
        int pinned = await SeedStoryAsync(authorId: author);
        await PinAsync(author, pinned);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        ManualTreeNeighborsDto dto = await UserPivotAsync(new UserNeighborsRequest { UserId = author });

        dto.PinnedStoryId.Should().Be(pinned);
        dto.Authored.Should().NotBeNull();
        dto.Authored!.TotalCount.Should().Be(3);
        dto.Authored.Items.First().StoryId.Should().Be(pinned,
            "the pinned story is the author's deliberate 'see this first' choice");
        dto.Authored.Items.Select(s => s.StoryId).Should().BeEquivalentTo([pinned, older, newer]);
    }

    [Fact]
    public async Task UserPivot_FavoritesArePublicOnly_VouchProjectsVoucheesStories()
    {
        int viewerUser = await SeedUserAsync("Pivot");
        int pubFav = await SeedStoryAsync();
        int hidFav = await SeedStoryAsync();
        await FavoriteAsync(viewerUser, pubFav);
        await FavoriteAsync(viewerUser, hidFav, hidden: true);

        int vouchee = await SeedUserAsync("Vouchee");
        int voucheeStory = await SeedStoryAsync(authorId: vouchee);
        await VouchAsync(viewerUser, vouchee);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        ManualTreeNeighborsDto dto = await UserPivotAsync(new UserNeighborsRequest { UserId = viewerUser });

        dto.Favorites!.Items.Select(s => s.StoryId).Should().BeEquivalentTo([pubFav],
            "hidden favorites never appear in manual");
        dto.VouchedStories!.Items.Select(s => s.StoryId).Should().BeEquivalentTo([voucheeStory],
            "vouch traverses forward only: voucher → vouchee → their published stories");
    }

    [Fact]
    public async Task UserPivot_FamilyIsRecsWrittenByTheUser_WithStoryHydratedPerRow()
    {
        int recommender = await SeedUserAsync("Recommender");
        int storyA = await SeedStoryAsync();
        int storyB = await SeedStoryAsync();
        await RecommendAsync(recommender, storyA, gem: true);
        await RecommendAsync(recommender, storyB);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        ManualTreeNeighborsDto dto = await UserPivotAsync(new UserNeighborsRequest { UserId = recommender });

        dto.RecommendationFamily!.Items.Should().HaveCount(2);
        dto.RecommendationFamily.Items.Select(i => i.Story.StoryId).Should().BeEquivalentTo([storyA, storyB],
            "each compound row carries its target story's listing");
        dto.RecommendationFamily.Items.Single(i => i.Story.StoryId == storyA)
            .Recommendation.IsHiddenGem.Should().BeTrue();
    }

    // ── Paging ─────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Paging_TotalCountIsHonest_AndPagesAreDisjoint()
    {
        int author = await SeedUserAsync("Author");
        int story = await SeedStoryAsync(authorId: author);
        for (int i = 0; i < 7; i++)
            await FavoriteAsync(await SeedUserAsync($"Fav{i}"), story);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        ManualTreeNeighborsDto page1 = await StoryPivotAsync(new StoryNeighborsRequest
        { StoryId = story, PageSize = 5, FavoritersPage = 1 });
        ManualTreeNeighborsDto page2 = await StoryPivotAsync(new StoryNeighborsRequest
        { StoryId = story, PageSize = 5, FavoritersPage = 2 });

        page1.Favoriters!.TotalCount.Should().Be(7);
        page1.Favoriters.Items.Should().HaveCount(5);
        page2.Favoriters!.Items.Should().HaveCount(2);
        page1.Favoriters.Items.Select(u => u.UserId)
            .Should().NotIntersectWith(page2.Favoriters.Items.Select(u => u.UserId));
    }

    // ── Rehydration displays + viewer visibility ───────────────────────────────────────────────

    [Fact]
    public async Task GetNodeDisplays_OmitsRatingGatedStories_SoTheClientPrunesThem()
    {
        int author = await SeedUserAsync("Author");
        int sfw = await SeedStoryAsync(authorId: author);
        int mature = await SeedStoryAsync(authorId: author, rating: Rating.M);

        using IServiceScope scope = Factory.Services.CreateScope();
        IManualTreeSearchReadService svc = scope.ServiceProvider.GetRequiredService<IManualTreeSearchReadService>();

        SetActiveUser(FakeActiveUserContext.Anonymous()); // SFW viewer
        ManualTreeNodeDisplaysDto sfwView = await svc.GetNodeDisplaysAsync([sfw, mature], [author]);
        sfwView.Stories.Select(d => d.EntityId).Should().BeEquivalentTo([sfw],
            "a rating-gated story yields no display and the client tree prunes it");
        sfwView.Users.Should().ContainSingle().Which.EntityId.Should().Be(author);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(author, showMatureContent: true));
        ManualTreeNodeDisplaysDto matureView = await svc.GetNodeDisplaysAsync([sfw, mature], [author]);
        matureView.Stories.Select(d => d.EntityId).Should().BeEquivalentTo([sfw, mature]);
    }

    // ── Pinned Story write gate (Phase 2) ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAuthorSettings_PinsOwnPublishedStory_AndRejectsForeignOrMissing()
    {
        int author = await SeedUserAsync("Author");
        int own = await SeedStoryAsync(authorId: author);
        int foreignAuthor = await SeedUserAsync("Other");
        int foreign = await SeedStoryAsync(authorId: foreignAuthor);

        SetActiveUser(author);
        using IServiceScope scope = Factory.Services.CreateScope();
        IUserSettingsService settings = scope.ServiceProvider.GetRequiredService<IUserSettingsService>();

        // Own, published story: pins.
        await settings.UpdateAuthorSettingsAsync(new AuthorSettingsDto(Rating.T, own));
        (await settings.GetMySettingsAsync()).Author.PinnedStoryId.Should().Be(own);

        // Someone else's story: rejected by the service gate (UI only offers own stories,
        // but the gate lives server-side).
        Func<Task> pinForeign = () => settings.UpdateAuthorSettingsAsync(new AuthorSettingsDto(Rating.T, foreign));
        await pinForeign.Should().ThrowAsync<InvalidOperationException>();

        // Null unpins.
        await settings.UpdateAuthorSettingsAsync(new AuthorSettingsDto(Rating.T, null));
        (await settings.GetMySettingsAsync()).Author.PinnedStoryId.Should().BeNull();
    }

    // ── WU40 path-hop hydration on the Automatic tab (privacy correction) ──────────────────────

    [Fact]
    public async Task SearchAsync_HydratesPathHops_WithUsernamesAndStoryTitles()
    {
        // A one-hop hidden-gem chain: root story → (gem by user G) → gem story.
        int author = await SeedUserAsync("ChainAuthor");
        int root = await SeedStoryAsync(authorId: author);
        int gemStory = await SeedStoryAsync(authorId: author);
        int gemmer = await SeedUserAsync("Gemmer");
        await RecommendAsync(gemmer, root, gem: true);
        await RecommendAsync(gemmer, gemStory, gem: true);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DiscoveryMartRebuilder>().RebuildAllAsync();
        }

        SetActiveUser(FakeActiveUserContext.Anonymous());
        using IServiceScope searchScope = Factory.Services.CreateScope();
        TreeSearchListingResultDto result = await searchScope.ServiceProvider
            .GetRequiredService<ITreeSearchReadService>()
            .SearchAsync(
                new TreeSearchRequest
                {
                    RootStoryId = root, MaxDegrees = 2,
                    EdgeTypes = [TreeSearchEdgeType.HiddenGem], IncludePaths = true,
                },
                new StoryFilterDto());

        TreeSearchListingItemDto hit = result.Items.Should().ContainSingle(i => i.Story.StoryId == gemStory).Subject;
        hit.PathHops.Should().NotBeNull("chain-of-trust paths hydrate display hops (WU40)");
        hit.PathHops!.Should().Contain(h => !h.IsStory && h.Label != null && h.Label.StartsWith("Gemmer"),
            "user hops carry real usernames — chain-of-trust paths have no anonymized contributor");
        hit.PathHops.Should().Contain(h => h.IsStory && h.Id == gemStory && h.Label != null);
    }
}
