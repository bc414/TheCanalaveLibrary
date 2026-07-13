using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IViewCountWriteService"/> (Feature 45). Buffered-signal
/// "ping" endpoint (layer5-wasm.md §"Buffered-signal ping endpoints") — the handler calls the same
/// <see cref="IViewCountWriteService.RecordViewAsync"/> method as everywhere else; its server body
/// is already an in-process buffer merge (no <c>DbContext</c>), so the endpoint returns
/// <c>202 Accepted</c> rather than <c>200 Ok</c>/<c>204 NoContent</c>.
/// <para>
/// No auth gate — the interface's own doc comment is explicit: "Anonymous viewers count
/// (deliberately no auth gate)." Still wrapped in <see cref="EndpointHelpers.ExecuteWriteAsync"/> for
/// consistency with every other write handler, even though the buffer merge itself is not expected
/// to throw. No <c>RequireRateLimiting(...)</c> — a view ping is loss-tolerant and unbounded by
/// design (spec: "a view is a view"), not a token-bucket-guarded content write.
/// </para>
/// </summary>
public static class ViewCountEndpoints
{
    public static WebApplication MapViewCountEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/view-counts");

        group.MapPost("/{storyId:int}", (IViewCountWriteService viewCounts, int storyId) =>
            EndpointHelpers.ExecuteWriteAsync(async () =>
            {
                await viewCounts.RecordViewAsync(storyId);
                return Results.StatusCode(StatusCodes.Status202Accepted);
            }));

        return app;
    }
}
