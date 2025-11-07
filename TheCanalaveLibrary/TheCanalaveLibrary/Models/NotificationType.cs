using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Models;

public partial class NotificationType
{
    [Key]
    public NotificationTypeEnum NotificationTypeId { get; set; }

    [Required]
    [MaxLength(128)]
    public string NotificationKey { get; set; } = null!;

    [MaxLength(128)]
    public string DisplayName { get; set; } = null!;

    [MaxLength(512)]
    public string Description { get; set; } = null!;

    public bool DefaultEmailEnabled { get; set; }
    public bool DefaultCollapsed { get; set; }
    
    public NotificationCategoryEnum NotificationCategory { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<UserNotificationSetting> UserNotificationSettings { get; set; } = new List<UserNotificationSetting>();
}
