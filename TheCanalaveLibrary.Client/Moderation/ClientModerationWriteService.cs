using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IModerationWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerModerationWriteService : ServerModerationReadService. Auth rides the same-origin Identity
/// cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Standard mapping, delegated to <see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>: 400 →
/// <see cref="ArgumentException"/> (defensive; no method on this service actually produces 400
/// today, see ModerationEndpoints' class doc). 401/403/404 are the shared helper's standard arms
/// (WU-ErrorHandling2, 2026-07-30 — previously collapsed 401/403 into one
/// <see cref="UnauthorizedAccessException"/>, predating <see cref="SessionExpiredException"/>; see
/// ModerationEndpoints' class doc's "Known EndpointHelpers mismatch" note for why 401 can arrive
/// here for what is really a business-rule guard, not just "not signed in").
/// </para>
/// </summary>
public sealed class ClientModerationWriteService(HttpClient http)
    : ClientModerationReadService(http), IModerationWriteService
{
    public async Task SubmitReportAsync(SubmitReportRequest request)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/moderation/reports", request);
        await ThrowIfWriteFailedAsync(response);
    }

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

    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, detail => new ArgumentException(detail));
}
