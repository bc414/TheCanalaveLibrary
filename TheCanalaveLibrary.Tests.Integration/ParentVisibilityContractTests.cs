using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The enforcement mechanism for conditionality kind (g) — the parent-visibility invariant
/// (<c>identity-and-authorization.md</c> §"Parent-visibility guards", WU-ParentVisibility):
/// <b>child content is never more visible, nor more raisable, than the parent content that hosts it.</b>
///
/// <para>
/// Every surface the WU-ParentVisibility sweep governs is enrolled here. Each test seeds a parent that
/// is hidden in one specific way — unpublished, non-public story status, taken down, rating-gated
/// without a reveal, M-audience group, Private profile — and asserts the child read comes back empty
/// and the child raise is refused. Adding a new parent-scoped read or write means adding a row here.
/// </para>
///
/// <para>
/// <b>Raises vs clears (owner ruling D6, WU-AccessGateSweep2).</b> The refusals above are all
/// <i>raises</i> — a write that creates state or flips a bit false→true. A <i>clear</i> or lower on
/// the caller's own existing row is never visibility-guarded, on any axis, because it discloses and
/// entangles nothing. The "Clears on the caller's own row always succeed" section mirrors the raise
/// cases one axis at a time, plus the riders (a mixed payload is refused whole; no row + all-false is
/// a silent no-op with no existence oracle) and the conformance of every sibling clear that was
/// already unguarded — enrolment is the record. Without those mirrors a future sweep for missing
/// guards would re-add one to a clear, which is how the over-filtering lockout D6 corrected shipped.
/// </para>
///
/// <para>
/// <b>Enrolment is a claim this file has to keep honest.</b> The first draft of this suite asserted the
/// sentence above while nine of the 38 governed surfaces had no test — including
/// <c>RecordSuccessAsync</c>, the badge-award path, and both buffered writes whose drain-time validation
/// had nothing proving it drops hidden rows. The suite was green throughout. If a surface is listed in
/// the WU's inventory, it needs a test here; "the guard is called from that method" is not coverage.
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

    // ══ Coverage-gap closure (2026-07-26) ════════════════════════════════════════
    // The nine surfaces below were governed by the sweep but had no test in this suite's first
    // draft, while its doc comment already claimed full enrolment. See the note in that comment.

    [Fact]
    public async Task Recommendations_RecordSuccess_HiddenStory_Refused()
    {
        // The badge-award path: this awards Recommender/RecommenderSilver off the parent, so an
        // unguarded version lets a loop over guessed rec ids farm another user's counters and real
        // site badges without ever being able to see the stories involved.
        int storyId = await SeedStoryAsync(_authorId);
        int recId;

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
            recId = rec.RecommendationId;
        }

        await TakeDownStoryAsync(storyId); // parent now hidden

        int readerId = await SeedUserAsync("pv-reader");
        SetActiveUser(readerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IRecommendationWriteService>(scope).RecordSuccessAsync(recId);

        await act.Should().ThrowAsync<KeyNotFoundException>();

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext verifyDb = Resolve<ApplicationDbContext>(verifyScope);
        (await verifyDb.RecommendationSuccesses.AnyAsync(s => s.RecommendationId == recId))
            .Should().BeFalse("the refusal must leave no credit row behind");
        (await verifyDb.Recommendations.Where(r => r.RecommendationId == recId)
            .Select(r => r.SuccessfulRecCount).FirstAsync())
            .Should().Be(0, "and must not have moved the counter that feeds the badge threshold");
    }

    [Fact]
    public async Task Recommendations_RecordAttributionSource_DraftStory_Refused()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);
        int recId;

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
            recId = rec.RecommendationId;
        }

        int readerId = await SeedUserAsync("pv-attrib");
        SetActiveUser(readerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IRecommendationWriteService>(scope)
            .RecordAttributionSourceAsync(storyId, recId);

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "attribution feeds RecordSuccessAsync credit downstream");
    }

    [Fact]
    public async Task Comments_ToggleLike_DraftParent_Refused()
    {
        // Seeded directly: posting the comment through the service would (correctly) be refused.
        int postId = await SeedProfileBlogPostAsync(isPublished: false);
        long commentId;

        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            BlogPostComment comment = new()
            {
                BlogPostId = postId,
                UserId = _authorId,
                CommentText = "<p>author's own note on their draft</p>",
                DatePosted = DateTime.UtcNow,
            };
            db.BlogPostComments.Add(comment);
            await db.SaveChangesAsync();
            commentId = comment.CommentId;
        }

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<ICommentWriteService>(scope).ToggleLikeAsync(commentId);

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "the like is context-agnostic at the call site; the guard resolves the owning context");
    }

    [Fact]
    public async Task UserStoryInteraction_MarkStarted_DraftStory_Refused()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IUserStoryInteractionWriteService>(scope)
            .MarkStartedAsync(storyId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task UserStoryInteraction_MarkCompleted_DraftStory_Refused()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IUserStoryInteractionWriteService>(scope)
            .MarkCompletedAsync(storyId);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ChapterReadMark_SetAll_HiddenStoryWithPublishedChapters_Refused()
    {
        // The existing IsPublished chapter filter already made an all-draft story a no-op; a story
        // with PUBLISHED chapters that is itself hidden was the case it did not cover.
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);
        await SeedChapterAsync(storyId, isPublished: true);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IChapterReadMarkWriteService>(scope)
            .SetAllChaptersReadAsync(storyId, true);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task StoryLineage_Request_HiddenTarget_Refused()
    {
        int sourceStoryId = await SeedStoryAsync(_strangerId);                                  // owned by requester
        int targetStoryId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);      // someone else's draft

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();

        Func<Task> act = () => Resolve<IStoryLineageWriteService>(scope)
            .RequestLineageAsync(new CreateStoryLineageDto
            {
                SourceStoryId = sourceStoryId,
                TargetStoryId = targetStoryId,
                TypeId = 1,   // seeded lookup row
            });

        await act.Should().ThrowAsync<StoryLineageValidationException>(
            "the found-vs-not-found distinction was an id oracle over the whole Stories keyspace");
    }

    [Fact]
    public async Task ManualTreeSearch_NodeDisplays_PrivateProfile_Pruned()
    {
        await SetProfileVisibilityAsync(_authorId, ProfileVisibility.Private);

        SetActiveUser(FakeActiveUserContext.Anonymous());
        using IServiceScope scope = Factory.Services.CreateScope();

        ManualTreeNodeDisplaysDto result = await Resolve<IManualTreeSearchReadService>(scope)
            .GetNodeDisplaysAsync([], [_authorId, _strangerId]);

        result.Users.Should().NotContain(u => u.EntityId == _authorId,
            "a node the viewer may no longer see must not come back (rehydration contract)");
        result.Users.Should().Contain(u => u.EntityId == _strangerId,
            "a public profile in the same batch must still resolve");
    }

    [Fact]
    public async Task BufferedWrites_HiddenStory_DroppedAtDrainTime()
    {
        // The settled 2026-07-26 decision: buffer entry stays a pure in-memory write and the
        // visibility check happens in the flusher. Nothing proved that until this test — the entry
        // call must SUCCEED (no exception, no query) while the row never reaches the database.
        int hiddenStoryId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);
        int visibleStoryId = await SeedStoryAsync(_authorId);

        SetActiveUser(FakeActiveUserContext.Anonymous()); // view counts are anonymous-reachable

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            IViewCountWriteService views = Resolve<IViewCountWriteService>(scope);
            await views.RecordViewAsync(hiddenStoryId);   // must not throw — buffer-only by design
            await views.RecordViewAsync(visibleStoryId);
        }

        await Factory.Services.GetRequiredService<ViewCountFlusher>().FlushAsync();

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(verifyScope);

        int hiddenRows = await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM daily_story_stats WHERE story_id = {hiddenStoryId}")
            .SingleAsync();
        int visibleRows = await db.Database
            .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM daily_story_stats WHERE story_id = {visibleStoryId}")
            .SingleAsync();

        hiddenRows.Should().Be(0, "the drain-time predicate must drop views of an unpublished story");
        visibleRows.Should().Be(1, "and must leave the legitimate story's views untouched");
    }

    // ══ Clears on the caller's own row always succeed (D6, WU-AccessGateSweep2) ══════════════
    // Owner ruling D6 (worksheet 2026-08-04): a raise keeps the full guard; a clear or lower on the
    // caller's own existing row is permitted on every axis — rating, status, takedown. Each test
    // creates the caller's row first and hides the parent afterwards, the realistic order
    // (favorited while visible, then taken down / unpublished / mature turned off).

    private static UserStoryInteractionStateUpdate Usi(
        bool favorite = false, bool followed = false, bool readItLater = false) =>
        new(IsFavorite: favorite, IsHiddenFavorite: false, IsFollowed: followed,
            IsCompleted: false, IsReadItLater: readItLater, IsIgnored: false);

    private async Task SeedUsiRowAsync(int userId, int storyId,
        bool favorite = false, bool followed = false, bool readItLater = false, bool hasStarted = false)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);
        db.UserStoryInteractions.Add(new UserStoryInteraction
        {
            UserId = userId,
            StoryId = storyId,
            IsFavorite = favorite,
            IsFollowed = followed,
            IsReadItLater = readItLater,
            HasStarted = hasStarted,
        });
        await db.SaveChangesAsync();
    }

    private async Task<UserStoryInteraction?> LoadUsiRowAsync(int userId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);
        return await db.UserStoryInteractions.AsNoTracking()
            .FirstOrDefaultAsync(i => i.UserId == userId && i.StoryId == storyId);
    }

    private async Task SetUsiAsync(int storyId, UserStoryInteractionStateUpdate update)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        await Resolve<IUserStoryInteractionWriteService>(scope)
            .SetUserStoryInteractionStateAsync(storyId, update);
    }

    /// <summary>
    /// The counter producers are <c>ExecuteUpdate</c>s that silently no-op without a row, and no
    /// production path creates one at registration — seed it explicitly.
    /// </summary>
    private async Task SeedUserStatAsync(int userId, int favoritesOnStories = 0)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);
        db.UserStats.Add(new UserStat { UserId = userId, FavoritesOnStories = favoritesOnStories });
        await db.SaveChangesAsync();
    }

    private async Task SeedReadMarkAsync(int userId, int chapterId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);
        db.UserChapterInteractions.Add(new UserChapterInteraction
        {
            UserId = userId,
            ChapterId = chapterId,
            IsRead = true,
            ReadProgress = 1f,
            LastInteractionDate = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<UserChapterInteraction?> LoadReadMarkAsync(int userId, int chapterId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);
        return await db.UserChapterInteractions.AsNoTracking()
            .FirstOrDefaultAsync(i => i.UserId == userId && i.ChapterId == chapterId);
    }

    // ── UserStoryInteraction panel: one clear per axis ────────────────────────────────

    [Fact]
    public async Task UserStoryInteraction_Clear_TakenDownStory_Succeeds()
    {
        int storyId = await SeedStoryAsync(_authorId);
        await SeedUsiRowAsync(_strangerId, storyId, favorite: true, readItLater: true);
        await TakeDownStoryAsync(storyId);

        SetActiveUser(_strangerId);
        // Unfavorite; ReadItLater stays true — an unchanged true bit is not a raise.
        await SetUsiAsync(storyId, Usi(readItLater: true));

        UserStoryInteraction? row = await LoadUsiRowAsync(_strangerId, storyId);
        row.Should().NotBeNull();
        row!.IsFavorite.Should().BeFalse(
            "takedown is never reveal-bypassable — refusing the clear froze the reader's row permanently");
        row.IsReadItLater.Should().BeTrue("the untouched bit keeps its value");
    }

    [Fact]
    public async Task UserStoryInteraction_Clear_DraftStory_SparseCleanupDeletesRow()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);
        await SeedUsiRowAsync(_strangerId, storyId, favorite: true);

        SetActiveUser(_strangerId);
        await SetUsiAsync(storyId, Usi());

        (await LoadUsiRowAsync(_strangerId, storyId)).Should().BeNull(
            "an all-false update deleting the row is a clear — since D1 any author can unpublish, and "
            + "that must not freeze their readers' rows");
    }

    [Fact]
    public async Task UserStoryInteraction_Clear_MRatedStory_MatureOffViewer_Succeeds()
    {
        int storyId = await SeedStoryAsync(_authorId, rating: Rating.M);
        await SeedUsiRowAsync(_strangerId, storyId, favorite: true, followed: true);

        SetActiveUser(_strangerId); // ShowMatureContent = false, no reveal
        await SetUsiAsync(storyId, Usi(followed: true));

        UserStoryInteraction? row = await LoadUsiRowAsync(_strangerId, storyId);
        row!.IsFavorite.Should().BeFalse(
            "a mature-off reader must be able to un-favorite an M story favorited while mature-on");
        row.IsFollowed.Should().BeTrue();
    }

    // ── UserStoryInteraction riders ───────────────────────────────────────────────────

    [Fact]
    public async Task UserStoryInteraction_MixedPayload_HiddenStory_RefusedWhole_RowUnchanged()
    {
        int storyId = await SeedStoryAsync(_authorId);
        await SeedUsiRowAsync(_strangerId, storyId, favorite: true);
        await TakeDownStoryAsync(storyId);

        SetActiveUser(_strangerId);
        // favorite true→false (a clear) AND followed false→true (a raise) in one payload.
        Func<Task> act = () => SetUsiAsync(storyId, Usi(followed: true));

        await act.Should().ThrowAsync<KeyNotFoundException>("any raise anywhere guards the whole call");

        UserStoryInteraction? row = await LoadUsiRowAsync(_strangerId, storyId);
        row!.IsFavorite.Should().BeTrue("no per-bit partial application — the clear half must not land either");
        row.IsFollowed.Should().BeFalse();
    }

    [Fact]
    public async Task UserStoryInteraction_NoRow_AllFalse_HiddenOrNonexistentStory_SilentNoOp()
    {
        int hiddenStoryId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);

        SetActiveUser(_strangerId);

        Func<Task> hidden = () => SetUsiAsync(hiddenStoryId, Usi());
        Func<Task> absent = () => SetUsiAsync(999_999, Usi());

        await hidden.Should().NotThrowAsync("nothing to clear and nothing to raise");
        await absent.Should().NotThrowAsync(
            "a hidden story and a nonexistent one must answer identically — the early return precedes "
            + "the guard, so the reorder adds no existence oracle");
        (await LoadUsiRowAsync(_strangerId, hiddenStoryId)).Should().BeNull("and no row is created");
    }

    [Fact]
    public async Task UserStoryInteraction_Clear_TakenDownStory_DecrementsAuthorFavoritesCounter()
    {
        int storyId = await SeedStoryAsync(_authorId);
        await SeedUserStatAsync(_authorId, favoritesOnStories: 1);
        await SeedUsiRowAsync(_strangerId, storyId, favorite: true);
        await TakeDownStoryAsync(storyId);

        SetActiveUser(_strangerId);
        await SetUsiAsync(storyId, Usi());

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);
        int favorites = await db.UserStats.Where(us => us.UserId == _authorId)
            .Select(us => us.FavoritesOnStories).FirstAsync();
        favorites.Should().Be(0,
            "D6's accepted counter consequence — the favorite is genuinely withdrawn, even while hidden");
    }

    // ── Chapter read-marks: mark-unread is a clear ────────────────────────────────────

    [Theory]
    [InlineData("taken-down story")]
    [InlineData("draft story")]
    [InlineData("unpublished chapter")]
    public async Task ChapterReadMark_Unread_HiddenParent_Succeeds_HasStartedKept(string hiddenBy)
    {
        int storyId = await SeedStoryAsync(_authorId,
            status: hiddenBy == "draft story" ? StoryStatusEnum.Draft : StoryStatusEnum.InProgress);
        int chapterId = await SeedChapterAsync(storyId, isPublished: hiddenBy != "unpublished chapter");
        await SeedReadMarkAsync(_strangerId, chapterId);
        await SeedUsiRowAsync(_strangerId, storyId, hasStarted: true);
        if (hiddenBy == "taken-down story") await TakeDownStoryAsync(storyId);

        SetActiveUser(_strangerId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await Resolve<IChapterReadMarkWriteService>(scope).SetChapterReadAsync(chapterId, false);

        UserChapterInteraction? mark = await LoadReadMarkAsync(_strangerId, chapterId);
        mark!.IsRead.Should().BeFalse($"mark-unread must succeed on a {hiddenBy}");
        mark.ReadProgress.Should().Be(0f, "both fields move together (WU45)");

        (await LoadUsiRowAsync(_strangerId, storyId))!.HasStarted.Should().BeTrue(
            "HasStarted stays non-clearable — D6 does not make it a clear");
    }

    [Fact]
    public async Task ChapterReadMark_Unread_NonexistentIds_SilentNoOp()
    {
        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadMarkWriteService marks = Resolve<IChapterReadMarkWriteService>(scope);

        await marks.Invoking(m => m.SetChapterReadAsync(999_999, false)).Should().NotThrowAsync(
            "the clear path does no chapter lookup — 'hidden succeeds, absent 404s' would be an oracle");
        await marks.Invoking(m => m.SetAllChaptersReadAsync(999_999, false)).Should().NotThrowAsync(
            "same for mark-all-unread: an absent story is an empty chapter list");
    }

    [Fact]
    public async Task ChapterReadMark_SetAllUnread_TakenDownStory_ClearsExistingRows()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int chapterId = await SeedChapterAsync(storyId, isPublished: true);
        await SeedReadMarkAsync(_strangerId, chapterId);
        await TakeDownStoryAsync(storyId);

        SetActiveUser(_strangerId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await Resolve<IChapterReadMarkWriteService>(scope).SetAllChaptersReadAsync(storyId, false);

        (await LoadReadMarkAsync(_strangerId, chapterId))!.IsRead.Should().BeFalse(
            "mark-all-unread is a clear of the caller's own rows");
    }

    // ── Likes: an existing like row makes the toggle an unlike, i.e. a clear ─────────

    [Fact]
    public async Task BlogPost_Unlike_PostUnpublishedAfterLike_Succeeds()
    {
        int postId = await SeedProfileBlogPostAsync(isPublished: true);
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            db.BlogPostLikes.Add(new BlogPostLike { BlogPostId = postId, UserId = _strangerId });
            ProfileBlogPost post = await db.Set<ProfileBlogPost>().FirstAsync(p => p.BlogPostId == postId);
            post.LikeCount = 1;
            post.IsPublished = false; // the author pulled it back after the like
            await db.SaveChangesAsync();
        }

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        BlogPostLikeResultDto result = await Resolve<IBlogPostWriteService>(scope).ToggleLikeAsync(postId);

        result.IsLiked.Should().BeFalse();
        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext verifyDb = Resolve<ApplicationDbContext>(verifyScope);
        (await verifyDb.BlogPostLikes.AnyAsync(l => l.BlogPostId == postId && l.UserId == _strangerId))
            .Should().BeFalse("the caller's own like row is removed");
    }

    [Fact]
    public async Task Comments_Unlike_TakenDownComment_Succeeds()
    {
        int postId = await SeedProfileBlogPostAsync(isPublished: true);
        long commentId;
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            BlogPostComment comment = new()
            {
                BlogPostId = postId,
                UserId = _authorId,
                CommentText = "<p>later removed by a moderator</p>",
                DatePosted = DateTime.UtcNow,
                LikeCount = 1,
                IsTakenDown = true,
            };
            db.BlogPostComments.Add(comment);
            await db.SaveChangesAsync();
            commentId = comment.CommentId;
            db.CommentLikes.Add(new CommentLike { CommentId = commentId, UserId = _strangerId });
            await db.SaveChangesAsync();
        }

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        CommentLikeResultDto result = await Resolve<ICommentWriteService>(scope).ToggleLikeAsync(commentId);

        result.IsLiked.Should().BeFalse();
        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext verifyDb = Resolve<ApplicationDbContext>(verifyScope);
        (await verifyDb.CommentLikes.AnyAsync(l => l.CommentId == commentId && l.UserId == _strangerId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Recommendations_Unlike_TakenDownStory_Succeeds()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int recId;
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            int recommenderId = await SeedUserAsync("pv-recommender");
            Recommendation rec = new()
            {
                StoryId = storyId,
                RecommenderId = recommenderId,
                StatusId = (short)RecommendationStatusEnum.Approved,
                DatePosted = DateTime.UtcNow,
                LikeCount = 1,
                RecommendationDetail = new RecommendationDetail { Text = "<p>endorsement</p>" },
            };
            db.Recommendations.Add(rec);
            await db.SaveChangesAsync();
            recId = rec.RecommendationId;
            db.RecommendationLikes.Add(new RecommendationLike { RecommendationId = recId, UserId = _strangerId });
            await db.SaveChangesAsync();
        }
        await TakeDownStoryAsync(storyId);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        RecommendationLikeResultDto result =
            await Resolve<IRecommendationWriteService>(scope).ToggleLikeAsync(recId);

        result.IsLiked.Should().BeFalse();
        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext verifyDb = Resolve<ApplicationDbContext>(verifyScope);
        (await verifyDb.RecommendationLikes.AnyAsync(l => l.RecommendationId == recId && l.UserId == _strangerId))
            .Should().BeFalse();
    }

    // ── Following: alerts on is a raise, alerts off a clear ───────────────────────────

    private async Task SeedFollowAsync(int followerId, int followedId, bool receiveAlerts)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);
        db.FollowedUsers.Add(new FollowedUser
        {
            UserId = followerId,
            FollowedUserId = followedId,
            ReceiveAlerts = receiveAlerts,
            DateFollowed = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<bool?> LoadReceiveAlertsAsync(int followerId, int followedId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);
        return await db.FollowedUsers
            .Where(f => f.UserId == followerId && f.FollowedUserId == followedId)
            .Select(f => (bool?)f.ReceiveAlerts)
            .FirstOrDefaultAsync();
    }

    [Fact]
    public async Task Following_ReceiveAlertsOn_PrivateProfile_Refused()
    {
        await SeedFollowAsync(_strangerId, _authorId, receiveAlerts: false);
        await SetProfileVisibilityAsync(_authorId, ProfileVisibility.Private);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        Func<Task> act = () => Resolve<IFollowingWriteService>(scope).SetReceiveAlertsAsync(_authorId, true);

        await act.Should().ThrowAsync<KeyNotFoundException>(
            "alerts on re-enrolls the actor in a hidden profile's fan-out — the raise FollowAsync refuses");
        (await LoadReceiveAlertsAsync(_strangerId, _authorId)).Should().BeFalse();
    }

    [Fact]
    public async Task Following_ReceiveAlertsOff_PrivateProfile_Succeeds()
    {
        await SeedFollowAsync(_strangerId, _authorId, receiveAlerts: true);
        await SetProfileVisibilityAsync(_authorId, ProfileVisibility.Private);

        SetActiveUser(_strangerId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await Resolve<IFollowingWriteService>(scope).SetReceiveAlertsAsync(_authorId, false);

        (await LoadReceiveAlertsAsync(_strangerId, _authorId)).Should().BeFalse("turning alerts off is a clear");
    }

    // ── Conformance: sibling clears that were already unguarded (D6's enumeration) ────
    // D6: "where a clear is already unguarded, that is now a recorded conformance rather than an
    // accident". Each is enrolled here so a future sweep for missing guards cannot add one.

    [Fact]
    public async Task Following_UnfollowAndRemoveVouch_PrivateProfile_Succeed()
    {
        await SeedFollowAsync(_strangerId, _authorId, receiveAlerts: true);
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            db.Vouches.Add(new Vouch
            {
                VouchingUserId = _strangerId,
                VouchedUserId = _authorId,
                DateVouched = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        await SetProfileVisibilityAsync(_authorId, ProfileVisibility.Private);

        SetActiveUser(_strangerId);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            IFollowingWriteService following = Resolve<IFollowingWriteService>(scope);
            await following.UnfollowAsync(_authorId);
            await following.RemoveVouchAsync(_authorId);
        }

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext verifyDb = Resolve<ApplicationDbContext>(verifyScope);
        (await verifyDb.FollowedUsers.AnyAsync(f => f.UserId == _strangerId && f.FollowedUserId == _authorId))
            .Should().BeFalse();
        (await verifyDb.Vouches.AnyAsync(v => v.VouchingUserId == _strangerId && v.VouchedUserId == _authorId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Groups_Leave_MatureAudience_MatureOffMember_Succeeds()
    {
        int groupId = await SeedGroupAsync(Rating.M);
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            db.GroupMembers.Add(new GroupMember
            {
                GroupId = groupId,
                UserId = _strangerId,
                Role = GroupRole.Member,
                DateJoined = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        SetActiveUser(_strangerId); // joined while mature-on; mature is now off
        using (IServiceScope scope = Factory.Services.CreateScope())
            await Resolve<IGroupWriteService>(scope).LeaveAsync(groupId);

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext verifyDb = Resolve<ApplicationDbContext>(verifyScope);
        (await verifyDb.GroupMembers.AnyAsync(m => m.GroupId == groupId && m.UserId == _strangerId))
            .Should().BeFalse("JoinAsync is guarded; LeaveAsync must never be");
    }

    [Fact]
    public async Task CustomList_RemoveStory_TakenDownStory_Succeeds()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int listId;
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            CustomList list = new()
            {
                UserId = _strangerId,
                ListName = "PV list",
                IsPublic = true,
                DateCreated = DateTime.UtcNow,
            };
            db.CustomLists.Add(list);
            await db.SaveChangesAsync();
            listId = list.CustomListId;
            db.CustomListEntries.Add(new CustomListEntry { ListId = listId, StoryId = storyId, DateAdded = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await TakeDownStoryAsync(storyId);

        SetActiveUser(_strangerId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await Resolve<ICustomListWriteService>(scope).RemoveStoryAsync(listId, storyId);

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext verifyDb = Resolve<ApplicationDbContext>(verifyScope);
        (await verifyDb.CustomListEntries.AnyAsync(e => e.ListId == listId && e.StoryId == storyId))
            .Should().BeFalse("owner-gated (Class A) and never story-guarded");
    }

    [Fact]
    public async Task Series_RemoveStory_OwnTakenDownStory_Succeeds()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int seriesId;
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            Series series = new() { AuthorId = _authorId, Name = "PV series", DateCreated = DateTime.UtcNow };
            db.Series.Add(series);
            await db.SaveChangesAsync();
            seriesId = series.SeriesId;
            db.SeriesEntries.Add(new SeriesEntry { SeriesId = seriesId, StoryId = storyId, OrderIndex = 0 });
            await db.SaveChangesAsync();
        }
        await TakeDownStoryAsync(storyId);

        SetActiveUser(_authorId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await Resolve<ISeriesWriteService>(scope).RemoveStoryAsync(seriesId, storyId);

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext verifyDb = Resolve<ApplicationDbContext>(verifyScope);
        (await verifyDb.SeriesEntries.AnyAsync(e => e.SeriesId == seriesId && e.StoryId == storyId))
            .Should().BeFalse("takedown hides the story from its author too — removal must still work");
    }

    [Fact]
    public async Task ContentReveal_Remove_TakenDownStory_Succeeds()
    {
        int storyId = await SeedStoryAsync(_authorId, rating: Rating.M);
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            db.UserContentReveals.Add(new UserContentReveal
            {
                UserId = _strangerId,
                EntityType = RevealedEntityType.Story,
                EntityId = storyId,
                DateRevealed = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        await TakeDownStoryAsync(storyId);

        SetActiveUser(_strangerId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await Resolve<IContentRevealService>(scope).RemoveAsync(RevealedEntityType.Story, storyId);

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext verifyDb = Resolve<ApplicationDbContext>(verifyScope);
        (await verifyDb.UserContentReveals.AnyAsync(r => r.UserId == _strangerId && r.EntityId == storyId))
            .Should().BeFalse();
    }

    [Fact]
    public async Task Recommendations_CurationFlagClears_TakenDownStory_Succeed()
    {
        // SetHiddenGemAsync(false) by the recommender and SetHighlightedByAuthorAsync(false) by the
        // story author: both clears, both unguarded. (Their true raises carry no parent guard either —
        // which guard should apply to them is unruled; tracker F10 item 2.)
        int storyId = await SeedStoryAsync(_authorId);
        int recId;
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            Recommendation rec = new()
            {
                StoryId = storyId,
                RecommenderId = _strangerId,
                StatusId = (short)RecommendationStatusEnum.Approved,
                DatePosted = DateTime.UtcNow,
                IsHiddenGem = true,
                IsHighlightedByAuthor = true,
                RecommendationDetail = new RecommendationDetail { Text = "<p>endorsement</p>" },
            };
            db.Recommendations.Add(rec);
            await db.SaveChangesAsync();
            recId = rec.RecommendationId;
        }
        await TakeDownStoryAsync(storyId);

        SetActiveUser(_strangerId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await Resolve<IRecommendationWriteService>(scope).SetHiddenGemAsync(recId, false);

        SetActiveUser(_authorId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            await Resolve<IRecommendationWriteService>(scope).SetHighlightedByAuthorAsync(recId, false);

        using IServiceScope verifyScope = Factory.Services.CreateScope();
        ApplicationDbContext verifyDb = Resolve<ApplicationDbContext>(verifyScope);
        Recommendation stored = await verifyDb.Recommendations.AsNoTracking().FirstAsync(r => r.RecommendationId == recId);
        stored.IsHiddenGem.Should().BeFalse();
        stored.IsHighlightedByAuthor.Should().BeFalse();
    }

    // ══ Profile blog posts are profile-tab data (WU-AccessGateSweep2) ═══════════════════
    // A profile post by id respects its author's ProfileVisibility exactly as the by-author list
    // does (access-gating-first-principles.md §5 row 1b; the F15 permalink precedent). Every child of
    // BlogPostVisibilityGuard inherits it. Group and site posts carry no profile check.

    [Fact]
    public async Task BlogPost_PrivateAuthor_HiddenFromStrangerAndAnonymous_WithEveryChild()
    {
        int postId = await SeedProfileBlogPostAsync(isPublished: true);
        int pollId = await SeedBlogPostPollAsync(postId);
        int optionId;
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            optionId = await db.PollOptions.Where(o => o.PollId == pollId)
                .OrderBy(o => o.SortOrder).Select(o => o.PollOptionId).FirstAsync();
        }
        await SetProfileVisibilityAsync(_authorId, ProfileVisibility.Private);

        SetActiveUser(_strangerId);
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            (await Resolve<IBlogPostReadService>(scope).GetByIdAsync(postId))
                .Should().BeNull("by-id is just another path to a Private profile's tab data");
            (await Resolve<ICommentReadService>(scope).GetBlogPostCommentsAsync(postId, 1, 20))
                .Comments.Should().BeEmpty();
            (await Resolve<IPollReadService>(scope).GetPollsForBlogPostAsync(postId))
                .Should().BeEmpty();

            await Resolve<ICommentWriteService>(scope).Invoking(s => s.PostBlogPostCommentAsync(
                    new PostBlogPostCommentDto { BlogPostId = postId, CommentText = "<p>should not land</p>" }))
                .Should().ThrowAsync<KeyNotFoundException>();
            await Resolve<IPollWriteService>(scope).Invoking(s => s.VoteAsync(pollId, [optionId], false))
                .Should().ThrowAsync<KeyNotFoundException>();
            await Resolve<IBlogPostWriteService>(scope).Invoking(s => s.ToggleLikeAsync(postId))
                .Should().ThrowAsync<KeyNotFoundException>();
        }

        SetActiveUser(FakeActiveUserContext.Anonymous());
        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            (await Resolve<IBlogPostReadService>(scope).GetByIdAsync(postId)).Should().BeNull();
            (await Resolve<ICommentReadService>(scope).GetBlogPostCommentsAsync(postId, 1, 20))
                .Comments.Should().BeEmpty();
            (await Resolve<IPollReadService>(scope).GetPollsForBlogPostAsync(postId)).Should().BeEmpty();
        }
    }

    [Fact]
    public async Task BlogPost_PrivateAuthor_MRated_GateIsRealNotFound()
    {
        int postId = await SeedProfileBlogPostAsync(isPublished: true, rating: Rating.M);
        await SetProfileVisibilityAsync(_authorId, ProfileVisibility.Private);

        foreach (FakeActiveUserContext viewer in new[]
                 {
                     FakeActiveUserContext.AuthenticatedUser(_strangerId, showMatureContent: false),
                     FakeActiveUserContext.Anonymous(),
                 })
        {
            SetActiveUser(viewer);
            using IServiceScope scope = Factory.Services.CreateScope();
            (await Resolve<IBlogPostReadService>(scope).GetBlogPostGateAsync(postId)).Should().BeNull(
                "the interstitial would otherwise disclose a Private profile's post title and author");
        }
    }

    [Fact]
    public async Task BlogPost_PrivateAuthor_AuthorStillSeesOwnPost()
    {
        int postId = await SeedProfileBlogPostAsync(isPublished: true);
        await SetProfileVisibilityAsync(_authorId, ProfileVisibility.Private);

        SetActiveUser(_authorId);
        using IServiceScope scope = Factory.Services.CreateScope();

        (await Resolve<IBlogPostReadService>(scope).GetByIdAsync(postId)).Should().NotBeNull();
    }

    [Fact]
    public async Task BlogPost_UsersOnlyAuthor_VisibleToSignedInStranger_HiddenFromAnonymous()
    {
        int postId = await SeedProfileBlogPostAsync(isPublished: true);
        await SetProfileVisibilityAsync(_authorId, ProfileVisibility.UsersOnly);

        SetActiveUser(_strangerId);
        using (IServiceScope scope = Factory.Services.CreateScope())
            (await Resolve<IBlogPostReadService>(scope).GetByIdAsync(postId)).Should().NotBeNull();

        SetActiveUser(FakeActiveUserContext.Anonymous());
        using (IServiceScope scope = Factory.Services.CreateScope())
            (await Resolve<IBlogPostReadService>(scope).GetByIdAsync(postId)).Should().BeNull();
    }

    [Fact]
    public async Task BlogPost_VerifiedBot_BypassesConsent_NotProfilePrivacy()
    {
        // Control: a Public author's M post IS served to a verified bot (Class B bypass).
        int publicMPostId = await SeedProfileBlogPostAsync(isPublished: true, rating: Rating.M);

        int privateAuthorId = await SeedUserAsync("pv-private-author");
        int privatePostId;
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            ProfileBlogPost post = new()
            {
                AuthorId = privateAuthorId,
                Title = "PV private author post",
                Content = "<p>body</p>",
                Rating = Rating.E,
                IsPublished = true,
                DateCreated = DateTime.UtcNow,
                LastUpdatedDate = DateTime.UtcNow,
            };
            db.Set<ProfileBlogPost>().Add(post);
            await db.SaveChangesAsync();
            privatePostId = post.BlogPostId;
        }
        await SetProfileVisibilityAsync(privateAuthorId, ProfileVisibility.Private);

        SetActiveUser(new FakeActiveUserContext { IsVerifiedBot = true });
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostReadService posts = Resolve<IBlogPostReadService>(scope);

        (await posts.GetByIdAsync(publicMPostId)).Should().NotBeNull("bots bypass consent (Class B)");
        (await posts.GetByIdAsync(privatePostId)).Should().BeNull("bots never bypass privacy (Class A)");
    }

    [Fact]
    public async Task BlogPost_GroupAndSitePosts_UnaffectedByPrivateAuthor()
    {
        int groupId = await SeedGroupAsync(Rating.E);
        int groupPostId;
        int sitePostId;
        using (IServiceScope seedScope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = Resolve<ApplicationDbContext>(seedScope);
            GroupBlogPost groupPost = new()
            {
                GroupId = groupId,
                AuthorId = _authorId,
                Title = "PV group post",
                Content = "<p>body</p>",
                Rating = Rating.E,
                IsPublished = true,
                DateCreated = DateTime.UtcNow,
                LastUpdatedDate = DateTime.UtcNow,
            };
            SiteBlogPost sitePost = new()
            {
                AuthorId = _authorId,
                Title = "PV site announcement",
                Content = "<p>body</p>",
                Rating = Rating.E,
                IsPublished = true,
                DateCreated = DateTime.UtcNow,
                LastUpdatedDate = DateTime.UtcNow,
            };
            db.Set<GroupBlogPost>().Add(groupPost);
            db.SiteBlogPosts.Add(sitePost);
            await db.SaveChangesAsync();
            groupPostId = groupPost.BlogPostId;
            sitePostId = sitePost.BlogPostId;
        }
        await SetProfileVisibilityAsync(_authorId, ProfileVisibility.Private);

        SetActiveUser(_strangerId);
        using IServiceScope scope = Factory.Services.CreateScope();
        IBlogPostReadService posts = Resolve<IBlogPostReadService>(scope);

        (await posts.GetByIdAsync(groupPostId)).Should().NotBeNull("a group post is the group's content");
        (await posts.GetByIdAsync(sitePostId)).Should().NotBeNull(
            "a site announcement by a Private moderator stays public");
    }

    // ══ Story acknowledgments + lineage: the by-story reads (WU-AccessGateSweep2) ═════

    private async Task SeedAcceptedAcknowledgmentAsync(int storyId, int acknowledgedUserId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);
        db.StoryAcknowledgments.Add(new StoryAcknowledgment
        {
            StoryId = storyId,
            AcknowledgedUserId = acknowledgedUserId,
            AcknowledgmentRoleId = 1, // seeded lookup row (Beta Reader)
            StatusId = StoryAcknowledgmentStatus.Accepted,
            DateAcknowledged = DateTime.UtcNow,
            DateResponded = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedApprovedLineageAsync(int sourceStoryId, int targetStoryId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = Resolve<ApplicationDbContext>(scope);
        db.StoryLineages.Add(new StoryLineage
        {
            SourceStoryId = sourceStoryId,
            TargetStoryId = targetStoryId,
            RelationshipTypeId = 1, // seeded lookup row
            StatusId = StoryLineageStatus.Approved,
            DateCreated = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private async Task<(int Acknowledgments, int Lineage)> ReadStoryChildrenAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IReadOnlyList<StoryAcknowledgmentDto> credits =
            await Resolve<IStoryAcknowledgmentReadService>(scope).GetAcknowledgmentsForStoryAsync(storyId);
        IReadOnlyList<StoryLineageDto> links =
            await Resolve<IStoryLineageReadService>(scope).GetLineageForStoryAsync(storyId);
        return (credits.Count, links.Count);
    }

    [Fact]
    public async Task AcknowledgmentsAndLineage_DraftStory_EmptyForStranger_PopulatedForAuthor()
    {
        int draftStoryId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);
        int targetStoryId = await SeedStoryAsync(_authorId);
        int helperId = await SeedUserAsync("pv-beta-reader");
        await SeedAcceptedAcknowledgmentAsync(draftStoryId, helperId);
        await SeedApprovedLineageAsync(draftStoryId, targetStoryId);

        SetActiveUser(_strangerId);
        (await ReadStoryChildrenAsync(draftStoryId)).Should().Be((0, 0),
            "credits and links are exactly as visible as the story they hang off");

        SetActiveUser(FakeActiveUserContext.Anonymous());
        (await ReadStoryChildrenAsync(draftStoryId)).Should().Be((0, 0));

        SetActiveUser(_authorId);
        (await ReadStoryChildrenAsync(draftStoryId)).Should().Be((1, 1), "authors keep their drafts");
    }

    [Fact]
    public async Task AcknowledgmentsAndLineage_TakenDownStory_EmptyForEveryone()
    {
        int storyId = await SeedStoryAsync(_authorId);
        int targetStoryId = await SeedStoryAsync(_authorId);
        int helperId = await SeedUserAsync("pv-beta-reader");
        await SeedAcceptedAcknowledgmentAsync(storyId, helperId);
        await SeedApprovedLineageAsync(storyId, targetStoryId);
        await TakeDownStoryAsync(storyId);

        SetActiveUser(_strangerId);
        (await ReadStoryChildrenAsync(storyId)).Should().Be((0, 0));

        SetActiveUser(_authorId);
        (await ReadStoryChildrenAsync(storyId)).Should().Be((0, 0), "takedown outranks authorship");
    }
}
