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

    /// <summary>
    /// Creates a new <see cref="GroupBlogPost"/> in the specified group. Requires an authenticated
    /// user who is a member of the group. <c>AuthorId</c> is server-stamped from
    /// <see cref="IActiveUserContext.UserId"/>. Sanitizes <c>dto.Content</c> before persisting.
    /// Fires <c>NotifyNewGroupBlogPostAsync</c> best-effort post-commit to members with
    /// <c>NotifyForNewBlogPost = true</c>.
    /// </summary>
    /// <returns>The new <c>BlogPostId</c>.</returns>
    /// <exception cref="BlogPostValidationException">Title or content validation fails.</exception>
    /// <exception cref="KeyNotFoundException">Group not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not a member of the group.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<int> CreateGroupBlogPostAsync(CreateGroupBlogPostDto dto);

    /// <summary>
    /// Creates a new <see cref="SiteBlogPost"/> (WU-SiteNews). Gated <c>IsModerator || IsAdmin</c>
    /// (the <see cref="SitePoll"/> precedent — <c>ServerPollWriteService.CreateSitePollAsync</c>),
    /// not author-only. <c>AuthorId</c> is server-stamped. Sanitizes <c>dto.Content</c>. When
    /// <c>dto.IsPublished &amp;&amp; dto.NotifyAllUsers</c>, fires the SiteAnnouncement fan-out
    /// best-effort post-commit and stamps <see cref="SiteBlogPost.NotifiedAtUtc"/>.
    /// </summary>
    /// <returns>The new <c>BlogPostId</c>.</returns>
    /// <exception cref="BlogPostValidationException">Title or content validation fails.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not a moderator or admin.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task<int> CreateSiteBlogPostAsync(CreateSiteBlogPostDto dto);

    /// <summary>
    /// Updates an existing <see cref="SiteBlogPost"/>. Gated <c>IsModerator || IsAdmin</c> — any
    /// moderator/admin may manage any site post, not just its creator (mirrors
    /// <c>ServerPollWriteService</c>'s <c>LoadAuthorizedPollWithOptionsAsync</c> site-poll branch).
    /// Re-sanitizes <c>dto.Content</c>. The false→true publish transition with
    /// <c>dto.NotifyAllUsers</c> fires the fan-out exactly once — see
    /// <see cref="SiteBlogPost.NotifiedAtUtc"/>; a later edit never re-fires it.
    /// </summary>
    /// <exception cref="BlogPostValidationException">Title or content validation fails.</exception>
    /// <exception cref="KeyNotFoundException">Site post not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not a moderator or admin.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task UpdateSiteBlogPostAsync(UpdateSiteBlogPostDto dto);

    /// <summary>
    /// Hard-deletes a site announcement. Gated <c>IsModerator || IsAdmin</c>, same rule as
    /// <see cref="UpdateSiteBlogPostAsync"/>. FK cascades handle comments and likes.
    /// </summary>
    /// <exception cref="KeyNotFoundException">Site post not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not a moderator or admin.</exception>
    /// <exception cref="InvalidOperationException">Caller is not authenticated.</exception>
    Task DeleteSiteBlogPostAsync(int blogPostId);
}
