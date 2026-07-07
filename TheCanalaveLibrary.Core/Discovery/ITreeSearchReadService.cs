namespace TheCanalaveLibrary.Core;

/// <summary>
/// Automatic Tree Search (Feature 59): live recursive-CTE traversal over the precomputed
/// <c>user_story_tree_search_entries</c> edge-list mart (rebuilt daily by the L8 worker —
/// `layer8-data-marts.md`). Rating (SFW/M) and the active viewer's interaction exclusions are
/// applied at the presentation join AFTER traversal — a mature story may act as a silent bridge
/// node without ever being shown. Traversal returns story IDs only and never reveals edge-owner
/// identity.
/// </summary>
public interface ITreeSearchReadService
{
    /// <summary>
    /// Runs one traversal. Throws <see cref="ArgumentException"/> when the request is malformed:
    /// no root / both roots, empty edge set, non-positive <c>MaxDegrees</c>, or
    /// <c>IncludePaths</c> with an edge set not ⊆ {HiddenGem, AuthorSpotlight}.
    /// </summary>
    Task<TreeSearchResultDto> TraverseAsync(TreeSearchRequest request, CancellationToken ct = default);
}
