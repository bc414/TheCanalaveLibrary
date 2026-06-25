using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for public profile display (Feature 21, WU30).
/// All queries go through <see cref="ReadOnlyApplicationDbContext"/> (NoTracking).
///
/// <see cref="GetProfileHeaderAsync"/>: loads the banner DTO from User, UserStat, UserBadge+Badge,
/// and outgoing Vouches. <c>RelationshipState</c> is always <c>null</c> on return — the page
/// dispatcher (<c>ProfilePage.razor</c>) overlays it via
/// <c>IFollowingReadService.GetRelationshipStateAsync</c> when the viewer is authenticated and
/// is not the profile owner, then re-constructs via <c>header with { RelationshipState = ... }</c>.
///
/// <see cref="GetProfileTextAsync"/>: reads the cold <c>UserProfile.Text</c> partition (bio HTML).
/// The dispatcher calls this separately so it can load it lazily on Profile-tab activation.
/// </summary>
public class ServerUserProfileReadService(
    ReadOnlyApplicationDbContext readDb,
    IActiveUserContext activeUser) : IUserProfileReadService
{
    private const string DefaultAvatarUrl = "/img/default-avatar.svg";

    public async Task<ProfileHeaderDto?> GetProfileHeaderAsync(int userId, bool includePrivate)
    {
        var row = await readDb.Users
            .Where(u => u.Id == userId)
            .Select(u => new
            {
                u.Id,
                Username         = u.UserName!,
                u.Tagline,
                AvatarUrl        = u.ProfilePictureRelativeUrl,
                Privacy          = u.PrivacySettings,
                Stats            = u.UserStat,
                Badges           = u.UserBadges
                    .OrderBy(ub => ub.BadgeKeyNavigation.SortOrder)
                    .Select(ub => new UserCardBadgeDto(ub.BadgeKeyNavigation.IconBaseUrl, ub.BadgeKeyNavigation.DisplayName))
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (row is null) return null;

        // Visibility gating for non-owners.
        // UsersOnly: requires the viewer to be authenticated (active user context resolves that).
        // Private: only the owner can view their own profile.
        if (!includePrivate)
        {
            if (row.Privacy.ProfileVisibility == ProfileVisibility.Private)
                return null;

            if (row.Privacy.ProfileVisibility == ProfileVisibility.UsersOnly
                && activeUser.UserId is null)
                return null;
        }

        // Outgoing vouches — loaded in a second query to avoid a fan-out join duplicating badge rows.
        List<VouchDisplayDto> vouches = await readDb.Vouches
            .Where(v => v.VouchingUserId == userId)
            .OrderBy(v => v.DateVouched)
            .Select(v => new VouchDisplayDto(
                new UserCardDto(
                    v.VouchedUser.Id,
                    v.VouchedUser.UserName!,
                    v.VouchedUser.Tagline,
                    v.VouchedUser.ProfilePictureRelativeUrl ?? DefaultAvatarUrl,
                    new List<UserCardBadgeDto>()),
                v.VouchText,
                v.DateVouched))
            .ToListAsync();

        // Stats are suppressed when the owner chose to hide them from visitors.
        // The owner always sees their own stats (includePrivate = true).
        UserStatsDto? stats = null;
        if ((includePrivate || row.Privacy.ShowUserStats) && row.Stats is not null)
        {
            UserStat s = row.Stats;
            stats = new UserStatsDto(
                s.StoriesWritten,
                s.WordsWritten,
                s.FollowerCount,
                s.AuthorsFollowed,
                s.CommentsWritten,
                s.RecommendationsWritten,
                s.RecommendationsReceived,
                s.BlogPostsWritten,
                s.GroupsJoined,
                s.FavoritesOnStories,
                s.StoriesRead,
                s.StoriesInProgress,
                s.StoriesIgnored);
        }

        return new ProfileHeaderDto(
            row.Id,
            row.Username,
            row.AvatarUrl ?? DefaultAvatarUrl,
            row.Tagline,
            row.Badges,
            vouches,
            stats,
            RelationshipState: null,   // dispatcher overlays via IFollowingReadService
            row.Privacy.ProfileVisibility,
            row.Privacy.AllowProfileComments,
            row.Privacy.ShowUserStats);
    }

    public async Task<string?> GetProfileTextAsync(int userId)
    {
        return await readDb.UserProfiles
            .Where(p => p.UserId == userId)
            .Select(p => p.Text)
            .FirstOrDefaultAsync();
    }
}
