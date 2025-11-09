namespace TheCanalaveLibrary.Core.Models;

public class GroupBlogPost : BaseBlogPost
{
    public int GroupId { get; set; }
    public virtual Group? Group { get; set; }
}