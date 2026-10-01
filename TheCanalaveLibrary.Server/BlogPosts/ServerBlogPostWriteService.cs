using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for Blog Posts. Inherits the read path via primary-constructor
/// chaining (mirrors <see cref="ServerCommentWriteService"/> / <see cref="ServerStoryWriteService"/>).
/// <para>
/// <b>Security model:</b> every profile and group mutation probes its own subtype's child set and checks
/// the row's <c>AuthorId</c> against <c>IActiveUserContext.UserId</c>, throwing
/// <see cref="UnauthorizedAccessException"/> on mismatch or on an authorless post (owned by nobody); an
/// id of another subtype is <see cref="KeyNotFoundException"/> (owner ruling D10). Site announcements
/// are moderator-gated instead (see their section). The UI <c>@if (isOwner)</c> affordance is
/// convenience only; the service gate is the actual control (settled WU24, <c>cross-cutting.md</c>
/// §"Active-User-Conditional Handling").
/// </para>
/// <para>
/// <b>Deletes:</b> every subtype deletes through <see cref="TptDelete.BlogPostAsync"/> (the post's
/// comments and polls through their base rows, then the post) in one execution-strategy transaction —
/// <c>layer2-services.md</c> §"Hard deletes of content parents".
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

        // One probe on the PROFILE child set (owner ruling D10's per-subtype lifecycle): a group or site
        // id is not found here, so it never runs the base-table half of the update below. The prior
        // published state (WU-B2) rides the same projection — it detects the false→true transition.
        var existing = await writeDb.ProfileBlogPosts
            .Where(p => p.BlogPostId == dto.BlogPostId)
            .Select(p => new { p.AuthorId, p.IsPublished })
            .FirstOrDefaultAsync();

        if (existing is null)
            throw new KeyNotFoundException($"Blog post {dto.BlogPostId} not found.");

        // An authorless post (author's account deleted — SET NULL) is owned by nobody.
        if (existing.AuthorId is not int authorId || authorId != userId)
            throw new UnauthorizedAccessException("You can only edit your own blog posts.");

        // Ownership gate on the optional story link (WU-B2) — same control as the create path.
        await EnsureLinkedStoryOwnedAsync(dto.StoryId, userId);

        bool wasPublished = existing.IsPublished;

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
        if (!wasPublished && dto.IsPublished)
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

        // Probe the PROFILE child set (owner ruling D10's per-subtype lifecycle; layer2-services.md
        // §"Scalar projections on nullable FK columns"): a group or site id is not found here — it used
        // to reach a profile_blog_posts stub delete that affected 0 rows and surfaced as a 500.
        var existing = await writeDb.ProfileBlogPosts
            .Where(p => p.BlogPostId == blogPostId)
            .Select(p => new { p.AuthorId })
            .FirstOrDefaultAsync();

        if (existing is null)
            throw new KeyNotFoundException($"Blog post {blogPostId} not found.");

        // An authorless post (author's account deleted — SET NULL) is owned by nobody.
        if (existing.AuthorId is not int authorId || authorId != userId)
            throw new UnauthorizedAccessException("You can only delete your own blog posts.");

        await DeleteWithDependentsAsync(blogPostId);

        // Decrement BlogPostsWritten post-commit (D22; cross-cutting.md §"UserStats Updates").
        await writeDb.UserStats.Where(us => us.UserId == authorId)
            .ExecuteUpdateAsync(s => s.SetProperty(us => us.BlogPostsWritten, us => us.BlogPostsWritten - 1));
    }

    /// <summary>
    /// The one delete shape for every blog-post subtype (owner ruling D10): the post's comments and
    /// polls through their base rows, then the post through <c>base_blog_posts</c> — all
    /// <see cref="TptDelete.BlogPostAsync"/>, in one transaction under the execution strategy (a bare
    /// <c>BeginTransactionAsync</c> throws under <c>EnableRetryOnFailure</c>). The delegate tracks
    /// nothing, and every statement is idempotent, so a retry is safe. Counters stay with the caller,
    /// after this returns.
    /// <para><b>A 0-row delete is a lost race</b> (layer2-services.md §"Hard deletes of content
    /// parents"): two deletes of one post can both pass the caller's owner probe. The loser deletes
    /// nothing, so it throws <see cref="KeyNotFoundException"/> (rolling back) — the 404 a sequential
    /// re-delete gets — and never reaches the caller's counter decrement.</para>
    /// </summary>
    private async Task DeleteWithDependentsAsync(int blogPostId)
    {
        var strategy = writeDb.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await writeDb.Database.BeginTransactionAsync();
            if (await TptDelete.BlogPostAsync(writeDb, blogPostId) == 0)
                throw new KeyNotFoundException($"Blog post {blogPostId} not found.");
            await tx.CommitAsync();
        });
    }

    public async Task<BlogPostLikeResultDto> ToggleLikeAsync(int blogPostId)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Liking a blog post requires an authenticated user.");

        // An existing like row makes this call an unlike — a clear on the caller's own row, which
        // is never visibility-guarded (owner ruling D6). The row's FK already proves the post exists.
        bool alreadyLiked = await writeDb.BlogPostLikes
            .AnyAsync(l => l.BlogPostId == blogPostId && l.UserId == userId);

        if (!alreadyLiked)
        {
            bool exists = await writeDb.BlogPosts.AnyAsync(b => b.BlogPostId == blogPostId);
            if (!exists)
                throw new KeyNotFoundException($"Blog post {blogPostId} not found.");
        }

        // Kind (g), raise only: writeDb is unfiltered, so the existence probe above proves nothing
        // about visibility — a non-author could inflate LikeCount on someone's unpublished draft.
        // Same message as a missing post (non-disclosure). On the unlike path the same answer
        // decides only what the response may carry (below), never whether the clear lands.
        bool postVisible;
        await using (ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync())
            postVisible = await BlogPostVisibilityGuard.IsBlogPostVisibleAsync(readDb, ActiveUser, blogPostId);
        if (!alreadyLiked && !postVisible)
            throw new KeyNotFoundException($"Blog post {blogPostId} not found.");

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

        // A clear's response is a read and stays gated (identity-and-authorization.md §"Raises vs
        // clears"): the unlike on a post now hidden from the caller has landed, but its current
        // aggregate is not theirs to learn.
        if (!postVisible)
            return new BlogPostLikeResultDto(0, false);

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

    // ── Group-post lifecycle (owner ruling D10, WU-TptHardDelete) ────────────────────
    // Author-only, gated on the GROUP child set (a profile or site id is not found). No membership
    // recheck: the author owns their row and may have left the group — the EditCommentAsync /
    // DeleteCommentAsync precedent. No rating-vs-audience check on update: it mirrors create exactly
    // (the waterfall ruling is worksheet D43, pending). No fan-out and no rate limit, mirroring the
    // profile and site update paths. No BlogPostsWritten move: group create does not move it either
    // (which posts that counter counts is owner-open — tracker F16).

    public async Task UpdateGroupBlogPostAsync(UpdateGroupBlogPostDto dto)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Updating a group blog post requires an authenticated user.");

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new BlogPostValidationException(errors);

        await RequireOwnGroupPostAsync(dto.BlogPostId, userId, "edit");

        string sanitizedContent = sanitizer.Sanitize(dto.Content);

        // Base-table columns: Title and Content only (author_id never changes after creation).
        await writeDb.BlogPosts
            .Where(b => b.BlogPostId == dto.BlogPostId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Title,   dto.Title.Trim())
                .SetProperty(b => b.Content, sanitizedContent));

        // Child-table columns. IsPublished is untouched — group posts publish on create.
        await writeDb.GroupBlogPosts
            .Where(p => p.BlogPostId == dto.BlogPostId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Rating,          dto.Rating)
                .SetProperty(p => p.HasSpoilers,     dto.HasSpoilers)
                .SetProperty(p => p.LastUpdatedDate, DateTime.UtcNow));
    }

    public async Task DeleteGroupBlogPostAsync(int blogPostId)
    {
        if (ActiveUser.UserId is not int userId)
            throw new InvalidOperationException("Deleting a group blog post requires an authenticated user.");

        await RequireOwnGroupPostAsync(blogPostId, userId, "delete");
        await DeleteWithDependentsAsync(blogPostId);
    }

    /// <summary>
    /// The group-post gate: missing or not a group post → <see cref="KeyNotFoundException"/>; not the
    /// caller's, or authorless (author's account deleted — owned by nobody) →
    /// <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    private async Task RequireOwnGroupPostAsync(int blogPostId, int userId, string verb)
    {
        var existing = await writeDb.GroupBlogPosts
            .Where(p => p.BlogPostId == blogPostId)
            .Select(p => new { p.AuthorId })
            .FirstOrDefaultAsync();

        if (existing is null)
            throw new KeyNotFoundException($"Group blog post {blogPostId} not found.");

        if (existing.AuthorId is not int authorId || authorId != userId)
            throw new UnauthorizedAccessException($"You can only {verb} your own group blog posts.");
    }

    // ── Site announcements (WU-SiteNews) ──────────────────────────────────────────
    // Security model diverges from the rest of this service: IsModerator || IsAdmin gates every
    // mutation (listed explicitly — Admin does not inherit Moderator), not author-only ownership
    // — the SitePoll precedent (ServerPollWriteService.CreateSitePollAsync / the
    // LoadAuthorizedPollWithOptionsAsync site-poll branch: any moderator manages any site post).
    // Deliberately NOT UserStats.BlogPostsWritten-tracked — that counter feeds community-content
    // badges; site announcements are staff output, not community contribution. (Only the profile
    // create/delete paths move it today; group posts do not, and the recompute counts every
    // base_blog_posts row — the disagreement is owner-open, tracker F16.)

    public async Task<int> CreateSiteBlogPostAsync(CreateSiteBlogPostDto dto)
    {
        int authorId = ActiveUser.RequireModerator();
        rateLimit.EnsureAllowed(WriteActionKind.ContentCreate, authorId);

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new BlogPostValidationException(errors);

        string sanitizedContent = sanitizer.Sanitize(dto.Content);

        SiteBlogPost post = new()
        {
            AuthorId        = authorId,             // server-stamped; absent from DTO
            Title           = dto.Title.Trim(),
            Content         = sanitizedContent,
            Rating          = Rating.E,              // never exposed to the editor — staff content
            IsPublished     = dto.IsPublished,
            NotifyAllUsers  = dto.NotifyAllUsers,
            DateCreated     = DateTime.UtcNow,
            LastUpdatedDate = DateTime.UtcNow
        };

        writeDb.SiteBlogPosts.Add(post);
        await writeDb.SaveChangesAsync();

        // Fire immediately when created already-published with NotifyAllUsers set (unlike
        // Profile/GroupBlogPost, a SiteBlogPost can be created published — no forced draft-first).
        // The false→true transition case lives in UpdateSiteBlogPostAsync.
        if (post.IsPublished && post.NotifyAllUsers)
            await FireAnnouncementFanOutAsync(post.BlogPostId, authorId);

        return post.BlogPostId;
    }

    public async Task UpdateSiteBlogPostAsync(UpdateSiteBlogPostDto dto)
    {
        int userId = ActiveUser.RequireModerator();

        List<string> errors = dto.CanSave();
        if (errors.Count > 0) throw new BlogPostValidationException(errors);

        var existing = await writeDb.SiteBlogPosts
            .Where(p => p.BlogPostId == dto.BlogPostId)
            .Select(p => new { p.IsPublished, p.NotifiedAtUtc })
            .FirstOrDefaultAsync();

        if (existing is null)
            throw new KeyNotFoundException($"Site announcement {dto.BlogPostId} not found.");

        string sanitizedContent = sanitizer.Sanitize(dto.Content);

        // Base-table columns: Title and Content only (author_id never changes after creation).
        await writeDb.BlogPosts
            .Where(b => b.BlogPostId == dto.BlogPostId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Title,   dto.Title.Trim())
                .SetProperty(b => b.Content, sanitizedContent));

        // Child-table columns.
        await writeDb.SiteBlogPosts
            .Where(p => p.BlogPostId == dto.BlogPostId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsPublished,     dto.IsPublished)
                .SetProperty(p => p.LastUpdatedDate, DateTime.UtcNow)
                .SetProperty(p => p.NotifyAllUsers,  dto.NotifyAllUsers));

        // Fire-once guard (SiteBlogPost.NotifiedAtUtc doc): only the false→true publish
        // transition, only when NotifyAllUsers is set, and only if it hasn't already fired — a
        // later edit (including flipping NotifyAllUsers back on after the first fan-out) never
        // re-notifies. There is no quiet-period debounce here (unlike BasePoll's edit-notification
        // sweep) — a single staff post either announces once or it doesn't.
        if (existing.IsPublished == false && dto.IsPublished && dto.NotifyAllUsers && existing.NotifiedAtUtc is null)
            await FireAnnouncementFanOutAsync(dto.BlogPostId, userId);
    }

    public async Task DeleteSiteBlogPostAsync(int blogPostId)
    {
        ActiveUser.RequireModerator();

        bool exists = await writeDb.SiteBlogPosts.AnyAsync(p => p.BlogPostId == blogPostId);
        if (!exists)
            throw new KeyNotFoundException($"Site announcement {blogPostId} not found.");

        // The same shape as every blog-post delete (owner ruling D10): comments and polls through
        // their base rows, then the post. No counter — site posts are not BlogPostsWritten-tracked.
        await DeleteWithDependentsAsync(blogPostId);
    }

    private async Task FireAnnouncementFanOutAsync(int blogPostId, int authorId)
    {
        try
        {
            await notifications.NotifyNewSiteAnnouncementAsync(blogPostId, authorId);
            await writeDb.SiteBlogPosts
                .Where(p => p.BlogPostId == blogPostId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.NotifiedAtUtc, DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            // Notification failure must never roll back the primary action.
            logger.LogWarning(ex,
                "SiteAnnouncement notification fan-out failed for blog post {BlogPostId}",
                blogPostId);
        }
    }
}
