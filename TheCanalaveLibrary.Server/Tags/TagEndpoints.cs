using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="ITagReadService"/> / <see cref="ITagWriteService"/>.
/// Thin pass-throughs: no business logic here — validation and the mod/admin gate live in the
/// service (single enforcement point). Exception→status translation is
/// <see cref="EndpointHelpers.ExecuteWriteAsync"/>'s (MA-407 — this class carried the original
/// private 3-case copy): TagValidationException → 400 (ProblemDetails.Detail carries the message
/// verbatim — it is user-facing), UnauthorizedAccessException → 403,
/// KeyNotFoundException → 404. The write routes carry a <c>RequireAuthorization()</c> floor
/// (endpoint-authz sweep 2026-07-18) so unauthenticated callers get 401 without reaching the
/// service; the service's RequireMod remains the enforcement point (403 for signed-in non-mods).
/// </summary>
public static class TagEndpoints
{
    public static WebApplication MapTagEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/tags");

        // ── Reads (public, no auth gate — mirrors the public /tags browse page) ──

        group.MapGet("/directory", async (ITagReadService tags) =>
            Results.Ok(await tags.GetTagDirectoryAsync()));

        // Covers GetTagsByTypeAsync and its four GetAll{Type}TagsAsync convenience wrappers —
        // the wrappers are client-side sugar over the same query (same shape as the server impl).
        group.MapGet("/", async (ITagReadService tags, TagTypeEnum type) =>
            Results.Ok(await tags.GetTagsByTypeAsync(type)));

        group.MapGet("/chips/search", async (ITagReadService tags, TagTypeEnum type, string term) =>
            Results.Ok(await tags.SearchTagChipsAsync(type, term)));

        // Repeated-key query binding (?ids=1&ids=2) — order-preserving, matching the service's
        // reorder-to-input contract.
        group.MapGet("/chips/by-ids", async (ITagReadService tags, int[] ids) =>
            Results.Ok(await tags.GetTagChipsByIdsAsync(ids)));

        // ── Writes (mod/admin — enforced by the service's RequireMod, translated here) ──
        // RequireRateLimiting("TagWrites"): per-user (IP fallback) HTTP edge limit — the tag API
        // is the one write surface that is plain HTTP today (security.md "HTTP Edge Rate Limiting").

        group.MapPost("/", (ITagWriteService tags, CreateTagDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () => Results.Ok(await tags.CreateTagAsync(dto))))
            .RequireAuthorization()
            .RequireRateLimiting("TagWrites");

        group.MapPut("/{tagId:int}", (ITagWriteService tags, int tagId, UpdateTagDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    tagId != dto.TagId
                        ? Results.Problem(detail: "Route tagId does not match body TagId.",
                            statusCode: StatusCodes.Status400BadRequest)
                        // Body is the raw sprite-warning string (or JSON null) — the service contract
                        // returns string?, and Layer 5 is a body-swap: no new wrapper DTO is minted.
                        : Results.Json(await tags.UpdateTagAsync(dto))))
            .RequireAuthorization()
            .RequireRateLimiting("TagWrites");

        group.MapDelete("/{tagId:int}", (ITagWriteService tags, int tagId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await tags.DeleteTagAsync(tagId);
                    return Results.NoContent();
                }))
            .RequireAuthorization()
            .RequireRateLimiting("TagWrites");

        return app;
    }
}
