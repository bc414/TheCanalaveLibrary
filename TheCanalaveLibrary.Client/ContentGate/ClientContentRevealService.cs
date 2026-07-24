using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IContentRevealService"/>: HttpClient wrapper over the
/// ContentGate reveal-management endpoints (Server/ContentGate/ContentGateEndpoints.cs).
/// </summary>
public sealed class ClientContentRevealService(HttpClient http) : IContentRevealService
{
    public async Task<IReadOnlyList<RevealDisplayDto>> GetMyRevealsAsync() =>
        await http.GetFromJsonAsync<List<RevealDisplayDto>>("api/content-gate/reveals") ?? [];

    public async Task RemoveAsync(RevealedEntityType entityType, int entityId)
    {
        HttpResponseMessage response =
            await http.DeleteAsync($"api/content-gate/reveals/{(short)entityType}/{entityId}");
        response.EnsureSuccessStatusCode();
    }
}
