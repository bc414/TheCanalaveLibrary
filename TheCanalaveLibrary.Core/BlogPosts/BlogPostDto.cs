namespace TheCanalaveLibrary.Core;

/// <summary>
/// Which <see cref="BaseBlogPost"/> subtype a <see cref="BlogPostDto"/> came from — the child table
/// is the discriminator (TPT). Lifecycle operations are per subtype (owner ruling D10), so a page
/// that offers one must know which it shows. Never stored.
/// </summary>
public enum BlogPostKind : short
{
    Profile = 0,
    Group = 1,
    Site = 2,
}

/// <summary>
/// Full display DTO for a single blog post view page.
/// <see cref="AuthorDisplayName"/> is nullable to support deleted-author anonymization (SET NULL on delete).
/// <see cref="Content"/> is sanitized HTML — safe to render with <c>@@((MarkupString)Content)</c> via
/// <c>RichTextView</c>.
/// <see cref="IsLikedByCurrentUser"/> is per-viewer state, projected from <c>BlogPostLikes</c>
/// using the active user's id; false for anonymous viewers.
/// <see cref="ViewerHasCompletedStory"/> is per-viewer state (WU-B2; mirrors
/// <c>ChapterReadingDto.ViewerHasCompletedStory</c>): true only when the post is story-linked and
/// the signed-in viewer's <c>UserStoryInteraction.IsCompleted</c> is set for that story. Gates the
/// spoiler interstitial's reveal path — false for anonymous viewers and non-story-linked posts.
/// <see cref="Kind"/> names the subtype: the profile-post editor (<c>/blog/{id}/edit</c>) serves
/// <see cref="BlogPostKind.Profile"/> only, so the page offers Edit for that kind alone
/// (WU-TptHardDelete).
/// </summary>
public record BlogPostDto(
    int BlogPostId,
    int? AuthorId,
    string? AuthorDisplayName,
    string Title,
    string Content,
    Rating Rating,
    bool HasSpoilers,
    int? StoryId,
    string? LinkedStoryTitle,
    DateTime DateCreated,
    DateTime LastUpdatedDate,
    int LikeCount,
    bool IsLikedByCurrentUser,
    bool IsPublished,
    BlogPostKind Kind,
    bool ViewerHasCompletedStory = false);
