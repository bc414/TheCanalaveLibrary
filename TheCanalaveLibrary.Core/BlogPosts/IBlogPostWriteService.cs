namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Blog Posts service contract. Inherits the read interface so callers that need
/// both read and write inject only the narrowest applicable interface (layer2-services.md
/// §"CQRS-Lite with Inheritance"). All mutations are author-only; moderation delete is WU34.
/// </summary>
public interface IBlogPostWriteService : IBlogPostReadService
{
    /// <summary>
    /// Creates a new <see cref="ProfileBlogPost"/>. Requires an authenticated user.
    /// <c>AuthorId</c> is server-stamped from <see cref="IActiveUserContext.UserId"/>;
    /// it is absent from <paramref name="dto"/> (mirrors <c>CreateStoryDTO</c>).
    /// Sanitizes <c>dto.Content</c> before persisting. Increments
    /// <c>UserStats.BlogPostsWritten</c> via <c>ExecuteUpdateAsync</c>.
    /// </summary>
    /// <returns>The new <c>BlogPostId</c>.</returns>
    /// <exception cref="BlogPostValidationException">Title or content validation fails.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<int> CreateProfileBlogPostAsync(CreateProfileBlogPostDto dto);

    /// <summary>
    /// Updates an existing blog post. Author-only: throws <see cref="UnauthorizedAccessException"/>
    /// if the caller is not the post's author. Re-sanitizes <c>dto.Content</c> before persisting.
    /// </summary>
    /// <exception cref="BlogPostValidationException">Title or content validation fails.</exception>
    /// <exception cref="KeyNotFoundException">Blog post not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the post's author.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task UpdateBlogPostAsync(UpdateBlogPostDto dto);

    /// <summary>
    /// Hard-deletes a blog post. Author-only. FK cascades handle comments and likes.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Blog post not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the post's author.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task DeleteBlogPostAsync(int blogPostId);

    /// <summary>
    /// Toggles a like on a blog post. Requires an authenticated user. Returns the new
    /// <see cref="BlogPostLikeResultDto"/> with the updated denormalized <c>LikeCount</c> and the
    /// caller's new like state. No notification generated (anti-addictive design — §6 <c>BlogPostLike</c>).
    /// </summary>
    /// <exception cref="KeyNotFoundException">Blog post not found.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<BlogPostLikeResultDto> ToggleLikeAsync(int blogPostId);
}
