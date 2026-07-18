using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IStoryArcWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerStoryArcWriteService : ServerStoryArcReadService. Auth rides the same-origin Identity
/// cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates StoryArcEndpoints' status codes back into the service contract's typed exceptions —
/// the shared MA-008 shape (<see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>); 400
/// reconstructs <see cref="StoryArcValidationException"/>.
/// </para>
/// </summary>
public sealed class ClientStoryArcWriteService(HttpClient http)
    : ClientStoryArcReadService(http), IStoryArcWriteService
{
    public async Task<int> CreateArcAsync(CreateStoryArcDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/story-arcs", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task UpdateArcAsync(UpdateStoryArcDto dto)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync($"api/story-arcs/{dto.StoryArcId}", dto);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task DeleteArcAsync(int storyArcId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/story-arcs/{storyArcId}");
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of StoryArcEndpoints') — the
    /// shared MA-008 shape.</summary>
    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new StoryArcValidationException([msg]));
}
