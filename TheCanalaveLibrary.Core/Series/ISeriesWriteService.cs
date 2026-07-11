namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Series service contract (Feature 9, WU41). Every method requires an
/// authenticated user; membership mutations additionally require
/// <c>Series.AuthorId == Story.AuthorId == ActiveUser.UserId</c> — a series holds only the owner's
/// own stories (WU41 settled decision; see <c>audit/Stories.md</c> Feature 9).
/// </summary>
public interface ISeriesWriteService : ISeriesReadService
{
    /// <summary>Creates a new series owned by the current user. Throws <see cref="SeriesValidationException"/>
    /// on invalid input or a duplicate name for this author.</summary>
    Task<int> CreateSeriesAsync(CreateSeriesDto dto);

    /// <summary>Renames/redescribes a series. Owner-only — throws <see cref="UnauthorizedAccessException"/>
    /// otherwise.</summary>
    Task UpdateSeriesAsync(UpdateSeriesDto dto);

    /// <summary>Deletes a series (cascade clears its <c>SeriesEntry</c> rows; member stories are
    /// untouched). Owner-only.</summary>
    Task DeleteSeriesAsync(int seriesId);

    /// <summary>
    /// Appends <paramref name="storyId"/> to the end of the series (next <c>OrderIndex</c>).
    /// Idempotent — a no-op if already a member. Throws <see cref="UnauthorizedAccessException"/>
    /// unless the caller owns both the series and the story.
    /// </summary>
    Task AddStoryAsync(int seriesId, int storyId);

    /// <summary>Removes a story from a series. Owner-only. Idempotent — a no-op if not a member.</summary>
    Task RemoveStoryAsync(int seriesId, int storyId);

    /// <summary>
    /// Rewrites <c>OrderIndex</c> for every entry in the series to match the order of
    /// <paramref name="orderedStoryIds"/>. Owner-only. Throws <see cref="SeriesValidationException"/>
    /// if the given id set doesn't exactly match the series' current membership.
    /// </summary>
    Task ReorderAsync(int seriesId, IReadOnlyList<int> orderedStoryIds);
}
