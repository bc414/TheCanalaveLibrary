namespace TheCanalaveLibrary.Core;

/// <summary>
/// Summary view of a single conversation for the conversation list.
/// <para>
/// <see cref="UnreadCount"/> counts messages sent <em>after</em> the viewer's
/// <c>LastReadTimestamp</c> by the other participant (own messages never count as unread).
/// Zero when the viewer has no <c>LastReadTimestamp</c> yet and there are no messages
/// (i.e. immediately after creating the conversation). Deliberately still populated for
/// archived conversations — sticky archiving mutes the global badge, not this count.
/// </para>
/// <para>
/// <see cref="LastMessagePreview"/> is an HTML-stripped plain-text excerpt (≤100 chars) of
/// the most recent message, or <c>null</c> if the conversation has no messages yet.
/// </para>
/// <para>
/// Carries no <c>IsArchived</c> flag: listings are always scoped
/// (<see cref="ConversationScope"/>), so every row's archived state is implied by the scope
/// it was requested under. The per-thread flag lives on
/// <see cref="ConversationThreadDto.IsArchived"/>, where direct-URL navigation needs it.
/// </para>
/// </summary>
public record ConversationSummaryDto(
    int ConversationId,
    string Subject,
    MessagingParticipantDto OtherParticipant,
    string? LastMessagePreview,
    DateTime? LastMessageDate,
    int UnreadCount);
