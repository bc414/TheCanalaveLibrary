using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IFanonReadService"/> / <see cref="IFanonWriteService"/>
/// (WU-TagFanon). Thin pass-throughs — the mod gate, the never-notify-twice rule, and adoption
/// semantics all live in the service. Dashboard reads are public (the /fanon pages are public
/// read-only); the author-facing adoption reads and every write require authentication, with the
/// service enforcing mod-vs-author authority per method.
/// </summary>
public static class FanonEndpoints
{
    public static WebApplication MapFanonEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/fanon");

        // ── Dashboard reads (public — "one page, two experiences") ──

        group.MapGet("/groups", async (IFanonReadService fanon, TagTypeEnum axis, string? search, int page = 1, int pageSize = 25) =>
            Results.Ok(await fanon.GetGroupsAsync(axis, search, page, pageSize)));

        group.MapGet("/groups/count", async (IFanonReadService fanon, TagTypeEnum axis, string? search) =>
            Results.Ok(await fanon.GetGroupCountAsync(axis, search)));

        group.MapGet("/groups/stories", async (IFanonReadService fanon, TagTypeEnum axis, int baseTagId, string name) =>
            Results.Ok(await fanon.GetGroupStoriesAsync(axis, baseTagId, name)));

        group.MapGet("/established", async (IFanonReadService fanon) =>
            Results.Ok(await fanon.GetEstablishedFanonTagsAsync()));

        group.MapGet("/official-name", async (IFanonReadService fanon, TagTypeEnum axis, string name) =>
            Results.Json(await fanon.FindOfficialTagByNameAsync(axis, name)));

        // ── Author-facing adoption reads ──

        group.MapGet("/my-adoptions", (IFanonReadService fanon) =>
                EndpointHelpers.ExecuteAsync(async () => Results.Ok(await fanon.GetMyAdoptionIndexAsync())))
            .RequireAuthorization();

        group.MapGet("/my-adoptions/{tagId:int}", (IFanonReadService fanon, int tagId) =>
                EndpointHelpers.ExecuteAsync(async () =>
                {
                    TagAdoptionPageDto? page = await fanon.GetMyAdoptionPageAsync(tagId);
                    // Bodied Results.Problem, not bare Results.NotFound() — layer5-wasm.md's
                    // JSON-API rule (WU-ErrorHandling2 audit, 2026-07-30): a bare 404 re-executes
                    // into the HTML not-found route under UseStatusCodePagesWithReExecute.
                    return page is null
                        ? Results.Problem(statusCode: StatusCodes.Status404NotFound)
                        : Results.Ok(page);
                }))
            .RequireAuthorization();

        // ── Writes (mod: link/notify · author: adopt/dismiss — service enforces authority) ──

        group.MapPost("/links", (IFanonWriteService fanon, FanonLinkCreateDto dto) =>
                EndpointHelpers.ExecuteAsync(async () => Results.Ok(await fanon.LinkGroupAsync(dto))))
            .RequireAuthorization()
            .RequireRateLimiting("TagWrites");

        group.MapPost("/links/notify", (IFanonWriteService fanon, string name, int baseTagId) =>
                EndpointHelpers.ExecuteAsync(async () => Results.Ok(await fanon.NotifyNewAuthorsAsync(name, baseTagId))))
            .RequireAuthorization()
            .RequireRateLimiting("TagWrites");

        group.MapPost("/adopt/{tagId:int}/story/{storyId:int}", (IFanonWriteService fanon, int tagId, int storyId) =>
                EndpointHelpers.ExecuteAsync(async () => Results.Ok(await fanon.AdoptAsync(tagId, storyId))))
            .RequireAuthorization()
            .RequireRateLimiting("TagWrites");

        group.MapPost("/adopt/{tagId:int}/all", (IFanonWriteService fanon, int tagId) =>
                EndpointHelpers.ExecuteAsync(async () => Results.Ok(await fanon.AdoptAllAsync(tagId))))
            .RequireAuthorization()
            .RequireRateLimiting("TagWrites");

        group.MapPut("/adopt/{tagId:int}/dismissed", (IFanonWriteService fanon, int tagId, bool dismissed) =>
                EndpointHelpers.ExecuteAsync(async () =>
                {
                    await fanon.SetDismissedAsync(tagId, dismissed);
                    return Results.NoContent();
                }))
            .RequireAuthorization()
            .RequireRateLimiting("TagWrites");

        return app;
    }
}
