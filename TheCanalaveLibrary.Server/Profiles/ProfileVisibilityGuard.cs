using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Shared profile-visibility predicate (Class-A access control — WU-AccessGate Phase 1).
/// <see cref="ProfileVisibility"/> was previously enforced only on the profile header/bio
/// (<see cref="ServerUserProfileReadService"/>); every profile-tab data path (following list,
/// outgoing vouches, series-by-author, blog-posts-by-author, recommendations-by-user, public
/// custom lists, profile comment wall) was independently reachable over HTTP and ignored it.
/// Those services now call this predicate at the top of their profile-scoped methods and return
/// an empty result when it fails.
/// <para>
/// Semantics mirror <c>GetProfileHeaderAsync</c> exactly: the owner always passes; Private hides
/// from all non-owners; UsersOnly hides from anonymous viewers only. A missing user passes —
/// there is nothing to protect, and the caller's own query returns empty for it anyway.
/// </para>
/// </summary>
public static class ProfileVisibilityGuard
{
    public static async Task<bool> IsProfileVisibleAsync(
        ReadOnlyApplicationDbContext readDb, IActiveUserContext viewer, int profileUserId)
    {
        if (viewer.UserId == profileUserId) return true;

        ProfileVisibility? visibility = await readDb.Users
            .Where(u => u.Id == profileUserId)
            .Select(u => (ProfileVisibility?)u.PrivacySettings.ProfileVisibility)
            .FirstOrDefaultAsync();

        if (visibility is null) return true;

        return visibility switch
        {
            ProfileVisibility.Private => false,
            ProfileVisibility.UsersOnly => viewer.UserId is not null,
            _ => true,
        };
    }
}
