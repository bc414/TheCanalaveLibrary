using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IAccountStatusReadService"/> — HttpClient wrapper over
/// Server/Identity/AccountStatusEndpoints.cs. Same-origin Identity cookie rides along
/// automatically on the WASM fetch-backed <see cref="HttpClient"/>; no request body/route value
/// is sent — the endpoint resolves the caller's own id server-side.
/// </summary>
public sealed class ClientAccountStatusReadService(HttpClient http) : IAccountStatusReadService
{
    public async Task<AccountStatusDto> GetMyAccountStatusAsync() =>
        (await http.GetFromJsonAsync<AccountStatusDto>("api/account-status"))!;
}
