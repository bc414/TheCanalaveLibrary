using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class PrivateMessage
{
    public long MessageId { get; set; }

    public int ConversationId { get; set; }

    public int? SenderUserId { get; set; }

    public string MessageText { get; set; } = null!;

    public DateTime DateSent { get; set; }

    public virtual Conversation Conversation { get; set; } = null!;

    public virtual User? SenderUser { get; set; }
}
