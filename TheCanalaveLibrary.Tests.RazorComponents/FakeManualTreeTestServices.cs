using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Tests.RazorComponents;

/// <summary>
/// Configurable fake for <see cref="IManualTreeSearchReadService"/> (WU40 — ExploreTab/DeepDiveTab
/// bUnit tests). Set the section results per anchor type; captures the last requests so tests can
/// assert the (edge, direction) flags the component actually sent.
/// </summary>
internal sealed class FakeManualTreeSearchReadService : IManualTreeSearchReadService
{
    public ManualTreeNeighborsDto StoryResult { get; set; } = new();
    public ManualTreeNeighborsDto UserResult { get; set; } = new();
    public ManualTreeNodeDisplaysDto Displays { get; set; } = new([], []);

    public StoryNeighborsRequest? LastStoryRequest { get; private set; }
    public UserNeighborsRequest? LastUserRequest { get; private set; }

    public Task<ManualTreeNeighborsDto> GetStoryNeighborsAsync(StoryNeighborsRequest request, CancellationToken ct = default)
    {
        LastStoryRequest = request;
        return Task.FromResult(StoryResult);
    }

    public Task<ManualTreeNeighborsDto> GetUserNeighborsAsync(UserNeighborsRequest request, CancellationToken ct = default)
    {
        LastUserRequest = request;
        return Task.FromResult(UserResult);
    }

    public Task<ManualTreeNodeDisplaysDto> GetNodeDisplaysAsync(
        IReadOnlyCollection<int> storyIds, IReadOnlyCollection<int> userIds, CancellationToken ct = default) =>
        Task.FromResult(Displays);
}
