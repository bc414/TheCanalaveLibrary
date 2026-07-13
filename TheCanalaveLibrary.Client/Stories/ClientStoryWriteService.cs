using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IStoryWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerStoryWriteService : ServerStoryReadService. Auth rides the same-origin Identity cookie —
/// WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates StoryEndpoints' status codes back into the service contract's typed exceptions so
/// components behave identically on either side of the interface: 400 →
/// <see cref="StoryValidationException"/> (message from ProblemDetails.Detail), 401/403 →
/// <see cref="UnauthorizedAccessException"/>, 404 → <see cref="KeyNotFoundException"/>.
/// </para>
/// </summary>
public sealed class ClientStoryWriteService(HttpClient http) : ClientStoryReadService(http), IStoryWriteService
{
    public async Task<int> CreateStoryAsync(CreateStoryDTO dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/stories", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task UpdateStoryAsync(StoryUpdateDTO dto)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync($"api/stories/{dto.StoryId}", dto);
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>
    /// Multipart upload (layer5-wasm.md §"Streams and multipart") — builds a
    /// <see cref="MultipartFormDataContent"/> with a <see cref="StreamContent"/> part; the endpoint
    /// reads it back via <c>IFormFile</c>.
    /// </summary>
    public async Task<string> UploadCoverArtAsync(Stream content, string contentType, int storyId)
    {
        using MultipartFormDataContent form = new();
        using StreamContent streamContent = new(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(streamContent, "file", "cover");

        HttpResponseMessage response = await Http.PostAsync($"api/stories/{storyId}/cover", form);
        await ThrowIfWriteFailedAsync(response);
        return (await response.Content.ReadFromJsonAsync<string>())!;
    }

    /// <summary>Status-code → contract-exception translation (inverse of StoryEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new StoryValidationException([detail ?? "The story failed validation."]);
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("You can only edit your own stories.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Story not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
