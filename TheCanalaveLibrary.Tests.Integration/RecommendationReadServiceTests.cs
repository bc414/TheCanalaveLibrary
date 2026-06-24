using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IRecommendationReadService"/> (WU29). Covers:
/// Approved-only filter; spotlight-first ordering; per-viewer IsLikedByCurrentUser.
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class RecommendationReadServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _userId;
    private int _otherUserId;
    private int _storyId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userId = await SeedUserAsync();
        _otherUserId = await SeedUserAsync();
        _storyId = await SeedStoryAsync();
    }

    // ── Approved-only filter ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetForStory_ApprovedOnly_ExcludesPendingAndRejected()
    {
        // Seed approved + a pending (StatusId=1) rec via DB.
        int approvedId = await SeedRecAsync(_userId, _storyId, statusId: RecommendationStatusEnum.Approved);
        await SeedRecAsync(_otherUserId, _storyId, statusId: RecommendationStatusEnum.PendingApproval); // pending — must not appear

        SetActiveUser(FakeActiveUserContext.Anonymous());
        List<RecommendationDto> recs = await CallGetForStoryAsync(_storyId);

        recs.Should().ContainSingle(r => r.RecommendationId == approvedId,
            "only approved recommendations are returned");
    }

    // ── Spotlight ordering ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetForStory_SpotlightedFirst_ThenByDatePostedDescending()
    {
        // Seed three recs: two plain (older then newer), one spotlighted.
        int plainOldId = await SeedRecAsync(_userId, _storyId, statusId: RecommendationStatusEnum.Approved, isHighlighted: false);
        int plainNewId = await SeedRecAsync(_otherUserId, _storyId, statusId: RecommendationStatusEnum.Approved, isHighlighted: false);
        int highlightedId = await SeedRecAsync(null, _storyId, statusId: RecommendationStatusEnum.Approved, isHighlighted: true);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        List<RecommendationDto> recs = await CallGetForStoryAsync(_storyId);

        recs[0].RecommendationId.Should().Be(highlightedId, "spotlighted must come first");
        recs[1].RecommendationId.Should().Be(plainNewId, "among non-spotlighted, newest first");
        recs[2].RecommendationId.Should().Be(plainOldId);
    }

    // ── Per-viewer IsLikedByCurrentUser ───────────────────────────────────────────

    [Fact]
    public async Task GetForStory_ViewerHasLiked_IsLikedByCurrentUserTrue()
    {
        int recId = await SeedRecAsync(_otherUserId, _storyId, statusId: RecommendationStatusEnum.Approved);

        // Like via write service as userId.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_userId, showMatureContent: false));
        await CallToggleLikeAsync(recId);

        List<RecommendationDto> recs = await CallGetForStoryAsync(_storyId);
        recs.Should().ContainSingle(r => r.RecommendationId == recId)
            .Which.IsLikedByCurrentUser.Should().BeTrue();
    }

    [Fact]
    public async Task GetForStory_Anonymous_IsLikedByCurrentUserAlwaysFalse()
    {
        int recId = await SeedRecAsync(_userId, _storyId, statusId: RecommendationStatusEnum.Approved);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        List<RecommendationDto> recs = await CallGetForStoryAsync(_storyId);

        recs.First(r => r.RecommendationId == recId).IsLikedByCurrentUser.Should().BeFalse();
    }

    // ── IsOwnRecommendation ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetForStory_OwnRecommendation_IsOwnRecommendationTrue()
    {
        int recId = await SeedRecAsync(_userId, _storyId, statusId: RecommendationStatusEnum.Approved);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_userId, showMatureContent: false));
        List<RecommendationDto> recs = await CallGetForStoryAsync(_storyId);

        recs.First(r => r.RecommendationId == recId).IsOwnRecommendation.Should().BeTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task<List<RecommendationDto>> CallGetForStoryAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRecommendationReadService>()
            .GetForStoryAsync(storyId);
    }

    private async Task CallToggleLikeAsync(int id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRecommendationWriteService>()
            .ToggleLikeAsync(id);
    }

    private async Task<int> SeedRecAsync(
        int? recommenderId, int storyId, RecommendationStatusEnum statusId,
        bool isHighlighted = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Recommendation rec = new()
        {
            StoryId               = storyId,
            RecommenderId         = recommenderId,
            StatusId              = (short)statusId,
            IsHighlightedByAuthor = isHighlighted,
            DatePosted            = DateTime.UtcNow
        };
        rec.RecommendationDetail = new RecommendationDetail { Text = new string('x', 500) };
        db.Recommendations.Add(rec);
        await db.SaveChangesAsync();
        return rec.RecommendationId;
    }

}
