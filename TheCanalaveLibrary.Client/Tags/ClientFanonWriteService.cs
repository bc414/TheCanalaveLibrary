using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IFanonWriteService"/> over FanonEndpoints. Mirrors the server inheritance
/// (write : read) and rethrows the same typed exceptions from status codes, so pages'
/// catch-and-display works identically in both render modes.
/// </summary>
public class ClientFanonWriteService(HttpClient http) : ClientFanonReadService(http), IFanonWriteService
{
    public async Task<int> LinkGroupAsync(FanonLinkCreateDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/fanon/links", dto);
        await ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new TagValidationException(msg));
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task<int> NotifyNewAuthorsAsync(string name, int baseTagId)
    {
        HttpResponseMessage response = await Http.PostAsync(
            $"api/fanon/links/notify?name={Uri.EscapeDataString(name)}&baseTagId={baseTagId}", null);
        await ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new TagValidationException(msg));
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task<AdoptResultDto> AdoptAsync(int targetTagId, int storyId)
    {
        HttpResponseMessage response = await Http.PostAsync($"api/fanon/adopt/{targetTagId}/story/{storyId}", null);
        await ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new TagValidationException(msg));
        return await response.Content.ReadFromJsonAsync<AdoptResultDto>() ?? new AdoptResultDto(0, 0);
    }

    public async Task<AdoptResultDto> AdoptAllAsync(int targetTagId)
    {
        HttpResponseMessage response = await Http.PostAsync($"api/fanon/adopt/{targetTagId}/all", null);
        await ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new TagValidationException(msg));
        return await response.Content.ReadFromJsonAsync<AdoptResultDto>() ?? new AdoptResultDto(0, 0);
    }

    public async Task SetDismissedAsync(int targetTagId, bool dismissed)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/fanon/adopt/{targetTagId}/dismissed?dismissed={(dismissed ? "true" : "false")}", null);
        await ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new TagValidationException(msg));
    }
}
