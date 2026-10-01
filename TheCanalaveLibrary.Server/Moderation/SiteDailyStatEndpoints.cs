using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="ISiteDailyStatReadService"/> (Feature 62), backing the
/// <c>/mod/stats</c> dashboard (<c>ModStatsPage.razor</c>, <c>[Authorize(Roles = "Moderator,Admin")]</c>).
/// Read-only, no write counterpart. Thin pass-through: no business logic here.
/// <para>
/// <b>Auth.</b> Defense in depth: <c>ServerSiteDailyStatReadService</c> gates both reads with the
/// shared <c>RequireModerator()</c> (owner ruling D9's sweep, WU-ModerationIntegrity 2026-09-30 — the
/// circuit has no endpoint, so the service is the enforcement point of record), and this group carries
/// the named <see cref="AuthorizationPolicies.RequireModerator"/> policy as the edge half (MA-702,
/// 2026-07-18). Both handlers wrap in <see cref="EndpointHelpers.ExecuteAsync"/>, since the service can
/// now throw (layer5-wasm.md: every handler, write or read, whose service can throw).
/// </para>
/// <para>
/// <c>CancellationToken</c> parameters are dropped at the client boundary per layer5-wasm.md's
/// "CancellationToken parameters" note — the endpoint binds ASP.NET's own request-aborted token;
/// the client impl never threads one through.
/// </para>
/// </summary>
public static class SiteDailyStatEndpoints
{
    public static WebApplication MapSiteDailyStatEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/site-daily-stats");

        group.MapGet("/latest", (ISiteDailyStatReadService stats, HttpContext http) =>
                EndpointHelpers.ExecuteAsync(async () =>
                    Results.Json(await stats.GetLatestAsync(http.RequestAborted))))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        group.MapGet("/series", (ISiteDailyStatReadService stats, int days, HttpContext http) =>
                EndpointHelpers.ExecuteAsync(async () =>
                    Results.Ok(await stats.GetSeriesAsync(days, http.RequestAborted))))
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        return app;
    }
}
