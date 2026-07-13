using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IRecommendationReadService"/> /
/// <see cref="IRecommendationWriteService"/>. Thin pass-throughs: no business logic here —
/// validation, author-only ownership checks, and the Hidden Gem / spotlight limits all live in the
/// service (single enforcement point). The endpoint's only added job is exception→status
/// translation, via the shared <see cref="EndpointHelpers.ExecuteWriteAsync"/> (layer5-wasm.md
/// §"The Error-Translation Contract").
/// <para>
/// Reads are public — recommendations cannot have spoilers (audit/Recommendations.md), mirroring
/// the public story page's recommendation display. <see cref="IRecommendationReadService.GetRecommendedStoryIdsAsync"/>,
/// <see cref="IRecommendationReadService.GetHiddenGemStoryIdsAsync"/>, and
/// <see cref="IRecommendationReadService.GetHelpfulPromptRecommendationIdAsync"/> resolve the viewer
/// from <c>IActiveUserContext</c> but the service degrades gracefully to an empty list/null for
/// anonymous callers rather than throwing (mirrors <c>IFollowingReadService.GetRelationshipStateAsync</c>'s
/// zero-state pattern), so they stay public rather than <c>RequireAuthorization()</c>-gated.
/// </para>
/// <para>
/// Writes (submit/edit/delete/like/Hidden-Gem toggle/spotlight/attribution) require an
/// authenticated user — the service enforces author-only ownership via
/// <see cref="UnauthorizedAccessException"/> (→ 403) and unauthenticated-caller guards via
/// <see cref="InvalidOperationException"/> (→ 401); <c>RequireAuthorization()</c> is added as
/// defense-in-depth so the cookie handler's own 401 (Program.cs <c>OnRedirectToLogin</c>) wins the
/// race first in the normal case. <c>SubmitAsync</c> additionally throttles via
/// <c>IWriteRateLimitService</c> (<c>WriteActionKind.ContentCreate</c>) — translated to 429 by
/// <c>ExecuteWriteAsync</c>.
/// </para>
/// <para>
/// <b>Known EndpointHelpers mismatch (flagged, not fixed here — out of scope for this add-only
/// pass, same category as FollowingEndpoints'):</b> <see cref="IRecommendationWriteService.SetHiddenGemAsync"/>
/// and <see cref="IRecommendationWriteService.SetHighlightedByAuthorAsync"/> throw
/// <see cref="InvalidOperationException"/> both for "caller not authenticated" AND for their
/// reject-at-limit business rules (5 Hidden Gems / 5 spotlighted-per-story) — the shared
/// <c>ExecuteWriteAsync</c> maps every <see cref="InvalidOperationException"/> to 401 uniformly, so a
/// limit-reached rejection surfaces as 401 rather than the more accurate 400. The message still
/// crosses via <c>ProblemDetails.Detail</c>, so <c>ClientRecommendationWriteService</c> reads it
/// through rather than losing it, but the HTTP status itself is semantically imprecise for that case.
/// </para>
/// </summary>
public static class RecommendationEndpoints
{
    public static WebApplication MapRecommendationEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/recommendations");

        // ── Reads (public — recommendations cannot have spoilers; mirrors the public story page) ──

        group.MapGet("/story/{storyId:int}", async (IRecommendationReadService recs, int storyId) =>
            Results.Ok(await recs.GetForStoryAsync(storyId)));

        group.MapGet("/{recommendationId:int}",
            async (IRecommendationReadService recs, int recommendationId) =>
                Results.Json(await recs.GetByIdAsync(recommendationId)));

        group.MapGet("/mine/recommended-story-ids", async (IRecommendationReadService recs) =>
            Results.Ok(await recs.GetRecommendedStoryIdsAsync()));

        group.MapGet("/mine/hidden-gem-story-ids", async (IRecommendationReadService recs) =>
            Results.Ok(await recs.GetHiddenGemStoryIdsAsync()));

        group.MapGet("/helpful-prompt/{storyId:int}",
            async (IRecommendationReadService recs, int storyId) =>
                Results.Json(await recs.GetHelpfulPromptRecommendationIdAsync(storyId)));

        group.MapGet("/by-user/{userId:int}/story-ids",
            async (IRecommendationReadService recs, int userId) =>
                Results.Ok(await recs.GetRecommendedStoryIdsByUserAsync(userId)));

        // ── Writes (authenticated — author-only ownership enforced in the service) ──

        group.MapPost("/", (IRecommendationWriteService recs, RecommendationSubmitDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await recs.SubmitAsync(dto))))
            .RequireAuthorization();

        group.MapPut("/{recommendationId:int}", (
                IRecommendationWriteService recs,
                int recommendationId,
                UpdateRecommendationDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    recommendationId != dto.RecommendationId
                        ? Results.Problem(
                            detail: "Route recommendationId does not match body RecommendationId.",
                            statusCode: StatusCodes.Status400BadRequest)
                        : await EditAndReturnNoContentAsync(recs, dto)))
            .RequireAuthorization();

        group.MapDelete("/{recommendationId:int}",
                (IRecommendationWriteService recs, int recommendationId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await recs.DeleteAsync(recommendationId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapPost("/{recommendationId:int}/like",
                (IRecommendationWriteService recs, int recommendationId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                        Results.Ok(await recs.ToggleLikeAsync(recommendationId))))
            .RequireAuthorization();

        group.MapPut("/{recommendationId:int}/hidden-gem", (
                IRecommendationWriteService recs,
                int recommendationId,
                bool isHiddenGem) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await recs.SetHiddenGemAsync(recommendationId, isHiddenGem);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPut("/{recommendationId:int}/spotlight", (
                IRecommendationWriteService recs,
                int recommendationId,
                bool isHighlighted) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await recs.SetHighlightedByAuthorAsync(recommendationId, isHighlighted);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPost("/{recommendationId:int}/success",
                (IRecommendationWriteService recs, int recommendationId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await recs.RecordSuccessAsync(recommendationId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapPost("/attribution/{storyId:int}/{recommendationId:int}", (
                IRecommendationWriteService recs,
                int storyId,
                int recommendationId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await recs.RecordAttributionSourceAsync(storyId, recommendationId);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> EditAndReturnNoContentAsync(
        IRecommendationWriteService recs, UpdateRecommendationDto dto)
    {
        await recs.EditAsync(dto);
        return Results.NoContent();
    }
}
