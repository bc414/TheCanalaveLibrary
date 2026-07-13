using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TheCanalaveLibrary.Core;
using TheCanalaveLibrary.Server;

namespace TheCanalaveLibrary.Tests.Integration;

/// <summary>
/// Integration tests for <see cref="IChapterWriteService.MoveChapterAsync"/> and
/// <see cref="IChapterWriteService.DeleteChapterAsync"/> (WU45 — drag-to-reorder + deletion).
/// Covers: contiguous renumbering both directions (the negative-pass discipline against the
/// unique (story_id, chapter_number) index), author gating, target-range validation, arc-bound
/// shifts under the remove+insert composition (grow when a chapter moves in, shrink when one
/// moves out, slide when the change is before the arc, untouched after), empty-arc auto-delete,
/// delete cascades (contents + read state + TPT-safe comment removal incl. base_comments rows),
/// and Story.WordCount refresh after delete.
///
/// <b>Per-test seeding:</b> author via <c>SeedUserAsync</c>, story via <c>SeedStoryAsync</c>,
/// chapters via <c>IChapterWriteService.CreateChapterAsync</c> (the real append-only write path,
/// so PrimaryContentId/contents rows are realistic), arcs via <c>IStoryArcWriteService</c>.
/// Tier: Integration (Testcontainers Postgres via <see cref="PostgresFixture"/>).
/// </summary>
[Collection("Postgres")]
public class ChapterReorderDeleteTests(PostgresFixture postgres) : IntegrationTestBase(postgres)
{
    private int _authorId;
    private int _otherUserId;
    private int _storyId;

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _authorId    = await SeedUserAsync("author");
        _otherUserId = await SeedUserAsync("other");
        _storyId     = await SeedStoryAsync(_authorId);
        SetActiveUser(_authorId);
    }

    // ── Move: renumbering ────────────────────────────────────────────────────────

    [Fact]
    public async Task Move_Later_ShiftsInterveningDown_AndLandsAtTarget()
    {
        await SeedChaptersAsync(5); // titles "Ch A".."Ch E" at numbers 1..5

        await MoveAsync(_storyId, 2, 4); // B: 2 → 4

        (await TitlesInOrderAsync()).Should().ContainInOrder("Ch A", "Ch C", "Ch D", "Ch B", "Ch E");
        await AssertContiguousNumberingAsync(5);
    }

    [Fact]
    public async Task Move_Earlier_ShiftsInterveningUp_AndLandsAtTarget()
    {
        await SeedChaptersAsync(5);

        await MoveAsync(_storyId, 4, 2); // D: 4 → 2

        (await TitlesInOrderAsync()).Should().ContainInOrder("Ch A", "Ch D", "Ch B", "Ch C", "Ch E");
        await AssertContiguousNumberingAsync(5);
    }

    [Fact]
    public async Task Move_ToFirstAndLast_Work()
    {
        await SeedChaptersAsync(4);

        await MoveAsync(_storyId, 3, 1);
        (await TitlesInOrderAsync()).Should().ContainInOrder("Ch C", "Ch A", "Ch B", "Ch D");

        await MoveAsync(_storyId, 1, 4);
        (await TitlesInOrderAsync()).Should().ContainInOrder("Ch A", "Ch B", "Ch D", "Ch C");
        await AssertContiguousNumberingAsync(4);
    }

    [Fact]
    public async Task Move_SamePosition_IsNoOp()
    {
        await SeedChaptersAsync(3);
        await MoveAsync(_storyId, 2, 2);
        (await TitlesInOrderAsync()).Should().ContainInOrder("Ch A", "Ch B", "Ch C");
    }

    [Fact]
    public async Task Move_TargetOutOfRange_ThrowsValidation()
    {
        await SeedChaptersAsync(3);
        Func<Task> act = () => MoveAsync(_storyId, 1, 4);
        await act.Should().ThrowAsync<ChapterValidationException>();
    }

    [Fact]
    public async Task Move_NonAuthor_ThrowsUnauthorized()
    {
        await SeedChaptersAsync(3);
        SetActiveUser(_otherUserId);
        Func<Task> act = () => MoveAsync(_storyId, 1, 2);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ── Move: arc-bound composition (WU45 settled rule) ──────────────────────────

    [Fact]
    public async Task Move_IntoArcSpan_GrowsTheArc()
    {
        await SeedChaptersAsync(8);
        int arcId = await CreateArcAsync(_storyId, "Book 1", 1, 4);

        await MoveAsync(_storyId, 7, 3); // outside chapter dropped inside the arc

        StoryArcDto arc = (await GetArcsAsync(_storyId)).Single(a => a.StoryArcId == arcId);
        (arc.StartChapterNumber, arc.EndChapterNumber).Should().Be((1, 5),
            "a chapter moved into an arc's span joins it — the arc grows by one");
    }

    [Fact]
    public async Task Move_OutOfArc_ShrinksTheArc()
    {
        await SeedChaptersAsync(8);
        int arcId = await CreateArcAsync(_storyId, "Book 1", 1, 4);

        await MoveAsync(_storyId, 2, 8); // arc chapter dragged past the end of the story

        StoryArcDto arc = (await GetArcsAsync(_storyId)).Single(a => a.StoryArcId == arcId);
        (arc.StartChapterNumber, arc.EndChapterNumber).Should().Be((1, 3),
            "a chapter moved out of an arc leaves it — the arc shrinks by one");
    }

    [Fact]
    public async Task Move_WhollyBeforeArc_SlidesTheArc()
    {
        await SeedChaptersAsync(8);
        int arcId = await CreateArcAsync(_storyId, "Book 2", 5, 8);

        await MoveAsync(_storyId, 6, 2); // move happens inside the arc→before-it direction

        StoryArcDto arc = (await GetArcsAsync(_storyId)).Single(a => a.StoryArcId == arcId);
        (arc.StartChapterNumber, arc.EndChapterNumber).Should().Be((6, 8),
            "removing at 6 (inside) then inserting at 2 (before) nets Start+1, End+0");
    }

    [Fact]
    public async Task Move_WithinSameArc_LeavesBoundsUnchanged()
    {
        await SeedChaptersAsync(8);
        int arcId = await CreateArcAsync(_storyId, "Book 1", 2, 6);

        await MoveAsync(_storyId, 3, 5);

        StoryArcDto arc = (await GetArcsAsync(_storyId)).Single(a => a.StoryArcId == arcId);
        (arc.StartChapterNumber, arc.EndChapterNumber).Should().Be((2, 6));
    }

    [Fact]
    public async Task Move_VacatesSingleChapterArc_AutoDeletesIt()
    {
        await SeedChaptersAsync(5);
        await CreateArcAsync(_storyId, "Interlude", 3, 3);

        await MoveAsync(_storyId, 3, 5);

        (await GetArcsAsync(_storyId)).Should().BeEmpty(
            "an arc whose only chapter was moved away is empty (Start > End) — auto-deleted");
    }

    // ── Delete ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RenumbersLaterChaptersDown()
    {
        List<int> chapterIds = await SeedChaptersAsync(4);

        await DeleteAsync(chapterIds[1]); // delete "Ch B" (number 2)

        (await TitlesInOrderAsync()).Should().ContainInOrder("Ch A", "Ch C", "Ch D");
        await AssertContiguousNumberingAsync(3);
    }

    [Fact]
    public async Task Delete_CascadesContentsReadStateAndComments_InclBaseRows()
    {
        List<int> chapterIds = await SeedChaptersAsync(2);
        int chapterId = chapterIds[0];
        await SeedChapterCommentAsync(chapterId, _otherUserId, "Great chapter!");
        await SeedReadRowAsync(_otherUserId, chapterId);

        await DeleteAsync(chapterId);

        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.ChapterContents.AnyAsync(cc => cc.ChapterId == chapterId)).Should().BeFalse();
        (await db.UserChapterInteractions.AnyAsync(i => i.ChapterId == chapterId)).Should().BeFalse();
        (await db.ChapterComments.AnyAsync(cc => cc.ChapterId == chapterId)).Should().BeFalse();
        // The TPT trap this path specifically guards: no orphaned base_comments row survives.
        (await db.BaseComments.AnyAsync(bc => bc.CommentText == "Great chapter!")).Should().BeFalse(
            "deleting via EF must remove the TPT base row, not just the chapter_comments child row");
    }

    [Fact]
    public async Task Delete_ShrinksCoveringArc_AndAutoDeletesEmptiedArc()
    {
        List<int> chapterIds = await SeedChaptersAsync(6);
        int bookOneId = await CreateArcAsync(_storyId, "Book 1", 1, 3);
        await CreateArcAsync(_storyId, "Interlude", 4, 4);
        int bookTwoId = await CreateArcAsync(_storyId, "Book 2", 5, 6);

        await DeleteAsync(chapterIds[3]); // delete number 4 — the Interlude's only chapter

        IReadOnlyList<StoryArcDto> arcs = await GetArcsAsync(_storyId);
        arcs.Should().HaveCount(2, "the emptied single-chapter arc auto-deletes");
        StoryArcDto bookOne = arcs.Single(a => a.StoryArcId == bookOneId);
        (bookOne.StartChapterNumber, bookOne.EndChapterNumber).Should().Be((1, 3),
            "an arc wholly before the deletion is untouched");
        StoryArcDto bookTwo = arcs.Single(a => a.StoryArcId == bookTwoId);
        (bookTwo.StartChapterNumber, bookTwo.EndChapterNumber).Should().Be((4, 5),
            "an arc wholly after the deletion slides down by one");
    }

    [Fact]
    public async Task Delete_RefreshesStoryWordCount()
    {
        await SeedChaptersAsync(2); // each chapter body has a known word count
        int before = await LoadStoryWordCountAsync();

        List<int> ids = await CurrentChapterIdsAsync();
        await DeleteAsync(ids[0]);

        int after = await LoadStoryWordCountAsync();
        after.Should().BeLessThan(before, "the deleted chapter's words leave the story total");
    }

    [Fact]
    public async Task Delete_NonAuthor_ThrowsUnauthorized()
    {
        List<int> ids = await SeedChaptersAsync(1);
        SetActiveUser(_otherUserId);
        Func<Task> act = () => DeleteAsync(ids[0]);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Delete_MissingChapter_ThrowsKeyNotFound()
    {
        Func<Task> act = () => DeleteAsync(999_999);
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Creates <paramref name="count"/> chapters through the real append-only write path,
    /// titled "Ch A", "Ch B", … so order assertions read naturally. Returns ChapterIds in
    /// creation (= chapter number) order.
    /// </summary>
    private async Task<List<int>> SeedChaptersAsync(int count)
    {
        List<int> ids = [];
        for (int i = 0; i < count; i++)
        {
            using IServiceScope scope = Factory.Services.CreateScope();
            IChapterWriteService svc = scope.ServiceProvider.GetRequiredService<IChapterWriteService>();
            ids.Add(await svc.CreateChapterAsync(new CreateChapterDto
            {
                StoryId     = _storyId,
                Title       = $"Ch {(char)('A' + i)}",
                ChapterText = $"<p>Body of chapter {(char)('A' + i)} with several words.</p>"
            }));
        }
        return ids;
    }

    private async Task MoveAsync(int storyId, int from, int to)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterWriteService svc = scope.ServiceProvider.GetRequiredService<IChapterWriteService>();
        await svc.MoveChapterAsync(storyId, from, to);
    }

    private async Task DeleteAsync(int chapterId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IChapterWriteService svc = scope.ServiceProvider.GetRequiredService<IChapterWriteService>();
        await svc.DeleteChapterAsync(chapterId);
    }

    private async Task<int> CreateArcAsync(int storyId, string title, int start, int end)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryArcWriteService svc = scope.ServiceProvider.GetRequiredService<IStoryArcWriteService>();
        return await svc.CreateArcAsync(new CreateStoryArcDto(storyId, title, start, end));
    }

    private async Task<IReadOnlyList<StoryArcDto>> GetArcsAsync(int storyId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        IStoryArcReadService svc = scope.ServiceProvider.GetRequiredService<IStoryArcReadService>();
        return await svc.GetArcsForStoryAsync(storyId);
    }

    private async Task<List<string>> TitlesInOrderAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Chapters.AsNoTracking()
            .Where(c => c.StoryId == _storyId)
            .OrderBy(c => c.ChapterNumber)
            .Select(c => c.Title)
            .ToListAsync();
    }

    private async Task<List<int>> CurrentChapterIdsAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Chapters.AsNoTracking()
            .Where(c => c.StoryId == _storyId)
            .OrderBy(c => c.ChapterNumber)
            .Select(c => c.ChapterId)
            .ToListAsync();
    }

    private async Task AssertContiguousNumberingAsync(int expectedCount)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        List<int> numbers = await db.Chapters.AsNoTracking()
            .Where(c => c.StoryId == _storyId)
            .OrderBy(c => c.ChapterNumber)
            .Select(c => c.ChapterNumber)
            .ToListAsync();
        numbers.Should().Equal(Enumerable.Range(1, expectedCount),
            "renumbering must always leave a contiguous 1..N sequence");
    }

    private async Task SeedChapterCommentAsync(int chapterId, int commenterId, string text)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.ChapterComments.Add(new ChapterComment
        {
            ChapterId   = chapterId,
            UserId      = commenterId,
            CommentText = text,
            DatePosted  = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedReadRowAsync(int userId, int chapterId)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.UserChapterInteractions.Add(new UserChapterInteraction
        {
            UserId              = userId,
            ChapterId           = chapterId,
            IsRead              = true,
            ReadProgress        = 1f,
            LastInteractionDate = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> LoadStoryWordCountAsync()
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        ApplicationDbContext db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Stories.AsNoTracking()
            .Where(s => s.StoryId == _storyId)
            .Select(s => s.WordCount)
            .SingleAsync();
    }
}
