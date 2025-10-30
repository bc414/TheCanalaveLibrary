using System;
using System.Collections.Generic;

namespace TheCanalaveLibrary.Models;

public partial class DefaultSearchSetting
{
    public string SearchModeKey { get; set; } = null!;

    public string InteractionFilterKey { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public string? DefaultValue { get; set; }

    public virtual UserInteractionFilter InteractionFilterKeyNavigation { get; set; } = null!;

    public virtual SearchMode SearchModeKeyNavigation { get; set; } = null!;
}
