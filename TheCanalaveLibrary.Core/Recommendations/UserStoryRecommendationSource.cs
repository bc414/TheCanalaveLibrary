namespace TheCanalaveLibrary.Core;

/// <summary>
/// Sparse 1-to-1 partition off <see cref="UserStoryInteraction"/> recording <b>how the viewer's
/// <c>IsReadItLater</c> bit came to be set</b>: from a recommendation's card, or by following a
/// recommendation's direct "Read now" link to 90% of Chapter 1 (owner ruling D3 — spec §5.6). It is
/// what makes the "was this recommendation helpful?" prompt appear, and what
/// <c>RecordSuccessAsync</c> requires and consumes. Horizontally partitioned to keep
/// <see cref="UserStoryInteraction"/> rows lean. Rules: <c>layer2-services.md</c> §"Attribution
/// (Feature 30)".
/// </summary>
public class UserStoryRecommendationSource
{
    public int UserId { get; set; }
    public int StoryId { get; set; }
    public int SourceRecommendationId { get; set; }

    public UserStoryInteraction UserStoryInteraction { get; set; } = null!;
    public Recommendation SourceRecommendation { get; set; } = null!;
}
