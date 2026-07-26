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
    public async Task GetForStory_ApprovedOnly_ExcludesNeedsRevisionAndRejected()
    {
        // Seed approved + a NeedsRevision (StatusId=1) rec via DB (WU-RecLifecycle statuses).
        int approvedId = await SeedRecAsync(_userId, _storyId, statusId: RecommendationStatusEnum.Approved);
        await SeedRecAsync(_otherUserId, _storyId, statusId: RecommendationStatusEnum.NeedsRevision); // hidden — must not appear

        SetActiveUser(FakeActiveUserContext.Anonymous());
        List<RecommendationDto> recs = await CallGetForStoryAsync(_storyId);

        recs.Should().ContainSingle(r => r.RecommendationId == approvedId,
            "only approved recommendations are returned to public viewers");
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

    // ── WU-RecLifecycle: per-viewer visibility of hidden recs ─────────────────────

    [Fact]
    public async Task GetForStory_StoryAuthor_SeesNeedsRevisionAndRejectedRecs()
    {
        int authorId = await SeedUserAsync();
        int storyId = await SeedStoryAsync(authorId);
        int needsRevisionId = await SeedRecAsync(_userId, storyId, statusId: RecommendationStatusEnum.NeedsRevision, note: "fix the spoiler");
        int rejectedId = await SeedRecAsync(_otherUserId, storyId, statusId: RecommendationStatusEnum.Rejected);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(authorId, showMatureContent: false));
        List<RecommendationDto> recs = await CallGetForStoryAsync(storyId);

        recs.Should().Contain(r => r.RecommendationId == needsRevisionId)
            .Which.RevisionRequestNote.Should().Be("fix the spoiler",
                "the author sees the note they attached");
        recs.Should().Contain(r => r.RecommendationId == rejectedId,
            "the author sees removed recs (to unblock them)");
    }

    [Fact]
    public async Task GetForStory_Recommender_SeesOwnHiddenRecWithNote()
    {
        int hiddenId = await SeedRecAsync(_userId, _storyId, statusId: RecommendationStatusEnum.NeedsRevision, note: "reword the ending mention");
        await SeedRecAsync(_otherUserId, _storyId, statusId: RecommendationStatusEnum.NeedsRevision, note: "someone else's note");

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_userId, showMatureContent: false));
        List<RecommendationDto> recs = await CallGetForStoryAsync(_storyId);

        recs.Should().ContainSingle(r => r.RecommendationId == hiddenId,
                "a recommender sees their OWN hidden rec but not other users' hidden recs")
            .Which.RevisionRequestNote.Should().Be("reword the ending mention");
    }

    [Fact]
    public async Task GetForStory_PublicViewer_StatusIsApprovedAndNoteIsNull()
    {
        int approvedId = await SeedRecAsync(_userId, _storyId, statusId: RecommendationStatusEnum.Approved);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        List<RecommendationDto> recs = await CallGetForStoryAsync(_storyId);

        RecommendationDto rec = recs.Single(r => r.RecommendationId == approvedId);
        rec.Status.Should().Be(RecommendationStatusEnum.Approved);
        rec.RevisionRequestNote.Should().BeNull("the note never leaks to public viewers");
    }

    // ── WU-RecLifecycle: D1 regression — profile-tab candidate ids are Approved-only ──

    [Fact]
    public async Task GetRecommendedStoryIdsByUser_ExcludesNeedsRevisionAndRejected()
    {
        int approvedStory = await SeedStoryAsync();
        int hiddenStory = await SeedStoryAsync();
        int rejectedStory = await SeedStoryAsync();
        await SeedRecAsync(_userId, approvedStory, statusId: RecommendationStatusEnum.Approved);
        await SeedRecAsync(_userId, hiddenStory, statusId: RecommendationStatusEnum.NeedsRevision);
        await SeedRecAsync(_userId, rejectedStory, statusId: RecommendationStatusEnum.Rejected);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: false));
        IReadOnlyList<int> ids = await CallGetRecommendedStoryIdsByUserAsync(_userId);

        ids.Should().Contain(approvedStory);
        ids.Should().NotContain(hiddenStory,
            "a NeedsRevision rec's story id must not leak onto the public profile tab (D1)");
        ids.Should().NotContain(rejectedStory,
            "a Rejected rec's story id must not leak onto the public profile tab (D1)");
    }

    // ── WU-RecLifecycle: bookshelf "Needs attention" ──────────────────────────────

    [Fact]
    public async Task GetMyRecommendationsNeedingAttention_ReturnsOwnHiddenRecsWithNote()
    {
        int hiddenStory = await SeedStoryAsync();
        int rejectedStory = await SeedStoryAsync();
        int approvedStory = await SeedStoryAsync();
        int hiddenId = await SeedRecAsync(_userId, hiddenStory, statusId: RecommendationStatusEnum.NeedsRevision, note: "trim the summary spoilers");
        int rejectedId = await SeedRecAsync(_userId, rejectedStory, statusId: RecommendationStatusEnum.Rejected);
        await SeedRecAsync(_userId, approvedStory, statusId: RecommendationStatusEnum.Approved);
        await SeedRecAsync(_otherUserId, hiddenStory, statusId: RecommendationStatusEnum.NeedsRevision); // not mine

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_userId, showMatureContent: false));
        List<RecommendationDto> recs = await CallGetMyNeedingAttentionAsync();

        recs.Should().HaveCount(2, "own non-Approved recs only — Approved and other users' excluded");
        recs.Should().Contain(r => r.RecommendationId == hiddenId)
            .Which.RevisionRequestNote.Should().Be("trim the summary spoilers");
        recs.Should().Contain(r => r.RecommendationId == rejectedId);
    }

    [Fact]
    public async Task GetMyRecommendationsNeedingAttention_Anonymous_ReturnsEmpty()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        List<RecommendationDto> recs = await CallGetMyNeedingAttentionAsync();
        recs.Should().BeEmpty();
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
        bool isHighlighted = false, string? note = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Recommendation rec = new()
        {
            StoryId               = storyId,
            RecommenderId         = recommenderId,
            StatusId              = (short)statusId,
            IsHighlightedByAuthor = isHighlighted,
            RevisionRequestNote   = note,
            DatePosted            = DateTime.UtcNow
        };
        rec.RecommendationDetail = new RecommendationDetail { Text = new string('x', 500) };
        db.Recommendations.Add(rec);
        await db.SaveChangesAsync();
        return rec.RecommendationId;
    }

    private async Task<IReadOnlyList<int>> CallGetRecommendedStoryIdsByUserAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRecommendationReadService>()
            .GetRecommendedStoryIdsByUserAsync(userId);
    }

    private async Task<List<RecommendationDto>> CallGetMyNeedingAttentionAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRecommendationReadService>()
            .GetMyRecommendationsNeedingAttentionAsync();
    }
}
