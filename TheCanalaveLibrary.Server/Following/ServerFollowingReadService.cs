using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation of <see cref="IFollowingReadService"/>. Uses
/// <see cref="ReadOnlyApplicationDbContext"/> (no-tracking) and projects straight to DTOs.
/// Avatar URLs are copied verbatim from <c>User.ProfilePictureRelativeUrl</c> (or a default
/// fallback) — not resolved through <c>ISpriteReadService</c>. Badges are empty until WU36.
/// </summary>
public class ServerFollowingReadService(
    ReadOnlyApplicationDbContext readDb,
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
        return await readDb.FollowedUsers
            .Where(f => f.UserId == userId)
            .OrderBy(f => f.DateFollowed)
            .Select(f => new UserCardDto(
                f.FollowedUserNavigation.Id,
                f.FollowedUserNavigation.UserName!,
                f.FollowedUserNavigation.Tagline,
                f.FollowedUserNavigation.ProfilePictureRelativeUrl ?? DefaultAvatarUrl,
                new List<UserCardBadgeDto>()
            ))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<VouchDisplayDto>> GetOutgoingVouchesAsync(int userId)
    {
        return await readDb.Vouches
            .Where(v => v.VouchingUserId == userId)
            .OrderBy(v => v.DateVouched)
            .Select(v => new VouchDisplayDto(
                new UserCardDto(
                    v.VouchedUser.Id,
                    v.VouchedUser.UserName!,
                    v.VouchedUser.Tagline,
                    v.VouchedUser.ProfilePictureRelativeUrl ?? DefaultAvatarUrl,
                    new List<UserCardBadgeDto>()
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

        return await readDb.Vouches
            .Where(v => v.VouchedUserId == ownerId)
            .OrderBy(v => v.DateVouched)
            .Select(v => new VouchDisplayDto(
                new UserCardDto(
                    v.VouchingUser.Id,
                    v.VouchingUser.UserName!,
                    v.VouchingUser.Tagline,
                    v.VouchingUser.ProfilePictureRelativeUrl ?? DefaultAvatarUrl,
                    new List<UserCardBadgeDto>()
                ),
                v.VouchText,
                v.DateVouched
            ))
            .ToListAsync();
    }
}
