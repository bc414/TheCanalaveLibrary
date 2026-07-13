using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IStoryLineageReadService"/> / <see cref="IStoryLineageWriteService"/>
/// (Feature 10, WU42). Thin pass-throughs: no business logic here — validation and the source/target
/// ownership gates live in <see cref="ServerStoryLineageWriteService"/> (single enforcement point).
/// Every write handler wraps in the shared <see cref="EndpointHelpers.ExecuteWriteAsync"/> for
/// exception→status translation (layer5-wasm.md §"The Error-Translation Contract").
/// <para>
/// Read auth: <see cref="IStoryLineageReadService.GetLineageForStoryAsync"/> is public — its own doc
/// comment says "for the public story-page display" (consumed by the public <c>StoryPage</c>, no
/// <c>[Authorize]</c>). <see cref="IStoryLineageReadService.GetLineageTypesAsync"/> is a seeded
/// lookup table (Inspired By / Prequel / Sequel / Companion Piece) — same public-lookup treatment as
/// <c>ITagReadService.GetTagDirectoryAsync</c>, not sensitive on its own even though its only
/// consumer today is the authenticated lineage-request form.
/// <see cref="IStoryLineageReadService.GetManageDataForUserAsync"/> is gated — its own doc comment
/// says "Requires an authenticated caller."
/// </para>
/// <para>
/// Write auth: <c>RequireAuthorization()</c> on every write — the interface's own doc comment says
/// "Every method requires an authenticated user," and each method additionally enforces ownership of
/// the relevant side of the link (source for request/delete, target for approve/reject) via
/// <see cref="UnauthorizedAccessException"/>, translated to 403. No <c>RequireRateLimiting(...)</c> —
/// <see cref="ServerStoryLineageWriteService"/> doesn't call an <c>IWriteRateLimitService</c> token
/// bucket.
/// </para>
/// </summary>
public static class StoryLineageEndpoints
{
    public static WebApplication MapStoryLineageEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/story-lineage");

        // ── Reads (see class summary for per-method auth rationale) ──

        group.MapGet("/by-story/{storyId:int}", async (IStoryLineageReadService lineage, int storyId) =>
            Results.Ok(await lineage.GetLineageForStoryAsync(storyId)));

        group.MapGet("/manage", async (IStoryLineageReadService lineage) =>
                Results.Ok(await lineage.GetManageDataForUserAsync()))
            .RequireAuthorization();

        group.MapGet("/types", async (IStoryLineageReadService lineage) =>
            Results.Ok(await lineage.GetLineageTypesAsync()));

        // ── Writes (authenticated — source/target ownership enforced by the service) ──

        group.MapPost("/", (IStoryLineageWriteService lineage, CreateStoryLineageDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await lineage.RequestLineageAsync(dto);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPost("/{sourceStoryId:int}/{targetStoryId:int}/{typeId}/approve",
                (IStoryLineageWriteService lineage, int sourceStoryId, int targetStoryId, short typeId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await lineage.ApproveLineageAsync(sourceStoryId, targetStoryId, typeId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapPost("/{sourceStoryId:int}/{targetStoryId:int}/{typeId}/reject",
                (IStoryLineageWriteService lineage, int sourceStoryId, int targetStoryId, short typeId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await lineage.RejectLineageAsync(sourceStoryId, targetStoryId, typeId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapDelete("/{sourceStoryId:int}/{targetStoryId:int}/{typeId}",
                (IStoryLineageWriteService lineage, int sourceStoryId, int targetStoryId, short typeId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await lineage.DeleteLineageAsync(sourceStoryId, targetStoryId, typeId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        return app;
    }
}
