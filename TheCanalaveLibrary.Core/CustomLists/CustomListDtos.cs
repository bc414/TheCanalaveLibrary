namespace TheCanalaveLibrary.Core;

/// <summary>
/// One custom list as it appears in a list-of-lists surface (the owner's <c>/my-lists</c> page and
/// the profile Lists tab). <paramref name="StoryCount"/> counts only entries whose story survives
/// the viewer's read-context filters (content rating / takedown) — the same count the list's own
/// page will show that viewer, so the two surfaces never disagree.
/// </summary>
public record CustomListSummaryDto(
    int CustomListId,
    string ListName,
    bool IsPublic,
    DateTime DateCreated,
    int StoryCount);

/// <summary>
/// One custom list's header for its own page (<c>/lists/{id}</c>). Owner identity rides along so
/// the page can render the byline link and compute owner-vs-viewer affordances client-side
/// (visibility itself is enforced in the read service — a private list simply returns null to
/// non-owners). <paramref name="StoryCount"/> follows the same viewer-visible rule as
/// <see cref="CustomListSummaryDto"/>.
/// </summary>
public record CustomListDetailDto(
    int CustomListId,
    string ListName,
    bool IsPublic,
    DateTime DateCreated,
    int OwnerUserId,
    string OwnerUserName,
    int StoryCount);

/// <summary>
/// One of the active user's lists annotated with whether a given story is already in it — the shape
/// the StoryCard caret menu's "Add to list" expander renders (checkmark toggles).
/// </summary>
public record CustomListMembershipDto(
    int CustomListId,
    string ListName,
    bool ContainsStory);
