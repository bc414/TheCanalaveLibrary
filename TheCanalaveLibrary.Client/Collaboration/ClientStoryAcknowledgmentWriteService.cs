using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IStoryAcknowledgmentWriteService"/>. Inherits the read impl (CQRS-lite),
/// mirroring ServerStoryAcknowledgmentWriteService : ServerStoryAcknowledgmentReadService. Auth
/// rides the same-origin Identity cookie.
/// <para>
/// Translates StoryAcknowledgmentEndpoints' status codes back into the service contract's typed
/// exceptions — the shared MA-008 shape; 400 reconstructs
/// <see cref="StoryAcknowledgmentValidationException"/>.
/// </para>
/// </summary>
public sealed class ClientStoryAcknowledgmentWriteService(HttpClient http)
    : ClientStoryAcknowledgmentReadService(http), IStoryAcknowledgmentWriteService
{
    public async Task RequestAcknowledgmentAsync(CreateStoryAcknowledgmentDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/story-acknowledgments", dto);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task AcceptAsync(int storyId, short roleId)
    {
        HttpResponseMessage response = await Http.PostAsync(
            $"api/story-acknowledgments/{storyId}/{roleId}/accept", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task DeclineAsync(int storyId, short roleId)
    {
        HttpResponseMessage response = await Http.PostAsync(
            $"api/story-acknowledgments/{storyId}/{roleId}/decline", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task RevokeAsync(int storyId, int acknowledgedUserId, short roleId)
    {
        HttpResponseMessage response = await Http.DeleteAsync(
            $"api/story-acknowledgments/{storyId}/{acknowledgedUserId}/{roleId}");
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation — the shared MA-008 shape.</summary>
    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new StoryAcknowledgmentValidationException([msg]));
}
