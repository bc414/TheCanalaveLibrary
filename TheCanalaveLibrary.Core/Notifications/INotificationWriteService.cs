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
}
