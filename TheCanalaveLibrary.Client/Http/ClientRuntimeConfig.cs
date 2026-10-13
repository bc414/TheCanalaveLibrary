using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// The WASM client's copy of the server's deploy-time settings (<see cref="ClientConfigDto"/>), loaded
/// once at boot from <c>GET /api/client-config</c> (layer5-wasm.md §"Server configuration the client
/// needs"; WU-ParityAndRemaining, 2026-10-13). Registered as a singleton; <c>Client/Program.cs</c> calls
/// <see cref="LoadAsync"/> after <c>builder.Build()</c> and before <c>RunAsync()</c>, and services that
/// need a value read it through a lazy factory, so nothing reads it before the load.
/// <para>Before this, <c>Client/Program.cs</c> hardcoded the sprite root, so the server's
/// <c>Sprites:BaseUrl</c> seam reached only the circuit and every WASM render would have kept the old
/// root at a CDN cutover (service audit §2.7.3).</para>
/// </summary>
public sealed class ClientRuntimeConfig
{
    /// <summary>The sprite root (<c>Sprites:BaseUrl</c> on the server). The built-in default until
    /// <see cref="LoadAsync"/> succeeds.</summary>
    public string SpriteBaseUrl { get; private set; } = ClientConfigDto.DefaultSpriteBaseUrl;

    /// <summary>
    /// Fetches the server's settings and adopts them. A failure — a non-2xx answer, a network error, an
    /// unreadable or empty body — is logged at Warning and leaves the built-in defaults in place: the
    /// app still boots and renders sprites from the default root, which is correct for every deploy
    /// that has not moved them (logging.md: observed, never a silent catch).
    /// </summary>
    public async Task LoadAsync(HttpClient http, ILogger logger)
    {
        try
        {
            ClientConfigDto? config = await http.GetFromJsonAsync<ClientConfigDto>("api/client-config");
            if (config is null || string.IsNullOrWhiteSpace(config.SpriteBaseUrl))
            {
                logger.LogWarning(
                    "Client config response was empty; keeping the default sprite root {SpriteBaseUrl}",
                    SpriteBaseUrl);
                return;
            }
            SpriteBaseUrl = config.SpriteBaseUrl;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Loading the client config failed; keeping the default sprite root {SpriteBaseUrl}",
                SpriteBaseUrl);
        }
    }
}
