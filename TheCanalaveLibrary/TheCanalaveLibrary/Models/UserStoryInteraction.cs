using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class UserStoryInteraction
{
    public int UserId { get; set; }

    public int StoryId { get; set; }

    public byte ReadStatus { get; set; }

    public bool IsActivelyReading { get; set; }

    public byte FavoriteStatus { get; set; }

    public bool IsFollowed { get; set; }

    public bool IsReadItLater { get; set; }

    public bool IsIgnored { get; set; }

    public DateTime? FavoriteDate { get; set; }

    public DateTime? FollowedDate { get; set; }

    public DateTime? ReadItLaterDate { get; set; }

    public DateTime? IgnoredDate { get; set; }

    public int? SourceRecommendationId { get; set; }

    public virtual Recommendation? SourceRecommendation { get; set; }

    public virtual Story Story { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
