using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation. Inherits the read path via primary-constructor chaining.
/// Applies the six panel-managed bits in a single upsert: load→apply→stamp dates→sparse cleanup→save.
/// HasStarted is never touched — it belongs to the reading path (WU26).
/// </summary>
public class ServerUserStoryInteractionWriteService(
    ReadOnlyApplicationDbContext readDb,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser)
    : ServerUserStoryInteractionReadService(readDb, activeUser), IUserStoryInteractionWriteService
{
    public async Task SetUserStoryInteractionStateAsync(int storyId, UserStoryInteractionStateUpdate update)
    {
        if (CurrentUserId is not int userId)
            throw new InvalidOperationException("This operation requires an authenticated user.");

        // Reject impossible combinations per spec §4 before touching the database.
        ValidateCombination(update);

        // Load the tracked row + its date partition, or prepare a new row.
        UserStoryInteraction? row = await writeDb.UserStoryInteractions
            .Include(i => i.InteractionDatePartition)
            .FirstOrDefaultAsync(i => i.UserId == userId && i.StoryId == storyId);

        bool isNew = row is null;
        if (isNew)
        {
            // Only create a row when at least one bit will be true after the update.
            if (!AnyBitTrue(update))
                return;

            row = new UserStoryInteraction { UserId = userId, StoryId = storyId };
            writeDb.UserStoryInteractions.Add(row);
        }

        // Apply the six panel bits — HasStarted is intentionally untouched.
        DateTime now = DateTime.UtcNow;
        EnsureDatePartition(row!, now, update);

        row!.IsFavorite = update.IsFavorite;
        row.IsHiddenFavorite = update.IsHiddenFavorite;
        row.IsFollowed = update.IsFollowed;
        row.IsCompleted = update.IsCompleted;
        row.IsReadItLater = update.IsReadItLater;
        row.IsIgnored = update.IsIgnored;

        // Stamp / clear dates on the date partition.
        if (row.InteractionDatePartition is { } d)
        {
            d.FavoriteDate = update.IsFavorite ? (d.FavoriteDate ?? now) : null;
            d.HiddenFavoriteDate = update.IsHiddenFavorite ? (d.HiddenFavoriteDate ?? now) : null;
            d.FollowedDate = update.IsFollowed ? (d.FollowedDate ?? now) : null;
            d.CompletedDate = update.IsCompleted ? (d.CompletedDate ?? now) : null;
            d.ReadItLaterDate = update.IsReadItLater ? (d.ReadItLaterDate ?? now) : null;
            d.IgnoredDate = update.IsIgnored ? (d.IgnoredDate ?? now) : null;
        }

        // Sparse cleanup: if all bits (including the read-only HasStarted) are false, remove the row.
        if (!row.HasStarted && !AnyBitTrue(update))
        {
            writeDb.UserStoryInteractions.Remove(row);
        }

        await writeDb.SaveChangesAsync();
    }

    // ── helpers ─────────────────────────────────────────────────────────────────

    private static void EnsureDatePartition(UserStoryInteraction row, DateTime now, UserStoryInteractionStateUpdate update)
    {
        if (row.InteractionDatePartition is not null) return;
        if (!AnyBitTrue(update)) return;

        row.InteractionDatePartition = new UserStoryInteractionDate { UserId = row.UserId, StoryId = row.StoryId };
    }

    private static bool AnyBitTrue(UserStoryInteractionStateUpdate update) =>
        update.IsFavorite || update.IsHiddenFavorite || update.IsFollowed
        || update.IsCompleted || update.IsReadItLater || update.IsIgnored;

    public async Task MarkStartedAsync(int storyId)
    {
        if (CurrentUserId is not int userId) return;  // anonymous: no-op

        UserStoryInteraction? row = await writeDb.UserStoryInteractions
            .FirstOrDefaultAsync(i => i.UserId == userId && i.StoryId == storyId);

        if (row is null)
        {
            row = new UserStoryInteraction { UserId = userId, StoryId = storyId };
            writeDb.UserStoryInteractions.Add(row);
        }

        row.HasStarted = true;
        await writeDb.SaveChangesAsync();
    }

    private static void ValidateCombination(UserStoryInteractionStateUpdate update)
    {
        // Per spec §4: all 8 (HasStarted × IsCompleted × IsIgnored) combos are valid —
        // including (HasStarted=0, IsCompleted=1), which is the panel's "read elsewhere" use case.
        // No panel-writable combination is currently forbidden; the call site is kept so future
        // restrictions slot in without restructuring.
    }
}
