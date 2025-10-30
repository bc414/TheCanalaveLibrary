using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class Badge
{
    public string BadgeKey { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string IconUrl { get; set; } = null!;

    public int SortOrder { get; set; }

    public virtual ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
}
