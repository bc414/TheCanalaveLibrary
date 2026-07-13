using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IStoryLineageWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerStoryLineageWriteService : ServerStoryLineageReadService. Auth rides the same-origin
/// Identity cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates StoryLineageEndpoints' status codes back into the service contract's typed exceptions:
/// 400 → <see cref="StoryLineageValidationException"/> (message from ProblemDetails.Detail), 401/403
/// → <see cref="UnauthorizedAccessException"/>, 404 → <see cref="KeyNotFoundException"/>.
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

    /// <summary>Status-code → contract-exception translation (inverse of StoryLineageEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new StoryLineageValidationException([detail ?? "The lineage request failed validation."]);
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("You do not own the story required for this action.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Lineage link not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
