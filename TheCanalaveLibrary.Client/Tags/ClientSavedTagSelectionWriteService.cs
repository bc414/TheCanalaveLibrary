using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ISavedTagSelectionWriteService"/>. Inherits the read impl (CQRS-lite),
/// mirroring <see cref="ServerSavedTagSelectionWriteService"/> : <see cref="ServerSavedTagSelectionReadService"/>.
/// Auth rides the same-origin Identity cookie — WASM's fetch-backed HttpClient sends it automatically.
/// <para>
/// Translates SavedTagSelectionEndpoints' status codes back into the service contract's typed
/// exceptions — the shared MA-008 shape (<see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>);
/// 400 reconstructs <see cref="SavedTagSelectionValidationException"/>.
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

    /// <summary>Status-code → contract-exception translation (inverse of SavedTagSelectionEndpoints')
    /// — the shared MA-008 shape.</summary>
    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response,
            msg => new SavedTagSelectionValidationException([msg]));
}
