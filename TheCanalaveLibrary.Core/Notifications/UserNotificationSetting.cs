namespace TheCanalaveLibrary.Core;

/// <summary>
/// A user's sparse override of one notification type's two settable fields.
///
/// <para><b>Sparse per field (owner ruling D19).</b> Each column is NULL when the user follows the
/// type's default for that field, and the effective value is resolved per field: the column when
/// non-NULL, otherwise <see cref="NotificationType.DefaultEmailEnabled"/> /
/// <see cref="NotificationType.DefaultCollapsed"/>. The row exists only while at least one column is
/// non-NULL. The write rule lives in one place, <c>NotificationSettingUpsert</c> (Server); see
/// <c>layer2-services.md</c> §"Notification Generation" → "Filtering semantics".</para>
/// </summary>
public partial class UserNotificationSetting
{
    public int UserId { get; set; }

    public NotificationTypeEnum NotificationTypeId { get; set; }

    /// <summary>Email override; NULL = follow the type's <c>DefaultEmailEnabled</c>.</summary>
    public bool? EmailEnabled { get; set; }

    /// <summary>Panel-display override; NULL = follow the type's <c>DefaultCollapsed</c>.</summary>
    public bool? Collapsed { get; set; }

    public virtual NotificationType NotificationType { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
