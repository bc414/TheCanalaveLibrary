namespace TheCanalaveLibrary.Core.Models;

public class BlogPostPoll : BasePoll
{
    public int BlogPostId { get; set; }

    public BaseBlogPost BlogPost { get; set; } = null!;
}