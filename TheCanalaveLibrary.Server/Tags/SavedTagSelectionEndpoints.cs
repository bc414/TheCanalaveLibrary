using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="ISavedTagSelectionReadService"/> /
/// <see cref="ISavedTagSelectionWriteService"/> (Feature 15, WU43). Thin pass-throughs: no business
/// logic here — validation and ownership checks live in the service (single enforcement point). The
/// endpoint's only added job is exception→status translation, via the shared
/// <see cref="EndpointHelpers.ExecuteWriteAsync"/> (layer5-wasm.md §"The Error-Translation Contract").
/// <para>
/// Saved tag selections are a per-user feature (saved filter presets) — every read and write
/// requires an authenticated user, so <c>RequireAuthorization()</c> gates the whole surface (unlike
/// Tags/Comments, which have public read paths). <see cref="ISavedTagSelectionReadService.GetMySelectionsAsync"/>
/// and <see cref="ISavedTagSelectionWriteService"/>'s methods already throw/no-op for unauthenticated
/// callers at the service layer, but the endpoint-level gate keeps the failure mode a clean 401 from
/// the cookie handler (Program.cs <c>OnRedirectToLogin</c>) instead of relying on that fallback.
/// </para>
/// </summary>
public static class SavedTagSelectionEndpoints
{
    public static WebApplication MapSavedTagSelectionEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/saved-tag-selections");

        // ── Reads (all require an authenticated user — this is a per-user feature) ──

        group.MapGet("/", async (ISavedTagSelectionReadService selections, SavedTagSelectionSortEnum sort) =>
                Results.Ok(await selections.GetMySelectionsAsync(sort)))
            .RequireAuthorization();

        // Nullable return is not an error condition (missing vs. not-visible are both a contractual
        // null, per the interface doc) — 200 with a JSON null body, not a 404 Results.Problem.
        group.MapGet("/{id:int}", async (ISavedTagSelectionReadService selections, int id) =>
                Results.Json(await selections.GetSelectionDetailAsync(id)))
            .RequireAuthorization();

        group.MapGet("/public/{userId:int}", async (ISavedTagSelectionReadService selections, int userId) =>
                Results.Ok(await selections.GetPublicSelectionsByUserAsync(userId)))
            .RequireAuthorization();

        // ── Writes (owner-only enforced in the service; endpoint translates the resulting exceptions) ──

        group.MapPost("/", (ISavedTagSelectionWriteService selections, SavedTagSelectionInput input) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await selections.CreateAsync(input))))
            .RequireAuthorization();

        group.MapPut("/{id:int}", (ISavedTagSelectionWriteService selections, int id, SavedTagSelectionInput input) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await selections.UpdateAsync(id, input);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapDelete("/{id:int}", (ISavedTagSelectionWriteService selections, int id) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await selections.DeleteAsync(id);
                    return Results.NoContent();
                }))
            .RequireAuthorization();

        group.MapPost("/{sourceId:int}/copy", (ISavedTagSelectionWriteService selections, int sourceId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await selections.CopyPublicSelectionAsync(sourceId))))
            .RequireAuthorization();

        return app;
    }
}
