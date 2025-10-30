using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class UserSearchSetting
{
    public int UserSearchSettingId { get; set; }

    public int UserId { get; set; }

    public string SearchModeKey { get; set; } = null!;

    public string InteractionFilterKey { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public string? Value { get; set; }

    public virtual UserInteractionFilter InteractionFilterKeyNavigation { get; set; } = null!;

    public virtual SearchMode SearchModeKeyNavigation { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
