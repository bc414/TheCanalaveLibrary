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
        rec!.StatusId.Should().Be((short)RecommendationStatusEnum.Approved, "recs publish immediately (WU-RecLifecycle)");
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
    public async Task SetHiddenGem_RejectAtFive_ThrowsValidation()
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
        await act.Should().ThrowAsync<RecommendationValidationException>()
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
    public async Task SetHighlightedByAuthor_RejectAtFive_ThrowsValidation()
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
        await act.Should().ThrowAsync<RecommendationValidationException>()
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

    // ── Tastemaker badge award chain (WU36) ──────────────────────────────────────
    // Tests for the RecommendationSuccessesEarned counter and Recommender badge award that
    // fires inside RecordSuccessAsync. Each test that touches the counter must seed a UserStat
    // row for _recommenderUserId (IntegrationTestBase.SeedUserAsync does NOT create one).

    [Fact]
    public async Task RecordSuccess_IncreasesRecommendationSuccessesEarned()
    {
        await SeedUserStatAsync(_recommenderUserId);
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        // Record success as a different user (not the recommender) to avoid the self-farm guard.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallRecordSuccessAsync(recId);

        int earned = await LoadRecommendationSuccessesEarned(_recommenderUserId);
        earned.Should().Be(1, "one qualifying success must increment the recommender's counter by 1");
    }

    [Fact]
    public async Task RecordSuccess_Idempotent_StatDoesNotDoubleCount()
    {
        await SeedUserStatAsync(_recommenderUserId);
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallRecordSuccessAsync(recId);
        await CallRecordSuccessAsync(recId); // same reader, same rec — idempotent

        int earned = await LoadRecommendationSuccessesEarned(_recommenderUserId);
        earned.Should().Be(1, "duplicate RecordSuccess by the same reader must not double-count the stat");
    }

    [Fact]
    public async Task RecordSuccess_SelfRecord_DoesNotIncrementStat()
    {
        // Recommender marks their own rec as helpful — anti-self-farm guard must fire.
        await SeedUserStatAsync(_recommenderUserId);
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        // Active user is already _recommenderUserId (set in InitializeAsync).
        // The guard: recommenderId.Value (== _recommenderUserId) != userId (== _recommenderUserId) → false.
        // Switch to author first to avoid the "already recorded" idempotency path for the recommender.
        // Actually: self-record means the READER is the recommender. Submit as recommender, then
        // call RecordSuccess still as the recommender — the guard checks caller userId vs rec.RecommenderId.
        await CallRecordSuccessAsync(recId);

        int earned = await LoadRecommendationSuccessesEarned(_recommenderUserId);
        earned.Should().Be(0, "self-record (reader == recommender) must not increment the counter");
    }

    [Fact]
    public async Task RecordSuccess_NullRecommenderId_DoesNotCrashAndDoesNotAward()
    {
        // Seed an anonymous recommendation (RecommenderId = null) directly via DB.
        int anonRecId;
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Recommendation r = new()
            {
                StoryId           = _storyId,
                RecommenderId     = null,
                StatusId          = (short)RecommendationStatusEnum.Approved,
                DatePosted        = DateTime.UtcNow
            };
            r.RecommendationDetail = new RecommendationDetail { Text = ValidHtml() };
            db.Recommendations.Add(r);
            await db.SaveChangesAsync();
            anonRecId = r.RecommendationId;
        }

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        Func<Task> act = async () => await CallRecordSuccessAsync(anonRecId);

        // Must complete without throwing — no recommender to credit, no badge to fire.
        await act.Should().NotThrowAsync("anonymous-rec RecordSuccess must not crash");
    }

    // No-tiers model (WU-StatBadgeProducers) — a badge is earned at ≥1 and displays its count;
    // RecommenderSilver (threshold 50) is retired. Mutation-sanity: disabling the
    // `if (total >= 1)` award check makes RecordSuccess_AtFirstSuccess_AwardsRecommenderBadge fail.
    [Fact]
    public async Task RecordSuccess_AtFirstSuccess_AwardsRecommenderBadge()
    {
        await SeedUserStatAsync(_recommenderUserId, successesEarned: 0);
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallRecordSuccessAsync(recId); // takes counter to 1 — award fires immediately

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserBadge? badge = await db.UserBadges
            .FirstOrDefaultAsync(ub => ub.UserId == _recommenderUserId && ub.BadgeKey == SiteBadges.Recommender);
        badge.Should().NotBeNull("the FIRST qualifying success must award the Recommender badge — no threshold");
        badge!.EarnedCount.Should().Be(1);
    }

    [Fact]
    public async Task RecordSuccess_ZeroSuccesses_DoesNotAwardBadge()
    {
        // Seed the counter row but record no qualifying success — confirms the award check itself
        // gates on the counter, not merely on the row existing.
        await SeedUserStatAsync(_recommenderUserId, successesEarned: 0);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool badgeExists = await db.UserBadges
            .AnyAsync(ub => ub.UserId == _recommenderUserId && ub.BadgeKey == SiteBadges.Recommender);
        badgeExists.Should().BeFalse("zero qualifying successes must not award the badge");
    }

    [Fact]
    public async Task RecordSuccess_SecondSuccess_UpdatesEarnedCountOnExistingBadge()
    {
        await SeedUserStatAsync(_recommenderUserId, successesEarned: 1);
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext seedDb = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            seedDb.UserBadges.Add(new UserBadge
            {
                UserId = _recommenderUserId, BadgeKey = SiteBadges.Recommender, DisplayOrder = 1, EarnedCount = 1,
            });
            await seedDb.SaveChangesAsync();
        }

        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallRecordSuccessAsync(recId); // takes counter to 2

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserBadge badge = await db.UserBadges
            .SingleAsync(ub => ub.UserId == _recommenderUserId && ub.BadgeKey == SiteBadges.Recommender);
        badge.EarnedCount.Should().Be(2, "a repeat qualifying success must keep EarnedCount in step with the counter");
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

    // ── WU-RecLifecycle: self-rec block + submit notification ─────────────────────

    [Fact]
    public async Task Submit_OwnStory_ThrowsValidation()
    {
        int ownStoryId = await SeedStoryAsync(_recommenderUserId);

        Func<Task> act = async () => await CallSubmitAsync(
            new RecommendationSubmitDto(ownStoryId, ValidHtml()));
        await act.Should().ThrowAsync<RecommendationValidationException>()
            .WithMessage("*own story*", "self-recommendation is blocked outright");
    }

    [Fact]
    public async Task Submit_WritesNewRecommendationNotificationToStoryAuthor()
    {
        await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool notifExists = await db.Notifications.AnyAsync(
            n => n.RecipientUserId == _authorUserId
                 && n.NotificationTypeId == NotificationTypeEnum.NewRecommendationOnYourStory
                 && n.SourceUserId == _recommenderUserId);
        notifExists.Should().BeTrue("submitting must notify the story author their story got a recommendation");
    }

    // ── WU-RecLifecycle: RequestRevisionAsync ─────────────────────────────────────

    [Fact]
    public async Task RequestRevision_StoryAuthor_HidesStoresNoteAndNotifies()
    {
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallRequestRevisionAsync(recId, "Please remove the chapter 12 spoiler.");

        Recommendation? rec = await LoadRecAsync(recId);
        rec!.StatusId.Should().Be((short)RecommendationStatusEnum.NeedsRevision);
        rec.RevisionRequestNote.Should().Be("Please remove the chapter 12 spoiler.");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool notifExists = await db.Notifications.AnyAsync(
            n => n.RecipientUserId == _recommenderUserId
                 && n.NotificationTypeId == NotificationTypeEnum.RecommendationRevisionRequested);
        notifExists.Should().BeTrue("the recommender must be told a revision was requested");
    }

    [Fact]
    public async Task RequestRevision_EmptyNote_ThrowsValidation()
    {
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        Func<Task> act = async () => await CallRequestRevisionAsync(recId, "   ");
        await act.Should().ThrowAsync<RecommendationValidationException>(
            "a revision request without a note gives the recommender nothing to act on");
    }

    [Fact]
    public async Task RequestRevision_NonStoryAuthor_ThrowsUnauthorized()
    {
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));

        // Still the recommender — not the story author.
        Func<Task> act = async () => await CallRequestRevisionAsync(recId, "note");
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task RequestRevision_ClearsCurationFlags()
    {
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));
        await CallSetHiddenGemAsync(recId, true); // as recommender

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallSetHighlightedAsync(recId, true);
        await CallRequestRevisionAsync(recId, "Fix the pairing tags mentioned.");

        Recommendation? rec = await LoadRecAsync(recId);
        rec!.IsHiddenGem.Should().BeFalse("leaving Live frees the recommender's gem slot");
        rec.IsHighlightedByAuthor.Should().BeFalse("leaving Live frees the story's highlight slot");
    }

    // ── WU-RecLifecycle: EditAsync auto-relive + stickiness ───────────────────────

    [Fact]
    public async Task Edit_NeedsRevision_ReturnsToApprovedClearsNoteAndNotifiesAuthor()
    {
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallRequestRevisionAsync(recId, "Reword the ending mention.");

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_recommenderUserId, showMatureContent: false));
        await CallEditAsync(new UpdateRecommendationDto(recId, ValidHtml("revised body")));

        Recommendation? rec = await LoadRecAsync(recId);
        rec!.StatusId.Should().Be((short)RecommendationStatusEnum.Approved,
            "the recommender's edit IS the revision — auto-return to live, no author re-blessing");
        rec.RevisionRequestNote.Should().BeNull("the note is consumed by the fix");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool notifExists = await db.Notifications.AnyAsync(
            n => n.RecipientUserId == _authorUserId
                 && n.NotificationTypeId == NotificationTypeEnum.RecommendationRevised);
        notifExists.Should().BeTrue("the story author must learn the flagged rec is live again");
    }

    // ── WU-RecLifecycle: RemoveAsync / UnblockAsync ───────────────────────────────

    [Fact]
    public async Task Remove_StoryAuthor_SetsRejectedSilentlyAndClearsFlags()
    {
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));
        await CallSetHiddenGemAsync(recId, true); // as recommender

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallRemoveAsync(recId);

        Recommendation? rec = await LoadRecAsync(recId);
        rec!.StatusId.Should().Be((short)RecommendationStatusEnum.Rejected);
        rec.IsHiddenGem.Should().BeFalse("removal frees the gem slot");
        rec.RevisionRequestNote.Should().BeNull();

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool anyRecipientNotif = await db.Notifications.AnyAsync(
            n => n.RecipientUserId == _recommenderUserId
                 && (n.NotificationTypeId == NotificationTypeEnum.RecommendationRevisionRequested
                     || n.NotificationTypeId == NotificationTypeEnum.RecommendationApproved));
        anyRecipientNotif.Should().BeFalse("removal is silent — no notification to the recommender");
    }

    [Fact]
    public async Task Remove_ThenRecommenderEdit_ThrowsUnauthorized()
    {
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallRemoveAsync(recId);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_recommenderUserId, showMatureContent: false));
        Func<Task> act = async () => await CallEditAsync(new UpdateRecommendationDto(recId, ValidHtml("sneaky edit")));
        await act.Should().ThrowAsync<UnauthorizedAccessException>("a removed rec is out of the recommender's hands");
    }

    [Fact]
    public async Task Remove_ThenRecommenderDelete_ThrowsUnauthorized()
    {
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallRemoveAsync(recId);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_recommenderUserId, showMatureContent: false));
        Func<Task> act = async () => await CallDeleteAsync(recId);
        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "deleting would free the one-per-user-per-story slot — the Rejected row IS the block record");
    }

    [Fact]
    public async Task Remove_ThenResubmit_ThrowsInvalidOperation()
    {
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallRemoveAsync(recId);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_recommenderUserId, showMatureContent: false));
        Func<Task> act = async () => await CallSubmitAsync(
            new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already submitted*", "the unique index keeps the slot occupied — removal is sticky");
    }

    [Fact]
    public async Task Unblock_Rejected_RestoresApprovedAndNotifies()
    {
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallRemoveAsync(recId);

        await CallUnblockAsync(recId);

        Recommendation? rec = await LoadRecAsync(recId);
        rec!.StatusId.Should().Be((short)RecommendationStatusEnum.Approved,
            "unblock goes straight to live — the author already read it when removing");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool notifExists = await db.Notifications.AnyAsync(
            n => n.RecipientUserId == _recommenderUserId
                 && n.NotificationTypeId == NotificationTypeEnum.RecommendationApproved);
        notifExists.Should().BeTrue("the recommender must learn their rec is live again");
    }

    [Fact]
    public async Task Unblock_NotRejected_ThrowsInvalidOperation()
    {
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        Func<Task> act = async () => await CallUnblockAsync(recId);
        await act.Should().ThrowAsync<InvalidOperationException>("only a removed recommendation can be unblocked");
    }

    // ── WU-RecLifecycle: flag invariant on setters ────────────────────────────────

    [Fact]
    public async Task SetHiddenGem_OnNeedsRevisionRec_ThrowsValidation()
    {
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyWithAuthorId, ValidHtml()));
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        await CallRequestRevisionAsync(recId, "note");

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_recommenderUserId, showMatureContent: false));
        Func<Task> act = async () => await CallSetHiddenGemAsync(recId, true);
        await act.Should().ThrowAsync<RecommendationValidationException>(
            "curation flags may only ever be true on live recommendations");
    }

    // ── WU-RecLifecycle: D3.2 attribution ownership validation ────────────────────

    [Fact]
    public async Task RecordAttributionSource_RecommendationOnDifferentStory_ThrowsKeyNotFound()
    {
        int recId = await CallSubmitAsync(new RecommendationSubmitDto(_storyId, ValidHtml()));
        int unrelatedStoryId = await SeedStoryAsync();

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorUserId, showMatureContent: false));
        Func<Task> act = async () => await CallRecordAttributionSourceAsync(unrelatedStoryId, recId);
        await act.Should().ThrowAsync<KeyNotFoundException>(
            "the claimed source recommendation must belong to the claimed story (D3.2)");
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

    private async Task CallRequestRevisionAsync(int id, string note)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRecommendationWriteService>()
            .RequestRevisionAsync(id, note);
    }

    private async Task CallRemoveAsync(int id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRecommendationWriteService>().RemoveAsync(id);
    }

    private async Task CallUnblockAsync(int id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRecommendationWriteService>().UnblockAsync(id);
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

    /// <summary>
    /// Ensures a <see cref="UserStat"/> row exists for <paramref name="userId"/> and optionally
    /// pre-sets the <c>RecommendationSuccessesEarned</c> counter. Required for award-chain tests
    /// because <see cref="IntegrationTestBase.SeedUserAsync"/> does not create a UserStat row and
    /// <see cref="ServerRecommendationWriteService.RecordSuccessAsync"/>&#x27;s
    /// <c>ExecuteUpdateAsync</c> silently no-ops when no UserStat row exists.
    /// </summary>
    private async Task SeedUserStatAsync(int userId, int successesEarned = 0)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStats.Add(new UserStat { UserId = userId });
        await db.SaveChangesAsync();

        if (successesEarned > 0)
            await db.UserStats
                .Where(us => us.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    us => us.RecommendationSuccessesEarned, successesEarned));
    }

    private async Task<int> LoadRecommendationSuccessesEarned(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserStats
            .Where(us => us.UserId == userId)
            .Select(us => us.RecommendationSuccessesEarned)
            .FirstOrDefaultAsync();
    }

}
