namespace TheCanalaveLibrary.Core;

/// <summary>
/// Full display DTO for the series page (<c>/series/{SeriesId}/{*Slug}</c>).
/// <see cref="AuthorName"/> is nullable (author may be deleted — SET NULL on User delete, same as
/// <see cref="StoryDetailsDTO.AuthorName"/>). <see cref="OrderedStoryIds"/> is the raw (unfiltered)
/// membership set in <c>OrderIndex</c> order — the page hydrates it via
/// <see cref="IStoryReadService.GetListingsByIdsAsync"/>, which reorders to match and silently drops
/// ids the viewer can't see (content-rating/takedown filters). See Feature 9 WU41 settled note in
/// <c>audit/Stories.md</c> for why Count-ish fields here are raw while
/// <see cref="StorySeriesMembershipDto"/>'s Position/Count are viewer-filtered.
/// </summary>
public record SeriesDetailDto(
    int SeriesId,
    string Name,
    string? Description,
    int? AuthorId,
    string? AuthorName,
    DateTime DateCreated,
    IReadOnlyList<int> OrderedStoryIds);
