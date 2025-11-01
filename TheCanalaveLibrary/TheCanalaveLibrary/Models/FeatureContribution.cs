using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Models;

public partial class FeatureContribution
{
    public int ContributionId { get; set; }

    public int? UserId { get; set; }

    public long? CommentId { get; set; }

    public int? BlogPostId { get; set; }

    [Required]
    [MaxLength(256)]
    public string FeatureName { get; set; } = null!;

    public DateTime DateAwarded { get; set; }

    public virtual BaseBlogPost? BlogPost { get; set; }

    public virtual BaseComment? Comment { get; set; }

    public virtual User? User { get; set; }
}
