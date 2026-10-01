namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Recommendations service contract (Features 27–30).
/// No-tracking projections; per-viewer fields (<c>IsLikedByCurrentUser</c>,
/// <c>IsOwnRecommendation</c>) computed via filtered Include on the read side.
/// </summary>
public interface IRecommendationReadService
{
    /// <summary>
    /// Returns the recommendations for a story the current viewer may see, ordered: spotlighted
    /// (<c>IsHighlightedByAuthor</c>) first, then by <c>DatePosted</c> descending.
    /// Per-viewer <c>IsLikedByCurrentUser</c> and <c>IsOwnRecommendation</c> are included.
    /// <para><b>Per-viewer visibility (WU-RecLifecycle):</b> everyone sees <c>Approved</c>; the
    /// story's author additionally sees <c>NeedsRevision</c>/<c>Rejected</c> (to act on them); a
    /// recommender additionally sees their own hidden recommendation (with the author's
    /// <c>RevisionRequestNote</c>). <c>Status</c>/<c>RevisionRequestNote</c> are populated only on
    /// those elevated rows — public viewers only ever receive Approved rows.</para>
    /// </summary>
    Task<List<RecommendationDto>> GetForStoryAsync(int storyId);

    /// <summary>
    /// Returns the active user's own non-Approved recommendations (<c>NeedsRevision</c> with the
    /// author's note, and <c>Rejected</c>), newest first — the Bookshelves Recommendations tab's
    /// "Needs attention" section (WU-RecLifecycle). Anonymous → empty.
    /// </summary>
    Task<List<RecommendationDto>> GetMyRecommendationsNeedingAttentionAsync();

    /// <summary>Returns a single recommendation by id, or null if not found or not Approved.</summary>
    Task<RecommendationDto?> GetByIdAsync(int recommendationId);

    /// <summary>
    /// Returns the IDs of stories the active user has written an approved recommendation for.
    /// Anonymous → empty. Used by the Recommendations bookshelf tab ("My Recommendations").
    /// </summary>
    Task<IReadOnlyList<int>> GetRecommendedStoryIdsAsync();

    /// <summary>
    /// Returns the IDs of stories the active user has written an approved Hidden Gem
    /// recommendation for. Anonymous → empty. Used by the Hidden Gems bookshelf tab.
    /// </summary>
    Task<IReadOnlyList<int>> GetHiddenGemStoryIdsAsync();

    /// <summary>
    /// The "was this recommendation helpful?" prompt for <paramref name="storyId"/> (spec §5.6, owner
    /// ruling D3): the recommendation the viewer's attribution names, as a full DTO, because the widget
    /// shows it as a reminder — the Read It Later may have been long ago. Read by the reading page at
    /// the Ch.1 ≥90% moment. Returns the recommendation only when all four gates hold, else null:
    /// <list type="number">
    ///   <item>the viewer has an attribution (<see cref="UserStoryRecommendationSource"/>) for the story;</item>
    ///   <item>the recommendation is visible — <c>Approved</c>, not taken down, and its story passes the
    ///   viewer's story-visibility guard;</item>
    ///   <item><c>RecommenderId</c> is non-null (anonymous or since-deleted recommenders get no prompt —
    ///   gated here, at read time, so a deletion between the save and the read is covered);</item>
    ///   <item>no <see cref="RecommendationSuccess"/> exists yet for (viewer, recommendation).</item>
    /// </list>
    /// Anonymous → null.
    /// </summary>
    Task<RecommendationDto?> GetHelpfulPromptAsync(int storyId);

    /// <summary>
    /// Returns the IDs of stories that <paramref name="userId"/> has written an Approved
    /// recommendation for. Used by the profile page's Recommendations tab as the candidate ID
    /// set, passed to <see cref="IStoryReadService.GetListingsAsync"/> with
    /// <c>restrictToStoryIds</c>. Public — any viewer may see another user's recommendations.
    /// </summary>
    Task<IReadOnlyList<int>> GetRecommendedStoryIdsByUserAsync(int userId);
}
