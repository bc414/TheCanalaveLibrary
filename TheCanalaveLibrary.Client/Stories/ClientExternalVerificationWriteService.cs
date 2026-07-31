using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IExternalVerificationWriteService"/>. Inherits the read impl (CQRS-lite),
/// mirroring <c>ServerExternalVerificationWriteService : ServerExternalVerificationReadService</c>.
/// Auth rides the same-origin Identity cookie.
///
/// Delegates the standard status-code mapping to
/// <see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>: 401/403 covers
/// <c>RequireModerator()</c>'s genuine denial AND the several <see cref="InvalidOperationException"/>
/// business-rule guards EndpointHelpers also maps to 401 — e.g. "Verify your X account first",
/// same known mismatch as <c>ClientModerationWriteService</c>; 404 is defensive (this service
/// raises <c>SingleAsync</c> exceptions rather than 404 for a missing identity/link today).
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

    private static Task ThrowIfWriteFailedAsync(HttpResponseMessage response) =>
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, detail => new InvalidOperationException(detail));
}
