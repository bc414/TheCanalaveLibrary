using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="ICoOccurrenceReadService"/>. Thin pass-throughs — no
/// business logic here; the viewer's rating filter is resolved internally from
/// <see cref="IActiveUserContext"/>, so nothing sensitive crosses the HTTP boundary. Public reads:
/// Also Favorited / Also Recommended are public story-page strips — no auth gate, same treatment
/// as <see cref="ITagReadService"/>'s public reads.
///
/// <para>POST, not GET: <see cref="CoOccurrenceRequest.ExcludedInteractions"/> is an optional
/// array sibling to scalar params, which isn't GET-bindable (<c>layer5-wasm.md</c> "Reads with
/// non-scalar parameters" — WU-RelatedStories added the array to let a live
/// <c>UserStoryInteractionFilter</c> override the server-resolved §8.7 default).</para>
/// </summary>
public static class CoOccurrenceEndpoints
{
    public static WebApplication MapCoOccurrenceEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/co-occurrence");

        group.MapPost("/also-favorited", async (
                ICoOccurrenceReadService coOccurrence, CoOccurrenceRequest request, CancellationToken ct) =>
            Results.Ok(await coOccurrence.GetAlsoFavoritedAsync(
                request.StoryId, request.Take, request.ExcludedInteractions, ct)));

        group.MapPost("/also-recommended", async (
                ICoOccurrenceReadService coOccurrence, CoOccurrenceRequest request, CancellationToken ct) =>
            Results.Ok(await coOccurrence.GetAlsoRecommendedAsync(
                request.StoryId, request.Take, request.ExcludedInteractions, ct)));

        return app;
    }
}
