using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IBadgeReadService"/>: HttpClient wrapper over BadgeEndpoints
/// (Server/Badges/BadgeEndpoints.cs). Same DTOs, same method contract — only the transport differs
/// (the Layer-5 body-swap).
/// </summary>
public class ClientBadgeReadService(HttpClient http) : IBadgeReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<EarnedBadgeDto>> GetMyBadgesForCurationAsync(int userId) =>
        await Http.GetFromJsonAsync<List<EarnedBadgeDto>>($"api/badges?userId={userId}") ?? [];
}
