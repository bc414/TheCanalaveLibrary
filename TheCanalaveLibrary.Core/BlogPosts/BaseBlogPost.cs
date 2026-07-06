using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

/// <summary>
/// TPT root for profile and group blog posts. Shared identity + display payload + shared counters.
/// Discovery columns (<c>DateCreated</c>, <c>LastUpdatedDate</c>, <c>Rating</c>,
/// <c>IsPublished</c>) live on the child tables — declared on each derived class so EF Core 10
/// maps them to the child table, not here.
/// </summary>
public abstract class BaseBlogPost : IModeratableContent
{
    // IModeratableContent — AuthorUserId maps to the blog post's AuthorId FK
    int? IModeratableContent.AuthorUserId => AuthorId;

    [Key]
    public int BlogPostId { get; set; }
    public int? AuthorId { get; set; }

    [Required]
    [MaxLength(256)]
    public string Title { get; set; } = null!;

    [Required]
    public string Content { get; set; } = null!;

    // ViewCount dropped (R2): never written by any path; view counting is stories-only, accumulated
    // in daily_story_stats. Re-addable via a per-post daily-stat row if blog views are ever wanted.
    public int LikeCount { get; set; }

    public int ActiveReportCount { get; set; }

    // Soft-delete (IsTakenDown named filter) — WU34; renamed from IsHidden/DateModeratedRemoved/ModerationRemovalReason in pre-integration cleanup
    public bool IsTakenDown { get; set; }
    public DateTime? TakedownDate { get; set; }
    [MaxLength(1024)]
    public string? TakedownReason { get; set; }

    public virtual User? Author { get; set; }

    public virtual ICollection<BlogPostComment> BlogPostComments { get; set; } = new List<BlogPostComment>();

    public virtual ICollection<BlogPostLike> Likes { get; set; } = new List<BlogPostLike>();

    public virtual ICollection<FeatureContribution> FeatureContributions { get; set; } = new List<FeatureContribution>();

    public ICollection<BasePoll> Polls { get; set; } = new List<BasePoll>();
}
