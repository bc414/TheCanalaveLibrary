using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

public partial class BaseComment : IModeratableContent
{
    // IModeratableContent — AuthorUserId maps to the comment's UserId FK
    int? IModeratableContent.AuthorUserId => UserId;

    [Key]
    public long CommentId { get; set; }

    public int? UserId { get; set; }

    public long? ParentCommentId { get; set; }

    [Required]
    public string CommentText { get; set; } = null!;

    public int LikeCount { get; set; }

    public int ActiveReportCount { get; set; }

    // Soft-delete (IsTakenDown named filter) — WU34; renamed from IsHidden/DateModeratedRemoved/ModerationRemovalReason in pre-integration cleanup
    public bool IsTakenDown { get; set; }
    public DateTime? TakedownDate { get; set; }
    [MaxLength(1024)]
    public string? TakedownReason { get; set; }

    public virtual ICollection<CommentLike> Likes { get; set; } = new List<CommentLike>();

    public virtual ICollection<BaseComment> InverseParentComment { get; set; } = new List<BaseComment>();

    public virtual BaseComment? ParentComment { get; set; }

    public virtual User? Author { get; set; }
}
