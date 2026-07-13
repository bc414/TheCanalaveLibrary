using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="ISiteDailyStatReadService"/>: HttpClient wrapper over
/// SiteDailyStatEndpoints (Server/Moderation/SiteDailyStatEndpoints.cs). Read-only interface with no
/// matching write service — one client class, no read/write inheritance split (layer5-wasm.md
/// "Client Service Implementations" — "Read-only interfaces").
/// <para>
/// <c>CancellationToken</c> parameters are accepted for interface conformance but never threaded
/// across the HTTP boundary (layer5-wasm.md "CancellationToken parameters are dropped at the client
/// boundary") — callers needing request cancellation would use <c>HttpClient</c>'s own
/// <c>CancellationToken</c> overloads directly, not the service interface's token.
/// </para>
/// </summary>
public sealed class ClientSiteDailyStatReadService(HttpClient http) : ISiteDailyStatReadService
{
    public async Task<SiteDailyStatDto?> GetLatestAsync(CancellationToken ct = default) =>
        await http.GetNullableFromJsonAsync<SiteDailyStatDto?>("api/site-daily-stats/latest");

    public async Task<IReadOnlyList<SiteDailyStatDto>> GetSeriesAsync(int days, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<SiteDailyStatDto>>($"api/site-daily-stats/series?days={days}") ?? [];
}
