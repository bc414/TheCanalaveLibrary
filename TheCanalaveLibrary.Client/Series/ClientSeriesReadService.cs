using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ISeriesReadService"/>: HttpClient wrapper over SeriesEndpoints
/// (Server/Series/SeriesEndpoints.cs). Same DTOs, same method contracts — only the transport
/// differs (the Layer-5 body-swap). All three reads are public endpoints (see SeriesEndpoints'
/// class summary), so no auth handling is needed here beyond the cookie WASM's HttpClient already
/// sends automatically.
/// </summary>
public class ClientSeriesReadService(HttpClient http) : ISeriesReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<SeriesDetailDto?> GetSeriesByIdAsync(int seriesId) =>
        await Http.GetNullableFromJsonAsync<SeriesDetailDto?>($"api/series/{seriesId}");

    public async Task<IReadOnlyList<SeriesListingDto>> GetSeriesByAuthorAsync(int authorId) =>
        await Http.GetFromJsonAsync<List<SeriesListingDto>>($"api/series/by-author/{authorId}") ?? [];

    public async Task<IReadOnlyList<StorySeriesMembershipDto>> GetMembershipsForStoryAsync(int storyId) =>
        await Http.GetFromJsonAsync<List<StorySeriesMembershipDto>>(
            $"api/series/memberships/story/{storyId}") ?? [];
}
