using System.Threading.RateLimiting;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The service-layer write throttle exercised through the REAL production wiring — every other
/// integration test runs against the pass-through <see cref="FakeWriteRateLimitService"/>
/// (TestAppFactory default); this class re-registers the real
/// <see cref="ServerWriteRateLimitService"/> with a tightened, non-replenishing limits table and
/// proves the L2 write services actually consult it. See security.md §"Write Throttling".
/// </summary>
[Collection("Postgres")]
public sealed class WriteThrottleTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private const int TightLimit = 3;

    private WebApplicationFactory<Program> _throttledFactory = null!;
    private int _posterId;
    private int _wallOwnerId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _posterId = await SeedUserAsync("ThrottlePoster");
        _wallOwnerId = await SeedUserAsync("ThrottleWallOwner");

        _throttledFactory = Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IWriteRateLimitService>();
            services.AddSingleton<IWriteRateLimitService>(new ServerWriteRateLimitService(
                Enum.GetValues<WriteActionKind>().ToDictionary(
                    kind => kind,
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = TightLimit,
                        TokensPerPeriod = 1,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(10),
                        AutoReplenishment = false, // nothing refills mid-test — deterministic counts
                        QueueLimit = 0,
                    })));
        }));
    }

    public override Task DisposeAsync()
    {
        _throttledFactory.Dispose();
        return base.DisposeAsync();
    }

    /// <summary>The derived factory has its own DI container — set ITS fake, not the base one.</summary>
    private void SetThrottledActiveUser(int userId)
    {
        FakeActiveUserContext fake = _throttledFactory.Services.GetRequiredService<FakeActiveUserContext>();
        fake.UserId = userId;
        fake.IsAuthenticated = true;
    }

    [Fact]
    public async Task CommentPosts_ThrottleAtTheBucketLimit_ThroughTheRealServiceWiring()
    {
        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        ICommentWriteService comments = scope.ServiceProvider.GetRequiredService<ICommentWriteService>();

        for (int i = 0; i < TightLimit; i++)
        {
            await comments.PostUserProfileCommentAsync(
                new PostUserProfileCommentDto(_wallOwnerId, null, $"throttle test comment {i}"));
        }

        Func<Task> overLimit = () => comments.PostUserProfileCommentAsync(
            new PostUserProfileCommentDto(_wallOwnerId, null, "one past the bucket"));

        (await overLimit.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(WriteActionKind.Comment);
    }

    [Fact]
    public async Task ImageUploads_ThrottleAtTheBucketLimit_ThroughTheStorageService()
    {
        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        IImageStorageService imageStorage = scope.ServiceProvider.GetRequiredService<IImageStorageService>();
        byte[] png = TestImages.Png(4, 4);

        for (int i = 0; i < TightLimit; i++)
        {
            using MemoryStream content = new(png);
            await imageStorage.SaveAsync(content, "image/png", ImageKind.ProfilePicture, _posterId);
        }

        using MemoryStream overLimitContent = new(png);
        Func<Task> overLimit = () =>
            imageStorage.SaveAsync(overLimitContent, "image/png", ImageKind.ProfilePicture, _posterId);

        (await overLimit.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(WriteActionKind.ImageUpload);
    }

    [Fact]
    public async Task AnExhaustedBucket_NeverAffectsADifferentUser()
    {
        SetThrottledActiveUser(_posterId);
        using (IServiceScope scope = _throttledFactory.Services.CreateScope())
        {
            ICommentWriteService comments = scope.ServiceProvider.GetRequiredService<ICommentWriteService>();
            for (int i = 0; i < TightLimit; i++)
            {
                await comments.PostUserProfileCommentAsync(
                    new PostUserProfileCommentDto(_wallOwnerId, null, $"exhaust {i}"));
            }
        }

        // The wall owner comments back — a different user's bucket, untouched.
        SetThrottledActiveUser(_wallOwnerId);
        using IServiceScope replyScope = _throttledFactory.Services.CreateScope();
        ICommentWriteService replyService = replyScope.ServiceProvider.GetRequiredService<ICommentWriteService>();

        Func<Task> reply = () => replyService.PostUserProfileCommentAsync(
            new PostUserProfileCommentDto(_posterId, null, "different user, fresh bucket"));

        await reply.Should().NotThrowAsync();
    }

    // ── Owner ruling D20's throttle half (WU-ThrottleCoverage, 2026-10-13) ───────────────────────
    // ContentCreate now reaches custom lists (create + clone), series and arcs; the three new kinds
    // (Vote, RepeatRequest, Reveal) each have a real call site. Each test gets a fresh limiter
    // (InitializeAsync builds a new factory), so the counts are natural.

    [Fact]
    public async Task CustomListCreates_ThrottleUnderContentCreate()
    {
        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        ICustomListWriteService lists = scope.ServiceProvider.GetRequiredService<ICustomListWriteService>();

        for (int i = 0; i < TightLimit; i++)
            await lists.CreateListAsync($"Throttle list {i}", isPublic: false);

        Func<Task> overLimit = () => lists.CreateListAsync("One past the bucket", isPublic: false);

        (await overLimit.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(WriteActionKind.ContentCreate);
        (await CountAsync<CustomList>(l => l.UserId == _posterId)).Should().Be(TightLimit);
    }

    [Fact]
    public async Task CustomListClones_SpendTheSameContentCreateBucket()
    {
        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        ICustomListWriteService lists = scope.ServiceProvider.GetRequiredService<ICustomListWriteService>();

        int sourceId = await lists.CreateListAsync("Clone source", isPublic: false);
        await lists.CreateListAsync("Second list", isPublic: false);
        await lists.CloneListAsync(sourceId); // a clone is a list create (D20)

        Func<Task> overLimit = () => lists.CreateListAsync("One past the bucket", isPublic: false);

        (await overLimit.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(WriteActionKind.ContentCreate);
        (await lists.Awaiting(l => l.CloneListAsync(sourceId)).Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(WriteActionKind.ContentCreate);
    }

    [Fact]
    public async Task SeriesCreates_ThrottleUnderContentCreate()
    {
        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        ISeriesWriteService series = scope.ServiceProvider.GetRequiredService<ISeriesWriteService>();

        for (int i = 0; i < TightLimit; i++)
            await series.CreateSeriesAsync(new CreateSeriesDto { Name = $"Throttle series {i}" });

        Func<Task> overLimit = () => series.CreateSeriesAsync(new CreateSeriesDto { Name = "One past the bucket" });

        (await overLimit.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(WriteActionKind.ContentCreate);
        (await CountAsync<Series>(s => s.AuthorId == _posterId)).Should().Be(TightLimit);
    }

    [Fact]
    public async Task StoryArcCreates_ThrottleUnderContentCreate()
    {
        int storyId = await SeedStoryAsync(authorId: _posterId);
        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        IStoryArcWriteService arcs = scope.ServiceProvider.GetRequiredService<IStoryArcWriteService>();

        // Arcs may run past the story's last chapter, so none are seeded.
        for (int n = 1; n <= TightLimit; n++)
            await arcs.CreateArcAsync(new CreateStoryArcDto(storyId, $"Arc {n}", n, n));

        Func<Task> overLimit = () => arcs.CreateArcAsync(
            new CreateStoryArcDto(storyId, "One past the bucket", TightLimit + 1, TightLimit + 1));

        (await overLimit.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(WriteActionKind.ContentCreate);
        (await CountAsync<StoryArc>(a => a.StoryId == storyId)).Should().Be(TightLimit);
    }

    [Fact]
    public async Task AcknowledgmentRevokeRerequestCycle_ThrottlesUnderRepeatRequest()
    {
        // The vector a state guard ("one re-request per decline") would miss: revoke deletes the row, so
        // the next request is a fresh insert that notifies again with no trace of the repeat.
        int storyId = await SeedStoryAsync(authorId: _posterId);
        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        IStoryAcknowledgmentWriteService credits =
            scope.ServiceProvider.GetRequiredService<IStoryAcknowledgmentWriteService>();
        CreateStoryAcknowledgmentDto credit = new()
            { StoryId = storyId, AcknowledgedUserId = _wallOwnerId, AcknowledgmentRoleId = BetaReaderRoleId };

        for (int i = 0; i < TightLimit; i++)
        {
            await credits.RequestAcknowledgmentAsync(credit);
            await credits.RevokeAsync(storyId, _wallOwnerId, BetaReaderRoleId); // never throttled
        }

        Func<Task> overLimit = () => credits.RequestAcknowledgmentAsync(credit);

        (await overLimit.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(WriteActionKind.RepeatRequest);
        (await CountAsync<StoryAcknowledgment>(a => a.StoryId == storyId)).Should().Be(0);
    }

    [Fact]
    public async Task AcknowledgmentRequests_ThatFailValidation_SpendNoToken()
    {
        // The placement exception's side benefit (security.md): the call sits after validation.
        int storyId = await SeedStoryAsync(authorId: _posterId);
        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        IStoryAcknowledgmentWriteService credits =
            scope.ServiceProvider.GetRequiredService<IStoryAcknowledgmentWriteService>();

        for (int i = 0; i < TightLimit + 2; i++)
        {
            Func<Task> selfCredit = () => credits.RequestAcknowledgmentAsync(new CreateStoryAcknowledgmentDto
                { StoryId = storyId, AcknowledgedUserId = _posterId, AcknowledgmentRoleId = BetaReaderRoleId });
            await selfCredit.Should().ThrowAsync<StoryAcknowledgmentValidationException>();
        }

        Func<Task> real = () => credits.RequestAcknowledgmentAsync(new CreateStoryAcknowledgmentDto
            { StoryId = storyId, AcknowledgedUserId = _wallOwnerId, AcknowledgmentRoleId = BetaReaderRoleId });

        await real.Should().NotThrowAsync("refused requests notified nobody, so they spent nothing");
    }

    [Fact]
    public async Task AThrottledReRequest_LeavesNoDirtyRowForTheScopesNextSave()
    {
        // A circuit keeps one write context for many calls. The throttle sits before the Declined row is
        // flipped back to Pending; were it after, the refused flip would stay tracked and the next
        // successful save on the same scope would write it — a re-request the bucket had refused.
        int storyId = await SeedStoryAsync(authorId: _posterId);
        int declinedRecipientId = await SeedUserAsync("ThrottleDecliner");
        int[] otherRecipients =
            [await SeedUserAsync("ThrottleR1"), await SeedUserAsync("ThrottleR2"), await SeedUserAsync("ThrottleR3")];
        using (IServiceScope seed = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.StoryAcknowledgments.Add(new StoryAcknowledgment
            {
                StoryId = storyId, AcknowledgedUserId = declinedRecipientId, AcknowledgmentRoleId = BetaReaderRoleId,
                StatusId = StoryAcknowledgmentStatus.Declined, DateAcknowledged = DateTime.UtcNow,
                DateResponded = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        IStoryAcknowledgmentWriteService credits =
            scope.ServiceProvider.GetRequiredService<IStoryAcknowledgmentWriteService>();
        foreach (int recipientId in otherRecipients)
        {
            await credits.RequestAcknowledgmentAsync(new CreateStoryAcknowledgmentDto
                { StoryId = storyId, AcknowledgedUserId = recipientId, AcknowledgmentRoleId = BetaReaderRoleId });
        }

        Func<Task> refused = () => credits.RequestAcknowledgmentAsync(new CreateStoryAcknowledgmentDto
            { StoryId = storyId, AcknowledgedUserId = declinedRecipientId, AcknowledgmentRoleId = BetaReaderRoleId });
        await refused.Should().ThrowAsync<WriteRateLimitExceededException>();

        // The scope's next save: an unthrottled revoke of one of the other credits.
        await credits.RevokeAsync(storyId, otherRecipients[0], BetaReaderRoleId);

        using IServiceScope check = Factory.Services.CreateScope();
        ApplicationDbContext fresh = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        StoryAcknowledgment declined = await fresh.StoryAcknowledgments.AsNoTracking().SingleAsync(a =>
            a.StoryId == storyId && a.AcknowledgedUserId == declinedRecipientId);
        declined.StatusId.Should().Be(StoryAcknowledgmentStatus.Declined);
    }

    [Fact]
    public async Task LineageRequests_ThrottleOnlyWhenNotifying()
    {
        int[] ownStories = new int[TightLimit + 2];
        for (int i = 0; i < ownStories.Length; i++)
            ownStories[i] = await SeedStoryAsync(authorId: _posterId);
        int othersStory = await SeedStoryAsync(authorId: _wallOwnerId); // InProgress, E — visible to the requester

        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        IStoryLineageWriteService lineage = scope.ServiceProvider.GetRequiredService<IStoryLineageWriteService>();

        // Self-owned links auto-approve and notify nobody: more of them than the bucket holds, all free.
        for (int i = 1; i < ownStories.Length; i++)
        {
            await lineage.RequestLineageAsync(new CreateStoryLineageDto
                { SourceStoryId = ownStories[0], TargetStoryId = ownStories[i], TypeId = InspiredByTypeId });
        }

        // Requests that notify the other author spend the bucket.
        for (int i = 0; i < TightLimit; i++)
        {
            await lineage.RequestLineageAsync(new CreateStoryLineageDto
                { SourceStoryId = ownStories[i], TargetStoryId = othersStory, TypeId = InspiredByTypeId });
        }

        Func<Task> overLimit = () => lineage.RequestLineageAsync(new CreateStoryLineageDto
            { SourceStoryId = ownStories[TightLimit], TargetStoryId = othersStory, TypeId = InspiredByTypeId });

        (await overLimit.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(WriteActionKind.RepeatRequest);
        (await CountAsync<StoryLineage>(l => l.TargetStoryId == othersStory)).Should().Be(TightLimit);
    }

    [Fact]
    public async Task LineageRequests_ThatFailValidation_SpendNoToken()
    {
        // The placement exception (security.md): the call sits after validation, so a refused request
        // (a duplicate of a Pending link, the double-click case, or an unknown type) spends nothing.
        int[] ownStories = new int[TightLimit];
        for (int i = 0; i < ownStories.Length; i++)
            ownStories[i] = await SeedStoryAsync(authorId: _posterId);
        int othersStory = await SeedStoryAsync(authorId: _wallOwnerId);

        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        IStoryLineageWriteService lineage = scope.ServiceProvider.GetRequiredService<IStoryLineageWriteService>();
        CreateStoryLineageDto first = new()
            { SourceStoryId = ownStories[0], TargetStoryId = othersStory, TypeId = InspiredByTypeId };

        await lineage.RequestLineageAsync(first); // the one token spent before the loop

        for (int i = 0; i < TightLimit + 2; i++)
        {
            await lineage.Awaiting(l => l.RequestLineageAsync(first))
                .Should().ThrowAsync<StoryLineageValidationException>("a Pending link of this type already exists");
            await lineage.Awaiting(l => l.RequestLineageAsync(new CreateStoryLineageDto
                    { SourceStoryId = ownStories[1], TargetStoryId = othersStory, TypeId = 999 }))
                .Should().ThrowAsync<StoryLineageValidationException>("no such lineage type");
        }

        // The rest of the bucket is intact: every remaining notifying request lands.
        for (int i = 1; i < TightLimit; i++)
        {
            await lineage.RequestLineageAsync(new CreateStoryLineageDto
                { SourceStoryId = ownStories[i], TargetStoryId = othersStory, TypeId = InspiredByTypeId });
        }

        (await CountAsync<StoryLineage>(l => l.TargetStoryId == othersStory)).Should().Be(TightLimit);
    }

    [Fact]
    public async Task AThrottledLineageReRequest_LeavesNoDirtyRowForTheScopesNextSave()
    {
        // The acknowledgment test's lineage twin. The throttle sits before the Rejected row is flipped
        // back to Pending; were it after, the refused flip would stay tracked on the circuit's write
        // context and the scope's next successful save would write it — a re-request the bucket refused.
        int[] ownStories = new int[TightLimit + 2];
        for (int i = 0; i < ownStories.Length; i++)
            ownStories[i] = await SeedStoryAsync(authorId: _posterId);
        int othersStory = await SeedStoryAsync(authorId: _wallOwnerId);
        using (IServiceScope seed = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.StoryLineages.Add(new StoryLineage
            {
                SourceStoryId = ownStories[0], TargetStoryId = othersStory, RelationshipTypeId = InspiredByTypeId,
                StatusId = StoryLineageStatus.Rejected, DateCreated = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        IStoryLineageWriteService lineage = scope.ServiceProvider.GetRequiredService<IStoryLineageWriteService>();
        for (int i = 1; i <= TightLimit; i++)
        {
            await lineage.RequestLineageAsync(new CreateStoryLineageDto
                { SourceStoryId = ownStories[i], TargetStoryId = othersStory, TypeId = InspiredByTypeId });
        }

        Func<Task> refused = () => lineage.RequestLineageAsync(new CreateStoryLineageDto
            { SourceStoryId = ownStories[0], TargetStoryId = othersStory, TypeId = InspiredByTypeId });
        await refused.Should().ThrowAsync<WriteRateLimitExceededException>();

        // The scope's next save: a self-owned link, which auto-approves and is never charged.
        await lineage.RequestLineageAsync(new CreateStoryLineageDto
            { SourceStoryId = ownStories[0], TargetStoryId = ownStories[TightLimit + 1], TypeId = InspiredByTypeId });

        using IServiceScope check = Factory.Services.CreateScope();
        ApplicationDbContext fresh = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        StoryLineage rejected = await fresh.StoryLineages.AsNoTracking().SingleAsync(l =>
            l.SourceStoryId == ownStories[0] && l.TargetStoryId == othersStory && l.RelationshipTypeId == InspiredByTypeId);
        rejected.StatusId.Should().Be(StoryLineageStatus.Rejected);
    }

    [Fact]
    public async Task LineageRequests_ToAnAuthorlessTarget_SpendNothing()
    {
        // RepeatRequest guards a notification; a target story with no author (its account deleted)
        // has nobody to tell, so the request is free, like a self-owned one.
        int[] ownStories = new int[TightLimit + 2];
        for (int i = 0; i < ownStories.Length; i++)
            ownStories[i] = await SeedStoryAsync(authorId: _posterId);
        int authorlessStory = await SeedStoryAsync(); // InProgress, E, AuthorId null
        int othersStory = await SeedStoryAsync(authorId: _wallOwnerId);

        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        IStoryLineageWriteService lineage = scope.ServiceProvider.GetRequiredService<IStoryLineageWriteService>();

        foreach (int source in ownStories)
        {
            await lineage.RequestLineageAsync(new CreateStoryLineageDto
                { SourceStoryId = source, TargetStoryId = authorlessStory, TypeId = InspiredByTypeId });
        }

        // The whole bucket is still there for requests that do notify.
        for (int i = 0; i < TightLimit; i++)
        {
            await lineage.RequestLineageAsync(new CreateStoryLineageDto
                { SourceStoryId = ownStories[i], TargetStoryId = othersStory, TypeId = InspiredByTypeId });
        }

        Func<Task> overLimit = () => lineage.RequestLineageAsync(new CreateStoryLineageDto
            { SourceStoryId = ownStories[TightLimit], TargetStoryId = othersStory, TypeId = InspiredByTypeId });

        (await overLimit.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(WriteActionKind.RepeatRequest);
        (await CountAsync<StoryLineage>(l => l.TargetStoryId == authorlessStory)).Should().Be(ownStories.Length);
    }

    [Fact]
    public async Task PollVotes_ThrottleUnderVote_RetractionsIncluded()
    {
        int moderatorId = await SeedUserAsync("ThrottleModerator");
        FakeActiveUserContext fake = _throttledFactory.Services.GetRequiredService<FakeActiveUserContext>();
        fake.UserId = moderatorId;
        fake.IsAuthenticated = true;
        fake.IsModerator = true;
        int pollId;
        int[] optionIds;
        using (IServiceScope modScope = _throttledFactory.Services.CreateScope())
        {
            IPollWriteService polls = modScope.ServiceProvider.GetRequiredService<IPollWriteService>();
            pollId = await polls.CreateSitePollAsync(new PollEditDto
            {
                PollName = "Throttle poll",
                AllowMultiple = false,
                ResultsVisibility = PollResultsVisibility.Always,
                AnonymityMode = PollAnonymityMode.Anonymous,
                DateOpened = DateTime.UtcNow.AddMinutes(-5),
                Options = [new PollOptionEditDto(null, "Turtwig"), new PollOptionEditDto(null, "Piplup")],
            });
            ApplicationDbContext db = modScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            optionIds = await db.PollOptions.Where(o => o.PollId == pollId)
                .OrderBy(o => o.PollOptionId).Select(o => o.PollOptionId).ToArrayAsync();
        }

        fake.IsModerator = false;
        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        IPollWriteService voter = scope.ServiceProvider.GetRequiredService<IPollWriteService>();

        await voter.VoteAsync(pollId, [optionIds[0]], voteAnonymously: false); // vote
        await voter.VoteAsync(pollId, [optionIds[1]], voteAnonymously: false); // change
        await voter.VoteAsync(pollId, [], voteAnonymously: false);             // retraction — one method, one token

        Func<Task> overLimit = () => voter.VoteAsync(pollId, [optionIds[0]], voteAnonymously: false);

        (await overLimit.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(WriteActionKind.Vote);
        (await CountAsync<PollVote>(v => v.PollId == pollId)).Should().Be(0);
    }

    [Fact]
    public async Task Reveals_ThrottleUnderReveal_AndSkipUndefinedOrMissingTargets()
    {
        int[] stories = [await SeedStoryAsync(rating: Rating.M), await SeedStoryAsync(rating: Rating.M),
                         await SeedStoryAsync(rating: Rating.M)];
        SetThrottledActiveUser(_posterId); // mature off: the reveal is what admits these
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        ContentRevealRecorder recorder = scope.ServiceProvider.GetRequiredService<ContentRevealRecorder>();

        // An undefined type is dropped before the bucket: more of them than it holds, all free.
        for (int i = 0; i < TightLimit + 2; i++)
            await recorder.RecordAsync((RevealedEntityType)99, stories[0]);

        await recorder.RecordAsync(RevealedEntityType.Story, int.MaxValue); // missing: a token, no row
        await recorder.RecordAsync(RevealedEntityType.Story, stories[0]);
        await recorder.RecordAsync(RevealedEntityType.Story, stories[1]);

        Func<Task> overLimit = () => recorder.RecordAsync(RevealedEntityType.Story, stories[2]);

        (await overLimit.Should().ThrowAsync<WriteRateLimitExceededException>())
            .Which.Kind.Should().Be(WriteActionKind.Reveal);

        using IServiceScope check = Factory.Services.CreateScope();
        ApplicationDbContext db = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<(RevealedEntityType, int)> rows = (await db.UserContentReveals.AsNoTracking()
                .Where(r => r.UserId == _posterId).ToListAsync())
            .Select(r => (r.EntityType, r.EntityId)).ToList();
        rows.Should().BeEquivalentTo(new[] { (RevealedEntityType.Story, stories[0]), (RevealedEntityType.Story, stories[1]) });
    }

    [Fact]
    public async Task Reveals_MapEachTypeToItsOwnTable_AGroupPostIsNoProfilePost()
    {
        // The recorder mirrors GetMyRevealsAsync's type → set mapping: a BlogPost reveal names a PROFILE
        // post (a group post reveals its group), so a BlogPost reveal of a group post's id records nothing.
        int groupId, groupPostId, profilePostId;
        using (IServiceScope seed = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = seed.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Group group = new()
            {
                CreatorId = _wallOwnerId, GroupName = $"Throttle group {Guid.NewGuid():N}", AudienceRating = Rating.M,
                MaxContentRating = Rating.M, DateCreated = DateTime.UtcNow,
            };
            db.Groups.Add(group);
            await db.SaveChangesAsync();
            GroupBlogPost groupPost = new()
            {
                GroupId = group.GroupId, AuthorId = _wallOwnerId, Title = "Group post", Content = "<p>x</p>",
                Rating = Rating.M, IsPublished = true, DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow,
            };
            ProfileBlogPost profilePost = new()
            {
                AuthorId = _wallOwnerId, Title = "Profile post", Content = "<p>x</p>",
                Rating = Rating.M, IsPublished = true, DateCreated = DateTime.UtcNow, LastUpdatedDate = DateTime.UtcNow,
            };
            db.Set<GroupBlogPost>().Add(groupPost);
            db.ProfileBlogPosts.Add(profilePost);
            await db.SaveChangesAsync();
            (groupId, groupPostId, profilePostId) = (group.GroupId, groupPost.BlogPostId, profilePost.BlogPostId);
        }

        SetThrottledActiveUser(_posterId);
        using IServiceScope scope = _throttledFactory.Services.CreateScope();
        ContentRevealRecorder recorder = scope.ServiceProvider.GetRequiredService<ContentRevealRecorder>();
        await recorder.RecordAsync(RevealedEntityType.BlogPost, groupPostId);
        await recorder.RecordAsync(RevealedEntityType.BlogPost, profilePostId);
        await recorder.RecordAsync(RevealedEntityType.Group, groupId);

        using IServiceScope check = Factory.Services.CreateScope();
        ApplicationDbContext fresh = check.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<(RevealedEntityType, int)> rows = (await fresh.UserContentReveals.AsNoTracking()
                .Where(r => r.UserId == _posterId).ToListAsync())
            .Select(r => (r.EntityType, r.EntityId)).ToList();
        rows.Should().BeEquivalentTo(new[] { (RevealedEntityType.BlogPost, profilePostId), (RevealedEntityType.Group, groupId) });
    }

    private const short BetaReaderRoleId = 1;  // HasData-seeded acknowledgment role; survives Respawn
    private const short InspiredByTypeId = 1;  // HasData-seeded lineage type; survives Respawn

    private async Task<int> CountAsync<T>(System.Linq.Expressions.Expression<Func<T, bool>> predicate) where T : class
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Set<T>().CountAsync(predicate);
    }
}
