using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Manual Tree Search pivots (Feature 33 / WU40) — stateless, degree-1 queries over LIVE tables.
/// See <see cref="IManualTreeSearchReadService"/> for the contract and privacy model.
///
/// <para><b>Design notes (audit/Discovery.md F33):</b> this service owns its own recommendation-
/// family / favoriters / catalog queries rather than composing
/// <c>IRecommendationReadService.GetForStoryAsync</c> — that method returns the full unpaged list
/// with no flag composition, and honest per-section paging (count + page in SQL) is a settled
/// requirement here. The <see cref="RecommendationDto"/>/<see cref="UserCardDto"/> projections
/// are transcribed from <c>ServerRecommendationReadService</c> and must stay shape-identical
/// (the tree pane renders through the same components); they are inlined at each Select site
/// because EF cannot translate a shared method call inside a projection expression.</para>
///
/// <para><b>Story visibility:</b> every story-valued row is constrained by a subquery against the
/// filtered <c>Stories</c> DbSet (global ContentRating/IsTakenDown query filters apply to the
/// DbSet root, NOT to navigations) plus the published-status window (2..7 — drafts/pending/
/// rejected are not globally filtered, so the window is explicit). Counts and pages share one
/// predicate so <c>TotalCount</c> is never a lie.</para>
/// </summary>
public class ServerManualTreeSearchReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IStoryReadService storyReadService,
    IActiveUserContext activeUser) : IManualTreeSearchReadService
{
    private const string DefaultAvatarUrl = "/img/default-avatar.svg";
    private const short ApprovedStatusId = 2;

    public async Task<ManualTreeNeighborsDto> GetStoryNeighborsAsync(
        StoryNeighborsRequest request, CancellationToken ct = default)
    {
        using Activity? activity =
            CanalaveTelemetry.Discovery.Source.StartActivity("Discovery.ManualTreePivot");
        activity?.SetTag("canalave.manual_tree.anchor", "story");
        activity?.SetTag("canalave.manual_tree.anchor_id", request.StoryId);

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync(ct);
        IQueryable<Story> visible = VisibleStories(readDb);

        UserCardDto? author = null;
        if (request.IncludeAuthor)
        {
            // Querying via the filtered Stories DbSet: an anchor the viewer cannot see yields
            // no author (and empty sections below) rather than leaking through the pivot.
            author = await readDb.Stories
                .Where(s => s.StoryId == request.StoryId && s.Author != null)
                .Select(s => new UserCardDto(
                    s.Author!.Id,
                    s.Author.UserName!,
                    s.Author.Tagline,
                    s.Author.ProfilePictureRelativeUrl ?? DefaultAvatarUrl,
                    s.Author.UserBadges
                        .Where(ub => ub.DisplayOrder > 0)
                        .OrderBy(ub => ub.DisplayOrder)
                        .Select(ub => new UserCardBadgeDto(ub.BadgeKeyNavigation.IconBaseUrl, ub.BadgeKeyNavigation.DisplayName))
                        .ToList()))
                .FirstOrDefaultAsync(ct);
        }

        ManualTreeSectionDto<ManualTreeRecItemDto>? family = null;
        if (request.IncludeRecommendations || request.IncludeHiddenGems || request.IncludeSpotlights)
        {
            IQueryable<Recommendation> q = FamilyQuery(readDb, visible,
                    request.IncludeRecommendations, request.IncludeHiddenGems, request.IncludeSpotlights)
                .Where(r => r.StoryId == request.StoryId);
            family = await PageFamilyAsync(q, request.RecommendationsPage, request.PageSize, ct);
        }

        ManualTreeSectionDto<UserCardDto>? favoriters = null;
        if (request.IncludeFavoriters)
        {
            // Public favorites only — the both-true (favorite + hidden) state is a favorite
            // hidden from visitors, so !IsHiddenFavorite excludes it here exactly as the
            // profile queries do. Manual never surfaces hidden-favorite reach at all.
            IQueryable<User> q = readDb.UserStoryInteractions
                .Where(i => i.StoryId == request.StoryId && i.IsFavorite && !i.IsHiddenFavorite)
                .Join(readDb.Users, i => i.UserId, u => u.Id, (i, u) => u);

            int total = await q.CountAsync(ct);
            List<UserCardDto> items = await q
                .OrderBy(u => u.UserName)
                .Skip((Math.Max(request.FavoritersPage, 1) - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(u => new UserCardDto(
                    u.Id,
                    u.UserName!,
                    u.Tagline,
                    u.ProfilePictureRelativeUrl ?? DefaultAvatarUrl,
                    u.UserBadges
                        .Where(ub => ub.DisplayOrder > 0)
                        .OrderBy(ub => ub.DisplayOrder)
                        .Select(ub => new UserCardBadgeDto(ub.BadgeKeyNavigation.IconBaseUrl, ub.BadgeKeyNavigation.DisplayName))
                        .ToList()))
                .ToListAsync(ct);
            favoriters = new ManualTreeSectionDto<UserCardDto>(items, total);
        }

        return new ManualTreeNeighborsDto
        {
            Author = author,
            RecommendationFamily = family,
            Favoriters = favoriters,
        };
    }

    public async Task<ManualTreeNeighborsDto> GetUserNeighborsAsync(
        UserNeighborsRequest request, CancellationToken ct = default)
    {
        using Activity? activity =
            CanalaveTelemetry.Discovery.Source.StartActivity("Discovery.ManualTreePivot");
        activity?.SetTag("canalave.manual_tree.anchor", "user");
        activity?.SetTag("canalave.manual_tree.anchor_id", request.UserId);

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync(ct);
        IQueryable<Story> visible = VisibleStories(readDb);

        ManualTreeSectionDto<ManualTreeRecItemDto>? family = null;
        if (request.IncludeRecommendations || request.IncludeHiddenGems || request.IncludeSpotlights)
        {
            IQueryable<Recommendation> q = FamilyQuery(readDb, visible,
                    request.IncludeRecommendations, request.IncludeHiddenGems, request.IncludeSpotlights)
                .Where(r => r.RecommenderId == request.UserId);
            family = await PageFamilyAsync(q, request.RecommendationsPage, request.PageSize, ct);
        }

        ManualTreeSectionDto<StoryListingDto>? favorites = null;
        if (request.IncludeFavorites)
        {
            IQueryable<Story> q = readDb.UserStoryInteractions
                .Where(i => i.UserId == request.UserId && i.IsFavorite && !i.IsHiddenFavorite)
                .Join(visible, i => i.StoryId, s => s.StoryId, (i, s) => s);
            favorites = await PageStoriesAsync(
                q.OrderByDescending(s => s.PublishedDate).ThenBy(s => s.StoryId),
                request.FavoritesPage, request.PageSize, ct);
        }

        ManualTreeSectionDto<StoryListingDto>? authored = null;
        int? pinnedStoryId = await readDb.Users
            .Where(u => u.Id == request.UserId)
            .Select(u => u.PinnedStoryId)
            .FirstOrDefaultAsync(ct);
        if (request.IncludeAuthored)
        {
            // Pinned story sorted first (the author's deliberate "see this first" choice),
            // then newest-published — the ordering the settled section model requires.
            IQueryable<Story> q = visible.Where(s => s.AuthorId == request.UserId);
            authored = await PageStoriesAsync(
                q.OrderByDescending(s => s.StoryId == pinnedStoryId)
                 .ThenByDescending(s => s.PublishedDate).ThenBy(s => s.StoryId),
                request.AuthoredPage, request.PageSize, ct);
        }

        ManualTreeSectionDto<StoryListingDto>? vouched = null;
        if (request.IncludeVouchedStories)
        {
            // Forward direction only: this user's ≤5 vouchees → their published stories.
            // (Incoming vouches are owner-private, §5.8 — there is no reverse traversal.)
            IQueryable<int> voucheeIds = readDb.Vouches
                .Where(v => v.VouchingUserId == request.UserId)
                .Select(v => v.VouchedUserId);
            IQueryable<Story> q = visible
                .Where(s => s.AuthorId != null && voucheeIds.Contains(s.AuthorId.Value));
            vouched = await PageStoriesAsync(
                q.OrderByDescending(s => s.PublishedDate).ThenBy(s => s.StoryId),
                request.VouchedStoriesPage, request.PageSize, ct);
        }

        return new ManualTreeNeighborsDto
        {
            RecommendationFamily = family,
            Favorites = favorites,
            Authored = authored,
            PinnedStoryId = pinnedStoryId,
            VouchedStories = vouched,
        };
    }

    public async Task<ManualTreeNodeDisplaysDto> GetNodeDisplaysAsync(
        IReadOnlyCollection<int> storyIds, IReadOnlyCollection<int> userIds, CancellationToken ct = default)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync(ct);

        List<ManualTreeNodeDisplayDto> stories = [];
        if (storyIds.Count > 0)
        {
            // Filtered Stories DbSet + status window: an id the viewer can no longer see simply
            // doesn't come back — the caller prunes it (rehydration contract).
            stories = await VisibleStories(readDb)
                .Where(s => storyIds.Contains(s.StoryId))
                .Select(s => new ManualTreeNodeDisplayDto(
                    s.StoryId, s.StoryListing.StoryTitle, s.StoryListing.CoverArtRelativeUrl))
                .ToListAsync(ct);
        }

        List<ManualTreeNodeDisplayDto> users = [];
        if (userIds.Count > 0)
        {
            users = await readDb.Users
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new ManualTreeNodeDisplayDto(
                    u.Id, u.UserName!, u.ProfilePictureRelativeUrl))
                .ToListAsync(ct);
        }

        return new ManualTreeNodeDisplaysDto(stories, users);
    }

    // ── Shared query fragments ─────────────────────────────────────────────────────────────────

    /// <summary>Published, viewer-visible stories: the filtered DbSet (ContentRating/IsTakenDown
    /// global filters) plus the explicit published-status window (2..7 — status is not globally
    /// filtered, so drafts/pending/rejected must be excluded here). Returned as an IQueryable and
    /// captured OUTSIDE lambdas — EF translates a captured queryable subquery, not a method call
    /// inside an expression tree.</summary>
    private static IQueryable<Story> VisibleStories(ReadOnlyApplicationDbContext readDb) =>
        readDb.Stories.Where(s =>
            s.StoryStatusId >= StoryStatusEnum.InProgress && s.StoryStatusId <= StoryStatusEnum.OpenBeta);

    /// <summary>
    /// The recommendation family: ONE query whose flags widen/narrow the WHERE clause — a row
    /// matching several flags appears once (badges stack on the card). Approved, non-anonymized
    /// rows only, constrained to viewer-visible stories.
    /// </summary>
    private static IQueryable<Recommendation> FamilyQuery(
        ReadOnlyApplicationDbContext readDb, IQueryable<Story> visible,
        bool plain, bool gems, bool spotlights)
    {
        return readDb.Recommendations.Where(r =>
            r.StatusId == ApprovedStatusId
            && r.RecommenderId != null
            && visible.Any(s => s.StoryId == r.StoryId)
            && ((plain && !r.IsHiddenGem && !r.IsHighlightedByAuthor)
                || (gems && r.IsHiddenGem)
                || (spotlights && r.IsHighlightedByAuthor)));
    }

    /// <summary>Counts + pages one family query, then hydrates each row's story listing into
    /// the compound-row item. Ordering: newest first, id tiebreak (stable pagination).</summary>
    private async Task<ManualTreeSectionDto<ManualTreeRecItemDto>> PageFamilyAsync(
        IQueryable<Recommendation> query, int page, int pageSize, CancellationToken ct)
    {
        int? currentUserId = activeUser.UserId;

        int total = await query.CountAsync(ct);
        var recs = await query
            .OrderByDescending(r => r.DatePosted).ThenBy(r => r.RecommendationId)
            .Skip((Math.Max(page, 1) - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RecommendationDto(
                r.RecommendationId,
                r.StoryId,
                r.Recommender == null ? null : new UserCardDto(
                    r.Recommender.Id,
                    r.Recommender.UserName!,
                    r.Recommender.Tagline,
                    r.Recommender.ProfilePictureRelativeUrl ?? DefaultAvatarUrl,
                    r.Recommender.UserBadges
                        .Where(ub => ub.DisplayOrder > 0)
                        .OrderBy(ub => ub.DisplayOrder)
                        .Select(ub => new UserCardBadgeDto(ub.BadgeKeyNavigation.IconBaseUrl, ub.BadgeKeyNavigation.DisplayName))
                        .ToList()),
                r.RecommendationDetail.Text,
                r.LikeCount,
                r.IsHiddenGem,
                r.IsHighlightedByAuthor,
                r.SuccessfulRecCount,
                r.DatePosted,
                currentUserId != null && r.Likes.Any(l => l.UserId == currentUserId),
                currentUserId != null && r.RecommenderId == currentUserId))
            .ToListAsync(ct);

        List<StoryListingDto> listings = await HydrateOrderedAsync(
            [.. recs.Select(r => r.StoryId).Distinct()]);
        Dictionary<int, StoryListingDto> byId = listings.ToDictionary(l => l.StoryId);

        // A rec whose story failed hydration (vanished between the two queries) drops silently —
        // the compound row is meaningless without its story half.
        List<ManualTreeRecItemDto> items = [.. recs
            .Where(r => byId.ContainsKey(r.StoryId))
            .Select(r => new ManualTreeRecItemDto(r, byId[r.StoryId]))];

        return new ManualTreeSectionDto<ManualTreeRecItemDto>(items, total);
    }

    /// <summary>Counts + pages one ordered story query, hydrating the page's ids into listings
    /// via the existing <see cref="IStoryReadService.GetListingsByIdsAsync"/> (preserving the
    /// page order — the hydrator does not).</summary>
    private async Task<ManualTreeSectionDto<StoryListingDto>> PageStoriesAsync(
        IOrderedQueryable<Story> orderedQuery, int page, int pageSize, CancellationToken ct)
    {
        int total = await orderedQuery.CountAsync(ct);
        List<int> pageIds = await orderedQuery
            .Skip((Math.Max(page, 1) - 1) * pageSize)
            .Take(pageSize)
            .Select(s => s.StoryId)
            .ToListAsync(ct);

        return new ManualTreeSectionDto<StoryListingDto>(await HydrateOrderedAsync(pageIds), total);
    }

    private async Task<List<StoryListingDto>> HydrateOrderedAsync(IReadOnlyList<int> orderedIds)
    {
        if (orderedIds.Count == 0) return [];
        StoryListingDto[] listings = await storyReadService.GetListingsByIdsAsync(orderedIds);
        Dictionary<int, StoryListingDto> byId = listings.ToDictionary(l => l.StoryId);
        return [.. orderedIds.Where(byId.ContainsKey).Select(id => byId[id])];
    }
}
