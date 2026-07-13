using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IStoryArcWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerStoryArcWriteService : ServerStoryArcReadService. Auth rides the same-origin Identity
/// cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates StoryArcEndpoints' status codes back into the service contract's typed exceptions:
/// 400 → <see cref="StoryArcValidationException"/> (message from ProblemDetails.Detail), 401/403 →
/// <see cref="UnauthorizedAccessException"/>, 404 → <see cref="KeyNotFoundException"/>.
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

    /// <summary>Status-code → contract-exception translation (inverse of StoryArcEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new StoryArcValidationException([detail ?? "The story arc failed validation."]);
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("You must be the author of this story.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Story arc not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
