namespace TheCanalaveLibrary.Core;

/// <summary>
/// Display DTO for a single notification item. Returned by
/// <see cref="INotificationReadService.GetNotificationsAsync"/>.
///
/// <para><b>Collapsed</b> is the effective user preference — the per-user
/// <c>UserNotificationSetting.Collapsed</c> override when a row exists, otherwise
/// <c>NotificationType.DefaultCollapsed</c>. WU33 uses it to collapse/expand notification
/// groups in the grouped-by-category view.</para>
///
/// <para><b>SourceUserId / SourceUserName</b> are nullable — a notification whose source
/// user was deleted carries <c>null</c> (SET NULL policy, not RESTRICT). The display layer
/// falls back to "Someone" / "A user" when both are null.</para>
///
/// <para><b>TargetTitle / TargetUrl</b> are nullable — resolved by a two-pass batch
/// enrichment in <c>GetNotificationsAsync</c> (see <c>layer2-services.md</c> §"Polymorphic
/// RelatedEntityId — Two-Pass Batch Enrichment"). Types with no navigable target produce
/// null for both fields; the UI renders the message as plain text in that case.</para>
///
/// <para><b>WU33 additive fields</b>: <c>SourceUserName</c>, <c>TargetTitle</c>, and
/// <c>TargetUrl</c> are new nullable members appended after the original positional
/// parameters. Callers using named arguments or object initialisers are unaffected.</para>
/// </summary>
public record NotificationDto(
    long NotificationId,
    NotificationTypeEnum NotificationTypeId,
    NotificationCategoryEnum CategoryId,
    int? SourceUserId,
    /// <summary>
    /// Actor's display name. Null when the source user was deleted or the type has no actor.
    /// </summary>
    string? SourceUserName,
    /// <summary>
    /// Resolved title of the polymorphic related entity (story title, chapter title, group name, …).
    /// Null for types with no navigable target (e.g. site announcements, account warnings).
    /// </summary>
    string? TargetTitle,
    /// <summary>
    /// Resolved deep-link URL for the polymorphic related entity. Null when <c>TargetTitle</c>
    /// is null (always null together). The UI renders <c>&lt;a href="@n.TargetUrl"&gt;@n.TargetTitle&lt;/a&gt;</c>
    /// when non-null, or plain text otherwise.
    /// </summary>
    string? TargetUrl,
    int RelatedEntityId,
    bool IsRead,
    DateTime DateCreated,
    /// <summary>Effective collapsed preference (user override or type default).</summary>
    bool Collapsed
);
