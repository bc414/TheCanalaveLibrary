using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ISpotlightReadService"/>: HttpClient wrapper over SpotlightEndpoints
/// (Server/Spotlight/SpotlightEndpoints.cs). Same DTOs, same method contracts — only the transport
/// differs (the Layer-5 body-swap). <see cref="GetActiveSpotlightsAsync"/> hits the public
/// <c>/active</c> route; the rest hit the auth-only <c>/my-*</c>/<c>/blocks</c> routes and rely on
/// the same-origin Identity cookie riding along automatically.
/// </summary>
public class ClientSpotlightReadService(HttpClient http) : ISpotlightReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<SpotlightDisplayDto>> GetActiveSpotlightsAsync() =>
        await Http.GetFromJsonAsync<List<SpotlightDisplayDto>>("api/spotlight/active") ?? [];

    public async Task<IReadOnlyList<SpotlightSlotDto>> GetMyAvailableSlotsAsync() =>
        await Http.GetFromJsonAsync<List<SpotlightSlotDto>>("api/spotlight/my-slots") ?? [];

    public async Task<IReadOnlyList<SpotlightBookingDto>> GetMyBookingsAsync() =>
        await Http.GetFromJsonAsync<List<SpotlightBookingDto>>("api/spotlight/my-bookings") ?? [];

    public async Task<IReadOnlyList<SpotlightPickCandidateDto>> GetMyPickCandidatesAsync() =>
        await Http.GetFromJsonAsync<List<SpotlightPickCandidateDto>>("api/spotlight/my-pick-candidates") ?? [];

    public async Task<IReadOnlyList<SpotlightBlockDto>> GetBlockAvailabilityAsync() =>
        await Http.GetFromJsonAsync<List<SpotlightBlockDto>>("api/spotlight/blocks") ?? [];
}
