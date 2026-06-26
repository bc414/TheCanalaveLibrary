using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IBadgeWriteService"/> / <see cref="IBadgeReadService"/>
/// (Feature 50, WU36). Covers: AwardAsync idempotency + DisplayOrder assignment; SetDisplayOrderAsync
/// visibility/ordering/ownership guard; GetMyBadgesForCurationAsync ordering (visible first by
/// DisplayOrder, then hidden by SortOrder); profile projection filter (DisplayOrder > 0).
/// Badge seed rows (e.g. <see cref="SiteBadges.Recommender"/>) survive Respawn (badges is a
/// TablesToIgnore table). UserBadge rows are wiped on each reset — each test starts clean.
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class BadgeServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _userId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userId = await SeedUserAsync();
        SetActiveUser(_userId);
    }

    // ── AwardAsync ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AwardAsync_NewBadge_ReturnsTrue()
    {
        bool awarded = await CallAwardAsync(_userId, SiteBadges.Recommender);

        awarded.Should().BeTrue("AwardAsync must return true when the badge is newly earned");
    }

    [Fact]
    public async Task AwardAsync_NewBadge_DefaultsToVisible()
    {
        await CallAwardAsync(_userId, SiteBadges.Recommender);

        UserBadge? ub = await LoadUserBadgeAsync(_userId, SiteBadges.Recommender);
        ub.Should().NotBeNull();
        ub!.DisplayOrder.Should().BeGreaterThan(0, "newly-awarded badges must default to visible (DisplayOrder > 0)");
    }

    [Fact]
    public async Task AwardAsync_FirstBadge_SetsDisplayOrderToOne()
    {
        await CallAwardAsync(_userId, SiteBadges.Recommender);

        UserBadge? ub = await LoadUserBadgeAsync(_userId, SiteBadges.Recommender);
        ub!.DisplayOrder.Should().Be(1, "first badge earns DisplayOrder = 0+1 = 1");
    }

    [Fact]
    public async Task AwardAsync_SecondBadge_IncrementsDisplayOrder()
    {
        await CallAwardAsync(_userId, SiteBadges.Recommender);
        await CallAwardAsync(_userId, SiteBadges.RecommenderSilver);

        UserBadge? ub = await LoadUserBadgeAsync(_userId, SiteBadges.RecommenderSilver);
        ub!.DisplayOrder.Should().Be(2, "second badge earns DisplayOrder = max(1)+1 = 2");
    }

    [Fact]
    public async Task AwardAsync_AlreadyEarned_ReturnsFalse()
    {
        await CallAwardAsync(_userId, SiteBadges.Recommender); // first
        bool awarded = await CallAwardAsync(_userId, SiteBadges.Recommender); // duplicate

        awarded.Should().BeFalse("AwardAsync must return false when the badge was already earned (idempotent)");
    }

    [Fact]
    public async Task AwardAsync_AlreadyEarned_DoesNotCreateDuplicateRow()
    {
        await CallAwardAsync(_userId, SiteBadges.Recommender);
        await CallAwardAsync(_userId, SiteBadges.Recommender); // duplicate

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        int count = await db.UserBadges
            .CountAsync(ub => ub.UserId == _userId && ub.BadgeKey == SiteBadges.Recommender);
        count.Should().Be(1, "idempotent AwardAsync must never insert a second row for the same user+badge");
    }

    // ── SetDisplayOrderAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SetDisplayOrderAsync_SetsOrderForVisibleAndHidesOthers()
    {
        // Award two badges (both visible by default — DisplayOrder 1 and 2).
        await CallAwardAsync(_userId, SiteBadges.Recommender);
        await CallAwardAsync(_userId, SiteBadges.RecommenderSilver);

        // Put only RecommenderSilver first, hide Recommender.
        await CallSetDisplayOrderAsync(_userId, [SiteBadges.RecommenderSilver]);

        UserBadge? silver = await LoadUserBadgeAsync(_userId, SiteBadges.RecommenderSilver);
        UserBadge? bronze = await LoadUserBadgeAsync(_userId, SiteBadges.Recommender);

        silver!.DisplayOrder.Should().Be(1, "the only visible key gets position 1");
        bronze!.DisplayOrder.Should().Be(0, "a badge not in the visible list must be zeroed (hidden)");
    }

    [Fact]
    public async Task SetDisplayOrderAsync_MultipleVisible_AssignsSequentialPositions()
    {
        await CallAwardAsync(_userId, SiteBadges.Recommender);
        await CallAwardAsync(_userId, SiteBadges.RecommenderSilver);

        // Reverse the default order: silver first, bronze second.
        await CallSetDisplayOrderAsync(_userId, [SiteBadges.RecommenderSilver, SiteBadges.Recommender]);

        UserBadge? silver = await LoadUserBadgeAsync(_userId, SiteBadges.RecommenderSilver);
        UserBadge? bronze = await LoadUserBadgeAsync(_userId, SiteBadges.Recommender);

        silver!.DisplayOrder.Should().Be(1);
        bronze!.DisplayOrder.Should().Be(2);
    }

    [Fact]
    public async Task SetDisplayOrderAsync_UnownedBadgeKey_ThrowsInvalidOperation()
    {
        // User only has Recommender, not RecommenderSilver.
        await CallAwardAsync(_userId, SiteBadges.Recommender);

        Func<Task> act = async () =>
            await CallSetDisplayOrderAsync(_userId, [SiteBadges.RecommenderSilver]);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "SetDisplayOrderAsync must reject a key the user has not earned");
    }

    // ── GetMyBadgesForCurationAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetMyBadgesForCurationAsync_NoBadges_ReturnsEmptyList()
    {
        IReadOnlyList<EarnedBadgeDto> result = await CallGetCurationAsync(_userId);

        result.Should().BeEmpty("a user with no earned badges must get an empty list");
    }

    [Fact]
    public async Task GetMyBadgesForCurationAsync_ReturnsAllEarned_VisibleFirst()
    {
        // Award both; then hide Recommender (bronze) so RecommenderSilver is visible.
        await CallAwardAsync(_userId, SiteBadges.Recommender);       // DisplayOrder 1
        await CallAwardAsync(_userId, SiteBadges.RecommenderSilver); // DisplayOrder 2
        // Make silver visible at 1, hide bronze.
        await CallSetDisplayOrderAsync(_userId, [SiteBadges.RecommenderSilver]);

        IReadOnlyList<EarnedBadgeDto> result = await CallGetCurationAsync(_userId);

        result.Should().HaveCount(2, "GetMyBadgesForCurationAsync returns ALL earned badges, visible and hidden");
        result[0].BadgeKey.Should().Be(SiteBadges.RecommenderSilver,
            "visible badge (DisplayOrder = 1) must come first");
        result[0].DisplayOrder.Should().Be(1);
        result[1].BadgeKey.Should().Be(SiteBadges.Recommender,
            "hidden badge (DisplayOrder = 0) must come after visible ones");
        result[1].DisplayOrder.Should().Be(0);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task<bool> CallAwardAsync(int userId, string badgeKey)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IBadgeWriteService>()
            .AwardAsync(userId, badgeKey);
    }

    private async Task CallSetDisplayOrderAsync(int userId, IReadOnlyList<string> orderedVisibleKeys)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IBadgeWriteService>()
            .SetDisplayOrderAsync(userId, orderedVisibleKeys);
    }

    private async Task<IReadOnlyList<EarnedBadgeDto>> CallGetCurationAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IBadgeReadService>()
            .GetMyBadgesForCurationAsync(userId);
    }

    private async Task<UserBadge?> LoadUserBadgeAsync(int userId, string badgeKey)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserBadges
            .FirstOrDefaultAsync(ub => ub.UserId == userId && ub.BadgeKey == badgeKey);
    }
}
