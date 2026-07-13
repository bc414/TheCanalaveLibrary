using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ICoOccurrenceReadService"/>: HttpClient wrapper over CoOccurrenceEndpoints
/// (Server/Discovery/CoOccurrenceEndpoints.cs). Read-only, no matching write service — one client
/// class, no read/write inheritance split (layer5-wasm.md §"Client Service Implementations"). The
/// viewer's rating/interaction filters are resolved server-side from the cookie — nothing
/// sensitive crosses the HTTP boundary.
///
/// <para>The interface's <c>CancellationToken ct = default</c> parameters are kept for contract
/// conformance but never threaded into the HttpClient calls — layer5-wasm.md §"CancellationToken
/// parameters are dropped at the client boundary".</para>
/// </summary>
public class ClientCoOccurrenceReadService(HttpClient http) : ICoOccurrenceReadService
{
    private HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<RelatedStoryScoreDto>> GetAlsoFavoritedAsync(
        int storyId, int take = 10, CancellationToken ct = default) =>
        await Http.GetFromJsonAsync<List<RelatedStoryScoreDto>>(
            $"api/co-occurrence/also-favorited?storyId={storyId}&take={take}") ?? [];

    public async Task<IReadOnlyList<RelatedStoryScoreDto>> GetAlsoRecommendedAsync(
        int storyId, int take = 10, CancellationToken ct = default) =>
        await Http.GetFromJsonAsync<List<RelatedStoryScoreDto>>(
            $"api/co-occurrence/also-recommended?storyId={storyId}&take={take}") ?? [];
}
