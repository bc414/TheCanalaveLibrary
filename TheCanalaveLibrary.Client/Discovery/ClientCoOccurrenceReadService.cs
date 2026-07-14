using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ICoOccurrenceReadService"/>: HttpClient wrapper over CoOccurrenceEndpoints
/// (Server/Discovery/CoOccurrenceEndpoints.cs). Read-only, no matching write service — one client
/// class, no read/write inheritance split (layer5-wasm.md §"Client Service Implementations"). The
/// viewer's rating filter is resolved server-side from the cookie — nothing sensitive crosses the
/// HTTP boundary; <c>excludedInteractions</c> is the caller's own live filter choice, not secret.
///
/// <para>POST, body-bound via <see cref="CoOccurrenceRequest"/> — the optional exclusions array
/// isn't GET-bindable (layer5-wasm.md §"Reads with non-scalar parameters").</para>
///
/// <para>The interface's <c>CancellationToken ct = default</c> parameters are kept for contract
/// conformance but never threaded into the HttpClient calls — layer5-wasm.md §"CancellationToken
/// parameters are dropped at the client boundary".</para>
/// </summary>
public class ClientCoOccurrenceReadService(HttpClient http) : ICoOccurrenceReadService
{
    private HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<RelatedStoryScoreDto>> GetAlsoFavoritedAsync(
        int storyId, int take = 10,
        IReadOnlyList<UserStoryInteractionTypeEnum>? excludedInteractions = null,
        CancellationToken ct = default)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync(
            "api/co-occurrence/also-favorited", new CoOccurrenceRequest(storyId, take, excludedInteractions));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<RelatedStoryScoreDto>>() ?? [];
    }

    public async Task<IReadOnlyList<RelatedStoryScoreDto>> GetAlsoRecommendedAsync(
        int storyId, int take = 10,
        IReadOnlyList<UserStoryInteractionTypeEnum>? excludedInteractions = null,
        CancellationToken ct = default)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync(
            "api/co-occurrence/also-recommended", new CoOccurrenceRequest(storyId, take, excludedInteractions));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<RelatedStoryScoreDto>>() ?? [];
    }
}
