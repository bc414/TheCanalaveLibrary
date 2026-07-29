using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

public class ServerBlogPostReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IBlogPostReadService
{
    /// <summary>
    /// Exposed as a protected property so the derived write service can access the user context
    /// without double-capturing the constructor parameter (eliminates CS9107/CS9124 warnings).
    /// </summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    /// <summary>
    /// Read contexts are created per method from this factory (`await using`) — see
    /// <c>layer2-services.md</c> §"Read-context concurrency: factory per method".
    /// </summary>
    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;

    public async Task<BlogPostDto?> GetByIdAsync(int blogPostId)
    {
        int? currentUserId = ActiveUser.UserId;

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // First branch: ProfileBlogPosts — TPT join pulls base columns (title, content,
        // author_id, like_count) alongside child columns (rating, date_created, is_published,
        // has_spoilers, story_id). GroupId/GroupAudience are null markers here so the anonymous
        // type unifies with the group branch below.
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
                p.IsPublished,
                p.HasSpoilers,
                p.StoryId,
                LinkedStoryTitle = p.Story != null ? p.Story.StoryListing.StoryTitle : null,
                GroupId = (int?)null,
                GroupAudience = (Rating?)null,
                IsLikedByCurrentUser = currentUserId != null
                    && p.Likes.Any(l => l.UserId == currentUserId)
            })
            .FirstOrDefaultAsync();

        bool isGroupPost = false;

        // Second branch (WU-AccessGate, bug B7): GroupBlogPosts — group-post permalinks
        // (/blog/{id}) 404'd for everyone because this read only knew ProfileBlogPosts. Loaded
        // with the GroupAudience filter bypassed so the visibility decision can honor a per-group
        // reveal below (the filtered-navigation join would drop the row before we could ask).
        // Group posts carry no StoryId (WU-B2 — group posts are not story-linkable), so
        // StoryId/LinkedStoryTitle project as null markers to unify with the profile branch.
        if (row is null)
        {
            row = await readDb.GroupBlogPosts
                .IgnoreQueryFilters(["GroupAudience"]) // elevated read: audience decided post-load (reveal-aware)
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
                    p.IsPublished,
                    p.HasSpoilers,
                    StoryId = (int?)null,
                    LinkedStoryTitle = (string?)null,
                    GroupId = (int?)p.GroupId,
                    GroupAudience = (Rating?)(p.Group != null ? p.Group.AudienceRating : Rating.E),
                    IsLikedByCurrentUser = currentUserId != null
                        && p.Likes.Any(l => l.UserId == currentUserId)
                })
                .FirstOrDefaultAsync();

            isGroupPost = row is not null;
        }

        // Third branch (WU-SiteNews): SiteBlogPosts — never group-owned, never story-linked, no
        // spoilers. Rating stays E/T in practice (the create/update DTOs don't expose a picker),
        // so StoryId/LinkedStoryTitle/GroupId/GroupAudience all project as null markers to unify
        // with the other two branches, same as the group branch does for StoryId.
        if (row is null)
        {
            row = await readDb.SiteBlogPosts
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
                    p.IsPublished,
                    HasSpoilers = false,
                    StoryId = (int?)null,
                    LinkedStoryTitle = (string?)null,
                    GroupId = (int?)null,
                    GroupAudience = (Rating?)null,
                    IsLikedByCurrentUser = currentUserId != null
                        && p.Likes.Any(l => l.UserId == currentUserId)
                })
                .FirstOrDefaultAsync();
        }

        if (row is null) return null;

        // Draft gate + consent resolution (WU-AccessGate, Direct-navigation plane) both live in
        // BlogPostVisibilityGuard as of WU-ParentVisibility — this read is the reference caller and
        // owns none of the rule any more. Facts come from the projection above, so delegating costs
        // no extra query; the guard resolves the reveal itself only when consent is what the
        // decision turns on (a GROUP reveal covers all group-owned content — audience gate AND
        // M-rated group posts, one consent per community; a profile post gates on its own per-post
        // reveal). Every child read of this post now asks the same predicate.
        bool isAuthor = currentUserId.HasValue && currentUserId == row.AuthorId;

        BlogPostVisibilityFacts facts = new(
            row.BlogPostId, row.AuthorId, row.IsPublished, row.Rating,
            isGroupPost, row.GroupId, row.GroupAudience);

        if (!await BlogPostVisibilityGuard.IsVisibleAsync(readDb, ActiveUser, facts)) return null;

        // Per-viewer completion signal (WU-B2): gates the spoiler interstitial's reveal path.
        // One extra query only for signed-in viewers of a story-linked (profile) post — a pure
        // projection, never a visibility gate. False for anonymous / non-story-linked.
        bool viewerHasCompletedStory = false;
        if (currentUserId is int uid && row.StoryId is int sid)
        {
            viewerHasCompletedStory = await readDb.UserStoryInteractions
                .AnyAsync(i => i.UserId == uid && i.StoryId == sid && i.IsCompleted);
        }

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
            row.IsPublished,
            viewerHasCompletedStory);
    }

    public async Task<GatedMetadataDto?> GetBlogPostGateAsync(int blogPostId)
    {
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // elevated read: gated-existence metadata. Profile posts gate on their own M rating
        // (reveal target = the post); group posts gate on the group audience OR the post rating
        // (reveal target = the GROUP — one consent covers group-owned content). Unpublished and
        // taken-down posts return null → real 404 (the IsTakenDown filter stays active).
        GatedMetadataDto? gate = await readDb.ProfileBlogPosts
            .Where(p => p.BlogPostId == blogPostId && p.IsPublished && p.Rating == Rating.M)
            .Select(p => new GatedMetadataDto(
                RevealedEntityType.BlogPost,
                p.BlogPostId,
                p.Title,
                p.AuthorId,
                p.Author != null ? p.Author.UserName : null,
                p.Rating))
            .FirstOrDefaultAsync();

        if (gate is not null) return gate;

        return await readDb.GroupBlogPosts
            .IgnoreQueryFilters(["GroupAudience"])
            .Where(p => p.BlogPostId == blogPostId && p.IsPublished
                        && (p.Rating == Rating.M || (p.Group != null && p.Group.AudienceRating == Rating.M)))
            .Select(p => new GatedMetadataDto(
                RevealedEntityType.Group,
                p.GroupId,
                p.Title,
                p.AuthorId,
                p.Author != null ? p.Author.UserName : null,
                Rating.M))
            .FirstOrDefaultAsync();
    }

    public async Task<(BlogPostListingDto[] Items, int TotalCount)> GetByAuthorAsync(
        int authorId, int page, int pageSize, bool includeUnpublished = false)
    {
        Rating maxRating = ActiveUser.MaxRating; // centralized Discovery-plane ceiling (WU-AccessGate)

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // The unpublished view is owner-only, enforced HERE, not by trusting the caller's flag
        // (endpoint-authz sweep 2026-07-18 — the flag rides the public HTTP route, so a forged
        // includeUnpublished=true must degrade to the public view rather than leak drafts).
        // When false: published-only, rating-filtered (the usual public-feed case).
        // When true (owner verified): also include drafts (IsPublished = false), and bypass the
        //             rating ceiling so the author can see their own mature unpublished posts.
        if (includeUnpublished && ActiveUser.UserId != authorId)
            includeUnpublished = false;

        // Class-A: an author's blog listing is profile-tab data; respect their ProfileVisibility
        // (WU-AccessGate Phase 1 — /api/blog-posts/by-author/{id} is directly reachable).
        // The owner passes the guard by definition, so the unpublished view is unaffected.
        if (!await ProfileVisibilityGuard.IsProfileVisibleAsync(readDb, ActiveUser, authorId))
            return ([], 0);
        IQueryable<ProfileBlogPost> query = includeUnpublished
            ? readDb.ProfileBlogPosts
                .Where(p => p.AuthorId == authorId)
            : readDb.ProfileBlogPosts
                .Where(p => p.AuthorId == authorId && p.IsPublished && p.Rating <= maxRating);

        int totalCount = await query.CountAsync();

        if (totalCount == 0)
            return ([], 0);

        // Load raw content server-side; MakeSnippet is computed in-process after projection
        // (SQL has no HTML-stripping function; the snippet is a display concern, not a search one).
        List<(int BlogPostId, string Title, string Content, DateTime DateCreated, Rating Rating, bool HasSpoilers, bool IsPublished)> rows
            = await query
                .OrderByDescending(p => p.DateCreated)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ValueTuple<int, string, string, DateTime, Rating, bool, bool>(
                    p.BlogPostId, p.Title, p.Content, p.DateCreated, p.Rating, p.HasSpoilers, p.IsPublished))
                .ToListAsync();

        BlogPostListingDto[] items = rows
            .Select(r => new BlogPostListingDto(
                r.Item1, r.Item2, BlogPostText.MakeSnippet(r.Item3), r.Item4, r.Item5, r.Item6,
                IsPublished: r.Item7))
            .ToArray();

        return (items, totalCount);
    }

    public async Task<BlogPostEditDto?> GetForEditAsync(int blogPostId)
    {
        // Edit page is author-only — no rating check. Ownership is enforced HERE (endpoint-authz
        // sweep 2026-07-18): the page's post-load comparison is affordance, not a control
        // (identity-and-authorization.md §"Security vs affordance") — without this gate any
        // authenticated user could read any draft's full content over the /edit route.
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        var row = await readDb.ProfileBlogPosts
            .Where(p => p.BlogPostId == blogPostId)
            .Select(p => new { p.AuthorId, p.Title, p.Content, p.Rating, p.IsPublished, p.HasSpoilers, p.StoryId })
            .FirstOrDefaultAsync();

        if (row is null) return null;
        if (ActiveUser.UserId is not int viewerId || row.AuthorId != viewerId)
            throw new UnauthorizedAccessException("You can only edit your own blog posts.");

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

    public async Task<(BlogPostListingDto[] Items, int TotalCount)> GetByGroupAsync(
        int groupId, int page, int pageSize)
    {
        Rating maxRating = ActiveUser.MaxRating; // centralized Discovery-plane ceiling (WU-AccessGate)

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Kind (g): the group's own audience gate. This filters on the bare GroupId FK and never
        // expands the Group navigation, so the GroupAudience filter cannot reach it — an M-audience
        // group's E/T posts (title, date, and a snippet of the body) were readable with mature off.
        // The post-rating ceiling below is a separate axis and does not substitute for it.
        if (!await GroupVisibilityGuard.IsGroupVisibleAsync(readDb, ActiveUser, groupId))
            return ([], 0);

        // Filter by group and apply content-rating ceiling (explicit .Where — same pattern as profile
        // blog posts; named filter not available on TPT derived DbSets, see cross-cutting.md).
        IQueryable<GroupBlogPost> query = readDb.GroupBlogPosts
            .Where(p => p.GroupId == groupId && p.IsPublished && p.Rating <= maxRating);

        int totalCount = await query.CountAsync();
        if (totalCount == 0) return ([], 0);

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
                r.Item1, r.Item2, BlogPostText.MakeSnippet(r.Item3), r.Item4, r.Item5, r.Item6,
                IsPublished: true))  // group feed always published-only
            .ToArray();

        return (items, totalCount);
    }

    public async Task<(BlogPostListingDto[] Items, int TotalCount)> GetSiteAnnouncementsAsync(
        int page, int pageSize, bool includeUnpublished = false)
    {
        // No profile-visibility guard (unlike GetByAuthorAsync) and no rating ceiling (unlike
        // GetByGroupAsync) — site announcements are staff content, not a person's or group's
        // content, and never mature.
        //
        // The unpublished view is moderator/admin-only, enforced HERE, not by trusting the
        // caller's flag (the GetByAuthorAsync precedent from the 2026-07-18 endpoint-authz
        // sweep): the flag rides the public HTTP route, so a forged includeUnpublished=true
        // must degrade to the published view rather than leak draft titles/snippets.
        if (includeUnpublished && !(ActiveUser.IsModerator || ActiveUser.IsAdmin))
            includeUnpublished = false;

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        IQueryable<SiteBlogPost> query = includeUnpublished
            ? readDb.SiteBlogPosts
            : readDb.SiteBlogPosts.Where(p => p.IsPublished);

        int totalCount = await query.CountAsync();
        if (totalCount == 0) return ([], 0);

        List<(int BlogPostId, string Title, string Content, DateTime DateCreated, Rating Rating, bool IsPublished)> rows
            = await query
                .OrderByDescending(p => p.DateCreated)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ValueTuple<int, string, string, DateTime, Rating, bool>(
                    p.BlogPostId, p.Title, p.Content, p.DateCreated, p.Rating, p.IsPublished))
                .ToListAsync();

        BlogPostListingDto[] items = rows
            .Select(r => new BlogPostListingDto(
                r.Item1, r.Item2, BlogPostText.MakeSnippet(r.Item3), r.Item4, r.Item5,
                HasSpoilers: false, IsPublished: r.Item6))
            .ToArray();

        return (items, totalCount);
    }

    public async Task<SiteAnnouncementEditDto?> GetSiteAnnouncementForEditAsync(int blogPostId)
    {
        // Moderator/admin gate enforced HERE, not just in the write service (GetForEditAsync
        // precedent, endpoint-authz sweep 2026-07-18): the /site/{id}/edit route carries only
        // RequireAuthorization(), so without this gate any authenticated user could read a
        // DRAFT announcement's full content. Role, not authorship — any moderator manages any
        // site post (SitePoll's rule).
        if (!(ActiveUser.IsModerator || ActiveUser.IsAdmin))
            throw new UnauthorizedAccessException("Only moderators can manage site announcements.");

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        var row = await readDb.SiteBlogPosts
            .Where(p => p.BlogPostId == blogPostId)
            .Select(p => new { p.AuthorId, p.Title, p.Content, p.IsPublished, p.NotifyAllUsers })
            .FirstOrDefaultAsync();

        if (row is null) return null;

        return new SiteAnnouncementEditDto(
            blogPostId, row.AuthorId, row.Title, row.Content, row.IsPublished, row.NotifyAllUsers);
    }
}
