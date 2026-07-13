using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IDiscoveryDefaultsReadService"/>: HttpClient wrapper over
/// DiscoveryDefaultsEndpoints (Server/Discovery/DiscoveryDefaultsEndpoints.cs). Read-only, no
/// matching write service — one client class, no read/write inheritance split (layer5-wasm.md
/// §"Client Service Implementations"). No <c>userId</c> parameter ever crosses the HTTP boundary —
/// the target viewer is resolved server-side from the cookie, same pattern as
/// <see cref="IUserSettingsService"/>.
/// </summary>
public class ClientDiscoveryDefaultsReadService(HttpClient http) : IDiscoveryDefaultsReadService
{
    private HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<UserStoryInteractionTypeEnum>> GetDefaultExcludedInteractionsAsync(
        string searchModeKey) =>
        await Http.GetFromJsonAsync<List<UserStoryInteractionTypeEnum>>(
            $"api/discovery-defaults?searchModeKey={Uri.EscapeDataString(searchModeKey)}") ?? [];
}
