using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The enforcement mechanism for conditionality kind (g) — the parent-visibility invariant
/// (<c>identity-and-authorization.md</c> §"Parent-visibility guards", WU-ParentVisibility):
/// <b>child content is never more visible, nor more writable, than the parent content that hosts it.</b>
///
/// <para>
/// Every surface the WU-ParentVisibility sweep governs is enrolled here. Each test seeds a parent that
/// is hidden in one specific way — unpublished, non-public story status, taken down, rating-gated
/// without a reveal, M-audience group, Private profile — and asserts the child read comes back empty
/// and the child write is refused. Adding a new parent-scoped read or write means adding a row here.
/// </para>
///
/// <para>
/// <b>Why this file exists rather than trusting the convention doc.</b> The rule was already written
/// down (as the "join-not-bare-projection rule" in <c>layer2-services.md</c>) and the WU-AccessGate
/// sweep still shipped <c>GetUserNeighborsAsync</c> returning a Private profile's contents to
/// anonymous callers. Prose did not hold; a failing test will.
/// </para>
///
/// <para>
/// <b>Two axes, deliberately not the same.</b> Confidentiality (story status, takedown) is absolute —
/// no consent bypasses it. Consent (content rating) is bypassable by a reveal, and a few writes are
/// deliberately rating-permissive because listing or recommending is not reading; those cases are
/// called out individually below and assert the permissive behavior on purpose.
/// </para>
/// </summary>
[Collection("Postgres")]
public class ParentVisibilityContractTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _strangerId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync("pv-author");
        _strangerId = await SeedUserAsync("pv-stranger");
    }

    private T Resolve<T>(IServiceScope scope) where T : notnull =>
        scope.ServiceProvider.GetRequiredService<T>();

    /// <summary>Satisfies the 500-character minimum enforced by <c>RecommendationSubmitDto.CanSave</c>.</summary>
    private const string LongRecommendationText =
        "<p>" +
        "This recommendation exists only to satisfy the five-hundred-character minimum that the " +
        "submit path enforces on stripped plain text, so that the assertion under test is about " +
        "parent visibility rather than about validation. It deliberately says nothing interesting " +
        "about any story. The parent-visibility guard runs before validation, so a hidden story " +
        "still fails with KeyNotFoundException and never reaches this length check at all; when " +
        "the story is merely rating-gated the guard lets the call through and the length rule is " +
        "what decides the outcome, which is exactly the distinction these two tests draw." +
        "</p>";

    // ── Seeding helpers for hidden parents ───────────────────────────────────────

    /// <summary>Creates a profile blog post owned by <see cref="_authorId"/>.</summary>
    private async Task<int> SeedProfileBlogPostAsync(bool isPublished, Rating rating = Rating.E)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);

        ProfileBlogPost post = new()
        {
            AuthorId = _authorId,
            Title = $"PV Post {Guid.NewGuid():N}",
            Content = "<p>body</p>",
            Rating = rating,
            IsPublished = isPublished,
            HasSpoilers = false,
            DateCreated = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow,
        };
        db.Set<ProfileBlogPost>().Add(post);
        await db.SaveChangesAsync();
        return post.BlogPostId;
    }

    /// <summary>Attaches a poll (owned by the post's author) to a blog post.</summary>
    private async Task<int> SeedBlogPostPollAsync(int blogPostId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);

        BlogPostPoll poll = new()
        {
            BlogPostId = blogPostId,
            OwnerId = _authorId,
            PollName = "PV Poll",
            Description = "PV poll description",
            DateOpened = DateTime.UtcNow.AddMinutes(-5),
            AllowMultiple = false,
            ResultsVisibility = PollResultsVisibility.Always,
            AnonymityMode = PollAnonymityMode.Public,
        };
        poll.PollOptions.Add(new PollOption { Text = "Option A", SortOrder = 0 });
        poll.PollOptions.Add(new PollOption { Text = "Option B", SortOrder = 1 });
        db.Polls.Add(poll);
        await db.SaveChangesAsync();
        return poll.PollId;
    }

    private async Task<int> SeedChapterAsync(int storyId, bool isPublished)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);

        Chapter chapter = new()
        {
            StoryId = storyId,
            ChapterNumber = 1,
            Title = "PV Chapter",
            IsPublished = isPublished,
        };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        return chapter.ChapterId;
    }

    private async Task<int> SeedGroupAsync(Rating audience)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);

        Group group = new()
        {
            CreatorId = _authorId,
            GroupName = $"PV Group {Guid.NewGuid():N}",
            Description = "PV group",
            AudienceRating = audience,
            MaxContentRating = audience,
            DateCreated = DateTime.UtcNow,
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return group.GroupId;
    }

    private async Task SetProfileVisibilityAsync(int userId, ProfileVisibility visibility)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);

        User user = await db.Users.FirstAsync(u => u.Id == userId);
        user.PrivacySettings.ProfileVisibility = visibility;
        await db.SaveChangesAsync();
    }

    private async Task TakeDownStoryAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);
        await db.Stories.Where(s => s.StoryId == storyId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsTakenDown, true));
    }

    // ══ Polls — the original D2 ══════════════════════════════════════════════════

    [Fact]
    public async Task Polls_ByBlogPost_DraftParent_HiddenFromStranger()
    {
        int postId = await SeedProfileBlogPostAsync(isPublished: false);
        await SeedBlogPostPollAsync(postId);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        PollDto[] polls = await Resolve<IPollReadService>(scope).GetPollsForBlogPostAsync(postId);

        polls.Should().BeEmpty("a poll on an unpublished draft must be invisible to non-authors (D2)");
    }

    [Fact]
    public async Task Polls_ByBlogPost_DraftParent_StillVisibleToAuthor()
    {
        int postId = await SeedProfileBlogPostAsync(isPublished: false);
        await SeedBlogPostPollAsync(postId);

        SetActiveUser(_authorId);
        using IServiceScope scope = Factory.Services.CreateScope();

        PollDto[] polls = await Resolve<IPollReadService>(scope).GetPollsForBlogPostAsync(postId);

        polls.Should().ContainSingle(
            "the author must keep managing their own draft's poll — the blog editor depends on it");
    }

    [Fact]
    public async Task Polls_ByPollId_DraftParent_HiddenFromStranger()
    {
        int postId = await SeedProfileBlogPostAsync(isPublished: false);
        int pollId = await SeedBlogPostPollAsync(postId);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        PollDto? poll = await Resolve<IPollReadService>(scope).GetPollAsync(pollId);

        poll.Should().BeNull("poll ids are enumerable, so by-id is the wider half of D2");
    }

    [Fact]
    public async Task Polls_Vote_DraftParent_Refused()
    {
        int postId = await SeedProfileBlogPostAsync(isPublished: false);
        int pollId = await SeedBlogPostPollAsync(postId);

        int optionId;
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            optionId = await db.PollOptions.Where(o => o.PollId == pollId)
                .OrderBy(o => o.SortOrder).Select(o => o.PollOptionId).FirstAsync();
        }

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IPollWriteService>(scope).VoteAsync(pollId, [optionId], false);

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "voting on a draft's poll sets ConfigLocked and freezes the author's config pre-publication");

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext verifyDb = Resolve<ApplicationDbContext>(verifyScope);
        (await verifyDb.PollVotes.AnyAsync(v => v.PollOptionId == optionId))
            .Should().BeFalse("the refusal must leave no vote row behind");
    }

    // ══ Comments — all four contexts ═════════════════════════════════════════════

    [Fact]
    public async Task Comments_BlogPost_DraftParent_ReadEmptyAndWriteRefused()
    {
        int postId = await SeedProfileBlogPostAsync(isPublished: false);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        CommentPageDto page = await Resolve<ICommentReadService>(scope)
            .GetBlogPostCommentsAsync(postId, 1, 20);
        page.Comments.Should().BeEmpty();

        Func<Task> act = () => Resolve<ICommentWriteService>(scope)
            .PostBlogPostCommentAsync(new PostBlogPostCommentDto
            {
                BlogPostId = postId,
                CommentText = "<p>should not land</p>",
            });

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Comments_Chapter_DraftChapter_ReadEmptyAndWriteRefused()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int chapterId = await SeedChapterAsync(storyId, isPublished: false);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        CommentPageDto page = await Resolve<ICommentReadService>(scope)
            .GetChapterCommentsAsync(chapterId, 1, 20);
        page.Comments.Should().BeEmpty();

        Func<Task> act = () => Resolve<ICommentWriteService>(scope)
            .PostChapterCommentAsync(new PostChapterCommentDto
            {
                ChapterId = chapterId,
                CommentText = "<p>should not land</p>",
            });

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Comments_Chapter_DraftStoryStatus_ReadEmpty()
    {
        // The chapter is published but the STORY is still a draft — confidentiality axis.
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);
        int chapterId = await SeedChapterAsync(storyId, isPublished: true);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        CommentPageDto page = await Resolve<ICommentReadService>(scope)
            .GetChapterCommentsAsync(chapterId, 1, 20);

        page.Comments.Should().BeEmpty("a published chapter of an unpublished story is still hidden");
    }

    [Fact]
    public async Task Comments_Group_MatureAudience_MatureOffViewer_ReadEmptyAndWriteRefused()
    {
        int groupId = await SeedGroupAsync(Rating.M);

        SetActiveUser(_strangerId); // ShowMatureContent = false
        using IServiceScope scope = Factory.Services.CreateScope();

        CommentPageDto page = await Resolve<ICommentReadService>(scope)
            .GetGroupCommentsAsync(groupId, 1, 20);
        page.Comments.Should().BeEmpty("the GroupAudience filter never reached a bare-GroupId query");

        Func<Task> act = () => Resolve<ICommentWriteService>(scope)
            .PostGroupCommentAsync(new PostGroupCommentDto
            {
                GroupId = groupId,
                CommentText = "<p>should not land</p>",
            });

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "a mature-off account could seed a wall it cannot read");
    }

    [Fact]
    public async Task Comments_Profile_PrivateProfile_WriteRefused()
    {
        await SetProfileVisibilityAsync(_authorId, ProfileVisibility.Private);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<ICommentWriteService>(scope)
            .PostUserProfileCommentAsync(new PostUserProfileCommentDto(
                _authorId, null, "<p>should not land</p>"));

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "the read has called ProfileVisibilityGuard since WU-AccessGate; the write never did");
    }

    // ══ Blog posts ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task BlogPost_Like_DraftParent_Refused()
    {
        int postId = await SeedProfileBlogPostAsync(isPublished: false);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IBlogPostWriteService>(scope).ToggleLikeAsync(postId);

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "a non-author could inflate LikeCount on an unpublished draft");
    }

    [Fact]
    public async Task BlogPosts_ByGroup_MatureAudience_MatureOffViewer_Empty()
    {
        int groupId = await SeedGroupAsync(Rating.M);

        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            db.Set<GroupBlogPost>().Add(new GroupBlogPost
            {
                GroupId = groupId,
                AuthorId = _authorId,
                Title = "PV group post",
                Content = "<p>body</p>",
                Rating = Rating.E,       // E-rated post inside an M-audience group
                IsPublished = true,
                DateCreated = DateTime.UtcNow,
                LastUpdatedDate = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        (BlogPostListingDto[] Items, int TotalCount) result =
            await Resolve<IBlogPostReadService>(scope).GetByGroupAsync(groupId, 1, 20);

        result.Items.Should().BeEmpty(
            "the post's own E rating passed the ceiling; the GROUP's M audience is the gate");
    }

    // ══ Groups ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Groups_Members_MatureAudience_MatureOffViewer_Empty()
    {
        int groupId = await SeedGroupAsync(Rating.M);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        (GroupMemberDto[] Members, int TotalCount) result =
            await Resolve<IGroupReadService>(scope).GetMembersAsync(groupId, 1, 20);

        result.Members.Should().BeEmpty("the roster is as visible as the group");
    }

    [Fact]
    public async Task Groups_Join_MatureAudience_MatureOffViewer_Refused()
    {
        int groupId = await SeedGroupAsync(Rating.M);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IGroupWriteService>(scope).JoinAsync(groupId);

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "joining unlocked the membership-gated writes and M-content notification fan-out");
    }

    // ══ Stories and their children ═══════════════════════════════════════════════

    [Fact]
    public async Task StoryArcs_DraftStory_Empty()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);

        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            db.StoryArcs.Add(new StoryArc
            {
                StoryId = storyId,
                Title = "Spoiler-shaped arc title",
                StartChapterNumber = 1,
                EndChapterNumber = 5,
            });
            await db.SaveChangesAsync();
        }

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        IReadOnlyList<StoryArcDto> arcs =
            await Resolve<IStoryArcReadService>(scope).GetArcsForStoryAsync(storyId);

        arcs.Should().BeEmpty("arc titles and ranges are a story's narrative skeleton");
    }

    [Fact]
    public async Task StoryTotalViews_TakenDownStory_ReturnsZero()
    {
        int storyId = await SeedStoryAsync(_authorId);
        await TakeDownStoryAsync(storyId);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        long views = await Resolve<IStoryReadService>(scope).GetStoryTotalViewsAsync(storyId);

        views.Should().Be(0, "raw SQL has no EF model and therefore no query filter at all");
    }

    [Fact]
    public async Task Recommendations_ForStory_DraftStory_Empty()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);

        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            Recommendation rec = new()
            {
                StoryId = storyId,
                RecommenderId = _strangerId,
                StatusId = (short)RecommendationStatusEnum.Approved,
                DatePosted = DateTime.UtcNow,
                RecommendationDetail = new RecommendationDetail { Text = "<p>endorsement</p>" },
            };
            db.Recommendations.Add(rec);
            await db.SaveChangesAsync();
        }

        int viewerId = await SeedUserAsync("pv-viewer");
        SetActiveUser(viewerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        List<RecommendationDto> recs =
            await Resolve<IRecommendationReadService>(scope).GetForStoryAsync(storyId);

        recs.Should().BeEmpty("recommendations are as visible as the story they endorse");
    }

    [Fact]
    public async Task Recommendations_Submit_DraftStory_Refused()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IRecommendationWriteService>(scope)
            .SubmitAsync(new RecommendationSubmitDto(
                storyId, LongRecommendationText));

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "a rec on an unpublished story takes the one-per-user slot permanently");
    }

    [Fact]
    public async Task Recommendations_Submit_MRatedStory_MatureOffUser_StillAllowed()
    {
        // Deliberately permissive on the CONSENT axis (WU29). This asserts the exception is
        // preserved, so a future tightening of the guard cannot silently revoke it.
        int storyId = await SeedStoryAsync(_authorId, rating: Rating.M);

        SetActiveUser(_strangerId); // ShowMatureContent = false
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IRecommendationWriteService>(scope)
            .SubmitAsync(new RecommendationSubmitDto(
                storyId, LongRecommendationText));

        await act.Should().NotThrowAsync(
            "mature-off users may still recommend an M-rated story — settled WU29 behavior");
    }

    [Fact]
    public async Task UserStoryInteraction_Favorite_DraftStory_Refused()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IUserStoryInteractionWriteService>(scope)
            .SetUserStoryInteractionStateAsync(storyId, new UserStoryInteractionStateUpdate(
                IsFavorite: true, IsHiddenFavorite: false, IsFollowed: false,
                IsCompleted: false, IsReadItLater: false, IsIgnored: false));

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "favoriting bumps the story AUTHOR's public FavoritesOnStories counter");
    }

    [Fact]
    public async Task ChapterReadMark_DraftChapter_Refused()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int chapterId = await SeedChapterAsync(storyId, isPublished: false);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IChapterReadMarkWriteService>(scope)
            .SetChapterReadAsync(chapterId, true);

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "a read-mark on a draft cascades into MarkStarted/MarkCompleted on the hidden story");
    }

    [Fact]
    public async Task CustomList_AddStory_DraftStory_Refused()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        int listId = await Resolve<ICustomListWriteService>(scope)
            .CreateListAsync("PV list", isPublic: true);

        Func<Task> act = () => Resolve<ICustomListWriteService>(scope).AddStoryAsync(listId, storyId);

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "a public list could otherwise enumerate hidden story ids to every viewer");
    }

    // ══ Profiles ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Following_Follow_PrivateProfile_Refused()
    {
        await SetProfileVisibilityAsync(_authorId, ProfileVisibility.Private);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IFollowingWriteService>(scope).FollowAsync(_authorId);

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "following bumps the target's public FollowerCount and fires a notification");
    }

    [Fact]
    public async Task Following_Vouch_PrivateProfile_Refused()
    {
        await SetProfileVisibilityAsync(_authorId, ProfileVisibility.Private);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IFollowingWriteService>(scope)
            .VouchAsync(_authorId, "<p>attacker-authored HTML</p>");

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "a vouch persists caller-authored HTML onto a profile the caller cannot open");
    }

    [Fact]
    public async Task ManualTreeSearch_UserNeighbors_PrivateProfile_Empty()
    {
        int storyId = await SeedStoryAsync(_authorId);

        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            db.UserStoryInteractions.Add(new UserStoryInteraction
            {
                UserId = _authorId,
                StoryId = storyId,
                IsFavorite = true,
            });
            await db.SaveChangesAsync();
        }

        await SetProfileVisibilityAsync(_authorId, ProfileVisibility.Private);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        using IServiceScope scope = Factory.Services.CreateScope();

        ManualTreeNeighborsDto result = await Resolve<IManualTreeSearchReadService>(scope)
            .GetUserNeighborsAsync(new UserNeighborsRequest
            {
                UserId = _authorId,
                IncludeFavorites = true,
                PageSize = 20,
            });

        result.Favorites.Should().BeNull(
            "this is the surface the WU-AccessGate sweep missed — anonymous callers reached a "
            + "Private profile's favorites, authored stories and pinned story");
    }

    // ══ Moderation ═══════════════════════════════════════════════════════════════

    [Fact]
    public async Task Report_NonexistentTarget_Refused()
    {
        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IModerationWriteService>(scope)
            .SubmitReportAsync(new SubmitReportRequest(
                ReportedEntityType.Story, 999_999, 1, null));

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "no existence check at all meant the queue could hold dangling reports");
    }

    [Fact]
    public async Task Report_DraftStory_Refused()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IModerationWriteService>(scope)
            .SubmitReportAsync(new SubmitReportRequest(
                ReportedEntityType.Story, storyId, 1, null));

        await act.Should().ThrowAsync<KeyNotFoundException>();

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(verifyScope);
        int reportCount = await db.Stories.Where(s => s.StoryId == storyId)
            .Select(s => s.ActiveReportCount).FirstAsync();
        reportCount.Should().Be(0, "ActiveReportCount must not be bumpable on unpublished content");
    }

    [Fact]
    public async Task Report_TakenDownStory_StillAccepted()
    {
        // The takedown exemption (settled 2026-07-26): a good-faith report filed just after a
        // moderator removes the content must still land rather than erroring.
        int storyId = await SeedStoryAsync(_authorId);
        await TakeDownStoryAsync(storyId);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IModerationWriteService>(scope)
            .SubmitReportAsync(new SubmitReportRequest(
                ReportedEntityType.Story, storyId, 1, null));

        await act.Should().NotThrowAsync(
            "takedown is the one hiding reason that must not block a report");
    }
}
