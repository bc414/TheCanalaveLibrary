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
/// components behave identically on either side of the interface — the shared MA-008 shape
/// (<see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>); 400 reconstructs
/// <see cref="StoryValidationException"/>.
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

    /// <summary>Status-code → contract-exception translation (inverse of StoryEndpoints') — the
    /// shared MA-008 shape.</summary>
    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new StoryValidationException([msg]));
}
