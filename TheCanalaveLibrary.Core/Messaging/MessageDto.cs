namespace TheCanalaveLibrary.Core;

/// <summary>
/// A single message in a conversation thread.
/// <para>
/// <see cref="MessageText"/> is the sanitized, trusted HTML stored in the database —
/// rendered via <c>RichTextView</c> without re-sanitization (sanitize-once-on-save convention).
/// </para>
/// <para>
/// <see cref="SenderUserId"/> is <c>null</c> when the original sender has been deleted
/// (SetNull FK policy on <c>PrivateMessage.SenderUserId</c>); in that case
/// <see cref="SenderUsername"/> will be "[deleted]" and <see cref="IsOwnMessage"/> false.
/// </para>
/// </summary>
public record MessageDto(
    long MessageId,
    int ConversationId,
    int? SenderUserId,
    string SenderUsername,
    string SenderAvatarUrl,
    string MessageText,
    DateTime DateSent,
    bool IsOwnMessage);
