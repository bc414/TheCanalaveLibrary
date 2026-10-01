using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The home of owner ruling D10's invariant (WU-TptHardDelete): <b>deleting a content parent leaves no
/// TPT base row behind</b> — no <c>base_comments</c> row for its comments, no <c>base_polls</c> row (or
/// option, or vote) for its polls — on every delete path: chapter, profile / site / group blog post,
/// and the moderation hard delete of a story or blog post. Plus the FK posture itself: each of the five
/// content-parent → TPT-child FKs refuses a raw parent delete with 23001 (the only tests that catch a
/// future migration silently reverting RESTRICT to CASCADE), and a catalog guard over
/// <c>pg_constraint.confdeltype</c> for those five plus the two SET NULL FKs that must not flip
/// (<c>parent_comment_id</c>, D12; the poll owner, D11).
///
/// <para><b>Assertions read raw SQL.</b> "Zero surviving base rows" is counted with
/// <c>SELECT COUNT(*) … WHERE id = ANY(@ids)</c> over ids captured before the delete. An EF query over
/// <c>BaseComments</c> would throw while materializing an orphan (the very poison D10 describes) and
/// make a failing test unreadable.</para>
///
/// <para><b>Seeding plan (CLAUDE.md Phase-4 rules):</b>
/// <list type="bullet">
///   <item><description><b>Per-test seeding:</b> <c>InitializeAsync</c> seeds an author, two other
///   users (commenter/voter) and a moderator via <c>SeedUserAsync</c>; ids only.</description></item>
///   <item><description><b>FK parents:</b> stories via <c>SeedStoryAsync(author)</c>; chapters via
///   <c>IChapterWriteService.CreateChapterAsync</c> (real <c>PrimaryContentId</c>); profile and site
///   posts inline (author / moderator as FK parent); groups via
///   <c>IGroupWriteService.CreateGroupAsync</c> (creator becomes a member); comments, replies, likes,
///   polls, options and votes inline (users and parents above); reports inline (reporter user;
///   <c>ReportReasons</c> from <c>HasData</c>).</description></item>
///   <item><description><b>Count-sensitive:</b> none.</description></item>
/// </list></para>
/// Tier: Integration (Testcontainers Postgres; the migrations under test are the schema).
/// </summary>
[Collection("Postgres")]
public class TptHardDeleteTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _otherId;
    private int _thirdId;
    private int _modId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync("author");
        _otherId  = await SeedUserAsync("other");
        _thirdId  = await SeedUserAsync("third");
        _modId    = await SeedUserAsync("mod");
    }

    // ── 1. Chapter delete ────────────────────────────────────────────────────────

    [Fact]
    public async Task ChapterDelete_LeavesNoBaseComment_Reply_OrLike()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int chapterId = await CreateChapterAsync(storyId, "Ch 1");
        long rootId  = await SeedChapterCommentAsync(chapterId, _otherId, "root");
        long replyId = await SeedChapterCommentAsync(chapterId, _thirdId, "reply", parentId: rootId);
        await SeedCommentLikeAsync(rootId, _thirdId);
        long[] ids = [rootId, replyId];

        SetActiveUser(_authorId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IChapterWriteService>().DeleteChapterAsync(chapterId);

        (await CountBaseCommentsAsync(ids)).Should().Be(0, "no base_comments row may outlive its chapter");
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM comment_likes WHERE comment_id = ANY({ids})"))
            .Should().Be(0, "likes cascade off the deleted base rows");
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM chapters WHERE chapter_id = {chapterId}"))
            .Should().Be(0);
    }

    // ── 2–4. Blog-post deletes, all three subtypes ───────────────────────────────

    [Fact]
    public async Task ProfilePostDelete_LeavesNoBaseComment_OrPollRows()
    {
        int postId = await SeedProfilePostAsync(_authorId);
        long[] commentIds = await SeedBlogPostCommentsAsync(postId);
        (int pollId, int[] optionIds) = await SeedPollWithVotesAsync(postId, _authorId);

        SetActiveUser(_authorId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IBlogPostWriteService>().DeleteBlogPostAsync(postId);

        await AssertBlogPostGoneWithDependentsAsync(postId, commentIds, pollId, optionIds);
    }

    [Fact]
    public async Task SitePostDelete_LeavesNoBaseComment_OrPollRows()
    {
        int postId = await SeedSitePostAsync(_modId);
        long[] commentIds = await SeedBlogPostCommentsAsync(postId);
        (int pollId, int[] optionIds) = await SeedPollWithVotesAsync(postId, _modId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IBlogPostWriteService>().DeleteSiteBlogPostAsync(postId);

        await AssertBlogPostGoneWithDependentsAsync(postId, commentIds, pollId, optionIds);
    }

    [Fact]
    public async Task GroupPostDelete_ByItsAuthor_Succeeds_AndLeavesNoBaseComment_OrPollRows()
    {
        // D10's group-post lifecycle case: before WU-TptHardDelete a group-post author could never
        // delete their post (the profile-only path hit a 0-row stub delete → 500).
        int groupId = await CreateGroupAsync(_authorId);
        int postId = await CreateGroupPostAsync(groupId, _authorId);
        long[] commentIds = await SeedBlogPostCommentsAsync(postId);
        (int pollId, int[] optionIds) = await SeedPollWithVotesAsync(postId, _authorId);

        SetActiveUser(_authorId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IBlogPostWriteService>().DeleteGroupBlogPostAsync(postId);

        await AssertBlogPostGoneWithDependentsAsync(postId, commentIds, pollId, optionIds);
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM group_blog_posts WHERE blog_post_id = {postId}"))
            .Should().Be(0);
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM groups WHERE group_id = {groupId}"))
            .Should().Be(1, "deleting a post never touches its group");
    }

    // ── 5–6. Moderation hard delete ──────────────────────────────────────────────

    [Fact]
    public async Task ModerationHardDelete_OfAStory_LeavesNoBaseCommentFromAnyChapter()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int ch1 = await CreateChapterAsync(storyId, "Ch 1");
        int ch2 = await CreateChapterAsync(storyId, "Ch 2");
        long c1    = await SeedChapterCommentAsync(ch1, _otherId, "on chapter 1");
        long reply = await SeedChapterCommentAsync(ch1, _thirdId, "reply on chapter 1", parentId: c1);
        long c2    = await SeedChapterCommentAsync(ch2, _otherId, "on chapter 2");
        await SeedCommentLikeAsync(c2, _thirdId);
        long[] ids = [c1, reply, c2];
        long reportId = await SeedReportAsync(ReportedEntityType.Story, storyId, _otherId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IModerationWriteService>()
                .ResolveWithRemovalAsync(reportId, "Illegal content.", hardDelete: true);

        (await CountBaseCommentsAsync(ids)).Should().Be(0,
            "a story's hard delete must leave no base comment from any of its chapters (D10)");
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM stories WHERE story_id = {storyId}")).Should().Be(0);
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM chapters WHERE story_id = {storyId}")).Should().Be(0);
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM chapter_contents WHERE chapter_id = ANY({new[] { ch1, ch2 }})"))
            .Should().Be(0, "contents cascade with their chapters past the Restrict primary_content_id FK");

        using IServiceScope check = Factory.Services.CreateScope();
        ApplicationDbContext db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.Reports.Where(r => r.ReportId == reportId).Select(r => r.ReportStatusId).SingleAsync())
            .Should().Be(ReportStatusEnum.ResolvedActionTaken);
    }

    [Fact]
    public async Task ModerationHardDelete_OfABlogPost_LeavesNoBaseComment_OrPollRows()
    {
        int postId = await SeedProfilePostAsync(_authorId);
        long[] commentIds = await SeedBlogPostCommentsAsync(postId);
        (int pollId, int[] optionIds) = await SeedPollWithVotesAsync(postId, _authorId);
        long reportId = await SeedReportAsync(ReportedEntityType.BlogPost, postId, _otherId);

        SetActiveUser(FakeActiveUserContext.Moderator(_modId));
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IModerationWriteService>()
                .ResolveWithRemovalAsync(reportId, "Illegal content.", hardDelete: true);

        await AssertBlogPostGoneWithDependentsAsync(postId, commentIds, pollId, optionIds);
    }

    // ── 7. The FK posture itself: a raw parent delete with children present is refused ──

    [Fact]
    public async Task RawDelete_OfAChapterWithAComment_IsRefusedByTheChapterCommentsFk()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int chapterId = await CreateChapterAsync(storyId, "Ch 1");
        await SeedChapterCommentAsync(chapterId, _otherId, "still here");

        // Release the chapter's own Restrict FK to its primary content first, so the only constraint
        // the delete can trip is the one under test.
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.ExecuteSqlAsync(
                $"UPDATE chapters SET primary_content_id = NULL WHERE chapter_id = {chapterId}");

        await AssertParentDeleteRefusedAsync(
            $"DELETE FROM chapters WHERE chapter_id = {chapterId}",
            "fk_chapter_comments_chapters_chapter_id");
    }

    [Fact]
    public async Task RawDelete_OfABlogPostWithAComment_IsRefusedByTheBlogPostCommentsFk()
    {
        int postId = await SeedProfilePostAsync(_authorId);
        await SeedBlogPostCommentsAsync(postId);

        await AssertParentDeleteRefusedAsync(
            $"DELETE FROM base_blog_posts WHERE blog_post_id = {postId}",
            "fk_blog_post_comments_blog_posts_blog_post_id");
    }

    [Fact]
    public async Task RawDelete_OfABlogPostWithAPoll_IsRefusedByTheBlogPostPollsFk()
    {
        int postId = await SeedProfilePostAsync(_authorId);
        await SeedPollWithVotesAsync(postId, _authorId);

        await AssertParentDeleteRefusedAsync(
            $"DELETE FROM base_blog_posts WHERE blog_post_id = {postId}",
            "fk_blog_post_polls_base_blog_posts_blog_post_id");
    }

    [Fact]
    public async Task RawDelete_OfAGroupWithAComment_IsRefusedByTheGroupCommentsFk()
    {
        int groupId = await CreateGroupAsync(_authorId);
        await SeedGroupCommentAsync(groupId, _authorId);

        await AssertParentDeleteRefusedAsync(
            $"DELETE FROM groups WHERE group_id = {groupId}",
            "fk_group_comments_groups_group_id");
    }

    [Fact]
    public async Task RawDelete_OfAGroupWithAPost_IsRefusedByTheGroupBlogPostsFk()
    {
        int groupId = await CreateGroupAsync(_authorId);
        await CreateGroupPostAsync(groupId, _authorId);

        await AssertParentDeleteRefusedAsync(
            $"DELETE FROM groups WHERE group_id = {groupId}",
            "fk_group_blog_posts_groups_group_id");
    }

    // ── 8. Catalog guard ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Catalog_TheFiveContentParentFksAreRestrict_AndTheTwoSetNullFksAreUnchanged()
    {
        Dictionary<string, string> expected = new()
        {
            ["fk_chapter_comments_chapters_chapter_id"]          = "r",
            ["fk_blog_post_comments_blog_posts_blog_post_id"]    = "r",
            ["fk_blog_post_polls_base_blog_posts_blog_post_id"]  = "r",
            ["fk_group_comments_groups_group_id"]                = "r",
            ["fk_group_blog_posts_groups_group_id"]              = "r",
            // D12: the reparent mechanism — must stay SET NULL, never swept up into RESTRICT.
            ["fk_base_comments_base_comments_parent_comment_id"] = "n",
            // D11: a poll survives its owner.
            ["fk_base_polls_asp_net_users_owner_id"]             = "n",
        };

        Dictionary<string, string> actual = [];
        await using NpgsqlConnection conn = new(ConnectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = new(
            "SELECT conname, confdeltype::text FROM pg_constraint WHERE contype = 'f' AND conname = ANY(@names)", conn);
        cmd.Parameters.AddWithValue("names", expected.Keys.ToArray());
        await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                actual[reader.GetString(0)] = reader.GetString(1);

        actual.Should().BeEquivalentTo(expected,
            "confdeltype: r = RESTRICT, n = SET NULL (owner rulings D10, D11, D12)");
    }

    // ── Assertion helpers ────────────────────────────────────────────────────────

    private async Task<int> CountAsync(FormattableString sql)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Database.SqlQuery<int>(sql).SingleAsync();
    }

    private Task<int> CountBaseCommentsAsync(long[] ids) =>
        CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM base_comments WHERE comment_id = ANY({ids})");

    private async Task AssertBlogPostGoneWithDependentsAsync(int postId, long[] commentIds, int pollId, int[] optionIds)
    {
        (await CountBaseCommentsAsync(commentIds)).Should().Be(0,
            "no base_comments row may outlive its blog post (D10)");
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM base_polls WHERE poll_id = {pollId}"))
            .Should().Be(0, "no base_polls row may outlive its blog post (D10)");
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM poll_options WHERE poll_option_id = ANY({optionIds})"))
            .Should().Be(0, "options cascade off the deleted base_polls row");
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM poll_votes WHERE poll_option_id = ANY({optionIds})"))
            .Should().Be(0, "votes cascade off the deleted options");
        (await CountAsync($"SELECT COUNT(*)::int AS \"Value\" FROM base_blog_posts WHERE blog_post_id = {postId}"))
            .Should().Be(0);
    }

    private async Task AssertParentDeleteRefusedAsync(FormattableString sql, string constraint)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Func<Task> act = () => db.Database.ExecuteSqlAsync(sql);

        // 23001, not 23503: Postgres reports an ON DELETE RESTRICT refusal as restrict_violation
        // (a NO ACTION FK would give foreign_key_violation) — so this also pins RESTRICT itself.
        PostgresException ex = (await act.Should().ThrowAsync<PostgresException>()).Which;
        ex.SqlState.Should().Be(PostgresErrorCodes.RestrictViolation);
        ex.ConstraintName.Should().Be(constraint,
            "a CASCADE here would silently orphan the TPT base rows (D10)");
    }

    // ── Seeding helpers ──────────────────────────────────────────────────────────

    private async Task<int> CreateChapterAsync(int storyId, string title)
    {
        SetActiveUser(_authorId);
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IChapterWriteService>().CreateChapterAsync(new CreateChapterDto
        {
            StoryId     = storyId,
            Title       = title,
            ChapterText = $"<p>Body of {title}.</p>"
        });
    }

    private async Task<long> SeedChapterCommentAsync(int chapterId, int commenterId, string text, long? parentId = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ChapterComment comment = new()
        {
            ChapterId       = chapterId,
            UserId          = commenterId,
            CommentText     = text,
            ParentCommentId = parentId,
            DatePosted      = DateTime.UtcNow
        };
        db.ChapterComments.Add(comment);
        await db.SaveChangesAsync();
        return comment.CommentId;
    }

    private async Task SeedCommentLikeAsync(long commentId, int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.CommentLikes.Add(new CommentLike { CommentId = commentId, UserId = userId });
        await db.SaveChangesAsync();
    }

    /// <summary>A root comment, a reply to it, and a like on the root — on any blog-post subtype.</summary>
    private async Task<long[]> SeedBlogPostCommentsAsync(int postId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        BlogPostComment root = new()
        {
            BlogPostId = postId, UserId = _otherId, CommentText = "root", DatePosted = DateTime.UtcNow
        };
        db.BlogPostComments.Add(root);
        await db.SaveChangesAsync();
        BlogPostComment reply = new()
        {
            BlogPostId = postId, UserId = _thirdId, CommentText = "reply", ParentCommentId = root.CommentId,
            DatePosted = DateTime.UtcNow
        };
        db.BlogPostComments.Add(reply);
        db.CommentLikes.Add(new CommentLike { CommentId = root.CommentId, UserId = _thirdId });
        await db.SaveChangesAsync();
        return [root.CommentId, reply.CommentId];
    }

    private async Task SeedGroupCommentAsync(int groupId, int commenterId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.GroupComments.Add(new GroupComment
        {
            GroupId = groupId, UserId = commenterId, CommentText = "wall post", DatePosted = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    /// <summary>A two-option blog-post poll with a vote from each of the two other users.</summary>
    private async Task<(int PollId, int[] OptionIds)> SeedPollWithVotesAsync(int postId, int ownerId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        BlogPostPoll poll = new()
        {
            BlogPostId        = postId,
            OwnerId           = ownerId,
            PollName          = "Favorite starter?",
            DateOpened        = DateTime.UtcNow,
            ResultsVisibility = PollResultsVisibility.Always,
            AnonymityMode     = PollAnonymityMode.Anonymous,
            PollOptions       =
            {
                new PollOption { Text = "Turtwig", SortOrder = 0 },
                new PollOption { Text = "Piplup",  SortOrder = 1 },
            }
        };
        db.Polls.Add(poll);
        await db.SaveChangesAsync();

        int[] optionIds = [..poll.PollOptions.OrderBy(o => o.SortOrder).Select(o => o.PollOptionId)];
        db.PollVotes.Add(new PollVote { PollOptionId = optionIds[0], UserId = _otherId });
        db.PollVotes.Add(new PollVote { PollOptionId = optionIds[1], UserId = _thirdId });
        await db.SaveChangesAsync();
        return (poll.PollId, optionIds);
    }

    private async Task<int> SeedProfilePostAsync(int authorId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ProfileBlogPost post = new()
        {
            AuthorId = authorId, Title = "Profile post", Content = "<p>body</p>", Rating = Rating.E,
            IsPublished = true, DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow
        };
        db.ProfileBlogPosts.Add(post);
        await db.SaveChangesAsync();
        return post.BlogPostId;
    }

    private async Task<int> SeedSitePostAsync(int authorId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        SiteBlogPost post = new()
        {
            AuthorId = authorId, Title = "Site news", Content = "<p>news</p>", Rating = Rating.E,
            IsPublished = true, DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow
        };
        db.SiteBlogPosts.Add(post);
        await db.SaveChangesAsync();
        return post.BlogPostId;
    }

    private async Task<int> CreateGroupAsync(int creatorId)
    {
        SetActiveUser(creatorId);
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IGroupWriteService>().CreateGroupAsync(new CreateGroupDto
        {
            GroupName = $"Group {Guid.NewGuid():N}"[..20]
        });
    }

    private async Task<int> CreateGroupPostAsync(int groupId, int authorId)
    {
        SetActiveUser(authorId);
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IBlogPostWriteService>().CreateGroupBlogPostAsync(
            new CreateGroupBlogPostDto { GroupId = groupId, Title = "Group post", Content = "<p>hello</p>", Rating = Rating.E });
    }

    private async Task<long> SeedReportAsync(ReportedEntityType type, long entityId, int reporterId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        short reasonId = await db.ReportReasons.OrderBy(r => r.ReportReasonId)
            .Select(r => r.ReportReasonId).FirstAsync();
        Report report = new()
        {
            ReportedEntityType = type,
            ReportedEntityId   = entityId,
            ReportReasonId     = reasonId,
            ReporterUserId     = reporterId,
            ReportedUserId     = _authorId,
            ReportStatusId     = ReportStatusEnum.Open,
            DateReported       = DateTime.UtcNow,
        };
        db.Reports.Add(report);
        await db.SaveChangesAsync();
        return report.ReportId;
    }
}
