using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// <see cref="ContentCounterRecalculator"/> (owner ruling D21, WU-CounterSymmetry): every one of its
/// 11 counter specs gets one row whose value drifted while ground truth exists (the match-and-correct
/// pass) and one whose value drifted with no ground truth at all (the zero-unmatched pass). The ground
/// truth is seeded through EF; the drift is applied afterwards with raw <c>UPDATE</c>s, the way a lost
/// update or a crash between two commits would leave it.
/// <para>
/// <b>Per-test seeding (FK parents):</b> three users (SeedUserAsync); two stories by the author
/// (SeedStoryAsync); chapters inline with the two-step <c>PrimaryContentId</c> (a Chapter ⇄
/// ChapterContent circular FK), so contents need their chapter and the chapter its story; a
/// <c>ChapterComment</c> needs its chapter and its commenter; a <c>ProfileBlogPost</c> its author;
/// a <c>Recommendation</c> its story, recommender, Approved status (2, HasData) and detail row; likes and
/// successes their users; a <c>Report</c> its reason (1, HasData) — the reported id is polymorphic, no FK.
/// </para>
/// Tier: Integration (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class ContentCounterRecalculatorTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    // A comment id above int.MaxValue: comment ids and reports.reported_entity_id are bigint, and the
    // active_report_count join must not narrow either side.
    private const long BigCommentId = 5_000_000_000L;

    private int _authorId, _userB, _userC;
    private int _story, _storyNoPublished;
    private int _ch1, _ch2, _ch3;
    private long _comment1, _comment2;
    private int _blog1, _blog2;
    private int _rec1, _rec2;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync("Author");
        _userB = await SeedUserAsync("B");
        _userC = await SeedUserAsync("C");
        _story = await SeedStoryAsync(_authorId);
        _storyNoPublished = await SeedStoryAsync(_authorId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);

        // ── Chapters: ch1 published (primary 100 words + a 50-word alternate), ch2 a draft (70 words),
        // ch3 a draft with no version at all (only possible mid-create, but legal in the schema).
        _ch1 = await AddChapterAsync(db, _story, 1, published: true, primaryWords: 100, alternateWords: 50);
        _ch2 = await AddChapterAsync(db, _story, 2, published: false, primaryWords: 70, alternateWords: null);
        Chapter empty = new() { StoryId = _story, ChapterNumber = 3, Title = "Ch3", IsPublished = false, VersionCount = 0 };
        db.Chapters.Add(empty);
        await db.SaveChangesAsync();
        _ch3 = empty.ChapterId;

        // ── Comments: comment1 (big id) liked by the author and C; comment2 liked by nobody.
        ChapterComment c1 = new() { CommentId = BigCommentId, ChapterId = _ch1, UserId = _userB, CommentText = "c1", DatePosted = DateTime.UtcNow };
        ChapterComment c2 = new() { ChapterId = _ch1, UserId = _userB, CommentText = "c2", DatePosted = DateTime.UtcNow };
        db.ChapterComments.AddRange(c1, c2);
        await db.SaveChangesAsync();
        _comment1 = c1.CommentId;
        _comment2 = c2.CommentId;
        db.CommentLikes.AddRange(
            new CommentLike { CommentId = _comment1, UserId = _authorId },
            new CommentLike { CommentId = _comment1, UserId = _userC });

        // ── Blog posts: blog1 liked by B; blog2 by nobody.
        ProfileBlogPost b1 = NewPost("b1"), b2 = NewPost("b2");
        db.ProfileBlogPosts.AddRange(b1, b2);
        await db.SaveChangesAsync();
        _blog1 = b1.BlogPostId;
        _blog2 = b2.BlogPostId;
        db.BlogPostLikes.Add(new BlogPostLike { BlogPostId = _blog1, UserId = _userB });

        // ── Recommendations: rec1 (by B) has one like (author) and one success (C); rec2 (by C) none.
        Recommendation r1 = NewRec(_userB), r2 = NewRec(_userC);
        db.Recommendations.AddRange(r1, r2);
        await db.SaveChangesAsync();
        _rec1 = r1.RecommendationId;
        _rec2 = r2.RecommendationId;
        db.RecommendationLikes.Add(new RecommendationLike { RecommendationId = _rec1, UserId = _authorId });
        db.RecommendationSuccesses.Add(new RecommendationSuccess { RecommendationId = _rec1, UserId = _userC, DateRecorded = DateTime.UtcNow });

        // ── Reports. Counted: Open + UnderReview. Not counted: resolved ones, and a Message report whose
        // numeric id collides with a user id (the type must discriminate). One open report per reporter
        // per target (ix_reports_open_reporter_target), so the two open ones on B come from two reporters.
        db.Reports.AddRange(
            NewReport(ReportedEntityType.User, _userB, _authorId, ReportStatusEnum.Open),
            NewReport(ReportedEntityType.User, _userB, _userC, ReportStatusEnum.UnderReview),
            NewReport(ReportedEntityType.User, _userB, _userC, ReportStatusEnum.ResolvedActionTaken),
            NewReport(ReportedEntityType.User, _userC, _authorId, ReportStatusEnum.ResolvedNoAction),
            NewReport(ReportedEntityType.Message, _userC, _userB, ReportStatusEnum.Open),
            NewReport(ReportedEntityType.Story, _story, _userB, ReportStatusEnum.Open),
            NewReport(ReportedEntityType.Story, _storyNoPublished, _userB, ReportStatusEnum.ResolvedNoAction),
            NewReport(ReportedEntityType.Comment, _comment1, _userC, ReportStatusEnum.Open),
            NewReport(ReportedEntityType.BlogPost, _blog1, _userB, ReportStatusEnum.UnderReview),
            NewReport(ReportedEntityType.Recommendation, _rec1, _authorId, ReportStatusEnum.Open));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task EverySpec_CorrectsADriftedValue_AndZeroesADriftedValueWithNoGroundTruth()
    {
        await DriftAsync(
            // like_count ×3
            $"UPDATE base_comments SET like_count = 5 WHERE comment_id = {_comment1}",
            $"UPDATE base_comments SET like_count = 3 WHERE comment_id = {_comment2}",
            $"UPDATE base_blog_posts SET like_count = 0 WHERE blog_post_id = {_blog1}",
            $"UPDATE base_blog_posts SET like_count = 4 WHERE blog_post_id = {_blog2}",
            $"UPDATE recommendations SET like_count = 9 WHERE recommendation_id = {_rec1}",
            $"UPDATE recommendations SET like_count = 2 WHERE recommendation_id = {_rec2}",
            // successful_rec_count
            $"UPDATE recommendations SET successful_rec_count = 0 WHERE recommendation_id = {_rec1}",
            $"UPDATE recommendations SET successful_rec_count = 6 WHERE recommendation_id = {_rec2}",
            // version_count (ch2 left correct at 1)
            $"UPDATE chapters SET version_count = 7 WHERE chapter_id = {_ch1}",
            $"UPDATE chapters SET version_count = 4 WHERE chapter_id = {_ch3}",
            // word_count
            $"UPDATE stories SET word_count = 999 WHERE story_id = {_story}",
            $"UPDATE stories SET word_count = 55 WHERE story_id = {_storyNoPublished}",
            // active_report_count ×5
            $"UPDATE \"AspNetUsers\" SET active_report_count = 0 WHERE id = {_userB}",
            $"UPDATE \"AspNetUsers\" SET active_report_count = 3 WHERE id = {_userC}",
            $"UPDATE stories SET active_report_count = 0 WHERE story_id = {_story}",
            $"UPDATE stories SET active_report_count = 3 WHERE story_id = {_storyNoPublished}",
            $"UPDATE base_comments SET active_report_count = 0 WHERE comment_id = {_comment1}",
            $"UPDATE base_comments SET active_report_count = 2 WHERE comment_id = {_comment2}",
            $"UPDATE base_blog_posts SET active_report_count = 4 WHERE blog_post_id = {_blog1}",
            $"UPDATE base_blog_posts SET active_report_count = 1 WHERE blog_post_id = {_blog2}",
            $"UPDATE recommendations SET active_report_count = 0 WHERE recommendation_id = {_rec1}",
            $"UPDATE recommendations SET active_report_count = 1 WHERE recommendation_id = {_rec2}");

        ContentCounterRecalcResult first = await RecalculateAsync();
        first.CountersCorrected.Should().Be(22,
            "exactly the 22 drifted values — the IS DISTINCT FROM guard makes rows-affected mean 'actually corrected'");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Db(scope);

        (await CommentAsync(db, _comment1)).Should().Be((2, 1), "two like rows; one Open report on the big-id comment");
        (await CommentAsync(db, _comment2)).Should().Be((0, 0));
        (await BlogAsync(db, _blog1)).Should().Be((1, 1), "one like; one UnderReview report");
        (await BlogAsync(db, _blog2)).Should().Be((0, 0));
        (await RecAsync(db, _rec1)).Should().Be((1, 1, 1), "one like, one success, one Open report");
        (await RecAsync(db, _rec2)).Should().Be((0, 0, 0));

        (await db.Chapters.Where(c => c.ChapterId == _ch1).Select(c => c.VersionCount).SingleAsync()).Should().Be(2);
        (await db.Chapters.Where(c => c.ChapterId == _ch2).Select(c => c.VersionCount).SingleAsync()).Should().Be(1);
        (await db.Chapters.Where(c => c.ChapterId == _ch3).Select(c => c.VersionCount).SingleAsync()).Should().Be(0);

        (await StoryAsync(db, _story)).Should().Be((100, 1),
            "word_count is ch1's primary only — the draft ch2 (70) and the non-primary alternate (50) are excluded");
        (await StoryAsync(db, _storyNoPublished)).Should().Be((0, 0), "no published chapter; only a resolved report");

        (await UserReportsAsync(db, _userB)).Should().Be(2, "Open + UnderReview count; the resolved report does not");
        (await UserReportsAsync(db, _userC)).Should().Be(0,
            "only a resolved User report targets C, and the Open Message report whose numeric id equals C's is not a User report");
    }

    [Fact]
    public async Task ASecondPass_CorrectsNothing()
    {
        await DriftAsync(
            $"UPDATE base_comments SET like_count = 5 WHERE comment_id = {_comment1}",
            $"UPDATE stories SET word_count = 999 WHERE story_id = {_story}");

        (await RecalculateAsync()).CountersCorrected.Should().BeGreaterThan(0);
        (await RecalculateAsync()).CountersCorrected.Should().Be(0, "idempotent: a converged database corrects nothing");
    }

    [Fact]
    public void TheSpecCount_IsEleven()
    {
        // like_count ×3, successful_rec_count, version_count, stories.word_count, active_report_count ×5 —
        // the test above drifts every one of them.
        ContentCounterRecalculator.CounterCount.Should().Be(11);
    }

    // ── helpers ──────────────────────────────────────────────────────────────────

    private static ApplicationDbContext Db(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    private async Task<ContentCounterRecalcResult> RecalculateAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ContentCounterRecalculator>().RecalculateAllAsync();
    }

    private async Task DriftAsync(params string[] statements)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        foreach (string sql in statements)
            (await Db(scope).Database.ExecuteSqlRawAsync(sql)).Should().Be(1, sql);
    }

    private static async Task<int> AddChapterAsync(
        ApplicationDbContext db, int storyId, int number, bool published, int primaryWords, int? alternateWords)
    {
        Chapter chapter = new()
        {
            StoryId = storyId, ChapterNumber = number, Title = $"Ch{number}", IsPublished = published,
            FirstPublishedDate = published ? DateTime.UtcNow : null, VersionCount = alternateWords is null ? 1 : 2,
        };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();

        ChapterContent primary = new()
        {
            ChapterId = chapter.ChapterId, AuthorId = null, ChapterText = "primary", SortOrder = 0,
            WordCount = primaryWords, PublishDate = published ? DateTime.UtcNow : null,
        };
        db.ChapterContents.Add(primary);
        if (alternateWords is int alt)
            db.ChapterContents.Add(new ChapterContent
            {
                ChapterId = chapter.ChapterId, AuthorId = null, ChapterText = "alternate", SortOrder = 1,
                WordCount = alt, PublishDate = published ? DateTime.UtcNow : null,
            });
        await db.SaveChangesAsync();

        chapter.PrimaryContentId = primary.ChapterContentId;
        await db.SaveChangesAsync();
        return chapter.ChapterId;
    }

    private ProfileBlogPost NewPost(string title) => new()
    {
        AuthorId = _authorId, Title = title, Content = "content", IsPublished = true,
        DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow, Rating = Rating.E,
    };

    private Recommendation NewRec(int recommenderId) => new()
    {
        StoryId = _story, RecommenderId = recommenderId, StatusId = 2, DatePosted = DateTime.UtcNow,
        RecommendationDetail = new RecommendationDetail { Text = "rec" },
    };

    private static Report NewReport(ReportedEntityType type, long id, int reporterId, ReportStatusEnum status) => new()
    {
        ReporterUserId = reporterId, ReportedEntityType = type, ReportedEntityId = id, ReportReasonId = 1,
        ReportStatusId = status, DateReported = DateTime.UtcNow,
        DateResolved = status is ReportStatusEnum.ResolvedNoAction or ReportStatusEnum.ResolvedActionTaken ? DateTime.UtcNow : null,
    };

    // Anonymous-type projections (Npgsql cannot read a projected ValueTuple), mapped to tuples here.
    private static async Task<(int Likes, int Reports)> CommentAsync(ApplicationDbContext db, long id)
    {
        var c = await db.BaseComments.Where(x => x.CommentId == id)
            .Select(x => new { x.LikeCount, x.ActiveReportCount }).SingleAsync();
        return (c.LikeCount, c.ActiveReportCount);
    }

    private static async Task<(int Likes, int Reports)> BlogAsync(ApplicationDbContext db, int id)
    {
        var b = await db.BlogPosts.Where(x => x.BlogPostId == id)
            .Select(x => new { x.LikeCount, x.ActiveReportCount }).SingleAsync();
        return (b.LikeCount, b.ActiveReportCount);
    }

    private static async Task<(int Likes, int Successes, int Reports)> RecAsync(ApplicationDbContext db, int id)
    {
        var r = await db.Recommendations.Where(x => x.RecommendationId == id)
            .Select(x => new { x.LikeCount, x.SuccessfulRecCount, x.ActiveReportCount }).SingleAsync();
        return (r.LikeCount, r.SuccessfulRecCount, r.ActiveReportCount);
    }

    private static async Task<(int Words, int Reports)> StoryAsync(ApplicationDbContext db, int id)
    {
        var s = await db.Stories.Where(x => x.StoryId == id)
            .Select(x => new { x.WordCount, x.ActiveReportCount }).SingleAsync();
        return (s.WordCount, s.ActiveReportCount);
    }

    private static async Task<int> UserReportsAsync(ApplicationDbContext db, int id) =>
        await db.Users.Where(u => u.Id == id).Select(u => u.ActiveReportCount).SingleAsync();
}
