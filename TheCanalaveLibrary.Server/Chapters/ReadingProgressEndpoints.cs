using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IReadingProgressWriteService"/> — the Feature-44 buffered
/// reading-progress ping (layer2-services.md §"Signal Buffering"; canonical pair with
/// <c>ViewCount*</c>). "Fast and dumb" buffered-signal ping endpoint (layer5-wasm.md §"API
/// Endpoint Organization"): the handler calls the same service method used everywhere else — its
/// body is an in-process buffer merge, no <c>DbContext</c> — and returns 202 Accepted rather than
/// running through <see cref="EndpointHelpers.ExecuteWriteAsync"/> (the service throws nothing;
/// anonymous callers silently no-op per the interface's own contract).
/// <para>
/// Deliberately NO <c>RequireAuthorization()</c> (MA-302, 2026-07-18): the interface's contract is
/// "anonymous viewers are silently ignored" — the service resolves the user from
/// <see cref="IActiveUserContext"/> and no-ops when it is null, so an anonymous caller can neither
/// write anything nor read anything here. The earlier <c>RequireAuthorization()</c> made every
/// anonymous WASM reader's scroll tick 401 → <c>HttpRequestException</c> out of the
/// <c>[JSInvokable]</c> scroll handler on the public reading page. Same anonymous-tolerant posture
/// as <c>ViewCountEndpoints</c>, its canonical buffered-signal pair.
/// </para>
/// </summary>
public static class ReadingProgressEndpoints
{
    public static WebApplication MapReadingProgressEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/reading-progress");

        group.MapPost("/",
            async (IReadingProgressWriteService readingProgress, int chapterId, float progress) =>
            {
                await readingProgress.RecordProgressAsync(chapterId, progress);
                return Results.Accepted();
            });

        return app;
    }
}
