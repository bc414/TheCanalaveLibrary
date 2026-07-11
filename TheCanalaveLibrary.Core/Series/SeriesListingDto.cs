namespace TheCanalaveLibrary.Core;

/// <summary>
/// Lightweight DTO for rendering a <c>SeriesCard</c> on the "My Series" owner list (<c>/series</c>)
/// and the profile Series tab. <see cref="StoryCount"/> is the raw (unfiltered) member count —
/// mirrors <see cref="GroupCardDto.MemberCount"/>'s precedent of "count is a cheap raw number, the
/// hydrated deck is the authority and silently drops what the viewer can't see" (see Feature 9 WU41
/// settled note in <c>audit/Stories.md</c>).
/// </summary>
public record SeriesListingDto(
    int SeriesId,
    string Name,
    string? Description,
    int StoryCount,
    int? AuthorId,
    string? AuthorName,
    DateTime DateCreated);
