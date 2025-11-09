using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core.Models;

public partial class PrivateMessage
{
    [Key]
    public long MessageId { get; set; }

    public int ConversationId { get; set; }

    public int? SenderUserId { get; set; }

    [Required]
    public string MessageText { get; set; } = null!;

    public DateTime DateSent { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User? SenderUser { get; set; }
}
