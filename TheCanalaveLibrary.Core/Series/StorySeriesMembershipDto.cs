namespace TheCanalaveLibrary.Core;

/// <summary>
/// One "this story is part of series X" box for the story page (<see cref="StorySeriesMembershipDto"/>
/// per series a story belongs to — a story may be in more than one series, WU41 settled decision).
/// <see cref="Position"/> and <see cref="Count"/> (and the Prev/Next pair) are computed only over
/// members that survive the viewer's <c>ContentRating</c>/<c>IsTakenDown</c> read filters — never a
/// raw <c>SeriesEntry</c> count — so "Part 2 of 3" always matches what the viewer can actually reach
/// and Prev/Next never link to a story hidden from them. See <c>ServerSeriesReadService
/// .GetMembershipsForStoryAsync</c> and the Feature 9 WU41 settled note in <c>audit/Stories.md</c>.
/// </summary>
public record StorySeriesMembershipDto(
    int SeriesId,
    string SeriesName,
    int Position,
    int Count,
    int? PrevStoryId,
    string? PrevStoryTitle,
    int? NextStoryId,
    string? NextStoryTitle);
