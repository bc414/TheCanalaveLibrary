namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the UserStoryInteractions feature cluster. Scoped to the current viewer
/// via <see cref="IActiveUserContext"/>. Anonymous viewers always receive all-false state.
/// </summary>
public interface IUserStoryInteractionReadService
{
    /// <summary>
    /// Returns the current viewer's interaction state for a single story, or an all-false default
    /// when no row exists or the viewer is anonymous.
    /// </summary>
    Task<UserStoryInteractionStateDto> GetStateAsync(int storyId);

    /// <summary>
    /// Batch-loads the current viewer's interaction state for a set of stories in one query.
    /// Missing rows are omitted — callers treat an absent key as all-false (same as
    /// <see cref="UserStoryInteractionStateDto.AllFalse"/>). This is the N+1-safe method for
    /// listing contexts: the page or deck calls it once and passes each card its slice as a
    /// [Parameter] rather than each card injecting the service.
    /// </summary>
    Task<IReadOnlyDictionary<int, UserStoryInteractionStateDto>> GetStatesByStoryIdsAsync(
        IReadOnlyList<int> storyIds);

    /// <summary>
    /// Returns all story IDs in the active user's bookshelf for the given interaction-backed tab.
    /// Anonymous → empty without a DB hit. Tabs not backed by <see cref="UserStoryInteraction"/>
    /// (<see cref="BookshelfTab.MyStories"/>, <see cref="BookshelfTab.Recommendations"/>,
    /// <see cref="BookshelfTab.HiddenGems"/>) throw <see cref="ArgumentOutOfRangeException"/> —
    /// the dispatcher routes those to the appropriate service instead.
    /// </summary>
    Task<IReadOnlyList<int>> GetBookshelfStoryIdsAsync(BookshelfTab tab);

    /// <summary>
    /// Returns the IDs of stories that <paramref name="userId"/> has marked as a favorite,
    /// for use on a public profile's Favorites tab. When <paramref name="includePrivate"/> is
    /// <c>false</c> (viewer is not the owner), hidden favorites
    /// (<see cref="UserStoryInteraction.IsHiddenFavorite"/>) are excluded
    /// (<c>WHERE is_favorite AND NOT is_hidden_favorite</c>). When <c>true</c> (owner viewing own
    /// profile), all favorites are returned regardless of the hidden flag. This satisfies §5.27's
    /// <c>AllowDiscoveryFromHiddenFavorites</c> requirement for the profile display surface.
    /// </summary>
    Task<IReadOnlyList<int>> GetFavoriteStoryIdsAsync(int userId, bool includePrivate);
}
