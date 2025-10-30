using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class NotificationType
{
    public byte NotificationTypeId { get; set; }

    public string NotificationKey { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public string Description { get; set; } = null!;

    public bool DefaultEmailEnabled { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<UserNotificationSetting> UserNotificationSettings { get; set; } = new List<UserNotificationSetting>();
}
