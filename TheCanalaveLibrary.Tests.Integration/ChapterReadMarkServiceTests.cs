using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IChapterReadMarkWriteService"/> (WU45 — durable manual
/// read-marks). Covers the settled semantics: both fields move together (read → IsRead=true +
/// ReadProgress=1; unread → false + 0); a pending buffered ping is discarded so a later flush
/// cannot resurrect overridden state; mark-read flips the story's HasStarted (idempotent,
/// never un-set by mark-unread); mark-all touches published chapters only and never creates rows
/// for mark-unread (sparse semantics); the enriched <c>GetChapterListAsync</c> (WU45) reflects
/// the state; anonymous callers throw.
///
/// <b>Per-test seeding:</b> user via <c>SeedUserAsync</c>, story via <c>SeedStoryAsync</c>,
/// chapters via the local <c>SeedChapterAsync</c> (direct ApplicationDbContext insert — FK
/// parents: story row from SeedStoryAsync). Respawn resets between tests (testing.md).
/// Tier: Integration (Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class ChapterReadMarkServiceTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _userId;
    private int _storyId;
    private int _chapterId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _userId    = await SeedUserAsync();
        _storyId   = await SeedStoryAsync();
        _chapterId = await SeedChapterAsync(_storyId);
        SetActiveUser(_userId);
    }

    // ── Per-chapter mark ─────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkRead_NoExistingRow_CreatesRowWithBothFieldsSet()
    {
        await SetReadAsync(_chapterId, true);

        UserChapterInteraction? row = await LoadRowAsync(_userId, _chapterId);
        row.Should().NotBeNull();
        row!.IsRead.Should().BeTrue();
        row.ReadProgress.Should().Be(1f, "manual marks set BOTH fields (WU45 settled)");
    }

    [Fact]
    public async Task MarkRead_FlipsHasStarted_OnTheStory()
    {
        await SetReadAsync(_chapterId, true);

        (await LoadHasStartedAsync(_userId, _storyId)).Should().BeTrue(
            "manual mark-read is the 'read it elsewhere' case — reading began");
    }

    [Fact]
    public async Task MarkUnread_ResetsBothFields_ButKeepsHasStarted()
    {
        await SetReadAsync(_chapterId, true);
        await SetReadAsync(_chapterId, false);

        UserChapterInteraction? row = await LoadRowAsync(_userId, _chapterId);
        row.Should().NotBeNull();
        row!.IsRead.Should().BeFalse();
        row.ReadProgress.Should().Be(0f,
            "leaving high-water progress behind would let the next flush re-flip IsRead");

        (await LoadHasStartedAsync(_userId, _storyId)).Should().BeTrue(
            "HasStarted is a permanent past event — mark-unread never clears it");
    }

    [Fact]
    public async Task MarkUnread_NoExistingRow_IsNoOp()
    {
        await SetReadAsync(_chapterId, false);
        (await LoadRowAsync(_userId, _chapterId)).Should().BeNull(
            "absent row already means unread — sparse semantics, no row created");
    }

    [Fact]
    public async Task MarkRead_MissingChapter_ThrowsKeyNotFound()
    {
        Func<Task> act = () => SetReadAsync(999_999, true);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task MarkRead_Anonymous_Throws()
    {
        SetActiveUser(FakeActiveUserContext.Anonymous());
        Func<Task> act = () => SetReadAsync(_chapterId, true);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── The buffer-resurrection guard (the reason this seam exists) ───────────────

    [Fact]
    public async Task MarkUnread_DiscardsPendingBufferedPing_SoFlushCannotResurrect()
    {
        // Reader scrolled to 95% — ping is sitting in the buffer, not yet flushed.
        await RecordProgressAsync(_chapterId, 0.95f);

        // Manual mark-unread must ALSO discard that pending ping…
        await SetReadAsync(_chapterId, false);

        // …because otherwise this flush would high-water-merge 0.95 back in and re-flip IsRead.
        await FlushAsync();

        UserChapterInteraction? row = await LoadRowAsync(_userId, _chapterId);
        row.Should().BeNull("the ping was discarded before it ever landed; mark-unread on a "
            + "rowless chapter stays rowless");
    }

    [Fact]
    public async Task MarkUnread_AfterFlushedRead_StaysUnread_WhenNoNewPings()
    {
        // Progress already durably flushed at 0.95 (IsRead=true).
        await RecordProgressAsync(_chapterId, 0.95f);
        await FlushAsync();

        await SetReadAsync(_chapterId, false);
        await FlushAsync(); // empty flush — nothing pending may resurrect the old state

        UserChapterInteraction? row = await LoadRowAsync(_userId, _chapterId);
        row!.IsRead.Should().BeFalse();
        row.ReadProgress.Should().Be(0f);
    }

    // ── Mark-all ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAllRead_CreatesRowsForPublishedChaptersOnly()
    {
        int ch2      = await SeedChapterAsync(_storyId, chapterNumber: 2);
        int draftCh3 = await SeedChapterAsync(_storyId, chapterNumber: 3, isPublished: false);

        await SetAllReadAsync(_storyId, true);

        (await LoadRowAsync(_userId, _chapterId))!.IsRead.Should().BeTrue();
        (await LoadRowAsync(_userId, ch2))!.IsRead.Should().BeTrue();
        (await LoadRowAsync(_userId, draftCh3)).Should().BeNull(
            "drafts are invisible to readers — mark-all never touches them");
        (await LoadHasStartedAsync(_userId, _storyId)).Should().BeTrue();
    }

    [Fact]
    public async Task MarkAllUnread_FlipsExistingRows_AndCreatesNone()
    {
        int ch2 = await SeedChapterAsync(_storyId, chapterNumber: 2);
        await SetReadAsync(_chapterId, true); // only chapter 1 has a row

        await SetAllReadAsync(_storyId, false);

        UserChapterInteraction? row1 = await LoadRowAsync(_userId, _chapterId);
        row1!.IsRead.Should().BeFalse();
        row1.ReadProgress.Should().Be(0f);
        (await LoadRowAsync(_userId, ch2)).Should().BeNull(
            "mark-unread never creates rows — absent already means unread");
    }

    [Fact]
    public async Task MarkAll_MissingStory_ThrowsKeyNotFound()
    {
        Func<Task> act = () => SetAllReadAsync(999_999, true);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Read-path round-trip (WU45 enriched GetChapterListAsync + watermark) ────────

    [Fact]
    public async Task GetChapterList_ReflectsManualMarks_AndWatermarkIsStamped()
    {
        int ch2 = await SeedChapterAsync(_storyId, chapterNumber: 2);
        await SetReadAsync(_chapterId, true);

        IReadOnlyList<ChapterListEntryDto> list = await GetChapterListAsync(_storyId);
        list.Should().HaveCount(2);
        list[0].IsRead.Should().BeTrue();
        list[0].ReadProgress.Should().Be(1f);
        list[0].ChapterId.Should().Be(_chapterId);
        list[1].IsRead.Should().BeFalse();
        list[1].ReadProgress.Should().Be(0f);

        (await GetWatermarkAsync(_storyId)).Should().NotBeNull(
            "a manual mark stamps LastInteractionDate — the New-badge watermark");
    }

    [Fact]
    public async Task GetChapterList_Anonymous_AllRowsUnread_AndNullWatermark()
    {
        await SetReadAsync(_chapterId, true); // authenticated mark first

        SetActiveUser(FakeActiveUserContext.Anonymous());
        IReadOnlyList<ChapterListEntryDto> list = await GetChapterListAsync(_storyId);
        list[0].IsRead.Should().BeFalse("read state is per-viewer; anonymous has none");
        (await GetWatermarkAsync(_storyId)).Should().BeNull();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task<int> SeedChapterAsync(int storyId, int chapterNumber = 1, bool isPublished = true)
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

    private async Task SetReadAsync(int chapterId, bool isRead)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadMarkWriteService svc =
            scope.ServiceProvider.GetRequiredService<IChapterReadMarkWriteService>();
        await svc.SetChapterReadAsync(chapterId, isRead);
    }

    private async Task SetAllReadAsync(int storyId, bool isRead)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadMarkWriteService svc =
            scope.ServiceProvider.GetRequiredService<IChapterReadMarkWriteService>();
        await svc.SetAllChaptersReadAsync(storyId, isRead);
    }

    private async Task RecordProgressAsync(int chapterId, float progress)
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

    private async Task<bool> LoadHasStartedAsync(int userId, int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        UserStoryInteraction? row = await db.UserStoryInteractions
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId && u.StoryId == storyId);
        return row?.HasStarted ?? false;
    }

    private async Task<IReadOnlyList<ChapterListEntryDto>> GetChapterListAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadService svc = scope.ServiceProvider.GetRequiredService<IChapterReadService>();
        return await svc.GetChapterListAsync(storyId);
    }

    private async Task<DateTime?> GetWatermarkAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterReadService svc = scope.ServiceProvider.GetRequiredService<IChapterReadService>();
        return await svc.GetViewerLastInteractionUtcAsync(storyId);
    }
}
