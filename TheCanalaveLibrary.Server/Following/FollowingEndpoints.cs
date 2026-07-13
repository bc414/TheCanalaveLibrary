using Microsoft.AspNetCore.Mvc;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IFollowingReadService"/> / <see cref="IFollowingWriteService"/>
/// (Following + Vouches, Feature 19). Thin pass-throughs: no business logic or auth logic here — the
/// self-follow/self-vouch guards, the <see cref="FollowingConstants.MaxVouchesPerUser"/> limit, and
/// the incoming-vouches viewer-scoping all live in <c>ServerFollowingReadService</c>/
/// <c>ServerFollowingWriteService</c> (single enforcement point). Every write handler wraps in the
/// shared <see cref="EndpointHelpers.ExecuteWriteAsync"/> for exception→status translation
/// (layer5-wasm.md §"The Error-Translation Contract").
/// <para>
/// Read auth: <see cref="IFollowingReadService.GetRelationshipStateAsync"/>,
/// <see cref="IFollowingReadService.GetFollowedUsersAsync"/>, and
/// <see cref="IFollowingReadService.GetOutgoingVouchesAsync"/> are public — who follows whom and
/// outgoing vouches are public profile info (§5.8 display asymmetry), and
/// <c>GetRelationshipStateAsync</c> already degrades to a zero-state DTO for anonymous callers
/// rather than throwing. <see cref="IFollowingReadService.GetIncomingVouchesAsync"/> is gated with
/// <c>RequireAuthorization()</c> — it resolves the target user from the cookie
/// (<c>IActiveUserContext</c>), never a route parameter, so it is meaningless for an anonymous caller
/// and the incoming-vouches list is private to its recipient (§5.8) rather than mirroring a public
/// page.
/// </para>
/// <para>
/// Write auth: <c>RequireAuthorization()</c> on every write — every
/// <see cref="IFollowingWriteService"/> method requires an authenticated user
/// (<c>ServerFollowingWriteService.RequireAuthenticatedUser</c>). No <c>RequireRateLimiting(...)</c> —
/// unlike Tags, <c>ServerFollowingWriteService</c> doesn't call an <c>IWriteRateLimitService</c> token
/// bucket.
/// </para>
/// <para>
/// <b>Known EndpointHelpers mismatch (flagged, not fixed here — out of scope for this add-only
/// pass):</b> <c>FollowAsync</c>/<c>VouchAsync</c>'s self-target guards and
/// <c>SetReceiveAlertsAsync</c>'s "you don't follow this user" guard all throw
/// <see cref="InvalidOperationException"/> for genuine business-rule reasons, not because the caller
/// is unauthenticated — but the shared <see cref="EndpointHelpers.ExecuteWriteAsync"/> maps every
/// <see cref="InvalidOperationException"/> to 401 uniformly (its doc comment's stated assumption,
/// "every throw site is an ...requires an authenticated user guard," doesn't hold for these three
/// call sites). The message still crosses via <c>ProblemDetails.Detail</c>, so
/// <c>ClientFollowingWriteService</c> reads it through rather than losing it, but the HTTP status
/// itself (401) is semantically wrong for a self-follow/self-vouch/no-op-alert rejection (400 would
/// be more accurate). Left as-is per this sweep's mechanical, add-only scope.
/// </para>
/// </summary>
public static class FollowingEndpoints
{
    public static WebApplication MapFollowingEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/following");

        // ── Reads ──

        group.MapGet("/relationship/{targetUserId:int}",
            async (IFollowingReadService following, int targetUserId) =>
                Results.Ok(await following.GetRelationshipStateAsync(targetUserId)));

        group.MapGet("/{userId:int}", async (IFollowingReadService following, int userId) =>
            Results.Ok(await following.GetFollowedUsersAsync(userId)));

        group.MapGet("/vouches/outgoing/{userId:int}",
            async (IFollowingReadService following, int userId) =>
                Results.Ok(await following.GetOutgoingVouchesAsync(userId)));

        group.MapGet("/vouches/incoming", async (IFollowingReadService following) =>
                Results.Ok(await following.GetIncomingVouchesAsync()))
            .RequireAuthorization();

        // ── Writes (authenticated — see class summary for the EndpointHelpers 401 caveat) ──

        group.MapPost("/{targetUserId:int}", (IFollowingWriteService following, int targetUserId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await following.FollowAsync(targetUserId);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapDelete("/{targetUserId:int}", (IFollowingWriteService following, int targetUserId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await following.UnfollowAsync(targetUserId);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPut("/{targetUserId:int}/alerts",
                (IFollowingWriteService following, int targetUserId, bool receiveAlerts) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await following.SetReceiveAlertsAsync(targetUserId, receiveAlerts);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapPost("/vouches/{targetUserId:int}",
                (IFollowingWriteService following, int targetUserId, [FromBody] string? vouchText) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await following.VouchAsync(targetUserId, vouchText);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapDelete("/vouches/{targetUserId:int}",
                (IFollowingWriteService following, int targetUserId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await following.RemoveVouchAsync(targetUserId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        return app;
    }
}
