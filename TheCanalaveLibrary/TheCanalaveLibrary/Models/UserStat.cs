using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class UserStat
{
    public int UserId { get; set; }

    public int StoriesRead { get; set; }

    public int StoriesInProgress { get; set; }

    public int StoriesIgnored { get; set; }

    public int ChaptersRead { get; set; }

    public int WordsRead { get; set; }

    public int RecommendationsFoundUseful { get; set; }

    public int StoriesWritten { get; set; }

    public long WordsWritten { get; set; }

    public int CommentsWritten { get; set; }

    public int RecommendationsWritten { get; set; }

    public int BlogPostsWritten { get; set; }

    public int AcknowledgedAsBetaReaderCount { get; set; }

    public int AcknowledgedAsInspirationCount { get; set; }

    public int FeatureContributions { get; set; }

    public int FollowerCount { get; set; }

    public int AuthorsFollowed { get; set; }

    public int FavoritesOnStories { get; set; }

    public long ViewsOnStories { get; set; }

    public int GroupsJoined { get; set; }

    public int RecommendationsReceived { get; set; }

    public int SpotlightCount { get; set; }

    public int ActiveReportCount { get; set; }

    public virtual User User { get; set; } = null!;
}
