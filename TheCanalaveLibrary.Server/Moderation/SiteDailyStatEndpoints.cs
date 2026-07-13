using Microsoft.AspNetCore.Authorization;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="ISiteDailyStatReadService"/> (Feature 62), backing the
/// <c>/mod/stats</c> dashboard (<c>ModStatsPage.razor</c>, <c>[Authorize(Roles = "Moderator,Admin")]</c>).
/// Read-only, no write counterpart. Thin pass-through: no business logic here.
/// <para>
/// <b>Auth.</b> <c>ServerSiteDailyStatReadService</c> performs no role check of its own (plain LINQ
/// over the read context) — same shape as <see cref="ModerationEndpoints"/>'s mod-only reads. Per
/// identity-and-authorization.md's "Endpoint-level is the actual security boundary — it does not
/// inherit from the page," an inline role requirement is applied here
/// (<c>new AuthorizeAttribute { Roles = "Moderator,Admin" }</c>) mirroring <c>ModStatsPage</c>'s own
/// gate, rather than a plain <c>RequireAuthorization()</c> floor that would let any signed-in
/// non-mod user read site-wide aggregate stats over HTTP. No named "RequireModerator" policy is
/// registered in <c>Program.cs</c> (untouched by this pass), so the role requirement is inlined via
/// the <see cref="AuthorizeAttribute"/> overload of <c>RequireAuthorization</c>.
/// </para>
/// <para>
/// <c>CancellationToken</c> parameters are dropped at the client boundary per layer5-wasm.md's
/// "CancellationToken parameters" note — the endpoint binds ASP.NET's own request-aborted token;
/// the client impl never threads one through.
/// </para>
/// </summary>
public static class SiteDailyStatEndpoints
{
    private static readonly AuthorizeAttribute ModeratorOnly = new() { Roles = "Moderator,Admin" };

    public static WebApplication MapSiteDailyStatEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/site-daily-stats");

        group.MapGet("/latest", async (ISiteDailyStatReadService stats, HttpContext http) =>
                Results.Json(await stats.GetLatestAsync(http.RequestAborted)))
            .RequireAuthorization(ModeratorOnly);

        group.MapGet("/series", async (ISiteDailyStatReadService stats, int days, HttpContext http) =>
                Results.Ok(await stats.GetSeriesAsync(days, http.RequestAborted)))
            .RequireAuthorization(ModeratorOnly);

        return app;
    }
}
