using Microsoft.AspNetCore.Mvc;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IGroupReadService"/> / <see cref="IGroupWriteService"/>
/// (Features 38/39/40, WU32). Thin pass-throughs: no business logic or auth logic here — the
/// member/admin gates, the content-rating waterfall, and the <c>GroupAudience</c> visibility
/// filter all live in <c>ServerGroupReadService</c>/<c>ServerGroupWriteService</c> (single
/// enforcement point). Every write handler wraps in the shared
/// <see cref="EndpointHelpers.ExecuteWriteAsync"/> for exception→status translation
/// (layer5-wasm.md §"The Error-Translation Contract"). <c>GetListingsAsync</c>/
/// <c>GetMembersAsync</c> return <c>Task&lt;(T[] Items, int TotalCount)&gt;</c> tuples, translated
/// to <see cref="PagedResult{T}"/> at the HTTP boundary per layer5-wasm.md §"Paged results."
/// <para>
/// Read auth: public — group listing, detail, current-user-role, and member-list reads all mirror
/// the public <c>/groups</c> and <c>/group/{GroupId}/{*Slug}</c> pages. The audience filter (Mature
/// groups hidden from mature-disabled users) and per-row nullability (e.g.
/// <see cref="IGroupReadService.GetCurrentUserRoleAsync"/> returning <c>null</c> for anonymous/
/// non-member callers) already do the narrowing — no endpoint-level gate needed.
/// </para>
/// <para>
/// Write auth: <c>RequireAuthorization()</c> on every write, as the authenticated-user floor.
/// The finer-grained member/admin distinction rides through the service's own
/// <see cref="UnauthorizedAccessException"/> throws (translated to 403 by
/// <see cref="EndpointHelpers.ExecuteWriteAsync"/>) — folder CRUD and <c>RemoveStoryAsync</c> are
/// admin-only, <c>AddStoryAsync</c> is member-only, <c>JoinAsync</c>/<c>LeaveAsync</c>/
/// <c>CreateGroupAsync</c> require only authentication. No <c>RequireRateLimiting(...)</c> — unlike
/// Tags, <c>ServerGroupWriteService</c> doesn't call an <c>IWriteRateLimitService</c> token bucket.
/// </para>
/// </summary>
public static class GroupEndpoints
{
    public static WebApplication MapGroupEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/groups");

        // ── Reads (public — see class summary) ──

        group.MapGet("/", async (IGroupReadService groups, int page, int pageSize) =>
        {
            (GroupCardDto[] Items, int TotalCount) result = await groups.GetListingsAsync(page, pageSize);
            return Results.Ok(new PagedResult<GroupCardDto>(result.Items, result.TotalCount));
        });

        group.MapGet("/{groupId:int}", async (IGroupReadService groups, int groupId) =>
            Results.Json(await groups.GetByIdAsync(groupId)));

        group.MapGet("/{groupId:int}/role", async (IGroupReadService groups, int groupId) =>
            Results.Json(await groups.GetCurrentUserRoleAsync(groupId)));

        group.MapGet("/{groupId:int}/members", async (IGroupReadService groups, int groupId, int page, int pageSize) =>
        {
            (GroupMemberDto[] Members, int TotalCount) result = await groups.GetMembersAsync(groupId, page, pageSize);
            return Results.Ok(new PagedResult<GroupMemberDto>(result.Members, result.TotalCount));
        });

        // ── Writes (authenticated floor — member/admin gates enforced by the service, translated here) ──

        group.MapPost("/", (IGroupWriteService groups, CreateGroupDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await groups.CreateGroupAsync(dto))))
            .RequireAuthorization();

        group.MapPut("/{groupId:int}", (IGroupWriteService groups, int groupId, UpdateGroupDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    groupId != dto.GroupId
                        ? Results.Problem(detail: "Route groupId does not match body GroupId.",
                            statusCode: StatusCodes.Status400BadRequest)
                        : await UpdateAndRespondAsync(groups, dto)))
            .RequireAuthorization();

        group.MapPost("/{groupId:int}/join", (IGroupWriteService groups, int groupId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await groups.JoinAsync(groupId);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPost("/{groupId:int}/leave", (IGroupWriteService groups, int groupId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await groups.LeaveAsync(groupId);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPost("/stories", (IGroupWriteService groups, AddGroupStoryDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await groups.AddStoryAsync(dto);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapDelete("/stories/{groupStoryId:int}", (IGroupWriteService groups, int groupStoryId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await groups.RemoveStoryAsync(groupStoryId);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPut("/stories/{groupStoryId:int}/folder/{groupFolderId:int}",
                (IGroupWriteService groups, int groupStoryId, int groupFolderId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await groups.AssignStoryToFolderAsync(groupStoryId, groupFolderId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapDelete("/stories/{groupStoryId:int}/folder/{groupFolderId:int}",
                (IGroupWriteService groups, int groupStoryId, int groupFolderId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await groups.UnassignStoryFromFolderAsync(groupStoryId, groupFolderId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapPost("/folders", (IGroupWriteService groups, CreateFolderDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await groups.CreateFolderAsync(dto))))
            .RequireAuthorization();

        // [FromBody] pins a bare string parameter to the JSON body — minimal APIs bind a plain
        // string from the query string by default (same reasoning as FollowingEndpoints' vouchText).
        group.MapPut("/folders/{groupFolderId:int}/name",
                (IGroupWriteService groups, int groupFolderId, [FromBody] string newName) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await groups.RenameFolderAsync(groupFolderId, newName);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapDelete("/folders/{groupFolderId:int}", (IGroupWriteService groups, int groupFolderId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await groups.DeleteFolderAsync(groupFolderId);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPut("/folders/{groupFolderId:int}/sort-order",
                (IGroupWriteService groups, int groupFolderId, int newSortOrder) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await groups.ReorderFolderAsync(groupFolderId, newSortOrder);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        return app;
    }

    private static async Task<IResult> UpdateAndRespondAsync(IGroupWriteService groups, UpdateGroupDto dto)
    {
        await groups.UpdateGroupAsync(dto);
        return Results.NoContent();
    }
}
