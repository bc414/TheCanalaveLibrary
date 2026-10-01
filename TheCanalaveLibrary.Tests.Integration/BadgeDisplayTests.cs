using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Zero-count counter-backed badges are hidden at display (owner ruling D21, WU-CounterSymmetry): a
/// <c>UserBadge</c> whose <c>EarnedCount</c> recomputed to 0 stays in the table — the recalc never
/// deletes it — but every display projection drops it when its key is counter-backed
/// (<see cref="SiteBadges.CounterBackedKeys"/>). A manual grant carries <c>EarnedCount = 0</c> by
/// design and always shows; the owner's curation read returns everything.
/// <para>
/// All eleven projection sites apply the one shared expression <see cref="SiteBadges.IsDisplayed"/>.
/// Six sites, one from each read-service family, are driven here: the profile header, user search, the
/// recommendation card, the following list, the outgoing-vouch list and the tree-search author card.
/// <b>Per-test seeding:</b> the badge holder, a viewer and a story author (SeedUserAsync); a story by the
/// story author and one by the holder (SeedStoryAsync); <c>UserBadge</c> rows inline (the <c>badges</c>
/// catalogue is HasData-seeded, so the FK parents survive Respawn); an Approved recommendation inline
/// (status 2 via HasData, detail row required); the viewer's follow and vouch of the holder inline.
/// </para>
/// Tier: Integration (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class BadgeDisplayTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _holderId;
    private int _viewerId;
    private int _storyId;
    private int _holderStoryId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _holderId = await SeedUserAsync("Holder");
        _viewerId = await SeedUserAsync("Viewer");
        _storyId = await SeedStoryAsync(await SeedUserAsync("StoryAuthor"));
        _holderStoryId = await SeedStoryAsync(_holderId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserBadges.AddRange(
            // Counter-backed, recomputed to 0 (e.g. the recommendation behind it was deleted) — hidden.
            new UserBadge { UserId = _holderId, BadgeKey = SiteBadges.Recommender, DisplayOrder = 1, EarnedCount = 0, DateEarned = DateTime.UtcNow },
            // Manual grant: 0 by design — always shown.
            new UserBadge { UserId = _holderId, BadgeKey = SiteBadges.Patron, DisplayOrder = 2, EarnedCount = 0, DateEarned = DateTime.UtcNow },
            // Counter-backed with a real count — shown (the positive control for the key filter).
            new UserBadge { UserId = _holderId, BadgeKey = SiteBadges.BetaReader, DisplayOrder = 3, EarnedCount = 2, DateEarned = DateTime.UtcNow });
        db.Recommendations.Add(new Recommendation
        {
            StoryId = _storyId, RecommenderId = _holderId, StatusId = 2, DatePosted = DateTime.UtcNow,
            RecommendationDetail = new RecommendationDetail { Text = "<p>Read it.</p>" },
        });
        db.FollowedUsers.Add(new FollowedUser { UserId = _viewerId, FollowedUserId = _holderId, DateFollowed = DateTime.UtcNow });
        db.Vouches.Add(new Vouch { VouchingUserId = _viewerId, VouchedUserId = _holderId, DateVouched = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    private static readonly string[] Displayed = ["Patron", "Beta Reader"];

    [Fact]
    public async Task ProfileHeader_HidesAZeroCountCounterBackedBadge_ButShowsAZeroCountManualGrant()
    {
        SetActiveUser(_viewerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        ProfileHeaderDto? header = await scope.ServiceProvider.GetRequiredService<IUserProfileReadService>()
            .GetProfileHeaderAsync(_holderId, includePrivate: false);

        header!.Badges.Select(b => b.Name).Should().Equal(new[] { "Patron", "Beta Reader" },
            "the 0-count Recommender badge is hidden; Patron is a manual grant; Beta Reader has a count");
    }

    [Fact]
    public async Task RecommendationCard_HidesAZeroCountCounterBackedBadge_ButShowsAZeroCountManualGrant()
    {
        SetActiveUser(_viewerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        List<RecommendationDto> recs = await scope.ServiceProvider.GetRequiredService<IRecommendationReadService>()
            .GetForStoryAsync(_storyId);

        recs.Should().ContainSingle().Which.Recommender!.Badges.Select(b => b.Name)
            .Should().Equal("Patron", "Beta Reader");
    }

    [Fact]
    public async Task UserSearch_HidesAZeroCountCounterBackedBadge()
    {
        SetActiveUser(_viewerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        string holderName = (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.FindAsync(_holderId))!.UserName!;
        IReadOnlyList<UserCardDto> found = await scope.ServiceProvider.GetRequiredService<IUserProfileReadService>()
            .SearchUsersByNameAsync(holderName);

        found.Should().ContainSingle().Which.Badges.Select(b => b.Name).Should().Equal(Displayed);
    }

    [Fact]
    public async Task FollowingList_HidesAZeroCountCounterBackedBadge()
    {
        SetActiveUser(_viewerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        IReadOnlyList<UserCardDto> followed = await scope.ServiceProvider.GetRequiredService<IFollowingReadService>()
            .GetFollowedUsersAsync(_viewerId);

        followed.Should().ContainSingle().Which.Badges.Select(b => b.Name).Should().Equal(Displayed);
    }

    [Fact]
    public async Task OutgoingVouchList_HidesAZeroCountCounterBackedBadge()
    {
        SetActiveUser(_viewerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        IReadOnlyList<VouchDisplayDto> vouches = await scope.ServiceProvider.GetRequiredService<IFollowingReadService>()
            .GetOutgoingVouchesAsync(_viewerId);

        vouches.Should().ContainSingle().Which.User.Badges.Select(b => b.Name).Should().Equal(Displayed);
    }

    [Fact]
    public async Task TreeSearchAuthorCard_HidesAZeroCountCounterBackedBadge()
    {
        SetActiveUser(_viewerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        ManualTreeNeighborsDto pivot = await scope.ServiceProvider.GetRequiredService<IManualTreeSearchReadService>()
            .GetStoryNeighborsAsync(new StoryNeighborsRequest
            {
                StoryId = _holderStoryId, IncludeRecommendations = false, IncludeHiddenGems = false,
                IncludeSpotlights = false, IncludeFavoriters = false,
            });

        pivot.Author!.Badges.Select(b => b.Name).Should().Equal(Displayed);
    }

    [Fact]
    public async Task Curation_StillReturnsEveryRow_IncludingTheZeroCountCounterBackedBadge()
    {
        SetActiveUser(_holderId);
        using IServiceScope scope = Factory.Services.CreateScope();
        IReadOnlyList<EarnedBadgeDto> mine = await scope.ServiceProvider.GetRequiredService<IBadgeReadService>()
            .GetMyBadgesForCurationAsync(_holderId);

        mine.Select(b => b.BadgeKey).Should().BeEquivalentTo(
            new[] { SiteBadges.Recommender, SiteBadges.Patron, SiteBadges.BetaReader },
            "the owner's management view is not a display surface — the recalc keeps the row and so does this read");
    }
}
