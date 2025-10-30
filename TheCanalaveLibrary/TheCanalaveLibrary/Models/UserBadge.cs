using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class UserBadge
{
    public int UserId { get; set; }

    public string BadgeKey { get; set; } = null!;

    public DateTime DateEarned { get; set; }

    public int DisplayOrder { get; set; }

    public virtual Badge BadgeKeyNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
