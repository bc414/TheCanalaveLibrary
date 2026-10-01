using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// The author side of the story lifecycle (WU-StoryLifecycle; owner rulings D1/D2): create always
/// lands a Draft with no publish date, <see cref="IStoryWriteService.TransitionStatusAsync"/> applies
/// the transition table against real Postgres (trust-waiver routing, the conditional-on-status write,
/// the never-re-stamp publish date), and a property save never moves status. The pure table is
/// exhaustively Unit-covered (<c>Tests.Unit/StoryLifecycleTests</c>); the moderator side lives in
/// <c>ModerationServiceTests</c>.
///
/// <para><b>Seeding:</b> one author (<see cref="IntegrationTestBase.SeedUserAsync"/>) plus the
/// Setting/Genre tag pair <c>CanSave</c> requires, per test. Stories come from the real create path
/// (so the Draft/no-date behavior is what's under test) or from <c>SeedStoryAsync</c> for a given
/// starting status. Trust is set by a direct <c>ExecuteUpdate</c> on the seeded user.</para>
/// Tier: Integration (Testcontainers Postgres).
/// </summary>
[Collection("Postgres")]
public class StoryLifecycleTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _settingTagId;
    private int _genreTagId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId = await SeedUserAsync("LifecycleAuthor");

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        string suffix = Guid.NewGuid().ToString("N")[..8];
        Tag setting = new() { TagName = $"Lifecycle Setting {suffix}", TagTypeId = TagTypeEnum.Setting };
        Tag genre = new() { TagName = $"Lifecycle Genre {suffix}", TagTypeId = TagTypeEnum.Genre };
        db.Tags.AddRange(setting, genre);
        await db.SaveChangesAsync();
        (_settingTagId, _genreTagId) = (setting.TagId, genre.TagId);

        SetActiveUser(_authorId);
    }

    // ── Create ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_IsAlwaysDraft_WithNoPublishDate_EvenForAnImportWithAnOriginalDate()
    {
        int storyId = await CreateAsync(StoryStatusEnum.InProgress, originalPublished: new DateOnly(2019, 5, 4));

        Story story = await LoadAsync(storyId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.Draft, "a new story is always a Draft (D1)");
        story.PublishedDate.Should().BeNull("NULL = never published on this site (D2)");
        story.OriginalPublishedDate.Should().Be(new DateOnly(2019, 5, 4),
            "the import's original date is kept as display-only provenance — and never backdates");
    }

    // ── Submit ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Submit_Untrusted_EntersTheQueue_WithSubmittedDate()
    {
        int storyId = await CreateAsync(StoryStatusEnum.InProgress);
        DateTime before = DateTime.UtcNow.AddSeconds(-1);

        StoryStatusEnum result = await TransitionAsync(storyId, StoryStatusEnum.PendingApproval);

        result.Should().Be(StoryStatusEnum.PendingApproval);
        Story story = await LoadAsync(storyId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.PendingApproval);
        story.SubmittedDate.Should().NotBeNull().And.BeOnOrAfter(before);
        story.PublishedDate.Should().BeNull();
    }

    [Fact]
    public async Task Submit_WithoutAStatusWhenPublished_IsRefused()
    {
        int storyId = await CreateAsync(StoryStatusEnum.Draft); // "not chosen yet"

        Func<Task> submit = () => TransitionAsync(storyId, StoryStatusEnum.PendingApproval);

        (await submit.Should().ThrowAsync<StoryValidationException>())
            .Which.Errors.Should().Equal(StoryLifecycle.PostApprovalStatusRequiredMessage);
        (await LoadAsync(storyId)).StoryStatusId.Should().Be(StoryStatusEnum.Draft);
    }

    [Fact]
    public async Task Submit_Trusted_PublishesDirectly_AndLeavesTheTrustRecordAlone()
    {
        await SetTrustAsync(approved: 1, canAutoApprove: true);
        int storyId = await CreateAsync(StoryStatusEnum.OpenBeta);
        DateTime before = DateTime.UtcNow.AddSeconds(-1);

        StoryStatusEnum result = await TransitionAsync(storyId, StoryStatusEnum.PendingApproval);

        result.Should().Be(StoryStatusEnum.OpenBeta, "the trust waiver routes submit to PostApprovalStatus");
        Story story = await LoadAsync(storyId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.OpenBeta);
        story.PublishedDate.Should().NotBeNull().And.BeOnOrAfter(before);
        story.SubmittedDate.Should().BeNull("it never entered the queue");
        (await LoadAuthorAsync()).ApprovedStorySubmissions.Should().Be(1,
            "only a moderator's approval adds to the record — the waiver never does");
    }

    [Fact]
    public async Task Submit_TrustRevoked_EntersTheQueue()
    {
        await SetTrustAsync(approved: 3, canAutoApprove: false);
        int storyId = await CreateAsync(StoryStatusEnum.InProgress);

        (await TransitionAsync(storyId, StoryStatusEnum.PendingApproval)).Should().Be(StoryStatusEnum.PendingApproval);
    }

    // ── Withdraw / revise ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Withdraw_PendingToDraft()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.PendingApproval);

        (await TransitionAsync(storyId, StoryStatusEnum.Draft)).Should().Be(StoryStatusEnum.Draft);
        (await LoadAsync(storyId)).StoryStatusId.Should().Be(StoryStatusEnum.Draft);
    }

    [Fact]
    public async Task Revise_RejectedToDraft()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Rejected);

        (await TransitionAsync(storyId, StoryStatusEnum.Draft)).Should().Be(StoryStatusEnum.Draft);
    }

    // ── Illegal moves ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(StoryStatusEnum.PendingApproval, StoryStatusEnum.Rejected)]   // only a moderator rejects
    [InlineData(StoryStatusEnum.PendingApproval, StoryStatusEnum.InProgress)] // self-approve
    [InlineData(StoryStatusEnum.Draft, StoryStatusEnum.Completed)]            // publish without submit
    [InlineData(StoryStatusEnum.Rejected, StoryStatusEnum.PendingApproval)]   // resubmit without revising
    [InlineData(StoryStatusEnum.InProgress, StoryStatusEnum.Rejected)]
    [InlineData(StoryStatusEnum.Draft, (StoryStatusEnum)99)]                  // undefined enum value
    public async Task IllegalTargets_AreRefused_AndChangeNothing(StoryStatusEnum from, StoryStatusEnum target)
    {
        int storyId = await SeedStoryAsync(_authorId, status: from);

        Func<Task> act = () => TransitionAsync(storyId, target);

        await act.Should().ThrowAsync<StoryValidationException>();
        (await LoadAsync(storyId)).StoryStatusId.Should().Be(from);
    }

    [Fact]
    public async Task NonAuthor_IsForbidden_AndMissingStory_IsNotFound()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);
        SetActiveUser(await SeedUserAsync("Stranger"));

        await this.Invoking(t => t.TransitionAsync(storyId, StoryStatusEnum.PendingApproval))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        await this.Invoking(t => t.TransitionAsync(999_999, StoryStatusEnum.PendingApproval))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Published-set moves, unpublish, republish (D2: never re-stamp) ──────────────

    [Fact]
    public async Task PublishedMoves_UnpublishAndTrustedRepublish_KeepTheOriginalPublishDate()
    {
        await SetTrustAsync(approved: 1, canAutoApprove: true);
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.InProgress);
        DateTime original = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        await UpdateStoryAsync(storyId, u => u.SetProperty(s => s.PublishedDate, original));

        (await TransitionAsync(storyId, StoryStatusEnum.OnHiatus)).Should().Be(StoryStatusEnum.OnHiatus);
        (await LoadAsync(storyId)).PublishedDate.Should().Be(original);

        (await TransitionAsync(storyId, StoryStatusEnum.Draft)).Should().Be(StoryStatusEnum.Draft);
        Story unpublished = await LoadAsync(storyId);
        unpublished.PublishedDate.Should().Be(original,
            "unpublishing keeps the stamp — NULL means never published, which is no longer true");

        // SeedStoryAsync's PostApprovalStatus is InProgress, so the trusted resubmit lands there.
        (await TransitionAsync(storyId, StoryStatusEnum.PendingApproval)).Should().Be(StoryStatusEnum.InProgress);
        (await LoadAsync(storyId)).PublishedDate.Should().Be(original,
            "republication is not a publication event — the story keeps its year-old date (D2)");
    }

    [Fact]
    public async Task Transition_NeverTouchesLastUpdatedDate()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.InProgress);
        DateTime lastUpdated = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        await UpdateStoryAsync(storyId, u => u.SetProperty(s => s.LastUpdatedDate, lastUpdated));

        await TransitionAsync(storyId, StoryStatusEnum.Completed);
        await TransitionAsync(storyId, StoryStatusEnum.Draft);

        (await LoadAsync(storyId)).LastUpdatedDate.Should().Be(lastUpdated,
            "a status move is not a content update — bumping it would reopen the republish bump vector");
    }

    [Fact]
    public async Task SameStatus_IsANoOp()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.PendingApproval);
        DateTime submitted = (await LoadAsync(storyId)).SubmittedDate!.Value;

        (await TransitionAsync(storyId, StoryStatusEnum.PendingApproval)).Should().Be(StoryStatusEnum.PendingApproval);
        (await LoadAsync(storyId)).SubmittedDate.Should().Be(submitted, "a no-op writes nothing");
    }

    // ── Property saves never move status ──────────────────────────────────────────

    [Fact]
    public async Task UpdateStoryAsync_OnAPendingStory_StaysPending_AndIgnoresAForgedStatusEcho()
    {
        int storyId = await CreateAsync(StoryStatusEnum.InProgress);
        await TransitionAsync(storyId, StoryStatusEnum.PendingApproval);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            IStoryWriteService write = scope.ServiceProvider.GetRequiredService<IStoryWriteService>();
            IStoryReadService reads = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
            StoryUpdateDTO dto = (await reads.GetStoryForEditAsync(storyId))!;
            dto.Title = "Edited while queued";
            dto.StoryStatusId = StoryStatusEnum.Completed; // forged — the echo must be ignored
            await write.UpdateStoryAsync(dto);
        }

        Story story = await LoadAsync(storyId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.PendingApproval,
            "an in-queue edit is allowed and does not re-queue or publish (D1 sub-edge)");
    }

    [Fact]
    public async Task GetStoryForEditAsync_EchoesStatus_AndTheRejectionReasonOnlyWhileRejected()
    {
        int rejectedId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Rejected);
        await UpdateStoryAsync(rejectedId, u => u.SetProperty(s => s.TakedownReason, "Needs a summary."));
        int draftId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);
        await UpdateStoryAsync(draftId, u => u.SetProperty(s => s.TakedownReason, "stale reason"));

        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryReadService reads = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        StoryUpdateDTO rejected = (await reads.GetStoryForEditAsync(rejectedId))!;
        StoryUpdateDTO draft = (await reads.GetStoryForEditAsync(draftId))!;

        rejected.StoryStatusId.Should().Be(StoryStatusEnum.Rejected);
        rejected.RejectionReason.Should().Be("Needs a summary.");
        draft.RejectionReason.Should().BeNull("the reason is only meaningful while the story is Rejected");
    }

    // ── Takedown freezes status (D1's non-overlap, enforced — review fixes 2026-09-30) ──

    [Theory]
    [InlineData(StoryStatusEnum.InProgress, StoryStatusEnum.Draft)]           // unpublish
    [InlineData(StoryStatusEnum.InProgress, StoryStatusEnum.Completed)]       // published move
    [InlineData(StoryStatusEnum.Draft, StoryStatusEnum.PendingApproval)]      // submit (untrusted → queue)
    [InlineData(StoryStatusEnum.PendingApproval, StoryStatusEnum.Draft)]      // withdraw
    public async Task TakenDownStory_RefusesEveryAuthorMove_AndChangesNothing(
        StoryStatusEnum from, StoryStatusEnum target)
    {
        // Without the freeze, the first row is step one of "unpublish → resubmit → in front of a
        // moderator", where approve would count toward trust and reject would overwrite the
        // takedown's own reason — the overlap D1 confines rejection to pre-publication to avoid.
        int storyId = await SeedStoryAsync(_authorId, status: from);
        await UpdateStoryAsync(storyId, u => u
            .SetProperty(s => s.IsTakenDown, true)
            .SetProperty(s => s.TakedownReason, "Removed by a moderator."));

        Func<Task> act = () => TransitionAsync(storyId, target);

        (await act.Should().ThrowAsync<StoryValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.Contains("taken down"));
        Story story = await LoadAsync(storyId);
        story.StoryStatusId.Should().Be(from);
        story.TakedownReason.Should().Be("Removed by a moderator.");
    }

    [Fact]
    public async Task TakenDownStory_TrustedSubmit_IsAlsoRefused()
    {
        // The waiver route never touches the queue, but the freeze is about the status, not the
        // queue: reversing a takedown must restore the story exactly as it was taken down.
        await SetTrustAsync(approved: 1, canAutoApprove: true);
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.Draft);
        await UpdateStoryAsync(storyId, u => u.SetProperty(s => s.IsTakenDown, true));

        (await this.Invoking(t => t.TransitionAsync(storyId, StoryStatusEnum.PendingApproval))
                .Should().ThrowAsync<StoryValidationException>())
            .Which.Errors.Should().ContainSingle(e => e.Contains("taken down"));
        Story story = await LoadAsync(storyId);
        story.StoryStatusId.Should().Be(StoryStatusEnum.Draft);
        story.PublishedDate.Should().BeNull();
    }

    // ── The conditional write itself (not the pre-read check) ───────────────────────

    [Fact]
    public async Task Transition_StatusChangesBetweenReadAndWrite_Throws_AndOverwritesNothing()
    {
        // The author withdraws a pending story while a moderator approves it: the approval lands
        // strictly between TransitionStatusAsync's read and its conditional update. A sequential
        // test can't reach this path — the pre-read would already see the new status.
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.PendingApproval);
        InterleavingCommandInterceptor moderatorApproves = new(ConnectionString, "UPDATE stories",
            $"UPDATE stories SET story_status_id = {(int)StoryStatusEnum.InProgress}, published_date = now() " +
            $"WHERE story_id = {storyId}");

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            (ServerStoryWriteService write, ApplicationDbContext writeDb) =
                InterleavingCommandInterceptor.CreateService<ServerStoryWriteService>(scope, ConnectionString, moderatorApproves);
            await using (writeDb)
            {
                (await write.Invoking(w => w.TransitionStatusAsync(storyId, StoryStatusEnum.Draft))
                        .Should().ThrowAsync<StoryValidationException>())
                    .Which.Errors.Should().ContainSingle(e => e.Contains("just changed"));
            }
        }

        moderatorApproves.Fired.Should().BeTrue("the competing write must land between the read and the write");
        (await LoadAsync(storyId)).StoryStatusId.Should().Be(StoryStatusEnum.InProgress,
            "the guarded WHERE affects 0 rows instead of overwriting the moderator's approval with Draft");
    }

    [Fact]
    public async Task Transition_TakedownLandsBetweenReadAndWrite_Throws_AndOverwritesNothing()
    {
        int storyId = await SeedStoryAsync(_authorId, status: StoryStatusEnum.InProgress);
        InterleavingCommandInterceptor moderatorTakesDown = new(ConnectionString, "UPDATE stories",
            $"UPDATE stories SET is_taken_down = true WHERE story_id = {storyId}");

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            (ServerStoryWriteService write, ApplicationDbContext writeDb) =
                InterleavingCommandInterceptor.CreateService<ServerStoryWriteService>(scope, ConnectionString, moderatorTakesDown);
            await using (writeDb)
            {
                await write.Invoking(w => w.TransitionStatusAsync(storyId, StoryStatusEnum.Draft))
                    .Should().ThrowAsync<StoryValidationException>();
            }
        }

        moderatorTakesDown.Fired.Should().BeTrue();
        (await LoadAsync(storyId)).StoryStatusId.Should().Be(StoryStatusEnum.InProgress,
            "!IsTakenDown rides the conditional WHERE, so the frozen status survives the race");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private async Task<int> CreateAsync(StoryStatusEnum postApprovalStatus, DateOnly? originalPublished = null)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryWriteService write = scope.ServiceProvider.GetRequiredService<IStoryWriteService>();
        return await write.CreateStoryAsync(new CreateStoryDTO
        {
            Title = $"Lifecycle Story {Guid.NewGuid():N}",
            ShortDescription = "test",
            LongDescription = "test long description",
            Rating = Rating.E,
            PostApprovalStatus = postApprovalStatus,
            OriginalPublishedDate = originalPublished,
            StoryTags =
            [
                new StoryTagDTO { TagId = _settingTagId, TagTypeEnum = TagTypeEnum.Setting, Priority = TagPriority.Primary },
                new StoryTagDTO { TagId = _genreTagId, TagTypeEnum = TagTypeEnum.Genre, Priority = TagPriority.Primary }
            ]
        });
    }

    private async Task<StoryStatusEnum> TransitionAsync(int storyId, StoryStatusEnum target)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryWriteService write = scope.ServiceProvider.GetRequiredService<IStoryWriteService>();
        return await write.TransitionStatusAsync(storyId, target);
    }

    private async Task<Story> LoadAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Stories.SingleAsync(s => s.StoryId == storyId);
    }

    private async Task<User> LoadAuthorAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.SingleAsync(u => u.Id == _authorId);
    }

    private async Task SetTrustAsync(int approved, bool canAutoApprove)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Users.Where(u => u.Id == _authorId).ExecuteUpdateAsync(u => u
            .SetProperty(x => x.ApprovedStorySubmissions, approved)
            .SetProperty(x => x.CanAutoApprove, canAutoApprove));
    }

    private async Task UpdateStoryAsync(int storyId, Action<UpdateSettersBuilder<Story>> setters)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Stories.Where(s => s.StoryId == storyId).ExecuteUpdateAsync(setters);
    }
}
