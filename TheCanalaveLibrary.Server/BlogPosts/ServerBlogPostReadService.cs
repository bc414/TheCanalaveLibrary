using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

public class ServerBlogPostReadService(
    ReadOnlyApplicationDbContext readDb,
    IActiveUserContext activeUser) : IBlogPostReadService
{
    /// <summary>
    /// Exposed as a protected property so the derived write service can access the user context
    /// without double-capturing the constructor parameter (eliminates CS9107/CS9124 warnings).
    /// </summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    public async Task<BlogPostDto?> GetByIdAsync(int blogPostId)
    {
        int? currentUserId = ActiveUser.UserId;
        Rating maxRating = ActiveUser.ShowMatureContent ? Rating.M : Rating.T;

        // Single query through ProfileBlogPosts — TPT join pulls base columns (title, content,
        // author_id, like_count, view_count) alongside child columns (rating, date_created,
        // is_published, has_spoilers, story_id). Returns null for GroupBlogPost URLs (no child row).
        var row = await readDb.ProfileBlogPosts
            .Where(p => p.BlogPostId == blogPostId)
            .Select(p => new
            {
                p.BlogPostId,
                p.AuthorId,
                AuthorDisplayName = p.Author != null ? p.Author.UserName : null,
                p.Title,
                p.Content,
                p.Rating,
                p.DateCreated,
                p.LastUpdatedDate,
                p.LikeCount,
                p.ViewCount,
                p.IsPublished,
                p.HasSpoilers,
                p.StoryId,
                LinkedStoryTitle = p.Story != null ? p.Story.StoryListing.StoryTitle : null,
                IsLikedByCurrentUser = currentUserId != null
                    && p.Likes.Any(l => l.UserId == currentUserId)
            })
            .FirstOrDefaultAsync();

        if (row is null) return null;

        bool isAuthor = currentUserId.HasValue && currentUserId == row.AuthorId;

        if (!row.IsPublished && !isAuthor) return null;
        if (!isAuthor && row.Rating > maxRating) return null;

        return new BlogPostDto(
            row.BlogPostId,
            row.AuthorId,
            row.AuthorDisplayName,
            row.Title,
            row.Content,
            row.Rating,
            row.HasSpoilers,
            row.StoryId,
            row.LinkedStoryTitle,
            row.DateCreated,
            row.LastUpdatedDate,
            row.LikeCount,
            row.IsLikedByCurrentUser,
            row.ViewCount,
            row.IsPublished);
    }

    public async Task<(BlogPostListingDto[] Items, int TotalCount)> GetByAuthorAsync(
        int authorId, int page, int pageSize)
    {
        Rating maxRating = ActiveUser.ShowMatureContent ? Rating.M : Rating.T;

        IQueryable<ProfileBlogPost> query = readDb.ProfileBlogPosts
            .Where(p => p.AuthorId == authorId && p.IsPublished && p.Rating <= maxRating);

        int totalCount = await query.CountAsync();

        if (totalCount == 0)
            return ([], 0);

        // Load raw content server-side; MakeSnippet is computed in-process after projection
        // (SQL has no HTML-stripping function; the snippet is a display concern, not a search one).
        List<(int BlogPostId, string Title, string Content, DateTime DateCreated, Rating Rating, bool HasSpoilers)> rows
            = await query
                .OrderByDescending(p => p.DateCreated)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ValueTuple<int, string, string, DateTime, Rating, bool>(
                    p.BlogPostId, p.Title, p.Content, p.DateCreated, p.Rating, p.HasSpoilers))
                .ToListAsync();

        BlogPostListingDto[] items = rows
            .Select(r => new BlogPostListingDto(
                r.Item1, r.Item2, BlogPostText.MakeSnippet(r.Item3), r.Item4, r.Item5, r.Item6))
            .ToArray();

        return (items, totalCount);
    }

    public async Task<BlogPostEditDto?> GetForEditAsync(int blogPostId)
    {
        // Edit page is author-only — no rating check. The page verifies ownership after load.
        var row = await readDb.ProfileBlogPosts
            .Where(p => p.BlogPostId == blogPostId)
            .Select(p => new { p.AuthorId, p.Title, p.Content, p.Rating, p.IsPublished, p.HasSpoilers, p.StoryId })
            .FirstOrDefaultAsync();

        if (row is null) return null;

        return new BlogPostEditDto(
            blogPostId,
            row.AuthorId,
            row.Title,
            row.Content,
            row.Rating,
            row.HasSpoilers,
            row.StoryId,
            row.IsPublished);
    }
}
