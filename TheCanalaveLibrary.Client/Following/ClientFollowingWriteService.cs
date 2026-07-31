using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IFollowingWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerFollowingWriteService : ServerFollowingReadService. Auth rides the same-origin Identity
/// cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// Standard mapping, delegated to <see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>: 400 →
/// <see cref="FollowingValidationException"/> carrying <c>ProblemDetails.Detail</c> — this covers
/// every business-rule rejection the service produces (self-follow, self-vouch, the no-op-alert
/// "you don't follow this user" guard, and the 6th-vouch <see cref="VouchLimitException"/>; the
/// concrete server type isn't distinguishable from a bare 400, but both are user-facing and the
/// message is what components display). 401/403/404 are the shared helper's standard arms
/// (WU-ErrorHandling2, 2026-07-30 — previously collapsed both into one
/// <see cref="UnauthorizedAccessException"/>, predating <see cref="SessionExpiredException"/>).
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

    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(
            response, detail => new FollowingValidationException([detail]));
}
