using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class FollowedUser
{
    public int UserId { get; set; }

    public int FollowedUserId { get; set; }

    public DateTime DateFollowed { get; set; }

    public bool ReceiveAlerts { get; set; }

    public virtual User FollowedUserNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
