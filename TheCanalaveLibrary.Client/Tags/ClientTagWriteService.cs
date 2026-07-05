using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ITagWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerTagWriteService : ServerTagReadService. Auth rides the same-origin Identity cookie —
/// WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates TagEndpoints' status codes back into the service contract's typed exceptions so
/// components behave identically on either side of the interface: 400 → TagValidationException
/// (message from ProblemDetails.Detail — TagDirectoryDesktop shows it inline), 401/403 →
/// UnauthorizedAccessException, 404 → KeyNotFoundException.
/// </para>
/// </summary>
public sealed class ClientTagWriteService(HttpClient http) : ClientTagReadService(http), ITagWriteService
{
    public async Task<TagSaveResult> CreateTagAsync(CreateTagDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/tags", dto);
        await ThrowIfWriteFailedAsync(response);
        return (await response.Content.ReadFromJsonAsync<TagSaveResult>())!;
    }

    public async Task<string?> UpdateTagAsync(UpdateTagDto dto)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync($"api/tags/{dto.TagId}", dto);
        await ThrowIfWriteFailedAsync(response);
        // Body is the raw sprite-warning string or JSON null (see TagEndpoints — no wrapper DTO).
        return await response.Content.ReadFromJsonAsync<string?>();
    }

    public async Task DeleteTagAsync(int tagId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/tags/{tagId}");
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of TagEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                ProblemPayload? problem = await response.Content.ReadFromJsonAsync<ProblemPayload>();
                throw new TagValidationException(problem?.Detail ?? "The tag failed validation.");
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("Tag administration requires moderator or admin role.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Tag not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }

    /// <summary>Just the ProblemDetails field we consume — MVC's type isn't referenced in WASM.</summary>
    private sealed record ProblemPayload(string? Detail);
}
