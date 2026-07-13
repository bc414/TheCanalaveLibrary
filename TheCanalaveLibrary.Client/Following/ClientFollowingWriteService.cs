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
/// <see cref="VouchLimitException"/> — the only 400 this write service ever produces (the 6th-vouch
/// guard in <c>VouchAsync</c>; <see cref="VouchLimitException"/>'s parameterless constructor bakes in
/// its own message, so there's nothing to read off the response body for this case), 401/403 →
/// <see cref="UnauthorizedAccessException"/> (message read through from
/// <c>ProblemDetails.Detail</c> — see FollowingEndpoints' class summary on the shared
/// <c>InvalidOperationException</c> → 401 mapping covering the self-follow/self-vouch/no-op-alert
/// guards alongside genuine "not signed in"), 404 → <see cref="KeyNotFoundException"/> (defensive;
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
                throw new VouchLimitException();
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
