using Microsoft.EntityFrameworkCore;
using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.Server;

/// <summary>
/// Server-side write implementation for badges (Feature 50, WU36).
/// Inherits the read path via primary-constructor chaining.
///
/// <para><b>AwardAsync idempotency:</b> Checks for an existing row before inserting — no-op and
/// returns <c>false</c> if already earned. Callers do not need a prior existence check; they can
/// call <c>AwardAsync</c> on every qualifying event without risk of duplicates.</para>
///
/// <para><b>Default visibility:</b> Newly awarded badges receive
/// <c>DisplayOrder = (max existing DisplayOrder) + 1</c>, so they appear in the profile badge bar
/// without the user visiting Settings first.</para>
///
/// <para><b>SetDisplayOrderAsync:</b> Loads all earned badges into the change tracker (small set —
/// typically &lt;10 rows) and updates DisplayOrder in one <c>SaveChangesAsync</c>. Validates that
/// every requested key is owned before persisting.</para>
/// </summary>
public class ServerBadgeWriteService(
    IDbContextFactory<ReadOnlyApplicationDbContext> readDbFactory,
    ApplicationDbContext writeDb)
    : ServerBadgeReadService(readDbFactory), IBadgeWriteService
{
    /// <inheritdoc/>
    public async Task<bool> AwardAsync(int userId, string badgeKey)
    {
        // Idempotency: no-op if already earned.
        bool alreadyEarned = await writeDb.UserBadges
            .AnyAsync(ub => ub.UserId == userId && ub.BadgeKey == badgeKey);
        if (alreadyEarned) return false;

        // Visible-by-default: slot after the current highest DisplayOrder for this user.
        int? maxOrder = await writeDb.UserBadges
            .Where(ub => ub.UserId == userId)
            .MaxAsync(ub => (int?)ub.DisplayOrder);
        int newDisplayOrder = (maxOrder ?? 0) + 1;

        writeDb.UserBadges.Add(new UserBadge
        {
            UserId       = userId,
            BadgeKey     = badgeKey,
            DisplayOrder = newDisplayOrder
            // DateEarned defaults to CURRENT_TIMESTAMP via EF HasDefaultValueSql.
        });

        await writeDb.SaveChangesAsync();
        return true;
    }

    /// <inheritdoc/>
    public async Task SetDisplayOrderAsync(int userId, IReadOnlyList<string> orderedVisibleKeys)
    {
        // Load all earned badges into the change tracker (typically ≤10 rows; safe for SaveChanges).
        List<UserBadge> earned = await writeDb.UserBadges
            .Where(ub => ub.UserId == userId)
            .ToListAsync();

        // Validate: every requested key must be owned by this user.
        HashSet<string> earnedSet = [.. earned.Select(ub => ub.BadgeKey)];
        foreach (string key in orderedVisibleKeys)
        {
            if (!earnedSet.Contains(key))
                throw new InvalidOperationException(
                    $"Badge '{key}' has not been earned by user {userId} and cannot be shown.");
        }

        // Build key → desired DisplayOrder map (1-based for visible keys).
        Dictionary<string, int> displayOrders = orderedVisibleKeys
            .Select((key, idx) => (key, order: idx + 1))
            .ToDictionary(x => x.key, x => x.order);

        // Apply: visible keys get 1..n; all other earned keys become hidden (0).
        foreach (UserBadge ub in earned)
            ub.DisplayOrder = displayOrders.TryGetValue(ub.BadgeKey, out int order) ? order : 0;

        await writeDb.SaveChangesAsync();
    }
}
