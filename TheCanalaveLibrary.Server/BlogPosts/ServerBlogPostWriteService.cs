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

        // Ownership gate on the optional story link (WU-B2): the editor's own-stories dropdown is
        // affordance; this check is the control (identity-and-authorization.md §"Security vs
        // affordance"). A forged StoryId would link another author's story and, since WU-B2's
        // publish fan-out, spam that story's followers/favoriters/read-it-later audience.
        await EnsureLinkedStoryOwnedAsync(dto.StoryId, authorId);

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

        // No notification on create (WU-B2): profile posts are drafts here (IsPublished = false).
        // The follower/story fan-out fires on the publish transition in UpdateBlogPostAsync.

        return post.BlogPostId;
    }

    /// <summary>
    /// Throws <see cref="UnauthorizedAccessException"/> when <paramref name="storyId"/> names a
    /// story the caller doesn't own (or that no longer has an owner — deleted authors SET NULL).
    /// No-op when <paramref name="storyId"/> is null. WU-B2 story-link integrity gate.
    /// </summary>
    private async Task EnsureLinkedStoryOwnedAsync(int? storyId, int authorId)
    {
        if (storyId is not int linkedStoryId) return;

        bool ownsStory = await writeDb.Stories
            .AnyAsync(s => s.StoryId == linkedStoryId && s.AuthorId == authorId);
        if (!ownsStory)
            throw new UnauthorizedAccessException("You can only link your own stories.");
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

        // Ownership gate on the optional story link (WU-B2) — same control as the create path.
        await EnsureLinkedStoryOwnedAsync(dto.StoryId, userId);

        // Prior published state, read from the child table only (WU-B2): detects the false→true
        // publish transition below. Null when the id isn't a profile post — the base-table update
        // still runs (preserving pre-B2 behavior for raw non-profile ids) but no fan-out fires.
        bool? wasPublished = await writeDb.ProfileBlogPosts
            .Where(p => p.BlogPostId == dto.BlogPostId)
            .Select(p => (bool?)p.IsPublished)
            .FirstOrDefaultAsync();

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

        // Publish-transition fan-out (WU-B2, best-effort post-commit): fires only on the
        // false→true edge — drafts stay silent, and a republish after unpublish re-notifies
        // (intentional; the create-core's unread-dedup absorbs back-to-back bursts). Recipient
        // resolution + 13>14>15>16 precedence live in NotifyNewProfileBlogPostAsync.
        if (wasPublished == false && dto.IsPublished)
        {
            try
            {
                await notifications.NotifyNewProfileBlogPostAsync(dto.BlogPostId, userId, dto.StoryId);
            }
            catch (Exception ex)
            {
                // Notification failure must never roll back the primary action.
                logger.LogWarning(ex,
                    "Profile blog post publish fan-out failed for blog post {BlogPostId}",
                    dto.BlogPostId);
            }
        }
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
        // BlogPostLike / BlogPostComment rows cascade. ExecuteDeleteAsync is unsupported on TPT
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

        bool nowLiked;
        if (alreadyLiked)
        {
            await writeDb.BlogPostLikes
                .Where(l => l.BlogPostId == blogPostId && l.UserId == userId)
                .ExecuteDeleteAsync();
            nowLiked = false;
        }
        else
        {
            writeDb.BlogPostLikes.Add(new BlogPostLike { BlogPostId = blogPostId, UserId = userId });
            await writeDb.SaveChangesAsync();
            nowLiked = true;
        }

        // LikeCount stays on the base table — updated via base DbSet as an atomic ±1 delta
        // (layer2-services.md counter rule — a C#-computed absolute is a read-then-write with a
        // lost-update window under concurrent likes by different users; MA-705). The clamp keeps
        // the old Math.Max(0, …) guard against pre-existing drift.
        int delta = nowLiked ? 1 : -1;
        await writeDb.BlogPosts
            .Where(b => b.BlogPostId == blogPostId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                b => b.LikeCount,
                b => b.LikeCount + delta < 0 ? 0 : b.LikeCount + delta));

        // Re-read the landed value so the returned count is accurate under concurrency.
        int newCount = await writeDb.BlogPosts
            .Where(b => b.BlogPostId == blogPostId)
            .Select(b => b.LikeCount)
            .SingleAsync();

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
