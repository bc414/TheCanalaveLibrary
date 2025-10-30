using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class Recommendation
{
    public int RecommendationId { get; set; }

    public int StoryId { get; set; }

    public int? RecommenderId { get; set; }

    public string Text { get; set; } = null!;

    public byte StatusId { get; set; }

    public bool IsHiddenGem { get; set; }

    public bool IsHighlightedByAuthor { get; set; }

    public int LikeCount { get; set; }

    public DateTime DatePosted { get; set; }

    public int ActiveReportCount { get; set; }

    public virtual ICollection<RecommendationSuccess> RecommendationSuccesses { get; set; } = new List<RecommendationSuccess>();

    public virtual User? Recommender { get; set; }

    public virtual RecommendationStatus Status { get; set; } = null!;

    public virtual Story Story { get; set; } = null!;

    public virtual ICollection<UserStoryInteraction> UserStoryInteractions { get; set; } = new List<UserStoryInteraction>();
}
