namespace TheCanalaveLibrary.Core.Models;

public partial class BlogPostComment : BaseComment
{
    public int BlogPostId { get; set; }

    public virtual BaseBlogPost BlogPost { get; set; } = null!;
}
