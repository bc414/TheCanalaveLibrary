using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// In-memory stand-in for <see cref="IUserStoryInteractionWriteService"/> used by
/// <see cref="UserStoryInteractionPanelTests"/>. Records each SetInteractionStateAsync call so tests
/// can assert what the debounce flush dispatched without needing a host or database.
/// </summary>
public class FakeUserStoryInteractionWriteService : IUserStoryInteractionWriteService
{
    public List<(int StoryId, InteractionStateUpdate Update)> SetStateCalls { get; } = [];

    public Task SetInteractionStateAsync(int storyId, InteractionStateUpdate update)
    {
        SetStateCalls.Add((storyId, update));
        return Task.CompletedTask;
    }

    // Read methods — return harmless defaults; not exercised by the panel render tests.
    public Task<UserStoryInteractionStateDto> GetStateAsync(int storyId) =>
        Task.FromResult(UserStoryInteractionStateDto.AllFalse(storyId));

    public Task<IReadOnlyDictionary<int, UserStoryInteractionStateDto>> GetStatesByStoryIdsAsync(
        IReadOnlyList<int> storyIds) =>
        Task.FromResult<IReadOnlyDictionary<int, UserStoryInteractionStateDto>>(
            new Dictionary<int, UserStoryInteractionStateDto>());
}
