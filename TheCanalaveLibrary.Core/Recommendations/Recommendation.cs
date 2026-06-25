using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

public class Recommendation
{
    public int RecommendationId { get; set; }
    public int StoryId { get; set; }
    public int? RecommenderId { get; set; }
    public short StatusId { get; set; }
    public bool IsHiddenGem { get; set; }
    public bool IsHighlightedByAuthor { get; set; }
    public int SuccessfulRecCount { get; set; }
    public int LikeCount { get; set; }
    public DateTime DatePosted { get; set; }
    public int ActiveReportCount { get; set; }

    // Soft-delete (ModeratedVisibility named filter) — WU34
    public bool IsHidden { get; set; }
    public DateTime? DateModeratedRemoved { get; set; }
    [MaxLength(1024)]
    public string? ModerationRemovalReason { get; set; }

    public RecommendationDetail RecommendationDetail { get; set; } = null!;
    public ICollection<RecommendationLike> Likes { get; set; } = [];
    public ICollection<RecommendationSuccess> RecommendationSuccesses { get; set; } = [];

    public User? Recommender { get; set; }
    public RecommendationStatus Status { get; set; } = null!;
    public Story Story { get; set; } = null!;
    public ICollection<UserStoryInteraction> UserStoryInteractions { get; set; } = [];
}
