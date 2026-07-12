namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of Feature 37 (Polls). All projections are viewer-relative: results visibility and
/// the viewer's own votes are computed server-side (see <see cref="PollDto"/>). Requirements:
/// <c>audit/BlogPosts.md</c> Feature 37 (settled 2026-07-12).
/// </summary>
public interface IPollReadService
{
    /// <summary>
    /// All site polls for the <c>/polls</c> page, newest-opened first.
    /// <paramref name="includeArchived"/> false = active list only (<c>IsArchived = false</c>);
    /// true = both (the page splits them via <see cref="PollDto.IsArchived"/>).
    /// </summary>
    Task<PollDto[]> GetSitePollsAsync(bool includeArchived);

    /// <summary>Polls attached to a blog post, in creation order (rendered after post content).</summary>
    Task<PollDto[]> GetPollsForBlogPostAsync(int blogPostId);

    /// <summary>Single poll (either kind) or null when it doesn't exist.</summary>
    Task<PollDto?> GetPollAsync(int pollId);
}
