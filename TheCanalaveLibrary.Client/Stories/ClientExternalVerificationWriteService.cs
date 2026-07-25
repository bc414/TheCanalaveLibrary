using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IExternalVerificationWriteService"/>. Inherits the read impl (CQRS-lite),
/// mirroring <c>ServerExternalVerificationWriteService : ServerExternalVerificationReadService</c>.
/// Auth rides the same-origin Identity cookie.
///
/// Translates ExternalVerificationEndpoints' status codes back into the service contract's typed
/// exceptions: 401/403 → <see cref="UnauthorizedAccessException"/> (covers
/// <c>RequireModerator()</c>'s genuine denial AND the several <see cref="InvalidOperationException"/>
/// business-rule guards EndpointHelpers also maps to 401 — e.g. "Verify your X account first",
/// same known mismatch as <c>ClientModerationWriteService</c>), 404 →
/// <see cref="KeyNotFoundException"/> (defensive — this service raises <c>SingleAsync</c>
/// exceptions rather than 404 for a missing identity/link today).
/// </summary>
public sealed class ClientExternalVerificationWriteService(HttpClient http)
    : ClientExternalVerificationReadService(http), IExternalVerificationWriteService
{
    public async Task<string> EnsureMyVerificationCodeAsync()
    {
        HttpResponseMessage response = await Http.PostAsync("api/external-verification/my-code", content: null);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<string>() ?? string.Empty;
    }

    public async Task SubmitAccountForVerificationAsync(AddExternalAccountRequest request)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/external-verification/accounts", request);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task RequestLinkVerificationAsync(int storyExternalLinkId)
    {
        HttpResponseMessage response = await Http.PostAsync(
            $"api/external-verification/links/{storyExternalLinkId}/request", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task ApproveAccountVerificationAsync(int userExternalIdentityId)
    {
        HttpResponseMessage response = await Http.PostAsync(
            $"api/external-verification/accounts/{userExternalIdentityId}/approve", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task RejectAccountVerificationAsync(int userExternalIdentityId, string reason)
    {
        string query = $"?reason={Uri.EscapeDataString(reason)}";
        HttpResponseMessage response = await Http.PostAsync(
            $"api/external-verification/accounts/{userExternalIdentityId}/reject{query}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task ApproveLinkVerificationAsync(int storyExternalLinkId)
    {
        HttpResponseMessage response = await Http.PostAsync(
            $"api/external-verification/links/{storyExternalLinkId}/approve", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task RejectLinkVerificationAsync(int storyExternalLinkId, string reason)
    {
        string query = $"?reason={Uri.EscapeDataString(reason)}";
        HttpResponseMessage response = await Http.PostAsync(
            $"api/external-verification/links/{storyExternalLinkId}/reject{query}", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of ExternalVerificationEndpoints').</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;

        string? detail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
        switch (response.StatusCode)
        {
            case HttpStatusCode.Unauthorized:
            case HttpStatusCode.Forbidden:
                throw new UnauthorizedAccessException(
                    detail ?? "This action requires an authenticated moderator or admin.");
            case HttpStatusCode.NotFound:
                throw new KeyNotFoundException(detail ?? "Account or link not found.");
            default:
                response.EnsureSuccessStatusCode(); // throws HttpRequestException with the status
                return;
        }
    }
}
