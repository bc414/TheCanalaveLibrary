using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IMessagingReadService"/>: HttpClient wrapper over MessagingEndpoints
/// (Server/Messaging/MessagingEndpoints.cs). Same DTOs, same method contracts — only the transport
/// differs (the Layer-5 body-swap). Every endpoint requires authentication; calling one of these
/// while signed out (or as a non-participant, for <see cref="GetConversationThreadAsync"/>) surfaces
/// as an unhandled <see cref="HttpRequestException"/> from <c>GetFromJsonAsync</c> — reads don't
/// carry the write path's typed-exception contract (layer5-wasm.md's POST-for-complex-reads rule).
/// </summary>
public class ClientMessagingReadService(HttpClient http) : IMessagingReadService
{
    /// <summary>Exposed to the write subclass — primary-ctor params can't be shared directly.</summary>
    protected HttpClient Http { get; } = http;

    public async Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(
        bool includeArchived = false) =>
        await Http.GetFromJsonAsync<List<ConversationSummaryDto>>(
            $"api/messaging/conversations?includeArchived={includeArchived}") ?? [];

    public async Task<ConversationThreadDto> GetConversationThreadAsync(
        int conversationId, int page, int pageSize) =>
        (await Http.GetFromJsonAsync<ConversationThreadDto>(
            $"api/messaging/conversations/{conversationId}?page={page}&pageSize={pageSize}"))!;

    public async Task<int> GetUnreadConversationCountAsync() =>
        await Http.GetFromJsonAsync<int>("api/messaging/unread-count");

    public async Task<MessagingParticipantDto?> FindUserByUsernameAsync(string username) =>
        await Http.GetNullableFromJsonAsync<MessagingParticipantDto?>(
            $"api/messaging/users/lookup?username={Uri.EscapeDataString(username)}");
}
