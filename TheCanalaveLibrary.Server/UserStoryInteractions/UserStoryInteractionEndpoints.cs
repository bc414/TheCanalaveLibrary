using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IUserStoryInteractionReadService"/> /
/// <see cref="IUserStoryInteractionWriteService"/>. Every method on both interfaces resolves the
/// current viewer via <see cref="IActiveUserContext"/> (or, for
/// <see cref="IUserStoryInteractionReadService.GetFavoriteStoryIdsAsync"/>, an explicit
/// <c>userId</c> whose result is still only meaningful in a per-viewer context) — the whole
/// cluster is inherently per-user data (favorites, bookshelves, follows), so the entire group
/// carries <c>RequireAuthorization()</c> rather than the per-endpoint public/private judgment call
/// layer5-wasm.md normally calls for.
/// <para>
/// Thin pass-throughs: no business logic here. <see cref="EndpointHelpers.ExecuteWriteAsync"/>
/// handles the exception→status translation (layer5-wasm.md §"The Error-Translation Contract").
/// This cluster mints no dedicated <c>*ValidationException</c> type — the write service throws
/// plain <see cref="InvalidOperationException"/> for "no authenticated user" (translated to 401,
/// mirroring <c>IUserSettingsService</c>'s self-referential pattern), and the read service's
/// <see cref="IUserStoryInteractionReadService.GetBookshelfStoryIdsAsync"/> throws
/// <see cref="ArgumentOutOfRangeException"/> for tabs not backed by <c>UserStoryInteraction</c>
/// (translated to 400 — <c>ArgumentOutOfRangeException</c> is an <c>ArgumentException</c>, so
/// <c>ExecuteWriteAsync</c>'s validation-exception branch already covers it). That bookshelf read
/// is therefore wrapped in <c>ExecuteWriteAsync</c> too, even though it performs no mutation — the
/// helper is exception-driven, not write-specific by name.
/// </para>
/// <para>
/// <see cref="IUserStoryInteractionWriteService.MarkStartedAsync"/> is a direct
/// <c>DbContext</c> write in the server impl (load-or-create + <c>SaveChangesAsync</c>), NOT an
/// in-process buffered-signal merge like <c>IViewCountWriteService</c>/
/// <c>IReadingProgressWriteService</c> (see <c>layer2-services.md</c> §"Signal Buffering") — so its
/// endpoint returns <c>204 No Content</c> like an ordinary write, not <c>202 Accepted</c>.
/// </para>
/// </summary>
public static class UserStoryInteractionEndpoints
{
    public static WebApplication MapUserStoryInteractionEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/user-story-interactions").RequireAuthorization();

        // ── Reads ──

        group.MapGet("/{storyId:int}", async (IUserStoryInteractionReadService interactions, int storyId) =>
            Results.Ok(await interactions.GetStateAsync(storyId)));

        // Repeated-key query binding (?storyIds=1&storyIds=2), same shape as ITagReadService's
        // GetTagChipsByIdsAsync — endpoint parameter is the GET-bindable int[]; the service
        // signature's IReadOnlyList<int> accepts the array implicitly.
        group.MapGet("/by-ids", async (IUserStoryInteractionReadService interactions, int[] storyIds) =>
            Results.Ok(await interactions.GetStatesByStoryIdsAsync(storyIds)));

        // Wrapped in ExecuteWriteAsync solely for the ArgumentOutOfRangeException → 400
        // translation (see type doc comment above) — GetBookshelfStoryIdsAsync itself is read-only.
        group.MapGet("/bookshelf", (IUserStoryInteractionReadService interactions, BookshelfTab tab) =>
            EndpointHelpers.ExecuteWriteAsync(async () =>
                Results.Ok(await interactions.GetBookshelfStoryIdsAsync(tab))));

        // includePrivate is derived server-side (owner-only hidden favorites) — never accepted from
        // the client, same pattern as UserProfileEndpoints' includePrivate derivation (MA-602;
        // endpoint-authz sweep 2026-07-18).
        group.MapGet("/favorites/{userId:int}",
            async (IUserStoryInteractionReadService interactions, IActiveUserContext activeUser, int userId) =>
                Results.Ok(await interactions.GetFavoriteStoryIdsAsync(
                    userId, includePrivate: activeUser.UserId == userId)));

        // ── Writes ──

        group.MapPost("/{storyId:int}", (
                IUserStoryInteractionWriteService interactions,
                int storyId,
                UserStoryInteractionStateUpdate update) =>
            EndpointHelpers.ExecuteWriteAsync(async () =>
            {
                await interactions.SetUserStoryInteractionStateAsync(storyId, update);
                return Results.NoContent();
            }));

        group.MapPost("/{storyId:int}/started",
            (IUserStoryInteractionWriteService interactions, int storyId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await interactions.MarkStartedAsync(storyId);
                    return Results.NoContent();
                }));

        return app;
    }
}
