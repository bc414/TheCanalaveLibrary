using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IUserActivityWriteService"/> (WU-SiteDailyStat, Feature 62 L2 —
/// buffered "last active" ping). Buffered-signal ping endpoint (layer5-wasm.md §"Buffered-signal ping
/// endpoints") — the handler calls the same <see cref="IUserActivityWriteService.RecordActivityAsync"/>
/// method as <c>UserActivityTracker.razor</c>'s server-circuit call; its server body is already an
/// in-process buffer merge (<see cref="UserActivityBuffer"/>, no <c>DbContext</c>), so the endpoint
/// returns <c>202 Accepted</c>.
/// <para>
/// Trust decision: <see cref="IUserActivityWriteService.RecordActivityAsync"/> takes an explicit
/// <c>int userId</c>, but a WASM caller must never be able to record activity for an arbitrary other
/// user. This endpoint takes NO userId from the route or body — it resolves the caller's own id from
/// <see cref="IActiveUserContext.UserId"/> server-side and calls the service with that (same
/// self-referential treatment as <see cref="IUserSettingsService"/>, layer5-wasm.md §"Client Service
/// Implementations" "Self-referential services"). <c>RequireAuthorization()</c> guarantees an
/// authenticated caller; if <c>UserId</c> is somehow still null, the ping is silently a no-op rather
/// than an error — a best-effort buffered signal has nothing useful to report as a failure.
/// </para>
/// </summary>
public static class UserActivityEndpoints
{
    public static WebApplication MapUserActivityEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/user-activity");

        group.MapPost("/", (IUserActivityWriteService activity, IActiveUserContext activeUser) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    if (activeUser.UserId is int userId)
                    {
                        await activity.RecordActivityAsync(userId);
                    }

                    return Results.StatusCode(StatusCodes.Status202Accepted);
                }))
            .RequireAuthorization();

        return app;
    }
}
