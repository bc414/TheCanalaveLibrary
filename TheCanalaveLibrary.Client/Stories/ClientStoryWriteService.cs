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

    public async Task<StoryStatusEnum> TransitionStatusAsync(int storyId, StoryStatusEnum targetStatus)
    {
        // Numeric enum on the wire: minimal API's enum TryParse accepts it, and an undefined value
        // (e.g. 99) still reaches the service's own Enum.IsDefined check → 400.
        HttpResponseMessage response = await Http.PostAsync(
            $"api/stories/{storyId}/status?target={(short)targetStatus}", content: null);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<StoryStatusEnum>();
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

    /// <summary>Bodiless <c>DELETE api/stories/{id}/cover</c> → 204 (owner ruling D14: clearing a
    /// cover is its own call).</summary>
    public async Task RemoveCoverArtAsync(int storyId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/stories/{storyId}/cover");
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Bodiless <c>DELETE api/stories/{id}</c> → 204: the author story delete (owner ruling
    /// D15). 401/403/404 come back as the contract's exceptions through the shared mapping.</summary>
    public async Task DeleteStoryAsync(int storyId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/stories/{storyId}");
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of StoryEndpoints') — the
    /// shared MA-008 shape.</summary>
    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, errs => new StoryValidationException([.. errs]));
}
