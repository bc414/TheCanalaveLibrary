using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IStoryArcReadService"/>: HttpClient wrapper over
/// Server/Stories/StoryArcEndpoints.cs. Same DTOs, same method contracts — only the transport
/// differs (the Layer-5 body-swap).
/// </summary>
public class ClientStoryArcReadService(HttpClient http) : IStoryArcReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<StoryArcDto>> GetArcsForStoryAsync(int storyId) =>
        await Http.GetFromJsonAsync<List<StoryArcDto>>($"api/story-arcs/by-story/{storyId}") ?? [];
}
