using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IDiscoveryDefaultsReadService"/> and
/// <see cref="IDiscoveryFilterSettingsService"/>. Thin pass-throughs — no business logic here.
/// <para>
/// <b>Public read (unchanged).</b> The effective default-exclusion set is resolved from the
/// active viewer's cookie internally (anonymous viewers get the system defaults only, per the
/// interface doc comment) — no user id ever crosses the HTTP boundary, so this is a public read
/// like <see cref="ITagReadService"/>'s.
/// </para>
/// <para>
/// <b>Authenticated sub-group (WU-DiscoveryOverrideUI).</b> <c>/my-matrix</c> is the settings-page
/// read+write surface — deliberately not folded into the public <c>GET /</c> above, which stays a
/// pure anonymous-safe read. Writes go through <see cref="EndpointHelpers.ExecuteAsync"/> for
/// exception→status translation, same as <c>NotificationEndpoints</c>' settings write.
/// </para>
/// </summary>
public static class DiscoveryDefaultsEndpoints
{
    public static WebApplication MapDiscoveryDefaultsEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/discovery-defaults");

        // Single scalar parameter (a SiteSearchModes constant string) — plain GET, query-bound.
        group.MapGet("/", async (IDiscoveryDefaultsReadService discoveryDefaults, string searchModeKey) =>
            Results.Ok(await discoveryDefaults.GetDefaultExcludedInteractionsAsync(searchModeKey)));

        // ── Authenticated: the current user's own override matrix ──
        RouteGroupBuilder myMatrix = group.MapGroup("/my-matrix").RequireAuthorization();

        myMatrix.MapGet("/", (IDiscoveryFilterSettingsService filterSettings) =>
            EndpointHelpers.ExecuteAsync(async () =>
                Results.Ok(await filterSettings.GetMyMatrixAsync())));

        myMatrix.MapPut("/{searchModeKey}/{filterKey}", (
                IDiscoveryFilterSettingsService filterSettings,
                string searchModeKey,
                string filterKey,
                bool isEnabled) =>
            EndpointHelpers.ExecuteAsync(async () =>
            {
                await filterSettings.SetOverrideAsync(searchModeKey, filterKey, isEnabled);
                return Results.NoContent();
            }));

        return app;
    }
}
