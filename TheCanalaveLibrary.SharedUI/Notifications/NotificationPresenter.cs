using TheCanalaveLibrary.Core;

namespace TheCanalaveLibrary.SharedUI;

/// <summary>
/// Composes per-notification display data (message text, icon, accent color) from a
/// <see cref="NotificationDto"/>. Static presenter — no service injection, no instance state.
/// Mirrors the role of <see cref="UserStoryInteractionVisuals"/> (static enum-to-display-data
/// map), extended with a per-instance composition step because notifications carry actor and
/// target data from the DTO that enum lookup alone cannot provide.
///
/// <para><b>Message composition:</b> the actor falls back to "Someone" when
/// <see cref="NotificationDto.SourceUserName"/> is null (source deleted via SET NULL, or type
/// has no actor). Target entity name is embedded into the message text; navigation to
/// <see cref="NotificationDto.TargetUrl"/> is handled by the caller (e.g. via
/// <c>OnActivate</c> on <see cref="NotificationItem"/>), not by this class.</para>
///
/// <para><b>Icon + accent per type:</b> defaults to the row's
/// <see cref="NotificationCategoryVisuals"/> entry; a small set of per-type overrides reuse
/// other existing constants (<c>HiddenGem</c> → <see cref="RecommendationIcons.HiddenGemIconPath"/>)
/// as the single source of truth — same reuse discipline as
/// <see cref="BookshelfTabVisuals"/>.</para>
///
/// <para><b>Forward-compat stubs:</b> types whose generating write-path is not yet implemented
/// produce no DB rows but are covered by switch arms here for completeness. Default arm catches
/// any future type added to <see cref="NotificationTypeEnum"/> before this switch is updated.</para>
/// </summary>
public static class NotificationPresenter
{
    /// <summary>
    /// Composes display data for a single notification.
    /// </summary>
    /// <param name="n">The notification DTO from the read service (enriched with batch fields).</param>
    /// <returns>
    /// <c>Text</c> — human-readable message with actor and target names embedded as plain text.
    /// <c>IconPath</c> — SVG path data for the notification's icon (24×24 viewBox, nonzero fill).
    /// <c>AccentColor</c> — hex accent color for the icon and any unread indicator.
    /// </returns>
    public static (string Text, string IconPath, string AccentColor) Compose(NotificationDto n)
    {
        string actor = n.SourceUserName ?? "Someone";
        string? target = n.TargetTitle;
        NotificationCategoryVisuals.Info cat = NotificationCategoryVisuals.For(n.CategoryId);

        string text = n.NotificationTypeId switch
        {
            // ── YourFollows category ──────────────────────────────────────────────
            NotificationTypeEnum.NewFollowerOnYou =>
                $"{actor} is now following you",

            NotificationTypeEnum.NewVouchOnYou =>
                $"{actor} vouched for you",

            // ── YourStories category ──────────────────────────────────────────────
            NotificationTypeEnum.NewStoryFavorite =>
                target is not null ? $"{actor} favorited {target}" : $"{actor} favorited your story",

            NotificationTypeEnum.NewStoryFollower =>
                target is not null ? $"{actor} started following {target}" : $"{actor} started following your story",

            NotificationTypeEnum.NewRecommendationOnYourStory =>
                target is not null ? $"{actor} wrote a recommendation for {target}" : $"{actor} wrote a recommendation for your story",

            NotificationTypeEnum.HiddenGem =>
                target is not null ? $"{actor} marked a recommendation as a Hidden Gem" : $"{actor} recognised a recommendation as a Hidden Gem",

            NotificationTypeEnum.NewStoryComment =>
                target is not null ? $"{actor} commented on {target}" : $"{actor} commented on your story",

            NotificationTypeEnum.YourStoryAddedToGroup =>
                target is not null ? $"Your story was added to {target}" : "Your story was added to a group",

            NotificationTypeEnum.TagUpdateSuggestion =>
                target is not null ? $"{actor} suggested tag updates for {target}" : $"{actor} suggested tag updates for your story",

            NotificationTypeEnum.StoryRejected =>
                target is not null ? $"{target} was not approved for the library" : "Your story was not approved for the library",

            NotificationTypeEnum.NewStoryAcknowledgement =>
                target is not null ? $"{actor} acknowledged {target}" : $"{actor} acknowledged your story",

            // ── YourProfile category ──────────────────────────────────────────────
            NotificationTypeEnum.NewCommentOnYourProfile =>
                $"{actor} commented on your profile",

            NotificationTypeEnum.CommentReply =>
                $"{actor} replied to your comment",

            // ── YourRecommendations category ──────────────────────────────────────
            NotificationTypeEnum.RecommendationApproved =>
                target is not null ? $"Your recommendation for {target} was approved" : "Your recommendation was approved",

            NotificationTypeEnum.RecommendationHighlighted =>
                target is not null ? $"The author of {target} highlighted your recommendation" : "An author highlighted your recommendation",

            NotificationTypeEnum.SuccessfulRec =>
                target is not null ? $"Your recommendation for {target} helped a reader discover it" : "Your recommendation helped a reader discover a story",

            NotificationTypeEnum.NewRecommendationByFollowedUser =>
                target is not null ? $"{actor} recommended {target}" : $"{actor} wrote a recommendation",

            // ── Collaborations category ───────────────────────────────────────────
            NotificationTypeEnum.StoryRelationshipRequested =>
                target is not null ? $"{actor} requested a story relationship with {target}" : $"{actor} requested a story relationship",

            NotificationTypeEnum.StoryRelationshipApproved =>
                target is not null ? $"Your story relationship for {target} was approved" : "Your story relationship was approved",

            // ── Groups category ───────────────────────────────────────────────────
            NotificationTypeEnum.NewGroupStory =>
                target is not null ? $"A new story was added to {target}" : "A new story was added to a group you follow",

            NotificationTypeEnum.NewGroupBlogPost =>
                target is not null ? $"New blog post in a group you follow: {target}" : "A new blog post was added to a group you follow",

            // ── YourFollows — followed-content fan-out types ──────────────────────
            NotificationTypeEnum.NewChapterOnFollowedStory =>
                target is not null ? $"New chapter: {target}" : "A story you follow has a new chapter",

            NotificationTypeEnum.NewStoryByFollowedUser =>
                target is not null ? $"{actor} published {target}" : $"{actor} published a new story",

            NotificationTypeEnum.NewBlogPostByFollowedUser =>
                target is not null ? $"{actor} posted a blog entry: {target}" : $"{actor} posted a new blog entry",

            NotificationTypeEnum.NewBlogPostOnFollowedStory =>
                target is not null ? $"New blog post on {target}" : "A story you follow has a new blog post",

            NotificationTypeEnum.NewBlogPostOnFavoritedStory =>
                target is not null ? $"New blog post on {target}" : "A story you favorited has a new blog post",

            NotificationTypeEnum.NewBlogPostOnReadItLaterStory =>
                target is not null ? $"New blog post on {target}" : "A story in your Read It Later list has a new blog post",

            // ── Warnings category ─────────────────────────────────────────────────
            NotificationTypeEnum.ContentRemoved =>
                "Content was removed from your account",

            NotificationTypeEnum.AccountWarning =>
                "Your account has received a warning",

            NotificationTypeEnum.AccountSuspended =>
                "Your account has been suspended",

            NotificationTypeEnum.AccountBanned =>
                "Your account has been banned",

            // ── YourReports category ──────────────────────────────────────────────
            NotificationTypeEnum.ReportReceived =>
                "A new report has been filed",

            NotificationTypeEnum.ReportResolved =>
                "A report you filed has been resolved",

            NotificationTypeEnum.ReportResolvedNoAction =>
                "A report you filed was reviewed; no action was taken",

            // ── SiteNews category ─────────────────────────────────────────────────
            NotificationTypeEnum.SiteAnnouncement =>
                "A new site announcement has been posted",

            // ── Forward-compat catch-all ──────────────────────────────────────────
            _ => "You have a new notification"
        };

        // Per-type icon/color overrides — reuse existing constants as single source of truth.
        // HiddenGem gets the gem icon rather than the YourStories default.
        var (iconPath, accentColor) = n.NotificationTypeId switch
        {
            NotificationTypeEnum.HiddenGem =>
                (RecommendationIcons.HiddenGemIconPath, RecommendationIcons.HiddenGemAccentColor),
            _ =>
                (cat.IconPath, cat.AccentColor)
        };

        return (text, iconPath, accentColor);
    }
}
