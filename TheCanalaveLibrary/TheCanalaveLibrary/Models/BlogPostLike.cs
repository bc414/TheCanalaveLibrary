using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class BlogPostLike
{
    public int UserId { get; set; }

    public int BlogPostId { get; set; }

    public DateTime DateLiked { get; set; }

    public virtual BlogPost BlogPost { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
