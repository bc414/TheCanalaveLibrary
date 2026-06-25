namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the messaging service (Feature 49). Uses the read replica DbContext.
/// Inject this in components that only need to display conversations or messages.
/// All methods are scoped to the current authenticated viewer via <c>IActiveUserContext</c>.
/// </summary>
public interface IMessagingReadService
{
    /// <summary>
    /// Returns all conversations the current user participates in, ordered by the most recent
    /// message date (newest first). Archived conversations are excluded unless
    /// <paramref name="includeArchived"/> is <c>true</c>.
    /// </summary>
    Task<IReadOnlyList<ConversationSummaryDto>> GetConversationsAsync(bool includeArchived = false);

    /// <summary>
    /// Returns a paged thread view for a specific conversation.
    /// Throws <see cref="KeyNotFoundException"/> if the conversation doesn't exist or the
    /// current user is not a participant.
    /// </summary>
    /// <param name="conversationId">The conversation to load.</param>
    /// <param name="page">1-based page number (1 = most recent messages).</param>
    /// <param name="pageSize">Number of messages per page.</param>
    Task<ConversationThreadDto> GetConversationThreadAsync(
        int conversationId, int page, int pageSize);

    /// <summary>
    /// Returns the number of conversations with at least one unread message (messages sent
    /// after the viewer's <c>LastReadTimestamp</c> by the other participant).
    /// Returns 0 when the current user is anonymous.
    /// </summary>
    Task<int> GetUnreadConversationCountAsync();

    /// <summary>
    /// Looks up a user by their exact username (case-insensitive) for the compose flow.
    /// Returns <c>null</c> when no matching user exists.
    /// Used as a building-block method by the "New Message" standalone compose flow
    /// (no <c>IUserProfileReadService</c> exists until WU30).
    /// </summary>
    Task<MessagingParticipantDto?> FindUserByUsernameAsync(string username);
}
