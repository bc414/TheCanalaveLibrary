namespace TheCanalaveLibrary.Core;

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

    /// <summary>
    /// How many <see cref="StoryAcknowledgment"/> credits (role Beta Reader) this user has
    /// <see cref="StoryAcknowledgmentStatus.Accepted"/>. Consent-gated — an author's credit alone
    /// does not count until the credited user accepts (WU-StatBadgeProducers). Drives
    /// <see cref="SiteBadges.BetaReader"/>, auto-awarded at ≥1.
    /// </summary>
    public int AcknowledgedAsBetaReaderCount { get; set; }

    /// <summary>
    /// How many <see cref="StoryLineage"/> "Inspired By" links (type id 1) approved by this user
    /// (as the inspiring TARGET story's author) exist, i.e. how many other stories this user's work
    /// has inspired (WU-StatBadgeProducers). Not sourced from <see cref="StoryAcknowledgment"/> —
    /// role 5 "Inspiration" was retired in favor of this already-built, already consent-gated
    /// mechanism. No badge consumer yet.
    /// </summary>
    public int AcknowledgedAsInspirationCount { get; set; }

    public int FollowerCount { get; set; }

    public int AuthorsFollowed { get; set; }

    public int FavoritesOnStories { get; set; }

    public long ViewsOnStories { get; set; }

    public int GroupsJoined { get; set; }

    public int RecommendationsReceived { get; set; }

    /// <summary>
    /// Author-side Tastemaker aggregate: how many readers followed one of this user's recommendations
    /// to a story and clicked "this recommendation was helpful" (distinct RecommendationSuccess rows,
    /// anti-self-farm). Incremented in <see cref="ServerRecommendationWriteService.RecordSuccessAsync"/>.
    /// Drives <see cref="SiteBadges.Recommender"/>, auto-awarded at ≥1 and displaying this count as
    /// <c>UserBadge.EarnedCount</c> (no-tiers model, WU-StatBadgeProducers — supersedes the retired
    /// Bronze/Silver split). Do NOT confuse with <see cref="RecommendationsFoundUseful"/> (reader-side,
    /// a different concept).
    /// </summary>
    public int RecommendationSuccessesEarned { get; set; }

    public int SpotlightCount { get; set; }

    public virtual User User { get; set; } = null!;
}
