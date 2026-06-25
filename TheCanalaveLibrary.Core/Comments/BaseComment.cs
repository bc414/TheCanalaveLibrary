using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

public partial class BaseComment
{
    [Key]
    public long CommentId { get; set; }

    public int? UserId { get; set; }

    public long? ParentCommentId { get; set; }

    [Required]
    public string CommentText { get; set; } = null!;

    public int LikeCount { get; set; }

    public int ActiveReportCount { get; set; }

    // Soft-delete (ModeratedVisibility named filter) — WU34
    public bool IsHidden { get; set; }
    public DateTime? DateModeratedRemoved { get; set; }
    [MaxLength(1024)]
    public string? ModerationRemovalReason { get; set; }

    public virtual ICollection<CommentLike> Likes { get; set; } = new List<CommentLike>();

    public virtual ICollection<FeatureContribution> FeatureContributions { get; set; } = new List<FeatureContribution>();

    public virtual ICollection<BaseComment> InverseParentComment { get; set; } = new List<BaseComment>();

    public virtual BaseComment? ParentComment { get; set; }

    public virtual User? Author { get; set; }
}
