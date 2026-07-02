using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for Comments. Chapter context shipped WU19; blog-post context
/// added WU31. Both contexts use the same two-step load pattern: paginate root ids (golden-index
/// order, L6/post-MVP DDL), then fetch roots + their direct replies in one EF query; in-memory
/// ordering restores roots newest-first and replies oldest-first.
/// Queries go through the typed TPT DbSet (<c>ChapterComments</c>, <c>BlogPostComments</c>) so
/// EF Core uses the TPT join automatically and all inherited <c>BaseComment</c> columns are
/// accessible without navigating through the shadow one-to-one FK on <c>base_comments</c>.
/// </summary>
public class ServerCommentReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : ICommentReadService
{
    /// <summary>
    /// Exposed as a protected property so the derived write service can access the user context
    /// without double-capturing the constructor parameter (eliminates CS9107/CS9124 warnings).
    /// </summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    public async Task<CommentPageDto> GetChapterCommentsAsync(int chapterId, int page, int pageSize)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

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

    public async Task<CommentPageDto> GetGroupCommentsAsync(int groupId, int page, int pageSize)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // Mirrors GetBlogPostCommentsAsync exactly — same two-step load and in-memory ordering,
        // over GroupComments instead of BlogPostComments. No spoiler flag on group comments.
        int totalRootCount = await readDb.GroupComments
            .Where(c => c.GroupId == groupId && c.ParentCommentId == null)
            .CountAsync();

        if (totalRootCount == 0)
            return new CommentPageDto([], 0);

        List<long> rootIds = await readDb.GroupComments
            .Where(c => c.GroupId == groupId && c.ParentCommentId == null)
            .OrderByDescending(c => c.DatePosted)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => c.CommentId)
            .ToListAsync();

        if (rootIds.Count == 0)
            return new CommentPageDto([], totalRootCount);

        int? currentUserId = ActiveUser.UserId;

        List<CommentDto> comments = await readDb.GroupComments
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
                // Group comments have no spoiler flag — always false.
                false,
                currentUserId != null && c.Likes.Any(l => l.UserId == currentUserId)))
            .ToListAsync();

        Dictionary<long, int> rootOrder = rootIds
            .Select((id, idx) => (id, idx))
            .ToDictionary(t => t.id, t => t.idx);

        List<CommentDto> ordered = comments
            .OrderBy(c => c.ParentCommentId.HasValue
                ? rootOrder.GetValueOrDefault(c.ParentCommentId.Value, int.MaxValue)
                : rootOrder.GetValueOrDefault(c.CommentId, int.MaxValue))
            .ThenBy(c => c.ParentCommentId.HasValue)
            .ThenBy(c => c.ParentCommentId.HasValue ? c.DatePosted : DateTime.MinValue)
            .ToList();

        return new CommentPageDto(ordered, totalRootCount);
    }

    public async Task<CommentPageDto> GetUserProfileCommentsAsync(int profileUserId, int page, int pageSize)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // Mirrors GetGroupCommentsAsync exactly — same two-step load and in-memory ordering,
        // over UserProfileComments instead of GroupComments. No spoiler flag on profile comments.
        int totalRootCount = await readDb.UserProfileComments
            .Where(c => c.ProfileUserId == profileUserId && c.ParentCommentId == null)
            .CountAsync();

        if (totalRootCount == 0)
            return new CommentPageDto([], 0);

        List<long> rootIds = await readDb.UserProfileComments
            .Where(c => c.ProfileUserId == profileUserId && c.ParentCommentId == null)
            .OrderByDescending(c => c.DatePosted)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => c.CommentId)
            .ToListAsync();

        if (rootIds.Count == 0)
            return new CommentPageDto([], totalRootCount);

        int? currentUserId = ActiveUser.UserId;

        List<CommentDto> comments = await readDb.UserProfileComments
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
                // Profile-wall comments have no spoiler flag — always false.
                false,
                currentUserId != null && c.Likes.Any(l => l.UserId == currentUserId)))
            .ToListAsync();

        Dictionary<long, int> rootOrder = rootIds
            .Select((id, idx) => (id, idx))
            .ToDictionary(t => t.id, t => t.idx);

        List<CommentDto> ordered = comments
            .OrderBy(c => c.ParentCommentId.HasValue
                ? rootOrder.GetValueOrDefault(c.ParentCommentId.Value, int.MaxValue)
                : rootOrder.GetValueOrDefault(c.CommentId, int.MaxValue))
            .ThenBy(c => c.ParentCommentId.HasValue)
            .ThenBy(c => c.ParentCommentId.HasValue ? c.DatePosted : DateTime.MinValue)
            .ToList();

        return new CommentPageDto(ordered, totalRootCount);
    }

    public async Task<CommentPageDto> GetBlogPostCommentsAsync(int blogPostId, int page, int pageSize)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        // Mirrors GetChapterCommentsAsync exactly — same two-step load and in-memory ordering,
        // over BlogPostComments instead of ChapterComments. No spoiler flag on blog-post comments.
        int totalRootCount = await readDb.BlogPostComments
            .Where(c => c.BlogPostId == blogPostId && c.ParentCommentId == null)
            .CountAsync();

        if (totalRootCount == 0)
            return new CommentPageDto([], 0);

        List<long> rootIds = await readDb.BlogPostComments
            .Where(c => c.BlogPostId == blogPostId && c.ParentCommentId == null)
            .OrderByDescending(c => c.DatePosted)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => c.CommentId)
            .ToListAsync();

        if (rootIds.Count == 0)
            return new CommentPageDto([], totalRootCount);

        int? currentUserId = ActiveUser.UserId;

        List<CommentDto> comments = await readDb.BlogPostComments
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
                // Blog-post comments have no spoiler flag — always false (IsSpoiler is BaseComment).
                false,
                currentUserId != null && c.Likes.Any(l => l.UserId == currentUserId)))
            .ToListAsync();

        Dictionary<long, int> rootOrder = rootIds
            .Select((id, idx) => (id, idx))
            .ToDictionary(t => t.id, t => t.idx);

        List<CommentDto> ordered = comments
            .OrderBy(c => c.ParentCommentId.HasValue
                ? rootOrder.GetValueOrDefault(c.ParentCommentId.Value, int.MaxValue)
                : rootOrder.GetValueOrDefault(c.CommentId, int.MaxValue))
            .ThenBy(c => c.ParentCommentId.HasValue)
            .ThenBy(c => c.ParentCommentId.HasValue ? c.DatePosted : DateTime.MinValue)
            .ToList();

        return new CommentPageDto(ordered, totalRootCount);
    }
}
