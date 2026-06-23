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
}
