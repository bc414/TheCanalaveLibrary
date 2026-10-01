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

    /// <summary>
    /// Returns a page of profile-wall comments for a given user, ordered roots-newest-first and
    /// replies-oldest-first within each root. Mirrors <see cref="GetGroupCommentsAsync"/>. No
    /// spoiler flag on profile comments. The <paramref name="profileUserId"/> is the wall owner
    /// (whose profile the comments appear on), not the commenter.
    /// <c>ProfileVisibility</c>-gated (WU-AccessGate): empty when the wall owner's profile is hidden
    /// from the caller; the owner always sees their own wall. <c>AllowProfileComments</c> governs
    /// posting, not reading — the profile page hides the wall for <c>Nobody</c>.
    /// </summary>
    Task<CommentPageDto> GetUserProfileCommentsAsync(int profileUserId, int page, int pageSize);
}
