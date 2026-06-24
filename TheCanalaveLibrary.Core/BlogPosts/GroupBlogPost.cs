namespace TheCanalaveLibrary.Core;

/// <summary>
/// Blog post authored within a group context.
/// TPT child of <see cref="BaseBlogPost"/>; maps to <c>group_blog_posts</c> table.
/// GroupBlogPost UI ships in WU32 (Groups); this entity is here for L1 completeness.
/// </summary>
public class GroupBlogPost : BaseBlogPost
{
    public bool IsPublished { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime LastUpdatedDate { get; set; }
    public Rating Rating { get; set; }

    public int GroupId { get; set; }
    public virtual Group? Group { get; set; }
}
