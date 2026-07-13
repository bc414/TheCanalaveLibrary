using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Layer-5 API surface for <see cref="INotificationReadService"/> / <see cref="INotificationWriteService"/>
/// (Features 41/42/43). Thin pass-throughs: no business logic here — the service is the single
/// enforcement point.
/// <para>
/// <b>Scope — component-reachable surface only.</b> <see cref="INotificationWriteService"/> also
/// declares ~20 semantic <c>NotifyNew*Async</c>/<c>Notify{Story,Report,Account,Spotlight,Poll}*Async</c>
/// generation methods (comment posting, follows, group fan-out, moderation, spotlight go-live, poll
/// edits, etc.). Those are called internally, server-to-server, by other write services after their
/// own commit (<c>cross-cutting.md</c> "Notification Creation") — no <c>.razor</c> file injects them,
/// so they get no endpoint here. Mapping them would also be a privilege-escalation surface (e.g. a
/// WASM client could otherwise mint arbitrary <c>AccountBanned</c>/<c>StoryApproved</c> notifications
/// naming any <c>moderatorSourceId</c>). <see cref="TheCanalaveLibrary.Client.ClientNotificationWriteService"/>
/// still implements every one of them (a client impl must satisfy the whole interface to compile) —
/// each throws <see cref="NotSupportedException"/>, since no code path ever calls them over HTTP.
/// </para>
/// <para>
/// Every endpoint requires authentication (<see cref="INotificationReadService"/>'s own doc comment:
/// "all methods are self-scoped… sourced from IActiveUserContext") — the whole cluster is per-user
/// data, same blanket-gate rationale as <c>UserStoryInteractionEndpoints</c>, rather than the
/// per-endpoint public/private judgment call layer5-wasm.md normally calls for. The service's
/// anonymous-safe zero/empty-return behavior is therefore moot over HTTP: the cookie handler's 401
/// (Program.cs <c>OnRedirectToLogin</c>) always wins before the service runs.
/// </para>
/// <para>
/// Writes wrap in the shared <see cref="EndpointHelpers.ExecuteWriteAsync"/> for exception→status
/// translation (layer5-wasm.md §"The Error-Translation Contract"), even though none of the three
/// mapped write methods throws a typed exception in practice today — defense-in-depth, mirrors
/// ChapterReadMarkEndpoints/StoryLineageEndpoints.
/// </para>
/// </summary>
public static class NotificationEndpoints
{
    public static WebApplication MapNotificationEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/notifications").RequireAuthorization();

        // ── Reads ──

        group.MapGet("/unread-count", async (INotificationReadService notifications) =>
            Results.Ok(await notifications.GetUnreadCountAsync()));

        group.MapGet("/total-count", async (INotificationReadService notifications) =>
            Results.Ok(await notifications.GetTotalCountAsync()));

        group.MapGet("/", async (
                INotificationReadService notifications,
                int page,
                int pageSize,
                NotificationFeedOrder order = NotificationFeedOrder.NewestFirst) =>
            Results.Ok(await notifications.GetNotificationsAsync(page, pageSize, order)));

        group.MapGet("/settings", async (INotificationReadService notifications) =>
            Results.Ok(await notifications.GetSettingsAsync()));

        // ── Writes ──

        group.MapPost("/{notificationId:long}/mark-read",
            (INotificationWriteService notifications, long notificationId) =>
                EndpointHelpers.ExecuteWriteAsync(async () =>
                {
                    await notifications.MarkAsReadAsync(notificationId);
                    return Results.NoContent();
                }));

        group.MapPost("/mark-all-read", (INotificationWriteService notifications) =>
            EndpointHelpers.ExecuteWriteAsync(async () =>
            {
                await notifications.MarkAllAsReadAsync();
                return Results.NoContent();
            }));

        group.MapPut("/settings/{notifType}", (
                INotificationWriteService notifications,
                NotificationTypeEnum notifType,
                bool emailEnabled,
                bool collapsed) =>
            EndpointHelpers.ExecuteWriteAsync(async () =>
            {
                await notifications.SetSettingAsync(notifType, emailEnabled, collapsed);
                return Results.NoContent();
            }));

        return app;
    }
}
