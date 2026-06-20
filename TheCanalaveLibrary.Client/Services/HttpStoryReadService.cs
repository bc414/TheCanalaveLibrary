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
}