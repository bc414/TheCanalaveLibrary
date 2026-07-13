using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="ISiteSettingsReadService"/> / <see cref="ISiteSettingsWriteService"/>
/// (WU-Spotlight's cross-cutting settings cluster). Thin pass-through: no business logic here — the
/// mod gate for writes lives in <c>ServerSiteSettingsWriteService.RequireModerator</c> (single
/// enforcement point). The write handler wraps in the shared
/// <see cref="EndpointHelpers.ExecuteWriteAsync"/> for exception→status translation
/// (layer5-wasm.md §"The Error-Translation Contract").
/// <para>
/// <b>Read auth.</b> <c>ServerSiteSettingsReadService</c> performs no role check of its own (plain
/// single-row PK lookup) — same shape as <see cref="SiteDailyStatEndpoints"/>. Today the only
/// component that injects <see cref="ISiteSettingsReadService"/>/<see cref="ISiteSettingsWriteService"/>
/// directly is <c>ModSpotlightPage</c> (<c>[Authorize(Roles = "Moderator,Admin")]</c>) — every
/// public-facing consumer (e.g. <c>ServerSpotlightReadService</c>) reaches settings through its own
/// server-side service composition, never over this HTTP surface. Per identity-and-authorization.md's
/// "Endpoint-level is the actual security boundary — it does not inherit from the page," this read
/// carries an inline role requirement here (mirroring <c>ModSpotlightPage</c>'s own gate) rather than
/// a plain <c>RequireAuthorization()</c> floor that would let any signed-in non-mod user read raw
/// setting values over HTTP. Revisit if a future public page needs a direct client read.
/// </para>
/// <para>
/// <b>Write auth.</b> <c>RequireAuthorization()</c> floor — the service's own
/// <c>RequireModerator()</c> throws <see cref="UnauthorizedAccessException"/> for a non-mod caller,
/// which <see cref="EndpointHelpers.ExecuteWriteAsync"/> maps to 403.
/// </para>
/// </summary>
public static class SiteSettingsEndpoints
{
    /// <summary>Mirrors <c>ModSpotlightPage</c>'s own gate — see class doc's "Read auth" paragraph.</summary>
    private static readonly AuthorizeAttribute ModeratorOnly = new() { Roles = "Moderator,Admin" };

    public static WebApplication MapSiteSettingsEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/site-settings");

        group.MapGet("/{settingKey}",
                async (ISiteSettingsReadService settings, string settingKey, int fallback) =>
                    Results.Ok(await settings.GetIntAsync(settingKey, fallback)))
            .RequireAuthorization(ModeratorOnly);

        // [FromBody] pins the bare int to the JSON body (layer5-wasm.md — minimal APIs don't bind a
        // plain scalar from the body implicitly; mirrors GroupEndpoints'/FollowingEndpoints' pattern).
        group.MapPost("/{settingKey}",
                (ISiteSettingsWriteService settings, string settingKey, [FromBody] int value) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await settings.SetIntAsync(settingKey, value);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        return app;
    }
}
