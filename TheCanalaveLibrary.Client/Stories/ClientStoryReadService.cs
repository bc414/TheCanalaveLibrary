using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IStoryReadService"/>: HttpClient wrapper over
/// Server/Stories/StoryEndpoints.cs. Same DTOs, same method contracts — only the transport differs
/// (the Layer-5 body-swap). <see cref="GetListingsAsync"/>/<see cref="GetRecentListingsAsync"/>
/// translate through <see cref="PagedResult{T}"/> at the HTTP boundary only (layer5-wasm.md
/// §"Paged results") — the tuple shape the interface expects is unchanged.
/// </summary>
public class ClientStoryReadService(HttpClient http) : IStoryReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId) =>
        await Http.GetNullableFromJsonAsync<StoryDetailsDTO?>($"api/stories/{storyId}");

    public async Task<StoryUpdateDTO?> GetStoryForEditAsync(int storyId)
    {
        // 401/403 → UnauthorizedAccessException, mirroring the server service's author gate so
        // StoryEditorPage's forbidden handling works identically under both render modes
        // (status→contract-exception translation, layer5-wasm.md "The Error-Translation Contract").
        using HttpResponseMessage response = await Http.GetAsync($"api/stories/{storyId}/edit");
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new UnauthorizedAccessException("You must be the author of this story.");
        response.EnsureSuccessStatusCode();
        return await ClientHttpHelpers.ReadNullableFromJsonAsync<StoryUpdateDTO?>(response.Content);
    }

    public async Task<StoryListingDto[]> GetListingsByIdsAsync(IReadOnlyList<int> storyIds)
    {
        if (storyIds.Count == 0) return [];

        string query = string.Join('&', storyIds.Select(id => $"storyIds={id}"));
        return await Http.GetFromJsonAsync<StoryListingDto[]>($"api/stories/by-ids?{query}") ?? [];
    }

    public async Task<(StoryListingDto[] Items, int TotalCount)> GetRecentListingsAsync(int page, int pageSize)
    {
        PagedResult<StoryListingDto> result = (await Http.GetFromJsonAsync<PagedResult<StoryListingDto>>(
            $"api/stories/recent?page={page}&pageSize={pageSize}"))!;
        return (result.Items, result.TotalCount);
    }

    public async Task<(StoryListingDto[] Items, int TotalCount)> GetListingsAsync(
        StoryFilterDto filter, IReadOnlyCollection<int>? restrictToStoryIds = null)
    {
        string url = "api/stories/query";
        if (restrictToStoryIds is { Count: > 0 })
            url += "?" + string.Join('&', restrictToStoryIds.Select(id => $"restrictToStoryIds={id}"));

        HttpResponseMessage response = await Http.PostAsJsonAsync(url, filter);
        response.EnsureSuccessStatusCode();
        PagedResult<StoryListingDto> result =
            (await response.Content.ReadFromJsonAsync<PagedResult<StoryListingDto>>())!;
        return (result.Items, result.TotalCount);
    }

    public async Task<StoryListingDto[]> GetRandomBatchAsync(StoryFilterDto filter, int batchSize)
    {
        HttpResponseMessage response =
            await Http.PostAsJsonAsync($"api/stories/random-batch?batchSize={batchSize}", filter);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<StoryListingDto[]>() ?? [];
    }

    public async Task<IReadOnlyList<int>> FilterCandidateIdsAsync(
        IReadOnlyCollection<int> candidateIds, StoryFilterDto filter)
    {
        if (candidateIds.Count == 0) return []; // mirrors the server impl's short-circuit

        string query = string.Join('&', candidateIds.Select(id => $"candidateIds={id}"));
        HttpResponseMessage response = await Http.PostAsJsonAsync($"api/stories/filter-candidates?{query}", filter);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<int>>() ?? [];
    }

    public async Task<IReadOnlyList<int>> GetStoryIdsByAuthorAsync(int authorId) =>
        await Http.GetFromJsonAsync<List<int>>($"api/stories/by-author/{authorId}") ?? [];

    public async Task<IReadOnlyList<ExternalPlatformDto>> GetExternalPlatformsAsync() =>
        await Http.GetFromJsonAsync<List<ExternalPlatformDto>>("api/stories/external-platforms") ?? [];

    public async Task<long> GetStoryTotalViewsAsync(int storyId) =>
        await Http.GetFromJsonAsync<long>($"api/stories/{storyId}/total-views");

    public async Task<IReadOnlyList<StoryTitleSearchDto>> SearchStoriesByTitleAsync(string term)
    {
        // Mirror the server impl's blank-term short-circuit — no round trip for a contractual no-op.
        if (string.IsNullOrWhiteSpace(term)) return [];

        return await Http.GetFromJsonAsync<List<StoryTitleSearchDto>>(
            $"api/stories/search-by-title?term={Uri.EscapeDataString(term)}") ?? [];
    }
}
