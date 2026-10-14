namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Notifications feature cluster (Features 42, 43).
/// All methods are self-scoped: they operate on the currently authenticated user's
/// notifications and settings, sourced from <c>IActiveUserContext</c>. Anonymous callers
/// receive safe zero/empty responses (no exception).
///
/// <para>The notification bell in the layout injects this interface directly — a legitimate
/// cross-cutting injection per <c>render-and-layout.md</c>'s "Notification bell" paragraph.</para>
///
/// <para>Implemented server-side by <c>ServerNotificationReadService</c> (uses
/// <c>ReadOnlyApplicationDbContext</c>) and, on WASM, by <c>ClientNotificationReadService</c> over
/// HTTP (global InteractiveAuto since the Global Flip, 2026-07-13).</para>
/// </summary>
public interface INotificationReadService
{
    /// <summary>
    /// Returns the number of unread notifications for the current user. Returns 0 for
    /// anonymous callers. Consumed by the layout bell badge.
    /// </summary>
    Task<int> GetUnreadCountAsync();

    /// <summary>
    /// Returns the total number of notifications for the current user (read + unread).
    /// Used by <c>PaginationControls</c> on the <c>/notifications</c> page. Returns 0
    /// for anonymous callers. Added additively in WU33 — existing callers unaffected.
    /// </summary>
    Task<int> GetTotalCountAsync();

    /// <summary>
    /// Returns a page of notifications for the current user. Returns an empty array for
    /// anonymous callers.
    /// </summary>
    /// <param name="page">1-indexed page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    /// <param name="order">
    /// Feed ordering. Defaults to <see cref="NotificationFeedOrder.NewestFirst"/> —
    /// existing callers that omit this parameter are unaffected (WU33 additive).
    /// </param>
    Task<NotificationDto[]> GetNotificationsAsync(
        int page,
        int pageSize,
        NotificationFeedOrder order = NotificationFeedOrder.NewestFirst);

    /// <summary>
    /// Returns all notification types with the current user's effective preferences, resolved
    /// per field (owner ruling D19): the <c>UserNotificationSetting</c> column when it is non-NULL,
    /// otherwise that field's <c>NotificationType</c> default. Ordered by category then type.
    /// Used by the Feature 43 settings page (WU33).
    /// </summary>
    Task<NotificationSettingDto[]> GetSettingsAsync();
}
