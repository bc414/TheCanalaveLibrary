using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IDiscoveryFilterSettingsService"/>: HttpClient wrapper over
/// DiscoveryDefaultsEndpoints' authenticated sub-group (Server/Discovery/DiscoveryDefaultsEndpoints.cs).
/// No <c>userId</c> parameter ever crosses the HTTP boundary — the target user is resolved
/// server-side from the cookie, same pattern as <see cref="IUserSettingsService"/>.
/// </summary>
public sealed class ClientDiscoveryFilterSettingsService(HttpClient http) : IDiscoveryFilterSettingsService
{
    private HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<DiscoveryFilterModeDto>> GetMyMatrixAsync() =>
        await Http.GetFromJsonAsync<List<DiscoveryFilterModeDto>>("api/discovery-defaults/my-matrix") ?? [];

    public async Task SetOverrideAsync(string searchModeKey, string filterKey, bool isEnabled)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/discovery-defaults/my-matrix/{Uri.EscapeDataString(searchModeKey)}/{Uri.EscapeDataString(filterKey)}" +
            $"?isEnabled={(isEnabled ? "true" : "false")}",
            content: null);
        await ThrowIfFailedAsync(response);
    }

    private static Task ThrowIfFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, detail => new InvalidOperationException(detail));
}
