using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for Recommendations. No-tracking projections via
/// <see cref="ReadOnlyApplicationDbContext"/>. Approved-only filter, highlighted/spotlighted first.
/// Per-viewer <c>IsLikedByCurrentUser</c> is an EF-translated EXISTS subquery; no separate round-trip.
/// Badges are empty until WU36.
/// </summary>
public class ServerRecommendationReadService(
    ReadOnlyApplicationDbContext readDb,
    IActiveUserContext activeUser) : IRecommendationReadService
{
    private const string DefaultAvatarUrl = "/img/default-avatar.svg";
    private const short ApprovedStatusId = 2;

    /// <summary>
    /// Protected so the derived write service can access the user context without double-capturing
    /// the constructor parameter (avoids CS9107/CS9124 warnings).
    /// </summary>
    protected IActiveUserContext ActiveUser { get; } = activeUser;

    protected ReadOnlyApplicationDbContext ReadDb { get; } = readDb;

    public async Task<List<RecommendationDto>> GetForStoryAsync(int storyId)
    {
        int? currentUserId = ActiveUser.UserId;

        return await ReadDb.Recommendations
            .Where(r => r.StoryId == storyId && r.StatusId == ApprovedStatusId)
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
                    new List<UserCardBadgeDto>()),
                r.RecommendationDetail.Text,
                r.LikeCount,
                r.IsHiddenGem,
                r.IsHighlightedByAuthor,
                r.SuccessfulRecCount,
                r.DatePosted,
                currentUserId != null && r.Likes.Any(l => l.UserId == currentUserId),
                currentUserId != null && r.RecommenderId == currentUserId))
            .ToListAsync();
    }

    public async Task<RecommendationDto?> GetByIdAsync(int recommendationId)
    {
        int? currentUserId = ActiveUser.UserId;

        return await ReadDb.Recommendations
            .Where(r => r.RecommendationId == recommendationId && r.StatusId == ApprovedStatusId)
            .Select(r => new RecommendationDto(
                r.RecommendationId,
                r.StoryId,
                r.Recommender == null ? null : new UserCardDto(
                    r.Recommender.Id,
                    r.Recommender.UserName!,
                    r.Recommender.Tagline,
                    r.Recommender.ProfilePictureRelativeUrl ?? DefaultAvatarUrl,
                    new List<UserCardBadgeDto>()),
                r.RecommendationDetail.Text,
                r.LikeCount,
                r.IsHiddenGem,
                r.IsHighlightedByAuthor,
                r.SuccessfulRecCount,
                r.DatePosted,
                currentUserId != null && r.Likes.Any(l => l.UserId == currentUserId),
                currentUserId != null && r.RecommenderId == currentUserId))
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<int>> GetRecommendedStoryIdsAsync()
    {
        int? userId = ActiveUser.UserId;
        if (userId is null) return [];

        return await ReadDb.Recommendations
            .Where(r => r.RecommenderId == userId && r.StatusId == ApprovedStatusId)
            .Select(r => r.StoryId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<IReadOnlyList<int>> GetHiddenGemStoryIdsAsync()
    {
        int? userId = ActiveUser.UserId;
        if (userId is null) return [];

        return await ReadDb.Recommendations
            .Where(r => r.RecommenderId == userId && r.StatusId == ApprovedStatusId && r.IsHiddenGem)
            .Select(r => r.StoryId)
            .Distinct()
            .ToListAsync();
    }
}
