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
/// Two of the eleven projection sites are driven here (the profile header and the recommendation
/// card); all eleven share the one predicate. <b>Per-test seeding:</b> the badge holder, a viewer and a
/// story author (SeedUserAsync); a story (SeedStoryAsync); <c>UserBadge</c> rows inline (the
/// <c>badges</c> catalogue is HasData-seeded, so the FK parents survive Respawn); an Approved
/// recommendation inline (status 2 via HasData, detail row required).
/// </para>
/// Tier: Integration (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class BadgeDisplayTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _holderId;
    private int _viewerId;
    private int _storyId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _holderId = await SeedUserAsync("Holder");
        _viewerId = await SeedUserAsync("Viewer");
        _storyId = await SeedStoryAsync(await SeedUserAsync("StoryAuthor"));

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
        await db.SaveChangesAsync();
    }

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
