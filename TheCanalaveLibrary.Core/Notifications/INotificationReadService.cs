namespace TheCanalaveLibrary.Core;

/// <summary>
/// Read side of the Notifications feature cluster (Features 42, 43).
/// All methods are self-scoped: they operate on the currently authenticated user's
/// notifications and settings, sourced from <c>IActiveUserContext</c>. Anonymous callers
/// receive safe zero/empty responses (no exception).
///
/// <para>The notification bell in the layout injects this interface directly — a legitimate
/// cross-cutting injection per <c>cross-cutting.md</c> "Notification Creation."</para>
///
/// <para>Implemented server-side by <c>ServerNotificationReadService</c> (uses
/// <c>ReadOnlyApplicationDbContext</c>). MVP is InteractiveServer-only; a client HTTP impl
/// lives in the Post-MVP L5 batch.</para>
/// </summary>
public interface INotificationReadService
{
    /// <summary>
    /// Returns the number of unread notifications for the current user. Returns 0 for
    /// anonymous callers. Consumed by the layout bell badge.
    /// </summary>
    Task<int> GetUnreadCountAsync();

    /// <summary>
    /// Returns a page of notifications for the current user, newest first. Returns an empty
    /// array for anonymous callers.
    /// </summary>
    /// <param name="page">1-indexed page number.</param>
    /// <param name="pageSize">Number of items per page.</param>
    Task<NotificationDto[]> GetNotificationsAsync(int page, int pageSize);

    /// <summary>
    /// Returns all notification types with the current user's effective preferences
    /// (user override when a <c>UserNotificationSetting</c> row exists, otherwise
    /// <c>NotificationType</c> defaults). Ordered by category then type.
    /// Used by the Feature 43 settings page (WU33).
    /// </summary>
    Task<NotificationSettingDto[]> GetSettingsAsync();
}
