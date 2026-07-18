using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ICustomListWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// <c>ServerCustomListWriteService</c> : <c>ServerCustomListReadService</c>. Translates
/// <c>CustomListEndpoints</c>' status codes back into the service contract's typed exceptions —
/// the shared MA-008 shape (<see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>); 400
/// reconstructs <see cref="CustomListValidationException"/>.
/// </summary>
public sealed class ClientCustomListWriteService(HttpClient http)
    : ClientCustomListReadService(http), ICustomListWriteService
{
    public async Task<int> CreateListAsync(string listName, bool isPublic)
    {
        HttpResponseMessage response = await Http.PostAsync(
            $"api/custom-lists?listName={Uri.EscapeDataString(listName)}&isPublic={isPublic}",
            content: null);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task RenameListAsync(int listId, string newListName)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/custom-lists/{listId}/name?newName={Uri.EscapeDataString(newListName)}",
            content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task SetListVisibilityAsync(int listId, bool isPublic)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/custom-lists/{listId}/visibility?isPublic={isPublic}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task DeleteListAsync(int listId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/custom-lists/{listId}");
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task AddStoryAsync(int listId, int storyId)
    {
        HttpResponseMessage response = await Http.PostAsync(
            $"api/custom-lists/{listId}/stories/{storyId}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task RemoveStoryAsync(int listId, int storyId)
    {
        HttpResponseMessage response = await Http.DeleteAsync(
            $"api/custom-lists/{listId}/stories/{storyId}");
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task<int> CloneListAsync(int sourceListId)
    {
        HttpResponseMessage response = await Http.PostAsync(
            $"api/custom-lists/{sourceListId}/clone", content: null);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    /// <summary>Status-code → contract-exception translation (inverse of CustomListEndpoints') — the
    /// shared MA-008 shape.</summary>
    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new CustomListValidationException([msg]));
}
