namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Story Lineage service contract (Feature 10, WU42 — formerly "Story
/// Relationships," renamed to disambiguate from <c>StoryCharacterPairing</c> and
/// <c>UserStoryInteraction</c>). A <see cref="StoryLineage"/> is a one-way link declaring that one
/// story is a Sequel/Prequel/Inspired-By/Companion-Piece of another (spec §939). Cross-author links
/// require the target author's approval before <see cref="GetLineageForStoryAsync"/> surfaces them.
/// </summary>
public interface IStoryLineageReadService
{
    /// <summary>
    /// Returns every <see cref="StoryLineageStatus.Approved"/> link where <paramref name="storyId"/>
    /// is the source, for the public story-page display. Empty when the story has no approved
    /// outgoing links (including when it has only Pending/Rejected ones).
    /// </summary>
    Task<IReadOnlyList<StoryLineageDto>> GetLineageForStoryAsync(int storyId);

    /// <summary>
    /// Aggregated data for the current user's owner-wide management page (<c>/story-lineages</c>):
    /// every outgoing link across all their stories (any status) + every incoming Pending request
    /// targeting any of their stories. Requires an authenticated caller.
    /// </summary>
    Task<StoryLineageManageDto> GetManageDataForUserAsync();

    /// <summary>
    /// The seeded lineage type lookup (Inspired By / Prequel / Sequel / Companion Piece), ordered by
    /// id — feeds the type <c>&lt;select&gt;</c> on the lineage-request form.
    /// </summary>
    Task<IReadOnlyList<StoryLineageTypeDto>> GetLineageTypesAsync();
}
