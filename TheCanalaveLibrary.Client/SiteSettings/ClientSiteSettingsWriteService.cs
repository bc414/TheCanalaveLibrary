using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ISiteSettingsWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerSiteSettingsWriteService : ServerSiteSettingsReadService. Auth rides the same-origin
/// Identity cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Delegates the standard status-code mapping to
/// <see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/> — the service's own
/// <c>RequireModerator</c> denial (401/403) is the only failure mode <c>SetIntAsync</c> documents,
/// so the validation factory is defensive-only.
/// </para>
/// </summary>
public sealed class ClientSiteSettingsWriteService(HttpClient http)
    : ClientSiteSettingsReadService(http), ISiteSettingsWriteService
{
    public async Task SetIntAsync(string settingKey, int value)
    {
        HttpResponseMessage response =
            await Http.PostAsJsonAsync($"api/site-settings/{Uri.EscapeDataString(settingKey)}", value);
        await ClientHttpHelpers.ThrowIfWriteFailedAsync(
            response, detail => new InvalidOperationException(detail));
    }
}
