using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IStoryLineageWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerStoryLineageWriteService : ServerStoryLineageReadService. Auth rides the same-origin
/// Identity cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates StoryLineageEndpoints' status codes back into the service contract's typed
/// exceptions — the shared MA-008 shape (<see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>);
/// 400 reconstructs <see cref="StoryLineageValidationException"/>.
/// </para>
/// </summary>
public sealed class ClientStoryLineageWriteService(HttpClient http)
    : ClientStoryLineageReadService(http), IStoryLineageWriteService
{
    public async Task RequestLineageAsync(CreateStoryLineageDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/story-lineage", dto);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task ApproveLineageAsync(int sourceStoryId, int targetStoryId, short typeId)
    {
        HttpResponseMessage response = await Http.PostAsync(
            $"api/story-lineage/{sourceStoryId}/{targetStoryId}/{typeId}/approve", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task RejectLineageAsync(int sourceStoryId, int targetStoryId, short typeId)
    {
        HttpResponseMessage response = await Http.PostAsync(
            $"api/story-lineage/{sourceStoryId}/{targetStoryId}/{typeId}/reject", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task DeleteLineageAsync(int sourceStoryId, int targetStoryId, short typeId)
    {
        HttpResponseMessage response = await Http.DeleteAsync(
            $"api/story-lineage/{sourceStoryId}/{targetStoryId}/{typeId}");
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of StoryLineageEndpoints') —
    /// the shared MA-008 shape.</summary>
    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new StoryLineageValidationException([msg]));
}
