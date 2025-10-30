using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class UserProfileComment
{
    public long CommentId { get; set; }

    public int ProfileUserId { get; set; }

    public virtual BaseComment Comment { get; set; } = null!;

    public virtual User ProfileUser { get; set; } = null!;
}
