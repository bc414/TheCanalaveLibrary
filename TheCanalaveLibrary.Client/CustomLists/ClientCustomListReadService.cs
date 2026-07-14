using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ICustomListReadService"/> over <c>CustomListEndpoints</c>
/// (<c>/api/custom-lists</c>). Auth rides the same-origin Identity cookie. Mirrors the server
/// impl's anonymous short-circuits where the contract defines them (no round trip for a
/// contractual empty).
/// </summary>
public class ClientCustomListReadService(HttpClient http) : ICustomListReadService
{
    protected HttpClient Http { get; } = http; // protected property, not the ctor param — CS9107

    public async Task<List<CustomListSummaryDto>> GetMyListsAsync() =>
        await Http.GetFromJsonAsync<List<CustomListSummaryDto>>("api/custom-lists/mine") ?? [];

    // Nullable-returning read — empty 200 body when null; must go through GetNullableFromJsonAsync
    // (layer5-wasm.md §"The Error-Translation Contract").
    public async Task<CustomListDetailDto?> GetListDetailAsync(int listId) =>
        await Http.GetNullableFromJsonAsync<CustomListDetailDto>($"api/custom-lists/{listId}");

    public async Task<IReadOnlyList<int>> GetListStoryIdsAsync(int listId, CustomListSortEnum sort) =>
        await Http.GetFromJsonAsync<int[]>(
            $"api/custom-lists/{listId}/story-ids?sort={(int)sort}") ?? [];

    public async Task<List<CustomListSummaryDto>> GetPublicListsByUserAsync(int userId) =>
        await Http.GetFromJsonAsync<List<CustomListSummaryDto>>(
            $"api/custom-lists/public/{userId}") ?? [];

    public async Task<List<CustomListMembershipDto>> GetMyListMembershipsAsync(int storyId) =>
        await Http.GetFromJsonAsync<List<CustomListMembershipDto>>(
            $"api/custom-lists/memberships?storyId={storyId}") ?? [];
}
