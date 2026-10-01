using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Durable-direct implementation of manual read-marks (WU45). See the interface for the settled
/// semantics (both fields move together; buffer discard; MarkStarted on read). Deliberately NOT
/// part of the Feature-44 signal-buffer pipeline — manual marks are durable intent
/// (layer2-services.md §"Signal Buffering": buffers are for loss-tolerant signals only).
/// <para>
/// <b>Kind (g) applies to mark-read only (owner ruling D6, WU-AccessGateSweep2).</b> A read-mark is
/// durable intent about content the user actually read, so <i>raising</i> one is refused for a
/// chapter they cannot see: marking a guessed draft chapter wrote a <c>UserChapterInteraction</c>
/// row and cascaded into <c>MarkStartedAsync</c>/<c>MarkCompletedAsync</c> against the hidden
/// story, corrupting the caller's own <c>StoriesRead</c>/<c>StoriesInProgress</c> counters.
/// Mark-unread is a <i>clear</i> of the caller's own rows and is never visibility-guarded, on any
/// axis — nor existence-checked, since <c>writeDb</c> sees hidden rows and "hidden succeeds, absent
/// 404s" would be an oracle. Rule: <c>identity-and-authorization.md</c> §"Parent-visibility
/// guards" → "Raises vs clears".
/// </para>
/// </summary>
public class ServerChapterReadMarkWriteService(
    ApplicationDbContext writeDb,
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser,
    ReadingProgressBuffer progressBuffer,
    IUserStoryInteractionWriteService usiWrite) : IChapterReadMarkWriteService
{
    /// <summary>
    /// The raise guard for a single chapter. <c>writeDb</c> is unfiltered, so the guard needs a
    /// read context.
    /// </summary>
    private async Task RequireChapterVisibleAsync(int chapterId)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        if (!await StoryVisibilityGuard.IsChapterVisibleAsync(readDb, activeUser, chapterId))
            throw new KeyNotFoundException($"Chapter {chapterId} not found.");
    }

    public async Task SetChapterReadAsync(int chapterId, bool isRead)
    {
        int userId = activeUser.RequireUserId();

        // Mark-unread is a clear: no guard, no chapter lookup (D6).
        if (!isRead)
        {
            await ClearChapterAsync(userId, chapterId);
            return;
        }

        await RequireChapterVisibleAsync(chapterId);

        var chapter = await writeDb.Chapters
            .Where(c => c.ChapterId == chapterId)
            .Select(c => new
            {
                c.ChapterId,
                c.StoryId,
                // A3: is this chapter the story's last published chapter? (mirrors ChapterReadingDto's
                // NextChapterNumber-is-null check). Combined with StoryStatusId below to gate the
                // MarkCompletedAsync trigger to Completed stories only.
                IsLastPublished = !c.Story.Chapters.Any(other => other.IsPublished && other.ChapterNumber > c.ChapterNumber),
                c.Story.StoryStatusId
            })
            .FirstOrDefaultAsync();
        if (chapter is null) throw new KeyNotFoundException($"Chapter {chapterId} not found.");

        UserChapterInteraction? row = await writeDb.UserChapterInteractions
            .FirstOrDefaultAsync(i => i.UserId == userId && i.ChapterId == chapterId);

        if (row is null)
        {
            writeDb.UserChapterInteractions.Add(new UserChapterInteraction
            {
                UserId              = userId,
                ChapterId           = chapterId,
                IsRead              = true,
                ReadProgress        = 1f,
                LastInteractionDate = DateTime.UtcNow
            });
        }
        else
        {
            row.IsRead              = true;
            row.ReadProgress        = 1f;
            row.LastInteractionDate = DateTime.UtcNow;
        }

        // Drop any in-flight buffered ping BEFORE saving — its high-water merge on the next flush
        // would otherwise resurrect the overridden progress (the whole reason for this seam).
        progressBuffer.Discard(userId, chapterId);
        await writeDb.SaveChangesAsync();

        // "Read it elsewhere" implies reading began; idempotent, never clears other flags.
        await usiWrite.MarkStartedAsync(chapter.StoryId);

        // A3 (2026-07-24): auto-complete on reaching the final chapter of a Completed story.
        // Gated to Completed stories only — an ongoing story's completion stays un-set (its
        // "caught up" state is the existing query-time computation, layer2-services.md).
        if (chapter.IsLastPublished && chapter.StoryStatusId == StoryStatusEnum.Completed)
            await usiWrite.MarkCompletedAsync(chapter.StoryId);
    }

    public async Task SetAllChaptersReadAsync(int storyId, bool isRead)
    {
        int userId = activeUser.RequireUserId();

        // Mark-all-unread is a clear: no story existence check, no guard (D6).
        if (!isRead)
        {
            await ClearStoryAsync(userId, storyId);
            return;
        }

        // A3: StoryStatusId gates the MarkCompletedAsync trigger below (fetched alongside the
        // existence check rather than a second round-trip).
        var story = await writeDb.Stories
            .Where(s => s.StoryId == storyId)
            .Select(s => new { s.StoryStatusId })
            .FirstOrDefaultAsync();
        if (story is null) throw new KeyNotFoundException($"Story {storyId} not found.");

        // Kind (g). The IsPublished filter below already made an all-draft story a no-op, but a
        // published-chapter story that is itself Draft/PendingApproval/Rejected, taken down, or
        // M-rated-unrevealed was still fully markable.
        await using (ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync())
        {
            if (!await StoryVisibilityGuard.IsStoryVisibleAsync(readDb, activeUser, storyId))
                throw new KeyNotFoundException($"Story {storyId} not found.");
        }

        List<int> chapterIds = await GetPublishedChapterIdsAsync(storyId);
        if (chapterIds.Count == 0) return;

        List<UserChapterInteraction> existing = await LoadRowsAsync(userId, chapterIds);

        DateTime nowUtc = DateTime.UtcNow;
        foreach (UserChapterInteraction row in existing)
        {
            row.IsRead              = true;
            row.ReadProgress        = 1f;
            row.LastInteractionDate = nowUtc;
        }

        // Create rows for never-touched chapters.
        HashSet<int> existingIds = existing.Select(r => r.ChapterId).ToHashSet();
        foreach (int chapterId in chapterIds.Where(id => !existingIds.Contains(id)))
        {
            writeDb.UserChapterInteractions.Add(new UserChapterInteraction
            {
                UserId              = userId,
                ChapterId           = chapterId,
                IsRead              = true,
                ReadProgress        = 1f,
                LastInteractionDate = nowUtc
            });
        }

        progressBuffer.Discard(userId, chapterIds);
        await writeDb.SaveChangesAsync();

        await usiWrite.MarkStartedAsync(storyId);

        // A3 (2026-07-24): mark-all covers every published chapter by definition (chapterIds is
        // built from IsPublished above and guarded non-empty), so it always reaches the final
        // chapter — gate to Completed stories only, same as the single-chapter path.
        if (story.StoryStatusId == StoryStatusEnum.Completed)
            await usiWrite.MarkCompletedAsync(storyId);
    }

    // ── Clears (D6: never visibility-guarded, never existence-checked) ───────────────────────

    /// <summary>
    /// Flips the caller's own row for <paramref name="chapterId"/> to unread. Absent row ⇒ already
    /// unread (sparse) and nothing is written, so a nonexistent, hidden and visible chapter are
    /// indistinguishable. Never touches <c>HasStarted</c>/<c>IsCompleted</c> (permanent past event /
    /// never auto-cleared).
    /// </summary>
    private async Task ClearChapterAsync(int userId, int chapterId)
    {
        UserChapterInteraction? row = await writeDb.UserChapterInteractions
            .FirstOrDefaultAsync(i => i.UserId == userId && i.ChapterId == chapterId);

        if (row is not null)
        {
            row.IsRead              = false;
            row.ReadProgress        = 0f;
            row.LastInteractionDate = DateTime.UtcNow;
        }

        // Discard BEFORE saving — and even with no row: a buffered ping would otherwise land a
        // fresh row on the next flush and resurrect the overridden progress.
        progressBuffer.Discard(userId, chapterId);

        if (row is not null)
            await writeDb.SaveChangesAsync();
    }

    /// <summary>
    /// Flips the caller's existing rows on the story's <b>published</b> chapters to unread
    /// (published-only, WU45 settled; absent rows stay absent — sparse semantics). An absent or
    /// chapterless story is a silent no-op, indistinguishable from a hidden one.
    /// </summary>
    private async Task ClearStoryAsync(int userId, int storyId)
    {
        List<int> chapterIds = await GetPublishedChapterIdsAsync(storyId);
        if (chapterIds.Count == 0) return;

        DateTime nowUtc = DateTime.UtcNow;
        foreach (UserChapterInteraction row in await LoadRowsAsync(userId, chapterIds))
        {
            row.IsRead              = false;
            row.ReadProgress        = 0f;
            row.LastInteractionDate = nowUtc;
        }

        progressBuffer.Discard(userId, chapterIds);
        await writeDb.SaveChangesAsync();
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Published chapters only — drafts are invisible to readers and stay untouched.</summary>
    private Task<List<int>> GetPublishedChapterIdsAsync(int storyId) =>
        writeDb.Chapters
            .Where(c => c.StoryId == storyId && c.IsPublished)
            .Select(c => c.ChapterId)
            .ToListAsync();

    private Task<List<UserChapterInteraction>> LoadRowsAsync(int userId, List<int> chapterIds) =>
        writeDb.UserChapterInteractions
            .Where(i => i.UserId == userId && chapterIds.Contains(i.ChapterId))
            .ToListAsync();
}
