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
    /// Returns a page of <see cref="BlogPostListingDto"/> for a given author, ordered
    /// newest-first. Includes published posts only (for profile feed — use the author-bypass
    /// overload when the current user is the author and wants to see drafts).
    /// </summary>
    Task<(BlogPostListingDto[] Items, int TotalCount)> GetByAuthorAsync(
        int authorId, int page, int pageSize);

    /// <summary>
    /// Returns the edit-form DTO for a given blog post, or <c>null</c> if not found.
    /// The caller (editor page) uses <see cref="BlogPostEditDto.AuthorId"/> for a UX
    /// pre-check; the real authorization gate lives in the write service.
    /// Bypasses the content-rating filter so the author can load their own mature/unpublished post.
    /// </summary>
    Task<BlogPostEditDto?> GetForEditAsync(int blogPostId);
}
