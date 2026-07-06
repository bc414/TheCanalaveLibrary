using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Blog Posts. Inherits the read path via primary-constructor
/// chaining (mirrors <see cref="ServerCommentWriteService"/> / <see cref="ServerStoryWriteService"/>).
/// <para>
/// <b>Security model:</b> every mutation loads the entity and checks
/// <c>entity.AuthorId == IActiveUserContext.UserId</c>, throwing <see cref="UnauthorizedAccessException"/>
/// on mismatch. The UI <c>@if (isOwner)</c> affordance is convenience only; the service gate is the
/// actual control (settled WU24, <c>cross-cutting.md</c> §"Active-User-Conditional Handling").
/// </para>
/// <para>
/// <b>Sanitize-once-on-save:</b> raw HTML from the editor is sanitized via
/// <see cref="IHtmlSanitizationService.Sanitize"/> immediately before persisting. Never sanitize
/// display output — only sanitize on write.
/// </para>
/// </summary>
public class ServerBlogPostWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb,
    IActiveUserContext activeUser,
    IHtmlSanitizationService sanitizer,
    INotificationWriteService notifications,
    IWriteRateLimitService rateLimit,
    ILogger<ServerBlogPostWriteService> logger)
    : ServerBlogPostReadService(readDbFactory, activeUser), IBlogPostWriteService
{
    public async Task<int> CreateProfileBlogPostAsync(CreateProfileBlogPostDto dto)
    {
        if (ActiveUser.UserId is not int authorId)
            throw new InvalidOperationException("Creating a blog post requires an authenticated user.");
        rateLimit.EnsureAllowed(WriteActionKind.ContentCreate, authorId);

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new BlogPostValidationException(errors);

        string sanitizedContent = sanitizer.Sanitize(dto.Content);

        ProfileBlogPost post = new()
        {
            AuthorId        = authorId,              // server-stamped; absent from DTO
            Title           = dto.Title.Trim(),
            Content         = sanitizedContent,
            Rating          = dto.Rating,
            HasSpoilers     = dto.HasSpoilers,
            StoryId         = dto.StoryId,
            IsPublished     = false,                 // drafts by default; author publishes explicitly
            DateCreated     = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow
        };

        writeDb.BlogPosts.Add(post);
        await writeDb.SaveChangesAsync();

        // Increment UserStats.BlogPostsWritten — ExecuteUpdateAsync pattern (cross-cutting.md
        // §"UserStats Updates"). Best-effort: stat drift is recovered by the background recalculator.
        await writeDb.UserStats
            .Where(us => us.UserId == authorId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.BlogPostsWritten, us => us.BlogPostsWritten + 1));

        // TODO(WU33): notify followers of a new blog post once the notification type exists.

        return post.BlogPostId;
    }

    public async Task UpdateBlogPostAsync(UpdateBlogPostDto dto)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Updating a blog post requires an authenticated user.");

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new BlogPostValidationException(errors);

        int? existingAuthorId = await writeDb.BlogPosts
            .Where(b => b.BlogPostId == dto.BlogPostId)
            .Select(b => (int?)b.AuthorId)
            .FirstOrDefaultAsync();

        if (existingAuthorId is null)
            throw new KeyNotFoundException($"Blog post {dto.BlogPostId} not found.");

        if (existingAuthorId != userId)
            throw new UnauthorizedAccessException("You can only edit your own blog posts.");

        string sanitizedContent = sanitizer.Sanitize(dto.Content);

        // Base-table columns: Title and Content only (author_id never changes after creation).
        await writeDb.BlogPosts
            .Where(b => b.BlogPostId == dto.BlogPostId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Title,   dto.Title.Trim())
                .SetProperty(b => b.Content, sanitizedContent));

        // Child-table columns: discovery + profile-specific fields.
        await writeDb.ProfileBlogPosts
            .Where(p => p.BlogPostId == dto.BlogPostId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Rating,          dto.Rating)
                .SetProperty(p => p.IsPublished,     dto.IsPublished)
                .SetProperty(p => p.LastUpdatedDate, DateTime.UtcNow)
                .SetProperty(p => p.HasSpoilers,     dto.HasSpoilers)
                .SetProperty(p => p.StoryId,         dto.StoryId));
    }

    public async Task DeleteBlogPostAsync(int blogPostId)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Deleting a blog post requires an authenticated user.");

        int? existingAuthorId = await writeDb.BlogPosts
            .Where(b => b.BlogPostId == blogPostId)
            .Select(b => (int?)b.AuthorId)
            .FirstOrDefaultAsync();

        if (existingAuthorId is null)
            throw new KeyNotFoundException($"Blog post {blogPostId} not found.");

        if (existingAuthorId != userId)
            throw new UnauthorizedAccessException("You can only delete your own blog posts.");

        // Change-tracker stub delete: EF issues child-then-base DELETE in one transaction.
        // BlogPostLike / BlogPostComment rows cascade; FeatureContribution.BlogPostId is SET NULL
        // (configured in BaseBlogPostConfiguration). ExecuteDeleteAsync is unsupported on TPT
        // base-type DbSets — change-tracker stub is the clean alternative.
        writeDb.Remove(new ProfileBlogPost { BlogPostId = blogPostId });
        await writeDb.SaveChangesAsync();

        // Decrement BlogPostsWritten counter (cross-cutting.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == existingAuthorId.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.BlogPostsWritten, us => us.BlogPostsWritten - 1));
    }

    public async Task<BlogPostLikeResultDto> ToggleLikeAsync(int blogPostId)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Liking a blog post requires an authenticated user.");

        int? currentLikeCount = await writeDb.BlogPosts
            .Where(b => b.BlogPostId == blogPostId)
            .Select(b => (int?)b.LikeCount)
            .FirstOrDefaultAsync();

        if (currentLikeCount is null)
            throw new KeyNotFoundException($"Blog post {blogPostId} not found.");

        bool alreadyLiked = await writeDb.BlogPostLikes
            .AnyAsync(l => l.BlogPostId == blogPostId && l.UserId == userId);

        int newCount;
        bool nowLiked;

        if (alreadyLiked)
        {
            await writeDb.BlogPostLikes
                .Where(l => l.BlogPostId == blogPostId && l.UserId == userId)
                .ExecuteDeleteAsync();

            newCount = Math.Max(0, currentLikeCount.Value - 1);
            nowLiked = false;
        }
        else
        {
            writeDb.BlogPostLikes.Add(new BlogPostLike { BlogPostId = blogPostId, UserId = userId });
            await writeDb.SaveChangesAsync();

            newCount = currentLikeCount.Value + 1;
            nowLiked = true;
        }

        // LikeCount stays on the base table — updated via base DbSet.
        await writeDb.BlogPosts
            .Where(b => b.BlogPostId == blogPostId)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.LikeCount, newCount));

        // No notification generated — anti-addictive design (BlogPostLike entity comment).
        return new BlogPostLikeResultDto(newCount, nowLiked);
    }

    public async Task<int> CreateGroupBlogPostAsync(CreateGroupBlogPostDto dto)
    {
        if (ActiveUser.UserId is not int authorId)
            throw new InvalidOperationException("Creating a group blog post requires an authenticated user.");
        rateLimit.EnsureAllowed(WriteActionKind.ContentCreate, authorId);

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new BlogPostValidationException(errors);

        // Verify the caller is a member of the group.
        bool isMember = await writeDb.GroupMembers
            .AnyAsync(m => m.GroupId == dto.GroupId && m.UserId == authorId);
        if (!isMember)
            throw new UnauthorizedAccessException("You must be a member of this group to post a blog post.");

        // Write context is unfiltered — group loads regardless of audience rating.
        bool groupExists = await writeDb.Groups
            .AnyAsync(g => g.GroupId == dto.GroupId);
        if (!groupExists)
            throw new KeyNotFoundException($"Group {dto.GroupId} not found.");

        string sanitizedContent = sanitizer.Sanitize(dto.Content);

        GroupBlogPost post = new()
        {
            AuthorId        = authorId,
            GroupId         = dto.GroupId,
            Title           = dto.Title.Trim(),
            Content         = sanitizedContent,
            Rating          = dto.Rating,
            HasSpoilers     = dto.HasSpoilers,
            StoryId         = dto.StoryId,
            IsPublished     = true,              // group blog posts publish immediately
            DateCreated     = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow
        };

        writeDb.GroupBlogPosts.Add(post);
        await writeDb.SaveChangesAsync();

        // Fan-out notification to members with NotifyForNewBlogPost = true (best-effort post-commit).
        try
        {
            await notifications.NotifyNewGroupBlogPostAsync(dto.GroupId, post.BlogPostId, authorId);
        }
        catch (Exception ex)
        {
            // Notification failure must never roll back the primary action.
            logger.LogWarning(ex,
                "NewGroupBlogPost notification fan-out failed for blog post {BlogPostId} in group {GroupId}",
                post.BlogPostId, dto.GroupId);
        }

        return post.BlogPostId;
    }
}
