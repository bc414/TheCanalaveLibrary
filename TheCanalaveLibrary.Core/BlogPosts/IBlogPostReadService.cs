namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Blog Posts service contract. Profile blog posts are the WU31 scope;
/// group blog posts follow in WU32. The L2 service is generic over <see cref="BaseBlogPost"/>;
/// only the ProfileBlogPost write/read UI ships now.
/// </summary>
public interface IBlogPostReadService
{
    /// <summary>
    /// Returns the full display DTO for a single blog post, or <c>null</c> when:
    /// — the post does not exist, OR
    /// — the post is unpublished (<c>IsPublished = false</c>) and the caller is not the author.
    /// <c>IsLikedByCurrentUser</c> is per-viewer (always false for anonymous users).
    /// The content-rating global filter applies; authors viewing their own mature/draft posts
    /// bypass it via <c>IgnoreQueryFilters(["ContentRating"])</c>.
    /// </summary>
    Task<BlogPostDto?> GetByIdAsync(int blogPostId);

    /// <summary>
    /// Returns a page of <see cref="BlogPostListingDto"/> for a given author, ordered newest-first.
    /// By default includes published posts only (for a public profile feed). When
    /// <paramref name="includeUnpublished"/> is <c>true</c> (owner viewing their own Blog tab),
    /// unpublished drafts are included too, with <see cref="BlogPostListingDto.IsPublished"/> set
    /// accordingly so the card can display a "Draft" badge. Callers pass
    /// <c>includeUnpublished: includePrivate</c> where <c>includePrivate = viewerId == authorId</c>.
    /// </summary>
    Task<(BlogPostListingDto[] Items, int TotalCount)> GetByAuthorAsync(
        int authorId, int page, int pageSize, bool includeUnpublished = false);

    /// <summary>
    /// Returns the edit-form DTO for a given blog post, or <c>null</c> if not found.
    /// The caller (editor page) uses <see cref="BlogPostEditDto.AuthorId"/> for a UX
    /// pre-check; the real authorization gate lives in the write service.
    /// Bypasses the content-rating filter so the author can load their own mature/unpublished post.
    /// </summary>
    Task<BlogPostEditDto?> GetForEditAsync(int blogPostId);

    /// <summary>
    /// Returns a page of published <see cref="GroupBlogPost"/> listings for the specified group,
    /// ordered newest-first. Content-rating filter applied (group-level + global user ceiling).
    /// </summary>
    Task<(BlogPostListingDto[] Items, int TotalCount)> GetByGroupAsync(
        int groupId, int page, int pageSize);
}
