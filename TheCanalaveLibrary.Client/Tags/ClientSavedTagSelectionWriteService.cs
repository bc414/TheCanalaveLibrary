using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ISavedTagSelectionWriteService"/>. Inherits the read impl (CQRS-lite),
/// mirroring <see cref="ServerSavedTagSelectionWriteService"/> : <see cref="ServerSavedTagSelectionReadService"/>.
/// Auth rides the same-origin Identity cookie — WASM's fetch-backed HttpClient sends it automatically.
/// <para>
/// Translates SavedTagSelectionEndpoints' status codes back into the service contract's typed
/// exceptions: 400 → <see cref="SavedTagSelectionValidationException"/>, 401/403 →
/// <see cref="UnauthorizedAccessException"/>, 404 → <see cref="KeyNotFoundException"/>.
/// </para>
/// </summary>
public sealed class ClientSavedTagSelectionWriteService(HttpClient http)
    : ClientSavedTagSelectionReadService(http), ISavedTagSelectionWriteService
{
    public async Task<int> CreateAsync(SavedTagSelectionInput input)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/saved-tag-selections", input);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task UpdateAsync(int id, SavedTagSelectionInput input)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync($"api/saved-tag-selections/{id}", input);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task DeleteAsync(int id)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/saved-tag-selections/{id}");
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task<int> CopyPublicSelectionAsync(int sourceId)
    {
        HttpResponseMessage response =
            await Http.PostAsync($"api/saved-tag-selections/{sourceId}/copy", content: null);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Status-code → contract-exception translation (inverse of SavedTagSelectionEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                // SavedTagSelectionValidationException's ctor takes IReadOnlyList<string> errors, not a
                // single message — but ProblemDetails.Detail (ex.Message server-side) is already the
                // errors joined into one string (EndpointHelpers.ExecuteWriteAsync), so the original
                // per-error list can't be reconstructed. Wrap the joined detail as a single-element
                // list; callers that display "the" validation error see the same text either way.
                throw new SavedTagSelectionValidationException(
                    [detail ?? "The saved tag selection failed validation."]);
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("You must be the owner of this saved tag selection.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Saved tag selection not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
