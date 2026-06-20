namespace TheCanalaveLibrary.Core;

/// <summary>
/// Explicit junction for a user liking a comment (§6.11). Deliberately anti-addictive: no DateLiked,
/// no notification. The denormalized count lives on <see cref="BaseComment.LikeCount"/>.
/// </summary>
public class CommentLike
{
    public long CommentId { get; set; }
    public int UserId { get; set; }

    // --- Navigation Properties ---
    public virtual BaseComment Comment { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
