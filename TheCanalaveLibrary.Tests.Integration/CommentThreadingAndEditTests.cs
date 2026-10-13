using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Service audit §2.12's comment defects (WU-ParityAndRemaining, 2026-10-13; layer2-services.md
/// §"Group Comments — Per-Context Method Pattern" → "Rules every context shares"):
/// <list type="bullet">
/// <item>One-level threading is enforced by the service in all four contexts (worksheet D12's "Left open"
/// paragraph). Before, only the UI withheld Reply from replies, so a direct call created a depth-3
/// reply no read path renders.</item>
/// <item><c>EditCommentAsync</c> stamps <c>DateEdited</c> only on a real change, takes the spoiler flag on
/// chapter comments only, and the read DTO carries the stamp.</item>
/// </list>
/// FK parents per test: users via <see cref="IntegrationTestBase.SeedUserAsync"/>; a story via
/// <see cref="IntegrationTestBase.SeedStoryAsync"/> plus an inline published chapter; a published profile
/// post and an E-audience group inline; the profile wall is the seeded user. Tier: Integration.
/// </summary>
[Collection("Postgres")]
public class CommentThreadingAndEditTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _userId;
    private int _chapterId;
    private int _blogPostId;
    private int _groupId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userId = await SeedUserAsync();
        int storyId = await SeedStoryAsync();

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter chapter = new()
        {
            StoryId = storyId, ChapterNumber = 1, Title = "Chapter 1", PrimaryContentId = null,
            IsPublished = true, FirstPublishedDate = DateTime.UtcNow, VersionCount = 0
        };
        ProfileBlogPost post = new()
        {
            AuthorId = _userId, Title = "Post", Content = "<p>Body.</p>", Rating = Rating.E,
            IsPublished = true, DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow
        };
        Group group = new()
        {
            GroupName = $"Threads {Guid.NewGuid():N}"[..24], Description = "test", CreatorId = _userId,
            DateCreated = DateTime.UtcNow, AudienceRating = Rating.E, MaxContentRating = Rating.M
        };
        db.Chapters.Add(chapter);
        db.BlogPosts.Add(post);
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        _chapterId = chapter.ChapterId;
        _blogPostId = post.BlogPostId;
        _groupId = group.GroupId;

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_userId, showMatureContent: false));
    }

    public enum Context { Chapter, BlogPost, Group, Profile }

    // ── One-level threading ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(Context.Chapter)]
    [InlineData(Context.BlogPost)]
    [InlineData(Context.Group)]
    [InlineData(Context.Profile)]
    public async Task AReplyToAReply_IsRefused_AndWritesNothing(Context context)
    {
        long rootId = await PostAsync(context, parentId: null);
        long replyId = await PostAsync(context, parentId: rootId);
        int before = await CountCommentsAsync();

        Func<Task> act = () => PostAsync(context, parentId: replyId);

        (await act.Should().ThrowAsync<CommentValidationException>())
            .Which.Errors.Should().Equal(["Replies can only be made to top-level comments."]);
        (await CountCommentsAsync()).Should().Be(before, "the refusal comes before the insert");
    }

    [Theory]
    [InlineData(Context.Chapter)]
    [InlineData(Context.Profile)]
    public async Task AReplyToATopLevelComment_StillLands(Context context)
    {
        long rootId = await PostAsync(context, parentId: null);

        long replyId = await PostAsync(context, parentId: rootId);

        (await LoadAsync(replyId))!.ParentCommentId.Should().Be(rootId);
    }

    [Fact]
    public async Task AReplyWhoseParentWasDeleted_IsTopLevel_AndCanBeRepliedTo()
    {
        // D12's reparent: deleting the root SET NULLs the reply's parent, so the reply is top-level now
        // and the depth rule allows answering it.
        long rootId = await PostAsync(Context.Chapter, parentId: null);
        long replyId = await PostAsync(Context.Chapter, parentId: rootId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<ICommentWriteService>().DeleteCommentAsync(rootId);

        long answerId = await PostAsync(Context.Chapter, parentId: replyId);

        (await LoadAsync(answerId))!.ParentCommentId.Should().Be(replyId);
    }

    // ── Edits: the edited marker and the spoiler flag ────────────────────────────

    [Fact]
    public async Task AnEditThatChangesTheText_StampsDateEdited_AndTheReadCarriesIt()
    {
        long id = await PostAsync(Context.Chapter, parentId: null);
        DateTime before = DateTime.UtcNow;

        await EditAsync(new UpdateCommentDto { CommentId = id, CommentText = "<p>Changed.</p>" });

        BaseComment stored = (await LoadAsync(id))!;
        stored.CommentText.Should().Contain("Changed.");
        stored.DateEdited.Should().NotBeNull().And.BeOnOrAfter(before.AddSeconds(-1));

        using IServiceScope scope = Factory.Services.CreateScope();
        CommentPageDto page = await scope.ServiceProvider.GetRequiredService<ICommentReadService>()
            .GetChapterCommentsAsync(_chapterId, 1, 20);
        page.Comments.Single(c => c.CommentId == id).DateEdited
            .Should().BeCloseTo(stored.DateEdited!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task AnIdenticalResave_LeavesDateEditedNull()
    {
        long id = await PostAsync(Context.Chapter, parentId: null);
        string stored = (await LoadAsync(id))!.CommentText;

        // The same text again, and the same text with no spoiler change.
        await EditAsync(new UpdateCommentDto { CommentId = id, CommentText = stored });
        await EditAsync(new UpdateCommentDto { CommentId = id, CommentText = stored, IsSpoiler = false });

        (await LoadAsync(id))!.DateEdited.Should().BeNull("\"(edited)\" marks a real change only");
    }

    [Fact]
    public async Task MarkingAChapterRootASpoiler_Persists_AndStamps()
    {
        long id = await PostAsync(Context.Chapter, parentId: null);
        string stored = (await LoadAsync(id))!.CommentText;

        await EditAsync(new UpdateCommentDto { CommentId = id, CommentText = stored, IsSpoiler = true });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ChapterComment comment = await db.ChapterComments.SingleAsync(c => c.CommentId == id);
        comment.IsSpoiler.Should().BeTrue();
        comment.DateEdited.Should().NotBeNull("a spoiler-flag change is a real change");
    }

    [Fact]
    public async Task ANullSpoilerFlag_LeavesTheFlagAlone()
    {
        long id = await PostAsync(Context.Chapter, parentId: null, spoiler: true);

        await EditAsync(new UpdateCommentDto { CommentId = id, CommentText = "<p>New words.</p>", IsSpoiler = null });

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.ChapterComments.SingleAsync(c => c.CommentId == id)).IsSpoiler.Should().BeTrue();
    }

    [Theory]
    [InlineData(Context.BlogPost, true)]
    [InlineData(Context.Group, false)]
    [InlineData(Context.Profile, true)]
    public async Task ASpoilerFlagOnANonChapterComment_IsRefused_AndChangesNothing(Context context, bool flag)
    {
        long id = await PostAsync(context, parentId: null);

        Func<Task> act = () => EditAsync(new UpdateCommentDto
        {
            CommentId = id, CommentText = "<p>Changed.</p>", IsSpoiler = flag
        });

        (await act.Should().ThrowAsync<CommentValidationException>())
            .Which.Errors.Should().Equal(["Only chapter comments can be marked as spoilers."]);
        BaseComment stored = (await LoadAsync(id))!;
        stored.CommentText.Should().NotContain("Changed.");
        stored.DateEdited.Should().BeNull();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private async Task<long> PostAsync(Context context, long? parentId, bool spoiler = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICommentWriteService svc = scope.ServiceProvider.GetRequiredService<ICommentWriteService>();
        const string text = "<p>A comment.</p>";
        return context switch
        {
            Context.Chapter => await svc.PostChapterCommentAsync(new PostChapterCommentDto
            {
                ChapterId = _chapterId, ParentCommentId = parentId, CommentText = text, IsSpoiler = spoiler
            }),
            Context.BlogPost => await svc.PostBlogPostCommentAsync(new PostBlogPostCommentDto
            {
                BlogPostId = _blogPostId, ParentCommentId = parentId, CommentText = text
            }),
            Context.Group => await svc.PostGroupCommentAsync(new PostGroupCommentDto
            {
                GroupId = _groupId, ParentCommentId = parentId, CommentText = text
            }),
            _ => await svc.PostUserProfileCommentAsync(new PostUserProfileCommentDto(_userId, parentId, text)),
        };
    }

    private async Task EditAsync(UpdateCommentDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ICommentWriteService>().EditCommentAsync(dto);
    }

    private async Task<BaseComment?> LoadAsync(long id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.BaseComments.AsNoTracking().FirstOrDefaultAsync(c => c.CommentId == id);
    }

    private async Task<int> CountCommentsAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.BaseComments.CountAsync();
    }
}
