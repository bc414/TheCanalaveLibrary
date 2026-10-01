using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for recommendation attribution on the interaction write path (Feature 30, owner
/// ruling D3, WU-InertFeatures): the attribution is metadata on the <c>IsReadItLater</c> bit.
/// <list type="bullet">
///   <item><b>RIL from the card</b> (<c>SetReadItLaterFromRecommendationAsync</c>): creates the USI row,
///   its date partition and the attribution in one unit of work — with <b>no pre-existing USI row</b>,
///   the case the old on-load write FK-failed on; preserves the other bits; author gate; a bit already
///   set records nothing; first attribution wins; refusals for hidden stories / non-live recs /
///   anonymous callers.</item>
///   <item><b>Direct link</b> (<c>MarkStartedAsync(storyId, recId)</c>): records the attribution with
///   or without a prior row; a bogus parameter is silently ignored and HasStarted still lands.</item>
///   <item><b>Removal trigger 1</b>: RIL true→false deletes it; sparse cleanup cascades it; a
///   direct-link attribution with the bit never set survives an unrelated toggle; a re-RIL after a
///   clear starts a new attribution.</item>
/// </list>
/// Seeding (testing.md "FK parents"): every test seeds its users, story and recommendation via the
/// base helpers / an inline <see cref="ApplicationDbContext"/> insert; the USI parent row is either
/// absent on purpose (the FK-order pin) or seeded explicitly.
/// Tier: <b>Integration</b> (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class RecommendationAttributionTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _readerId;
    private int _authorId;
    private int _recommenderId;
    private int _storyId;
    private int _recId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _readerId = await SeedUserAsync("Reader");
        _authorId = await SeedUserAsync("StoryAuthor");
        _recommenderId = await SeedUserAsync("Recommender");
        _storyId = await SeedStoryAsync(_authorId);
        _recId = await SeedRecAsync(_storyId, _recommenderId);
        SetActiveUser(_readerId);
    }

    // ── RIL from the card ────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadItLaterFromCard_WithNoPriorRow_CreatesRowDatesAndAttribution_InOneSave()
    {
        await ReadItLaterFromCardAsync(_recId);

        UserStoryInteraction row = (await LoadRowAsync(_readerId, _storyId))!;
        row.IsReadItLater.Should().BeTrue();
        row.InteractionDatePartition!.ReadItLaterDate.Should().NotBeNull();
        row.RecommendationSource.Should().NotBeNull(
            "the FK-order pin: parent and attribution commit together, so no USI row has to pre-exist " +
            "(the old on-load write failed exactly here)");
        row.RecommendationSource!.SourceRecommendationId.Should().Be(_recId);
    }

    [Fact]
    public async Task ReadItLaterFromCard_PreservesTheOtherBits()
    {
        await SeedRowAsync(_readerId, _storyId, favorite: true, followed: true, hasStarted: true);

        await ReadItLaterFromCardAsync(_recId);

        UserStoryInteraction row = (await LoadRowAsync(_readerId, _storyId))!;
        row.IsReadItLater.Should().BeTrue();
        row.IsFavorite.Should().BeTrue("the card sets one bit — never the panel's six-bit absolute set");
        row.IsFollowed.Should().BeTrue();
        row.HasStarted.Should().BeTrue();
    }

    [Fact]
    public async Task ReadItLaterFromCard_ByTheStorysAuthor_SavesButRecordsNoAttribution()
    {
        SetActiveUser(_authorId);
        await ReadItLaterFromCardAsync(_recId);

        UserStoryInteraction row = (await LoadRowAsync(_authorId, _storyId))!;
        row.IsReadItLater.Should().BeTrue("D3: allow the RIL itself");
        row.RecommendationSource.Should().BeNull("D3 author gate: no attribution that could never be consumed");
    }

    [Fact]
    public async Task ReadItLaterFromCard_WhenTheBitIsAlreadySet_RecordsNoAttribution()
    {
        await SeedRowAsync(_readerId, _storyId, readItLater: true);

        await ReadItLaterFromCardAsync(_recId);

        (await LoadRowAsync(_readerId, _storyId))!.RecommendationSource.Should().BeNull(
            "the attribution records how the bit came to be set — it was set elsewhere first");
    }

    [Fact]
    public async Task ReadItLaterFromCard_WhenAnAttributionAlreadyExists_FirstWins()
    {
        int otherRec = await SeedRecAsync(_storyId, await SeedUserAsync("OtherRecommender"));
        // A direct-link reader: started via rec A, never saved the story.
        await MarkStartedAsync(_storyId, _recId);

        await ReadItLaterFromCardAsync(otherRec);

        UserStoryInteraction row = (await LoadRowAsync(_readerId, _storyId))!;
        row.IsReadItLater.Should().BeTrue();
        row.RecommendationSource!.SourceRecommendationId.Should().Be(_recId, "first attribution wins");
    }

    [Fact]
    public async Task ReRilAfterAClear_FromADifferentRecommendation_StartsANewAttribution()
    {
        int otherRec = await SeedRecAsync(_storyId, await SeedUserAsync("SecondRecommender"));
        await ReadItLaterFromCardAsync(_recId);
        await SetStateAsync(_storyId, readItLater: false, favorite: true); // un-RIL (trigger 1)

        await ReadItLaterFromCardAsync(otherRec);

        (await LoadRowAsync(_readerId, _storyId))!.RecommendationSource!.SourceRecommendationId
            .Should().Be(otherRec,
                "D3: cascade-only removal would have credited the wrong recommender here");
    }

    [Theory]
    [InlineData("TakenDownStory")]
    [InlineData("DraftStory")]
    [InlineData("NeedsRevisionRec")]
    [InlineData("RejectedRec")]
    [InlineData("TakenDownRec")]
    [InlineData("MissingRec")]
    public async Task ReadItLaterFromCard_WhenTheRecOrStoryIsNotLive_ThrowsKeyNotFound_AndWritesNothing(string state)
    {
        int storyId = await SeedStoryAsync(_authorId,
            status: state == "DraftStory" ? StoryStatusEnum.Draft : StoryStatusEnum.InProgress);
        int recId = await SeedRecAsync(storyId, _recommenderId);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            switch (state)
            {
                case "TakenDownStory":
                    await db.Stories.Where(s => s.StoryId == storyId).ExecuteUpdateAsync(u => u.SetProperty(s => s.IsTakenDown, true));
                    break;
                case "NeedsRevisionRec":
                    await db.Recommendations.Where(r => r.RecommendationId == recId)
                        .ExecuteUpdateAsync(u => u.SetProperty(r => r.StatusId, (short)RecommendationStatusEnum.NeedsRevision));
                    break;
                case "RejectedRec":
                    await db.Recommendations.Where(r => r.RecommendationId == recId)
                        .ExecuteUpdateAsync(u => u.SetProperty(r => r.StatusId, (short)RecommendationStatusEnum.Rejected));
                    break;
                case "TakenDownRec":
                    await db.Recommendations.Where(r => r.RecommendationId == recId)
                        .ExecuteUpdateAsync(u => u.SetProperty(r => r.IsTakenDown, true));
                    break;
            }
        }

        Func<Task> act = () => ReadItLaterFromCardAsync(state == "MissingRec" ? recId + 10_000 : recId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        (await LoadRowAsync(_readerId, storyId)).Should().BeNull();
    }

    [Fact]
    public async Task ReadItLaterFromCard_Anonymous_ThrowsInvalidOperation()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        Func<Task> act = () => ReadItLaterFromCardAsync(_recId);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Direct link: MarkStartedAsync(storyId, recId) ─────────────────────────────

    [Fact]
    public async Task MarkStarted_WithAnAttributableRec_AndNoPriorRow_RecordsTheAttribution()
    {
        await MarkStartedAsync(_storyId, _recId);

        UserStoryInteraction row = (await LoadRowAsync(_readerId, _storyId))!;
        row.HasStarted.Should().BeTrue();
        row.IsReadItLater.Should().BeFalse("the direct link persists provenance, it does not save the story");
        row.RecommendationSource!.SourceRecommendationId.Should().Be(_recId);
    }

    [Fact]
    public async Task MarkStarted_WithAnAttributableRec_AndAnExistingRow_RecordsTheAttribution()
    {
        await SeedRowAsync(_readerId, _storyId, favorite: true);

        await MarkStartedAsync(_storyId, _recId);

        UserStoryInteraction row = (await LoadRowAsync(_readerId, _storyId))!;
        row.IsFavorite.Should().BeTrue();
        row.RecommendationSource!.SourceRecommendationId.Should().Be(_recId);
    }

    [Theory]
    [InlineData("OtherStory")]
    [InlineData("Rejected")]
    [InlineData("Missing")]
    public async Task MarkStarted_WithABogusRec_StillStarts_AndRecordsNothing_WithoutThrowing(string bogus)
    {
        int recId = _recId;
        if (bogus == "OtherStory")
            recId = await SeedRecAsync(await SeedStoryAsync(_authorId), _recommenderId);
        else if (bogus == "Rejected")
        {
            using IServiceScope scope = Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Recommendations
                .Where(r => r.RecommendationId == recId)
                .ExecuteUpdateAsync(u => u.SetProperty(r => r.StatusId, (short)RecommendationStatusEnum.Rejected));
        }
        else
            recId += 10_000;

        await MarkStartedAsync(_storyId, recId); // the URL is untrusted — MarkStarted must not throw

        UserStoryInteraction row = (await LoadRowAsync(_readerId, _storyId))!;
        row.HasStarted.Should().BeTrue("the primary write is unaffected by an unattributable parameter");
        row.RecommendationSource.Should().BeNull();
    }

    // ── Removal trigger 1 (and 2) ─────────────────────────────────────────────────

    [Fact]
    public async Task ClearingReadItLater_DeletesTheAttribution_AndKeepsTheRow()
    {
        await ReadItLaterFromCardAsync(_recId);
        await SetStateAsync(_storyId, readItLater: false, favorite: true);

        UserStoryInteraction row = (await LoadRowAsync(_readerId, _storyId))!;
        row.IsFavorite.Should().BeTrue();
        row.RecommendationSource.Should().BeNull("D3 trigger 1: the attribution dies with the bit it describes");
    }

    [Fact]
    public async Task ClearingEveryBit_SparseCleanup_CascadesTheAttribution()
    {
        await ReadItLaterFromCardAsync(_recId);
        await SetStateAsync(_storyId, readItLater: false);

        (await LoadRowAsync(_readerId, _storyId)).Should().BeNull();
        using IServiceScope scope = Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().UserStoryRecommendationSources
            .AnyAsync(s => s.UserId == _readerId)).Should().BeFalse();
    }

    [Fact]
    public async Task DirectLinkAttribution_WithTheBitNeverSet_SurvivesAnUnrelatedToggle()
    {
        await MarkStartedAsync(_storyId, _recId);
        await SetStateAsync(_storyId, favorite: true);

        (await LoadRowAsync(_readerId, _storyId))!.RecommendationSource.Should().NotBeNull(
            "trigger 1 fires on IsReadItLater true→false only");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task ReadItLaterFromCardAsync(int recId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IUserStoryInteractionWriteService>()
            .SetReadItLaterFromRecommendationAsync(recId);
    }

    private async Task MarkStartedAsync(int storyId, int? recId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IUserStoryInteractionWriteService>()
            .MarkStartedAsync(storyId, recId);
    }

    private async Task SetStateAsync(int storyId, bool readItLater = false, bool favorite = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IUserStoryInteractionWriteService>()
            .SetUserStoryInteractionStateAsync(storyId, new UserStoryInteractionStateUpdate(
                IsFavorite: favorite, IsHiddenFavorite: false, IsFollowed: false,
                IsCompleted: false, IsReadItLater: readItLater, IsIgnored: false));
    }

    private async Task<int> SeedRecAsync(int storyId, int recommenderId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Recommendation rec = new()
        {
            StoryId = storyId,
            RecommenderId = recommenderId,
            StatusId = (short)RecommendationStatusEnum.Approved,
            DatePosted = DateTime.UtcNow,
            RecommendationDetail = new RecommendationDetail { Text = "<p>endorsement</p>" },
        };
        db.Recommendations.Add(rec);
        await db.SaveChangesAsync();
        return rec.RecommendationId;
    }

    private async Task SeedRowAsync(int userId, int storyId,
        bool favorite = false, bool followed = false, bool readItLater = false, bool hasStarted = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStoryInteractions.Add(new UserStoryInteraction
        {
            UserId = userId, StoryId = storyId, IsFavorite = favorite, IsFollowed = followed,
            IsReadItLater = readItLater, HasStarted = hasStarted,
        });
        await db.SaveChangesAsync();
    }

    private async Task<UserStoryInteraction?> LoadRowAsync(int userId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().UserStoryInteractions
            .Include(i => i.InteractionDatePartition)
            .Include(i => i.RecommendationSource)
            .FirstOrDefaultAsync(i => i.UserId == userId && i.StoryId == storyId);
    }
}
