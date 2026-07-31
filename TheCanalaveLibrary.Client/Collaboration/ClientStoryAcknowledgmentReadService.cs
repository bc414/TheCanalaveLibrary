using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IStoryAcknowledgmentReadService"/>: HttpClient wrapper over
/// Server/Collaboration/StoryAcknowledgmentEndpoints.cs. Same DTOs, same method contracts — only the
/// transport differs (the Layer-5 body-swap). Mirrors <c>ClientStoryLineageReadService</c>.
/// </summary>
public class ClientStoryAcknowledgmentReadService(HttpClient http) : IStoryAcknowledgmentReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<StoryAcknowledgmentDto>> GetAcknowledgmentsForStoryAsync(int storyId) =>
        await Http.GetFromJsonAsync<List<StoryAcknowledgmentDto>>($"api/story-acknowledgments/by-story/{storyId}") ?? [];

    public async Task<StoryAcknowledgmentManageDto> GetManageDataForUserAsync() =>
        (await Http.GetFromJsonAsync<StoryAcknowledgmentManageDto>("api/story-acknowledgments/manage"))!;

    public async Task<IReadOnlyList<AcknowledgmentRoleDto>> GetAcknowledgmentRolesAsync() =>
        await Http.GetFromJsonAsync<List<AcknowledgmentRoleDto>>("api/story-acknowledgments/roles") ?? [];
}
