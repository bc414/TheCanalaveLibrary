using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IDiscoveryDefaultsReadService"/>. Thin pass-through — no
/// business logic here. Public read: the effective default-exclusion set is resolved from the
/// active viewer's cookie internally (anonymous viewers get the system defaults only, per the
/// interface doc comment) — no user id ever crosses the HTTP boundary, so this is a public read
/// like <see cref="ITagReadService"/>'s.
/// </summary>
public static class DiscoveryDefaultsEndpoints
{
    public static WebApplication MapDiscoveryDefaultsEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/discovery-defaults");

        // Single scalar parameter (a SiteSearchModes constant string) — plain GET, query-bound.
        group.MapGet("/", async (IDiscoveryDefaultsReadService discoveryDefaults, string searchModeKey) =>
            Results.Ok(await discoveryDefaults.GetDefaultExcludedInteractionsAsync(searchModeKey)));

        return app;
    }
}
