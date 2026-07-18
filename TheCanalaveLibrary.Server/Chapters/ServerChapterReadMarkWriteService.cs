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
            .Select(c => new { c.ChapterId, c.StoryId })
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
            await usiWrite.MarkStartedAsync(chapter.StoryId);
    }

    public async Task SetAllChaptersReadAsync(int storyId, bool isRead)
    {
        int userId = activeUser.RequireUserId();

        bool storyExists = await writeDb.Stories.AnyAsync(s => s.StoryId == storyId);
        if (!storyExists) throw new KeyNotFoundException($"Story {storyId} not found.");

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
            await usiWrite.MarkStartedAsync(storyId);
    }
}
