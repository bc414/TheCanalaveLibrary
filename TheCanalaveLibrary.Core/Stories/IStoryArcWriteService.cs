namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Story Arcs contract (Feature 8, WU45). All methods are author-gated: the
/// caller must be the story's author. Range rules (Start ≥ 1, Start ≤ End, no overlap with any
/// other arc of the same story, unique title per story) are enforced here — deliberately
/// service-layer business logic, not DB constraints (WU45 settled; see audit/Stories.md F8).
/// Chapter reorder/delete adjusts arc bounds in <c>IChapterWriteService</c>, not here.
/// </summary>
public interface IStoryArcWriteService : IStoryArcReadService
{
    /// <returns>The new <c>StoryArc.StoryArcId</c>.</returns>
    /// <exception cref="StoryArcValidationException">Range/title validation failed.</exception>
    /// <exception cref="KeyNotFoundException">Story not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the story's author.</exception>
    Task<int> CreateArcAsync(CreateStoryArcDto dto);

    /// <exception cref="StoryArcValidationException">Range/title validation failed.</exception>
    /// <exception cref="KeyNotFoundException">Arc not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the story's author.</exception>
    Task UpdateArcAsync(UpdateStoryArcDto dto);

    /// <summary>Deletes an arc. The chapters it covered simply become arc-less (gap) chapters.</summary>
    /// <exception cref="KeyNotFoundException">Arc not found.</exception>
    /// <exception cref="UnauthorizedAccessException">Caller is not the story's author.</exception>
    Task DeleteArcAsync(int storyArcId);
}
