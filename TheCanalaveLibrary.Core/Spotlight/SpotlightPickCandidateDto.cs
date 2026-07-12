namespace TheCanalaveLibrary.Core;

/// <summary>
/// One of the active user's own approved recommendations, offered as the <b>primary</b>
/// story-pick path on the redemption page ("spotlight a story you've recommended") — picking one
/// selects its story and pre-attaches the recommendation. The secondary path is the
/// <c>StoryTitlePicker</c> title search.
/// </summary>
public record SpotlightPickCandidateDto(
    int RecommendationId,
    int StoryId,
    string StoryTitle,
    string? AuthorName,
    bool IsHiddenGem,
    DateTime DatePosted);
