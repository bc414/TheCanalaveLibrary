using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IFollowingWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerFollowingWriteService : ServerFollowingReadService. Auth rides the same-origin Identity
/// cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Translates FollowingEndpoints' status codes back into the service contract's typed exceptions so
/// components behave identically on either side of the interface: 400 →
/// <see cref="FollowingValidationException"/> carrying <c>ProblemDetails.Detail</c> — this covers
/// every business-rule rejection the service produces (self-follow, self-vouch, and the
/// no-op-alert "you don't follow this user" guard, all now <see cref="FollowingValidationException"/>
/// server-side, plus the 6th-vouch <see cref="VouchLimitException"/>; the concrete server type isn't
/// distinguishable from a bare 400, but both are user-facing and the message is what components
/// display), 401/403 → <see cref="UnauthorizedAccessException"/> (genuine "not signed in" — the
/// service's auth guard still throws <see cref="InvalidOperationException"/> → 401, message read
/// through from <c>ProblemDetails.Detail</c>), 404 → <see cref="KeyNotFoundException"/> (defensive;
/// this service never actually throws <see cref="KeyNotFoundException"/> today).
/// </para>
/// </summary>
public sealed class ClientFollowingWriteService(HttpClient http)
    : ClientFollowingReadService(http), IFollowingWriteService
{
    public async Task FollowAsync(int targetUserId)
    {
        HttpResponseMessage response = await Http.PostAsync($"api/following/{targetUserId}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task UnfollowAsync(int targetUserId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/following/{targetUserId}");
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task SetReceiveAlertsAsync(int targetUserId, bool receiveAlerts)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/following/{targetUserId}/alerts?receiveAlerts={(receiveAlerts ? "true" : "false")}",
            content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task VouchAsync(int targetUserId, string? vouchText)
    {
        HttpResponseMessage response =
            await Http.PostAsJsonAsync($"api/following/vouches/{targetUserId}", vouchText);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task RemoveVouchAsync(int targetUserId)
    {
        HttpResponseMessage response = await Http.DeleteAsync($"api/following/vouches/{targetUserId}");
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of FollowingEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                // Every business-rule rejection this service produces now maps to 400 (self-follow,
                // self-vouch, no-op-alert, and the 6th-vouch VouchLimitException) — reconstruct the
                // family type carrying the server's ProblemDetails.Detail. The concrete server type
                // (FollowingValidationException vs VouchLimitException) doesn't round-trip, but both
                // are user-facing and the message is what the UI displays.
                string? badRequestDetail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new FollowingValidationException([badRequestDetail ?? "The request failed validation."]);
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new UnauthorizedAccessException(detail ?? "This action requires you to be signed in.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
