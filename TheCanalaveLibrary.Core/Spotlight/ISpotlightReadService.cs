namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Community Spotlight cluster (Feature 55, WU-Spotlight — requirements record:
/// <c>audit/Spotlight.md</c>; conventions: <c>layer2-services.md</c> §"Community Spotlight").
/// Composes <c>IStoryReadService</c>/<c>IRecommendationReadService</c> for presentation
/// projections; the viewer's <c>ContentRating</c>/<c>IsTakenDown</c> filters shape every read.
/// </summary>
public interface ISpotlightReadService
{
    /// <summary>
    /// The placements live right now (<c>StartDate &lt;= now &lt; EndDate</c>), for the homepage
    /// section, ordered by start. A placement whose story the viewer can't see (rating ceiling,
    /// takedown) drops out entirely; an invisible attached recommendation just nulls back to the
    /// blank-rec display state.
    /// </summary>
    Task<IReadOnlyList<SpotlightDisplayDto>> GetActiveSpotlightsAsync();

    /// <summary>The active user's redeemable (<c>Available</c>) slots, oldest grant first.
    /// Anonymous → empty.</summary>
    Task<IReadOnlyList<SpotlightSlotDto>> GetMyAvailableSlotsAsync();

    /// <summary>The active user's placements that are still upcoming or live. Anonymous → empty.</summary>
    Task<IReadOnlyList<SpotlightBookingDto>> GetMyBookingsAsync();

    /// <summary>
    /// The active user's own approved recommendations (of currently-visible stories) as the
    /// primary story-pick path — picking one selects its story and pre-attaches the
    /// recommendation. Anonymous → empty.
    /// </summary>
    Task<IReadOnlyList<SpotlightPickCandidateDto>> GetMyPickCandidatesAsync();

    /// <summary>
    /// The bookable calendar: every block from the current one through the booking horizon
    /// (both <c>site_settings</c>), each with its booked count vs. capacity.
    /// </summary>
    Task<IReadOnlyList<SpotlightBlockDto>> GetBlockAvailabilityAsync();
}
