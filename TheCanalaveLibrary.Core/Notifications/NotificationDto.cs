namespace TheCanalaveLibrary.Core;

/// <summary>
/// Display DTO for a single notification item. Returned by
/// <see cref="INotificationReadService.GetNotificationsAsync"/>.
///
/// <para><b>Collapsed</b> is the effective user preference — the per-user
/// <c>UserNotificationSetting.Collapsed</c> override when a row exists, otherwise
/// <c>NotificationType.DefaultCollapsed</c>. WU33 uses it to collapse/expand notification
/// groups in the panel.</para>
///
/// <para><b>SourceUserId</b> is nullable — a notification whose source user was deleted
/// carries <c>null</c> (SET NULL policy, not RESTRICT). The display layer should handle
/// this gracefully (e.g. "Deleted user").</para>
/// </summary>
public record NotificationDto(
    long NotificationId,
    NotificationTypeEnum NotificationTypeId,
    NotificationCategoryEnum CategoryId,
    int? SourceUserId,
    int RelatedEntityId,
    bool IsRead,
    DateTime DateCreated,
    /// <summary>Effective collapsed preference (user override or type default).</summary>
    bool Collapsed
);
