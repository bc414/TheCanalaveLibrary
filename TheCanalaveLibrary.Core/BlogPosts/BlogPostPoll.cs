namespace TheCanalaveLibrary.Core;

/// <summary>
/// Poll attached to a blog post — created/managed by the post's author, rendered as blocks
/// after the post content (multiple per post allowed).
/// </summary>
public class BlogPostPoll : BasePoll
{
    public int BlogPostId { get; set; }

    public BaseBlogPost BlogPost { get; set; } = null!;
}
