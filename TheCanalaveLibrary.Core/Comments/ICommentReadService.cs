namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Comments service contract. Chapter-context only for MVP; profile/group/blog-post
/// contexts follow in their respective feature work-units (WU30/WU32/WU31).
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
}
