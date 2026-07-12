namespace TheCanalaveLibrary.Core;

/// <summary>
/// One active homepage spotlight placement, composed for display: the story's standard listing
/// card plus the optional endorsing recommendation (null = the blank-rec display state — either
/// none was attached, or the attached one is no longer visible to this viewer). Produced by
/// <see cref="ISpotlightReadService.GetActiveSpotlightsAsync"/> by composing
/// <c>IStoryReadService.GetListingsByIdsAsync</c> and <c>IRecommendationReadService</c> — the
/// spotlight service owns no story/rec projection of its own.
/// </summary>
public record SpotlightDisplayDto(
    int SpotlightId,
    DateTime StartDate,
    DateTime EndDate,
    StoryListingDto Story,
    RecommendationDto? Recommendation);
