using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Durable-direct implementation of manual read-marks (WU45). See the interface for the settled
/// semantics (both fields move together; buffer discard; MarkStarted on read). Deliberately NOT
/// part of the Feature-44 signal-buffer pipeline — manual marks are durable intent
/// (layer2-services.md §"Signal Buffering": buffers are for loss-tolerant signals only).
/// </summary>
public class ServerChapterReadMarkWriteService(
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    ReadingProgressBuffer progressBuffer,
    IUserStoryInteractionWriteService usiWrite) : IChapterReadMarkWriteService
{
    public async Task SetChapterReadAsync(int chapterId, bool isRead)
    {
        int userId = activeUser.RequireUserId();

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
            // Mark-unread with no row is a no-op — absent row already means unread (sparse).
            if (!isRead)
            {
                progressBuffer.Discard(userId, chapterId);
                return;
            }
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
            row.IsRead              = isRead;
            row.ReadProgress        = isRead ? 1f : 0f;
            row.LastInteractionDate = DateTime.UtcNow;
        }

        // Drop any in-flight buffered ping BEFORE saving — its high-water merge on the next flush
        // would otherwise resurrect the overridden progress (the whole reason for this seam).
        progressBuffer.Discard(userId, chapterId);
        await writeDb.SaveChangesAsync();

        // "Read it elsewhere" implies reading began; idempotent, never clears other flags.
        // Mark-unread deliberately does NOT touch HasStarted (Has- prefix = permanent past event).
        if (isRead)
        {
            await usiWrite.MarkStartedAsync(chapter.StoryId);

            // A3 (2026-07-24): auto-complete on reaching the final chapter of a Completed story.
            // Gated to Completed stories only — an ongoing story's completion stays un-set (its
            // "caught up" state is the existing query-time computation, layer2-services.md).
            if (chapter.IsLastPublished && chapter.StoryStatusId == StoryStatusEnum.Completed)
                await usiWrite.MarkCompletedAsync(chapter.StoryId);
        }
    }

    public async Task SetAllChaptersReadAsync(int storyId, bool isRead)
    {
        int userId = activeUser.RequireUserId();

        // A3: StoryStatusId gates the MarkCompletedAsync trigger below (fetched alongside the
        // existence check rather than a second round-trip).
        var story = await writeDb.Stories
            .Where(s => s.StoryId == storyId)
            .Select(s => new { s.StoryStatusId })
            .FirstOrDefaultAsync();
        if (story is null) throw new KeyNotFoundException($"Story {storyId} not found.");

        // Published chapters only — drafts are invisible to readers and stay untouched.
        List<int> chapterIds = await writeDb.Chapters
            .Where(c => c.StoryId == storyId && c.IsPublished)
            .Select(c => c.ChapterId)
            .ToListAsync();
        if (chapterIds.Count == 0) return;

        List<UserChapterInteraction> existing = await writeDb.UserChapterInteractions
            .Where(i => i.UserId == userId && chapterIds.Contains(i.ChapterId))
            .ToListAsync();

        DateTime nowUtc = DateTime.UtcNow;
        foreach (UserChapterInteraction row in existing)
        {
            row.IsRead              = isRead;
            row.ReadProgress        = isRead ? 1f : 0f;
            row.LastInteractionDate = nowUtc;
        }

        if (isRead)
        {
            // Create rows for never-touched chapters; for mark-unread absent rows stay absent
            // (already unread — sparse semantics, matches the USI "no row = all false" rule).
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
        }

        progressBuffer.Discard(userId, chapterIds);
        await writeDb.SaveChangesAsync();

        if (isRead)
        {
            await usiWrite.MarkStartedAsync(storyId);

            // A3 (2026-07-24): mark-all covers every published chapter by definition (chapterIds is
            // built from IsPublished above and guarded non-empty), so it always reaches the final
            // chapter — gate to Completed stories only, same as the single-chapter path.
            if (story.StoryStatusId == StoryStatusEnum.Completed)
                await usiWrite.MarkCompletedAsync(storyId);
        }
    }
}
