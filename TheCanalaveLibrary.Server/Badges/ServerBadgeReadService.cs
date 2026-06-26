using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side read implementation for badges (Feature 50, WU36).
/// No-tracking projections via <see cref="ReadOnlyApplicationDbContext"/>.
/// Returns the full earned-badge list for the curation settings form.
/// </summary>
public class ServerBadgeReadService(ReadOnlyApplicationDbContext readDb) : IBadgeReadService
{
    /// <summary>
    /// Protected so the derived write service can access the read DbContext without capturing
    /// it a second time (avoids CS9107 warning on the shared constructor parameter).
    /// </summary>
    protected ReadOnlyApplicationDbContext ReadDb { get; } = readDb;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EarnedBadgeDto>> GetMyBadgesForCurationAsync(int userId)
    {
        // Visible badges (DisplayOrder > 0) first, ordered by DisplayOrder ascending.
        // Hidden badges (DisplayOrder == 0) follow, ordered by catalogue SortOrder.
        // CASE WHEN display_order = 0 THEN 2147483647 ELSE display_order END is valid PostgreSQL.
        return await ReadDb.UserBadges
            .Where(ub => ub.UserId == userId)
            .OrderBy(ub => ub.DisplayOrder == 0 ? int.MaxValue : ub.DisplayOrder)
            .ThenBy(ub => ub.BadgeKeyNavigation.SortOrder)
            .Select(ub => new EarnedBadgeDto(
                ub.BadgeKey,
                ub.BadgeKeyNavigation.DisplayName,
                ub.BadgeKeyNavigation.Description,
                ub.BadgeKeyNavigation.IconBaseUrl,
                ub.BadgeKeyNavigation.SortOrder,
                ub.DisplayOrder))
            .ToListAsync();
    }
}
