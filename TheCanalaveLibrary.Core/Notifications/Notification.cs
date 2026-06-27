namespace TheCanalaveLibrary.Core;

public partial class Notification
{
    public long NotificationId { get; set; }

    public int RecipientUserId { get; set; }

    public NotificationTypeEnum NotificationTypeId { get; set; }

    public int? SourceUserId { get; set; }

    public int RelatedEntityId { get; set; }

    public bool IsRead { get; set; }

    public DateTime DateCreated { get; set; }

    public virtual NotificationType NotificationType { get; set; } = null!;

    public virtual User RecipientUser { get; set; } = null!;

    public virtual User? SourceUser { get; set; }
}
