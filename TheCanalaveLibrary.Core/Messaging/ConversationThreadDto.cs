namespace TheCanalaveLibrary.Core;

/// <summary>
/// The full thread view for a single conversation — header, participants, and a paged message list.
/// Messages are ordered oldest-first within the page (ascending <c>DateSent</c>), while pagination
/// loads older messages as the user scrolls up (latest page by default, earlier pages on request).
/// </summary>
public record ConversationThreadDto(
    int ConversationId,
    string Subject,
    MessagingParticipantDto OtherParticipant,
    IReadOnlyList<MessageDto> Messages,
    int TotalMessageCount);
