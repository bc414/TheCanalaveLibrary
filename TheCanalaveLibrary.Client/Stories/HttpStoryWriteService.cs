using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// Client-side implementation of the story write service that communicates with the server's API.
/// </summary>
public class HttpStoryWriteService : IStoryWriteService
{
    private readonly HttpClient _httpClient;

    public HttpStoryWriteService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<int> CreateStoryAsync(CreateStoryDTO dto)
    {
        HttpResponseMessage response = await _httpClient.PostAsJsonAsync("api/stories", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task UpdateStoryAsync(StoryUpdateDTO dto)
    {
        HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"api/stories/{dto.StoryId}", dto);
        response.EnsureSuccessStatusCode();
    }
}
