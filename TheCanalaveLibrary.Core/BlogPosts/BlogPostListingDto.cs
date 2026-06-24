namespace TheCanalaveLibrary.Core;

/// <summary>
/// Lightweight DTO for rendering a <c>BlogPostCard</c> in the profile feed.
/// <see cref="ContentSnippet"/> is a plain-text excerpt (HTML stripped), safe to render as text.
/// Per-viewer interaction state (liked, etc.) is intentionally absent — the listing surface
/// does not show per-viewer like state (anti-addictive design).
/// </summary>
public record BlogPostListingDto(
    int BlogPostId,
    string Title,
    string ContentSnippet,
    DateTime DateCreated,
    Rating Rating,
    bool HasSpoilers);
