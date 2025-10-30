using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class BlogPostComment
{
    public long CommentId { get; set; }

    public int BlogPostId { get; set; }

    public virtual BlogPost BlogPost { get; set; } = null!;

    public virtual BaseComment Comment { get; set; } = null!;
}
