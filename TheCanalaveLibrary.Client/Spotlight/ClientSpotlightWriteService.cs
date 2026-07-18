using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ISpotlightWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// <see cref="ServerSpotlightWriteService"/> : <see cref="ServerSpotlightReadService"/>. Auth rides
/// the same-origin Identity cookie — WASM's fetch-backed HttpClient sends it automatically for
/// same-origin requests.
/// <para>
/// Translates SpotlightEndpoints' status codes back into the service contract's typed exceptions —
/// the shared MA-008 shape (<see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>); 400
/// reconstructs <see cref="SpotlightValidationException"/> via its single-string constructor
/// (round-trips the joined message verbatim, no re-wrapping into a list needed).
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

    /// <summary>Status-code → contract-exception translation (inverse of SpotlightEndpoints') — the
    /// shared MA-008 shape.</summary>
    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, msg => new SpotlightValidationException(msg));
}
