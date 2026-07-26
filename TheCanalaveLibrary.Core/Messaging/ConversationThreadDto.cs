namespace TheCanalaveLibrary.Core;

/// <summary>
/// The full thread view for a single conversation — header, participants, and a paged message list.
/// Messages are ordered oldest-first within the page (ascending <c>DateSent</c>), while pagination
/// loads older messages as the user scrolls up (latest page by default, earlier pages on request).
/// <para>
/// <see cref="IsArchived"/> is the <em>viewer's own</em> archived state for this conversation (the
/// flag lives on the participant row, so the two parties can differ). It is carried on the thread —
/// not read off the conversation list — so the archive control resolves correctly when a thread is
/// opened by direct URL, where the sidebar list may not contain that conversation at all.
/// </para>
/// </summary>
public record ConversationThreadDto(
    int ConversationId,
    string Subject,
    MessagingParticipantDto OtherParticipant,
    IReadOnlyList<MessageDto> Messages,
    int TotalMessageCount,
    bool IsArchived);
