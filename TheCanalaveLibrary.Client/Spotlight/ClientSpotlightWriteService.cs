using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ISpotlightWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// <see cref="ServerSpotlightWriteService"/> : <see cref="ServerSpotlightReadService"/>. Auth rides
/// the same-origin Identity cookie — WASM's fetch-backed HttpClient sends it automatically for
/// same-origin requests.
/// <para>
/// Translates SpotlightEndpoints' status codes back into the service contract's typed exceptions:
/// 400 → <see cref="SpotlightValidationException"/> (message read through from
/// <c>ProblemDetails.Detail</c> — the server already joins the validation-error list into one
/// message via <c>ex.Message</c>, so the single-string constructor round-trips it verbatim, no
/// re-wrapping into a list needed), 401/403 → <see cref="UnauthorizedAccessException"/> (covers
/// both the cookie handler's own 401 and the service's own "requires an authenticated user"
/// <c>InvalidOperationException</c> → 401 mapping), 404 → <see cref="KeyNotFoundException"/> (not
/// currently thrown by <c>RedeemSlotAsync</c> — included for symmetry with the shared
/// server-side <c>EndpointHelpers</c> contract in case a future revision adds a not-found case).
/// </para>
/// </summary>
public sealed class ClientSpotlightWriteService(HttpClient http)
    : ClientSpotlightReadService(http), ISpotlightWriteService
{
    public async Task RedeemSlotAsync(RedeemSpotlightSlotDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/spotlight/redeem", dto);
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of SpotlightEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        switch (response.StatusCode)
        {
            case HttpStatusCode.BadRequest:
                string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
                throw new SpotlightValidationException(detail ?? "The spotlight request failed validation.");
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException("Spotlight redemption requires an authenticated user.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException("Spotlight slot not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
