using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

/// <summary>
/// A blog post can be just for a user, or can it be attached to a story or a group
/// </summary>
public abstract class BaseBlogPost
{
    public int BlogPostId { get; set; }
    public int? AuthorId { get; set; }
    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public int ViewCount { get; set; }

    public int LikeCount { get; set; }

    public bool IsPublished { get; set; }

    public DateTime DateCreated { get; set; }

    public DateTime LastUpdatedDate { get; set; }

    public int ActiveReportCount { get; set; }
    public Rating Rating { get; set; }

    public virtual User? Author { get; set; }

    public virtual ICollection<BlogPostComment> BlogPostComments { get; set; } = new List<BlogPostComment>();

    public virtual ICollection<BlogPostLike> BlogPostLikes { get; set; } = new List<BlogPostLike>();

    public virtual ICollection<FeatureContribution> FeatureContributions { get; set; } = new List<FeatureContribution>();

    

    
}
