using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ISiteSettingsWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerSiteSettingsWriteService : ServerSiteSettingsReadService. Auth rides the same-origin
/// Identity cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates SiteSettingsEndpoints' status codes back into the service contract's typed exceptions:
/// 401/403 → <see cref="UnauthorizedAccessException"/> (the service's own <c>RequireModerator</c>
/// denial — this is the only failure mode <c>SetIntAsync</c> documents).
/// </para>
/// </summary>
public sealed class ClientSiteSettingsWriteService(HttpClient http)
    : ClientSiteSettingsReadService(http), ISiteSettingsWriteService
{
    public async Task SetIntAsync(string settingKey, int value)
    {
        HttpResponseMessage response =
            await Http.PostAsJsonAsync($"api/site-settings/{Uri.EscapeDataString(settingKey)}", value);
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of SiteSettingsEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new UnauthorizedAccessException(detail ?? "This operation requires a moderator.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
