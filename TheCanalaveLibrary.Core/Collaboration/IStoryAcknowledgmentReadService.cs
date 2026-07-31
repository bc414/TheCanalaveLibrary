namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Story Acknowledgments service contract (WU-StatBadgeProducers). A
/// <see cref="StoryAcknowledgment"/> is an author's credit to another registered user for helping
/// with a story — consent-gated, the credited user must accept before it is public or counts toward
/// <c>UserStat.AcknowledgedAsBetaReaderCount</c>.
/// </summary>
public interface IStoryAcknowledgmentReadService
{
    /// <summary>
    /// Returns every <see cref="StoryAcknowledgmentStatus.Accepted"/> credit for
    /// <paramref name="storyId"/>, for the public story-page display. Empty when the story has no
    /// accepted credits (including when it has only Pending/Declined ones).
    /// </summary>
    Task<IReadOnlyList<StoryAcknowledgmentDto>> GetAcknowledgmentsForStoryAsync(int storyId);

    /// <summary>
    /// Aggregated data for the current user's owner-wide management page
    /// (<c>/acknowledgments</c>): every credit the caller has authored (any status) + every Pending
    /// credit naming the caller as the recipient. Requires an authenticated caller.
    /// </summary>
    Task<StoryAcknowledgmentManageDto> GetManageDataForUserAsync();

    /// <summary>The seeded role lookup (Beta Reader / Planner / Cover Artist / Editor), ordered by
    /// id — feeds the role <c>&lt;select&gt;</c> on the credit-request form.</summary>
    Task<IReadOnlyList<AcknowledgmentRoleDto>> GetAcknowledgmentRolesAsync();
}
