using Microsoft.AspNetCore.Mvc;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="IMessagingReadService"/> / <see cref="IMessagingWriteService"/>
/// (private messaging, Feature 49). Thin pass-throughs: no business logic or auth logic here — the
/// conversation-membership guard (<see cref="KeyNotFoundException"/> for non-participants), the
/// <c>AllowPrivateMessages</c> privacy gate (<see cref="MessagingPermissionException"/>), and message
/// validation all live in <c>ServerMessagingReadService</c>/<c>ServerMessagingWriteService</c> (single
/// enforcement point). Every write handler wraps in the shared
/// <see cref="EndpointHelpers.ExecuteWriteAsync"/> for exception→status translation
/// (layer5-wasm.md §"The Error-Translation Contract"; <see cref="MessagingPermissionException"/> → 403
/// is already one of the shared cases there).
/// <para>
/// Auth: every endpoint — reads and writes alike — carries <c>RequireAuthorization()</c>. All
/// messaging data is strictly per-user (private conversations); there is no anonymous or public read
/// path, unlike Tags/Groups/Following. The service resolves the current viewer from
/// <c>IActiveUserContext</c> and enforces conversation membership itself; the endpoint only
/// translates the resulting exceptions, never duplicates the check.
/// </para>
/// <para>
/// No <c>RequireRateLimiting(...)</c> — unlike Tags, <c>ServerMessagingWriteService</c> already calls
/// <c>IWriteRateLimitService.EnsureAllowed(WriteActionKind.Message, ...)</c> in
/// <c>StartConversationAsync</c>/<c>SendMessageAsync</c> (service-layer token bucket,
/// security.md §"Write Throttling"); <see cref="WriteRateLimitExceededException"/> → 429 is handled by
/// the shared <see cref="EndpointHelpers.ExecuteWriteAsync"/>, so an additional HTTP-edge policy would
/// be redundant (mirrors FollowingEndpoints'/GroupEndpoints' reasoning for the inverse case).
/// </para>
/// </summary>
public static class MessagingEndpoints
{
    public static WebApplication MapMessagingEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/messaging");

        // ── Reads (all authenticated — see class summary) ──

        group.MapGet("/conversations",
                async (IMessagingReadService messaging, bool includeArchived = false) =>
                    Results.Ok(await messaging.GetConversationsAsync(includeArchived)))
            .RequireAuthorization();

        group.MapGet("/conversations/{conversationId:int}",
                async (IMessagingReadService messaging, int conversationId, int page, int pageSize) =>
                    Results.Ok(await messaging.GetConversationThreadAsync(conversationId, page, pageSize)))
            .RequireAuthorization();

        group.MapGet("/unread-count", async (IMessagingReadService messaging) =>
                Results.Ok(await messaging.GetUnreadConversationCountAsync()))
            .RequireAuthorization();

        group.MapGet("/users/lookup", async (IMessagingReadService messaging, string username) =>
                Results.Json(await messaging.FindUserByUsernameAsync(username)))
            .RequireAuthorization();

        // ── Writes (authenticated — service enforces membership/privacy gates, translated here) ──

        group.MapPost("/conversations", (IMessagingWriteService messaging, StartConversationDto dto) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                    Results.Ok(await messaging.StartConversationAsync(dto))))
            .RequireAuthorization();

        // [FromBody] pins the bare string parameter to the JSON body — minimal APIs bind a plain
        // string from the query string by default (same reasoning as FollowingEndpoints' vouchText).
        group.MapPost("/conversations/{conversationId:int}/messages",
                (IMessagingWriteService messaging, int conversationId, [FromBody] string messageHtml) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                        Results.Ok(await messaging.SendMessageAsync(conversationId, messageHtml))))
            .RequireAuthorization();

        group.MapPut("/conversations/{conversationId:int}/read",
                (IMessagingWriteService messaging, int conversationId) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await messaging.MarkConversationReadAsync(conversationId);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        group.MapPut("/conversations/{conversationId:int}/archived",
                (IMessagingWriteService messaging, int conversationId, bool archived) =>
                    EndpointHelpers.ExecuteWriteAsync(async () =>
                    {
                        await messaging.SetArchivedAsync(conversationId, archived);
                        return Results.NoContent();
                    }))
            .RequireAuthorization();

        return app;
    }
}
