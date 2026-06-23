namespace TheCanalaveLibrary.Core;

/// <summary>
/// The six panel-managed bits sent to <see cref="IUserStoryInteractionWriteService.SetInteractionStateAsync"/>.
/// HasStarted is deliberately absent — only the reading path (WU26) sets it, and the write service
/// preserves whatever value is already stored.
/// </summary>
public record InteractionStateUpdate(
    bool IsFavorite,
    bool IsHiddenFavorite,
    bool IsFollowed,
    bool IsCompleted,
    bool IsReadItLater,
    bool IsIgnored);
