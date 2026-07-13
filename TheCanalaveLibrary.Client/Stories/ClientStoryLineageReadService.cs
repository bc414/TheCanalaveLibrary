using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IStoryLineageReadService"/>: HttpClient wrapper over
/// Server/Stories/StoryLineageEndpoints.cs. Same DTOs, same method contracts — only the transport
/// differs (the Layer-5 body-swap).
/// </summary>
public class ClientStoryLineageReadService(HttpClient http) : IStoryLineageReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<StoryLineageDto>> GetLineageForStoryAsync(int storyId) =>
        await Http.GetFromJsonAsync<List<StoryLineageDto>>($"api/story-lineage/by-story/{storyId}") ?? [];

    public async Task<StoryLineageManageDto> GetManageDataForUserAsync() =>
        (await Http.GetFromJsonAsync<StoryLineageManageDto>("api/story-lineage/manage"))!;

    public async Task<IReadOnlyList<StoryLineageTypeDto>> GetLineageTypesAsync() =>
        await Http.GetFromJsonAsync<List<StoryLineageTypeDto>>("api/story-lineage/types") ?? [];
}
