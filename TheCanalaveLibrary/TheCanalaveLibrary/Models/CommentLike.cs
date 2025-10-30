using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class CommentLike
{
    public int UserId { get; set; }

    public long CommentId { get; set; }

    public DateTime DateLiked { get; set; }

    public virtual BaseComment Comment { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
