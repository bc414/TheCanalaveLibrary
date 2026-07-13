using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IModerationWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerModerationWriteService : ServerModerationReadService. Auth rides the same-origin Identity
/// cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates ModerationEndpoints' status codes back into the service contract's typed exceptions:
/// 400 → <see cref="ArgumentException"/> (message from <c>ProblemDetails.Detail</c> — defensive; no
/// method on this service actually produces 400 today, see ModerationEndpoints' class doc), 401/403
/// → <see cref="UnauthorizedAccessException"/> (message read through — covers both
/// <c>RequireModerator()</c>'s genuine "not signed in"/"not a mod" denial and the several other
/// <see cref="InvalidOperationException"/>-throwing business-rule guards EndpointHelpers also maps to
/// 401; see ModerationEndpoints' class doc's "Known EndpointHelpers mismatch" note — same shape as
/// <c>ClientFollowingWriteService</c>'s documented caveat), 404 →
/// <see cref="KeyNotFoundException"/> (defensive; this service raises <c>SingleAsync</c>/EF exceptions
/// rather than <see cref="KeyNotFoundException"/> for a missing report/story today).
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

    /// <summary>Status-code → contract-exception translation (inverse of ModerationEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                throw new ArgumentException(detail ?? "The moderation request was rejected.");
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException(
                    detail ?? "This action requires an authenticated moderator or admin.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException(detail ?? "Report or story not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
