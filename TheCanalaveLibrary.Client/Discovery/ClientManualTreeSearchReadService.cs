using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IManualTreeSearchReadService"/>: HttpClient wrapper over
/// ManualTreeSearchEndpoints (Server/Discovery/ManualTreeSearchEndpoints.cs). Same DTOs, same
/// method contracts — only the transport differs (the Layer-5 body-swap). Read-only, no matching
/// write service — one client class, no read/write inheritance split (layer5-wasm.md §"Client
/// Service Implementations"). No ThrowIfWriteFailedAsync-style exception translation: a malformed
/// request DTO is a client bug, not a user-facing validation case (layer5-wasm.md §"Reads with
/// non-scalar parameters").
///
/// <para>The interface's <c>CancellationToken ct = default</c> parameters are kept for contract
/// conformance but never threaded into the HttpClient calls — layer5-wasm.md §"CancellationToken
/// parameters are dropped at the client boundary".</para>
/// </summary>
public class ClientManualTreeSearchReadService(HttpClient http) : IManualTreeSearchReadService
{
    private HttpClient Http { get; } = http;

    public async Task<ManualTreeNeighborsDto> GetStoryNeighborsAsync(
        StoryNeighborsRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage response =
            await Http.PostAsJsonAsync("api/manual-tree-search/neighbors/story", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ManualTreeNeighborsDto>())!;
    }

    public async Task<ManualTreeNeighborsDto> GetUserNeighborsAsync(
        UserNeighborsRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage response =
            await Http.PostAsJsonAsync("api/manual-tree-search/neighbors/user", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ManualTreeNeighborsDto>())!;
    }

    public async Task<ManualTreeNodeDisplaysDto> GetNodeDisplaysAsync(
        IReadOnlyCollection<int> storyIds, IReadOnlyCollection<int> userIds, CancellationToken ct = default)
    {
        // Mirror the server impl's no-op short-circuit — no round trip when nothing was asked for.
        if (storyIds.Count == 0 && userIds.Count == 0) return new ManualTreeNodeDisplaysDto([], []);

        IEnumerable<string> storyParams = storyIds.Select(id => $"storyIds={id}");
        IEnumerable<string> userParams = userIds.Select(id => $"userIds={id}");
        string query = string.Join('&', storyParams.Concat(userParams));

        return (await Http.GetFromJsonAsync<ManualTreeNodeDisplaysDto>(
            $"api/manual-tree-search/node-displays?{query}"))!;
    }
}
