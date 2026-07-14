using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// WU-Marts end-to-end coverage over real Postgres: the raw-SQL mart rebuilds (staging build +
/// atomic swap + index renames), the F60 edge projections (all six edge types, consent,
/// visibility, anonymized-rec exclusion), the F61 ranked co-occurrence reads, and the F59
/// recursive-CTE traversal (wide co-favorite discovery, deep hidden-gem chains with paths, the
/// vouch projection, the mature-silent-bridge rule, exclusion filters, both sort orders).
///
/// Each test seeds its own small hand-built graph via the base helpers + direct
/// ApplicationDbContext rows (FK parents: stories via SeedStoryAsync, users via SeedUserAsync;
/// recommendation_statuses / themes are HasData lookups that survive Respawn), then triggers
/// <see cref="DiscoveryMartRebuilder"/> directly — the hosted worker is removed by
/// TestAppFactory, so rebuilds here are deterministic. Mart tables are OUTSIDE Respawn's delete
/// graph (created after fixture init): the fresh-staging rebuild fully replaces them each time,
/// so every test rebuilds after its own seeding and never trusts leftover mart rows.
/// </summary>
[Collection("Postgres")]
public sealed class DiscoveryMartTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private readonly PostgresFixture _postgres = postgres;

    // ── Seeding helpers (direct ground-truth rows, DataSeeder-style service bypass) ──────────

    private async Task FavoriteAsync(int userId, int storyId, bool hidden = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStoryInteractions.Add(new UserStoryInteraction
        {
            UserId = userId, StoryId = storyId, IsFavorite = !hidden, IsHiddenFavorite = hidden,
        });
        await db.SaveChangesAsync();
    }

    private async Task MarkInteractionAsync(int userId, int storyId, Action<UserStoryInteraction> mutate)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserStoryInteraction row = new() { UserId = userId, StoryId = storyId };
        mutate(row);
        db.UserStoryInteractions.Add(row);
        await db.SaveChangesAsync();
    }

    private async Task SetConsentAsync(int userId, bool consent)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await db.Users.FindAsync(userId) ?? throw new InvalidOperationException("seed user missing");
        user.AllowDiscoveryFromHiddenFavorites = consent;
        await db.SaveChangesAsync();
    }

    private async Task<int> RecommendAsync(
        int? recommenderId, int storyId, bool gem = false, bool spotlight = false, bool takenDown = false)
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
            IsTakenDown = takenDown,
            DatePosted = DateTime.UtcNow,
            RecommendationDetail = new RecommendationDetail { Text = "<p>test rec</p>" },
        };
        db.Recommendations.Add(rec);
        await db.SaveChangesAsync();
        return rec.RecommendationId;
    }

    private async Task VouchAsync(int voucherId, int voucheeId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Vouches.Add(new Vouch { VouchingUserId = voucherId, VouchedUserId = voucheeId, DateVouched = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    private async Task<int> SeedDraftStoryAsync(int authorId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Story story = new()
        {
            AuthorId = authorId, Rating = Rating.E, StoryStatusId = StoryStatusEnum.Draft,
            PublishedDate = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow,
            StoryListing = new StoryListing { StoryTitle = "Draft story", ShortDescription = "draft" },
            StoryDetail = new StoryDetail { LongDescription = "draft", PostApprovalStatus = StoryStatusEnum.InProgress },
        };
        db.Stories.Add(story);
        await db.SaveChangesAsync();
        return story.StoryId;
    }

    private async Task RebuildMartsAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        DiscoveryMartRebuilder rebuilder = scope.ServiceProvider.GetRequiredService<DiscoveryMartRebuilder>();
        await rebuilder.RebuildAllAsync();
    }

    private async Task<List<(int UserId, int StoryId, short EdgeType)>> ReadTreeEdgesAsync()
    {
        await using NpgsqlConnection connection = new(_postgres.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(
            "SELECT user_id, story_id, edge_type FROM user_story_tree_search_entries ORDER BY user_id, story_id, edge_type",
            connection);
        List<(int, int, short)> edges = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            edges.Add((reader.GetInt32(0), reader.GetInt32(1), reader.GetInt16(2)));
        return edges;
    }

    private ITreeSearchReadService TreeSearch(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ITreeSearchReadService>();

    // ── F60: edge projections ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TreeMart_ProjectsAllSixEdgeTypes_WithConsentVisibilityAndAnonymizedRules()
    {
        int author = await SeedUserAsync("author");
        int story = await SeedStoryAsync(author);
        int reader = await SeedUserAsync("reader");
        int consentingHider = await SeedUserAsync("consenting");
        int nonConsentingHider = await SeedUserAsync("nonconsenting");
        int gemRecommender = await SeedUserAsync("gem-rec");
        int spotlightRecommender = await SeedUserAsync("spot-rec");
        int voucher = await SeedUserAsync("voucher");

        await FavoriteAsync(reader, story);                                   // → Favorite
        await SetConsentAsync(consentingHider, true);
        await FavoriteAsync(consentingHider, story, hidden: true);            // → Favorite (consented)
        await FavoriteAsync(nonConsentingHider, story, hidden: true);         // → nothing (no consent)
        await RecommendAsync(gemRecommender, story, gem: true);               // → Recommendation + HiddenGem
        await RecommendAsync(spotlightRecommender, story, spotlight: true);   // → Recommendation + AuthorSpotlight
        await RecommendAsync(recommenderId: null, story);                     // anonymized → nothing
        await VouchAsync(voucher, author);                                    // → Vouch on the author's story

        // Draft story: no edges at all, even with a favorite and the vouch projection in play.
        int draftStory = await SeedDraftStoryAsync(author);
        await FavoriteAsync(reader, draftStory);

        await RebuildMartsAsync();
        List<(int UserId, int StoryId, short EdgeType)> edges = await ReadTreeEdgesAsync();

        edges.Should().BeEquivalentTo(new[]
        {
            (author, story, (short)TreeSearchEdgeType.AuthoredBy),
            (reader, story, (short)TreeSearchEdgeType.Favorite),
            (consentingHider, story, (short)TreeSearchEdgeType.Favorite),
            (gemRecommender, story, (short)TreeSearchEdgeType.Recommendation),
            (gemRecommender, story, (short)TreeSearchEdgeType.HiddenGem),
            (spotlightRecommender, story, (short)TreeSearchEdgeType.Recommendation),
            (spotlightRecommender, story, (short)TreeSearchEdgeType.AuthorSpotlight),
            (voucher, story, (short)TreeSearchEdgeType.Vouch),
        });
    }

    [Fact]
    public async Task TreeMart_RebuildTwice_SwapSurvivesAndReflectsNewGroundTruth()
    {
        int author = await SeedUserAsync("author");
        int story = await SeedStoryAsync(author);
        int reader = await SeedUserAsync("reader");
        await FavoriteAsync(reader, story);

        await RebuildMartsAsync();
        (await ReadTreeEdgesAsync()).Should().HaveCount(2); // AuthoredBy + Favorite

        int secondReader = await SeedUserAsync("reader2");
        await FavoriteAsync(secondReader, story);

        // The second rebuild exercises the full rename dance against already-swapped tables —
        // the index/constraint renames are what make this not collide (DiscoveryMartSchema).
        await RebuildMartsAsync();
        List<(int UserId, int StoryId, short EdgeType)> edges = await ReadTreeEdgesAsync();
        edges.Should().HaveCount(3);
        edges.Should().Contain((secondReader, story, (short)TreeSearchEdgeType.Favorite));
    }

    // ── F61: co-occurrence reads ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task AlsoFavorited_RanksBySharedUserCount_BothDirections()
    {
        int storyA = await SeedStoryAsync();
        int storyB = await SeedStoryAsync();
        int storyC = await SeedStoryAsync();
        int user1 = await SeedUserAsync();
        int user2 = await SeedUserAsync();
        int user3 = await SeedUserAsync();
        await FavoriteAsync(user1, storyA); await FavoriteAsync(user1, storyB);
        await FavoriteAsync(user2, storyA); await FavoriteAsync(user2, storyB);
        await FavoriteAsync(user3, storyA); await FavoriteAsync(user3, storyC);

        await RebuildMartsAsync();
        SetActiveUser(FakeActiveUserContext.Anonymous());
        using IServiceScope scope = Factory.Services.CreateScope();
        ICoOccurrenceReadService service = scope.ServiceProvider.GetRequiredService<ICoOccurrenceReadService>();

        IReadOnlyList<RelatedStoryScoreDto> related = await service.GetAlsoFavoritedAsync(storyA);
        related.Select(r => (r.RelatedStoryId, r.Score)).Should().Equal((storyB, 2), (storyC, 1));

        // Full matrix both directions: B's related list scores A back.
        IReadOnlyList<RelatedStoryScoreDto> reverse = await service.GetAlsoFavoritedAsync(storyB);
        reverse.Should().ContainSingle(r => r.RelatedStoryId == storyA && r.Score == 2);
    }

    [Fact]
    public async Task AlsoFavorited_ViewerIgnoredStory_IsExcludedAtReadTime()
    {
        int storyA = await SeedStoryAsync();
        int storyB = await SeedStoryAsync();
        int user1 = await SeedUserAsync();
        int user2 = await SeedUserAsync();
        await FavoriteAsync(user1, storyA); await FavoriteAsync(user1, storyB);
        await FavoriteAsync(user2, storyA); await FavoriteAsync(user2, storyB);

        int viewer = await SeedUserAsync("viewer");
        await MarkInteractionAsync(viewer, storyB, r => r.IsIgnored = true);

        await RebuildMartsAsync();
        SetActiveUser(viewer);
        using IServiceScope scope = Factory.Services.CreateScope();
        ICoOccurrenceReadService service = scope.ServiceProvider.GetRequiredService<ICoOccurrenceReadService>();

        // Ignored is the seeded §8.7 default exclusion for the AlsoFavorited mode.
        (await service.GetAlsoFavoritedAsync(storyA)).Should().BeEmpty();

        SetActiveUser(FakeActiveUserContext.Anonymous());
        using IServiceScope anonymousScope = Factory.Services.CreateScope();
        ICoOccurrenceReadService anonymousService =
            anonymousScope.ServiceProvider.GetRequiredService<ICoOccurrenceReadService>();
        (await anonymousService.GetAlsoFavoritedAsync(storyA)).Should().ContainSingle(r => r.RelatedStoryId == storyB);
    }

    [Fact]
    public async Task AlsoFavorited_HiddenFavorites_CountOnlyWithEdgeOwnerConsent()
    {
        int storyA = await SeedStoryAsync();
        int storyB = await SeedStoryAsync();
        int storyC = await SeedStoryAsync();
        int consenting = await SeedUserAsync("consenting");
        int nonConsenting = await SeedUserAsync("nonconsenting");
        await SetConsentAsync(consenting, true);
        await FavoriteAsync(consenting, storyA);
        await FavoriteAsync(consenting, storyB, hidden: true);      // consented → contributes
        await FavoriteAsync(nonConsenting, storyA);
        await FavoriteAsync(nonConsenting, storyC, hidden: true);   // no consent → silent

        await RebuildMartsAsync();
        SetActiveUser(FakeActiveUserContext.Anonymous());
        using IServiceScope scope = Factory.Services.CreateScope();
        ICoOccurrenceReadService service = scope.ServiceProvider.GetRequiredService<ICoOccurrenceReadService>();

        IReadOnlyList<RelatedStoryScoreDto> related = await service.GetAlsoFavoritedAsync(storyA);
        related.Should().ContainSingle(r => r.RelatedStoryId == storyB && r.Score == 1);
        related.Should().NotContain(r => r.RelatedStoryId == storyC);
    }

    [Fact]
    public async Task AlsoFavorited_ExplicitExclusions_OverrideTheDefaultEntirely()
    {
        // storyB and storyC are both co-favorited with storyA by other users. The viewer has
        // Ignored storyB (the seeded §8.7 default exclusion for AlsoFavorited) and separately
        // Favorited storyC themselves (not a default-excluded kind).
        int storyA = await SeedStoryAsync();
        int storyB = await SeedStoryAsync();
        int storyC = await SeedStoryAsync();
        int user1 = await SeedUserAsync();
        int user2 = await SeedUserAsync();
        await FavoriteAsync(user1, storyA); await FavoriteAsync(user1, storyB);
        await FavoriteAsync(user2, storyA); await FavoriteAsync(user2, storyC);

        int viewer = await SeedUserAsync("viewer");
        await MarkInteractionAsync(viewer, storyB, r => r.IsIgnored = true);
        await FavoriteAsync(viewer, storyC);

        await RebuildMartsAsync();
        SetActiveUser(viewer);
        using IServiceScope scope = Factory.Services.CreateScope();
        ICoOccurrenceReadService service = scope.ServiceProvider.GetRequiredService<ICoOccurrenceReadService>();

        // null (unspecified): resolves the §8.7 defaults internally — Ignored is excluded,
        // Favorite is not. Regression guard on the pre-WU-RelatedStories behavior.
        (await service.GetAlsoFavoritedAsync(storyA))
            .Should().ContainSingle(r => r.RelatedStoryId == storyC);

        // Explicit empty list bypasses the defaults lookup entirely — the Ignored story is no
        // longer excluded because nothing overrides the default now.
        (await service.GetAlsoFavoritedAsync(storyA, excludedInteractions: []))
            .Select(r => r.RelatedStoryId).Should().BeEquivalentTo([storyB, storyC]);

        // Explicit non-default exclusion set: excludes Favorite (dropping storyC) while leaving
        // the Ignored story visible (Ignored is no longer in the effective set at all).
        (await service.GetAlsoFavoritedAsync(
                storyA, excludedInteractions: [UserStoryInteractionTypeEnum.Favorite]))
            .Should().ContainSingle(r => r.RelatedStoryId == storyB);
    }

    [Fact]
    public async Task AlsoRecommended_MirrorsOnRecommenders_ExcludingAnonymized()
    {
        int storyA = await SeedStoryAsync();
        int storyB = await SeedStoryAsync();
        int storyC = await SeedStoryAsync();
        int recommender = await SeedUserAsync("rec");
        await RecommendAsync(recommender, storyA);
        await RecommendAsync(recommender, storyB);
        await RecommendAsync(recommenderId: null, storyA); // anonymized pair-partner would be storyC
        await RecommendAsync(recommenderId: null, storyC);

        await RebuildMartsAsync();
        SetActiveUser(FakeActiveUserContext.Anonymous());
        using IServiceScope scope = Factory.Services.CreateScope();
        ICoOccurrenceReadService service = scope.ServiceProvider.GetRequiredService<ICoOccurrenceReadService>();

        IReadOnlyList<RelatedStoryScoreDto> related = await service.GetAlsoRecommendedAsync(storyA);
        related.Should().ContainSingle(r => r.RelatedStoryId == storyB && r.Score == 1,
            "anonymized recommendations contribute no co-occurrence");
    }

    // ── F59: recursive-CTE traversal ─────────────────────────────────────────────────────────

    [Fact]
    public async Task TreeSearch_WideDegree2_FindsCoFavoritedStories_ExcludingRootAndIgnored()
    {
        int story1 = await SeedStoryAsync();
        int story2 = await SeedStoryAsync();
        int story3 = await SeedStoryAsync();
        int user1 = await SeedUserAsync();
        int user2 = await SeedUserAsync();
        await FavoriteAsync(user1, story1); await FavoriteAsync(user1, story2);
        await FavoriteAsync(user2, story1); await FavoriteAsync(user2, story3);
        await RebuildMartsAsync();

        SetActiveUser(FakeActiveUserContext.Anonymous());
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            TreeSearchResultDto result = await TreeSearch(scope).TraverseAsync(new TreeSearchRequest
            {
                RootStoryId = story1, MaxDegrees = 2, EdgeTypes = [TreeSearchEdgeType.Favorite],
            });
            result.Hits.Select(h => h.StoryId).Should().BeEquivalentTo([story2, story3],
                "co-favorited stories are two degrees out; the root itself is never a result");
            result.Hits.Should().OnlyContain(h => h.Degree == 2);
            result.DegreesReached.Should().Be(2);
            result.ResultCapTruncated.Should().BeFalse();
        }

        // The viewer's own Ignored row filters at the presentation join (AutoTreeSearch default).
        int viewer = await SeedUserAsync("viewer");
        await MarkInteractionAsync(viewer, story2, r => r.IsIgnored = true);
        SetActiveUser(viewer);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            TreeSearchResultDto result = await TreeSearch(scope).TraverseAsync(new TreeSearchRequest
            {
                RootStoryId = story1, MaxDegrees = 2, EdgeTypes = [TreeSearchEdgeType.Favorite],
            });
            result.Hits.Select(h => h.StoryId).Should().BeEquivalentTo([story3]);
        }
    }

    [Fact]
    public async Task TreeSearch_DeepHiddenGemChain_ReachesNicheStoryAtDegree6_WithShortestPaths()
    {
        // Chain-of-trust wiring: curator1 gems {g1,g2}, curator2 gems {g2,g3}, curator3 gems
        // {g3,g4} → from g1: g2@2, g3@4, g4@6 — the deep-mode experience in miniature.
        int gem1 = await SeedStoryAsync();
        int gem2 = await SeedStoryAsync();
        int gem3 = await SeedStoryAsync();
        int gem4 = await SeedStoryAsync();
        int curator1 = await SeedUserAsync("curator1");
        int curator2 = await SeedUserAsync("curator2");
        int curator3 = await SeedUserAsync("curator3");
        await RecommendAsync(curator1, gem1, gem: true); await RecommendAsync(curator1, gem2, gem: true);
        await RecommendAsync(curator2, gem2, gem: true); await RecommendAsync(curator2, gem3, gem: true);
        await RecommendAsync(curator3, gem3, gem: true); await RecommendAsync(curator3, gem4, gem: true);
        await RebuildMartsAsync();

        SetActiveUser(FakeActiveUserContext.Anonymous());
        using IServiceScope scope = Factory.Services.CreateScope();

        TreeSearchResultDto deep = await TreeSearch(scope).TraverseAsync(new TreeSearchRequest
        {
            RootStoryId = gem1, MaxDegrees = 6, EdgeTypes = [TreeSearchEdgeType.HiddenGem],
            IncludePaths = true, Sort = TreeSearchSortOrder.ByDegree,
        });

        deep.Hits.Select(h => (h.StoryId, h.Degree)).Should().Equal((gem2, 2), (gem3, 4), (gem4, 6));
        deep.DegreesReached.Should().Be(6);
        deep.Hits.Should().OnlyContain(h => !string.IsNullOrEmpty(h.Path),
            "chain-of-trust requests materialize one shortest path per hit");

        // A shallower walk cannot reach the tail — depth is the knob, not a weight.
        TreeSearchResultDto shallow = await TreeSearch(scope).TraverseAsync(new TreeSearchRequest
        {
            RootStoryId = gem1, MaxDegrees = 4, EdgeTypes = [TreeSearchEdgeType.HiddenGem],
        });
        shallow.Hits.Select(h => h.StoryId).Should().BeEquivalentTo([gem2, gem3]);
    }

    [Fact]
    public async Task TreeSearch_MatureStory_IsASilentBridge_ForSfwViewers()
    {
        int storyE1 = await SeedStoryAsync(rating: Rating.E);
        int storyM = await SeedStoryAsync(rating: Rating.M);
        int storyE2 = await SeedStoryAsync(rating: Rating.E);
        int user1 = await SeedUserAsync();
        int user2 = await SeedUserAsync();
        await FavoriteAsync(user1, storyE1); await FavoriteAsync(user1, storyM);
        await FavoriteAsync(user2, storyM); await FavoriteAsync(user2, storyE2);
        await RebuildMartsAsync();

        int sfwViewer = await SeedUserAsync("sfw");
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(sfwViewer, showMatureContent: false));
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            TreeSearchResultDto result = await TreeSearch(scope).TraverseAsync(new TreeSearchRequest
            {
                RootStoryId = storyE1, MaxDegrees = 4, EdgeTypes = [TreeSearchEdgeType.Favorite],
            });
            result.Hits.Select(h => h.StoryId).Should().Contain(storyE2,
                "the mature story routes the walk as a bridge node");
            result.Hits.Select(h => h.StoryId).Should().NotContain(storyM,
                "but is never shown to a SFW viewer (rating filters at the presentation join)");
        }

        int matureViewer = await SeedUserAsync("mature", showMature: true);
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(matureViewer, showMatureContent: true));
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            TreeSearchResultDto result = await TreeSearch(scope).TraverseAsync(new TreeSearchRequest
            {
                RootStoryId = storyE1, MaxDegrees = 4, EdgeTypes = [TreeSearchEdgeType.Favorite],
            });
            result.Hits.Select(h => h.StoryId).Should().Contain([storyM, storyE2]);
        }
    }

    [Fact]
    public async Task TreeSearch_VouchProjection_SurfacesTheVoucheesPublishedStories()
    {
        int upAndComer = await SeedUserAsync("author");
        int published1 = await SeedStoryAsync(upAndComer);
        int published2 = await SeedStoryAsync(upAndComer);
        int draft = await SeedDraftStoryAsync(upAndComer);
        int voucher = await SeedUserAsync("voucher");
        await VouchAsync(voucher, upAndComer);
        await RebuildMartsAsync();

        SetActiveUser(FakeActiveUserContext.Anonymous());
        using IServiceScope scope = Factory.Services.CreateScope();
        TreeSearchResultDto result = await TreeSearch(scope).TraverseAsync(new TreeSearchRequest
        {
            RootUserId = voucher, MaxDegrees = 1, EdgeTypes = [TreeSearchEdgeType.Vouch],
        });

        result.Hits.Select(h => h.StoryId).Should().BeEquivalentTo([published1, published2],
            "a vouch projects onto every PUBLISHED story of the vouchee — never drafts");
        result.Hits.Should().OnlyContain(h => h.Degree == 1);
        _ = draft; // documented negative: the draft never appears
    }
}
