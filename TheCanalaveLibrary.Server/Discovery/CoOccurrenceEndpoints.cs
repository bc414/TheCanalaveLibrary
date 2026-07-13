using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="ICoOccurrenceReadService"/>. Thin pass-throughs — no
/// business logic here; the viewer's rating/interaction filters are resolved internally from
/// <see cref="IActiveUserContext"/>, so nothing sensitive crosses the HTTP boundary. Public reads:
/// Also Favorited / Also Recommended are public story-page strips — no auth gate, same treatment
/// as <see cref="ITagReadService"/>'s public reads.
/// </summary>
public static class CoOccurrenceEndpoints
{
    public static WebApplication MapCoOccurrenceEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/co-occurrence");

        // Both params are scalars — plain GET, query-bound. `take`'s C# default (10) mirrors the
        // service's own default — ASP.NET Core minimal APIs treat a delegate parameter with a
        // default value as optional, so an omitted query value binds identically to a direct
        // service call.
        group.MapGet("/also-favorited", async (
                ICoOccurrenceReadService coOccurrence, int storyId, int take = 10, CancellationToken ct = default) =>
            Results.Ok(await coOccurrence.GetAlsoFavoritedAsync(storyId, take, ct)));

        group.MapGet("/also-recommended", async (
                ICoOccurrenceReadService coOccurrence, int storyId, int take = 10, CancellationToken ct = default) =>
            Results.Ok(await coOccurrence.GetAlsoRecommendedAsync(storyId, take, ct)));

        return app;
    }
}
