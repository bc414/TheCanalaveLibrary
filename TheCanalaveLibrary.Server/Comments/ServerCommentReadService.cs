using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for Comments. Chapter-context only for MVP.
/// Uses a two-step load: paginate root ids from the golden index
/// (<c>(chapter_id, date_posted DESC)</c>, L6/post-MVP), then fetch roots + their direct replies
/// in one EF query. In-memory ordering restores roots newest-first and replies oldest-first.
/// Queries go through the <c>ChapterComments</c> DbSet (not <c>BaseComments</c>) so that EF Core
/// uses the TPT join automatically and all inherited <c>BaseComment</c> properties are accessible
/// without navigating through the shadow <c>chapter_comment_comment_id</c> FK.
/// </summary>
public class ServerCommentReadService(
    ReadOnlyApplicationDbContext readDb,
    IActiveUserContext activeUser) : ICommentReadService
{
    /// <summary>
    /// Exposed as a protected property so the derived write service can access the user context
    /// without double-capturing the constructor parameter (eliminates CS9107/CS9124 warnings).
    /// </summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    public async Task<CommentPageDto> GetChapterCommentsAsync(int chapterId, int page, int pageSize)
    {
        // Root count for PaginationControls — does not include replies.
        int totalRootCount = await readDb.ChapterComments
            .Where(c => c.ChapterId == chapterId && c.ParentCommentId == null)
            .CountAsync();

        if (totalRootCount == 0)
            return new CommentPageDto([], 0);

        // Step 1: page the root comment ids (golden index order — post-MVP DDL adds the composite
        // index; this query works correctly before that index exists, just without index scan).
        List<long> rootIds = await readDb.ChapterComments
            .Where(c => c.ChapterId == chapterId && c.ParentCommentId == null)
            .OrderByDescending(c => c.DatePosted)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => c.CommentId)
            .ToListAsync();

        if (rootIds.Count == 0)
            return new CommentPageDto([], totalRootCount);

        // Step 2: fetch roots-on-page plus their direct replies in a single query.
        // Query through ChapterComments so EF Core's TPT join gives us base + child columns
        // together (avoids the shadow one-to-one FK on base_comments that makes
        // BaseComments.Where(c.ChapterComment != null) unreliable on freshly-inserted rows).
        int? currentUserId = ActiveUser.UserId;  // property, not constructor param (avoids CS9107)

        List<CommentDto> comments = await readDb.ChapterComments
            .Where(c => rootIds.Contains(c.CommentId)
                        || (c.ParentCommentId != null && rootIds.Contains(c.ParentCommentId.Value)))
            .Select(c => new CommentDto(
                c.CommentId,
                c.ParentCommentId,
                c.UserId,
                c.Author != null ? c.Author.UserName : null,
                c.Author != null ? c.Author.ProfilePictureRelativeUrl : null,
                c.CommentText,
                c.DatePosted,
                c.LikeCount,
                c.IsSpoiler,
                // EXISTS subquery for per-viewer like state. Always false for anonymous — EF won't
                // emit a join when currentUserId is null because the outer && short-circuits.
                currentUserId != null && c.Likes.Any(l => l.UserId == currentUserId)))
            .ToListAsync();

        // In-memory ordering: roots DatePosted DESC (per rootIds order), replies DatePosted ASC.
        // rootIds preserves the paginated root order (newest-first from the SQL step above).
        Dictionary<long, int> rootOrder = rootIds
            .Select((id, idx) => (id, idx))
            .ToDictionary(t => t.id, t => t.idx);

        List<CommentDto> ordered = comments
            .OrderBy(c => c.ParentCommentId.HasValue
                ? rootOrder.GetValueOrDefault(c.ParentCommentId.Value, int.MaxValue)
                : rootOrder.GetValueOrDefault(c.CommentId, int.MaxValue))
            .ThenBy(c => c.ParentCommentId.HasValue)   // roots (false) before their replies (true)
            .ThenBy(c => c.ParentCommentId.HasValue ? c.DatePosted : DateTime.MinValue)
            .ToList();

        return new CommentPageDto(ordered, totalRootCount);
    }
}
