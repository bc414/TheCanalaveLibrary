namespace TheCanalaveLibrary.Core.Models;

public partial class ConversationParticipant
{
    public int ConversationId { get; set; }

    public int UserId { get; set; }

    public DateTime? LastReadTimestamp { get; set; }

    public bool IsArchived { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
