namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Story Arcs service contract (Feature 8, WU45). Arcs are author-defined
/// contiguous chapter-number ranges; gaps between arcs are legal (those chapters belong to no arc).
/// </summary>
public interface IStoryArcReadService
{
    /// <summary>
    /// Returns every arc of a story in reading order (by <c>StartChapterNumber</c> — the list
    /// index + 1 is the "Arc X" ordinal). Empty list when the story has no arcs (the ~95% case)
    /// or does not exist.
    /// </summary>
    Task<IReadOnlyList<StoryArcDto>> GetArcsForStoryAsync(int storyId);
}
