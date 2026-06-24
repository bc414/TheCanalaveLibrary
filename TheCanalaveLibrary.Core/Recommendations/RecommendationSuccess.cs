namespace TheCanalaveLibrary.Core;

/// <summary>Created when a user confirms a recommendation was helpful (after finishing Chapter 1 via WU26 trigger).</summary>
public class RecommendationSuccess
{
    public int UserId { get; set; }
    public int RecommendationId { get; set; }
    public DateTime DateRecorded { get; set; }

    public Recommendation Recommendation { get; set; } = null!;
    public User User { get; set; } = null!;
}
