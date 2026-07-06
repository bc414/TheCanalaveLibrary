using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for the reading-progress signal buffer's flush path (Feature 44 L2):
/// buffered pings are invisible to the DB until <see cref="ReadingProgressFlusher.FlushAsync"/>
/// runs, then land as one batched <c>unnest … ON CONFLICT</c> upsert with high-water progress,
/// latest timestamp, and sticky <c>IsRead</c>. The timer worker is removed by
/// <see cref="TestAppFactory"/>; every flush here is deterministic (testing.md).
/// FK parents per test: user via <see cref="IntegrationTestBase.SeedUserAsync"/>, story via
/// <see cref="IntegrationTestBase.SeedStoryAsync"/>, chapter via <see cref="SeedChapterAsync"/>.
/// Tier: Integration (real Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class ReadingProgressFlushTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _userId;
    private int _storyId;
    private int _chapterId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userId = await SeedUserAsync();
        _storyId = await SeedStoryAsync();
        _chapterId = await SeedChapterAsync(_storyId);
        SetActiveUser(_userId);
    }

    // ── Buffering + flush ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecordProgress_DoesNotTouchTheDb_UntilFlush()
    {
        await RecordAsync(_chapterId, 0.4f);

        (await LoadRowAsync(_userId, _chapterId)).Should().BeNull(
            "pings are buffered in-process; nothing persists before a flush");

        int written = await FlushAsync();
        written.Should().Be(1);

        UserChapterInteraction? row = await LoadRowAsync(_userId, _chapterId);
        row.Should().NotBeNull();
        row!.ReadProgress.Should().Be(0.4f);
        row.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task MultiplePings_CoalesceToOneRow_WithHighWaterProgress()
    {
        await RecordAsync(_chapterId, 0.2f);
        await RecordAsync(_chapterId, 0.7f);
        await RecordAsync(_chapterId, 0.5f);

        int written = await FlushAsync();

        written.Should().Be(1, "three pings for one (user, chapter) coalesce to one entry");
        UserChapterInteraction? row = await LoadRowAsync(_userId, _chapterId);
        row!.ReadProgress.Should().Be(0.7f);
    }

    [Fact]
    public async Task ProgressAt90Percent_SetsIsRead_AndNeverUnsets()
    {
        await RecordAsync(_chapterId, 0.95f);
        await FlushAsync();

        UserChapterInteraction? row = await LoadRowAsync(_userId, _chapterId);
        row!.IsRead.Should().BeTrue();
        row.ReadProgress.Should().Be(0.95f);

        // A later low ping (re-opened the chapter at the top) must not regress either field.
        await RecordAsync(_chapterId, 0.1f);
        await FlushAsync();

        row = await LoadRowAsync(_userId, _chapterId);
        row!.IsRead.Should().BeTrue("IsRead is sticky — never auto-unset");
        row.ReadProgress.Should().Be(0.95f, "cross-flush GREATEST keeps the high-water mark");
    }

    [Fact]
    public async Task ConcurrentReaders_FlushAsOneBatch()
    {
        int secondUserId = await SeedUserAsync();
        int secondChapterId = await SeedChapterAsync(_storyId, chapterNumber: 2);

        await RecordAsync(_chapterId, 0.3f);          // user 1, chapter 1
        SetActiveUser(secondUserId);
        await RecordAsync(_chapterId, 0.6f);          // user 2, chapter 1
        await RecordAsync(secondChapterId, 0.9f);     // user 2, chapter 2

        int written = await FlushAsync();

        written.Should().Be(3, "one flush cycle writes every buffered reader's entry in one batch");
        (await LoadRowAsync(_userId, _chapterId))!.ReadProgress.Should().Be(0.3f);
        (await LoadRowAsync(secondUserId, _chapterId))!.ReadProgress.Should().Be(0.6f);
        (await LoadRowAsync(secondUserId, secondChapterId))!.IsRead.Should().BeTrue();
    }

    [Fact]
    public async Task AnonymousViewer_NoOps()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());

        await RecordAsync(_chapterId, 0.8f);
        int written = await FlushAsync();

        written.Should().Be(0);
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.UserChapterInteractions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task PingForDeletedChapter_IsDropped_WithoutPoisoningTheBatch()
    {
        // Buffer a ping, then delete the chapter before the flush — the EXISTS guard must drop
        // that entry while the healthy entry still lands.
        int doomedChapterId = await SeedChapterAsync(_storyId, chapterNumber: 2);
        await RecordAsync(doomedChapterId, 0.5f);
        await RecordAsync(_chapterId, 0.4f);

        using (IServiceScope scope = Factory.Services.CreateScope())
        {
            ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Chapters.Where(c => c.ChapterId == doomedChapterId).ExecuteDeleteAsync();
        }

        Func<Task> flush = FlushAsync;
        await flush.Should().NotThrowAsync("a stale ping must not FK-fail the whole batch");

        (await LoadRowAsync(_userId, _chapterId)).Should().NotBeNull();
        (await LoadRowAsync(_userId, doomedChapterId)).Should().BeNull();
    }

    // ── Actively Reading recency sort (RecentlyRead) ──────────────────────────────────

    [Fact]
    public async Task RecentlyReadSort_OrdersByLatestChapterPing_NeverPingedLast()
    {
        // Three stories: B pinged most recently, A pinged earlier, C never pinged.
        int storyA = _storyId;
        int storyB = await SeedStoryAsync();
        int storyC = await SeedStoryAsync();
        int chapterB = await SeedChapterAsync(storyB);

        await RecordAsync(_chapterId, 0.5f);   // story A
        await FlushAsync();
        await RecordAsync(chapterB, 0.5f);     // story B — later ping
        await FlushAsync();

        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryReadService stories = scope.ServiceProvider.GetRequiredService<IStoryReadService>();
        (StoryListingDto[] items, int total) = await stories.GetListingsAsync(
            new StoryFilterDto { Sort = DefaultSortOrder.RecentlyRead },
            restrictToStoryIds: [storyA, storyB, storyC]);

        total.Should().Be(3);
        items.Select(i => i.StoryId).Should().Equal(
            storyB, storyA, storyC); // most-recently-read first; never-pinged last (NULLS LAST, R5)
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────

    /// <summary>Bare chapter row — the only FK parent <c>user_chapter_interactions</c> needs.</summary>
    private async Task<int> SeedChapterAsync(int storyId, int chapterNumber = 1)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Chapter chapter = new()
        {
            StoryId = storyId,
            ChapterNumber = chapterNumber,
            Title = $"Chapter {chapterNumber}",
            IsPublished = true
        };
        db.Chapters.Add(chapter);
        await db.SaveChangesAsync();
        return chapter.ChapterId;
    }

    private async Task RecordAsync(int chapterId, float progress)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IReadingProgressWriteService svc =
            scope.ServiceProvider.GetRequiredService<IReadingProgressWriteService>();
        await svc.RecordProgressAsync(chapterId, progress);
    }

    private Task<int> FlushAsync() =>
        Factory.Services.GetRequiredService<ReadingProgressFlusher>().FlushAsync();

    private async Task<UserChapterInteraction?> LoadRowAsync(int userId, int chapterId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.UserChapterInteractions
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId && u.ChapterId == chapterId);
    }
}
