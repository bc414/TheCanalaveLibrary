using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IModerationWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerModerationWriteService : ServerModerationReadService. Auth rides the same-origin Identity
/// cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Standard mapping, delegated to <see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>: 400 →
/// <see cref="ModerationValidationException"/> over <c>ProblemDetails.Detail</c>, so the server's
/// user-facing guard text (approve/reject "already handled", the live-author and entry-status
/// guards, account-action and auto-approve validation — WU-StoryLifecycle, 2026-09-30, pulled
/// forward from service audit §2.7.5) reaches the moderator verbatim on WASM instead of collapsing
/// to the generic error. 401/403/404 are the shared helper's standard arms (WU-ErrorHandling2,
/// 2026-07-30). Since WU-ModerationIntegrity (2026-09-30) every server guard here answers with its
/// real status — 400 for a business rule (an already-resolved report, the account-status transition
/// table), 404 for an unknown id — so no guard still arrives as 401. Report submission is not here:
/// it is <see cref="ClientReportSubmissionService"/> (owner ruling D9's split).
/// </para>
/// </summary>
public sealed class ClientModerationWriteService(HttpClient http)
    : ClientModerationReadService(http), IModerationWriteService
{
    public async Task ClaimReportAsync(long reportId)
    {
        HttpResponseMessage response =
            await Http.PostAsync($"api/moderation/reports/{reportId}/claim", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task ResolveNoActionAsync(long reportId, string? actionNotes)
    {
        string query = actionNotes is null ? "" : $"?actionNotes={Uri.EscapeDataString(actionNotes)}";
        HttpResponseMessage response = await Http.PostAsync(
            $"api/moderation/reports/{reportId}/resolve-no-action{query}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task ResolveWithRemovalAsync(long reportId, string removalReason, bool hardDelete = false)
    {
        string query = $"?removalReason={Uri.EscapeDataString(removalReason)}&hardDelete={hardDelete}";
        HttpResponseMessage response = await Http.PostAsync(
            $"api/moderation/reports/{reportId}/resolve-removal{query}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task ApplyAccountActionAsync(long reportId, ModeratorActionType action,
        string reason, DateTime? suspendedUntilUtc = null)
    {
        string query = $"?action={action}&reason={Uri.EscapeDataString(reason)}" +
            (suspendedUntilUtc is DateTime s
                ? $"&suspendedUntilUtc={Uri.EscapeDataString(s.ToString("o"))}"
                : "");
        HttpResponseMessage response = await Http.PostAsync(
            $"api/moderation/reports/{reportId}/account-action{query}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task ApplyAccountActionToUserAsync(int targetUserId, short reasonId,
        ModeratorActionType action, string reason, DateTime? suspendedUntilUtc = null)
    {
        string query = $"?reasonId={reasonId}&action={action}&reason={Uri.EscapeDataString(reason)}" +
            (suspendedUntilUtc is DateTime s
                ? $"&suspendedUntilUtc={Uri.EscapeDataString(s.ToString("o"))}"
                : "");
        HttpResponseMessage response = await Http.PostAsync(
            $"api/moderation/users/{targetUserId}/account-action{query}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task ReinstateUserAsync(int targetUserId, string reason)
    {
        string query = $"?reason={Uri.EscapeDataString(reason)}";
        HttpResponseMessage response = await Http.PostAsync(
            $"api/moderation/users/{targetUserId}/reinstate{query}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task ApproveStoryAsync(int storyId)
    {
        HttpResponseMessage response = await Http.PostAsync(
            $"api/moderation/submissions/{storyId}/approve", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task RejectStoryAsync(int storyId, string reason)
    {
        string query = $"?reason={Uri.EscapeDataString(reason)}";
        HttpResponseMessage response = await Http.PostAsync(
            $"api/moderation/submissions/{storyId}/reject{query}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task SetCanAutoApproveAsync(int targetUserId, bool canAutoApprove, short reasonId, string reason)
    {
        string query = $"?enabled={canAutoApprove}&reasonId={reasonId}&reason={Uri.EscapeDataString(reason)}";
        HttpResponseMessage response = await Http.PostAsync(
            $"api/moderation/users/{targetUserId}/auto-approve{query}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, detail => new ModerationValidationException([detail]));
}
