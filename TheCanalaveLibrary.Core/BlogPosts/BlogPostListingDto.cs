namespace TheCanalaveLibrary.Core;

/// <summary>
/// Lightweight DTO for rendering a <c>BlogPostCard</c> in the profile feed or group blog section.
/// <see cref="ContentSnippet"/> is a plain-text excerpt (HTML stripped), safe to render as text.
/// Per-viewer interaction state (liked, etc.) is intentionally absent — the listing surface
/// does not show per-viewer like state (anti-addictive design).
///
/// <see cref="IsPublished"/> is always <c>true</c> for group-feed projections
/// (<see cref="IBlogPostReadService.GetByGroupAsync"/>). For author-feed projections
/// (<see cref="IBlogPostReadService.GetByAuthorAsync"/>), it may be <c>false</c> when
/// <c>includeUnpublished</c> is passed (owner viewing their own profile).
///
/// <see cref="AuthorId"/> (tracker B17, WU-TrackerFixes 2026-10-13) lets a mixed-author feed — the
/// group blog section — hide the Report control on the viewer's own posts. Null for a post whose
/// author deleted their account. Optional and last, so older payloads still deserialize.
/// </summary>
public record BlogPostListingDto(
    int BlogPostId,
    string Title,
    string ContentSnippet,
    DateTime DateCreated,
    Rating Rating,
    bool HasSpoilers,
    bool IsPublished = true,
    int? AuthorId = null);
