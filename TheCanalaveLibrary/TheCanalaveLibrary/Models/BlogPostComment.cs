using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class BlogPostComment : BaseComment
{
    public int BlogPostId { get; set; }

    public virtual BaseBlogPost BlogPost { get; set; } = null!;
    
    public DateTime DatePosted { get; set; }
}
