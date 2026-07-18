using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ITagWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerTagWriteService : ServerTagReadService. Auth rides the same-origin Identity cookie —
/// WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates TagEndpoints' status codes back into the service contract's typed exceptions so
/// components behave identically on either side of the interface — the shared MA-008 shape
/// (<see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>); 400 reconstructs
/// TagValidationException (TagDirectoryDesktop shows its message inline).
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
        // Body is the raw sprite-warning string — or EMPTY when the service returned null
        // (HttpResultsHelper writes nothing for a null result value; Global Flip wave finding).
        return await ClientHttpHelpers.ReadNullableFromJsonAsync<string?>(response.Content);
    }

    public async Task DeleteTagAsync(int tagId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/tags/{tagId}");
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of TagEndpoints') — the
    /// shared MA-008 shape.</summary>
    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new TagValidationException(msg));
}
