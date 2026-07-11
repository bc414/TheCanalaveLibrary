namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read-side projection of one <see cref="SiteDailyStat"/> row (Feature 62). Mirrors the entity's
/// stock-vs-flow split — see layer8-data-marts.md §"site_daily_stats" for what each column means
/// and its known approximations (<see cref="ChaptersRead"/>, <see cref="ActiveUsers"/>).
/// </summary>
public record SiteDailyStatDto(
    DateOnly StatDate,
    int TotalUsers,
    int TotalStories,
    long TotalWords,
    int? NewUsers,
    int NewStories,
    int NewChapters,
    long NewWords,
    int NewComments,
    int NewBlogPosts,
    int NewGroups,
    int NewFollows,
    int NewRecommendationsWritten,
    int NewRecommendationSuccesses,
    int ReportsFiled,
    int ReportsResolved,
    int FavoritesAdded,
    int ChaptersRead,
    long StoryViews,
    int? ActiveUsers);
