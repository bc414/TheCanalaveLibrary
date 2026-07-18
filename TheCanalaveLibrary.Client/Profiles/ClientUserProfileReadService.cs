using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IUserProfileReadService"/>: HttpClient wrapper over UserProfileEndpoints
/// (Server/Profiles/UserProfileEndpoints.cs). Read-only, no matching write service — one client
/// class, no read/write inheritance split (layer5-wasm.md §"Client Service Implementations").
/// </summary>
public class ClientUserProfileReadService(HttpClient http) : IUserProfileReadService
{
    private HttpClient Http { get; } = http;

    // includePrivate is accepted for interface parity but ignored on the wire — the server derives
    // it from IActiveUserContext, never a client-supplied bool (MA-602).
    public async Task<ProfileHeaderDto?> GetProfileHeaderAsync(int userId, bool includePrivate) =>
        await Http.GetNullableFromJsonAsync<ProfileHeaderDto?>($"api/user-profiles/{userId}");

    public async Task<string?> GetProfileTextAsync(int userId) =>
        await Http.GetNullableFromJsonAsync<string?>($"api/user-profiles/{userId}/bio");
}
