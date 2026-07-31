using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IStoryAcknowledgmentReadService"/> /
/// <see cref="IStoryAcknowledgmentWriteService"/> (WU-StatBadgeProducers). Thin pass-throughs: no
/// business logic here — validation and the story/recipient ownership gates live in
/// <see cref="ServerStoryAcknowledgmentWriteService"/> (single enforcement point). Every write
/// handler wraps in the shared <see cref="EndpointHelpers.ExecuteAsync"/> for exception→status
/// translation (layer5-wasm.md §"The Error-Translation Contract"). Mirrors
/// <see cref="StoryLineageEndpoints"/>'s shape.
/// <para>
/// Read auth: <see cref="IStoryAcknowledgmentReadService.GetAcknowledgmentsForStoryAsync"/> and
/// <see cref="IStoryAcknowledgmentReadService.GetAcknowledgmentRolesAsync"/> are public (story-page
/// display / seeded lookup — same treatment as the lineage equivalents).
/// <see cref="IStoryAcknowledgmentReadService.GetManageDataForUserAsync"/> is gated.
/// </para>
/// <para>
/// Write auth: <c>RequireAuthorization()</c> on every write; ownership/recipient-identity is
/// additionally enforced by the service via <see cref="UnauthorizedAccessException"/> (→ 403).
/// <c>Accept</c>/<c>Decline</c> take no recipient id — the service derives it from the authenticated
/// caller, so there is no id for a malicious caller to spoof.
/// </para>
/// </summary>
public static class StoryAcknowledgmentEndpoints
{
    public static WebApplication MapStoryAcknowledgmentEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/story-acknowledgments");

        // ── Reads ──

        group.MapGet("/by-story/{storyId:int}", async (IStoryAcknowledgmentReadService svc, int storyId) =>
            Results.Ok(await svc.GetAcknowledgmentsForStoryAsync(storyId)));

        group.MapGet("/manage", async (IStoryAcknowledgmentReadService svc) =>
                Results.Ok(await svc.GetManageDataForUserAsync()))
            .RequireAuthorization();

        group.MapGet("/roles", async (IStoryAcknowledgmentReadService svc) =>
            Results.Ok(await svc.GetAcknowledgmentRolesAsync()));

        // ── Writes (authenticated — ownership/recipient identity enforced by the service) ──

        group.MapPost("/", (IStoryAcknowledgmentWriteService svc, CreateStoryAcknowledgmentDto dto) =>
                EndpointHelpers.ExecuteAsync(async () =>
                {
                    await svc.RequestAcknowledgmentAsync(dto);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPost("/{storyId:int}/{roleId}/accept",
                (IStoryAcknowledgmentWriteService svc, int storyId, short roleId) =>
                    EndpointHelpers.ExecuteAsync(async () =>
                    {
                        await svc.AcceptAsync(storyId, roleId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapPost("/{storyId:int}/{roleId}/decline",
                (IStoryAcknowledgmentWriteService svc, int storyId, short roleId) =>
                    EndpointHelpers.ExecuteAsync(async () =>
                    {
                        await svc.DeclineAsync(storyId, roleId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapDelete("/{storyId:int}/{acknowledgedUserId:int}/{roleId}",
                (IStoryAcknowledgmentWriteService svc, int storyId, int acknowledgedUserId, short roleId) =>
                    EndpointHelpers.ExecuteAsync(async () =>
                    {
                        await svc.RevokeAsync(storyId, acknowledgedUserId, roleId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        return app;
    }
}
