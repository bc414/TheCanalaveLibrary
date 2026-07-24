namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Custom Lists service contract (Feature 51, WU-CustomLists — settled design:
/// <c>audit/CustomLists.md</c> §"Settled design"). Custom lists are named, shareable story
/// collections: private lists are owner-only; public lists are viewable by anyone (including
/// anonymous viewers — same posture as public profile tabs) and cloneable by authenticated users.
/// Story visibility inside a list always rides the read context's content-rating/takedown filters —
/// a viewer never sees (or counts) entries they couldn't open.
/// </summary>
public interface ICustomListReadService
{
    /// <summary>
    /// Every list owned by the active user, newest-created first. Anonymous callers get an empty
    /// list — lists require an account. Backs the <c>/my-lists</c> page.
    /// </summary>
    Task<List<CustomListSummaryDto>> GetMyListsAsync();

    /// <summary>
    /// One list's header for its page (<c>/lists/{id}</c>), or <c>null</c> when it doesn't exist,
    /// or exists but is private and not owned by the active user (missing and not-visible are
    /// deliberately indistinguishable).
    /// </summary>
    Task<CustomListDetailDto?> GetListDetailAsync(int listId);

    /// <summary>
    /// The viewer-visible story ids of one list, ordered per <paramref name="sort"/> (title sorts
    /// join the story row; date sorts use the entry's <c>DateAdded</c>, ties broken by story id).
    /// Empty when the list isn't visible to the active user (same gate as
    /// <see cref="GetListDetailAsync"/>). Callers page over the returned ids and hydrate via
    /// <c>IStoryReadService.GetListingsByIdsAsync</c> (which preserves input order).
    /// </summary>
    Task<IReadOnlyList<int>> GetListStoryIdsAsync(int listId, CustomListSortEnum sort);

    /// <summary>
    /// Mature count-line disclosure for a public list viewed below the M ceiling (WU-AccessGate):
    /// interstitial-grade metadata (title/author/rating) for entries the viewer's rating filter
    /// hid. Empty for mature-on viewers and for the owner (whose reads are Personal-plane
    /// unfiltered). Same list-visibility gate as the other reads.
    /// </summary>
    Task<IReadOnlyList<GatedMetadataDto>> GetListHiddenMatureAsync(int listId);

    /// <summary>
    /// Every <c>IsPublic</c> list owned by <paramref name="userId"/>, newest-created first. Backs
    /// the profile Lists tab (<c>ProfileTab.Lists</c>) — public to any viewer, mirroring
    /// <c>ISavedTagSelectionReadService.GetPublicSelectionsByUserAsync</c>.
    /// </summary>
    Task<List<CustomListSummaryDto>> GetPublicListsByUserAsync(int userId);

    /// <summary>
    /// The active user's lists annotated with whether <paramref name="storyId"/> is already in each
    /// — the StoryCard caret menu's "Add to list" expander shape, ordered by list name. Anonymous
    /// callers get an empty list.
    /// </summary>
    Task<List<CustomListMembershipDto>> GetMyListMembershipsAsync(int storyId);
}
