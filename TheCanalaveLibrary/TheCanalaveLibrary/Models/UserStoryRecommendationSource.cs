namespace TheCanalaveLibrary.Models;

/// <summary>
/// A sparse table that gets an entry whenever someone uses the read it later button specifically from a recommendation.
/// This allows attribution to the recommender if the user eventually finds the recommendation helpful. It has a
/// one-to-one relationship with a UserStoryInteraction using the (UserId, StoryId) primary key, but was vertically
/// partitioned out of UserStoryInteractions to reduce the row size of that table for performance.
/// </summary>
public class UserStoryRecommendationSource
{
    public int UserId { get; set; }
    public int StoryId { get; set; }
    public int SourceRecommendationId { get; set; }

    public virtual UserStoryInteraction UserStoryInteraction { get; set; } = null!;
    public virtual Recommendation SourceRecommendation { get; set; } = null!;
}