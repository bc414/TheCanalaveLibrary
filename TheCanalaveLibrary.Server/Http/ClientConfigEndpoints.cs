using Microsoft.AspNetCore.Mvc;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// <c>GET /api/client-config</c>: the deploy-time settings the WASM client must honor
/// (<see cref="ClientConfigDto"/>; layer5-wasm.md §"Server configuration the client needs";
/// WU-ParityAndRemaining, 2026-10-13). The client fetches it once at boot, before <c>RunAsync</c>.
/// <para><b>Anonymous on purpose.</b> Every value here is a public deploy fact the browser learns anyway
/// (the sprite root is in every rendered <c>img src</c>), and the WASM host fetches it before any
/// sign-in state matters. Nothing user-specific or secret may ever be added to the record.</para>
/// <para>It returns the registered singleton the server's own consumers read, never a second
/// configuration read, so the circuit and WASM cannot disagree.</para>
/// </summary>
public static class ClientConfigEndpoints
{
    public const string Route = "/api/client-config";

    public static WebApplication MapClientConfigEndpoints(this WebApplication app)
    {
        app.MapGet(Route, ([FromServices] ClientConfigDto config) => Results.Ok(config));
        return app;
    }
}
