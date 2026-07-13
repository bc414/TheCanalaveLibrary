using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IFollowingReadService"/>: HttpClient wrapper over FollowingEndpoints
/// (Server/Following/FollowingEndpoints.cs). Same DTOs, same method contracts — only the transport
/// differs (the Layer-5 body-swap). <see cref="GetIncomingVouchesAsync"/> hits an authenticated-only
/// endpoint (see FollowingEndpoints' class summary); calling it while signed out surfaces as an
/// unhandled <see cref="HttpRequestException"/> from <c>GetFromJsonAsync</c> — reads don't carry the
/// write path's typed-exception contract (layer5-wasm.md's POST-for-complex-reads rule).
/// </summary>
public class ClientFollowingReadService(HttpClient http) : IFollowingReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<UserRelationshipStateDto> GetRelationshipStateAsync(int targetUserId) =>
        (await Http.GetFromJsonAsync<UserRelationshipStateDto>(
            $"api/following/relationship/{targetUserId}"))!;

    public async Task<IReadOnlyList<UserCardDto>> GetFollowedUsersAsync(int userId) =>
        await Http.GetFromJsonAsync<List<UserCardDto>>($"api/following/{userId}") ?? [];

    public async Task<IReadOnlyList<VouchDisplayDto>> GetOutgoingVouchesAsync(int userId) =>
        await Http.GetFromJsonAsync<List<VouchDisplayDto>>($"api/following/vouches/outgoing/{userId}") ?? [];

    public async Task<IReadOnlyList<VouchDisplayDto>> GetIncomingVouchesAsync() =>
        await Http.GetFromJsonAsync<List<VouchDisplayDto>>("api/following/vouches/incoming") ?? [];
}
