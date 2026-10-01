using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// In-memory stand-in for <see cref="IUserStoryInteractionWriteService"/> used by
/// <see cref="UserStoryInteractionPanelTests"/>. Records each SetUserStoryInteractionStateAsync call so tests
/// can assert what the debounce flush dispatched without needing a host or database.
/// </summary>
public class FakeUserStoryInteractionWriteService : IUserStoryInteractionWriteService
{
    public List<(int StoryId, UserStoryInteractionStateUpdate Update)> SetStateCalls { get; } = [];

    public Task SetUserStoryInteractionStateAsync(int storyId, UserStoryInteractionStateUpdate update)
    {
        SetStateCalls.Add((storyId, update));
        return Task.CompletedTask;
    }

    // Read methods — serve whatever a test seeds into States (empty by default, so the panel render
    // tests see all-false). Hosts that batch-load state (the Spotlight display) or re-read it after a
    // recommendation card's Read It Later read through here.
    public Dictionary<int, UserStoryInteractionStateDto> States { get; } = [];
    public List<IReadOnlyList<int>> GetStatesCalls { get; } = [];

    public Task<UserStoryInteractionStateDto> GetStateAsync(int storyId) =>
        Task.FromResult(States.TryGetValue(storyId, out UserStoryInteractionStateDto? s)
            ? s : UserStoryInteractionStateDto.AllFalse(storyId));

    public Task<IReadOnlyDictionary<int, UserStoryInteractionStateDto>> GetStatesByStoryIdsAsync(
        IReadOnlyList<int> storyIds)
    {
        GetStatesCalls.Add(storyIds);
        return Task.FromResult<IReadOnlyDictionary<int, UserStoryInteractionStateDto>>(
            storyIds.Where(States.ContainsKey).ToDictionary(id => id, id => States[id]));
    }

    public Task<IReadOnlyList<int>> GetBookshelfStoryIdsAsync(BookshelfTab tab) =>
        Task.FromResult<IReadOnlyList<int>>([]);

    public Task<IReadOnlyList<int>> GetFavoriteStoryIdsAsync(int userId, bool includePrivate) =>
        Task.FromResult<IReadOnlyList<int>>([]);

    // (storyId, direct-link recommendation id) — the second half is the ?rec= attribution carrier
    // (owner ruling D3), null when the reader arrived without one.
    public List<(int StoryId, int? RecommendationId)> MarkStartedCalls { get; } = [];
    public Task MarkStartedAsync(int storyId, int? attributedRecommendationId = null)
    {
        MarkStartedCalls.Add((storyId, attributedRecommendationId));
        return Task.CompletedTask;
    }

    public List<int> ReadItLaterFromRecommendationCalls { get; } = [];
    /// <summary>Set to make the next card Read It Later throw (error-path tests).</summary>
    public Exception? ReadItLaterFromRecommendationThrows { get; set; }
    public Task SetReadItLaterFromRecommendationAsync(int recommendationId)
    {
        if (ReadItLaterFromRecommendationThrows is { } ex) return Task.FromException(ex);
        ReadItLaterFromRecommendationCalls.Add(recommendationId);
        return Task.CompletedTask;
    }

    public List<int> MarkCompletedCalls { get; } = [];
    public Task MarkCompletedAsync(int storyId)
    {
        MarkCompletedCalls.Add(storyId);
        return Task.CompletedTask;
    }
}
