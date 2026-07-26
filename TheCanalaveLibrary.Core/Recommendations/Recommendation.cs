using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Core;

public class Recommendation : IModeratableContent
{
    // IModeratableContent — AuthorUserId maps to the recommendation's RecommenderId FK
    int? IModeratableContent.AuthorUserId => RecommenderId;

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

    // Soft-delete (IsTakenDown named filter) — WU34; renamed from IsHidden/DateModeratedRemoved/ModerationRemovalReason in pre-integration cleanup
    public bool IsTakenDown { get; set; }
    public DateTime? TakedownDate { get; set; }
    [MaxLength(1024)]
    public string? TakedownReason { get; set; }

    // WU-RecLifecycle: the story author's Request-Revision note (plain text, Blazor-escaped at
    // render). Same shape/placement rationale as TakedownReason — sparse, nullable, lifecycle-
    // paired free text; non-null only while StatusId == NeedsRevision. Cleared on return to
    // Approved (recommender edit) and on Remove.
    [MaxLength(500)]
    public string? RevisionRequestNote { get; set; }

    public RecommendationDetail RecommendationDetail { get; set; } = null!;
    public ICollection<RecommendationLike> Likes { get; set; } = [];
    public ICollection<RecommendationSuccess> RecommendationSuccesses { get; set; } = [];

    public User? Recommender { get; set; }
    public RecommendationStatus Status { get; set; } = null!;
    public Story Story { get; set; } = null!;
    public ICollection<UserStoryInteraction> UserStoryInteractions { get; set; } = [];
}
