namespace TheCanalaveLibrary.Core;

/// <summary>
/// Blog post authored for a user's profile — may optionally link to one of the author's stories.
/// TPT child of <see cref="BaseBlogPost"/>; maps to <c>profile_blog_posts</c> table.
/// </summary>
public class ProfileBlogPost : BaseBlogPost
{
    public bool IsPublished { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime LastUpdatedDate { get; set; }
    public Rating Rating { get; set; }

    /// <summary>
    /// Optional FK to the story this post is about. SET NULL on story deletion.
    /// </summary>
    public int? StoryId { get; set; }

    public virtual Story? Story { get; set; }

    /// <summary>
    /// True when the post contains spoilers for the linked (or any other) story.
    /// Readers see a spoiler indicator on <see cref="BlogPostCard"/> and the view page.
    /// </summary>
    public bool HasSpoilers { get; set; }
}
