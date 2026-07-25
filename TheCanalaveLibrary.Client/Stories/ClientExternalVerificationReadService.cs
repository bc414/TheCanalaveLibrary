using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IExternalVerificationReadService"/>: HttpClient wrapper over
/// ExternalVerificationEndpoints (Server/Stories/ExternalVerificationEndpoints.cs). Same DTOs,
/// same method contracts — only the transport differs (the Layer-5 body-swap).
/// </summary>
public class ClientExternalVerificationReadService(HttpClient http) : IExternalVerificationReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<VerificationPlatformDto>> GetVerificationPlatformsAsync() =>
        await Http.GetFromJsonAsync<VerificationPlatformDto[]>("api/external-verification/platforms") ?? [];

    public async Task<IReadOnlyList<ExternalAccountDto>> GetMyExternalAccountsAsync() =>
        await Http.GetFromJsonAsync<ExternalAccountDto[]>("api/external-verification/my-accounts") ?? [];

    public async Task<IReadOnlyList<PendingAccountVerificationDto>> GetPendingAccountVerificationsAsync() =>
        await Http.GetFromJsonAsync<PendingAccountVerificationDto[]>("api/external-verification/pending-accounts") ?? [];

    public async Task<IReadOnlyList<PendingLinkVerificationDto>> GetPendingLinkVerificationsAsync() =>
        await Http.GetFromJsonAsync<PendingLinkVerificationDto[]>("api/external-verification/pending-links") ?? [];
}
