using Microsoft.AspNetCore.Mvc;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="ISeriesReadService"/> / <see cref="ISeriesWriteService"/>
/// (Feature 9, WU41). Thin pass-throughs: no business logic or auth logic here — validation and
/// the owner-only gate (<c>Series.AuthorId == Story.AuthorId == ActiveUser.UserId</c>) live in
/// <see cref="ServerSeriesWriteService"/> (single enforcement point). Every write handler wraps in
/// the shared <see cref="EndpointHelpers.ExecuteWriteAsync"/> for exception→status translation
/// (layer5-wasm.md §"The Error-Translation Contract").
/// <para>
/// Read auth: public. A series row has no visibility filter of its own (member stories are
/// individually filtered when hydrated), and every page that consumes these reads is public or
/// allow-anonymous — <c>SeriesPage</c> (<c>/series/{id}</c>), <c>StoryPage</c> (public,
/// <see cref="ISeriesReadService.GetMembershipsForStoryAsync"/>), <c>ProfilePage</c>
/// (<c>[AllowAnonymous]</c>, <see cref="ISeriesReadService.GetSeriesByAuthorAsync"/>). The
/// authenticated-only <c>MySeriesPage</c> (<c>/series</c>) consumes the same public
/// <see cref="ISeriesReadService.GetSeriesByAuthorAsync"/> read as the public ProfilePage Series
/// tab — no endpoint-level gate needed since one consuming page is public (mirrors TagEndpoints'
/// read-auth rule).
/// </para>
/// <para>
/// Write auth: <c>RequireAuthorization()</c> on every write — every
/// <see cref="ISeriesWriteService"/> method requires an authenticated user
/// (<c>ServerSeriesWriteService.RequireAuthenticatedUser</c>), and membership/edit/delete
/// mutations additionally enforce ownership via <c>UnauthorizedAccessException</c>, translated to
/// 403 by <see cref="EndpointHelpers.ExecuteWriteAsync"/>. No <c>RequireRateLimiting(...)</c> —
/// unlike Tags, <see cref="ServerSeriesWriteService"/> doesn't call an
/// <c>IWriteRateLimitService</c> token bucket.
/// </para>
/// </summary>
public static class SeriesEndpoints
{
    public static WebApplication MapSeriesEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/series");

        // ── Reads (public — see class summary) ──

        group.MapGet("/{seriesId:int}", async (ISeriesReadService series, int seriesId) =>
            Results.Json(await series.GetSeriesByIdAsync(seriesId)));

        group.MapGet("/by-author/{authorId:int}", async (ISeriesReadService series, int authorId) =>
            Results.Ok(await series.GetSeriesByAuthorAsync(authorId)));

        group.MapGet("/memberships/story/{storyId:int}", async (ISeriesReadService series, int storyId) =>
            Results.Ok(await series.GetMembershipsForStoryAsync(storyId)));

        // ── Writes (authenticated — ownership enforced by the service, translated here) ──

        group.MapPost("/", (ISeriesWriteService series, CreateSeriesDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await series.CreateSeriesAsync(dto))))
            .RequireAuthorization();

        group.MapPut("/{seriesId:int}", (ISeriesWriteService series, int seriesId, UpdateSeriesDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    seriesId != dto.SeriesId
                        ? Results.Problem(detail: "Route seriesId does not match body SeriesId.",
                            statusCode: StatusCodes.Status400BadRequest)
                        : await UpdateAndRespondAsync(series, dto)))
            .RequireAuthorization();

        group.MapDelete("/{seriesId:int}", (ISeriesWriteService series, int seriesId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await series.DeleteSeriesAsync(seriesId);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPost("/{seriesId:int}/stories/{storyId:int}",
                (ISeriesWriteService series, int seriesId, int storyId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await series.AddStoryAsync(seriesId, storyId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapDelete("/{seriesId:int}/stories/{storyId:int}",
                (ISeriesWriteService series, int seriesId, int storyId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await series.RemoveStoryAsync(seriesId, storyId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        // Body-bound (not query-bound) — a reorder list can exceed a handful of ids and this is a
        // write, so it rides the JSON body like CreateSeriesDto/UpdateSeriesDto rather than the
        // repeated-key query-array pattern GET reads use (layer5-wasm.md's POST-for-complex-reads
        // rule is a read-only concern; [FromBody] here just pins binding unambiguously since a
        // bare List<int>/int[] parameter defaults to query-string binding in minimal APIs).
        group.MapPut("/{seriesId:int}/order",
                (ISeriesWriteService series, int seriesId, [FromBody] List<int> orderedStoryIds) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await series.ReorderAsync(seriesId, orderedStoryIds);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> UpdateAndRespondAsync(ISeriesWriteService series, UpdateSeriesDto dto)
    {
        await series.UpdateSeriesAsync(dto);
        return Results.NoContent();
    }
}
