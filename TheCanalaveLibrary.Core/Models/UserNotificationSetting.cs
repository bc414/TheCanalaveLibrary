namespace TheCanalaveLibrary.Core.Models;

public partial class UserNotificationSetting
{
    public int UserId { get; set; }

    public NotificationTypeEnum NotificationTypeId { get; set; }

    public bool EmailEnabled { get; set; }
    public bool Collapsed { get; set; }

    public virtual NotificationType NotificationType { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
