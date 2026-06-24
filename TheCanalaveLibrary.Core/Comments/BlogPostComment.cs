namespace TheCanalaveLibrary.Core;

public partial class BlogPostComment : BaseComment
{
    public DateTime DatePosted { get; set; }

    public int BlogPostId { get; set; }

    public virtual BaseBlogPost BlogPost { get; set; } = null!;
}
