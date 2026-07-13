using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IBadgeWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerBadgeWriteService : ServerBadgeReadService. Auth rides the same-origin Identity cookie —
/// WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// The interface itself declares no <see cref="UnauthorizedAccessException"/> and only one
/// documented exception (<see cref="InvalidOperationException"/> from
/// <c>SetDisplayOrderAsync</c>'s badge-ownership check) — so 401/403 map to
/// <see cref="InvalidOperationException"/> here too, mirroring <c>ClientChapterReadMarkWriteService</c>'s
/// approach for interfaces with no dedicated auth exception. The server's
/// <c>ProblemDetails.Detail</c> text is read and reused verbatim rather than hardcoded, because 401
/// from this endpoint can mean either "not authenticated" (RequireAuthorization floor) or the
/// mis-mapped badge-ownership validation error (see <c>BadgeEndpoints</c> doc comment) — reusing the
/// server's own message avoids losing the real cause in the second case.
/// </para>
/// </summary>
public sealed class ClientBadgeWriteService(HttpClient http) : ClientBadgeReadService(http), IBadgeWriteService
{
    public async Task<bool> AwardAsync(int userId, string badgeKey)
    {
        HttpResponseMessage response = await Http.PostAsync(
            $"api/badges/award?userId={userId}&badgeKey={Uri.EscapeDataString(badgeKey)}", content: null);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<bool>();
    }

    public async Task SetDisplayOrderAsync(int userId, IReadOnlyList<string> orderedVisibleKeys)
    {
        HttpResponseMessage response = await Http.PutAsJsonAsync(
            $"api/badges/display-order?userId={userId}", orderedVisibleKeys);
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of BadgeEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
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
