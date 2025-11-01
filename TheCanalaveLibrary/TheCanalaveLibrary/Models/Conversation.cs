using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Models;

public partial class Conversation
{
    public int ConversationId { get; set; }

    [Required] [MaxLength(2048)] public string Subject { get; set; } = null!;

    public DateTime DateCreated { get; set; }

    public virtual ICollection<ConversationParticipant> ConversationParticipants { get; set; } = new List<ConversationParticipant>();

    public virtual ICollection<PrivateMessage> PrivateMessages { get; set; } = new List<PrivateMessage>();
}
