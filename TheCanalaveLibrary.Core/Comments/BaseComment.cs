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

    [Required]
    public DateTime DatePosted { get; set; }

    public int LikeCount { get; set; }

    public int ActiveReportCount { get; set; }

    public virtual BlogPostComment? BlogPostComment { get; set; }

    public virtual ChapterComment? ChapterComment { get; set; }

    public virtual ICollection<CommentLike> Likes { get; set; } = new List<CommentLike>();

    public virtual ICollection<FeatureContribution> FeatureContributions { get; set; } = new List<FeatureContribution>();

    public virtual GroupComment? GroupComment { get; set; }

    public virtual ICollection<BaseComment> InverseParentComment { get; set; } = new List<BaseComment>();

    public virtual BaseComment? ParentComment { get; set; }

    public virtual User? Author { get; set; }

    public virtual UserProfileComment? UserProfileComment { get; set; }
}
