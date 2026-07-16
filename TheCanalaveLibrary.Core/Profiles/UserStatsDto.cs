namespace TheCanalaveLibrary.Core;

/// <summary>
/// Denormalized counter snapshot from <see cref="UserStat"/>, projected for display in the
/// profile banner's <c>UserStatsBlock</c>. Counters for unbuilt features are omitted
/// (ViewsOnStories WU38, SpotlightCount post-MVP). <c>ActiveReportCount</c> was dropped from
/// <see cref="UserStat"/> entirely (WU-UserStatRecalc) — it was an orphaned duplicate never
/// populated; live report counts live on each target entity's own column (see
/// <c>ServerModerationWriteService.AdjustActiveReportCountAsync</c>).
/// The service only populates this when the profile owner's
/// <c>PrivacySettings.ShowUserStats</c> is <c>true</c> or the viewer is the owner;
/// the banner receives <c>null</c> when stats are hidden.
/// </summary>
public record UserStatsDto(
    int StoriesWritten,
    long WordsWritten,
    int FollowerCount,
    int AuthorsFollowed,
    int CommentsWritten,
    int RecommendationsWritten,
    int RecommendationsReceived,
    int BlogPostsWritten,
    int GroupsJoined,
    int FavoritesOnStories,
    int StoriesRead,
    int StoriesInProgress,
    int StoriesIgnored);
