using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class BaseComment
{
    public long CommentId { get; set; }

    public int? UserId { get; set; }

    public long? ParentCommentId { get; set; }

    public string CommentText { get; set; } = null!;

    public int LikeCount { get; set; }

    public DateTime DatePosted { get; set; }

    public int ActiveReportCount { get; set; }

    public string CommentType { get; set; } = null!;

    public virtual BlogPostComment? BlogPostComment { get; set; }

    public virtual ChapterComment? ChapterComment { get; set; }

    public virtual ICollection<CommentLike> CommentLikes { get; set; } = new List<CommentLike>();

    public virtual ICollection<FeatureContribution> FeatureContributions { get; set; } = new List<FeatureContribution>();

    public virtual GroupComment? GroupComment { get; set; }

    public virtual ICollection<BaseComment> InverseParentComment { get; set; } = new List<BaseComment>();

    public virtual BaseComment? ParentComment { get; set; }

    public virtual User? User { get; set; }

    public virtual UserProfileComment? UserProfileComment { get; set; }
}
