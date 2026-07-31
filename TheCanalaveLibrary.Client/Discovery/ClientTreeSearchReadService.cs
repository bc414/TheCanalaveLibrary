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
        // Server-side Validate(request) throws ArgumentException (400) for a malformed request —
        // now wrapped in ExecuteAsync server-side (WU-ErrorHandling2, 2026-07-30, same WU); read
        // translator here so it surfaces as a 400, not an unhandled 500.
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/tree-search/traverse", request);
        await ClientHttpHelpers.ThrowIfReadFailedAsync(response);
        return (await response.Content.ReadFromJsonAsync<TreeSearchResultDto>())!;
    }

    public async Task<TreeSearchListingResultDto> SearchAsync(
        TreeSearchRequest request, StoryFilterDto filter, CancellationToken ct = default)
    {
        // Two independent 400 sources land on the same status code: Validate(request)'s
        // ArgumentException (malformed tree-search shape) and — since this calls through to
        // IStoryReadService.FilterCandidateIdsAsync internally — ApplyFiltersAsync's
        // ValidateShipShape StoryValidationException (malformed ship criteria, same as
        // StoryEndpoints' /query et al). Reconstructing as StoryValidationException either way
        // keeps the message user-facing via ExceptionPresenter rather than losing it to the
        // generic "unexpected error" path a plain InvalidOperationException would hit.
        HttpResponseMessage response = await Http.PostAsJsonAsync(
            "api/tree-search/search", new TreeSearchListingRequest(request, filter));
        await ClientHttpHelpers.ThrowIfWriteFailedAsync(
            response, detail => new StoryValidationException([detail]));
        return (await response.Content.ReadFromJsonAsync<TreeSearchListingResultDto>())!;
    }
}
