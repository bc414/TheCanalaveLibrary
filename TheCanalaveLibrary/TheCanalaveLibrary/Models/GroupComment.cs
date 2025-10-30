using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class GroupComment
{
    public long CommentId { get; set; }

    public int GroupId { get; set; }

    public virtual BaseComment Comment { get; set; } = null!;

    public virtual Group Group { get; set; } = null!;
}
