using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ISiteSettingsReadService"/>: HttpClient wrapper over SiteSettingsEndpoints
/// (Server/SiteSettings/SiteSettingsEndpoints.cs). Same DTOs (a bare <c>int</c>), same method
/// contract — only the transport differs (the Layer-5 body-swap).
/// </summary>
public class ClientSiteSettingsReadService(HttpClient http) : ISiteSettingsReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<int> GetIntAsync(string settingKey, int fallback) =>
        await Http.GetFromJsonAsync<int>(
            $"api/site-settings/{Uri.EscapeDataString(settingKey)}?fallback={fallback}");
}
