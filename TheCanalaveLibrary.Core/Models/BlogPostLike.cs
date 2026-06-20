namespace TheCanalaveLibrary.Core;

/// <summary>
/// Explicit junction for a user liking a blog post. Anti-addictive: no DateLiked, no notification.
/// The denormalized count lives on <see cref="BaseBlogPost.LikeCount"/>.
/// </summary>
public class BlogPostLike
{
    public int BlogPostId { get; set; }
    public int UserId { get; set; }

    // --- Navigation Properties ---
    public virtual BaseBlogPost BlogPost { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
