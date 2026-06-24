using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IRecommendationWriteService"/> (WU29). Covers:
/// min-length reject; auto-approve on submit; one-per-user unique-violation friendly error;
/// edit/delete author-only + anonymous guard; like toggle count round-trip; Hidden-Gem reject-at-5;
/// highlight reject-at-5/story; RecordSuccessAsync idempotency + SuccessfulRecCount increment;
/// attribution-source row; end-to-end Hidden-Gem → notification row.
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class RecommendationWriteServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorUserId;       // story author
    private int _recommenderUserId;  // the user writing the recommendation
    private int _storyId;
    private int _storyWithAuthorId;  // story where _authorUserId is the explicit AuthorId

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorUserId = await SeedUserAsync();
        _recommenderUserId = await SeedUserAsync();
        _storyId = await SeedStoryAsync();
        _storyWithAuthorId = await SeedStoryAsync(_authorUserId);
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_recommenderUserId, showMatureContent: false));
    }

    // ── SubmitAsync ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_ValidBody_InsertsApprovedRecommendation()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        Recommendation? rec = await LoadRecAsync(id);
        rec.Should().NotBeNull();
        rec!.StatusId.Should().Be((short)RecommendationStatusEnum.Approved, "auto-approved on submit (MVP)");
        rec.RecommenderId.Should().Be(_recommenderUserId);
        rec.StoryId.Should().Be(_storyId);
    }

    [Fact]
    public async Task Submit_ValidBody_PersistsBodyInRecommendationDetail()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml("unique content")));

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        RecommendationDetail? detail = await db.RecommendationDetails
            .FirstOrDefaultAsync(d => d.RecommendationId == id);
        detail.Should().NotBeNull();
        detail!.Text.Should().Contain("unique content");
    }

    [Fact]
    public async Task Submit_TooShort_ThrowsValidationException()
    {
        Func<Task> act = async () => await CallSubmitAsync(
            new RecommendationSubmitDto(_storyId, "<p>Too short.</p>"));
        await act.Should().ThrowAsync<RecommendationValidationException>();
    }

    [Fact]
    public async Task Submit_ScriptTag_IsStrippedBySanitizer()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(
            _storyId, ValidHtml("safe text") + "<script>evil()</script>"));

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        RecommendationDetail? detail = await db.RecommendationDetails
            .FirstOrDefaultAsync(d => d.RecommendationId == id);
        detail!.Text.Should().NotContain("script");
    }

    [Fact]
    public async Task Submit_DuplicateUserStory_ThrowsInvalidOperation()
    {
        await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        Func<Task> act = async () => await CallSubmitAsync(
            new RecommendationSubmitDto(_storyId, ValidHtml()));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already submitted*");
    }

    [Fact]
    public async Task Submit_Anonymous_ThrowsInvalidOperation()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        Func<Task> act = async () => await CallSubmitAsync(
            new RecommendationSubmitDto(_storyId, ValidHtml()));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── EditAsync ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Edit_Author_UpdatesBody()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml("original")));

        await CallEditAsync(new UpdateRecommendationDto(id, ValidHtml("edited text xyz")));

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        RecommendationDetail? detail = await db.RecommendationDetails
            .FirstOrDefaultAsync(d => d.RecommendationId == id);
        detail!.Text.Should().Contain("edited text xyz");
    }

    [Fact]
    public async Task Edit_NonAuthor_ThrowsUnauthorized()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        Func<Task> act = async () => await CallEditAsync(
            new UpdateRecommendationDto(id, ValidHtml("attacker text")));
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Edit_Anonymous_ThrowsInvalidOperation()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.Anonymous());
        Func<Task> act = async () => await CallEditAsync(
            new UpdateRecommendationDto(id, ValidHtml()));
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_Author_RemovesRow()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        await CallDeleteAsync(id);

        Recommendation? rec = await LoadRecAsync(id);
        rec.Should().BeNull("hard delete must remove the row");
    }

    [Fact]
    public async Task Delete_NonAuthor_ThrowsUnauthorized()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        Func<Task> act = async () => await CallDeleteAsync(id);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── ToggleLikeAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ToggleLike_FirstLike_IncreasesCountAndReturnsIsLikedTrue()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        // Like as author user.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        RecommendationLikeResultDto result = await CallToggleLikeAsync(id);

        result.IsLiked.Should().BeTrue();
        result.LikeCount.Should().Be(1);

        Recommendation? rec = await LoadRecAsync(id);
        rec!.LikeCount.Should().Be(1);
    }

    [Fact]
    public async Task ToggleLike_Unlike_DecreasesCountAndReturnsIsLikedFalse()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallToggleLikeAsync(id); // like
        RecommendationLikeResultDto result = await CallToggleLikeAsync(id); // unlike

        result.IsLiked.Should().BeFalse();
        result.LikeCount.Should().Be(0);
    }

    [Fact]
    public async Task ToggleLike_Anonymous_ThrowsInvalidOperation()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.Anonymous());
        Func<Task> act = async () => await CallToggleLikeAsync(id);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── SetHiddenGemAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task SetHiddenGem_True_SetsFlag()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        await CallSetHiddenGemAsync(id, true);

        Recommendation? rec = await LoadRecAsync(id);
        rec!.IsHiddenGem.Should().BeTrue();
    }

    [Fact]
    public async Task SetHiddenGem_RejectAtFive_ThrowsInvalidOperation()
    {
        // After Respawn reset, count starts at 0. Use real service calls to fill the limit.
        // Use _recommenderUserId throughout — Respawn guarantees no prior HG rows.
        for (int i = 0; i < RecommendationConstants.MaxHiddenGemsPerUser; i++)
        {
            int sid = await SeedStoryAsync();
            int rid = await CallSubmitAsync(new RecommendationSubmitDto(sid, ValidHtml()));
            await CallSetHiddenGemAsync(rid, true);
        }

        // The (N+1)th must throw.
        int newStoryId = await SeedStoryAsync();
        int newRecId = await CallSubmitAsync(new RecommendationSubmitDto(newStoryId, ValidHtml()));

        Func<Task> act = async () => await CallSetHiddenGemAsync(newRecId, true);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*5*", "must cite the limit");
    }

    [Fact]
    public async Task SetHiddenGem_NonAuthor_ThrowsUnauthorized()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        Func<Task> act = async () => await CallSetHiddenGemAsync(id, true);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SetHiddenGem_True_WritesNotificationRow()
    {
        int sid = _storyWithAuthorId; // story whose AuthorId == _authorUserId
        int id = await CallSubmitAsync(new RecommendationSubmitDto(sid, ValidHtml()));

        await CallSetHiddenGemAsync(id, true);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool notifExists = await db.Notifications.AnyAsync(
            n => n.RecipientUserId == _authorUserId
                 && n.NotificationTypeId == NotificationTypeEnum.HiddenGem);
        notifExists.Should().BeTrue("setting a Hidden Gem must fire a notification to the story author");
    }

    // ── SetHighlightedByAuthorAsync ───────────────────────────────────────────────

    [Fact]
    public async Task SetHighlightedByAuthor_StoryAuthor_SetsFlag()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallSetHighlightedAsync(id, true);

        Recommendation? rec = await LoadRecAsync(id);
        rec!.IsHighlightedByAuthor.Should().BeTrue();
    }

    [Fact]
    public async Task SetHighlightedByAuthor_NonAuthor_ThrowsUnauthorized()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));

        // Still as recommender (not the story author).
        Func<Task> act = async () => await CallSetHighlightedAsync(id, true);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task SetHighlightedByAuthor_RejectAtFive_ThrowsInvalidOperation()
    {
        // Recommender submits 6 recommendations on the same story — can't because of unique constraint.
        // Instead: create 6 different recommender users' recommendations (or use 5 diff stories).
        // Simpler: seed 5 recs by different seeded users and spotlight each, then try a 6th.
        // To keep test self-contained: set up a story with AuthorId = _authorUserId,
        // then seed 5 recs via DB directly (bypassing unique constraint).
        using IServiceScope dbScope = Factory.Services.CreateScope();
        ApplicationDbContext db = dbScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        for (int i = 0; i < RecommendationConstants.MaxHighlightedPerStory; i++)
        {
            Recommendation r = new()
            {
                StoryId          = _storyWithAuthorId,
                RecommenderId    = null, // anonymous seed — avoids unique FK conflict
                StatusId         = (short)RecommendationStatusEnum.Approved,
                IsHighlightedByAuthor = true,
                DatePosted       = DateTime.UtcNow
            };
            r.RecommendationDetail = new RecommendationDetail { Text = ValidHtml() };
            db.Recommendations.Add(r);
        }
        await db.SaveChangesAsync();

        // Now submit a 6th real rec as recommender and try to spotlight it.
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        Func<Task> act = async () => await CallSetHighlightedAsync(recId, true);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*5*");
    }

    // ── RecordSuccessAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordSuccess_FirstCall_IncrementsSuccessfulRecCount()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallRecordSuccessAsync(id);

        Recommendation? rec = await LoadRecAsync(id);
        rec!.SuccessfulRecCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordSuccess_Idempotent_DoesNotDoubleCount()
    {
        int id = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallRecordSuccessAsync(id);
        await CallRecordSuccessAsync(id); // second call for same user — idempotent

        Recommendation? rec = await LoadRecAsync(id);
        rec!.SuccessfulRecCount.Should().Be(1, "duplicate RecordSuccess for same user must not double-count");
    }

    // ── RecordAttributionSourceAsync ──────────────────────────────────────────────

    [Fact]
    public async Task RecordAttributionSource_WritesSourceRow()
    {
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));

        // UserStoryRecommendationSource has a composite FK to UserStoryInteractions (UserId, StoryId).
        // In real flow, opening the story creates the USI row before attribution is ever recorded.
        // Seed it explicitly here (testing.md "FK parent rows" rule).
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            bool usiExists = await db.UserStoryInteractions
                .AnyAsync(u => u.UserId == _authorUserId && u.StoryId == _storyId);
            if (!usiExists)
            {
                db.UserStoryInteractions.Add(new UserStoryInteraction { UserId = _authorUserId, StoryId = _storyId });
                await db.SaveChangesAsync();
            }
        }

        await CallRecordAttributionSourceAsync(_storyId, recId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext assertDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool exists = await assertDb.UserStoryRecommendationSources
            .AnyAsync(s => s.UserId == _authorUserId && s.StoryId == _storyId
                           && s.SourceRecommendationId == recId);
        exists.Should().BeTrue();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static string ValidHtml(string? suffix = null)
    {
        // 496 'a' chars + " end." (5 chars) = 501 plain-text chars, safely above the 500-char minimum.
        string text = new string('a', 496) + (suffix ?? " end.");
        return $"<p>{text}</p>";
    }

    private async Task<int> CallSubmitAsync(RecommendationSubmitDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRecommendationWriteService>()
            .SubmitAsync(dto);
    }

    private async Task CallEditAsync(UpdateRecommendationDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRecommendationWriteService>().EditAsync(dto);
    }

    private async Task CallDeleteAsync(int id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRecommendationWriteService>().DeleteAsync(id);
    }

    private async Task<RecommendationLikeResultDto> CallToggleLikeAsync(int id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRecommendationWriteService>()
            .ToggleLikeAsync(id);
    }

    private async Task CallSetHiddenGemAsync(int id, bool isHiddenGem)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRecommendationWriteService>()
            .SetHiddenGemAsync(id, isHiddenGem);
    }

    private async Task CallSetHighlightedAsync(int id, bool isHighlighted)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRecommendationWriteService>()
            .SetHighlightedByAuthorAsync(id, isHighlighted);
    }

    private async Task CallRecordSuccessAsync(int id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRecommendationWriteService>()
            .RecordSuccessAsync(id);
    }

    private async Task CallRecordAttributionSourceAsync(int storyId, int recId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRecommendationWriteService>()
            .RecordAttributionSourceAsync(storyId, recId);
    }

    private async Task<Recommendation?> LoadRecAsync(int id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Recommendations.FirstOrDefaultAsync(r => r.RecommendationId == id);
    }

}
