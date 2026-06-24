namespace TheCanalaveLibrary.Core;

/// <summary>
/// Full display DTO for a single blog post view page.
/// <see cref="AuthorDisplayName"/> is nullable to support deleted-author anonymization (SET NULL on delete).
/// <see cref="Content"/> is sanitized HTML — safe to render with <c>@@((MarkupString)Content)</c> via
/// <c>RichTextView</c>.
/// <see cref="IsLikedByCurrentUser"/> is per-viewer state, projected from <c>BlogPostLikes</c>
/// using the active user's id; false for anonymous viewers.
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
    int ViewCount,
    bool IsPublished);
