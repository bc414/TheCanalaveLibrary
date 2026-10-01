using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for Recommendations. No-tracking projections via
/// <see cref="ReadOnlyApplicationDbContext"/>. Approved-only filter, highlighted/spotlighted first.
/// Per-viewer <c>IsLikedByCurrentUser</c> is an EF-translated EXISTS subquery; no separate round-trip.
/// Recommender <see cref="UserCardDto.Badges"/> projects the curated visible subset
/// (DisplayOrder &gt; 0, ordered by DisplayOrder); <see cref="UserCard"/> caps the display row.
/// </summary>
public class ServerRecommendationReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IRecommendationReadService
{
    private const string DefaultAvatarUrl = "/img/default-avatar.svg";
    private const short ApprovedStatusId = (short)RecommendationStatusEnum.Approved;

    /// <summary>
    /// Protected so the derived write service can access the user context without double-capturing
    /// the constructor parameter (avoids CS9107/CS9124 warnings).
    /// </summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    /// <summary>
    /// Read contexts are created per method from this factory (`await using`) — see
    /// <c>layer2-services.md</c> §"Read-context concurrency: factory per method".
    /// </summary>
    protected IDbContextFactory<ReadOnlyApplicationDbContext> ReadDbFactory { get; } = readDbFactory;

    public async Task<List<RecommendationDto>> GetForStoryAsync(int storyId)
    {
        int? currentUserId = ActiveUser.UserId;

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Kind (g): recommendations are exactly as visible as the story they endorse. The query
        // below filters on the bare StoryId FK, so none of Story's three filters reached it — full
        // rec text and recommender cards were readable for M-unrevealed, Draft/PendingApproval/
        // Rejected and taken-down stories. (The isStoryAuthor probe below uses the filtered set, but
        // it only ever returns false — it never gated the main query.)
        if (!await StoryVisibilityGuard.IsStoryVisibleAsync(readDb, ActiveUser, storyId))
            return [];

        // Per-viewer visibility (WU-RecLifecycle): everyone sees Approved; the story's author also
        // sees NeedsRevision/Rejected (to act on them); a recommender also sees their own hidden
        // rec (with the author's note). Status/RevisionRequestNote are projected only on those
        // elevated rows — public viewers only ever receive Approved rows, so nothing leaks.
        bool isStoryAuthor = currentUserId != null && await readDb.Stories
            .AnyAsync(s => s.StoryId == storyId && s.AuthorId == currentUserId);

        return await readDb.Recommendations
            .Where(r => r.StoryId == storyId &&
                (r.StatusId == ApprovedStatusId
                 || isStoryAuthor
                 || (currentUserId != null && r.RecommenderId == currentUserId)))
            .OrderByDescending(r => r.IsHighlightedByAuthor)
            .ThenByDescending(r => r.DatePosted)
            .Select(r => new RecommendationDto(
                r.RecommendationId,
                r.StoryId,
                r.Recommender == null ? null : new UserCardDto(
                    r.Recommender.Id,
                    r.Recommender.UserName!,
                    r.Recommender.Tagline,
                    r.Recommender.ProfilePictureRelativeUrl ?? DefaultAvatarUrl,
                    r.Recommender.UserBadges
                        .Where(ub => ub.DisplayOrder > 0 && (ub.EarnedCount > 0 || !SiteBadges.CounterBackedKeys.Contains(ub.BadgeKey)))
                        .OrderBy(ub => ub.DisplayOrder)
                        .Select(ub => new UserCardBadgeDto(ub.BadgeKeyNavigation.IconBaseUrl, ub.BadgeKeyNavigation.DisplayName, ub.EarnedCount))
                        .ToList()),
                r.RecommendationDetail.Text,
                r.LikeCount,
                r.IsHiddenGem,
                r.IsHighlightedByAuthor,
                r.SuccessfulRecCount,
                r.DatePosted,
                currentUserId != null && r.Likes.Any(l => l.UserId == currentUserId),
                currentUserId != null && r.RecommenderId == currentUserId,
                (RecommendationStatusEnum)r.StatusId,
                isStoryAuthor || (currentUserId != null && r.RecommenderId == currentUserId)
                    ? r.RevisionRequestNote
                    : null))
            .ToListAsync();
    }

    public async Task<List<RecommendationDto>> GetMyRecommendationsNeedingAttentionAsync()
    {
        int? userId = ActiveUser.UserId;
        if (userId is null) return [];

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Own non-Approved recs (NeedsRevision with the author's note + Rejected), newest first —
        // the Bookshelves "Needs attention" section (WU-RecLifecycle). Recommender card omitted
        // (it's the viewer's own rec); like/own flags trivially self-referential.
        return await readDb.Recommendations
            .Where(r => r.RecommenderId == userId && r.StatusId != ApprovedStatusId)
            .OrderByDescending(r => r.DatePosted)
            .Select(r => new RecommendationDto(
                r.RecommendationId,
                r.StoryId,
                null,
                r.RecommendationDetail.Text,
                r.LikeCount,
                r.IsHiddenGem,
                r.IsHighlightedByAuthor,
                r.SuccessfulRecCount,
                r.DatePosted,
                false,
                true,
                (RecommendationStatusEnum)r.StatusId,
                r.RevisionRequestNote))
            .ToListAsync();
    }

    public async Task<RecommendationDto?> GetByIdAsync(int recommendationId)
    {
        int? currentUserId = ActiveUser.UserId;

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Kind (g), keyed by rec id — the same leak as GetForStoryAsync but enumerable, and the DTO
        // discloses the parent StoryId. Null matches a nonexistent/non-Approved rec (non-disclosure).
        // Also the composition entry point for ServerSpotlightReadService.
        int? parentStoryId = await readDb.Recommendations
            .Where(r => r.RecommendationId == recommendationId)
            .Select(r => (int?)r.StoryId)
            .FirstOrDefaultAsync();

        if (parentStoryId is not int storyId
            || !await StoryVisibilityGuard.IsStoryVisibleAsync(readDb, ActiveUser, storyId))
            return null;

        return await ProjectPublicCard(
                readDb.Recommendations
                    .Where(r => r.RecommendationId == recommendationId && r.StatusId == ApprovedStatusId),
                currentUserId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// The public (Approved-only) card projection shared by <see cref="GetByIdAsync"/> and
    /// <see cref="GetHelpfulPromptAsync"/>: the recommender card with its curated badges, body, counts,
    /// and the per-viewer like/ownership flags. Lifecycle fields keep their defaults — public reads
    /// never carry them.
    /// </summary>
    private static IQueryable<RecommendationDto> ProjectPublicCard(IQueryable<Recommendation> source, int? currentUserId) =>
        source.Select(r => new RecommendationDto(
            r.RecommendationId,
            r.StoryId,
            r.Recommender == null ? null : new UserCardDto(
                r.Recommender.Id,
                r.Recommender.UserName!,
                r.Recommender.Tagline,
                r.Recommender.ProfilePictureRelativeUrl ?? DefaultAvatarUrl,
                r.Recommender.UserBadges
                    .Where(ub => ub.DisplayOrder > 0 && (ub.EarnedCount > 0 || !SiteBadges.CounterBackedKeys.Contains(ub.BadgeKey)))
                    .OrderBy(ub => ub.DisplayOrder)
                    .Select(ub => new UserCardBadgeDto(ub.BadgeKeyNavigation.IconBaseUrl, ub.BadgeKeyNavigation.DisplayName, ub.EarnedCount))
                    .ToList()),
            r.RecommendationDetail.Text,
            r.LikeCount,
            r.IsHiddenGem,
            r.IsHighlightedByAuthor,
            r.SuccessfulRecCount,
            r.DatePosted,
            currentUserId != null && r.Likes.Any(l => l.UserId == currentUserId),
            currentUserId != null && r.RecommenderId == currentUserId));

    public async Task<IReadOnlyList<int>> GetRecommendedStoryIdsAsync()
    {
        int? userId = ActiveUser.UserId;
        if (userId is null) return [];

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        return await readDb.Recommendations
            .Where(r => r.RecommenderId == userId && r.StatusId == ApprovedStatusId)
            .Select(r => r.StoryId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<int>> GetHiddenGemStoryIdsAsync()
    {
        int? userId = ActiveUser.UserId;
        if (userId is null) return [];

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();
        return await readDb.Recommendations
            .Where(r => r.RecommenderId == userId && r.StatusId == ApprovedStatusId && r.IsHiddenGem)
            .Select(r => r.StoryId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<RecommendationDto?> GetHelpfulPromptAsync(int storyId)
    {
        int? userId = ActiveUser.UserId;
        if (userId is null) return null;

        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Gate 1 — the viewer's attribution for this story (how their Read It Later bit was set, D3).
        int? recId = await readDb.UserStoryRecommendationSources
            .Where(src => src.UserId == userId && src.StoryId == storyId)
            .Select(src => (int?)src.SourceRecommendationId)
            .FirstOrDefaultAsync();
        if (recId is null) return null;

        // Gate 4 — no success recorded yet (Yes consumes the row, but a lingering one must not re-ask).
        if (await readDb.RecommendationSuccesses.AnyAsync(s => s.UserId == userId && s.RecommendationId == recId))
            return null;

        // Gate 2 — the rec is visible: its story passes the viewer's guard (rating, status, takedown);
        // Approved + not taken down are in the query below (the read context's IsTakenDown filter).
        if (!await StoryVisibilityGuard.IsStoryVisibleAsync(readDb, ActiveUser, storyId))
            return null;

        // Gate 3 — a named recommender, checked at read time so an account deleted between the save
        // and this read gets no prompt (an anonymous card cannot be thanked).
        return await ProjectPublicCard(
                readDb.Recommendations.Where(r => r.RecommendationId == recId
                                                  && r.StoryId == storyId
                                                  && r.StatusId == ApprovedStatusId
                                                  && r.RecommenderId != null),
                userId)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<int>> GetRecommendedStoryIdsByUserAsync(int userId)
    {
        // Returns the story ids for which the given user has written a recommendation,
        // for use as the candidate-id set on the Profile page's Recommendations tab.
        // RecommenderId is nullable (anonymous recs are allowed), so we compare int? == int.
        // No rating gating needed — the set only determines which stories appear in the
        // StoryDeck; the deck applies the global content-rating query filter at listing time.
        await using ReadOnlyApplicationDbContext readDb = await ReadDbFactory.CreateDbContextAsync();

        // Class-A: a user's recommendations are profile-tab data; respect their ProfileVisibility
        // (WU-AccessGate Phase 1 — /api/recommendations/by-user/{id}/story-ids is directly reachable).
        if (!await ProfileVisibilityGuard.IsProfileVisibleAsync(readDb, ActiveUser, userId))
            return [];

        // Approved-only (D1 fix, WU-RecLifecycle): the siblings above all filter by status and the
        // interface doc promises it; without this, NeedsRevision/Rejected story-ids would leak onto
        // the public profile tab. Applies to the owner viewing their own profile too — owner
        // visibility into hidden recs lives in GetMyRecommendationsNeedingAttentionAsync.
        return await readDb.Recommendations
            .Where(r => r.RecommenderId == userId && r.StatusId == ApprovedStatusId)
            .Select(r => r.StoryId)
            .Distinct()
            .ToListAsync();
    }
}
