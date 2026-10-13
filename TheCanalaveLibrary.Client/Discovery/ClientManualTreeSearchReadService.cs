using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IManualTreeSearchReadService"/>: HttpClient wrapper over
/// ManualTreeSearchEndpoints (Server/Discovery/ManualTreeSearchEndpoints.cs). Same DTOs, same
/// method contracts — only the transport differs (the Layer-5 body-swap). Read-only, no matching
/// write service — one client class, no read/write inheritance split (layer5-wasm.md §"Client
/// Service Implementations").
/// <para>Error translation differs per method, following the endpoint. The user pivot is wrapped in
/// <c>EndpointHelpers.ExecuteAsync</c> because its optional <see cref="StoryFilterDto"/> can carry
/// malformed ship criteria, which the server refuses with a user-facing
/// <see cref="StoryValidationException"/> (400). <see cref="GetUserNeighborsAsync"/> therefore
/// rebuilds that exception through <c>ClientHttpHelpers.ThrowIfWriteFailedAsync</c>, the same shape as
/// <c>ClientTreeSearchReadService.SearchAsync</c>; before WU-ParityAndRemaining (2026-10-13) a bare
/// <c>EnsureSuccessStatusCode</c> turned it into an <see cref="HttpRequestException"/> and the message
/// was lost on WASM (service audit §2.7.7). The story pivot and the node-display read stay
/// <c>EnsureSuccessStatusCode</c>: their endpoints are plain pass-throughs that never answer a typed
/// 400, so any failure there is a client bug or a fault (layer5-wasm.md §"Reads with non-scalar
/// parameters" → "Which translator a read uses").</para>
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
        await ClientHttpHelpers.ThrowIfWriteFailedAsync(
            response, errs => new StoryValidationException([.. errs]));
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
