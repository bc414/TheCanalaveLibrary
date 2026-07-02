using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation of <see cref="IFollowingReadService"/>. Uses
/// <see cref="ReadOnlyApplicationDbContext"/> (no-tracking) and projects straight to DTOs.
/// Avatar URLs are copied verbatim from <c>User.ProfilePictureRelativeUrl</c> (or a default
/// fallback) — not resolved through <c>ISpriteReadService</c>. Badges project the curated visible
/// subset (DisplayOrder &gt; 0, ordered by DisplayOrder); <see cref="UserCard"/> caps the display row.
/// </summary>
public class ServerFollowingReadService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    IActiveUserContext activeUser) : IFollowingReadService
{
    private const string DefaultAvatarUrl = "/img/default-avatar.svg";

    /// <summary>
    /// Exposed so the derived write service can call RequireAuthenticatedUser() without re-capturing
    /// the activeUser primary constructor parameter (avoids CS9107 double-capture warning).
    /// </summary>
    protected int? CurrentUserId => activeUser.UserId;

    public async Task<UserRelationshipStateDto> GetRelationshipStateAsync(int targetUserId)
    {
        int? viewerId = activeUser.UserId;
        if (viewerId is null)
            return new UserRelationshipStateDto(false, false, false, 0);

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();

        bool isFollowing = await readDb.FollowedUsers
            .AnyAsync(f => f.UserId == viewerId && f.FollowedUserId == targetUserId);

        bool receiveAlerts = isFollowing && await readDb.FollowedUsers
            .Where(f => f.UserId == viewerId && f.FollowedUserId == targetUserId)
            .Select(f => f.ReceiveAlerts)
            .FirstOrDefaultAsync();

        bool isVouched = await readDb.Vouches
            .AnyAsync(v => v.VouchingUserId == viewerId && v.VouchedUserId == targetUserId);

        int outgoingVouchCount = await readDb.Vouches
            .CountAsync(v => v.VouchingUserId == viewerId);

        return new UserRelationshipStateDto(isFollowing, receiveAlerts, isVouched, outgoingVouchCount);
    }

    public async Task<IReadOnlyList<UserCardDto>> GetFollowedUsersAsync(int userId)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        return await readDb.FollowedUsers
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.DateFollowed)
            .Select(f => new UserCardDto(
                f.FollowedUserNavigation.Id,
                f.FollowedUserNavigation.UserName!,
                f.FollowedUserNavigation.Tagline,
                f.FollowedUserNavigation.ProfilePictureRelativeUrl ?? DefaultAvatarUrl,
                f.FollowedUserNavigation.UserBadges
                    .Where(ub => ub.DisplayOrder > 0)
                    .OrderBy(ub => ub.DisplayOrder)
                    .Select(ub => new UserCardBadgeDto(ub.BadgeKeyNavigation.IconBaseUrl, ub.BadgeKeyNavigation.DisplayName))
                    .ToList()
            ))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<VouchDisplayDto>> GetOutgoingVouchesAsync(int userId)
    {
        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        return await readDb.Vouches
            .Where(v => v.VouchingUserId == userId)
            .OrderBy(v => v.DateVouched)
            .Select(v => new VouchDisplayDto(
                new UserCardDto(
                    v.VouchedUser.Id,
                    v.VouchedUser.UserName!,
                    v.VouchedUser.Tagline,
                    v.VouchedUser.ProfilePictureRelativeUrl ?? DefaultAvatarUrl,
                    v.VouchedUser.UserBadges
                        .Where(ub => ub.DisplayOrder > 0)
                        .OrderBy(ub => ub.DisplayOrder)
                        .Select(ub => new UserCardBadgeDto(ub.BadgeKeyNavigation.IconBaseUrl, ub.BadgeKeyNavigation.DisplayName))
                        .ToList()
                ),
                v.VouchText,
                v.DateVouched
            ))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<VouchDisplayDto>> GetIncomingVouchesAsync()
    {
        int? ownerId = activeUser.UserId;
        if (ownerId is null)
            return [];

        await using ReadOnlyApplicationDbContext readDb = await readDbFactory.CreateDbContextAsync();
        return await readDb.Vouches
            .Where(v => v.VouchedUserId == ownerId)
            .OrderBy(v => v.DateVouched)
            .Select(v => new VouchDisplayDto(
                new UserCardDto(
                    v.VouchingUser.Id,
                    v.VouchingUser.UserName!,
                    v.VouchingUser.Tagline,
                    v.VouchingUser.ProfilePictureRelativeUrl ?? DefaultAvatarUrl,
                    v.VouchingUser.UserBadges
                        .Where(ub => ub.DisplayOrder > 0)
                        .OrderBy(ub => ub.DisplayOrder)
                        .Select(ub => new UserCardBadgeDto(ub.BadgeKeyNavigation.IconBaseUrl, ub.BadgeKeyNavigation.DisplayName))
                        .ToList()
                ),
                v.VouchText,
                v.DateVouched
            ))
            .ToListAsync();
    }
}
