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
/// <c>RequireAuthorization()</c> is applied per this sweep's explicit instruction (self-referential
/// signal, same authenticated-caller posture as chapter-read-marks). Note this is stricter than
/// the interface's documented "anonymous viewers are silently ignored" contract — that no-op path
/// only becomes unreachable over HTTP (an anonymous caller now gets 401 instead of a silent
/// no-op); it stays fully reachable today via the InteractiveServer circuit, which never crosses
/// this HTTP surface. Flagged for the eventual WASM flip / browser debug wave rather than resolved
/// here, since this is a mechanical add-without-verify pass.
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
                })
            .RequireAuthorization();

        return app;
    }
}
