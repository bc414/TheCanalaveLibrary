namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the messaging service (Feature 49). Inherits read methods and adds mutations.
/// Inject this in components or pages that need to send messages or manage conversations.
/// All methods require an authenticated user; unauthenticated callers receive
/// <see cref="InvalidOperationException"/>.
/// </summary>
public interface IMessagingWriteService : IMessagingReadService
{
    /// <summary>
    /// Starts a new 1-on-1 conversation with a first message.
    /// <para>
    /// Enforces the <c>AllowPrivateMessages</c> privacy gate on the recipient — throws
    /// <see cref="MessagingPermissionException"/> if the recipient's settings block the sender.
    /// </para>
    /// <para>
    /// Sanitizes <see cref="StartConversationDto.MessageHtml"/> before persisting
    /// (sanitize-once-on-save). Throws <see cref="MessagingValidationException"/> on empty
    /// subject / empty body / self-message.
    /// </para>
    /// </summary>
    /// <returns>The new <c>ConversationId</c>.</returns>
    Task<int> StartConversationAsync(StartConversationDto dto);

    /// <summary>
    /// Appends a new message to an existing conversation.
    /// Does not re-check the <c>AllowPrivateMessages</c> gate — once a thread exists both
    /// parties can always reply.
    /// Throws <see cref="KeyNotFoundException"/> if the conversation doesn't exist or the
    /// current user is not a participant.
    /// </summary>
    /// <param name="conversationId">Target conversation.</param>
    /// <param name="messageHtml">Raw HTML from <c>EditorView.GetHtmlAsync()</c>.</param>
    Task<MessageDto> SendMessageAsync(int conversationId, string messageHtml);

    /// <summary>
    /// Advances the current user's <c>LastReadTimestamp</c> for a conversation to
    /// <see cref="DateTime.UtcNow"/>, clearing all unread state for that thread.
    /// No-ops when the current user is not a participant.
    /// </summary>
    Task MarkConversationReadAsync(int conversationId);

    /// <summary>
    /// Toggles the <c>IsArchived</c> flag on the current user's participant row for a
    /// conversation. Does not affect the other participant's view.
    /// Throws <see cref="KeyNotFoundException"/> if the current user is not a participant.
    /// </summary>
    Task SetArchivedAsync(int conversationId, bool archived);
}
