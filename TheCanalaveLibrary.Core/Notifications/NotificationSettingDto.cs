namespace TheCanalaveLibrary.Core;

/// <summary>
/// Display DTO for a single notification type's user-preference setting. Returned by
/// <see cref="INotificationReadService.GetSettingsAsync"/>.
///
/// <para><b>EmailEnabled</b> and <b>Collapsed</b> are effective values, resolved <b>per field</b>
/// (owner ruling D19): the user's <c>UserNotificationSetting</c> column when it is non-NULL,
/// otherwise the type default (<c>NotificationType.DefaultEmailEnabled</c> /
/// <c>DefaultCollapsed</c>). The sparse-override model stores a field only where the user differs
/// from its default.</para>
///
/// <para><b>EmailIsDefault</b> / <b>CollapsedIsDefault</b> are <c>true</c> when that field follows
/// the type default (no row, or a NULL column). Since D19 "explicitly set to the default" is stored
/// exactly like "never customized", so the two really are behaviourally identical: a later change to
/// the default reaches both. Nothing renders the flags yet (no "following the site default" control
/// was built — D19's accepted cost). <c>NotificationSettingsPage</c>'s optimistic <c>with { … }</c>
/// update leaves them stale until the next load, which is harmless while they are unrendered.</para>
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
    /// <summary><c>true</c> when <see cref="EmailEnabled"/> follows the type default.</summary>
    bool EmailIsDefault,
    /// <summary><c>true</c> when <see cref="Collapsed"/> follows the type default.</summary>
    bool CollapsedIsDefault
);
