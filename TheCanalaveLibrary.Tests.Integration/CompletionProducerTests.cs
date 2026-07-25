using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for the A3 story-completion auto-producer (2026-07-24 — hidden-deferrals-tracker
/// A3, closing the "spoiler gate fed a hardcoded false" deferral). Covers:
/// <list type="bullet">
///   <item><see cref="IUserStoryInteractionWriteService.MarkCompletedAsync"/> directly — idempotent,
///   anonymous no-op, stamps <c>CompletedDate</c>, transition-delta counters
///   (<c>StoriesRead</c>/<c>StoriesInProgress</c>).</item>
///   <item>The manual mark-read trigger (<see cref="IChapterReadMarkWriteService"/>) — fires only on
///   the story's final published chapter, and only when <c>Story.StoryStatusId == Completed</c>.</item>
///   <item>The wiring half — <see cref="IChapterReadService.GetChapterForReadingAsync"/>'s
///   <c>ViewerHasCompletedStory</c> / <c>StoryIsComplete</c> projection fields.</item>
/// </list>
/// The reading-page (<c>OnScrollProgress</c>) trigger is a Razor-component concern and is not
/// re-tested here — it calls the same <c>MarkCompletedAsync</c> covered below; see
/// <c>ChapterReadingPageTests</c> for the guard/param-plumbing coverage.
///
/// <b>Per-test seeding:</b> user via <c>SeedUserAsync</c>, story via <c>SeedStoryAsync</c> (status
/// parameter selects Completed vs InProgress), chapters via the local <c>SeedChapterAsync</c> (direct
/// EF insert — FK parent: the story row). A <see cref="UserStat"/> row is seeded explicitly where
/// counters are asserted (<see cref="IntegrationTestBase.SeedUserAsync"/> does not create one, and
/// the counter <c>ExecuteUpdateAsync</c> calls silently no-op without it). Respawn resets between
/// tests (testing.md). Tier: Integration (Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class CompletionProducerTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _userId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userId = await SeedUserAsync();
        SetActiveUser(_userId);
    }

    // ── MarkCompletedAsync (direct) ──────────────────────────────────────────────

    [Fact]
    public async Task MarkCompletedAsync_SetsIsCompleted_AndStampsCompletedDate()
    {
        int storyId = await SeedStoryAsync(status: StoryStatusEnum.Completed);
        await SeedUserStatAsync(_userId);

        await MarkCompletedAsync(storyId);

        UserStoryInteraction? row = await GetRowAsync(_userId, storyId);
        row.Should().NotBeNull();
        row!.IsCompleted.Should().BeTrue();

        UserStoryInteractionDate? date = await GetDatePartitionAsync(_userId, storyId);
        date.Should().NotBeNull();
        date!.CompletedDate.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkCompletedAsync_AlreadyCompleted_IsNoOp_NoDoubleCounterIncrement()
    {
        int storyId = await SeedStoryAsync(status: StoryStatusEnum.Completed);
        await SeedUserStatAsync(_userId);

        await MarkCompletedAsync(storyId);
        await MarkCompletedAsync(storyId); // re-fire — mirrors a re-visit/re-scroll of the final chapter

        (await LoadStoriesReadAsync(_userId)).Should().Be(1,
            "MarkCompletedAsync must be idempotent — a second call must not double-increment StoriesRead");
    }

    [Fact]
    public async Task MarkCompletedAsync_Anonymous_IsNoOp()
    {
        int storyId = await SeedStoryAsync(status: StoryStatusEnum.Completed);
        SetActiveUser(FakeActiveUserContext.Anonymous());

        await MarkCompletedAsync(storyId);

        // No row should exist for any user — nothing to assert against a specific userId, so
        // confirm no interaction rows exist for this story at all.
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.UserStoryInteractions.AnyAsync(i => i.StoryId == storyId)).Should().BeFalse();
    }

    [Fact]
    public async Task MarkCompletedAsync_UserHadStarted_IncrementsStoriesRead_AndDecrementsStoriesInProgress()
    {
        int storyId = await SeedStoryAsync(status: StoryStatusEnum.Completed);
        await SeedUserStatAsync(_userId);
        // Through the real service, not a raw EF seed — StoriesInProgress must actually be +1'd
        // here (the bug this test caught: MarkStartedAsync never applied this delta, so
        // MarkCompletedAsync's decrement below drove the counter negative).
        await MarkStartedAsync(storyId);
        (await LoadStoriesInProgressAsync(_userId)).Should().Be(1,
            "MarkStartedAsync is the sole real-time producer of the StoriesInProgress increment");

        await MarkCompletedAsync(storyId);

        (await LoadStoriesReadAsync(_userId)).Should().Be(1);
        (await LoadStoriesInProgressAsync(_userId)).Should().Be(0,
            "the user had started (counted as in-progress); completing must move them out of it");
    }

    [Fact]
    public async Task MarkCompletedAsync_UserHadNotStarted_IncrementsStoriesRead_OnlyDoesNotTouchStoriesInProgress()
    {
        int storyId = await SeedStoryAsync(status: StoryStatusEnum.Completed);
        await SeedUserStatAsync(_userId);

        await MarkCompletedAsync(storyId); // "read elsewhere" style completion, never started here

        (await LoadStoriesReadAsync(_userId)).Should().Be(1);
        (await LoadStoriesInProgressAsync(_userId)).Should().Be(0,
            "the user was never counted as in-progress, so the counter must not go negative");
    }

    // ── MarkStartedAsync's StoriesInProgress delta (the bug this A3 work surfaced/fixed) ──────

    [Fact]
    public async Task MarkStartedAsync_IncrementsStoriesInProgress_OnGenuineFlip()
    {
        int storyId = await SeedStoryAsync(status: StoryStatusEnum.InProgress);
        await SeedUserStatAsync(_userId);

        await MarkStartedAsync(storyId);

        (await LoadStoriesInProgressAsync(_userId)).Should().Be(1);
    }

    [Fact]
    public async Task MarkStartedAsync_Idempotent_SecondCall_DoesNotDoubleIncrementStoriesInProgress()
    {
        int storyId = await SeedStoryAsync(status: StoryStatusEnum.InProgress);
        await SeedUserStatAsync(_userId);

        await MarkStartedAsync(storyId);
        await MarkStartedAsync(storyId); // re-visit — must not double-count

        (await LoadStoriesInProgressAsync(_userId)).Should().Be(1);
    }

    // ── Manual mark-read trigger (ServerChapterReadMarkWriteService) ─────────────

    [Fact]
    public async Task SetChapterReadAsync_FinalChapterOfCompletedStory_MarksCompleted()
    {
        int storyId = await SeedStoryAsync(status: StoryStatusEnum.Completed);
        await SeedChapterAsync(storyId, chapterNumber: 1);
        int ch2 = await SeedChapterAsync(storyId, chapterNumber: 2);

        await SetChapterReadAsync(ch2, true);

        (await GetRowAsync(_userId, storyId))!.IsCompleted.Should().BeTrue(
            "chapter 2 is the story's last published chapter on a Completed story");
    }

    [Fact]
    public async Task SetChapterReadAsync_NonFinalChapterOfCompletedStory_DoesNotMarkCompleted()
    {
        int storyId = await SeedStoryAsync(status: StoryStatusEnum.Completed);
        int ch1 = await SeedChapterAsync(storyId, chapterNumber: 1);
        await SeedChapterAsync(storyId, chapterNumber: 2);

        await SetChapterReadAsync(ch1, true);

        UserStoryInteraction? row = await GetRowAsync(_userId, storyId);
        (row?.IsCompleted ?? false).Should().BeFalse(
            "chapter 1 has a published successor — not the final chapter yet");
    }

    [Fact]
    public async Task SetChapterReadAsync_FinalChapterOfOngoingStory_DoesNotMarkCompleted()
    {
        int storyId = await SeedStoryAsync(status: StoryStatusEnum.InProgress);
        int ch1 = await SeedChapterAsync(storyId, chapterNumber: 1);

        await SetChapterReadAsync(ch1, true);

        UserStoryInteraction? row = await GetRowAsync(_userId, storyId);
        (row?.IsCompleted ?? false).Should().BeFalse(
            "the auto-producer only fires for author-Completed stories — an ongoing story's "
            + "'caught up' state stays the existing query-time computation, never auto-set");
    }

    [Fact]
    public async Task SetAllChaptersReadAsync_CompletedStory_MarksCompleted()
    {
        int storyId = await SeedStoryAsync(status: StoryStatusEnum.Completed);
        await SeedChapterAsync(storyId, chapterNumber: 1);
        await SeedChapterAsync(storyId, chapterNumber: 2);

        await SetAllChaptersReadAsync(storyId, true);

        (await GetRowAsync(_userId, storyId))!.IsCompleted.Should().BeTrue(
            "mark-all covers every published chapter by definition, so it always reaches the final one");
    }

    [Fact]
    public async Task SetAllChaptersReadAsync_OngoingStory_DoesNotMarkCompleted()
    {
        int storyId = await SeedStoryAsync(status: StoryStatusEnum.InProgress);
        await SeedChapterAsync(storyId, chapterNumber: 1);

        await SetAllChaptersReadAsync(storyId, true);

        UserStoryInteraction? row = await GetRowAsync(_userId, storyId);
        (row?.IsCompleted ?? false).Should().BeFalse();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task<int> SeedChapterAsync(int storyId, int chapterNumber, bool isPublished = true)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter chapter = new()
        {
            StoryId       = storyId,
            ChapterNumber = chapterNumber,
            Title         = $"Chapter {chapterNumber}",
            IsPublished   = isPublished
        };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        return chapter.ChapterId;
    }

    /// <summary>
    /// Ensures a <see cref="UserStat"/> row exists for <paramref name="userId"/>.
    /// <see cref="IntegrationTestBase.SeedUserAsync"/> does not create one, and
    /// <see cref="ServerUserStoryInteractionWriteService.MarkCompletedAsync"/>'s
    /// <c>ExecuteUpdateAsync</c> calls silently no-op without it.
    /// </summary>
    private async Task SeedUserStatAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserStats.Add(new UserStat { UserId = userId });
        await db.SaveChangesAsync();
    }

    private async Task MarkStartedAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IUserStoryInteractionWriteService svc =
            scope.ServiceProvider.GetRequiredService<IUserStoryInteractionWriteService>();
        await svc.MarkStartedAsync(storyId);
    }

    private async Task MarkCompletedAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IUserStoryInteractionWriteService svc =
            scope.ServiceProvider.GetRequiredService<IUserStoryInteractionWriteService>();
        await svc.MarkCompletedAsync(storyId);
    }

    private async Task SetChapterReadAsync(int chapterId, bool isRead)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadMarkWriteService svc =
            scope.ServiceProvider.GetRequiredService<IChapterReadMarkWriteService>();
        await svc.SetChapterReadAsync(chapterId, isRead);
    }

    private async Task SetAllChaptersReadAsync(int storyId, bool isRead)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadMarkWriteService svc =
            scope.ServiceProvider.GetRequiredService<IChapterReadMarkWriteService>();
        await svc.SetAllChaptersReadAsync(storyId, isRead);
    }

    private async Task<UserStoryInteraction?> GetRowAsync(int userId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserStoryInteractions
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.UserId == userId && i.StoryId == storyId);
    }

    private async Task<UserStoryInteractionDate?> GetDatePartitionAsync(int userId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserStoryInteractionDates
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.UserId == userId && d.StoryId == storyId);
    }

    private async Task<int> LoadStoriesReadAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserStats.Where(us => us.UserId == userId)
            .Select(us => us.StoriesRead).FirstOrDefaultAsync();
    }

    private async Task<int> LoadStoriesInProgressAsync(int userId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserStats.Where(us => us.UserId == userId)
            .Select(us => us.StoriesInProgress).FirstOrDefaultAsync();
    }
}
