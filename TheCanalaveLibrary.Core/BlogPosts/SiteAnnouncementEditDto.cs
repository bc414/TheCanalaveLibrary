namespace TheCanalaveLibrary.Core;

/// <summary>
/// DTO that hydrates the site-announcement edit form — the <see cref="SiteBlogPost"/> counterpart
/// to <see cref="BlogPostEditDto"/>, kept separate rather than overloading that record with
/// fields (Rating/HasSpoilers/StoryId) it doesn't carry. <see cref="AuthorId"/> is informational
/// only here (unlike <see cref="BlogPostEditDto"/>'s UX pre-check use) — the real gate is
/// <c>IsModerator || IsAdmin</c>, not author identity; any moderator/admin may edit.
/// <see cref="Content"/> is sanitized HTML; safe to seed the EditorView's initial HTML.
/// </summary>
public record SiteAnnouncementEditDto(
    int BlogPostId,
    int? AuthorId,
    string Title,
    string Content,
    bool IsPublished,
    bool NotifyAllUsers);
