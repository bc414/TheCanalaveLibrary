using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="SiteDailyStatAggregator"/> (Feature 62, WU-SiteDailyStat) —
/// one dated event of every counted kind is seeded on a target UTC day (plus one deliberately
/// outside the day's range, to prove boundary filtering), the aggregator upserts, and every column
/// on the resulting <c>site_daily_stats</c> row is asserted. A second pass proves the upsert
/// <b>recomputes</b> rather than accumulates (unlike the view-count flusher's <c>+=</c>).
///
/// <c>site_daily_stats</c> is the one Layer-8 table with an EF model (layer8-data-marts.md), so
/// this test reads it via <c>ApplicationDbContext.SiteDailyStats</c> like any other entity — no
/// raw-SQL row hand-mapping needed on the assertion side.
/// </summary>
[Collection("Postgres")]
public class SiteDailyStatAggregatorTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private readonly DateOnly _day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3));
    private DateTime InDay => _day.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);
    private DateTime BeforeDay => _day.AddDays(-1).ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

    private int _userAId;
    private int _storyS1Id;
    private int _chapterId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        _userAId = await SeedUserAsync("StatsUserA");
        int userBId = await SeedUserAsync("StatsUserB");

        // --- new_users / total_users: A is "new" today, B predates the window (total only) ---
        await db.Database.ExecuteSqlAsync($"UPDATE \"AspNetUsers\" SET created_utc = {InDay} WHERE id = {_userAId}");
        await db.Database.ExecuteSqlAsync($"UPDATE \"AspNetUsers\" SET created_utc = {_day.AddDays(-30).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)} WHERE id = {userBId}");
        // --- active_users: A pinged today; B never did ---
        await db.Database.ExecuteSqlAsync($"UPDATE \"AspNetUsers\" SET last_active_utc = {InDay} WHERE id = {_userAId}");

        // --- new_stories / total_stories / total_words: S1 published today (word_count 500),
        // S2 published earlier (word_count 300) — both visible/InProgress, both count toward totals.
        _storyS1Id = await SeedStoryAsync(_userAId);
        int storyS2Id = await SeedStoryAsync(_userAId);
        await db.Database.ExecuteSqlAsync($"UPDATE stories SET published_date = {InDay}, word_count = 500 WHERE story_id = {_storyS1Id}");
        await db.Database.ExecuteSqlAsync($"UPDATE stories SET published_date = {_day.AddDays(-10).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)}, word_count = 300 WHERE story_id = {storyS2Id}");

        // --- new_chapters / new_words: one published chapter on S1 today, 500 words ---
        Chapter chapter = new() { StoryId = _storyS1Id, ChapterNumber = 1, Title = "Ch1", IsPublished = true };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        ChapterContent content = new()
        {
            ChapterId = chapter.ChapterId, AuthorId = _userAId, ChapterText = "text",
            WordCount = 500, PublishDate = InDay,
        };
        db.ChapterContents.Add(content);
        await db.SaveChangesAsync();
        chapter.PrimaryContentId = content.ChapterContentId;
        await db.SaveChangesAsync();
        _chapterId = chapter.ChapterId;

        // --- new_comments: one in-window ChapterComment, one deliberately BEFORE the window
        // (proves day-boundary filtering excludes it) ---
        db.ChapterComments.Add(new ChapterComment
        {
            ChapterId = _chapterId, UserId = _userAId, CommentText = "in window", DatePosted = InDay,
        });
        db.ChapterComments.Add(new ChapterComment
        {
            ChapterId = _chapterId, UserId = _userAId, CommentText = "before window", DatePosted = BeforeDay,
        });
        await db.SaveChangesAsync();

        // --- new_blog_posts ---
        db.ProfileBlogPosts.Add(new ProfileBlogPost
        {
            AuthorId = _userAId, Title = "post", Content = "content", IsPublished = true,
            DateCreated = InDay, LastUpdatedDate = InDay, Rating = Rating.E,
        });

        // --- new_groups ---
        db.Groups.Add(new Group
        {
            CreatorId = _userAId, GroupName = "Test Group", AudienceRating = Rating.E,
            MaxContentRating = Rating.E, DateCreated = InDay,
        });

        // --- new_follows ---
        db.FollowedUsers.Add(new FollowedUser { UserId = _userAId, FollowedUserId = userBId, DateFollowed = InDay });

        // --- new_recommendations_written ---
        Recommendation rec = new()
        {
            StoryId = _storyS1Id, RecommenderId = _userAId, StatusId = 2, DatePosted = InDay,
            RecommendationDetail = new RecommendationDetail { Text = "great story" },
        };
        db.Recommendations.Add(rec);
        await db.SaveChangesAsync();

        // --- new_recommendation_successes ---
        db.RecommendationSuccesses.Add(new RecommendationSuccess
        {
            UserId = userBId, RecommendationId = rec.RecommendationId, DateRecorded = InDay,
        });

        // --- reports_filed (filed today, still open) and reports_resolved (filed earlier,
        // resolved today) — kept as two separate rows so the two counters can't be conflated ---
        db.Reports.Add(new Report
        {
            ReporterUserId = _userAId, ReportedEntityType = ReportedEntityType.Story,
            ReportedEntityId = _storyS1Id, ReportReasonId = 1,
            ReportStatusId = ReportStatusEnum.Open, DateReported = InDay,
        });
        db.Reports.Add(new Report
        {
            ReporterUserId = _userAId, ReportedEntityType = ReportedEntityType.Story,
            ReportedEntityId = storyS2Id, ReportReasonId = 1,
            ReportStatusId = ReportStatusEnum.ResolvedNoAction,
            DateReported = BeforeDay, DateResolved = InDay,
        });

        // --- favorites_added ---
        db.UserStoryInteractions.Add(new UserStoryInteraction
        {
            UserId = _userAId, StoryId = _storyS1Id, IsFavorite = true,
            InteractionDatePartition = new UserStoryInteractionDate { FavoriteDate = InDay },
        });

        // --- chapters_read (approximate proxy) ---
        db.UserChapterInteractions.Add(new UserChapterInteraction
        {
            UserId = _userAId, ChapterId = _chapterId, ReadProgress = 1.0f, IsRead = true,
            LastInteractionDate = InDay,
        });

        await db.SaveChangesAsync();

        // --- story_views: daily_story_stats has no EF model — raw SQL, matching the R2 pattern ---
        await db.Database.ExecuteSqlAsync(
            $"INSERT INTO daily_story_stats (story_id, stat_date, view_count) VALUES ({_storyS1Id}, {_day}, 42)");
    }

    [Fact]
    public async Task UpsertDayAsync_ComputesEveryCounter()
    {
        await UpsertAsync();

        SiteDailyStat? row = await LoadRowAsync();
        row.Should().NotBeNull();

        row!.TotalUsers.Should().BeGreaterThanOrEqualTo(2, "at least the two seeded users exist by this day");
        row.TotalStories.Should().BeGreaterThanOrEqualTo(2);
        row.TotalWords.Should().BeGreaterThanOrEqualTo(800, "S1 (500) + S2 (300)");

        row.NewUsers.Should().Be(1, "only user A was created inside the window");
        row.NewStories.Should().Be(1, "only S1 was published inside the window");
        row.NewChapters.Should().Be(1);
        row.NewWords.Should().Be(500);
        row.NewComments.Should().Be(1, "the before-window comment must be excluded by the day boundary");
        row.NewBlogPosts.Should().Be(1);
        row.NewGroups.Should().Be(1);
        row.NewFollows.Should().Be(1);
        row.NewRecommendationsWritten.Should().Be(1);
        row.NewRecommendationSuccesses.Should().Be(1);
        row.ReportsFiled.Should().Be(1, "only the today-filed report counts, not the today-resolved one");
        row.ReportsResolved.Should().Be(1, "only the today-resolved report counts, not the today-filed one");
        row.FavoritesAdded.Should().Be(1);
        row.ChaptersRead.Should().Be(1);
        row.StoryViews.Should().Be(42);
        row.ActiveUsers.Should().Be(1, "only user A pinged last_active_utc inside the window");
    }

    [Fact]
    public async Task UpsertDayAsync_Recomputes_RatherThanAccumulates()
    {
        await UpsertAsync();
        (await LoadRowAsync())!.NewComments.Should().Be(1);

        // A second in-window comment lands after the first upsert...
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ChapterComments.Add(new ChapterComment
            {
                ChapterId = _chapterId, UserId = _userAId, CommentText = "second", DatePosted = InDay,
            });
            await db.SaveChangesAsync();
        }

        await UpsertAsync();

        SiteDailyStat? row = await LoadRowAsync();
        row!.NewComments.Should().Be(2, "the upsert recomputes the true count — it does not add onto the old value");

        int rowCount = await CountRowsForDayAsync();
        rowCount.Should().Be(1, "ON CONFLICT (stat_date) DO UPDATE — same day never produces a second row");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    private async Task UpsertAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        SiteDailyStatAggregator aggregator = scope.ServiceProvider.GetRequiredService<SiteDailyStatAggregator>();
        await aggregator.UpsertDayAsync(_day);
    }

    private async Task<SiteDailyStat?> LoadRowAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.SiteDailyStats.SingleOrDefaultAsync(s => s.StatDate == _day);
    }

    private async Task<int> CountRowsForDayAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.SiteDailyStats.CountAsync(s => s.StatDate == _day);
    }
}
