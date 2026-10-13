namespace TheCanalaveLibrary.Core;

public partial class ConversationParticipant
{
    public int ConversationId { get; set; }

    public int UserId { get; set; }

    public DateTime? LastReadTimestamp { get; set; }

    /// <summary>
    /// The private-message email watermark (owner ruling D18, WU-PrivateMessageEmail): the
    /// <c>DateSent</c> of the newest other-party message the last new-message email to this participant
    /// covered. NULL = never mailed. The current unread run counts as notified while this is set and
    /// later than <see cref="LastReadTimestamp"/>; reading the conversation (or replying, which stamps
    /// <see cref="LastReadTimestamp"/>) reopens it, so the next new message mails once more. Written
    /// only by <c>PrivateMessageEmailComposer</c>'s after-send hook, set-based, after the mail left.
    /// See <c>layer2-services.md</c> §"Private-Message Email".
    /// </summary>
    public DateTime? LastNotifiedTimestamp { get; set; }

    public bool IsArchived { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
