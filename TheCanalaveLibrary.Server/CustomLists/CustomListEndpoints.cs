using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="ICustomListReadService"/> /
/// <see cref="ICustomListWriteService"/> (Feature 51, WU-CustomLists). Thin pass-throughs:
/// validation and ownership checks live in the service (single enforcement point); the endpoint's
/// only added job is exception→status translation via the shared
/// <see cref="EndpointHelpers.ExecuteWriteAsync"/> (layer5-wasm.md §"The Error-Translation
/// Contract").
/// <para>
/// Read auth mirrors the consuming pages: <c>/mine</c> and <c>/memberships</c> back
/// authenticated-only surfaces (<c>/my-lists</c>, the caret-menu expander) →
/// <c>RequireAuthorization()</c>; the detail/story-ids/public-by-user reads back the public
/// <c>/lists/{id}</c> page and profile Lists tab (anonymous viewers can open public lists — same
/// posture as public profile tabs), with the service's own visibility gate returning null/empty
/// for private lists. All writes require auth. Create/rename carry the name as a query parameter
/// (scalar — the interface takes primitives per the DTO-strategy table; no transport DTO minted).
/// </para>
/// </summary>
public static class CustomListEndpoints
{
    public static WebApplication MapCustomListEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/custom-lists");

        // ── Reads ─────────────────────────────────────────────────────────────────

        group.MapGet("/mine", async (ICustomListReadService lists) =>
                Results.Ok(await lists.GetMyListsAsync()))
            .RequireAuthorization();

        // Nullable return is not an error condition (missing vs. not-visible are both a contractual
        // null, per the interface doc) — 200 with an empty body, not a 404 Results.Problem; the
        // client maps empty → null via GetNullableFromJsonAsync.
        group.MapGet("/{listId:int}", async (ICustomListReadService lists, int listId) =>
            Results.Json(await lists.GetListDetailAsync(listId)));

        group.MapGet("/{listId:int}/story-ids",
            async (ICustomListReadService lists, int listId, CustomListSortEnum sort) =>
                Results.Ok(await lists.GetListStoryIdsAsync(listId, sort)));

        group.MapGet("/public/{userId:int}", async (ICustomListReadService lists, int userId) =>
            Results.Ok(await lists.GetPublicListsByUserAsync(userId)));

        // Mature count-line disclosure (WU-AccessGate) — interstitial-grade metadata only.
        group.MapGet("/{listId:int}/hidden-mature", async (ICustomListReadService lists, int listId) =>
            Results.Ok(await lists.GetListHiddenMatureAsync(listId)));

        group.MapGet("/memberships", async (ICustomListReadService lists, int storyId) =>
                Results.Ok(await lists.GetMyListMembershipsAsync(storyId)))
            .RequireAuthorization();

        // ── Writes ────────────────────────────────────────────────────────────────

        group.MapPost("/", (ICustomListWriteService lists, string listName, bool isPublic) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await lists.CreateListAsync(listName, isPublic))))
            .RequireAuthorization();

        group.MapPut("/{listId:int}/name", (ICustomListWriteService lists, int listId, string newName) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await lists.RenameListAsync(listId, newName);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPut("/{listId:int}/visibility", (ICustomListWriteService lists, int listId, bool isPublic) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await lists.SetListVisibilityAsync(listId, isPublic);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapDelete("/{listId:int}", (ICustomListWriteService lists, int listId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await lists.DeleteListAsync(listId);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPost("/{listId:int}/stories/{storyId:int}",
                (ICustomListWriteService lists, int listId, int storyId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await lists.AddStoryAsync(listId, storyId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapDelete("/{listId:int}/stories/{storyId:int}",
                (ICustomListWriteService lists, int listId, int storyId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await lists.RemoveStoryAsync(listId, storyId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapPost("/{sourceListId:int}/clone", (ICustomListWriteService lists, int sourceListId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await lists.CloneListAsync(sourceListId))))
            .RequireAuthorization();

        return app;
    }
}
