namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read interface for the badge feature (Feature 50, WU36).
/// Returns earned badge data for the curation settings form.
/// </summary>
public interface IBadgeReadService
{
    /// <summary>
    /// Returns ALL badges earned by <paramref name="userId"/> — including hidden ones
    /// (<see cref="EarnedBadgeDto.DisplayOrder"/> == 0). Visible badges come first, ordered by
    /// <see cref="EarnedBadgeDto.DisplayOrder"/> ascending; hidden badges follow, ordered by
    /// <see cref="EarnedBadgeDto.SortOrder"/> ascending.
    /// </summary>
    Task<IReadOnlyList<EarnedBadgeDto>> GetMyBadgesForCurationAsync(int userId);
}
