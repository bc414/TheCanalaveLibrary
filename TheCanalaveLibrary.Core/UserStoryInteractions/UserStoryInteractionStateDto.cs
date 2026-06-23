namespace TheCanalaveLibrary.Core;

/// <summary>
/// Per-viewer interaction state for one story. All seven stored bits are included so the panel and
/// read-path consumers have a complete picture. Missing from the service dictionary means all false.
/// </summary>
public record UserStoryInteractionStateDto(
    int StoryId,
    bool HasStarted,
    bool IsCompleted,
    bool IsFavorite,
    bool IsHiddenFavorite,
    bool IsFollowed,
    bool IsReadItLater,
    bool IsIgnored)
{
    /// <summary>Default all-false state used when no row exists for the viewer.</summary>
    public static UserStoryInteractionStateDto AllFalse(int storyId) =>
        new(storyId, false, false, false, false, false, false, false);
}
