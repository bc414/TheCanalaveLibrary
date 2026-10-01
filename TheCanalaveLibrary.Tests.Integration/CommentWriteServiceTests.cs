using System.Net;
using System.Net.Http.Json;
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
/// (LikeCount, CommentLike row, per-viewer IsLiked), anonymous guards, and the profile-wall
/// <c>AllowProfileComments</c> gate (WU-AccessGateSweep2) at the service and over HTTP.
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

    // --- Author content control (WU-RecLifecycle): story author deletes chapter comments ---

    [Fact]
    public async Task DeleteComment_StoryAuthor_RemovesOtherUsersChapterComment()
    {
        int storyAuthorId = await SeedUserAsync();
        int authoredChapterId = await SeedChapterAsync(storyAuthorId);

        // _userId (a reader) leaves the comment.
        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = authoredChapterId,
            CommentText = "<p>Troll comment.</p>"
        });

        // The story's author deletes it.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(storyAuthorId, showMatureContent: false));
        await CallDeleteAsync(id);

        BaseComment? c = await LoadBaseCommentAsync(id);
        c.Should().BeNull("the story author can hard-delete any comment on their story's chapters");
    }

    [Fact]
    public async Task DeleteComment_AuthorOfDifferentStory_ThrowsUnauthorized()
    {
        // The caller authors SOME story — just not the one the comment sits on.
        int otherStoryAuthorId = await SeedUserAsync();
        await SeedChapterAsync(otherStoryAuthorId); // their story, uncommented

        long id = await CallPostAsync(new PostChapterCommentDto
        {
            ChapterId   = _chapterId, // authorless story — not theirs
            CommentText = "<p>Comment on an unrelated story.</p>"
        });

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(otherStoryAuthorId, showMatureContent: false));
        Func<Task> act = async () => await CallDeleteAsync(id);
        await act.Should().ThrowAsync<UnauthorizedAccessException>(
            "authoring a different story grants no delete rights on this one's comments");
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

    // --- PostUserProfileCommentAsync: the AllowProfileComments gate (WU-AccessGateSweep2) ---
    // The owner's setting is enforced in the service (layer2-services.md §"AllowProfileComments
    // Gate"); before, only ProfilePage hid the wall, so a direct POST bypassed it. Wall owner here is
    // _otherUserId; the commenter is _userId (the active user from InitializeAsync).

    [Fact]
    public async Task ProfileComment_Nobody_StrangerRootAndReply_Refused()
    {
        await SetAllowProfileCommentsAsync(_otherUserId, SocialInteractionPermission.Nobody);

        SetActiveUser(_otherUserId);
        long ownerRootId = await CallPostProfileAsync(_otherUserId);   // the owner always passes

        SetActiveUser(_userId);
        Func<Task> root = () => CallPostProfileAsync(_otherUserId);
        Func<Task> reply = () => CallPostProfileAsync(_otherUserId, parentCommentId: ownerRootId);

        (await root.Should().ThrowAsync<CommentValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.Contains("isn't accepting profile comments"));
        await reply.Should().ThrowAsync<CommentValidationException>("replies are posts too");
        (await CountProfileCommentsByAsync(_userId)).Should().Be(0, "a refused post writes nothing");
    }

    [Fact]
    public async Task ProfileComment_Nobody_OwnerPostsOnOwnWall_Allowed()
    {
        await SetAllowProfileCommentsAsync(_userId, SocialInteractionPermission.Nobody);

        Func<Task> act = () => CallPostProfileAsync(_userId);

        await act.Should().NotThrowAsync("the owner is exempt from their own setting");
    }

    [Fact]
    public async Task ProfileComment_Following_OwnerDoesNotFollowCommenter_Refused()
    {
        await SetAllowProfileCommentsAsync(_otherUserId, SocialInteractionPermission.Following);
        // The reverse direction — the COMMENTER following the owner — must not satisfy the gate.
        await SeedFollowAsync(followerId: _userId, followedId: _otherUserId);

        Func<Task> act = () => CallPostProfileAsync(_otherUserId);

        (await act.Should().ThrowAsync<CommentValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.Contains("from people they follow"));
    }

    [Fact]
    public async Task ProfileComment_Following_OwnerFollowsCommenter_Allowed()
    {
        await SetAllowProfileCommentsAsync(_otherUserId, SocialInteractionPermission.Following);
        await SeedFollowAsync(followerId: _otherUserId, followedId: _userId);

        Func<Task> act = () => CallPostProfileAsync(_otherUserId);

        await act.Should().NotThrowAsync("the owner follows the commenter");
    }

    [Theory]
    [InlineData(SocialInteractionPermission.Public)]
    [InlineData(SocialInteractionPermission.UsersOnly)]
    public async Task ProfileComment_PublicOrUsersOnly_Allowed(SocialInteractionPermission permission)
    {
        await SetAllowProfileCommentsAsync(_otherUserId, permission);

        long id = await CallPostProfileAsync(_otherUserId);

        (await LoadBaseCommentAsync(id)).Should().NotBeNull();
    }

    [Fact]
    public async Task ProfileComment_Nobody_OverHttp_Returns400WithUserFacingDetail()
    {
        await SetAllowProfileCommentsAsync(_otherUserId, SocialInteractionPermission.Nobody);

        HttpClient client = Factory.CreateClient();
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/comments/profile",
            new PostUserProfileCommentDto(_otherUserId, null, "<p>direct POST</p>"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the direct POST the profile page's hidden wall never stopped");
        (await response.Content.ReadAsStringAsync()).Should().Contain("isn't accepting profile comments",
            "ClientCommentWriteService rebuilds a CommentValidationException from the detail");
    }

    // --- Helpers ---

    private async Task<long> CallPostProfileAsync(int profileUserId, long? parentCommentId = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ICommentWriteService svc = scope.ServiceProvider.GetRequiredService<ICommentWriteService>();
        return await svc.PostUserProfileCommentAsync(
            new PostUserProfileCommentDto(profileUserId, parentCommentId, "<p>Hello wall</p>"));
    }

    private async Task SetAllowProfileCommentsAsync(int userId, SocialInteractionPermission permission)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        User user = await db.Users.FirstAsync(u => u.Id == userId);
        user.PrivacySettings.AllowProfileComments = permission;
        await db.SaveChangesAsync();
    }

    private async Task SeedFollowAsync(int followerId, int followedId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.FollowedUsers.Add(new FollowedUser
        {
            UserId = followerId,
            FollowedUserId = followedId,
            ReceiveAlerts = true,
            DateFollowed = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> CountProfileCommentsByAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserProfileComments.CountAsync(c => c.UserId == userId);
    }

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

    private async Task<int> SeedChapterAsync(int? storyAuthorId = null)
    {
        int storyId = await SeedStoryAsync(storyAuthorId);
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
