using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation. Projects UserStoryInteraction rows into DTOs using the
/// no-tracking ReadOnlyApplicationDbContext. Anonymous viewers receive empty/all-false results
/// without hitting the database.
/// </summary>
public class ServerUserStoryInteractionReadService(
    ReadOnlyApplicationDbContext readDb,
    IActiveUserContext activeUser) : IUserStoryInteractionReadService
{
    /// <summary>
    /// Exposed so the derived write service can access the viewer id without re-capturing the
    /// activeUser primary constructor parameter (avoids CS9107 double-capture warning).
    /// </summary>
    protected int? CurrentUserId => activeUser.UserId;

    public async Task<UserStoryInteractionStateDto> GetStateAsync(int storyId)
    {
        int? userId = activeUser.UserId;
        if (userId is null)
            return UserStoryInteractionStateDto.AllFalse(storyId);

        UserStoryInteraction? row = await readDb.UserStoryInteractions
            .FirstOrDefaultAsync(i => i.UserId == userId && i.StoryId == storyId);

        return row is null
            ? UserStoryInteractionStateDto.AllFalse(storyId)
            : ToDto(row);
    }

    public async Task<IReadOnlyDictionary<int, UserStoryInteractionStateDto>> GetStatesByStoryIdsAsync(
        IReadOnlyList<int> storyIds)
    {
        int? userId = activeUser.UserId;
        if (userId is null || storyIds.Count == 0)
            return new Dictionary<int, UserStoryInteractionStateDto>();

        List<UserStoryInteraction> rows = await readDb.UserStoryInteractions
            .Where(i => i.UserId == userId && storyIds.Contains(i.StoryId))
            .ToListAsync();

        return rows.ToDictionary(r => r.StoryId, ToDto);
    }

    public async Task<IReadOnlyList<int>> GetBookshelfStoryIdsAsync(BookshelfTab tab)
    {
        int? userId = activeUser.UserId;
        if (userId is null) return [];

        IQueryable<UserStoryInteraction> query = readDb.UserStoryInteractions
            .Where(i => i.UserId == userId);

        query = tab switch
        {
            BookshelfTab.Favorites => query.Where(i => i.IsFavorite),
            BookshelfTab.PrivateFavorites => query.Where(i => i.IsHiddenFavorite),
            BookshelfTab.Completed => query.Where(i => i.IsCompleted),
            BookshelfTab.Following => query.Where(i => i.IsFollowed),
            BookshelfTab.ReadItLater => query.Where(i => i.IsReadItLater),
            BookshelfTab.Ignored => query.Where(i => i.IsIgnored),
            BookshelfTab.ActivelyReading => query.Where(i => i.HasStarted && !i.IsCompleted && !i.IsIgnored),
            BookshelfTab.Abandoned => query.Where(i => i.IsIgnored && i.HasStarted),
            _ => throw new ArgumentOutOfRangeException(nameof(tab), tab,
                "Tab is not backed by UserStoryInteraction; route to the appropriate service.")
        };

        return await query.Select(i => i.StoryId).ToListAsync();
    }

    public async Task<IReadOnlyList<int>> GetFavoriteStoryIdsAsync(int userId, bool includePrivate)
    {
        // includePrivate = true when the owner views their own profile; false for visitors.
        // Hidden favorites (IsHiddenFavorite) are visible only to the owner.
        return await readDb.UserStoryInteractions
            .Where(i => i.UserId == userId
                        && i.IsFavorite
                        && (includePrivate || !i.IsHiddenFavorite))
            .Select(i => i.StoryId)
            .ToListAsync();
    }

    private static UserStoryInteractionStateDto ToDto(UserStoryInteraction i) =>
        new(i.StoryId, i.HasStarted, i.IsCompleted, i.IsFavorite,
            i.IsHiddenFavorite, i.IsFollowed, i.IsReadItLater, i.IsIgnored);
}
