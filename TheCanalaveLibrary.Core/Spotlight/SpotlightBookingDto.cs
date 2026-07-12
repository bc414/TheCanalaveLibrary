namespace TheCanalaveLibrary.Core;

/// <summary>One of the active user's upcoming/active placements (redemption page, "your
/// bookings" list).</summary>
public record SpotlightBookingDto(
    int SpotlightId,
    int StoryId,
    string StoryTitle,
    bool HasRecommendation,
    DateTime StartDate,
    DateTime EndDate);
