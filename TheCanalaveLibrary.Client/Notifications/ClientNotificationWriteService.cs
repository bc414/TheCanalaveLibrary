using System.Net;
using System.Runtime.CompilerServices;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="INotificationWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerNotificationWriteService : ServerNotificationReadService. Auth rides the same-origin
/// Identity cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// <b>Component-reachable surface</b> (the three methods <c>NotificationBell.razor</c>/
/// <c>NotificationsPage.razor</c>/<c>NotificationSettingsPage.razor</c> actually call):
/// <see cref="MarkAsReadAsync"/>, <see cref="MarkAllAsReadAsync"/>, <see cref="SetSettingAsync"/> —
/// these call NotificationEndpoints.cs and translate its status codes back into the service
/// contract's typed exceptions: 401/403 → <see cref="UnauthorizedAccessException"/>, 404 →
/// <see cref="KeyNotFoundException"/>.
/// </para>
/// <para>
/// <b>Internal-only surface</b> — every <c>NotifyNew*Async</c>/
/// <c>Notify{Story,Report,Account,Spotlight,Poll}*Async</c> semantic generation method. These are
/// called server-to-server only (comment posting, follows, group fan-out, moderation actions,
/// spotlight go-live, poll-edit sweeps — see <c>ServerNotificationWriteService</c>'s doc comment and
/// <c>cross-cutting.md</c> "Notification Creation"); no <c>.razor</c> file injects them, and
/// <c>NotificationEndpoints.cs</c> deliberately maps no HTTP surface for them (mapping one would let
/// a WASM client mint arbitrary notifications naming any source/moderator id — a privilege-escalation
/// surface, not just dead code). Implemented here only so the class satisfies the full interface;
/// each throws <see cref="NotSupportedException"/> — reaching one over WASM is a bug (a component
/// wrongly injecting the internal generation surface), not a scenario to support.
/// </para>
/// </summary>
public sealed class ClientNotificationWriteService(HttpClient http)
    : ClientNotificationReadService(http), INotificationWriteService
{
    // ── Component-reachable ──────────────────────────────────────────────────────

    public async Task MarkAsReadAsync(long notificationId)
    {
        HttpResponseMessage response =
            await Http.PostAsync($"api/notifications/{notificationId}/mark-read", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task MarkAllAsReadAsync()
    {
        HttpResponseMessage response =
            await Http.PostAsync("api/notifications/mark-all-read", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task SetSettingAsync(NotificationTypeEnum notifType, bool emailEnabled, bool collapsed)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/notifications/settings/{(short)notifType}" +
            $"?emailEnabled={(emailEnabled ? "true" : "false")}&collapsed={(collapsed ? "true" : "false")}",
            content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of NotificationEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new UnauthorizedAccessException(detail ?? "This action requires you to be signed in.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Notification not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }

    // ── Internal-only (server-to-server; never invoked from a WASM component) ────

    public Task NotifyNewFollowerAsync(int recipientUserId, int followerUserId) =>
        throw NotExposedOverHttp();

    public Task NotifyNewVouchAsync(int recipientUserId, int voucherUserId) =>
        throw NotExposedOverHttp();

    public Task NotifyStoryHiddenGemAsync(int recipientStoryAuthorId, int sourceRecommenderId) =>
        throw NotExposedOverHttp();

    public Task NotifyNewGroupStoryAsync(int groupId, int storyAuthorId, int sourceUserId) =>
        throw NotExposedOverHttp();

    public Task NotifyNewGroupBlogPostAsync(int groupId, int blogPostId, int authorId) =>
        throw NotExposedOverHttp();

    public Task NotifyStoryLineageRequestedAsync(int targetAuthorId, int requesterId, int sourceStoryId) =>
        throw NotExposedOverHttp();

    public Task NotifyStoryLineageApprovedAsync(int sourceAuthorId, int approverId, int targetStoryId) =>
        throw NotExposedOverHttp();

    public Task NotifyReportReceivedAsync(int reporterUserId, int moderatorSourceId) =>
        throw NotExposedOverHttp();

    public Task NotifyReportResolvedAsync(int reporterUserId, int moderatorSourceId) =>
        throw NotExposedOverHttp();

    public Task NotifyReportResolvedNoActionAsync(int reporterUserId, int moderatorSourceId) =>
        throw NotExposedOverHttp();

    public Task NotifyContentRemovedAsync(int contentAuthorUserId, int moderatorSourceId) =>
        throw NotExposedOverHttp();

    public Task NotifyStoryApprovedAsync(int storyAuthorUserId, int storyId, int moderatorSourceId) =>
        throw NotExposedOverHttp();

    public Task NotifyStoryRejectedAsync(int storyAuthorUserId, int storyId, int moderatorSourceId) =>
        throw NotExposedOverHttp();

    public Task NotifyAccountWarningAsync(int targetUserId, int moderatorSourceId) =>
        throw NotExposedOverHttp();

    public Task NotifyAccountSuspendedAsync(int targetUserId, int moderatorSourceId) =>
        throw NotExposedOverHttp();

    public Task NotifyAccountBannedAsync(int targetUserId, int moderatorSourceId) =>
        throw NotExposedOverHttp();

    public Task NotifySpotlightSlotGrantedAsync(int awardeeUserId, int grantingModeratorId) =>
        throw NotExposedOverHttp();

    public Task NotifyStorySpotlightedAsync(int storyAuthorUserId, int sponsorUserId, int storyId) =>
        throw NotExposedOverHttp();

    public Task NotifyRecommendationSpotlightedAsync(int recommenderUserId, int sponsorUserId, int storyId) =>
        throw NotExposedOverHttp();

    public Task NotifyPollUpdatedAsync(
        int pollOwnerUserId, IReadOnlyList<int> voterUserIds, int relatedEntityId) =>
        throw NotExposedOverHttp();

    private static NotSupportedException NotExposedOverHttp([CallerMemberName] string? method = null) =>
        new($"{method} is a server-internal notification-generation method (called only from other " +
            "server-side write services, in-process, after their own commit — see " +
            "ServerNotificationWriteService). NotificationEndpoints.cs maps no HTTP surface for it, so " +
            "it must never be reachable from a WASM component. Reaching this is a bug: some component " +
            "is injecting the write-internal generation surface.");
}
