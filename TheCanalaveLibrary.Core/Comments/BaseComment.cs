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

    /// <summary>
    /// When the author last changed the comment's text or spoiler flag; null = never edited. Stamped by
    /// <c>EditCommentAsync</c> only on a real change, so an identical re-save leaves it alone
    /// (WU-ParityAndRemaining, 2026-10-13; service audit §2.12). On the base table, not the children:
    /// editing is context-agnostic, and this is display metadata, never a sort key (the per-child
    /// <c>DatePosted</c> denormalization exists for sort keys).
    /// </summary>
    public DateTime? DateEdited { get; set; }

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
