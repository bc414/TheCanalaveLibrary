using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="ISpotlightReadService"/> / <see cref="ISpotlightWriteService"/>
/// (Feature 55, WU-Spotlight). Thin pass-throughs: no business logic here — validation and the
/// block-capacity/cooldown/ownership checks live in the service (single enforcement point). The
/// endpoint's only added job is exception→status translation via the shared
/// <see cref="EndpointHelpers.ExecuteWriteAsync"/> (layer5-wasm.md §"The Error-Translation
/// Contract").
/// <para>
/// <see cref="ISpotlightReadService.GetActiveSpotlightsAsync"/> is public — it backs the homepage
/// <c>CommunitySpotlightDisplay</c> section, which carries no <c>[Authorize]</c>. Every other read
/// (<c>GetMyAvailableSlotsAsync</c>, <c>GetMyBookingsAsync</c>, <c>GetMyPickCandidatesAsync</c>,
/// <c>GetBlockAvailabilityAsync</c>) and the one write (<c>RedeemSlotAsync</c>) back only
/// <c>SpotlightRedemptionPage</c> (<c>[Authorize]</c>), so they carry <c>RequireAuthorization()</c>
/// as the floor — mod-only slot-granting/scheduling isn't exposed here at all (that's
/// <see cref="ISpotlightSlotAllocator"/>, not yet part of the WASM-add pass); redemption's own
/// ownership/eligibility checks remain the enforcement point, this gate just keeps the failure mode
/// a clean 401 from the cookie handler.
/// </para>
/// </summary>
public static class SpotlightEndpoints
{
    public static WebApplication MapSpotlightEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/spotlight");

        // ── Reads ──

        // Public — mirrors the public homepage Community Spotlight section.
        group.MapGet("/active", async (ISpotlightReadService spotlights) =>
            Results.Ok(await spotlights.GetActiveSpotlightsAsync()));

        // Auth-only — every read below backs only the [Authorize] redemption page.
        group.MapGet("/my-slots", async (ISpotlightReadService spotlights) =>
                Results.Ok(await spotlights.GetMyAvailableSlotsAsync()))
            .RequireAuthorization();

        group.MapGet("/my-bookings", async (ISpotlightReadService spotlights) =>
                Results.Ok(await spotlights.GetMyBookingsAsync()))
            .RequireAuthorization();

        group.MapGet("/my-pick-candidates", async (ISpotlightReadService spotlights) =>
                Results.Ok(await spotlights.GetMyPickCandidatesAsync()))
            .RequireAuthorization();

        group.MapGet("/blocks", async (ISpotlightReadService spotlights) =>
                Results.Ok(await spotlights.GetBlockAvailabilityAsync()))
            .RequireAuthorization();

        // ── Writes ──

        group.MapPost("/redeem", (ISpotlightWriteService spotlights, RedeemSpotlightSlotDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await spotlights.RedeemSlotAsync(dto);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        return app;
    }
}
