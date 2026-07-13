using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ITreeSearchReadService"/>: HttpClient wrapper over TreeSearchEndpoints
/// (Server/Discovery/TreeSearchEndpoints.cs). Same DTOs, same method contracts — only the
/// transport differs (the Layer-5 body-swap). Read-only, no matching write service — one client
/// class, no read/write inheritance split (layer5-wasm.md §"Client Service Implementations").
/// <see cref="SearchAsync"/> translates through <see cref="TreeSearchListingRequest"/> at the HTTP
/// boundary only (see that record's doc comment) — the two-parameter shape the interface expects
/// is unchanged.
///
/// <para>The interface's <c>CancellationToken ct = default</c> parameters are kept for contract
/// conformance but never threaded into the HttpClient calls — layer5-wasm.md §"CancellationToken
/// parameters are dropped at the client boundary".</para>
/// </summary>
public class ClientTreeSearchReadService(HttpClient http) : ITreeSearchReadService
{
    private HttpClient Http { get; } = http;

    public async Task<TreeSearchResultDto> TraverseAsync(TreeSearchRequest request, CancellationToken ct = default)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/tree-search/traverse", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TreeSearchResultDto>())!;
    }

    public async Task<TreeSearchListingResultDto> SearchAsync(
        TreeSearchRequest request, StoryFilterDto filter, CancellationToken ct = default)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync(
            "api/tree-search/search", new TreeSearchListingRequest(request, filter));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TreeSearchListingResultDto>())!;
    }
}
