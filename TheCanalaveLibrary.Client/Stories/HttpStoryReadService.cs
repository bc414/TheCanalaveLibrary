using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// Client-side implementation of the story read service that communicates with the server's API.
/// </summary>
public class HttpStoryReadService : IStoryReadService
{
    private readonly HttpClient _httpClient;

    public HttpStoryReadService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId)
    {
        return await _httpClient.GetFromJsonAsync<StoryDetailsDTO?>($"api/stories/{storyId}");
    }

    public async Task<StoryUpdateDTO?> GetStoryForEditAsync(int storyId)
    {
        return await _httpClient.GetFromJsonAsync<StoryUpdateDTO?>($"api/stories/{storyId}/edit");
    }

    // Endpoint not yet mapped by StoryEndpoints (L5 is post-MVP — MVP components inject the server
    // service directly, see workplan.md Post-MVP "L5 — WASM enablement"). Minted here only so this
    // Client impl compiles against the WU12-extended IStoryReadService contract.
    public async Task<StoryListingDto[]> GetListingsByIdsAsync(IReadOnlyList<int> storyIds)
    {
        string idsParam = string.Join(",", storyIds);
        return await _httpClient.GetFromJsonAsync<StoryListingDto[]>($"api/stories/by-ids?ids={idsParam}")
            ?? [];
    }

    public async Task<(StoryListingDto[] Items, int TotalCount)> GetRecentListingsAsync(int page, int pageSize)
    {
        StoryListingPageDto? result = await _httpClient.GetFromJsonAsync<StoryListingPageDto>(
            $"api/stories/recent?page={page}&pageSize={pageSize}");
        return result is null ? ([], 0) : (result.Items, result.TotalCount);
    }
}
