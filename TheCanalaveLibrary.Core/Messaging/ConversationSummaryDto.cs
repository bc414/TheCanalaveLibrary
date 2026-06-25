namespace TheCanalaveLibrary.Core;

/// <summary>
/// Summary view of a single conversation for the conversation list.
/// <para>
/// <see cref="UnreadCount"/> counts messages sent <em>after</em> the viewer's
/// <c>LastReadTimestamp</c> by the other participant (own messages never count as unread).
/// Zero when the viewer has no <c>LastReadTimestamp</c> yet and there are no messages
/// (i.e. immediately after creating the conversation).
/// </para>
/// <para>
/// <see cref="LastMessagePreview"/> is an HTML-stripped plain-text excerpt (≤100 chars) of
/// the most recent message, or <c>null</c> if the conversation has no messages yet.
/// </para>
/// </summary>
public record ConversationSummaryDto(
    int ConversationId,
    string Subject,
    MessagingParticipantDto OtherParticipant,
    string? LastMessagePreview,
    DateTime? LastMessageDate,
    int UnreadCount,
    bool IsArchived);
