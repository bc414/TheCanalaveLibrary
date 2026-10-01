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
/// <para><b>SourceUserId / SourceUserName</b> are nullable, and two different nulls share them
/// (owner ruling D4): the source user was deleted (SET NULL policy, not RESTRICT), or the type has
/// no actor at all — system-sourced, self-caused, or deliberately de-identified (the moderation band
/// 70–82 and tag-adoption 26, D5). Disambiguate <b>by <see cref="NotificationTypeId"/></b>, never by
/// these columns: the presenter composes actor-free text for actor-free types and falls back to
/// "Someone" only for types that genuinely had an actor.</para>
///
/// <para><b>TargetTitle / TargetUrl / TargetContextTitle</b> are nullable — resolved by a two-pass
/// batch enrichment in <c>GetNotificationsAsync</c> (see <c>layer2-services.md</c> §"Polymorphic
/// RelatedEntityId — Two-Pass Batch Enrichment"). Types with no navigable target produce null for
/// all three; the UI renders the message as plain text in that case.</para>
///
/// <para><b>Additive fields</b>: <c>SourceUserName</c>, <c>TargetTitle</c>, and <c>TargetUrl</c>
/// (WU33) sit among the original positional parameters; <c>TargetContextTitle</c>
/// (WU-InertFeatures, 2026-09-30) is an optional trailing parameter, so existing constructions keep
/// compiling.</para>
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
    /// <summary>
    /// The single most specific entity of the event, interpreted per type; <c>0</c> = none
    /// (owner rulings D4/D16). <c>long</c> since WU-InertFeatures (report ids are <c>bigint</c>).
    /// </summary>
    long RelatedEntityId,
    bool IsRead,
    DateTime DateCreated,
    /// <summary>Effective collapsed preference (user override or type default).</summary>
    bool Collapsed,
    /// <summary>
    /// A second name the related entity yields for free when resolved — the story title for a
    /// <c>GroupStory</c> anchor (types 60/25, whose <see cref="TargetTitle"/> is the group name) or
    /// for a <c>Chapter</c> anchor (type 10, whose <see cref="TargetTitle"/> is the chapter title).
    /// Null for every other kind.
    /// </summary>
    string? TargetContextTitle = null
);
