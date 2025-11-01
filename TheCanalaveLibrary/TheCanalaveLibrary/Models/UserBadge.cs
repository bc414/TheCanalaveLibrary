using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TheCanalaveLibrary.Models;

public partial class UserBadge
{
    public int UserId { get; set; }

    [Required]
    [MaxLength(128)]
    public string BadgeKey { get; set; } = null!;

    public DateTime DateEarned { get; set; }

    public int DisplayOrder { get; set; }

    public virtual Badge BadgeKeyNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
