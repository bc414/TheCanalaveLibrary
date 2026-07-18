using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation. Inherits the read path via primary-constructor chaining.
/// Applies the six panel-managed bits in a single upsert: load→apply→stamp dates→sparse cleanup→save.
/// HasStarted is never touched — it belongs to the reading path (WU26).
/// </summary>
public class ServerUserStoryInteractionWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser)
    : ServerUserStoryInteractionReadService(readDbFactory, activeUser), IUserStoryInteractionWriteService
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

        // Capture derived state BEFORE applying the update (transition-delta rule —
        // cross-cutting.md §"Transition-delta rule for UserStoryInteraction-derived counters").
        bool wasFavorite    = row?.IsFavorite  ?? false;
        bool wasCompleted   = row?.IsCompleted ?? false;
        bool wasIgnored     = row?.IsIgnored   ?? false;
        bool hadStarted     = row?.HasStarted  ?? false;
        bool wasInProgress  = hadStarted && !wasCompleted;

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

        // ── Transition-delta UserStats updates ───────────────────────────────────
        // Counter moves only when the effective derived state *flips* (cross-cutting.md
        // §"Transition-delta rule for UserStoryInteraction-derived counters").

        // FavoritesOnStories → story author's stat
        bool willBeFavorite = update.IsFavorite;
        if (willBeFavorite != wasFavorite)
        {
            // Anonymous-type projection so a null AuthorId (authorless story) is not confused with
            // "row not found" (layer2-services.md §"Scalar projections on nullable FK columns").
            var storyRow = await writeDb.Stories
                .Where(s => s.StoryId == storyId)
                .Select(s => new { s.AuthorId })
                .FirstOrDefaultAsync();
            if (storyRow is { AuthorId: int storyAuthorId })
            {
                int delta = willBeFavorite ? 1 : -1;
                await writeDb.UserStats.Where(us => us.UserId == storyAuthorId)
                    .ExecuteUpdateAsync(s => s.SetProperty(us => us.FavoritesOnStories, us => us.FavoritesOnStories + delta));
            }
        }

        // StoriesRead / StoriesInProgress / StoriesIgnored → acting user's stat.
        // HasStarted is unchanged by this method; use captured hadStarted.
        bool willBeCompleted  = update.IsCompleted;
        bool willBeIgnored    = update.IsIgnored;
        bool willBeInProgress = hadStarted && !willBeCompleted;

        if (willBeCompleted != wasCompleted)
        {
            int delta = willBeCompleted ? 1 : -1;
            await writeDb.UserStats.Where(us => us.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(us => us.StoriesRead, us => us.StoriesRead + delta));
        }
        if (willBeInProgress != wasInProgress)
        {
            int delta = willBeInProgress ? 1 : -1;
            await writeDb.UserStats.Where(us => us.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(us => us.StoriesInProgress, us => us.StoriesInProgress + delta));
        }
        if (willBeIgnored != wasIgnored)
        {
            int delta = willBeIgnored ? 1 : -1;
            await writeDb.UserStats.Where(us => us.UserId == userId)
                .ExecuteUpdateAsync(s => s.SetProperty(us => us.StoriesIgnored, us => us.StoriesIgnored + delta));
        }
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
