using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IBadgeWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerBadgeWriteService : ServerBadgeReadService. Auth rides the same-origin Identity cookie —
/// WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// <c>SetDisplayOrderAsync</c>'s unowned-key business rule throws
/// <see cref="BadgeValidationException"/> server-side → 400, reconstructed here carrying
/// <c>ProblemDetails.Detail</c> (user-facing, surfaces the real cause). Everything else is the
/// standard mapping — delegates to <see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>
/// (WU-ErrorHandling2, 2026-07-30 — this class previously collapsed 401/403 into one
/// <see cref="InvalidOperationException"/> arm, predating <see cref="SessionExpiredException"/>).
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
        await ClientHttpHelpers.ThrowIfWriteFailedAsync(
            response, detail => new BadgeValidationException([detail]));
    }
}
