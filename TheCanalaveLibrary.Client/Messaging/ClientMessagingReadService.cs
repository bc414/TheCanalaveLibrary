using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IMessagingReadService"/>: HttpClient wrapper over MessagingEndpoints
/// (Server/Messaging/MessagingEndpoints.cs). Same DTOs, same method contracts — only the transport
/// differs (the Layer-5 body-swap). Every endpoint requires authentication.
/// <see cref="GetConversationsAsync"/>/<see cref="GetConversationThreadAsync"/> are wrapped in
/// <c>ExecuteAsync</c> server-side (RequireAuthenticatedUser's 401 safety net; the thread read's
/// membership-guard 404) and translated here via the shared read translator (WU-ErrorHandling2,
/// 2026-07-30 — previously an unhandled <see cref="HttpRequestException"/>, since reads didn't
/// carry the write path's typed-exception contract). <see cref="GetUnreadConversationCountAsync"/>/
/// <see cref="FindUserByUsernameAsync"/> stay untranslated — neither throws server-side (anon
/// short-circuits to 0 / null-safe lookup).
/// </summary>
public class ClientMessagingReadService(HttpClient http) : IMessagingReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(
        ConversationScope scope = ConversationScope.Active)
    {
        HttpResponseMessage response = await Http.GetAsync($"api/messaging/conversations?scope={scope}");
        await ClientHttpHelpers.ThrowIfReadFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<List<ConversationSummaryDto>>() ?? [];
    }

    public async Task<ConversationThreadDto> GetConversationThreadAsync(
        int conversationId, int page, int pageSize)
    {
        HttpResponseMessage response = await Http.GetAsync(
            $"api/messaging/conversations/{conversationId}?page={page}&pageSize={pageSize}");
        await ClientHttpHelpers.ThrowIfReadFailedAsync(response);
        return (await response.Content.ReadFromJsonAsync<ConversationThreadDto>())!;
    }

    public async Task<int> GetUnreadConversationCountAsync() =>
        await Http.GetFromJsonAsync<int>("api/messaging/unread-count");

    public async Task<MessagingParticipantDto?> FindUserByUsernameAsync(string username) =>
        await Http.GetNullableFromJsonAsync<MessagingParticipantDto?>(
            $"api/messaging/users/lookup?username={Uri.EscapeDataString(username)}");
}
