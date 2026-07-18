namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write interface for the badge feature (Feature 50, WU36). Handles synchronous inline award
/// checks (called best-effort from other write services after a qualifying event) and user-driven
/// curation of which earned badges are displayed on their profile and UserCards.
/// </summary>
public interface IBadgeWriteService : IBadgeReadService
{
    /// <summary>
    /// Idempotent award: inserts a <c>UserBadge</c> row for (<paramref name="userId"/>,
    /// <paramref name="badgeKey"/>) if it does not already exist. The new row is visible by
    /// default (<c>DisplayOrder = max existing DisplayOrder + 1</c>).
    /// <para>
    /// Returns <c>true</c> if newly awarded; <c>false</c> if the user already holds this badge.
    /// Never throws for a duplicate award — safe to call on every qualifying event without a
    /// prior existence check.
    /// </para>
    /// </summary>
    Task<bool> AwardAsync(int userId, string badgeKey);

    /// <summary>
    /// Owner curation: sets <c>DisplayOrder</c> for all earned badges belonging to
    /// <paramref name="userId"/>. Keys in <paramref name="orderedVisibleKeys"/> receive
    /// <c>DisplayOrder = 1, 2, … n</c> in list order; all other earned keys receive
    /// <c>DisplayOrder = 0</c> (hidden from profile and UserCards).
    /// </summary>
    /// <exception cref="BadgeValidationException">
    /// Thrown if any key in <paramref name="orderedVisibleKeys"/> has not been earned by this user
    /// (a business-rule rejection → 400, distinct from the unauthenticated-caller
    /// <see cref="InvalidOperationException"/> → 401).
    /// </exception>
    Task SetDisplayOrderAsync(int userId, IReadOnlyList<string> orderedVisibleKeys);
}
