using System.Net.Http.Json;
using TheCanalaveLibrary.Core.DTOs;
using TheCanalaveLibrary.Core.ServiceInterfaces;

namespace TheCanalaveLibrary.Client.Services;

public class HttpStoryOverviewService : IStoryOverviewService
{
    private readonly HttpClient _httpClient;

    public HttpStoryOverviewService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<StoryDetailsDTO?> GetStoryByIdAsync(int storyId)
    {
        // This calls an API endpoint like "/api/stories/{storyId}"
        return await _httpClient.GetFromJsonAsync<StoryDetailsDTO?>($"api/stories/{storyId}");
    }

    public async Task<int> GetRandomNumber()
    {
        return await _httpClient.GetFromJsonAsync<int>("api/stories/random-number");
    }
}