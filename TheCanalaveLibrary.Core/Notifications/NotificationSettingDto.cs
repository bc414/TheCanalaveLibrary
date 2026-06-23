namespace TheCanalaveLibrary.Core;

/// <summary>
/// Display DTO for a single notification type's user-preference setting. Returned by
/// <see cref="INotificationReadService.GetSettingsAsync"/>.
///
/// <para><b>EmailEnabled</b> and <b>Collapsed</b> are effective values — the per-user
/// <c>UserNotificationSetting</c> override when a row exists, otherwise the type defaults
/// (<c>NotificationType.DefaultEmailEnabled</c> / <c>DefaultCollapsed</c>). The sparse-override
/// model stores only rows where the user differs from the default.</para>
///
/// <para><b>IsDefault</b> is <c>true</c> when no override row exists (both values are type
/// defaults). WU33 uses this to distinguish "explicitly set to default" from "never customized"
/// — though behaviourally they are identical.</para>
/// </summary>
public record NotificationSettingDto(
    NotificationTypeEnum TypeId,
    NotificationCategoryEnum CategoryId,
    string DisplayName,
    string Description,
    /// <summary>Effective email preference (user override or type default).</summary>
    bool EmailEnabled,
    /// <summary>Effective collapsed preference (user override or type default).</summary>
    bool Collapsed,
    /// <summary><c>true</c> when no <c>UserNotificationSetting</c> override row exists.</summary>
    bool IsDefault
);
