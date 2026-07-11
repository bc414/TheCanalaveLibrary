namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Series service contract (Feature 9, WU41). Series are author-defined canonical
/// reading orders over the author's own stories. No visibility filter of its own — a series row is
/// visible to everyone (its member stories are individually subject to the usual
/// <c>ContentRating</c>/<c>IsTakenDown</c> filters when hydrated for display).
/// </summary>
public interface ISeriesReadService
{
    /// <summary>
    /// Returns the full detail DTO for a single series, or <c>null</c> when it doesn't exist.
    /// <see cref="SeriesDetailDto.OrderedStoryIds"/> is the raw membership set in
    /// <c>OrderIndex</c> order — hydrate via <see cref="IStoryReadService.GetListingsByIdsAsync"/>.
    /// </summary>
    Task<SeriesDetailDto?> GetSeriesByIdAsync(int seriesId);

    /// <summary>
    /// Returns every series owned by <paramref name="authorId"/>, newest-first. Used by the "My
    /// Series" owner list (<c>/series</c>) and the profile Series tab.
    /// </summary>
    Task<IReadOnlyList<SeriesListingDto>> GetSeriesByAuthorAsync(int authorId);

    /// <summary>
    /// Returns one <see cref="StorySeriesMembershipDto"/> per series <paramref name="storyId"/>
    /// belongs to (a story may be in more than one series) — for the "Part of series X" box(es) on
    /// the story page. Position/Count/Prev/Next are computed over viewer-visible members only; see
    /// <see cref="StorySeriesMembershipDto"/>. Empty list when the story is in no series.
    /// </summary>
    Task<IReadOnlyList<StorySeriesMembershipDto>> GetMembershipsForStoryAsync(int storyId);
}
