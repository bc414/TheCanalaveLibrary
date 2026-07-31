using System.Net;
using System.Net.Http.Json;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Client;

/// <summary>
/// WASM-side <see cref="IMessagingWriteService"/>. Inherits the read impl (CQRS-lite), mirroring
/// ServerMessagingWriteService : ServerMessagingReadService. Auth rides the same-origin Identity
/// cookie — WASM's fetch-backed HttpClient sends it automatically for same-origin requests.
/// <para>
/// 403 is intercepted here — the one arm this class still handles privately —
/// for <see cref="MessagingPermissionException"/> (the <c>AllowPrivateMessages</c> gate,
/// <c>StartConversationAsync</c> only; message read through from <c>ProblemDetails.Detail</c>).
/// Everything else (400 → <see cref="MessagingValidationException"/>, message from
/// <c>ProblemDetails.Detail</c> — the server side already joins the underlying error list with
/// "; ", so it is reconstructed as a single-element list, mirroring
/// <c>ClientCommentWriteService</c>/<c>ClientGroupWriteService</c>'s pattern for the same
/// list-of-strings constructor shape; 401/404/5xx) delegates to the shared
/// <see cref="ClientHttpHelpers.ThrowIfWriteFailedAsync"/> (WU-ErrorHandling2, 2026-07-30).
/// </para>
/// </summary>
public sealed class ClientMessagingWriteService(HttpClient http)
    : ClientMessagingReadService(http), IMessagingWriteService
{
    public async Task<int> StartConversationAsync(StartConversationDto dto)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync("api/messaging/conversations", dto);
        await ThrowIfWriteFailedAsync(response);
        return await response.Content.ReadFromJsonAsync<int>();
    }

    public async Task<MessageDto> SendMessageAsync(int conversationId, string messageHtml)
    {
        HttpResponseMessage response = await Http.PostAsJsonAsync(
            $"api/messaging/conversations/{conversationId}/messages", messageHtml);
        await ThrowIfWriteFailedAsync(response);
        return (await response.Content.ReadFromJsonAsync<MessageDto>())!;
    }

    public async Task MarkConversationReadAsync(int conversationId)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/messaging/conversations/{conversationId}/read", content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    public async Task SetArchivedAsync(int conversationId, bool archived)
    {
        HttpResponseMessage response = await Http.PutAsync(
            $"api/messaging/conversations/{conversationId}/archived?archived={archived}",
            content: null);
        await ThrowIfWriteFailedAsync(response);
    }

    /// <summary>Status-code → contract-exception translation (inverse of MessagingEndpoints'). 403
    /// is intercepted here for <see cref="MessagingPermissionException"/>; every other status
    /// delegates to the shared standard mapping.</summary>
    private static async Task ThrowIfWriteFailedAsync(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            string? permissionDetail = await ClientHttpHelpers.ReadProblemDetailAsync(response);
            throw new MessagingPermissionException(
                permissionDetail ?? "This user does not accept private messages.");
        }

        await ClientHttpHelpers.ThrowIfWriteFailedAsync(
            response, detail => new MessagingValidationException([detail]));
    }
}
