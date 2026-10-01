using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The check-then-act sites owner ruling D23 hardens (WU-CounterSymmetry): <c>JoinAsync</c>, the vouch
/// insert, every <c>ServerUserStoryInteractionWriteService</c> create site and its date partition — each
/// now an <c>INSERT … ON CONFLICT DO NOTHING</c> — plus the lineage approve guard (orchestrator amendment
/// U1, a conditional <c>WHERE status = Pending</c>).
/// <para>
/// Each race is made deterministic with <see cref="InterleavingCommandInterceptor"/> (testing.md
/// §"Testing a check-then-act guard: interleave, don't race"): the competing row lands on a separate
/// connection immediately before the service's own <c>INSERT INTO &lt;table&gt;</c>, i.e. after its
/// pre-read said "no row". <c>interceptReaders: true</c> makes the marker also match the pre-fix shape
/// (an EF <c>SaveChangesAsync</c> insert), so against that code each test fails with the 23505 the fix
/// removes. Every interleaved test asserts <c>interceptor.Fired</c>. Two tests are sequential, with no
/// interceptor: the partition-creation regression guard (<c>SetState_OnAHasStartedOnlyRow_*</c>) and the
/// sequential double approve (<c>ApproveLineage_Twice_*</c>, which also runs testing.md's convergence pass).
/// The interleaved tests skip that pass: their competing rows bypass the counter by design.
/// </para>
/// <para>
/// <b>Per-test seeding:</b> users and stories via the base helpers; <c>UserStat</c> rows for every user
/// whose counter is asserted (SeedUserAsync never creates one); the group via the real service; the
/// recommendation (Approved, with its detail row) and the pre-existing USI row inline.
/// </para>
/// Tier: Integration (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class CounterSymmetryTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _readerId;
    private int _storyId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync("Author");
        _readerId = await SeedUserAsync("Reader");
        _storyId = await SeedStoryAsync(_authorId);
        await SeedUserStatAsync(_authorId);
        await SeedUserStatAsync(_readerId);
    }

    // ── group_members (D23 + D24) ────────────────────────────────────────────────

    [Fact]
    public async Task Join_ACompetingJoinLandingBeforeTheInsert_IsANoOp_AndTheLoserCountsNothing()
    {
        SetActiveUser(_authorId);
        int groupId;
        using (IServiceScope create = Factory.Services.CreateScope())
            groupId = await create.ServiceProvider.GetRequiredService<IGroupWriteService>()
                .CreateGroupAsync(new CreateGroupDto { GroupName = "Contended" });

        SetActiveUser(_readerId);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO group_members",
            interleavedSql: $"""
                INSERT INTO group_members (user_id, group_id, role, notify_for_new_story, notify_for_new_blog_post, date_joined)
                VALUES ({_readerId}, {groupId}, 0, true, false, now());
                """,
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerGroupWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerGroupWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Awaiting(() => svc.JoinAsync(groupId)).Should().NotThrowAsync(
                "a join that lost the race is a no-op, never a 500 on pk_group_members");
        }
        interceptor.Fired.Should().BeTrue();

        using IServiceScope verify = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(verify);
        (await db.GroupMembers.CountAsync(m => m.GroupId == groupId && m.UserId == _readerId)).Should().Be(1);
        (await db.UserStats.Where(us => us.UserId == _readerId).Select(us => us.GroupsJoined).SingleAsync())
            .Should().Be(0, "the +1 runs only for the insert that landed a row — the winner owns the one count");
    }

    // ── vouches (D23's idempotency half) ─────────────────────────────────────────

    [Fact]
    public async Task Vouch_ACompetingVouchLandingBeforeTheInsert_IsANoOp_AndSendsNoSecondNotification()
    {
        SetActiveUser(_readerId);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO vouches",
            interleavedSql: $"""
                INSERT INTO vouches (vouching_user_id, vouched_user_id, vouch_text, date_vouched)
                VALUES ({_readerId}, {_authorId}, NULL, now());
                """,
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerFollowingWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerFollowingWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Awaiting(() => svc.VouchAsync(_authorId, "<p>Vouched.</p>")).Should().NotThrowAsync(
                "a double vouch is idempotent, never a 500 on pk_vouches");
        }
        interceptor.Fired.Should().BeTrue();

        using IServiceScope verify = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(verify);
        (await db.Vouches.CountAsync(v => v.VouchingUserId == _readerId && v.VouchedUserId == _authorId)).Should().Be(1);
        (await db.Notifications.CountAsync(n => n.RecipientUserId == _authorId
                                               && n.NotificationTypeId == NotificationTypeEnum.NewVouchOnYou))
            .Should().Be(0, "only the vouch that landed notifies — the competing raw insert sent none, so the loser must not either");
    }

    // ── user_story_interactions + its date partition (D23 "stated minimum") ─────

    [Fact]
    public async Task SetState_ACompetingFirstFavorite_IsAdopted_NotA500_AndTheFlipIsNotCountedTwice()
    {
        SetActiveUser(_readerId);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO user_story_interactions",
            interleavedSql: $"""
                INSERT INTO user_story_interactions (user_id, story_id, has_started, is_completed, is_favorite,
                    is_hidden_favorite, is_followed, is_read_it_later, is_ignored)
                VALUES ({_readerId}, {_storyId}, false, false, true, false, false, false, false);
                INSERT INTO user_story_interaction_dates (user_id, story_id, favorite_date) VALUES ({_readerId}, {_storyId}, now());
                """,
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerUserStoryInteractionWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerUserStoryInteractionWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Awaiting(() => svc.SetUserStoryInteractionStateAsync(_storyId, Favorite))
                .Should().NotThrowAsync("the loser re-reads the winner's row instead of 500ing on the PK");
        }
        interceptor.Fired.Should().BeTrue();

        using IServiceScope verify = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(verify);
        (await db.UserStoryInteractions.CountAsync(i => i.UserId == _readerId && i.StoryId == _storyId)).Should().Be(1);
        (await db.UserStoryInteractionDates.CountAsync(d => d.UserId == _readerId && d.StoryId == _storyId)).Should().Be(1);
        (await db.UserStats.Where(us => us.UserId == _authorId).Select(us => us.FavoritesOnStories).SingleAsync())
            .Should().Be(0, "the loser captured wasFavorite from the re-read row, so it moved no counter");
    }

    [Fact]
    public async Task SetState_ACompetingDatePartition_IsLoaded_NotA500()
    {
        // A reading-path row (HasStarted only) has no date partition yet; a favorite needs one.
        await SeedInteractionAsync(new UserStoryInteraction { UserId = _readerId, StoryId = _storyId, HasStarted = true });

        SetActiveUser(_readerId);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO user_story_interaction_dates",
            interleavedSql: $"INSERT INTO user_story_interaction_dates (user_id, story_id) VALUES ({_readerId}, {_storyId});",
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerUserStoryInteractionWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerUserStoryInteractionWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Awaiting(() => svc.SetUserStoryInteractionStateAsync(_storyId, Favorite))
                .Should().NotThrowAsync("moving the PK race to the partition table would only move the 500");
        }
        interceptor.Fired.Should().BeTrue();

        using IServiceScope verify = Factory.Services.CreateScope();
        UserStoryInteractionDate dates = await Db(verify).UserStoryInteractionDates
            .SingleAsync(d => d.UserId == _readerId && d.StoryId == _storyId);
        dates.FavoriteDate.Should().NotBeNull("the stamp lands on the partition the competitor created");
    }

    [Fact]
    public async Task SetState_OnAHasStartedOnlyRow_CreatesThePartition()
    {
        await SeedInteractionAsync(new UserStoryInteraction { UserId = _readerId, StoryId = _storyId, HasStarted = true });

        SetActiveUser(_readerId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IUserStoryInteractionWriteService>()
                .SetUserStoryInteractionStateAsync(_storyId, Favorite);

        using IServiceScope verify = Factory.Services.CreateScope();
        UserStoryInteraction row = await Db(verify).UserStoryInteractions
            .Include(i => i.InteractionDatePartition)
            .SingleAsync(i => i.UserId == _readerId && i.StoryId == _storyId);
        row.HasStarted.Should().BeTrue("the panel never touches HasStarted");
        row.IsFavorite.Should().BeTrue();
        row.InteractionDatePartition!.FavoriteDate.Should().NotBeNull();
    }

    [Fact]
    public async Task SetState_TheEnsuredRowDeletedBeforeItsReRead_IsEnsuredAgain_NotA401()
    {
        // The same user's all-false panel write in another tab sparse-deletes the row this call has just
        // ensured, between the ensure-insert and the re-read. The old re-read was FirstAsync, whose
        // InvalidOperationException EndpointHelpers maps to a 401 ("session expired").
        SetActiveUser(_readerId);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "FROM user_story_interactions AS",
            interleavedSql: $"DELETE FROM user_story_interactions WHERE user_id = {_readerId} AND story_id = {_storyId};",
            interceptReaders: true,
            fireOnMatch: 2); // match 1 is the initial load (no row yet); match 2 is the identical re-read
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerUserStoryInteractionWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerUserStoryInteractionWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Awaiting(() => svc.SetUserStoryInteractionStateAsync(_storyId, Favorite))
                .Should().NotThrowAsync("the ensure runs once more when its re-read finds the row gone");
        }
        interceptor.Fired.Should().BeTrue();

        using IServiceScope verify = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(verify);
        UserStoryInteraction row = await db.UserStoryInteractions
            .Include(i => i.InteractionDatePartition)
            .SingleAsync(i => i.UserId == _readerId && i.StoryId == _storyId);
        row.IsFavorite.Should().BeTrue("the raise lands on the re-ensured row");
        row.InteractionDatePartition!.FavoriteDate.Should().NotBeNull();
        (await db.UserStats.Where(us => us.UserId == _authorId).Select(us => us.FavoritesOnStories).SingleAsync())
            .Should().Be(1, "one genuine false-to-true flip");
    }

    [Fact]
    public async Task SetState_TheRowDeletedBeforeItsPartitionRead_FailsAsAConcurrencyConflict_AndResurrectsNothing()
    {
        // A partition only disappears with its parent (FK cascade). Here the parent is deleted between the
        // partition's ensure-insert and its read, so the write cannot land: it fails as a concurrency
        // conflict (tracker D11's update-vs-delete case) instead of re-creating an orphan partition.
        await SeedInteractionAsync(new UserStoryInteraction { UserId = _readerId, StoryId = _storyId, HasStarted = true });

        SetActiveUser(_readerId);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "FROM user_story_interaction_dates AS",
            interleavedSql: $"DELETE FROM user_story_interactions WHERE user_id = {_readerId} AND story_id = {_storyId};",
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerUserStoryInteractionWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerUserStoryInteractionWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Awaiting(() => svc.SetUserStoryInteractionStateAsync(_storyId, Favorite))
                .Should().ThrowAsync<DbUpdateConcurrencyException>();
        }
        interceptor.Fired.Should().BeTrue();

        using IServiceScope verify = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(verify);
        (await db.UserStoryInteractions.AnyAsync(i => i.UserId == _readerId && i.StoryId == _storyId)).Should().BeFalse();
        (await db.UserStoryInteractionDates.AnyAsync(d => d.UserId == _readerId && d.StoryId == _storyId)).Should().BeFalse();
    }

    [Fact]
    public async Task MarkStarted_ACompetingFirstStart_IsAdopted_AndStoriesInProgressIsNotCountedTwice()
    {
        SetActiveUser(_readerId);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO user_story_interactions",
            interleavedSql: $"""
                INSERT INTO user_story_interactions (user_id, story_id, has_started, is_completed, is_favorite,
                    is_hidden_favorite, is_followed, is_read_it_later, is_ignored)
                VALUES ({_readerId}, {_storyId}, true, false, false, false, false, false, false);
                """,
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerUserStoryInteractionWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerUserStoryInteractionWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Awaiting(() => svc.MarkStartedAsync(_storyId)).Should().NotThrowAsync();
        }
        interceptor.Fired.Should().BeTrue();

        using IServiceScope verify = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(verify);
        (await db.UserStoryInteractions.CountAsync(i => i.UserId == _readerId && i.StoryId == _storyId)).Should().Be(1);
        (await db.UserStats.Where(us => us.UserId == _readerId).Select(us => us.StoriesInProgress).SingleAsync())
            .Should().Be(0, "alreadyStarted was captured from the winner's row — no second +1");
    }

    [Fact]
    public async Task MarkCompleted_ACompetingFirstCompletion_ReturnsEarly_AndStoriesReadIsNotCountedTwice()
    {
        SetActiveUser(_readerId);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO user_story_interactions",
            interleavedSql: $"""
                INSERT INTO user_story_interactions (user_id, story_id, has_started, is_completed, is_favorite,
                    is_hidden_favorite, is_followed, is_read_it_later, is_ignored)
                VALUES ({_readerId}, {_storyId}, false, true, false, false, false, false, false);
                INSERT INTO user_story_interaction_dates (user_id, story_id, completed_date) VALUES ({_readerId}, {_storyId}, now());
                """,
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerUserStoryInteractionWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerUserStoryInteractionWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Awaiting(() => svc.MarkCompletedAsync(_storyId)).Should().NotThrowAsync();
        }
        interceptor.Fired.Should().BeTrue();

        using IServiceScope verify = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(verify);
        (await db.UserStoryInteractions.CountAsync(i => i.UserId == _readerId && i.StoryId == _storyId)).Should().Be(1);
        (await db.UserStoryInteractionDates.CountAsync(d => d.UserId == _readerId && d.StoryId == _storyId)).Should().Be(1);
        (await db.UserStats.Where(us => us.UserId == _readerId).Select(us => us.StoriesRead).SingleAsync())
            .Should().Be(0, "the already-complete early return is evaluated on the re-read row");
    }

    [Fact]
    public async Task ReadItLaterFromCard_ACompetingSave_IsAdopted_AndRecordsNoAttribution()
    {
        int recommenderId = await SeedUserAsync("Recommender");
        int recId = await SeedApprovedRecommendationAsync(recommenderId);

        SetActiveUser(_readerId);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO user_story_interactions",
            interleavedSql: $"""
                INSERT INTO user_story_interactions (user_id, story_id, has_started, is_completed, is_favorite,
                    is_hidden_favorite, is_followed, is_read_it_later, is_ignored)
                VALUES ({_readerId}, {_storyId}, false, false, false, false, false, true, false);
                """,
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerUserStoryInteractionWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerUserStoryInteractionWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Awaiting(() => svc.SetReadItLaterFromRecommendationAsync(recId)).Should().NotThrowAsync();
        }
        interceptor.Fired.Should().BeTrue();

        using IServiceScope verify = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(verify);
        UserStoryInteraction row = await db.UserStoryInteractions
            .Include(i => i.InteractionDatePartition)
            .SingleAsync(i => i.UserId == _readerId && i.StoryId == _storyId);
        row.IsReadItLater.Should().BeTrue();
        row.InteractionDatePartition!.ReadItLaterDate.Should().NotBeNull();
        (await db.UserStoryRecommendationSources.CountAsync(s => s.UserId == _readerId))
            .Should().Be(0, "the bit was already set by the winner, so this card did not set it (D3's defining sentence)");
    }

    // ── Atomic counters: VersionCount (D22) and the like toggles' landed value (service §2.4.6) ──

    [Fact]
    public async Task AddAlternateVersion_AConcurrentIncrementLandingBeforeTheSave_IsNotLost()
    {
        SetActiveUser(_authorId);
        int chapterId;
        using (IServiceScope create = Factory.Services.CreateScope())
            chapterId = await create.ServiceProvider.GetRequiredService<IChapterWriteService>().CreateChapterAsync(
                new CreateChapterDto { StoryId = _storyId, ChapterText = "<p>Primary.</p>" });

        // Another version's +1 lands after this call loaded the chapter (VersionCount 1) and before its
        // save. The old tracked `++` wrote back 2 — the competitor's increment was lost.
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "INSERT INTO chapter_contents",
            interleavedSql: $"UPDATE chapters SET version_count = version_count + 1 WHERE chapter_id = {chapterId};",
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerChapterWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerChapterWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await svc.AddAlternateVersionAsync(chapterId, new CreateChapterDto { StoryId = _storyId, ChapterText = "<p>Alternate.</p>" });
        }
        interceptor.Fired.Should().BeTrue();

        using IServiceScope verify = Factory.Services.CreateScope();
        (await Db(verify).Chapters.Where(c => c.ChapterId == chapterId).Select(c => c.VersionCount).SingleAsync())
            .Should().Be(3, "1 + the competitor's +1 + this call's atomic +1 — none lost");
    }

    [Fact]
    public async Task CommentLike_ReturnsTheLandedCount_IncludingALikeThatLandedMeanwhile()
    {
        long commentId = await SeedCommentOnAPublishedChapterAsync();
        int otherLiker = await SeedUserAsync("OtherLiker");

        // Another user's like (row + counter) lands between this call's load of the comment
        // (LikeCount 0) and its own counter statement. The old return was "loaded value + delta" = 1.
        SetActiveUser(_readerId);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "UPDATE base_comments",
            interleavedSql: $"""
                INSERT INTO comment_likes (comment_id, user_id) VALUES ({commentId}, {otherLiker});
                UPDATE base_comments SET like_count = like_count + 1 WHERE comment_id = {commentId};
                """);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerCommentWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerCommentWriteService>(scope, ConnectionString, interceptor);
        CommentLikeResultDto result;
        await using (writeDb)
        {
            result = await svc.ToggleLikeAsync(commentId);
        }
        interceptor.Fired.Should().BeTrue();

        result.IsLiked.Should().BeTrue();
        result.LikeCount.Should().Be(2, "the response re-reads the landed value (MA-705's fix, applied to this sibling)");
    }

    [Fact]
    public async Task RecommendationLike_ReturnsTheLandedCount_IncludingALikeThatLandedMeanwhile()
    {
        int recId = await SeedApprovedRecommendationAsync(await SeedUserAsync("Recommender"));
        int otherLiker = await SeedUserAsync("OtherLiker");

        SetActiveUser(_readerId);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "UPDATE recommendations",
            interleavedSql: $"""
                INSERT INTO recommendation_likes (recommendation_id, user_id) VALUES ({recId}, {otherLiker});
                UPDATE recommendations SET like_count = like_count + 1 WHERE recommendation_id = {recId};
                """);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerRecommendationWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerRecommendationWriteService>(scope, ConnectionString, interceptor);
        RecommendationLikeResultDto result;
        await using (writeDb)
        {
            result = await svc.ToggleLikeAsync(recId);
        }
        interceptor.Fired.Should().BeTrue();

        result.IsLiked.Should().BeTrue();
        result.LikeCount.Should().Be(2, "the response re-reads the landed value, not 'loaded value + delta'");
    }

    // ── Lineage approve (orchestrator amendment U1) ─────────────────────────────

    [Fact]
    public async Task ApproveLineage_Twice_TheSecondIsRefused_AndCountsAndNotifiesOnce()
    {
        (int sourceStoryId, int targetStoryId) = await RequestInspiredByAsync();
        await SeedStoriesWrittenGroundTruthAsync();

        SetActiveUser(_authorId);
        await ApproveAsync(sourceStoryId, targetStoryId);
        await FluentActions.Awaiting(() => ApproveAsync(sourceStoryId, targetStoryId))
            .Should().ThrowAsync<StoryLineageValidationException>().WithMessage("*no longer pending*");

        using IServiceScope verify = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(verify);
        (await db.UserStats.Where(us => us.UserId == _authorId).Select(us => us.AcknowledgedAsInspirationCount).SingleAsync())
            .Should().Be(1, "a double approve used to increment twice");
        (await db.Notifications.CountAsync(n => n.RecipientUserId == _readerId
                                               && n.NotificationTypeId == NotificationTypeEnum.StoryLineageApproved))
            .Should().Be(1);

        // testing.md "Counter convergence": the wired count equals the recompute.
        UserStatRecalcResult pass = await verify.ServiceProvider.GetRequiredService<UserStatRecalculator>().RecalculateAllAsync();
        pass.RowsInserted.Should().Be(0, "both users have seeded UserStat rows");
        pass.CountersCorrected.Should().Be(0, "the wired AcknowledgedAsInspirationCount must equal the recompute");
    }

    [Fact]
    public async Task ApproveLineage_AnApprovalLandingBetweenTheReadAndTheWrite_IsRefused_AndCountsNothing()
    {
        (int sourceStoryId, int targetStoryId) = await RequestInspiredByAsync();

        SetActiveUser(_authorId);
        InterleavingCommandInterceptor interceptor = new(ConnectionString, "UPDATE story_lineages",
            interleavedSql: $"""
                UPDATE story_lineages SET status_id = {(short)StoryLineageStatus.Approved}
                WHERE source_story_id = {sourceStoryId} AND target_story_id = {targetStoryId} AND relationship_type_id = 1;
                """,
            interceptReaders: true);
        using IServiceScope scope = Factory.Services.CreateScope();
        (ServerStoryLineageWriteService svc, ApplicationDbContext writeDb) =
            InterleavingCommandInterceptor.CreateService<ServerStoryLineageWriteService>(scope, ConnectionString, interceptor);
        await using (writeDb)
        {
            await FluentActions.Awaiting(() => svc.ApproveLineageAsync(sourceStoryId, targetStoryId, 1))
                .Should().ThrowAsync<StoryLineageValidationException>(
                    "the conditional WHERE status = Pending finds nothing once the competitor approved");
        }
        interceptor.Fired.Should().BeTrue();

        using IServiceScope verify = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(verify);
        (await db.UserStats.Where(us => us.UserId == _authorId).Select(us => us.AcknowledgedAsInspirationCount).SingleAsync())
            .Should().Be(0, "the loser of the race neither counts nor notifies");
        (await db.Notifications.CountAsync(n => n.NotificationTypeId == NotificationTypeEnum.StoryLineageApproved))
            .Should().Be(0);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static readonly UserStoryInteractionStateUpdate Favorite = new(
        IsFavorite: true, IsHiddenFavorite: false, IsFollowed: false,
        IsCompleted: false, IsReadItLater: false, IsIgnored: false);

    private static ApplicationDbContext Db(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    private async Task SeedUserStatAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        Db(scope).UserStats.Add(new UserStat { UserId = userId });
        await Db(scope).SaveChangesAsync();
    }

    /// <summary>SeedStoryAsync bypasses CreateStoryAsync's +1. Before a convergence pass, each user's
    /// StoriesWritten gets the ground truth those direct inserts created (testing.md "Counter
    /// convergence"), so the pass compares only what the test's own wired ops moved.</summary>
    private async Task SeedStoriesWrittenGroundTruthAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);
        foreach (int userId in new[] { _authorId, _readerId })
        {
            int stories = await db.Stories.CountAsync(s => s.AuthorId == userId);
            await db.UserStats.Where(us => us.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(us => us.StoriesWritten, stories));
        }
    }

    /// <summary>The prior reading-path state a test starts from (in production, MarkStartedAsync).</summary>
    private async Task SeedInteractionAsync(UserStoryInteraction row)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        Db(scope).UserStoryInteractions.Add(row);
        await Db(scope).SaveChangesAsync();
    }

    /// <summary>An Approved recommendation on <c>_storyId</c> (StatusId 2 via the HasData lookup,
    /// detail row required) by a third user, so the reader's card save is attributable.</summary>
    private async Task<int> SeedApprovedRecommendationAsync(int recommenderId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        Recommendation rec = new()
        {
            StoryId = _storyId, RecommenderId = recommenderId, StatusId = 2, DatePosted = DateTime.UtcNow,
            RecommendationDetail = new RecommendationDetail { Text = "<p>Read it.</p>" },
        };
        Db(scope).Recommendations.Add(rec);
        await Db(scope).SaveChangesAsync();
        return rec.RecommendationId;
    }

    /// <summary>A comment by the author on a published chapter of <c>_storyId</c>, so the reader can
    /// see (and like) it. Chapter inline with the two-step <c>PrimaryContentId</c>.</summary>
    private async Task<long> SeedCommentOnAPublishedChapterAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);
        Chapter chapter = new()
        {
            StoryId = _storyId, ChapterNumber = 1, Title = "Ch1", IsPublished = true,
            FirstPublishedDate = DateTime.UtcNow, VersionCount = 1,
        };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        ChapterContent content = new()
        {
            ChapterId = chapter.ChapterId, AuthorId = _authorId, ChapterText = "<p>Text.</p>", WordCount = 1,
            PublishDate = DateTime.UtcNow,
        };
        db.ChapterContents.Add(content);
        await db.SaveChangesAsync();
        chapter.PrimaryContentId = content.ChapterContentId;
        ChapterComment comment = new()
        {
            ChapterId = chapter.ChapterId, UserId = _authorId, CommentText = "<p>Hi.</p>", DatePosted = DateTime.UtcNow,
        };
        db.ChapterComments.Add(comment);
        await db.SaveChangesAsync();
        return comment.CommentId;
    }

    /// <summary>The reader's story cites the author's story as "Inspired By" (type 1) — Pending, since
    /// the two stories have different authors. The target author (<c>_authorId</c>) approves.</summary>
    private async Task<(int Source, int Target)> RequestInspiredByAsync()
    {
        int sourceStoryId = await SeedStoryAsync(_readerId);
        SetActiveUser(_readerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IStoryLineageWriteService>().RequestLineageAsync(
            new CreateStoryLineageDto { SourceStoryId = sourceStoryId, TargetStoryId = _storyId, TypeId = 1 });
        return (sourceStoryId, _storyId);
    }

    private async Task ApproveAsync(int sourceStoryId, int targetStoryId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IStoryLineageWriteService>()
            .ApproveLineageAsync(sourceStoryId, targetStoryId, 1);
    }
}
