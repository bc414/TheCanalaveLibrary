using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The author story delete — owner ruling D15 (WU-AuthorStoryDelete, 2026-10-13):
/// <see cref="IStoryWriteService.DeleteStoryAsync"/> is a hard delete by the primary author, under any
/// moderation state, through one transaction (story-scope <c>TptDelete</c>, the Spotlight slot return,
/// the story row) with everything else left to the database's cascades. D15's own test list, plus the
/// gate, the lost-race and circuit-tracker cases. The cover blob's inline delete is in
/// <see cref="BlobCleanupTests"/> (D14's inline sites); the HTTP status mapping in
/// <see cref="StoryEndpointsTests"/>. Tier: Integration.
/// <para><b>Per-test seeding and FK parents.</b> <see cref="InitializeAsync"/> seeds the author, a reader
/// and a moderator (<c>SeedUserAsync</c>) and the author's story (<c>SeedStoryAsync</c>, InProgress).
/// Chapters go through <see cref="IChapterWriteService"/> as the author (parent: the story); comments,
/// likes, interactions, lists, series, groups, recommendations, lineage, co-author, blog post, report and
/// notification rows are inserted directly with <see cref="ApplicationDbContext"/> (parents: those users and
/// stories; the report reason and the notification and lineage types are HasData lookups that survive
/// Respawn). <c>daily_story_stats</c> has no EF model, so it is raw SQL. Spotlight slots go through
/// <see cref="ISpotlightSlotAllocator"/> as the moderator and placements through
/// <see cref="ISpotlightWriteService.RedeemSlotAsync"/> as the reader (a sponsor who is not the author); a
/// past placement, which redemption refuses, is inserted directly.</para>
/// </summary>
[Collection("Postgres")]
public class StoryDeletionTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _readerId;
    private int _modId;
    private int _storyId;

    private static readonly int BlockDays = SiteSettingKeys.SpotlightBlockDurationDaysDefault;
    private static DateTime CurrentBlockStart => SpotlightBlocks.FloorToBlockStart(DateTime.UtcNow, BlockDays);
    private static DateTime NextBlockStart => CurrentBlockStart.AddDays(BlockDays);

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync("story-deleter");
        _readerId = await SeedUserAsync("reader");
        _modId = await SeedUserAsync("mod");
        _storyId = await SeedStoryAsync(_authorId);
    }

    // ── D15's tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_AStoryWithChaptersCommentsAndReplies_LeavesNoBaseComment_AndNoChapterOrContent()
    {
        int ch1 = await CreateChapterAsync("Ch 1");
        int ch2 = await CreateChapterAsync("Ch 2");
        long c1 = await SeedCommentAsync(ch1, _readerId, "on chapter 1");
        long reply = await SeedCommentAsync(ch1, _authorId, "the author's reply", parentId: c1);
        long c2 = await SeedCommentAsync(ch2, _readerId, "on chapter 2");
        await SeedAsync(db => db.CommentLikes.Add(new CommentLike { CommentId = c2, UserId = _authorId }));
        long[] ids = [c1, reply, c2];

        await DeleteAsAsync(_authorId, _storyId);

        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM base_comments WHERE comment_id = ANY({ids})"))
            .Should().Be(0, "no base_comments row may outlive its story (D10, at story scope)");
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM comment_likes WHERE comment_id = ANY({ids})")).Should().Be(0);
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM stories WHERE story_id = {_storyId}")).Should().Be(0);
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM chapters WHERE story_id = {_storyId}")).Should().Be(0);
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM chapter_contents WHERE chapter_id = ANY({new[] { ch1, ch2 }})"))
            .Should().Be(0, "contents cascade with their chapters past the Restrict primary_content_id FK");
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM story_listings WHERE story_id = {_storyId}")).Should().Be(0);
    }

    [Fact]
    public async Task Delete_RemovesEveryForeignRowThatExistsBecauseOfTheStory()
    {
        int otherStory = await SeedStoryAsync(_readerId);
        int listId = 0, seriesId = 0, groupId = 0, recId = 0;
        await SeedAsync(db =>
        {
            db.UserStoryInteractions.Add(new UserStoryInteraction
            {
                UserId = _readerId, StoryId = _storyId, IsFavorite = true,
                InteractionDatePartition = new UserStoryInteractionDate { FavoriteDate = DateTime.UtcNow },
            });
            db.CoAuthors.Add(new CoAuthor { StoryId = _storyId, CoAuthorUserId = _readerId, DateAdded = DateTime.UtcNow });
            db.StoryLineages.Add(new StoryLineage
            {
                SourceStoryId = _storyId, TargetStoryId = otherStory, RelationshipTypeId = 1,
                StatusId = StoryLineageStatus.Approved, DateCreated = DateTime.UtcNow,
            });
        });
        await SeedAsync(db =>
        {
            CustomList list = new() { UserId = _readerId, ListName = "Faves", IsPublic = true, DateCreated = DateTime.UtcNow };
            Series series = new() { AuthorId = _authorId, Name = "Saga", DateCreated = DateTime.UtcNow };
            Group group = new()
            {
                CreatorId = _readerId, GroupName = $"Club {Guid.NewGuid():N}", AudienceRating = Rating.E,
                MaxContentRating = Rating.E, DateCreated = DateTime.UtcNow,
            };
            Recommendation rec = new()
            {
                StoryId = _storyId, RecommenderId = _readerId, StatusId = (short)RecommendationStatusEnum.Approved,
                DatePosted = DateTime.UtcNow, RecommendationDetail = new RecommendationDetail { Text = new string('x', 500) },
            };
            db.AddRange(list, series, group, rec);
            db.SaveChanges();
            (listId, seriesId, groupId, recId) = (list.CustomListId, series.SeriesId, group.GroupId, rec.RecommendationId);
            db.CustomListEntries.Add(new CustomListEntry { ListId = listId, StoryId = _storyId, DateAdded = DateTime.UtcNow });
            db.SeriesEntries.Add(new SeriesEntry { SeriesId = seriesId, StoryId = _storyId, OrderIndex = 1 });
            db.GroupStories.Add(new GroupStory { GroupId = groupId, StoryId = _storyId, AddedByUserId = _readerId, DateAdded = DateTime.UtcNow });
        });

        await DeleteAsAsync(_authorId, _storyId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.UserStoryInteractions.AnyAsync(i => i.StoryId == _storyId)).Should().BeFalse("the reader's favorite");
        (await db.CustomListEntries.AnyAsync(e => e.StoryId == _storyId)).Should().BeFalse();
        (await db.SeriesEntries.AnyAsync(e => e.StoryId == _storyId)).Should().BeFalse();
        (await db.GroupStories.AnyAsync(g => g.StoryId == _storyId)).Should().BeFalse();
        (await db.Recommendations.AnyAsync(r => r.RecommendationId == recId)).Should().BeFalse(
            "D15's accepted casualty (i): another user's recommendation goes with the story");
        (await db.StoryLineages.AnyAsync(l => l.SourceStoryId == _storyId)).Should().BeFalse(
            "D15's accepted casualty (ii): the lineage link goes");
        (await db.CoAuthors.AnyAsync(c => c.StoryId == _storyId)).Should().BeFalse();

        // The containers and the derived work are other users' own and stay.
        (await db.CustomLists.AnyAsync(l => l.CustomListId == listId)).Should().BeTrue();
        (await db.Series.AnyAsync(s => s.SeriesId == seriesId)).Should().BeTrue();
        (await db.Groups.AnyAsync(g => g.GroupId == groupId)).Should().BeTrue();
        (await db.Stories.AnyAsync(s => s.StoryId == otherStory)).Should().BeTrue("the surviving child story is untouched");
    }

    [Fact]
    public async Task Delete_LeavesABlogPostThatNamedTheStory_WithANullStory_AndUnpinsIt()
    {
        int postId = 0;
        await SeedAsync(db =>
        {
            ProfileBlogPost post = new()
            {
                AuthorId = _readerId, Title = "About that story", Content = "<p>thoughts</p>", Rating = Rating.E,
                IsPublished = true, DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow, StoryId = _storyId,
            };
            db.ProfileBlogPosts.Add(post);
            db.SaveChanges();
            postId = post.BlogPostId;
        });
        await SeedAsync(db => db.Database.ExecuteSql(
            $"UPDATE \"AspNetUsers\" SET pinned_story_id = {_storyId} WHERE id = {_authorId}"));

        await DeleteAsAsync(_authorId, _storyId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.ProfileBlogPosts.Where(p => p.BlogPostId == postId).Select(p => p.StoryId).SingleAsync())
            .Should().BeNull("ProfileBlogPost.StoryId is SET NULL: the post survives");
        (await db.Users.Where(u => u.Id == _authorId).Select(u => u.PinnedStoryId).SingleAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Delete_RemovesTheStatRows_AndTheSiteAggregatorStopsCountingTheirViews()
    {
        // D15: "the one test that catches a cascade-only implementation". D15's premise was that the table
        // has no FK; it carries ON DELETE CASCADE (InitialSchema's raw DDL), so the cascade is what passes it.
        int survivor = await SeedStoryAsync(_readerId);
        DateOnly day = SiteDailyStatWorker.PreviousCompletedUtcDay(DateTime.UtcNow);
        await SeedAsync(db => db.Database.ExecuteSql(
            $"INSERT INTO daily_story_stats (story_id, stat_date, view_count) VALUES ({_storyId}, {day}, 40), ({survivor}, {day}, 2)"));

        await DeleteAsAsync(_authorId, _storyId);

        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM daily_story_stats WHERE story_id = {_storyId}"))
            .Should().Be(0);
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SiteDailyStatAggregator>().UpsertDayAsync(day);
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.SiteDailyStats.Where(s => s.StatDate == day).Select(s => s.StoryViews).SingleAsync())
            .Should().Be(2, "only the surviving story's views count");
    }

    [Fact]
    public async Task Delete_ByANonOwner_ByACoAuthor_Anonymously_OrOfAMissingStory_Throws_AndTheStorySurvives()
    {
        await SeedAsync(db => db.CoAuthors.Add(new CoAuthor { StoryId = _storyId, CoAuthorUserId = _readerId, DateAdded = DateTime.UtcNow }));
        int stranger = await SeedUserAsync("stranger");

        Func<Task> byStranger = () => DeleteAsAsync(stranger, _storyId);
        await byStranger.Should().ThrowAsync<UnauthorizedAccessException>();

        Func<Task> byCoAuthor = () => DeleteAsAsync(_readerId, _storyId);
        await byCoAuthor.Should().ThrowAsync<UnauthorizedAccessException>(
            "CoAuthor is credit and access, not ownership (D15)");

        Func<Task> anonymous = () => DeleteAsAsync(null, _storyId);
        await anonymous.Should().ThrowAsync<InvalidOperationException>();

        Func<Task> missing = () => DeleteAsAsync(_authorId, int.MaxValue);
        await missing.Should().ThrowAsync<KeyNotFoundException>();

        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM stories WHERE story_id = {_storyId}")).Should().Be(1);
    }

    [Fact]
    public async Task Delete_OfAnAuthorlessStory_IsRefused()
    {
        // A story whose author deleted their account survives anonymized (D13) and is owned by nobody.
        int orphan = await SeedStoryAsync(authorId: null);

        Func<Task> act = () => DeleteAsAsync(_authorId, orphan);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM stories WHERE story_id = {orphan}")).Should().Be(1);
    }

    [Theory]
    [InlineData(false, StoryStatusEnum.InProgress)]       // an open report on a live story
    [InlineData(true, StoryStatusEnum.InProgress)]        // taken down
    [InlineData(false, StoryStatusEnum.PendingApproval)]  // in the approval queue
    public async Task Delete_UnderModeration_Succeeds_AndTheReportSurvivesUnchanged(bool takenDown, StoryStatusEnum status)
    {
        int storyId = await SeedStoryAsync(_authorId, status: status);
        if (takenDown)
            await SeedAsync(db => db.Database.ExecuteSql(
                $"UPDATE stories SET is_taken_down = true, takedown_date = {DateTime.UtcNow}, takedown_reason = 'x' WHERE story_id = {storyId}"));
        long reportId = await SeedReportAsync(storyId);

        await DeleteAsAsync(_authorId, storyId);

        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM stories WHERE story_id = {storyId}")).Should().Be(0,
            "deletion is unconditional under moderation (D15)");
        using IServiceScope scope = Factory.Services.CreateScope();
        Report report = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Reports
            .AsNoTracking().SingleAsync(r => r.ReportId == reportId);
        (report.ReportStatusId, report.ReportedEntityId, report.ReportedUserId)
            .Should().Be((ReportStatusEnum.Open, (long)storyId, (int?)_authorId),
                "the ledger survives and keeps the author answerable; its status is owner-open (tracker F13)");
    }

    [Fact]
    public async Task Delete_OfAnApprovedStory_LeavesApprovedStorySubmissionsAndAutoApprove()
    {
        await SeedAsync(db => db.Users.Where(u => u.Id == _authorId).ExecuteUpdate(s => s
            .SetProperty(u => u.ApprovedStorySubmissions, 1)
            .SetProperty(u => u.CanAutoApprove, true)));

        await DeleteAsAsync(_authorId, _storyId);

        using IServiceScope scope = Factory.Services.CreateScope();
        var author = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users
            .Where(u => u.Id == _authorId).Select(u => new { u.ApprovedStorySubmissions, u.CanAutoApprove }).SingleAsync();
        (author.ApprovedStorySubmissions, author.CanAutoApprove).Should().Be((1, true),
            "a record of a decision, monotonic (D1): deleting an approved story is not farming trust back");
    }

    // ── D15's sub-ruling: an unstarted booking's slot comes back ─────────────────

    [Fact]
    public async Task Delete_WithAFutureBlockPlacement_ReturnsTheSlotToRedeemable()
    {
        int slotId = await GrantSlotToReaderAsync();
        await RedeemAsReaderAsync(slotId, NextBlockStart);

        await DeleteAsAsync(_authorId, _storyId);

        (await SlotStatusAsync(slotId)).Should().Be(SpotlightSlotStatus.Available, "the block had not started");
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM community_spotlights WHERE slot_id = {slotId}")).Should().Be(0);
        SetActiveUser(_readerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<ISpotlightReadService>().GetMyAvailableSlotsAsync())
            .Should().ContainSingle(s => s.SlotId == slotId, "the sponsor can book with it again");
    }

    [Fact]
    public async Task Delete_WithALiveBlockPlacement_ConsumesTheSlot()
    {
        int slotId = await GrantSlotToReaderAsync();
        await RedeemAsReaderAsync(slotId, CurrentBlockStart);

        await DeleteAsAsync(_authorId, _storyId);

        (await SlotStatusAsync(slotId)).Should().Be(SpotlightSlotStatus.Redeemed,
            "a block that has started counts as started (decision row 27 (a))");
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM community_spotlights WHERE slot_id = {slotId}")).Should().Be(0);
    }

    [Fact]
    public async Task Delete_WithAnAiredPlacement_ConsumesTheSlot_AndLeavesOtherStoriesSlotsAlone()
    {
        int airedSlot = await GrantSlotToReaderAsync();
        DateTime start = CurrentBlockStart.AddDays(-3 * BlockDays);
        await SeedAsync(db =>
        {
            db.SpotlightSlots.Where(s => s.SlotId == airedSlot)
                .ExecuteUpdate(u => u.SetProperty(s => s.Status, SpotlightSlotStatus.Redeemed));
            db.CommunitySpotlights.Add(new CommunitySpotlight
            {
                SlotId = airedSlot, StoryId = _storyId, SponsoringUserId = _readerId,
                StartDate = start, EndDate = start.AddDays(BlockDays), GoLiveNotifiedUtc = start,
            });
        });
        int otherStory = await SeedStoryAsync(_modId);
        int otherSlot = await GrantSlotToReaderAsync();
        await RedeemAsReaderAsync(otherSlot, NextBlockStart, otherStory);

        await DeleteAsAsync(_authorId, _storyId);

        (await SlotStatusAsync(airedSlot)).Should().Be(SpotlightSlotStatus.Redeemed, "it already aired");
        (await SlotStatusAsync(otherSlot)).Should().Be(SpotlightSlotStatus.Redeemed,
            "another story's unstarted booking is not this delete's to return");
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM community_spotlights WHERE story_id = {otherStory}")).Should().Be(1);
    }

    // ── Accepted dangles ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ANotificationThatNamedTheStory_ReadsBackAsAnInertRow()
    {
        await SeedAsync(db => db.Notifications.Add(new Notification
        {
            RecipientUserId = _readerId, NotificationTypeId = NotificationTypeEnum.NewStoryByFollowedUser,
            SourceUserId = _authorId, RelatedEntityId = _storyId, DateCreated = DateTime.UtcNow,
        }));

        await DeleteAsAsync(_authorId, _storyId);

        SetActiveUser(_readerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        NotificationDto[] feed = await scope.ServiceProvider.GetRequiredService<INotificationReadService>()
            .GetNotificationsAsync(1, 20);
        NotificationDto row = feed.Should().ContainSingle(n => n.RelatedEntityId == _storyId).Subject;
        (row.TargetTitle, row.TargetUrl).Should().Be(((string?)null, (string?)null),
            "a dangling RelatedEntityId resolves to no title and no link, never a throw (D15's accepted dangle)");
    }

    // ── Concurrency and the circuit ──────────────────────────────────────────────

    [Fact]
    public async Task Delete_AfterTheAuthorRepliedThroughTheSameScope_Succeeds_AndLeavesNothingTracked()
    {
        // The write context is circuit-scoped: an author who replied and then deletes the story does both
        // through one ApplicationDbContext. The tracker is cleared first (layer2-services.md §"Author story
        // deletion"), so nothing deleted set-based stays tracked for the circuit's next SaveChanges.
        int ch = await CreateChapterAsync("Ch 1");
        await PublishAsync(ch);
        long root = await SeedCommentAsync(ch, _readerId, "a reader's comment");

        SetActiveUser(_authorId);
        using IServiceScope scope = Factory.Services.CreateScope();
        long reply = await scope.ServiceProvider.GetRequiredService<ICommentWriteService>().PostChapterCommentAsync(
            new PostChapterCommentDto { ChapterId = ch, ParentCommentId = root, CommentText = "<p>thanks</p>" });
        ApplicationDbContext writeDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        writeDb.ChangeTracker.Entries().Should().NotBeEmpty("precondition: the reply is still tracked");

        await scope.ServiceProvider.GetRequiredService<IStoryWriteService>().DeleteStoryAsync(_storyId);

        writeDb.ChangeTracker.Entries().Should().BeEmpty();
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM base_comments WHERE comment_id = ANY({new[] { root, reply }})"))
            .Should().Be(0);
    }

    [Fact]
    public async Task ASecondDelete_OfTheSameStory_Is404_AndReturnsNoSlotTwice()
    {
        int slotId = await GrantSlotToReaderAsync();
        await RedeemAsReaderAsync(slotId, NextBlockStart);
        await DeleteAsAsync(_authorId, _storyId);
        // The sponsor books the returned slot elsewhere before the author's second tab retries.
        int elsewhere = await SeedStoryAsync(_modId);
        await RedeemAsReaderAsync(slotId, NextBlockStart, elsewhere);

        Func<Task> again = () => DeleteAsAsync(_authorId, _storyId);

        await again.Should().ThrowAsync<KeyNotFoundException>();
        (await SlotStatusAsync(slotId)).Should().Be(SpotlightSlotStatus.Redeemed, "the rebooked slot is untouched");
    }

    [Fact]
    public async Task ADeleteThatLosesARaceToAnotherDelete_Is404_AndRollsBack()
    {
        // A double submit from two tabs: both pass the owner gate, and the other tab's delete commits
        // between this one's gate and its story DELETE (testing.md: interleave, don't race). The 0-row
        // delete must answer as a sequential re-delete does (404), not report success.
        InterleavingCommandInterceptor otherTabDeletes = new(ConnectionString, "DELETE FROM stories",
            $"DELETE FROM stories WHERE story_id = {_storyId}");
        SetActiveUser(_authorId);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            (ServerStoryWriteService svc, ApplicationDbContext writeDb) = StoryWritesOver(scope, otherTabDeletes);
            await using (writeDb)
            {
                Func<Task> act = () => svc.DeleteStoryAsync(_storyId);
                await act.Should().ThrowAsync<KeyNotFoundException>();
            }
        }

        otherTabDeletes.Fired.Should().BeTrue("the competing delete must land between the gate and the delete");
    }

    [Fact]
    public async Task TheStoryRowIsDeleted_WhileTheSpotlightBookingLockIsHeld()
    {
        // The slot release and the story delete share one transaction under the booking lock, so a sponsor
        // cannot book the story between them (a booking that landed there would lose its slot to the
        // cascade). Proven from a second connection at the moment the story DELETE runs: the lock is taken.
        int slotId = await GrantSlotToReaderAsync();
        await RedeemAsReaderAsync(slotId, NextBlockStart);
        InterleavingCommandInterceptor lockProbe = new(ConnectionString, "DELETE FROM stories",
            """
            DO $$ BEGIN
                IF pg_try_advisory_lock(hashtext('canalave_spotlight_booking')) THEN
                    RAISE EXCEPTION 'the booking lock is not held while the story row is deleted';
                END IF;
            END $$;
            """);
        SetActiveUser(_authorId);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            (ServerStoryWriteService svc, ApplicationDbContext writeDb) = StoryWritesOver(scope, lockProbe);
            await using (writeDb)
                await svc.DeleteStoryAsync(_storyId);
        }

        lockProbe.Fired.Should().BeTrue();
        (await SlotStatusAsync(slotId)).Should().Be(SpotlightSlotStatus.Available,
            "the release ran inside the delete's transaction, which committed");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// <summary>The production service over a write context carrying <paramref name="interceptor"/>, with
    /// the Spotlight release sharing that context (as DI shares the scoped one in production), so both run
    /// in the delete's transaction. <see cref="InterleavingCommandInterceptor.CreateService{TService}"/>
    /// would resolve the release over the scope's own context instead.</summary>
    private (ServerStoryWriteService Service, ApplicationDbContext WriteDb) StoryWritesOver(
        IServiceScope scope, InterleavingCommandInterceptor interceptor)
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.EnableRetryOnFailure())
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(interceptor)
            .Options;
        ApplicationDbContext writeDb = new(options, scope.ServiceProvider.GetRequiredService<IActiveUserContext>());
        ServerStoryWriteService svc = ActivatorUtilities.CreateInstance<ServerStoryWriteService>(
            scope.ServiceProvider, writeDb, new SpotlightPlacementRelease(writeDb));
        return (svc, writeDb);
    }

    private async Task DeleteAsAsync(int? userId, int storyId)
    {
        SetActiveUser(userId is int id ? FakeActiveUserContext.AuthenticatedUser(id, showMatureContent: false) : FakeActiveUserContext.Anonymous());
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IStoryWriteService>().DeleteStoryAsync(storyId);
    }

    private async Task SeedAsync(Action<ApplicationDbContext> seed)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    private async Task<int> CountAsync(FormattableString sql)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Database.SqlQuery<int>(sql).SingleAsync();
    }

    private async Task<int> CreateChapterAsync(string title)
    {
        SetActiveUser(_authorId);
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IChapterWriteService>().CreateChapterAsync(new CreateChapterDto
        {
            StoryId = _storyId, Title = title, ChapterText = $"<p>Body of {title}.</p>",
        });
    }

    private async Task PublishAsync(int chapterId)
    {
        SetActiveUser(_authorId);
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IChapterWriteService>().SetPublishedAsync(chapterId, true);
    }

    private async Task<long> SeedCommentAsync(int chapterId, int commenterId, string text, long? parentId = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ChapterComment comment = new()
        {
            ChapterId = chapterId, UserId = commenterId, CommentText = text,
            ParentCommentId = parentId, DatePosted = DateTime.UtcNow,
        };
        db.ChapterComments.Add(comment);
        await db.SaveChangesAsync();
        return comment.CommentId;
    }

    private async Task<long> SeedReportAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        short reasonId = await db.ReportReasons.OrderBy(r => r.ReportReasonId).Select(r => r.ReportReasonId).FirstAsync();
        Report report = new()
        {
            ReportedEntityType = ReportedEntityType.Story, ReportedEntityId = storyId, ReportReasonId = reasonId,
            ReporterUserId = _readerId, ReportedUserId = _authorId, ReportStatusId = ReportStatusEnum.Open,
            DateReported = DateTime.UtcNow,
        };
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        return report.ReportId;
    }

    private async Task<int> GrantSlotToReaderAsync()
    {
        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISpotlightSlotAllocator>()
            .GrantSlotAsync(_readerId, SpotlightSlotSource.ModAward);
    }

    private async Task RedeemAsReaderAsync(int slotId, DateTime blockStart, int? storyId = null)
    {
        SetActiveUser(_readerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ISpotlightWriteService>()
            .RedeemSlotAsync(new RedeemSpotlightSlotDto(slotId, storyId ?? _storyId, null, blockStart));
    }

    private async Task<SpotlightSlotStatus> SlotStatusAsync(int slotId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().SpotlightSlots
            .Where(s => s.SlotId == slotId).Select(s => s.Status).SingleAsync();
    }
}
