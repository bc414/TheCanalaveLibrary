namespace TheCanalaveLibrary.Core;

/// <summary>
/// Sparse 1-to-1 partition off UserStoryInteraction. Created when a user opens a story from a recommendation,
/// enabling attribution if they later confirm the recommendation was helpful (§5.6). Horizontally partitioned
/// to keep UserStoryInteraction rows lean.
/// </summary>
public class UserStoryRecommendationSource
{
    public int UserId { get; set; }
    public int StoryId { get; set; }
    public int SourceRecommendationId { get; set; }

    public UserStoryInteraction UserStoryInteraction { get; set; } = null!;
    public Recommendation SourceRecommendation { get; set; } = null!;
}
