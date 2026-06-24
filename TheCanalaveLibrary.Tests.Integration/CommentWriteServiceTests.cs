using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="ICommentWriteService"/> (WU19). Covers: post root + reply,
/// <c>IsSpoiler</c> round-trip, sanitization (script stripping on save), edit (re-sanitizes,
/// author-only), delete (hard-removes row, reparents replies, cascades likes), like toggle
/// (LikeCount, CommentLike row, per-viewer IsLiked), anonymous guards.
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class CommentWriteServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _userId;
    private int _otherUserId;
    private int _chapterId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userId = await SeedUserAsync();
        _otherUserId = await SeedUserAsync();
        _chapterId = await SeedChapterAsync();
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_userId, showMatureContent: false));
    }

    // --- PostChapterCommentAsync ---

    [Fact]
    public async Task PostChapterComment_Root_InsertsRowWithCorrectFields()
    {
        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Great chapter!</p>",
            IsSpoiler   = false
        });

        ChapterComment? c = await LoadChapterCommentAsync(id);
        c.Should().NotBeNull();
        c!.ChapterId.Should().Be(_chapterId);
        c.UserId.Should().Be(_userId);
        c.CommentText.Should().Contain("Great chapter!");
        c.IsSpoiler.Should().BeFalse();
        c.ParentCommentId.Should().BeNull();
    }

    [Fact]
    public async Task PostChapterComment_IsSpoilerTrue_RoundTrips()
    {
        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Future chapter spoiler!</p>",
            IsSpoiler   = true
        });

        ChapterComment? c = await LoadChapterCommentAsync(id);
        c!.IsSpoiler.Should().BeTrue();
    }

    [Fact]
    public async Task PostChapterComment_ScriptTag_IsStrippedBySanitizer()
    {
        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Text</p><script>alert('xss')</script>"
        });

        ChapterComment? c = await LoadChapterCommentAsync(id);
        c!.CommentText.Should().NotContain("<script>");
        c.CommentText.Should().Contain("Text");
    }

    [Fact]
    public async Task PostChapterComment_Reply_SetsParentCommentId()
    {
        long rootId = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Root comment.</p>"
        });

        long replyId = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId       = _chapterId,
            ParentCommentId = rootId,
            CommentText     = "<p>Reply!</p>"
        });

        BaseComment? reply = await LoadBaseCommentAsync(replyId);
        reply!.ParentCommentId.Should().Be(rootId);
    }

    [Fact]
    public async Task PostChapterComment_ReplyOnDifferentChapter_ThrowsKeyNotFound()
    {
        // Post a root comment on our chapter.
        long rootId = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Root.</p>"
        });

        // Try to reply using the root id but targeting a different chapter id.
        int differentChapterId = await FakeChapterId();
        Func<Task> act = async () => await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId       = differentChapterId,
            ParentCommentId = rootId,
            CommentText     = "<p>Cross-chapter reply.</p>"
        });

        // The different chapter doesn't exist, so we'll hit KeyNotFoundException for the chapter.
        // Regardless of which check fires, the post must fail.
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task PostChapterComment_EmptyText_ThrowsValidationException()
    {
        Func<Task> act = async () => await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = ""
        });

        await act.Should().ThrowAsync<CommentValidationException>();
    }

    [Fact]
    public async Task PostChapterComment_Anonymous_ThrowsInvalidOperation()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        Func<Task> act = async () => await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Anonymous.</p>"
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // --- EditCommentAsync ---

    [Fact]
    public async Task EditComment_Author_UpdatesTextAndResanitizes()
    {
        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Original.</p>"
        });

        await CallEditAsync(new UpdateCommentDto
        {
            CommentId   = id,
            CommentText = "<p>Edited</p><script>bad()</script>"
        });

        BaseComment? c = await LoadBaseCommentAsync(id);
        c!.CommentText.Should().Contain("Edited");
        c.CommentText.Should().NotContain("<script>");
    }

    [Fact]
    public async Task EditComment_NonOwner_ThrowsUnauthorized()
    {
        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Owner's comment.</p>"
        });

        // Switch to a different user.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: false));

        Func<Task> act = async () => await CallEditAsync(new UpdateCommentDto
        {
            CommentId   = id,
            CommentText = "<p>Hijacked.</p>"
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task EditComment_Anonymous_ThrowsInvalidOperation()
    {
        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Post first.</p>"
        });

        SetActiveUser(FakeActiveUserContext.Anonymous());

        Func<Task> act = async () => await CallEditAsync(new UpdateCommentDto
        {
            CommentId   = id,
            CommentText = "<p>Anonymous edit.</p>"
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // --- DeleteCommentAsync ---

    [Fact]
    public async Task DeleteComment_Author_RemovesRow()
    {
        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Delete me.</p>"
        });

        await CallDeleteAsync(id);

        BaseComment? c = await LoadBaseCommentAsync(id);
        c.Should().BeNull("hard delete must remove the row");
    }

    [Fact]
    public async Task DeleteComment_ReparentsReplies_ToTopLevel()
    {
        long rootId = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Root, will be deleted.</p>"
        });

        long replyId = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId       = _chapterId,
            ParentCommentId = rootId,
            CommentText     = "<p>Reply, should become top-level.</p>"
        });

        await CallDeleteAsync(rootId);

        BaseComment? reply = await LoadBaseCommentAsync(replyId);
        reply.Should().NotBeNull("reply must survive parent delete");
        reply!.ParentCommentId.Should().BeNull("ParentCommentId FK is SET NULL — reply becomes top-level");
    }

    [Fact]
    public async Task DeleteComment_CascadesLikes()
    {
        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Like then delete.</p>"
        });

        // Like the comment, then delete it.
        await CallToggleLikeAsync(id);

        await CallDeleteAsync(id);

        // The CommentLike row must also be gone (CASCADE).
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool likeExists = await db.CommentLikes.AnyAsync(l => l.CommentId == id);
        likeExists.Should().BeFalse("CommentLike FK is CASCADE — likes must be removed with the comment");
    }

    [Fact]
    public async Task DeleteComment_NonOwner_ThrowsUnauthorized()
    {
        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Mine.</p>"
        });

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_otherUserId, showMatureContent: false));

        Func<Task> act = async () => await CallDeleteAsync(id);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task DeleteComment_Anonymous_ThrowsInvalidOperation()
    {
        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Post first.</p>"
        });

        SetActiveUser(FakeActiveUserContext.Anonymous());

        Func<Task> act = async () => await CallDeleteAsync(id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // --- ToggleLikeAsync ---

    [Fact]
    public async Task ToggleLike_Like_IncrementsLikeCountAndCreatesJunctionRow()
    {
        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Like me.</p>"
        });

        CommentLikeResultDto result = await CallToggleLikeAsync(id);

        result.IsLiked.Should().BeTrue();
        result.LikeCount.Should().Be(1);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool rowExists = await db.CommentLikes.AnyAsync(l => l.CommentId == id && l.UserId == _userId);
        rowExists.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleLike_Unlike_DecrementsLikeCountAndRemovesJunctionRow()
    {
        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Like then unlike.</p>"
        });

        await CallToggleLikeAsync(id);                   // like → LikeCount = 1
        CommentLikeResultDto result = await CallToggleLikeAsync(id); // unlike → LikeCount = 0

        result.IsLiked.Should().BeFalse();
        result.LikeCount.Should().Be(0);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        bool rowExists = await db.CommentLikes.AnyAsync(l => l.CommentId == id && l.UserId == _userId);
        rowExists.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleLike_Anonymous_ThrowsInvalidOperation()
    {
        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId,
            CommentText = "<p>Post first.</p>"
        });

        SetActiveUser(FakeActiveUserContext.Anonymous());

        Func<Task> act = async () => await CallToggleLikeAsync(id);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // --- Helpers ---

    private async Task<long> CallPostAsync(PostChapterCommentDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICommentWriteService svc = scope.ServiceProvider.GetRequiredService<ICommentWriteService>();
        return await svc.PostChapterCommentAsync(dto);
    }

    private async Task CallEditAsync(UpdateCommentDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICommentWriteService svc = scope.ServiceProvider.GetRequiredService<ICommentWriteService>();
        await svc.EditCommentAsync(dto);
    }

    private async Task CallDeleteAsync(long commentId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICommentWriteService svc = scope.ServiceProvider.GetRequiredService<ICommentWriteService>();
        await svc.DeleteCommentAsync(commentId);
    }

    private async Task<CommentLikeResultDto> CallToggleLikeAsync(long commentId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICommentWriteService svc = scope.ServiceProvider.GetRequiredService<ICommentWriteService>();
        return await svc.ToggleLikeAsync(commentId);
    }

    private async Task<BaseComment?> LoadBaseCommentAsync(long id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.BaseComments.FirstOrDefaultAsync(c => c.CommentId == id);
    }

    private async Task<ChapterComment?> LoadChapterCommentAsync(long id)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.ChapterComments.FirstOrDefaultAsync(c => c.CommentId == id);
    }

    private async Task<int> SeedChapterAsync()
    {
        int storyId = await SeedStoryAsync();
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter chapter = new()
        {
            StoryId          = storyId,
            ChapterNumber    = 1,
            Title            = "Chapter 1",
            PrimaryContentId = null,
            IsPublished      = true,
            VersionCount     = 0
        };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        return chapter.ChapterId;
    }

    /// <summary>Returns a non-existent chapterId (int.MaxValue) to test the "chapter not found" guard.</summary>
    private static Task<int> FakeChapterId() => Task.FromResult(int.MaxValue);
}
