using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// MVP direct-DB reading-progress writer (Feature 44 L2). L7 replaces this with Redis write-behind.
/// </summary>
public class ServerReadingProgressWriteService(
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser) : IReadingProgressWriteService
{
    public async Task RecordProgressAsync(int chapterId, float progress)
    {
        if (activeUser.UserId is not int userId) return;

        UserChapterInteraction? row = await writeDb.UserChapterInteractions
            .FirstOrDefaultAsync(uci => uci.UserId == userId && uci.ChapterId == chapterId);

        if (row is null)
        {
            row = new UserChapterInteraction { UserId = userId, ChapterId = chapterId };
            writeDb.UserChapterInteractions.Add(row);
        }

        row.ReadProgress = progress;
        row.LastInteractionDate = DateTime.UtcNow;
        if (progress >= 0.9f)
            row.IsRead = true;

        await writeDb.SaveChangesAsync();
    }
}
