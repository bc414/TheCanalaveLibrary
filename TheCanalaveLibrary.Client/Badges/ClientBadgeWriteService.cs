using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IBadgeWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerBadgeWriteService : ServerBadgeReadService. Auth rides the same-origin Identity cookie —
/// WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// <c>SetDisplayOrderAsync</c>'s unowned-key business rule now throws
/// <see cref="BadgeValidationException"/> server-side → 400, reconstructed here carrying
/// <c>ProblemDetails.Detail</c> (user-facing, surfaces the real cause). The only remaining
/// unauthenticated case is the <c>RequireAuthorization</c> floor / <c>RequireUserId</c> guard's
/// <see cref="InvalidOperationException"/> → 401; the interface declares no
/// <see cref="UnauthorizedAccessException"/>, so 401/403 map to <see cref="InvalidOperationException"/>
/// here too, mirroring <c>ClientChapterReadMarkWriteService</c>'s approach for interfaces with no
/// dedicated auth exception, reading the server's message through rather than hardcoding it.
/// </para>
/// </summary>
public sealed class ClientBadgeWriteService(HttpClient http) : ClientBadgeReadService(http), IBadgeWriteService
{
    /// <summary>
    /// Server-internal generation — no HTTP surface exists (see <c>BadgeEndpoints</c>: awards are
    /// earned, only <c>ServerRecommendationWriteService</c> calls this, in-process; mapping a route
    /// would let a WASM caller mint any catalogue badge). Implemented only to satisfy the interface;
    /// reaching it over WASM is a bug — same pattern as <c>ClientNotificationWriteService</c>.
    /// </summary>
    public Task<bool> AwardAsync(int userId, string badgeKey) =>
        throw new NotSupportedException(
            "AwardAsync is a server-internal badge-generation method (called only from other " +
            "server-side write services, in-process). BadgeEndpoints maps no HTTP surface for it, " +
            "so it must never be reachable from a WASM component.");

    public async Task SetDisplayOrderAsync(int userId, IReadOnlyList<string> orderedVisibleKeys)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync("api/badges/display-order", orderedVisibleKeys);
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of BadgeEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                // SetDisplayOrderAsync's unowned-key business rule (a requested key the caller
                // hasn't earned) now maps to 400 — reconstruct the family type carrying the
                // server's ProblemDetails.Detail.
                string? badRequestDetail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new BadgeValidationException([badRequestDetail ?? "The request failed validation."]);
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new InvalidOperationException(
                    detail ?? "This operation requires an authenticated user.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
