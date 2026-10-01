using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IExternalVerificationWriteService"/>. Inherits the read impl (CQRS-lite),
/// mirroring <c>ServerExternalVerificationWriteService : ServerExternalVerificationReadService</c>.
/// Auth rides the same-origin Identity cookie.
///
/// Delegates the standard status-code mapping to
/// <see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/>: 400 →
/// <see cref="ExternalVerificationValidationException"/> over <c>ProblemDetails.Detail</c> (the
/// server's business rules — "Verify your X account first", a malformed URL — reach the user
/// verbatim); 401 → <see cref="SessionExpiredException"/>; 403 → <c>RequireModerator()</c>'s denial;
/// 404 → an unknown identity, link or platform id (WU-ModerationIntegrity, 2026-09-30: the server
/// used to throw these as <see cref="InvalidOperationException"/>/<c>SingleAsync</c> failures, which
/// arrived as 401).
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
        ClientHttpHelpers.ThrowIfWriteFailedAsync(response, detail => new ExternalVerificationValidationException([detail]));
}
