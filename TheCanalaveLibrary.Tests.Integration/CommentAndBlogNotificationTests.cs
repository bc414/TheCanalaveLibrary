using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for the WU-B2 notification wiring: the four comment seams in
/// <see cref="ICommentWriteService"/> (NewStoryComment / NewCommentOnBlog /
/// NewCommentOnYourProfile / CommentReply, incl. the reply/container-suppress and null-skip
/// rules), the profile-blog publish-transition fan-out (types 13/14/15/16 with 13&gt;14&gt;15&gt;16
/// precedence-dedup), the StoryId ownership gate, the BlogPostDirect / Chapter enrichment
/// targets, and the <c>BlogPostDto.ViewerHasCompletedStory</c> projection.
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class CommentAndBlogNotificationTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;    // owns the story/chapter/blog/profile under test
    private int _commenterId; // acts on the author's content

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync("Author");
        _commenterId = await SeedUserAsync("Commenter");
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_commenterId, showMatureContent: false));
    }

    // ── Chapter comment seam ─────────────────────────────────────────────────────

    [Fact]
    public async Task ChapterComment_NotifiesStoryAuthor_WithChapterRelatedId()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        int chapterId = await SeedChapterAsync(storyId);

        await PostChapterCommentAsync(new PostChapterCommentDto
        {
            ChapterId = chapterId, CommentText = "<p>Nice!</p>"
        });

        Notification? n = await SingleNotificationOrDefaultAsync(_authorId, NotificationTypeEnum.NewStoryComment);
        n.Should().NotBeNull();
        n!.SourceUserId.Should().Be(_commenterId);
        n.RelatedEntityId.Should().Be(chapterId);
    }

    [Fact]
    public async Task ChapterComment_OnOwnChapter_DropsSelf()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        int chapterId = await SeedChapterAsync(storyId);
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: false));

        await PostChapterCommentAsync(new PostChapterCommentDto
        {
            ChapterId = chapterId, CommentText = "<p>My own chapter.</p>"
        });

        (await CountNotificationsAsync(_authorId)).Should().Be(0);
    }

    [Fact]
    public async Task ChapterReply_NotifiesParentAuthor_AndStillNotifiesDistinctOwner()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        int chapterId = await SeedChapterAsync(storyId);
        int thirdId = await SeedUserAsync("Replier");

        // Commenter posts the root comment.
        long rootId = await PostChapterCommentAsync(new PostChapterCommentDto
        {
            ChapterId = chapterId, CommentText = "<p>Root.</p>"
        });

        // A third user replies: parent author (commenter) gets CommentReply; the story author
        // is a different person, so they still get NewStoryComment for the reply-comment.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(thirdId, showMatureContent: false));
        await PostChapterCommentAsync(new PostChapterCommentDto
        {
            ChapterId = chapterId, ParentCommentId = rootId, CommentText = "<p>Reply.</p>"
        });

        Notification? reply = await SingleNotificationOrDefaultAsync(_commenterId, NotificationTypeEnum.CommentReply);
        reply.Should().NotBeNull();
        reply!.SourceUserId.Should().Be(thirdId);
        reply.RelatedEntityId.Should().Be(chapterId); // context id, never the (long) comment id

        (await CountNotificationsAsync(_authorId, NotificationTypeEnum.NewStoryComment)).Should().Be(2);
    }

    [Fact]
    public async Task ChapterReply_OwnerIsParentAuthor_GetsOnlyCommentReply()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        int chapterId = await SeedChapterAsync(storyId);

        // The story author posts a root comment on their own chapter (no notification — drop-self).
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: false));
        long rootId = await PostChapterCommentAsync(new PostChapterCommentDto
        {
            ChapterId = chapterId, CommentText = "<p>Author's own root.</p>"
        });

        // Commenter replies: owner == parent author → container-suppress rule sends exactly one
        // notification (CommentReply), never NewStoryComment + CommentReply to the same person.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_commenterId, showMatureContent: false));
        await PostChapterCommentAsync(new PostChapterCommentDto
        {
            ChapterId = chapterId, ParentCommentId = rootId, CommentText = "<p>Reply to author.</p>"
        });

        (await CountNotificationsAsync(_authorId, NotificationTypeEnum.CommentReply)).Should().Be(1);
        (await CountNotificationsAsync(_authorId, NotificationTypeEnum.NewStoryComment)).Should().Be(0);
    }

    [Fact]
    public async Task ChapterReply_ParentAuthorDeleted_SkipsReplyNotification()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        int chapterId = await SeedChapterAsync(storyId);

        long rootId = await PostChapterCommentAsync(new PostChapterCommentDto
        {
            ChapterId = chapterId, CommentText = "<p>Root before deletion.</p>"
        });

        // Simulate the SET-NULL author anonymization on the parent comment.
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.BaseComments.Where(c => c.CommentId == rootId)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.UserId, (int?)null));
        }

        int thirdId = await SeedUserAsync("Replier");
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(thirdId, showMatureContent: false));
        await PostChapterCommentAsync(new PostChapterCommentDto
        {
            ChapterId = chapterId, ParentCommentId = rootId, CommentText = "<p>Reply to ghost.</p>"
        });

        // Null-skip: no CommentReply row anywhere; the story author still gets NewStoryComment.
        (await CountAllNotificationsOfTypeAsync(NotificationTypeEnum.CommentReply)).Should().Be(0);
        (await CountNotificationsAsync(_authorId, NotificationTypeEnum.NewStoryComment)).Should().Be(2);
    }

    // ── Blog comment seam ────────────────────────────────────────────────────────

    [Fact]
    public async Task BlogComment_OnProfilePost_NotifiesBlogAuthor()
    {
        int postId = await SeedPublishedProfilePostAsync(_authorId);

        await PostBlogCommentAsync(new PostBlogPostCommentDto
        {
            BlogPostId = postId, CommentText = "<p>Nice post!</p>"
        });

        Notification? n = await SingleNotificationOrDefaultAsync(_authorId, NotificationTypeEnum.NewCommentOnBlog);
        n.Should().NotBeNull();
        n!.SourceUserId.Should().Be(_commenterId);
        n.RelatedEntityId.Should().Be(postId);
    }

    [Fact]
    public async Task BlogComment_OnGroupPost_NotifiesPostAuthor_ViaTptRoot()
    {
        int postId = await SeedGroupPostAsync(_authorId);

        await PostBlogCommentAsync(new PostBlogPostCommentDto
        {
            BlogPostId = postId, CommentText = "<p>Group post comment.</p>"
        });

        Notification? n = await SingleNotificationOrDefaultAsync(_authorId, NotificationTypeEnum.NewCommentOnBlog);
        n.Should().NotBeNull();
        n!.RelatedEntityId.Should().Be(postId);
    }

    [Fact]
    public async Task BlogReply_OwnerIsParentAuthor_GetsOnlyCommentReply()
    {
        int postId = await SeedPublishedProfilePostAsync(_authorId);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: false));
        long rootId = await PostBlogCommentAsync(new PostBlogPostCommentDto
        {
            BlogPostId = postId, CommentText = "<p>Author root.</p>"
        });

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_commenterId, showMatureContent: false));
        await PostBlogCommentAsync(new PostBlogPostCommentDto
        {
            BlogPostId = postId, ParentCommentId = rootId, CommentText = "<p>Reply.</p>"
        });

        (await CountNotificationsAsync(_authorId, NotificationTypeEnum.CommentReply)).Should().Be(1);
        (await CountNotificationsAsync(_authorId, NotificationTypeEnum.NewCommentOnBlog)).Should().Be(0);
    }

    // ── Group comment seam (replies only) ────────────────────────────────────────

    [Fact]
    public async Task GroupComment_TopLevel_GeneratesNoNotification()
    {
        int groupId = await SeedGroupAsync(_authorId);

        await PostGroupCommentAsync(new PostGroupCommentDto
        {
            GroupId = groupId, CommentText = "<p>Wall comment.</p>"
        });

        (await CountNotificationsAsync(_authorId)).Should().Be(0);
    }

    [Fact]
    public async Task GroupComment_Reply_NotifiesParentAuthorOnly()
    {
        int groupId = await SeedGroupAsync(_authorId);
        int thirdId = await SeedUserAsync("Replier");

        long rootId = await PostGroupCommentAsync(new PostGroupCommentDto
        {
            GroupId = groupId, CommentText = "<p>Root on wall.</p>"
        });

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(thirdId, showMatureContent: false));
        await PostGroupCommentAsync(new PostGroupCommentDto
        {
            GroupId = groupId, ParentCommentId = rootId, CommentText = "<p>Wall reply.</p>"
        });

        Notification? n = await SingleNotificationOrDefaultAsync(_commenterId, NotificationTypeEnum.CommentReply);
        n.Should().NotBeNull();
        n!.RelatedEntityId.Should().Be(groupId);
        (await CountNotificationsAsync(_authorId)).Should().Be(0); // group creator never notified
    }

    // ── Profile comment seam ─────────────────────────────────────────────────────

    [Fact]
    public async Task ProfileComment_NotifiesProfileOwner()
    {
        await PostProfileCommentAsync(new PostUserProfileCommentDto(_authorId, null, "<p>Hi!</p>"));

        Notification? n = await SingleNotificationOrDefaultAsync(_authorId, NotificationTypeEnum.NewCommentOnYourProfile);
        n.Should().NotBeNull();
        n!.SourceUserId.Should().Be(_commenterId);
        n.RelatedEntityId.Should().Be(_authorId);
    }

    [Fact]
    public async Task ProfileReply_OwnerIsParentAuthor_GetsOnlyCommentReply()
    {
        // Owner posts on their own wall (drop-self — no notification).
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: false));
        long rootId = await PostProfileCommentAsync(new PostUserProfileCommentDto(_authorId, null, "<p>Owner root.</p>"));

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_commenterId, showMatureContent: false));
        await PostProfileCommentAsync(new PostUserProfileCommentDto(_authorId, rootId, "<p>Reply.</p>"));

        (await CountNotificationsAsync(_authorId, NotificationTypeEnum.CommentReply)).Should().Be(1);
        (await CountNotificationsAsync(_authorId, NotificationTypeEnum.NewCommentOnYourProfile)).Should().Be(0);
    }

    // ── Profile-blog publish-transition fan-out ──────────────────────────────────

    [Fact]
    public async Task DraftCreate_FiresNothing()
    {
        int followerId = await SeedUserAsync("Follower");
        await SeedFollowAsync(followerId, _authorId, receiveAlerts: true);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: false));
        await CreateProfilePostAsync(_authorId); // drafts only — no publish

        (await CountNotificationsAsync(followerId)).Should().Be(0);
    }

    [Fact]
    public async Task PublishTransition_NotifiesAlertFollowers_AndSkipsMutedOnes()
    {
        int alertFollowerId = await SeedUserAsync("AlertFollower");
        int mutedFollowerId = await SeedUserAsync("MutedFollower");
        await SeedFollowAsync(alertFollowerId, _authorId, receiveAlerts: true);
        await SeedFollowAsync(mutedFollowerId, _authorId, receiveAlerts: false);

        int postId = await CreateAndPublishProfilePostAsync(_authorId);

        Notification? n = await SingleNotificationOrDefaultAsync(alertFollowerId, NotificationTypeEnum.NewBlogPostByFollowedUser);
        n.Should().NotBeNull();
        n!.RelatedEntityId.Should().Be(postId);
        (await CountNotificationsAsync(mutedFollowerId)).Should().Be(0);
    }

    [Fact]
    public async Task PublishTransition_StoryLinked_FansOutToInteractionSets_WithPrecedenceDedup()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        int storyFollowerId = await SeedUserAsync("StoryFollower");
        int favoriterId = await SeedUserAsync("Favoriter");
        int readLaterId = await SeedUserAsync("ReadLater");
        int bothId = await SeedUserAsync("Both"); // author-follower AND story-follower → one notification

        await SeedInteractionAsync(storyFollowerId, storyId, followed: true);
        await SeedInteractionAsync(favoriterId, storyId, favorite: true);
        await SeedInteractionAsync(readLaterId, storyId, readItLater: true);
        await SeedFollowAsync(bothId, _authorId, receiveAlerts: true);
        await SeedInteractionAsync(bothId, storyId, followed: true, favorite: true, readItLater: true);

        int postId = await CreateAndPublishProfilePostAsync(_authorId, storyId);

        (await SingleNotificationOrDefaultAsync(storyFollowerId, NotificationTypeEnum.NewBlogPostOnFollowedStory))!
            .RelatedEntityId.Should().Be(postId);
        (await SingleNotificationOrDefaultAsync(favoriterId, NotificationTypeEnum.NewBlogPostOnFavoritedStory))!
            .RelatedEntityId.Should().Be(postId);
        (await SingleNotificationOrDefaultAsync(readLaterId, NotificationTypeEnum.NewBlogPostOnReadItLaterStory))!
            .RelatedEntityId.Should().Be(postId);

        // Precedence-dedup: the multi-qualifier gets exactly one notification, and it's type 13.
        (await CountNotificationsAsync(bothId)).Should().Be(1);
        (await SingleNotificationOrDefaultAsync(bothId, NotificationTypeEnum.NewBlogPostByFollowedUser))
            .Should().NotBeNull();
    }

    [Fact]
    public async Task EditWithoutTransition_FiresNothing()
    {
        int followerId = await SeedUserAsync("Follower");
        await SeedFollowAsync(followerId, _authorId, receiveAlerts: true);

        int postId = await CreateAndPublishProfilePostAsync(_authorId);
        await MarkAllReadAsync(followerId); // consume the publish notification

        // Published → published edit: no new fan-out.
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: false));
        await UpdateProfilePostAsync(postId, isPublished: true);

        (await CountUnreadNotificationsAsync(followerId)).Should().Be(0);
    }

    [Fact]
    public async Task Republish_AfterPriorNotificationRead_NotifiesAgain()
    {
        int followerId = await SeedUserAsync("Follower");
        await SeedFollowAsync(followerId, _authorId, receiveAlerts: true);

        int postId = await CreateAndPublishProfilePostAsync(_authorId);
        await MarkAllReadAsync(followerId);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: false));
        await UpdateProfilePostAsync(postId, isPublished: false); // unpublish
        await UpdateProfilePostAsync(postId, isPublished: true);  // republish → intentional re-notify

        (await CountNotificationsAsync(followerId, NotificationTypeEnum.NewBlogPostByFollowedUser)).Should().Be(2);
    }

    // ── StoryId ownership gate ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateProfilePost_LinkingSomeoneElsesStory_Throws()
    {
        int foreignStoryId = await SeedStoryAsync(authorId: _commenterId);
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: false));

        Func<Task> act = () => CreateProfilePostAsync(_authorId, foreignStoryId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task UpdateProfilePost_LinkingSomeoneElsesStory_Throws()
    {
        int foreignStoryId = await SeedStoryAsync(authorId: _commenterId);
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: false));
        int postId = await CreateProfilePostAsync(_authorId);

        Func<Task> act = () => UpdateProfilePostAsync(postId, isPublished: false, storyId: foreignStoryId);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Enrichment targets ───────────────────────────────────────────────────────

    [Fact]
    public async Task Enrichment_ProfileBlogNotification_ResolvesTitleAndBlogUrl()
    {
        int followerId = await SeedUserAsync("Follower");
        await SeedFollowAsync(followerId, _authorId, receiveAlerts: true);
        int postId = await CreateAndPublishProfilePostAsync(_authorId);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(followerId, showMatureContent: false));
        NotificationDto[] page = await GetNotificationsAsync();

        NotificationDto dto = page.Single(n => n.NotificationTypeId == NotificationTypeEnum.NewBlogPostByFollowedUser);
        dto.TargetUrl.Should().Be($"/blog/{postId}");
        dto.TargetTitle.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Enrichment_NewStoryComment_ResolvesChapterUrl()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        int chapterId = await SeedChapterAsync(storyId);
        await PostChapterCommentAsync(new PostChapterCommentDto
        {
            ChapterId = chapterId, CommentText = "<p>Enrich me.</p>"
        });

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_authorId, showMatureContent: false));
        NotificationDto[] page = await GetNotificationsAsync();

        NotificationDto dto = page.Single(n => n.NotificationTypeId == NotificationTypeEnum.NewStoryComment);
        dto.TargetUrl.Should().Be($"/story/{storyId}/1"); // chapter deep-link (ChapterNumber = 1)
    }

    // ── ViewerHasCompletedStory projection ───────────────────────────────────────

    [Fact]
    public async Task GetById_ViewerCompletedLinkedStory_FlagTrue()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        int postId = await CreateAndPublishProfilePostAsync(_authorId, storyId);
        await SeedInteractionAsync(_commenterId, storyId, completed: true);

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_commenterId, showMatureContent: false));
        BlogPostDto? post = await GetPostByIdAsync(postId);

        post!.ViewerHasCompletedStory.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_ViewerNotCompleted_FlagFalse()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        int postId = await CreateAndPublishProfilePostAsync(_authorId, storyId);
        await SeedInteractionAsync(_commenterId, storyId, followed: true); // interaction row, not completed

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_commenterId, showMatureContent: false));
        BlogPostDto? post = await GetPostByIdAsync(postId);

        post!.ViewerHasCompletedStory.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_AnonymousViewer_FlagFalse()
    {
        int storyId = await SeedStoryAsync(authorId: _authorId);
        int postId = await CreateAndPublishProfilePostAsync(_authorId, storyId);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        BlogPostDto? post = await GetPostByIdAsync(postId);

        post!.ViewerHasCompletedStory.Should().BeFalse();
    }

    [Fact]
    public async Task GetById_NonStoryLinkedPost_FlagFalse()
    {
        int postId = await CreateAndPublishProfilePostAsync(_authorId);
        await SeedInteractionAsync(_commenterId, await SeedStoryAsync(), completed: true); // unrelated story

        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(_commenterId, showMatureContent: false));
        BlogPostDto? post = await GetPostByIdAsync(postId);

        post!.ViewerHasCompletedStory.Should().BeFalse();
    }

    // ── Service-call helpers ─────────────────────────────────────────────────────

    private async Task<long> PostChapterCommentAsync(PostChapterCommentDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICommentWriteService>()
            .PostChapterCommentAsync(dto);
    }

    private async Task<long> PostBlogCommentAsync(PostBlogPostCommentDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICommentWriteService>()
            .PostBlogPostCommentAsync(dto);
    }

    private async Task<long> PostGroupCommentAsync(PostGroupCommentDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICommentWriteService>()
            .PostGroupCommentAsync(dto);
    }

    private async Task<long> PostProfileCommentAsync(PostUserProfileCommentDto dto)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICommentWriteService>()
            .PostUserProfileCommentAsync(dto);
    }

    /// <summary>Creates a draft profile post as the CURRENT active user (caller sets it first).</summary>
    private async Task<int> CreateProfilePostAsync(int authorId, int? storyId = null)
    {
        SetActiveUser(FakeActiveUserContext.AuthenticatedUser(authorId, showMatureContent: false));
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IBlogPostWriteService>()
            .CreateProfileBlogPostAsync(new CreateProfileBlogPostDto
            {
                Title = "Fan-out post",
                Content = "<p>Body.</p>",
                Rating = Rating.E,
                StoryId = storyId
            });
    }

    private async Task UpdateProfilePostAsync(int postId, bool isPublished, int? storyId = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IBlogPostWriteService>()
            .UpdateBlogPostAsync(new UpdateBlogPostDto
            {
                BlogPostId = postId,
                Title = "Fan-out post",
                Content = "<p>Body.</p>",
                Rating = Rating.E,
                StoryId = storyId,
                IsPublished = isPublished
            });
    }

    /// <summary>Draft-create then publish-transition as <paramref name="authorId"/>; restores no active user.</summary>
    private async Task<int> CreateAndPublishProfilePostAsync(int authorId, int? storyId = null)
    {
        int postId = await CreateProfilePostAsync(authorId, storyId);
        await UpdateProfilePostAsync(postId, isPublished: true, storyId: storyId);
        return postId;
    }

    private async Task<BlogPostDto?> GetPostByIdAsync(int postId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IBlogPostReadService>()
            .GetByIdAsync(postId);
    }

    private async Task<NotificationDto[]> GetNotificationsAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<INotificationReadService>()
            .GetNotificationsAsync(1, 50);
    }

    // ── Seeding helpers ──────────────────────────────────────────────────────────

    private async Task<int> SeedChapterAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter chapter = new()
        {
            StoryId = storyId, ChapterNumber = 1, Title = "Chapter 1",
            PrimaryContentId = null, IsPublished = true, VersionCount = 0
        };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        return chapter.ChapterId;
    }

    private async Task<int> SeedGroupAsync(int creatorId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Group group = new()
        {
            GroupName = $"Test Group {Guid.NewGuid():N}"[..24],
            Description = "test",
            CreatorId = creatorId,
            DateCreated = DateTime.UtcNow,
            AudienceRating = Rating.E,
            MaxContentRating = Rating.M
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return group.GroupId;
    }

    private async Task<int> SeedPublishedProfilePostAsync(int authorId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ProfileBlogPost post = new()
        {
            AuthorId = authorId, Title = "Seeded post", Content = "<p>Body.</p>",
            Rating = Rating.E, IsPublished = true,
            DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow
        };
        db.BlogPosts.Add(post);
        await db.SaveChangesAsync();
        return post.BlogPostId;
    }

    private async Task<int> SeedGroupPostAsync(int authorId)
    {
        int groupId = await SeedGroupAsync(authorId);
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        GroupBlogPost post = new()
        {
            AuthorId = authorId, GroupId = groupId, Title = "Seeded group post",
            Content = "<p>Body.</p>", Rating = Rating.E, IsPublished = true,
            DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow
        };
        db.GroupBlogPosts.Add(post);
        await db.SaveChangesAsync();
        return post.BlogPostId;
    }

    private async Task SeedFollowAsync(int followerId, int followedId, bool receiveAlerts)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.FollowedUsers.Add(new FollowedUser
        {
            UserId = followerId, FollowedUserId = followedId,
            DateFollowed = DateTime.UtcNow, ReceiveAlerts = receiveAlerts
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedInteractionAsync(
        int userId, int storyId,
        bool followed = false, bool favorite = false, bool readItLater = false, bool completed = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStoryInteractions.Add(new UserStoryInteraction
        {
            UserId = userId, StoryId = storyId,
            IsFollowed = followed, IsFavorite = favorite,
            IsReadItLater = readItLater, IsCompleted = completed
        });
        await db.SaveChangesAsync();
    }

    // ── Notification assertion helpers ───────────────────────────────────────────

    private async Task<Notification?> SingleNotificationOrDefaultAsync(int recipientId, NotificationTypeEnum type)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications
            .SingleOrDefaultAsync(n => n.RecipientUserId == recipientId && n.NotificationTypeId == type);
    }

    private async Task<int> CountNotificationsAsync(int recipientId, NotificationTypeEnum? type = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications
            .CountAsync(n => n.RecipientUserId == recipientId
                             && (type == null || n.NotificationTypeId == type));
    }

    private async Task<int> CountUnreadNotificationsAsync(int recipientId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.CountAsync(n => n.RecipientUserId == recipientId && !n.IsRead);
    }

    private async Task<int> CountAllNotificationsOfTypeAsync(NotificationTypeEnum type)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Notifications.CountAsync(n => n.NotificationTypeId == type);
    }

    private async Task MarkAllReadAsync(int recipientId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Notifications
            .Where(n => n.RecipientUserId == recipientId)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
    }
}
