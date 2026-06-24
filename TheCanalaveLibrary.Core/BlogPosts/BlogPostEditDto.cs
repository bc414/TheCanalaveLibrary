namespace TheCanalaveLibrary.Core;

/// <summary>
/// DTO that hydrates the blog post edit form. Includes <see cref="AuthorId"/> so the editor page can
/// do a client-side UX pre-check (redirecting non-owners before they try to submit) — the real
/// authorization gate lives in the write service, not here.
/// <see cref="Content"/> is sanitized HTML; safe to seed the EditorView's initial HTML.
/// </summary>
public record BlogPostEditDto(
    int BlogPostId,
    int? AuthorId,
    string Title,
    string Content,
    Rating Rating,
    bool HasSpoilers,
    int? StoryId,
    bool IsPublished);
