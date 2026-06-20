namespace TheCanalaveLibrary.Core;

public class BlogPostPoll : BasePoll
{
    public int BlogPostId { get; set; }

    public BaseBlogPost BlogPost { get; set; } = null!;
}