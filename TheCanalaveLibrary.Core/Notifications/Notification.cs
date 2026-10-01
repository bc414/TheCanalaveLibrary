namespace TheCanalaveLibrary.Core;

public partial class Notification
{
    public long NotificationId { get; set; }

    public int RecipientUserId { get; set; }

    public NotificationTypeEnum NotificationTypeId { get; set; }

    /// <summary>
    /// The actor whose action caused this notification. <c>null</c> means one of two things that
    /// share this column: the actor's account was deleted (SET NULL), or the type has no actor at all
    /// — system-sourced, self-caused, or deliberately de-identified (the moderation band 70–82 and
    /// <see cref="NotificationTypeEnum.TagUpdateSuggestion"/>, owner rulings D4/D5). Tell them apart by
    /// <see cref="NotificationTypeId"/> at display time, never by this column.
    /// </summary>
    public int? SourceUserId { get; set; }

    /// <summary>
    /// The single most specific entity of the event (owner ruling D16) — interpreted per
    /// <see cref="NotificationTypeId"/>, never a second id column. <c>0</c> is the "no related
    /// entity" sentinel (0 is never a valid id in this schema). No FK. <c>long</c> because report and
    /// comment ids are <c>bigint</c> (widened WU-InertFeatures, 2026-09-30). See
    /// <c>layer2-services.md</c> §"Polymorphic RelatedEntityId".
    /// </summary>
    public long RelatedEntityId { get; set; }

    public bool IsRead { get; set; }

    public DateTime DateCreated { get; set; }

    public virtual NotificationType NotificationType { get; set; } = null!;

    public virtual User RecipientUser { get; set; } = null!;

    public virtual User? SourceUser { get; set; }
}
