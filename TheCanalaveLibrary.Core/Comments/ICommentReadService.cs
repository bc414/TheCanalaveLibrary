namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Comments service contract. Chapter context shipped WU19; blog-post context
/// added WU31; group context added WU32. Each context uses a dedicated method pair
/// (per-context method pattern — see <c>layer2-services.md</c> §"Group Comments").
/// </summary>
public interface ICommentReadService
{
    /// <summary>
    /// Returns a page of chapter comments with their direct replies, ordered roots-newest-first
    /// and replies-oldest-first within each root. <c>CommentPageDto.TotalRootCount</c> is the
    /// total count of root-level comments (not replies), suitable for feeding
    /// <c>PaginationControls.TotalCount</c>. <c>IsLikedByCurrentUser</c> on each DTO is
    /// per-viewer (always false for anonymous users).
    /// </summary>
    Task<CommentPageDto> GetChapterCommentsAsync(int chapterId, int page, int pageSize);

    /// <summary>
    /// Returns a page of blog-post comments with their direct replies, ordered roots-newest-first
    /// and replies-oldest-first within each root. Mirrors <see cref="GetChapterCommentsAsync"/>.
    /// <c>IsLikedByCurrentUser</c> on each DTO is per-viewer (always false for anonymous users).
    /// Blog-post comments have no spoiler flag — use <see cref="ProfileBlogPost.HasSpoilers"/> on
    /// the post itself.
    /// </summary>
    Task<CommentPageDto> GetBlogPostCommentsAsync(int blogPostId, int page, int pageSize);

    /// <summary>
    /// Returns a page of group comments with their direct replies, ordered roots-newest-first
    /// and replies-oldest-first within each root. Mirrors <see cref="GetBlogPostCommentsAsync"/>.
    /// No spoiler flag on group comments.
    /// </summary>
    Task<CommentPageDto> GetGroupCommentsAsync(int groupId, int page, int pageSize);
}
