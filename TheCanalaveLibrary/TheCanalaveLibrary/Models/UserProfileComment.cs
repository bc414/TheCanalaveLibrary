using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class UserProfileComment : BaseComment
{
    public int ProfileUserId { get; set; }

    public virtual User ProfileUser { get; set; } = null!;
}
