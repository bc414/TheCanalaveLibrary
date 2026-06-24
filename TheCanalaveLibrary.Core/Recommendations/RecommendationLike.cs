namespace TheCanalaveLibrary.Core;

/// <summary>Pure junction — records a reader's like on a recommendation. No DateLiked; no notification (anti-addictive, §6.11).</summary>
public class RecommendationLike
{
    public int UserId { get; set; }
    public int RecommendationId { get; set; }

    public User User { get; set; } = null!;
    public Recommendation Recommendation { get; set; } = null!;
}
