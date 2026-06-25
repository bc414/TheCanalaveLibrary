namespace TheCanalaveLibrary.Core;

/// <summary>
/// Write side of the Notifications feature cluster (Features 41, 42, 43). Inherits the read
/// interface so components/services that need both read and write can inject a single service.
///
/// <para><b>Generation API — semantic methods only.</b> The only public generation surface is
/// semantic per-event methods (<c>NotifyNew*Async</c>). There is no public generic
/// <c>CreateAsync</c>. All methods funnel through one private create-core in
/// <c>ServerNotificationWriteService</c> that owns the invariants: drop-self and dedup.
/// This keeps those invariants un-bypassable per-caller — the same principle as the
/// content-rating named query filter. See <c>cross-cutting.md</c> "Notification Creation"
/// and <c>layer2-services.md</c> "Notification Generation."</para>
///
/// <para><b>Best-effort post-commit.</b> Callers invoke these after their own
/// <c>SaveChangesAsync</c>, inside a <c>try/catch</c> that logs and swallows. A notification
/// failure must never roll back the caller's primary action.</para>
///
/// <para><b>Semantic methods land incrementally.</b> WU22 delivers the two single-recipient
/// methods whose source data is already at Stage 5 (Following). Fan-out methods
/// (<c>NotifyNewChapterAsync</c>, etc.) are added co-delivered with their triggering
/// work-units.</para>
/// </summary>
public interface INotificationWriteService : INotificationReadService
{
    // ── Read-side mutations ──────────────────────────────────────────────────────

    /// <summary>
    /// Marks a single notification as read. Silently no-ops if the notification
    /// does not belong to the current user or is already read.
    /// </summary>
    Task MarkAsReadAsync(long notificationId);

    /// <summary>
    /// Marks all unread notifications belonging to the current user as read.
    /// </summary>
    Task MarkAllAsReadAsync();

    // ── Settings ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the current user's preference override for <paramref name="notifType"/>.
    ///
    /// <para><b>Sparse model:</b> when both values match the type defaults, the override row
    /// is deleted (NULL = use default). Otherwise the row is upserted.</para>
    /// </summary>
    Task SetSettingAsync(NotificationTypeEnum notifType, bool emailEnabled, bool collapsed);

    // ── Semantic generation methods (WU22 slice) ──────────────────────────────────

    /// <summary>
    /// Creates a <c>NewFollowerOnYou</c> notification for <paramref name="recipientUserId"/>.
    /// Called by <c>ServerFollowingWriteService.FollowAsync</c> after its primary commit.
    /// </summary>
    Task NotifyNewFollowerAsync(int recipientUserId, int followerUserId);

    /// <summary>
    /// Creates a <c>NewVouchOnYou</c> notification for <paramref name="recipientUserId"/>.
    /// Called by <c>ServerFollowingWriteService.VouchAsync</c> after its primary commit.
    /// </summary>
    Task NotifyNewVouchAsync(int recipientUserId, int voucherUserId);

    // ── Semantic generation methods (WU29 slice) ──────────────────────────────────

    /// <summary>
    /// Creates a <c>HiddenGem</c> notification for the story author when a recommender designates
    /// their recommendation as a Hidden Gem. Called by
    /// <c>ServerRecommendationWriteService.SetHiddenGemAsync</c> after its primary commit.
    /// </summary>
    Task NotifyStoryHiddenGemAsync(int recipientStoryAuthorId, int sourceRecommenderId);

    // ── Semantic generation methods (WU32 slice — group fan-out) ─────────────────

    /// <summary>
    /// Fan-out notification sent to all group members with <c>NotifyForNewStory = true</c>
    /// when a story is added to a group (type <c>NewGroupStory = 60</c>). Also sends
    /// <c>YourStoryAddedToGroup = 25</c> to the story author (drop-self handled by the
    /// create-core). Called by <c>ServerGroupWriteService.AddStoryAsync</c> after its primary
    /// commit, best-effort (try/catch wraps the call).
    /// </summary>
    /// <param name="groupId">The group the story was added to.</param>
    /// <param name="storyAuthorId">The author of the added story (receives <c>YourStoryAddedToGroup</c>).</param>
    /// <param name="sourceUserId">The member who performed the add (drop-self source).</param>
    Task NotifyNewGroupStoryAsync(int groupId, int storyAuthorId, int sourceUserId);

    /// <summary>
    /// Fan-out notification sent to all group members with <c>NotifyForNewBlogPost = true</c>
    /// when a group blog post is published (type <c>NewGroupBlogPost = 61</c>). Called by
    /// <c>ServerBlogPostWriteService.CreateGroupBlogPostAsync</c> after its primary commit.
    /// </summary>
    /// <param name="groupId">The group the blog post belongs to.</param>
    /// <param name="blogPostId">The new blog post's id (used as <c>RelatedEntityId</c>).</param>
    /// <param name="authorId">The author of the blog post (drop-self source).</param>
    Task NotifyNewGroupBlogPostAsync(int groupId, int blogPostId, int authorId);
}
