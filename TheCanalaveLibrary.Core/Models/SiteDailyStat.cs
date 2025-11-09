namespace TheCanalaveLibrary.Core.Models;

public partial class SiteDailyStat
{
    public DateOnly StatDate { get; set; }

    public int NewUsers { get; set; }

    public int TotalUsers { get; set; }

    public int NewStories { get; set; }

    public int TotalStories { get; set; }

    public long NewWords { get; set; }

    public long TotalWords { get; set; }

    public int NewRecommendationSuccesses { get; set; }

    public int NewComments { get; set; }

    public int NewFollows { get; set; }

    public int NewChapters { get; set; }

    public int NewRecommendationsWritten { get; set; }

    public long PageViews { get; set; }

    public int ActiveUsers { get; set; }
}
