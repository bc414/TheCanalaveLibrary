using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IUserStoryInteractionWriteService"/>. Inherits the read impl (CQRS-lite),
/// mirroring ServerUserStoryInteractionWriteService : ServerUserStoryInteractionReadService. Auth
/// rides the same-origin Identity cookie — WASM's fetch-backed HttpClient sends it automatically
/// for same-origin requests.
/// </summary>
public sealed class ClientUserStoryInteractionWriteService(HttpClient http)
    : ClientUserStoryInteractionReadService(http), IUserStoryInteractionWriteService
{
    public async Task SetUserStoryInteractionStateAsync(int storyId, UserStoryInteractionStateUpdate update)
    {
        HttpResponseMessage response =
            await Http.PostAsJsonAsync($"api/user-story-interactions/{storyId}", update);
        await ThrowIfFailedAsync(response);
    }

    public async Task MarkStartedAsync(int storyId)
    {
        HttpResponseMessage response =
            await Http.PostAsync($"api/user-story-interactions/{storyId}/started", null);
        await ThrowIfFailedAsync(response);
    }
}
