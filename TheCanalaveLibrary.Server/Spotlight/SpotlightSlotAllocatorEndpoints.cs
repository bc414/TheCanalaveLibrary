using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 surface for <see cref="ISpotlightSlotAllocator"/> (Global Flip — surfaced by the flip
/// checklist's client-registration sweep: <c>ModSpotlightPage</c> injects the allocator directly,
/// so it needs an HTTP body-swap like any other component-injected service). Thin pass-throughs;
/// <c>ServerSpotlightSlotAllocator.RequireModerator()</c> is the enforcement point
/// (<c>UnauthorizedAccessException</c> → 403 via the shared helper). The whole group additionally
/// carries the Moderator/Admin role gate via the named
/// <see cref="AuthorizationPolicies.RequireModerator"/> policy (registered in <c>Program.cs</c>;
/// MA-702 fix, 2026-07-18 — replaces the earlier inline <c>AuthorizeAttribute</c>), mirroring
/// <c>ModSpotlightPage</c>'s own <c>[Authorize(Roles = "Moderator,Admin")]</c>.
/// </summary>
public static class SpotlightSlotAllocatorEndpoints
{
    public static WebApplication MapSpotlightSlotAllocatorEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/spotlight-slots")
            .RequireAuthorization(AuthorizationPolicies.RequireModerator);

        // Scalar + enum params bind from the query — no body needed (PollEndpoints' vote precedent).
        // maxStoryRating: E (default) = non-M slot, M = Mature-pool slot (WU-AccessGate).
        group.MapPost("/", (ISpotlightSlotAllocator allocator, int toUserId, SpotlightSlotSource source,
                Rating maxStoryRating = Rating.E) =>
            EndpointHelpers.ExecuteWriteAsync(async () =>
                Results.Ok(await allocator.GrantSlotAsync(toUserId, source, maxStoryRating))));

        group.MapDelete("/{slotId:int}", (ISpotlightSlotAllocator allocator, int slotId) =>
            EndpointHelpers.ExecuteWriteAsync(async () =>
            {
                await allocator.RevokeSlotAsync(slotId);
                return Results.NoContent();
            }));

        group.MapGet("/remaining-capacity", async (ISpotlightSlotAllocator allocator) =>
            Results.Ok(await allocator.GetRemainingMonthlyGrantCapacityAsync()));

        group.MapGet("/recent-grants", async (ISpotlightSlotAllocator allocator, int take) =>
            Results.Ok(await allocator.GetRecentGrantsAsync(take)));

        return app;
    }
}
