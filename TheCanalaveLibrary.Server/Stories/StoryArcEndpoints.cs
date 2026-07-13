using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IStoryArcReadService"/> / <see cref="IStoryArcWriteService"/>
/// (Feature 8, WU45). Thin pass-throughs: no business logic here — the range/title validation and
/// the author-only gate live in <see cref="ServerStoryArcWriteService"/> (single enforcement point).
/// Every write handler wraps in the shared <see cref="EndpointHelpers.ExecuteWriteAsync"/> for
/// exception→status translation (layer5-wasm.md §"The Error-Translation Contract").
/// <para>
/// Read auth: public. <see cref="IStoryArcReadService.GetArcsForStoryAsync"/> feeds
/// <c>StoryArcManagerPanel</c> and the reader-facing chapter navigation on the public
/// <c>StoryPage</c> (<c>/story/{StoryId:int}/{*StorySlug}</c>, no <c>[Authorize]</c>) — same
/// public-read rule as <c>TagEndpoints</c>.
/// </para>
/// <para>
/// Write auth: <c>RequireAuthorization()</c> on every write — every
/// <see cref="IStoryArcWriteService"/> method requires an authenticated user
/// (<c>ServerStoryArcWriteService.RequireAuthenticatedUser</c>, throws
/// <see cref="InvalidOperationException"/> otherwise, translated to 401), and additionally enforces
/// story-author ownership via <see cref="UnauthorizedAccessException"/> (403). No
/// <c>RequireRateLimiting(...)</c> — <see cref="ServerStoryArcWriteService"/> doesn't call an
/// <c>IWriteRateLimitService</c> token bucket.
/// </para>
/// </summary>
public static class StoryArcEndpoints
{
    public static WebApplication MapStoryArcEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/story-arcs");

        // ── Reads (public — see class summary) ──

        group.MapGet("/by-story/{storyId:int}", async (IStoryArcReadService arcs, int storyId) =>
            Results.Ok(await arcs.GetArcsForStoryAsync(storyId)));

        // ── Writes (authenticated — author ownership enforced by the service, translated here) ──

        group.MapPost("/", (IStoryArcWriteService arcs, CreateStoryArcDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await arcs.CreateArcAsync(dto))))
            .RequireAuthorization();

        group.MapPut("/{storyArcId:int}", (IStoryArcWriteService arcs, int storyArcId, UpdateStoryArcDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    storyArcId != dto.StoryArcId
                        ? Results.Problem(detail: "Route storyArcId does not match body StoryArcId.",
                            statusCode: StatusCodes.Status400BadRequest)
                        : await UpdateAndRespondAsync(arcs, dto)))
            .RequireAuthorization();

        group.MapDelete("/{storyArcId:int}", (IStoryArcWriteService arcs, int storyArcId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await arcs.DeleteArcAsync(storyArcId);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> UpdateAndRespondAsync(IStoryArcWriteService arcs, UpdateStoryArcDto dto)
    {
        await arcs.UpdateArcAsync(dto);
        return Results.NoContent();
    }
}
