using System.Net.Http.Json;
using TheCanalaveLibrary.Core.DTOs;
using TheCanalaveLibrary.Core.ServiceInterfaces;

namespace TheCanalaveLibrary.Client.Services;

public class HttpStoryService : IStoryService
{
    private readonly HttpClient _httpClient;

    public HttpStoryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId)
    {
        return await _httpClient.GetFromJsonAsync<StoryDetailsDTO>($"api/stories/{storyId}");
    }
}