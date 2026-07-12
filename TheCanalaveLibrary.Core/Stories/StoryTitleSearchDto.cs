namespace TheCanalaveLibrary.Core;

/// <summary>
/// One row of an incremental title-search result — the reusable typeahead picker's contract
/// (<see cref="IStoryReadService.SearchStoriesByTitleAsync"/>, minted for the Story Lineage target
/// picker, WU42, and also used to retrofit Groups' add-story entry). Deliberately lightweight (no
/// cover art, tags, etc.) — this is a picker row, not a listing card. Subject to the read context's
/// <c>ContentRating</c>/<c>IsTakenDown</c> filters, same as any other read.
/// </summary>
public record StoryTitleSearchDto(
    int StoryId,
    string Title,
    string? AuthorName);
